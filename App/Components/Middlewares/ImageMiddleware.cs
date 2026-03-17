using App.Utils;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using App.Web;
using Microsoft.AspNetCore.Builder;

namespace App.Middlewares
{
    /// <summary>
    /// 图片水印及缩略图处理器。
    /// 使用：app.UseImage(...); 请放在 app.UseStatic()前面。
    /// 图像url地址如：http://a.b.com/Pictury.png?w=200
    /// </summary>
    public class ImageMiddleware
    {
        public static List<string> Extensions = new List<string>() { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff" };

        private RequestDelegate _next;
        public ImageMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var path = context.Request.Path.Value;  // 无 host 和 querystring。如：/res/icon/add.png
            var ext = path.GetFileExtension();
            if (!Extensions.Contains(ext))
            {
                await _next.Invoke(context);
                return;
            }

            // 原图路径校验
            var rawPath = Asp.MapPath(path);
            if (!File.Exists(rawPath) && Asp.QueryString.IsEmpty())
            {
                //Asp.Error(404, "Not found");  // 因为 wwwroot 的原因，MapPath 出来的路径可能不对，就不做处理了
                await _next.Invoke(context);
                return;
            }

            // 原图输出
            var pathAndQuery = path + context.Request.QueryString;
            var mimeType = path.GetMimeType();
            var w = Asp.GetQueryInt("w");
            if (w == null)
            {
                Asp.WriteFile(rawPath, mimeType: mimeType);
                return;
            }

            // 缩略图参数
            var h = Asp.GetQueryInt("h");
            if (w > 1000) w = 1000;
            if (h != null && h > 1000) h = 1000;
            var key = pathAndQuery.ToLower().MD5();

            // 缩略图缓存策略
            var cachePath = Asp.MapPath($"/Caches/{key}.cache");
            if (!File.Exists(cachePath))
            {
                IO.PrepareDirectory(cachePath);
                using (var img = Painter.Thumbnail(rawPath, w.Value, h))
                {
                    if (img != null)
                    {
                        using (var data = img.Encode(SKEncodedImageFormat.Png, 100))
                        using (var stream = File.OpenWrite(cachePath))
                        {
                            data.SaveTo(stream);
                        }
                    }
                }
            }
            Asp.WriteFile(cachePath, mimeType: mimeType);
        }
    }

    /// <summary>封装成扩展方法</summary>
    public static class ImageMiddlewareExtension
    {
        /// <summary>使用图片处理中间件（支持图片放缩、缓存逻辑）</summary>
        public static IApplicationBuilder UseImager(this IApplicationBuilder app, Action<List<string>> options=null)
        {
            options?.Invoke(ImageMiddleware.Extensions);
            return app.UseMiddleware<ImageMiddleware>();
        }
    }

}
