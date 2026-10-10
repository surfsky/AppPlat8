using App.Components;
using App.DAL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using App.Utils;

namespace App.Pages
{

    /// <summary>
    /// 登录页模型
    /// </summary>
    [AllowAnonymous]
    public class LoginModel : BaseModel
    {
        public string WinTitle { get; set; }
        public SiteConfig Site { get; set; }

        public void OnGet()
        {
            Site = SiteConfig.Instance;
            WinTitle = String.Format("{0} v{1}", SiteConfig.Instance.Title, Common.GetVersion());
            Auth.SetVerifyCode("");
        }


        /// <summary>验证滑动验证码</summary>
        public IActionResult OnPostCheckSlider([FromBody] SliderData data)
        {
            var (ok, msg) = SliderVerifier.Validate(data);
            if (!ok)
            {
                Auth.SetVerifyCode("");
                return BuildResult(-1, msg);
            }
            else
            {
                string verifyCode = Random.Shared.Next(1000, 9999).ToString();
                Auth.SetVerifyCode(verifyCode);
                return BuildResult(0, "验证通过");
            }
        }

        /// <summary>登录。保持和旧实现一致的两参数签名，避免模型绑定/过滤器干扰。
        /// 返回 JSON { code, message, data.redirect }，由前端自行 window.location 跳转；
        /// 若调用方未带 AJAX 标识（X-Requested-With:XMLHttpRequest），也仍返回 JSON。
        /// </summary>
        public IActionResult OnPost(string userName, string password)
        {
            var code = Auth.GetVerifyCode();
            if (string.IsNullOrEmpty(code))
                return BuildResult(-1, "请先完成滑块验证");

            int n = Auth.Login(userName, password, code);
            if (n == 0)
            {
                string url = ReadReturnUrl();
                if (url.IsEmpty() || !Auth.IsSafeUrl(url, Request)) 
                    url = "/Index";
                return BuildResult(0, "登录成功", new { redirect = url });
            }
            else
            {
                string msg = "登录失败";
                switch (n)
                {
                    case -1: msg = "用户名或密码错"; break;
                    case -2: msg = "用户未启用"; break;
                    case -3: msg = "用户名或密码错"; break;
                    case -4: msg = "验证码失效，请重新滑动"; break;
                }
                return BuildResult(n, msg);
            }
        }

        /// <summary>从 Query / Form 按优先级读取 ReturnUrl。</summary>
        private string ReadReturnUrl()
        {
            return Request.Query["returnUrl"];
        }


    }
}
