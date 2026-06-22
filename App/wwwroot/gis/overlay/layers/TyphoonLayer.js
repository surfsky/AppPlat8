import { MapLayer } from "../core/MapLayer.js";
import { addOrUpdateGeoJsonSource, fetchWithTimeout, setInfo } from "../core/utils.js";

/****************************************************************
 * 台风路径图层
 ****************************************************************/
export class TyphoonLayer extends MapLayer {
  constructor() {
    super({
      name: "typhoon",
      title: "台风路径",
      api: "/httpapi/typhoon/list",
      refreshSeconds: 1800
    });
    this.sourceId = "typhoon-source";
    this.probFillLayerId = "typhoon-prob-fill-layer";
    this.probLayerId = "typhoon-prob-layer";
    this.historyLayerId = "typhoon-history-layer";
    this.forecastLayerId = "typhoon-forecast-layer";
    this.pointLayerId = "typhoon-point-layer";
    this.pointLabelLayerId = "typhoon-point-label-layer";
    this.tailLabelLayerId = "typhoon-tail-label-layer";
    this.nameLayerId = "typhoon-name-layer";
    this.centerLayerId = "typhoon-center-layer";
    this.legendId = "typhoonLegend";
    this.styleId = "typhoonStyle";
    this.selectId = "typhoonSelect";
    this.yearSelectId = "typhoonYearSelect";
    this.listApi = "/httpapi/typhoon/list";
    this.getApi = "/httpapi/typhoon/get";
    this.logsApi = "/httpapi/typhoon/logs";
    this.predictApi = "/httpapi/typhoon/predict";
    this.currentTrackApi = "https://agora.ex.nii.ac.jp/digital-typhoon/geojson/wnp/";
    this.currentForecastApi = "https://agora.ex.nii.ac.jp/digital-typhoon/json/jmaxml-forecast/wnp/";
    this.historyList = [];
    this.selectedYear = "";
    this.selectedCode = "";
    this.currentStormMap = new Map();
    this.legendEl = null;
    this.popup = null;
    this.eventBound = false;
    this.legendBound = false;
    this.currentStorms = [];
    this.typhoonSvgMarkup = "";
    this.centerIconMap = new Map();
    this.centerAnimId = 0;
    this.centerRotate = 0;
    this.centerLastTick = 0;
    this.windDefs = [
      { min: 0, max: 17.2, name: "热带低压(TD)", color: "#22c55e" },
      { min: 17.2, max: 24.5, name: "热带风暴(TS)", color: "#3b82f6" },
      { min: 24.5, max: 32.7, name: "强热带风暴(STS)", color: "#fde047" },
      { min: 32.7, max: 41.5, name: "台风(TY)", color: "#f59e0b" },
      { min: 41.5, max: 51, name: "强台风(STY)", color: "#a855f7" },
      { min: 51, max: Infinity, name: "超强台风(Super TY)", color: "#ef4444" }
    ];
    this.onPointMove = this.onPointMove.bind(this);
    this.onPointLeave = this.onPointLeave.bind(this);
    this.onPointClick = this.onPointClick.bind(this);
  }

  /**确保样式 */
  ensureStyle() {
    if (document.getElementById(this.styleId)) return;
    const style = document.createElement("style");
    style.id = this.styleId;
    style.textContent = `
      .typhoon-legend {
        position: fixed;
        left: 50%;
        bottom: 18px;
        transform: translateX(-50%);
        z-index: 1002;
        min-width: 560px;
        max-width: min(92vw, 900px);
        padding: 10px 14px;
        border-radius: 12px;
        background: rgba(248,250,252,0.94);
        border: 1px solid rgba(148,163,184,0.35);
        box-shadow: 0 10px 28px rgba(15,23,42,0.25);
        color: #0f172a;
        backdrop-filter: blur(8px);
      }
      .typhoon-legend.is-hidden { display: none; }
      .typhoon-legend-title {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 12px;
        margin-bottom: 8px;
        font-size: 13px;
        font-weight: 700;
      }
      .typhoon-legend-tip {
        font-size: 11px;
        color: #475569;
      }
      .typhoon-legend-row {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: 10px 12px;
        margin-top: 6px;
        font-size: 12px;
      }
      .typhoon-legend-label {
        color: #334155;
        font-weight: 700;
      }
      .typhoon-select {
        height: 28px;
        line-height: 28px;
        font-size: 12px;
        border-radius: 8px;
        border: 1px solid rgba(148,163,184,0.7);
        padding: 0 10px;
        background: rgba(255,255,255,0.92);
        color: #0f172a;
        outline: none;
      }
      .typhoon-legend-chip {
        display: inline-flex;
        align-items: center;
        gap: 6px;
        white-space: nowrap;
      }
      .typhoon-dot {
        width: 10px;
        height: 10px;
        border-radius: 50%;
        border: 1px solid rgba(15,23,42,0.45);
      }
      .typhoon-line {
        width: 20px;
        height: 0;
        border-top: 3px solid currentColor;
      }
      .typhoon-line.dashed { border-top-style: dashed; }
      .mapboxgl-popup.typhoon-popup .mapboxgl-popup-content {
        padding: 0;
        border-radius: 16px;
        overflow: hidden;
        background: rgba(248,250,252,0.98);
        box-shadow: 0 16px 32px rgba(15,23,42,0.3);
        min-width: 240px;
      }
      .typhoon-popup-head {
        background: linear-gradient(135deg, #0ea5e9, #2563eb);
        color: #eff6ff;
        text-align: center;
        padding: 12px 14px;
        font-size: 16px;
        font-weight: 700;
      }
      .typhoon-popup-body {
        padding: 14px 16px 16px;
        font-size: 13px;
        line-height: 1.8;
        color: #0f172a;
      }
      @media (max-width: 1020px) {
        .typhoon-legend {
          min-width: auto;
          left: 12px;
          right: 12px;
          bottom: 12px;
          transform: none;
        }
        .typhoon-select {
          width: 100%;
        }
      }
    `;
    document.head.appendChild(style);
  }

  /**确保图例容器 */
  ensureLegend() {
    this.ensureStyle();
    if (this.legendEl) return this.legendEl;
    let el = document.getElementById(this.legendId);
    if (!el) {
      el = document.createElement("div");
      el.id = this.legendId;
      el.className = "typhoon-legend is-hidden";
      document.body.appendChild(el);
    }
    this.legendEl = el;
    return el;
  }

  /**显示图例 */
  showLegend() {
    const el = this.ensureLegend();
    if (!el) return;
    el.classList.remove("is-hidden");
  }

  /**隐藏图例 */
  hideLegend() {
    const el = this.ensureLegend();
    if (!el) return;
    el.classList.add("is-hidden");
  }

  /**读取 JSON */
  async fetchJson(url) {
    const resp = await fetchWithTimeout(url, {}, 12000);
    if (!resp.ok) throw new Error(`台风接口请求失败: ${resp.status}`);
    return resp.json();
  }

  /**确保图标 */
  async ensureTyphoonSvg() {
    if (this.typhoonSvgMarkup) return;
    try {
      const url = new URL("./typhoon.svg", import.meta.url);
      const resp = await fetchWithTimeout(url.href, {}, 8000);
      if (resp.ok) {
        this.typhoonSvgMarkup = await resp.text();
      }
    } catch (_e) { }
    if (!this.typhoonSvgMarkup) {
      this.typhoonSvgMarkup = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><g class="typhoon-spin"><path fill="rgba(255,255,255,0.95)" fill-rule="evenodd" d="M32 4a28 28 0 1 1 0 56a28 28 0 1 1 0-56zm9 22a18 18 0 1 0 0 36a18 18 0 1 0 0-36z"/></g><circle cx="32" cy="32" r="11.4" fill="var(--ty-color,#ef4444)"/><circle cx="32" cy="32" r="7.2" fill="rgba(255,255,255,0.9)"/><circle cx="32" cy="32" r="3.2" fill="var(--ty-color,#ef4444)"/></svg>';
    }
  }

  /**获取中心图标 ID */
  getCenterIconId(color) {
    const key = String(color || "#ef4444").trim().toLowerCase();
    const safe = key.replace(/[^a-z0-9]+/g, "");
    return `typhoon-center-${safe || "ef4444"}`;
  }

  /**生成中心图标 SVG */
  getCenterSvg(color) {
    const fill = String(color || "#ef4444").trim() || "#ef4444";
    return (this.typhoonSvgMarkup || "")
      .replace(/var\(--ty-color,\s*#ef4444\)/gi, fill)
      .replace(/class="typhoon-spin"/gi, "")
      .replace(/\t/g, " ");
  }

  /**加载图片 */
  loadImage(url) {
    return new Promise((resolve, reject) => {
      const img = new Image();
      img.onload = () => resolve(img);
      img.onerror = reject;
      img.src = url;
    });
  }

  /**确保中心图标 */
  async ensureCenterImage(color) {
    const { map } = this.runtime;
    const iconId = this.getCenterIconId(color);
    if (map.hasImage(iconId)) return iconId;
    const svg = this.getCenterSvg(color);
    const dataUrl = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
    const img = await this.loadImage(dataUrl);
    if (!map.hasImage(iconId)) {
      map.addImage(iconId, img, { pixelRatio: 2 });
    }
    this.centerIconMap.set(iconId, true);
    return iconId;
  }

  /**确保中心图标列表 */
  async ensureCenterImages(storms) {
    const list = Array.isArray(storms) ? storms : [];
    for (const item of list) {
      await this.ensureCenterImage(item?.color);
    }
  }

  /**获取台风编号 */
  getItemCode(item) {
    return String(item?.code || item?.Code || "").trim();
  }

  /**获取中文名 */
  getChineseName(item) {
    return String(item?.chineseName || item?.ChineseName || "").trim();
  }

  /**获取年份 */
  getYear(item) {
    const code = this.getItemCode(item);
    return /^\d{4}/.test(code) ? Number(code.slice(0, 4)) : NaN;
  }

  /**格式化下拉文本 */
  formatOptionLabel(item) {
    const code = this.getItemCode(item);
    const name = this.getChineseName(item) || String(item?.name || item?.Name || "").trim() || "台风";
    const maxLevel = Number(item?.maxLevel || item?.MaxLevel);
    const tail = Number.isFinite(maxLevel) && maxLevel > 0 ? ` ${maxLevel}` : "";
    return `${code} ${name}${tail}`.trim();
  }

  /**获取风力颜色 */
  getWindMeta(windMs) {
    const val = Number(windMs) || 0;
    for (const item of this.windDefs) {
      if (val >= item.min && val < item.max) return item;
    }
    return this.windDefs[0];
  }

  /**构造接口地址 */
  buildUrl(base, params = {}) {
    const url = new URL(base, window.location.origin);
    Object.entries(params).forEach(([k, v]) => {
      if (v === undefined || v === null || v === "") return;
      url.searchParams.set(k, v);
    });
    return url.toString();
  }

  /**确保历史列表 */
  async ensureHistoryList() {
    if (this.historyList.length) return;
    const data = await this.fetchJson(this.listApi);
    this.historyList = Array.isArray(data?.data) ? data.data.slice() : [];
    this.historyList.sort((a, b) => this.getItemCode(b).localeCompare(this.getItemCode(a)));
    if (!this.selectedYear) {
      const first = this.historyList[0];
      this.selectedYear = first ? String(this.getYear(first)) : "";
    }
    if (!this.selectedCode) {
      const first = this.getYearItems(this.selectedYear)[0] || this.historyList[0];
      this.selectedCode = this.getItemCode(first);
    }
  }

  /**获取年份列表 */
  getYears() {
    const set = new Set();
    for (const item of this.historyList) {
      const year = this.getYear(item);
      if (Number.isFinite(year) && year > 1900) set.add(year);
    }
    return Array.from(set.values()).sort((a, b) => b - a);
  }

  /**获取指定年份台风 */
  getYearItems(year) {
    if (!year) return this.historyList;
    return this.historyList.filter(item => String(this.getYear(item)) === String(year));
  }

  /**渲染图例 */
  renderLegend() {
    const el = this.ensureLegend();
    const yearOptions = this.getYears()
      .map(year => `<option value="${year}" ${String(year) === String(this.selectedYear) ? "selected" : ""}>${year}</option>`)
      .join("");
    const items = this.getYearItems(this.selectedYear);
    const itemOptions = items
      .map(item => {
        const code = this.getItemCode(item);
        return `<option value="${code}" ${code === this.selectedCode ? "selected" : ""}>${this.formatOptionLabel(item)}</option>`;
      })
      .join("");
    const windHtml = this.windDefs.map(item => `
      <span class="typhoon-legend-chip">
        <span class="typhoon-dot" style="background:${item.color}"></span>
        <span>${item.name}</span>
      </span>
    `).join("");
    el.innerHTML = `
      <div class="typhoon-legend-title">
        <span>历史台风轨迹</span>
        <span class="typhoon-legend-tip">数据源：本地台风数据库</span>
      </div>
      <div class="typhoon-legend-row">
        <span class="typhoon-legend-label">年份</span>
        <select id="${this.yearSelectId}" class="typhoon-select">${yearOptions}</select>
        <span class="typhoon-legend-label">台风</span>
        <select id="${this.selectId}" class="typhoon-select">${itemOptions}</select>
      </div>
      <div class="typhoon-legend-row">
        <span class="typhoon-legend-label">轨迹</span>
        <span class="typhoon-legend-chip"><span class="typhoon-line" style="color:#fb7185"></span><span>历史路径</span></span>
        <span class="typhoon-legend-chip"><span class="typhoon-line dashed" style="color:#7c3aed"></span><span>当前预报</span></span>
        <span class="typhoon-legend-chip"><span class="typhoon-line dashed" style="color:#a855f7"></span><span>预测圈</span></span>
      </div>
      <div class="typhoon-legend-row">
        <span class="typhoon-legend-label">风力</span>
        ${windHtml}
      </div>
    `;
    this.bindLegend();
  }

  /**绑定图例事件 */
  bindLegend() {
    if (this.legendBound) return;
    const el = this.ensureLegend();
    el.addEventListener("change", async e => {
      const target = e.target;
      if (!target) return;
      if (target.id === this.yearSelectId) {
        this.selectedYear = String(target.value || "");
        const first = this.getYearItems(this.selectedYear)[0];
        this.selectedCode = this.getItemCode(first);
        this.renderLegend();
        await this.refresh(true);
        return;
      }
      if (target.id === this.selectId) {
        this.selectedCode = String(target.value || "");
        await this.refresh(true);
      }
    });
    this.legendBound = true;
  }

  /**获取台风详情 */
  async fetchTyphoon(code) {
    if (!code) return null;
    const data = await this.fetchJson(this.buildUrl(this.getApi, { code }));
    return data?.data || null;
  }

  /**获取轨迹 */
  async fetchLogs(code) {
    if (!code) return [];
    const data = await this.fetchJson(this.buildUrl(this.logsApi, { code }));
    return Array.isArray(data?.data) ? data.data : [];
  }

  /**获取预测 */
  async fetchPredict(code) {
    if (!code) return [];
    const data = await this.fetchJson(this.buildUrl(this.predictApi, { code }));
    return Array.isArray(data?.data) ? data.data : [];
  }

  /**获取实时轨迹地址 */
  getCurrentTrackUrl(code) {
    return `${this.currentTrackApi}${encodeURIComponent(code)}.en.json`;
  }

  /**获取实时预报地址 */
  getCurrentForecastUrl(code) {
    return `${this.currentForecastApi}${encodeURIComponent(code)}/all.json`;
  }

  /**UNIX 秒转 UTC */
  toUtcIsoFromUnix(sec) {
    const n = Number(sec);
    if (!Number.isFinite(n) || n <= 0) return "";
    return new Date(n * 1000).toISOString();
  }

  /**JST 文本转 UTC */
  toUtcIsoFromJstText(text) {
    const m = String(text || "").trim().match(/^(\d{4})-(\d{2})-(\d{2})\s+(\d{2}):(\d{2})\s+JST$/i);
    if (!m) return "";
    const y = Number(m[1]);
    const mm = Number(m[2]) - 1;
    const d = Number(m[3]);
    const hh = Number(m[4]) - 9;
    const mi = Number(m[5]);
    return new Date(Date.UTC(y, mm, d, hh, mi, 0)).toISOString();
  }

  /**节转米每秒 */
  knotToMs(value) {
    const n = Number(value);
    if (!Number.isFinite(n)) return 0;
    return Math.round(n * 0.514444);
  }

  /**格式化时间 */
  formatTime(value) {
    const date = value ? new Date(value) : null;
    if (!date || Number.isNaN(date.getTime())) return "-";
    const y = date.getFullYear();
    const m = `${date.getMonth() + 1}`.padStart(2, "0");
    const d = `${date.getDate()}`.padStart(2, "0");
    const hh = `${date.getHours()}`.padStart(2, "0");
    const mm = `${date.getMinutes()}`.padStart(2, "0");
    return `${y}-${m}-${d} ${hh}:${mm}`;
  }

  /**格式化本地时间 */
  formatLocalTime(value) {
    const txt = this.formatTime(value);
    return txt === "-" ? txt : `${txt}`;
  }

  /**格式化短时间 */
  formatMiniTime(value) {
    const date = value ? new Date(value) : null;
    if (!date || Number.isNaN(date.getTime())) return "--";
    const m = `${date.getMonth() + 1}`.padStart(2, "0");
    const d = `${date.getDate()}`.padStart(2, "0");
    const hh = `${date.getHours()}`.padStart(2, "0");
    return `${m}/${d}-${hh}`;
  }

  /**格式化坐标 */
  formatCoord(coord) {
    if (!Array.isArray(coord) || coord.length < 2) return "-";
    const lng = Number(coord[0]);
    const lat = Number(coord[1]);
    if (!Number.isFinite(lng) || !Number.isFinite(lat)) return "-";
    const ew = lng >= 0 ? "E" : "W";
    const ns = lat >= 0 ? "N" : "S";
    return `${Math.abs(lng).toFixed(1)}°${ew}/${Math.abs(lat).toFixed(1)}°${ns}`;
  }

  /**获取点半径 */
  getPointRadius(ms, isCurrent = false) {
    const wind = Number(ms) || 0;
    let radius = 4;
    if (wind >= 24.5) radius = 4.8;
    if (wind >= 32.7) radius = 5.6;
    if (wind >= 41.5) radius = 6.3;
    if (wind >= 51) radius = 7;
    if (isCurrent) radius += 1.8;
    return Number(radius.toFixed(1));
  }

  /**获取当前台风等级 */
  getCurrentLevelName(cls, windMs) {
    const map = {
      1: "热带低压(TD)",
      2: "热带风暴(TS)",
      3: "强热带风暴(STS)",
      4: "台风(TY)",
      5: "强台风(STY)",
      6: "超强台风(Super TY)"
    };
    return map[Number(cls)] || this.getWindMeta(windMs).name;
  }

  /**解析实时轨迹 */
  parseCurrentTrackGeoJson(data) {
    const features = Array.isArray(data?.features) ? data.features : [];
    return features.map(item => {
      const props = item?.properties || {};
      const coord = Array.isArray(item?.geometry?.coordinates) ? item.geometry.coordinates : null;
      if (!Array.isArray(coord) || coord.length < 2) return null;
      const windMs = this.knotToMs(props.wind);
      const time = this.toUtcIsoFromUnix(props.time);
      return {
        coord: [Number(coord[0]), Number(coord[1])],
        time,
        timeText: this.formatLocalTime(time),
        shortLabel: this.formatMiniTime(time),
        pressure: Number.isFinite(Number(props.pressure)) ? Number(props.pressure) : "-",
        windMs,
        levelCode: Number(props.class) || 0,
        levelName: this.getCurrentLevelName(props.class, windMs),
        source: "Digital Typhoon"
      };
    }).filter(Boolean).sort((a, b) => String(a.time || "").localeCompare(String(b.time || "")));
  }

  /**解析实时预报 */
  parseCurrentForecastJson(list) {
    const rows = Array.isArray(list) ? list.slice() : [];
    if (!rows.length) return { current: null, predicts: [] };
    rows.sort((a, b) => Number(a?.time || 0) - Number(b?.time || 0));
    const last = rows[rows.length - 1] || {};
    const forecast = Array.isArray(last?.forecast) ? last.forecast.slice() : [];
    forecast.sort((a, b) => Number(a?.period || 0) - Number(b?.period || 0));
    const current = forecast.find(x => Number(x?.period || 0) === 0) || forecast[0] || null;
    const predicts = forecast
      .filter(x => Number(x?.period || 0) > 0)
      .map(item => {
        const windMs = Number(item?.speed_ms);
        const time = this.toUtcIsoFromUnix(item?.forecasttime) || this.toUtcIsoFromJstText(item?.time);
        return {
          coord: [Number(item?.long), Number(item?.lat)],
          time,
          timeText: this.formatLocalTime(time),
          shortLabel: `${item?.period || 0}H`,
          pressure: Number.isFinite(Number(item?.pressure)) ? Number(item.pressure) : "-",
          windMs: Number.isFinite(windMs) ? Math.round(windMs) : 0,
          levelName: String(item?.class || "").trim() || this.getWindMeta(windMs).name,
          probKm: Number(item?.radius_km) || 0,
          period: Number(item?.period) || 0,
          agencyId: "jma",
          agencyName: "Digital Typhoon / JMA",
          agencyColor: "#7c3aed"
        };
      })
      .filter(item => Array.isArray(item.coord) && item.coord.every(Number.isFinite));
    return { current, predicts };
  }

  /**读取当前台风数据 */
  async ensureCurrentStormData(code) {
    const key = String(code || "").trim();
    if (!key) return null;
    if (this.currentStormMap.has(key)) return this.currentStormMap.get(key);
    let trackData = null;
    let forecastData = null;
    try {
      trackData = await this.fetchJson(this.getCurrentTrackUrl(key));
    } catch (_e) { }
    try {
      forecastData = await this.fetchJson(this.getCurrentForecastUrl(key));
    } catch (_e) { }
    const data = {
      track: this.parseCurrentTrackGeoJson(trackData),
      forecast: this.parseCurrentForecastJson(forecastData)
    };
    this.currentStormMap.set(key, data);
    return data;
  }

  /**是否结束 */
  isEndedTyphoon(typhoon) {
    const deathUtc = typhoon?.deathUtc || typhoon?.DeathUtc;
    if (deathUtc) {
      const ts = new Date(deathUtc).getTime();
      if (Number.isFinite(ts)) return ts < Date.now() - 6 * 3600 * 1000;
    }
    const year = this.getYear(typhoon);
    return Number.isFinite(year) && year < new Date().getUTCFullYear();
  }

  /**是否用实时数据 */
  shouldUseLiveData(typhoon, logs) {
    const year = this.getYear(typhoon);
    const curYear = new Date().getUTCFullYear();
    if (Number.isFinite(year) && year >= curYear) return true;
    if (Array.isArray(logs) && logs.length) return false;
    const deathUtc = typhoon?.deathUtc || typhoon?.DeathUtc;
    if (!deathUtc) return false;
    const ts = new Date(deathUtc).getTime();
    return Number.isFinite(ts) && ts >= Date.now() - 6 * 3600 * 1000;
  }

  /**构建圆环 */
  buildCircle(lng, lat, radiusM, steps = 72) {
    if (!Number.isFinite(lng) || !Number.isFinite(lat) || !Number.isFinite(radiusM) || radiusM <= 0) return null;
    const km = radiusM / 1000;
    const latDeg = km / 111.32;
    const lonScale = Math.max(0.15, Math.cos(lat * Math.PI / 180));
    const lonDeg = km / (111.32 * lonScale);
    const ring = [];
    for (let i = 0; i <= steps; i++) {
      const rad = Math.PI * 2 * i / steps;
      ring.push([
        lng + lonDeg * Math.cos(rad),
        lat + latDeg * Math.sin(rad)
      ]);
    }
    return ring;
  }

  /**构建圆要素 */
  buildCircleFeature(kind, center, radiusM, props) {
    const ring = this.buildCircle(center[0], center[1], radiusM);
    if (!ring) return null;
    return {
      type: "Feature",
      geometry: { type: "Polygon", coordinates: [ring] },
      properties: { kind, ...props }
    };
  }

  /**构建路径要素 */
  buildPathFeature(kind, coords, props) {
    if (!Array.isArray(coords) || coords.length < 2) return null;
    return {
      type: "Feature",
      geometry: { type: "LineString", coordinates: coords },
      properties: { kind, ...props }
    };
  }

  /**构建概率圈 */
  buildProbCircleFeature(item, agencyColor) {
    if (!item?.probKm || item.probKm <= 0) return null;
    return this.buildCircleFeature("prob-circle", item.coord, item.probKm * 1000, {
      agencyColor: agencyColor || item.agencyColor || "#7c3aed"
    });
  }

  /**构建中心图标要素 */
  buildCenterFeature(storm) {
    const center = Array.isArray(storm?.center) ? storm.center : null;
    if (!center || center.length < 2 || !center.every(Number.isFinite)) return null;
    const color = String(storm?.color || "#ef4444").trim() || "#ef4444";
    return {
      type: "Feature",
      geometry: { type: "Point", coordinates: center },
      properties: {
        kind: "storm-center",
        stormId: storm?.id || "",
        iconImage: this.getCenterIconId(color),
        iconSize: 1.45
      }
    };
  }

  /**规范化预测数据 */
  normalizePredicts(list, lastPoint) {
    const items = Array.isArray(list) ? list : [];
    if (!items.length || !lastPoint) return [];
    return items
      .map(item => {
        const tracks = Array.isArray(item?.items) ? item.items : [];
        const coords = [[Number(lastPoint.lng), Number(lastPoint.lat)]];
        tracks.forEach(track => {
          const lng = Number(track?.lng || track?.Lng);
          const lat = Number(track?.lat || track?.Lat);
          if (Number.isFinite(lng) && Number.isFinite(lat)) coords.push([lng, lat]);
        });
        if (coords.length <= 1) return null;
        const tail = coords[coords.length - 1];
        return {
          agencyId: String(item?.agencyId || item?.AgencyId || "predict"),
          agencyName: String(item?.agencyName || item?.AgencyName || "预测"),
          agencyColor: String(item?.agencyColor || item?.AgencyColor || "#2563eb"),
          coords,
          tail
        };
      })
      .filter(Boolean);
  }

  /**构建轨迹要素 */
  buildFeatures(typhoon, logs, predicts) {
    const features = [];
    const points = Array.isArray(logs) ? logs : [];
    if (!points.length) return { features, summary: null, currentStorms: [] };

    const code = this.getItemCode(typhoon);
    const name = this.getChineseName(typhoon) || String(typhoon?.name || typhoon?.Name || "").trim() || "台风";
    const stormId = code || "typhoon";
    const coords = [];
    points.forEach((item, i) => {
      const lng = Number(item?.lng || item?.Lng);
      const lat = Number(item?.lat || item?.Lat);
      if (!Number.isFinite(lng) || !Number.isFinite(lat)) return;
      coords.push([lng, lat]);
      const windMs = Number(item?.windMs || item?.WindMs) || 0;
      const windMeta = this.getWindMeta(windMs);
      const timeText = this.formatLocalTime(item?.timeUtc || item?.TimeUtc);
      const pressure = item?.pressure ?? item?.Pressure ?? "-";
      const levelName = item?.levelName || item?.LevelName || windMeta.name;
      const label = timeText ? timeText.slice(5, 16).replace(" ", "-") : "";
      const popupTitle = `${code} ${name}`.trim();
      features.push({
        type: "Feature",
        geometry: { type: "Point", coordinates: [lng, lat] },
        properties: {
          kind: "track-point",
          stormId,
          code,
          name,
          windMs,
          windColor: windMeta.color,
          pointRadius: i === points.length - 1 ? 5 : 3.8,
          isCurrent: i === points.length - 1 ? 1 : 0,
          popupTitle,
          popupTime: timeText || "-",
          popupCoord: `${lng.toFixed(1)}, ${lat.toFixed(1)}`,
          popupPressure: pressure,
          popupWind: windMs,
          popupLevel: levelName,
          popupType: i === points.length - 1 ? "当前位置" : "历史点"
        }
      });
      if (label) {
        features.push({
          type: "Feature",
          geometry: { type: "Point", coordinates: [lng, lat] },
          properties: {
            kind: "track-label",
            label,
            labelColor: windMeta.color
          }
        });
      }
    });

    if (coords.length > 1) {
      features.push({
        type: "Feature",
        geometry: { type: "LineString", coordinates: coords },
        properties: {
          kind: "history-path",
          color: "#fb7185"
        }
      });
    }

    const last = points[points.length - 1];
    const lastLng = Number(last?.lng || last?.Lng);
    const lastLat = Number(last?.lat || last?.Lat);
    if (Number.isFinite(lastLng) && Number.isFinite(lastLat)) {
      features.push({
        type: "Feature",
        geometry: { type: "Point", coordinates: [lastLng, lastLat] },
        properties: {
          kind: "name-label",
          label: `${code} ${name}`.trim()
        }
      });
    }

    const forecastList = this.normalizePredicts(predicts, { lng: lastLng, lat: lastLat });
    forecastList.forEach(item => {
      features.push({
        type: "Feature",
        geometry: { type: "LineString", coordinates: item.coords },
        properties: {
          kind: "forecast-path",
          agencyId: item.agencyId,
          agencyName: item.agencyName,
          agencyColor: item.agencyColor
        }
      });
      features.push({
        type: "Feature",
        geometry: { type: "Point", coordinates: item.tail },
        properties: {
          kind: "tail-label",
          label: item.agencyName,
          labelColor: item.agencyColor
        }
      });
    });

    const startText = String(points[0]?.timeUtc || points[0]?.TimeUtc || "").replace("T", " ").replace("Z", "").slice(0, 16);
    const endText = String(last?.timeUtc || last?.TimeUtc || "").replace("T", " ").replace("Z", "").slice(0, 16);
    const maxWind = Math.max(...points.map(item => Number(item?.windMs || item?.WindMs) || 0));
    const summary = {
      code,
      name,
      pointCnt: points.length,
      maxWind,
      startText,
      endText
    };
    const currentStorms = Number.isFinite(lastLng) && Number.isFinite(lastLat)
      ? [{ id: stormId, center: [lastLng, lastLat], color: this.getWindMeta(maxWind).color }]
      : [];
    currentStorms.forEach(item => {
      const feature = this.buildCenterFeature(item);
      if (feature) features.push(feature);
    });
    return { features, summary, currentStorms };
  }

  /**构建当前台风要素 */
  async buildCurrentFeatures(typhoon, code) {
    const data = await this.ensureCurrentStormData(code);
    const track = Array.isArray(data?.track) ? data.track.slice() : [];
    const predicts = Array.isArray(data?.forecast?.predicts) ? data.forecast.predicts.slice() : [];
    const currentForecast = data?.forecast?.current || null;
    if (!track.length && currentForecast) {
      const windMs = Number(currentForecast?.speed_ms);
      track.push({
        coord: [Number(currentForecast?.long), Number(currentForecast?.lat)],
        time: this.toUtcIsoFromUnix(currentForecast?.forecasttime) || this.toUtcIsoFromJstText(currentForecast?.time),
        timeText: this.formatLocalTime(this.toUtcIsoFromUnix(currentForecast?.forecasttime) || this.toUtcIsoFromJstText(currentForecast?.time)),
        shortLabel: "实况",
        pressure: Number.isFinite(Number(currentForecast?.pressure)) ? Number(currentForecast?.pressure) : "-",
        windMs: Number.isFinite(windMs) ? Math.round(windMs) : 0,
        levelName: String(currentForecast?.class || "").trim() || this.getWindMeta(windMs).name
      });
    }
    if (!track.length) return { features: [], summary: null, currentStorms: [] };

    const features = [];
    const name = this.getChineseName(typhoon) || String(typhoon?.name || typhoon?.Name || "").trim() || "台风";
    const stormId = `current-${code}`;
    const actualCoords = [];
    track.forEach((item, i) => {
      const coord = Array.isArray(item?.coord) ? item.coord : null;
      if (!coord || coord.length < 2 || !coord.every(Number.isFinite)) return;
      actualCoords.push(coord);
      const windMeta = this.getWindMeta(item.windMs);
      features.push({
        type: "Feature",
        geometry: { type: "Point", coordinates: coord },
        properties: {
          kind: "track-point",
          stormId,
          code,
          name,
          windMs: Number(item.windMs) || 0,
          windColor: windMeta.color,
          pointRadius: this.getPointRadius(item.windMs, i === track.length - 1),
          isCurrent: i === track.length - 1 ? 1 : 0,
          popupTitle: `${code} ${name}`.trim(),
          popupTime: item.timeText || this.formatLocalTime(item.time),
          popupCoord: this.formatCoord(coord),
          popupPressure: item.pressure ?? "-",
          popupWind: Number(item.windMs) || 0,
          popupLevel: item.levelName || windMeta.name,
          popupType: i === track.length - 1 ? "当前台风实况点" : "当前台风轨迹点"
        }
      });
      if (item.shortLabel) {
        features.push({
          type: "Feature",
          geometry: { type: "Point", coordinates: coord },
          properties: {
            kind: "track-label",
            label: item.shortLabel,
            labelColor: "#e2e8f0"
          }
        });
      }
    });

    const historyLine = this.buildPathFeature("history-path", actualCoords, { color: "#fb7185" });
    if (historyLine) features.push(historyLine);

    const currentItem = track[track.length - 1];
    const currentCenter = Array.isArray(currentItem?.coord) ? currentItem.coord : null;
    const agencyColor = "#7c3aed";
    if (currentCenter) {
      const pathCoords = [currentCenter, ...predicts.map(x => x.coord).filter(x => Array.isArray(x) && x.length >= 2)];
      const line = this.buildPathFeature("forecast-path", pathCoords, {
        agencyId: "jma",
        agencyName: "Digital Typhoon / JMA",
        agencyColor
      });
      if (line) features.push(line);
      predicts.forEach(item => {
        const coord = Array.isArray(item?.coord) ? item.coord : null;
        if (!coord || coord.length < 2 || !coord.every(Number.isFinite)) return;
        const windMeta = this.getWindMeta(item.windMs);
        features.push({
          type: "Feature",
          geometry: { type: "Point", coordinates: coord },
          properties: {
            kind: "track-point",
            stormId,
            code,
            name,
            windMs: Number(item.windMs) || 0,
            windColor: windMeta.color,
            pointRadius: this.getPointRadius(item.windMs, false),
            isCurrent: 0,
            popupTitle: `${code} ${name}`.trim(),
            popupTime: item.timeText || this.formatLocalTime(item.time),
            popupCoord: this.formatCoord(coord),
            popupPressure: item.pressure ?? "-",
            popupWind: Number(item.windMs) || 0,
            popupLevel: item.levelName || windMeta.name,
            popupType: `${item.period || 0}小时预报点`
          }
        });
        if (item.shortLabel) {
          features.push({
            type: "Feature",
            geometry: { type: "Point", coordinates: coord },
            properties: {
              kind: "track-label",
              label: item.shortLabel,
              labelColor: agencyColor
            }
          });
        }
        const circle = this.buildProbCircleFeature(item, agencyColor);
        if (circle) features.push(circle);
      });
      const tail = predicts[predicts.length - 1];
      if (tail?.coord) {
        features.push({
          type: "Feature",
          geometry: { type: "Point", coordinates: tail.coord },
          properties: {
            kind: "tail-label",
            label: "Digital Typhoon / JMA",
            labelColor: agencyColor
          }
        });
      }
      features.push({
        type: "Feature",
        geometry: { type: "Point", coordinates: currentCenter },
        properties: {
          kind: "name-label",
          label: `${code} ${name}`.trim()
        }
      });
    }

    const maxWind = Math.max(...track.map(item => Number(item?.windMs) || 0));
    const summary = {
      mode: "current",
      code,
      name,
      pointCnt: track.length,
      predictCnt: predicts.length,
      maxWind,
      startText: track[0]?.timeText || this.formatLocalTime(track[0]?.time),
      endText: currentItem?.timeText || this.formatLocalTime(currentItem?.time)
    };
    const currentStorms = currentCenter
      ? [{ id: stormId, center: currentCenter, color: this.getWindMeta(maxWind).color }]
      : [];
    currentStorms.forEach(item => {
      const feature = this.buildCenterFeature(item);
      if (feature) features.push(feature);
    });
    return { features, summary, currentStorms };
  }

  /**生成图层 ID 列表 */
  getLayerIds() {
    return [
      this.probFillLayerId,
      this.probLayerId,
      this.historyLayerId,
      this.forecastLayerId,
      this.pointLayerId,
      this.centerLayerId,
      this.pointLabelLayerId,
      this.tailLabelLayerId,
      this.nameLayerId
    ];
  }

  /**确保图层 */
  ensureLayers() {
    const { map } = this.runtime;
    if (!map.getLayer(this.probFillLayerId)) {
      map.addLayer({
        id: this.probFillLayerId,
        type: "fill",
        source: this.sourceId,
        filter: ["==", ["get", "kind"], "prob-circle"],
        paint: {
          "fill-color": ["coalesce", ["get", "agencyColor"], "#7c3aed"],
          "fill-opacity": 0.08
        }
      });
    }
    if (!map.getLayer(this.probLayerId)) {
      map.addLayer({
        id: this.probLayerId,
        type: "line",
        source: this.sourceId,
        filter: ["==", ["get", "kind"], "prob-circle"],
        paint: {
          "line-color": ["coalesce", ["get", "agencyColor"], "#7c3aed"],
          "line-width": 1.6,
          "line-dasharray": [2, 2],
          "line-opacity": 0.86
        }
      });
    }
    if (!map.getLayer(this.historyLayerId)) {
      map.addLayer({
        id: this.historyLayerId,
        type: "line",
        source: this.sourceId,
        filter: ["==", ["get", "kind"], "history-path"],
        paint: {
          "line-color": "#fb7185",
          "line-width": 2.4,
          "line-opacity": 0.96
        }
      });
    }
    if (!map.getLayer(this.forecastLayerId)) {
      map.addLayer({
        id: this.forecastLayerId,
        type: "line",
        source: this.sourceId,
        filter: ["==", ["get", "kind"], "forecast-path"],
        paint: {
          "line-color": ["get", "agencyColor"],
          "line-width": 2.4,
          "line-dasharray": [2, 2],
          "line-opacity": 0.9
        }
      });
    }
    if (!map.getLayer(this.pointLayerId)) {
      map.addLayer({
        id: this.pointLayerId,
        type: "circle",
        source: this.sourceId,
        filter: ["==", ["get", "kind"], "track-point"],
        paint: {
          "circle-color": ["get", "windColor"],
          "circle-radius": ["coalesce", ["get", "pointRadius"], 4],
          "circle-stroke-color": "#ffffff",
          "circle-stroke-width": ["case", ["==", ["get", "isCurrent"], 1], 2.2, 1]
        }
      });
    }
    if (!map.getLayer(this.centerLayerId)) {
      map.addLayer({
        id: this.centerLayerId,
        type: "symbol",
        source: this.sourceId,
        filter: ["==", ["get", "kind"], "storm-center"],
        layout: {
          "icon-image": ["get", "iconImage"],
          "icon-size": ["coalesce", ["get", "iconSize"], 1.45],
          "icon-anchor": "center",
          "icon-allow-overlap": true,
          "icon-ignore-placement": true,
          "icon-pitch-alignment": "viewport",
          "icon-rotation-alignment": "viewport"
        },
        paint: {
          "icon-opacity": 1
        }
      });
    }
    if (!map.getLayer(this.pointLabelLayerId)) {
      map.addLayer({
        id: this.pointLabelLayerId,
        type: "symbol",
        source: this.sourceId,
        minzoom: 4.4,
        filter: ["==", ["get", "kind"], "track-label"],
        layout: {
          "text-field": ["get", "label"],
          "text-size": 9,
          "text-offset": [0.7, -0.8],
          "text-anchor": "left",
          "text-allow-overlap": true
        },
        paint: {
          "text-color": ["get", "labelColor"],
          "text-halo-color": "rgba(255,255,255,0.95)",
          "text-halo-width": 1.1
        }
      });
    }
    if (!map.getLayer(this.tailLabelLayerId)) {
      map.addLayer({
        id: this.tailLabelLayerId,
        type: "symbol",
        source: this.sourceId,
        filter: ["==", ["get", "kind"], "tail-label"],
        layout: {
          "text-field": ["get", "label"],
          "text-size": 11,
          "text-offset": [1, 0],
          "text-anchor": "left",
          "text-allow-overlap": true
        },
        paint: {
          "text-color": ["get", "labelColor"],
          "text-halo-color": "rgba(255,255,255,0.98)",
          "text-halo-width": 1.3
        }
      });
    }
    if (!map.getLayer(this.nameLayerId)) {
      map.addLayer({
        id: this.nameLayerId,
        type: "symbol",
        source: this.sourceId,
        filter: ["==", ["get", "kind"], "name-label"],
        layout: {
          "text-field": ["get", "label"],
          "text-size": 12,
          "text-offset": [1.1, -1],
          "text-anchor": "left",
          "text-allow-overlap": true
        },
        paint: {
          "text-color": "#ffffff",
          "text-halo-color": "rgba(15,23,42,0.96)",
          "text-halo-width": 1.4
        }
      });
    }
    if (map.getLayer(this.centerLayerId)) {
      map.moveLayer(this.centerLayerId);
    }
  }

  /**生成弹窗 HTML */
  getPopupHtml(props) {
    return `
      <div class="typhoon-popup-card">
        <div class="typhoon-popup-head">${props.popupTitle || "台风点位"}</div>
        <div class="typhoon-popup-body">
          <div><b>时间：</b>${props.popupTime || "-"}</div>
          <div><b>坐标：</b>${props.popupCoord || "-"}</div>
          <div><b>中心气压：</b>${props.popupPressure || "-"} hPa</div>
          <div><b>最大风速：</b>${props.popupWind || "-"} m/s</div>
          <div><b>等级：</b>${props.popupLevel || "-"}</div>
          <div><b>类型：</b>${props.popupType || "-"}</div>
        </div>
      </div>
    `;
  }

  /**显示弹窗 */
  showPopup(feature, lngLat) {
    const { map } = this.runtime;
    if (!feature?.properties) return;
    if (!this.popup) {
      this.popup = new mapboxgl.Popup({
        closeButton: false,
        closeOnClick: false,
        className: "typhoon-popup",
        maxWidth: "320px"
      });
    }
    this.popup.setLngLat(lngLat || feature.geometry?.coordinates || map.getCenter())
      .setHTML(this.getPopupHtml(feature.properties))
      .addTo(map);
  }

  /**隐藏弹窗 */
  hidePopup() {
    if (this.popup) this.popup.remove();
  }

  /**鼠标移动 */
  onPointMove(e) {
    const feature = e?.features?.[0];
    if (!feature) return;
    this.showPopup(feature, e.lngLat);
  }

  /**鼠标移出 */
  onPointLeave() {
    const { map } = this.runtime;
    map.getCanvas().style.cursor = "";
    this.hidePopup();
  }

  /**点击点位 */
  onPointClick(e) {
    const feature = e?.features?.[0];
    if (!feature) return;
    this.showPopup(feature, e.lngLat);
  }

  /**确保交互 */
  ensureInteractions() {
    if (this.eventBound) return;
    const { map } = this.runtime;
    map.on("mouseenter", this.pointLayerId, () => {
      map.getCanvas().style.cursor = "pointer";
    });
    map.on("mousemove", this.pointLayerId, this.onPointMove);
    map.on("mouseleave", this.pointLayerId, this.onPointLeave);
    map.on("click", this.pointLayerId, this.onPointClick);
    this.eventBound = true;
  }

  /**同步中心标记 */
  syncCenterMarkers(storms) {
    this.currentStorms = Array.isArray(storms) ? storms.slice() : [];
    this.syncCenterAnimation();
  }

  /**设置标记可见 */
  setMarkerVisible(visible) {
    const { map } = this.runtime;
    if (map.getLayer(this.centerLayerId)) {
      map.setLayoutProperty(this.centerLayerId, "visibility", visible ? "visible" : "none");
    }
    this.syncCenterAnimation();
  }

  /**同步中心动画 */
  syncCenterAnimation() {
    const visible = !!this.visible && this.currentStorms.length > 0;
    if (!visible) {
      this.stopCenterAnimation();
      return;
    }
    this.startCenterAnimation();
  }

  /**启动中心动画 */
  startCenterAnimation() {
    if (this.centerAnimId) return;
    this.centerLastTick = 0;
    const step = (ts) => {
      const { map } = this.runtime || {};
      if (!map || !this.visible || !this.currentStorms.length || !map.getLayer(this.centerLayerId)) {
        this.stopCenterAnimation();
        return;
      }
      if (!this.centerLastTick) this.centerLastTick = ts;
      const gap = ts - this.centerLastTick;
      this.centerLastTick = ts;
      this.centerRotate = (this.centerRotate + gap * 0.12) % 360;
      map.setLayoutProperty(this.centerLayerId, "icon-rotate", this.centerRotate);
      this.centerAnimId = requestAnimationFrame(step);
    };
    this.centerAnimId = requestAnimationFrame(step);
  }

  /**停止中心动画 */
  stopCenterAnimation() {
    if (this.centerAnimId) {
      cancelAnimationFrame(this.centerAnimId);
      this.centerAnimId = 0;
    }
  }

  /**刷新图层 */
  async refresh() {
    await this.ensureTyphoonSvg();
    await this.ensureHistoryList();
    if (!this.selectedCode) {
      const first = this.getYearItems(this.selectedYear)[0] || this.historyList[0];
      this.selectedCode = this.getItemCode(first);
    }
    this.renderLegend();
    this.showLegend();

    if (!this.selectedCode) {
      addOrUpdateGeoJsonSource(this.runtime.map, this.sourceId, { type: "FeatureCollection", features: [] });
      setInfo("typhoonInfo", "暂无台风数据");
      return true;
    }

    const [typhoon, logs, predicts] = await Promise.all([
      this.fetchTyphoon(this.selectedCode).catch(() => null),
      this.fetchLogs(this.selectedCode).catch(() => []),
      this.fetchPredict(this.selectedCode).catch(() => [])
    ]);
    let result = this.buildFeatures(typhoon, logs, predicts);
    if (this.shouldUseLiveData(typhoon, logs)) {
      const liveResult = await this.buildCurrentFeatures(typhoon, this.selectedCode);
      if (liveResult?.features?.length) result = liveResult;
    }
    const { features, summary, currentStorms } = result;
    await this.ensureCenterImages(currentStorms);
    addOrUpdateGeoJsonSource(this.runtime.map, this.sourceId, { type: "FeatureCollection", features });
    this.ensureLayers();
    this.ensureInteractions();
    this.syncCenterMarkers(currentStorms);

    if (!summary) {
      setInfo("typhoonInfo", `台风：${this.selectedCode}，暂无轨迹数据`);
    } else {
      if (summary.mode === "current") {
        setInfo("typhoonInfo", `当前台风：${summary.code} ${summary.name}，实况点 ${summary.pointCnt} 个，预测点 ${summary.predictCnt || 0} 个，最高风速 ${summary.maxWind}m/s，起止 ${summary.startText} ~ ${summary.endText}`);
      } else {
        setInfo("typhoonInfo", `历史台风：${summary.code} ${summary.name}，轨迹点 ${summary.pointCnt} 个，最高风速 ${summary.maxWind}m/s，起止 ${summary.startText} ~ ${summary.endText}`);
      }
    }
    this.setOpacity(this.runtime.getOpacity(this.name));
    this.lastStatus = true;
    this.lastTime = Date.now();
    return true;
  }

  /**设置透明度 */
  setOpacity(opacity) {
    const { map } = this.runtime;
    const lineIds = [this.probLayerId, this.historyLayerId, this.forecastLayerId];
    const fillIds = [this.probFillLayerId];
    const circleIds = [this.pointLayerId];
    const textIds = [this.pointLabelLayerId, this.tailLabelLayerId, this.nameLayerId];
    const iconIds = [this.centerLayerId];
    lineIds.forEach(id => {
      if (map.getLayer(id)) map.setPaintProperty(id, "line-opacity", opacity);
    });
    fillIds.forEach(id => {
      if (map.getLayer(id)) map.setPaintProperty(id, "fill-opacity", opacity * 0.08);
    });
    circleIds.forEach(id => {
      if (map.getLayer(id)) map.setPaintProperty(id, "circle-opacity", opacity);
    });
    textIds.forEach(id => {
      if (map.getLayer(id)) map.setPaintProperty(id, "text-opacity", opacity);
    });
    iconIds.forEach(id => {
      if (map.getLayer(id)) map.setPaintProperty(id, "icon-opacity", opacity);
    });
  }

  /**隐藏图层 */
  hide() {
    super.hide();
    const { map } = this.runtime;
    this.getLayerIds().forEach(id => {
      if (map.getLayer(id)) map.setLayoutProperty(id, "visibility", "none");
    });
    this.setMarkerVisible(false);
    this.stopCenterAnimation();
    this.hideLegend();
    this.hidePopup();
    setInfo("typhoonInfo", "未开启");
    return true;
  }

  /**显示图层 */
  async show(opacity = 1) {
    const ok = await super.show(opacity);
    const { map } = this.runtime;
    this.getLayerIds().forEach(id => {
      if (map.getLayer(id)) map.setLayoutProperty(id, "visibility", "visible");
    });
    this.setMarkerVisible(true);
    this.showLegend();
    this.syncCenterAnimation();
    return ok;
  }
}
