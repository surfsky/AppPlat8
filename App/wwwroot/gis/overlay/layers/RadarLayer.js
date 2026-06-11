import { MapLayer } from "../core/MapLayer.js";
import { fetchWithTimeout, setInfo } from "../core/utils.js";


/****************************************************************
 * 实时降水雷达图图层
 ****************************************************************/
export class RadarLayer extends MapLayer {
  constructor() {
    super({
      name: "radar",
      title: "实时降水雷达图",
      api: "https://api.rainviewer.com/public/weather-maps.json",
      refreshSeconds: 300
    });
    this.sourceId = "radar-source";
    this.layerId = "radar-layer";
  }

  async getRadarTileUrlAndInfo() {
    const response = await fetchWithTimeout(this.api, {}, 8000);
    if (!response.ok) throw new Error(`RainViewer 元数据请求失败: ${response.status}`);
    const meta = await response.json();
    const host = (meta.host || "https://tilecache.rainviewer.com").trim();
    const frames = [
      ...(Array.isArray(meta?.radar?.past) ? meta.radar.past : []),
      ...(Array.isArray(meta?.radar?.nowcast) ? meta.radar.nowcast : [])
    ];
    const latest = frames[frames.length - 1];
    if (!latest?.path) throw new Error("RainViewer 未返回可用雷达帧");
    return {
      tileUrl: `${host}${latest.path}/256/{z}/{x}/{y}/2/1_1.png`,
      label: `雷达帧时间: ${new Date(latest.time * 1000).toLocaleString()}`
    };
  }

  async refresh() {
    const { map } = this.runtime;
    const { tileUrl, label } = await this.getRadarTileUrlAndInfo();
    const source = map.getSource(this.sourceId);
    if (!source) {
      map.addSource(this.sourceId, { type: "raster", tiles: [tileUrl], tileSize: 256 });
      map.addLayer({ id: this.layerId, type: "raster", source: this.sourceId, paint: { "raster-opacity": 0.8 } });
    } else {
      source.setTiles([tileUrl]);
    }
    setInfo("radarTime", label);
    this.setOpacity(this.runtime.getOpacity(this.name));
    this.lastStatus = true;
    this.lastTime = Date.now();
    return true;
  }

  setOpacity(opacity) {
    const { map } = this.runtime;
    if (map.getLayer(this.layerId)) map.setPaintProperty(this.layerId, "raster-opacity", opacity);
  }

  hide() {
    super.hide();
    const { map } = this.runtime;
    if (map.getLayer(this.layerId)) map.setLayoutProperty(this.layerId, "visibility", "none");
    setInfo("radarTime", "未开启");
    return true;
  }

  async show(opacity = 1) {
    const ok = await super.show(opacity);
    const { map } = this.runtime;
    if (map.getLayer(this.layerId)) map.setLayoutProperty(this.layerId, "visibility", "visible");
    return ok;
  }
}
