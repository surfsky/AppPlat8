import { MapLayer } from "../core/MapLayer.js";
import { addOrUpdateGeoJsonSource } from "../core/utils.js";

/****************************************************************
 * 经纬网格线图层
 ****************************************************************/
export class LatLonGridLayer extends MapLayer {
  constructor() {
    super({
      name: "latlonGrid",
      title: "经纬网格线",
      descript: "按缩放自动密度的经纬网格",
      refreshSeconds: 30
    });
    this.sourceId = "latlon-grid-source";
    this.lineLayerId = "latlon-grid-line-layer";
    this.lonLabelLayerId = "latlon-grid-lon-label-layer";
    this.latLabelLayerId = "latlon-grid-lat-label-layer";
    this.refreshTimer = null;
  }

  bind(runtime) {
    super.bind(runtime);
    const { map } = runtime;
    const onViewChanged = () => {
      if (this.visible) this.debouncedRefresh();
    };
    map.on("moveend", onViewChanged);
    map.on("zoomend", onViewChanged);
  }

  debouncedRefresh() {
    if (this.refreshTimer) clearTimeout(this.refreshTimer);
    this.refreshTimer = setTimeout(() => {
      this.refresh(true);
    }, 120);
  }

  getStepByZoom(zoom) {
    if (zoom < 4.5) return 5;
    if (zoom < 6.5) return 2;
    if (zoom < 8) return 1;
    return 0.5;
  }

  getLabelStrideByZoom(zoom) {
    if (zoom < 4.5) return 3;
    if (zoom < 6.5) return 2;
    return 1;
  }

  formatDegree(v) {
    const rounded = Math.round(v * 10) / 10;
    return Number.isInteger(rounded) ? String(rounded) : rounded.toFixed(1);
  }

  buildGeo(bounds, step, zoom) {
    const projectionName = this.runtime?.map?.getProjection && this.runtime.map.getProjection()?.name;
    const west = projectionName === "globe" ? -180 : Math.floor(bounds.getWest() / step) * step;
    const east = projectionName === "globe" ? 180 : Math.ceil(bounds.getEast() / step) * step;
    const south = projectionName === "globe" ? -90 : Math.floor(bounds.getSouth() / step) * step;
    const north = projectionName === "globe" ? 90 : Math.ceil(bounds.getNorth() / step) * step;

    const features = [];
    const spanLon = Math.max(step, east - west);
    const spanLat = Math.max(step, north - south);
    const labelStride = this.getLabelStrideByZoom(zoom);

    const clampLon = (v) => Math.max(west, Math.min(east, v));
    const clampLat = (v) => Math.max(south, Math.min(north, v));

    const labelMarginLat = Math.max(step * 0.12, spanLat * 0.015);
    const labelMarginLon = Math.max(step * 0.12, spanLon * 0.015);
    const lonLabelSouth = clampLat(south + labelMarginLat);
    const lonLabelNorth = clampLat(north - labelMarginLat);
    const latLabelWest = clampLon(west + labelMarginLon);
    const latLabelEast = clampLon(east - labelMarginLon);

    let lonIdx = 0;

    for (let lon = west; lon <= east; lon += step) {
      const text = `${this.formatDegree(Math.abs(lon))}°${lon >= 0 ? "E" : "W"}`;
      features.push({
        type: "Feature",
        geometry: { type: "LineString", coordinates: [[lon, south], [lon, north]] },
        properties: { label: text }
      });

      if (lonIdx % labelStride === 0) {
        features.push({
          type: "Feature",
          geometry: { type: "Point", coordinates: [lon, lonLabelSouth] },
          properties: { label: text, labelType: "lon", labelPos: "south" }
        });
        features.push({
          type: "Feature",
          geometry: { type: "Point", coordinates: [lon, lonLabelNorth] },
          properties: { label: text, labelType: "lon", labelPos: "north" }
        });
      }
      lonIdx += 1;
    }

    let latIdx = 0;
    for (let lat = south; lat <= north; lat += step) {
      const text = `${this.formatDegree(Math.abs(lat))}°${lat >= 0 ? "N" : "S"}`;
      features.push({
        type: "Feature",
        geometry: { type: "LineString", coordinates: [[west, lat], [east, lat]] },
        properties: { label: text }
      });

      if (latIdx % labelStride === 0) {
        features.push({
          type: "Feature",
          geometry: { type: "Point", coordinates: [latLabelWest, lat] },
          properties: { label: text, labelType: "lat", labelPos: "west" }
        });
        features.push({
          type: "Feature",
          geometry: { type: "Point", coordinates: [latLabelEast, lat] },
          properties: { label: text, labelType: "lat", labelPos: "east" }
        });
      }
      latIdx += 1;
    }

    return { type: "FeatureCollection", features };
  }

  async refresh(_force = false) {
    if (!this.runtime) return false;
    const { map } = this.runtime;
    const zoom = map.getZoom();
    const step = this.getStepByZoom(zoom);
    const geo = this.buildGeo(map.getBounds(), step, zoom);
    addOrUpdateGeoJsonSource(map, this.sourceId, geo);

    if (!map.getLayer(this.lineLayerId)) {
      map.addLayer({
        id: this.lineLayerId,
        type: "line",
        source: this.sourceId,
        filter: ["==", ["geometry-type"], "LineString"],
        paint: {
          "line-color": "#ffffff",
          "line-width": 1,
          "line-opacity": 0.82
        }
      });
    }

    if (!map.getLayer(this.lonLabelLayerId)) {
      map.addLayer({
        id: this.lonLabelLayerId,
        type: "symbol",
        source: this.sourceId,
        filter: [
          "all",
          ["==", ["geometry-type"], "Point"],
          ["==", ["get", "labelType"], "lon"]
        ],
        layout: {
          "text-field": ["get", "label"],
          "text-size": ["interpolate", ["linear"], ["zoom"], 4, 9, 8, 12],
          "text-rotate": 0,
          "text-anchor": [
            "match",
            ["get", "labelPos"],
            "north", "top",
            "south", "bottom",
            "center"
          ],
          "text-offset": [
            "match",
            ["get", "labelPos"],
            "north", ["literal", [0, 0.2]],
            "south", ["literal", [0, -0.2]],
            ["literal", [0, 0]]
          ],
          "text-padding": 2,
          "symbol-placement": "point",
          "text-allow-overlap": true,
          "text-ignore-placement": true
        },
        paint: {
          "text-color": "#ffffff",
          "text-halo-color": "#020617",
          "text-halo-width": 2.4,
          "text-halo-blur": 0.18
        }
      });
    }

    if (!map.getLayer(this.latLabelLayerId)) {
      map.addLayer({
        id: this.latLabelLayerId,
        type: "symbol",
        source: this.sourceId,
        filter: [
          "all",
          ["==", ["geometry-type"], "Point"],
          ["==", ["get", "labelType"], "lat"]
        ],
        layout: {
          "text-field": ["get", "label"],
          "text-size": ["interpolate", ["linear"], ["zoom"], 4, 9, 8, 12],
          "text-anchor": [
            "match",
            ["get", "labelPos"],
            "west", "left",
            "east", "right",
            "left"
          ],
          "text-offset": [
            "match",
            ["get", "labelPos"],
            "west", ["literal", [0.15, -0.15]],
            "east", ["literal", [-0.15, -0.15]],
            ["literal", [0.15, -0.15]]
          ],
          "text-padding": 2,
          "symbol-placement": "point",
          "text-allow-overlap": true,
          "text-ignore-placement": true
        },
        paint: {
          "text-color": "#ffffff",
          "text-halo-color": "#020617",
          "text-halo-width": 2.4,
          "text-halo-blur": 0.18
        }
      });
    }

    this.setOpacity(this.runtime.getOpacity(this.name));
    this.setDataTimeText("实时");
    this.setInfoExtra(`步长: ${step}°`);
    this.lastStatus = true;
    this.lastTime = Date.now();
    return true;
  }

  setOpacity(opacity) {
    if (!this.runtime) return;
    const { map } = this.runtime;
    if (map.getLayer(this.lineLayerId)) map.setPaintProperty(this.lineLayerId, "line-opacity", opacity);
    if (map.getLayer(this.lonLabelLayerId)) map.setPaintProperty(this.lonLabelLayerId, "text-opacity", opacity);
    if (map.getLayer(this.latLabelLayerId)) map.setPaintProperty(this.latLabelLayerId, "text-opacity", opacity);
  }

  hide() {
    super.hide();
    if (!this.runtime) return true;
    const { map } = this.runtime;
    if (map.getLayer(this.lineLayerId)) map.setLayoutProperty(this.lineLayerId, "visibility", "none");
    if (map.getLayer(this.lonLabelLayerId)) map.setLayoutProperty(this.lonLabelLayerId, "visibility", "none");
    if (map.getLayer(this.latLabelLayerId)) map.setLayoutProperty(this.latLabelLayerId, "visibility", "none");
    this.clearDataTime();
    this.setInfoExtra("");
    return true;
  }

  async show(opacity = 1) {
    const ok = await super.show(opacity);
    const { map } = this.runtime;
    if (map.getLayer(this.lineLayerId)) map.setLayoutProperty(this.lineLayerId, "visibility", "visible");
    if (map.getLayer(this.lonLabelLayerId)) map.setLayoutProperty(this.lonLabelLayerId, "visibility", "visible");
    if (map.getLayer(this.latLabelLayerId)) map.setLayoutProperty(this.latLabelLayerId, "visibility", "visible");
    return ok;
  }
}
