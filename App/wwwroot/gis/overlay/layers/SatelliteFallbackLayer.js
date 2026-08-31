import { MapLayer } from "../MapLayer.js";


/****************************************************************
 * 卫星云图兜底图层
 ****************************************************************/
export class SatelliteFallbackLayer extends MapLayer {
  constructor() {
    super({
      name: "satellite",
      title: "卫星云图兜底",
      api: "https://gibs.earthdata.nasa.gov/wmts/epsg3857/best/VIIRS_SNPP_CorrectedReflectance_TrueColor/default",
      refreshCron: "0 * * * *"
    });
    this.sourceId = "satellite-source";
    this.layerId = "satellite-layer";
  }

  formatUtcDate(offsetDays) {
    const d = new Date();
    d.setUTCDate(d.getUTCDate() - offsetDays);
    const y = d.getUTCFullYear();
    const m = String(d.getUTCMonth() + 1).padStart(2, "0");
    const day = String(d.getUTCDate()).padStart(2, "0");
    return `${y}-${m}-${day}`;
  }

  async refresh() {
    try {
      const { map } = this.runtime;
      const date = this.formatUtcDate(1);
      const tileUrl = `${this.api}/${date}/GoogleMapsCompatible_Level9/{z}/{y}/{x}.jpg`;
      const source = map.getSource(this.sourceId);
      if (!source) {
        map.addSource(this.sourceId, { type: "raster", tiles: [tileUrl], tileSize: 256 });
        map.addLayer({ id: this.layerId, type: "raster", source: this.sourceId, paint: { "raster-opacity": 0.8 } });
      } else {
        source.setTiles([tileUrl]);
      }
      this.setDataTimeText(`${date} UTC`);
      this.setInfoExtra("");
      this.setOpacity(this.runtime.getOpacity(this.name));
      this.lastStatus = true;
    } catch (e) {
      console.error("刷新卫星云图失败", e);
      const msg = e instanceof Error ? e.message : String(e || "卫星云图加载失败");
      this.setLastError(msg || "卫星云图加载失败");
      this.clearDataTime();
      this.setInfoExtra("");
      this.lastStatus = false;
      return false;
    }
    this.lastTime = Date.now();
    return true;
  }

  setOpacity(opacity) {
    const safe = Number.isFinite(Number(opacity)) ? Number(opacity) : 0.8;
    const { map } = this.runtime;
    if (map.getLayer(this.layerId)) map.setPaintProperty(this.layerId, "raster-opacity", safe);
    this.opacity = safe;
  }

  hide() {
    super.hide();
    const { map } = this.runtime;
    if (map.getLayer(this.layerId)) map.setLayoutProperty(this.layerId, "visibility", "none");
    this.clearDataTime();
    this.setInfoExtra("");
    return true;
  }

  async show(opacity = 0.8) {
    const ok = await super.show(opacity);
    const { map } = this.runtime;
    if (map.getLayer(this.layerId)) map.setLayoutProperty(this.layerId, "visibility", "visible");
    this.setOpacity(Number.isFinite(Number(opacity)) ? Number(opacity) : 0.8);
    return ok;
  }
}
