using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.Entities;
using App.Utils;

namespace App.DAL.GIS
{
    /// <summary>GIS 场景样式</summary>
    public record GisMapStyle(
        string Name,
        string Path,
        string Title = null,
        string TileTemplate = null,
        string LabelTemplate = null,
        string[] Subdomains = null,
        int TileSize = 256,
        int MaxZoom = 18,
        string Attrib = null
    );

    /// <summary>GIS 场景展示图层定义</summary>
    public record GisSceneLayerDef(string Name, string Title, string LayerType);

    /// <summary>GIS 地图投影</summary>
    public enum GisMapProjection
    {
        [UI("墨卡托")]   Mercator = 0,
        [UI("地球")]     Globe = 1,
    }

    /// <summary>GIS 场景</summary>
    [UI("GIS", "GIS场景")]
    public class GisScene : EntityBase<GisScene>, ISort
    {
        public static List<GisMapStyle> Styles = new List<GisMapStyle>
        {
            //-- 国内图层优先：天地图（需要 SiteConfig.TiandituKey 配置）
            // 天地图-影像（遥感），叠加影像注记，默认底图
            new("TiandituSatellite",
                Path: "tianditu://satellite",
                Title: "天地图遥感",
                TileTemplate: "https://t{s}.tianditu.gov.cn/img_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=img&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILECOL={x}&TILEROW={y}&TILEMATRIX={z}&tk={tk}",
                LabelTemplate: "https://t{s}.tianditu.gov.cn/cia_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=cia&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILECOL={x}&TILEROW={y}&TILEMATRIX={z}&tk={tk}",
                Subdomains: new [] { "0", "1", "2", "3", "4", "5", "6", "7" },
                TileSize: 256,
                MaxZoom: 18,
                Attrib: "影像：国家基础地理信息中心 Tianditu"
            ),
            // 天地图-矢量，叠加注记
            new("TiandituStreets",
                Path: "tianditu://streets",
                Title: "天地图街道",
                TileTemplate: "https://t{s}.tianditu.gov.cn/vec_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=vec&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILECOL={x}&TILEROW={y}&TILEMATRIX={z}&tk={tk}",
                LabelTemplate: "https://t{s}.tianditu.gov.cn/cva_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=cva&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILECOL={x}&TILEROW={y}&TILEMATRIX={z}&tk={tk}",
                Subdomains: new [] { "0", "1", "2", "3", "4", "5", "6", "7" },
                TileSize: 256,
                MaxZoom: 18,
                Attrib: "矢量：国家基础地理信息中心 Tianditu"
            ),
            // OpenStreetMap（无需 Key）
            new("OpenStreetMap",
                Path: "osm://default",
                Title: "OpenStreetMap",
                TileTemplate: "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
                Subdomains: null,
                TileSize: 256,
                MaxZoom: 19,
                Attrib: "© OpenStreetMap contributors"
            ),
            //-- 国际图层（需要 SiteConfig.MapKey 配置 Mapbox token）
            new("SatelliteStreets", "mapbox://styles/mapbox/satellite-streets-v12", "Mapbox 卫星街道"),
            new("Streets",          "mapbox://styles/mapbox/streets-v11",           "Mapbox 街道"),
            new("Satellite",        "mapbox://styles/mapbox/satellite-v9",          "Mapbox 卫星"),
            new("Dark",             "mapbox://styles/mapbox/dark-v10",              "Mapbox 暗色"),
            new("Light",            "mapbox://styles/mapbox/light-v10",             "Mapbox 明亮"),
            new("Outdoors",         "mapbox://styles/mapbox/outdoors-v11",          "Mapbox 户外"),
        };

        public static List<GisSceneLayerDef> Layers = new List<GisSceneLayerDef>
        {
            new("typhoon", "台风", "TyphoonLayer"),
            new("radar", "雷达图", "RadarLayer"),
            new("satellite", "卫星云图", "SatelliteFallbackLayer"),
            new("satelliteWorld", "红外云图", "SatelliteWorldMosaicLayer"),
            new("pressure", "气压", "PressureLayer"),
            new("wind", "气流", "WindLayer"),
            new("cityWeather", "城市综合天气", "CityWeatherLayer"),
            new("cityTemp", "城市温度", "CityTempLayer"),
            new("cityHumidity", "城市湿度", "CityHumidityLayer"),
            new("latlonGrid", "经纬度", "LatLonGridLayer"),
            new("tidePanel", "海况与潮汐", "TidePanelLayer"),
            new("adminBoundary", "行政边界", "AdminBoundaryLayer"),
        };

        [UI("名称")] public string Name { get; set; }
        [UI("图标")] public string Icon { get; set; }
        [UI("排序")] public int SortId { get; set; }
        [UI("描述")] public string Desc { get; set; }
        [UI("是否默认")] public bool? IsDefault { get; set; } = false;
        [UI("缩放级别")] public float? MapZoom { get; set; }
        [UI("中心点")] public string MapCenter { get; set; }
        [UI("倾斜角")] public int? MapPitch { get; set; } = 0;
        [UI("启用3D")] public bool? Map3D { get; set; } = false;
        [UI("自动旋转")] public bool? AutoRotate { get; set; } = false;
        [UI("地图样式")] public string MapStyle { get; set; } = "TiandituSatellite";
        [UI("地图投影")] public GisMapProjection MapProjection { get; set; } = GisMapProjection.Mercator;

        public virtual User Creator { get; set; }
        public string CreatorName => Creator?.Name;

        public virtual List<GisSceneMenu> SceneMenus { get; set; }
        public virtual List<GisScenePanel> ScenePanels { get; set; }
        public virtual List<GisSceneLayer> SceneLayers { get; set; }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                Name,
                SortId,
                Desc,
                IsDefault,
                MapZoom,
                MapCenter,
                MapPitch,
                Icon,
                Map3D,
                AutoRotate,
                MapStyle,
                MapProjection,
                CreatorId,
                CreateDt,
                UpdateDt,
                CreatorName,
            };
        }

        /// <summary>设为默认场景：全局唯一，自动取消其它场景的默认标记</summary>
        public static APIResult SetDefault(long id)
        {
            using var tran = Db.Context.Database.BeginTransaction();
            try
            {
                var target = Get(id);
                if (target == null) return new APIResult { Code = 404, Message = "场景不存在" };

                // 把所有其它场景的 IsDefault 置为 false
                foreach (var s in Set.Where(t => t.Id != id).ToList())
                {
                    if (s.IsDefault == true)
                    {
                        s.IsDefault = false;
                        Db.Update(s);
                    }
                }

                // 目标场景置为默认
                target.IsDefault = true;
                Db.Update(target);

                tran.Commit();
                return new APIResult { Code = 0, Message = "已设为默认场景", Data = target.Export() };
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        /// <summary>保存场景；若勾选 IsDefault，则自动清除其它默认标记，保证全局唯一</summary>
        public override void Save(ExportMode saveMode = ExportMode.Normal)
        {
            // 如果要设为默认，先清除其它默认场景
            if (this.IsDefault == true)
            {
                var others = Set.Where(t => t.Id != this.Id && t.IsDefault == true).ToList();
                foreach (var other in others)
                {
                    other.IsDefault = false;
                    Db.Update(other);
                }
            }
            base.Save(saveMode);
        }

        public static IQueryable<GisScene> Search(string name = null)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty()) q = q.Where(t => t.Name.Contains(name.Trim()));
            // 默认场景排最前，然后按 SortId、Id
            return q.OrderByDescending(t => t.IsDefault == true).ThenBy(t => t.SortId).ThenBy(t => t.Id);
        }

        public static GisScene GetDefaultScene()
        {
            // 先从数据库找 IsDefault=true 的第一条，找不到则回退到按 SortId 取第一条，再找不到返回保底默认
            var fromDb = Set.AsNoTracking()
                .OrderByDescending(t => t.IsDefault == true)
                .ThenBy(t => t.SortId)
                .ThenBy(t => t.Id)
                .FirstOrDefault();
            if (fromDb != null) return fromDb;

            return new GisScene
            {
                Name = "默认场景",
                Icon = "icon-default",
                SortId = -1,
                Desc = "默认场景",
                IsDefault = true,
                MapZoom = 12,
                MapCenter = "120.6034,27.5686",
                MapPitch = 0,
                Map3D = false,
                AutoRotate = false,
                MapStyle = "TiandituSatellite",
                MapProjection = GisMapProjection.Mercator,
            };
        }

        public static GisScene GetOrCreateDefaultScene()
        {
            var scene = Set.AsNoTracking()
                .OrderByDescending(t => t.IsDefault == true)
                .ThenBy(t => t.SortId)
                .ThenBy(t => t.Id)
                .FirstOrDefault();
            if (scene != null)
                return scene;

            scene = GetDefaultScene();
            scene.CreateDt = DateTime.Now;
            scene.CreatorId = 0;
            scene.Save();
            return scene;
        }
    }
}
