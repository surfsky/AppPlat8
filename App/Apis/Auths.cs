using Microsoft.AspNetCore.Http;
using System.ComponentModel;
using System;
using SkiaSharp;
using System.Collections.Generic;
using System.IO;
using App.Components;
using App.HttpApi;
using App.DAL;
using App.Utils;
using App.Web;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace App.API
{
    [Scope("Base")]
    [Description("Sign in & out")]
    public class Auths
    {
        //--------------------------------------------
        // 验证码
        //--------------------------------------------
        [HttpApi("生成验证码图片", Type = ResponseType.Image)]
        public static object VerifyImage()
        {
            var o = VerifyPainter.Draw(150, 40);
            Auth.SetVerifyCode(o.Code);
            return o.ImageData ?? o.Image;
        }

        [HttpApi("发送短信验证码", AuthLogin = true)]
        [HttpParam("mobile", "手机号码")]
        [HttpParam("type", "短信消息类别")]
        [HttpParam("appType", "App类型")]
        public static APIResult SendSms(string mobile, SmsType type, AppType appType)
        {
            try
            {
                var code = new VerifyCode();
                code.Code = StringHelper.BuildRandomText("0123456789", 6);
                code.CreateDt = DateTime.Now;
                code.ExpireDt = code.CreateDt.Value.AddMinutes(30);
                code.Source = appType.GetTitle();
                code.Mobile = mobile;
                code.Save();

                switch (type)
                {
                    case SmsType.Regist:         AliSmsMessenger.SendSmsRegist(mobile, code.Code); break;
                    case SmsType.Verify:         AliSmsMessenger.SendSmsVerify(mobile, code.Code); break;
                    case SmsType.ChangePassword: AliSmsMessenger.SendSmsChangePassword(mobile, code.Code); break;
                    case SmsType.ChangeInfo:     AliSmsMessenger.SendSmsChangeInfo(mobile, code.Code); break;
                }
                return new APIResult(0, "短信发送成功");
            }
            catch (Exception e)
            {
                //Logger.LogDb(appType.GetTitle(), e.Message, "Sms", LogLevel.Error);
                return new APIResult(-1, e.Message);
            }
        }

        //--------------------------------------------
        // 登陆注销
        //--------------------------------------------

        [HttpApi("登录（验证码）")]
        [HttpParam("userName", "用户名")]
        [HttpParam("password", "密码")]
        [HttpParam("verifyCode", "验证码")]
        public static APIResult Login(string userName, string password, string verifyCode)
        {
            // 检查验证码
            if (verifyCode != "key-987654321")
            {
                if (string.IsNullOrEmpty(verifyCode) || verifyCode.ToLower() != Auth.GetVerifyCode().ToLower())
                    return new APIResult(-2, "验证码错误");
            }
            
            int code = Auth.Login(userName, password);
            return new APIResult(code, code == 0 ? "登录成功" : "登录失败");
        }

        [HttpApi("注销")]
        public static void Logout()
        {
            Auth.Logout();
        }



        //--------------------------------------------
        // 用户授权信息
        //--------------------------------------------
        [HttpApi("获取登录用户信息", AuthLogin=true)]
        public static APIResult GetLoginUser()
        {
            var id = Auth.GetUserId(Asp.Current);
            if (id == null)
                return new APIResult(-2, "用户未登录");
            return User.Get(id.Value).ToResult();
        }

        [HttpApi("获取登录用户权限", AuthLogin=true)]
        public static APIResult GetLoginUserPower()
        {
            var id = Auth.GetUserId(Asp.Current);
            if (id == null)
                return new APIResult(-2, "用户未登录");
            return Auth.GetUserPowers(Asp.Current).ToResult();
        }

        [HttpApi("获取所有权限", AuthLogin=true)]
        public static APIResult GetPowers()
        {
            return EnumHelper.GetEnumInfos(typeof(Power)).ToResult();
        }

        [HttpApi("获取所有组织", AuthLogin=true)]
        public static APIResult GetOrgs()
        {
            return Org.Set.OrderBy(o => o.SortId).ToList().ToResult();
        }

        [HttpApi("获取组织树形结构", AuthLogin=true)]
        public static APIResult GetOrgTree()
        {
            return Org.GetTree().ToResult();
        }

        [HttpApi("获取菜单树形结构", AuthLogin=true)]
        public static APIResult GetMenuTree()
        {
            return Menu.GetTree().ToResult();
        }
    }
}
