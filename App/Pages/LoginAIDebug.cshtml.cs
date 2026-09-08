using System;
using System.Text;
using App.Components;
using App.DAL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages
{
    /// <summary>
    /// LoginAI · Debug API（仅 Development 启用）
    ///   GET /LoginAIDebug?userId=...&password=...&timestamp=...&nonce=...&sign=...
    ///   返回纯文本： payload / expect_sign / user_sign / match / validate / Auth.Login(code)
    /// </summary>
    [AllowAnonymous]
    public class LoginAIDebugModel : BaseModel
    {
        public LoginAIDebugModel() { }

        [BindProperty(SupportsGet = true)] public string UserId    { get; set; }
        [BindProperty(SupportsGet = true)] public string Password  { get; set; }
        [BindProperty(SupportsGet = true)] public long   Timestamp { get; set; }
        [BindProperty(SupportsGet = true)] public string Nonce     { get; set; }
        [BindProperty(SupportsGet = true)] public string Sign      { get; set; }

        public ContentResult OnGet()
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            if (!string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase))
                return Content("DISABLED (non-dev)", "text/plain; charset=utf-8");

            var cfg = SiteConfig.Instance;
            var kv = new System.Collections.Generic.SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"password", Password ?? ""},
                {"publicKey", cfg.PublicKey ?? ""},
                {"timestamp", Timestamp.ToString()},
                {"userId", UserId ?? ""}
            };
            if (!string.IsNullOrWhiteSpace(Nonce)) kv["nonce"] = Nonce.Trim();
            var sb = new StringBuilder();
            foreach (var p in kv) sb.Append(p.Key).Append('=').Append(p.Value).Append('&');
            if (sb.Length > 0) sb.Length--;
            var payload = sb.ToString();
            var expect = SignHelper.BuildSign(UserId, Password, Timestamp, cfg.PublicKey, cfg.PrivateKey, Nonce);
            var vr = SignHelper.Validate(UserId, Password, Timestamp, Nonce, Sign, cfg.PublicKey, cfg.PrivateKey, 3600);

            // 额外：模拟 LoginAI 下一步——解析用户 -> 调用 Auth.Login(name, password)
            string loginName = null;
            App.DAL.User u = null;
            int authCode = -9999;
            string authMsg = null;
            try
            {
                if (long.TryParse(UserId, out var uidLong) && uidLong > 0)
                {
                    u = App.DAL.User.Get(uidLong);
                    if (u != null) loginName = u.Name;
                }
                if (loginName == null)
                {
                    u = App.DAL.User.GetDetail(x => x.Name == UserId);
                    if (u != null) loginName = u.Name;
                }
                if (loginName == null)
                {
                    authMsg = "user-not-found";
                }
                else
                {
                    authCode = App.Components.Auth.Login(loginName, Password);
                    authMsg = authCode switch
                    {
                        0  => "OK",
                        -1 => "用户不存在",
                        -2 => "用户未启用",
                        -3 => "密码错误",
                        _  => $"unknown code={authCode}"
                    };
                }
            }
            catch (Exception ex) { authMsg = "EX:" + ex.Message; }

            return Content(
                "PUBLIC_KEY = " + (cfg.PublicKey ?? "") + "\n" +
                "PAYLOAD    = " + payload + "\n" +
                "EXPECT     = " + expect + "\n" +
                "USER_SIGN  = " + (Sign ?? "") + "\n" +
                "MATCH      = " + (expect == Sign ? "YES" : "NO") + "\n" +
                "VALIDATE   = Ok=" + vr.Ok + " / Msg=" + vr.Msg + " / Detail=" + vr.Detail + "\n" +
                "RESOLVE_USER= loginName=" + (loginName ?? "(null)") + " UserId=" + (u?.Id.ToString() ?? "(null)") + "\n" +
                "AUTH_LOGIN = code=" + authCode + " / " + authMsg + "\n",
                "text/plain; charset=utf-8");
        }
    }
}
