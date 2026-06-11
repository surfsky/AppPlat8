import { MapLayer } from "../core/MapLayer.js";
import { addOrUpdateGeoJsonSource, fetchWithTimeout, setInfo } from "../core/utils.js";


/****************************************************************
 * 台风路径图层
 ****************************************************************/
export class TyphoonLayer extends MapLayer {
  constructor() {
    super({
      name: "typhoon",
      title: "台风路径",
      api: "https://www.gdacs.org/xml/rss_tc_7d.xml",
      refreshSeconds: 1800
    });
    this.sourceId = "typhoon-source";
    this.lineLayerId = "typhoon-line-layer";
    this.pointLayerId = "typhoon-point-layer";
  }

  async refresh() {
    const response = await fetchWithTimeout(this.api, {}, 12000);
    if (!response.ok) throw new Error(`台风数据请求失败: ${response.status}`);
    const text = await response.text();

    const parser = new DOMParser();
    const xml = parser.parseFromString(text, "application/xml");
    const items = Array.from(xml.querySelectorAll("item"));
    const points = [];
    const coords = [];

    for (const item of items) {
      const title = item.querySelector("title")?.textContent || "Typhoon";
      const latText = item.querySelector("geo\\:lat, lat")?.textContent;
      const lonText = item.querySelector("geo\\:long, long")?.textContent;
      const lat = Number(latText);
      const lon = Number(lonText);
      if (!Number.isFinite(lat) || !Number.isFinite(lon)) continue;
      coords.push([lon, lat]);
      points.push({
        type: "Feature",
        geometry: { type: "Point", coordinates: [lon, lat] },
        properties: { title }
      });
    }

    const features = [...points];
    if (coords.length > 1) {
      features.push({
        type: "Feature",
        geometry: { type: "LineString", coordinates: coords },
        properties: { title: "Typhoon Path" }
      });
    }

    addOrUpdateGeoJsonSource(this.runtime.map, this.sourceId, { type: "FeatureCollection", features });

    const { map } = this.runtime;
    if (!map.getLayer(this.lineLayerId)) {
      map.addLayer({
        id: this.lineLayerId,
        type: "line",
        source: this.sourceId,
        filter: ["==", ["geometry-type"], "LineString"],
        paint: {
          "line-color": "#fb7185",
          "line-width": 2,
          "line-opacity": 0.9
        }
      });
    }
    if (!map.getLayer(this.pointLayerId)) {
      map.addLayer({
        id: this.pointLayerId,
        type: "circle",
        source: this.sourceId,
        filter: ["==", ["geometry-type"], "Point"],
        paint: {
          "circle-color": "#f43f5e",
          "circle-radius": 4,
          "circle-stroke-color": "#fff",
          "circle-stroke-width": 1
        }
      });
    }

    setInfo("typhoonInfo", `台风点数: ${points.length}，更新时间: ${new Date().toLocaleTimeString()}`);
    this.setOpacity(this.runtime.getOpacity(this.name));
    this.lastStatus = true;
    this.lastTime = Date.now();
    return true;
  }

  setOpacity(opacity) {
    const { map } = this.runtime;
    if (map.getLayer(this.lineLayerId)) map.setPaintProperty(this.lineLayerId, "line-opacity", opacity);
    if (map.getLayer(this.pointLayerId)) map.setPaintProperty(this.pointLayerId, "circle-opacity", opacity);
  }

  hide() {
    super.hide();
    const { map } = this.runtime;
    if (map.getLayer(this.lineLayerId)) map.setLayoutProperty(this.lineLayerId, "visibility", "none");
    if (map.getLayer(this.pointLayerId)) map.setLayoutProperty(this.pointLayerId, "visibility", "none");
    setInfo("typhoonInfo", "未开启");
    return true;
  }

  async show(opacity = 1) {
    const ok = await super.show(opacity);
    const { map } = this.runtime;
    if (map.getLayer(this.lineLayerId)) map.setLayoutProperty(this.lineLayerId, "visibility", "visible");
    if (map.getLayer(this.pointLayerId)) map.setLayoutProperty(this.pointLayerId, "visibility", "visible");
    return ok;
  }
}
