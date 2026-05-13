using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.DAL.GIS;
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

            item.Name = req.Name;
            item.Alias = req.Alias;
            item.SortId = req.SortId;
            item.MenuId = req.MenuId;
            item.Addr = req.Addr;
            item.GeoJson = req.GeoJson;
            item.DataJson = req.DataJson;

            var gpsText = string.IsNullOrWhiteSpace(req.Gps)
                ? TryBuildGpsFromGeoJson(req.GeoJson)
                : NormalizeGps(req.Gps);
            item.Gps = gpsText;

            item.Save();
            return BuildResult(0, "保存成功");
        }

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
