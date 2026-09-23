using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.EleUI;
using App.Entities;
using App.Utils;
using App.Web;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.OA
{
    [Auth(Power.MeetingView)]
    public class MeetingsModel : AdminModel
    {
        public Meeting Item { get; set; } = new Meeting();

        public void OnGet() { }

        public IActionResult OnGetData(Paging pi, List<DateTime> day, long? orgId, MeetingType? type, string keyword = "")
        {
            var list = Meeting.Search(day.GetVal(0), day.GetVal(1), orgId, type, keyword)
                .OrderByDescending(item => item.Day)
                .ThenByDescending(item => item.Id)
                .SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostExport(Paging pi, List<DateTime> day, long? orgId, MeetingType? type, string keyword = "")
        {
            if (!CheckPower(Power.MeetingExport)) return BuildResult(403, "无权导出");
            var exportPaging = new Paging { PageIndex = 1, PageSize = int.MaxValue, SortField = pi.SortField, SortDirection = pi.SortDirection };
            var list = Meeting.Search(day.GetVal(0), day.GetVal(1), orgId, type, keyword)
                .OrderByDescending(item => item.Day)
                .ThenByDescending(item => item.Id)
                .SortPageExport(exportPaging);
            ExcelExporter.Export(list, $"会议记录_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            return new EmptyResult();
        }

        public IActionResult OnPostImport()
        {
            if (!CheckPower(Power.MeetingImport)) return BuildResult(403, "无权导入");
            return EleHandler.ShowDrawer(
                title:"导入会议记录", 
                url: "/OA/MeetingImport", 
                direction: "rtl", 
                closeAction: DrawerCloseAction.RefreshData, 
                size: "680px"
                );
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0) return BuildResult(400, "参数错误");
            if (!CheckPower(Power.MeetingDelete)) return BuildResult(403, "无权删除");
            var count = 0;
            foreach (var id in ids)
            {
                var item = Meeting.Get(id);
                if (item == null) continue;
                item.Delete();
                count++;
            }
            return BuildResult(0, $"已删除 {count} 条会议记录");
        }
    }
}