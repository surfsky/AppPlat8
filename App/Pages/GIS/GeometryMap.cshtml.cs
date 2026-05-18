using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisGeometryView)]
    public class GeometryMapModel : AdminModel
    {
        public void OnGet() { }

        public JsonResult OnGetGeometryLayerData(long? menuId)
        {
            var query = GisGeometry.DataSet
                .Where(g => !string.IsNullOrWhiteSpace(g.GeoJson) || !string.IsNullOrWhiteSpace(g.Gps));

            if (menuId.HasValue)
            {
                var menuIds = GisMenu.All
                    .GetDescendants(menuId)
                    .Select(m => m.Id)
                    .Distinct()
                    .ToList();

                query = query.Where(g => g.MenuId.HasValue && menuIds.Contains(g.MenuId.Value));
            }

            var list = query
                .OrderBy(g => g.SortId)
                .ThenBy(g => g.Id)
                .Select(g => new
                {
                    id = g.Id,
                    type = g.Type,
                    menuId = g.MenuId,
                    name = g.Name,
                    alias = g.Alias,
                    sortId = g.SortId,
                    addr = g.Addr,
                    gps = g.Gps,
                    url = g.Url,
                    att = g.Att,
                    geoJson = g.GeoJson,
                    dataJson = g.DataJson,
                    icon = g.Menu != null ? g.Menu.Icon : null
                })
                .ToList();

            return BuildResult(0, "success", list);
        }

        public IActionResult OnPostDeleteGeometry([FromBody] DeleteReq req)
        {
            if (req == null || req.Id <= 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.GisGeometryDelete))
                return BuildResult(403, "无权操作");

            GisGeometry.Delete(req.Id);
            return BuildResult(0, "删除成功");
        }

        public class DeleteReq
        {
            public long Id { get; set; }
        }
    }
}
