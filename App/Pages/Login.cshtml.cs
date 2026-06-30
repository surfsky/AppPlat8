using App.Components;
using App.DAL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;

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
                return BuildResult(-1, msg);
            else
            {
                string verifyCode = Random.Shared.Next(1000, 9999).ToString();
                Auth.SetVerifyCode(verifyCode);  // Set verifycode in session
                return BuildResult(0, "验证通过");
            }
        }

        /// <summary>登录</summary>
        public IActionResult OnPost(string userName, string password)
        {
            // Get verifycode from session
            var verifyCode = Auth.GetVerifyCode();
            if (string.IsNullOrEmpty(verifyCode))
                 return BuildResult(-1, "请先完成滑块验证");

            // 调用 Auth.Login 进行登录
            var n = Auth.Login(userName, password, verifyCode);
            if (n == 0)
                return BuildResult(0, "登录成功", new { redirect = "/Index" });
            else
            {
                string msg = "登录失败";
                switch (n)
                {
                    case -1: msg = "用户名或密码错"; break;
                    case -2: msg = "用户未启用"; break;
                    case -3: msg = "用户名或密码错"; break;
                    case -4: msg = "验证码失效，请重新滑动"; break; // verifyCode mismatch
                }
                return BuildResult(n, msg);
            }
        }
    }
}
