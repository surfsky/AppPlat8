using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using App.HttpApi;
using App.Utils;
using Microsoft.EntityFrameworkCore;

namespace App.API
{
    //---------------------------------------------------------
    // AMap 相关的数据结构定义
    //---------------------------------------------------------
    internal class GisAddrItem
    {
        public string Name { get; set; }
        public string District { get; set; }
        public string Address { get; set; }
        public double Lng { get; set; }
        public double Lat { get; set; }
    }

    /// <summary>高德地图访问凭证配置项</summary>
    internal class AmapCredential
    {
        public string Name  { get; set; }
        public string Key { get; set; }
        public string SecurityKey { get; set; }
        public bool PreferSignature { get; set; }
    }


    /// <summary>高德地图接口返回的结果结构</summary>
    internal class AmapResponseBase
    {
        public string Status { get; set; }
        public string Info { get; set; }
        public string Infocode { get; set; }
    }

    /// <summary>高德地图 POI 接口返回的结果结构</summary>
    internal class AmapPoiResult : AmapResponseBase
    {
        public string Count { get; set; }
        public List<AmapPoi> Pois { get; set; } = new();
    }

    /// <summary>高德地图 POI 数据结构（简化）。TODO：将属性改为完全的string类型</summary>
    internal class AmapPoi
    {
        public JsonElement Adname { get; set; }
        public JsonElement Address { get; set; }
        public JsonElement Name { get; set; }
        public JsonElement Location { get; set; }
    }

    /// <summary>高德地图地理编码接口返回的结果结构。TODO：将属性改为完全的string类型</summary>
    internal class AmapGeocode
    {
        public JsonElement District { get; set; }
        public JsonElement Formatted_address { get; set; }
        public JsonElement Location { get; set; }
    }

    /// <summary>高德地图地理编码接口返回的结果结构</summary>
    internal class AmapGeocodeResult : AmapResponseBase
    {
        public List<AmapGeocode> Geocodes { get; set; } = new();
    }

    /// <summary>
    /// Gis 数据接口
    /// </summary>
    public class Gis
    {
        // 这里预定义了两个高德地图的访问凭证配置项，分别适用于服务器端和前端调用
        private static readonly List<AmapCredential> AmapCredentials = new()
        {
            new AmapCredential
            {
                Name = "WebServer",
                Key = "5eaa3c7ad8e09e3fdce1fb4fcf3e02f7",
                SecurityKey = string.Empty,
                PreferSignature = false
            },
            new AmapCredential
            {
                Name = "WebJs",
                Key = "b264475ba4e7ca7df2d147cc575cf645",
                SecurityKey = "d6347d4e9b4f4bbf6f6d92a4d9c5e78e",
                PreferSignature = true
            }
        };

        //---------------------------------------------------------
        // GIS菜单树相关接口
        //---------------------------------------------------------
        [HttpApi("获取GIS菜单树", AuthLogin = true)]
        public static APIResult GetMenuTree(long? excludeId = null, long? selectedId = null)
        {
            // 这里使用实时查询，避免前端在菜单调整后读取到旧缓存。
            var all = GisMenu.Set.AsNoTracking().ToList();
            var allMap = all.ToDictionary(t => t.Id, t => t);
            var visibleMap = all.ToDictionary(t => t.Id, t => t);

            if (excludeId.HasValue)
            {
                var blockedIds = all.GetDescendants(excludeId).Select(t => t.Id).ToHashSet();
                foreach (var id in blockedIds)
                {
                    visibleMap.Remove(id);
                }
            }

            if (selectedId.HasValue && allMap.TryGetValue(selectedId.Value, out var selected))
            {
                var current = selected;
                while (current != null)
                {
                    visibleMap[current.Id] = current;
                    if (current.ParentId == null) break;
                    if (!allMap.TryGetValue(current.ParentId.Value, out current)) break;
                }
            }

            var tree = visibleMap.Values.OrderBy(t => t.SortId).ThenBy(t => t.Id).ToList().ToTree();
            return tree.ToResult();
        }

        //---------------------------------------------------------
        // 地址查询相关接口（使用高德地图API）
        //---------------------------------------------------------
        [HttpApi("获取检查对象点位数据", AuthLogin = false)]
        public static APIResult GetCheckObjectPoints(string name = null, long? orgId = null, bool? isDel = false, string region = null, int maxCount = 500)
        {
            if (maxCount <= 0)
                maxCount = 100;
            if (maxCount > 5000)
                maxCount = 5000;

            var q = CheckObject.Set.AsNoTracking()
                .Where(t => !string.IsNullOrWhiteSpace(t.Gps));

            if (isDel != null)
                q = q.Where(t => t.IsDel == isDel.Value);
            if (orgId.IsNotEmpty())
                q = q.Where(t => t.DutyOrgId == orgId.Value);
            if (name.IsNotEmpty())
                q = q.Where(t => (t.Name ?? string.Empty).Contains(name.Trim()));

            var regionFilter = RegionFilter.Parse(region);
            var list = new List<object>();
            var rows = q
                .OrderBy(t => t.Id)
                .Take(maxCount * 4)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Address,
                    t.Gps,
                    t.SocialCreditCode,
                    t.RiskLevel,
                    t.ObjectType,
                    t.Scale,
                    t.DutyOrgId,
                    t.DutyUserName,
                })
                .ToList();

            foreach (var item in rows)
            {
                if (!TryParseLngLat(item.Gps, out var lng, out var lat))
                    continue;
                if (regionFilter != null && !regionFilter.Contains(lng, lat))
                    continue;

                list.Add(new
                {
                    id = item.Id,
                    name = item.Name,
                    gps = item.Gps,
                    addr = item.Address,
                    lng,
                    lat,
                    dataJson = new
                    {
                        item.SocialCreditCode,
                        item.RiskLevel,
                        item.ObjectType,
                        item.Scale,
                        item.DutyOrgId,
                        item.DutyUserName,
                    }
                });

                if (list.Count >= maxCount)
                    break;
            }

            return list.ToResult();
        }

        [HttpApi("获取地址列表", AuthLogin = true)]
        public static APIResult GetAddrs(string name)
        {
            name = NormalizeAddressQuery(name);
            if (name.IsEmpty())
                return new APIResult(-1, "请输入地址关键字");

            var list = new List<GisAddrItem>();
            var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var query = new Dictionary<string, string>
            {
                ["keywords"] = name,
                ["offset"] = "12",
                ["page"] = "1",
                ["extensions"] = "base"
            };

            // POI 文本检索更接近 geocoder.getLocation 的体验，适合“会展中心”等场景。
            if (TryGetAmapResult<AmapPoiResult>("/v3/place/text", query, out var poiResult, out _))
            {
                if (poiResult?.Pois != null)
                {
                    foreach (var poi in poiResult.Pois)
                    {
                        var location = GetJsonText(poi.Location);
                        if (!TryParseLngLat(location, out var gcjLng, out var gcjLat))
                            continue;

                        var nameText = GetJsonText(poi.Name);
                        if (nameText.IsEmpty())
                            continue;

                        var district = GetJsonText(poi.Adname);
                        var address = GetJsonText(poi.Address);
                        var wgs = Gcj02ToWgs84(gcjLng, gcjLat);
                        var key = $"{nameText}:{Math.Round(wgs.lng, 6)},{Math.Round(wgs.lat, 6)}";
                        if (!dedup.Add(key))
                            continue;

                        list.Add(new GisAddrItem
                        {
                            Name = nameText,
                            District = district,
                            Address = address,
                            Lng = Math.Round(wgs.lng, 6),
                            Lat = Math.Round(wgs.lat, 6)
                        });
                    }
                }
            }

            if (list.Count > 0)
                return list.Take(12).ToList().ToResult();

            // 回退到旧版 geocode 逻辑。
            return GetAddrsGeo(name);
        }

        [HttpApi("获取地址列表（旧）", AuthLogin = true)]
        public static APIResult GetAddrsGeo(string name)
        {
            name = NormalizeAddressQuery(name);
            if (name.IsEmpty())
                return new APIResult(-1, "请输入地址关键字");

            var list = new List<GisAddrItem>();
            var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var query = new Dictionary<string, string>
            {
                ["address"] = name
            };

            if (!TryGetAmapResult<AmapGeocodeResult>("/v3/geocode/geo", query, out var geocodeResult, out var failInfo))
                return new APIResult(-1, failInfo);

            if (geocodeResult?.Geocodes != null)
            {
                foreach (var geocode in geocodeResult.Geocodes)
                {
                    var location = GetJsonText(geocode.Location);
                    if (!TryParseLngLat(location, out var gcjLng, out var gcjLat))
                        continue;

                    var wgs = Gcj02ToWgs84(gcjLng, gcjLat);
                    var formattedAddress = GetJsonText(geocode.Formatted_address);
                    if (formattedAddress.IsEmpty())
                        formattedAddress = name;
                    var district = GetJsonText(geocode.District);
                    var key = $"{formattedAddress}:{Math.Round(wgs.lng, 6)},{Math.Round(wgs.lat, 6)}";
                    if (!dedup.Add(key))
                        continue;

                    list.Add(new GisAddrItem
                    {
                        Name = formattedAddress,
                        District = district,
                        Address = formattedAddress,
                        Lng = Math.Round(wgs.lng, 6),
                        Lat = Math.Round(wgs.lat, 6)
                    });
                }
            }

            return list.Take(12).ToList().ToResult();
        }

        [HttpApi("获取单个地址", AuthLogin = true)]
        public static APIResult GetAddr(string name)
        {
            name = NormalizeAddressQuery(name);
            if (name.IsEmpty())
                return new APIResult(-1, "请输入地址");

            // 获取地址信息
            var query = new Dictionary<string, string>
            {
                ["address"] = name
            };
            if (!TryGetAmapResult<AmapGeocodeResult>("/v3/geocode/geo", query, out var geocodeResult, out var failInfo))
                return new APIResult(-1, failInfo);

            // 解析
            var geocode = geocodeResult?.Geocodes?.FirstOrDefault();
            var location = geocode == null ? string.Empty : GetJsonText(geocode.Location);
            if (!TryParseLngLat(location, out var gcjLng, out var gcjLat))
                return new APIResult(-1, "未找到该地址");

            // 转换为 WGS84 坐标系
            var wgs = Gcj02ToWgs84(gcjLng, gcjLat);
            var formattedAddress = geocode == null ? string.Empty : GetJsonText(geocode.Formatted_address);
            if (formattedAddress.IsEmpty())
                formattedAddress = name;
            var addr = new GisAddrItem
            {
                Name = formattedAddress,
                District = geocode == null ? string.Empty : GetJsonText(geocode.District),
                Address = formattedAddress,
                Lng = Math.Round(wgs.lng, 6),
                Lat = Math.Round(wgs.lat, 6)
            };
            return addr.ToResult();
        }

        private static string GetJsonText(JsonElement node)
        {
            if (node.ValueKind == JsonValueKind.Undefined || node.ValueKind == JsonValueKind.Null)
                return string.Empty;

            if (node.ValueKind == JsonValueKind.String)
                return node.GetString() ?? string.Empty;

            if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in node.EnumerateArray())
                {
                    var text = GetJsonText(item);
                    if (!text.IsEmpty())
                        return text;
                }

                return string.Empty;
            }

            if (node.ValueKind == JsonValueKind.Object)
            {
                if (node.TryGetProperty("name", out var nameValue))
                {
                    var name = GetJsonText(nameValue);
                    if (!name.IsEmpty())
                        return name;
                }

                if (node.TryGetProperty("value", out var valueValue))
                {
                    var value = GetJsonText(valueValue);
                    if (!value.IsEmpty())
                        return value;
                }

                var rawObject = node.GetRawText();
                return string.Equals(rawObject, "null", StringComparison.OrdinalIgnoreCase) ? string.Empty : rawObject;
            }

            var raw = node.GetRawText();
            if (raw.IsEmpty())
                return string.Empty;

            return string.Equals(raw, "null", StringComparison.OrdinalIgnoreCase) ? string.Empty : raw.Trim('"');
        }

        private static bool GetAmapResult<T>(string path, Dictionary<string, string> query, out T result, out string failInfo, AmapCredential credential)
            where T : AmapResponseBase
        {
            result = null;
            var includeSignature = credential.PreferSignature;
            var body = Fetch(BuildAmapUrl(path, query, credential, includeSignature), out var error);
            try
            {
                result = body.Parse<T>();
                var status = result.Status;
                var info = result.Info;
                if (status == "1")
                {
                    failInfo = string.Empty;
                    return true;
                }
                failInfo = $"{credential.Key}: {info}";
                return false;
            }
            catch (Exception ex)
            {
                failInfo = $"{credential.Key}: 返回格式异常({ex.Message})";
                return false;
            }
        }

        private static bool TryGetAmapResult<T>(string path, Dictionary<string, string> query, out T result, out string failInfo)
            where T : AmapResponseBase
        {
            result = null;
            failInfo = string.Empty;
            var errors = new List<string>();
            foreach (var credential in AmapCredentials)
            {
                if (credential.PreferSignature)
                    continue;

                if (GetAmapResult(path, query, out result, out var oneFail, credential))
                    return true;
                if (!oneFail.IsEmpty())
                    errors.Add(oneFail);
            }

            failInfo = errors.Count > 0 ? string.Join(" | ", errors) : "高德地址查询失败";
            return false;
        }

        private static string NormalizeAddressQuery(string input)
        {
            var text = (input ?? string.Empty).Trim();
            if (text.IsEmpty())
                return string.Empty;

            // Some callers may already send URL-encoded text; decode once to avoid %25 double-encoding downstream.
            if (text.Contains('%') || text.Contains('+'))
            {
                try
                {
                    var decoded = Uri.UnescapeDataString(text.Replace("+", " "));
                    if (!decoded.IsEmpty())
                        text = decoded.Trim();
                }
                catch
                {
                    // Keep original text when decode fails.
                }
            }
            return text;
        }

        private static string BuildAmapUrl(string path, Dictionary<string, string> query, AmapCredential credential, bool includeSignature)
        {
            var requestQuery = new Dictionary<string, string>(query)
            {
                ["key"] = credential.Key
            };

            var sortedRaw = requestQuery.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={kv.Value ?? string.Empty}")
                .ToList();

            var sortedEscaped = requestQuery.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value ?? string.Empty)}")
                .ToList();

            if (includeSignature)
            {
                var sigRaw = $"{path}?{string.Join("&", sortedRaw)}{credential.SecurityKey}";
                var sig = ToMd5(sigRaw);
                sortedEscaped.Add($"sig={sig}");
            }

            var queryText = string.Join("&", sortedEscaped);
            return $"https://restapi.amap.com{path}?{queryText}";
        }

        private static string Fetch(string url, out string error)
        {
            error = string.Empty;
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
                using var resp = client.GetAsync(url).GetAwaiter().GetResult();
                var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode)
                    error = $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}";
                return body;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return string.Empty;
            }
        }

        //---------------------------------------------------------
        // 解析经纬度字符串及坐标转换相关算法
        //---------------------------------------------------------
        private static bool TryParseLngLat(string location, out double lng, out double lat)
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

        private class RegionFilter
        {
            public string Type { get; set; }
            public double[] Data { get; set; }
            public double[] Center { get; set; }
            public double Radius { get; set; }
            public List<double[]> PolygonPoints { get; set; }

            public static RegionFilter Parse(string text)
            {
                if (text.IsEmpty())
                    return null;

                try
                {
                    using var doc = JsonDocument.Parse(text);
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object)
                        return null;

                    var type = root.TryGetProperty("type", out var typeNode) ? (typeNode.GetString() ?? string.Empty).Trim() : string.Empty;
                    if (type.IsEmpty())
                        return null;

                    var f = new RegionFilter { Type = type };
                    if (string.Equals(type, "Rectangle", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!root.TryGetProperty("data", out var dataNode) || dataNode.ValueKind != JsonValueKind.Array)
                            return null;
                        var arr = dataNode.EnumerateArray().Select(x => x.GetDouble()).ToArray();
                        if (arr.Length < 4)
                            return null;
                        f.Data = arr;
                        return f;
                    }

                    if (string.Equals(type, "Circle", StringComparison.OrdinalIgnoreCase))
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

                        f.Center = center;
                        f.Radius = radius;
                        return f;
                    }

                    if (string.Equals(type, "Polygon", StringComparison.OrdinalIgnoreCase))
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
                        f.PolygonPoints = points;
                        return f;
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
                if (string.Equals(Type, "Rectangle", StringComparison.OrdinalIgnoreCase))
                {
                    var left = Math.Min(Data[0], Data[2]);
                    var right = Math.Max(Data[0], Data[2]);
                    var bottom = Math.Min(Data[1], Data[3]);
                    var top = Math.Max(Data[1], Data[3]);
                    return lng >= left && lng <= right && lat >= bottom && lat <= top;
                }

                if (string.Equals(Type, "Circle", StringComparison.OrdinalIgnoreCase))
                {
                    return DistanceMeter(Center[0], Center[1], lng, lat) <= Radius;
                }

                if (string.Equals(Type, "Polygon", StringComparison.OrdinalIgnoreCase))
                {
                    return PointInPolygon(lng, lat, PolygonPoints);
                }

                return true;
            }

            private static bool PointInPolygon(double x, double y, List<double[]> points)
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

            private static double DistanceMeter(double lng1, double lat1, double lng2, double lat2)
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

        private static string ToMd5(string text)
        {
            using var md5 = MD5.Create();
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static (double lng, double lat) Gcj02ToWgs84(double lng, double lat)
        {
            if (OutOfChina(lng, lat))
                return (lng, lat);

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
            return (lng * 2 - mgLng, lat * 2 - mgLat);
        }

        private static bool OutOfChina(double lng, double lat)
        {
            return lng < 72.004 || lng > 137.8347 || lat < 0.8293 || lat > 55.8271;
        }

        private static double TransformLat(double lng, double lat)
        {
            var ret = -100.0 + 2.0 * lng + 3.0 * lat + 0.2 * lat * lat + 0.1 * lng * lat + 0.2 * Math.Sqrt(Math.Abs(lng));
            ret += (20.0 * Math.Sin(6.0 * lng * Math.PI) + 20.0 * Math.Sin(2.0 * lng * Math.PI)) * 2.0 / 3.0;
            ret += (20.0 * Math.Sin(lat * Math.PI) + 40.0 * Math.Sin(lat / 3.0 * Math.PI)) * 2.0 / 3.0;
            ret += (160.0 * Math.Sin(lat / 12.0 * Math.PI) + 320 * Math.Sin(lat * Math.PI / 30.0)) * 2.0 / 3.0;
            return ret;
        }

        private static double TransformLng(double lng, double lat)
        {
            var ret = 300.0 + lng + 2.0 * lat + 0.1 * lng * lng + 0.1 * lng * lat + 0.1 * Math.Sqrt(Math.Abs(lng));
            ret += (20.0 * Math.Sin(6.0 * lng * Math.PI) + 20.0 * Math.Sin(2.0 * lng * Math.PI)) * 2.0 / 3.0;
            ret += (20.0 * Math.Sin(lng * Math.PI) + 40.0 * Math.Sin(lng / 3.0 * Math.PI)) * 2.0 / 3.0;
            ret += (150.0 * Math.Sin(lng / 12.0 * Math.PI) + 300.0 * Math.Sin(lng / 30.0 * Math.PI)) * 2.0 / 3.0;
            return ret;
        }

    }
}
