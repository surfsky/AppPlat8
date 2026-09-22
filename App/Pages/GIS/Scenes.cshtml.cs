using System;
using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [Auth(Power.GisGeometryView)]
    public class ScenesModel : AdminModel
    {
        public GisScene Item { get; set; }

        public void OnGet()
        {
            Item = new GisScene();
        }

        public IActionResult OnGetData(Paging pi, string name)
        {
            if (!GisScene.Set.Any())
            {
                var scene = GisScene.GetDefaultScene();
                scene.CreateDt = DateTime.Now;
                scene.CreatorId = GetUserId();
                scene.IsDefault = true;
                scene.Save();
            }

            if (pi.SortField.IsEmpty())
            {
                pi.SortField = "IsDefault desc,SortId";
                pi.SortDirection = "desc";
            }
            var list = GisScene.Search(name)
                .SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        /// <summary>把指定场景设为默认（全局唯一）</summary>
        [Auth(Power.GisSceneEdit)]
        public IActionResult OnPostSetDefault([FromBody] long id)
        {
            if (!CheckPower(Power.GisSceneEdit))
                return BuildResult(403, "无权操作");
            var r = GisScene.SetDefault(id);
            return BuildResult(r.Code, r.Message, r.Data);
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
