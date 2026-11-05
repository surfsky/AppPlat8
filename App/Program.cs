using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using App.DAL;
using App.Pages.Maintains;
using App.Utils;
using App.Components;

namespace App
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // 构建 Host
            var builder = Host
                .CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.ConfigureAppConfiguration((ctx, _) =>
                    {
                        Paths.Load(); //.SetContentRoot(ctx.HostingEnvironment.ContentRootPath);   // 增加 appsettings.json 中的相关路径解析
                    });
                });
            var host = builder.Build();

            // 确保 4 个数据目录存在
            var summary = Paths.Summary();
            Logger.Info(summary);
            CreateDbIfNotExists(host);

            // 随站点生命周期自动拉起/结束控制台调度器，避免忘记启动，也避免退出时留下孤儿进程
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            if (host.Services.GetRequiredService<IConfiguration>().GetValue<bool>("Consoler:AutoStart"))
            {
                lifetime.ApplicationStarted.Register(() => StartConsolerOnBoot(host));
                lifetime.ApplicationStopping.Register(StopConsolerOnShutdown);
            }

            // 启动站点
            host.Run();
        }

        /// <summary>站点启动完成后自动拉起控制台调度器（App.Consoler）。</summary>
        private static void StartConsolerOnBoot(IHost host)
        {
            Task.Run(() =>
            {
                try
                {
                    var apiBaseUrl = ResolveApiBaseUrl(host);
                    ConsolerRunner.Start(apiBaseUrl, out var error);
                    if (string.IsNullOrWhiteSpace(error))
                        Components.Logger.Info("[Consoler] 调度器已随站点自动启动，ApiBase={0}", apiBaseUrl);
                    else
                        Components.Logger.Warn("[Consoler] 调度器自动启动失败：{0}", error);
                }
                catch (Exception ex)
                {
                    Components.Logger.Warn("[Consoler] 调度器自动启动异常：{0}", ex.Message);
                }
            });
        }

        /// <summary>站点退出时结束调度器进程，避免留下孤儿进程。</summary>
        private static void StopConsolerOnShutdown()
        {
            try
            {
                ConsolerRunner.Stop(out _);
            }
            catch (Exception ex)
            {
                Components.Logger.Warn("[Consoler] 调度器停止异常：{0}", ex.Message);
            }
        }

        /// <summary>取站点实际监听地址，作为调度器回调 API 的基础地址。</summary>
        private static string ResolveApiBaseUrl(IHost host)
        {
            var addresses = host.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()?.Addresses;
            var url = addresses?.FirstOrDefault(t => t.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                      ?? addresses?.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(url))
                return "http://localhost:6001";
            return url
                .Replace("0.0.0.0", "localhost")     // 通配地址不能作为客户端回调地址
                .Replace("[::]", "localhost")
                .Replace("+", "localhost")
                .Replace("*", "localhost")
                .TrimEnd('/');
        }


        // https://docs.microsoft.com/zh-cn/aspnet/core/data/ef-rp/intro
        private static void CreateDbIfNotExists(IHost host)
        {
            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    var context = services.GetRequiredService<AppPlatContext>();
                    AppPlatContextInitializer.Initialize(context);
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An error occurred creating the DB.");
                }
            }
        }
    }
}
