using System;
using System.Linq;
using System.Text;
using App.Components;
using App.DAL;
using App.Entities;
using App.Utils;
using App.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;

namespace App.Pages
{
    /// <summary>
    /// LoginAI —— AI 调试专用免滑块登录入口。
    ///
    /// 核心流程（OnGet 同步执行，失败/成功都回显到 LoginAI.cshtml，并把日志同时写入 LogDb 方便 AI 侧看到失败原因）：
    ///   1) Gate：SiteConfig.EnableLoginAI 与当前 HostingEnvironment 双重开关
    ///      - Production 环境下只有显式 EnableLoginAI=true 才放行
    ///      - 其它环境默认开启（若 EnableLoginAI=null/true）。
    ///   2) 解析 URL 参数：userId / password / timestamp / nonce / sign
    ///   3) SignHelper.Validate：时间戳窗口、nonce 单次使用、签名恒定时间比较
    ///   4) Resolve 登录账号：userId 可能是 Name（如 admin）或用户 Id 数字（如 206），
    ///      两者都支持。
    ///   5) 走和 Login 页相同的 Auth.Login(userName, password) 双参重载 ——
    ///      直接跳过滑块 verifyCode，走相同的 PasswordUtil.ComparePasswords +
    ///      AuthHelper.Login 写 Cookie 流程。
    ///   6) 登录成功后 302 到 /Index（前端 meta refresh + 同时有 Redirect Action）。
    ///
    /// 【安全要点】见 SignHelper 顶部注释，核心：
    ///   - HMAC-SHA256 + 私钥不外露；
    ///   - 时间戳 1 小时窗口自动过期；
    ///   - nonce 进程内去重 2 小时；
    ///   - 对外错误信息统一"签名或参数无效"，不给 Oracle。
    /// </summary>
    [AllowAnonymous]
    public class LoginAIModel : BaseModel
    {
        //--------------------------------------------------
        // 依赖：HostEnvironment 用于判断是否 Production
        // 注：BaseModel 里如果已经注入过 Asp/IWebHostEnv，这里直接再注入一个更明确
        //--------------------------------------------------
        private readonly IWebHostEnvironment _env;
        public LoginAIModel(IWebHostEnvironment env) { _env = env; }

        //--------------------------------------------------
        // 页面回显数据
        //--------------------------------------------------
        public class LoginAIResult
        {
            public bool   LoginOk    { get; set; }
            public string PublicMsg  { get; set; }   // 给 AI/前端看的模糊消息
            public string DebugText  { get; set; }   // 仅 debug=1 时显示
        }

        public LoginAIResult LastResult { get; private set; }

        // 签名/链接相关参数（Bind(SupportsGet) 也可以，这里直接从 Query 拿更直观）
        [BindProperty(SupportsGet = true)] public string UserId    { get; set; }
        [BindProperty(SupportsGet = true)] public string Password  { get; set; }
        [BindProperty(SupportsGet = true)] public long   Timestamp { get; set; }
        [BindProperty(SupportsGet = true)] public string Nonce     { get; set; }
        [BindProperty(SupportsGet = true)] public string Sign      { get; set; }
        [BindProperty(SupportsGet = true)] public string ReturnUrl { get; set; }

        //--------------------------------------------------
        // HTTP GET 处理
        //--------------------------------------------------
        public IActionResult OnGet()
        {
            var ip        = Asp.ClientIP;
            StringValues uaSv = Asp.Request?.Headers["User-Agent"] ?? StringValues.Empty;
            var ua        = uaSv.Count > 0 ? uaSv[0] ?? "-" : "-";
            var siteCfg   = SiteConfig.Instance;
            var isProd    = _env.IsProduction();

            // ---- 1) 开关 Gate ------------------------------------------------
            var enabledByCfg = siteCfg.EnableLoginAI ?? true;
            if (!enabledByCfg)
            {
                var msg = "LoginAI 已被站点配置禁用（SiteConfig.EnableLoginAI=false）";
                Logger.LogDb(LogLevel.Warn, user: "-", from: "LoginAI/Gate",
                    message: msg + $" IP={ip} UA={ua}");
                return StatusCode(StatusCodes.Status404NotFound, "Not Found");
            }
            if (isProd && siteCfg.EnableLoginAI != true)  // Prod 必须显式 true
            {
                var msg = "Production 环境 LoginAI 默认禁用，请在站点配置里显式 EnableLoginAI=true。";
                Logger.LogDb(LogLevel.Warn, user: "-", from: "LoginAI/Gate",
                    message: msg + $" IP={ip} UA={ua}");
                return StatusCode(StatusCodes.Status404NotFound, "Not Found");
            }

            // ---- 2) 如果没有任何参数，就直接显示引导页 ------------------------
            if (string.IsNullOrWhiteSpace(UserId) &&
                string.IsNullOrWhiteSpace(Password) &&
                Timestamp == 0 &&
                string.IsNullOrWhiteSpace(Sign))
            {
                return Page();
            }

            // ---- 3) SignHelper.Validate --------------------------------------
            var v = SignHelper.Validate(
                userId:     UserId,
                password:   Password,
                timestampMsUtc: Timestamp,
                nonce:      Nonce,
                sign:       Sign,
                publicKey:  siteCfg.PublicKey,
                privateKey: siteCfg.PrivateKey);

            if (!v.Ok)
            {
                Logger.LogDb(LogLevel.Warn, user: "-", from: "LoginAI/Sign",
                    message: $"验签失败。UserId={TruncateForLog(UserId)} Detail={v.Detail} IP={ip} UA={ua}");
                LastResult = new LoginAIResult
                {
                    LoginOk   = false,
                    PublicMsg = v.Msg,   // 对外模糊："签名或参数无效"
                    DebugText = BuildDebugText(v.Detail, siteCfg),
                };
                return Page();
            }

            // ---- 4) 解析登录名：UserId 可能是 Name 或 long Id -----------------
            string loginName = null;
            App.DAL.User u  = null;
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
                // 故意返回和"签名失败"相同的对外文案，不给 Oracle
                Logger.LogDb(LogLevel.Warn, user: "-", from: "LoginAI/ResolveUser",
                    message: $"用户未找到。UserId={TruncateForLog(UserId)} IP={ip} UA={ua}");
                LastResult = new LoginAIResult
                {
                    LoginOk   = false,
                    PublicMsg = "签名或参数无效",
                    DebugText = BuildDebugText("user-not-found", siteCfg),
                };
                return Page();
            }

            // ---- 5) 标准登录（不走滑块 verifyCode）---------------------------
            var code = Auth.Login(loginName, Password);
            if (code != 0)
            {
                string msg = code switch
                {
                    -1 => "用户不存在",
                    -2 => "用户未启用",
                    -3 => "密码错误",
                    _  => "登录失败(code=" + code + ")",
                };
                // 对外仍使用模糊文案
                Logger.LogDb(LogLevel.Warn, user: "-", from: "LoginAI/LoginFail",
                    message: $"账号={loginName} UserId={u.Id} {msg} IP={ip} UA={ua}");
                LastResult = new LoginAIResult
                {
                    LoginOk   = false,
                    PublicMsg = "签名或参数无效",
                    DebugText = BuildDebugText($"login-fail code={code} {msg}", siteCfg),
                };
                return Page();
            }

            // ---- 6) 登录成功 --------------------------------------------------
            var okMsg = $"LoginAI 登录成功。账号={u.Name} UserId={u.Id} IP={ip} UA={ua}";
            Logger.LogDb(LogLevel.Info, user: u.Name, from: "LoginAI/LoginOk", message: okMsg);

            // 清理可能遗留的滑块验证码 Session，避免后续访问 Login 正常流程受影响
            Auth.SetVerifyCode("");

            // 标准 302 登录语义：Cookie + Location 重定向（配合浏览器/脚本自动跟随）
            var redir = SafeReturnUrl(ReturnUrl);
            return Redirect(redir);
        }

        //--------------------------------------------------
        // 内部小工具
        //--------------------------------------------------
        private static string TruncateForLog(string s, int max = 32)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            if (s.Length <= max) return s;
            return s.Substring(0, max) + "…";
        }

        // 调试 & 示例（仅 debug=1 显示）
        //--------------------------------------------------
        public string BuildDebugText(string stage, SiteConfig cfg, App.DAL.User u = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("--- LoginAI 调试快照（生产切勿外传） ---");
            sb.AppendLine("Stage     : " + stage);
            sb.AppendLine("Env       : " + (_env?.EnvironmentName ?? "-"));
            sb.AppendLine("IP        : " + Asp.ClientIP);
            sb.AppendLine("UrlParams : userId=" + TruncateForLog(UserId)
                                  + " password=" + (string.IsNullOrWhiteSpace(Password) ? "(null)" : "***")
                                  + " timestamp=" + Timestamp
                                  + " nonce=" + TruncateForLog(Nonce)
                                  + " sign=" + TruncateForLog(Sign, 16) + "...");
            sb.AppendLine("PublicKey (cfg) : " + TruncateForLog(cfg.PublicKey, 32));
            sb.AppendLine("PrivateKey(len) : " + (string.IsNullOrWhiteSpace(cfg.PrivateKey) ? 0 : cfg.PrivateKey.Length));
            if (u != null)
                sb.AppendLine("TargetUser: Id=" + u.Id + " Name=" + u.Name + " RealName=" + (u.RealName ?? "-") + " OrgId=" + u.OrgId);
            sb.AppendLine();
            sb.AppendLine("--- C# 生成签名示例 ---");
            sb.AppendLine(@"
  var url = SignHelper.BuildLoginAIUrl(
      baseUrl:  ""http://localhost:6060"",
      userId:   ""admin"",
      password: ""Abc@123"",
      publicKey:  SiteConfig.Instance.PublicKey,
      privateKey: SiteConfig.Instance.PrivateKey,
      out var ts,
      out var nonce);
  // 然后把生成的 url 直接交给 AI 浏览器 GET 即可。
");
            sb.AppendLine();
            sb.AppendLine("--- Python 生成签名（等价实现，给 AI 脚本用） ---");
            sb.AppendLine(@"
  import hmac, hashlib, time, uuid, urllib.parse
  PUBLIC_KEY  = """ + (cfg.PublicKey ?? "") + @"""
  PRIVATE_KEY = """ + (string.IsNullOrWhiteSpace(cfg.PrivateKey) ? "" : "(把 SiteConfig.PrivateKey 填这里)") + @"""
  user_id   = ""admin""
  password  = ""Abc@123""
  ts_ms     = int(time.time() * 1000)
  nonce     = uuid.uuid4().hex
  payload = ""&"".join([
      f""nonce={nonce}"" if nonce else """",
      f""password={password}"",
      f""publicKey={PUBLIC_KEY}"",
      f""timestamp={ts_ms}"",
      f""userId={user_id}"",
  ])
  payload = ""&"".join([x for x in payload.split(""&"") if x])
  sign = hmac.new(PRIVATE_KEY.encode(), payload.encode(), hashlib.sha256).hexdigest()
  url = ""/LoginAI?"" + urllib.parse.urlencode({
      ""userId"": user_id, ""password"": password,
      ""timestamp"": ts_ms, ""nonce"": nonce, ""sign"": sign})
  print(url)
");
            return sb.ToString();
        }

        public string BuildSampleDebugText() => BuildDebugText("sample", SiteConfig.Instance);

        //--------------------------------------------------
        // 辅助：ReturnUrl 白名单（仅允许本地相对路径，避免开放重定向）
        //--------------------------------------------------
        private static string SafeReturnUrl(string returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl)) return "/Index";
            if (returnUrl.StartsWith("/", StringComparison.Ordinal) &&
                !returnUrl.StartsWith("//", StringComparison.Ordinal)) return returnUrl;
            return "/Index";
        }
    }
}
