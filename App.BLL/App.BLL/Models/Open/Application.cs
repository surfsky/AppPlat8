using System;
using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL
{
    /// <summary>
    /// App 类型
    /// </summary>
    public enum AppType : int
    {
        [UI("Web")]           Web = 0,
        [UI("MobileWeb")]     MobileWeb = 1,
        [UI("Tester")]        Tester = 2,

        [UI("iOS")]           iOS = 10,
        [UI("Android")]       Android = 11,
        [UI("Windows")]       Windows = 12,
        [UI("Mac")]           Mac = 13,
        [UI("Linux")]         Linux = 14,

        [UI("微信小程序")]    WechatMP = 20,
        [UI("支付宝小程序")]  AlipayMP = 21,
        [UI("钉钉小程序")]    DingTalkMP = 22
    }


    /// <summary>
    /// 接入应用
    /// </summary>
    public class Application : EntityBase<Application>
    {
        // 基础
        [UI("用户")]           public long? UserId { get; set; }
        [UI("应用类型")]        public AppType AppType { get; set; } = AppType.Web;
        [UI("应用名称")]        public string AppKey { get; set; }
        [UI("应用密钥")]        public string AppSecret { get; set;} 
    }

    /// <summary>
    /// 应用接口身份验票
    /// </summary>
    public class Token
    {
        public string AppKey { get; set; }
        public string TimeStamp { get; set; }
        public DateTime ExpireDt { get; set; }

        /// <summary>默认构造函数</summary>
        public Token(string appKey, string timeStamp, DateTime expireDt)
        {
            AppKey = appKey;
            TimeStamp = timeStamp;
            ExpireDt = expireDt;
        }

        /// <summary>创建验票字符串</summary>
        public static string Create(string appKey, string appSecret, int minutes)
        {
            // 在数据库中检测 appKey 和 appSecret 是否有效
            var app = Application.Set.FirstOrDefault(a => a.AppKey == appKey && a.AppSecret == appSecret);
            if (app == null)
                throw new Exception("appKey 或 appSecret 无效");

            // 创建验票字符串
            var now = DateTime.Now;
            var o = new Token(appKey, now.ToTimeStamp(), now.AddMinutes(minutes));
            return o.ToJson().DesEncrypt("12345687");
        }

        /// <summary>检测验票字符串</summary>
        public static Token Check(string tokenText)
        {
            var o = tokenText.DesDecrypt("12345687").ParseJson<Token>();
            if (o != null && o.ExpireDt > DateTime.Now)
                return o;
            return null;
        }
    }    
}