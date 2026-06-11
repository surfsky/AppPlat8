using System;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisGeometryEdit)]
    public class SceneFormModel : AdminModel
    {
        public GisScene Item { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnGetData(long id)
        {
            var item = GisScene.GetDetail(id) ?? new GisScene
            {
                SortId = 0,
            };
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

            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
