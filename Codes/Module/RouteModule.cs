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
    /*
    <system.webServer>
      <modules>
        <add name="RouteModule" type="App.Components.RouteModule" />
      </modules>
    /<system.webServer>
    <RewriterRule>
        <LookFor>~/Article/(\d+) </LookFor>
        <SendTo>~/Article.aspx?id=$1 </SendTo>
    < RewriterRule>
    * 
    */
    /// <summary>
    /// 数据库路由方案
    /// </summary>
    public class RouteModule : IHttpModule
    { 
        /// <summary>判断指定字符串和正则表达式是否匹配</summary>
        bool IsMatch(string text, string regex)
        {
            return new Regex(regex, RegexOptions.IgnoreCase).IsMatch(text);
        }

        /// <summary>将定字符串用匹配正则表达式解析，并用替代正则表达式转换</summary>
        string ReplaceRegex(string text, string matchRegex, string replaceRegex)
        {
            return Regex.Replace(text, matchRegex, replaceRegex, RegexOptions.IgnoreCase);
        }

        public void Dispose() { }
        public void Init(HttpApplication application)
        {
            application.BeginRequest += (sender, e)=>
            {
                var uri = HttpContext.Current.Request.Url;
                foreach (Route route in Route.All)
                {
                    // 路径路由
                    if (route.Type == RouteType.Path)
                    {
                        if (IsMatch(uri.PathAndQuery, route.From))
                        {
                            var path = ReplaceRegex(uri.PathAndQuery, route.From, route.To);
                            HttpContext.Current.RewritePath(path);  // URL重写，地址栏不变动
                            return;
                        }
                    }
                    // 协议路由（可以强制使用https）
                    else if (route.Type == RouteType.Protocol)
                    {
                        if (IsMatch(uri.Scheme, route.From))
                        {
                            var protocol = ReplaceRegex(uri.Scheme, route.From, route.To);
                            var url = string.Format("{0}://{1}{2}", protocol, uri.Authority, uri.PathAndQuery);
                            HttpContext.Current.Response.Redirect(url);  // 地址跳转
                            return;
                        }
                    }
                    // 主机路由（可以做负载均衡）
                    else if (route.Type == RouteType.Host)
                    {
                        if (IsMatch(uri.Host, route.From))
                        {
                            var host = ReplaceRegex(uri.Host, route.From, route.To);
                            var url = string.Format("{0}://{1}{2}", uri.Scheme, host, uri.PathAndQuery);
                            HttpContext.Current.Response.Redirect(url);  // 地址跳转
                            return;
                        }
                    }
                }
            };
        }

    }

}
