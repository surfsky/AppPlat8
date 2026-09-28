using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using App.HttpApi;
using App.Utils;
using App.EleUI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using System.IO;

namespace App.Pages.Shared
{
    [Auth(AuthLogin =true)]
    [RequestSizeLimit(2147483648)]        // 2GB
    [RequestFormLimits(MultipartBodyLengthLimit = 2147483648, ValueCountLimit = 4096)]
    public class AttsModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public string UniId { get; set; }  // 关联对象ID，格式为：对象类型-对象ID

        [BindProperty(SupportsGet = true)]
        public string Name { get; set; }

        public Att Item { get; set; }

        // 封装：强制 HTTP 200，让 axios 走 .then 分支以便拿到真实 msg
        private JsonResult OkResult(int code, string msg, object data = null)
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

        /// <summary>获取附件列表</summary>
        /// <param name="pi">分页参数</param>
        /// <param name="uniId">关联对象ID</param>
        /// <param name="fileName">文件名</param>
        /// <param name="type">附件类型</param>
        /// <returns>附件列表</returns>
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

        /// <summary>删除附件</summary>
        /// <param name="ids">附件ID列表</param>
        /// <param name="uniId">关联对象ID</param>
        /// <returns>删除结果</returns>
        public IActionResult OnPostDelete([FromBody] long[] ids, string uniId)
        {
            uniId = uniId?.Trim();
            if (ids == null || ids.Length == 0)
                return OkResult(400, "请先勾选要删除的附件");
            if (string.IsNullOrWhiteSpace(uniId))
                return OkResult(400, "参数错误：缺少uniId");
            if (!CheckPower(Power.CheckObjectEdit))
                return OkResult(403, "无权删除附件");

            var allowIds = Att.Set.Where(t => ids.Contains(t.Id) && t.Key == uniId).Select(t => t.Id).ToList();
            if (allowIds.Count == 0)
                return OkResult(404, "未找到可删除的附件");

            Att.DeleteBatch(allowIds);
            return OkResult(0, $"删除成功，共{allowIds.Count}个附件");
        }

        //----------------------------------------------------------------
        // 下载附件
        //----------------------------------------------------------------
        /// <summary>下载附件</summary>
        /// <param name="id">附件ID</param>
        /// <param name="uniId">关联对象ID</param>
        /// <returns>下载结果</returns>
        public IActionResult OnGetDownload(long id, string uniId)
        {
            uniId = uniId?.Trim();
            if (id <= 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.CheckObjectView))
                return BuildResult(403, "无权访问");

            var item = Att.Get(id);
            if (item == null)
                return BuildResult(404, "附件不存在");
            if (!string.IsNullOrWhiteSpace(uniId) &&
                !string.Equals(item.Key, uniId, StringComparison.OrdinalIgnoreCase))
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


        //----------------------------------------------------------------
        // 上传附件
        //----------------------------------------------------------------
        /// <summary>上传附件</summary>
        /// <param name="uniId">关联对象ID</param>
        /// <returns>上传结果</returns>
        public IActionResult OnPostUpload(string uniId)
        {
            try
            {
                uniId = uniId?.Trim();
                if (string.IsNullOrWhiteSpace(uniId))
                    return OkResult(400, "参数错误：缺少uniId");
                if (!CheckPower(Power.CheckObjectEdit))
                    return OkResult(403, "无权上传附件");

                var files = Request?.Form?.Files;
                if (files == null || files.Count == 0)
                    return OkResult(400, "请先选择要上传的文件");

                var folder = uniId.Split('-')[0];  // uniId 格式为 folder-id, 取前面的 folder 作为目录
                var nextSortId = (Att.Set.Where(t => t.Key == uniId).Select(t => (int?)t.SortId).Max() ?? 0) + 1;
                var result = new List<object>();
                foreach (IFormFile file in files)
                {
                    if (file == null || file.Length <= 0)
                        continue;

                    var url = Uploader.SaveFile(folder, file);
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
                    return OkResult(400, "未上传任何有效文件");

                return OkResult(0, $"上传成功，共{result.Count}个文件", result);
            }
            catch (Exception ex)
            {
                var message = string.IsNullOrWhiteSpace(ex?.Message) ? "上传失败" : ex.Message;
                return OkResult(400, message);
            }
        }

        //----------------------------------------------------------------
        // 移动附件到指定目录
        //----------------------------------------------------------------
        /// <summary>移动附件</summary>
        /// <param name="req">移动请求参数</param>
        /// <returns>移动结果</returns>
        public IActionResult OnPostMoveTo([FromBody] MoveToRequest req)
        {
            if (req?.Ids == null || req.Ids.Length == 0)
                return OkResult(400, "请先勾选要移动的附件");
            req.UniId = req.UniId?.Trim();
            if (string.IsNullOrWhiteSpace(req.UniId))
                return OkResult(400, "缺少源目录Key");
            if (req.TargetMenuId <= 0)
                return OkResult(400, "请选择目标目录");
            if (!CheckPower(Power.CheckObjectEdit))
                return OkResult(403, "无权移动附件");

            // 强校验：当前页面必须属于知识库目录
            if (!req.UniId.StartsWith("KbMenu-", StringComparison.OrdinalIgnoreCase))
                return OkResult(400, "当前页面不是知识库目录，不支持移动");

            // 目标目录必须存在
            var target = App.DAL.KbMenu.Get(req.TargetMenuId);
            if (target == null)
                return OkResult(404, "目标目录不存在或已删除");

            var targetKey = $"KbMenu-{req.TargetMenuId}";
            if (string.Equals(req.UniId, targetKey, StringComparison.OrdinalIgnoreCase))
                return OkResult(400, "目标目录与源目录相同");

            // 安全：只更新"选中且Att.Key == 源uniId"的记录，防止越权
            var toMove = Att.Set.Where(t => req.Ids.Contains(t.Id) && t.Key == req.UniId).ToList();
            var affected = 0;
            foreach (var a in toMove)
            {
                a.Key = targetKey;
                a.Save();
                affected++;
            }
            return OkResult(0, $"移动成功，共{affected}个附件", new { moved = affected, targetKey });
        }

        //----------------------------------------------------------------
        // 附件排序
        //----------------------------------------------------------------
        /// <summary>上移一个附件（基于 ElePayloadEnvelope）</summary>
        public IActionResult OnPostMoveUp([FromBody] ElePayload env) => MoveAtt(env, "up");

        /// <summary>下移一个附件（基于 ElePayloadEnvelope）</summary>
        public IActionResult OnPostMoveDown([FromBody] ElePayload env) => MoveAtt(env, "down");

        /// <summary>附件排序：上移/下移（核心实现）</summary>
        private IActionResult MoveAtt(ElePayload env, string direction)
        {
            if (env == null)
                return OkResult(400, "未收到 ElePayload，请检查按钮 Payload");
            direction = (direction ?? string.Empty).Trim().ToLowerInvariant();
            if (direction != "up" && direction != "down")
                return OkResult(400, "移动方向错误");

            // 结构化优先，扁平兜底：保证无论 EleTable 走新 Payload kinks 还是旧默认扁平链路，都能拿到
            string uniId = env.SafeUniId?.Trim();
            if (string.IsNullOrWhiteSpace(uniId))
                uniId = Request.Query["uniId"].ToString().Trim();
            if (string.IsNullOrWhiteSpace(uniId))
                return OkResult(400, "缺少 uniId，Payload 必须包含 ContextInfo 或 URL 中带 uniId");

            long moveId = env.SafeSelectedCount > 0 ? env.SafeFirstSelectedId ?? 0L : 0L;
            if (moveId <= 0)
                return OkResult(400, "请先勾选一个附件再调整顺序");
            if (!CheckPower(Power.CheckObjectEdit))
                return OkResult(403, "无权调整附件顺序");

            // ============ 关键修复：idx 判断基准和 EleTable 展示排序完全对齐 ============
            // 读取 EleTable 当前真实请求的 sortField/sortDirection（用户视觉看到的展示顺序）
            // env.SafePage 已经做了"结构化 Page 优先 → 扁平 *_Flat 兜底"，这里直接取；再兜底 Request.Query
            var safePage = env.SafePage;
            string sortField = safePage.SortField?.Trim();
            if (string.IsNullOrWhiteSpace(sortField)) sortField = Request.Query["sortField"].ToString().Trim();
            if (string.IsNullOrWhiteSpace(sortField)) sortField = "SortId";

            string sortDir = safePage.SortDirection?.Trim();
            if (string.IsNullOrWhiteSpace(sortDir)) sortDir = Request.Query["sortDirection"].ToString().Trim();
            bool asc = !(sortDir ?? "ASC").Trim().Equals("DESC", StringComparison.OrdinalIgnoreCase);

            using (var tran = EntityBase.Db.Database.BeginTransaction())
            {
                try
                {
                    // 同一 uniId 下的附件，先按【EleTable 当前真实展示顺序】排序取 siblings（用于找 idx 和交换对手）
                    var rawSiblings = Att.Set.Where(t => t.Key == uniId).ToList();
                    if (rawSiblings.Count <= 1)
                        return OkResult(400, "只有一个附件，无需移动");

                    // =====================================
                    // 【关键修复：排序初始化在 idx 判断之前完成】
                    //   ps.若 sortid 为空，可考虑先用id来设置sortid，再考虑互换。
                    //   原因：如果 SortId 全是 0 或有冲突，OrderSiblingsByField(SortId asc)
                    //   只能靠二级 ThenByDescending(Id) 定序，导致 1813(Id大) 排第 0，
                    //   和 EleTable 客户端"列值相同按出现顺序"显示的基准错位，造成
                    //   "明明勾第2条却提示已经是最上面"的误判。
                    // =====================================
                    var distinctSortIds = new HashSet<int>();
                    var needInit = false;
                    foreach (var s in rawSiblings)
                    {
                        if (s.SortId <= 0 || !distinctSortIds.Add(s.SortId))
                        {
                            needInit = true;
                            break;
                        }
                    }
                    if (needInit)
                    {
                        // 先按当前展示顺序（sortField/asc）给 rawSiblings 排一遍，
                        // 再将第 i 个的 SortId 赋值成：ps 要求的 Id（若 Id 溢出 int 则 i+1 连续号）
                        var orderedForInit = OrderSiblingsByField(rawSiblings, sortField, asc).ToList();
                        for (var i = 0; i < orderedForInit.Count; i++)
                        {
                            int idAsInt = orderedForInit[i].Id > 0 && orderedForInit[i].Id <= int.MaxValue
                                ? (int)orderedForInit[i].Id
                                : (i + 1);
                            orderedForInit[i].SortId = idAsInt;
                            orderedForInit[i].Save();
                        }
                        // 从 Db 再取一次保证 siblings 的 SortId 是刚写入的最新值
                        rawSiblings = Att.Set.Where(t => t.Key == uniId).ToList();
                    }

                    // 现在 SortId 一定已初始化且无冲突：按当前展示顺序取 idx 基准
                    var siblings = OrderSiblingsByField(rawSiblings, sortField, asc).ToList();

                    var idx = siblings.FindIndex(t => t.Id == moveId);
                    if (idx < 0)
                        return OkResult(404, "选中的附件不属于当前目录");
                    if (direction == "up"   && idx == 0)
                        return OkResult(400, "已经是最上面了");
                    if (direction == "down" && idx == siblings.Count - 1)
                        return OkResult(400, "已经是最下面了");

                    int otherIdx = direction == "up" ? idx - 1 : idx + 1;
                    var current = siblings[idx];
                    var other   = siblings[otherIdx];
                    long  curId   = current.Id;
                    long  otherId = other.Id;
                    int   curNewSortId   = other.SortId;
                    int   otherNewSortId = current.SortId;
                    (current.SortId, other.SortId) = (curNewSortId, otherNewSortId);
                    current.Save();
                    other.Save();

                    tran.Commit();
                    var msg = direction == "up" ? "上移成功" : "下移成功";
                    // patch 目标：前端按 id 精确更新 row.sortId（用户要求"只需要更新sortId字段"）
                    var sortChanges = new[]
                    {
                        new { id = curId,   sortId = curNewSortId },
                        new { id = otherId, sortId = otherNewSortId }
                    };
                    return OkResult(0, msg, new
                    {
                        id = moveId,
                        direction,
                        uniId,
                        displayIdx   = idx,
                        swapWithIdx  = otherIdx,
                        sortField    = "SortId",
                        asc          = true,
                        sortChanges,
                        swapIds      = new[] { curId, otherId }
                    });
                }
                catch
                {
                    tran.Rollback();
                    throw;
                }
            }
        }

        /// <summary>附件 siblings 按 EleTable 当前真实展示列做动态排序（保证 idx 和用户视觉一致）</summary>
        private static IEnumerable<Att> OrderSiblingsByField(IEnumerable<Att> src, string sortField, bool asc)
        {
            var field = (sortField ?? string.Empty).Trim();
            IOrderedEnumerable<Att> ordered;
            switch (field)
            {
                case "FileName":
                    ordered = asc ? src.OrderBy(t => t.FileName, StringComparer.OrdinalIgnoreCase) : src.OrderByDescending(t => t.FileName, StringComparer.OrdinalIgnoreCase);
                    break;
                case "FileSize":
                case "fileSize":
                case "FileSizeText":
                case "fileSizeText":
                    ordered = asc ? src.OrderBy(t => t.FileSize ?? 0L) : src.OrderByDescending(t => t.FileSize ?? 0L);
                    break;
                case "CreateDt":
                case "createDt":
                    ordered = asc ? src.OrderBy(t => t.CreateDt) : src.OrderByDescending(t => t.CreateDt);
                    break;
                case "Type":
                case "type":
                    ordered = asc ? src.OrderBy(t => t.Type ?? 0) : src.OrderByDescending(t => t.Type ?? 0);
                    break;
                case "FileExtension":
                case "fileExtension":
                    ordered = asc ? src.OrderBy(t => t.FileExtension, StringComparer.OrdinalIgnoreCase) : src.OrderByDescending(t => t.FileExtension, StringComparer.OrdinalIgnoreCase);
                    break;
                case "Id":
                case "id":
                    ordered = asc ? src.OrderBy(t => t.Id) : src.OrderByDescending(t => t.Id);
                    break;
                case "SortId":
                case "sortId":
                default:
                    ordered = asc ? src.OrderBy(t => t.SortId) : src.OrderByDescending(t => t.SortId);
                    break;
            }
            // 二级兜底：Id desc（符合列表展示顺序默认约定）
            return ordered.ThenByDescending(t => t.Id);
        }



        /// <summary>移动附件请求参数</summary>
        /// <param name="Ids">附件ID列表</param>
        /// <param name="UniId">关联对象ID</param>
        /// <param name="TargetMenuId">目标目录ID</param>
        public class MoveToRequest
        {
            public long[] Ids { get; set; }
            public string UniId { get; set; }
            public long TargetMenuId { get; set; }
        }


    }
}
