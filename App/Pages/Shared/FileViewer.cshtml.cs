using System;
using System.Collections.Generic;
using System.IO;
using App.Components;
using App.DAL;
using App.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace App.Pages.Shared
{
    [CheckPower(Power.CheckObjectView)]
    public class FileViewerModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public string UniId { get; set; }

        [BindProperty(SupportsGet = true)]
        public long Id { get; set; }

        [BindProperty(SupportsGet = true)]
        public string FilePath { get; set; }

        public string FileName { get; set; }
        public string FileExt { get; set; }
        public string SourceUrl { get; set; }
        public string ViewerUrl { get; set; }
        public string DownloadUrl { get; set; }
        public string Error { get; set; }

        public void OnGet(string uniId, long id, string file)
        {
            UniId = uniId?.Trim();
            Id = id;
            FilePath = file?.Trim();

            if (!string.IsNullOrWhiteSpace(FilePath))
            {
                InitFromStaticFile(FilePath);
                return;
            }

            var item = GetAtt(UniId, id, out var err);
            if (item == null)
            {
                Error = err;
                return;
            }

            FileName = string.IsNullOrWhiteSpace(item.FileName)
                ? Path.GetFileName(item.Url ?? string.Empty)
                : item.FileName;
            FileExt = (item.FileExtension ?? string.Empty).Trim().TrimStart('.').ToLower();
            SourceUrl = $"/Shared/FileViewer?handler=Content&uniId={Uri.EscapeDataString(UniId)}&id={id}";
            ViewerUrl = BuildViewerUrl(SourceUrl, FileName, FileExt);
            DownloadUrl = $"/Shared/Atts?handler=Download&uniId={Uri.EscapeDataString(UniId)}&id={id}";
        }

        public IActionResult OnGetContent(string uniId, long id)
        {
            var item = GetAtt(uniId?.Trim(), id, out var err);
            if (item == null)
                return BuildResult(404, err);

            var path = App.Web.Asp.MapPath(item.Content);
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                return BuildResult(404, "文件不存在或已被删除");

            var ext = Path.GetExtension(item.FileName ?? item.Content);
            var mimeType = ResolveMimeType(path, ext);

            return PhysicalFile(path, mimeType);
        }

        public IActionResult OnGetStaticContent(string file, bool download = false)
        {
            if (!TryResolveStaticFile(file, out var safePath, out var fullPath, out var err))
                return BuildResult(404, err);

            var ext = Path.GetExtension(fullPath);
            var mimeType = ResolveMimeType(fullPath, ext);

            if (download)
            {
                return PhysicalFile(fullPath, mimeType, Path.GetFileName(fullPath));
            }

            return PhysicalFile(fullPath, mimeType);
        }

        private static Att GetAtt(string uniId, long id, out string err)
        {
            err = string.Empty;
            if (id <= 0 || string.IsNullOrWhiteSpace(uniId))
            {
                err = "参数错误";
                return null;
            }

            var item = Att.Get(id);
            if (item == null || !string.Equals(item.Key, uniId, StringComparison.OrdinalIgnoreCase))
            {
                err = "附件不存在";
                return null;
            }

            return item;
        }

        private static bool TryResolveStaticFile(string relativeFile, out string safePath, out string fullPath, out string err)
        {
            safePath = (relativeFile ?? string.Empty).Replace('\\', '/').Trim().TrimStart('/');
            fullPath = string.Empty;
            err = string.Empty;

            if (string.IsNullOrWhiteSpace(safePath))
            {
                err = "文件路径为空";
                return false;
            }

            if (safePath.Contains("..", StringComparison.Ordinal))
            {
                err = "非法文件路径";
                return false;
            }

            if (!safePath.StartsWith("Samples/", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(safePath, "Samples", StringComparison.OrdinalIgnoreCase))
            {
                err = "仅允许访问 Samples 目录";
                return false;
            }

            var root = Path.Combine(Directory.GetCurrentDirectory(), "Files");
            fullPath = Path.GetFullPath(Path.Combine(root, safePath.Replace('/', Path.DirectorySeparatorChar)));
            var rootPath = Path.GetFullPath(root);
            if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            {
                err = "非法文件路径";
                return false;
            }

            if (!System.IO.File.Exists(fullPath))
            {
                err = "文件不存在";
                return false;
            }

            return true;
        }

        private void InitFromStaticFile(string relativeFile)
        {
            if (!TryResolveStaticFile(relativeFile, out var safePath, out var fullPath, out var err))
            {
                Error = err;
                return;
            }

            FileName = Path.GetFileName(fullPath);
            FileExt = Path.GetExtension(fullPath).TrimStart('.').ToLower();
            SourceUrl = $"/Shared/FileViewer?handler=StaticContent&file={Uri.EscapeDataString(safePath)}";
            ViewerUrl = BuildViewerUrl(SourceUrl, FileName, FileExt);
            DownloadUrl = $"/Shared/FileViewer?handler=StaticContent&file={Uri.EscapeDataString(safePath)}&download=true";
        }

        private static string BuildViewerUrl(string sourceUrl, string fileName, string fileExt)
        {
            var src = Uri.EscapeDataString(sourceUrl ?? string.Empty);
            var name = Uri.EscapeDataString(fileName ?? string.Empty);
            var ext = (fileExt ?? string.Empty).Trim().ToLower();
            if (string.IsNullOrWhiteSpace(ext))
                ext = Path.GetExtension(fileName ?? string.Empty).TrimStart('.').ToLower();

            var imageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "png", "jpg", "jpeg", "gif", "webp", "bmp", "svg"
            };
            var textExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "txt", "json", "xml", "yaml", "yml", "log", "csv"
            };
            var mindmapExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "mm", "mmd", "xmind"
            };
            var videoExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "mp4", "webm", "ogv", "mov", "m4v", "avi"
            };
            var nameText = (fileName ?? string.Empty).ToLower();
            var likelyPanorama = nameText.Contains("pano") || nameText.Contains("panorama") || nameText.Contains("360");

            if (ext == "pdf")
                return $"/Shared/FileViewPdf?src={src}&name={name}";

            if (ext == "doc" || ext == "docx")
                return $"/Shared/FileViewWord?src={src}&name={name}";

            if (ext == "xls" || ext == "xlsx")
                return $"/Shared/FileViewExcel?src={src}&name={name}";

            if (ext == "glb" || ext == "gltf" || ext == "usdz")
                return $"/Shared/FileViewModel?src={src}&name={name}";

            if (ext == "md" || ext == "markdown")
                return $"/Shared/FileViewMarkdown?src={src}&name={name}";

            if (mindmapExts.Contains(ext))
                return $"/Shared/FileViewMind?src={src}&name={name}";

            if (videoExts.Contains(ext))
                return $"/Shared/FileViewVideo?src={src}&name={name}";

            if (imageExts.Contains(ext) && likelyPanorama)
                return $"/Shared/FileViewPanorama?src={src}&name={name}";

            if (imageExts.Contains(ext))
                return $"/Shared/FileViewImage?src={src}&name={name}";

            if (textExts.Contains(ext))
                return $"/Shared/FileViewText?src={src}&name={name}";

            return $"/Shared/FileViewPdf?src={src}&name={name}";
        }

        private static string ResolveMimeType(string filePath, string ext)
        {
            var provider = new FileExtensionContentTypeProvider();
            if (!string.IsNullOrWhiteSpace(filePath) && provider.TryGetContentType(filePath, out var providerMime) && !string.IsNullOrWhiteSpace(providerMime))
                return providerMime;

            var mime = App.Utils.IO.GetMimeType(ext);
            mime = NormalizeMimeType(mime);

            if (string.IsNullOrWhiteSpace(mime))
                return "application/octet-stream";

            return mime;
        }

        private static string NormalizeMimeType(string mime)
        {
            var value = (mime ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            // 历史数据中可能出现 application/application/* 的重复前缀。
            if (value.StartsWith("application/application/", StringComparison.OrdinalIgnoreCase))
                value = value.Substring("application/".Length);

            return value;
        }
    }
}
