using Quartz;
/// <summary>
/// 报表统计任务
/// </summary>
public class HeartBeat : IJob
{
	public Task Execute(IJobExecutionContext context)
	{
		Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 开始执行心跳任务");
		return Task.CompletedTask;
	}
}

