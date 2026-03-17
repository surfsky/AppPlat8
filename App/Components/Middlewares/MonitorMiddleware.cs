using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using App.Utils;
using System.Threading.Tasks;
using App.Web;
using System.Threading;
using Microsoft.AspNetCore.Builder;

namespace App.Middlewares
{
    /// <summary>监控信息</summary>
    public class MonitorInfo
    {
        public string Url { get; set; }
        public DateTime RequestDt { get; set; }
        public double Seconds { get; set; }
        public string ClientIP { get; set; }

        public override string ToString()
        {
            return string.Format("{0:yyyy-MM-dd HH:mm:ss:fff} 请求 {1}, IP地址 {2}, 耗时 {3:F4} 秒", RequestDt, Url, ClientIP, Seconds);
        }
    }

    /// <summary>
    /// 网站访问监控中间件。 
    /// 使用方法：app.UseMonitor(...);
    /// </summary>
    public class MonitorMiddleware
    {
        private RequestDelegate _next;
        public MonitorMiddleware(RequestDelegate next)
        {
            _next = next;
        }
        public async Task Invoke(HttpContext context)
        {
            // start
            var watch = new Stopwatch();
            watch.Start();

            // next
            await _next.Invoke(context);

            // end
            watch.Stop();
            var info = new MonitorInfo()
            {
                //Url = context.Request.Path.ToString(),
                Url = context.Request.GetFullUrl(),
                RequestDt = DateTime.Now,
                Seconds = watch.ElapsedMilliseconds / 1000.0,
                ClientIP = Asp.ClientIP
            };

            // 登陆相关网页和接口去除Querystring，避免信息泄露
            if (info.Url.ToLower().Contains("login"))
                info.Url = info.Url.TrimQuery();

            // 输出日志
            MonitorMiddlewareExtension.Callback.Invoke(info);
        }
    }

    /// <summary>
    /// 使用监控中间件。
    /// app.UseMonitor(options => Logger.Log(...);
    /// </summary>
    public static class MonitorMiddlewareExtension
    {
        public static Action<MonitorInfo> Callback;

        /// <summary>使用中间件（请确保已使用 services.AddHttpContextAccessor())</summary>
        public static IApplicationBuilder UseMonitor(this IApplicationBuilder app, Action<MonitorInfo> options)
        {
            Callback = options;
            return app.UseMiddleware<MonitorMiddleware>();
        }
    }




}
