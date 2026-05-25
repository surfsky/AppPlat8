using Quartz;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// 接口检测与菜单统计任务
/// </summary>
public class ApiCheckJob : IJob
{	
	/// <summary>执行接口检测与菜单统计的调度任务</summary>
	public Task Execute(IJobExecutionContext context)
	{
		Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 开始执行接口检测与菜单统计");
		ApiChecker.RunOnce();
		Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 调度任务执行完成");
		return Task.CompletedTask;
	}
}



/// <summary>
/// GIS API 同步任务运行器
/// </summary>
public static class ApiChecker
{
	/// <summary>运行一次 API 检测和统计任务</summary>
	public static void RunOnce()
	{
		// 遍历接口表，调用接口并更新统计数据
		foreach (var api in GisApi.ValidSet.ToList())
		{
			try
            {
                int cnt = CheckApi(api);
				if (cnt < 0)
				{
					api.IsLive = false;
				}
				else
				{
					api.DataCnt = cnt;
					api.DataDt = DateTime.Now;
					api.LastErr = null;
					api.IsLive = true;
				}
                api.Save();
                GisApi.RefreshStats(api.MenuId);
                var menuCount = GisMenu.FixAll();
                Console.WriteLine($"接口 {api.Name} ({api.Id}) 检测完成，数据量: {cnt}, 菜单统计更新数: {menuCount}");
            }
            catch (Exception ex)
			{
				Console.WriteLine($"访问接口 {api.Name} ({api.Id}) 时发生异常: {ex.Message}");
			}
		}
	}

    private static int CheckApi(GisApi api)
    {
        var cnt = -1;
        var isLive = false;
        var url = api.DataUrl;
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            url = "http://" + url;

        var response = new HttpClient() { Timeout = TimeSpan.FromSeconds(10) }.GetAsync(url).Result;
        isLive = response.IsSuccessStatusCode;
        if (isLive)
        {
            var content = response.Content.ReadAsStringAsync().Result;
            int.TryParse(content, out cnt);
        }
        return cnt;
    }
}
