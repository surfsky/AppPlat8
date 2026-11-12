import { MapLayer } from "../core/MapLayer.js";
import { setInfo } from "../core/utils.js";


/****************************************************************
 * 卫星云图兜底图层
 ****************************************************************/
export class SatelliteFallbackLayer extends MapLayer {
  constructor() {
    super({
      name: "satellite",
      title: "卫星云图兜底",
      api: "https://gibs.earthdata.nasa.gov/wmts/epsg3857/best/VIIRS_SNPP_CorrectedReflectance_TrueColor/default",
      refreshSeconds: 3600
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
    const { map } = this.runtime;
    const date = this.formatUtcDate(1);
    const tileUrl = `${this.api}/${date}/GoogleMapsCompatible_Level9/{z}/{y}/{x}.jpg`;
    const source = map.getSource(this.sourceId);
    if (!source) {
      map.addSource(this.sourceId, { type: "raster", tiles: [tileUrl], tileSize: 256 });
      map.addLayer({ id: this.layerId, type: "raster", source: this.sourceId, paint: { "raster-opacity": 0.45 } });
    } else {
      source.setTiles([tileUrl]);
    }
    setInfo("satelliteTime", `卫星云图日期(UTC): ${date}`);
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
    setInfo("satelliteTime", "未开启");
    return true;
  }

  async show(opacity = 1) {
    const ok = await super.show(opacity);
    const { map } = this.runtime;
    if (map.getLayer(this.layerId)) map.setLayoutProperty(this.layerId, "visibility", "visible");
    return ok;
  }
}
