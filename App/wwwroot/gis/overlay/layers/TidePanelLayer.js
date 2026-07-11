import { MapLayer } from "../core/MapLayer.js";
import { addOrUpdateGeoJsonSource, clamp, fetchWithTimeout, findNearestHourlyIndex, getTimeSeriesStepSeconds, setInfo } from "../core/utils.js";


/****************************************************************
 * 潮汐面板图层
 ****************************************************************/
export class TidePanelLayer extends MapLayer {
  static TIDE_STATIONS = {
    cn_wenzhou: { name: "温州潮位站", lat: 28.02833, lon: 120.625, timezone: "Asia/Shanghai" },
    cn_ruian: { name: "瑞安潮位站", lat: 27.785, lon: 120.62, timezone: "Asia/Shanghai" },
    cn_aojiang: { name: "鳌江潮位站", lat: 27.595611, lon: 120.551806, timezone: "Asia/Shanghai" },
    cn_pipamen: { name: "琵琶门潮位站", lat: 27.508369, lon: 120.668023, timezone: "Asia/Shanghai" }, 
    cn_nanjishan: { name: "南麂山潮位站", lat: 27.465, lon: 121.06317, timezone: "Asia/Shanghai" },
    cn_ningbo: { name: "宁波舟山", lat: 29.85, lon: 122.12, timezone: "Asia/Shanghai" },
    cn_xiamen: { name: "厦门外海", lat: 24.48, lon: 118.08, timezone: "Asia/Shanghai" },
    cn_shenzhen: { name: "深圳大鹏湾", lat: 22.56, lon: 114.28, timezone: "Asia/Shanghai" },
    cn_qingdao: { name: "青岛外海", lat: 36.08, lon: 120.33, timezone: "Asia/Shanghai" },
    cn_dalian: { name: "大连外海", lat: 38.92, lon: 121.64, timezone: "Asia/Shanghai" },
    cn_sanya: { name: "三亚外海", lat: 18.22, lon: 109.5, timezone: "Asia/Shanghai" },
    us_sf: { name: "San Francisco", lat: 37.8, lon: -122.47, timezone: "America/Los_Angeles" }
  };


  constructor() {
    super({
      name: "tidePanel",
      title: "潮汐面板",
      api: "https://marine-api.open-meteo.com/v1/marine",
      refreshSeconds: 600,
      dataInterval: "1小时"
    });
    this.boundStationChange = false;
    this.panelId = "tidePanelContainer";
    this.styleId = "tide-panel-style";
    this.sourceId = "tide-station-source";
    this.dotLayerId = "tide-station-dot-layer";
    this.labelLayerId = "tide-station-label-layer";
    this.boundStationMapEvents = false;
    this.boundChartEvents = false;
    this.chartData = { times: [], values: [] };
    this.selectedChartIndex = -1;
    this.chart = null;
    this.boundWindowResize = false;
  }

  /**绑定运行时并确保面板 UI 已创建 */
  bind(runtime) {
    super.bind(runtime);
    this.ensurePanel();
    const stationEl = this.getStationEl();
    if (stationEl) stationEl.value = "cn_wenzhou";
    if (!this.boundStationChange && stationEl) {
      stationEl.addEventListener("change", async () => {
        if (!this.visible) return;
        try {
          this.syncStationSource();
          await this.refresh(true);
        } catch (error) {
          console.error("潮汐站点切换刷新失败", error);
          setInfo("tideInfo", "潮汐站点切换失败");
        }
      });
      this.boundStationChange = true;
    }
    this.ensureStationLayers();
  }

  /**获取面板根节点 */
  getPanelEl() {
    return document.getElementById(this.panelId);
  }

  /**获取站点下拉框 */
  getStationEl() {
    return document.getElementById("tideStation");
  }

  /**获取当前选中的站点编号 */
  getStationId() {
    return this.getStationEl()?.value || "cn_wenzhou";
  }

  /**确保潮汐面板 DOM 已创建 */
  ensurePanel() {
    this.ensureStyle();
    if (this.getPanelEl()) return;

    const panel = document.createElement("div");
    panel.id = this.panelId;
    panel.className = "tide-panel";
    panel.innerHTML = this.getPanelHtml();
    document.body.appendChild(panel);
    this.ensureChart();
    this.bindChartEvents();
  }

  /**确保潮汐面板样式已注入 */
  ensureStyle() {
    if (document.getElementById(this.styleId)) return;
    const style = document.createElement("style");
    style.id = this.styleId;
    style.textContent = this.getPanelStyle();
    document.head.appendChild(style);
  }

  /**构建潮汐面板样式 */
  getPanelStyle() {
    return `
      #${this.panelId} {
        position: fixed;
        top: 84px;
        right: 16px;
        width: 380px;
        max-width: calc(100vw - 24px);
        max-height: calc(100vh - 100px);
        overflow: auto;
        background: rgba(241, 245, 249, 0.95);
        border: 1px solid rgba(148, 163, 184, 0.35);
        border-radius: 12px;
        box-shadow: 0 10px 28px rgba(15, 23, 42, 0.25);
        padding: 14px;
        z-index: 1000;
        display: none;
      }
      #${this.panelId} h3 {
        font-size: 15px;
        margin-bottom: 10px;
        color: #0f172a;
      }
      #${this.panelId} .stats {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 8px;
        margin-bottom: 10px;
      }
      #${this.panelId} .stat {
        background: #fff;
        border-radius: 10px;
        border: 1px solid #e2e8f0;
        padding: 8px;
      }
      #${this.panelId} .stat .k {
        font-size: 11px;
        color: #475569;
      }
      #${this.panelId} .stat .v {
        margin-top: 3px;
        font-size: 15px;
        color: #0f172a;
        font-weight: 700;
      }
      #${this.panelId} .tide-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 8px;
        margin-bottom: 8px;
      }
      #${this.panelId} .tide-title {
        font-size: 13px;
        color: #0f172a;
        font-weight: 600;
      }
      #${this.panelId} .tide-header select {
        border: 1px solid #cbd5e1;
        border-radius: 8px;
        padding: 4px 8px;
        background: #fff;
        font-size: 12px;
        color: #0f172a;
        max-width: 180px;
      }
      #${this.panelId} #tideChart {
        width: 100%;
        height: 220px;
        border-radius: 10px;
        background: linear-gradient(180deg, #ffffff, #e2e8f0);
        border: 1px solid #cbd5e1;
        cursor: crosshair;
        display: block;
        overflow: hidden;
      }
      #${this.panelId} .tide-cursor-info {
        margin-top: 6px;
        min-height: 20px;
        font-size: 12px;
        color: #334155;
      }
      #${this.panelId} .tide-cursor-time {
        font-weight: 600;
        color: #0f172a;
        margin-right: 10px;
      }
      #${this.panelId} .tide-cursor-height {
        color: #0369a1;
        font-weight: 600;
      }
      #${this.panelId} #tideInfo {
        margin-top: 8px;
      }
      #${this.panelId} .legend {
        margin-top: 8px;
        font-size: 11px;
        color: #334155;
        line-height: 1.55;
      }
      #${this.panelId} .legend span {
        display: inline-flex;
        align-items: center;
        margin-right: 10px;
      }
      #${this.panelId} .legend i {
        width: 10px;
        height: 10px;
        border-radius: 50%;
        margin-right: 4px;
        display: inline-block;
      }
      #${this.panelId} .dot-tide { background: #0ea5e9; }
      #${this.panelId} .dot-now { background: #ef4444; }
      #${this.panelId} .dot-peak { background: #22c55e; }
      @media (max-width: 1020px) {
        #${this.panelId} {
          top: auto;
          bottom: 12px;
          left: 12px;
          right: 12px;
          width: auto;
          max-height: 40vh;
        }
      }
    `;
  }

  /**构建站点 GeoJSON */
  buildStationGeo() {
    const currentId = this.getStationId();
    const features = Object.entries(TidePanelLayer.TIDE_STATIONS).map(([id, item]) => ({
      type: "Feature",
      geometry: {
        type: "Point",
        coordinates: [item.lon, item.lat]
      },
      properties: {
        id,
        name: item.name,
        selected: id === currentId ? 1 : 0
      }
    }));
    return { type: "FeatureCollection", features };
  }

  /**确保站点图层已创建 */
  ensureStationLayers() {
    if (!this.runtime?.map) return;
    const { map } = this.runtime;
    addOrUpdateGeoJsonSource(map, this.sourceId, this.buildStationGeo());

    if (!map.getLayer(this.dotLayerId)) {
      map.addLayer({
        id: this.dotLayerId,
        type: "circle",
        source: this.sourceId,
        paint: {
          "circle-radius": [
            "case",
            ["==", ["get", "selected"], 1], 7,
            5
          ],
          "circle-color": [
            "case",
            ["==", ["get", "selected"], 1], "#ef4444",
            "#38bdf8"
          ],
          "circle-stroke-width": [
            "case",
            ["==", ["get", "selected"], 1], 2.5,
            1.5
          ],
          "circle-stroke-color": "#ffffff",
          "circle-opacity": 0.95
        }
      });
    }

    if (!map.getLayer(this.labelLayerId)) {
      map.addLayer({
        id: this.labelLayerId,
        type: "symbol",
        source: this.sourceId,
        layout: {
          "text-field": ["get", "name"],
          "text-size": 12,
          "text-anchor": "top",
          "text-offset": [0, 0.9],
          "text-allow-overlap": true,
          "text-ignore-placement": true
        },
        paint: {
          "text-color": [
            "case",
            ["==", ["get", "selected"], 1], "#fef2f2",
            "#e0f2fe"
          ],
          "text-halo-color": "#0f172a",
          "text-halo-width": 1.8
        }
      });
    }

    this.bindStationEvents();
    this.setStationVisibility(this.visible);
  }

  /**绑定站点点位交互 */
  bindStationEvents() {
    if (this.boundStationMapEvents || !this.runtime?.map) return;
    const { map } = this.runtime;

    map.on("click", this.dotLayerId, async (evt) => {
      const feature = evt.features?.[0];
      const id = feature?.properties?.id;
      if (!id) return;
      await this.selectStation(id);
    });

    map.on("click", this.labelLayerId, async (evt) => {
      const feature = evt.features?.[0];
      const id = feature?.properties?.id;
      if (!id) return;
      await this.selectStation(id);
    });

    map.on("mouseenter", this.dotLayerId, () => {
      map.getCanvas().style.cursor = "pointer";
    });

    map.on("mouseenter", this.labelLayerId, () => {
      map.getCanvas().style.cursor = "pointer";
    });

    map.on("mouseleave", this.dotLayerId, () => {
      map.getCanvas().style.cursor = "";
    });

    map.on("mouseleave", this.labelLayerId, () => {
      map.getCanvas().style.cursor = "";
    });

    this.boundStationMapEvents = true;
  }

  /**同步站点源数据 */
  syncStationSource() {
    if (!this.runtime?.map) return;
    addOrUpdateGeoJsonSource(this.runtime.map, this.sourceId, this.buildStationGeo());
  }

  /**设置站点图层可见性 */
  setStationVisibility(visible) {
    if (!this.runtime?.map) return;
    const { map } = this.runtime;
    const visibility = visible ? "visible" : "none";
    if (map.getLayer(this.dotLayerId)) map.setLayoutProperty(this.dotLayerId, "visibility", visibility);
    if (map.getLayer(this.labelLayerId)) map.setLayoutProperty(this.labelLayerId, "visibility", visibility);
  }

  /**设置站点图层透明度 */
  setStationOpacity(opacity) {
    if (!this.runtime?.map) return;
    const { map } = this.runtime;
    if (map.getLayer(this.dotLayerId)) {
      map.setPaintProperty(this.dotLayerId, "circle-opacity", Math.max(0.25, opacity));
      map.setPaintProperty(this.dotLayerId, "circle-stroke-opacity", Math.max(0.35, opacity));
    }
    if (map.getLayer(this.labelLayerId)) {
      map.setPaintProperty(this.labelLayerId, "text-opacity", Math.max(0.45, opacity));
    }
  }

  /**选择站点并刷新面板 */
  async selectStation(id) {
    const stationEl = this.getStationEl();
    if (!stationEl || !TidePanelLayer.TIDE_STATIONS[id]) return;
    if (stationEl.value !== id) stationEl.value = id;
    this.syncStationSource();
    if (!this.visible) return;
    try {
      await this.refresh(true);
    } catch (error) {
      console.error("切换潮汐站点失败", error);
      setInfo("tideInfo", "潮汐站点切换失败");
    }
  }

  /**构建潮汐面板 HTML */
  getPanelHtml() {
    const options = Object.entries(TidePanelLayer.TIDE_STATIONS)
      .map(([key, item]) => `<option value="${key}"${key === "cn_wenzhou" ? " selected" : ""}>${item.name}</option>`)
      .join("");

    return `
      <h3>海况与潮汐面板</h3>
      <div class="stats">
        <div class="stat"><div class="k">当前浪高</div><div class="v" id="waveHeight">--</div></div>
        <div class="stat"><div class="k">波浪周期</div><div class="v" id="wavePeriod">--</div></div>
        <div class="stat"><div class="k">波浪方向</div><div class="v" id="waveDirection">--</div></div>
        <div class="stat"><div class="k">潮汐峰值</div><div class="v" id="tidePeak">--</div></div>
      </div>
      <div class="tide-header">
        <div class="tide-title">未来48小时潮汐曲线</div>
        <select id="tideStation" aria-label="潮汐站点">
          ${options}
        </select>
      </div>
      <div id="tideChart"></div>
      <div id="tideCursorInfo" class="tide-cursor-info">点击曲线查看时间和潮位</div>
      <div id="tideInfo" class="info">加载中...</div>
      <div class="legend">
        <span><i class="dot-tide"></i>潮位曲线</span>
      </div>
    `;
  }

  /**获取图表容器 */
  getChartEl() {
    return document.getElementById("tideChart");
  }

  /**确保图表实例已创建 */
  ensureChart() {
    const el = this.getChartEl();
    if (!el || !window.echarts) return null;
    if (!this.chart || this.chart.isDisposed?.()) {
      this.chart = window.echarts.init(el, null, { renderer: "canvas" });
    }
    if (!this.boundWindowResize) {
      window.addEventListener("resize", () => this.resizeChart());
      this.boundWindowResize = true;
    }
    return this.chart;
  }

  /**调整图表尺寸 */
  resizeChart() {
    const chart = this.ensureChart();
    chart?.resize?.();
  }

  /**绑定图表交互 */
  bindChartEvents() {
    if (this.boundChartEvents) return;
    const chart = this.ensureChart();
    if (!chart) return;

    chart.getZr().on("click", (evt) => {
      const idx = this.getChartIndexFromEvent(evt);
      if (idx < 0) return;
      this.selectedChartIndex = idx;
      this.drawTideChart();
      chart.dispatchAction({ type: "showTip", seriesIndex: 0, dataIndex: idx });
      this.renderSelectedTideInfo(idx);
    });

    this.boundChartEvents = true;
  }

  /**格式化图表时间 */
  formatChartTime(value) {
    const dt = new Date(value);
    if (Number.isNaN(dt.getTime())) return String(value || "");
    const mm = String(dt.getMonth() + 1).padStart(2, "0");
    const dd = String(dt.getDate()).padStart(2, "0");
    const hh = String(dt.getHours()).padStart(2, "0");
    const mi = String(dt.getMinutes()).padStart(2, "0");
    return `${mm}-${dd} ${hh}:${mi}`;
  }

  /**格式化小时标签 */
  formatChartHour(value) {
    const dt = new Date(value);
    if (Number.isNaN(dt.getTime())) return "";
    return `${String(dt.getHours()).padStart(2, "0")}:00`;
  }

  /**获取图表命中索引 */
  getChartIndexFromEvent(evt) {
    const chart = this.ensureChart();
    if (!chart || !this.chartData.values.length) return -1;
    const point = [evt.offsetX, evt.offsetY];
    if (!chart.containPixel({ gridIndex: 0 }, point)) return -1;
    const raw = chart.convertFromPixel({ xAxisIndex: 0 }, point[0]);
    const idx = Math.round(Number(raw));
    return clamp(idx, 0, this.chartData.values.length - 1);
  }

  /**渲染当前选中潮位信息 */
  renderSelectedTideInfo(idx) {
    const host = document.getElementById("tideCursorInfo");
    if (!host) return;
    const times = this.chartData.times || [];
    const values = this.chartData.values || [];
    if (idx < 0 || idx >= times.length || idx >= values.length) {
      host.textContent = "点击曲线查看时间和潮位";
      return;
    }
    host.innerHTML = `
      <span class="tide-cursor-time">${this.formatChartTime(times[idx])}</span>
      <span class="tide-cursor-height">潮位 ${Number(values[idx]).toFixed(2)} m</span>
    `;
  }

  /**构建潮位图表配置 */
  buildTideChartOption() {
    const times = this.chartData.times || [];
    const values = this.chartData.values || [];
    const min = Math.min(...values);
    const max = Math.max(...values);
    const span = Math.max(0.6, max - min);
    const yMin = Number((min - span * 0.1).toFixed(2));
    const yMax = Number((max + span * 0.1).toFixed(2));

    let nowIdx = 0;
    let bestGap = Infinity;
    const now = Date.now();
    times.forEach((t, i) => {
      const gap = Math.abs(new Date(t).getTime() - now);
      if (gap < bestGap) {
        bestGap = gap;
        nowIdx = i;
      }
    });
    const activeIdx = this.selectedChartIndex >= 0 && this.selectedChartIndex < values.length
      ? this.selectedChartIndex
      : nowIdx;

    const selectedTime = times[activeIdx] || "";
    const selectedValue = Number(values[activeIdx] ?? 0);

    return {
      animation: false,
      grid: { left: 44, right: 16, top: 16, bottom: 50, containLabel: false },
      tooltip: {
        trigger: "axis",
        triggerOn: "click",
        axisPointer: {
          type: "line",
          snap: true,
          lineStyle: { color: "#ef4444", width: 2 }
        },
        backgroundColor: "rgba(15,23,42,0.82)",
        borderWidth: 0,
        textStyle: { color: "#ffffff", fontSize: 11 },
        formatter: (params) => {
          const row = Array.isArray(params) ? params[0] : params;
          const idx = row?.dataIndex ?? activeIdx;
          const val = Number(values[idx] ?? 0).toFixed(2);
          return `${this.formatChartTime(times[idx])}<br/>潮位 ${val} m`;
        }
      },
      xAxis: {
        type: "category",
        boundaryGap: false,
        data: times,
        axisTick: {
          show: true,
          interval: 0,
          alignWithLabel: false,
          lineStyle: { color: "rgba(148,163,184,0.55)" }
        },
        axisLabel: {
          color: "#475569",
          fontSize: 10,
          margin: 14,
          interval: (index) => index % 6 === 0,
          formatter: (value) => this.formatChartHour(value)
        },
        axisLine: { lineStyle: { color: "rgba(148,163,184,0.55)" } },
        splitLine: {
          show: true,
          interval: 0,
          lineStyle: { color: "rgba(148,163,184,0.18)" }
        }
      },
      yAxis: {
        type: "value",
        min: yMin,
        max: yMax,
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: {
          color: "#475569",
          formatter: (value) => `${Number(value).toFixed(1)}m`
        },
        splitNumber: 4,
        splitLine: { lineStyle: { color: "rgba(148,163,184,0.25)" } }
      },
      series: [
        {
          name: "潮位曲线",
          type: "line",
          smooth: 0.35,
          showSymbol: false,
          symbol: "circle",
          symbolSize: 7,
          lineStyle: { width: 3, color: "#0ea5e9" },
          itemStyle: { color: "#0ea5e9" },
          data: values
        },
        {
          name: "选中点",
          type: "scatter",
          data: selectedTime ? [[selectedTime, selectedValue]] : [],
          symbolSize: 11,
          itemStyle: { color: "#ffffff", borderColor: "#ef4444", borderWidth: 3 },
          z: 6
        }
      ]
    };
  }

  /**绘制潮位趋势图 */
  drawTideChart(times = this.chartData.times, values = this.chartData.values) {
    if (Array.isArray(times) && Array.isArray(values) && times.length && values.length) {
      this.chartData = { times: times.slice(), values: values.slice() };
    }
    const dataValues = this.chartData.values || [];
    const chart = this.ensureChart();
    if (!chart) return;
    if (!dataValues.length) return;
    chart.setOption(this.buildTideChartOption(), true);
    this.resizeChart();
    const activeIdx = this.selectedChartIndex >= 0 && this.selectedChartIndex < dataValues.length
      ? this.selectedChartIndex
      : 0;
    chart.dispatchAction({ type: "showTip", seriesIndex: 0, dataIndex: activeIdx });
    this.renderSelectedTideInfo(activeIdx);
  }

  /**刷新海况与潮汐数据 */
  async refresh() {
    this.ensurePanel();
    this.ensureStationLayers();
    const stationId = this.getStationId();
    const station = TidePanelLayer.TIDE_STATIONS[stationId] || TidePanelLayer.TIDE_STATIONS.cn_wenzhou;

    const query = new URLSearchParams({
      latitude: String(station.lat),
      longitude: String(station.lon),
      hourly: "sea_level_height_msl,wave_height,wave_direction,wave_period",
      timezone: station.timezone,
      forecast_days: "3"
    });

    const response = await fetchWithTimeout(`${this.api}?${query.toString()}`, {}, 12000);
    if (!response.ok) throw new Error(`海况潮位请求失败: ${response.status}`);
    const data = await response.json();
    const h = data.hourly || {};
    const times = Array.isArray(h.time) ? h.time : [];
    const sea = Array.isArray(h.sea_level_height_msl) ? h.sea_level_height_msl : [];
    const waveHeight = Array.isArray(h.wave_height) ? h.wave_height : [];
    const waveDirection = Array.isArray(h.wave_direction) ? h.wave_direction : [];
    const wavePeriod = Array.isArray(h.wave_period) ? h.wave_period : [];
    if (!times.length || !sea.length) throw new Error("潮位序列为空");

    const idx = findNearestHourlyIndex(times);
    const stepSec = getTimeSeriesStepSeconds(times);
    if (stepSec > 0) this.setDataInterval(this.formatSecondsAsText(stepSec));
    setInfo("waveHeight", `${Number(waveHeight[idx] ?? 0).toFixed(1)} m`);
    setInfo("waveDirection", `${Math.round(Number(waveDirection[idx] ?? 0))}°`);
    setInfo("wavePeriod", `${Number(wavePeriod[idx] ?? 0).toFixed(1)} s`);

    const curveTimes = times.slice(0, 49);
    const curveValues = sea.slice(0, 49).map(v => Number(v));
    this.selectedChartIndex = findNearestHourlyIndex(curveTimes);
    this.drawTideChart(curveTimes, curveValues);
    setInfo("tidePeak", `${Math.max(...curveValues).toFixed(2)} m`);
    setInfo("tideInfo", `站点 ${station.name}，更新: ${new Date().toLocaleString()}`);
    this.setDataTime(times[idx] || "");
    this.setInfoExtra(`站点: ${station.name}`);
    this.syncStationSource();
    this.setOpacity(this.runtime.getOpacity(this.name));
    this.lastStatus = true;
    this.lastTime = Date.now();
    return true;
  }

  /**设置面板透明度 */
  setOpacity(opacity) {
    const panel = this.getPanelEl();
    if (panel) panel.style.opacity = String(opacity);
    this.setStationOpacity(opacity);
  }

  /**隐藏面板 */
  hide() {
    super.hide();
    const panel = this.getPanelEl();
    if (panel) panel.style.display = "none";
    this.setStationVisibility(false);
    this.clearDataTime();
    this.setInfoExtra("");
    return true;
  }

  /**显示面板 */
  async show(opacity = 1) {
    this.ensurePanel();
    this.ensureStationLayers();
    const panel = this.getPanelEl();
    if (panel) panel.style.display = "block";
    this.setStationVisibility(true);
    const ok = await super.show(opacity);
    this.resizeChart();
    this.setOpacity(opacity);
    return ok;
  }
}
