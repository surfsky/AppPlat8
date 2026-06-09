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
    public class ScenePanelsModel : AdminModel
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

            var allPanels = GisPanel.Set.OrderBy(t => t.Position).ToList();
            var checkedIds = GisScenePanel.Set
                .Where(t => t.SceneId == SceneId)
                .Select(t => t.PanelId)
                .ToList();

            return BuildResult(0, "success", new { 
                panels = allPanels.Select(t => new { t.Id, t.Title }), 
                checkedIds = checkedIds 
            });
        }

        public IActionResult OnPostSave([FromBody] List<long> panelIds)
        {
            if (SceneId <= 0)
                return BuildResult(400, "参数错误");

            // 删除原有关联
            var oldItems = GisScenePanel.Set.Where(t => t.SceneId == SceneId).ToList();
            foreach (var item in oldItems)
            {
                item.Delete();
            }

            // 批量添加新关联
            if (panelIds != null && panelIds.Count > 0)
            {
                foreach (var panelId in panelIds)
                {
                    new GisScenePanel { SceneId = SceneId, PanelId = panelId }.Save();
                }
            }

            return BuildResult(0, "保存成功");
        }
    }
}
