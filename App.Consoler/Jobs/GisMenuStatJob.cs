using Quartz;
using App.DAL;
using App.DAL.GIS;

/// <summary>
/// GisMenu 统计任务（独立）
/// </summary>
public class GisMenuStatJob : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        var count = GisMenu.FixAll();
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] GisMenu.FixAll 执行完成，更新菜单数: {count}");
        return Task.CompletedTask;
    }
}
