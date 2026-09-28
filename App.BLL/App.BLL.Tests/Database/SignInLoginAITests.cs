using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using App.DAL;

namespace App.BLL.Tests.Database
{
    [TestClass]
    public class SignInLoginAITests
    {
        [TestMethod]
        public void Print_LoginAI_URL_For_Default_Admin()
        {
            // 目的：输出一个即时可用的 /LoginAI URL，供 curl 登录拿到 cookie，
            // 然后继续 POST OnPostTest / OnPostMoveUp 两个 Handler。
            var cfg = SiteConfig.Instance ?? new SiteConfig();
            var pub = cfg.PublicKey ?? "PUB-AI-DEV:20260908:14fd9b8a2e3a4d7f95c5b514a6e9263a";
            var prv = cfg.PrivateKey ?? "PRV-AI-DEV:20260908:9b4c0a94c2ad43af807c13d3e0ef56ba19f0d2c94a3a472ab11a4a6a5980a718";
            Assert.IsNotNull(pub, nameof(pub));
            Assert.IsNotNull(prv, nameof(prv));

            // 找 SignHelper（App.Utils.SignHelper 或 App.Components.SignHelper）
            Type signType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                signType = asm.GetType("App.Utils.SignHelper", false)
                         ?? asm.GetType("App.Components.SignHelper", false)
                         ?? asm.GetType("SignHelper", false);
                if (signType != null) break;
            }
            Assert.IsNotNull(signType, "SignHelper type not found in loaded assemblies. Did you reference App.BLL and App.EleUI?");

            var build = signType.GetMethod("BuildSign", new[] { typeof(string), typeof(string), typeof(long), typeof(string), typeof(string), typeof(string) });
            Assert.IsNotNull(build, "BuildSign(userName,password,timestamp,publicKey,privateKey,nonce) static method not found on SignHelper");

            string userName = "admin";
            string password = "admin123";
            long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string nonce = Guid.NewGuid().ToString("N").Substring(0, 12);
            var sign = (string)build.Invoke(null, new object[] { userName, password, ts, pub, prv, nonce });
            Assert.IsFalse(string.IsNullOrWhiteSpace(sign), "BuildSign returned empty sign");

            var sb = new StringBuilder();
            sb.Append("http://localhost:6060/LoginAI?")
              .Append("userName=").Append(Uri.EscapeDataString(userName))
              .Append("&password=").Append(Uri.EscapeDataString(password))
              .Append("&timestamp=").Append(ts)
              .Append("&nonce=").Append(Uri.EscapeDataString(nonce))
              .Append("&sign=").Append(Uri.EscapeDataString(sign));
            var url = sb.ToString();

            // 必须通过 Console.Error 写出，因为 dotnet test 默认 stdout 会吞掉；
            // 我们也在 TestContext 里写，两者都覆盖。
            Console.Error.WriteLine("=== LOGINAI_URL ===");
            Console.Error.WriteLine(url);
            Console.Error.WriteLine("=== END LOGINAI_URL ===");
            TestContext?.WriteLine("LOGINAI_URL: {0}", url);

            Assert.IsTrue(url.StartsWith("http://"), "LoginAI URL format invalid: " + url);
        }

        public TestContext TestContext { get; set; }
    }
}
