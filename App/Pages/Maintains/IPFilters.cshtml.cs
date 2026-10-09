using System;
using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Maintains
{
    [Auth(Power.Admin)]
    public class IPFiltersModel : AuthModel
    {
        public IPFilter Item { get; set; }

        public void OnGet(){}

        public IActionResult OnGetData(Paging pi, string ip = null)
        {
            var list = IPFilter.Search(ip: ip).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.Admin))
                return BuildResult(403, "无权操作");

            var items = IPFilter.Set.Where(t => ids.Contains(t.Id)).ToList();
            foreach (var item in items)
            {
                item.Delete();
            }
            return BuildResult(0, "删除成功");
        }
    }
}
