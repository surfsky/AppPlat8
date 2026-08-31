import { MapLayer } from "../MapLayer.js";
import { addOrUpdateGeoJsonSource, chunkArray, fetchWithTimeout } from "../utils.js";




/****************************************************************
 * 城市温度标签图层
 ****************************************************************/
export class CityTempLayer extends MapLayer {
  static CITY_TEMP_POINTS = [
    { name: "北京", lat: 39.9, lon: 116.4, level: 1 },
    { name: "上海", lat: 31.23, lon: 121.47, level: 1 },
    { name: "广州", lat: 23.13, lon: 113.26, level: 1 },
    { name: "深圳", lat: 22.54, lon: 114.06, level: 1 },
    { name: "重庆", lat: 29.56, lon: 106.55, level: 1 },
    { name: "武汉", lat: 30.59, lon: 114.31, level: 1 },
    { name: "成都", lat: 30.57, lon: 104.07, level: 1 },
    { name: "西安", lat: 34.34, lon: 108.94, level: 1 },
    { name: "南京", lat: 32.06, lon: 118.8, level: 2 },
    { name: "杭州", lat: 30.27, lon: 120.16, level: 2 },
    { name: "青岛", lat: 36.07, lon: 120.38, level: 2 },
    { name: "厦门", lat: 24.48, lon: 118.09, level: 2 },
    { name: "福州", lat: 26.08, lon: 119.3, level: 2 },
    { name: "宁波", lat: 29.87, lon: 121.55, level: 3 },
    { name: "温州", lat: 27.99, lon: 120.7, level: 3 },
    { name: "台州", lat: 28.66, lon: 121.43, level: 4 },
    { name: "金华", lat: 29.08, lon: 119.65, level: 4 },
    { name: "丽水", lat: 28.45, lon: 119.92, level: 4 },
    { name: "乐清", lat: 28.11, lon: 120.96, level: 5 },
    { name: "瑞安", lat: 27.78, lon: 120.65, level: 5 },
    { name: "永嘉", lat: 28.15, lon: 120.69, level: 5 },
    { name: "龙港", lat: 27.58, lon: 120.55, level: 5 },
    { name: "平阳", lat: 27.66, lon: 120.57, level: 5 },
    { name: "苍南", lat: 27.52, lon: 120.43, level: 5 },
    { name: "文成", lat: 27.79, lon: 120.09, level: 5 },
    { name: "泰顺", lat: 27.56, lon: 119.72, level: 5 },
    { name: "洞头", lat: 27.84, lon: 121.15, level: 5 },
    { name: "龙湾", lat: 27.93, lon: 120.81, level: 5 },
    { name: "瓯海", lat: 27.97, lon: 120.64, level: 5 },
    { name: "鹿城", lat: 28.01, lon: 120.65, level: 5 }
  ];

  constructor() {
    super({
      name: "cityTemp",
      title: "城市温度标签",
      descript: "按缩放级别显示城市温度",
      api: "https://api.open-meteo.com/v1/forecast",
      refreshCron: "*/30 * * * *"
    });
    this.sourceId = "city-temp-source";
    this.layerId = "city-temp-layer";
    this.cache = new Map(); // { name: { temp, time, dataTime, fetchedAt, respDate } }
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
    }, 1200 + Math.random() * 500); // 稍微长一点的延迟，并增加随机偏移以错开请求
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
      current: "temperature_2m",
      timezone: "Asia/Shanghai"
    });
    
    try {
      const response = await fetchWithTimeout(`${this.api}?${query.toString()}`, {}, 10000);
      const respDate = response.headers.get("last-modified") || response.headers.get("date") || "";
      if (response.status === 429) {
        console.warn("CityTemp: 限制请求频率，跳过本次更新");
        return [];
      }
      if (!response.ok) throw new Error(`城市温度请求失败: ${response.status}`);
      const data = await response.json();
      const list = Array.isArray(data) ? data : [data];
      return cities.map((c, i) => {
        const t = Number(list[i]?.current?.temperature_2m);
        const dataTime = list[i]?.current?.time || "";
        return {
          ...c,
          temp: Number.isFinite(t) ? t : Number.NaN,
          dataTime,
          fetchedAt: respDate ? new Date(respDate).getTime() : Date.now(),
          respDate
        };
      });
    } catch (e) {
      console.error("CityTemp fetchBatch error:", e);
      return [];
    }
  }

  async refresh(force = false) {
    if (!this.runtime) return false;
    const { map } = this.runtime;
    const zoom = map.getZoom();
    const cities = this.visibleCitiesByZoom(zoom);
    const now = Date.now();

    try {
      // 找出缓存中没有或已过期的城市
      const CACHE_TTL = 30 * 60 * 1000; // 30分钟缓存
      const citiesToFetch = cities.filter(c => {
        const cached = this.cache.get(c.name);
        return force || !cached || (now - cached.time > CACHE_TTL);
      });

      if (citiesToFetch.length > 0) {
        const chunks = chunkArray(citiesToFetch, 35);
        let gotAny = false;
        for (const chunk of chunks) {
          const part = await this.fetchBatch(chunk);
          for (const item of part) {
            if (Number.isFinite(item.temp)) {
              this.cache.set(item.name, {
                temp: item.temp,
                time: now,
                dataTime: item.dataTime || "",
                fetchedAt: item.fetchedAt || now,
                respDate: item.respDate || ""
              });
              gotAny = true;
            }
          }
        }
        // 有城市需要取，但没有任何结果，视为失败
        if (citiesToFetch.length > 0 && !gotAny) {
          const allCached = cities.every(c => Number.isFinite(this.cache.get(c.name)?.temp));
          if (!allCached) throw new Error("城市温度无可用结果");
        }
      }

      const features = cities.map(c => {
        const cached = this.cache.get(c.name);
        const temp = cached ? cached.temp : Number.NaN;
        const text = Number.isFinite(temp) ? `${c.name}\n${temp.toFixed(0)}°C` : `${c.name}\n--`;
        return {
          type: "Feature",
          geometry: { type: "Point", coordinates: [c.lon, c.lat] },
          properties: { text, temp: Number.isFinite(temp) ? temp : -99 }
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
              "interpolate", ["linear"], ["get", "temp"],
              -10, "#93c5fd",
              0, "#bfdbfe",
              12, "#fef08a",
              22, "#fdba74",
              30, "#fb7185",
              36, "#ef4444"
            ],
            "text-halo-color": "#000000",
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
    } catch (e) {
      console.error("刷新城市温度失败", e);
      const msg = e instanceof Error ? e.message : String(e || "城市温度加载失败");
      this.setLastError(msg || "城市温度加载失败");
      this.clearDataTime();
      this.setInfoExtra("");
      this.lastStatus = false;
      return false;
    }
    this.lastTime = now;
    return true;
  }

  setOpacity(opacity) {
    const safe = Number.isFinite(Number(opacity)) ? Number(opacity) : 0.8;
    if (!this.runtime) return;
    const { map } = this.runtime;
    if (map.getLayer(this.layerId)) {
      map.setPaintProperty(this.layerId, "text-opacity", 0.95 * safe);
    }
    this.opacity = safe;
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

  async show(opacity = 0.8) {
    const ok = await super.show(opacity);
    const { map } = this.runtime;
    if (map.getLayer(this.layerId)) map.setLayoutProperty(this.layerId, "visibility", "visible");
    this.setOpacity(Number.isFinite(Number(opacity)) ? Number(opacity) : 0.8);
    return ok;
  }
}
