import { MapLayer } from "../core/MapLayer.js";
import { chunkArray, fetchWithTimeout } from "../core/utils.js";
import { CityTempLayer } from "./CityTempLayer.js";

/****************************************************************
 * 城市综合天气图层
 * - 使用自定义 Marker 显示水滴图标 + 文字
 * - 点击 Marker 弹出基础信息和 5 日天气预报
 * - 所有 UI 与逻辑均在本文件内实现
 ****************************************************************/
export class CityWeatherLayer extends MapLayer {
  static STYLE_ID = "city-weather-layer-style";
  static ICONS = {
    sun: "/gis/overlay/layers/sun.svg",
    cloud: "/gis/overlay/layers/cloud.svg",
    rain: "/gis/overlay/layers/rain.svg",
    snow: "/gis/overlay/layers/snow.svg",
    wind: "/gis/overlay/layers/wind.svg"
  };

  constructor() {
    super({
      name: "cityWeather",
      title: "城市综合天气",
      descript: "综合展示城市天气、温度、湿度，并支持查看 5 日天气预报",
      api: "https://api.open-meteo.com/v1/forecast",
      refreshSeconds: 600
    });
    this.cache = new Map();
    this.markerMap = new Map();
    this.refreshTimer = null;
    this.popup = null;
  }

  bind(runtime) {
    super.bind(runtime);
    this.ensureStyles();
    const { map } = runtime;
    map.on("moveend", () => {
      if (this.visible) this.debouncedRefresh();
    });
  }

  ensureStyles() {
    if (document.getElementById(CityWeatherLayer.STYLE_ID)) return;
    const style = document.createElement("style");
    style.id = CityWeatherLayer.STYLE_ID;
    style.textContent = `
      .city-weather-marker {
        display: flex;
        align-items: center;
        gap: 7px;
        cursor: pointer;
        pointer-events: auto;
        user-select: none;
      }
      .city-weather-icon {
        width: 34px;
        height: 34px;
        flex: 0 0 34px;
        filter: drop-shadow(0 2px 6px rgba(15, 23, 42, 0.42));
      }
      .city-weather-text {
        display: flex;
        flex-direction: column;
        gap: 2px;
        padding: 6px 8px 6px 9px;
        min-width: 96px;
        border-radius: 12px;
        background: linear-gradient(180deg, rgba(15, 23, 42, 0.82), rgba(30, 41, 59, 0.72));
        box-shadow: 0 4px 14px rgba(2, 6, 23, 0.24);
        line-height: 1.1;
        backdrop-filter: blur(4px);
        border: 1px solid rgba(255,255,255,0.14);
      }
      .city-weather-top {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 8px;
      }
      .city-weather-city {
        font-size: 13px;
        font-weight: 700;
        color: #ffffff;
        text-shadow: 0 1px 2px rgba(15, 23, 42, 0.8);
      }
      .city-weather-badge {
        padding: 1px 5px;
        border-radius: 999px;
        font-size: 10px;
        font-weight: 700;
        color: #e2e8f0;
        background: rgba(255,255,255,0.12);
        border: 1px solid rgba(255,255,255,0.14);
        white-space: nowrap;
      }
      .city-weather-main {
        font-size: 13px;
        font-weight: 700;
        /*text-shadow: 0 1px 2px rgba(255,255,255,0.6);*/
      }
      .city-weather-sub {
        font-size: 11px;
        color: #e2e8f0;
        text-shadow: 0 1px 2px rgba(15, 23, 42, 0.85);
      }
      .city-weather-mapbox-popup .mapboxgl-popup-content {
        padding: 0;
        border-radius: 16px;
        overflow: hidden;
        box-shadow: 0 18px 48px rgba(15, 23, 42, 0.22);
      }
      .city-weather-mapbox-popup .mapboxgl-popup-tip {
        border-top-color: #ffffff;
      }
      .city-weather-mapbox-popup .mapboxgl-popup-close-button {
        right: 8px;
        top: 8px;
        width: 28px;
        height: 28px;
        font-size: 20px;
        color: #64748b;
        border-radius: 999px;
      }
      .city-weather-popup {
        min-width: 420px;
        max-width: 560px;
        color: #0f172a;
        background: #ffffff;
      }
      .city-weather-popup-header {
        padding: 16px 18px 12px;
        background: linear-gradient(135deg, #eff6ff, #f8fafc);
        border-bottom: 1px solid rgba(148, 163, 184, 0.2);
        cursor: grab;
        touch-action: none;
      }
      .city-weather-mapbox-popup.is-dragging .city-weather-popup-header {
        cursor: grabbing;
      }
      .city-weather-popup-header h3 {
        margin: 0;
        font-size: 19px;
      }
      .city-weather-popup-header-meta {
        margin-top: 6px;
        font-size: 12px;
        color: red;
      }
      .city-weather-popup-section {
        padding: 14px 18px;
      }
      .city-weather-popup-section-title {
        margin: 0 0 10px;
        font-size: 12px;
        font-weight: 700;
        color: #64748b;
        letter-spacing: 0.04em;
      }
      .city-weather-popup-current {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 8px 10px;
        font-size: 13px;
      }
      .city-weather-popup-card {
        padding: 10px 12px;
        border-radius: 12px;
        background: rgba(148, 163, 184, 0.1);
      }
      .city-weather-popup-key {
        color: #475569;
      }
      .city-weather-popup-list {
        display: grid;
        gap: 6px;
      }
      .city-weather-popup-day {
        display: grid;
        grid-template-columns: 72px minmax(92px, 1fr) max-content max-content;
        gap: 8px 10px;
        align-items: center;
        padding: 8px 10px;
        border-radius: 10px;
        background: rgba(148, 163, 184, 0.12);
        font-size: 12px;
      }
      .city-weather-popup-day:hover {
        background: rgba(96, 165, 250, 0.1);
      }
      .city-weather-popup-weather {
        display: flex;
        align-items: center;
        gap: 6px;
        min-width: 0;
        white-space: nowrap;
      }
      .city-weather-popup-weather img {
        width: 16px;
        height: 16px;
        flex: 0 0 16px;
      }
      .city-weather-popup-temp {
        font-weight: 700;
        white-space: nowrap;
      }
      .city-weather-popup-rain {
        color: #475569;
        white-space: nowrap;
      }
      @media (max-width: 640px) {
        .city-weather-popup {
          min-width: min(440px, calc(100vw - 28px));
          max-width: min(440px, calc(100vw - 28px));
        }
        .city-weather-popup-day {
          grid-template-columns: 64px minmax(84px, 1fr) max-content max-content;
          gap: 6px 8px;
          font-size: 11px;
          padding: 7px 8px;
        }
      }
    `;
    document.head.appendChild(style);
  }

  debouncedRefresh() {
    if (this.refreshTimer) clearTimeout(this.refreshTimer);
    this.refreshTimer = setTimeout(() => this.refresh(), 1200 + Math.random() * 500);
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

  getTempColor(temp) {
    if (!Number.isFinite(temp)) return "#e2e8f0";
    if (temp <= 0) return "#2563eb";
    if (temp <= 10) return "#60a5fa";
    if (temp <= 20) return "#facc15";
    if (temp <= 28) return "#fb923c";
    if (temp <= 34) return "#fb7185";
    return "#dc2626";
  }

  getWeatherMeta(code, windSpeed = NaN) {
    const icon = CityWeatherLayer.ICONS;
    if (Number.isFinite(windSpeed) && windSpeed >= 20) {
      return { kind: "wind", label: "大风", icon: icon.wind };
    }
    const n = Number(code);
    if (n === 0) return { kind: "sun", label: "晴", icon: icon.sun };
    if ([1].includes(n)) return { kind: "cloud", label: "晴间多云", icon: icon.cloud };
    if ([2].includes(n)) return { kind: "cloud", label: "多云", icon: icon.cloud };
    if ([3, 45, 48].includes(n)) return { kind: "cloud", label: "阴", icon: icon.cloud };
    if ([51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 80, 81, 82].includes(n)) {
      return { kind: "rain", label: "雨", icon: icon.rain };
    }
    if ([71, 73, 75, 77, 85, 86].includes(n)) return { kind: "snow", label: "雪", icon: icon.snow };
    if ([95, 96, 99].includes(n)) return { kind: "wind", label: "强对流", icon: icon.wind };
    return { kind: "cloud", label: "多云", icon: icon.cloud };
  }

  formatTemp(temp) {
    return Number.isFinite(temp) ? `${temp.toFixed(0)}°C` : "--";
  }

  formatHumidity(humidity) {
    return Number.isFinite(humidity) ? `${humidity.toFixed(0)}%` : "--";
  }

  async fetchBatch(cities) {
    if (!cities.length) return [];
    const query = new URLSearchParams({
      latitude: cities.map(c => c.lat.toFixed(2)).join(","),
      longitude: cities.map(c => c.lon.toFixed(2)).join(","),
      current: "temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m",
      daily: "weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max,precipitation_sum",
      forecast_days: "5",
      timezone: "Asia/Shanghai"
    });

    try {
      const response = await fetchWithTimeout(`${this.api}?${query.toString()}`, {}, 12000);
      const respDate = response.headers.get("last-modified") || response.headers.get("date") || "";
      if (response.status === 429) {
        console.warn("CityWeather: 限制请求频率，跳过本次更新");
        return [];
      }
      if (!response.ok) throw new Error(`城市综合天气请求失败: ${response.status}`);
      const data = await response.json();
      const list = Array.isArray(data) ? data : [data];
      return cities.map((c, i) => {
        const row = list[i] || {};
        const current = row.current || {};
        const daily = row.daily || {};
        const forecast = Array.isArray(daily.time)
          ? daily.time.map((date, idx) => ({
              date,
              weatherCode: Number(daily.weather_code?.[idx]),
              tempMax: Number(daily.temperature_2m_max?.[idx]),
              tempMin: Number(daily.temperature_2m_min?.[idx]),
              precipitationProbability: Number(daily.precipitation_probability_max?.[idx]),
              precipitationSum: Number(daily.precipitation_sum?.[idx])
            }))
          : [];

        return {
          ...c,
          temp: Number(current.temperature_2m),
          humidity: Number(current.relative_humidity_2m),
          weatherCode: Number(current.weather_code),
          windSpeed: Number(current.wind_speed_10m),
          dataTime: current.time || "",
          forecast,
          fetchedAt: respDate ? new Date(respDate).getTime() : Date.now(),
          respDate
        };
      });
    } catch (e) {
      console.error("CityWeather fetchBatch error:", e);
      return [];
    }
  }

  removeAllMarkers() {
    this.markerMap.forEach(marker => marker.remove());
    this.markerMap.clear();
  }

  createPopupContent(city, item) {
    const root = document.createElement("div");
    root.className = "city-weather-popup";
    const meta = this.getWeatherMeta(item.weatherCode, item.windSpeed);
    const updateTime = this.formatDataTime(item) || "--";
    const basic = `
      <div class="city-weather-popup-header">
        <h3>${city.name}</h3>
        <div class="city-weather-popup-header-meta">${meta.label} · 更新时间 ${updateTime}</div>
      </div>
      <div class="city-weather-popup-section">
        <div class="city-weather-popup-section-title">基础信息</div>
        <div class="city-weather-popup-current">
          <div class="city-weather-popup-card"><span class="city-weather-popup-key">温度：</span>${this.formatTemp(item.temp)}</div>
          <div class="city-weather-popup-card"><span class="city-weather-popup-key">湿度：</span>${this.formatHumidity(item.humidity)}</div>
          <div class="city-weather-popup-card"><span class="city-weather-popup-key">风速：</span>${Number.isFinite(item.windSpeed) ? `${item.windSpeed.toFixed(1)} km/h` : "--"}</div>
          <div class="city-weather-popup-card"><span class="city-weather-popup-key">经纬度：</span>${city.lon.toFixed(2)}, ${city.lat.toFixed(2)}</div>
        </div>
      </div>
      <div class="city-weather-popup-section" style="padding-top:0;">
        <div class="city-weather-popup-section-title">未来 5 日预报</div>
        <div class="city-weather-popup-list"></div>
      </div>
    `;
    root.innerHTML = basic;
    const list = root.querySelector(".city-weather-popup-list");
    const forecast = Array.isArray(item.forecast) ? item.forecast : [];
    forecast.forEach(day => {
      const dayMeta = this.getWeatherMeta(day.weatherCode, NaN);
      const row = document.createElement("div");
      row.className = "city-weather-popup-day";
      const dt = new Date(day.date);
      const dateText = Number.isNaN(dt.getTime())
        ? (day.date || "--")
        : `${dt.getMonth() + 1}/${dt.getDate()} ${["周日","周一","周二","周三","周四","周五","周六"][dt.getDay()]}`;
      row.innerHTML = `
        <div>${dateText}</div>
        <div class="city-weather-popup-weather">
          <img src="${dayMeta.icon}" alt="${dayMeta.label}">
          <span>${dayMeta.label}</span>
        </div>
        <div class="city-weather-popup-temp">${this.formatTemp(day.tempMin)} ~ ${this.formatTemp(day.tempMax)}</div>
        <div class="city-weather-popup-rain">降水 ${Number.isFinite(day.precipitationProbability) ? `${day.precipitationProbability.toFixed(0)}%` : "--"}</div>
      `;
      list.appendChild(row);
    });
    return root;
  }

  openPopup(city, item) {
    if (!this.runtime) return;
    const { map } = this.runtime;
    if (this.popup) this.popup.remove();
    this.popup = new mapboxgl.Popup({
      closeButton: true,
      closeOnClick: true,
      maxWidth: "420px",
      className: "city-weather-mapbox-popup"
    })
      .setLngLat([city.lon, city.lat])
      .setDOMContent(this.createPopupContent(city, item))
      .addTo(map);
    this.enablePopupDragging(this.popup);
  }

  enablePopupDragging(popup) {
    const popupEl = popup?.getElement?.();
    if (!popupEl) return;
    const header = popupEl.querySelector(".city-weather-popup-header");
    if (!header || header.dataset.dragBound === "1") return;
    header.dataset.dragBound = "1";
    popup.__dragOffset = popup.__dragOffset || [0, 0];

    let startX = 0;
    let startY = 0;
    let startOffsetX = 0;
    let startOffsetY = 0;
    let dragging = false;

    const onMove = evt => {
      if (!dragging) return;
      const dx = evt.clientX - startX;
      const dy = evt.clientY - startY;
      popup.__dragOffset = [startOffsetX + dx, startOffsetY + dy];
      popup.setOffset(popup.__dragOffset);
    };

    const onUp = () => {
      if (!dragging) return;
      dragging = false;
      popupEl.classList.remove("is-dragging");
      window.removeEventListener("pointermove", onMove);
      window.removeEventListener("pointerup", onUp);
      window.removeEventListener("pointercancel", onUp);
    };

    header.addEventListener("pointerdown", evt => {
      if (evt.target.closest(".mapboxgl-popup-close-button")) return;
      dragging = true;
      startX = evt.clientX;
      startY = evt.clientY;
      startOffsetX = popup.__dragOffset?.[0] || 0;
      startOffsetY = popup.__dragOffset?.[1] || 0;
      popupEl.classList.add("is-dragging");
      window.addEventListener("pointermove", onMove);
      window.addEventListener("pointerup", onUp);
      window.addEventListener("pointercancel", onUp);
      evt.preventDefault();
    });
  }

  buildMarker(city, item) {
    const meta = this.getWeatherMeta(item.weatherCode, item.windSpeed);
    const tempColor = this.getTempColor(item.temp);
    const root = document.createElement("div");
    root.className = "city-weather-marker";
    root.innerHTML = `
      <img class="city-weather-icon" src="${meta.icon}" alt="${meta.label}" />
      <div class="city-weather-text" style="color:${tempColor};">
        <div class="city-weather-top">
          <div class="city-weather-city">${city.name}</div>
          <div class="city-weather-badge">${meta.label}</div>
        </div>
        <div class="city-weather-main">${this.formatTemp(item.temp)}</div>
        <div class="city-weather-sub">湿度 ${this.formatHumidity(item.humidity)}</div>
      </div>
    `;
    root.addEventListener("click", evt => {
      evt.stopPropagation();
      this.openPopup(city, item);
    });
    root.style.opacity = `${this.opacity ?? 1}`;
    return new mapboxgl.Marker({ element: root, anchor: "bottom-left", offset: [0, -6] })
      .setLngLat([city.lon, city.lat])
      .addTo(this.runtime.map);
  }

  rebuildMarkers(cities) {
    this.removeAllMarkers();
    cities.forEach(city => {
      const item = this.cache.get(city.name);
      if (!item) return;
      const marker = this.buildMarker(city, item);
      this.markerMap.set(city.name, marker);
    });
  }

  async refresh(force = false) {
    if (!this.runtime) return false;
    const zoom = this.runtime.map.getZoom();
    const cities = this.visibleCitiesByZoom(zoom);
    const now = Date.now();
    const cacheTtl = 30 * 60 * 1000;
    const citiesToFetch = cities.filter(c => {
      const cached = this.cache.get(c.name);
      return force || !cached || (now - cached.time > cacheTtl);
    });

    if (citiesToFetch.length > 0) {
      const chunks = chunkArray(citiesToFetch, 20);
      for (const chunk of chunks) {
        const part = await this.fetchBatch(chunk);
        for (const item of part) {
          if (Number.isFinite(item.temp) || Number.isFinite(item.humidity)) {
            this.cache.set(item.name, {
              ...item,
              time: now
            });
          }
        }
      }
    }

    this.rebuildMarkers(cities);
    this.setOpacity(this.runtime.getOpacity(this.name));
    const timeText = this.getInfoTime(cities) || new Date(now).toLocaleString("zh-CN", { hour12: false });
    this.setDataTimeText(timeText);
    this.setInfoExtra("");
    this.lastStatus = true;
    this.lastTime = now;
    return true;
  }

  setOpacity(opacity) {
    this.opacity = opacity;
    this.markerMap.forEach(marker => {
      const el = marker.getElement();
      if (el) el.style.opacity = `${opacity}`;
    });
  }

  hide() {
    super.hide();
    this.removeAllMarkers();
    if (this.popup) {
      this.popup.remove();
      this.popup = null;
    }
    this.clearDataTime();
    this.setInfoExtra("");
    return true;
  }

  async show(opacity = 1) {
    const ok = await super.show(opacity);
    this.setOpacity(opacity);
    return ok;
  }
}
