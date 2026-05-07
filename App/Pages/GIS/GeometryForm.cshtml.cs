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
    [CheckPower(Power.GisGeometryEdit)]
    public class GeometryFormModel : AdminModel
    {
        public GisGeometry Item { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnGetData(long id, long? menuId, string gps, string geoJson)
        {
            var item = GisGeometry.GetDetail(id) ?? new GisGeometry();
            if (id == 0)
            {
                item.MenuId = menuId;
                if (!string.IsNullOrWhiteSpace(gps))
                    item.GPS = gps;
                if (!string.IsNullOrWhiteSpace(geoJson))
                    item.GeoJson = geoJson;
            }
            return BuildResult(0, "success", item);
        }

        public IActionResult OnPostSave([FromBody] GisGeometry req, long? menuId)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            if (req.Id == 0 && !req.MenuId.HasValue && menuId.HasValue)
                req.MenuId = menuId;

            GisGeometry item;
            if (req.Id > 0)
            {
                item = GisGeometry.Get(req.Id);
                if (item == null)
                    return BuildResult(403, "无权编辑或数据不存在");
            }
            else
            {
                item = new GisGeometry();
                item.CreateDt = DateTime.Now;
                item.CreatorId = GetUserId();
            }

            item.Name = req.Name;
            item.Alias = req.Alias;
            item.SortId = req.SortId;
            item.MenuId = req.MenuId;
            item.Addr = req.Addr;
            item.GPS = req.GPS;
            item.GeoJson = req.GeoJson;
            item.DataJson = req.DataJson;

            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
