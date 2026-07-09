using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [Auth(Power.GisSceneEdit)]
    public class SceneLayersModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public long SceneId { get; set; }

        /// <summary>获取场景图层数据</summary>
        public IActionResult OnGetData()
        {
            if (SceneId <= 0)
                return BuildResult(400, "参数错误");

            var scene = GisScene.Get(SceneId);
            if (scene == null)
                return BuildResult(404, "场景不存在");

            var checkedNames = GisSceneLayer.Set
                .Where(t => t.SceneId == SceneId)
                .Select(t => t.LayerName)
                .ToList();

            var layers = GisScene.Layers
                .Select(t => new
                {
                    t.Name,
                    t.Title,
                    t.LayerType
                })
                .ToList();

            return BuildResult(0, "success", new
            {
                layers,
                checkedNames
            });
        }

        /// <summary>保存场景图层</summary>
        public IActionResult OnPostSave([FromBody] List<string> req)
        {
            if (SceneId <= 0)
                return BuildResult(400, "参数错误");

            var scene = GisScene.Get(SceneId);
            if (scene == null)
                return BuildResult(404, "场景不存在");

            var validNames = GisScene.Layers
                .Select(t => t.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var layerNames = (req ?? new List<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Where(t => validNames.Contains(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var oldItems = GisSceneLayer.Set.Where(t => t.SceneId == SceneId).ToList();
            foreach (var item in oldItems)
                item.Delete();

            foreach (var name in layerNames)
            {
                new GisSceneLayer
                {
                    SceneId = SceneId,
                    LayerName = name,
                    CreateDt = DateTime.Now
                }.Save();
            }

            return BuildResult(0, "保存成功");
        }
    }
}
