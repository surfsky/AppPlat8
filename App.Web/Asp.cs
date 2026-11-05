using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Web;
//using System.Web.Compilation;
//using System.Web.SessionState;
//using System.Web.UI;
//using System.Web.UI.HtmlControls;
using App.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace App.Web
{
    /// <summary>
    /// ASP.NET 网页相关辅助方法
    /// </summary>
    public static partial class Asp
    {
        /// <summary>
        /// 注册 HttpContextAccessor 单例服务。
        /// 可用 var accessor = app.ApplicationServices.GetRequiredService<IHttpContextAccessor>() 获取上下文对象
        /// </summary>
        public static void AddHttpContext(this IServiceCollection services)
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        }

        static IHttpContextAccessor _contextAccessor;
        static string _hostFolder { get; set; }

        /// <summary>配置App.Web参数（请确保已使用 services.AddHttpContextAccessor())</summary>
        public static void UserAppWeb(this IApplicationBuilder app, string hostFolder)
        {
            _contextAccessor = app.ApplicationServices.GetRequiredService<IHttpContextAccessor>();
            _hostFolder = hostFolder;
        }

        /// <summary>配置App.Web参数</summary>
        public static void Configure(IHttpContextAccessor contextAccessor, string hostFolder)
        {
            _contextAccessor = contextAccessor;
            _hostFolder = hostFolder;
        }


        //-------------------------------------
        // HttpContext
        //-------------------------------------
        public static HttpContext Current               => _contextAccessor.HttpContext;
        //public static HttpServerUtility Server        => Asp.Current.Server;
        public static ISession Session                  => Asp.Current.Session;
        public static ConnectionInfo Connection         => Asp.Current.Connection;
        //public static HttpApplicationState Application  => Asp.Current.Application;
        //public static Page Page                         => Asp.Current.Handler as Page;
        public static ClaimsPrincipal User              => Asp.Current.User;
        public static string Url                        => Asp.Current.Request.GetDisplayUrl(); 
        public static string RawUrl                     => Asp.Current.Request.GetDisplayUrl();
        public static string QueryString                => Url.GetQueryString();
        public static HttpResponse Response             => Asp.Current.Response;
        public static HttpRequest Request
        {
            get
            {
                try { return Asp.Current.Request; }
                catch { return null; }
            }
        }

        /// <summary>是否是网站运行环境</summary>
        public static bool IsWeb                        => Asp.Current != null;

        /// <summary>请求是否有效（避免触发“HttpRequest在上下文中不可用”的异常）</summary>
        public static bool IsRequestOk                  => Request != null;

        /// <summary>主机根路径（如http://www.abc.com:8080/）</summary>
        public static string Host
        {
            get
            {
                var req = Asp.Current.Request;
                return $"{req.Scheme}://{req.Host.Value}";
            }
        }

        /// <summary>获取请求的完整路径</summary>
        public static string GetFullUrl(this HttpRequest request)
        {
            return new StringBuilder()
                .Append(request.Scheme)
                .Append("://")
                .Append(request.Host)
                .Append(request.PathBase)
                .Append(request.Path)
                .Append(request.QueryString)
                .ToString();
        }

        /// <summary>获取服务器 IP</summary>
        public static string ServerIP => Current.Connection.LocalIpAddress.MapToIPv4().ToString();

        /// <summary>获取客户端真实IP</summary>
        public static string ClientIP
        {
            get
            {
                try   { return Current.Connection.RemoteIpAddress.ToString(); }
                catch { return ""; }
            }
        }

        /// <summary>
        /// 强行断开与客户端的socket连接。
        /// 只有代码发生错误（恶意的攻击），希望终止对于客户端的响应/连接时才可以使用Response.Close()
        /// </summary>
        public static void Close()
        {
            //Response.Close();
            Response.Body.Close();
        }
        


        //-------------------------------------------
        // Url & Path
        //-------------------------------------------
        /// <summary>是否是本网站文件（如果以.~/开头或host相同是本站图片）</summary>
        public static bool IsSiteFile(this string url)
        {
            if (url.IsEmpty())
                return false;
            if (url.StartsWith("/") || url.StartsWith("~/") || url.StartsWith("."))
                return true;
            url = Asp.ResolveUrl(url);
            Uri uri = new Uri(url);
            return uri.Host.ToLower() == Request.Host.Value.ToLower();
        }

        /// <summary>将虚拟路径转化为物理路径。</summary>
        /// <param name="baseWwwroot">是否基于 wwwroot 目录（一些静态资源文件是放在wwwroot 下的，但其路径已去除 wwwroot 目录）</param>
        public static string MapPath(this string virtualPath, bool baseWwwroot=false)
        {
            if (virtualPath.IsEmpty())
                return "";
            if (virtualPath.Contains("/"))
            {
                if (baseWwwroot)
                    return Path.Combine(_hostFolder, "wwwroot", virtualPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                return Path.Combine(_hostFolder, virtualPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            }
            return virtualPath;
        }


        /// <summary>
        /// 将 URL 转化为完整路径。如:
        /// （1）../default.aspx 转化为 http://..../application1/default.aspx
        /// （2）~/default.aspx 转化为 http://..../application1/default.aspx
        /// </summary>
        public static string ResolveFullUrl(this string relativeUrl)
        {
            relativeUrl = relativeUrl.TrimStart("~");
            if (relativeUrl.IsEmpty())
                return "";
            if (relativeUrl.ToLower().StartsWith("http"))
                return relativeUrl;
            var url = ResolveUrl(relativeUrl);
            return Asp.Host + url;
        }

        /// <summary>
        /// 将 URL 转化为从根目录开始的路径。如:
        /// （1）../default.aspx 转化为 /application1/default.aspx
        /// （2）~/default.aspx 转化为 /application1/default.aspx
        ///  1、当URL以斜线开始（/或\），也不会改动它！
        ///  2、当URL以〜/开始，它会被AppVirtualPath取代。
        ///  3、当URL是一个绝对URL，也不会改变它。
        ///  4、在任何其他情况下（甚至以〜开始，而不是斜杠），将追加URL到AppVirtualPath。
        ///  5、每当它修改URL，还修复斜杠。删除双斜线，用/替换\。
        /// </summary>
        public static string ResolveUrl(this string relativeUrl)
        {
            if (relativeUrl.IsEmpty())
                return "";
            relativeUrl = relativeUrl.TrimStart("~");
            //return relativeUrl.IsEmpty() ? "" : new Control().ResolveUrl(relativeUrl);
            return relativeUrl;
        }


        /// <summary>
        /// 将 URL 转化为相对于浏览器当前路径的相对路径。
        /// 如浏览器当前为 /pages/test.aspx，则
        /// （1）/pages/default.aspx 转化为 default.aspx
        /// （2）~/default.aspx      转化为 ../default.aspx
        /// </summary>
        //public static string ResolveClientUrl(this string relativeUrl)
        //{
        //    relativeUrl = relativeUrl.TrimStart("~");
        //    return relativeUrl.IsEmpty() ? "" : new Control().ResolveClientUrl(relativeUrl);
        //}

        /// <summary>获取当前网页请求的来源地址（前一个地址）</summary>
        public static string GetUrlReferrer()
        {
            return Request.Headers["Referer"].ToString();
        }

        //-------------------------------------
        // QueryString
        //-------------------------------------
        /// <summary>获取请求参数（Query-Form-Header-Cookie）</summary>
        public static T? GetParam<T>(string key) where T : struct
        {
            StringValues v;
            if (!Request.Query.TryGetValue(key, out v))
                if (!Request.Form.TryGetValue(key, out v))
                    if (!Request.Headers.TryGetValue(key, out v))
                        if (Request.Cookies.TryGetValue(key, out string s))
                            return s.Parse<T>();
            return v.FirstOrDefault().Parse<T>();
        }

        /// <summary>获取查询字符串</summary>
        public static T? GetQuery<T>(string queryKey) where T : struct
        {
            return GetQueryString(queryKey).Parse<T?>();
        }

        /// <summary>获取查询字符串</summary>
        public static string GetQueryString(string queryKey, bool ignoreCase=true)
        {
            if (ignoreCase)
                return Request.Query[queryKey];
            var url = new Url(Request.GetDisplayUrl());
            return url[queryKey];
        }

        /// <summary>获取查询字符串中的整型参数值</summary>
        public static int? GetQueryInt(string queryKey)
        {
            return GetQueryString(queryKey).ParseInt();
        }

        /// <summary>获取查询字符串中的整型参数值</summary>
        public static Int64? GetQueryLong(string queryKey)
        {
            return GetQueryString(queryKey).ParseLong();
        }

        /// <summary>获取查询字符串中的boolean参数值</summary>
        public static bool? GetQueryBool(string queryKey)
        {
            return GetQueryString(queryKey).ParseBool();
        }


        /// <summary>允许跨域（未测试）</summary>
        public static void EnableCros()
        {
            var origin = Asp.Current.Request.Headers["Origin"];
            Asp.Current.Response.Headers.Append("Access-Control-Allow-Origin", origin);
        }
    }

}