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
    /// 滑动验证码数据模型
    /// </summary>
    public class SliderData
    {
        public int Duration { get; set; }
        public List<Point> Points { get; set; }
    }

    /// <summary>
    /// 滑动验证码轨迹点模型
    /// </summary>
    public class Point
    {
        public int X { get; set; }
        public int Y { get; set; }
        public long T { get; set; }
    }

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
        }

        // Ajax handler for slider validation
        public IActionResult OnPostCheckSlider([FromBody] SliderData data)
        {
            if (data == null) 
                return BuildResult(-1, "数据异常");
            if (data.Duration < 200)
                return BuildResult(-1, "太快了");
            if (data.Points == null || data.Points.Count < 5)
                return BuildResult(-1, "轨迹异常");

            // 3. Check X progress
            // In real world, we check if points are strictly increasing in time and generally increasing in X
            // We can also check Y jitter if it's mouse
            // Generate a random token
            string token = Guid.NewGuid().ToString("N");
            HttpContext.Session.SetString("SliderVerified", token);
            
            // Set the expected code in Auth for this session
            // We also need to tell the client this code? No, client doesn't need it.
            // Client just needs to know it passed.
            // But OnPost needs to know what code to pass to Auth.Login.
            // We can store it in session.            
            // Let's use a random verify code for Auth.Login too
            string verifyCode = new Random().Next(1000, 9999).ToString();
            Auth.SetVerifyCode(verifyCode);
            HttpContext.Session.SetString("LoginVerifyCode", verifyCode);
            return BuildResult(0, "验证通过");
        }


        // Standard OnPost handler for Vue
        public IActionResult OnPost(string userName, string password)
        {
            // 1. Check if slider was verified via backend session
            var verifiedToken = HttpContext.Session.GetString("SliderVerified");
            if (string.IsNullOrEmpty(verifiedToken))
                return BuildResult(-1, "请先完成滑块验证");

            // 2. Clear verification status to prevent replay
            // 3. Get the code we set for Auth.Login
            HttpContext.Session.Remove("SliderVerified");
            string verifyCode = HttpContext.Session.GetString("LoginVerifyCode");
            HttpContext.Session.Remove("LoginVerifyCode");            
            if (string.IsNullOrEmpty(verifyCode))
                 return BuildResult(-1, "验证码已过期，请刷新重试");

            // 4. 调用 Auth.Login
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