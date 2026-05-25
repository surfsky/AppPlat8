using Quartz;
using App.DAL;

/// <summary>
/// 检查对象检查状态修复任务
/// </summary>
public class CheckObjectStatusFixJob : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
		var conn = context.MergedJobDataMap.GetString("conn");
		var cnt = CheckObject.FixAll();
		Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 修复了{cnt}个检查对象");
		return Task.CompletedTask;
    }
}
