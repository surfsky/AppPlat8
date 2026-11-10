using System;
using System.Collections.Generic;
using System.Linq;
using App.API;
using App.Components;
using App.DAL;
using App.EleUI;
using App.Entities;
using App.Services;
using App.Utils;
using App.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Checks.Reports
{
    /// <summary>专项任务统计报表页。</summary>
    [Auth(Power.CheckTaskView)]
    public class CheckTaskStatModel : AuthModel
    {
        public CheckTaskStatReportRow Item { get; set; } = new();
        public List<SelectListItem> TaskOptions { get; private set; } = new();
        public List<CheckTaskStatReportRow> Rows { get; private set; } = new();

        [BindProperty(SupportsGet = true)] public long? TaskId { get; set; }
        [BindProperty(SupportsGet = true)] public long? OrgId { get; set; }

        public void OnGet(long? taskId, long? orgId)
        {
            NormalizeFilters(taskId, orgId);
        }

        public IActionResult OnGetData(long? taskId, long? orgId)
        {
            NormalizeFilters(taskId, orgId);
            Rows = TaskId.HasValue
                ? CheckTaskStatReportService.GetRows(TaskId.Value, OrgId.GetValueOrDefault(), Orgs.GetAuthVisibleOrgIds(GetUser()))
                : new List<CheckTaskStatReportRow>();

            var pi = new Paging
            {
                PageIndex = 1,
                PageSize = Rows.Count,
                Total = Rows.Count,
                SortField = "SortId",
                SortDirection = "ASC",
            };
            pi.SetTotal(Rows.Count);
            return BuildResult(0, "success", Rows, pi);
        }

        public IActionResult OnPostExport(long? taskId, long? orgId)
        {
            NormalizeFilters(taskId, orgId);
            Rows = TaskId.HasValue
                ? CheckTaskStatReportService.GetRows(TaskId.Value, OrgId.GetValueOrDefault(), Orgs.GetAuthVisibleOrgIds(GetUser()))
                : new List<CheckTaskStatReportRow>();

            var data = Rows.Select(t => t.Export()).ToList();
            var config = new UISetting(typeof(CheckTaskStatReportRow), "专项任务统计报表")
                .BuildExportColumnConfig(freezeCols: 2);
            ExcelExporter.Export(data, config, $"专项任务统计_{TaskId}_{DateTime.Now:yyyyMMddHHmmss}.xls");
            Logger.Info($"导出专项任务统计报表，任务 {TaskId}，组织 {OrgId}，共 {Rows.Count} 行");
            return new EmptyResult();
        }

        void NormalizeFilters(long? taskId = null, long? orgId = null)
        {
            var user = GetUser();
            var visibleOrgIds = Orgs.GetAuthVisibleOrgIds(user);
            TaskId = taskId ?? TaskId;
            OrgId = orgId ?? OrgId;
            if (!OrgId.HasValue || !visibleOrgIds.Contains(OrgId.Value))
                OrgId = ResolveDefaultOrgId(user, visibleOrgIds);

            TaskOptions = BuildTaskOptions(visibleOrgIds);
            if (TaskId.HasValue && !TaskOptions.Any(t => t.Value == TaskId.Value.ToString()))
                TaskId = null;
        }

        static List<SelectListItem> BuildTaskOptions(HashSet<long> visibleOrgIds)
        {
            var ids = visibleOrgIds.ToList();
            if (ids.Count == 0)
                return new List<SelectListItem>();

            return CheckTask.Set
                .AsNoTracking()
                .Where(t => t.Type == CheckTaskType.Special)
                .Where(t => (t.OrgId.HasValue && ids.Contains(t.OrgId.Value))
                    || t.CheckObjects.Any(o => (o.OrgId.HasValue && ids.Contains(o.OrgId.Value))
                        || (o.CheckObject != null && o.CheckObject.OrgId.HasValue && ids.Contains(o.CheckObject.OrgId.Value))))
                .OrderByDescending(t => t.StartDt)
                .ThenByDescending(t => t.Id)
                .Select(t => new SelectListItem { Value = t.Id.ToString(), Text = t.Name })
                .ToList();
        }

        static long? ResolveDefaultOrgId(User user, HashSet<long> visibleOrgIds)
        {
            if (user?.AuthOrgId.HasValue == true && visibleOrgIds.Contains(user.AuthOrgId.Value))
                return user.AuthOrgId.Value;
            if (user?.OrgId.HasValue == true && visibleOrgIds.Contains(user.OrgId.Value))
                return user.OrgId.Value;
            return App.DAL.Org.All
                .Where(t => visibleOrgIds.Contains(t.Id))
                .OrderBy(t => t.SortId)
                .ThenBy(t => t.Id)
                .Select(t => (long?)t.Id)
                .FirstOrDefault();
        }
    }
}
