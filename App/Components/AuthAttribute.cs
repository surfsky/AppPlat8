using App.DAL;
using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace App.Components
{
    /// <summary>访问鉴权</summary>
    /// <example>
    /// [Auth(Permission.UserView)]
    /// [Auth(Permission.UserView, Permission.UserEdit, Permission.UserNew, Permission.UserDelete)]
    /// [Auth(AuthLogin=true, AuthSign=true)]
    /// public class UserPage : Page {...}
    /// </example>
    public class AuthAttribute : ResultFilterAttribute
    {
        /// <summary>是否忽略安全检测</summary>
        public bool Ignore { get; set; } = false;
        /// <summary>校验登陆</summary>
        public bool AuthLogin { get; set; } = false;  // 可用 Auth({AuthLogin = true}) 设置
        /// <summary>校验URL签名（尚未实现）</summary>
        public bool AuthSign { get; set; } = false;

        
        /// <summary>查看权限</summary>
        public Permission? ViewPermission { get; set; }
        /// <summary>新建权限</summary>
        public Permission? NewPermission { get; set; }
        /// <summary>编辑权限</summary>
        public Permission? EditPermission { get; set; }
        /// <summary>删除权限</summary>
        public Permission? DeletePermission { get; set; }


        /// <summary>是否安全（有查看、登录、签名鉴权；或忽略）</summary>
        public bool IsSafe =>  Ignore || AuthLogin || AuthSign || ViewPermission != null;

        
        // 构造方法。注：dotnet 8 Attribute 构造函数不支持参数默认值和可空类型，只能写多个构造函数，若没有匹配的构造函数可以这么写：Auth(AuthLogin = true)
        public AuthAttribute() { }
        public AuthAttribute(Permission viewPermission)
        {
            ViewPermission = viewPermission;
            NewPermission = viewPermission;
            EditPermission = viewPermission;
            DeletePermission = viewPermission;
        }
        public AuthAttribute(Permission viewPermission, Permission editPermission)
        {
            ViewPermission = viewPermission;
            NewPermission = editPermission;
            EditPermission = editPermission;
            DeletePermission = editPermission;
        }
        public AuthAttribute(Permission viewPermission, Permission newPermission, Permission editPermission, Permission deletePermission)
        {
            ViewPermission = viewPermission;
            NewPermission = newPermission;
            EditPermission = editPermission;
            DeletePermission = deletePermission;
        }

        // 页面权限校验
        public override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            HttpContext context = filterContext.HttpContext;
            if (Ignore)
                return;
            if (AuthLogin && Auth.IsLogin(context))
                return;
            if (ViewPermission != null && Auth.HasPermission(ViewPermission.Value, context))
                return;

            //  校验失败的情况，根据请求方法返回不同的结果，GET 请求返回页面，POST 请求返回 JSON 错误结果
            if (context.Request.Method == "GET")
            {
                Auth.WriteFailPage(context);
                filterContext.Result = new EmptyResult();
            }
            else if (context.Request.Method == "POST")
            {
                filterContext.Result = new JsonResult(new {code=403, success = false, message = Auth.MSG_CHECK_POWER_FAIL_ACTION})
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }        
    }
}
