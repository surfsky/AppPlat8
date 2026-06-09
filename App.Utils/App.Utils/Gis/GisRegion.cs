using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using App.Utils;

namespace App.Utils.Gis
{
    /// <summary>Gis 区域类型</summary>
    public enum GisRegionType
    {
        Rectangle,
        Circle,
        Polygon
    }

    /// <summary>
    /// Gis 区域解析器
    /// </summary>
    public class GisRegion
    {
        public GisRegionType Type { get; set; }
        public double[] Data { get; set; }
        public double[] Center { get; set; }
        public double Radius { get; set; }
        public List<double[]> PolygonPoints { get; set; }

        /// <summary>解析区域定义字符串，支持矩形（Rectangle）、圆形（Circle）和多边形（Polygon）三种类型的区域定义。</summary>
        /// <param name="text">区域定义字符串</param>
        /// <returns>解析后的 GisRegion 对象，如果解析失败则返回 null</returns>
        /// <example>
        /// 矩形：{"type":"Rectangle","data":[lng1,lat1,lng2,lat2]} 或者简化的矩形定义：lng1,lat1,lng2,lat2
        /// 圆形：{"type":"Circle","center":[lng,lat],"radius":radius}
        /// 多边形：{"type":"Polygon","points":[lng1,lat1,lng2,lat2,...lngn,latn]}
        /// </example>
        public static GisRegion Parse(string text)
        {
            if (text.IsEmpty())
                return null;

            try
            {
                var r = new GisRegion();
                using var doc = System.Text.Json.JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return null;

                var type = root.TryGetProperty("type", out var typeNode) ? (typeNode.GetString() ?? string.Empty).Trim() : string.Empty;
                if (type.IsEmpty())
                {
                    // 尝试解析简化的矩形定义格式：lng1,lat1,lng2,lat2
                    var arr = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (arr.Length == 4)
                    {
                        r.Type = GisRegionType.Rectangle;
                        r.Data = arr.Select(x => double.Parse(x)).ToArray();
                        return r;
                    }
                    else
                    {
                        return null;
                    }
                }

                // 根据 type 字段的值解析不同类型的区域定义
                if (!Enum.TryParse<GisRegionType>(type, true, out var parsedType))
                    return null;
                r.Type = parsedType;
                if (parsedType == GisRegionType.Rectangle)
                {
                    if (!root.TryGetProperty("data", out var dataNode) || dataNode.ValueKind != JsonValueKind.Array)
                        return null;
                    var arr = dataNode.EnumerateArray().Select(x => x.GetDouble()).ToArray();
                    if (arr.Length < 4)
                        return null;
                    r.Data = arr;
                    return r;
                }

                if (parsedType == GisRegionType.Circle)
                {
                    if (!root.TryGetProperty("center", out var centerNode) || centerNode.ValueKind != JsonValueKind.Array)
                        return null;
                    var center = centerNode.EnumerateArray().Select(x => x.GetDouble()).ToArray();
                    if (center.Length < 2)
                        return null;
                    var radius = root.TryGetProperty("radius", out var radiusNode) && radiusNode.ValueKind == JsonValueKind.Number
                        ? radiusNode.GetDouble()
                        : 0;
                    if (radius <= 0)
                        return null;

                    r.Center = center;
                    r.Radius = radius;
                    return r;
                }

                if (parsedType == GisRegionType.Polygon)
                {
                    if (!root.TryGetProperty("data", out var polyNode) || polyNode.ValueKind != JsonValueKind.Array)
                        return null;

                    var points = new List<double[]>();
                    foreach (var p in polyNode.EnumerateArray())
                    {
                        if (p.ValueKind != JsonValueKind.Array)
                            continue;
                        var pair = p.EnumerateArray().Select(x => x.GetDouble()).ToArray();
                        if (pair.Length < 2)
                            continue;
                        points.Add(new[] { pair[0], pair[1] });
                    }
                    if (points.Count < 3)
                        return null;
                    r.PolygonPoints = points;
                    return r;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        public bool Contains(double lng, double lat)
        {
            if (Type == GisRegionType.Rectangle)
            {
                var left = Math.Min(Data[0], Data[2]);
                var right = Math.Max(Data[0], Data[2]);
                var bottom = Math.Min(Data[1], Data[3]);
                var top = Math.Max(Data[1], Data[3]);
                return lng >= left && lng <= right && lat >= bottom && lat <= top;
            }

            if (Type == GisRegionType.Circle)
            {
                return DistanceMeter(Center[0], Center[1], lng, lat) <= Radius;
            }

            if (Type == GisRegionType.Polygon)
            {
                return PointInPolygon(lng, lat, PolygonPoints);
            }

            return true;
        }

        public static bool PointInPolygon(double x, double y, List<double[]> points)
        {
            var inside = false;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                var xi = points[i][0];
                var yi = points[i][1];
                var xj = points[j][0];
                var yj = points[j][1];

                var intersect = ((yi > y) != (yj > y)) &&
                                (x < (xj - xi) * (y - yi) / ((yj - yi) + 1e-12) + xi);
                if (intersect)
                    inside = !inside;
            }
            return inside;
        }

        public static double DistanceMeter(double lng1, double lat1, double lng2, double lat2)
        {
            const double earthRadius = 6378137d;
            var dLat = (lat2 - lat1) * Math.PI / 180d;
            var dLng = (lng2 - lng1) * Math.PI / 180d;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                    + Math.Cos(lat1 * Math.PI / 180d) * Math.Cos(lat2 * Math.PI / 180d)
                    * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadius * c;
        }
    }

}