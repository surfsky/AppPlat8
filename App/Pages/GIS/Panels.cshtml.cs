using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [Auth(Power.GisGeometryView)]
    public class PanelsModel : AdminModel
    {
        public GisPanel Item { get; set; }

        public void OnGet()
        {
            Item = new GisPanel();
        }

        public IActionResult OnGetData(Paging pi, string title, bool? inGis, bool? inDashboard)
        {
            var list = GisPanel.Search(title, inGis, inDashboard)
                .OrderBy(t => t.Position)
                .ThenBy(t => t.Id)
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
                var item = GisPanel.Get(id);
                if (item != null)
                    item.Delete();
            }
            return BuildResult(0, "删除成功");
        }
    }
}
