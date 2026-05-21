using Quartz;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using Microsoft.EntityFrameworkCore;

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
		GisApiRunner.RunOnce(conn, apiBaseUrl ?? string.Empty, checkObjectLimit, checkObjectIntervalMs, amapKey ?? string.Empty);
		Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 调度任务执行完成");
		return Task.CompletedTask;
	}
}



/// <summary>
/// GIS API 同步任务运行器
/// </summary>
public static class GisApiRunner
{
	/// <summary>运行一次GIS API同步任务</summary>
	/// <param name="conn">数据库连接字符串</param>
	/// <param name="apiBaseUrl">API 基础 URL</param>
	/// <param name="checkObjectLimit">检查对象限制</param>
	/// <param name="checkObjectIntervalMs">检查对象间隔（毫秒）</param>
	/// <param name="amapKey">高德地图 API Key</param>
	public static void RunOnce(string conn, string apiBaseUrl, int checkObjectLimit, int checkObjectIntervalMs, string amapKey)
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

	/// <summary>确保检查对象点位接口已存在于 GisApi 表中，并且启用状态正确</summary>
	/// <param name="apiBaseUrl"></param>
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
