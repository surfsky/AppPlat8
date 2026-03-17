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
    /// </summary>
    public class AuthHelper
    {
        //-----------------------------------------------
        // 
        //-----------------------------------------------
        /// <example>AuthHelper.Login("123", "Admin", "1,2,3", DateTime.Now.AddDays(1));</example>
        public static ClaimsPrincipal Login(string userId, string userName, string roleIds, DateTime expiration)
        {
            var claims = new[] { new Claim("UserId", userId), new Claim("UserName", userName), new Claim("RoleIds", roleIds) };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            Asp.Current.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties() { IsPersistent = false }
                );
            return principal;
        }

        /// <summary>Logout</summary>
        public static void Logout()
        {
            Asp.Current.SignOutAsync();
            Asp.Current.Session.Clear();
        }

        //-----------------------------------------------
        // 
        //-----------------------------------------------
        /// <summary>Current user</summary>
        public static ClaimsPrincipal User => Asp.Current.User;

        /// <summary>Is login</summary>
        public static bool IsLogin()
        {
            return (Asp.Current.User != null && Asp.Current.User.Identity.IsAuthenticated);
        }

        /// <summary>Get user id</summary>
        public static string GetUserId()
        {
            return IsLogin() ? Asp.Current.User.Identity.Name : "";
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
    }
}
