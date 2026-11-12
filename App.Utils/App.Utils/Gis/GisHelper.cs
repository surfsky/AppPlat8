using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using App.Utils;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;


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

        /// <summary>合并 GeoJSON 中的 Polygon / MultiPolygon 区域，返回一个 GeoJSON FeatureCollection。</summary>
        public static string CombineShape(string[] geoData)
        {
            if (geoData == null || geoData.Length < 2)
                throw new ArgumentException("至少需要两个图形才能合并。", nameof(geoData));

            var factory = new GeometryFactory(new PrecisionModel(), 4326);
            var polygons = new List<Geometry>();
            foreach (var json in geoData)
            {
                if (string.IsNullOrWhiteSpace(json))
                    throw new ArgumentException("待合并图形包含空 GeoJSON。", nameof(geoData));

                using var document = ParseJsonDocument(json);
                CollectPolygonalGeometry(document.RootElement, factory, polygons);
            }

            if (polygons.Count < 2)
                throw new ArgumentException("至少需要两个有效的多边形区域。", nameof(geoData));

            var merged = UnaryUnionOp.Union(polygons);
            if (merged == null || merged.IsEmpty || !(merged is Polygon || merged is MultiPolygon))
                throw new ArgumentException("合并结果不是有效的多边形区域。", nameof(geoData));

            var geometryNode = WritePolygonalGeometry(merged);
            return new JsonObject
            {
                ["type"] = "FeatureCollection",
                ["features"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "Feature",
                        ["properties"] = new JsonObject(),
                        ["geometry"] = geometryNode
                    }
                }
            }.ToJsonString();
        }

        private static JsonDocument ParseJsonDocument(string json)
        {
            var document = JsonDocument.Parse(json);
            while (document.RootElement.ValueKind == JsonValueKind.String)
            {
                var nested = document.RootElement.GetString();
                document.Dispose();
                if (string.IsNullOrWhiteSpace(nested))
                    throw new ArgumentException("GeoJSON 内容为空。", nameof(json));
                document = JsonDocument.Parse(nested);
            }
            return document;
        }

        private static void CollectPolygonalGeometry(JsonElement node, GeometryFactory factory, List<Geometry> polygons)
        {
            if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty("type", out var typeNode))
                throw new ArgumentException("GeoJSON 缺少 type 字段。");

            switch (typeNode.GetString())
            {
                case "FeatureCollection":
                    if (node.TryGetProperty("features", out var features) && features.ValueKind == JsonValueKind.Array)
                        foreach (var feature in features.EnumerateArray())
                            CollectPolygonalGeometry(feature, factory, polygons);
                    break;
                case "Feature":
                    if (node.TryGetProperty("geometry", out var geometry) && geometry.ValueKind != JsonValueKind.Null)
                        CollectPolygonalGeometry(geometry, factory, polygons);
                    break;
                case "GeometryCollection":
                    if (node.TryGetProperty("geometries", out var geometries) && geometries.ValueKind == JsonValueKind.Array)
                        foreach (var collectionGeometry in geometries.EnumerateArray())
                            CollectPolygonalGeometry(collectionGeometry, factory, polygons);
                    break;
                case "Polygon":
                    polygons.Add(ParsePolygon(node.GetProperty("coordinates"), factory));
                    break;
                case "MultiPolygon":
                    foreach (var polygon in node.GetProperty("coordinates").EnumerateArray())
                        polygons.Add(ParsePolygon(polygon, factory));
                    break;
                // Shape GeoJSON may also include labels/points; only polygonal parts participate in union.
                case "Point":
                case "MultiPoint":
                case "LineString":
                case "MultiLineString":
                    break;
                default:
                    throw new ArgumentException($"不支持的 GeoJSON 类型：{typeNode.GetString()}。");
            }
        }

        private static Polygon ParsePolygon(JsonElement coordinates, GeometryFactory factory)
        {
            if (coordinates.ValueKind != JsonValueKind.Array || coordinates.GetArrayLength() == 0)
                throw new ArgumentException("Polygon 缺少坐标环。");

            var rings = coordinates.EnumerateArray().Select(ring =>
            {
                var points = ring.EnumerateArray().Select(ParseCoordinate).ToList();
                if (points.Count < 3)
                    throw new ArgumentException("Polygon 坐标环至少需要三个点。");
                if (!SamePosition(points[0], points[^1]))
                    points.Add(new Coordinate(points[0]));
                if (points.Count < 4)
                    throw new ArgumentException("Polygon 坐标环无效。");
                return factory.CreateLinearRing(points.ToArray());
            }).ToArray();

            return factory.CreatePolygon(rings[0], rings.Skip(1).ToArray());
        }

        private static Coordinate ParseCoordinate(JsonElement position)
        {
            if (position.ValueKind != JsonValueKind.Array || position.GetArrayLength() < 2)
                throw new ArgumentException("GeoJSON 坐标至少需要经度和纬度。");
            var values = position.EnumerateArray().Select(x => x.GetDouble()).ToArray();
            return values.Length > 2 ? new CoordinateZ(values[0], values[1], values[2]) : new Coordinate(values[0], values[1]);
        }

        private static bool SamePosition(Coordinate a, Coordinate b) => a.X.Equals(b.X) && a.Y.Equals(b.Y);

        private static JsonObject WritePolygonalGeometry(Geometry geometry)
        {
            if (geometry is Polygon polygon)
                return new JsonObject { ["type"] = "Polygon", ["coordinates"] = WritePolygonCoordinates(polygon) };

            var multiPolygon = (MultiPolygon)geometry;
            var coordinates = new JsonArray();
            foreach (var polygonPart in multiPolygon.Geometries.Cast<Polygon>())
                coordinates.Add(WritePolygonCoordinates(polygonPart));
            return new JsonObject
            {
                ["type"] = "MultiPolygon",
                ["coordinates"] = coordinates
            };
        }

        private static JsonArray WritePolygonCoordinates(Polygon polygon)
        {
            var rings = new JsonArray { WriteRing(polygon.ExteriorRing.Coordinates) };
            for (var i = 0; i < polygon.NumInteriorRings; i++)
                rings.Add(WriteRing(polygon.GetInteriorRingN(i).Coordinates));
            return rings;
        }

        private static JsonArray WriteRing(Coordinate[] coordinates)
        {
            var ring = new JsonArray();
            foreach (var coordinate in coordinates)
            {
                var position = new JsonArray(coordinate.X, coordinate.Y);
                if (!double.IsNaN(coordinate.Z) && !double.IsInfinity(coordinate.Z))
                    position.Add(coordinate.Z);
                ring.Add(position);
            }
            return ring;
        }

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
