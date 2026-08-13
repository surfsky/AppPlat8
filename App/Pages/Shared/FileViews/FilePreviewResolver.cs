using System;
using System.IO;
using App.DAL;
using App.Entities;

namespace App.Pages.Shared.FileViews
{
    /// <summary>统一解析文件预览页所需的附件上下文。</summary>
    public static class FilePreviewResolver
    {
        /// <summary>根据附件id或显式src/name参数解析预览上下文。</summary>
        public static PreviewFileInfo Resolve(string idText, string sourceUrl, string fileName)
        {
            long.TryParse((idText ?? string.Empty).Trim(), out var id);
            return Resolve(id, sourceUrl, fileName);
        }

        /// <summary>根据附件id或显式src/name参数解析预览上下文。</summary>
        public static PreviewFileInfo Resolve(long id, string sourceUrl, string fileName)
        {
            var info = new PreviewFileInfo
            {
                Id = id,
                SourceUrl = (sourceUrl ?? string.Empty).Trim(),
                FileName = (fileName ?? string.Empty).Trim()
            };

            if (id > 0)
            {
                var item = Att.Get(id);
                if (item != null)
                {
                    var rawName = string.IsNullOrWhiteSpace(item.FileName)
                        ? Path.GetFileName(item.Url ?? string.Empty)
                        : item.FileName.Trim();
                    info.FileName = rawName;
                    info.SourceUrl = $"/Shared/FileViews/Viewer?handler=Content&id={id}";
                    info.PhysicalPath = App.Web.Asp.MapPath(item.Content);
                }
            }

            if (string.IsNullOrWhiteSpace(info.FileName))
            {
                var srcText = info.SourceUrl ?? string.Empty;
                var q = srcText.IndexOf('?');
                if (q >= 0) srcText = srcText.Substring(0, q);
                var hash = srcText.IndexOf('#');
                if (hash >= 0) srcText = srcText.Substring(0, hash);
                info.FileName = Path.GetFileName(srcText.Replace('\\', '/'));
            }

            if (string.IsNullOrWhiteSpace(info.PhysicalPath))
            {
                info.PhysicalPath = TryResolveStaticSourcePath(info.SourceUrl);
            }

            info.FileExt = Path.GetExtension(info.FileName ?? string.Empty).TrimStart('.').ToLower();
            return info;
        }

        /// <summary>尝试从静态预览链接中反解物理文件路径。</summary>
        private static string TryResolveStaticSourcePath(string sourceUrl)
        {
            if (string.IsNullOrWhiteSpace(sourceUrl))
                return string.Empty;

            try
            {
                var url = new Uri(sourceUrl, UriKind.RelativeOrAbsolute);
                var query = url.IsAbsoluteUri ? url.Query : sourceUrl;
                var marker = query.IndexOf('?');
                if (marker >= 0)
                    query = query.Substring(marker);
                var collection = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(query);
                var handler = collection.TryGetValue("handler", out var handlerValue)
                    ? handlerValue.ToString().Trim()
                    : string.Empty;
                if (!string.Equals(handler, "StaticContent", StringComparison.OrdinalIgnoreCase))
                    return string.Empty;

                var fileText = collection.TryGetValue("file", out var fileValue)
                    ? fileValue.ToString().Trim()
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(fileText))
                    return string.Empty;

                var safePath = fileText.Replace('\\', '/').Trim().TrimStart('/');
                if ((!safePath.StartsWith("Samples/", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(safePath, "Samples", StringComparison.OrdinalIgnoreCase))
                    || safePath.Contains("..", StringComparison.Ordinal))
                    return string.Empty;

                var root = Path.Combine(Directory.GetCurrentDirectory(), "Files");
                var fullPath = Path.GetFullPath(Path.Combine(root, safePath.Replace('/', Path.DirectorySeparatorChar)));
                var rootPath = Path.GetFullPath(root);
                if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                    return string.Empty;

                return File.Exists(fullPath) ? fullPath : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <summary>文件预览上下文。</summary>
    public class PreviewFileInfo
    {
        public long Id { get; set; }

        public string SourceUrl { get; set; }

        public string FileName { get; set; }

        public string FileExt { get; set; }

        public string PhysicalPath { get; set; }
    }
}
