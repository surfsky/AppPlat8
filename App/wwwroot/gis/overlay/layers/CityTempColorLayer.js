import { MapLayer } from "../MapLayer.js";
import { chunkArray, fetchWithTimeout, findNearestHourlyIndex } from "../utils.js";

/****************************************************************
 * 气温色斑图层全局配置
 ****************************************************************/
export const CityTempColorConfig = Object.freeze({
  /**API 参数 */
  api: {
    baseUrl: "https://api.open-meteo.com/v1/forecast",
    hourlyVar: "temperature_2m",
    timezone: "Asia/Shanghai",
    forecastDays: "1",
    queryNodeLimit: 40,
    queryGroupLimit: 4,
    refreshCron: "*/30 * * * *",
    debounceMsMin: 800,
    debounceMsJitter: 400
  },
  /**原始采样网格 */
  grid: {
    rows: 18,
    cols: 22
  },
  /**画布/图层层叠 */
  canvas: {
    heatmapZIndex: 1.4,
    heatmapOpacity: 0.68,
    maxDpr: 2
  },
  /**渲染 */
  heatmap: {
    cellPx: 8,
    fillAlpha: 0.92,
    /**温度 -> 颜色：冷(−10°C)蓝紫 → 冷蓝 → 常温绿 → 暖黄 → 热橙红(40°C) */
    colorStops: [
      { temp: -10, color: [67, 56, 202] },
      { temp: 0,   color: [14, 116, 144] },
      { temp: 10,  color: [20, 184, 166] },
      { temp: 18,  color: [34, 197, 94] },
      { temp: 26,  color: [234, 179, 8] },
      { temp: 33,  color: [249, 115, 22] },
      { temp: 40,  color: [220, 38, 38] }
    ]
  },
  /**缓存/边界外扩 */
  cache: {
    ttlMs: 10 * 60 * 1000,
    maxEntries: 10
  },
  bounds: {
    lngPadMin: 8,
    lngPadMax: 24,
    lngPadRatio: 0.18,
    latPadMin: 6,
    latPadMax: 18,
    latPadRatio: 0.22
  }
});

/****************************************************************
 * 气温色斑图层（基于气温场的 canvas 色斑渲染）
 ****************************************************************/
export class CityTempColorLayer extends MapLayer {
  static CONFIG = CityTempColorConfig;

  constructor() {
    super({
      name: "cityTempColor",
      title: "气温色斑",
      api: CityTempColorConfig.api.baseUrl,
      refreshCron: CityTempColorConfig.api.refreshCron
    });
    this.heatmapCanvasId = "cityTempColorCanvas";
    this.styleId = "city-temp-color-style";
    this.heatmapCanvas = null;
    this.heatmapCtx = null;
    this.hostEl = null;
    this.dpr = 1;
    this.field = null;
    this.isMapMoving = false;
    this.heatmapDirty = true;
    this.refreshTimer = null;
    this.resizeHandler = () => this.resizeCanvas();
    this.fieldCache = new Map();
  }

  bind(runtime) {
    super.bind(runtime);
    this.ensureCanvas();
    const { map } = runtime;
    map.on("movestart", () => {
      this.isMapMoving = true;
      this.clearHeatmap();
    });
    map.on("moveend", () => {
      this.isMapMoving = false;
      this.heatmapDirty = true;
      if (this.visible) this.debouncedRefresh();
    });
  }

  debouncedRefresh() {
    if (this.refreshTimer) clearTimeout(this.refreshTimer);
    this.refreshTimer = setTimeout(() => {
      this.refresh();
    }, CityTempColorConfig.api.debounceMsMin + Math.random() * CityTempColorConfig.api.debounceMsJitter);
  }

  ensureCanvas() {
    if (this.heatmapCanvas && this.heatmapCtx) return;
    this.ensureStyle();
    const { map } = this.runtime;
    this.hostEl = map?.getContainer?.() || document.body;

    this.heatmapCanvas = document.getElementById(this.heatmapCanvasId);
    if (!this.heatmapCanvas) {
      this.heatmapCanvas = document.createElement("canvas");
      this.heatmapCanvas.id = this.heatmapCanvasId;
      this.heatmapCanvas.setAttribute("aria-hidden", "true");
      this.heatmapCanvas.style.display = "none";
      this.hostEl.appendChild(this.heatmapCanvas);
    }
    this.heatmapCtx = this.heatmapCanvas.getContext("2d", { alpha: true });
    this.ensureCanvasOrder();
    this.resizeCanvas();
    window.addEventListener("resize", this.resizeHandler);
  }

  ensureStyle() {
    if (document.getElementById(this.styleId)) return;
    const style = document.createElement("style");
    style.id = this.styleId;
    const C = CityTempColorConfig.canvas;
    style.textContent = `
      #${this.heatmapCanvasId} {
        position: absolute;
        left: 0;
        top: 0;
        width: 100%;
        height: 100%;
        pointer-events: none;
        z-index: ${C.heatmapZIndex};
        opacity: ${C.heatmapOpacity};
        image-rendering: optimizeQuality;
      }
    `;
    document.head.appendChild(style);
  }

  ensureCanvasOrder() {
    if (!this.heatmapCanvas) return;
    const { map } = this.runtime || {};
    const host = map?.getContainer?.() || this.hostEl || document.body;
    if (this.heatmapCanvas.parentNode !== host) host.appendChild(this.heatmapCanvas);
  }

  clearHeatmap() {
    if (this.heatmapCtx && this.heatmapCanvas) {
      this.heatmapCtx.clearRect(0, 0, this.heatmapCanvas.width, this.heatmapCanvas.height);
    }
  }

  resizeCanvas() {
    if (!this.heatmapCanvas) return;
    const host = this.hostEl || this.runtime?.map?.getContainer?.();
    const rect = host?.getBoundingClientRect?.();
    const cssW = Math.max(1, Math.round(rect?.width || window.innerWidth));
    const cssH = Math.max(1, Math.round(rect?.height || window.innerHeight));
    this.dpr = Math.max(1, Math.min(CityTempColorConfig.canvas.maxDpr, window.devicePixelRatio || 1));
    this.heatmapCanvas.width = Math.max(1, Math.round(cssW * this.dpr));
    this.heatmapCanvas.height = Math.max(1, Math.round(cssH * this.dpr));
    this.heatmapCanvas.style.width = `${cssW}px`;
    this.heatmapCanvas.style.height = `${cssH}px`;
    const cx = this.heatmapCanvas.getContext("2d", { alpha: true });
    if (cx) cx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
    this.heatmapCtx = cx;
    this.heatmapDirty = true;
  }

  getSamplingBounds() {
    const { map } = this.runtime;
    const b = map.getBounds();
    const B = CityTempColorConfig.bounds;
    const west = Math.floor(b.getWest());
    const east = Math.ceil(b.getEast());
    const south = Math.floor(b.getSouth());
    const north = Math.ceil(b.getNorth());
    const lngSpan = Math.max(1, east - west);
    const latSpan = Math.max(1, north - south);
    const lngPad = Math.max(B.lngPadMin, Math.min(B.lngPadMax, Math.round(lngSpan * B.lngPadRatio)));
    const latPad = Math.max(B.latPadMin, Math.min(B.latPadMax, Math.round(latSpan * B.latPadRatio)));
    const clamp = (v, min, max) => Math.max(min, Math.min(max, v));
    return {
      west: clamp(west - lngPad, -180, 180),
      east: clamp(east + lngPad, -180, 180),
      south: clamp(south - latPad, -88, 88),
      north: clamp(north + latPad, -88, 88)
    };
  }

  buildSamplingNodes(bounds) {
    const G = CityTempColorConfig.grid;
    const nodes = [];
    for (let r = 0; r < G.rows; r++) {
      const lat = bounds.north - ((bounds.north - bounds.south) * r / (G.rows - 1));
      for (let c = 0; c < G.cols; c++) {
        const lon = bounds.west + ((bounds.east - bounds.west) * c / (G.cols - 1));
        nodes.push({ row: r, col: c, lat, lon });
      }
    }
    return nodes;
  }

  buildChunkQuery(nodes) {
    const A = CityTempColorConfig.api;
    return new URLSearchParams({
      latitude: nodes.map(n => n.lat.toFixed(1)).join(","),
      longitude: nodes.map(n => n.lon.toFixed(1)).join(","),
      hourly: A.hourlyVar,
      timezone: A.timezone,
      forecast_days: A.forecastDays
    });
  }

  async fetchFieldChunk(nodes) {
    const query = this.buildChunkQuery(nodes);
    const response = await fetchWithTimeout(`${this.api}?${query.toString()}`, {}, 12000);
    const respDate = response.headers.get("last-modified") || response.headers.get("date") || "";
    if (response.status === 429) throw Object.assign(new Error("请求频率过快"), { code: 429 });
    if (response.status === 414) throw Object.assign(new Error("气温请求参数过长"), { code: 414 });
    if (!response.ok) throw new Error(`气温请求失败: ${response.status}`);
    const data = await response.json();
    return {
      list: Array.isArray(data) ? data : [data],
      respDate
    };
  }

  async fetchField() {
    const bounds = this.getSamplingBounds();
    const cacheKey = `${bounds.west},${bounds.east},${bounds.south},${bounds.north}`;
    const C = CityTempColorConfig.cache;

    if (this.fieldCache.has(cacheKey)) {
      const cached = this.fieldCache.get(cacheKey);
      if (Date.now() - cached.timestamp < C.ttlMs) return cached.data;
    }

    const nodes = this.buildSamplingNodes(bounds);
    const nodeChunks = chunkArray(nodes, CityTempColorConfig.api.queryNodeLimit);
    const chunkGroups = chunkArray(nodeChunks, CityTempColorConfig.api.queryGroupLimit);
    const G = CityTempColorConfig.grid;

    try {
      const list = [];
      let respDate = "";
      for (const group of chunkGroups) {
        const results = await Promise.all(group.map(chunk => this.fetchFieldChunk(chunk)));
        for (const result of results) {
          if (!respDate && result.respDate) respDate = result.respDate;
          list.push(...result.list);
        }
      }

      const grid = Array.from({ length: G.rows }, () => Array(G.cols).fill(null));
      let okCount = 0;
      let dataTime = "";
      const varName = CityTempColorConfig.api.hourlyVar;

      for (let i = 0; i < nodes.length; i++) {
        const node = nodes[i];
        const item = list[i] || {};
        const times = Array.isArray(item?.hourly?.time) ? item.hourly.time : [];
        const idx = times.length ? findNearestHourlyIndex(times) : 0;
        if (!dataTime && times.length) dataTime = String(times[idx] || "");
        const temp = Number(item?.hourly?.[varName]?.[idx]);
        if (!Number.isFinite(temp)) continue;
        grid[node.row][node.col] = { ...node, temp };
        okCount += 1;
      }

      if (okCount === 0) throw new Error("气温网格采样为空");

      const result = {
        bounds,
        rows: G.rows,
        cols: G.cols,
        grid,
        okCount,
        totalCount: nodes.length,
        dataTime,
        fetchedAt: Date.now(),
        respDate
      };

      this.fieldCache.set(cacheKey, { timestamp: Date.now(), data: result });
      if (this.fieldCache.size > C.maxEntries) {
        const firstKey = this.fieldCache.keys().next().value;
        this.fieldCache.delete(firstKey);
      }
      return result;
    } catch (e) {
      if (e?.code === 429) {
        console.warn("Open-Meteo 限制请求频率，使用旧气温数据");
        if (this.field) return this.field;
      }
      if (this.field) return this.field;
      throw e;
    }
  }

  formatDataTime(field) {
    const t = field?.dataTime ? String(field.dataTime) : "";
    if (t) {
      const s = t.replace("T", " ");
      return s.length >= 16 ? s.substring(0, 16) : s;
    }
    const d = field?.respDate ? new Date(field.respDate) : (field?.fetchedAt ? new Date(field.fetchedAt) : null);
    if (!d || Number.isNaN(d.getTime())) return "";
    return d.toLocaleString("zh-CN", { hour12: false });
  }

  /**双线性采样气温 */
  bilinearSample(field, rowF, colF) {
    const r0 = Math.floor(rowF);
    const c0 = Math.floor(colF);
    const r1 = Math.min(field.rows - 1, r0 + 1);
    const c1 = Math.min(field.cols - 1, c0 + 1);
    const tr = rowF - r0;
    const tc = colF - c0;

    const p00 = field.grid[r0][c0];
    const p10 = field.grid[r0][c1];
    const p11 = field.grid[r1][c1];
    const p01 = field.grid[r1][c0];
    if (!p00 || !p10 || !p11 || !p01) return Number.NaN;

    const v00 = Number(p00.temp);
    const v10 = Number(p10.temp);
    const v11 = Number(p11.temp);
    const v01 = Number(p01.temp);
    if (![v00, v10, v11, v01].every(Number.isFinite)) return Number.NaN;

    const top = v00 + (v10 - v00) * tc;
    const bottom = v01 + (v11 - v01) * tc;
    return top + (bottom - top) * tr;
  }

  /**温度 -> 颜色；越界/NaN -> 完全透明（地球外不染色） */
  tempToColor(temp, alpha = CityTempColorConfig.heatmap.fillAlpha) {
    const stops = CityTempColorConfig.heatmap.colorStops;
    if (!Number.isFinite(temp)) return "rgba(0,0,0,0)";
    const t = temp;
    if (t <= stops[0].temp) {
      const c = stops[0].color;
      return `rgba(${c[0]},${c[1]},${c[2]},${alpha})`;
    }
    for (let i = 1; i < stops.length; i++) {
      const prev = stops[i - 1];
      const curr = stops[i];
      if (t <= curr.temp) {
        const k = (t - prev.temp) / Math.max(0.001, curr.temp - prev.temp);
        const r = Math.round(prev.color[0] + (curr.color[0] - prev.color[0]) * k);
        const g = Math.round(prev.color[1] + (curr.color[1] - prev.color[1]) * k);
        const b = Math.round(prev.color[2] + (curr.color[2] - prev.color[2]) * k);
        return `rgba(${r},${g},${b},${alpha})`;
      }
    }
    const c = stops[stops.length - 1].color;
    return `rgba(${c[0]},${c[1]},${c[2]},${alpha})`;
  }

  sampleTempAtLngLat(lng, lat) {
    const f = this.field;
    if (!f) return Number.NaN;
    if (lng < f.bounds.west || lng > f.bounds.east || lat < f.bounds.south || lat > f.bounds.north) return Number.NaN;
    const colF = (lng - f.bounds.west) / (f.bounds.east - f.bounds.west) * (f.cols - 1);
    const rowF = (f.bounds.north - lat) / (f.bounds.north - f.bounds.south) * (f.rows - 1);
    return this.bilinearSample(f, rowF, colF);
  }

  /**绘制气温色斑（地球外/网格外 -> 不填色） */
  drawHeatmap() {
    if (!this.heatmapCtx || !this.heatmapCanvas || !this.field || this.isMapMoving) return;
    const H = CityTempColorConfig.heatmap;
    const { map } = this.runtime;
    const cssW = this.heatmapCanvas.width / this.dpr;
    const cssH = this.heatmapCanvas.height / this.dpr;
    const cell = H.cellPx;
    const cols = Math.max(2, Math.ceil(cssW / cell));
    const rows = Math.max(2, Math.ceil(cssH / cell));
    this.heatmapCtx.clearRect(0, 0, this.heatmapCanvas.width, this.heatmapCanvas.height);

    const projectionName = map.getProjection && map.getProjection()?.name;
    const isGlobe = projectionName === "globe";

    for (let r = 0; r < rows; r++) {
      const y = r * cell;
      for (let c = 0; c < cols; c++) {
        const x = c * cell;
        const px = x + cell * 0.5;
        const py = y + cell * 0.5;
        const lnglat = map.unproject([px, py]);
        if (isGlobe) {
          if (!lnglat || !Number.isFinite(lnglat.lng) || !Number.isFinite(lnglat.lat)) continue;
          if (lnglat.lat < -85 || lnglat.lat > 85) continue;
          const reproj = map.project([lnglat.lng, lnglat.lat]);
          const dx = reproj.x - px;
          const dy = reproj.y - py;
          if (dx * dx + dy * dy > cell * cell * 2.25) continue;
        }
        const v = this.sampleTempAtLngLat(lnglat.lng, lnglat.lat);
        if (!Number.isFinite(v)) continue;
        this.heatmapCtx.fillStyle = this.tempToColor(v, H.fillAlpha);
        this.heatmapCtx.fillRect(x, y, cell + 1, cell + 1);
      }
    }
    this.heatmapDirty = false;
  }

  async refresh(force = false) {
    if (!this.runtime) return false;
    this.ensureCanvas();
    this.ensureCanvasOrder();
    try {
      this.field = await this.fetchField();
      this.heatmapDirty = true;
      this.drawHeatmap();
      const timeText = this.formatDataTime(this.field);
      this.setDataTimeText(timeText);
      this.setInfoExtra("");
      this.setOpacity(this.runtime.getOpacity(this.name));
      this.lastStatus = true;
    } catch (e) {
      console.error("刷新气温色斑失败", e);
      const msg = e instanceof Error ? e.message : String(e || "气温色斑数据加载失败");
      this.setLastError(msg || "气温色斑数据加载失败");
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
    if (this.heatmapCanvas) {
      this.heatmapCanvas.style.opacity = String(safe);
    }
    this.opacity = safe;
  }

  hide() {
    super.hide();
    if (this.heatmapCanvas) this.heatmapCanvas.style.display = "none";
    this.clearHeatmap();
    this.clearDataTime();
    this.setInfoExtra("");
    return true;
  }

  async show(opacity = 0.8) {
    this.ensureCanvas();
    this.ensureCanvasOrder();
    if (this.heatmapCanvas) this.heatmapCanvas.style.display = "block";
    const ok = await super.show(opacity);
    this.setOpacity(Number.isFinite(Number(opacity)) ? Number(opacity) : 0.8);
    this.heatmapDirty = true;
    this.drawHeatmap();
    return ok;
  }
}
