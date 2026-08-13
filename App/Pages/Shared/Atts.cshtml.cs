using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using System.IO;

namespace App.Pages.Shared
{
    [Auth(Power.CheckObjectView)]
    public class AttsModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public string UniId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Name { get; set; }

        public Att Item { get; set; }

        // 封装：强制 HTTP 200，让 axios 走 .then 分支以便拿到真实 msg
        private JsonResult OkBuildResult(int code, string msg, object data = null)
        {
            var json = BuildResult(code, msg, data);
            json.StatusCode = 200;
            return json;
        }

        public void OnGet(string uniId, string name)
        {
            UniId = uniId?.Trim();
            Name = name;
        }

        public IActionResult OnGetData(Paging pi, string uniId, string fileName, AttType? type)
        {
            uniId = uniId?.Trim();
            if (string.IsNullOrWhiteSpace(uniId))
                return BuildResult(400, "参数错误：缺少uniId");

            var q = Att.Set.Where(t => t.Key == uniId);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var keyword = fileName.Trim();
                q = q.Where(t => (t.FileName != null && t.FileName.Contains(keyword))
                              || (t.Content != null && t.Content.Contains(keyword))
                              || (t.Remark != null && t.Remark.Contains(keyword)));
            }
            if (type != null)
                q = q.Where(t => t.Type == type);

            var list = q.OrderBy(t => t.SortId).ThenByDescending(t => t.Id).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids, string uniId)
        {
            uniId = uniId?.Trim();
            if (ids == null || ids.Length == 0)
                return OkBuildResult(400, "请先勾选要删除的附件");
            if (string.IsNullOrWhiteSpace(uniId))
                return OkBuildResult(400, "参数错误：缺少uniId");
            if (!CheckPower(Power.CheckObjectEdit))
                return OkBuildResult(403, "无权删除附件");

            var allowIds = Att.Set.Where(t => ids.Contains(t.Id) && t.Key == uniId).Select(t => t.Id).ToList();
            if (allowIds.Count == 0)
                return OkBuildResult(404, "未找到可删除的附件");

            Att.DeleteBatch(allowIds);
            return OkBuildResult(0, $"删除成功，共{allowIds.Count}个附件");
        }

        public IActionResult OnPostUpload(string uniId)
        {
            try
            {
                uniId = uniId?.Trim();
                if (string.IsNullOrWhiteSpace(uniId))
                    return OkBuildResult(400, "参数错误：缺少uniId");
                if (!CheckPower(Power.CheckObjectEdit))
                    return OkBuildResult(403, "无权上传附件");

                var files = Request?.Form?.Files;
                if (files == null || files.Count == 0)
                    return OkBuildResult(400, "请先选择要上传的文件");

                var nextSortId = (Att.Set.Where(t => t.Key == uniId).Select(t => (int?)t.SortId).Max() ?? 0) + 1;
                var result = new List<object>();

                foreach (IFormFile file in files)
                {
                    if (file == null || file.Length <= 0)
                        continue;

                    var url = Uploader.SaveFile(file, nameof(Att));
                    var item = new Att
                    {
                        Key = uniId,
                        Content = url,
                        FileName = file.FileName,
                        SortId = nextSortId++,
                        Protect = true,
                        FileSize = file.Length
                    };
                    item.Save();
                    result.Add(item.Export(ExportMode.Detail));
                }

                if (result.Count == 0)
                    return OkBuildResult(400, "未上传任何有效文件");

                return OkBuildResult(0, $"上传成功，共{result.Count}个文件", result);
            }
            catch (Exception ex)
            {
                var message = string.IsNullOrWhiteSpace(ex?.Message) ? "上传失败" : ex.Message;
                return OkBuildResult(400, message);
            }
        }

        public IActionResult OnPostMoveTo([FromBody] MoveToRequest req)
        {
            if (req?.Ids == null || req.Ids.Length == 0)
                return OkBuildResult(400, "请先勾选要移动的附件");
            req.UniId = req.UniId?.Trim();
            if (string.IsNullOrWhiteSpace(req.UniId))
                return OkBuildResult(400, "缺少源目录Key");
            if (req.TargetMenuId <= 0)
                return OkBuildResult(400, "请选择目标目录");
            if (!CheckPower(Power.CheckObjectEdit))
                return OkBuildResult(403, "无权移动附件");

            // 强校验：当前页面必须属于知识库目录
            if (!req.UniId.StartsWith("KbMenu-", StringComparison.OrdinalIgnoreCase))
                return OkBuildResult(400, "当前页面不是知识库目录，不支持移动");

            // 目标目录必须存在
            var target = App.DAL.KbMenu.Get(req.TargetMenuId);
            if (target == null)
                return OkBuildResult(404, "目标目录不存在或已删除");

            var targetKey = $"KbMenu-{req.TargetMenuId}";
            if (string.Equals(req.UniId, targetKey, StringComparison.OrdinalIgnoreCase))
                return OkBuildResult(400, "目标目录与源目录相同");

            // 安全：只更新"选中且Att.Key == 源uniId"的记录，防止越权
            var toMove = Att.Set.Where(t => req.Ids.Contains(t.Id) && t.Key == req.UniId).ToList();
            var affected = 0;
            foreach (var a in toMove)
            {
                a.Key = targetKey;
                a.Save();
                affected++;
            }
            return OkBuildResult(0, $"移动成功，共{affected}个附件", new { moved = affected, targetKey });
        }

        public class MoveToRequest
        {
            public long[] Ids { get; set; }
            public string UniId { get; set; }
            public long TargetMenuId { get; set; }
        }

        public IActionResult OnGetDownload(long id, string uniId)
        {
            uniId = uniId?.Trim();
            if (id <= 0 || string.IsNullOrWhiteSpace(uniId))
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.CheckObjectView))
                return BuildResult(403, "无权访问");

            var item = Att.Get(id);
            if (item == null || item.Key != uniId)
                return BuildResult(404, "附件不存在");

            var path = App.Web.Asp.MapPath(item.Content);
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                return BuildResult(404, "文件不存在或已被删除");

            var physExt = Path.GetExtension(path) ?? string.Empty;
            var mimeType = ResolveMimeType(path, physExt);
            if (string.IsNullOrWhiteSpace(mimeType))
                mimeType = "application/octet-stream";

            // 注意：不再传 fileDownloadName，也不手写 Content-Disposition，
            // 因为前端已通过 a.download = 数据库原名(Att.FileName) 强制指定，
            // 这样彻底绕开 ASP.NET Core Content-Disposition 中文编码 + Chrome 忽略 a.download 的双坑。
            return PhysicalFile(path, mimeType);
        }

        /// <summary>解析下载响应的MimeType</summary>
        private static string ResolveMimeType(string filePath, string ext)
        {
            var provider = new FileExtensionContentTypeProvider();
            if (!string.IsNullOrWhiteSpace(filePath)
                && provider.TryGetContentType(filePath, out var providerMime)
                && !string.IsNullOrWhiteSpace(providerMime))
            {
                return providerMime;
            }

            var mime = App.Utils.IO.GetMimeType(ext);
            mime = NormalizeMimeType(mime);
            return string.IsNullOrWhiteSpace(mime) ? "application/octet-stream" : mime;
        }

        /// <summary>清理异常的MimeType字符串</summary>
        private static string NormalizeMimeType(string mime)
        {
            var value = (mime ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            if (value.StartsWith("application/application/", StringComparison.OrdinalIgnoreCase))
                value = value.Substring("application/".Length);

            return value;
        }
    }
}
