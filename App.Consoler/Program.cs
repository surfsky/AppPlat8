using App.DAL;
using App.Entities;
using Quartz;
using Quartz.Impl;
using Microsoft.EntityFrameworkCore;
namespace App.Consoler;

/// <summary>App.Consoler 程序入口：负责配置解析、数据库连接、任务调度。</summary>
public static class Program
{
    /// <summary>程序入口</summary>
    public static async Task Main(string[] args)
    {
        var config = ConsolerConfig.Parse(args);
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 调度器启动中");
        Console.WriteLine($"配置文件: {ConsolerConfig.ConfigFilePath}");
        Console.WriteLine($"API Base Url: {config.ApiBase}");
        Console.WriteLine($"数据库连接: {config.Conn}");

        // 数据库连接校验：连不上直接退出，避免后续任务空跑
        if (!CanConnect(config.Conn))
        {
            Console.WriteLine($"数据库连接失败: {config.Conn}");
            return;
        }

        ConfigureEntity(config.Conn);

        // --run=JobName：单次执行指定任务，不启动调度器
        if (!string.IsNullOrWhiteSpace(config.Run))
        {
            await RunSingleJobAsync(config);
            return;
        }

        // 默认：启动 Quartz 常驻调度
        await RunSchedulerAsync();
    }


    //---------------------------------------------------------------
    // 单次任务
    //---------------------------------------------------------------
    /// <summary>执行一次指定任务（--run=JobName）</summary>
    static async Task RunSingleJobAsync(ConsolerConfig config)
    {
        var job = CreateJob(config.Run);
        if (job == null)
        {
            Console.WriteLine($"任务 {config.Run} 不存在");
            return;
        }

        JobExtras.Current = config.Extras;
        var context = new FakeJobExecutionContext(config.Extras);
        await job.Execute(context);
        JobExtras.Current = null;
        Console.WriteLine($"任务 {config.Run} 执行完成");
    }

    /// <summary>按名称创建任务实例</summary>
    static IJob? CreateJob(string jobName)
    {
        return (jobName ?? "").Trim().ToLower() switch
        {
            "statjob" => new StatJob(),
            "apicheckjob" => new ApiCheckJob(),
            "checkobjectstatusfixjob" => new CheckObjectStatusFixJob(),
            "checkobjectgpsfixjob" => new CheckObjectGpsFixJob(),
            "gismenustatjob" => new GisMenuStatJob(),
            "typhoonimportjob" => new TyphoonImportJob(),
            "typhoonjob" => new TyphoonJob(),
            "checkerusermergejob" => new CheckerUserMergeJob(),
            _ => null,
        };
    }


    //---------------------------------------------------------------
    // 常驻调度
    //---------------------------------------------------------------
    /// <summary>启动 Quartz 调度器并常驻等待退出</summary>
    static async Task RunSchedulerAsync()
    {
        var factory = new StdSchedulerFactory();
        var scheduler = await factory.GetScheduler();

        // 定义调度任务和触发器（cron 格式：秒 分 时 日 月 周）
        await ScheduleJobAsync<HeartBeat>(scheduler, "*/20 * * * * ?", "心跳任务"); // 每 20 秒执行一次心跳
        await ScheduleJobAsync<StatJob>(scheduler, "0 0 * * * ?", "报表统计"); // 每天 0 点执行报表统计
        await ScheduleJobAsync<ApiCheckJob>(scheduler, "0 0 * * * ?", "接口检测与菜单统计"); // 每天 0 点执行接口检测与菜单统计
        await ScheduleJobAsync<CheckObjectStatusFixJob>(scheduler, "0 0 * * * ?", "检查对象状态修复"); // 每天 0 点执行检查对象状态修复
        await ScheduleJobAsync<CheckObjectGpsFixJob>(scheduler, "0 0 * * * ?", "检查对象GPS修复"); // 每天 0 点执行检查对象GPS修复
        await ScheduleJobAsync<GisMenuStatJob>(scheduler, "0 0 * * * ?", "菜单统计修复"); // 每天 0 点执行菜单统计修复
        await ScheduleJobAsync<TyphoonJob>(scheduler, "0 */5 * * * ?", "台风数据采集"); // 每 5 分钟采集最近的台风轨迹

        await scheduler.Start();
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 调度器已启动，当前等待任务触发");
        Console.WriteLine("按 Ctrl+C 退出。");

        await WaitForExitSignalAsync();
        await scheduler.Shutdown(waitForJobsToComplete: true);
    }

    /// <summary>注册任务并打印下一次触发时间</summary>
    static async Task ScheduleJobAsync<TJob>(IScheduler scheduler, string cron, string title) where TJob : IJob
    {
        var job = JobBuilder.Create<TJob>().Build();
        var trigger = TriggerBuilder.Create().WithCronSchedule(cron).Build();
        await scheduler.ScheduleJob(job, trigger);
        var next = trigger.GetNextFireTimeUtc()?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
        Console.WriteLine(
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 已注册任务: {typeof(TJob).Name} / {title} / Cron={cron} / Next={next}");
    }

    /// <summary>等待 Ctrl+C 退出信号</summary>
    static Task WaitForExitSignalAsync()
    {
        var tcs = new TaskCompletionSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            tcs.TrySetResult();
        };
        return tcs.Task;
    }


    //---------------------------------------------------------------
    // 数据库
    //---------------------------------------------------------------
    /// <summary>检查数据库连接是否成功</summary>
    static bool CanConnect(string conn)
    {
        var options = new DbContextOptionsBuilder<AppPlatContext>().UseSqlite(conn).Options;
        using var db = new AppPlatContext(options);
        return db.Database.CanConnect();
    }

    /// <summary>配置实体访问上下文</summary>
    static void ConfigureEntity(string conn)
    {
        var options = new DbContextOptionsBuilder<AppPlatContext>().UseSqlite(conn).Options;
        var db = new AppPlatContext(options);
        EntityConfig.Instance.OnGetDb += () => db;
        // 说明：已移除全局自动数据权限过滤器（OnGetDataAccessScope / DataAccessFilter.Apply）。
        // 各 Job 中若需要按组织收敛数据，应显式通过 OrgScopeHelper.FilterByOrgScope 过滤。
        EntityConfig.Instance.OnGetDataAuditScope += () => new DataAuditScope
        {
            Enabled = false,
        };
    }
}