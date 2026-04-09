using App.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using App.Entities;

namespace App.DAL
{
    /// <summary>
    /// 开放平台-应用及身份验票。
    /// 理论上一个app 对应一个身份验票。
    /// 微信有个 token 替换时间段的概念，在此替换时间内，新旧token都有效。
    /// </summary>
    [UI("开放平台", "应用及验票")]
    public class OpenApp : EntityBase<OpenApp>
    {
        [UI("组织")]           public long? OrgID { get; set; }
        [UI("组织名称")]       public string OrgName { get; set; }

        [UI("应用名称")]       public string AppName { get; set; }
        [UI("应用Key")]        public string AppKey { get; set; }
        [UI("应用Secret")]     public string AppSecret { get; set; }
        [UI("OldToken")]       public string OldToken { get; set; }
        [UI("Token")]          public string Token { get; set; }
        [UI("Token时间戳")]    public string TimeStamp { get; set; }
        [UI("Token过期时间")]  public DateTime? ExpireDt { get; set; }

        /*
        public override object Export(ExportType type = ExportType.Normal)
        {
            return new
            {
                AppName,
                AppKey,
                AppSecret,
                OldToken,
                Token,
                TimeStamp,
                ExpireDt
            };
        }
        */

        /// <summary>创建验票字符串</summary>
        public static string CreateToken(string appKey, string appSecret, int minutes)
        {
            // 校对appKey和appSecet
            var app = OpenApp.Get(t => t.AppKey == appKey && t.AppSecret == appSecret);
            if (app == null)
                return "";

            // 保存旧验票，过度时间可用
            app.OldToken = app.Token;

            // 创建新验票
            var now = DateTime.Now;
            app.UpdateDt = now;
            app.TimeStamp = now.ToTimeStamp();
            app.ExpireDt = now.AddMinutes(minutes);

            // token 内容为key、timestamp、expiredt 组合编码而成
            var o = new { app.AppKey, app.TimeStamp, app.ExpireDt };
            app.Token = Encrypt(o.ToJson());
            app.Save();
            return app.Token;
        }

        /// <summary>检测验票字符串</summary>
        public static OpenApp CheckToken(string token)
        {
            // 确保 token 解析有效，appkey 有效
            var o = Decrypt(token).ParseJson<OpenApp>();
            if (o == null)
                return null;
            var app = OpenApp.Get(t => t.AppKey == o.AppKey);
            if (app == null)
                return null;

            // token 未过期，且与数据库核对一致
            var now = DateTime.Now;
            if (now <= o.ExpireDt && app.Token == token)
                return o;
            // token 在过渡期内，且与数据库旧 Token 一致
            else if (o.UpdateDt != null && now <= o.UpdateDt.Value.AddMinutes(5) && app.OldToken == token)
                return o;
            return null;
        }


        // Token 加解密算法（建议隔一段时间更改一次）
        static string Encrypt(string text)
        {
            return text.DesEncrypt("12345678");
        }
        static string Decrypt(string text)
        {
            return text.DesDecrypt("12345678");
        }


    }
}
