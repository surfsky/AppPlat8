/**
 * 地图叠加图层（如云图、气压等）相关逻辑
 */
import { setInfo } from "./overlay/utils.js";
import { LayerManager } from "./overlay/LayerManager.js";
import { RadarLayer } from "./overlay/layers/RadarLayer.js";
import { SatelliteLiveLayer } from "./overlay/layers/SatelliteLiveLayer.js";
import { SatelliteFallbackLayer } from "./overlay/layers/SatelliteFallbackLayer.js";
import { SatelliteWorldMosaicLayer } from "./overlay/layers/SatelliteWorldMosaicLayer.js";
import { PressureLayer } from "./overlay/layers/PressureLayer.js";
import { AdminBoundaryLayer } from "./overlay/layers/AdminBoundaryLayer.js";
import { WindLayer } from "./overlay/layers/WindLayer.js";
import { CityTempLayer } from "./overlay/layers/CityTempLayer.js";
import { CityTempColorLayer } from "./overlay/layers/CityTempColorLayer.js";
import { CityHumidityLayer } from "./overlay/layers/CityHumidityLayer.js";
import { CityWeatherLayer } from "./overlay/layers/CityWeatherLayer.js";
import { LatLonGridLayer } from "./overlay/layers/LatLonGridLayer.js";
import { TidePanelLayer } from "./overlay/layers/TidePanelLayer.js";
import { TyphoonLayer } from "./overlay/layers/TyphoonLayer.js?v=2";

/**Cloud 图层配置 */
const layerDefs = [
  { name: "typhoon", title: "台风", layerType: "TyphoonLayer", infoId: "typhoonInfo" },
  { name: "radar", title: "雷达图", layerType: "RadarLayer", infoId: "radarTime" },
  //{ name: "satelliteLive", title: "卫星云图", infoId: "satelliteLiveTime" },
  { name: "satellite", title: "卫星云图", layerType: "SatelliteFallbackLayer", infoId: "satelliteTime" },
  { name: "satelliteWorld", title: "红外云图", layerType: "SatelliteWorldMosaicLayer", infoId: "satelliteWorldTime" },
  { name: "pressure", title: "气压", layerType: "PressureLayer", infoId: "pressureInfo" },
  { name: "wind", title: "气流", layerType: "WindLayer", infoId: "windInfo" },
  { name: "cityWeather", title: "城市综合天气", layerType: "CityWeatherLayer", infoId: "cityWeatherInfo" },
  { name: "cityTempColor", title: "气温色斑", layerType: "CityTempColorLayer", infoId: "cityTempColorInfo" },
  { name: "cityTemp", title: "城市温度", layerType: "CityTempLayer", infoId: "cityTempInfo" },
  { name: "cityHumidity", title: "城市湿度", layerType: "CityHumidityLayer", infoId: "cityHumidityInfo" },
  { name: "latlonGrid", title: "经纬度", layerType: "LatLonGridLayer", infoId: "gridInfo" },
  { name: "tidePanel", title: "海况与潮汐", layerType: "TidePanelLayer", infoId: "tidePanelInfo" },
  { name: "adminBoundary", title: "行政边界", layerType: "AdminBoundaryLayer", infoId: "adminBoundaryInfo" },
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
    new CityTempColorLayer(),
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
    <div class="view-layer-item view-layer-item-config" data-layer-name="${def.name}">
      <label class="view-layer-check">
        <input type="checkbox" id="${def.name}" data-info-id="${def.infoId}">
        <span>${def.title}</span>
      </label>
      <div class="view-layer-item-right">
        <div id="${def.infoId}" class="view-layer-info">未开启</div>
        <button type="button" class="layer-config-btn" data-layer-name="${def.name}" title="图层配置">
          <i class="fa-solid fa-gear"></i>
        </button>
      </div>
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
  ensureLayerConfigStyles();
  const layers = createLayers();
  const manager = new LayerManager(ctx.map, layers);
  manager.bindUi();
  initLayerInfos();
  bindStyleReload(manager, ctx.map);
  bindConfigButtons(manager);

  window.__gisIndexOverlayApi = {
    manager,
    layers,
    defs: layerDefs.map(item => ({ ...item })),
    refreshVisible(force = false) {
      return manager.refreshVisible(force);
    },
    setActiveLayers(layerNames = []) {
      return manager.setActiveLayers(layerNames);
    }
  };
}

/**确保图层配置按钮样式已注入 */
function ensureLayerConfigStyles() {
  const styleId = "gis-layer-config-styles";
  if (document.getElementById(styleId)) return;
  const el = document.createElement("style");
  el.id = styleId;
  el.textContent = `
    .view-layer-item-config {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
    }
    .view-layer-item-config .view-layer-check {
      flex: 1;
      min-width: 0;
    }
    .view-layer-item-right {
      display: flex;
      align-items: center;
      gap: 6px;
      flex-shrink: 0;
    }
    .view-layer-item-right .view-layer-info {
      margin: 0;
    }
    .layer-config-btn {
      width: 26px;
      height: 26px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      border-radius: 6px;
      background: transparent;
      border: 1px solid rgba(148, 163, 184, 0.35);
      color: #93c5fd;
      cursor: pointer;
      font-size: 13px;
      line-height: 1;
      padding: 0;
      transition: all 0.15s ease;
    }
    .layer-config-btn:hover {
      background: rgba(59, 130, 246, 0.18);
      border-color: rgba(96, 165, 250, 0.55);
      color: #bfdbfe;
    }
    .layer-config-btn:active {
      transform: translateY(1px);
    }
    .layer-config-drawer .el-descriptions {
      --el-descriptions-table-border: 1px solid #e2e8f0;
    }
    .layer-config-drawer .el-descriptions__label {
      width: 120px;
      color: #475569;
      font-weight: 600;
    }
    .layer-config-drawer .section-title {
      font-size: 14px;
      font-weight: 700;
      color: #1e293b;
      margin: 0 0 12px 0;
      padding-left: 10px;
      border-left: 3px solid #3b82f6;
      line-height: 1.4;
    }
    .layer-config-drawer .section-block + .section-block {
      margin-top: 22px;
    }
    .layer-config-drawer .error-empty {
      color: #94a3b8;
    }
    .layer-config-drawer .error-text {
      color: #dc2626;
      word-break: break-all;
    }
    .layer-config-drawer .refresh-actions {
      display: flex;
      justify-content: flex-end;
      gap: 10px;
      padding-top: 8px;
    }
  `;
  document.head.appendChild(el);
}

/**绑定图层配置按钮点击事件 */
function bindConfigButtons(manager) {
  const host = document.getElementById("view-overlay-menu");
  if (!host) return;
  host.querySelectorAll(".layer-config-btn").forEach(btn => {
    btn.addEventListener("click", (evt) => {
      evt.preventDefault();
      evt.stopPropagation();
      const name = btn.getAttribute("data-layer-name");
      if (!name) return;
      try {
        if (manager && typeof manager.openLayerConfig === "function") {
          manager.openLayerConfig(name);
        }
      } catch (e) {
        console.error("打开图层配置失败", e);
      }
    });
  });
}

window.addEventListener("gis:index-ready", initOverlayManager);
if (document.readyState !== "loading") {
  setTimeout(initOverlayManager, 0);
} else {
  document.addEventListener("DOMContentLoaded", () => setTimeout(initOverlayManager, 0), { once: true });
}
