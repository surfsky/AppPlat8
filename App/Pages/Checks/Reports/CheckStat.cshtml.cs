using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.API;
using App.DAL;
using App.EleUI;
using App.Services;
using App.Utils;
using App.Web;
using Microsoft.AspNetCore.Mvc;
//using OrgEntity = App.DAL.Org;

namespace App.Pages.Checks.Reports
{
    /// <summary>网格检查统计报表页。</summary>
    [Auth(Permission.CheckHazardView)]
    public class CheckStatModel : AuthModel
    {
        public CheckStatReportRow Item { get; set; } = new();

        [BindProperty(SupportsGet = true)] public long? OrgId { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime StartDt { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime EndDt { get; set; }
        [BindProperty(SupportsGet = true)] public string QuickRange { get; set; } = "month";

        /// <summary>首屏初始化默认值。</summary>
        public void OnGet()
        {
            SetFilters();
        }


        //---------------------------------------------------------------
        // 参数处理
        //---------------------------------------------------------------
        /// <summary>规范筛选参数。</summary>
        void SetFilters(long? orgId = null, DateTime? startDt = null, DateTime? endDt = null)
        {
            // 处理组织ID
            var user = GetUser();
            var visibleOrgIds = Orgs.GetAuthVisibleOrgIds(user);
            OrgId = orgId ?? OrgId;
            if (!OrgId.HasValue || !visibleOrgIds.Contains(OrgId.Value))
                OrgId = ResolveDefaultOrgId(user, visibleOrgIds);

            // 处理时间范围
            StartDt = startDt ?? StartDt;
            EndDt = endDt ?? EndDt;
            if (StartDt == default || EndDt == default)
                (StartDt, EndDt) = ResolveRange(QuickRange);
            if (StartDt > EndDt)
                (StartDt, EndDt) = (EndDt, StartDt);
        }

        /// <summary>解析默认组织。</summary>
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

        //---------------------------------------------------------------
        // 时间范围快捷方式
        //---------------------------------------------------------------
        /// <summary>根据快捷范围解析起止时间。</summary>
        static (DateTime start, DateTime end) ResolveRange(string quickRange)
        {
            var now = DateTime.Now;
            var key = (quickRange ?? "month").Trim().ToLowerInvariant();
            if (key == "quarter")
            {
                var month = ((now.Month - 1) / 3) * 3 + 1;
                var start = new DateTime(now.Year, month, 1, 0, 0, 0);
                return (start, start.AddMonths(3).AddSeconds(-1));
            }
            if (key == "year")
            {
                var start = new DateTime(now.Year, 1, 1, 0, 0, 0);
                return (start, start.AddYears(1).AddSeconds(-1));
            }

            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0);
            return (monthStart, monthStart.AddMonths(1).AddSeconds(-1));
        }


        /// <summary>时间范围快捷方式联动开始/结束时间。</summary>
        public IActionResult OnPostQuickRangeChanged([FromBody] ControlChangeRequest req)
        {
            var key = req?.Value?.ToString()?.Trim().ToLowerInvariant();
            var (start, end) = ResolveRange(key);
            return EleHandler
                .SetControl<CheckStatModel>(t => t.StartDt, Value: start.ToString("yyyy-MM-ddTHH:mm:ss"))
                .SetControl<CheckStatModel>(t => t.EndDt, Value: end.ToString("yyyy-MM-ddTHH:mm:ss"))
                .ToActionResult();
        }

        //---------------------------------------------------------------
        // 报表数据查询与导出
        //---------------------------------------------------------------
        /// <summary>查询报表数据。</summary>
        public IActionResult OnGetData(long? orgId, DateTime? startDt, DateTime? endDt, string quickRange = null)
        {
            QuickRange = quickRange ?? QuickRange;
            SetFilters(orgId, startDt, endDt);
            var user = GetUser();
            var visibleOrgIds = Orgs.GetAuthVisibleOrgIds(user);
            var rows = OrgId.HasValue
                ? CheckStatReportService.GetRows(OrgId.Value, StartDt, EndDt, visibleOrgIds)
                : new List<CheckStatReportRow>();
            SetDateRange(rows);
            var pi = new Paging
            {
                PageIndex = 1,
                PageSize = rows.Count,
                Total = rows.Count,
            };
            pi.SetTotal(rows.Count);
            return BuildResult(0, "success", rows, pi);
        }

        /// <summary>导出报表数据。</summary>
        public IActionResult OnPostExport(long? orgId, DateTime? startDt, DateTime? endDt, string quickRange = null)
        {
            QuickRange = quickRange ?? QuickRange;
            SetFilters(orgId, startDt, endDt);
            var user = GetUser();
            var visibleOrgIds = Orgs.GetAuthVisibleOrgIds(user);
            var rows = OrgId.HasValue
                ? CheckStatReportService.GetRows(OrgId.Value, StartDt, EndDt, visibleOrgIds)
                : new List<CheckStatReportRow>();
            SetDateRange(rows);

            var list = rows.Select(t => t.Export()).ToList();
            var cfg = new UISetting(typeof(CheckStatReportRow), "检查统计报表").BuildExportColumnConfig(freezeCols: 2);
            ExcelExporter.Export(list, cfg, $"检查统计报表_{StartDt:yyyyMMddHHmmss}-{EndDt:yyyyMMddHHmmss}_{DateTime.Now:HHmmss}.xls");
            Logger.Info($"导出检查统计报表，组织 {OrgId}，日期 {StartDt:yyyy-MM-dd HH:mm:ss} ~ {EndDt:yyyy-MM-dd HH:mm:ss}，共 {rows.Count} 行");
            return new EmptyResult();
        }


        /// <summary>把当前查询的时间范围回填到结果行。</summary>
        void SetDateRange(IEnumerable<CheckStatReportRow> rows)
        {
            var start = StartDt.ToString("yyyy-MM-ddTHH:mm:ss");
            var end = EndDt.ToString("yyyy-MM-ddTHH:mm:ss");
            foreach (var row in rows)
            {
                row.CheckStartDt = start;
                row.CheckEndDt = end;
                row.HazardStart = start;
                row.HazardEnd = end;
            }
        }
    }
}
