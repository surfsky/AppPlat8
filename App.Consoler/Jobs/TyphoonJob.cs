using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using Quartz;
namespace App.Consoler;

/// <summary>
/// 台风数据采集任务：每 10 分钟从 Digital Typhoon 抓取最近台风的实时轨迹，
/// 仅补录数据库中不存在的台风（已存在的跳过，避免重复写入）。
/// 数据源与前端 TyphoonLayer 保持一致，便于前后端观感统一。
/// </summary>
public class TyphoonJob : IJob
{
    /// <summary>实时轨迹 GeoJSON 基地址（Digital Typhoon / GeoNLP 镜像）</summary>
    const string TrackApi = "https://www.geonlp.org/digital-typhoon/geojson/wnp/";
    /// <summary>在线台风编号列表页（Digital Typhoon / GeoNLP 镜像）</summary>
    const string ListApi = "https://www.geonlp.org/digital-typhoon/latest/track/index.html.en";
    /// <summary>单次采集的台风数量上限</summary>
    const int MaxCount = 10;
    /// <summary>节 → 米/秒 换算系数</summary>
    const double KnotToMs = 0.514444;
    /// <summary>允许通过代理放行名称不匹配证书的只读公开台风源。</summary>
    static readonly HashSet<string> TrustedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "www.geonlp.org",
        "codh.ex.nii.ac.jp",
        "agora.ex.nii.ac.jp"
    };

    static readonly HttpClient DirectHttp = CreateHttp(useProxy: false, timeoutSeconds: 8);
    static readonly HttpClient ProxyHttp = CreateHttp(useProxy: true, timeoutSeconds: 20);

    /// <summary>执行采集</summary>
    public async Task Execute(IJobExecutionContext context)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 开始执行台风数据采集任务");

        if (EntityConfig.Db is not AppPlatContext db)
            throw new InvalidOperationException("数据库上下文未初始化");

        try
        {
            await CollectAsync(db);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 台风数据采集失败：{FlattenException(ex)}");
        }
    }

    /// <summary>抓取最近台风并补录数据库</summary>
    static async Task CollectAsync(AppPlatContext db)
    {
        var codes = await FetchLatestCodesAsync();
        if (codes.Count == 0)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 未获取到台风编号，任务结束");
            return;
        }

        var exist = db.GisTyphoons.Where(t => codes.Contains(t.Code)).Select(t => t.Code).ToHashSet();
        Console.WriteLine($"候选台风 {codes.Count} 个，数据库已有 {exist.Count} 个");

        var payloads = new List<object>();
        foreach (var code in codes)
        {
            if (exist.Contains(code))
            {
                Console.WriteLine($"台风 {code} 已存在，跳过");
                continue;
            }
            try
            {
                var payload = await FetchTrackPayloadAsync(code);
                if (payload == null)
                {
                    Console.WriteLine($"台风 {code} 无有效轨迹，跳过");
                    continue;
                }
                payloads.Add(payload);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"台风 {code} 抓取失败：{FlattenException(ex)}");
            }
        }

        if (payloads.Count == 0)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 无新增台风，任务结束");
            return;
        }

        var json = JsonSerializer.Serialize(payloads);
        var result = GisTyphoonImporter.ImportLiveData(db, json);
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 台风采集完成，新增台风 {result.TyphoonAddCnt}，新增轨迹 {result.LogAddCnt}");
        foreach (var log in result.Logs)
            Console.WriteLine(log);
    }

    /// <summary>抓取在线台风编号（当年），按编号倒序取前 N 个</summary>
    static async Task<List<string>> FetchLatestCodesAsync()
    {
        var html = await GetRemoteTextAsync(ListApi);
        var year = DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture);

        // 优先匹配台风概要页链接（wnp/s/202601.html），匹配不到时退回宽松匹配
        var codes = ExtractCodes(html, @"wnp/s/(\d{6})", year);
        if (codes.Count == 0)
            codes = ExtractCodes(html, @"\b(\d{6})\b", year);

        return codes.Take(MaxCount).ToList();
    }

    /// <summary>按正则提取当年台风编号，按编号倒序返回</summary>
    static List<string> ExtractCodes(string html, string pattern, string year)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(html, pattern))
        {
            var code = m.Groups[1].Value;
            if (code.StartsWith(year, StringComparison.Ordinal))
                set.Add(code);
        }
        return set.OrderByDescending(c => c, StringComparer.Ordinal).ToList();
    }

    /// <summary>抓取单个台风的轨迹，返回可被 ImportLiveData 解析的对象；无轨迹时返回 null</summary>
    static async Task<object?> FetchTrackPayloadAsync(string code)
    {
        var url = $"{TrackApi}{Uri.EscapeDataString(code)}.en.json";
        var json = await GetRemoteTextAsync(url);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
            return null;

        var name = "";
        if (root.TryGetProperty("properties", out var meta) && meta.TryGetProperty("name", out var nameEl))
            name = nameEl.GetString() ?? "";

        var points = new List<TrackPoint>();
        foreach (var f in features.EnumerateArray())
        {
            if (!f.TryGetProperty("properties", out var props)) continue;
            if (!f.TryGetProperty("geometry", out var geom)) continue;
            if (!geom.TryGetProperty("coordinates", out var coord) || coord.ValueKind != JsonValueKind.Array || coord.GetArrayLength() < 2) continue;
            if (!coord[0].TryGetDouble(out var lng) || !coord[1].TryGetDouble(out var lat)) continue;

            var unix = GetUnixTime(props, "time");
            if (unix <= 0) continue;

            points.Add(new TrackPoint
            {
                Time = unix,
                Lng = Math.Round(lng, 4),
                Lat = Math.Round(lat, 4),
                Pressure = GetInt(props, "pressure"),
                WindMs = (int)Math.Round(GetDouble(props, "wind") * KnotToMs, MidpointRounding.AwayFromZero),
                LevelCode = GetInt(props, "class")
            });
        }
        if (points.Count == 0)
            return null;

        var track = points.OrderBy(p => p.Time).Select(p => new
        {
            time = ToIso(p.Time),
            lng = p.Lng,
            lat = p.Lat,
            pressure = p.Pressure,
            windMs = p.WindMs,
            levelCode = p.LevelCode
        });
        return new { code, name, track };
    }

    /// <summary>UNIX 秒转 UTC ISO 文本</summary>
    static string ToIso(long unixSeconds)
    {
        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }

    /// <summary>读取 UNIX 秒</summary>
    static long GetUnixTime(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n)) return n;
        return long.TryParse(v.ToString(), out var r) ? r : 0;
    }

    /// <summary>读取小数</summary>
    static double GetDouble(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)) return d;
        return double.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : 0;
    }

    /// <summary>读取整数</summary>
    static int? GetInt(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
        return int.TryParse(v.ToString(), out var r) ? r : null;
    }

    /// <summary>读取远端文本：优先直连，失败后回退默认网络通道。</summary>
    static async Task<string> GetRemoteTextAsync(string url)
    {
        Exception? directError = null;
        try
        {
            return await DirectHttp.GetStringAsync(url);
        }
        catch (Exception ex)
        {
            directError = ex;
        }

        try
        {
            return await ProxyHttp.GetStringAsync(url);
        }
        catch (Exception ex)
        {
            throw new HttpRequestException($"访问 {url} 失败。直连: {FlattenException(directError!)} | 代理: {FlattenException(ex)}", ex);
        }
    }

    /// <summary>创建带浏览器标识的 HttpClient。</summary>
    static HttpClient CreateHttp(bool useProxy, int timeoutSeconds)
    {
        var handler = new HttpClientHandler
        {
            UseProxy = useProxy,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };
        if (useProxy)
            handler.ServerCertificateCustomValidationCallback = ValidateTyphoonSourceCertificate;

        var http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (compatible; AppPlat/1.0)");
        http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json,text/html,*/*");
        return http;
    }

    /// <summary>仅对指定台风源放行代理导致的证书名称不匹配，其它证书错误仍然拦截。</summary>
    static bool ValidateTyphoonSourceCertificate(HttpRequestMessage request, X509Certificate2? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None)
            return true;

        if (request?.RequestUri == null)
            return false;

        if (!TrustedHosts.Contains(request.RequestUri.Host))
            return false;

        return errors == SslPolicyErrors.RemoteCertificateNameMismatch;
    }

    /// <summary>拍平异常链，便于直接看清根因</summary>
    static string FlattenException(Exception ex)
    {
        var parts = new List<string>();
        for (var cur = ex; cur != null; cur = cur.InnerException)
            parts.Add(cur.Message);
        return string.Join(" | ", parts.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct());
    }

    /// <summary>轨迹点中间结构</summary>
    class TrackPoint
    {
        public long Time { get; set; }
        public double Lng { get; set; }
        public double Lat { get; set; }
        public int? Pressure { get; set; }
        public int WindMs { get; set; }
        public int? LevelCode { get; set; }
    }
}
