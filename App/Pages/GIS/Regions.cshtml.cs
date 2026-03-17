using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisRegionView)]
    public class RegionsModel : AdminModel
    {
        public GisRegion Item { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnGetData(Paging pi, string name, RegionType? regionType)
        {
            var list = GisRegion.Search(name, regionType, null, null).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.GisRegionDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
                GisRegion.Delete(id);
            return BuildResult(0, "删除成功");
        }
    }
}
