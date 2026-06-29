using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [Auth(Power.GisGeometryView)]
    public class ApisModel : AdminModel
    {
        public GisApi Item { get; set; }

        public void OnGet(long? menuId)
        {
            Item = new GisApi { MenuId = menuId };
        }

        public IActionResult OnGetData(Paging pi, long? menuId, string name)
        {
            var list = GisApi.Search(menuId, name)
                .OrderBy(t => t.SortId)
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
                GisApi.Delete(id);

            return BuildResult(0, "删除成功");
        }

        public IActionResult OnPostRefresh([FromBody] long id)
        {
            if (!CheckPower(Power.GisGeometryEdit))
                return BuildResult(403, "无权操作");

            var item = GisApi.Get(id);
            if (item == null)
                return BuildResult(404, "接口不存在");

            var (okCount, failCount) = GisApi.RefreshStats(item.MenuId);
            var menuFixCnt = GisMenu.FixAll();
            return BuildResult(0, "刷新完成", new
            {
                okCount,
                failCount,
                menuFixCnt,
            });
        }
    }
}
