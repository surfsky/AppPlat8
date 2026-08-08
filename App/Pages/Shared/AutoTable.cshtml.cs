using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using App.Components;
using App.DAL;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Shared
{
    /// <summary>Excel 智能表格</summary>
    [Auth(Power.CheckObjectEdit)]
    public class AutoTableModel : AdminModel
    {
        private readonly AutoExcelStore _store = new();

        [BindProperty(SupportsGet = true)]
        public new string File { get; set; }

        [BindProperty(SupportsGet = true)]
        public string UniId { get; set; }

        [BindProperty(SupportsGet = true)]
        public long Id { get; set; }

        [BindProperty(SupportsGet = true)]
        public string LabelPosition { get; set; } = "left";

        [BindProperty(SupportsGet = true)]
        public int HeaderRow { get; set; } = 0;

        /**打开页面 */
        public void OnGet(string file, string uniId, long id, int headerRow = 0)
        {
            File = file;
            UniId = uniId?.Trim();
            Id = id;
            HeaderRow = headerRow;

            if (!string.IsNullOrWhiteSpace(File))
                return;

            if (string.IsNullOrWhiteSpace(UniId) || Id <= 0)
                return;

            try
            {
                File = ResolveTargetFile(File, UniId, Id);
            }
            catch
            {
                // Keep page load resilient; Meta/Data handlers will return explicit errors if needed.
            }
        }

        /**读取表格元数据 */
        public IActionResult OnGetMeta(string file, string uniId, long id, int headerRow = 0)
        {
            try
            {
                var targetFile = ResolveTargetFile(file, uniId, id);
                var resolvedHeaderRow = ResolveHeaderRowNumber(targetFile, headerRow);
                var model = _store.ReadSheet(targetFile, resolvedHeaderRow);
                return BuildResult(0, "ok", new
                {
                    file = model.File,
                    fileName = model.FileName,
                    sheetName = model.SheetName,
                    columns = model.Columns,
                    headerRow = resolvedHeaderRow
                });
            }
            catch (Exception ex)
            {
                return BuildResult(400, ex.Message);
            }
        }

        /**读取分页数据 */
        public IActionResult OnGetData(string file, string uniId, long id, string filters, int pageIndex = 0, int pageSize = 20, string sortKey = "", string sortOrder = "", int headerRow = 0)
        {
            try
            {
                var targetFile = ResolveTargetFile(file, uniId, id);
                var resolvedHeaderRow = ResolveHeaderRowNumber(targetFile, headerRow);
                var query = _store.Query(targetFile, ParseFilters(filters), pageIndex, pageSize, sortKey, sortOrder, resolvedHeaderRow);
                return BuildResult(0, "ok", new
                {
                    file = query.File,
                    fileName = query.FileName,
                    sheetName = query.SheetName,
                    columns = query.Columns,
                    rows = query.Rows,
                    headerRow = resolvedHeaderRow
                }, query.Pager);
            }
            catch (Exception ex)
            {
                return BuildResult(400, ex.Message);
            }
        }

        /**下载原始 Excel */
        public IActionResult OnGetDownload(string file, string uniId, long id)
        {
            try
            {
            var targetFile = ResolveTargetFile(file, uniId, id);
            var path = _store.ResolveFilePath(targetFile);
                var name = Path.GetFileName(path);
                var mime = IO.GetMimeType(Path.GetExtension(path));
                if (string.IsNullOrWhiteSpace(mime))
                    mime = "application/octet-stream";
                return PhysicalFile(path, mime, name);
            }
            catch (Exception ex)
            {
                return BuildResult(400, ex.Message);
            }
        }

        /**删除数据行 */
        public IActionResult OnPostDelete([FromBody] AutoTableDeleteReq req)
        {
            try
            {
                var targetFile = ResolveTargetFile(req?.File, req?.UniId, req?.AttId ?? 0);
                _store.DeleteRow(targetFile, req?.Id ?? 0, req?.HeaderRow ?? 1);
                return BuildResult(0, "删除成功");
            }
            catch (Exception ex)
            {
                return BuildResult(400, ex.Message);
            }
        }

        public class AutoTableDeleteReq
        {
            public string File { get; set; }

            public string UniId { get; set; }

            public long? AttId { get; set; }

            public int? HeaderRow { get; set; }

            public int Id { get; set; }
        }

        private static string ResolveTargetFile(string file, string uniId, long attId)
        {
            if (!string.IsNullOrWhiteSpace(file))
                return file;

            var key = (uniId ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(key) && attId > 0)
            {
                var item = Att.Get(attId);
                if (item == null || !string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("附件不存在");

                var path = ExtractRelativeFilePath(item.Content);
                if (string.IsNullOrWhiteSpace(path))
                    path = ResolveRelativePathFromPhysical(item.Content);
                if (string.IsNullOrWhiteSpace(path))
                    throw new InvalidOperationException("无法识别 Excel 文件路径");

                var ext = (item.FileExtension ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(ext))
                    ext = Path.GetExtension(item.FileName ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(ext))
                    ext = Path.GetExtension(path).Trim().TrimStart('.').ToLowerInvariant();

                if (ext != "xls" && ext != "xlsx")
                    throw new InvalidOperationException("当前附件不是 Excel 文件");

                return path;
            }

            throw new InvalidOperationException("缺少文件参数");
        }

        private int ResolveHeaderRowNumber(string file, int headerRow)
        {
            if (headerRow > 0)
                return headerRow;

            return _store.DetectHeaderRowNumber(file, 20);
        }

        private static string ExtractRelativeFilePath(string source)
        {
            var text = (source ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                    text = uri.AbsolutePath ?? string.Empty;
                else
                    return string.Empty;
            }

            if (text.StartsWith("~/", StringComparison.Ordinal))
                text = "/" + text.Substring(2);

            var q = text.IndexOf('?');
            if (q >= 0)
                text = text.Substring(0, q);

            var hash = text.IndexOf('#');
            if (hash >= 0)
                text = text.Substring(0, hash);

            text = text.Replace('\\', '/').Trim();
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            if (text.Contains("..", StringComparison.Ordinal))
                return string.Empty;

            var lower = text.ToLowerInvariant();
            if (lower.StartsWith("/files/"))
                return text.Substring("/files/".Length);

            if (lower.StartsWith("files/"))
                return text.Substring("files/".Length);

            return text.TrimStart('/');
        }

        private static string ResolveRelativePathFromPhysical(string source)
        {
            try
            {
                var physical = App.Web.Asp.MapPath(source);
                if (string.IsNullOrWhiteSpace(physical) || !System.IO.File.Exists(physical))
                    return string.Empty;

                var filesRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Files"));
                var full = Path.GetFullPath(physical);
                if (!full.StartsWith(filesRoot, StringComparison.OrdinalIgnoreCase))
                    return string.Empty;

                var rel = full.Substring(filesRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return rel.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
            }
            catch
            {
                return string.Empty;
            }
        }

        /**解析查询条件 */
        private static Dictionary<string, string> ParseFilters(string filters)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(filters))
                return map;

            using var doc = JsonDocument.Parse(filters);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return map;

            foreach (var item in doc.RootElement.EnumerateObject())
                map[item.Name] = item.Value.ToString();
            return map;
        }
    }
}
