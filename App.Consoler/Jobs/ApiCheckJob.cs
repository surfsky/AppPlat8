using Quartz;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using Microsoft.EntityFrameworkCore;
namespace App.Consoler;

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
	static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };

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
		var url = ResolveDataUrl(api.DataUrl, ConsolerConfig.Instance.ApiBase);
		using var response = Client.GetAsync(url).GetAwaiter().GetResult();
		if (!response.IsSuccessStatusCode)
			return -1;

		var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
		return int.TryParse(content, out var cnt) && cnt >= 0 ? cnt : -1;
    }

	/// <summary>绝对地址直接使用；无协议地址按站点相对路径处理。</summary>
	internal static string ResolveDataUrl(string? dataUrl, string apiBase)
	{
		var url = dataUrl?.Trim();
		if (string.IsNullOrEmpty(url))
			throw new ArgumentException("接口地址不能为空。", nameof(dataUrl));

		if (url.Contains("://"))
		{
			if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
				(uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
				throw new ArgumentException("接口地址必须使用 HTTP 或 HTTPS。", nameof(dataUrl));
			return url;
		}

		return apiBase.TrimEnd('/') + "/" + url.TrimStart('/');
	}
}
