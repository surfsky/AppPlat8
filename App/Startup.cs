using System;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using App.HttpApi;
using App.Middlewares;
using App.Web;
using App.DAL;
using App.Components;
using App.Entities;
using App.Pages.Chats;

namespace App
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// IOC services configuration
        /// </summary>
        public void ConfigureServices(IServiceCollection services)
        {
            Logger.Info("server start");
            services.AddHttpContextAccessor();                  // HttpContext
            services.AddDistributedMemoryCache();               // 

            // Session
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromHours(12);
            });
            services.AddControllersWithViews().AddRazorRuntimeCompilation();  // MVC + Razor Runtime Compilation
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
            {
                options.LoginPath = new PathString("/Login");
                options.Cookie.HttpOnly = true;
            });
            services.Configure<FormOptions>(options =>
            {
                options.ValueCountLimit = 1024;                 // 1024
                options.ValueLengthLimit = 4194304;             // 4194304 = 1024 * 1024 * 4
            });
            services.AddRazorPages().AddNewtonsoftJson();       // JSON settings for Razor Pages.TODO: 如何设置序列化的一些规则，如日期格式、枚举格式、CamelCase等？
            services.AddServerSideBlazor();                     // Blazor
            services.AddBootstrapBlazor();                      // BootstrapBlazor
            services.AddSignalR(op =>
            {
                //op.KeepAliveInterval = new TimeSpan(0, 1, 0);  // 1 min
                op.ClientTimeoutInterval = new TimeSpan(0, 0, 20);  // 20 sec
            });


            // db
            var sqlite = Configuration.GetConnectionString("Sqlite");
            services.AddDbContext<AppPlatContext>(options => {
                options.UseSqlite(sqlite, builder => builder.MigrationsAssembly("App"));
            });

            // EntityBase
            EntityConfig.Instance.OnGetDb += () => Common.GetDbConnection();
        }

        /// <summary>
        /// Http pipeline configuration
        /// </summary>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // 异常处理
            app.UseExceptionCatch(ex => Logger.Error("Exception: {0}\r\n{1}", ex.Message, ex.StackTrace));  // 全局异常捕获中间件
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();                         // 开发环境异常页面中间件，显示详细的异常信息和堆栈跟踪
            }
            else
            {
                app.UseExceptionHandler("/Error");                       // 生产环境异常处理页面中间件，重定向到 /Error 页面
                app.UseStatusCodePagesWithRedirects("/Error?code={0}");  // 状态码页面中间件，重定向到 /Error 页面并传递状态码
            }

            // 文件和授权（顺序不要动）
            app.UseSession();                               // 会话状态管理
            app.UseImager();                                // 图像处理中间件：缓存、缩放等
            app.UseStaticFiles();                           // 静态文件
            app.UseRouting();                               // 路由
            app.UseAuthentication();                        // 认证
            app.UseAuthorization();                         // 授权
 
            // 自定义中间件
            app.UserAppWeb(env.ContentRootPath);            // 注册后，可用 Asp.Current, Asp.User, Asp.Response 等静态属性获取当前请求的上下文信息
            app.UseMonitor(o => Console.WriteLine("{0} {1} {2}", o.Url, o.Seconds, o.ClientIP));  // 监控网页访问情况，输出访问的 URL、耗时和客户端 IP 地址
            app.UseHttpApi(o =>                             // HttpApi 配置（见 /Apis 目录）
            {
                o.TypePrefix = "App.API.";
                o.FormatEnum = EnumFomatting.Int;
                o.OnVisit += (ctx, method, attr, inputs) => Logger.Info("{0} {1} from {2}", ctx.Request.Method, ctx.Request.GetFullUrl(), ctx.Connection.RemoteIpAddress);
            });

            // 终端路由配置
            app.UseWebSockets();                            // WebSocket SignalR
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();                  // 启用 Razor Pages 路由支持
                endpoints.MapHub<ChatHub>("/ChatHub");      // 注册 SignalR 的 ChatHub 集线器，指定访问路径 /ChatHub
                endpoints.MapBlazorHub();                   // 启用 Blazor 应用的 SignalR 通信路由（Blazor Server 核心）
                endpoints.MapControllers();                 // 启用 MVC 控制器的路由支持（见 /Controllers 目录）
            });
        }
    }
}
