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
    [Auth(Power.GisGeometryView)]
    public class GeometriesModel : AdminModel
    {
        public GisGeometry Item { get; set; }

        public void OnGet(long? menuId)
        {
            Item = new GisGeometry();
            if (menuId.HasValue)
                Item.MenuId = menuId.Value;
        }

        public IActionResult OnGetData(Paging pi, string name, GeometryType? type, long? menuId)
        {
            var list = GisGeometry.Search(name:name, type:type, menuId:menuId, recursive:false).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.GisGeometryDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
                GisGeometry.Delete(id);
            return BuildResult(0, "删除成功");
        }
    }
}
