using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using App.HttpApi;
using App.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace App.HttpApi.Test
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpContextAccessor();                  // ע�� HttpContext ����
            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
            });
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddResponseCaching();
            services.AddResponseCompression();
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();     // ����HttpContext����������

            // ��֤��https://www.cnblogs.com/RainingNight/archive/2017/09/26/7512903.html��
            //services.AddAuthentication(options =>
            //{
            //    options.DefaultAuthenticateScheme   = CookieAuthenticationDefaults.AuthenticationScheme;
            //    options.DefaultSignInScheme         = CookieAuthenticationDefaults.AuthenticationScheme;
            //    options.DefaultSignOutScheme        = CookieAuthenticationDefaults.AuthenticationScheme;
            //    options.DefaultForbidScheme         = CookieAuthenticationDefaults.AuthenticationScheme;
            //    options.DefaultChallengeScheme      = OpenIdConnectDefaults.AuthenticationScheme;
            //})
            //.AddCookie()
            //.AddOpenIdConnect(o =>
            //{
            //    o.ClientId = "server.hybrid";
            //    o.ClientSecret = "secret";
            //    o.Authority = "https://demo.identityserver.io/";
            //    o.ResponseType = OpenIdConnectResponseType.CodeIdToken;
            //})
            ;

            services.AddRazorPages();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            //var accessor = app.ApplicationServices.GetRequiredService<IHttpContextAccessor>();

            // ��Map�������м��
            //app.Map("/map1", Handler1);
            //app.Map("/map2", Handler2);
            //app.Map("/map3", Handler3);
            //app.MapWhen(context => context.Request.Query.ContainsKey("branch"), HandleBranch);  // ����·��

            // Ĭ���м��
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
            else
                app.UseExceptionHandler("/Error");

            // jiang 
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(env.ContentRootPath + "\\Files"),
                RequestPath = "/Files"
            });

            //app.UseAuthentication();       // Authenticate before you access
            app.UseResponseCaching();      //
            app.UseResponseCompression();  // 
            app.UseCookiePolicy();         // ..
            app.UseRouting();
            //app.UseRouter();             //
            app.UseSession();              // ����Session֧��

            // ʹ���Զ�����м��
            //app.UseRequestCulture();
            //app.UseMiddleware<RequestCultureMiddleware>();
            //app.UseMiddleware<ValidateBrowserMiddleware>();

            //
            //app.UseAuthorize();
            //HttpApiConfig.Configure(app.ApplicationServices.GetRequiredService<IHttpContextAccessor>());  // ��services�л�ȡ�������󣬲�ʹ��֮
            app.UseHttpApi(o =>                        // ���� HttpApi
            {
                o.TypePrefix = "App.API.";
                o.FormatEnum = EnumFomatting.Int;
            });

            //app.UseHttpApi(accessor);
            //Asp.Configure(accessor, env.ContentRootPath);
            app.UseEndpoints(endpoints => endpoints.MapRazorPages());
        }


        //
        // handler(middleware)
        //
        private static void Handler1(IApplicationBuilder app)
        {
            app.Run(async context =>
            {
                await context.Response.WriteAsync("method1");
            });
        }
        private static void Handler2(IApplicationBuilder app)
        {
            app.Run(async context =>
            {
                await context.Response.WriteAsync("method2");
            });
        }
        private static void Handler3(IApplicationBuilder app)
        {
            app.Use(async (context, next) =>
            {
                await context.Response.WriteAsync("method3");
                await next.Invoke();
            });
            app.Run(async context =>
            {
                await context.Response.WriteAsync("method4");
            });
        }

        private static void HandleBranch(IApplicationBuilder app)
        {
            app.Run(async context =>
            {
                var branchVer = context.Request.Query["branch"];
                await context.Response.WriteAsync($"Branch used = {branchVer}");
            });
        }
    }
}
