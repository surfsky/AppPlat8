using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using App.API;
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
    //[Auth(Power.GisGeometryView)]  // 暂时先关闭权限校验
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
            var (okCount, failCount) = RefreshMenuStats(menuId);
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

        public JsonResult OnGetGeometryLayerData(long? menuId, bool? isVisible = null)
        {
            var menus = GetTargetMenus(menuId);
            var list = new List<object>();
            foreach (var menu in menus)
            {
                try
                {
                    var items = Gis.GetMenuGeometryItems(menu.Id, isVisible: isVisible);
                    list.AddRange(items.Select(BuildGeometryRow));
                }
                catch
                {
                    // 单个菜单失败不影响整页其它菜单数据
                }
            }

            return new JsonResult(new { code = 0, data = list.OrderBy(t => GetSortId(t)).ThenBy(t => GetId(t)).ToList() });
        }

        public JsonResult OnGetMenuItems(long menuId, string keyword = null, bool? isVisible = null, int pageIndex = 0, int pageSize = 20)
        {
            if (pageSize <= 0) pageSize = 20;
            if (pageSize > 200) pageSize = 200;
            if (pageIndex < 0) pageIndex = 0;

            var menu = GisMenu.Get(menuId);
            if (menu == null)
                return BuildResult(404, "菜单不存在");

            var pi = new Paging { PageIndex = pageIndex, PageSize = pageSize };
            var list = Gis.GetMenuGeometryItems(menuId, pi, keyword, isVisible)
                .Select(BuildGeometryRow)
                .ToList();
            var mapIds = new List<long>();

            if (menu.DataFrom == GisDataFrom.API)
            {
                var total = menu.DataCnt ?? list.Count;
                pi.SetTotal(total);
            }
            else
            {
                var query = GisGeometry.Search(menuId: menuId, recursive: true, isVisible: isVisible);
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    var kw = keyword.Trim();
                    query = query.Where(t => (t.Name ?? "").Contains(kw) || (t.Alias ?? "").Contains(kw) || (t.Addr ?? "").Contains(kw));
                }
                mapIds = query
                    .OrderBy(t => t.SortId)
                    .ThenBy(t => t.Id)
                    .Select(t => t.Id)
                    .ToList();
                pi.SetTotal(query.Count());
            }

            return BuildResult(0, "success", new { items = list, pageInfo = pi, mapIds });
        }

        public JsonResult OnGetGeometryDetail(long id)
        {
            if (id < 0)
            {
                var item = GetApiGeometryDetail(id);
                if (item == null)
                    return BuildResult(404, "点位不存在或数据已变化");

                return BuildResult(0, "success", new
                {
                    id = item.Id,
                    rawId = item.RawId,
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
                    atts = new List<object>(),
                    canEdit = false,
                    isVisible = item.IsVisible,
                    scale = item.Scale,
                    labelColor = item.LabelColor,
                    dataFrom = item.DataFrom
                });
            }

            var geo = GisGeometry.DataSet
                .Include(g => g.Menu)
                .FirstOrDefault(g => g.Id == id);
            if (geo == null)
                return BuildResult(404, "图形不存在或无权访问");

            var canEdit = Auth.CheckPower(HttpContext, Power.GisGeometryEdit) && GisGeometry.Get(id) != null;
            var atts = BuildGeometryAtts(geo.UniId);

            return BuildResult(0, "success", new
            {
                id = geo.Id,
                type = geo.Type,
                menuId = geo.MenuId,
                menuName = geo.MenuName,
                name = geo.Name,
                alias = geo.Alias,
                addr = geo.Addr,
                gps = geo.Gps,
                region = geo.Region,
                url = geo.Url,
                file = geo.File,
                geoJson = geo.GeoJson,
                dataJson = geo.DataJson,
                dataRows = ParseDataRows(geo.DataJson),
                atts,
                canEdit,
                isVisible = geo.IsVisible,
                scale = geo.Scale,
                labelColor = geo.LabelColor,
                dataFrom = GisDataFrom.Geometry
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

            foreach (var candidate in EnumerateJsonCandidates(dataJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(candidate);
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

                    if (rows.Count > 0)
                        return rows;
                }
                catch
                {
                    // ignore and continue trying normalized candidates
                }
            }

            rows.Add(new { key = "原文", value = dataJson.Trim() });
            return rows;
        }

        static List<GisMenu> GetTargetMenus(long? menuId)
        {
            if (menuId.HasValue && menuId.Value > 0)
            {
                var menu = GisMenu.Get(menuId.Value);
                return menu == null ? new List<GisMenu>() : new List<GisMenu> { menu };
            }

            var all = GisMenu.Set.AsNoTracking()
                .OrderBy(t => t.SortId)
                .ThenBy(t => t.Id)
                .ToList();
            var parentIds = all.Where(t => t.ParentId.HasValue).Select(t => t.ParentId.Value).ToHashSet();
            return all.Where(t => !parentIds.Contains(t.Id)).ToList();
        }

        static object BuildGeometryRow(GeometryItem item)
        {
            return new
            {
                id = item.Id,
                rawId = item.RawId,
                type = item.Type,
                menuId = item.MenuId,
                name = item.Name,
                alias = item.Alias,
                sortId = item.SortId,
                addr = item.Addr,
                gps = item.Gps,
                region = item.Region,
                url = item.Url,
                file = item.File,
                geoJson = item.GeoJson,
                dataJson = item.DataJson,
                isVisible = item.IsVisible,
                scale = item.Scale,
                labelColor = item.LabelColor,
                icon = item.Icon,
                menuName = item.MenuName,
                dataFrom = item.DataFrom
            };
        }

        static int GetSortId(object row)
        {
            var prop = row.GetType().GetProperty("sortId");
            return prop == null ? 0 : Convert.ToInt32(prop.GetValue(row) ?? 0);
        }

        static long GetId(object row)
        {
            var prop = row.GetType().GetProperty("id");
            return prop == null ? 0 : Convert.ToInt64(prop.GetValue(row) ?? 0);
        }

        static GeometryItem GetApiGeometryDetail(long id)
        {
            var apiId = Math.Abs(id);
            var menuId = apiId / 1_000_000_000L;
            var rawId = apiId % 1_000_000_000L;
            if (menuId <= 0 || rawId <= 0)
                return null;

            var menu = GisMenu.Get(menuId);
            if (menu == null)
                return null;

            var pageSize = menu.DataCnt ?? 200;
            if (pageSize <= 0) pageSize = 200;
            if (pageSize > 5000) pageSize = 5000;
            var list = Gis.GetMenuGeometryItems(menuId, new Paging { PageIndex = 0, PageSize = pageSize }, null);
            return list.FirstOrDefault(t => t.RawId == rawId || t.Id == id);
        }

        static (int okCount, int failCount) RefreshMenuStats(long? menuId)
        {
            var menus = GetTargetMenus(menuId)
                .Where(t => t.DataFrom == GisDataFrom.API)
                .ToList();

            var okCount = 0;
            var failCount = 0;
            foreach (var menu in menus)
            {
                try
                {
                    var list = Gis.GetMenuGeometryItems(menu.Id);
                    menu.DataCnt = list.Count;
                    menu.DataDt = DateTime.Now;
                    menu.Save();
                    okCount++;
                }
                catch
                {
                    failCount++;
                }
            }
            return (okCount, failCount);
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

        private static IEnumerable<string> EnumerateJsonCandidates(string dataJson)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var text = (dataJson ?? string.Empty).Trim();
            for (var i = 0; i < 4 && !string.IsNullOrWhiteSpace(text); i++)
            {
                if (seen.Add(text))
                    yield return text;

                var normalized = NormalizeJsonLikeText(text);
                if (!string.IsNullOrWhiteSpace(normalized) && seen.Add(normalized))
                    yield return normalized;

                var next = TryUnwrapJsonText(text);
                if (string.IsNullOrWhiteSpace(next) || string.Equals(next, text, StringComparison.Ordinal))
                    yield break;

                text = next;
            }
        }

        private static string TryUnwrapJsonText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text ?? string.Empty;

            var normalized = NormalizeJsonLikeText(text);
            try
            {
                if ((normalized.StartsWith("\"") && normalized.EndsWith("\""))
                    || (normalized.StartsWith("'") && normalized.EndsWith("'")))
                {
                    var decoded = JsonSerializer.Deserialize<string>(normalized.Replace('\'', '"'));
                    if (!string.IsNullOrWhiteSpace(decoded))
                        return decoded.Trim();
                }
            }
            catch
            {
                // ignore and fallback to manual unescape
            }

            var manual = NormalizeQuotedText(normalized);
            if (!string.Equals(manual, normalized, StringComparison.Ordinal))
                return manual;

            var unescaped = normalized.Replace("\\\"", "\"").Replace("\\\\", "\\").Trim();
            return unescaped;
        }

        private static string NormalizeJsonLikeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text ?? string.Empty;

            return text.Trim()
                .Replace('“', '"')
                .Replace('”', '"')
                .Replace('‘', '\'')
                .Replace('’', '\'');
        }

        private static string NormalizeQuotedText(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input ?? string.Empty;

            var text = input.Trim();
            for (var i = 0; i < 3; i++)
            {
                if ((text.StartsWith("\"") && text.EndsWith("\"")) || (text.StartsWith("'") && text.EndsWith("'")))
                    text = text.Substring(1, text.Length - 2).Trim();

                if (text.StartsWith("\\\"") && text.EndsWith("\\\"") && text.Length >= 4)
                    text = text.Substring(2, text.Length - 4).Trim();
                else
                    break;
            }

            return text;
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
                previewUrl = $"/Shared/FileViews/Viewer?uniId={Uri.EscapeDataString(uniId)}&id={t.Id}",
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
