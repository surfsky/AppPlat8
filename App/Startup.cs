using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
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
using System.IO;
using System.Text.Json.Serialization;
using App.Utils;

namespace App
{
    public class Startup
    {
        // 防止实体审计回调里写 Log 实体再次触发 OnEntityAudit 产生自激循环
        private static readonly AsyncLocal<bool> _writingAudit = new AsyncLocal<bool>();

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
            services.AddControllersWithViews()
                .AddRazorRuntimeCompilation()
                .AddJsonOptions(options =>
                {
                    // Keep compatibility with legacy payloads that send enum names as strings.
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                });  // MVC + Razor Runtime Compilation
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
            {
                options.LoginPath = new PathString("/Login");
                options.Cookie.HttpOnly = true;
            });
            services.Configure<FormOptions>(options =>
            {
                options.ValueCountLimit = 2048;                 // 2048
                options.ValueLengthLimit = 4194304;             // 4194304 = 1024 * 1024 * 4
                options.MultipartBodyLengthLimit = 2147483648;  // 2GB，支持批量目录上传
            });
            services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = 2147483648; // 2GB
            });
            services.AddRazorPages(options =>
            {
                // GIS: 新路由语义化，同时兼容旧页面路径
                //options.Conventions.AddPageRoute("/GIS/Regions", "GIS/Geometries");
                //options.Conventions.AddPageRoute("/GIS/RegionForm", "GIS/GeometryForm");
            });
            services.AddServerSideBlazor();                     // Blazor
            services.AddBootstrapBlazor();                      // BootstrapBlazor
            services.AddSignalR(op =>
            {
                //op.KeepAliveInterval = new TimeSpan(0, 1, 0);  // 1 min
                op.ClientTimeoutInterval = new TimeSpan(0, 0, 20);  // 20 sec
            });

            SetDbService(services);
        }

        /// <summary>设置数据库服务</summary>
        private void SetDbService(IServiceCollection services)
        {
            // db
            var sqlite = Configuration.GetConnectionString("Sqlite");
            services.AddDbContext<AppPlatContext>(options =>
            {
                options.UseSqlite(sqlite, builder => builder.MigrationsAssembly("App"));
            });

            // EntityBase
            EntityConfig.Instance.OnGetDb += () => Common.GetDbConnection();
            
            // 实体变更审计：写 Logs 表（统一入口，宿主完全控制行为，低层类库不感知具体日志实现）
            EntityConfig.Instance.OnEntityAudit += (op, entity, message) =>
            {
                if (_writingAudit.Value) return;                         // 防重入：当前正在写 Log 实体，避免递归循环
                if (entity == null) return;
                var type = entity.GetType();
                if (!EntityAuditHelper.ShouldAudit(type)) return;        // 黑名单：Log/History/Att/Online 等不写

                // 启动期（如 EF Seed 初始化 / 后台 console 作业）没有 HTTP 上下文 → 直接跳过，
                // 否则会写一堆"操作者空 / IP 空"的脏日志（比如首次启动 Seed 初始用户/角色/字典等）。
                if (!Asp.IsWeb || !Asp.IsRequestOk)
                    return;

                try
                {
                    string action = op.GetTitle();
                    _writingAudit.Value = true;
                    Logger.LogAudit(type.Name, action, message ?? "");
                }
                finally
                {
                    _writingAudit.Value = false;
                }
            };

            // 数据权限范围：根据用户角色、组织、责任数据收敛。
            EntityConfig.Instance.OnGetDataAccessScope += () =>
            {
                var db = Common.GetDbConnection();
                var userId = Auth.GetUserId();
                if (db == null || !userId.HasValue)
                    return new DataAccessScope { Enabled = false, AllowAll = true };

                var user = db.Users.FirstOrDefault(t => t.Id == userId.Value);
                if (user == null)
                    return new DataAccessScope { Enabled = false, AllowAll = true };

                var hasAll = false;
                var hasOrg = false;
                var hasOwn = false;

                if (string.Equals(user.Name, "admin", StringComparison.OrdinalIgnoreCase))
                {
                    hasAll = true;
                    hasOrg = true;
                    hasOwn = true;
                }
                else
                {
                    var roleIds = db.Users
                        .Where(t => t.Id == user.Id)
                        .SelectMany(t => t.Roles)
                        .Select(t => t.Id)
                        .ToList();

                    var powerIds = db.RolePowers
                        .Where(t => roleIds.Contains(t.RoleId))
                        .Select(t => t.PowerId)
                        .ToList();

                    hasAll = powerIds.Contains(Power.DataAll);
                    hasOrg = powerIds.Contains(Power.DataUnit);
                    hasOwn = powerIds.Contains(Power.DataDuty);
                }

                // 无数据权限标识时默认按责任数据收敛，避免越权。
                if (!hasAll && !hasOrg && !hasOwn)
                    hasOwn = true;

                var orgId = user.OrgId;
                var authOrgIds = user.AuthOrgIds ?? new List<long>();
                if (authOrgIds.Count == 0)
                {
                    // 兼容：直接用当前 db 上下文查 UserOrgs
                    authOrgIds = db.UserOrgs
                        .Where(t => t.UserId == user.Id && t.OrgId != null)
                        .Select(t => t.OrgId.Value)
                        .Distinct()
                        .ToList();
                }
                long? primaryAuthOrgId = authOrgIds.FirstOrDefault(t => t > 0);
                if (primaryAuthOrgId <= 0) primaryAuthOrgId = null;

                return new DataAccessScope
                {
                    Enabled = true,
                    AllowAll = hasAll,
                    AllowOrg = hasOrg,
                    AllowOwn = hasOwn,
                    UserId = user.Id,
                    OrgId = primaryAuthOrgId ?? orgId,
                    IncludeSubOrgs = true,
                };
            };

            // 数据审计权限（看不懂，和OnGetDataAccessScope 的区别？）
            EntityConfig.Instance.OnGetDataAuditScope += () =>
            {
                var db = Common.GetDbConnection();
                var userId = Auth.GetUserId();
                if (db == null || !userId.HasValue)
                    return new DataAuditScope { Enabled = false };

                var user = db.Users.FirstOrDefault(t => t.Id == userId.Value);
                if (user == null)
                    return new DataAuditScope { Enabled = false };

                // 审计 OrgId：优先用 AuthOrgIds.First 或 UserOrgs 首个，空时用所属组织
                var auditAuthOrgIds = user.AuthOrgIds ?? new List<long>();
                if (auditAuthOrgIds.Count == 0)
                {
                    auditAuthOrgIds = db.UserOrgs
                        .Where(t => t.UserId == user.Id && t.OrgId != null)
                        .Select(t => t.OrgId.Value)
                        .Distinct()
                        .ToList();
                }
                long? auditPrimary = auditAuthOrgIds.FirstOrDefault(t => t > 0);
                if (auditPrimary <= 0) auditPrimary = null;

                return new DataAuditScope
                {
                    Enabled = true,
                    UserId = user.Id,
                    OrgId = auditPrimary ?? user.OrgId,
                };
            };
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

            // 允许通过 /Files/* 访问项目根目录 Files 下的上传文件。
            app.UseStaticFiles();

            // 允许下载Files目录下的各种静态文件
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(env.ContentRootPath, "Files")),
                RequestPath = "/Files",
                ContentTypeProvider = GetFileProvider(),
            });
            // 允许下载 Pages 目录下的普通静态文件
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(env.ContentRootPath, "Pages")),
                RequestPath = "/Pages"
            });

            //
            app.UseRouting();                               // 路由
            app.UseAuthentication();                        // 认证
            app.UseAuthorization();                         // 授权

            // 自定义中间件
            app.UserAppWeb(env.ContentRootPath);            // 注册后，可用 Asp.Current, Asp.User, Asp.Response 等静态属性获取当前请求的上下文信息
            app.UseMonitor(o => Logger.Info("[PAGE] {0} from {1} use {2}s", o.Url, o.ClientIP, o.Seconds));  // 监控网页访问情况，输出访问的 URL、耗时和客户端 IP 地址
            app.UseHttpApi(o =>                             // HttpApi 配置（代码见 /Apis 目录）
            {
                o.TypePrefix = "App.API.";
                o.FormatEnum = App.HttpApi.EnumFomatting.Int;
                o.FormatIndented = Formatting.Indented;
                o.FormatDateTime = "yyyy-MM-dd";
                o.FormatLowCamel = true;
                o.FormatLongNumber = "Int64,Decimal";
                o.Language = "en";
                o.OnVisit += args => Logger.Info("[API] {0} {1} from {2}", args.Context.Request.Method, args.Context.Request.GetFullUrl(), args.Context.Connection.RemoteIpAddress);
                o.OnBan += args => Logger.Warn("[BAN] {0} {1} from {2}", args.Context.Request.Method, args.Context.Request.GetFullUrl(), args.Context.Connection.RemoteIpAddress);
                o.OnAuth += args =>
                {
                    var path = args?.Context?.Request?.Path.Value ?? string.Empty;
                    if (path.Equals("/HttpApi/Gis/GetCheckObjects", StringComparison.OrdinalIgnoreCase))
                        return;
                    if (path.Equals("/HttpApi/Gis/GetCheckObjectPoints", StringComparison.OrdinalIgnoreCase))
                        return;

                    if (!AuthHelper.CheckHeaderAuth())
                        throw new HttpApiException(StatusCodes.Status401Unauthorized, "Unauthorized token");
                };
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

        private static FileExtensionContentTypeProvider GetFileProvider()
        {
            var provider = new FileExtensionContentTypeProvider();
            // Office / 文档
            provider.Mappings[".pdf"] = "application/pdf";
            provider.Mappings[".doc"] = "application/msword";
            provider.Mappings[".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            provider.Mappings[".xls"] = "application/vnd.ms-excel";
            provider.Mappings[".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            provider.Mappings[".ppt"] = "application/vnd.ms-powerpoint";
            provider.Mappings[".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
            provider.Mappings[".csv"] = "text/csv";
            provider.Mappings[".txt"] = "text/plain";
            provider.Mappings[".md"] = "text/markdown";
            provider.Mappings[".markdown"] = "text/markdown";
            provider.Mappings[".json"] = "application/json";
            provider.Mappings[".xml"] = "application/xml";
            provider.Mappings[".yaml"] = "application/x-yaml";
            provider.Mappings[".yml"] = "application/x-yaml";

            // 脑图
            provider.Mappings[".mm"] = "text/xml";
            provider.Mappings[".mmd"] = "text/plain";
            provider.Mappings[".xmind"] = "application/vnd.xmind.workbook";

            // 图片 / 全景
            provider.Mappings[".jpg"] = "image/jpeg";
            provider.Mappings[".jpeg"] = "image/jpeg";
            provider.Mappings[".png"] = "image/png";
            provider.Mappings[".gif"] = "image/gif";
            provider.Mappings[".webp"] = "image/webp";
            provider.Mappings[".bmp"] = "image/bmp";
            provider.Mappings[".svg"] = "image/svg+xml";

            // 视频
            provider.Mappings[".mp4"] = "video/mp4";
            provider.Mappings[".mov"] = "video/quicktime";
            provider.Mappings[".avi"] = "video/x-msvideo";
            provider.Mappings[".mkv"] = "video/x-matroska";
            provider.Mappings[".webm"] = "video/webm";
            provider.Mappings[".ogv"] = "video/ogg";
            provider.Mappings[".m4v"] = "video/x-m4v";
            provider.Mappings[".m3u8"] = "application/vnd.apple.mpegurl";

            // 模型
            provider.Mappings[".glb"] = "model/gltf-binary";
            provider.Mappings[".gltf"] = "model/gltf+json";
            provider.Mappings[".usdz"] = "model/vnd.usdz+zip";
            provider.Mappings[".obj"] = "model/obj";
            provider.Mappings[".fbx"] = "application/octet-stream";

            // 压缩包
            provider.Mappings[".zip"] = "application/zip";
            provider.Mappings[".rar"] = "application/vnd.rar";
            provider.Mappings[".7z"] = "application/x-7z-compressed";
            return provider;
        }
    }
}
