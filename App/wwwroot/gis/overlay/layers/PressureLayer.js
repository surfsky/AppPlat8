import { MapLayer } from "../core/MapLayer.js";
import { addOrUpdateGeoJsonSource, fetchWithTimeout, findNearestHourlyIndex, getTimeSeriesStepSeconds } from "../core/utils.js";





/****************************************************************
 * 气压等压线图层
 ****************************************************************/
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
      refreshSeconds: 900,
      dataInterval: "1小时"
    });
    this.contourSourceId = "pressure-contour-source";
    this.labelSourceId = "pressure-label-source";
    this.contourLayerId = "pressure-contour-layer";
    this.labelLayerId = "pressure-label-layer";
    
    // 原始采样缓存
    this.rawCache = {
      data: null,
      bounds: null,
      timestamp: 0
    };
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
    let dataInterval = this.dataInterval;

    for (let i = 0; i < nodes.length; i++) {
      const node = nodes[i];
      const item = list[i] || {};
      const times = Array.isArray(item?.hourly?.time) ? item.hourly.time : [];
      const values = Array.isArray(item?.hourly?.pressure_msl) ? item.hourly.pressure_msl : [];
      if (times.length > 1 && (!dataInterval || dataInterval === this.dataInterval)) {
        const sec = getTimeSeriesStepSeconds(times);
        if (sec > 0) dataInterval = this.formatSecondsAsText(sec);
      }
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
      dataInterval,
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
        properties: { text: `${Math.round(level)}hPa` }
      });
    }
    return { type: "FeatureCollection", features };
  }

  async refresh() {
    const rawField = await this.fetchField();
    const field = this.buildSmoothedField(rawField, this.getSmoothFactor());
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
          "line-color": [
            "interpolate", ["linear"], ["get", "level"],
            980, "#1d4ed8",
            1000, "#0ea5e9",
            1012, "#22c55e",
            1020, "#f59e0b",
            1032, "#ef4444"
          ],
          "line-width": 1.6,
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
          "text-size": 11,
          "text-letter-spacing": 0.02,
          "text-max-angle": 15,
          "text-rotation-alignment": "map",
          "text-keep-upright": false,
          "text-allow-overlap": true,
          "text-ignore-placement": true,
          "text-padding": 1
        },
        paint: {
          "text-color": "#ffffff",
          "text-halo-color": "rgba(0,0,0,0.96)",
          "text-halo-width": 1.9,
          "text-halo-blur": 0.35,
          "text-opacity": 0.95
        }
      });
    }

    const sampleTs = rawField.sampleTime ? new Date(rawField.sampleTime).getTime() : Number.NaN;
    let ageText = "时效未知";
    if (Number.isFinite(sampleTs)) {
      const ageMin = Math.round((Date.now() - sampleTs) / 60000);
      ageText = ageMin >= 0 ? `${ageMin}分钟前` : `预报${Math.abs(ageMin)}分钟后`;
    }
    if (rawField.sampleTime) this.setDataTime(rawField.sampleTime);
    if (rawField.dataInterval) this.setDataInterval(rawField.dataInterval);
    this.setInfoExtra(`时效: ${ageText}`);
    this.setOpacity(this.runtime.getOpacity(this.name));
    this.lastStatus = true;
    this.lastTime = Date.now();
    return true;
  }

  setOpacity(opacity) {
    const { map } = this.runtime;
    if (map.getLayer(this.contourLayerId)) map.setPaintProperty(this.contourLayerId, "line-opacity", opacity);
    if (map.getLayer(this.labelLayerId)) map.setPaintProperty(this.labelLayerId, "text-opacity", opacity);
  }

  hide() {
    super.hide();
    const { map } = this.runtime;
    if (map.getLayer(this.contourLayerId)) map.setLayoutProperty(this.contourLayerId, "visibility", "none");
    if (map.getLayer(this.labelLayerId)) map.setLayoutProperty(this.labelLayerId, "visibility", "none");
    this.clearDataTime();
    this.setInfoExtra("");
    return true;
  }

  async show(opacity = 1) {
    const ok = await super.show(opacity);
    const { map } = this.runtime;
    if (map.getLayer(this.contourLayerId)) map.setLayoutProperty(this.contourLayerId, "visibility", "visible");
    if (map.getLayer(this.labelLayerId)) map.setLayoutProperty(this.labelLayerId, "visibility", "visible");
    return ok;
  }
}
