import { setInfo } from "./core/utils.js";
import { LayerManager } from "./core/LayerManager.js";
import { RadarLayer } from "./layers/RadarLayer.js";
import { SatelliteLiveLayer } from "./layers/SatelliteLiveLayer.js";
import { SatelliteFallbackLayer } from "./layers/SatelliteFallbackLayer.js";
import { AdminBoundaryLayer } from "./layers/AdminBoundaryLayer.js";
import { PressureLayer } from "./layers/PressureLayer.js";
import { WindLayer } from "./layers/WindLayer.js";
import { CityTempLayer } from "./layers/CityTempLayer.js";
import { TyphoonLayer } from "./layers/TyphoonLayer.js";
import { TidePanelLayer } from "./layers/TidePanelLayer.js";
import { LatLonGridLayer } from "./layers/LatLonGridLayer.js";

const MAPBOX_TOKEN = "YOUR_MAPBOX_TOKEN";
mapboxgl.accessToken = MAPBOX_TOKEN;

function initCtrlToggle() {
  const panel = document.querySelector(".ctrl");
  const handle = document.getElementById("ctrlHandle");
  const btnCollapse = document.getElementById("btnCtrlCollapse");
  const key = "cloud.ctrl.collapsed";

  if (!panel || !handle) return;

  const setCollapsed = (collapsed) => {
    panel.style.display = collapsed ? "none" : "block";
    handle.style.display = collapsed ? "flex" : "none";
    try {
      localStorage.setItem(key, collapsed ? "1" : "0");
    } catch (_e) { }
  };

  btnCollapse?.addEventListener("click", () => setCollapsed(true));
  handle.addEventListener("click", () => setCollapsed(false));

  let initCollapsed = false;
  try {
    initCollapsed = localStorage.getItem(key) === "1";
  } catch (_e) { }
  setCollapsed(initCollapsed);
}

function setGlobalMeta(text) {
  setInfo("globalMeta", text);
}

function localizeMapLabelsToChinese(map) {
  const style = map.getStyle();
  if (!style || !Array.isArray(style.layers)) return;
  for (const layer of style.layers) {
    if (layer.type !== "symbol" || layer.source !== "composite") continue;
    try {
      map.setLayoutProperty(layer.id, "text-field", ["coalesce", ["get", "name_zh"], ["get", "name"]]);
      const sourceLayer = layer["source-layer"] || "";
      if (/settlement|place|state_label|country_label|marine_label|water_point_label/i.test(layer.id)
        || /place_label|settlement|state_label|country_label/i.test(sourceLayer)) {
        map.setLayoutProperty(layer.id, "visibility", "none");
      }
    } catch (_e) {
      // ignore incompatible symbol layer
    }
  }
}

const map = new mapboxgl.Map({
  container: "map",
  style: "mapbox://styles/mapbox/satellite-v9",
  center: [105, 35],
  zoom: 4,
  minZoom: 3,
  maxZoom: 10,
  projection: "mercator"
});
window["map"] = map;
map.addControl(new mapboxgl.NavigationControl());

const overlayLayers = [
  new RadarLayer(),
  new SatelliteLiveLayer(),
  new SatelliteFallbackLayer(),
  new PressureLayer(),
  new AdminBoundaryLayer(),
  new WindLayer(),
  new CityTempLayer(),
  new TyphoonLayer(),
  new LatLonGridLayer(),
  new TidePanelLayer()
];

map.on("styledata", () => localizeMapLabelsToChinese(map));

initCtrlToggle();

map.on("load", async () => {
  const manager = new LayerManager(map, overlayLayers);
  manager.bindUi();
  let lastView = { center: map.getCenter(), zoom: map.getZoom() };

  setInfo("radarTime", "未开启");
  setInfo("satelliteLiveTime", "未开启");
  setInfo("satelliteTime", "未开启");
  setInfo("pressureInfo", "未开启");
  setInfo("adminBoundaryInfo", "未开启");
  setInfo("windInfo", "未开启");
  setInfo("cityTempInfo", "未开启");
  setInfo("typhoonInfo", "未开启");
  setInfo("gridInfo", "未开启");
  setInfo("tidePanelInfo", "未开启");

  map.on("moveend", async () => {
    const center = map.getCenter();
    const zoom = map.getZoom();
    const p0 = map.project(lastView.center);
    const p1 = map.project(center);
    const dx = p1.x - p0.x;
    const dy = p1.y - p0.y;
    const pixelMove = Math.sqrt(dx * dx + dy * dy);
    const zoomDelta = Math.abs(zoom - lastView.zoom);
    const minRefreshDistance = Math.min(window.innerWidth, window.innerHeight) / 3;

    if (pixelMove < minRefreshDistance && zoomDelta < 0.05) {
      return;
    }

    await manager.refreshVisible(true);
    lastView = { center, zoom };
    setGlobalMeta(`最后刷新: ${new Date().toLocaleString()}`);
  });

  map.on("zoomend", async () => {
    await manager.refreshVisible(true);
    lastView = { center: map.getCenter(), zoom: map.getZoom() };
    setGlobalMeta(`最后刷新: ${new Date().toLocaleString()}`);

    showInfo();
  });

  map.on("move", showInfo);

  // 显示当前缩放级别和中心坐标(zoom保留2位小数，经纬度保留6位小数)
  function showInfo(){
    var lblInfo = document.getElementById("lblInfo");
    lblInfo.innerHTML = `${map.getZoom().toFixed(2)}：${map.getCenter().toArray().map(n => n.toFixed(6)).join(', ')}`;
  }

  setInterval(async () => {
    await manager.refreshVisible(false);
    setGlobalMeta(`最后刷新: ${new Date().toLocaleString()}`);
  }, 2 * 60 * 1000);

  setGlobalMeta(`最后刷新: ${new Date().toLocaleString()}`);
});
