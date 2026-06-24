import { MapLayer } from "../core/MapLayer.js";
import { fetchWithTimeout } from "../core/utils.js";

const ADMIN_COUNTRY_GEOJSON = "https://geo.datav.aliyun.com/areas_v3/bound/100000.json";
const ADMIN_PROVINCE_GEOJSON = "https://geo.datav.aliyun.com/areas_v3/bound/100000_full.json";
const CACHE_TTL_MS = 24 * 60 * 60 * 1000;



/****************************************************************
 * 行政边界图层
 ****************************************************************/
export class AdminBoundaryLayer extends MapLayer {
  constructor() {
    super({
      name: "adminBoundary",
      title: "行政边界",
      api: `${ADMIN_COUNTRY_GEOJSON}, ${ADMIN_PROVINCE_GEOJSON}`,
      refreshSeconds: 0
    });
    this.countrySource = "admin-country-source";
    this.provinceSource = "admin-province-source";
    this.citySource = "admin-city-source";
    this.districtSource = "admin-district-source";
    this.provinceFeatures = [];
    this.loadedProvinceAdcode = null;
    this.cacheMap = new Map();
    this.layers = [
      "admin-boundary-country-layer",
      "admin-boundary-province-layer",
      "admin-boundary-city-layer",
      "admin-boundary-district-layer"
    ];
  }

  getCacheKey(url) {
    return `cloud7_admin_cache_${url}`;
  }

  async fetchJson(url) {
    if (this.cacheMap.has(url)) return this.cacheMap.get(url);

    const cacheKey = this.getCacheKey(url);
    try {
      const raw = localStorage.getItem(cacheKey);
      if (raw) {
        const parsed = JSON.parse(raw);
        if (parsed?.ts && parsed?.data && Date.now() - parsed.ts < CACHE_TTL_MS) {
          this.cacheMap.set(url, parsed.data);
          return parsed.data;
        }
      }
    } catch (_e) {
      // ignore broken cache
    }

    const response = await fetchWithTimeout(url, {}, 12000);
    if (!response.ok) throw new Error(`行政区边界请求失败: ${response.status}`);
    const json = await response.json();
    this.cacheMap.set(url, json);
    try {
      localStorage.setItem(cacheKey, JSON.stringify({ ts: Date.now(), data: json }));
    } catch (_e) {
      // ignore storage quota
    }
    return json;
  }

  isPointInRing(point, ring) {
    const x = point[0];
    const y = point[1];
    let inside = false;
    for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) {
      const xi = ring[i][0];
      const yi = ring[i][1];
      const xj = ring[j][0];
      const yj = ring[j][1];
      const intersect = ((yi > y) !== (yj > y)) && (x < ((xj - xi) * (y - yi)) / ((yj - yi) || 1e-12) + xi);
      if (intersect) inside = !inside;
    }
    return inside;
  }

  featureContainsPoint(feature, lng, lat) {
    const geom = feature?.geometry;
    if (!geom) return false;
    const point = [lng, lat];

    if (geom.type === "Polygon") {
      const rings = geom.coordinates || [];
      if (!rings.length) return false;
      if (!this.isPointInRing(point, rings[0])) return false;
      for (let i = 1; i < rings.length; i++) {
        if (this.isPointInRing(point, rings[i])) return false;
      }
      return true;
    }

    if (geom.type === "MultiPolygon") {
      const polygons = geom.coordinates || [];
      for (const polygon of polygons) {
        if (!polygon.length) continue;
        if (!this.isPointInRing(point, polygon[0])) continue;
        let inHole = false;
        for (let i = 1; i < polygon.length; i++) {
          if (this.isPointInRing(point, polygon[i])) {
            inHole = true;
            break;
          }
        }
        if (!inHole) return true;
      }
    }

    return false;
  }

  getFeatureCenter(feature) {
    const center = feature?.properties?.center;
    if (Array.isArray(center) && center.length === 2) return center;
    return null;
  }

  pickNearestProvinceAdcode(centerLngLat) {
    for (const f of this.provinceFeatures) {
      if (this.featureContainsPoint(f, centerLngLat.lng, centerLngLat.lat)) {
        return f?.properties?.adcode || null;
      }
    }

    if (!this.provinceFeatures.length) return null;
    let best = null;
    let bestDist = Infinity;
    for (const f of this.provinceFeatures) {
      const c = this.getFeatureCenter(f);
      if (!c) continue;
      const dx = Number(c[0]) - centerLngLat.lng;
      const dy = Number(c[1]) - centerLngLat.lat;
      const d = dx * dx + dy * dy;
      if (d < bestDist) {
        bestDist = d;
        best = f;
      }
    }
    return best?.properties?.adcode || null;
  }

  async ensureCityDistrictSource(adcode) {
    const { map } = this.runtime;
    if (!adcode) return;
    if (this.loadedProvinceAdcode === adcode && map.getSource(this.citySource) && map.getSource(this.districtSource)) return;

    const cityData = await this.fetchJson(`https://geo.datav.aliyun.com/areas_v3/bound/${adcode}_full.json`);
    const cityFeatures = Array.isArray(cityData?.features) ? cityData.features : [];

    const districtFeatures = [];
    for (const cf of cityFeatures) {
      const cityCode = cf?.properties?.adcode;
      if (!cityCode) continue;
      try {
        const districtData = await this.fetchJson(`https://geo.datav.aliyun.com/areas_v3/bound/${cityCode}_full.json`);
        const dFeatures = Array.isArray(districtData?.features) ? districtData.features : [];
        districtFeatures.push(...dFeatures);
      } catch (_e) {
        // ignore per-city failure
      }
    }

    const cityGeo = { type: "FeatureCollection", features: cityFeatures };
    const districtGeo = { type: "FeatureCollection", features: districtFeatures };

    const citySourceObj = map.getSource(this.citySource);
    const districtSourceObj = map.getSource(this.districtSource);
    if (!citySourceObj) map.addSource(this.citySource, { type: "geojson", data: cityGeo });
    else citySourceObj.setData(cityGeo);
    if (!districtSourceObj) map.addSource(this.districtSource, { type: "geojson", data: districtGeo });
    else districtSourceObj.setData(districtGeo);

    this.loadedProvinceAdcode = adcode;
  }

  async ensureLayers() {
    const { map } = this.runtime;
    const existing = map.getLayer(this.layers[0]);

    if (!map.getSource(this.countrySource)) {
      map.addSource(this.countrySource, { type: "geojson", data: await this.fetchJson(ADMIN_COUNTRY_GEOJSON) });
    }
    if (!map.getSource(this.provinceSource)) {
      const provinceData = await this.fetchJson(ADMIN_PROVINCE_GEOJSON);
      this.provinceFeatures = Array.isArray(provinceData?.features) ? provinceData.features : [];
      map.addSource(this.provinceSource, { type: "geojson", data: provinceData });
    }

    const adcode = this.pickNearestProvinceAdcode(map.getCenter());
    await this.ensureCityDistrictSource(adcode);

    if (existing) return;

    map.addLayer({
      id: this.layers[0],
      type: "line",
      source: this.countrySource,
      minzoom: 2,
      paint: {
        "line-color": "#b4f8ff",
        "line-width": ["interpolate", ["linear"], ["zoom"], 2, 1.2, 8, 2.8],
        "line-opacity": 0.86
      }
    });

    map.addLayer({
      id: this.layers[1],
      type: "line",
      source: this.provinceSource,
      minzoom: 4,
      paint: {
        "line-color": "#7dd3fc",
        "line-width": ["interpolate", ["linear"], ["zoom"], 4, 0.9, 9, 1.8],
        "line-opacity": 0.8
      }
    });

    map.addLayer({
      id: this.layers[2],
      type: "line",
      source: this.citySource,
      minzoom: 5,
      paint: {
        "line-color": "#67e8f9",
        "line-width": ["interpolate", ["linear"], ["zoom"], 5, 0.7, 10, 1.5, 14, 2.2],
        "line-opacity": 0.72
      }
    });

    map.addLayer({
      id: this.layers[3],
      type: "line",
      source: this.districtSource,
      minzoom: 6.3,
      paint: {
        "line-color": "#34d399",
        "line-width": ["interpolate", ["linear"], ["zoom"], 6.3, 0.55, 10, 1.2, 14, 2.0],
        "line-opacity": 0.66
      }
    });
  }

  async refresh() {
    await this.ensureLayers();
    const adcode = this.pickNearestProvinceAdcode(this.runtime.map.getCenter());
    await this.ensureCityDistrictSource(adcode);
    this.setOpacity(this.runtime.getOpacity(this.name));
    this.setDataTimeText("静态");
    this.setInfoExtra("");
    this.lastStatus = true;
    this.lastTime = Date.now();
    return true;
  }

  setOpacity(opacity) {
    const { map } = this.runtime;
    for (const id of this.layers) {
      if (map.getLayer(id)) map.setPaintProperty(id, "line-opacity", opacity);
    }
  }

  hide() {
    super.hide();
    const { map } = this.runtime;
    for (const id of this.layers) {
      if (map.getLayer(id)) map.setLayoutProperty(id, "visibility", "none");
    }
    this.clearDataTime();
    this.setInfoExtra("");
    return true;
  }

  async show(opacity = 1) {
    const ok = await super.show(opacity);
    const { map } = this.runtime;
    for (const id of this.layers) {
      if (map.getLayer(id)) map.setLayoutProperty(id, "visibility", "visible");
    }
    return ok;
  }
}
