using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Dynamic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using App.DAL;
using App.Components;
using App.HttpApi;
using App.Utils;

namespace App
{
    /// <summary>
    /// 页面模型基类，提供一些公共方法和属性（默认无需进行安全验证）
    /// </summary>
    public partial class BaseModel : PageModel
    {
        //-------------------------------------------------
        // 构建API结果
        //-------------------------------------------------
        private static readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                // 让非 ASCII 字符（如中文）直接以 UTF-8 输出，不被转义为 \uXXXX 形式。
                // 这样前端 JSON.parse 后能直接拿到中文，开发者工具里看到的中文也是人类可读形式。
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            // Keep response enums as numbers for EleUI compatibility (formatters/select filters expect numeric enum values).
            options.Converters.Add(new Int64ToStringJsonConverter());
            options.Converters.Add(new NullableInt64ToStringJsonConverter());
            options.Converters.Add(new UInt64ToStringJsonConverter());
            options.Converters.Add(new NullableUInt64ToStringJsonConverter());
            return options;
        }

        /// <summary>构建API结果</summary>
        public static JsonResult BuildResult(int code, string message, object data = null, Paging pager = null)
        {
            // 业务错误时同步返回 HTTP 错误状态码（如 500/4xx），这样前端的 fetch 能通过 !resp.ok 识别错误，触发 setError 路径，
            // 避免把整段错误 JSON 误当作流式 AI 输出渲染在 AI 回答气泡里。
            // code == 0 表示成功，仍按 200 返回。
            var result = new JsonResult(new APIResult(code, message, data, pager), _jsonOptions);
            if (code != 0)
            {
                result.StatusCode = code >= 400 && code <= 599 ? code : 500;
            }
            return result;
        }



        //-------------------------------------------------
        // 页面处理事件
        //-------------------------------------------------
        /// <summary>页面处理器调用之前执行（加了在线逻辑）</summary>
        public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
        {
            base.OnPageHandlerExecuting(context);

            // 如果用户已经登录，更新在线记录
            if (User.Identity.IsAuthenticated)
                Auth.UpdateOnlineUser(GetUserId());
        }

        /// <summary>统一拦截 AppException，转为 JSON 错误结果</summary>
        public override void OnPageHandlerExecuted(PageHandlerExecutedContext context)
        {
            if (context.Exception is AppException ex)
            {
                context.ExceptionHandled = true;
                context.Result = BuildResult(ex.Code, ex.Message);
            }
            base.OnPageHandlerExecuted(context);
        }

        //-------------------------------------------------
        // 业务校验
        //-------------------------------------------------
        /// <summary>条件为 false 时抛出 AppException，由 BaseModel 统一拦截为 BuildResult</summary>
        public static void Assert(bool condition, string message, int code = 400)
        {
            if (!condition) throw new AppException(code, message);
        }

        /// <summary>Func 返回 false 时抛出 AppException（延迟求值，可省不必要的计算）</summary>
        public static void Assert(Func<bool> predicate, string message, int code = 400)
        {
            if (!predicate()) throw new AppException(code, message);
        }

        /// <summary>对象为 null 时抛出 AppException</summary>
        public static T Exist<T>(T obj, string message = "数据不存在") where T : class
        {
            if (obj == null) throw new AppException(404, message);
            return obj;
        }

        /// <summary>可空对象无值时抛出 AppException，否则返回 Value</summary>
        public static T Exist<T>(T? obj, string message = "请选择一项") where T : struct
        {
            if (!obj.HasValue) throw new AppException(400, message);
            return obj.Value;
        }



        //-------------------------------------------------
        // 用户权限和校验
        //-------------------------------------------------
        /// <summary>获取当前登录用户对象</summary>
        protected App.DAL.User GetUser() => Auth.GetUser();

        /// <summary>当前登录用户名（从验票中获取）</summary>
        protected string GetUserName() => Auth.GetUserName(HttpContext);

        /// <summary>当前登录用户标识符（从验票中获取）</summary>
        protected long? GetUserId() => Auth.GetUserId(HttpContext);

        /// <summary>检查当前用户是否拥有某个权限</summary>
        protected bool HasPermission(Permission p) => Auth.HasPermission(p, HttpContext);



    }
}