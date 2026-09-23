using System;
using System.Text;
using App.Components;
using App.DAL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace App.Pages
{
    /// <summary>
    /// LoginAI · Debug API（仅 Development 启用）
    ///   GET /LoginAI?userName=...&password=...&timestamp=...&nonce=...&sign=...
    ///   返回纯APIResult 格式
    /// </summary>
    [AllowAnonymous]
    public class LoginAIModel : BaseModel
    {
        public LoginAIModel() { }

        [BindProperty(SupportsGet = true)] public string UserName    { get; set; }
        [BindProperty(SupportsGet = true)] public string Password  { get; set; }
        [BindProperty(SupportsGet = true)] public long   Timestamp { get; set; }
        [BindProperty(SupportsGet = true)] public string Nonce     { get; set; }
        [BindProperty(SupportsGet = true)] public string Sign      { get; set; }

        public ActionResult OnGet()
        {
            var cfg = SiteConfig.Instance;

            // check LoginAI enabled
            if (!cfg.EnableLoginAI ?? true)
                return Content("DISABLED (cfg)", "text/plain; charset=utf-8");

            // check sign
            var expect = SignHelper.BuildSign(UserName, Password, Timestamp, cfg.PublicKey, cfg.PrivateKey, Nonce);
            var vr = SignHelper.Validate(UserName, Password, Timestamp, Nonce, Sign, cfg.PublicKey, cfg.PrivateKey, 3600);
            if (!vr.Ok)
                return BuildResult(-4, vr.Msg);

            // check password
            var authCode = App.Components.Auth.Login(UserName, Password);
            var authMsg = authCode switch
            {
                0  => "OK",
                -1 => "用户不存在",
                -2 => "用户未启用",
                -3 => "密码错误",
                _  => $"unknown code={authCode}"
            };
            return BuildResult(authCode, authMsg);
        }
    }
}
