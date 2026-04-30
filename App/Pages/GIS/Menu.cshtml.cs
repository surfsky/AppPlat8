using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisGeometryView)]
    public class MenuModel : AdminModel
    {
        public GisMenu Item { get; set; }

        public void OnGet() { }

        public IActionResult OnGetData()
        {
            var items = GisMenu.GetTree();
            return BuildResult(0, "success", items);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "请选择要删除的记录");
            if (!CheckPower(Power.GisGeometryDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
            {
                var item = GisMenu.Get(id);
                if (item == null)
                    continue;

                if (GisMenu.Set.Any(m => m.ParentId == id))
                    return BuildResult(400, $"菜单[{item.Name}]下还有子菜单，请先删除子菜单");

                if (GisGeometry.Set.Any(g => g.MenuId == id))
                    return BuildResult(400, $"菜单[{item.Name}]下还有图形，请先迁移或删除图形");

                item.Delete();
            }

            GisMenu.ClearCache();
            return BuildResult(0, "删除成功");
        }
    }
}
