import { MapLayer } from "../core/MapLayer.js";
import { fetchWithTimeout } from "../core/utils.js";


/****************************************************************
 * 近实时卫星云图图层
 ****************************************************************/
export class SatelliteLiveLayer extends MapLayer {
  constructor() {
    super({
      name: "satelliteLive",
      title: "近实时卫星云图",
      api: "https://api.rainviewer.com/public/weather-maps.json",
      refreshSeconds: 300,
      dataInterval: "15分钟"
    });
    this.sourceId = "satellite-live-source";
    this.layerId = "satellite-live-layer";
  }

  async getTileInfo() {
    const response = await fetchWithTimeout(this.api, {}, 8000);
    if (!response.ok) throw new Error(`RainViewer 元数据请求失败: ${response.status}`);
    const meta = await response.json();
    const host = (meta.host || "https://tilecache.rainviewer.com").trim();
    const frames = Array.isArray(meta?.satellite?.infrared) ? meta.satellite.infrared : [];
    const latest = frames[frames.length - 1];
    if (!latest?.path) throw new Error("RainViewer 未返回可用卫星红外帧");
    const ts = Number(latest.time) || 0;
    const prev = frames.length > 1 ? Number(frames[frames.length - 2]?.time) || 0 : 0;
    const intervalSec = ts > 0 && prev > 0 ? Math.abs(ts - prev) : 0;
    return {
      tileUrl: `${host}${latest.path}/256/{z}/{x}/{y}/0/0_0.png`,
      time: ts > 0 ? new Date(ts * 1000) : null,
      dataInterval: intervalSec > 0 ? this.formatSecondsAsText(intervalSec) : this.dataInterval
    };
  }

  async refresh() {
    const { map } = this.runtime;
    const { tileUrl, time, dataInterval } = await this.getTileInfo();
    const source = map.getSource(this.sourceId);
    if (!source) {
      map.addSource(this.sourceId, { type: "raster", tiles: [tileUrl], tileSize: 256 });
      map.addLayer({ id: this.layerId, type: "raster", source: this.sourceId, paint: { "raster-opacity": 0.65 } });
    } else {
      source.setTiles([tileUrl]);
    }
    if (time) this.setDataTime(time);
    if (dataInterval) this.setDataInterval(dataInterval);
    this.setInfoExtra("");
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
    this.clearDataTime();
    this.setInfoExtra("");
    return true;
  }

  async show(opacity = 1) {
    const ok = await super.show(opacity);
    const { map } = this.runtime;
    if (map.getLayer(this.layerId)) map.setLayoutProperty(this.layerId, "visibility", "visible");
    return ok;
  }
}
