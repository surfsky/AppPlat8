import { MapLayer } from "../core/MapLayer.js";
import { chunkArray, fetchWithTimeout } from "../core/utils.js";


/****************************************************************
 * 风场粒子动画图层
 ****************************************************************/
export class WindLayer extends MapLayer {
  static GRID = {
    rows: 20,
    cols: 24
  };
  static QUERY_NODE_LIMIT = 32;
  static QUERY_GROUP_LIMIT = 4;


  constructor() {
    super({
      name: "wind",
      title: "风场粒子动画",
      api: "https://api.open-meteo.com/v1/forecast",
      refreshSeconds: 360
    });
    this.canvasId = "windCanvas";
    this.styleId = "wind-canvas-style";
    this.canvas = null;
    this.ctx = null;
    this.particles = [];
    this.animId = null;
    this.field = null;
    this.resizeHandler = () => this.resizeCanvas();
    this.lastFrameAt = 0;
    this.refreshTimer = null;
    this.fieldCache = new Map(); // 简单的内存缓存，key 为经纬度边界的字符串
    this.hostEl = null;
    this.dpr = 1;
    this.isMapMoving = false;
  }

  bind(runtime) {
    super.bind(runtime);
    this.ensureCanvas();
    const { map } = runtime;
    map.on("movestart", () => {
      this.isMapMoving = true;
      this.clearCanvas();
    });
    map.on("moveend", () => {
      this.isMapMoving = false;
      this.clearCanvas();
      if (this.visible) {
        this.debouncedRefresh();
      }
    });
  }

  debouncedRefresh() {
    if (this.refreshTimer) clearTimeout(this.refreshTimer);
    this.refreshTimer = setTimeout(() => {
      this.refresh();
    }, 800 + Math.random() * 400); // 使用随机偏移量，避免与其它图层同时请求
  }

  /**确保风场画布已创建 */
  ensureCanvas() {
    if (this.canvas && this.ctx) return;
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
    this.ensureCanvasOrder();
    this.resizeCanvas();
    window.addEventListener("resize", this.resizeHandler);
  }

  /**确保风场画布位于地图之上、面板之下 */
  ensureCanvasOrder() {
    if (!this.canvas) return;
    const { map } = this.runtime || {};
    const host = map?.getContainer?.() || this.hostEl || document.body;
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
    return `
      #${this.canvasId} {
        position: absolute;
        left: 0;
        top: 0;
        width: 100%;
        height: 100%;
        pointer-events: none;
        z-index: 3;
        opacity: 0.8;
        image-rendering: optimizeQuality;
      }
    `;
  }

  clearCanvas() {
    if (this.ctx && this.canvas) this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
  }

  resizeCanvas() {
    if (!this.canvas) return;
    const host = this.hostEl || this.runtime?.map?.getContainer?.();
    const rect = host?.getBoundingClientRect?.();
    const cssW = Math.max(1, Math.round(rect?.width || window.innerWidth));
    const cssH = Math.max(1, Math.round(rect?.height || window.innerHeight));
    this.dpr = Math.max(1, Math.min(2, window.devicePixelRatio || 1));
    this.canvas.width = Math.max(1, Math.round(cssW * this.dpr));
    this.canvas.height = Math.max(1, Math.round(cssH * this.dpr));
    this.canvas.style.width = `${cssW}px`;
    this.canvas.style.height = `${cssH}px`;
    if (this.ctx) this.ctx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
    this.createParticles(); // 窗口大小改变时重置粒子
  }

  getSamplingBounds() {
    const { map } = this.runtime;
    const b = map.getBounds();
    // 使用整数边界作为缓存 key 的一部分，减少微小移动导致的重复请求
    const west = Math.floor(b.getWest());
    const east = Math.ceil(b.getEast());
    const south = Math.floor(b.getSouth());
    const north = Math.ceil(b.getNorth());
    const lngSpan = Math.max(1, east - west);
    const latSpan = Math.max(1, north - south);
    const lngPad = Math.max(12, Math.min(32, Math.round(lngSpan * 0.18)));
    const latPad = Math.max(10, Math.min(24, Math.round(latSpan * 0.22)));

    const clamp = (v, min, max) => Math.max(min, Math.min(max, v));
    return {
      west: clamp(west - lngPad, -180, 180),
      east: clamp(east + lngPad, -180, 180),
      south: clamp(south - latPad, -88, 88),
      north: clamp(north + latPad, -88, 88)
    };
  }

  findNearestHourlyIndex(times) {
    const now = Date.now();
    let bestIdx = 0;
    let bestGap = Infinity;
    for (let i = 0; i < times.length; i++) {
      const t = new Date(times[i]).getTime();
      const gap = Math.abs(t - now);
      if (gap < bestGap) {
        bestGap = gap;
        bestIdx = i;
      }
    }
    return bestIdx;
  }

  buildSamplingNodes(bounds) {
    const nodes = [];
    for (let r = 0; r < WindLayer.GRID.rows; r++) {
      const lat = bounds.north - ((bounds.north - bounds.south) * r / (WindLayer.GRID.rows - 1));
      for (let c = 0; c < WindLayer.GRID.cols; c++) {
        const lon = bounds.west + ((bounds.east - bounds.west) * c / (WindLayer.GRID.cols - 1));
        nodes.push({ row: r, col: c, lat, lon });
      }
    }
    return nodes;
  }

  buildChunkQuery(nodes) {
    return new URLSearchParams({
      latitude: nodes.map(n => n.lat.toFixed(1)).join(","),
      longitude: nodes.map(n => n.lon.toFixed(1)).join(","),
      hourly: "wind_speed_10m,wind_direction_10m",
      timezone: "Asia/Shanghai",
      forecast_days: "1"
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
    
    // 检查缓存
    if (this.fieldCache.has(cacheKey)) {
      const cached = this.fieldCache.get(cacheKey);
      if (Date.now() - cached.timestamp < 10 * 60 * 1000) { // 10分钟有效
        return cached.data;
      }
    }

    const nodes = this.buildSamplingNodes(bounds);
    const nodeChunks = chunkArray(nodes, WindLayer.QUERY_NODE_LIMIT);
    const chunkGroups = chunkArray(nodeChunks, WindLayer.QUERY_GROUP_LIMIT);

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

      const grid = Array.from({ length: WindLayer.GRID.rows }, () => Array(WindLayer.GRID.cols).fill(null));
      let okCount = 0;
      let dataTime = "";

      for (let i = 0; i < nodes.length; i++) {
        const node = nodes[i];
        const item = list[i] || {};
        const times = Array.isArray(item?.hourly?.time) ? item.hourly.time : [];
        const idx = times.length ? this.findNearestHourlyIndex(times) : 0;
        if (!dataTime && times.length) dataTime = String(times[idx] || "");
        const speed = Number(item?.hourly?.wind_speed_10m?.[idx]);
        const dir = Number(item?.hourly?.wind_direction_10m?.[idx]);
        if (!Number.isFinite(speed) || !Number.isFinite(dir)) continue;

        // 核心优化：在网格点直接预计算 U/V 分量
        // 绝对不能直接插值角度，否则在 0/360 度交界处会出现严重的汇聚和错误方向
        const vec = this.vectorFromSpeedDir(speed, dir);
        grid[node.row][node.col] = { ...node, u: vec.u, v: vec.v };
        okCount += 1;
      }

      if (okCount === 0) throw new Error("风场网格采样为空");
      
      const result = {
        bounds,
        rows: WindLayer.GRID.rows,
        cols: WindLayer.GRID.cols,
        grid,
        okCount,
        totalCount: nodes.length,
        dataTime,
        fetchedAt: Date.now(),
        respDate
      };

      // 写入缓存
      this.fieldCache.set(cacheKey, { timestamp: Date.now(), data: result });
      // 清理旧缓存
      if (this.fieldCache.size > 10) {
        const firstKey = this.fieldCache.keys().next().value;
        this.fieldCache.delete(firstKey);
      }

      return result;
    } catch (e) {
      if (e?.code === 429) {
        console.warn("Open-Meteo 限制请求频率，使用旧数据");
        if (this.field) return this.field;
      }
      if (this.field) return this.field; // 报错则降级使用旧数据
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
    const speedMs = Math.max(0, Math.min(18, speed / 3.6));
    const rad = (dirDeg * Math.PI) / 180;
    const toRad = rad + Math.PI;
    return {
      u: Math.sin(toRad) * speedMs,
      v: Math.cos(toRad) * speedMs
    };
  }

  sampleWindAtLngLat(lng, lat) {
    const f = this.field;
    if (!f) return null;
    if (lng < f.bounds.west || lng > f.bounds.east || lat < f.bounds.south || lat > f.bounds.north) return null;

    const colF = (lng - f.bounds.west) / (f.bounds.east - f.bounds.west) * (f.cols - 1);
    const rowF = (f.bounds.north - lat) / (f.bounds.north - f.bounds.south) * (f.rows - 1);
    
    // 对 U 和 V 分量分别进行双线性插值
    const u = this.bilinearSample(f, rowF, colF, "u");
    const v = this.bilinearSample(f, rowF, colF, "v");
    
    if (!Number.isFinite(u) || !Number.isFinite(v)) return null;
    return { u, v };
  }

  createParticles() {
    if (!this.canvas) return;
    const bounds = this.getParticleBounds();
    const cssW = this.canvas.width / this.dpr;
    const cssH = this.canvas.height / this.dpr;
    const area = cssW * cssH;
    // 覆盖范围扩大后，适度降低粒子密度避免过于拥挤。
    const count = Math.max(1400, Math.min(3000, Math.floor(area / 900)));
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
    return {
      lng: bounds.getWest() + Math.random() * (bounds.getEast() - bounds.getWest()),
      lat: bounds.getSouth() + Math.random() * (bounds.getNorth() - bounds.getSouth()),
      life: 20 + Math.random() * 80,
      age: 0
    };
  }

  resetParticle(p, bounds) {
    const next = this.initParticle(bounds);
    Object.assign(p, next);
  }

  drawFrame(now = 0) {
    if (!this.visible || !this.ctx || !this.canvas) return;
    const cssW = this.canvas.width / this.dpr;
    const cssH = this.canvas.height / this.dpr;
    
    if (this.isMapMoving) {
      // 地图拖动时不保留残影，避免尾迹与底图相对滑动造成“模糊拖花”。
      this.clearCanvas();
    } else {
      // Windy 风格：使用 destination-in 实现渐隐尾迹
      this.ctx.globalCompositeOperation = "destination-in";
      this.ctx.fillStyle = "rgba(0, 0, 0, 0.97)";
      this.ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);
      this.ctx.globalCompositeOperation = "source-over";
    }

    if (now - this.lastFrameAt < 30) {
      this.animId = requestAnimationFrame(t => this.drawFrame(t));
      return;
    }
    this.lastFrameAt = now;

    const { map } = this.runtime;
    const opacity = this.runtime.getOpacity(this.name);
    const bounds = this.getParticleBounds();
    
    // 设置绘图样式
    this.ctx.lineWidth = 1.1;
    this.ctx.lineCap = "round";
    this.ctx.strokeStyle = `rgba(255, 255, 255, ${0.85 * opacity})`;

    for (const p of this.particles) {
      // 1. 获取当前位置的屏幕坐标用于绘图
      const pos = map.project([p.lng, p.lat]);
      
      // 2. 采样风场
      const v = this.sampleWindAtLngLat(p.lng, p.lat);
      if (!v || p.age > p.life) {
        this.resetParticle(p, bounds);
        continue;
      }

      // 3. 计算地理空间位移 (物理准确性：将 m/s 转换为 经纬度变化)
      const dt = 0.004; // 时间步长
      
      // 引入微小的运动随机性（Jitter），防止粒子完美对齐导致汇聚
      const noise = (Math.random() - 0.5) * 0.15;
      const u = v.u + noise;
      const v_noise = v.v + noise;

      const latRad = p.lat * Math.PI / 180;
      const dLat = (v_noise * dt); 
      const cosLat = Math.max(0.2, Math.abs(Math.cos(latRad)));
      const dLng = (u * dt) / cosLat;

      const nextLng = p.lng + dLng;
      const nextLat = Math.max(-88, Math.min(88, p.lat + dLat));
      
      // 4. 获取下一位置的屏幕坐标
      const nextPos = map.project([nextLng, nextLat]);

      // 5. 检查是否在屏幕内且移动有效
      if (pos.x < 0 || pos.x > cssW || pos.y < 0 || pos.y > cssH) {
        this.resetParticle(p, bounds);
        continue;
      }

      // 6. 绘图：只绘制当前这一小段位移
      this.ctx.beginPath();
      this.ctx.moveTo(pos.x, pos.y);
      this.ctx.lineTo(nextPos.x, nextPos.y);
      this.ctx.stroke();

      // 7. 更新粒子地理位置
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
      this.startAnimation();
      const timeText = this.formatDataTime(this.field);
      this.setDataTimeText(timeText);
      this.setInfoExtra("");
      this.lastStatus = true;
    } catch (e) {
      console.error("刷新风场失败", e);
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
    this.clearDataTime();
    this.setInfoExtra("");
    return true;
  }

  async show(opacity = 1) {
    this.ensureCanvas();
    this.ensureCanvasOrder();
    if (this.canvas) this.canvas.style.display = "block";
    const ok = await super.show(opacity);
    this.setOpacity(opacity);
    this.startAnimation();
    return ok;
  }
}
