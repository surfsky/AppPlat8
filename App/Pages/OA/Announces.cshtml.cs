using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Admin
{
    [Auth(Power.AnnounceView)]
    public class AnnouncesModel : AuthModel
    {
        // 辅助属性，用于 Razor 页面 TagHelper 绑定列元数据
        public Announce Item { get; set; }
        public void OnGet() {}


        /// <summary>查询</summary>
        public IActionResult OnGetData(Paging pi, string title, AnnounceStatus? status, List<DateTime> createDt)
        {
            DateTime? startDt = createDt.GetVal(0);
            DateTime? endDt = createDt.GetVal(1);
            var cu = Auth.GetUser();
            var q = Announce.Search(title, status, fromDt:startDt, toDt:endDt);
            if (!Auth.IsAdmin(cu))
                q = q.FilterByOrg(cu);
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
            var cu = Auth.GetUser();
            var isAdmin = Auth.IsAdmin(cu);
            var scopeId = cu?.EffectiveAuthOrgId;
            foreach (var id in ids)
            {
                var target = Announce.Get(id);
                if (target == null) continue;
                if (!isAdmin && !OrgFilter.IsAuth(scopeId, target.OrgId))
                    return BuildResult(403, $"无权删除记录（Id={id}）");
                Announce.Delete(id);
            }
            return BuildResult(0, "删除成功");
        }
    }
}
