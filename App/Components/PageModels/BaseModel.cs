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
using App.DAL;
using App.Components;
using App.HttpApi;

namespace App
{
    /// <summary>
    /// 页面模型基类，提供一些公共方法和属性
    /// </summary>
    public partial class BaseModel : PageModel
    {
        private static readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };

            // Keep response enums as numbers for EleUI compatibility (formatters/select filters expect numeric enum values).
            options.Converters.Add(new Int64ToStringJsonConverter());
            options.Converters.Add(new NullableInt64ToStringJsonConverter());
            options.Converters.Add(new UInt64ToStringJsonConverter());
            options.Converters.Add(new NullableUInt64ToStringJsonConverter());
            return options;
        }


        /// <summary>
        /// 导出当前模型上标记了 [BindProperty] 的属性，供前端初始化/回传使用。
        /// </summary>
        public object Export()
        {
            IDictionary<string, object> data = new ExpandoObject();
            var properties = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);

            foreach (var property in properties)
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;
                if (property.GetCustomAttribute<BindPropertyAttribute>() == null)
                    continue;

                data[property.Name] = property.GetValue(this);
            }

            return data;
        }

        //-------------------------------------------------
        // 构建API结果
        //-------------------------------------------------
        /// <summary>构建API结果</summary>
        public static JsonResult BuildResult(int code, string message, object data = null, Paging pager = null)
        {
            return new JsonResult(new APIResult(code, message, data, pager), _jsonOptions);
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

        public override void OnPageHandlerExecuted(PageHandlerExecutedContext context)
        {
            base.OnPageHandlerExecuted(context);
        }



        //-------------------------------------------------
        // 用户权限和校验
        //-------------------------------------------------
        /// <summary>检查当前用户是否拥有某个权限</summary>
        protected bool CheckPower(Power power) => Auth.CheckPower(HttpContext, power);

        /// <summary>获取当前登录用户拥有的全部权限列表</summary>
        protected List<Power> GetPowers() => Auth.GetUserPowers(HttpContext);

        /// <summary>当前登录用户名</summary>
        protected string GetUserName() => Auth.GetUserName(HttpContext);

        /// <summary>当前登录用户标识符</summary>
        protected long? GetUserId() => Auth.GetUserId(HttpContext);

        protected App.DAL.User GetUser() => Auth.GetUser();
    }
}
