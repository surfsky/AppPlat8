using App.DAL;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace App.Components
{
    /// <summary>
    /// App自定义权限验证过滤器
    /// </summary>
    public class CheckPowerAttribute : ResultFilterAttribute
    {
        /// <summary>权限名称</summary>
        public Power Power { get; set; }

        public CheckPowerAttribute(Power power)
        {
            Power = power;
        }


        public override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            HttpContext context = filterContext.HttpContext;
            if (!Auth.CheckPower(context, Power))
            {
                if (context.Request.Method == "GET")
                {
                    Auth.CheckPowerFailWithPage(context);

                    //http://stackoverflow.com/questions/9837180/how-to-skip-action-execution-from-an-actionfilter
                    // -修正越权访问页面时会报错[服务器无法在发送 HTTP 标头之后追加标头]（龙涛软件-9374）。
                    filterContext.Result = new EmptyResult();
                }
                else if (context.Request.Method == "POST")
                {
                    // return a simple JSON error instead of using FineUI.UIHelper
                    filterContext.Result = new JsonResult(new
                    {
                        success = false,
                        message = Auth.MSG_CHECK_POWER_FAIL_ACTION
                    })
                    {
                        StatusCode = StatusCodes.Status403Forbidden
                    };
                }
            }

        }




    }
}