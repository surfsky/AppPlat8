import { MapLayer } from "../MapLayer.js";
import { chunkArray, fetchWithTimeout, findNearestHourlyIndex, getTimeSeriesStepSeconds } from "../utils.js";


/****************************************************************
 * 风场图层全局配置（所有可调参数统一收口）
 ****************************************************************/
export const WindConfig = Object.freeze({
  /**风场 API 参数 */
  api: {
    baseUrl: "https://api.open-meteo.com/v1/forecast",
    hourlyVars: "wind_speed_10m,wind_direction_10m",
    timezone: "Asia/Shanghai",
    forecastDays: "1",
    /**单批请求点数量（URL 长度限制） */
    queryNodeLimit: 32,
    /**单批并发组大小 */
    queryGroupLimit: 4,
    /**刷新 cron（与 MapLayer 对齐） */
    refreshCron: "*/30 * * * *",
    /**moveend 后防抖刷新（毫秒） */
    debounceMsMin: 800,
    debounceMsJitter: 400
  },
  /**风场采样网格（原始采样 + 插值基数） */
  grid: {
    rows: 20,
    cols: 24
  },
  /**画布/图层层叠 */
  canvas: {
    particleZIndex: 3,
    particleOpacity: 0.88,
    heatmapZIndex: 2,
    heatmapOpacity: 0.55,
    maxDpr: 2
  },
  /**背景色块（按风速填充蓝→绿） */
  heatmap: {
    cellPx: 6,
    fillAlpha: 0.9,
    colorStops: [
      { speed: 0, color: [30, 64, 175] },
      { speed: 5, color: [14, 116, 144] },
      { speed: 10, color: [6, 182, 212] },
      { speed: 15, color: [34, 197, 94] },
      { speed: 22, color: [22, 163, 74] },
      { speed: 30, color: [21, 128, 61] }
    ]
  },
  /**粒子动画 */
  particles: {
    minCount: 2800,
    maxCount: 6000,
    /**每多少像素面积生成 1 个粒子（面积越大粒子越多） */
    areaDivisor: 260,
    /**粒子生命周期帧数：lifeMin + rand(0, lifeRange) */
    lifeMin: 40,
    lifeRange: 80,
    /**每帧时间步长（越大粒子移动越快，轨迹越长） */
    dt: 0.009,
    /**每帧最小间隔（控制最大帧率） */
    frameIntervalMs: 24,
    /**速度放大系数（在 m/s → 经纬度位移前额外视觉倍率） */
    speedAmp: 1.35,
    /**上限速度（避免台风区粒子飞出屏幕过快），单位 m/s */
    speedCapMs: 13,
    /**粒子抖动噪声幅度 */
    jitter: 0.10,
    /**尾迹渐隐 alpha（越接近 1 尾迹越长） */
    trailFadeAlpha: 0.92,
    lineWidth: 1.2,
    strokeOpacity: 0.9
  },
  /**数据源/缓存 */
  cache: {
    /**字段缓存有效时长（毫秒） */
    ttlMs: 10 * 60 * 1000,
    maxEntries: 10
  },
  /**采样边界外延（让当前视野边缘也有足够粒子/风场数据） */
  bounds: {
    lngPadMin: 12,
    lngPadMax: 32,
    lngPadRatio: 0.18,
    latPadMin: 10,
    latPadMax: 24,
    latPadRatio: 0.22
  }
});


/****************************************************************
 * 风场粒子动画图层
 ****************************************************************/
export class WindLayer extends MapLayer {
  static CONFIG = WindConfig;


  constructor() {
    super({
      name: "wind",
      title: "风场粒子动画",
      api: WindConfig.api.baseUrl,
      refreshCron: WindConfig.api.refreshCron
    });
    this.canvasId = "windCanvas";
    this.heatmapCanvasId = "windHeatmapCanvas";
    this.styleId = "wind-canvas-style";
    this.canvas = null;
    this.ctx = null;
    this.heatmapCanvas = null;
    this.heatmapCtx = null;
    this.particles = [];
    this.animId = null;
    this.field = null;
    this.resizeHandler = () => this.resizeCanvas();
    this.lastFrameAt = 0;
    this.refreshTimer = null;
    this.fieldCache = new Map();
    this.hostEl = null;
    this.dpr = 1;
    this.isMapMoving = false;
    this.heatmapDirty = true;
  }

  bind(runtime) {
    super.bind(runtime);
    this.ensureCanvas();
    const { map } = runtime;
    map.on("movestart", () => {
      this.isMapMoving = true;
      this.clearCanvas();
      this.clearHeatmap();
    });
    map.on("moveend", () => {
      this.isMapMoving = false;
      this.clearCanvas();
      this.heatmapDirty = true;
      if (this.visible) {
        this.debouncedRefresh();
      }
    });
  }

  debouncedRefresh() {
    if (this.refreshTimer) clearTimeout(this.refreshTimer);
    this.refreshTimer = setTimeout(() => {
      this.refresh();
    }, WindConfig.api.debounceMsMin + Math.random() * WindConfig.api.debounceMsJitter);
  }

  /**确保风场画布已创建 */
  ensureCanvas() {
    if (this.canvas && this.ctx && this.heatmapCanvas && this.heatmapCtx) return;
    this.ensureStyle();
    const { map } = this.runtime;
    this.hostEl = map?.getContainer?.() || document.body;

    this.canvas = document.getElementById(this.canvasId);
    if (!this.canvas) {
      this.canvas = document.createElement("canvas");
      this.canvas.id = this.canvasId;
      this.canvas.setAttribute("aria-hidden", "true");
      this.canvas.style.display = "none";
      this.hostEl.appendChild(this.canvas);
    }
    this.ctx = this.canvas.getContext("2d", { alpha: true });

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

  /**确保风场画布位于地图之上、面板之下 */
  ensureCanvasOrder() {
    if (!this.canvas) return;
    const { map } = this.runtime || {};
    const host = map?.getContainer?.() || this.hostEl || document.body;
    if (this.heatmapCanvas && this.heatmapCanvas.parentNode !== host) host.appendChild(this.heatmapCanvas);
    if (this.canvas.parentNode !== host) host.appendChild(this.canvas);
  }

  /**确保风场画布样式已注入 */
  ensureStyle() {
    if (document.getElementById(this.styleId)) return;
    const style = document.createElement("style");
    style.id = this.styleId;
    style.textContent = this.getCanvasStyle();
    document.head.appendChild(style);
  }

  /**构建风场画布样式 */
  getCanvasStyle() {
    const C = WindConfig.canvas;
    return `
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
      #${this.canvasId} {
        position: absolute;
        left: 0;
        top: 0;
        width: 100%;
        height: 100%;
        pointer-events: none;
        z-index: ${C.particleZIndex};
        opacity: ${C.particleOpacity};
        image-rendering: optimizeQuality;
      }
    `;
  }

  clearCanvas() {
    if (this.ctx && this.canvas) this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
  }

  clearHeatmap() {
    if (this.heatmapCtx && this.heatmapCanvas) this.heatmapCtx.clearRect(0, 0, this.heatmapCanvas.width, this.heatmapCanvas.height);
  }

  resizeCanvas() {
    if (!this.canvas) return;
    const host = this.hostEl || this.runtime?.map?.getContainer?.();
    const rect = host?.getBoundingClientRect?.();
    const cssW = Math.max(1, Math.round(rect?.width || window.innerWidth));
    const cssH = Math.max(1, Math.round(rect?.height || window.innerHeight));
    this.dpr = Math.max(1, Math.min(WindConfig.canvas.maxDpr, window.devicePixelRatio || 1));
    for (const c of [this.canvas, this.heatmapCanvas]) {
      if (!c) continue;
      c.width = Math.max(1, Math.round(cssW * this.dpr));
      c.height = Math.max(1, Math.round(cssH * this.dpr));
      c.style.width = `${cssW}px`;
      c.style.height = `${cssH}px`;
      const cx = c.getContext("2d", { alpha: true });
      if (cx) cx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
    }
    this.heatmapDirty = true;
    this.createParticles();
  }

  getSamplingBounds() {
    const { map } = this.runtime;
    const b = map.getBounds();
    const B = WindConfig.bounds;
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
    const G = WindConfig.grid;
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
    const A = WindConfig.api;
    return new URLSearchParams({
      latitude: nodes.map(n => n.lat.toFixed(1)).join(","),
      longitude: nodes.map(n => n.lon.toFixed(1)).join(","),
      hourly: A.hourlyVars,
      timezone: A.timezone,
      forecast_days: A.forecastDays
    });
  }

  async fetchFieldChunk(nodes) {
    const query = this.buildChunkQuery(nodes);
    const response = await fetchWithTimeout(`${this.api}?${query.toString()}`, {}, 12000);
    const respDate = response.headers.get("last-modified") || response.headers.get("date") || "";
    if (response.status === 429) throw Object.assign(new Error("请求频率过快"), { code: 429 });
    if (response.status === 414) throw Object.assign(new Error("风场请求参数过长"), { code: 414 });
    if (!response.ok) throw new Error(`风场请求失败: ${response.status}`);
    const data = await response.json();
    return {
      list: Array.isArray(data) ? data : [data],
      respDate
    };
  }

  async fetchField() {
    const bounds = this.getSamplingBounds();
    const cacheKey = `${bounds.west},${bounds.east},${bounds.south},${bounds.north}`;
    const C = WindConfig.cache;

    if (this.fieldCache.has(cacheKey)) {
      const cached = this.fieldCache.get(cacheKey);
      if (Date.now() - cached.timestamp < C.ttlMs) {
        return cached.data;
      }
    }

    const nodes = this.buildSamplingNodes(bounds);
    const nodeChunks = chunkArray(nodes, WindConfig.api.queryNodeLimit);
    const chunkGroups = chunkArray(nodeChunks, WindConfig.api.queryGroupLimit);
    const G = WindConfig.grid;

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
      let refreshText = this.refreshText;
      const varNames = WindConfig.api.hourlyVars.split(",");

      for (let i = 0; i < nodes.length; i++) {
        const node = nodes[i];
        const item = list[i] || {};
        const times = Array.isArray(item?.hourly?.time) ? item.hourly.time : [];
        const idx = times.length ? findNearestHourlyIndex(times) : 0;
        if (!dataTime && times.length) dataTime = String(times[idx] || "");
        const speedKmh = Number(item?.hourly?.[varNames[0]]?.[idx]);
        const dir = Number(item?.hourly?.[varNames[1]]?.[idx]);
        if (!Number.isFinite(speedKmh) || !Number.isFinite(dir)) continue;

        const vec = this.vectorFromSpeedDir(speedKmh, dir);
        grid[node.row][node.col] = { ...node, u: vec.u, v: vec.v, speed: vec.speed };
        okCount += 1;
      }

      if (okCount === 0) throw new Error("风场网格采样为空");

      const result = {
        bounds,
        rows: G.rows,
        cols: G.cols,
        grid,
        okCount,
        totalCount: nodes.length,
        dataTime,
        refreshText,
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
        console.warn("Open-Meteo 限制请求频率，使用旧数据");
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

  bilinearSample(field, rowF, colF, key) {
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

    const v00 = Number(p00[key]);
    const v10 = Number(p10[key]);
    const v11 = Number(p11[key]);
    const v01 = Number(p01[key]);
    if (![v00, v10, v11, v01].every(Number.isFinite)) return Number.NaN;

    const top = v00 + (v10 - v00) * tc;
    const bottom = v01 + (v11 - v01) * tc;
    return top + (bottom - top) * tr;
  }

  vectorFromSpeedDir(speed, dirDeg) {
    const P = WindConfig.particles;
    const speedMsRaw = Number.isFinite(speed) ? speed / 3.6 : 0;
    const speedMs = Math.max(0, Math.min(P.speedCapMs, speedMsRaw * P.speedAmp));
    const rad = (dirDeg * Math.PI) / 180;
    const toRad = rad + Math.PI;
    return {
      u: Math.sin(toRad) * speedMs,
      v: Math.cos(toRad) * speedMs,
      speed: speedMs
    };
  }

  sampleWindAtLngLat(lng, lat) {
    const f = this.field;
    if (!f) return null;
    if (lng < f.bounds.west || lng > f.bounds.east || lat < f.bounds.south || lat > f.bounds.north) return null;

    const colF = (lng - f.bounds.west) / (f.bounds.east - f.bounds.west) * (f.cols - 1);
    const rowF = (f.bounds.north - lat) / (f.bounds.north - f.bounds.south) * (f.rows - 1);

    const u = this.bilinearSample(f, rowF, colF, "u");
    const v = this.bilinearSample(f, rowF, colF, "v");
    const speed = this.bilinearSample(f, rowF, colF, "speed");

    if (!Number.isFinite(u) || !Number.isFinite(v)) return null;
    return { u, v, speed: Number.isFinite(speed) ? speed : Math.hypot(u, v) };
  }

  /**速度 -> RGBA 插值色；NaN/非有限值 -> 完全透明（地球外不染色） */
  speedToColor(speed, alpha = WindConfig.heatmap.fillAlpha) {
    if (!Number.isFinite(speed)) return "rgba(0,0,0,0)";
    const stops = WindConfig.heatmap.colorStops;
    const s = Math.max(0, speed);
    if (s <= stops[0].speed) {
      const c = stops[0].color;
      return `rgba(${c[0]},${c[1]},${c[2]},${alpha})`;
    }
    for (let i = 1; i < stops.length; i++) {
      const prev = stops[i - 1];
      const curr = stops[i];
      if (s <= curr.speed) {
        const t = (s - prev.speed) / Math.max(0.001, curr.speed - prev.speed);
        const r = Math.round(prev.color[0] + (curr.color[0] - prev.color[0]) * t);
        const g = Math.round(prev.color[1] + (curr.color[1] - prev.color[1]) * t);
        const b = Math.round(prev.color[2] + (curr.color[2] - prev.color[2]) * t);
        return `rgba(${r},${g},${b},${alpha})`;
      }
    }
    const c = stops[stops.length - 1].color;
    return `rgba(${c[0]},${c[1]},${c[2]},${alpha})`;
  }

  /**绘制热力图底色（画布降采样提升性能；地球外 -> 不填色） */
  drawHeatmap() {
    if (!this.heatmapCtx || !this.heatmapCanvas || !this.field || this.isMapMoving) return;
    const H = WindConfig.heatmap;
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
        const v = this.sampleWindAtLngLat(lnglat.lng, lnglat.lat);
        if (!v || !Number.isFinite(v.speed)) continue;
        this.heatmapCtx.fillStyle = this.speedToColor(v.speed, H.fillAlpha);
        this.heatmapCtx.fillRect(x, y, cell + 1, cell + 1);
      }
    }
    this.heatmapDirty = false;
  }

  createParticles() {
    if (!this.canvas) return;
    const P = WindConfig.particles;
    const bounds = this.getParticleBounds();
    const cssW = this.canvas.width / this.dpr;
    const cssH = this.canvas.height / this.dpr;
    const area = cssW * cssH;
    const count = Math.max(P.minCount, Math.min(P.maxCount, Math.floor(area / P.areaDivisor)));
    this.particles = [];
    for (let i = 0; i < count; i++) {
      this.particles.push(this.initParticle(bounds));
    }
  }

  getParticleBounds() {
    const fieldBounds = this.field?.bounds;
    if (fieldBounds) {
      return {
        getWest: () => fieldBounds.west,
        getEast: () => fieldBounds.east,
        getSouth: () => fieldBounds.south,
        getNorth: () => fieldBounds.north
      };
    }
    return this.runtime.map.getBounds();
  }

  initParticle(bounds) {
    const P = WindConfig.particles;
    return {
      lng: bounds.getWest() + Math.random() * (bounds.getEast() - bounds.getWest()),
      lat: bounds.getSouth() + Math.random() * (bounds.getNorth() - bounds.getSouth()),
      life: P.lifeMin + Math.random() * P.lifeRange,
      age: 0
    };
  }

  resetParticle(p, bounds) {
    const next = this.initParticle(bounds);
    Object.assign(p, next);
  }

  drawFrame(now = 0) {
    if (!this.visible || !this.ctx || !this.canvas) return;
    const P = WindConfig.particles;
    const cssW = this.canvas.width / this.dpr;
    const cssH = this.canvas.height / this.dpr;

    if (this.heatmapDirty) this.drawHeatmap();

    if (this.isMapMoving) {
      this.clearCanvas();
    } else {
      this.ctx.globalCompositeOperation = "destination-in";
      this.ctx.fillStyle = `rgba(0, 0, 0, ${P.trailFadeAlpha})`;
      this.ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);
      this.ctx.globalCompositeOperation = "source-over";
    }

    if (now - this.lastFrameAt < P.frameIntervalMs) {
      this.animId = requestAnimationFrame(t => this.drawFrame(t));
      return;
    }
    this.lastFrameAt = now;

    const { map } = this.runtime;
    const opacity = this.runtime.getOpacity(this.name);
    const bounds = this.getParticleBounds();
    const dt = P.dt;

    this.ctx.lineWidth = P.lineWidth;
    this.ctx.lineCap = "round";
    this.ctx.strokeStyle = `rgba(255, 255, 255, ${P.strokeOpacity * opacity})`;

    for (const p of this.particles) {
      const pos = map.project([p.lng, p.lat]);
      const v = this.sampleWindAtLngLat(p.lng, p.lat);
      if (!v || p.age > p.life) {
        this.resetParticle(p, bounds);
        continue;
      }

      const noise = (Math.random() - 0.5) * P.jitter;
      const u = v.u + noise;
      const v_noise = v.v + noise;

      const latRad = p.lat * Math.PI / 180;
      const dLat = (v_noise * dt);
      const cosLat = Math.max(0.2, Math.abs(Math.cos(latRad)));
      const dLng = (u * dt) / cosLat;

      const nextLng = p.lng + dLng;
      const nextLat = Math.max(-88, Math.min(88, p.lat + dLat));
      const nextPos = map.project([nextLng, nextLat]);

      if (pos.x < 0 || pos.x > cssW || pos.y < 0 || pos.y > cssH) {
        this.resetParticle(p, bounds);
        continue;
      }

      this.ctx.beginPath();
      this.ctx.moveTo(pos.x, pos.y);
      this.ctx.lineTo(nextPos.x, nextPos.y);
      this.ctx.stroke();

      p.lng = nextLng;
      p.lat = nextLat;
      p.age++;
    }

    this.animId = requestAnimationFrame(t => this.drawFrame(t));
  }

  startAnimation() {
    this.ensureCanvas();
    if (!this.particles.length) this.createParticles();
    this.lastFrameAt = 0;
    if (!this.animId) this.animId = requestAnimationFrame(t => this.drawFrame(t));
  }

  stopAnimation() {
    if (this.animId) {
      cancelAnimationFrame(this.animId);
      this.animId = null;
    }
    if (this.ctx && this.canvas) this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
  }

  async refresh() {
    this.ensureCanvas();
    this.ensureCanvasOrder();
    try {
      this.field = await this.fetchField();
      this.createParticles();
      this.heatmapDirty = true;
      this.startAnimation();
      const timeText = this.formatDataTime(this.field);
      this.setDataTimeText(timeText);
      this.setInfoExtra("");
      this.lastStatus = true;
    } catch (e) {
      console.error("刷新风场失败", e);
      const msg = e instanceof Error ? e.message : String(e || "风场数据加载失败");
      this.setLastError(msg || "风场数据加载失败");
      this.clearDataTime();
      this.setInfoExtra("");
      this.lastStatus = false;
      return false;
    }
    this.lastTime = Date.now();
    return true;
  }

  setOpacity(opacity) {
    if (this.canvas) this.canvas.style.opacity = String(opacity);
  }

  hide() {
    super.hide();
    this.stopAnimation();
    if (this.canvas) this.canvas.style.display = "none";
    if (this.heatmapCanvas) this.heatmapCanvas.style.display = "none";
    this.clearDataTime();
    this.setInfoExtra("");
    return true;
  }

  async show(opacity = 1) {
    this.ensureCanvas();
    this.ensureCanvasOrder();
    if (this.canvas) this.canvas.style.display = "block";
    if (this.heatmapCanvas) this.heatmapCanvas.style.display = "block";
    const ok = await super.show(opacity);
    this.setOpacity(opacity);
    this.heatmapDirty = true;
    this.startAnimation();
    return ok;
  }
}
