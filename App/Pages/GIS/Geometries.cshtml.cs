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
    public class GeometriesModel : AuthModel
    {
        public GisGeometry Item { get; set; }

        public void OnGet(long? menuId)
        {
            Item = new GisGeometry();
            if (menuId.HasValue)
                Item.MenuId = menuId.Value;
        }

        public IActionResult OnGetData(Paging pi, string name, GeometryType? type, bool? isVisible, long? menuId, string menuIds = null)
        {
            var ids = ParseIds(menuIds);
            if (ids.Count == 0)
            {
                // 查询指定单个菜单下的几何数据
                var list0 = GisGeometry.Search(name:name, type:type, isVisible:isVisible, menuId:menuId, recursive:true).SortPageExport(pi);
                return BuildResult(0, "success", list0, pi);
            }
            else
            {
                // 查询指定几个菜单及其子菜单下的几何数据
                var allMenuIds = ResolveMenuWithDescendants(ids);
                var q = GisGeometry.Search(name:name, type:type, isVisible:isVisible)
                    .Where(g => g.MenuId.HasValue && allMenuIds.Contains(g.MenuId.Value));

                var list = q.SortPageExport(pi);
                return BuildResult(0, "success", list, pi);
            }
        }


        static HashSet<long> ResolveMenuWithDescendants(HashSet<long> menuIds)
        {
            var result = new HashSet<long>();
            if (menuIds == null || menuIds.Count == 0)
                return result;

            foreach (var menuId in menuIds)
            {
                var descendants = GisMenu.All.GetDescendants(menuId).Select(m => m.Id);
                foreach (var id in descendants)
                    result.Add(id);
            }

            return result;
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!HasPower(Power.GisGeometryDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
                GisGeometry.Delete(id);
            return BuildResult(0, "删除成功");
        }
    }
}
