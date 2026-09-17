using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.Entities;
using App.Utils;
using App.Services;
using App.Web;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Checks.Reports
{
    public class CheckObjectCheckerStatModel : AdminModel
    {
        public CheckObjectCheckerStatRow Item { get; set; }

        [BindProperty(SupportsGet = true)] public DateTime StartDt { get; set; } = DateTime.Today.AddMonths(-1);
        [BindProperty(SupportsGet = true)] public DateTime EndDt   { get; set; } = DateTime.Today;
        public List<CheckObjectCheckerStatRow> Rows { get; private set; } = new List<CheckObjectCheckerStatRow>();

        public void OnGet()
        {
            EnsureRange();
            Rows = CheckObjectStatService.GetCheckerStat(StartDt, EndDt);
        }

        void EnsureRange()
        {
            if (StartDt == DateTime.MinValue) StartDt = DateTime.Today.AddMonths(-1);
            if (EndDt   == DateTime.MinValue) EndDt   = DateTime.Today;
            if (StartDt > EndDt)              (StartDt, EndDt) = (EndDt, StartDt);
        }

        public IActionResult OnGetData()
        {
            EnsureRange();
            Rows = CheckObjectStatService.GetCheckerStat(StartDt, EndDt);
            var pi = new Paging { PageIndex = 1, PageSize = Rows.Count, Total = Rows.Count, SortField = "Index", SortDirection = "ASC" };
            pi.SetTotal(Rows.Count);
            return BuildResult(0, "success", Rows, pi);
        }

        public IActionResult OnPostExport()
        {
            EnsureRange();
            Rows = CheckObjectStatService.GetCheckerStat(StartDt, EndDt);
            var pi = new Paging { PageIndex = 1, PageSize = int.MaxValue, Total = Rows.Count };
            pi.SetTotal(Rows.Count);
            var list = Rows.AsQueryable().SortPageExport(pi);
            ExcelExporter.Export(list, $"网格员对象统计_{StartDt:yyyyMMdd}-{EndDt:yyyyMMdd}_{DateTime.Now:HHmmss}.xlsx");
            Logger.Info($"导出网格员对象统计报表，日期 {StartDt:yyyy-MM-dd} ~ {EndDt:yyyy-MM-dd}，共 {Rows.Count} 行");
            return new EmptyResult();
        }
    }
}
