using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using App.Utils;


namespace App.Utils.Gis
{
    /// <summary>
    /// 地理空间帮助类
    /// </summary>
    public class GisHelper
    {
        public const string CoordTypeWgs84 = "WGS84";
        public const string CoordTypeGcj02 = "GCJ02";
        public const string CoordTypeBd09 = "BD09";

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

        public static string NormalizeCoordType(string coordType)
        {
            var text = (coordType ?? string.Empty)
                .Trim()
                .Replace("-", string.Empty)
                .Replace("_", string.Empty)
                .Replace(" ", string.Empty)
                .ToUpperInvariant();

            if (text == "GCJ02" || text == "GCJ" || text == "MARS" || text == "火星坐标")
                return CoordTypeGcj02;

            if (text == "BD09" || text == "BD" || text == "BAIDU" || text == "百度坐标")
                return CoordTypeBd09;

            return CoordTypeWgs84;
        }

        public static LngLat Bd09ToGcj02(double lng, double lat)
        {
            const double xPi = Math.PI * 3000.0 / 180.0;
            var x = lng - 0.0065;
            var y = lat - 0.006;
            var z = Math.Sqrt(x * x + y * y) - 0.00002 * Math.Sin(y * xPi);
            var theta = Math.Atan2(y, x) - 0.000003 * Math.Cos(x * xPi);
            var gcjLng = z * Math.Cos(theta);
            var gcjLat = z * Math.Sin(theta);
            return new LngLat(gcjLng, gcjLat);
        }

        public static bool TryConvertToWgs84(string gps, string coordType, out string normalizedGps, out string error)
        {
            normalizedGps = string.Empty;
            error = string.Empty;

            var point = LngLat.Parse(gps);
            if (point == null)
            {
                error = $"经纬度格式错误: {gps}";
                return false;
            }

            var sourceType = NormalizeCoordType(coordType);
            LngLat wgs = point;
            if (sourceType == CoordTypeGcj02)
            {
                wgs = Gcj02ToWgs84(point.Lng, point.Lat);
            }
            else if (sourceType == CoordTypeBd09)
            {
                var gcj = Bd09ToGcj02(point.Lng, point.Lat);
                wgs = Gcj02ToWgs84(gcj.Lng, gcj.Lat);
            }

            normalizedGps = string.Create(CultureInfo.InvariantCulture, $"{wgs.Lng:0.######},{wgs.Lat:0.######}");
            return true;
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
}
