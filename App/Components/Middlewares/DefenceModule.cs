using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using App.Utils;
using App.DAL;
using App.Components;
using App.Web;

namespace App.Middlewares
{
    /// <summary>
    /// 网站防护中间件（IP黑名单、访问频率）
    /// 若超过访问频次阈值，则自动封禁该 IP 一段时间。
    /// </summary>
    public class DefenceMiddleware
    {
        private readonly RequestDelegate _next;

        public DefenceMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>执行请求防护逻辑</summary>
        public async Task Invoke(HttpContext context)
        {
            var ip = Asp.ClientIP;

            // IP 黑名单过滤：命中则立即中断，不再进入后续管道
            if (IPFilter.IsBanned(ip))
            {
                IO.Trace("BanIP : " + ip);
                Logger.LogDb(LogLevel.Error, "OverFreqency", "BanIP", ip);
                context.Abort();
                return;
            }

            // 访问频率限制（10秒一个周期计算访问次数）
            if (SiteConfig.Instance.VisitFreqency != null)
            {
                if (VisitCounter.IsHeavy(ip, "", 10, SiteConfig.Instance.VisitFreqency.Value * 10))
                {
                    Logger.LogDb(LogLevel.Error, "OverFreqency", "访问过于频繁被禁");
                    IPFilter.Ban(ip, "访问过于频繁被禁", SiteConfig.Instance.BanMinutes);
                }
            }

            await _next(context);
        }
    }

    /// <summary>
    /// 防护中间件扩展方法。
    /// 使用方式：app.UseDefence();
    /// </summary>
    public static class DefenceMiddlewareExtension
    {
        /// <summary>启用网站防护中间件（请确保已使用 services.AddHttpContextAccessor()）</summary>
        public static IApplicationBuilder UseDefence(this IApplicationBuilder app)
        {
            return app.UseMiddleware<DefenceMiddleware>();
        }
    }
}
