using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using App.DAL;
using App.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace App
{
    public class Program
    {
        public static void Main(string[] args)
        {

            var builder = Host
                .CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => {
                    webBuilder.UseStartup<Startup>();
                    // 在 Host 构建前把 ContentRootPath 注入给 Paths（最准）
                    webBuilder.ConfigureAppConfiguration((ctx, _) =>
                    {
                        Paths.SetContentRoot(ctx.HostingEnvironment.ContentRootPath);   // 增加 appsettings.json 中的相关路径解析
                    });
                });
            var host = builder.Build();
            // 启动早期：确保 4 个目录存在 + 做写测试 + 打印汇总到 Console（Logger 此时可能还没完全初始化，写 Console 更稳）
            var summary = Paths.EnsureAll(testWritable: true);
            Console.WriteLine(summary);
            Components.Logger.Info(summary);
            CreateDbIfNotExists(host);
            host.Run();
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
