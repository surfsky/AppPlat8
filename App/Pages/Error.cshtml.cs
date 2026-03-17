using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
//using Aliyun.Acs.Core.Logging;
using App.DAL;
using App.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace App.Pages
{
    public class ErrorModel : BaseModel
    {
        public int ErrorCode;
        public string ErrorMessage;
        public string RequestId;

        public void OnGet()
        {
            var code = Asp.GetQueryInt("code");
            if (code != null)
            {
                // HTTP错误
                this.ErrorCode = code.Value;
                this.ErrorMessage = GetHttpCodeMessage(code.Value);
            }
            else
            {
                // 内部异常错误
                var exception = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                if (exception != null)
                {
                    this.ErrorCode = 500;
                    this.ErrorMessage = exception.Error.Message;
                }
            }

            this.RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            Logger.Error("Code={0}, Message={1}, RequestId={2}", ErrorCode, ErrorMessage, this.RequestId);
        }

        /// <summary>获取HTTP代码信息</summary>
        public string GetHttpCodeMessage(int code)
        {
            switch (code)
            {
                case 400: return "请求无效";
                case 401: return "未授权";
                case 403: return "禁止访问";
                case 404: return "文件未找到";
                case 500: return "内部服务器错误";
                default:  return "未知错误";
            }
        }
    }
}