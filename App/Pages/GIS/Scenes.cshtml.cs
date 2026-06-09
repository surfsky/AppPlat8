using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisGeometryView)]
    public class ScenesModel : AdminModel
    {
        public GisScene Item { get; set; }

        public void OnGet()
        {
            Item = new GisScene();
        }

        public IActionResult OnGetData(Paging pi, string name)
        {
            var list = GisScene.Search(name)
                .SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.GisGeometryDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
            {
                var item = GisScene.Get(id);
                if (item != null)
                    item.Delete();
            }
            return BuildResult(0, "删除成功");
        }
    }
}
