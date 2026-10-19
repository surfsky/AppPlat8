using App.DAL;
using App.Entities;
using App.Utils;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Quartz.Impl;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;


//---------------------------------------------------------------
// 启动参数检测
// public static void Main(string[] args)
//---------------------------------------------------------------
// 解析检查对象点位接口基础URL
var apiBaseUrl = args
	.FirstOrDefault(t => t.StartsWith("--api-base=", StringComparison.OrdinalIgnoreCase))
	?.Substring("--api-base=".Length)
	?.Trim();
if (string.IsNullOrWhiteSpace(apiBaseUrl))
	apiBaseUrl = "http://localhost:6060";

// 高德地图API密钥
var amapKey = args
	.FirstOrDefault(t => t.StartsWith("--amap-key=", StringComparison.OrdinalIgnoreCase))
	?.Substring("--amap-key=".Length)
	?.Trim();
if (string.IsNullOrWhiteSpace(amapKey))
	amapKey = "5eaa3c7ad8e09e3fdce1fb4fcf3e02f7";

// 检查数据库连接
var connArg = args.FirstOrDefault(t => t.StartsWith("--conn=", StringComparison.OrdinalIgnoreCase));
var conn = connArg?.Substring("--conn=".Length);
if (string.IsNullOrWhiteSpace(conn))
{
	var dbPath = ResolveDefaultDbPath();
	conn = $"Data Source={dbPath};";
}
if (!CanConnect(conn))
{
	Console.WriteLine($"数据库连接失败: {conn}");
	return;
}
ConfigureEntity(conn);
Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 调度器启动中");
Console.WriteLine($"API Base Url: {apiBaseUrl}");
Console.WriteLine($"数据库连接: {conn}");



//---------------------------------------------------------------
// 指定启动某个任务
//---------------------------------------------------------------
if (args.Any(t => t.StartsWith("--run=", StringComparison.OrdinalIgnoreCase)))
{
	var runArg = args.FirstOrDefault(t => t.StartsWith("--run=", StringComparison.OrdinalIgnoreCase));
	var run = runArg?.Substring("--run=".Length);
	if (string.IsNullOrWhiteSpace(run))
	{
		Console.WriteLine("run 参数为空");
		return;
	}
	var job = GetJob(run);
	if (job == null)
	{
		Console.WriteLine($"任务 {run} 不存在");
		return;
	}

	// 解析其他 --key=value 参数，通过 AsyncLocal 上下文传递给 Job
	var extras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
	foreach (var a in args)
	{
		var m = System.Text.RegularExpressions.Regex.Match(a, @"^--(?<k>[A-Za-z0-9_\-]+)=(?<v>.*)$");
		if (!m.Success) continue;
		var k = m.Groups["k"].Value;
		if (k.Equals("run", StringComparison.OrdinalIgnoreCase)) continue;
		if (k.Equals("conn", StringComparison.OrdinalIgnoreCase)) continue;
		if (k.Equals("api-base", StringComparison.OrdinalIgnoreCase)) continue;
		if (k.Equals("amap-key", StringComparison.OrdinalIgnoreCase)) continue;
		extras[k] = m.Groups["v"].Value;
	}
	extras["conn"] = conn ?? "";
	JobExtras.Current = extras;

	// 直接运行指定任务（Quartz context 接口用 FakeJobExecutionContext 仅满足 Execute 签名）
	var ctx = new FakeJobExecutionContext(extras);
	await job.Execute(ctx);
	JobExtras.Current = null;
	Console.WriteLine($"任务 {run} 执行完成");
	return;
}

/// <summary>获取指定任务实例</summary>
IJob? GetJob(string? jobName)
{
	return (jobName ?? "").Trim().ToLower() switch
	{
		"statjob" => new StatJob(),
		"apicheckjob" => new ApiCheckJob(),
		"checkobjectstatusfixjob" => new CheckObjectStatusFixJob(),
		"checkobjectgpsfixjob" => new CheckObjectGpsFixJob(),
		"gismenustatjob" => new GisMenuStatJob(),
		"typhoonimportjob" => new TyphoonImportJob(),
		"checkerusermergejob" => new CheckerUserMergeJob(),
		_ => null,
	};
}



//---------------------------------------------------------------
// 启动调度任务
//---------------------------------------------------------------
await RunSchedulerAsync();


/// <summary>启动Quartz调度任务</summary>
static async Task RunSchedulerAsync()
{
	var factory = new StdSchedulerFactory();
	var scheduler = await factory.GetScheduler();

	// 定义调度任务和触发器（cron 格式：秒 分 时 日 月 周）
    await ScheduleJobAsync<HeartBeat>(scheduler, "*/10 * * * * ?", "心跳任务");     // 每10秒执行一次心跳任务，从第 0 秒开始，每 10 秒触发
	await ScheduleJobAsync<StatJob>(scheduler, "0 0 * * * ?", "报表统计");                  // 每天0点执行报表统计
	await ScheduleJobAsync<ApiCheckJob>(scheduler, "0 0 * * * ?", "接口检测与菜单统计");      // 每天0点执行接口检测与菜单统计
	await ScheduleJobAsync<CheckObjectStatusFixJob>(scheduler, "0 0 * * * ?", "检查对象状态修复"); // 每天0点执行检查对象检查状态修复
	await ScheduleJobAsync<CheckObjectGpsFixJob>(scheduler, "0 0 * * * ?", "检查对象GPS修复");     // 每天0点执行检查对象GPS修复
	await ScheduleJobAsync<GisMenuStatJob>(scheduler, "0 0 * * * ?", "菜单统计修复");         // 每天0点执行菜单统计修复

	// 启动调度器
	await scheduler.Start();
	Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 调度器已启动，当前等待任务触发");

	// 等待退出信号
	Console.WriteLine("按 Ctrl+C 退出。");
	using var quitEvent = new ManualResetEventSlim(false);
	Console.CancelKeyPress += (_, e) =>
	{
		e.Cancel = true;
		quitEvent.Set();
	};
	quitEvent.Wait();
	await scheduler.Shutdown(waitForJobsToComplete: true);
}

/// <summary>注册任务并打印下一次触发时间。</summary>
static async Task ScheduleJobAsync<TJob>(IScheduler scheduler, string cron, string title) where TJob : IJob
{
	var job = JobBuilder.Create<TJob>().Build();
	var trigger = TriggerBuilder.Create().WithCronSchedule(cron).Build();
	await scheduler.ScheduleJob(job, trigger);
	var next = trigger.GetNextFireTimeUtc()?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
	Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 已注册任务: {typeof(TJob).Name} / {title} / Cron={cron} / Next={next}");
}




//---------------------------------------------------------------
// 以下是一些辅助方法
//---------------------------------------------------------------
/// <summary>添加路径到候选列表</summary>
static void AddPath(List<string> paths, string path)
{
	if (string.IsNullOrWhiteSpace(path))
		return;
	paths.Add(Path.GetFullPath(path));
}


/// <summary>解析默认数据库路径</summary>
static string ResolveDefaultDbPath()
{
	// 候选路径
	var candidates = new List<string>();
	AddPath(candidates, Path.Combine(Directory.GetCurrentDirectory(), "App", "Db", "sqlite.db"));
	AddPath(candidates, Path.Combine(AppContext.BaseDirectory, "App", "Db", "sqlite.db"));
	var current = new DirectoryInfo(AppContext.BaseDirectory);
	for (var i = 0; i < 8 && current != null; i++)
	{
		AddPath(candidates, Path.Combine(current.FullName, "App", "Db", "sqlite.db"));
		current = current.Parent;
	}

	// 选择第一个存在的路径
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

/// <summary>配置实体访问上下文</summary>
static void ConfigureEntity(string conn)
{
	var options = new DbContextOptionsBuilder<AppPlatContext>()
		.UseSqlite(conn)
		.Options;
	var db = new AppPlatContext(options);
	EntityConfig.Instance.OnGetDb += () => db;
	// 说明：已移除全局自动数据权限过滤器（OnGetDataAccessScope / DataAccessFilter.Apply）。
	// 各 Job 中若需要按组织收敛数据，应显式通过 OrgScopeHelper.FilterByOrgScope 过滤。
	EntityConfig.Instance.OnGetDataAuditScope += () => new DataAuditScope
	{
		Enabled = false,
	};
}

/// <summary>简单的命令行参数 → Job 参数的 AsyncLocal 传递容器</summary>
public static class JobExtras
{
	public static readonly AsyncLocal<Dictionary<string, string>?> _current = new();
	public static Dictionary<string, string>? Current { get => _current.Value; set => _current.Value = value; }
}
