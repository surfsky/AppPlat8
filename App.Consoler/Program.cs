using App.DAL;
using App.DAL.GIS;
using App.Entities;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Quartz.Impl;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

var connArg = args.FirstOrDefault(t => t.StartsWith("--conn=", StringComparison.OrdinalIgnoreCase));
var conn = connArg?.Substring("--conn=".Length);
if (string.IsNullOrWhiteSpace(conn))
{
	var dbPath = ResolveDefaultDbPath();
	conn = $"Data Source={dbPath};";
}

var scheduleMode = args.Any(t => string.Equals(t, "--schedule", StringComparison.OrdinalIgnoreCase));
var cron = args
	.FirstOrDefault(t => t.StartsWith("--cron=", StringComparison.OrdinalIgnoreCase))
	?.Substring("--cron=".Length)
	?.Trim();
if (string.IsNullOrWhiteSpace(cron))
	cron = "0 0 2 * * ?";

var checkObjectLimit = ParseIntArg(args, "--check-object-limit=", 10, 1, 1000);
var checkObjectIntervalMs = ParseIntArg(args, "--check-object-interval-ms=", 1800, 200, 60_000);
var apiBaseUrl = args
	.FirstOrDefault(t => t.StartsWith("--api-base=", StringComparison.OrdinalIgnoreCase))
	?.Substring("--api-base=".Length)
	?.Trim();
if (string.IsNullOrWhiteSpace(apiBaseUrl))
	apiBaseUrl = "http://localhost:6060";

var amapKey = args
	.FirstOrDefault(t => t.StartsWith("--amap-key=", StringComparison.OrdinalIgnoreCase))
	?.Substring("--amap-key=".Length)
	?.Trim();
if (string.IsNullOrWhiteSpace(amapKey))
	amapKey = "5eaa3c7ad8e09e3fdce1fb4fcf3e02f7";

if (!CanConnect(conn))
{
	Console.WriteLine($"数据库连接失败: {conn}");
	return;
}

if (!scheduleMode)
{
	GisApiRunner.RunOnce(conn, apiBaseUrl, checkObjectLimit, checkObjectIntervalMs, amapKey);
    return;
}

await RunSchedulerAsync(conn, cron, apiBaseUrl, checkObjectLimit, checkObjectIntervalMs, amapKey);

static int ParseIntArg(string[] cmdArgs, string key, int defaultValue, int min, int max)
{
	var text = cmdArgs
		.FirstOrDefault(t => t.StartsWith(key, StringComparison.OrdinalIgnoreCase))
		?.Substring(key.Length)
		?.Trim();

	if (!int.TryParse(text, out var value))
		return defaultValue;
    if (value < min) return min;
    if (value > max) return max;
	return value;
}

static string ResolveDefaultDbPath()
{
	var candidates = new List<string>();

	void AddCandidate(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return;
		candidates.Add(Path.GetFullPath(path));
	}

	AddCandidate(Path.Combine(Directory.GetCurrentDirectory(), "App", "Db", "sqlite.db"));
	AddCandidate(Path.Combine(AppContext.BaseDirectory, "App", "Db", "sqlite.db"));

	var current = new DirectoryInfo(AppContext.BaseDirectory);
	for (var i = 0; i < 8 && current != null; i++)
	{
		AddCandidate(Path.Combine(current.FullName, "App", "Db", "sqlite.db"));
		current = current.Parent;
	}

	var firstExists = candidates.FirstOrDefault(File.Exists);
	if (!string.IsNullOrWhiteSpace(firstExists))
		return firstExists;

	return candidates.First();
}

static bool CanConnect(string conn)
{
	var options = new DbContextOptionsBuilder<AppPlatContext>()
		.UseSqlite(conn)
		.Options;
	using var db = new AppPlatContext(options);
	return db.Database.CanConnect();
}

static async Task RunSchedulerAsync(string conn, string cron, string apiBaseUrl, int checkObjectLimit, int checkObjectIntervalMs, string amapKey)
{
	var factory = new StdSchedulerFactory();
	var scheduler = await factory.GetScheduler();

	var job = JobBuilder.Create<GisApiDailyJob>()
		.WithIdentity("gis-api-daily-job")
		.UsingJobData("conn", conn)
		.UsingJobData("apiBaseUrl", apiBaseUrl)
		.UsingJobData("checkObjectLimit", checkObjectLimit)
		.UsingJobData("checkObjectIntervalMs", checkObjectIntervalMs)
		.UsingJobData("amapKey", amapKey)
		.Build();

	var trigger = TriggerBuilder.Create()
		.WithIdentity("gis-api-daily-trigger")
		.WithCronSchedule(cron)
		.Build();

	await scheduler.ScheduleJob(job, trigger);
	await scheduler.Start();

	Console.WriteLine($"Quartz 调度已启动，Cron={cron}");
	Console.WriteLine($"CheckObject同步参数: limit={checkObjectLimit}, intervalMs={checkObjectIntervalMs}");
	Console.WriteLine("按 Ctrl+C 退出。可用 --cron=... 覆盖默认每日2点执行。");

	using var quitEvent = new ManualResetEventSlim(false);
	Console.CancelKeyPress += (_, e) =>
	{
		e.Cancel = true;
		quitEvent.Set();
	};

	quitEvent.Wait();
	await scheduler.Shutdown(waitForJobsToComplete: true);
}

public class GisApiDailyJob : IJob
{
	public Task Execute(IJobExecutionContext context)
	{
		var conn = context.MergedJobDataMap.GetString("conn");
		var apiBaseUrl = context.MergedJobDataMap.GetString("apiBaseUrl");
		var checkObjectLimit = context.MergedJobDataMap.GetInt("checkObjectLimit");
		var checkObjectIntervalMs = context.MergedJobDataMap.GetInt("checkObjectIntervalMs");
		var amapKey = context.MergedJobDataMap.GetString("amapKey");
		if (string.IsNullOrWhiteSpace(conn))
		{
			Console.WriteLine("GisApiDailyJob: conn为空，跳过执行");
			return Task.CompletedTask;
		}

		Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 开始执行接口检测与菜单统计");
		GisApiRunner.RunOnce(conn, apiBaseUrl, checkObjectLimit, checkObjectIntervalMs, amapKey);
		Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 调度任务执行完成");
		return Task.CompletedTask;
	}
}

public static class GisApiRunner
{
	public static void RunOnce(string conn, string apiBaseUrl, int checkObjectLimit, int checkObjectIntervalMs, string amapKey)
	{
		var options = new DbContextOptionsBuilder<AppPlatContext>()
			.UseSqlite(conn)
			.Options;

		using var db = new AppPlatContext(options);
		Func<Microsoft.EntityFrameworkCore.DbContext> onGetDb = () => db;
		Func<DataAccessScope> onGetScope = () => new DataAccessScope
		{
			Enabled = false,
			AllowAll = true,
		};
		EntityConfig.Instance.OnGetDb += onGetDb;
		EntityConfig.Instance.OnGetDataAccessScope += onGetScope;

		try
		{
			EnsureCheckObjectGisApi(apiBaseUrl);

			var syncResult = CheckObjectGpsSync.Sync(checkObjectLimit, checkObjectIntervalMs, amapKey);
			Console.WriteLine($"CheckObject.Gps 同步完成：尝试={syncResult.Total} 成功={syncResult.Success} 失败={syncResult.Failed}");

			var (okCount, failCount) = GisApi.RefreshStats(null);
			var menuCount = GisMenu.FixAll();
			Console.WriteLine($"GisApi.RefreshStats 完成：成功={okCount} 失败={failCount}");
			Console.WriteLine($"GisMenu.FixAll 执行完成，更新菜单数: {menuCount}");
		}
		finally
		{
			EntityConfig.Instance.OnGetDb -= onGetDb;
			EntityConfig.Instance.OnGetDataAccessScope -= onGetScope;
		}
	}

	private static void EnsureCheckObjectGisApi(string apiBaseUrl)
	{
		var baseUrl = (apiBaseUrl ?? string.Empty).Trim().TrimEnd('/');
		if (string.IsNullOrWhiteSpace(baseUrl))
			return;

		const string apiName = "检查对象点位接口";
		var targetUrl = $"{baseUrl}/httpapi/gis/GetCheckObjectPoints";

		var item = GisApi.Set.FirstOrDefault(t => t.Name == apiName) ?? GisApi.Set.FirstOrDefault(t => t.DataUrl == targetUrl);
		if (item == null)
		{
			item = new GisApi
			{
				Name = apiName,
				DataUrl = targetUrl,
				IsEnabled = true,
				SortId = 1000,
			};
			item.Save();
			Console.WriteLine($"已写入 GisApi: {apiName} -> {targetUrl}");
			return;
		}

		var changed = false;
		if (!string.Equals(item.DataUrl, targetUrl, StringComparison.OrdinalIgnoreCase))
		{
			item.DataUrl = targetUrl;
			changed = true;
		}
		if (!item.IsEnabled)
		{
			item.IsEnabled = true;
			changed = true;
		}

		if (changed)
		{
			item.Save();
			Console.WriteLine($"已更新 GisApi: {item.Name} -> {item.DataUrl}");
		}
	}
}

internal static class CheckObjectGpsSync
{
	private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

	internal sealed class SyncResult
	{
		public int Total { get; set; }
		public int Success { get; set; }
		public int Failed { get; set; }
	}

	public static SyncResult Sync(int limit, int intervalMs, string amapKey)
	{
		var result = new SyncResult();
		if (string.IsNullOrWhiteSpace(amapKey))
		{
			Console.WriteLine("CheckObject.Gps 同步跳过：amapKey为空");
			return result;
		}

		var items = CheckObject.Set
			.Where(t => t.IsDel != true && string.IsNullOrWhiteSpace(t.Gps))
			.Where(t => !string.IsNullOrWhiteSpace(t.Address) || !string.IsNullOrWhiteSpace(t.Name))
			.OrderBy(t => t.Id)
			.Take(limit)
			.ToList();

		result.Total = items.Count;
		if (items.Count == 0)
			return result;

		for (var i = 0; i < items.Count; i++)
		{
			var item = items[i];
			var q = !string.IsNullOrWhiteSpace(item.Address) ? item.Address.Trim() : item.Name?.Trim() ?? string.Empty;
			if (string.IsNullOrWhiteSpace(q))
			{
				result.Failed++;
				continue;
			}

			try
			{
				if (TryGeocodeWgs84(q, amapKey, out var gps))
				{
					item.Gps = gps;
					item.UpdateDt = DateTime.Now;
					result.Success++;
				}
				else
				{
					result.Failed++;
				}
			}
			catch (Exception ex)
			{
				result.Failed++;
				Console.WriteLine($"CheckObject[{item.Id}] 坐标转换失败: {ex.Message}");
			}

			if (i < items.Count - 1)
				Thread.Sleep(intervalMs);
		}

		CheckObject.Db.SaveChanges();
		return result;
	}

	private static bool TryGeocodeWgs84(string queryText, string amapKey, out string gps)
	{
		gps = string.Empty;
		var address = Uri.EscapeDataString(queryText);
		var key = Uri.EscapeDataString(amapKey);
		var url = $"https://restapi.amap.com/v3/geocode/geo?key={key}&address={address}";

		using var resp = Http.GetAsync(url).GetAwaiter().GetResult();
		if (!resp.IsSuccessStatusCode)
			return false;

		var text = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		if (string.IsNullOrWhiteSpace(text))
			return false;

		using var doc = JsonDocument.Parse(text);
		var root = doc.RootElement;
		var status = root.TryGetProperty("status", out var statusNode) ? statusNode.GetString() : "0";
		if (status != "1")
			return false;

		if (!root.TryGetProperty("geocodes", out var geocodes) || geocodes.ValueKind != JsonValueKind.Array || geocodes.GetArrayLength() == 0)
			return false;

		var geo = geocodes[0];
		if (!geo.TryGetProperty("location", out var locNode))
			return false;

		var location = locNode.GetString() ?? string.Empty;
		var parts = location.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length < 2)
			return false;

		if (!double.TryParse(parts[0], out var gcjLng) || !double.TryParse(parts[1], out var gcjLat))
			return false;

		var wgs = Gcj02ToWgs84(gcjLng, gcjLat);
		gps = $"{wgs.lng:0.######},{wgs.lat:0.######}";
		return true;
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
