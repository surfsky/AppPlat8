using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using App.Utils;

namespace App.Components
{
    //=====================================================================
    // AMap 相关的数据结构定义
    //=====================================================================
    /// <summary>地址查询结果项</summary>
    public class GisAddrItem
    {
        public string Name { get; set; }
        public string District { get; set; }
        public string Address { get; set; }
        public double Lng { get; set; }
        public double Lat { get; set; }
    }

    /// <summary>高德地图访问凭证配置项</summary>
    public class AmapCredential
    {
        public string Name { get; set; }
        public string Key { get; set; }
        public string SecurityKey { get; set; }
        public bool PreferSignature { get; set; }
    }

    /// <summary>高德地图接口返回的结果结构</summary>
    public class AmapResponseBase
    {
        public string Status { get; set; }
        public string Info { get; set; }
        public string Infocode { get; set; }
    }

    /// <summary>高德地图 POI 接口返回的结果结构</summary>
    public class AmapPoiResult : AmapResponseBase
    {
        public string Count { get; set; }
        public List<AmapPoi> Pois { get; set; } = new();
    }

    /// <summary>高德地图 POI 数据结构（简化）</summary>
    public class AmapPoi
    {
        public JsonElement Adname { get; set; }
        public JsonElement Address { get; set; }
        public JsonElement Name { get; set; }
        public JsonElement Location { get; set; }
    }

    /// <summary>高德地图地理编码接口返回的结果结构</summary>
    public class AmapGeocode
    {
        public JsonElement District { get; set; }
        public JsonElement Formatted_address { get; set; }
        public JsonElement Location { get; set; }
    }

    /// <summary>高德地图地理编码接口返回的结果结构</summary>
    public class AmapGeocodeResult : AmapResponseBase
    {
        public List<AmapGeocode> Geocodes { get; set; } = new();
    }

    //=====================================================================
    // 高德地图 API 调用辅助类
    //=====================================================================
    /// <summary>
    /// 高德地图 API 调用辅助类
    /// </summary>
    public class AmapHelper
    {
        // 预定义两个高德地图的访问凭证配置项，分别适用于服务器端和前端调用
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

        /// <summary>获取高德地图凭证列表（供外部遍历使用）</summary>
        public static IReadOnlyList<AmapCredential> Credentials => AmapCredentials;


        /// <summary>获取地址列表</summary>
        public static List<GisAddrItem> GetAddrs(string name)
        {
            name = AmapHelper.NormalizeAddressQuery(name);
            var list = new List<GisAddrItem>();
            var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var query = new Dictionary<string, string>
            {
                ["keywords"] = name,
                ["offset"] = "12",
                ["page"] = "1",
                ["extensions"] = "base"
            };

            // POI 文本检索更接近 geocoder.getLocation 的体验，适合"会展中心"等场景。
            if (AmapHelper.TryGetAmapResult<AmapPoiResult>("/v3/place/text", query, out var poiResult, out _))
            {
                if (poiResult?.Pois != null)
                {
                    foreach (var poi in poiResult.Pois)
                    {
                        var location = AmapHelper.GetJsonText(poi.Location);
                        if (!GisHelper.TryParseLngLat(location, out var gcjLng, out var gcjLat))
                            continue;

                        var nameText = AmapHelper.GetJsonText(poi.Name);
                        if (nameText.IsEmpty())
                            continue;

                        var district = AmapHelper.GetJsonText(poi.Adname);
                        var address = AmapHelper.GetJsonText(poi.Address);
                        var wgs = GisHelper.Gcj02ToWgs84(gcjLng, gcjLat);
                        var key = $"{nameText}:{Math.Round(wgs.Lng, 6)},{Math.Round(wgs.Lat, 6)}";
                        if (!dedup.Add(key))
                            continue;

                        list.Add(new GisAddrItem
                        {
                            Name = nameText,
                            District = district,
                            Address = address,
                            Lng = Math.Round(wgs.Lng, 6),
                            Lat = Math.Round(wgs.Lat, 6)
                        });
                    }
                }
            }

            if (list.Count > 0)
                return list.Take(12).ToList();
            return new List<GisAddrItem>();
        }

        /// <summary>获取单个地址的经纬度信息</summary>
        public static GisAddrItem GetAddr(string name)
        {
            name = NormalizeAddressQuery(name);
            if (name.IsEmpty())
                return null;

            var query = new Dictionary<string, string>
            {
                ["address"] = name
            };
            if (!TryGetAmapResult<AmapGeocodeResult>("/v3/geocode/geo", query, out var geocodeResult, out _))
                return null;

            var geocode = geocodeResult?.Geocodes?.FirstOrDefault();
            var location = geocode == null ? string.Empty : GetJsonText(geocode.Location);
            if (!GisHelper.TryParseLngLat(location, out var gcjLng, out var gcjLat))
                return null;

            // 转换为 WGS84 坐标系
            var wgs = GisHelper.Gcj02ToWgs84(gcjLng, gcjLat);
            var formattedAddress = geocode == null ? string.Empty : GetJsonText(geocode.Formatted_address);
            if (formattedAddress.IsEmpty())
                formattedAddress = name;
            return new GisAddrItem
            {
                Name = formattedAddress,
                District = geocode == null ? string.Empty : GetJsonText(geocode.District),
                Address = formattedAddress,
                Lng = Math.Round(wgs.Lng, 6),
                Lat = Math.Round(wgs.Lat, 6)
            };
        }

        /// <summary>尝试获取AMap接口结果（自动遍历凭证，跳过签名型）</summary>
        public static bool TryGetAmapResult<T>(string path, Dictionary<string, string> query, out T result, out string failInfo)
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

        /// <summary>获取AMap接口结果（指定凭证）</summary>
        public static bool GetAmapResult<T>(string path, Dictionary<string, string> query, out T result, out string failInfo, AmapCredential credential)
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

        /// <summary>从JsonElement中提取文本值</summary>
        public static string GetJsonText(JsonElement node)
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

        /// <summary>规范化地址查询参数（解码URL编码）</summary>
        public static string NormalizeAddressQuery(string input)
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

        /// <summary>构建高德地图API的完整URL</summary>
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

        /// <summary>计算MD5哈希</summary>
        private static string ToMd5(string text)
        {
            using var md5 = MD5.Create();
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>HTTP GET 请求</summary>
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
    }
}
