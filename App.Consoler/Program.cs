using App.DAL;
using App.Entities;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Quartz.Impl;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;


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

// 检测直接启动任务参数
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

	// 直接运行指定任务
	await job.Execute(default!);
	Console.WriteLine($"任务 {run} 执行完成");
	return;
}

// 启动调度任务
await RunSchedulerAsync();


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
		_ => null,
	};
}

//---------------------------------------------------------------
// 以下是调度任务的实现
//---------------------------------------------------------------
/// <summary>启动Quartz调度任务</summary>
/// <param name="conn"></param>
/// <param name="cron"></param>
/// <param name="apiBaseUrl"></param>
/// <param name="checkObjectLimit"></param>
/// <param name="checkObjectIntervalMs"></param>
/// <param name="amapKey"></param>
/// <returns></returns>
static async Task RunSchedulerAsync()
{
	var factory = new StdSchedulerFactory();
	var scheduler = await factory.GetScheduler();

	// 定义调度任务和触发器（cron 格式：分 时 日 月 周）
	await scheduler.ScheduleJob(JobBuilder.Create<StatJob>().Build(),                 TriggerBuilder.Create().WithCronSchedule("0 0 * * * ?").Build());  // 每天0点执行报表统计
	await scheduler.ScheduleJob(JobBuilder.Create<ApiCheckJob>().Build(),             TriggerBuilder.Create().WithCronSchedule("0 0 * * * ?").Build());  // 每天0点执行接口检测与菜单统计
	await scheduler.ScheduleJob(JobBuilder.Create<CheckObjectStatusFixJob>().Build(), TriggerBuilder.Create().WithCronSchedule("0 0 * * * ?").Build());  // 每天0点执行检查对象检查状态修复
	await scheduler.ScheduleJob(JobBuilder.Create<CheckObjectGpsFixJob>().Build(),    TriggerBuilder.Create().WithCronSchedule("0 0 * * * ?").Build());  // 每天0点执行检查对象GPS修复
	await scheduler.ScheduleJob(JobBuilder.Create<GisMenuStatJob>().Build(),          TriggerBuilder.Create().WithCronSchedule("0 0 * * * ?").Build());  // 每天0点执行菜单统计修复

	// 启动调度器
	await scheduler.Start();

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


//---------------------------------------------------------------
// 以下是一些辅助方法
//---------------------------------------------------------------
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
	Func<DbContext> onGetDb = () => db;
	Func<DataAccessScope> onGetScope = () => new DataAccessScope
	{
		Enabled = false,
		AllowAll = true,
	};
	EntityConfig.Instance.OnGetDb += onGetDb;
	EntityConfig.Instance.OnGetDataAccessScope += onGetScope;
	return db.Database.CanConnect();
}
