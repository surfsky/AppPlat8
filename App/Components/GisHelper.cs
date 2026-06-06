using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using App.Utils;


/// <summary>经纬度点</summary>
/// <param name="Lng"></param>
/// <param name="Lat"></param>
/// <returns></returns>
public record LngLat(double Lng, double Lat)
{
    public static LngLat Parse(string gps)
    {
        if (string.IsNullOrWhiteSpace(gps))
            return null;

        var parts = gps.Split(',');
        if (parts.Length != 2)
            return null;

        if (double.TryParse(parts[0], out double lng) && double.TryParse(parts[1], out double lat))
            return new LngLat(lng, lat);

        return null;
    }
}


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


/// <summary>
/// 地理空间帮助类
/// </summary>
public class GisHelper
{

    public static bool IsInRegion(string gps, string geoRegion)
    {
        var lngLat = LngLat.Parse(gps);
        if (lngLat == null)
            return false;

        // 解析geoRegion，判断lngLat是否在区域内
        // 这里可以使用第三方库，如NetTopologySuite，来处理地理空间数据，判断点是否在多边形内等复杂的地理空间关系。
        var region = GisRegion.Parse(geoRegion);
        if (region == null)
            return false;
        return region.Contains(lngLat.Lng, lngLat.Lat);    
    }

    //---------------------------------------------------------
    // 解析经纬度字符串及坐标转换相关算法
    //---------------------------------------------------------
    public static bool TryParseLngLat(string location, out double lng, out double lat)
    {
        lng = 0;
        lat = 0;
        if (location.IsEmpty())
            return false;

        var arr = location.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (arr.Length < 2)
            return false;

        return double.TryParse(arr[0], NumberStyles.Float, CultureInfo.InvariantCulture, out lng)
            && double.TryParse(arr[1], NumberStyles.Float, CultureInfo.InvariantCulture, out lat);
    }



    public static LngLat Gcj02ToWgs84(double lng, double lat)
    {
        if (OutOfChina(lng, lat))
            return new LngLat(lng, lat);

        const double a = 6378245.0;
        const double ee = 0.00669342162296594323;
        var dLat = TransformLat(lng - 105.0, lat - 35.0);
        var dLng = TransformLng(lng - 105.0, lat - 35.0);
        var radLat = lat / 180.0 * Math.PI;
        var magic = Math.Sin(radLat);
        magic = 1 - ee * magic * magic;
        var sqrtMagic = Math.Sqrt(magic);
        dLat = (dLat * 180.0) / ((a * (1 - ee)) / (magic * sqrtMagic) * Math.PI);
        dLng = (dLng * 180.0) / (a / sqrtMagic * Math.Cos(radLat) * Math.PI);
        var mgLat = lat + dLat;
        var mgLng = lng + dLng;
        return new LngLat(lng * 2 - mgLng, lat * 2 - mgLat);
    }

    public static bool OutOfChina(double lng, double lat)
    {
        return lng < 72.004 || lng > 137.8347 || lat < 0.8293 || lat > 55.8271;
    }

    public static double TransformLat(double lng, double lat)
    {
        var ret = -100.0 + 2.0 * lng + 3.0 * lat + 0.2 * lat * lat + 0.1 * lng * lat + 0.2 * Math.Sqrt(Math.Abs(lng));
        ret += (20.0 * Math.Sin(6.0 * lng * Math.PI) + 20.0 * Math.Sin(2.0 * lng * Math.PI)) * 2.0 / 3.0;
        ret += (20.0 * Math.Sin(lat * Math.PI) + 40.0 * Math.Sin(lat / 3.0 * Math.PI)) * 2.0 / 3.0;
        ret += (160.0 * Math.Sin(lat / 12.0 * Math.PI) + 320 * Math.Sin(lat * Math.PI / 30.0)) * 2.0 / 3.0;
        return ret;
    }

    public static double TransformLng(double lng, double lat)
    {
        var ret = 300.0 + lng + 2.0 * lat + 0.1 * lng * lng + 0.1 * lng * lat + 0.1 * Math.Sqrt(Math.Abs(lng));
        ret += (20.0 * Math.Sin(6.0 * lng * Math.PI) + 20.0 * Math.Sin(2.0 * lng * Math.PI)) * 2.0 / 3.0;
        ret += (20.0 * Math.Sin(lng * Math.PI) + 40.0 * Math.Sin(lng / 3.0 * Math.PI)) * 2.0 / 3.0;
        ret += (150.0 * Math.Sin(lng / 12.0 * Math.PI) + 300.0 * Math.Sin(lng / 30.0 * Math.PI)) * 2.0 / 3.0;
        return ret;
    }
}

