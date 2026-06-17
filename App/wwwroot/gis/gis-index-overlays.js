/**
 * 地图叠加图层（如云图、气压等）相关逻辑
 */
import { setInfo } from "./overlay/core/utils.js";
import { LayerManager } from "./overlay/core/LayerManager.js";
import { RadarLayer } from "./overlay/layers/RadarLayer.js";
import { SatelliteLiveLayer } from "./overlay/layers/SatelliteLiveLayer.js";
import { SatelliteFallbackLayer } from "./overlay/layers/SatelliteFallbackLayer.js";
import { SatelliteWorldMosaicLayer } from "./overlay/layers/SatelliteWorldMosaicLayer.js";
import { PressureLayer } from "./overlay/layers/PressureLayer.js";
import { AdminBoundaryLayer } from "./overlay/layers/AdminBoundaryLayer.js";
import { WindLayer } from "./overlay/layers/WindLayer.js";
import { CityTempLayer } from "./overlay/layers/CityTempLayer.js";
import { CityHumidityLayer } from "./overlay/layers/CityHumidityLayer.js";
import { CityWeatherLayer } from "./overlay/layers/CityWeatherLayer.js";
import { LatLonGridLayer } from "./overlay/layers/LatLonGridLayer.js";
import { TidePanelLayer } from "./overlay/layers/TidePanelLayer.js";
import { TyphoonLayer } from "./overlay/layers/TyphoonLayer.js";

/**Cloud 图层配置 */
const layerDefs = [
  { name: "typhoon", title: "台风", infoId: "typhoonInfo" },
  { name: "radar", title: "雷达图", infoId: "radarTime" },
  //{ name: "satelliteLive", title: "卫星云图", infoId: "satelliteLiveTime" },
  { name: "satellite", title: "卫星云图", infoId: "satelliteTime" },
  { name: "satelliteWorld", title: "红外云图", infoId: "satelliteWorldTime" },
  { name: "pressure", title: "气压", infoId: "pressureInfo" },
  { name: "wind", title: "气流", infoId: "windInfo" },
  { name: "cityWeather", title: "城市综合天气", infoId: "cityWeatherInfo" },
  { name: "cityTemp", title: "城市温度", infoId: "cityTempInfo" },
  { name: "cityHumidity", title: "城市湿度", infoId: "cityHumidityInfo" },
  { name: "latlonGrid", title: "经纬度", infoId: "gridInfo" },
  { name: "tidePanel", title: "海况与潮汐", infoId: "tidePanelInfo" },
  { name: "adminBoundary", title: "行政边界", infoId: "adminBoundaryInfo" },
];

/**创建图层实例 */
function createLayers() {
  return [
    new RadarLayer(),
    new SatelliteLiveLayer(),
    new SatelliteFallbackLayer(),
    new SatelliteWorldMosaicLayer(),
    new PressureLayer(),
    new AdminBoundaryLayer(),
    new WindLayer(),
    new CityWeatherLayer(),
    new CityTempLayer(),
    new CityHumidityLayer(),
    new LatLonGridLayer(),
    new TidePanelLayer(),
    new TyphoonLayer()
  ];
}

/**获取 GIS 首页上下文 */
function getContext() {
  return window.__gisIndexContext || null;
}

/**构建单个图层项 HTML */
function buildLayerItemHtml(def) {
  return `
    <div class="view-layer-item" data-layer-name="${def.name}">
      <label class="view-layer-check">
        <input type="checkbox" id="${def.name}" data-info-id="${def.infoId}">
        <span>${def.title}</span>
      </label>
      <div id="${def.infoId}" class="view-layer-info">未开启</div>
    </div>
  `;
}

/**渲染视图图层菜单 */
function renderOverlayMenu() {
  const host = document.getElementById("view-overlay-menu");
  if (!host) return;
  host.innerHTML = layerDefs.map(buildLayerItemHtml).join("");
}

/**初始化图层信息文案 */
function initLayerInfos() {
  layerDefs.forEach(def => setInfo(def.infoId, "未开启"));
}

/**挂接样式切换后的图层重建 */
function bindStyleReload(manager, map) {
  if (map.__gisOverlayStyleBound) return;
  map.on("style.load", async () => {
    try {
      await manager.refreshVisible(true);
    } catch (error) {
      console.error("重建叠加图层失败", error);
    }
  });
  map.__gisOverlayStyleBound = true;
}

/**初始化 Cloud 图层到 gis/index */
function initOverlayManager() {
  if (window.__gisIndexOverlayApi) return;

  const ctx = getContext();
  if (!ctx || !ctx.map) return;

  renderOverlayMenu();
  const layers = createLayers();
  const manager = new LayerManager(ctx.map, layers);
  manager.bindUi();
  initLayerInfos();
  bindStyleReload(manager, ctx.map);

  window.__gisIndexOverlayApi = {
    manager,
    layers,
    refreshVisible(force = false) {
      return manager.refreshVisible(force);
    }
  };
}

window.addEventListener("gis:index-ready", initOverlayManager);
if (document.readyState !== "loading") {
  setTimeout(initOverlayManager, 0);
} else {
  document.addEventListener("DOMContentLoaded", () => setTimeout(initOverlayManager, 0), { once: true });
}
