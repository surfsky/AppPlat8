using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using App.Utils;

namespace App.Components
{
    /// <summary>
    /// AI 调试登录 URL 签名工具（仅用于非生产调试用途，便于 AI 自动化过滑块登进去写测试/截图）。
    ///
    /// 一、URL 约定（用户要求的格式）：
    ///   /LoginAI?userId=xxx&password=xxx&timestamp=xxx&sign=xxx
    ///   同时建议额外传 nonce=xxxxxxxxxxxx 用于防重放（不传时也能工作，但存在 URL 被盗用复用 1 小时风险）。
    ///
    /// 二、签名原文 Payload（按 key 字母序排序，key=value，& 连接；顺序错误会导致签名校验失败）：
    ///   nonce   （可选：如传入必须参与签名；推荐 Guid / 16 位以上随机串）
    ///   password（明文密码；如果 AI 侧只拿得到 DB 里的哈希值，也可改为约定直接传 hash 值，改这里 ComparePasswords 逻辑即可）
    ///   publicKey（SiteConfig.PublicKey：公开字符串，用于把签名和部署环境绑定，避免为 Dev 生成的签名能在 Prod 复用）
    ///   timestamp（毫秒级 Unix 时间戳，UTC；过期窗口 ±30 分钟，等价于链接有效期 1 小时）
    ///   userId   （支持 账号 Name 或 用户 Id 数字；LoginAI 内部二选一匹配）
    ///
    ///   示例：
    ///     nonce=abc&password=Abc@123&publicKey=PUB-AI-DEV-2026&timestamp=1788800000000&userId=admin
    ///
    /// 三、签名算法：
    ///   sign = HMACSHA256(Encoding.UTF8.GetBytes(privateKey), Encoding.UTF8.GetBytes(payload))
    ///        => 小写十六进制（64 字符）。
    ///   privateKey 来自 SiteConfig.PrivateKey（必须私有，绝对不要把它拼到 URL 或暴露给前端/日志）。
    ///
    /// 四、风险与对策（都已实现或提供开关，见 SignHelper.Validate 内注释）：
    ///   1. URL 被盗用：HMAC 签名保证不掌握 privateKey 无法伪造；且 timestamp 1 小时自动失效。
    ///   2. 重放攻击：nonce 单次使用（NonceCache 去重，TTL=2 小时）；AI 每次生成 URL 都要换 nonce。
    ///   3. 明文密码泄露：登录成功后 302 跳转走，浏览器历史/Referer 仍可能泄漏——生产环境务必禁用或改为传 userId+签名+token（不含 password），或走 mTLS。
    ///   4. 暴力破解 privateKey：privateKey 请使用 Guid.NewGuid().ToString("N") + 盐，不要使用示例默认值；可随时在后台站点配置里改。
    ///   5. 生产误开放：LoginAI 仅在 ASPNETCORE_ENVIRONMENT != Production 或显式 EnableLoginAI=true 时工作（LoginAI.cshtml.cs 里做了 gate）。
    ///   6. 签名长度错误 / 非 hex 字符：Validate 早期返回，避免进入 HMAC 计算浪费 CPU。
    ///   7. 枚举 userId 是否存在：统一返回 "签名或参数无效" 模糊错误，不给攻击者区分 "userId 对不对" 的 Oracle。
    /// </summary>
    public static class SignHelper
    {
        //------------------------------------------------------------
        // 常量 & 内部缓存（进程内非持久，重启清）
        //------------------------------------------------------------
        public const long DefaultValiditySeconds = 1800;           // 30 分钟 = 链接 1 小时有效窗口（±30min）
        public const long NonceTtlSeconds = 7200;                   // 2 小时

        /// <summary>nonce 去重：key=nonce 值，value=过期 UTC。</summary>
        private static readonly ConcurrentDictionary<string, DateTimeOffset> NonceCache =
            new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.Ordinal);

        /// <summary>清理掉 2 小时以上的 nonce，避免内存无限长（每 1000 次写触发一次清理）。</summary>
        private static int _nonceWriteCount;

        //------------------------------------------------------------
        // 公开：生成签名 / 生成示例 URL
        //------------------------------------------------------------

        /// <summary>按用户约定的字段，生成合法 HMAC-SHA256 签名（小写 hex 64 字符）。</summary>
        public static string BuildSign(
            string userId,
            string password,
            long timestampMsUtc,
            string publicKey,
            string privateKey,
            string nonce = null)
        {
            var payload = BuildPayloadString(userId, password, timestampMsUtc, publicKey, nonce);
            return HmacHex(privateKey, payload);
        }

        /// <summary>生成一个直接可以 GET 的完整 URL，用于把它交给 AI 浏览器。</summary>
        public static string BuildLoginAIUrl(
            string baseUrl,
            string userId,
            string password,
            string publicKey,
            string privateKey,
            out long usedTimestamp,
            out string usedNonce)
        {
            usedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            usedNonce    = Guid.NewGuid().ToString("N");
            var sign = BuildSign(userId, password, usedTimestamp, publicKey, privateKey, usedNonce);

            var sb = new StringBuilder(baseUrl.TrimEnd('/'));
            sb.Append("/LoginAI?userId=").Append(Uri.EscapeDataString(userId ?? ""));
            sb.Append("&password=").Append(Uri.EscapeDataString(password ?? ""));
            sb.Append("&timestamp=").Append(usedTimestamp.ToString());
            sb.Append("&nonce=").Append(usedNonce);
            sb.Append("&sign=").Append(sign);
            return sb.ToString();
        }

        //------------------------------------------------------------
        // 公开：校验 + 风险提示（LoginAI 页面入口调用）
        //------------------------------------------------------------

        public class ValidateResult
        {
            public bool   Ok     { get; set; }
            public string Msg    { get; set; }   // 错误描述（返回给前端前建议统一模糊化）
            public string Detail { get; set; }   // 日志用的具体错误
        }

        /// <summary>校验 URL 参数（时间戳、签名、nonce 去重）。</summary>
        public static ValidateResult Validate(
            string userId,
            string password,
            long timestampMsUtc,
            string nonce,
            string sign,
            string publicKey,
            string privateKey,
            long validSecs = DefaultValiditySeconds)
        {
            // 1) 基本参数有效性：尽早返回，避免 HMAC CPU 消耗
            if (string.IsNullOrWhiteSpace(userId))   return Fail("userId 为空",   "invalid-params");
            if (string.IsNullOrWhiteSpace(password)) return Fail("password 为空", "invalid-params");
            if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey))
                return Fail("PublicKey/PrivateKey 未配置，请在站点配置里设置", "siteconfig-missing");

            if (timestampMsUtc <= 0) return Fail("timestamp 无效", "bad-timestamp");

            var now = DateTimeOffset.UtcNow;
            var ts  = DateTimeOffset.FromUnixTimeMilliseconds(timestampMsUtc);
            if (ts.AddSeconds(validSecs) < now)    return Fail("签名已过期", "expired");
            if (ts.AddSeconds(-validSecs) > now)   return Fail("签名在未来，可能本机时钟漂移", "future-timestamp");

            if (string.IsNullOrWhiteSpace(sign) || sign.Length != 64)
                return Fail("签名格式错误（应为 HMAC-SHA256 小写 hex 64 字符）", "bad-sign-format");
            // 必须是纯 hex（0-9/a-f），否则拒绝
            for (int i = 0; i < sign.Length; i++)
            {
                var c = sign[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    return Fail("签名格式错误（应为小写 hex）", "bad-sign-format");
            }

            // 2) nonce 单次使用（防止同一 URL 在 1 小时窗口内反复被其他人拿去登录）
            if (!string.IsNullOrWhiteSpace(nonce))
            {
                // nonce 规范化：允许字母数字 + - _ 其他字符直接拒绝
                if (!System.Text.RegularExpressions.Regex.IsMatch(nonce, "^[A-Za-z0-9_-]{6,64}$"))
                    return Fail("nonce 格式错误（6-64 位字母数字/-/_）", "bad-nonce");

                var expireAt = now.AddSeconds(NonceTtlSeconds);
                var added = NonceCache.TryAdd(nonce, expireAt);
                if (!added)
                    return Fail("nonce 已被使用（重放拒绝）", "replay-nonce");
                TryTrimNonceCache(now);
            }

            // 3) 重新计算签名，与传入 sign 做恒定时间比较（避免时序攻击）
            var expect = BuildSign(userId, password, timestampMsUtc, publicKey, privateKey, nonce);
            if (!FixedTimeEquals(expect, sign))
                return Fail("签名不匹配", "sign-mismatch");

            return new ValidateResult { Ok = true, Msg = "ok", Detail = "sign-ok" };
        }

        //------------------------------------------------------------
        // 内部辅助
        //------------------------------------------------------------

        private static string BuildPayloadString(
            string userId,
            string password,
            long   timestampMsUtc,
            string publicKey,
            string nonce)
        {
            // key 按字母序（大小写无关）排列
            var kv = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(nonce))    kv["nonce"]    = nonce.Trim();
            kv["password"]  = password  ?? "";
            kv["publicKey"] = publicKey ?? "";
            kv["timestamp"] = timestampMsUtc.ToString();
            kv["userId"]    = userId    ?? "";
            return string.Join("&", kv.Select(p => $"{p.Key}={p.Value}"));
        }

        private static string HmacHex(string key, string message)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key ?? "")))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message ?? ""));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>恒定时间比较：两个字符串长度相同且内容相同才返回 true，每个字节都比较。</summary>
        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static ValidateResult Fail(string detail, string code)
        {
            return new ValidateResult
            {
                Ok     = false,
                Msg    = "签名或参数无效",  // 对外模糊错误，不给 Oracle
                Detail = $"{code}: {detail}",
            };
        }

        private static void TryTrimNonceCache(DateTimeOffset now)
        {
            var cnt = System.Threading.Interlocked.Increment(ref _nonceWriteCount);
            if ((cnt % 1000) != 0) return;

            List<string> toRemove = null;
            foreach (var kv in NonceCache)
            {
                if (kv.Value <= now)
                {
                    toRemove ??= new List<string>();
                    toRemove.Add(kv.Key);
                }
            }
            if (toRemove == null) return;
            foreach (var k in toRemove) NonceCache.TryRemove(k, out _);
        }
    }
}
