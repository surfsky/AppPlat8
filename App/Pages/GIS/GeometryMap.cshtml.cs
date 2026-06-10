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

        /// <summary>获取地图图层点位数据</summary>
        public JsonResult OnGetGeometryLayerData(long? menuId)
        {
            var query = GisGeometry.Search(menuId: menuId, isValid: true, recursive: true);
            var list = query
                .OrderBy(g => g.SortId)
                .ThenBy(g => g.Id)
                .Select(SelectItem())
                .ToList();

            return BuildResult(0, "success", list);
        }

        /// <summary>获取单个点位数据</summary>
        public JsonResult OnGetGeometryItem(long id)
        {
            if (id <= 0)
                return BuildResult(400, "参数错误");

            var item = GisGeometry.Search(isValid: true)
                .Where(g => g.Id == id)
                .Select(SelectItem())
                .FirstOrDefault();

            if (item == null)
                return BuildResult(404, "点位不存在");

            return BuildResult(0, "success", item);
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

        public IActionResult OnPostSaveGeometry([FromBody] GisGeometry req)
        {
            if (req == null || req.Id <= 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.GisGeometryEdit))
                return BuildResult(403, "无权操作");

            var item = GisGeometry.Get(req.Id);
            if (item == null)
                return BuildResult(404, "点位不存在");

            if (!string.IsNullOrWhiteSpace(req.Gps)) item.Gps = req.Gps;
            if (!string.IsNullOrWhiteSpace(req.GeoJson)) item.GeoJson = req.GeoJson;
            
            item.Save();
            return BuildResult(0, "保存成功");
        }

        public class DeleteReq
        {
            public long Id { get; set; }
        }

        /// <summary>点位投影</summary>
        private static System.Linq.Expressions.Expression<System.Func<GisGeometry, object>> SelectItem()
        {
            return g => new
            {
                id = g.Id,
                type = g.Type,
                menuId = g.MenuId,
                name = g.Name,
                alias = g.Alias,
                sortId = g.SortId,
                addr = g.Addr,
                gps = g.Gps,
                region = g.Region,
                url = g.Url,
                file = g.File,
                geoJson = g.GeoJson,
                dataJson = g.DataJson,
                icon = g.Menu != null ? g.Menu.Icon : null
            };
        }
    }
}
