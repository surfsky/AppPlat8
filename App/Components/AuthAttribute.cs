using App.DAL;
using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace App.Components
{
    /// <summary>访问鉴权</summary>
    /// <example>
    /// [Auth(Power.UserView)]
    /// [Auth(Power.UserView, Power.UserEdit, Power.UserNew, Power.UserDelete)]
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
        public Power? ViewPower { get; set; }
        /// <summary>新建权限</summary>
        public Power? NewPower { get; set; }
        /// <summary>编辑权限</summary>
        public Power? EditPower { get; set; }
        /// <summary>删除权限</summary>
        public Power? DeletePower { get; set; }


        /// <summary>是否安全（有查看、登录、签名鉴权；或忽略）</summary>
        public bool IsSafe =>  Ignore || AuthLogin || AuthSign || ViewPower != null;

        
        // 构造方法。注：dotnet 8 Attribute 构造函数不支持参数默认值和可空类型，只能写多个构造函数，若没有匹配的构造函数可以这么写：Auth(AuthLogin = true)
        public AuthAttribute() { }
        public AuthAttribute(Power viewPower)
        {
            ViewPower = viewPower;
            NewPower = viewPower;
            EditPower = viewPower;
            DeletePower = viewPower;
        }
        public AuthAttribute(Power viewPower, Power newPower, Power editPower, Power deletePower)
        {
            ViewPower = viewPower;
            NewPower = newPower;
            EditPower = editPower;
            DeletePower = deletePower;
        }

        // 页面权限校验
        public override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            HttpContext context = filterContext.HttpContext;
            if (Ignore)
                return;
            if (AuthLogin && !Auth.IsLogin(context))
                return;
            if (ViewPower != null && !Auth.CheckPower(context, ViewPower.Value))
                return;

            //  校验失败的情况，根据请求方法返回不同的结果，GET 请求返回页面，POST 请求返回 JSON 错误结果
            if (context.Request.Method == "GET")
            {
                Auth.WritePowerFailPage(context);
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
