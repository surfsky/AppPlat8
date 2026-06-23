using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using App.Components;
using System.Text.RegularExpressions;

namespace App.Pages.GIS
{
    //[CheckPower(Power.GisGeometryView)]  // 暂时先关闭权限校验
    public class IndexModel : BaseModel
    {
        public void OnGet()
        {
        }

        public JsonResult OnGetMenuData()
        {
            var menus = GisMenu.GetTree();
            return BuildResult(0, "success", menus);
        }

        public JsonResult OnPostRefreshMenuData([FromBody] RefreshMenuDataReq req)
        {
            var menuId = req?.MenuId;
            var (okCount, failCount) = GisApi.RefreshStats(menuId);
            var menuFixCnt = GisMenu.FixAll();

            return BuildResult(0, "刷新完成", new
            {
                okCount,
                failCount,
                menuFixCnt,
                menuId,
                refreshDt = DateTime.Now,
            });
        }

        public JsonResult OnGetLayerData()
        {
            var tagLookup = CheckObjectTag.IncludeSet
                .Select(t => new
                {
                    t.CheckObjectId,
                    TagName = t.Tag != null ? t.Tag.Name : null
                })
                .ToList()
                .Where(t => !string.IsNullOrWhiteSpace(t.TagName))
                .GroupBy(t => t.CheckObjectId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.TagName).Distinct().ToList());

            var objects = CheckObject.Set
                .Where(o => !string.IsNullOrWhiteSpace(o.Gps))
                .Select(o => new
                {
                    o.Id,
                    o.Name,
                    o.Gps,
                    o.Address,
                    o.SocialCreditCode
                })
                .ToList();

            var list = new List<object>();
            foreach (var item in objects)
            {
                if (!TryParseGps(item.Gps, out var lng, out var lat))
                    continue;

                tagLookup.TryGetValue(item.Id, out var tags);
                list.Add(new
                {
                    id = item.Id,
                    name = item.Name,
                    lat,
                    lng,
                    address = item.Address,
                    socialCreditCode = item.SocialCreditCode,
                    tags = tags ?? new List<string>()
                });
            }

            return new JsonResult(new { code = 0, data = list });
        }

        public JsonResult OnGetGeometryLayerData(long? menuId)
        {
            var query = GisGeometry.Search(menuId: menuId, isValid: true);  // 当前菜单下的有效点位（不递归）
            var list = query
                .OrderBy(g => g.SortId)
                .ThenBy(g => g.Id)
                //.SortPageExport(new Paging { PageSize = int.MaxValue })  // 导出版本，查询时就排序分页
                .Select(g => new
                {
                    id = g.Id,
                    type = g.Type,
                    menuId = g.MenuId,
                    name = g.Name,
                    alias = g.Alias,
                    sortId = g.SortId,
                    addr = g.Addr,
                    gps = g.Gps,
                    region = g.Region,
                    url = g.Url,
                    file = g.File,
                    geoJson = g.GeoJson,
                    dataJson = g.DataJson,
                    icon = g.Menu != null ? g.Menu.Icon : null
                })
                .ToList();

            return new JsonResult(new { code = 0, data = list });
        }

        public JsonResult OnGetGeometryDetail(long id)
        {
            var item = GisGeometry.DataSet
                .Include(g => g.Menu)
                .FirstOrDefault(g => g.Id == id);
            if (item == null)
                return BuildResult(404, "图形不存在或无权访问");

            var canEdit = Auth.CheckPower(HttpContext, Power.GisGeometryEdit) && GisGeometry.Get(id) != null;
            var atts = BuildGeometryAtts(item.UniId);

            return BuildResult(0, "success", new
            {
                id = item.Id,
                type = item.Type,
                menuId = item.MenuId,
                menuName = item.MenuName,
                name = item.Name,
                alias = item.Alias,
                addr = item.Addr,
                gps = item.Gps,
                region = item.Region,
                url = item.Url,
                file = item.File,
                geoJson = item.GeoJson,
                dataJson = item.DataJson,
                dataRows = ParseDataRows(item.DataJson),
                atts,
                canEdit
            });
        }

        public JsonResult OnGetPanelData(string theme = "dark", long? sceneId = null)
        {
            var q = GisPanel.Set.AsNoTracking().Where(t => t.InGis);
            
            if (sceneId.HasValue && sceneId.Value > 0)
            {
                var panelIds = GisScenePanel.Set.AsNoTracking()
                    .Where(t => t.SceneId == sceneId.Value)
                    .Select(t => t.PanelId)
                    .ToList();
                q = q.Where(t => panelIds.Contains(t.Id));
            }

            var list = q
                .OrderBy(t => t.Position)
                .ThenBy(t => t.Id)
                .Select(t => new
                {
                    id = t.Id,
                    title = t.Title,
                    info = t.Info,
                    position = t.Position,
                    content = t.Content,
                    chartJson = t.ChartJson,
                    inGis = t.InGis,
                    inDashboard = t.InDashboard,
                    theme = string.IsNullOrWhiteSpace(theme) ? "dark" : theme.Trim().ToLower(),
                })
                .ToList();

            return BuildResult(0, "success", list);
        }

        private static List<object> ParseDataRows(string dataJson)
        {
            var rows = new List<object>();
            if (string.IsNullOrWhiteSpace(dataJson))
                return rows;

            try
            {
                using var doc = JsonDocument.Parse(dataJson);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in root.EnumerateObject())
                        rows.Add(new { key = p.Name, value = FormatElement(p.Value) });
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    var i = 0;
                    foreach (var item in root.EnumerateArray())
                    {
                        rows.Add(new { key = $"[{i}]", value = FormatElement(item) });
                        i++;
                    }
                }
                else
                {
                    rows.Add(new { key = "value", value = FormatElement(root) });
                }
            }
            catch
            {
                rows.Add(new { key = "raw", value = dataJson });
            }

            return rows;
        }

        public class RefreshMenuDataReq
        {
            public long? MenuId { get; set; }
        }

        private static string FormatElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Null => string.Empty,
                JsonValueKind.Undefined => string.Empty,
                _ => JsonSerializer.Serialize(element)
            };
        }

        private static List<object> BuildGeometryAtts(string uniId)
        {
            if (string.IsNullOrWhiteSpace(uniId))
                return new List<object>();

            var items = Att.Set
                .Where(t => t.Key == uniId)
                .OrderBy(t => t.SortId)
                .ThenBy(t => t.Id)
                .ToList();

            return items.Select(t => (object)new
            {
                id = t.Id,
                fileName = string.IsNullOrWhiteSpace(t.FileName) ? Path.GetFileName(t.Url ?? string.Empty) : t.FileName,
                url = t.Url,
                type = t.Type,
                fileSizeText = t.FileSizeText,
                ext = (t.FileExtension ?? string.Empty).Trim().TrimStart('.').ToLower(),
                previewUrl = $"/Shared/FileViewer?uniId={Uri.EscapeDataString(uniId)}&id={t.Id}",
                downloadUrl = $"/Shared/Atts?handler=Download&uniId={Uri.EscapeDataString(uniId)}&id={t.Id}"
            }).ToList();
        }

        private static bool TryParseGps(string gps, out double lng, out double lat)
        {
            lng = 0;
            lat = 0;
            if (string.IsNullOrWhiteSpace(gps))
                return false;

            var text = gps.Replace("，", ",").Replace("；", ",").Replace(";", ",").Trim();
            text = Regex.Replace(text, "\\s+", ",");
            var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
                return false;

            if (!double.TryParse(parts[0], out lng))
                return false;
            if (!double.TryParse(parts[1], out lat))
                return false;

            return true;
        }
    }
}
