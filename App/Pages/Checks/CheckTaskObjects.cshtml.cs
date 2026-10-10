using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.EleUI;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Checks
{
    /// <summary>
    /// 检查任务 → 检查对象选取页（从 CheckTaskForm 抽屉打开）。
    /// 纯对象选取工作区：顶部过滤 + 中部双列表（查询结果 / 当前选择）+ 底部操作。
    /// </summary>
    //[Auth(Power.CheckTaskEdit)]
    public class CheckTaskObjectsModel : BaseModel  // AuthModel
    {
        public long TaskId { get; set; }
        public string TaskName { get; set; }
        public CheckTask Task { get; set; }

        /// <summary>过滤区的 EleTreePicker/ElePicker/EleSelect 字段宿主，For="Item.Xxx" 会被转为 filters.xxx，对应 setup 里的官方 filters.*（picker/treePicker 直接读写这个对象）。</summary>
        public CheckObject Item { get; set; } = new CheckObject();
        /// <summary>所属网格（多选 ids：独立于 Item.DutyOrgId 单值）。</summary>
        public List<long> DutyOrgIds { get; set; } = new List<long>();
        /// <summary>标签（多选 ids）。</summary>
        public List<long> TagIds { get; set; } = new List<long>();

        public void OnGet(long taskId, string taskName)
        {
            TaskId = taskId;
            TaskName = taskName ?? string.Empty;
            Task = CheckTask.Set.FirstOrDefault(t => t.Id == taskId) ?? new CheckTask();
        }

        //---------------------------------------------------------------
        // 左侧：查询结果候选对象
        //---------------------------------------------------------------
        public IActionResult OnGetSearchResults(
            Paging pi,
            long taskId,
            string name,
            string code,
            string socialCreditCode,
            string address,
            string dutyMan,
            CheckObjectType? objectType,
            CheckScope? scope,
            CheckObjectScale? scale,
            CheckRiskLevel? riskLevel,
            CheckIndustryType? industryType,
            long? dutyOrgId,
            List<long> dutyOrgIds,
            long? checkerId,
            List<long> tagIds,
            bool onlyUnselected = true)
        {
            pi ??= new Paging { PageIndex = 1, PageSize = 20 };
            if (string.IsNullOrEmpty(pi.SortField)) { pi.SortField = "Id"; pi.SortDirection = "DESC"; }

            var cu = Auth.GetUser();
            var isAdmin = Auth.IsAdmin(cu);

            IQueryable<CheckObject> q = CheckObject.Search(
                name: name ?? "",
                code: code ?? "",
                socialCreditCode: socialCreditCode ?? "",
                address: address ?? "",
                dutyMan: dutyMan ?? "",
                dutyOrgId: dutyOrgId,
                dutyOrgIds: dutyOrgIds,
                tagIds: tagIds,
                checkerId: checkerId,
                objectType: objectType,
                scope: scope,
                scale: scale,
                riskLevel: riskLevel,
                industryType: industryType,
                createStartDt: null,
                createEndDt: null,
                updateStartDt: null,
                updateEndDt: null,
                lastCheckStartDt: null,
                lastCheckEndDt: null,
                hasHarzard: null,
                isChecked: null,
                isDel: false,
                isProductInNight: null,
                isThreePlacesThreeEnterprises: null,
                includeTags: true,
                includeContacts: false
            );

            if (!isAdmin)
                q = q.FilterByOrg(cu, nameof(CheckObject.DutyOrgId));

            if (onlyUnselected && taskId > 0)
            {
                var selectedIds = CheckTaskObject.Set
                    .Where(cto => cto.TaskId == taskId && cto.ObjectId != null)
                    .Select(cto => cto.ObjectId.Value)
                    .Distinct()
                    .ToList();
                if (selectedIds.Count > 0)
                    q = q.Where(o => !selectedIds.Contains(o.Id));
            }

            pi.SetTotal(q.Count());
            q = q.SortAndPage(pi);

            var list = q.AsEnumerable().Select(o => new CandidateRow
            {
                Id = o.Id,
                Name = o.Name ?? "",
                Code = o.Code ?? "",
                Address = o.Address ?? "",
                DutyOrgId = o.DutyOrgId,
                DutyOrgName = o.DutyOrgName ?? o.DutyOrg?.FullName ?? o.DutyOrg?.Name ?? "",
                CheckerId = o.CheckerId,
                CheckerName = o.CheckerName ?? BuildCheckerName(o.Checker),
                ObjectTypeName = o.ObjectType.HasValue ? o.ObjectType.Value.ToString() : "",
                ScaleName = o.Scale.HasValue ? o.Scale.Value.ToString() : "",
                RiskLevelName = o.RiskLevel.HasValue ? o.RiskLevel.Value.ToString() : "",
            }).ToList();

            return BuildResult(0, "success", list, pi);
        }

        //---------------------------------------------------------------
        // 右侧：当前已选对象（按任务 CheckTaskObject.SortId 排序）
        //---------------------------------------------------------------
        public IActionResult OnGetSelectedData(long taskId, Paging pi)
        {
            if (taskId <= 0)
                return BuildResult(0, "success", new List<object>(), pi);

            pi ??= new Paging { PageIndex = 1, PageSize = 500 };
            if (string.IsNullOrEmpty(pi.SortField)) { pi.SortField = "SortId"; pi.SortDirection = "ASC"; }

            var cu = Auth.GetUser();
            var authOrgId = cu?.EffectiveAuthOrgId;
            var isAdmin = Auth.IsAdmin(cu);

            IQueryable<CheckTaskObject> q = CheckTaskObject.Set
                .Include(t => t.Object)
                    .ThenInclude(o => o.DutyOrg)
                .Include(t => t.Object)
                    .ThenInclude(o => o.Checker)
                .Where(t => t.TaskId == taskId);

            if (!isAdmin && authOrgId.HasValue && authOrgId.Value > 0)
            {
                var scopeIds = OrgFilter.GetAuthOrgIds(authOrgId.Value);
                q = q.Where(t => t.Object.DutyOrgId == null || scopeIds.Contains(t.Object.DutyOrgId.Value));
            }
            else if (!isAdmin)
            {
                q = q.Where(t => t.Object.DutyOrgId == null);
            }

            pi.SetTotal(q.Count());
            q = q.SortAndPage(pi);

            var list = q.AsEnumerable().Select(t => new SelectedRow
            {
                Id = t.Id,
                TaskId = t.TaskId ?? 0,
                ObjectId = t.ObjectId ?? 0,
                SortId = t.SortId,
                IsFinished = t.IsFinished,
                ObjectName = t.Object?.Name ?? "",
                DutyOrgName = t.Object != null
                    ? (t.Object.DutyOrgName ?? t.Object.DutyOrg?.FullName ?? t.Object.DutyOrg?.Name ?? "")
                    : "",
                CheckerName = t.Object != null
                    ? (t.Object.CheckerName ?? BuildCheckerName(t.Object.Checker))
                    : "",
                StatusName = t.IsFinished ? "已完成" : "未完成",
            }).ToList();

            return BuildResult(0, "success", list, pi);
        }

        //---------------------------------------------------------------
        // 保存：按当前选择 Ids（从上到下顺序）重写 CheckTaskObject，并同步 Task 总数量/进度
        //---------------------------------------------------------------
        public class SaveRequest
        {
            public long TaskId { get; set; }
            public List<long> ObjectIds { get; set; }
        }

        public IActionResult OnPostSaveSelection([FromBody] SaveRequest req)
            => OnPostSaveCore(req);

        public IActionResult OnPostSave([FromBody] SaveRequest req)
            => OnPostSaveCore(req);

        private IActionResult OnPostSaveCore(SaveRequest req)
        {
            if (req == null || req.TaskId <= 0)
                return BuildResult(400, "参数错误");

            var task = CheckTask.Get(req.TaskId);
            if (task == null)
                return BuildResult(404, "检查任务不存在");

            var cu = Auth.GetUser();
            var isAdmin = Auth.IsAdmin(cu);
            var authOrgId = cu?.EffectiveAuthOrgId;
            var scopeIds = isAdmin ? null : OrgFilter.GetAuthOrgIds(authOrgId);

            var ids = (req.ObjectIds ?? new List<long>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (!isAdmin && scopeIds != null)
            {
                var scopeHash = new HashSet<long>(scopeIds);
                var scopeCheck = CheckObject.Set
                    .Where(o => ids.Contains(o.Id))
                    .Select(o => new { o.Id, o.Name, o.DutyOrgId })
                    .ToList();
                foreach (var co in scopeCheck)
                {
                    if (co.DutyOrgId == null) continue;
                    if (!scopeHash.Contains(co.DutyOrgId.Value))
                        return BuildResult(403, $"无权将对象加入任务：{co.Name ?? co.Id.ToString()}");
                }
            }

            var oldList = CheckTaskObject.Set.Where(o => o.TaskId == req.TaskId).ToList();
            // 先保留已经 IsFinished=true 的行（避免重置检查结果），用 ObjectId 匹配
            var finishedIds = new HashSet<long>(
                oldList.Where(o => o.IsFinished && o.ObjectId.HasValue).Select(o => o.ObjectId.Value)
            );

            foreach (var old in oldList)
                CheckTaskObject.Delete(old.Id);

            var now = DateTime.Now;
            var uid = GetUserId();
            int finishCount = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                var cto = new CheckTaskObject
                {
                    TaskId = req.TaskId,
                    ObjectId = ids[i],
                    SortId = (i + 1) * 10,
                    IsFinished = finishedIds.Contains(ids[i]),
                    CreateDt = now,
                    CreatorId = uid,
                };
                if (cto.IsFinished) finishCount++;
                cto.Save();
            }

            task.TotalCount = ids.Count;
            task.FinishCount = finishCount;
            task.Progress = ids.Count == 0 ? 0 : (float)finishCount * 100 / ids.Count;
            task.Save();

            return EleHandler.BuildCommandsResult(
                new ClientCommand(ClientCommandType.Toast, new NotifyArgs(NotifyType.Success, "保存成功", "提示")),
                new ClientCommand(ClientCommandType.RefreshData, new RefreshDataArgs(RefreshScope.Parent)),
                new ClientCommand(ClientCommandType.CloseDrawer, null)
            );
        }

        //---------------------------------------------------------------
        // 按钮：取消
        //---------------------------------------------------------------
        public IActionResult OnPostCancel()
            => EleHandler.BuildCommandsResult(new ClientCommand(ClientCommandType.CloseDrawer, null));

        public IActionResult OnPostResetCandidates()
            => EleHandler.BuildCommandsResult(
                new ClientCommand(ClientCommandType.RefreshData, new RefreshDataArgs(RefreshScope.Self))
            );

        public IActionResult OnPostSearchCandidates()
            => EleHandler.BuildCommandsResult(
                new ClientCommand(ClientCommandType.RefreshData, new RefreshDataArgs(RefreshScope.Self))
            );

        //---------------------------------------------------------------
        // EleButton 标准 Command/Handler：POST envelope
        //   envelope 形如 { pageIndex, pageSize, sortField, sortDirection, filters, payloadKinds, payload }
        //   - Handler = "Search"  → 解析 filters 字段，转发给已有的 OnGetSearchResults 查询实现
        //   - Handler = "Reset"   → 只返回成功，真正的"清空 form 并重新加载"在客户端 resetFilters
        //---------------------------------------------------------------

        /// <summary>EleUI postHandler envelope 模型（pageInfo + filterInfo + payload）。</summary>
        public class PostEnvelope
        {
            public int pageIndex { get; set; } = 1;
            public int pageSize { get; set; } = 20;
            public string sortField { get; set; } = "Id";
            public string sortDirection { get; set; } = "DESC";
            public Dictionary<string, object> filters { get; set; } = new Dictionary<string, object>();
            public string payloadKinds { get; set; }
            public object payload { get; set; }
        }

        /// <summary>标准查询 Handler（对应 EleButton Command=Search, Handler=Search）。</summary>
        public IActionResult OnPostSearch([FromBody] PostEnvelope env)
        {
            env ??= new PostEnvelope();
            env.filters ??= new Dictionary<string, object>();
            var f = env.filters;
            TVal Get<TVal>(string key, TVal def = default)
            {
                if (!f.TryGetValue(key, out var v) || v == null) return def;
                try { return (TVal)Convert.ChangeType(v, typeof(TVal)); }
                catch { return def; }
            }
            List<long> GetLongList(string key)
            {
                if (!f.TryGetValue(key, out var v) || v == null) return new List<long>();
                if (v is List<long> ll1) return ll1;
                if (v is long[] la) return new List<long>(la);
                if (v is IEnumerable<object> en)
                {
                    var lst = new List<long>();
                    foreach (var item in en)
                    {
                        if (item == null) continue;
                        if (long.TryParse(item.ToString(), out var lid)) lst.Add(lid);
                    }
                    return lst;
                }
                var s = v.ToString();
                if (string.IsNullOrWhiteSpace(s)) return new List<long>();
                return s.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => long.TryParse(x.Trim(), out var lid) ? (long?)lid : null)
                    .Where(x => x.HasValue).Select(x => x.Value).ToList();
            }

            return OnGetSearchResults(
                pi: new Paging
                {
                    PageIndex = env.pageIndex > 0 ? env.pageIndex : 1,
                    PageSize = env.pageSize > 0 ? env.pageSize : 20,
                    SortField = string.IsNullOrWhiteSpace(env.sortField) ? "Id" : env.sortField,
                    SortDirection = string.IsNullOrWhiteSpace(env.sortDirection) ? "DESC" : env.sortDirection,
                },
                taskId: Get<long>("taskId", TaskId),
                name: Get<string>("name", null),
                code: Get<string>("code", null),
                socialCreditCode: Get<string>("socialCreditCode", null),
                address: Get<string>("address", null),
                dutyMan: Get<string>("dutyMan", null),
                objectType: f.ContainsKey("objectType") && f["objectType"] != null
                    ? (CheckObjectType?)Enum.ToObject(typeof(CheckObjectType), Get<int>("objectType", 0))
                    : null,
                scope: f.ContainsKey("scope") && f["scope"] != null
                    ? (CheckScope?)Enum.ToObject(typeof(CheckScope), Get<int>("scope", 0))
                    : null,
                scale: f.ContainsKey("scale") && f["scale"] != null
                    ? (CheckObjectScale?)Enum.ToObject(typeof(CheckObjectScale), Get<int>("scale", 0))
                    : null,
                riskLevel: f.ContainsKey("riskLevel") && f["riskLevel"] != null
                    ? (CheckRiskLevel?)Enum.ToObject(typeof(CheckRiskLevel), Get<int>("riskLevel", 0))
                    : null,
                industryType: f.ContainsKey("industryType") && f["industryType"] != null
                    ? (CheckIndustryType?)Enum.ToObject(typeof(CheckIndustryType), Get<int>("industryType", 0))
                    : null,
                dutyOrgId: f.ContainsKey("dutyOrgId") && f["dutyOrgId"] != null ? (long?)Get<long>("dutyOrgId", 0) : null,
                dutyOrgIds: GetLongList("dutyOrgIds"),
                checkerId: f.ContainsKey("checkerId") && f["checkerId"] != null ? (long?)Get<long>("checkerId", 0) : null,
                tagIds: GetLongList("tagIds"),
                onlyUnselected: !f.ContainsKey("onlyUnselected") || Get<bool>("onlyUnselected", true)
            );
        }

        /// <summary>标准重置 Handler（对应 EleButton Command=Reset, Handler=Reset）。</summary>
        /// <remarks>
        /// 真正的「清空 form.value 过滤字段 + 重新加载数据」在客户端 resetFilters 完成；
        /// 这里只返回命令：刷新左表（因为字段清空后需要重新加载）。
        /// </remarks>
        public IActionResult OnPostReset([FromBody] PostEnvelope env)
        {
            return EleHandler.BuildCommandsResult(
                new ClientCommand(ClientCommandType.Toast, new NotifyArgs(NotifyType.Success, "已重置过滤条件", "提示")),
                new ClientCommand(ClientCommandType.RefreshData, new RefreshDataArgs(RefreshScope.Self))
            );
        }

        public IActionResult OnPostRemoveRow()
            => EleHandler.BuildCommandsResult(
                new ClientCommand(ClientCommandType.Toast, new NotifyArgs(NotifyType.Info, "请先选中右侧列表中的行再点删除；或点确定时按最终列表保存。", "提示"))
            );

        public IActionResult OnPostMoveRowUp()
            => EleHandler.BuildCommandsResult(
                new ClientCommand(ClientCommandType.Toast, new NotifyArgs(NotifyType.Info, "请先选中右侧列表中的行再点上移；或点确定时按最终列表顺序保存。", "提示"))
            );

        public IActionResult OnPostMoveRowDown()
            => EleHandler.BuildCommandsResult(
                new ClientCommand(ClientCommandType.Toast, new NotifyArgs(NotifyType.Info, "请先选中右侧列表中的行再点下移；或点确定时按最终列表顺序保存。", "提示"))
            );

        public IActionResult OnPostAddToSelection()
            => EleHandler.BuildCommandsResult(
                new ClientCommand(ClientCommandType.Toast, new NotifyArgs(NotifyType.Info, "请在左侧列表中勾选后点『选择 》』加入右侧；或点确定时按最终列表保存。", "提示"))
            );

        public IActionResult OnPostRemoveFromSelection()
            => EleHandler.BuildCommandsResult(
                new ClientCommand(ClientCommandType.Toast, new NotifyArgs(NotifyType.Info, "请在右侧列表中勾选后点『《 移除』。", "提示"))
            );

        //---------------------------------------------------------------
        // 工具
        //---------------------------------------------------------------
        private static string BuildCheckerName(App.DAL.User u)
        {
            if (u == null) return "";
            var name = string.IsNullOrEmpty(u.RealName) ? u.Name : u.RealName;
            if (!string.IsNullOrEmpty(u.Mobile)) name = $"{name}({u.Mobile})";
            return name ?? "";
        }

        public class CandidateRow
        {
            public long Id { get; set; }
            public string Name { get; set; }
            public string Code { get; set; }
            public string Address { get; set; }
            public long? DutyOrgId { get; set; }
            public string DutyOrgName { get; set; }
            public long? CheckerId { get; set; }
            public string CheckerName { get; set; }
            public string ObjectTypeName { get; set; }
            public string ScaleName { get; set; }
            public string RiskLevelName { get; set; }
        }

        public class SelectedRow
        {
            public long Id { get; set; }
            public long TaskId { get; set; }
            public long ObjectId { get; set; }
            public int SortId { get; set; }
            public bool IsFinished { get; set; }
            public string ObjectName { get; set; }
            public string DutyOrgName { get; set; }
            public string CheckerName { get; set; }
            public string StatusName { get; set; }
        }
    }
}
