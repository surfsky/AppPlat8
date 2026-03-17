using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.Entities;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Admin
{
    [Auth(Power.AnnounceView)]
    public class AnnouncesModel : AdminModel
    {
        // 辅助属性，用于 Razor 页面 TagHelper 绑定列元数据
        public Announce Item { get; set; }

        public void OnGet() {}


        /// <summary>查询</summary>
        public async Task<IActionResult> OnGetData(Paging pi, string title, AnnounceStatus? status, List<DateTime> createTime)
        {
            var q = createTime.Count > 0 ? Announce.Search(title, status, fromDt:createTime[0], toDt:createTime[1]) : Announce.Search(title, status);
            var list = q.SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }


        /// <summary>删除</summary>
        public IActionResult OnPostDelete([FromBody]long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.AnnounceDelete))
                return BuildResult(403, "无权操作");
            foreach (var id in ids)
                Announce.Delete(id);
            return BuildResult(0, "删除成功");
        }
    }
}
