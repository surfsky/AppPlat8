using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.Entities;
using App.Utils;
using App.Services;
using App.Web;
using App.DAL;
using Microsoft.AspNetCore.Mvc;
using App.API;

namespace App.Pages.Checks.Reports
{
    public class CheckObjectStatModel : AuthModel
    {
        public CheckObjectStatRow Item {get; set;}

        [BindProperty(SupportsGet = true)] public long? OrgId { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime StartDt { get; set; } = DateTime.Today.AddMonths(-1);
        [BindProperty(SupportsGet = true)] public DateTime EndDt   { get; set; } = DateTime.Today;
        public List<CheckObjectStatRow> Rows { get; private set; } = new List<CheckObjectStatRow>();


        public void OnGet()
        {
            SetFilters();
        }

        //---------------------------------------------------------------
        // 参数处理
        //---------------------------------------------------------------

        void SetFilters(long? orgId = null, DateTime? startDt = null, DateTime? endDt = null)
        {
            // 处理组织ID
            var user = GetUser();
            var visibleOrgIds = Orgs.GetAuthVisibleOrgIds(user);
            OrgId = orgId ?? OrgId;
            if (!OrgId.HasValue || !visibleOrgIds.Contains(OrgId.Value))
                OrgId = ResolveDefaultOrgId(user, visibleOrgIds);

            // 处理时间范围
            if (StartDt == DateTime.MinValue) StartDt = DateTime.Today.AddMonths(-1);
            if (EndDt   == DateTime.MinValue) EndDt   = DateTime.Today;
            if (StartDt > EndDt)              (StartDt, EndDt) = (EndDt, StartDt);
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
        // 数据查询与导出
        //---------------------------------------------------------------
        public IActionResult OnGetData(long? orgId, DateTime? startDt, DateTime? endDt)
        {
            SetFilters(orgId, startDt, endDt);
            Rows = OrgId.HasValue
                ? CheckObjectStatService.GetStat(OrgId.Value, StartDt, EndDt)
                : new List<CheckObjectStatRow>();
            var pi = new Paging { PageIndex = 1, PageSize = Rows.Count, Total = Rows.Count, SortField = "Index", SortDirection = "ASC" };
            pi.SetTotal(Rows.Count);
            return BuildResult(0, "success", Rows, pi);
        }

        public IActionResult OnPostExport(long? orgId, DateTime? startDt, DateTime? endDt)
        {
            SetFilters(orgId, startDt, endDt);
            Rows = OrgId.HasValue
                ? CheckObjectStatService.GetStat(OrgId.Value, StartDt, EndDt)
                : new List<CheckObjectStatRow>();
            var pi = new Paging { PageIndex = 1, PageSize = int.MaxValue, Total = Rows.Count };
            pi.SetTotal(Rows.Count);
            var list = Rows.AsQueryable().SortPageExport(pi);
            var cfg = new UISetting(typeof(CheckObjectStatRow), "企业分类统计表").BuildExportColumnConfig(freezeCols: 2);
            ExcelExporter.Export(list, cfg, $"企业分类统计_{StartDt:yyyyMMdd}-{EndDt:yyyyMMdd}_{DateTime.Now:HHmmss}.xls");
            Logger.Info($"导出企业分类统计报表，日期 {StartDt:yyyy-MM-dd} ~ {EndDt:yyyy-MM-dd}，共 {Rows.Count} 行");
            return new EmptyResult();
        }
    }
}
