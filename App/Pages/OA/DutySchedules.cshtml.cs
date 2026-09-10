using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.Entities;
using App.Utils;
using App.EleUI;
using App.Web;
using System.Collections.Generic;

namespace App.Pages.OA
{
    [Auth(Power.DutyScheduleView)]
    public class DutySchedulesModel : AdminModel
    {
        public DutySchedule Item { get; set; } = new DutySchedule();

        public void OnGet() { }

        /// <summary>查询</summary>
        public IActionResult OnGetData(
            Paging pi,
            List<DateTime> day,
            string keyword = "")
        {
            DateTime? startDay = day.GetVal(0);
            DateTime? endDay = day.GetVal(1);
            var q = DutySchedule.Search(fromDt: startDay, toDt: endDay, keyword: keyword);
            var list = q.OrderByDescending(t => t.Day).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        /// <summary>导出</summary>
        public IActionResult OnPostExport(
            Paging pi,
            List<DateTime> day,
            string keyword = "")
        {
            if (!CheckPower(Power.DutyScheduleExport))
                return BuildResult(403, "无权导出");

            DateTime? startDay = day.GetVal(0);
            DateTime? endDay = day.GetVal(1);
            var exportPi = new Paging { PageIndex = 1, PageSize = int.MaxValue, SortField = pi.SortField, SortDirection = pi.SortDirection };
            var q = DutySchedule.Search(fromDt: startDay, toDt: endDay, keyword: keyword);
            var list = q.OrderByDescending(t => t.Day).SortPageExport(exportPi);
            ExcelExporter.Export(list, $"值班表_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            Logger.Info($"导出值班表 {list.Count} 条");
            return new EmptyResult();
        }

        /// <summary>批量删除（物理删除，不再是逻辑删除）</summary>
        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.DutyScheduleDelete))
                return BuildResult(403, "无权操作");
            int n = 0;
            foreach (var id in ids)
            {
                var item = DutySchedule.Get(id);
                if (item != null) { item.Delete(); n++; }
            }
            return BuildResult(0, $"已删除 {n} 条值班记录");
        }
    }
}
