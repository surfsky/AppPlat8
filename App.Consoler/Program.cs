using App.DAL;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Quartz.Impl;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;


// 解析命令行参数
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

// 检查数据库连接
if (!CanConnect(conn))
{
	Console.WriteLine($"数据库连接失败: {conn}");
	return;
}

// 执行一次接口检测与菜单统计
if (!scheduleMode)
{
	GisApiRunner.RunOnce(conn, apiBaseUrl, checkObjectLimit, checkObjectIntervalMs, amapKey);
    return;
}

// 启动调度任务
await RunSchedulerAsync(conn, cron, apiBaseUrl, checkObjectLimit, checkObjectIntervalMs, amapKey);

/// <summary>解析整数参数</summary>
/// <param name="cmdArgs"></param>
/// <param name="key"></param>
/// <param name="defaultValue"></param>
/// <param name="min"></param>
/// <param name="max"></param>
/// <returns></returns>
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

/// <summary>解析默认数据库路径</summary>
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

/// <summary>检查数据库连接是否成功</summary>
static bool CanConnect(string conn)
{
	var options = new DbContextOptionsBuilder<AppPlatContext>()
		.UseSqlite(conn)
		.Options;
	using var db = new AppPlatContext(options);
	return db.Database.CanConnect();
}

/// <summary>启动Quartz调度任务</summary>
/// <param name="conn"></param>
/// <param name="cron"></param>
/// <param name="apiBaseUrl"></param>
/// <param name="checkObjectLimit"></param>
/// <param name="checkObjectIntervalMs"></param>
/// <param name="amapKey"></param>
/// <returns></returns>
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
