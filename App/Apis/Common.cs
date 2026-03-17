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

namespace App.API
{
    [Scope("Base")]
    [Description("通用接口")]
    public class Common
    {
        //--------------------------------------------
        // 网站信息
        //--------------------------------------------
        [HttpApi("网站信息", CacheSeconds = 60 * 60, AuthLogin=true)]
        public static APIResult GetSiteInfo()
        {
            var site = SiteConfig.Instance;
            return new
            {
                site.Title,
                site.Icon,
                site.BeiAnNo,
                site.LoginBg,
            }.ToResult();
        }


        //--------------------------------------------
        // 图片/文件处理
        //--------------------------------------------
        [HttpApi("生成缩略图", Type = ResponseType.Image)]
        [HttpParam("u", "url。图像地址，支持...~/ 等路径表达式，请先用UrlEncode处理，且路径短于256个字符。")]
        [HttpParam("w", "width")]
        [HttpParam("h", "height")]
        public static SKBitmap Thumbnail(string u, int w, int? h = null)
        {
            // 尝试从缓存文件中获取文件
            var cacheCode = string.Format("{0}-{1}-{2}", u, w, h).MD5();
            string cacheFile = $"/Caches/{cacheCode}.cache";
            string path = Asp.MapPath(cacheFile);
            if (File.Exists(path))
                return Painter.LoadImage(path);

            // 创建缩略图
            IO.PrepareDirectory(path);
            if (w > 1000) w = 1000;
            if (h != null && h > 1000) h = 1000;
            SKBitmap img = HttpHelper.GetThumbnail(u, w, h);
            if (img != null)
            {
                using (var image = SKImage.FromBitmap(img))
                using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                using (var stream = File.OpenWrite(path))
                {
                    data.SaveTo(stream);
                }
            }
            return img;
        }

        [HttpApi("Upload file", PostFile = true, AuthLogin = true)]
        [HttpParam("folder", "file folder, eg. Articles")]
        [HttpParam("fileName", "file name, eg. a.png")]
        public APIResult Upload(string folder, string fileName)
        {
            var exts = new List<string> { ".jpg", ".png", ".gif", ".mp3", ".mp4", ".txt", ".md" };
            var ext = fileName.GetFileExtension();
            if (!exts.Contains(ext))
                return new APIResult(-1, "File deny", 13);

            // 构造存储路径
            var url = Uploader.GetSavePath(folder, fileName);
            var path = Asp.MapPath(url);
            var fi = new FileInfo(path);
            if (!fi.Directory.Exists)
                Directory.CreateDirectory(fi.Directory.FullName);

            // 存储第一个文件
            var files = Asp.Request.Form.Files;
            if (files.Count == 0)
                return new APIResult(-1, "File doesn't exist", 11);
            using (var stream = File.Create(path))
                files[0].CopyToAsync(stream);
            return new APIResult(0, url);
        }




        //--------------------------------------------
        // 辅助
        //--------------------------------------------
        [HttpApi("生成Guid")]
        public static string Guid()
        {
            return System.Guid.NewGuid().ToString("N");
        }

        [HttpApi("获取枚举信息")]
        [HttpParam("enumType", "枚举类型。如App.DAL.ArticleType")]
        public static APIResult GetEnumInfos(string enumType)
        {
            var type = Reflector.GetType(enumType);
            if (type != null && type.IsEnum)
                return type.GetEnumInfos().ToResult();
            return new APIResult(-1);
        }


    }
}
