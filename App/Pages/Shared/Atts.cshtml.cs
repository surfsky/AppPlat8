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
using System.IO;

namespace App.Pages.Shared
{
    [CheckPower(Power.CheckObjectView)]
    public class AttsModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public string UniId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Name { get; set; }

        public Att Item { get; set; }

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
                return BuildResult(400, "参数错误");
            if (string.IsNullOrWhiteSpace(uniId))
                return BuildResult(400, "参数错误：缺少uniId");
            if (!CheckPower(Power.CheckObjectEdit))
                return BuildResult(403, "无权操作");

            var allowIds = Att.Set.Where(t => ids.Contains(t.Id) && t.Key == uniId).Select(t => t.Id).ToList();
            if (allowIds.Count == 0)
                return BuildResult(404, "未找到可删除附件");

            Att.DeleteBatch(allowIds);
            return BuildResult(0, "删除成功");
        }

        public IActionResult OnPostUpload(string uniId)
        {
            try
            {
                uniId = uniId?.Trim();
                if (string.IsNullOrWhiteSpace(uniId))
                    return BuildResult(400, "参数错误：缺少uniId");
                if (!CheckPower(Power.CheckObjectEdit))
                    return BuildResult(403, "无权操作");

                var files = Request?.Form?.Files;
                if (files == null || files.Count == 0)
                    return BuildResult(400, "请先选择要上传的文件");

                var nextSortId = (Att.Set.Where(t => t.Key == uniId).Select(t => (int?)t.SortId).Max() ?? 0) + 1;
                var result = new List<object>();

                foreach (IFormFile file in files)
                {
                    if (file == null || file.Length <= 0)
                        continue;

                    var url = Uploader.SaveFile(file, nameof(Att), file.FileName);
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
                    return BuildResult(400, "未上传任何有效文件");

                return BuildResult(0, $"上传成功，共{result.Count}个文件", result);
            }
            catch (Exception ex)
            {
                var message = string.IsNullOrWhiteSpace(ex?.Message) ? "上传失败" : ex.Message;
                return BuildResult(400, message);
            }
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

            var ext = Path.GetExtension(item.FileName ?? item.Content);
            var mimeType = IO.GetMimeType(ext);
            if (string.IsNullOrWhiteSpace(mimeType))
                mimeType = "application/octet-stream";

            var downloadName = string.IsNullOrWhiteSpace(item.FileName)
                ? Path.GetFileName(path)
                : item.FileName;

            return PhysicalFile(path, mimeType, downloadName);
        }
    }
}
