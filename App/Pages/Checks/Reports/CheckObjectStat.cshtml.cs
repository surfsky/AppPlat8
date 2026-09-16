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
    public class CheckObjectStatModel : AdminModel
    {
        public CheckObjectStatRow Item {get; set;}

        [BindProperty(SupportsGet = true)] public DateTime StartDt { get; set; } = DateTime.Today.AddMonths(-1);
        [BindProperty(SupportsGet = true)] public DateTime EndDt   { get; set; } = DateTime.Today;
        public List<CheckObjectStatRow> Rows { get; private set; } = new List<CheckObjectStatRow>();


        public void OnGet()
        {
            EnsureRange();
            Rows = CheckObjectStatService.GetStat(StartDt, EndDt);
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
            Rows = CheckObjectStatService.GetStat(StartDt, EndDt);
            var pi = new Paging { PageIndex = 1, PageSize = Rows.Count, Total = Rows.Count, SortField = "Index", SortDirection = "ASC" };
            pi.SetTotal(Rows.Count);
            return BuildResult(0, "success", Rows, pi);
        }

        public IActionResult OnPostExport()
        {
            EnsureRange();
            Rows = CheckObjectStatService.GetStat(StartDt, EndDt);
            var pi = new Paging { PageIndex = 1, PageSize = int.MaxValue, Total = Rows.Count };
            pi.SetTotal(Rows.Count);
            var list = Rows.AsQueryable().SortPageExport(pi);
            ExcelExporter.Export(list, $"企业分类统计_{StartDt:yyyyMMdd}-{EndDt:yyyyMMdd}_{DateTime.Now:HHmmss}.xlsx");
            Logger.Info($"导出企业分类统计报表，日期 {StartDt:yyyy-MM-dd} ~ {EndDt:yyyy-MM-dd}，共 {Rows.Count} 行");
            return new EmptyResult();
        }
    }
}
