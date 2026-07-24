using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Security.Principal;
using App.Utils;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace App.Web
{
    /// <summary>
    /// Web Auth Helper
    /// </summary>
    public class AuthHelper
    {

        //-----------------------------------------------
        // Login/Logout
        //-----------------------------------------------
        /// <summary>Logout</summary>
        public static void Logout()
        {
            Asp.Current.SignOutAsync();
            Asp.Current.Session.Clear();
        }

        /// <example>AuthHelper.Login("123", "Admin", "1,2,3", DateTime.Now.AddDays(1));</example>
        public static ClaimsPrincipal Login(string userId, string userName, string roleIds, DateTime expiration)
        {
            var claims = new[]
            {
                new Claim("UserId", userId ?? ""),
                new Claim("UserName", userName ?? ""),
                new Claim("RoleIds", roleIds ?? ""),
                new Claim(ClaimTypes.NameIdentifier, userId ?? ""),
                new Claim(ClaimTypes.Name, userName ?? "")
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            Asp.Current.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties() { IsPersistent = false, ExpiresUtc = expiration.ToUniversalTime() }
                );

            // Expose bearer token for non-cookie terminals.
            var token = CreateBearerToken(userId, userName, roleIds, expiration);
            if (token.IsNotEmpty())
                Asp.Current.Response.Headers["X-Auth-Token"] = token;

            return principal;
        }


        //-----------------------------------------------
        // 令牌信息
        //-----------------------------------------------
        /// <summary>Current user</summary>
        public static ClaimsPrincipal User => Asp.Current.User;

        /// <summary>Is login</summary>
        public static bool IsLogin(HttpContext context=null)
        {
            context = context ?? Asp.Current;
            if (context.User != null && context.User.Identity.IsAuthenticated)
                return true;
            return CheckHeaderAuth(context);
        }

        /// <summary>Get user id</summary>
        public static string GetUserId()
        {
            if (!IsLogin())
                return "";
            return Asp.Current.User.Claims.FirstOrDefault(x => x.Type == "UserId")?.Value ?? "";
        }
        /// <summary>Get user name</summary>
        public static string GetUserName()
        {
            if (IsLogin())
                return Asp.Current.User.Claims.Where(x => x.Type == "UserName").FirstOrDefault().Value;
            return "";
        }

        /// <summary>Get role ids</summary>
        public static List<T> GetRoles<T>() where  T: struct
        {
            var roleIds = new List<T>();
            if (IsLogin())
            {
                string text = Asp.Current.User.Claims.Where(x => x.Type == "RoleIds").FirstOrDefault().Value;
                roleIds.AddRange(text.Split<T>());
            }
            return roleIds;
        }

        /// <summary>Is current login user in a specific role</summary>
        public static bool HasRole(string role)
        {
            if (IsLogin())
                return Asp.Current.User.IsInRole(role);
            return false;
        }


        //-----------------------------------------------
        // Bearer token
        //-----------------------------------------------
        // DES key must be 8 chars for legacy helper compatibility.
        // TODO：标准 JWT（含签名密钥轮换、aud/iss、refresh token）
        const string BearerEncryptKey = "a8p1L2t3";

        public class BearerTicket
        {
            public string UserId { get; set; }
            public string UserName { get; set; }
            public string RoleIds { get; set; }
            public DateTime ExpireDt { get; set; }
        }


        //-----------------------------------------------
        // Header bearer token 令牌
        // http header: Authorization: Bearer token
        //-----------------------------------------------
        public static string CreateBearerToken(ClaimsPrincipal principal, DateTime expiration)
        {
            if (principal == null)
                return "";

            string userId   = principal.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value ?? "";
            string userName = principal.Claims.FirstOrDefault(c => c.Type == "UserName")?.Value ?? "";
            string roleIds   = principal.Claims.FirstOrDefault(c => c.Type == "RoleIds")?.Value ?? "";
            if (userId.IsEmpty() || userName.IsEmpty())
                return "";

            return CreateBearerToken(userId, userName, roleIds, expiration);
        }

        public static string CreateBearerToken(string userId, string userName, string roleIds, DateTime expiration)
        {
            var ticket = new BearerTicket
            {
                UserId = userId ?? "",
                UserName = userName ?? "",
                RoleIds = roleIds ?? "",
                ExpireDt = expiration
            };
            return ticket.ToJson().DesEncrypt(BearerEncryptKey);
        }


        public static bool CheckHeaderAuth(HttpContext context = null)
        {
            context = context ?? Asp.Current;
            if (context == null)
                return false;
            if (context.User != null && context.User.Identity != null && context.User.Identity.IsAuthenticated)
                return true;

            // Check header token
            var token = GetHeaderToken();
            if (!TryCreatePrincipal(token, out var principal))
                return false;

            context.User = principal;
            return true;
        }

        /// <summary>Get header bearer token</summary>
        public static string GetHeaderToken()
        {
            var auth = Asp.Current.Request.Headers["Authorization"].FirstOrDefault();
            if (auth.IsEmpty())
                return "";

            var text = auth.Trim();
            if (text.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return text.Substring(7).Trim();

            var token = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            return token;
        }

        /// <summary>Try create principal from bearer token</summary>
        static bool TryCreatePrincipal(string token, out ClaimsPrincipal principal)
        {
            principal = null;
            if (token.IsEmpty())
                return false;
            try
            {
                var json = token.DesDecrypt(BearerEncryptKey);
                var ticket = json.ParseJson<BearerTicket>();
                if (ticket == null)
                    return false;

                if (ticket.UserId.IsEmpty() || ticket.UserName.IsEmpty())
                    return false;
                if (ticket.ExpireDt <= DateTime.Now)
                    return false;

                var claims = new[]
                {
                    new Claim("UserId", ticket.UserId),
                    new Claim("UserName", ticket.UserName),
                    new Claim("RoleIds", ticket.RoleIds ?? ""),
                    new Claim(ClaimTypes.NameIdentifier, ticket.UserId),
                    new Claim(ClaimTypes.Name, ticket.UserName)
                };
                var identity = new ClaimsIdentity(claims, "Bearer");
                principal = new ClaimsPrincipal(identity);
                return true;
            }
            catch
            {
                return false;
            }
        }


    }
}
