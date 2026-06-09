using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisGeometryEdit)]
    public class SceneMenusModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public long SceneId { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnGetData()
        {
            var scene = GisScene.Get(SceneId);
            if (scene == null)
                return BuildResult(404, "场景不存在");

            var allMenus = GisMenu.GetTree();
            var checkedIds = GisSceneMenu.Set
                .Where(t => t.SceneId == SceneId)
                .Select(t => t.MenuId)
                .ToList();

            return BuildResult(0, "success", new { 
                menus = allMenus, 
                checkedIds = checkedIds 
            });
        }

        public IActionResult OnPostSave([FromBody] List<long> menuIds)
        {
            if (SceneId <= 0)
                return BuildResult(400, "参数错误");

            // 删除原有关联
            var oldItems = GisSceneMenu.Set.Where(t => t.SceneId == SceneId).ToList();
            foreach (var item in oldItems)
            {
                item.Delete();
            }

            // 批量添加新关联
            if (menuIds != null && menuIds.Count > 0)
            {
                foreach (var menuId in menuIds)
                {
                    new GisSceneMenu { SceneId = SceneId, MenuId = menuId }.Save();
                }
            }

            return BuildResult(0, "保存成功");
        }
    }
}
