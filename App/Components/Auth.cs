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
            var name = GetUserName(Asp.Current) ?? "";
            var userId = GetUserId(Asp.Current);
            try
            {
                Logger.LogAudit("Auth", "Logout", $"用户注销。账号={name} UserId={userId} IP={Asp.ClientIP}");
            }
            catch { }
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
            if (string.IsNullOrEmpty(GetVerifyCode()) || GetVerifyCode().ToLower() != verifyCode?.ToLower())
            {
                var ip = Asp.ClientIP;
                // 登录尚未成功，Cookie Claim 还没写，必须显式传 operator/userName/ip 给 Logger，否则 LogDb 里取 Auth.GetUserName() 是空
                Logger.LogDb(LogLevel.Info, user: userName,
                    from: "Auth/LoginFail",
                    message: $"账号={userName} 登录失败：验证码错误。IP={ip}");
                return -4;
            }
            return Login(userName, password);
        }

        /// <summary>登录</summary>
        public static int Login(string userName, string password)
        {
            User user = App.DAL.User.GetDetail(u => u.Name == userName);
            string ip = Asp.ClientIP;
            if (user == null)
            {
                Logger.LogDb(LogLevel.Info, user: userName, from: "Auth/LoginFail",
                    message: $"账号={userName} 登录失败：用户不存在。IP={ip}");
                return -1;
            }
            if (!PasswordUtil.ComparePasswords(user.Password, password))
            {
                Logger.LogDb(LogLevel.Info, user: userName, from: "Auth/LoginFail",
                    message: $"账号={userName} 登录失败：密码错误。IP={ip}");
                return -3;
            }
            if (user.IsDel == true)
            {
                Logger.LogDb(LogLevel.Info, user: userName, from: "Auth/LoginFail",
                    message: $"账号={userName} 登录失败：用户已失效（IsDel=true）。IP={ip}");
                return -2;
            }
            LoginSuccess(user);
            return 0;
        }

        public static string CreateBearerToken()
        {
            return AuthHelper.CreateBearerToken(Asp.Current.User, DateTime.Now.AddDays(7));
        }


        /// <summary>登录成功，写入Cookie验票</summary>
        public static void LoginSuccess(User user)
        {
            RegisterOnlineUser(user.Id);

            // Aspnetcore 标准登录代码: Ticket验票--Principal主角--Identity身份--(1:n)--Claim属性
            var roleIds = user.Roles.Select(r => r.Id).Aggregate("", (a, b) => a + "," + b).TrimStart(',');
            AuthHelper.Login(user.Id.ToString(), user.Name, roleIds, DateTime.Now.AddDays(7));

            // 登录审计日志：注意 AuthHelper.Login 先写了 ClaimsPrincipal，再写日志
            // 此时如果走默认的 LogAudit→Auth.GetUserName()，部分场景（Auth.GetUserName 取 Session）仍取不到，
            // 所以直接显式传 operator + IP（Asp.ClientIP 与 Request 无关，可独立取）
            var ua = Asp.Request?.Headers["User-Agent"].FirstOrDefault();
            var ip = Asp.ClientIP;
            Logger.LogDb(LogLevel.Info,
                user: user.Name,
                from: "Auth/Login",
                message: $"用户登录成功。账号={user.Name} 姓名={user.NickName ?? user.RealName ?? "-"} 手机号={user.Mobile ?? "-"} IP={ip} 浏览器={ua ?? "-"}");
        }

        /// <summary>当前登录用户标识符</summary>
        public static long? GetUserId(HttpContext context=null)
        {
            context = context ?? Asp.Current;
            if (!IsLogin(context))
                return null;

            var userId = context.User.Claims.Where(x => x.Type == "UserId").FirstOrDefault()?.Value;
            if (string.IsNullOrWhiteSpace(userId))
                return null;
            return Convert.ToInt64(userId);
        }
        
        /// <summary>当前用户是否已登录</summary>
        public static bool IsLogin(HttpContext context=null)
        {
            return AuthHelper.IsLogin(context);
        }


        /// <summary>当前登录用户名</summary>
        public static string GetUserName(HttpContext context=null)
        {
            context = context ?? Asp.Current;
            if (!IsLogin(context))
                return null;

            var userName = context.User.Claims.Where(x => x.Type == "UserName").FirstOrDefault()?.Value;
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
        /// 把错误发到客户端，客户端弹出提示框
        /// </summary>
        public static void WritePowerFailAlert()
        {
            var script = $"<script>alert('{MSG_CHECK_POWER_FAIL_ACTION}');</script>";
            Asp.Current.Response.WriteAsync(script); // 考虑改成 EleManager.Alert
        }

        /// <summary>检查权限失败（页面第一次加载）</summary>
        public static void WritePowerFailPage(HttpContext context)
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
                        roleIds.Add(Convert.ToInt64(roleId));
                }
            }

            return roleIds;
        }

        /// <summary>获取当前登录用户拥有的全部权限列表</summary>
        public static List<Power> GetUserPowers(HttpContext context)
        {
            return Asp.GetSessionData<List<Power>>("UserPowers", () =>
            {
                var name = GetUserName(context);
                if (name.IsEmpty())
                    return new List<Power>();
                if (name == "admin")
                    return Enum.GetValues(typeof(Power)).Cast<Power>().ToList();
                var user = User.Set.FirstOrDefault(t => t.Name == name);
                if (user == null)
                    return new List<Power>();

                // 版本号（long/秒级ticks）写入 Session：下次请求对比DB版本，不同则缓存失效
                var ver = user.GetPermissionVersion();
                Asp.SetSession("UserPowersVer", ver);
                return user.GetPowers();
            },
            // 版本检查器：每次命中缓存前先判断版本是否变化，变化则重取权限
            validate: () =>
            {
                var name = GetUserName(context);
                if (name.IsEmpty() || name == "admin") return true;

                var cachedVerObj = Asp.GetSession("UserPowersVer");
                long cachedVer = (cachedVerObj is long lv) ? lv : (cachedVerObj is int iv ? iv : -1);
                var user = User.Set.FirstOrDefault(t => t.Name == name);
                long curVer = user?.GetPermissionVersion() ?? -2;
                if (curVer == -2) return true;
                return cachedVer == curVer;
            });
        }
    }
}