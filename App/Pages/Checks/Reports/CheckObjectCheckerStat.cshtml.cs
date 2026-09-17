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
            var cfg = new UISetting(typeof(CheckObjectCheckerStatRow), "网格员对象统计表").BuildExportColumnConfig(freezeCols: 3);

            // 顺序修正：把「网格员」列（子类属性，反射时被排到所有父类列之后）调整到「序号/组织科室」之后的第 3 位根节点，
            // 与页面 EleColumn 顺序、Export() 匿名类顺序保持一致，避免表头错位。
            if (cfg.Columns != null)
            {
                var checkerCol = cfg.Columns.FirstOrDefault(c =>
                    (c.PropertyName == "CheckerName") || (!c.IsLeaf && c.Children != null && c.Children.Any(cc => cc.PropertyName == "CheckerName")));
                if (checkerCol == null)
                    checkerCol = cfg.Columns.FirstOrDefault(c => c.Label == "网格员");
                if (checkerCol != null)
                {
                    cfg.Columns.Remove(checkerCol);
                    int insertIdx = 0;
                    // 1. 先跳过「序号」(Index)
                    if (cfg.Columns.Count > insertIdx && (cfg.Columns[insertIdx].PropertyName == "Index" || cfg.Columns[insertIdx].Label == "序号"))
                        insertIdx++;
                    // 2. 再跳过「组织科室」(SectionName)
                    if (cfg.Columns.Count > insertIdx && (cfg.Columns[insertIdx].PropertyName == "SectionName" || cfg.Columns[insertIdx].Label == "组织科室"))
                        insertIdx++;
                    // 3. 把网格员插到第 3 位（index=2）
                    cfg.Columns.Insert(Math.Min(insertIdx, cfg.Columns.Count), checkerCol);
                }
            }

            ExcelExporter.Export(list, cfg, $"网格员对象统计_{StartDt:yyyyMMdd}-{EndDt:yyyyMMdd}_{DateTime.Now:HHmmss}.xls");
            Logger.Info($"导出网格员对象统计报表，日期 {StartDt:yyyy-MM-dd} ~ {EndDt:yyyy-MM-dd}，共 {Rows.Count} 行");
            return new EmptyResult();
        }
    }
}
