using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using App.DAL;
using App.DAL.GIS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using App.Components;
using System.Text.RegularExpressions;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisGeometryView)]
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
            var query = GisGeometry.DataSet
                .Where(g => !string.IsNullOrWhiteSpace(g.GeoJson) || !string.IsNullOrWhiteSpace(g.GPS));

            if (menuId.HasValue)
                query = query.Where(g => g.MenuId == menuId.Value);

            var list = query
                .OrderBy(g => g.SortId)
                .ThenBy(g => g.Id)
                .Select(g => new
                {
                    id = g.Id,
                    menuId = g.MenuId,
                    name = g.Name,
                    alias = g.Alias,
                    sortId = g.SortId,
                    addr = g.Addr,
                    gps = g.GPS,
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

            return BuildResult(0, "success", new
            {
                id = item.Id,
                menuId = item.MenuId,
                menuName = item.MenuName,
                name = item.Name,
                alias = item.Alias,
                addr = item.Addr,
                gps = item.GPS,
                geoJson = item.GeoJson,
                dataJson = item.DataJson,
                dataRows = ParseDataRows(item.DataJson),
                canEdit
            });
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
