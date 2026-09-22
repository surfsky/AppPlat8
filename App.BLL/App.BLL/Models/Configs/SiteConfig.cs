using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using App.Entities;
using App.Utils;

namespace App.DAL
{
    /// <summary>
    /// 站点配置
    /// </summary>
    public class SiteConfig : EntityBase<SiteConfig>
    {
        //--------------------------------------
        // 字段默认值兜底
        // 说明：SiteConfig 新列（PublicKey/PrivateKey 等）如果在老数据行里为 null，
        //       EF materializer 会把属性覆盖为 null，导致 C# 的 auto-property initializer 失效。
        //       因此关键安全字段统一用【private backing field + property getter null-coalescing】
        //       的形式，确保即使 DB 未 Seed / 列未回填 也能跑通 LoginAI。
        //--------------------------------------
        private string _publicKey;
        private string _privateKey;
        private bool?  _enableLoginAI;

        // 基础
        [UI("基础", "网站标题")]           public string Title { get; set; }
        [UI("基础", "备案号")]             public string BeiAnNo { get; set; }
        [UI("基础", "服务条款")]             public string Terms { get; set; }

        // UI
        [UI("UI", "网站主题")]             public string Theme { get; set; }
        [UI("UI", "网站图标")]             public string Icon { get; set; }
        [UI("UI", "登陆页背景图片")]        public string LoginBg { get; set; }

        // 地图
        [UI("地图", "Mapbox Key)")]        public string MapboxKey { get; set; }
        [UI("地图", "天地图 Key")]          public string TiandituKey { get; set; }
        [UI("地图", "驾驶舱标题")]           public string GisTitle { get; set; } = "数据驾驶舱";

        // 数据
        [UI("数据", "分页大小")]            public int    PageSize              { get; set; } = 50;
        [UI("数据", "默认密码")]            public string DefaultPassword       { get; set; } = "Abc@123";
        [UI("数据", "可上传文件类型")]       public string UpFileTypes           { get; set; } = ".gif, .png, .jpg, .jpeg, .bmp, .mp3, .mp4, .doc, .docx, .xls, .xlsx, .ppt, .pptx, .pdf, .cdr";
        [UI("数据", "可上传文件大小（M）")]   public long?  UpFileSize            { get; set; } = 50;

        // 安全：AI 调试登录 /LoginAI 的签名密钥（仅在 Development/显式开启时生效，绝对不要提交到公开 Git）
        [UI("安全", "AI 调试公钥（参与签名原文，可公开）")]
        public string PublicKey
        {
            get => _publicKey ?? (_publicKey = "PUB-AI-DEV-20260908-14fd9b8a2e3a4d7f95c5b514a6e9263a");
            set => _publicKey = value;
        }

        [UI("安全", "AI 调试私钥（HMAC-SHA256 密钥，保密）")]
        public string PrivateKey
        {
            get => _privateKey ?? (_privateKey = "PRV-AI-DEV-20260908-9b4c0a94c2ad43af807c13d3e0ef56ba19f0d2c94a3a472ab11a4a6a5980a718");
            set => _privateKey = value;
        }

        [UI("安全", "是否启用 /LoginAI（默认仅 Development 环境启用，设为 false 可强制关闭）")]
        public bool? EnableLoginAI
        {
            get => _enableLoginAI ?? true;
            set => _enableLoginAI = value;
        }

        /// <summary>检查文件扩展名是否在可上传文件类型中</summary>
        public static bool IsSupportFile(string ext)
        {
            var exts = SiteConfig.Instance.UpFileTypes.SplitString();
            return exts.Contains(ext);
        }
    }
}
