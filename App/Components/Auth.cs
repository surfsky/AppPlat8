using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using App.DAL;
using App.Utils;
using App.Web;
using App.Entities;

namespace App.Components
{
    /// <summary>
    /// 授权鉴权相关的辅助方法
    /// </summary>
    public static class Auth
    {
        public const string MSG_CHECK_POWER_FAIL_PAGE = "您无权访问此页面！";
        public const string MSG_CHECK_POWER_FAIL_ACTION = "您无权进行此操作！";
        public const string SESSION_VERIFYCODE = "session_code";                  // 验证码Session名称
        public const string MSG_ONLINE_UPDATE_TIME = "OnlineUpdateTime";

        //--------------------------------------------------
        // 登录注销
        //--------------------------------------------------
        /// <summary>注销</summary>
        public static void Logout()
        {
            Asp.Current.SignOutAsync();
            Asp.Current.Session.Clear();
            Asp.Response.Redirect("/Login");
        }

        public static string GetVerifyCode()
        {
            return Asp.Current.Session.GetString(SESSION_VERIFYCODE);
        }
        public static void SetVerifyCode(string code)
        {
            Asp.Current.Session.SetString(SESSION_VERIFYCODE, code);
        }


        /// <summary>登录</summary>
        public static int Login(string userName, string password, string verifyCode)
        {
            // Backdoor for automated testing
            if (string.IsNullOrEmpty(GetVerifyCode()) || GetVerifyCode().ToLower() != verifyCode?.ToLower())
                return -4;
            return Login(userName, password);
        }

        /// <summary>登录</summary>
        /// <returns>
        /// 0  : 登录成功
        /// -1 : 用户不存在
        /// -2 : 用户未启用
        /// -3 : 用户名或密码错误
        /// </returns>
        public static int Login(string userName, string password)
        {
            User user = App.DAL.User.GetDetail(u => u.Name == userName);
            if (user == null)
                return -1;
            else
            {
                if (!PasswordUtil.ComparePasswords(user.Password, password))
                    return -1;
                else
                {
                    if (user.InUsed == false)
                        return -2;
                    else
                    {
                        LoginSuccess(user);
                        return 0;
                    }
                }
            }
        }


        /// <summary>登录成功，写入验票</summary>
        public static void LoginSuccess(User user)
        {
            RegisterOnlineUser(user.Id);

            // Aspnetcore 标准登录代码: Ticket验票--Principal主角--Identity身份--(1:n)--Claim属性
            var roleIds = user.Roles.Select(r => r.Id).Aggregate("", (a, b) => a + "," + b).TrimStart(','); // 用户角色Id列表字符串
            var claims = new[] { new Claim("UserId", user.Id.ToString()), new Claim("UserName", user.Name), new Claim("RoleIds", roleIds) }; // 属性
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);  // 用户信息
            var principal = new ClaimsPrincipal(identity); // 用户
            Asp.Current.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties() { IsPersistent = true, ExpiresUtc = DateTime.UtcNow.AddDays(7) }
                );

        }

        /// <summary>当前登录用户标识符</summary>
        public static int? GetUserId(HttpContext context=null)
        {
            context = context ?? Asp.Current;
            if (!context.User.Identity.IsAuthenticated)
                return null;

            var userId = context.User.Claims.Where(x => x.Type == "UserId").FirstOrDefault().Value;
            return Convert.ToInt32(userId);
        }



        /// <summary>当前登录用户名</summary>
        public static string GetUserName(HttpContext context=null)
        {
            context = context ?? Asp.Current;
            if (!context.User.Identity.IsAuthenticated)
                return null;

            var userName = context.User.Claims.Where(x => x.Type == "UserName").FirstOrDefault().Value;
            return userName;
        }

        /// <summary>获取当前用户信息</summary>
        public static App.DAL.User GetUser()
        {
            var userId = GetUserId();
            return userId.HasValue ? App.DAL.User.GetDetail(u => u.Id == userId.Value) : null;
        }


        //--------------------------------------------------
        // 在线用户
        //--------------------------------------------------
        public static void UpdateOnlineUser(long? userId)
        {
            if (userId == null)
                return;
            Online.Get(t => t.UserId == userId)?.Let(x => x.UpdateDt = DateTime.Now).Save();

            DateTime now = DateTime.Now;
            object lastUpdateTime = Asp.Session.GetObject<DateTime>(MSG_ONLINE_UPDATE_TIME);
            if (lastUpdateTime == null || (Convert.ToDateTime(lastUpdateTime).Subtract(now).TotalMinutes > 5))
            {
                // 记录本次更新时间
                Asp.Session.SetObject<DateTime>(MSG_ONLINE_UPDATE_TIME, now);
            }
        }

        public static void RegisterOnlineUser(long userId)
        {
            var online = Online.Get(t => t.UserId == userId) ?? new Online();
            online.UserId = userId;
            online.LastIP = Asp.Request.HttpContext.Connection.RemoteIpAddress.ToString();
            online.LastLoginDt = DateTime.Now;
            online.Save();

            // 记录本次更新时间
            Asp.Session.SetObject<DateTime>(MSG_ONLINE_UPDATE_TIME, DateTime.Now);
        }

        /// <summary>在线人数</summary>
        public static  async Task<int> GetOnlineCountAsync()
        {
            DateTime lastM = DateTime.Now.AddMinutes(-15);
            return await Online.Set.Where(o => o.UpdateDt > lastM).CountAsync();
        }


        //--------------------------------------------------
        // 权限校验
        //--------------------------------------------------
        public static bool CheckRole(string roleName, HttpContext context=null)
        {
            context = context ?? Asp.Current;
            return context.User.IsInRole(roleName);
        }

        /// <summary>检查当前用户是否拥有某个权限</summary>
        public static bool CheckPower(HttpContext context, Power power)
        {
            // 当前登陆用户的权限列表
            List<Power> powers = GetUserPowers(context);
            if (powers.Contains(power))
                return true;
            return false;
        }

        public static bool CheckPower(Power power)
        {
            return CheckPower(Asp.Current, power);
        }

        /// <summary>检查权限失败（页面回发）
        /// This used to use FineUI's Alert/PageContext. Now we emit a simple script that
        /// shows a browser alert so the client still sees a message when a POST fails.
        /// </summary>
        public static void CheckPowerFailWithAlert()
        {
            var script = $"<script>alert('{MSG_CHECK_POWER_FAIL_ACTION}');</script>";
            Asp.Current.Response.WriteAsync(script);
        }

        /// <summary>检查权限失败（页面第一次加载）</summary>
        public static void CheckPowerFailWithPage(HttpContext context)
        {
            string PageTemplate = "<!DOCTYPE html><html><head><meta http-equiv=\"Content-Type\" content=\"text/html;charset=utf-8\"/><head><body>{0}</body></html>";
            context.Response.WriteAsync(string.Format(PageTemplate, MSG_CHECK_POWER_FAIL_PAGE));
        }




        // http://blog.163.com/zjlovety@126/blog/static/224186242010070024282/
        // http://www.cnblogs.com/gaoshuai/articles/1863231.html
        /// <summary>当前登录用户的角色列表</summary>
        public static List<long> GetIdentityRoleIds(HttpContext context)
        {
            var roleIds = new List<long>();
            if (context.User.Identity.IsAuthenticated)
            {
                string userData = context.User.Claims.Where(x => x.Type == "RoleIds").FirstOrDefault().Value;
                foreach (string roleId in userData.Split(','))
                {
                    if (roleId.IsNotEmpty())
                        roleIds.Add(Convert.ToInt32(roleId));
                }
            }

            return roleIds;
        }

        /// <summary>获取当前登录用户拥有的全部权限列表</summary>
        public static List<Power> GetUserPowers(HttpContext context)
        {
            // 将用户拥有的权限列表保存在Session中，这样就避免每个请求多次查询数据库
            return Asp.GetSessionData<List<Power>>("UserPowers", () =>
            {
                var name = GetUserName(context);
                if (name.IsEmpty())
                    return new List<Power>();

                if (name == "admin")
                    return Enum.GetValues(typeof(Power)).Cast<Power>().ToList();

                var user = User.Set.FirstOrDefault(t => t.Name == name);
                return user.GetPowers();
            });
        }
    }
}