using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Caching;
using System.Web.SessionState;
using App.Utils;
using App.DAL;

namespace App.Middlewares
{
    /// <summary>
    /// 网站防护模块（IP黑名单、访问频率）
    /// </summary>
    public class DefenceModule : IHttpModule
    {
        public void Dispose() { }
        public void Init(HttpApplication application)
        {
            application.BeginRequest += (sender, e)=>
            {
                var ip = Asp.ClientIP;

                // IP 黑名单过滤
                if (IPFilter.IsBanned(ip))
                {
                    IO.Trace("BanIP : " + ip);
                    Logger.Log("BanIP", ip);
                    HttpContext.Current.Request.Abort();
                    return;
                }

                // 访问频率限制（10秒一个周期计算访问次数）
                if (SiteConfig.Instance.VisitDensity != null)
                {
                    if (VisitCounter.IsHeavy(ip, "", 10, SiteConfig.Instance.VisitDensity.Value * 10))
                    {
                        Logger.LogDb("OverFreqency");
                        IPFilter.Ban(ip, "访问过于频繁被禁", SiteConfig.Instance.BanMinutes);
                    }
                }
            };
        }

        


    }

}
