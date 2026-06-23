using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisGeometryEdit)]
    public class SceneFormModel : AdminModel
    {
        public GisScene Item { get; set; }
        public List<SelectListItem> Styles { get; set; }
        //public List<SelectListItem> Projections { get; set; }

        public void OnGet()
        {
            Styles = GisScene.Styles.Select(x => new SelectListItem(x.Name, x.Name)).ToList();
            //Projections = Enum.GetValues<GisMapProjection>()
                //.Select(x => new SelectListItem(x.GetTitle(), ((int)x).ToString()))
                //.ToList();
        }

        public IActionResult OnGetData(long id)
        {
            var item = GisScene.GetDetail(id) ?? new GisScene();
            return BuildResult(0, "success", item.Export());
        }

        public IActionResult OnPostSave([FromBody] GisScene req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            GisScene item;
            if (req.Id > 0)
            {
                item = GisScene.Get(req.Id);
                if (item == null)
                    return BuildResult(403, "无权编辑或数据不存在");
            }
            else
            {
                item = new GisScene();
                item.CreateDt = DateTime.Now;
                item.CreatorId = GetUserId();
            }

            item.Name = req.Name;
            item.SortId = req.SortId;
            item.Desc = req.Desc;
            item.MapZoom = req.MapZoom;
            item.MapCenter = req.MapCenter;
            item.MapPitch = req.MapPitch;
            item.Icon = req.Icon;
            item.MapStyle = req.MapStyle;
            item.Map3D = req.Map3D;
            item.MapProjection = req.MapProjection;

            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
