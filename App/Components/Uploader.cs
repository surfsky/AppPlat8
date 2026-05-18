using App.DAL;
using App.Utils;
using App.Web;
using SkiaSharp;
using System.Collections.Generic;
using System;
using Microsoft.AspNetCore.Http;
using System.IO;
using App.HttpApi;
using System.Text.Json;

namespace App.Components
{
    /// <summary>
    /// 文件相关辅助方法
    /// </summary>
    public class Uploader
    {
        //-----------------------------------------------------------
        // 通用
        //-----------------------------------------------------------
        /// <summary>获取上传文件要保存的虚拟路径</summary>
        /// <param name="folderName">文件夹名称</param>
        /// <return>返回虚拟路径，如/Files/User/1234567890.png</return>
        public static string GetSavePath(string folderName, string fileName = ".png")
        {
            // 默认保存在 /Files/ 目录下
            // 如果 folderName 以/开头，则保存在 folderName 目录下
            string folder = string.Format("~/Files/{0}", folderName);
            if (folderName != null && folderName.StartsWith("/"))
                folder = folderName;

            // 合并目录和文件名
            string extension = fileName.GetFileExtension();
            string path = string.Format("{0}/{1}{2}", folder, new SnowflakeId().NewId(), extension);
            return path.TrimStart("~");
            //return Asp.ResolveUrl(path);
        }

        //-----------------------------------------------------------
        // 获取上传后的图片文件URL
        //-----------------------------------------------------------
        /// <summary>获取附件URL列表（并处理Base64图片上传）</summary>
        /// <param name="folderName">要保存的目录名，如“User”</param>
        /// <param name="urlOrDatas">URL或Base64编码的图片数据列表</param>
        public static List<string> SaveFiles(string folderName, List<string> urlOrDatas)
        {
            var urls = new List<string>();
            if (urlOrDatas != null)
            {
                foreach (var urlOrData in urlOrDatas)
                    urls.Add(SaveFile(folderName, urlOrData));
            }
            return urls;
        }

        /// <summary>获取附件URL（并处理Base64图片上传）</summary>
        /// <param name="folderName">要保存的目录名，如“User”</param>
        /// <param name="urlOrdata">URL或Base64编码的图片数据</param>
        /// <returns>返回URL</returns>
        public static string SaveFile(string folderName, string urlOrdata)
        {
            if (urlOrdata.IsEmpty())
                return "";
            if (urlOrdata.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                return Uploader.SaveBase64Image(folderName, urlOrdata);
            if (TryParseClientBase64FilePayload(urlOrdata, out var payloadName, out var payloadData))
                return Uploader.SaveBase64File(folderName, payloadData, payloadName);
            if (urlOrdata.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return Uploader.SaveBase64File(folderName, urlOrdata, null);
            return urlOrdata;
        }

        /// <summary>上传 base64 编码的任意文件</summary>
        public static string SaveBase64File(string folderName, string dataUrl, string fileName = null)
        {
            if (dataUrl.IsEmpty())
                return "";

            var commaIndex = dataUrl.IndexOf(',');
            if (commaIndex <= 0 || commaIndex >= dataUrl.Length - 1)
                return "";

            var header = dataUrl.Substring(0, commaIndex);
            var base64Text = dataUrl.Substring(commaIndex + 1);
            if (!header.Contains(";base64", StringComparison.OrdinalIgnoreCase))
                return "";

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64Text);
            }
            catch
            {
                return "";
            }

            var ext = fileName.GetFileExtension();
            if (ext.IsEmpty())
                ext = ResolveExtensionFromDataUrlHeader(header);
            if (ext.IsEmpty())
                ext = ".bin";

            var safeName = fileName.IsEmpty()
                ? $"file{ext}"
                : Path.GetFileName(fileName);
            if (safeName.GetFileExtension().IsEmpty())
                safeName += ext;

            var url = GetSavePath(folderName, safeName);
            var path = Asp.MapPath(url);
            IO.PrepareDirectory(path);
            File.WriteAllBytes(path, bytes);
            return url;
        }

        private static bool TryParseClientBase64FilePayload(string text, out string fileName, out string dataUrl)
        {
            fileName = null;
            dataUrl = null;

            if (text.IsEmpty())
                return false;

            var trimmed = text.Trim();
            if (!trimmed.StartsWith("{"))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    return false;

                if (!doc.RootElement.TryGetProperty("__eleFileUpload", out var marker)
                    || marker.ValueKind != JsonValueKind.True)
                    return false;

                if (!doc.RootElement.TryGetProperty("data", out var dataEl)
                    || dataEl.ValueKind != JsonValueKind.String)
                    return false;

                dataUrl = dataEl.GetString();
                if (doc.RootElement.TryGetProperty("name", out var nameEl)
                    && nameEl.ValueKind == JsonValueKind.String)
                    fileName = nameEl.GetString();

                return dataUrl.IsNotEmpty();
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveExtensionFromDataUrlHeader(string header)
        {
            var h = (header ?? string.Empty).ToLowerInvariant();
            if (h.Contains("application/pdf")) return ".pdf";
            if (h.Contains("application/zip")) return ".zip";
            if (h.Contains("application/json")) return ".json";
            if (h.Contains("text/plain")) return ".txt";
            if (h.Contains("text/csv")) return ".csv";
            if (h.Contains("video/mp4")) return ".mp4";
            if (h.Contains("image/png")) return ".png";
            if (h.Contains("image/jpeg")) return ".jpg";
            if (h.Contains("image/gif")) return ".gif";
            return string.Empty;
        }

        //-----------------------------------------------------------
        // Base64 图像文件上传
        //-----------------------------------------------------------
        /// <summary>上传多张图片（Base64编码）</summary>
        /// <param name="folderName">保存目录名。如：Products</param>
        /// <param name="urlOrData">Base64 编码的图片字符串或Url数组</param>
        public static List<string> SaveBase64Images(string folderName, params string[] urlOrData)
        {
            var urls = new List<string>();
            foreach (var text in urlOrData)
            {
                if (text.IsEmpty())
                    continue;

                // 如果是base64图片文本，则上传后记录url
                if (text.IsBase64Image())
                {
                    var url = Uploader.SaveBase64Image(folderName, text);
                    if (!url.IsEmpty())
                        urls.Add(url);
                }
                // 否则直接记录url
                else
                {
                    urls.Add(text);
                }
            }
            return urls;
        }

        /// <summary>上传 base64 编码的图像</summary>
        public static string SaveBase64Image(string folderName, string imageData)
        {
            using (var image = Painter.ParseImage(imageData))
            {
                if (image != null)
                {
                    var url = GetSavePath(folderName);
                    var path = Asp.MapPath(url);
                    IO.PrepareDirectory(path);
                    using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                    using (var stream = File.OpenWrite(path))
                    {
                        data.SaveTo(stream);
                    }
                    return url;
                }
            }
            return "";
        }

        //-----------------------------------------------------------
        // IFormFile 文件上传
        //-----------------------------------------------------------
        /// <summary>上传文件</summary>
        /// <param name="folderName">上传目录名</param>
        /// <param name="fileName">文件名。若为空，则自动生成文件名。</param>
        public static string SaveFile(IFormFile file, string folderName, string fileName = "", bool checkExtension = true)
        {
            // 扩展名及校验
            string ext;
            if (file.FileName != "blob")
                ext = file.FileName.GetFileExtension();
            else
                ext = fileName.GetFileExtension();
            if (checkExtension)
            {
                var exts = SiteConfig.Instance.UpFileTypes.SplitString();
                if (!exts.Contains(ext))
                    throw new Exception("禁止上传该类型文件");
            }

            // 文件名和路径
            if (fileName.IsEmpty())
                fileName = string.Format("{0}{1}", SnowflakeId.Instance.NewId(), ext);
            var dir = folderName.IsEmpty() ? "/Files/" : string.Format("/Files/{0}/", folderName);

            // 保存
            var url = string.Format("{0}{1}", dir, fileName);
            if (!url.StartsWith("/"))
                url = "/" + url; // 确保url以/开头
            var path = Asp.MapPath(url);
            IO.PrepareDirectory(path);
            //file.SaveAs(path);
            using (var stream = new FileStream(path, FileMode.Create))
                file.CopyTo(stream);
            return url;
        }
    }
}
