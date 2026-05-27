using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.EleUI;
using App.Entities;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisGeometryEdit)]
    public class GeometryFormModel : AdminModel
    {
        public GisGeometry Item { get; set; }

        public void OnGet()
        {
        }

        /// <summary>获取点位数据</summary>
        public IActionResult OnGetData(long id, long? menuId, long? gisMenuId, string gps, string geoJson)
        {
            var item = GisGeometry.GetDetail(id) ?? new GisGeometry();
            var selectedMenuId = menuId ?? gisMenuId;
            if (id == 0)
            {
                item.MenuId = selectedMenuId;
                if (!string.IsNullOrWhiteSpace(gps))
                    item.Gps = gps;
                if (!string.IsNullOrWhiteSpace(geoJson))
                    item.GeoJson = geoJson;
            }
            return BuildResult(0, "success", item);
        }

        /// <summary>保存点位</summary>
        public IActionResult OnPostSave([FromBody] GisGeometry req, long? menuId, long? gisMenuId)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var selectedMenuId = menuId ?? gisMenuId;
            if (req.Id == 0 && !req.MenuId.HasValue && selectedMenuId.HasValue)
                req.MenuId = selectedMenuId;

            GisGeometry item;
            if (req.Id > 0)
            {
                item = GisGeometry.Get(req.Id);
                if (item == null)
                    return BuildResult(403, "无权编辑或数据不存在");
            }
            else
            {
                item = new GisGeometry();
                item.CreateDt = DateTime.Now;
                item.CreatorId = GetUserId();
            }

            item.Type = req.Type;
            item.Name = req.Name;
            item.Alias = req.Alias;
            item.SortId = req.SortId;
            item.MenuId = req.MenuId;
            item.Addr = req.Addr;
            item.Url = req.Url;
            item.File = Uploader.SaveFile(nameof(GisGeometry), req.File);
            item.GeoJson = req.GeoJson;
            item.DataJson = req.DataJson;
            item.Region = NormalizeRegion(req.Region);
            if (string.IsNullOrWhiteSpace(item.Region))
                item.Region = TryBuildRegionFromGeoJson(req.GeoJson);

            var gpsText = string.IsNullOrWhiteSpace(req.Gps)
                ? TryBuildGpsFromGeoJson(req.GeoJson)
                : NormalizeGps(req.Gps);
            item.Gps = gpsText;

            item.Save();
            return BuildResult(0, "保存成功");
        }

        /// <summary>显示点位附件</summary>
        public IActionResult OnPostShowFiles([FromBody] GisGeometry req)
        {
            var geometryId = req?.Id ?? 0;
            if (geometryId <= 0)
                return EleManager.ShowNotify("请先保存点位，再维护附件", NotifyType.Warning, "提示");

            var uniId = req.UniId;
            var geometryName = Uri.EscapeDataString(req?.Name ?? string.Empty);
            var url = $"/Shared/Atts?uniId={uniId}&name={geometryName}&md={this.Mode}";
            return EleManager.ShowDrawer(
                title: "点位附件",
                url: url,
                size: "50%"
                );
        }

        /// <summary>获取点位附件数据</summary>
        public IActionResult OnGetAttData(Paging pi, long id)
        {
            if (id <= 0)
                return BuildResult(0, "success", new { items = new List<object>(), total = 0 });

            var geometry = GisGeometry.GetDetail(id);
            if (geometry == null)
                return BuildResult(0, "success", new { items = new List<object>(), total = 0 });

            var uniId = geometry.UniId;
            if (string.IsNullOrWhiteSpace(uniId))
                return BuildResult(0, "success", new { items = new List<object>(), total = 0 });

            var pageIndex = pi?.PageIndex ?? 0;
            var pageSize = (pi?.PageSize ?? 10) > 0 ? pi.PageSize : 10;

            var query = Att.Set
                .Where(t => t.Key == uniId)
                .OrderBy(t => t.SortId)
                .ThenByDescending(t => t.Id);

            var total = query.Count();
            var rows = query
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToList()
                .Select(t => (object)new
                {
                    id = t.Id,
                    name = string.IsNullOrWhiteSpace(t.FileName) ? Path.GetFileName(t.Url ?? string.Empty) : t.FileName,
                    sizeText = t.FileSizeText,
                    createDtText = t.CreateDt?.ToString("yyyy-MM-dd HH:mm"),
                    previewUrl = $"/Shared/FileViewer?uniId={Uri.EscapeDataString(uniId)}&id={t.Id}"
                })
                .ToList();

            return BuildResult(0, "success", new
            {
                items = rows,
                total
            });
        }

        /// <summary>规范化GPS文本，生成经度在前、纬度在后的格式，如"{lng:0.######},{lat:0.######}"</summary>
        private static string NormalizeGps(string gps)
        {
            if (string.IsNullOrWhiteSpace(gps))
                return string.Empty;

            var parts = gps
                .Replace("，", ",")
                .Replace("；", ",")
                .Replace(";", ",")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length < 2)
                return gps.Trim();

            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lng)
                && !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.CurrentCulture, out lng))
                return gps.Trim();

            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                && !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.CurrentCulture, out lat))
                return gps.Trim();

            return string.Create(CultureInfo.InvariantCulture, $"{lng:0.######},{lat:0.######}");
        }

        private static string TryBuildGpsFromGeoJson(string geoJson)
        {
            if (string.IsNullOrWhiteSpace(geoJson))
                return string.Empty;

            try
            {
                using var doc = JsonDocument.Parse(geoJson);
                if (!TryCollectBounds(doc.RootElement, out var minLng, out var minLat, out var maxLng, out var maxLat))
                    return string.Empty;

                var lng = (minLng + maxLng) / 2d;
                var lat = (minLat + maxLat) / 2d;
                return string.Create(CultureInfo.InvariantCulture, $"{lng:0.######},{lat:0.######}");
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string NormalizeRegion(string region)
        {
            if (string.IsNullOrWhiteSpace(region))
                return string.Empty;

            var parts = region
                .Replace("，", ",")
                .Replace("；", ",")
                .Replace(";", ",")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length < 4)
                return string.Empty;

            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var tlx)
                && !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.CurrentCulture, out tlx))
                return string.Empty;
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var tly)
                && !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.CurrentCulture, out tly))
                return string.Empty;
            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var brx)
                && !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.CurrentCulture, out brx))
                return string.Empty;
            if (!double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var bry)
                && !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.CurrentCulture, out bry))
                return string.Empty;

            var minLng = Math.Min(tlx, brx);
            var maxLng = Math.Max(tlx, brx);
            var minLat = Math.Min(tly, bry);
            var maxLat = Math.Max(tly, bry);

            if (!double.IsFinite(minLng) || !double.IsFinite(maxLng) || !double.IsFinite(minLat) || !double.IsFinite(maxLat))
                return string.Empty;
            if (Math.Abs(maxLng - minLng) < 1e-9 || Math.Abs(maxLat - minLat) < 1e-9)
                return string.Empty;

            return string.Create(CultureInfo.InvariantCulture, $"{minLng:0.######},{maxLat:0.######},{maxLng:0.######},{minLat:0.######}");
        }

        private static string TryBuildRegionFromGeoJson(string geoJson)
        {
            if (string.IsNullOrWhiteSpace(geoJson))
                return string.Empty;

            try
            {
                using var doc = JsonDocument.Parse(geoJson);
                if (!TryCollectBounds(doc.RootElement, out var minLng, out var minLat, out var maxLng, out var maxLat))
                    return string.Empty;

                if (Math.Abs(maxLng - minLng) < 1e-9 || Math.Abs(maxLat - minLat) < 1e-9)
                    return string.Empty;

                return string.Create(CultureInfo.InvariantCulture, $"{minLng:0.######},{maxLat:0.######},{maxLng:0.######},{minLat:0.######}");
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool TryCollectBounds(JsonElement root, out double minLng, out double minLat, out double maxLng, out double maxLat)
        {
            var localMinLng = double.MaxValue;
            var localMinLat = double.MaxValue;
            var localMaxLng = double.MinValue;
            var localMaxLat = double.MinValue;

            var hasPoint = false;

            void VisitGeometry(JsonElement geometry)
            {
                if (geometry.ValueKind != JsonValueKind.Object)
                    return;
                if (!geometry.TryGetProperty("coordinates", out var coordinates))
                    return;
                VisitCoordinates(coordinates);
            }

            void VisitCoordinates(JsonElement coordinates)
            {
                if (coordinates.ValueKind != JsonValueKind.Array)
                    return;

                var values = coordinates.EnumerateArray().ToArray();
                if (values.Length >= 2
                    && values[0].ValueKind == JsonValueKind.Number
                    && values[1].ValueKind == JsonValueKind.Number)
                {
                    var lng = values[0].GetDouble();
                    var lat = values[1].GetDouble();
                    if (double.IsFinite(lng) && double.IsFinite(lat))
                    {
                        localMinLng = Math.Min(localMinLng, lng);
                        localMinLat = Math.Min(localMinLat, lat);
                        localMaxLng = Math.Max(localMaxLng, lng);
                        localMaxLat = Math.Max(localMaxLat, lat);
                        hasPoint = true;
                    }
                    return;
                }

                foreach (var child in values)
                    VisitCoordinates(child);
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("type", out var typeEl))
                {
                    var type = typeEl.GetString();
                    if (string.Equals(type, "FeatureCollection", StringComparison.OrdinalIgnoreCase)
                        && root.TryGetProperty("features", out var features)
                        && features.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var feature in features.EnumerateArray())
                        {
                            if (feature.ValueKind == JsonValueKind.Object && feature.TryGetProperty("geometry", out var geometry))
                                VisitGeometry(geometry);
                        }
                    }
                    else if (string.Equals(type, "Feature", StringComparison.OrdinalIgnoreCase)
                        && root.TryGetProperty("geometry", out var geometry))
                    {
                        VisitGeometry(geometry);
                    }
                    else
                    {
                        VisitGeometry(root);
                    }
                }
                else if (root.TryGetProperty("coordinates", out var coordinates))
                {
                    VisitCoordinates(coordinates);
                }
            }

            minLng = localMinLng;
            minLat = localMinLat;
            maxLng = localMaxLng;
            maxLat = localMaxLat;
            return hasPoint;
        }
    }
}
