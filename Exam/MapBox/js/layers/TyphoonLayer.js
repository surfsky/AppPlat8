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
      api: "https://www.jma.go.jp/bosai/typhoon/data/targetTc.json",
      refreshSeconds: 1800
    });
    this.sourceId = "typhoon-source";
    this.wind7FillLayerId = "typhoon-wind7-fill-layer";
    this.wind7LineLayerId = "typhoon-wind7-line-layer";
    this.wind10FillLayerId = "typhoon-wind10-fill-layer";
    this.wind10LineLayerId = "typhoon-wind10-line-layer";
    this.probFillLayerId = "typhoon-prob-fill-layer";
    this.probLayerId = "typhoon-prob-layer";
    this.historyLayerId = "typhoon-history-layer";
    this.pointLayerId = "typhoon-point-layer";
    this.pointLabelLayerId = "typhoon-point-label-layer";
    this.agencyTailLayerId = "typhoon-agency-tail-layer";
    this.nameLayerId = "typhoon-name-layer";
    this.legendId = "typhoonLegend";
    this.styleId = "typhoon-layer-style";
    this.selectId = "typhoonSelect";
    this.yearSelectId = "typhoonYearSelect";
    this.historyApi = new URL("./typhoons.json", import.meta.url).href;
    this.dataDir = new URL("../../typhoon/typhoon-org-cn/", import.meta.url).href;
    this.currentTrackApi = "https://agora.ex.nii.ac.jp/digital-typhoon/geojson/wnp/";
    this.currentForecastApi = "https://agora.ex.nii.ac.jp/digital-typhoon/json/jmaxml-forecast/wnp/";
    this.legendEl = null;
    this.popup = null;
    this.eventBound = false;
    this.centerMarkers = new Map();
    this.currentStorms = [];
    this.typhoonSvgMarkup = "";
    this.historyList = [];
    this.yearStormMap = new Map();
    this.currentStormMap = new Map();
    this.selectedKey = "";
    this.selectedYear = "";
    this.legendBound = false;
    this.enableDemo = false;
    this.cmaLevelMap = {
      0: "低压/减弱",
      1: "热带低压",
      2: "热带风暴",
      3: "强热带风暴",
      4: "台风",
      5: "强台风",
      6: "超强台风",
      9: "变性温带气旋"
    };
    this.agencyDefs = [
      { id: "cma", name: "中国", short: "中", color: "#2563eb", dash: [1.2, 0.8], biasLon: -0.32, biasLat: 0.08, blend: 0.22, demo: true },
      { id: "hko", name: "中国香港", short: "港", color: "#ec4899", dash: [1.2, 1.2], biasLon: -0.18, biasLat: 0.14, blend: 0.16, demo: true },
      { id: "cwa", name: "中国台湾", short: "台", color: "#f97316", dash: [1.8, 1.2], biasLon: -0.1, biasLat: -0.18, blend: 0.18, demo: true },
      { id: "kma", name: "韩国", short: "韩", color: "#1d4ed8", dash: [2.2, 1.2], biasLon: 0.14, biasLat: 0.18, blend: 0.14, demo: true },
      { id: "jma", name: "日本", short: "日", color: "#7c3aed", dash: [3, 2], biasLon: 0, biasLat: 0, blend: 0, demo: false },
      { id: "pagasa", name: "菲律宾", short: "菲", color: "#0ea5e9", dash: [1, 1.5], biasLon: -0.22, biasLat: -0.12, blend: 0.12, demo: true },
      { id: "jtwc", name: "美国", short: "美", color: "#ef4444", dash: [3, 1.4], biasLon: 0.38, biasLat: 0.08, blend: 0.25, demo: true },
      { id: "ecmwf", name: "欧洲", short: "欧", color: "#475569", dash: [0.8, 1.6], biasLon: 0.2, biasLat: -0.1, blend: 0.2, demo: true }
    ];
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

  ensureStyle() {
    const existed = document.getElementById(this.styleId);
    if (existed) return;
    const style = document.createElement("style");
    style.id = this.styleId;
    style.textContent = `
      .typhoon-legend {
        position: fixed;
        left: 50%;
        bottom: 18px;
        transform: translateX(-50%);
        z-index: 1001;
        min-width: 560px;
        max-width: min(92vw, 980px);
        padding: 10px 14px;
        border-radius: 12px;
        background: rgba(248, 250, 252, 0.94);
        border: 1px solid rgba(148, 163, 184, 0.4);
        box-shadow: 0 10px 28px rgba(15, 23, 42, 0.22);
        color: #0f172a;
        -webkit-backdrop-filter: blur(8px);
        backdrop-filter: blur(8px);
        pointer-events: auto;
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
        font-weight: 500;
      }

      .typhoon-legend-row {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: 10px 12px;
        font-size: 12px;
        margin-top: 6px;
      }

      .typhoon-legend-label {
        color: #334155;
        font-weight: 700;
        margin-right: 2px;
      }

      .typhoon-legend-chip {
        display: inline-flex;
        align-items: center;
        gap: 6px;
        white-space: nowrap;
      }

      .typhoon-legend-chip.is-disabled { opacity: 0.45; }

      .typhoon-select {
        height: 28px;
        line-height: 28px;
        font-size: 12px;
        border-radius: 8px;
        border: 1px solid rgba(148, 163, 184, 0.7);
        padding: 0 10px;
        background: rgba(255, 255, 255, 0.92);
        color: #0f172a;
        outline: none;
        min-width: min(66vw, 420px);
      }

      #typhoonYearSelect.typhoon-select {
        min-width: 132px;
      }

      .typhoon-select:focus {
        border-color: rgba(56, 189, 248, 0.9);
        box-shadow: 0 0 0 3px rgba(56, 189, 248, 0.25);
      }

      .typhoon-dot {
        width: 10px;
        height: 10px;
        border-radius: 50%;
        border: 1px solid rgba(15, 23, 42, 0.45);
      }

      .typhoon-line {
        width: 20px;
        height: 0;
        border-top: 3px solid currentColor;
      }

      .typhoon-line.dashed { border-top-style: dashed; }

      .typhoon-center-marker {
        position: relative;
        width: 46px;
        height: 46px;
        pointer-events: none;
        --ty-color: #ef4444;
        --ty-glow: rgba(239,68,68,0.45);
      }

      .typhoon-center-marker svg {
        width: 100%;
        height: 100%;
        display: block;
        filter: drop-shadow(0 0 10px var(--ty-glow));
        transform: scale(0.6);
        transform-origin: 50% 50%;
      }

      .typhoon-center-marker .typhoon-spin {
        transform-box: fill-box;
        transform-origin: center;
        animation: typhoon-spin 2.6s linear infinite;
      }

      @keyframes typhoon-spin {
        from { transform: rotate(0deg); }
        to { transform: rotate(360deg); }
      }

      .mapboxgl-popup.typhoon-popup .mapboxgl-popup-content {
        padding: 0;
        border-radius: 16px;
        overflow: hidden;
        background: rgba(248, 250, 252, 0.98);
        box-shadow: 0 16px 32px rgba(15, 23, 42, 0.3);
        min-width: 240px;
      }

      .typhoon-popup-card { color: #0f172a; }

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
      }

      .typhoon-popup-body b { color: #020617; }

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
          min-width: auto;
        }
      }
    `;
    document.head.appendChild(style);
  }

  getActiveAgencies() {
    if (this.enableDemo) return this.agencyDefs.slice();
    return this.agencyDefs.filter(x => !x.demo);
  }

  hexToRgba(color, alpha) {
    if (typeof color !== "string") return `rgba(239,68,68,${alpha})`;
    let hex = color.trim();
    if (hex.startsWith("rgb")) return `rgba(239,68,68,${alpha})`;
    if (!hex.startsWith("#")) return `rgba(239,68,68,${alpha})`;
    hex = hex.slice(1);
    if (hex.length === 3) hex = hex.split("").map(x => x + x).join("");
    if (hex.length !== 6) return `rgba(239,68,68,${alpha})`;
    const r = parseInt(hex.slice(0, 2), 16);
    const g = parseInt(hex.slice(2, 4), 16);
    const b = parseInt(hex.slice(4, 6), 16);
    if (![r, g, b].every(Number.isFinite)) return `rgba(239,68,68,${alpha})`;
    return `rgba(${r},${g},${b},${alpha})`;
  }

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

  /**获取台风编号 */
  getItemCode(item) {
    return String(item?.code || item?.id || "").trim();
  }

  /**获取中文名 */
  getChineseName(item) {
    return String(item?.chineseName || item?.chinesName || item?.chinaname || "").trim();
  }

  /**排序台风列表 */
  sortHistoryList(list) {
    return (Array.isArray(list) ? list : []).slice().sort((a, b) => {
      const ac = this.getItemCode(a);
      const bc = this.getItemCode(b);
      return bc.localeCompare(ac);
    });
  }

  async ensureHistoryList() {
    if (this.historyList && this.historyList.length) return;
    try {
      const resp = await fetchWithTimeout(this.historyApi, {}, 12000);
      if (resp.ok) {
        const list = await resp.json();
        this.historyList = this.sortHistoryList(list);
      }
    } catch (_e) { }
    if (!Array.isArray(this.historyList)) this.historyList = [];
    const years = this.getHistoryYears(this.historyList);
    if (!this.selectedYear) this.selectedYear = years.length ? String(years[0]) : "all";
    if (!this.selectedKey) {
      const item = this.getDefaultItem();
      if (item) this.selectedKey = `history:${this.getItemCode(item)}`;
    }
  }

  formatStormOptionLabel(item) {
    const id = this.getItemCode(item);
    const name = this.getChineseName(item) || String(item?.name || "").trim();
    const lvl = Number(item?.maxLevel);
    const levelText = Number.isFinite(lvl) && lvl > 0 ? ` ${lvl}` : "";
    return `${id} ${name || "台风"}${levelText}`.trim();
  }

  getYearFromId(id) {
    const s = String(id || "").trim();
    if (s.length >= 4 && /^\d{4}/.test(s)) return Number(s.slice(0, 4));
    return NaN;
  }

  getHistoryYears(list) {
    const set = new Set();
    for (const item of (Array.isArray(list) ? list : [])) {
      const y = this.getYearFromId(this.getItemCode(item));
      if (Number.isFinite(y) && y > 1900) set.add(y);
    }
    return Array.from(set.values()).sort((a, b) => b - a);
  }

  /**获取指定年份台风 */
  getYearItems(year) {
    if (!year || year === "all") return this.historyList || [];
    return (this.historyList || []).filter(item => String(this.getYearFromId(this.getItemCode(item))) === String(year));
  }

  /**获取默认台风 */
  getDefaultItem(year = this.selectedYear) {
    const list = this.getYearItems(year);
    const named = list.find(x => !!this.getChineseName(x));
    return named || list[0] || this.historyList[0] || null;
  }

  parseSelectedKey(value) {
    const val = String(value || "");
    if (!val) return { mode: "history", id: "" };
    const idx = val.indexOf(":");
    if (idx <= 0) return { mode: "history", id: val };
    const mode = val.slice(0, idx);
    const id = val.slice(idx + 1);
    return { mode, id };
  }

  /**读取文本 */
  async fetchText(url) {
    const response = await fetchWithTimeout(url, {}, 12000);
    if (!response.ok) throw new Error(`台风文本请求失败: ${response.status} ${url}`);
    return response.text();
  }

  /**读取 JSON */
  async fetchJson(url) {
    const response = await fetchWithTimeout(url, {}, 12000);
    if (!response.ok) throw new Error(`台风数据请求失败: ${response.status} ${url}`);
    return response.json();
  }

  /**获取年度文件地址 */
  getYearFileUrl(year) {
    return new URL(`CH${year}BST.txt`, this.dataDir).href;
  }

  /**获取实时轨迹地址 */
  getCurrentTrackUrl(code) {
    return `${this.currentTrackApi}${encodeURIComponent(code)}.en.json`;
  }

  /**获取实时预报地址 */
  getCurrentForecastUrl(code) {
    return `${this.currentForecastApi}${encodeURIComponent(code)}/all.json`;
  }

  /**确保年度缓存 */
  async ensureYearData(year) {
    const y = Number(year);
    if (!Number.isFinite(y) || y <= 1900) return [];
    if (this.yearStormMap.has(y)) return this.yearStormMap.get(y) || [];
    let list = [];
    try {
      const text = await this.fetchText(this.getYearFileUrl(y));
      list = this.parseYearText(text, y);
    } catch (_e) {
      list = [];
    }
    this.yearStormMap.set(y, list);
    return list;
  }

  /**解析年度文本 */
  parseYearText(text, year) {
    const lines = String(text || "").split(/\r?\n/);
    const list = [];
    let storm = null;
    for (const raw of lines) {
      const line = String(raw || "").trim();
      if (!line) continue;
      if (line.startsWith("66666")) {
        if (storm && storm.points.length) this.finishStorm(storm, list);
        storm = this.parseHeaderLine(line, year);
        continue;
      }
      if (!storm) continue;
      const point = this.parseTrackLine(line);
      if (point) storm.points.push(point);
    }
    if (storm && storm.points.length) this.finishStorm(storm, list);
    return list;
  }

  /**结束台风解析 */
  finishStorm(storm, list) {
    storm.points.sort((a, b) => String(a.timeCode).localeCompare(String(b.timeCode)));
    storm.maxWind = storm.points.reduce((m, x) => Math.max(m, Number(x.windMs) || 0), 0);
    storm.maxLevelCode = storm.points.reduce((m, x) => Math.max(m, Number(x.levelCode) || 0), 0);
    list.push(storm);
  }

  /**解析头记录 */
  parseHeaderLine(line, year) {
    const parts = String(line || "").trim().split(/\s+/);
    const shortCode = String(parts[1] || "").trim();
    const seqCode = String(parts[3] || "").trim();
    const rawName = String(parts[7] || "").trim();
    const code = shortCode !== "0000"
      ? `${year}${shortCode.slice(-2)}`
      : `${year}N${seqCode.slice(-2) || "00"}`;
    return {
      year,
      code,
      shortCode,
      seqCode,
      rawName,
      name: rawName === "(nameless)" ? "" : rawName,
      points: []
    };
  }

  /**解析轨迹记录 */
  parseTrackLine(line) {
    const parts = String(line || "").trim().split(/\s+/);
    if (parts.length < 6) return null;
    const timeCode = String(parts[0] || "").trim();
    const levelCode = Number(parts[1]);
    const lat = Number(parts[2]) / 10;
    const lng = Number(parts[3]) / 10;
    const pressure = Number(parts[4]);
    const windMs = Number(parts[5]);
    if (!/^\d{10}$/.test(timeCode)) return null;
    if (!Number.isFinite(lat) || !Number.isFinite(lng)) return null;
    return {
      timeCode,
      time: this.toUtcIso(timeCode),
      timeText: this.formatCmaTime(timeCode),
      shortLabel: this.formatMiniTimeCode(timeCode),
      levelCode: Number.isFinite(levelCode) ? levelCode : 0,
      coord: [lng, lat],
      pressure: Number.isFinite(pressure) ? pressure : "-",
      windMs: Number.isFinite(windMs) ? windMs : 0
    };
  }

  /**转 ISO 时间 */
  toUtcIso(code) {
    const s = String(code || "").trim();
    if (!/^\d{10}$/.test(s)) return "";
    return `${s.slice(0, 4)}-${s.slice(4, 6)}-${s.slice(6, 8)}T${s.slice(8, 10)}:00:00Z`;
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

  /**格式化 CMA 时间 */
  formatCmaTime(code) {
    const s = String(code || "").trim();
    if (!/^\d{10}$/.test(s)) return "-";
    return `${s.slice(0, 4)}-${s.slice(4, 6)}-${s.slice(6, 8)} ${s.slice(8, 10)}:00`;
  }

  /**格式化 CMA 短时间 */
  formatMiniTimeCode(code) {
    const s = String(code || "").trim();
    if (!/^\d{10}$/.test(s)) return "--";
    return `${s.slice(4, 6)}/${s.slice(6, 8)}-${s.slice(8, 10)}`;
  }

  /**获取 CMA 等级文本 */
  getCmaLevelName(levelCode, windMs) {
    const txt = this.cmaLevelMap[Number(levelCode)];
    if (txt) return txt;
    return this.getWindMeta(windMs).name;
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
        timeText: props.display_time || this.formatTime(time),
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
          timeText: item?.time || this.formatTime(time),
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

  /**转经纬度 */
  toLngLat(point) {
    if (!Array.isArray(point) || point.length < 2) return null;
    const lat = Number(point[0]);
    const lon = Number(point[1]);
    if (!Number.isFinite(lat) || !Number.isFinite(lon)) return null;
    return [lon, lat];
  }

  /**去重坐标 */
  dedupeCoords(coords) {
    const list = [];
    for (const coord of coords) {
      if (!Array.isArray(coord) || coord.length < 2) continue;
      const prev = list[list.length - 1];
      if (!prev || prev[0] !== coord[0] || prev[1] !== coord[1]) {
        list.push(coord);
      }
    }
    return list;
  }

  /**转数字 */
  toNumber(value, fallback = 0) {
    const num = Number(value);
    return Number.isFinite(num) ? num : fallback;
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

  /**按小时找规格 */
  getSpecByHours(specs, hours) {
    return (specs || []).find(x => Number(x?.advancedHours) === Number(hours)) || null;
  }

  /**读取风速 */
  getWindValue(specEntry, item, fallback = 18) {
    const a = this.toNumber(specEntry?.maximumWind?.sustained?.["m/s"], NaN);
    if (Number.isFinite(a) && a > 0) return a;
    const b = this.toNumber(item?.maximumWind?.sustained?.["m/s"], NaN);
    if (Number.isFinite(b) && b > 0) return b;
    return fallback;
  }

  /**读取气压 */
  getPressure(specEntry, item, fallback = "-") {
    return `${specEntry?.pressure || item?.pressure || fallback}`;
  }

  /**获取风力等级 */
  getWindMeta(ms) {
    const wind = this.toNumber(ms, 0);
    for (const item of this.windDefs) {
      if (wind >= item.min && wind < item.max) return item;
    }
    return this.windDefs[this.windDefs.length - 1];
  }

  /**获取点半径 */
  getPointRadius(ms, isCurrent = false) {
    const wind = this.toNumber(ms, 0);
    let radius = 4;
    if (wind >= 24.5) radius = 4.8;
    if (wind >= 32.7) radius = 5.6;
    if (wind >= 41.5) radius = 6.3;
    if (wind >= 51) radius = 7;
    if (isCurrent) radius += 1.8;
    return Number(radius.toFixed(1));
  }

  /**构建圆面 */
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

  /**构建线要素 */
  buildPathFeature(kind, coords, props) {
    if (!Array.isArray(coords) || coords.length < 2) return null;
    return {
      type: "Feature",
      geometry: { type: "LineString", coordinates: coords },
      properties: { kind, ...props }
    };
  }

  /**构建弹窗属性 */
  buildDetailProps(opt) {
    return {
      popupTitle: opt.popupTitle || "台风点位",
      popupTime: opt.popupTime || "-",
      popupCoord: opt.popupCoord || "-",
      popupPressure: `${opt.popupPressure || "-"}`,
      popupWind: `${opt.popupWind || "-"}`,
      popupLevel: opt.popupLevel || "-",
      popupType: opt.popupType || "-",
      popupSource: opt.popupSource || "-",
      popupNote: opt.popupNote || ""
    };
  }

  /**估算实况采样点 */
  buildHistoryItems(actualCoords, analysis, analysisSpec, stormBase) {
    const items = [];
    const total = actualCoords.length;
    if (total === 0) return items;
    const endTime = new Date(analysis?.validtime?.JST || stormBase.issue || Date.now()).getTime();
    const currentWind = this.getWindValue(analysisSpec, analysis, 22);
    const currentPressure = this.toNumber(this.getPressure(analysisSpec, analysis, 990), 990);
    for (let i = 0; i < total; i++) {
      const ratio = total <= 1 ? 1 : i / (total - 1);
      const time = new Date(endTime - (total - 1 - i) * 3 * 3600 * 1000).toISOString();
      const windMs = Math.round(currentWind * (0.42 + ratio * 0.58));
      const pressure = Math.round(currentPressure + (1 - ratio) * 38);
      items.push({
        coord: actualCoords[i],
        time,
        hours: -(total - 1 - i) * 3,
        windMs,
        pressure,
        windMeta: this.getWindMeta(windMs),
        agencyId: "analysis",
        agencyName: "实况",
        agencyColor: "#ffffff",
        shortLabel: this.formatMiniTime(time),
        isCurrent: i === total - 1
      });
    }
    return items;
  }

  /**构建基准预报点 */
  buildForecastItems(list, specs, agencyName) {
    const items = [];
    for (const item of list || []) {
      const hours = this.toNumber(item?.advancedHours, 0);
      if (hours <= 0) continue;
      const center = this.toLngLat(item?.center);
      if (!center) continue;
      const spec = this.getSpecByHours(specs, hours);
      const windMs = this.getWindValue(spec, item, 20);
      const probKm = this.toNumber(spec?.probabilityCircleRadius?.km, this.toNumber(item?.probabilityCircle?.radius, 0) / 1000);
      items.push({
        coord: center,
        hours,
        time: item?.validtime?.JST || spec?.validtime?.JST || "",
        pressure: this.getPressure(spec, item, "-"),
        windMs,
        windMeta: this.getWindMeta(windMs),
        probKm,
        agencyName,
        course: spec?.course || "-",
        moveSpeed: spec?.speed?.["km/h"] || spec?.speed?.note?.jp || spec?.speed?.note?.en || "-",
        type: spec?.category?.en || spec?.category?.jp || "TY"
      });
    }
    return items;
  }

  /**按小时找前次预报 */
  getPrevForecastItem(prevItems, hours) {
    return (prevItems || []).find(x => Number(x?.hours) === Number(hours)) || null;
  }

  /**模拟机构坐标 */
  getAgencyCoord(baseItem, prevItem, agency, i, total) {
    const baseCoord = baseItem?.coord;
    if (!Array.isArray(baseCoord) || baseCoord.length < 2) return null;
    const prevCoord = prevItem?.coord;
    const hours = this.toNumber(baseItem?.hours, 0);
    const ratio = total <= 1 ? 0 : i / (total - 1);
    const biasScale = Math.max(0.18, hours / 24);
    let lon = baseCoord[0] + agency.biasLon * biasScale + Math.sin((i + 1) * 0.8) * agency.blend * 0.55;
    let lat = baseCoord[1] + agency.biasLat * biasScale + Math.cos((i + 1) * 0.75) * agency.blend * 0.42;
    if (Array.isArray(prevCoord) && prevCoord.length >= 2) {
      lon = baseCoord[0] * (1 - agency.blend) + prevCoord[0] * agency.blend + (ratio - 0.5) * agency.biasLon * 0.7;
      lat = baseCoord[1] * (1 - agency.blend) + prevCoord[1] * agency.blend + (ratio - 0.5) * agency.biasLat * 0.7;
    }
    return [Number(lon.toFixed(3)), Number(lat.toFixed(3))];
  }

  /**构建机构预报点 */
  buildAgencyItems(baseItems, prevItems, agency) {
    const items = [];
    const total = baseItems.length;
    for (let i = 0; i < total; i++) {
      const base = baseItems[i];
      const prev = this.getPrevForecastItem(prevItems, base.hours);
      const coord = agency.id === "jma" ? base.coord : this.getAgencyCoord(base, prev, agency, i, total);
      if (!coord) continue;
      const windMs = Math.max(12, Math.round(base.windMs + agency.biasLon * 3 - agency.biasLat * 2));
      items.push({
        ...base,
        coord,
        windMs,
        windMeta: this.getWindMeta(windMs),
        pressure: `${Math.max(880, this.toNumber(base.pressure, 990) + Math.round((agency.biasLat - agency.biasLon) * 8))}`,
        agencyId: agency.id,
        agencyName: agency.name,
        agencyColor: agency.color,
        demo: agency.demo,
        shortLabel: `${agency.short}${base.hours}`
      });
    }
    return items;
  }

  /**构建点要素 */
  buildPointFeature(kind, item, stormBase, extra = {}) {
    const windMeta = item.windMeta || this.getWindMeta(item.windMs);
    return {
      type: "Feature",
      geometry: { type: "Point", coordinates: item.coord },
      properties: {
        kind,
        stormId: stormBase.stormId,
        stormName: stormBase.name,
        stormNumber: stormBase.number,
        trackType: extra.trackType || "forecast",
        agencyId: item.agencyId || extra.agencyId || "jma",
        agencyName: item.agencyName || extra.agencyName || "日本",
        agencyColor: item.agencyColor || extra.agencyColor || "#7c3aed",
        windColor: windMeta.color,
        pointRadius: this.getPointRadius(item.windMs, !!item.isCurrent),
        shortLabel: item.shortLabel || "",
        isCurrent: item.isCurrent ? 1 : 0,
        ...this.buildDetailProps({
          popupTitle: `${stormBase.number ? `${stormBase.number} ` : ""}${stormBase.name}`.trim(),
          popupTime: item.timeText || this.formatTime(item.time),
          popupCoord: this.formatCoord(item.coord),
          popupPressure: item.pressure,
          popupWind: item.windMs,
          popupLevel: item.levelName || windMeta.name,
          popupType: extra.popupType || (extra.trackType === "actual" ? "实况采样点" : `${item.hours}小时预报点`),
          popupSource: item.agencyName || extra.agencyName || "日本",
          popupNote: item.demo ? "演示机构路径，仅用于原型展示" : ""
        })
      }
    };
  }

  /**构建点标签要素 */
  buildPointLabelFeature(item, stormBase, labelColor) {
    return {
      type: "Feature",
      geometry: { type: "Point", coordinates: item.coord },
      properties: {
        kind: "track-label",
        stormId: stormBase.stormId,
        label: item.shortLabel || "",
        labelColor: labelColor || item.windMeta?.color || "#ffffff"
      }
    };
  }

  /**构建机构尾标 */
  buildAgencyTailFeature(item, stormBase, agency) {
    return {
      type: "Feature",
      geometry: { type: "Point", coordinates: item.coord },
      properties: {
        kind: "agency-tail-label",
        stormId: stormBase.stormId,
        label: agency.name,
        labelColor: agency.color
      }
    };
  }

  /**构建台风名称标注 */
  buildNameFeature(center, stormBase, windColor) {
    return {
      type: "Feature",
      geometry: { type: "Point", coordinates: center },
      properties: {
        kind: "name-label",
        label: `台风${stormBase.number ? ` ${stormBase.number}` : ""} ${stormBase.name}`.trim(),
        labelColor: windColor
      }
    };
  }

  /**构建风圈 */
  buildWindCircleFeatures(analysis, analysisSpec, stormBase) {
    const features = [];
    const center = this.toLngLat(analysis?.center);
    if (!center) return features;
    const galeKm = Math.max(
      this.toNumber(analysisSpec?.galeWarning?.[0]?.range?.km, 0),
      this.toNumber(analysisSpec?.galeWarning?.[1]?.range?.km, 0),
      this.toNumber(analysis?.galeWarningArea?.radius, 0) / 1000
    );
    const stormKm = Math.max(
      this.toNumber(analysisSpec?.stormWarning?.[0]?.range?.km, 0),
      this.toNumber(analysis?.stormWarningArea?.arc?.[0]?.[1], 0) / 1000
    );
    if (galeKm > 0) {
      const f = this.buildCircleFeature("wind-7-circle", center, galeKm * 1000, {
        stormId: stormBase.stormId,
        label: "7级风圈"
      });
      if (f) features.push(f);
    }
    if (stormKm > 0) {
      const f = this.buildCircleFeature("wind-10-circle", center, stormKm * 1000, {
        stormId: stormBase.stormId,
        label: "10级风圈"
      });
      if (f) features.push(f);
    }
    return features;
  }

  /**构建概率圈 */
  buildProbCircleFeature(item, stormBase, agency) {
    if (!item.probKm || item.probKm <= 0) return null;
    return this.buildCircleFeature("prob-circle", item.coord, item.probKm * 1000, {
      stormId: stormBase.stormId,
      agencyId: agency.id,
      agencyName: agency.name,
      agencyColor: agency.color
    });
  }

  /**构建单个台风要素 */
  buildStormFeatures(storm) {
    const features = [];
    const points = Array.isArray(storm?.points) ? storm.points.slice() : [];
    if (points.length === 0) return { features, summary: null, currentStorm: null };
    const meta = storm?.meta || null;
    const nameCn = this.getChineseName(meta) || storm?.name || storm?.rawName || "台风";
    const code = this.getItemCode(meta) || storm?.code || "";
    const stormBase = {
      stormId: storm?.code || "typhoon",
      name: nameCn,
      nameCn,
      number: code,
      issue: points[points.length - 1]?.time || ""
    };
    const historyItems = points.map((item, i) => ({
      ...item,
      windMeta: this.getWindMeta(item.windMs),
      levelName: this.getCmaLevelName(item.levelCode, item.windMs),
      isCurrent: i === points.length - 1
    }));
    const currentItem = historyItems[historyItems.length - 1];
    const currentCenter = currentItem?.coord || null;
    const actualCoords = this.dedupeCoords(historyItems.map(x => x.coord));
    const historyLine = this.buildPathFeature("history-path", actualCoords, {
      stormId: stormBase.stormId,
      lineColor: "#fb7185"
    });
    if (historyLine) features.push(historyLine);

    for (const item of historyItems) {
      features.push(this.buildPointFeature("track-point", item, stormBase, {
        trackType: "actual",
        agencyId: "cma",
        agencyName: "中国气象局最佳路径",
        agencyColor: "#ffffff"
      }));
      features.push(this.buildPointLabelFeature(item, stormBase, "#e2e8f0"));
    }

    if (currentCenter) {
      features.push(this.buildNameFeature(currentCenter, stormBase, currentItem?.windMeta?.color));
    }

    const summary = {
      mode: "history",
      name: nameCn,
      number: stormBase.number,
      pressure: currentItem?.pressure || "-",
      wind: currentItem?.windMs || 0,
      issue: stormBase.issue,
      startText: historyItems[0]?.timeText || "-",
      endText: currentItem?.timeText || "-",
      pointCnt: historyItems.length,
      maxWind: historyItems.reduce((m, x) => Math.max(m, Number(x.windMs) || 0), 0)
    };
    const currentStorm = currentCenter ? {
      id: stormBase.stormId,
      center: currentCenter,
      color: currentItem?.windMeta?.color || "#ef4444"
    } : null;
    return { features, summary, currentStorm };
  }

  /**构建当前台风要素 */
  async buildCurrentStormFeatures(code, meta) {
    const data = await this.ensureCurrentStormData(code);
    const track = Array.isArray(data?.track) ? data.track.slice() : [];
    const predicts = Array.isArray(data?.forecast?.predicts) ? data.forecast.predicts.slice() : [];
    const currentForecast = data?.forecast?.current || null;
    if (!track.length && currentForecast) {
      const windMs = Number(currentForecast?.speed_ms);
      track.push({
        coord: [Number(currentForecast?.long), Number(currentForecast?.lat)],
        time: this.toUtcIsoFromUnix(currentForecast?.forecasttime) || this.toUtcIsoFromUnix(currentForecast?.basetime) || this.toUtcIsoFromJstText(currentForecast?.time),
        timeText: currentForecast?.time || "-",
        shortLabel: "实况",
        pressure: Number.isFinite(Number(currentForecast?.pressure)) ? Number(currentForecast?.pressure) : "-",
        windMs: Number.isFinite(windMs) ? Math.round(windMs) : 0,
        levelName: String(currentForecast?.class || "").trim() || this.getWindMeta(windMs).name,
        source: "Digital Typhoon / JMA"
      });
    }
    if (!track.length) {
      return { features: [], summary: null, currentStorm: null };
    }

    const features = [];
    const nameCn = this.getChineseName(meta) || String(meta?.name || "").trim() || "台风";
    const stormBase = {
      stormId: `current-${code}`,
      name: nameCn,
      nameCn,
      number: code,
      issue: track[track.length - 1]?.time || ""
    };
    const historyItems = track.map((item, i) => ({
      ...item,
      windMeta: this.getWindMeta(item.windMs),
      isCurrent: i === track.length - 1
    }));
    const currentItem = historyItems[historyItems.length - 1];
    const currentCenter = currentItem?.coord || null;
    const actualCoords = this.dedupeCoords(historyItems.map(x => x.coord));
    const historyLine = this.buildPathFeature("history-path", actualCoords, {
      stormId: stormBase.stormId,
      lineColor: "#fb7185"
    });
    if (historyLine) features.push(historyLine);

    for (const item of historyItems) {
      features.push(this.buildPointFeature("track-point", item, stormBase, {
        trackType: "actual",
        agencyId: "dt",
        agencyName: "Digital Typhoon 实况",
        agencyColor: "#ffffff",
        popupType: "当前台风实况点"
      }));
      features.push(this.buildPointLabelFeature(item, stormBase, "#e2e8f0"));
    }

    if (currentCenter) {
      const agency = { id: "jma", name: "Digital Typhoon / JMA", color: "#7c3aed" };
      const pathItems = predicts.map(item => ({
        ...item,
        windMeta: this.getWindMeta(item.windMs)
      }));
      const pathCoords = this.dedupeCoords([currentCenter, ...pathItems.map(x => x.coord)]);
      const pathFeature = this.buildPathFeature("agency-path", pathCoords, {
        stormId: stormBase.stormId,
        agencyId: agency.id,
        agencyName: agency.name,
        agencyColor: agency.color
      });
      if (pathFeature) features.push(pathFeature);
      for (const item of pathItems) {
        features.push(this.buildPointFeature("track-point", item, stormBase, {
          trackType: "forecast",
          agencyId: agency.id,
          agencyName: agency.name,
          agencyColor: agency.color,
          popupType: `${item.period}小时预报点`
        }));
        features.push(this.buildPointLabelFeature(item, stormBase, agency.color));
        const circle = this.buildProbCircleFeature(item, stormBase, agency);
        if (circle) features.push(circle);
      }
      const lastPredict = pathItems[pathItems.length - 1];
      if (lastPredict) {
        features.push(this.buildAgencyTailFeature(lastPredict, stormBase, agency));
      }
      features.push(this.buildNameFeature(currentCenter, stormBase, currentItem?.windMeta?.color));
    }

    const summary = {
      mode: "current",
      name: nameCn,
      number: stormBase.number,
      pressure: currentItem?.pressure || "-",
      wind: currentItem?.windMs || 0,
      issue: stormBase.issue,
      startText: historyItems[0]?.timeText || "-",
      endText: currentItem?.timeText || "-",
      pointCnt: historyItems.length,
      maxWind: historyItems.reduce((m, x) => Math.max(m, Number(x.windMs) || 0), 0),
      predictCnt: predicts.length
    };
    const currentStorm = currentCenter ? {
      id: stormBase.stormId,
      center: currentCenter,
      color: currentItem?.windMeta?.color || "#ef4444"
    } : null;
    return { features, summary, currentStorm };
  }

  /**构建全部要素 */
  async buildFeatures() {
    await this.ensureHistoryList();
    let selected = this.parseSelectedKey(this.selectedKey);
    if (!selected.id) {
      const item = this.getDefaultItem();
      if (item) {
        this.selectedKey = `history:${this.getItemCode(item)}`;
        selected = this.parseSelectedKey(this.selectedKey);
      }
    }
    const code = selected.id;
    const meta = (this.historyList || []).find(x => this.getItemCode(x) === code) || null;
    const year = this.getYearFromId(code);
    const storms = await this.ensureYearData(year);
    const storm = storms.find(x => String(x?.code || "") === String(code));
    if (storm) {
      storm.meta = meta;
      const result = this.buildStormFeatures(storm);
      return {
        features: result.features,
        summaries: result.summary ? [result.summary] : [],
        currentStorms: result.currentStorm ? [result.currentStorm] : [],
        selected,
        missingCodes: []
      };
    }
    const currentResult = await this.buildCurrentStormFeatures(code, meta);
    if (currentResult.summary || currentResult.features.length) {
      return {
        features: currentResult.features,
        summaries: currentResult.summary ? [currentResult.summary] : [],
        currentStorms: currentResult.currentStorm ? [currentResult.currentStorm] : [],
        selected,
        missingCodes: []
      };
    }
    return { features: [], summaries: [], currentStorms: [], selected, missingCodes: code ? [code] : [] };
  }

  /**确保图例容器 */
  ensureLegend() {
    if (this.legendEl) return this.legendEl;
    this.ensureStyle();
    this.legendEl = document.getElementById(this.legendId);
    if (!this.legendEl) {
      this.legendEl = document.createElement("div");
      this.legendEl.id = this.legendId;
      this.legendEl.className = "typhoon-legend is-hidden";
      document.body.appendChild(this.legendEl);
    }
    return this.legendEl;
  }

  /**渲染图例 */
  renderLegend() {
    const el = this.ensureLegend();
    if (!el) return;
    const windHtml = this.windDefs.map(item => `
      <span class="typhoon-legend-chip">
        <span class="typhoon-dot" style="background:${item.color}"></span>
        <span>${item.name}</span>
      </span>
    `).join("");
    const selected = this.parseSelectedKey(this.selectedKey);
    const years = this.getHistoryYears(this.historyList);
    const yearOptions = [
      `<option value="all"${this.selectedYear === "all" ? " selected" : ""}>全部年份</option>`,
      ...years.map(y => `<option value="${y}"${String(this.selectedYear) === String(y) ? " selected" : ""}>${y}</option>`)
    ];
    const historyOptions = this.getYearItems(this.selectedYear).map(item => {
      const id = this.getItemCode(item);
      if (!id) return "";
      const label = this.formatStormOptionLabel(item);
      const key = `history:${id}`;
      const sel = selected.mode === "history" && selected.id === id ? " selected" : "";
      return `<option value="${key}"${sel}>${label}</option>`;
    }).filter(Boolean).join("");

    el.innerHTML = `
      <div class="typhoon-legend-title">
        <span>台风历史轨迹</span>
        <span class="typhoon-legend-tip">数据源：中国气象局最佳路径</span>
      </div>
      <div class="typhoon-legend-row">
        <span class="typhoon-legend-label">年份</span>
        <select id="${this.yearSelectId}" class="typhoon-select">
          ${yearOptions.join("")}
        </select>
        <span class="typhoon-legend-label">台风</span>
        <select id="${this.selectId}" class="typhoon-select">
          ${historyOptions}
        </select>
      </div>
      <div class="typhoon-legend-row">
        <span class="typhoon-legend-label">轨迹</span>
        <span class="typhoon-legend-chip">
          <span class="typhoon-line" style="color:#fb7185"></span>
          <span>历史路径</span>
        </span>
        <span class="typhoon-legend-chip">
          <span class="typhoon-line dashed" style="color:#7c3aed"></span>
          <span>当前预报</span>
        </span>
        <span class="typhoon-legend-chip">
          <span class="typhoon-dot" style="background:#ffffff"></span>
          <span>轨迹点</span>
        </span>
        <span class="typhoon-legend-chip">
          <span class="typhoon-line dashed" style="color:#a855f7"></span>
          <span>预测圈</span>
        </span>
      </div>
      <div class="typhoon-legend-row">
        <span class="typhoon-legend-label">风力</span>
        ${windHtml}
      </div>
    `;
  }

  /**显示图例 */
  showLegend() {
    const el = this.ensureLegend();
    if (!el) return;
    this.renderLegend();
    this.bindLegendEvents();
    el.classList.remove("is-hidden");
  }

  bindLegendEvents() {
    if (this.legendBound) return;
    const el = this.ensureLegend();
    if (!el) return;
    el.addEventListener("change", async (e) => {
      const target = e?.target;
      if (!target) return;
      if (target.id === this.yearSelectId) {
        const year = String(target.value || "all");
        this.selectedYear = year || "all";
        const item = this.getDefaultItem(this.selectedYear);
        this.selectedKey = item ? `history:${this.getItemCode(item)}` : "";
        this.renderLegend();
        try { await this.refresh(true); } catch (_e) { }
        return;
      }

      if (target.id === this.selectId) {
        const value = String(target.value || "");
        this.selectedKey = value;
        try {
          await this.refresh(true);
        } catch (_e) { }
      }
    });
    this.legendBound = true;
  }

  /**隐藏图例 */
  hideLegend() {
    const el = this.ensureLegend();
    if (!el) return;
    el.classList.add("is-hidden");
  }

  /**获取机构路径图层ID */
  getAgencyPathLayerId(agencyId) {
    return `typhoon-agency-path-${agencyId}`;
  }

  /**获取全部图层ID */
  getLayerIds() {
    return [
      this.wind7FillLayerId,
      this.wind7LineLayerId,
      this.wind10FillLayerId,
      this.wind10LineLayerId,
      this.probFillLayerId,
      this.probLayerId,
      this.historyLayerId,
      ...this.agencyDefs.map(x => this.getAgencyPathLayerId(x.id)),
      this.pointLayerId,
      this.pointLabelLayerId,
      this.agencyTailLayerId,
      this.nameLayerId
    ];
  }

  /**创建地图图层 */
  ensureLayers() {
    const { map } = this.runtime;
    if (!map.getLayer(this.wind7FillLayerId)) {
      map.addLayer({
        id: this.wind7FillLayerId,
        type: "fill",
        source: this.sourceId,
        filter: ["==", ["get", "kind"], "wind-7-circle"],
        paint: {
          "fill-color": "#67e8f9",
          "fill-opacity": 0.14
        }
      });
    }
    if (!map.getLayer(this.wind7LineLayerId)) {
      map.addLayer({
        id: this.wind7LineLayerId,
        type: "line",
        source: this.sourceId,
        filter: ["==", ["get", "kind"], "wind-7-circle"],
        paint: {
          "line-color": "#38bdf8",
          "line-width": 1.5,
          "line-opacity": 0.9
        }
      });
    }
    if (!map.getLayer(this.wind10FillLayerId)) {
      map.addLayer({
        id: this.wind10FillLayerId,
        type: "fill",
        source: this.sourceId,
        filter: ["==", ["get", "kind"], "wind-10-circle"],
        paint: {
          "fill-color": "#86efac",
          "fill-opacity": 0.16
        }
      });
    }
    if (!map.getLayer(this.wind10LineLayerId)) {
      map.addLayer({
        id: this.wind10LineLayerId,
        type: "line",
        source: this.sourceId,
        filter: ["==", ["get", "kind"], "wind-10-circle"],
        paint: {
          "line-color": "#22c55e",
          "line-width": 1.6,
          "line-opacity": 0.92
        }
      });
    }
    if (!map.getLayer(this.probFillLayerId)) {
      map.addLayer({
        id: this.probFillLayerId,
        type: "fill",
        source: this.sourceId,
        filter: ["==", ["get", "kind"], "prob-circle"],
        paint: {
          "fill-color": ["get", "agencyColor"],
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
          "line-color": ["get", "agencyColor"],
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
          "line-opacity": 0.95
        }
      });
    }
    for (const agency of this.getActiveAgencies()) {
      const id = this.getAgencyPathLayerId(agency.id);
      if (map.getLayer(id)) continue;
      const paint = {
        "line-color": agency.color,
        "line-width": agency.id === "jma" ? 2.6 : 2,
        "line-opacity": agency.id === "jma" ? 0.95 : 0.82
      };
      if (agency.dash.length) paint["line-dasharray"] = agency.dash;
      map.addLayer({
        id,
        type: "line",
        source: this.sourceId,
        filter: ["all", ["==", ["get", "kind"], "agency-path"], ["==", ["get", "agencyId"], agency.id]],
        paint
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
          "circle-stroke-color": ["coalesce", ["get", "agencyColor"], "#ffffff"],
          "circle-stroke-width": ["case", ["==", ["get", "isCurrent"], 1], 2.4, 1.2],
          "circle-opacity": 0.96
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
    if (!map.getLayer(this.agencyTailLayerId)) {
      map.addLayer({
        id: this.agencyTailLayerId,
        type: "symbol",
        source: this.sourceId,
        filter: ["==", ["get", "kind"], "agency-tail-label"],
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
  }

  /**生成弹窗 HTML */
  getPopupHtml(props) {
    return `
      <div class="typhoon-popup-card">
        <div class="typhoon-popup-head">${props.popupSource || "台风点位"}</div>
        <div class="typhoon-popup-body">
          <div><b>名称：</b>${props.popupTitle || "-"}</div>
          <div><b>时间：</b>${props.popupTime || "-"}</div>
          <div><b>当前位置：</b>${props.popupCoord || "-"}</div>
          <div><b>中心气压：</b>${props.popupPressure || "-"} 百帕</div>
          <div><b>最大风速：</b>${props.popupWind || "-"} 米/秒</div>
          <div><b>等级：</b>${props.popupLevel || "-"}</div>
          <div><b>类型：</b>${props.popupType || "-"}</div>
          ${props.popupNote ? `<div><b>备注：</b>${props.popupNote}</div>` : ""}
        </div>
      </div>
    `;
  }

  /**显示弹窗 */
  showPopup(feature, lngLat) {
    const { map } = this.runtime;
    if (!feature || !feature.properties) return;
    if (!this.popup) {
      this.popup = new mapboxgl.Popup({
        closeButton: false,
        closeOnClick: false,
        className: "typhoon-popup",
        maxWidth: "320px"
      });
    }
    const pos = lngLat || feature.geometry?.coordinates || map.getCenter();
    this.popup.setLngLat(pos).setHTML(this.getPopupHtml(feature.properties)).addTo(map);
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

  /**绑定点交互 */
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

  /**创建中心动画标记 */
  createCenterMarker(storm) {
    this.ensureStyle();
    const el = document.createElement("div");
    el.className = "typhoon-center-marker";
    el.style.setProperty("--ty-color", storm.color);
    el.style.setProperty("--ty-glow", this.hexToRgba(storm.color, 0.45));
    el.innerHTML = this.typhoonSvgMarkup;
    return new mapboxgl.Marker({ element: el, anchor: "center" }).setLngLat(storm.center);
  }

  /**同步中心动画标记 */
  syncCenterMarkers(storms) {
    const { map } = this.runtime;
    const active = new Set();
    for (const storm of storms) {
      active.add(storm.id);
      let marker = this.centerMarkers.get(storm.id);
      if (!marker) {
        marker = this.createCenterMarker(storm).addTo(map);
        this.centerMarkers.set(storm.id, marker);
      }
      marker.setLngLat(storm.center);
      const el = marker.getElement();
      if (el) el.style.display = "block";
      if (el) {
        el.style.setProperty("--ty-color", storm.color);
        el.style.setProperty("--ty-glow", this.hexToRgba(storm.color, 0.45));
      }
    }
    for (const [id, marker] of this.centerMarkers.entries()) {
      if (active.has(id)) continue;
      marker.remove();
      this.centerMarkers.delete(id);
    }
  }

  /**设置动画标记显示 */
  setMarkerVisible(visible) {
    for (const marker of this.centerMarkers.values()) {
      const el = marker.getElement();
      if (el) el.style.display = visible ? "block" : "none";
    }
  }

  /**刷新图层 */
  async refresh() {
    await this.ensureTyphoonSvg();
    const { features, summaries, currentStorms, selected, missingCodes } = await this.buildFeatures();
    addOrUpdateGeoJsonSource(this.runtime.map, this.sourceId, { type: "FeatureCollection", features });
    this.ensureLayers();
    this.ensureInteractions();
    this.currentStorms = currentStorms;
    this.syncCenterMarkers(currentStorms);
    this.showLegend();

    const sel = selected || this.parseSelectedKey(this.selectedKey);
    if (sel.mode === "history" && sel.id) {
      const item = (this.historyList || []).find(x => this.getItemCode(x) === String(sel.id));
      const label = item ? this.formatStormOptionLabel(item) : String(sel.id);
      if (missingCodes && missingCodes.length) {
        setInfo("typhoonInfo", `历史台风：${label}，未找到本地轨迹数据`);
      } else if (summaries.length) {
        const s = summaries[0];
        if (s.mode === "current") {
          setInfo("typhoonInfo", `当前台风：${label}`);
        } else {
          setInfo("typhoonInfo", `历史台风：${label}`);
        }
      } else {
        setInfo("typhoonInfo", `历史台风：${label}，暂无可用路径数据`);
      }
    } else {
      setInfo("typhoonInfo", "未选择历史台风");
    }

    this.setOpacity(this.runtime.getOpacity(this.name));
    this.lastStatus = true;
    this.lastTime = Date.now();
    return true;
  }

  /**设置透明度 */
  setOpacity(opacity) {
    const { map } = this.runtime;
    const lineIds = [
      this.wind7LineLayerId,
      this.wind10LineLayerId,
      this.probLayerId,
      this.historyLayerId,
      ...this.agencyDefs.map(x => this.getAgencyPathLayerId(x.id))
    ];
    const fillIds = [this.wind7FillLayerId, this.wind10FillLayerId, this.probFillLayerId];
    const circleIds = [this.pointLayerId];
    const textIds = [this.pointLabelLayerId, this.agencyTailLayerId, this.nameLayerId];
    for (const id of lineIds) {
      if (!map.getLayer(id)) continue;
      const base = id === this.probLayerId ? 0.86 : 0.95;
      map.setPaintProperty(id, "line-opacity", opacity * base);
    }
    for (const id of fillIds) {
      if (!map.getLayer(id)) continue;
      let base = 0.16;
      if (id === this.wind7FillLayerId) base = 0.14;
      if (id === this.probFillLayerId) base = 0.08;
      map.setPaintProperty(id, "fill-opacity", opacity * base);
    }
    for (const id of circleIds) {
      if (map.getLayer(id)) map.setPaintProperty(id, "circle-opacity", opacity);
    }
    for (const id of textIds) {
      if (map.getLayer(id)) map.setPaintProperty(id, "text-opacity", opacity);
    }
  }

  /**隐藏图层 */
  hide() {
    super.hide();
    const { map } = this.runtime;
    for (const id of this.getLayerIds()) {
      if (map.getLayer(id)) map.setLayoutProperty(id, "visibility", "none");
    }
    this.setMarkerVisible(false);
    this.hideLegend();
    this.hidePopup();
    setInfo("typhoonInfo", "未开启");
    return true;
  }

  /**显示图层 */
  async show(opacity = 1) {
    const ok = await super.show(opacity);
    const { map } = this.runtime;
    for (const id of this.getLayerIds()) {
      if (map.getLayer(id)) map.setLayoutProperty(id, "visibility", "visible");
    }
    this.setMarkerVisible(true);
    this.showLegend();
    return ok;
  }
}
