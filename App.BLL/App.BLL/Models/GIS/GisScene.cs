using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.Entities;
using App.Utils;

namespace App.DAL.GIS
{
    /// <summary>GIS 场景样式</summary>
    public record GisMapStyle(string Name, string Path);

    /// <summary>GIS 地图投影</summary>
    public enum GisMapProjection
    {
        [UI("墨卡托")]   Mercator = 0,
        [UI("地球")]     Globe = 1,
        [UI("等矩形")] Equirectangular = 2,
        [UI("自然地球")]  NaturalEarth = 3,
        [UI("温克尔三重")]  WinkelTripel = 4,
    }
    
    /// <summary>GIS 场景</summary>
    [UI("GIS", "GIS场景")]
    public class GisScene : EntityBase<GisScene>
    {
        public static List<GisMapStyle> Styles = new List<GisMapStyle>
        {
            new("Streets", "mapbox://styles/mapbox/streets-v11"),
            new("Satellite", "mapbox://styles/mapbox/satellite-v9"),  // 无标签
            new("SatelliteStreets", "mapbox://styles/mapbox/satellite-streets-v12"),  // 带标签
            new("Dark", "mapbox://styles/mapbox/dark-v10"),
            new("Light", "mapbox://styles/mapbox/light-v10"),
            new("Outdoors", "mapbox://styles/mapbox/outdoors-v11"),
            //new("Navigation", "mapbox://styles/mapbox/navigation-v1"),  // 导航。没看出和outdoors的区别
        };

        [UI("名称")] public string Name { get; set; }
        [UI("图标")] public string Icon { get; set; }
        [UI("排序")] public int SortId { get; set; }
        [UI("描述")] public string Desc { get; set; }
        [UI("缩放级别")] public float? MapZoom { get; set; }
        [UI("中心点")] public string MapCenter { get; set; }
        [UI("倾斜角")] public int? MapPitch { get; set; } = 0;
        [UI("启用3D")] public bool? Map3D { get; set; } = false;
        [UI("地图样式")] public string MapStyle { get; set; } = Styles[0].Name;
        [UI("地图投影")] public GisMapProjection MapProjection { get; set; } = GisMapProjection.Mercator;

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
                MapPitch,
                Icon,
                Map3D,
                MapStyle,
                MapProjection,
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
