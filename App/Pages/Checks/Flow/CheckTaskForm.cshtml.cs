using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.EleUI;
using App.DAL;
using App.Entities;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Checks
{
    [Auth(Power.CheckTaskEdit)]
    public class CheckTaskFormModel : AuthModel
    {
        public CheckTask Item { get; set; }
        public List<App.DAL.Org> OrgTree { get; set; }

        public void OnGet()
        {
            OrgTree = App.DAL.Org.GetTree();
        }

        public IActionResult OnGetData(long id)
        {
            var item = CheckTask.GetDetail(id) ?? new CheckTask();
            return BuildResult(0, "success", item.Export(ExportMode.Normal));
        }

        public IActionResult OnPostSave([FromBody] CheckTask req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = req.Id > 0 ? CheckTask.Get(req.Id) : new CheckTask();
            if (item.Id == 0)
            {
                item.CreatorId = GetUserId();
            }

            item.Name = req.Name;
            item.StartDt = req.StartDt;
            item.ExpireDt = req.ExpireDt;
            item.Remark = req.Remark;
            item.OrgId = req.OrgId;
            item.TotalCount = req.TotalCount;
            item.FinishCount = req.FinishCount;
            item.Progress = req.Progress;
            //item.SetCheckObjectIds(req.CheckObjectIds);
            //item.SetCheckSheetIds(req.CheckSheetIds);
            item.Save();
            return BuildResult(0, "保存成功");
        }

        //------------------------------------------------
        // 检查对象列表（EleList 数据源）& 管理
        //------------------------------------------------
        /// <summary>
        /// 检查任务下的检查对象列表（供 EleList 数据源）。
        /// 列：序号、企业名称、组织、网格员、状态（已完成 / 未完成）
        /// 若传入 checkerId，则只显示该网格员负责的对象。
        /// </summary>
        public IActionResult OnGetCheckObjectsData(long taskId, long? checkerId=null, Paging pi=null)
        {
            pi ??= new Paging { PageIndex = 1, PageSize = 10 };
            if (string.IsNullOrEmpty(pi.SortField)) pi.SortField = "SortId";
            if (string.IsNullOrEmpty(pi.SortDirection)) pi.SortDirection = "ASC";

            var cu = Auth.GetUser();
            var authOrgId = cu?.EffectiveAuthOrgId;
            var isAdmin = Auth.IsAdmin(cu);

            // 用 Search 简化基础过滤（taskId + checkerId），并补充嵌套 Include 供投影使用
            IQueryable<CheckTaskObject> q = CheckTaskObject.Search(taskId: taskId, checkerId: checkerId)
                .Include(t => t.CheckObject).ThenInclude(o => o.DutyOrg)
                .Include(t => t.CheckObject).ThenInclude(o => o.Checker);

            if (!isAdmin && authOrgId.HasValue && authOrgId.Value > 0)
            {
                var scopeIds = OrgFilter.GetAuthOrgIds(authOrgId.Value);
                q = q.Where(t => t.CheckObject.DutyOrgId == null || scopeIds.Contains(t.CheckObject.DutyOrgId.Value));
            }
            else if (!isAdmin)
            {
                q = q.Where(t => t.CheckObject.DutyOrgId == null);
            }

            pi.SetTotal(q.Count());
            q = q.SortAndPage(pi);

            var list = q.AsEnumerable().Select(t => new
            {
                t.Id,
                t.TaskId,
                t.CheckObjectId,
                t.IsFinished,
                SortId = t.SortId,
                ObjectName = t.CheckObject != null ? t.CheckObject.Name : "",
                DutyOrgName = t.CheckObject != null
                    ? (t.CheckObject.DutyOrgName
                       ?? t.CheckObject.DutyOrg?.FullName
                       ?? t.CheckObject.DutyOrg?.Name
                       ?? "")
                    : "",
                CheckerName = t.CheckObject != null
                    ? (t.CheckObject.CheckerName ?? BuildCheckerName(t.CheckObject.Checker))
                    : "",
                StatusName = t.IsFinished ? "已完成" : "未完成",
            }).ToList();

            return BuildResult(0, "success", list, pi);
        }

        //------------------------------------------------
        // 检查对象管理
        //------------------------------------------------
        /// <summary>打开“检查对象筛选管理”抽屉（支持选择/删除/上下移顺序等）。</summary>
        public IActionResult OnPostShowCheckObjects([FromBody] CheckTask req)
        {
            var taskId = req?.Id ?? 0;
            if (taskId <= 0)
                return EleHandler.ShowNotify("请先保存检查任务，再维护检查对象", NotifyType.Warning, "提示");

            var task = CheckTask.Get(taskId);
            var name = Uri.EscapeDataString(task?.Name ?? "检查任务对象选取");
            var url = $"/Checks/Flow/CheckTaskObjects?taskId={taskId}&taskName={name}&md={this.Mode}";
            return EleHandler.ShowDrawer(
                title: "检查对象",
                url: url,
                size: "100%",
                closeAction: DrawerCloseAction.RefreshData
            );
        }

        //------------------------------------------------
        // 附件列表（EleList 数据源）& 管理
        //------------------------------------------------
        /// <summary>附件列表数据源（EleList）</summary>
        public IActionResult OnGetAttsData(long taskId, Paging pi)
        {
            if (taskId <= 0)
                return BuildResult(0, "success", new List<object>(), pi);
            var uniId = CheckTask.Get(taskId)?.UniId;
            if (string.IsNullOrWhiteSpace(uniId))
                return BuildResult(0, "success", new List<object>(), pi);

            var q = Att.Search(key: uniId);
            var list = q.SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        /// <summary>打开附件管理抽屉（通用 /Shared/Atts 页面）</summary>
        public IActionResult OnPostShowAtts([FromBody] CheckTask req)
        {
            var taskId = req?.Id ?? 0;
            if (taskId <= 0)
                return EleHandler.ShowNotify("请先保存检查任务，再维护附件", NotifyType.Warning, "提示");

            var task = CheckTask.Get(taskId);
            var uniId = task?.UniId;
            var name = Uri.EscapeDataString(task?.Name ?? "检查任务附件");
            var url = $"/Shared/Atts?uniId={uniId}&name={name}&md={this.Mode}";
            return EleHandler.ShowDrawer(
                title: "附件",
                url: url,
                size: "100%",
                closeAction: DrawerCloseAction.RefreshData
            );
        }

        //------------------------------------------------
        // 工具
        //------------------------------------------------
        private static string BuildCheckerName(App.DAL.User u)
        {
            if (u == null) return "";
            var name = (u.RealName.IsEmpty() ? u.Name : u.RealName) ?? "";
            if (u.Mobile.IsNotEmpty()) name = $"{name}({u.Mobile})";
            return name;
        }
    }
}

