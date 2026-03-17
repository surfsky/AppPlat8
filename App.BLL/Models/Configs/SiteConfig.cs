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
        // 基础
        [UI("基础", "网站标题")]           public string Title { get; set; }
        [UI("基础", "备案号")]             public string BeiAnNo { get; set; }

        // UI
        [UI("UI", "网站主题")]             public string Theme { get; set; }
        [UI("UI", "网站图标")]             public string Icon { get; set; }
        [UI("UI", "登陆页背景图片")]        public string LoginBg { get; set; }

        // 数据
        [UI("数据", "分页大小")]            public int    PageSize              { get; set; } = 50;
        [UI("数据", "默认密码")]            public string DefaultPassword       { get; set; } = "Abc@123";
        [UI("数据", "可上传文件类型")]       public string UpFileTypes           { get; set; } = ".gif, .png, .jpg, .jpeg, .bmp, .mp3, .mp4, .doc, .docx, .xls, .xlsx, .ppt, .pptx, .pdf, .cdr";
        [UI("数据", "可上传文件大小（M）")]   public long?  UpFileSize            { get; set; } = 50;

    }
}