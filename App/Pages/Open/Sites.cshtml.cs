using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Open
{
    /// <summary>参考网站管理</summary>
    [Auth(Power.SiteView)]
    public class SitesModel : AdminModel
    {
        [BindProperty]
        public Site Item { get; set; }

        /// <summary>页面</summary>
        public void OnGet() { }

        /// <summary>查询</summary>
        public IActionResult OnGetData(Paging pi, string type, string name)
        {
            var list = Site.Search(type, name).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        /// <summary>删除</summary>
        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.SiteDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
                Site.Delete(id);
            return BuildResult(0, "删除成功");
        }
    }
}
