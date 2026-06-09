using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.Entities;
using App.Utils;

namespace App.DAL.GIS
{
    /// <summary>GIS 场景</summary>
    [UI("GIS", "GIS场景")]
    public class GisScene : EntityBase<GisScene>
    {
        [UI("名称")] public string Name { get; set; }
        [UI("排序")] public int SortId { get; set; }
        [UI("描述")] public string Desc { get; set; }
        [UI("缩放级别")] public int? MapZoom { get; set; }
        [UI("中心点")] public string MapCenter { get; set; }
        [UI("图标")] public string Icon { get; set; }

        public virtual User Creator { get; set; }
        public string CreatorName => Creator?.Name;

        public virtual List<GisSceneMenu> SceneMenus { get; set; }
        public virtual List<GisScenePanel> ScenePanels { get; set; }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                Name,
                SortId,
                Desc,
                MapZoom,
                MapCenter,
                Icon,
                CreatorId,
                CreateDt,
                UpdateDt,
                CreatorName,
            };
        }

        public static IQueryable<GisScene> Search(string name = null)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty()) q = q.Where(t => t.Name.Contains(name.Trim()));
            return q.OrderBy(t => t.SortId);
        }
    }
}
