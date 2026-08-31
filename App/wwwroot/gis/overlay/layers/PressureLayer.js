import { MapLayer } from "../MapLayer.js";
import { addOrUpdateGeoJsonSource, fetchWithTimeout, findNearestHourlyIndex, getTimeSeriesStepSeconds } from "../utils.js";





/****************************************************************
 * 气压等压线图层
 ****************************************************************/
export const PressureConfig = Object.freeze({
  /**色斑画布/图层 */
  canvas: {
    heatmapZIndex: 1.5,
    heatmapOpacity: 0.6,
    maxDpr: 2
  },
  /**色斑渲染 */
  heatmap: {
    cellPx: 8,
    fillAlpha: 0.58,
    /**气压 -> 颜色：低压(965hPa)深蓝紫 → 中压青/绿 → 高压(1045hPa)亮橙红（高饱和+高对比） */
    colorStops: [
      { hpa: 965, color: [67, 20, 150] },
      { hpa: 980, color: [59, 73, 223] },
      { hpa: 992, color: [14, 165, 233] },
      { hpa: 1000, color: [34, 197, 194] },
      { hpa: 1008, color: [163, 230, 53] },
      { hpa: 1016, color: [250, 204, 21] },
      { hpa: 1024, color: [249, 115, 22] },
      { hpa: 1032, color: [239, 68, 68] },
      { hpa: 1045, color: [185, 28, 28] }
    ]
  },
  /**采样平滑倍率 */
  smooth: {
    factorZoom4: 5,
    factorZoom5: 4,
    factorZoom6: 3,
    factorOther: 2
  }
});

export class PressureLayer extends MapLayer {
  static GRID = {
    rows: 12,
    cols: 16,
    contourStep: 2,
    smoothFactor: 5
  };

  constructor() {
    super({
      name: "pressure",
      title: "气压等压线",
      api: "https://api.open-meteo.com/v1/forecast",
      refreshCron: "*/30 * * * *"
    });
    this.contourSourceId = "pressure-contour-source";
    this.labelSourceId = "pressure-label-source";
    this.contourLayerId = "pressure-contour-layer";
    this.labelLayerId = "pressure-label-layer";
    this.heatmapCanvasId = "pressureHeatmapCanvas";
    this.styleId = "pressure-canvas-style";
    this.heatmapCanvas = null;
    this.heatmapCtx = null;
    this.hostEl = null;
    this.dpr = 1;
    this.isMapMoving = false;
    this.heatmapDirty = true;
    this.resizeHandler = () => this.resizeCanvas();
    this.field = null;
    
    // 原始采样缓存
    this.rawCache = {
      data: null,
      bounds: null,
      timestamp: 0
    };
  }

  bind(runtime) {
    super.bind(runtime);
    const { map } = runtime;
    map.on("movestart", () => {
      this.isMapMoving = true;
      this.clearHeatmap();
    });
    map.on("moveend", () => {
      this.isMapMoving = false;
      this.heatmapDirty = true;
      if (this.visible) {
        this.ensureCanvas();
        this.drawHeatmap();
      }
    });
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
    const C = PressureConfig.canvas;
    const cssText = `
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
    let style = document.getElementById(this.styleId);
    if (style) {
      style.textContent = cssText;
      return;
    }
    style = document.createElement("style");
    style.id = this.styleId;
    style.textContent = cssText;
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
    this.dpr = Math.max(1, Math.min(PressureConfig.canvas.maxDpr, window.devicePixelRatio || 1));
    this.heatmapCanvas.width = Math.max(1, Math.round(cssW * this.dpr));
    this.heatmapCanvas.height = Math.max(1, Math.round(cssH * this.dpr));
    this.heatmapCanvas.style.width = `${cssW}px`;
    this.heatmapCanvas.style.height = `${cssH}px`;
    const cx = this.heatmapCanvas.getContext("2d", { alpha: true });
    if (cx) cx.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
    this.heatmapCtx = cx;
    this.heatmapDirty = true;
  }

  /**气压值 -> 颜色；越界/NaN -> 完全透明（地球外不染色） */
  pressureToColor(hpa, alpha = PressureConfig.heatmap.fillAlpha) {
    const stops = PressureConfig.heatmap.colorStops;
    if (!Number.isFinite(hpa)) return "rgba(0,0,0,0)";
    const p = hpa;
    if (p <= stops[0].hpa) {
      const c = stops[0].color;
      return `rgba(${c[0]},${c[1]},${c[2]},${alpha})`;
    }
    for (let i = 1; i < stops.length; i++) {
      const prev = stops[i - 1];
      const curr = stops[i];
      if (p <= curr.hpa) {
        const t = (p - prev.hpa) / Math.max(0.01, curr.hpa - prev.hpa);
        const r = Math.round(prev.color[0] + (curr.color[0] - prev.color[0]) * t);
        const g = Math.round(prev.color[1] + (curr.color[1] - prev.color[1]) * t);
        const b = Math.round(prev.color[2] + (curr.color[2] - prev.color[2]) * t);
        return `rgba(${r},${g},${b},${alpha})`;
      }
    }
    const c = stops[stops.length - 1].color;
    return `rgba(${c[0]},${c[1]},${c[2]},${alpha})`;
  }

  /**在插值网格中采样经纬度气压 */
  samplePressureAtLngLat(lng, lat, smoothedField) {
    const f = smoothedField || this.field;
    if (!f) return Number.NaN;
    if (lng < f.bounds.west || lng > f.bounds.east || lat < f.bounds.south || lat > f.bounds.north) return Number.NaN;
    const colF = (lng - f.bounds.west) / (f.bounds.east - f.bounds.west) * (f.cols - 1);
    const rowF = (f.bounds.north - lat) / (f.bounds.north - f.bounds.south) * (f.rows - 1);
    return this.bilinearSample(f, rowF, colF);
  }

  /**绘制气压色斑（地球外/插值网格外 -> 不填色） */
  drawHeatmap(smoothedField) {
    if (!this.heatmapCtx || !this.heatmapCanvas || this.isMapMoving) return;
    const useField = smoothedField || this.field;
    if (!useField) return;
    const H = PressureConfig.heatmap;
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
        // 地球投影：非法经纬度 -> 跳过（避免地球外黑色背景染色）
        if (isGlobe) {
          if (!lnglat || !Number.isFinite(lnglat.lng) || !Number.isFinite(lnglat.lat)) continue;
          if (lnglat.lat < -85 || lnglat.lat > 85) continue;
          // Globe 下：通过 project 反向验证该经纬度是否落在可视球面内
          const reproj = map.project([lnglat.lng, lnglat.lat]);
          const dx = reproj.x - px;
          const dy = reproj.y - py;
          if (dx * dx + dy * dy > cell * cell * 2.25) continue;
        }
        const p = this.samplePressureAtLngLat(lnglat.lng, lnglat.lat, useField);
        if (!Number.isFinite(p)) continue;
        this.heatmapCtx.fillStyle = this.pressureToColor(p, H.fillAlpha);
        this.heatmapCtx.fillRect(x, y, cell + 1, cell + 1);
      }
    }
    this.heatmapDirty = false;
  }

  getSamplingBounds() {
    const { map } = this.runtime;
    const projectionName = map.getProjection && map.getProjection()?.name;
    if (projectionName === "globe") {
      return {
        west: -180,
        east: 180,
        south: -89.5,
        north: 89.5
      };
    }

    const b = map.getBounds();
    const w = b.getWest();
    const e = b.getEast();
    const s = b.getSouth();
    const n = b.getNorth();
    
    // 增加 10% 的缓冲区，确保边缘线段不被截断
    const dw = (e - w) * 0.1;
    const dh = (n - s) * 0.1;
    
    const clamp = (v, min, max) => Math.max(min, Math.min(max, v));
    return {
      west: clamp(w - dw, -180, 180),
      east: clamp(e + dw, -180, 180),
      south: clamp(s - dh, -89.5, 89.5),
      north: clamp(n + dh, -89.5, 89.5)
    };
  }

  buildSamplingNodes(bounds) {
    const nodes = [];
    for (let r = 0; r < PressureLayer.GRID.rows; r++) {
      const lat = bounds.north - ((bounds.north - bounds.south) * r / (PressureLayer.GRID.rows - 1));
      for (let c = 0; c < PressureLayer.GRID.cols; c++) {
        const lon = bounds.west + ((bounds.east - bounds.west) * c / (PressureLayer.GRID.cols - 1));
        nodes.push({ 
          row: r, 
          col: c, 
          lat: Number(lat.toFixed(2)), 
          lon: Number(lon.toFixed(2)) 
        });
      }
    }
    return nodes;
  }

  /**获取原始采样场 */
  async fetchField() {
    const currentBounds = this.getSamplingBounds();
    const now = Date.now();

    // 范围变化不大时复用采样，缩放变化也允许重建等压线
    if (this.rawCache.data && (now - this.rawCache.timestamp < 300000)) {
      const b = this.rawCache.bounds;
      const latDiff = Math.abs(currentBounds.north - b.north) + Math.abs(currentBounds.south - b.south);
      const lonDiff = Math.abs(currentBounds.east - b.east) + Math.abs(currentBounds.west - b.west);

      const latRange = Math.max(1, currentBounds.north - currentBounds.south);
      const lonRange = Math.max(1, currentBounds.east - currentBounds.west);
      if (latDiff < latRange * 0.12 && lonDiff < lonRange * 0.12) {
        return this.rawCache.data;
      }
    }

    const nodes = this.buildSamplingNodes(currentBounds);
    const query = new URLSearchParams({
      latitude: nodes.map(n => n.lat).join(","),
      longitude: nodes.map(n => n.lon).join(","),
      hourly: "pressure_msl",
      timezone: "Asia/Shanghai",
      forecast_days: "1"
    });

    const response = await fetchWithTimeout(`${this.api}?${query.toString()}`, {}, 12000);
    if (!response.ok) {
      if (response.status === 429 && this.rawCache.data) {
        console.warn("PressureLayer: 429 hit, falling back to cache");
        return this.rawCache.data;
      }
      throw new Error(`气压请求失败: ${response.status}`);
    }
    
    const data = await response.json();
    const list = Array.isArray(data) ? data : [data];
    const grid = Array.from({ length: PressureLayer.GRID.rows }, () => Array(PressureLayer.GRID.cols).fill(null));
    let okCount = 0;
    let sampleTime = null;
    let refreshText = this.refreshText;

    for (let i = 0; i < nodes.length; i++) {
      const node = nodes[i];
      const item = list[i] || {};
      const times = Array.isArray(item?.hourly?.time) ? item.hourly.time : [];
      const values = Array.isArray(item?.hourly?.pressure_msl) ? item.hourly.pressure_msl : [];
      const idx = times.length > 1 ? findNearestHourlyIndex(times) : 0;
      const p = Number(values[idx]);
      if (!Number.isFinite(p)) continue;
      if (!sampleTime && times[idx]) sampleTime = times[idx];
      grid[node.row][node.col] = { ...node, pressure: p };
      okCount += 1;
    }

    if (okCount === 0) throw new Error("气压网格采样为空");
    
    const result = {
      bounds: currentBounds,
      rows: PressureLayer.GRID.rows,
      cols: PressureLayer.GRID.cols,
      grid,
      sampleTime,
      refreshText,
      okCount,
      totalCount: nodes.length
    };

    this.rawCache = {
      data: result,
      bounds: currentBounds,
      timestamp: now
    };

    return result;
  }

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

    const v00 = Number(p00.pressure);
    const v10 = Number(p10.pressure);
    const v11 = Number(p11.pressure);
    const v01 = Number(p01.pressure);
    if (![v00, v10, v11, v01].every(Number.isFinite)) return Number.NaN;

    const top = v00 + (v10 - v00) * tc;
    const bottom = v01 + (v11 - v01) * tc;
    return top + (bottom - top) * tr;
  }

  /**根据缩放计算平滑倍率 */
  getSmoothFactor() {
    const zoom = Number(this.runtime?.map?.getZoom?.() || 4);
    if (zoom <= 3.5) return 6;
    if (zoom <= 5) return 5;
    if (zoom <= 6.5) return 4;
    return 3;
  }

  /**生成插值后的平滑网格 */
  buildSmoothedField(field, factor = 4) {
    const rows = (field.rows - 1) * factor + 1;
    const cols = (field.cols - 1) * factor + 1;
    const grid = Array.from({ length: rows }, () => Array(cols).fill(null));

    for (let r = 0; r < rows; r++) {
      const rf = r / (rows - 1) * (field.rows - 1);
      for (let c = 0; c < cols; c++) {
        const cf = c / (cols - 1) * (field.cols - 1);
        const lon = field.bounds.west + (field.bounds.east - field.bounds.west) * (c / (cols - 1));
        const lat = field.bounds.north - (field.bounds.north - field.bounds.south) * (r / (rows - 1));
        grid[r][c] = { row: r, col: c, lon, lat, pressure: this.bilinearSample(field, rf, cf) };
      }
    }

    return { ...field, rows, cols, grid };
  }

  /**构建等压值级别 */
  buildLevels(field, step = 2) {
    const values = [];
    for (let r = 0; r < field.rows; r++) {
      for (let c = 0; c < field.cols; c++) {
        const p = field.grid[r][c];
        if (p && Number.isFinite(p.pressure)) values.push(p.pressure);
      }
    }
    const min = Math.min(...values);
    const max = Math.max(...values);
    const start = Math.floor(min / step) * step;
    const end = Math.ceil(max / step) * step;
    const levels = [];
    for (let v = start; v <= end; v += step) levels.push(v);
    return levels;
  }

  /**计算边与等压值的交点 */
  interpolateEdge(a, b, level) {
    if (!a || !b) return null;
    const va = Number(a.pressure);
    const vb = Number(b.pressure);
    if (!Number.isFinite(va) || !Number.isFinite(vb)) return null;
    if ((va < level && vb < level) || (va > level && vb > level) || va === vb) return null;
    const t = (level - va) / (vb - va);
    return {
      lon: a.lon + (b.lon - a.lon) * t,
      lat: a.lat + (b.lat - a.lat) * t
    };
  }

  /**根据中心值处理鞍点格子 */
  getSaddleSegments(centerValue, level, top, right, bottom, left) {
    if (![top, right, bottom, left].every(Boolean)) return [];
    if (centerValue >= level) {
      return [
        [top, right],
        [bottom, left]
      ];
    }
    return [
      [top, left],
      [right, bottom]
    ];
  }

  /**使用 marching squares 生成线段 */
  marchingSegments(field, level) {
    const segs = [];
    for (let r = 0; r < field.rows - 1; r++) {
      for (let c = 0; c < field.cols - 1; c++) {
        const p00 = field.grid[r][c];
        const p10 = field.grid[r][c + 1];
        const p11 = field.grid[r + 1][c + 1];
        const p01 = field.grid[r + 1][c];
        if (!p00 || !p10 || !p11 || !p01) continue;

        const up = this.interpolateEdge(p00, p10, level);
        const right = this.interpolateEdge(p10, p11, level);
        const down = this.interpolateEdge(p01, p11, level);
        const left = this.interpolateEdge(p00, p01, level);
        const mask =
          (Number(p00.pressure) >= level ? 8 : 0) +
          (Number(p10.pressure) >= level ? 4 : 0) +
          (Number(p11.pressure) >= level ? 2 : 0) +
          (Number(p01.pressure) >= level ? 1 : 0);
        const centerValue = (Number(p00.pressure) + Number(p10.pressure) + Number(p11.pressure) + Number(p01.pressure)) / 4;

        switch (mask) {
          case 0:
          case 15:
            break;
          case 1:
          case 14:
            if (left && down) segs.push([left, down]);
            break;
          case 2:
          case 13:
            if (down && right) segs.push([down, right]);
            break;
          case 3:
          case 12:
            if (left && right) segs.push([left, right]);
            break;
          case 4:
          case 11:
            if (up && right) segs.push([up, right]);
            break;
          case 5:
          case 10:
            segs.push(...this.getSaddleSegments(centerValue, level, up, right, down, left));
            break;
          case 6:
          case 9:
            if (up && down) segs.push([up, down]);
            break;
          case 7:
          case 8:
            if (up && left) segs.push([up, left]);
            break;
          default:
            break;
        }
      }
    }
    return segs;
  }

  /**生成端点键 */
  getPointKey(point) {
    return `${point.lon.toFixed(6)},${point.lat.toFixed(6)}`;
  }

  /**连接等压线段 */
  stitchSegments(segs) {
    if (segs.length === 0) return [];

    const pointToSegs = new Map();
    const used = new Set();

    for (let i = 0; i < segs.length; i++) {
      const s = segs[i];
      const k1 = this.getPointKey(s[0]);
      const k2 = this.getPointKey(s[1]);
      if (k1 === k2) continue;

      if (!pointToSegs.has(k1)) pointToSegs.set(k1, []);
      if (!pointToSegs.has(k2)) pointToSegs.set(k2, []);
      pointToSegs.get(k1).push(i);
      pointToSegs.get(k2).push(i);
    }

    const lines = [];
    for (let i = 0; i < segs.length; i++) {
      if (used.has(i)) continue;

      let currentLine = [segs[i][0], segs[i][1]];
      used.add(i);

      let growing = true;
      while (growing) {
        growing = false;
        const tail = currentLine[currentLine.length - 1];
        const tailKey = this.getPointKey(tail);
        const candidates = pointToSegs.get(tailKey) || [];
        for (const segIdx of candidates) {
          if (!used.has(segIdx)) {
            const s = segs[segIdx];
            const sk1 = this.getPointKey(s[0]);
            const sk2 = this.getPointKey(s[1]);
            if (sk1 === tailKey) {
              currentLine.push(s[1]);
            } else if (sk2 === tailKey) {
              currentLine.push(s[0]);
            }
            used.add(segIdx);
            growing = true;
            break;
          }
        }
      }

      growing = true;
      while (growing) {
        growing = false;
        const head = currentLine[0];
        const headKey = this.getPointKey(head);
        const candidates = pointToSegs.get(headKey) || [];
        for (const segIdx of candidates) {
          if (!used.has(segIdx)) {
            const s = segs[segIdx];
            const sk1 = this.getPointKey(s[0]);
            const sk2 = this.getPointKey(s[1]);
            if (sk1 === headKey) {
              currentLine.unshift(s[1]);
            } else if (sk2 === headKey) {
              currentLine.unshift(s[0]);
            }
            used.add(segIdx);
            growing = true;
            break;
          }
        }
      }
      const cleaned = [];
      for (const point of currentLine) {
        const prev = cleaned[cleaned.length - 1];
        if (!prev || prev.lon !== point.lon || prev.lat !== point.lat) {
          cleaned.push(point);
        }
      }
      lines.push(cleaned);
    }
    return lines;
  }

  /**使用 Chaikin 算法平滑折线 */
  smoothLineCoords(coords, iterations = 2) {
    let points = Array.isArray(coords) ? coords.slice() : [];
    if (points.length < 3) return points;

    const times = Math.max(0, Math.min(3, Math.round(Number(iterations) || 0)));
    for (let n = 0; n < times; n++) {
      if (points.length < 3) break;
      const next = [points[0]];
      for (let i = 0; i < points.length - 1; i++) {
        const a = points[i];
        const b = points[i + 1];
        if (!Array.isArray(a) || !Array.isArray(b)) continue;
        const q = [
          a[0] * 0.75 + b[0] * 0.25,
          a[1] * 0.75 + b[1] * 0.25
        ];
        const r = [
          a[0] * 0.25 + b[0] * 0.75,
          a[1] * 0.25 + b[1] * 0.75
        ];
        next.push(q, r);
      }
      next.push(points[points.length - 1]);
      points = next;
    }
    return points;
  }

  /**获取折线平滑次数 */
  getLineSmoothIterations() {
    const zoom = Number(this.runtime?.map?.getZoom?.() || 4);
    if (zoom <= 3.5) return 3;
    if (zoom <= 5.5) return 2;
    return 1;
  }

  buildContourGeo(field, step = 2) {
    const features = [];
    const smoothTimes = this.getLineSmoothIterations();
    for (const level of this.buildLevels(field, step)) {
      const segs = this.marchingSegments(field, level);
      const stitched = this.stitchSegments(segs);
      for (const seg of stitched) {
        if (!Array.isArray(seg) || seg.length < 2) continue;
        const coords = this.smoothLineCoords(seg.map(p => [p.lon, p.lat]), smoothTimes);
        if (!Array.isArray(coords) || coords.length < 2) continue;
        features.push({
          type: "Feature",
          geometry: { type: "LineString", coordinates: coords },
          properties: { level }
        });
      }
    }
    return { type: "FeatureCollection", features };
  }

  buildLabelGeoFromContours(contourGeo) {
    const features = [];
    for (const f of contourGeo.features) {
      const coords = f?.geometry?.coordinates;
      const level = Number(f?.properties?.level);
      if (!Array.isArray(coords) || coords.length < 8 || !Number.isFinite(level)) continue;
      features.push({
        type: "Feature",
        geometry: { type: "LineString", coordinates: coords },
        properties: { text: `${Math.round(level)}` }
      });
    }
    return { type: "FeatureCollection", features };
  }

  async refresh() {
    this.ensureCanvas();
    this.ensureCanvasOrder();
    try {
      const rawField = await this.fetchField();
      const field = this.buildSmoothedField(rawField, this.getSmoothFactor());
      this.field = field;
      const contourGeo = this.buildContourGeo(field, PressureLayer.GRID.contourStep);
      const labelGeo = this.buildLabelGeoFromContours(contourGeo);

      addOrUpdateGeoJsonSource(this.runtime.map, this.contourSourceId, contourGeo);
      addOrUpdateGeoJsonSource(this.runtime.map, this.labelSourceId, labelGeo);

      const { map } = this.runtime;
      if (!map.getLayer(this.contourLayerId)) {
        map.addLayer({
          id: this.contourLayerId,
          type: "line",
          source: this.contourSourceId,
          layout: {
            "line-join": "round",
            "line-cap": "round"
          },
          paint: {
            "line-color": "#ffffff",
            "line-width": 1.8,
            "line-opacity": 0.92
          }
        });
      }
      if (!map.getLayer(this.labelLayerId)) {
        map.addLayer({
          id: this.labelLayerId,
          type: "symbol",
          source: this.labelSourceId,
          layout: {
            "symbol-placement": "line-center",
            "text-field": ["get", "text"],
            "text-size": 13,
            "text-font": ["Open Sans Bold", "Arial Unicode MS Bold"],
            "text-letter-spacing": 0.02,
            "text-max-angle": 18,
            "text-rotation-alignment": "viewport",
            "text-keep-upright": true,
            "text-allow-overlap": true,
            "text-ignore-placement": true,
            "text-padding": 1
          },
          paint: {
            "text-color": "#ffffff",
            "text-halo-color": "rgba(0,0,0,0.96)",
            "text-halo-width": 2.3,
            "text-halo-blur": 0,
            "text-opacity": 0.98
          }
        });
      }
      // 强制同步现有图层的属性（老的 label/contour 图层即使已经 addLayer 过也会应用新配置）
      const syncedOpacity = Number.isFinite(Number(this.runtime.getOpacity(this.name))) ? Number(this.runtime.getOpacity(this.name)) : 0.8;
      if (map.getLayer(this.contourLayerId)) {
        try {
          map.setLayoutProperty(this.contourLayerId, "visibility", "visible");
        } catch (_) { /* ignore */ }
        try {
          map.setPaintProperty(this.contourLayerId, "line-color", "#ffffff");
          map.setPaintProperty(this.contourLayerId, "line-width", 1.8);
          map.setPaintProperty(this.contourLayerId, "line-opacity", 0.92 * syncedOpacity);
        } catch (_) { /* ignore */ }
      }
      if (map.getLayer(this.labelLayerId)) {
        try {
          map.setLayoutProperty(this.labelLayerId, "visibility", "visible");
          map.setLayoutProperty(this.labelLayerId, "text-size", 13);
          map.setLayoutProperty(this.labelLayerId, "text-max-angle", 18);
          map.setLayoutProperty(this.labelLayerId, "text-font", ["Open Sans Bold", "Arial Unicode MS Bold"]);
          map.setLayoutProperty(this.labelLayerId, "text-rotation-alignment", "viewport");
          map.setLayoutProperty(this.labelLayerId, "text-keep-upright", true);
        } catch (_) { /* ignore */ }
        try {
          map.setPaintProperty(this.labelLayerId, "text-color", "#ffffff");
          map.setPaintProperty(this.labelLayerId, "text-halo-color", "rgba(0,0,0,0.96)");
          map.setPaintProperty(this.labelLayerId, "text-halo-width", 2.3);
          map.setPaintProperty(this.labelLayerId, "text-halo-blur", 0);
          map.setPaintProperty(this.labelLayerId, "text-opacity", 0.98 * syncedOpacity);
        } catch (_) { /* ignore */ }
      }

      this.setOpacity(this.runtime.getOpacity(this.name));
      this.drawHeatmap(field);

      const sampleTs = rawField.sampleTime ? new Date(rawField.sampleTime).getTime() : Number.NaN;
      let ageText = "时效未知";
      if (Number.isFinite(sampleTs)) {
        const ageMin = Math.round((Date.now() - sampleTs) / 60000);
        ageText = ageMin >= 0 ? `${ageMin}分钟前` : `预报${Math.abs(ageMin)}分钟后`;
      }
      if (rawField.sampleTime) this.setDataTime(rawField.sampleTime);
      this.setInfoExtra(`时效: ${ageText}`);
      this.lastStatus = true;
    } catch (e) {
      console.error("刷新气压等压线失败", e);
      const msg = e instanceof Error ? e.message : String(e || "气压数据加载失败");
      this.setLastError(msg || "气压数据加载失败");
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
    const { map } = this.runtime;
    if (map.getLayer(this.contourLayerId)) map.setPaintProperty(this.contourLayerId, "line-opacity", 0.92 * safe);
    if (map.getLayer(this.labelLayerId)) map.setPaintProperty(this.labelLayerId, "text-opacity", 0.98 * safe);
    if (this.heatmapCanvas) {
      this.heatmapCanvas.style.opacity = String(PressureConfig.canvas.heatmapOpacity * safe);
    }
    this.opacity = safe;
  }

  hide() {
    super.hide();
    const { map } = this.runtime;
    if (map.getLayer(this.contourLayerId)) map.setLayoutProperty(this.contourLayerId, "visibility", "none");
    if (map.getLayer(this.labelLayerId)) map.setLayoutProperty(this.labelLayerId, "visibility", "none");
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
    const { map } = this.runtime;
    if (map.getLayer(this.contourLayerId)) map.setLayoutProperty(this.contourLayerId, "visibility", "visible");
    if (map.getLayer(this.labelLayerId)) map.setLayoutProperty(this.labelLayerId, "visibility", "visible");
    this.setOpacity(Number.isFinite(Number(opacity)) ? Number(opacity) : 0.8);
    this.heatmapDirty = true;
    this.drawHeatmap();
    return ok;
  }
}
