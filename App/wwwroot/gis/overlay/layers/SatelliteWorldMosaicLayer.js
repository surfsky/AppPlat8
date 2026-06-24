import { MapLayer } from "../core/MapLayer.js";


/****************************************************************
 * 全球卫星云图（DWD 世界拼图，约 3 小时更新）
 ****************************************************************/
export class SatelliteWorldMosaicLayer extends MapLayer {
  constructor() {
    super({
      name: "satelliteWorld",
      title: "全球卫星云图",
      api: "https://maps.dwd.de/geoserver/dwd/wms",
      refreshSeconds: 900,
      dataInterval: "3小时"
    });
    this.wmsLayer = "Satellite_worldmosaic_3km_world_ir108_3h";
    this.sourceId = "satellite-world-source";
    this.layerId = "satellite-world-layer";
  }

  getLatestThreeHourUtcIso() {
    const now = new Date();
    const slotHour = Math.floor(now.getUTCHours() / 3) * 3;
    const slot = new Date(Date.UTC(
      now.getUTCFullYear(),
      now.getUTCMonth(),
      now.getUTCDate(),
      slotHour,
      0,
      0
    ));
    return slot.toISOString().replace(".000Z", "Z");
  }

  formatTimeLabel(isoTime) {
    const date = new Date(isoTime);
    if (Number.isNaN(date.getTime())) return isoTime;
    const y = date.getUTCFullYear();
    const m = String(date.getUTCMonth() + 1).padStart(2, "0");
    const d = String(date.getUTCDate()).padStart(2, "0");
    const hh = String(date.getUTCHours()).padStart(2, "0");
    const mm = String(date.getUTCMinutes()).padStart(2, "0");
    return `全球云图(UTC): ${y}-${m}-${d} ${hh}:${mm}`;
  }

  buildTileUrl(timeIso) {
    const time = encodeURIComponent(timeIso);
    return `${this.api}?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&FORMAT=image%2Fpng&TRANSPARENT=true&LAYERS=${this.wmsLayer}&CRS=EPSG%3A3857&WIDTH=256&HEIGHT=256&TIME=${time}&BBOX={bbox-epsg-3857}`;
  }

  async refresh() {
    const { map } = this.runtime;
    const timeIso = this.getLatestThreeHourUtcIso();
    const tileUrl = this.buildTileUrl(timeIso);
    const source = map.getSource(this.sourceId);
    if (!source) {
      map.addSource(this.sourceId, { type: "raster", tiles: [tileUrl], tileSize: 256 });
      map.addLayer({
        id: this.layerId,
        type: "raster",
        source: this.sourceId,
        paint: { "raster-opacity": 0.7 }
      });
    } else {
      source.setTiles([tileUrl]);
    }
    this.setDataTime(timeIso);
    this.setInfoExtra("");
    this.setOpacity(this.runtime.getOpacity(this.name));
    this.lastStatus = true;
    this.lastTime = Date.now();
    return true;
  }

  setOpacity(opacity) {
    const { map } = this.runtime;
    if (map.getLayer(this.layerId)) {
      map.setPaintProperty(this.layerId, "raster-opacity", opacity);
    }
  }

  hide() {
    super.hide();
    const { map } = this.runtime;
    if (map.getLayer(this.layerId)) {
      map.setLayoutProperty(this.layerId, "visibility", "none");
    }
    this.clearDataTime();
    this.setInfoExtra("");
    return true;
  }

  async show(opacity = 1) {
    const ok = await super.show(opacity);
    const { map } = this.runtime;
    if (map.getLayer(this.layerId)) {
      map.setLayoutProperty(this.layerId, "visibility", "visible");
    }
    return ok;
  }
}
