import { MapLayer } from "../core/MapLayer.js";
import { addOrUpdateGeoJsonSource, chunkArray, fetchWithTimeout } from "../core/utils.js";
import { CityTempLayer } from "./CityTempLayer.js";

/****************************************************************
 * 城市湿度标签图层
 ****************************************************************/
export class CityHumidityLayer extends MapLayer {
  constructor() {
    super({
      name: "cityHumidity",
      title: "城市湿度标签",
      descript: "按缩放级别显示城市相对湿度",
      api: "https://api.open-meteo.com/v1/forecast",
      refreshSeconds: 600,
      dataInterval: "1小时"
    });
    this.sourceId = "city-humidity-source";
    this.layerId = "city-humidity-layer";
    this.cache = new Map(); // { name: { humidity, time, dataTime, fetchedAt, respDate } }
    this.refreshTimer = null;
  }

  bind(runtime) {
    super.bind(runtime);
    const { map } = runtime;
    map.on("moveend", () => {
      if (this.visible) {
        this.debouncedRefresh();
      }
    });
  }

  debouncedRefresh() {
    if (this.refreshTimer) clearTimeout(this.refreshTimer);
    this.refreshTimer = setTimeout(() => {
      this.refresh();
    }, 1200 + Math.random() * 500);
  }

  cityLevelByZoom(zoom) {
    if (zoom < 4.6) return 2;
    if (zoom < 5.8) return 3;
    if (zoom < 6.8) return 4;
    return 5;
  }

  visibleCitiesByZoom(zoom) {
    const level = this.cityLevelByZoom(zoom);
    return CityTempLayer.CITY_TEMP_POINTS.filter(c => c.level <= level);
  }

  formatDataTime(item) {
    const t = item?.dataTime ? String(item.dataTime) : "";
    if (t) {
      const s = t.replace("T", " ");
      return s.length >= 16 ? s.substring(0, 16) : s;
    }
    const d = item?.respDate ? new Date(item.respDate) : (item?.fetchedAt ? new Date(item.fetchedAt) : null);
    if (!d || Number.isNaN(d.getTime())) return "";
    return d.toLocaleString("zh-CN", { hour12: false });
  }

  getInfoTime(cities) {
    let lastItem = null;
    for (const c of cities) {
      const item = this.cache.get(c.name);
      if (!item) continue;
      if (!lastItem || (item.fetchedAt || 0) > (lastItem.fetchedAt || 0)) lastItem = item;
    }
    return this.formatDataTime(lastItem);
  }

  async fetchBatch(cities) {
    if (!cities.length) return [];
    const query = new URLSearchParams({
      latitude: cities.map(c => c.lat.toFixed(2)).join(","),
      longitude: cities.map(c => c.lon.toFixed(2)).join(","),
      current: "relative_humidity_2m",
      timezone: "Asia/Shanghai"
    });

    try {
      const response = await fetchWithTimeout(`${this.api}?${query.toString()}`, {}, 10000);
      const respDate = response.headers.get("last-modified") || response.headers.get("date") || "";
      if (response.status === 429) {
        console.warn("CityHumidity: 限制请求频率，跳过本次更新");
        return [];
      }
      if (!response.ok) throw new Error(`城市湿度请求失败: ${response.status}`);
      const data = await response.json();
      const list = Array.isArray(data) ? data : [data];
      return cities.map((c, i) => {
        const humidity = Number(list[i]?.current?.relative_humidity_2m);
        const dataTime = list[i]?.current?.time || "";
        return {
          ...c,
          humidity: Number.isFinite(humidity) ? humidity : Number.NaN,
          dataTime,
          fetchedAt: respDate ? new Date(respDate).getTime() : Date.now(),
          respDate
        };
      });
    } catch (e) {
      console.error("CityHumidity fetchBatch error:", e);
      return [];
    }
  }

  async refresh(force = false) {
    if (!this.runtime) return false;
    const { map } = this.runtime;
    const zoom = map.getZoom();
    const cities = this.visibleCitiesByZoom(zoom);
    const now = Date.now();

    const CACHE_TTL = 30 * 60 * 1000;
    const citiesToFetch = cities.filter(c => {
      const cached = this.cache.get(c.name);
      return force || !cached || (now - cached.time > CACHE_TTL);
    });

    if (citiesToFetch.length > 0) {
      const chunks = chunkArray(citiesToFetch, 35);
      for (const chunk of chunks) {
        const part = await this.fetchBatch(chunk);
        for (const item of part) {
          if (Number.isFinite(item.humidity)) {
            this.cache.set(item.name, {
              humidity: item.humidity,
              time: now,
              dataTime: item.dataTime || "",
              fetchedAt: item.fetchedAt || now,
              respDate: item.respDate || ""
            });
          }
        }
      }
    }

    const features = cities.map(c => {
      const cached = this.cache.get(c.name);
      const humidity = cached ? cached.humidity : Number.NaN;
      const text = Number.isFinite(humidity) ? `${c.name}\n${humidity.toFixed(0)}%` : `${c.name}\n--`;
      return {
        type: "Feature",
        geometry: { type: "Point", coordinates: [c.lon, c.lat] },
        properties: { text, humidity: Number.isFinite(humidity) ? humidity : -1 }
      };
    });

    addOrUpdateGeoJsonSource(map, this.sourceId, { type: "FeatureCollection", features });
    if (!map.getLayer(this.layerId)) {
      map.addLayer({
        id: this.layerId,
        type: "symbol",
        source: this.sourceId,
        layout: {
          "text-field": ["get", "text"],
          "text-size": ["interpolate", ["linear"], ["zoom"], 4, 13, 8, 18],
          "text-line-height": 1.05,
          "text-offset": [0, 0.9],
          "text-anchor": "top",
          "text-allow-overlap": true,
          "text-ignore-placement": true,
          "text-max-width": 5
        },
        paint: {
          "text-color": [
            "interpolate", ["linear"], ["get", "humidity"],
            0, "#7f1d1d",
            20, "#991b1b",
            40, "#a16207",
            60, "#ca8a04",
            80, "#3f6212",
            100, "#14532d"
          ],
          "text-halo-color": "#ffffff",
          "text-halo-width": 1.8,
          "text-opacity": 0.95
        }
      });
    }

    const timeText = this.getInfoTime(cities) || new Date(now).toLocaleString("zh-CN", { hour12: false });
    this.setDataTimeText(timeText);
    this.setInfoExtra("");
    this.setOpacity(this.runtime.getOpacity(this.name));
    this.lastStatus = true;
    this.lastTime = now;
    return true;
  }

  setOpacity(opacity) {
    if (!this.runtime) return;
    const { map } = this.runtime;
    if (map.getLayer(this.layerId)) {
      map.setPaintProperty(this.layerId, "text-opacity", opacity);
    }
  }

  hide() {
    super.hide();
    if (!this.runtime) return true;
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
