import { MapLayer } from "../core/MapLayer.js";
import { addOrUpdateGeoJsonSource, fetchWithTimeout } from "../core/utils.js";

/****************************************************************
 * 台风路径图层
 ****************************************************************/
export class TyphoonLayer extends MapLayer {
  constructor() {
    super({
      name: "typhoon",
      title: "台风路径",
      api: "/httpapi/typhoon/list",
      refreshSeconds: 300,
      dataInterval: "3小时"
    });
    this.sourceId = "typhoon-source";
    this.wind7FillLayerId = "typhoon-wind7-fill-layer";
    this.wind7LineLayerId = "typhoon-wind7-line-layer";
    this.wind10FillLayerId = "typhoon-wind10-fill-layer";
    this.wind10LineLayerId = "typhoon-wind10-line-layer";
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
    this.legendMiniId = "typhoonLegendMini";
    this.styleId = "typhoonStyle";
    this.selectId = "typhoonSelect";
    this.yearSelectId = "typhoonYearSelect";
    this.listApi = "/httpapi/typhoon/list";
    this.getApi = "/httpapi/typhoon/get";
    this.logsApi = "/httpapi/typhoon/logs";
    this.predictApi = "/httpapi/typhoon/predict";
    this.currentTrackApi = "https://agora.ex.nii.ac.jp/digital-typhoon/geojson/wnp/";
    this.currentForecastApi = "https://agora.ex.nii.ac.jp/digital-typhoon/json/jmaxml-forecast/wnp/";
    this.currentListApi = "https://codh.ex.nii.ac.jp/digital-typhoon/latest/track/index.html.en";
    this.historyList = [];
    this.currentList = [];
    this.selectedYear = "";
    this.selectedCode = "";
    this.currentStormMap = new Map();
    this.currentStormTtlMs = 6 * 60 * 1000;
    this.liveRefreshAt = 0;
    this.currentListLoadedAt = 0;
    this.initSelectionReady = false;
    this.legendEl = null;
    this.legendMiniEl = null;
    this.legendCollapsed = false;
    this.popup = null;
    this.eventBound = false;
    this.legendBound = false;
    this.legendDragBound = false;
    this.legendDragId = 0;
    this.legendDragMoving = false;
    this.legendDragOffsetX = 0;
    this.legendDragOffsetY = 0;
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
    this.onCenterMove = this.onCenterMove.bind(this);
    this.onCenterLeave = this.onCenterLeave.bind(this);
    this.onCenterClick = this.onCenterClick.bind(this);
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
        z-index: 4014;
        min-width: 560px;
        max-width: min(92vw, 900px);
        padding: 8px 12px 10px;
        border-radius: 12px;
        background: rgba(248,250,252,0.72);
        border: 1px solid rgba(148,163,184,0.26);
        box-shadow: 0 12px 32px rgba(15,23,42,0.22);
        color: #0f172a;
        backdrop-filter: blur(14px);
        -webkit-backdrop-filter: blur(14px);
        overflow: hidden;
      }
      .typhoon-legend.is-hidden { display: none; }
      .typhoon-legend-actions {
        display: inline-flex;
        align-items: center;
        gap: 8px;
        margin-left: auto;
      }
      .typhoon-legend-toggle {
        width: 28px;
        height: 28px;
        border: 1px solid rgba(148,163,184,0.24);
        border-radius: 999px;
        background: rgba(255,255,255,0.72);
        color: #334155;
        cursor: pointer;
        transition: all .16s ease;
      }
      .typhoon-legend-toggle:hover {
        color: #0f172a;
        border-color: rgba(14,165,233,0.5);
        background: rgba(255,255,255,0.92);
      }
      .typhoon-legend-mini {
        position: fixed;
        right: 18px;
        bottom: 132px;
        z-index: 4015;
        width: 38px;
        height: 38px;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        border: 1px solid rgba(56,189,248,0.4);
        border-radius: 10px;
        background: rgba(3,16,80,0.9);
        color: #e0f2fe;
        box-shadow: 0 10px 24px rgba(2,6,23,0.34);
        cursor: pointer;
      }
      .typhoon-legend-mini.is-hidden { display: none; }
      .typhoon-legend-mini:hover {
        background: rgba(14,66,146,0.82);
        border-color: rgba(56,189,248,0.7);
      }
      .typhoon-legend-title {
        display: flex;
        align-items: center;
        justify-content: flex-start;
        gap: 12px;
        margin: -8px -12px 8px;
        padding: 8px 12px;
        font-size: 13px;
        font-weight: 700;
        cursor: move;
        user-select: none;
        touch-action: none;
        background: linear-gradient(180deg, rgba(255,255,255,0.36), rgba(255,255,255,0.12));
        border-bottom: 1px solid rgba(148,163,184,0.2);
      }
      .typhoon-legend-title.is-dragging { cursor: grabbing; }
      .typhoon-legend-title-text {
        display: inline-flex;
        align-items: center;
        gap: 8px;
        min-width: 0;
      }
      .typhoon-legend-drag {
        display: inline-flex;
        align-items: center;
        gap: 3px;
        color: #64748b;
        opacity: 0.85;
      }
      .typhoon-legend-drag i {
        display: block;
        width: 3px;
        height: 3px;
        border-radius: 50%;
        background: currentColor;
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
        .typhoon-legend-title {
          flex-wrap: wrap;
          gap: 6px 10px;
        }
        .typhoon-select {
          width: 100%;
        }
        .typhoon-legend-mini {
          right: 12px;
          bottom: 118px;
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
    this.ensureMiniButton();
    return el;
  }

  /**确保迷你按钮 */
  ensureMiniButton() {
    if (this.legendMiniEl) return this.legendMiniEl;
    let btn = document.getElementById(this.legendMiniId);
    if (!btn) {
      btn = document.createElement("button");
      btn.type = "button";
      btn.id = this.legendMiniId;
      btn.className = "typhoon-legend-mini is-hidden";
      btn.setAttribute("title", "展开台风面板");
      btn.setAttribute("aria-label", "展开台风面板");
      btn.innerHTML = '<i class="fa-solid fa-hurricane"></i>';
      document.body.appendChild(btn);
    }
    this.legendMiniEl = btn;
    return btn;
  }

  /**同步图例显隐 */
  syncLegendDisplay() {
    const el = this.ensureLegend();
    const mini = this.ensureMiniButton();
    const panelHidden = !this.visible || this.legendCollapsed;
    const miniHidden = !this.visible || !this.legendCollapsed;
    el.classList.toggle("is-hidden", panelHidden);
    mini.classList.toggle("is-hidden", miniHidden);
  }

  /**切换图例折叠 */
  toggleLegendCollapsed(collapsed) {
    this.legendCollapsed = typeof collapsed === "boolean"
      ? collapsed
      : !this.legendCollapsed;
    this.syncLegendDisplay();
  }

  /**显示图例 */
  showLegend() {
    this.syncLegendDisplay();
  }

  /**隐藏图例 */
  hideLegend() {
    this.syncLegendDisplay();
  }

  /**读取 JSON */
  async fetchJson(url) {
    const resp = await fetchWithTimeout(url, {}, 12000);
    if (!resp.ok) throw new Error(`台风接口请求失败: ${resp.status}`);
    return resp.json();
  }

  /**读取文本 */
  async fetchText(url) {
    const resp = await fetchWithTimeout(url, {}, 12000);
    if (!resp.ok) throw new Error(`台风文本请求失败: ${resp.status}`);
    return resp.text();
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

  /**是否登陆 */
  isLandfall(item) {
    return item?.isLand === true || item?.IsLand === true;
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
    const landText = this.isLandfall(item) ? "（登陆）" : "";
    return `${code} ${name}${tail}${landText}`.trim();
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

  /**按编号合并 */
  mergeItemsByCode(...groups) {
    const map = new Map();
    for (const group of groups) {
      for (const item of Array.isArray(group) ? group : []) {
        const code = this.getItemCode(item);
        if (!code) continue;
        const prev = map.get(code) || {};
        map.set(code, { ...prev, ...item, code });
      }
    }
    return Array.from(map.values()).sort((a, b) => this.getItemCode(b).localeCompare(this.getItemCode(a)));
  }

  /**获取编号序号 */
  getCodeSeq(code) {
    const txt = String(code || "").trim();
    const m = txt.match(/^(\d{4})(\d{2})$/);
    return m ? Number(m[2]) : NaN;
  }

  /**是否当前年份 */
  isCurrentYearCode(code) {
    const year = /^\d{4}/.test(String(code || "")) ? Number(String(code).slice(0, 4)) : NaN;
    return Number.isFinite(year) && year === new Date().getUTCFullYear();
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
    for (const item of this.mergeItemsByCode(this.historyList, this.currentList)) {
      const year = this.getYear(item);
      if (Number.isFinite(year) && year > 1900) set.add(year);
    }
    return Array.from(set.values()).sort((a, b) => b - a);
  }

  /**获取指定年份台风 */
  getYearItems(year) {
    const list = this.mergeItemsByCode(this.historyList, this.currentList);
    if (!year) return list;
    return list.filter(item => String(this.getYear(item)) === String(year));
  }

  /**查找台风项 */
  findItemByCode(code) {
    const key = String(code || "").trim();
    if (!key) return null;
    return this.mergeItemsByCode(this.historyList, this.currentList)
      .find(item => this.getItemCode(item) === key) || null;
  }

  /**是否当前活跃台风 */
  isCurrentCode(code) {
    const key = String(code || "").trim();
    if (!key) return false;
    return this.currentList.some(item => this.getItemCode(item) === key);
  }

  /**确保默认选择 */
  ensureDefaultSelection() {
    if (this.initSelectionReady) return;
    const years = this.getYears();
    if (!this.selectedYear && years.length) {
      this.selectedYear = String(years[0]);
    }
    const first = this.getYearItems(this.selectedYear)[0]
      || this.mergeItemsByCode(this.historyList, this.currentList)[0]
      || null;
    if (first) {
      this.selectedCode = this.getItemCode(first);
    }
    this.initSelectionReady = true;
  }

  /**渲染图例 */
  renderLegend() {
    const el = this.ensureLegend();
    const liveCnt = this.currentList.length;
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
        <span class="typhoon-legend-title-text">
          <span>台风路径</span>
        </span>
        <span class="typhoon-legend-actions">
          <span class="typhoon-legend-tip">${liveCnt > 0 ? `当前活跃 ${liveCnt} 个，实时源：Digital Typhoon` : "数据源：本地台风数据库"}</span>
          <button type="button" class="typhoon-legend-toggle" data-legend-action="collapse" title="收起台风面板" aria-label="收起台风面板">
            <i class="fa-solid fa-chevron-down"></i>
          </button>
        </span>
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
    const mini = this.ensureMiniButton();
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
    el.addEventListener("click", e => {
      const actionEl = e.target?.closest?.("[data-legend-action]");
      if (!actionEl) return;
      if (actionEl.dataset.legendAction === "collapse") {
        this.toggleLegendCollapsed(true);
      }
    });
    mini.addEventListener("click", () => this.toggleLegendCollapsed(false));
    this.bindLegendDrag();
    this.legendBound = true;
  }

  /**绑定图例拖动 */
  bindLegendDrag() {
    if (this.legendDragBound) return;
    const el = this.ensureLegend();
    const onMove = e => {
      if (!this.legendDragId) return;
      if (typeof e.pointerId === "number" && e.pointerId !== this.legendDragId) return;
      const rect = el.getBoundingClientRect();
      const ww = window.innerWidth || document.documentElement.clientWidth || 0;
      const wh = window.innerHeight || document.documentElement.clientHeight || 0;
      const left = Math.min(Math.max(8, e.clientX - this.legendDragOffsetX), Math.max(8, ww - rect.width - 8));
      const top = Math.min(Math.max(8, e.clientY - this.legendDragOffsetY), Math.max(8, wh - rect.height - 8));
      el.style.left = `${left}px`;
      el.style.top = `${top}px`;
      el.style.right = "auto";
      el.style.bottom = "auto";
      el.style.transform = "none";
      const head = el.querySelector(".typhoon-legend-title");
      if (head) head.classList.add("is-dragging");
      this.legendDragMoving = true;
    };
    const onUp = e => {
      if (!this.legendDragId) return;
      if (typeof e.pointerId === "number" && e.pointerId !== this.legendDragId) return;
      this.legendDragId = 0;
      const head = el.querySelector(".typhoon-legend-title");
      if (head) head.classList.remove("is-dragging");
      try {
        document.body.style.userSelect = "";
      } catch (_e) { }
    };
    el.addEventListener("pointerdown", e => {
      const head = e.target?.closest?.(".typhoon-legend-title");
      if (!head) return;
      if (e.target?.closest?.("select,option,input,button,a,label")) return;
      const rect = el.getBoundingClientRect();
      if (!this.legendDragMoving) {
        el.style.left = `${rect.left}px`;
        el.style.top = `${rect.top}px`;
        el.style.right = "auto";
        el.style.bottom = "auto";
        el.style.transform = "none";
      }
      this.legendDragId = typeof e.pointerId === "number" ? e.pointerId : 1;
      this.legendDragOffsetX = e.clientX - rect.left;
      this.legendDragOffsetY = e.clientY - rect.top;
      try {
        head.setPointerCapture?.(e.pointerId);
        document.body.style.userSelect = "none";
      } catch (_e) { }
      e.preventDefault();
    });
    window.addEventListener("pointermove", onMove);
    window.addEventListener("pointerup", onUp);
    window.addEventListener("pointercancel", onUp);
    this.legendDragBound = true;
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
    return this.getWindMeta(windMs).name;
  }

  msToBeaufortLevel(ms) {
    const v = Number(ms) || 0;
    const table = [
      { min: 0, max: 0.3, lvl: 0 },
      { min: 0.3, max: 1.6, lvl: 1 },
      { min: 1.6, max: 3.4, lvl: 2 },
      { min: 3.4, max: 5.5, lvl: 3 },
      { min: 5.5, max: 8.0, lvl: 4 },
      { min: 8.0, max: 10.8, lvl: 5 },
      { min: 10.8, max: 13.9, lvl: 6 },
      { min: 13.9, max: 17.2, lvl: 7 },
      { min: 17.2, max: 20.8, lvl: 8 },
      { min: 20.8, max: 24.5, lvl: 9 },
      { min: 24.5, max: 28.5, lvl: 10 },
      { min: 28.5, max: 32.7, lvl: 11 },
      { min: 32.7, max: 37.0, lvl: 12 },
      { min: 37.0, max: 41.5, lvl: 13 },
      { min: 41.5, max: 46.2, lvl: 14 },
      { min: 46.2, max: 51.0, lvl: 15 },
      { min: 51.0, max: 56.1, lvl: 16 },
      { min: 56.1, max: 61.3, lvl: 17 },
      { min: 61.3, max: Infinity, lvl: 18 }
    ];
    for (const item of table) {
      if (v >= item.min && v < item.max) return item.lvl;
    }
    return 0;
  }

  bearingToDirText(deg) {
    const v = ((Number(deg) || 0) % 360 + 360) % 360;
    const dirs = ["北", "北东北", "东北", "东东北", "东", "东东南", "东南", "南东南", "南", "南西南", "西南", "西西南", "西", "西西北", "西北", "北西北"];
    const idx = Math.round(v / 22.5) % 16;
    return dirs[idx] || "北";
  }

  haversineKm(a, b) {
    const lng1 = Number(a?.[0]), lat1 = Number(a?.[1]);
    const lng2 = Number(b?.[0]), lat2 = Number(b?.[1]);
    if (![lng1, lat1, lng2, lat2].every(Number.isFinite)) return 0;
    const toRad = d => d * Math.PI / 180;
    const R = 6371;
    const dLat = toRad(lat2 - lat1);
    const dLng = toRad(lng2 - lng1);
    const s1 = Math.sin(dLat / 2);
    const s2 = Math.sin(dLng / 2);
    const x = s1 * s1 + Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * s2 * s2;
    const c = 2 * Math.atan2(Math.sqrt(x), Math.sqrt(1 - x));
    return R * c;
  }

  bearingDeg(a, b) {
    const lng1 = Number(a?.[0]), lat1 = Number(a?.[1]);
    const lng2 = Number(b?.[0]), lat2 = Number(b?.[1]);
    if (![lng1, lat1, lng2, lat2].every(Number.isFinite)) return 0;
    const toRad = d => d * Math.PI / 180;
    const toDeg = r => r * 180 / Math.PI;
    const y = Math.sin(toRad(lng2 - lng1)) * Math.cos(toRad(lat2));
    const x = Math.cos(toRad(lat1)) * Math.sin(toRad(lat2)) - Math.sin(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.cos(toRad(lng2 - lng1));
    const brng = toDeg(Math.atan2(y, x));
    return ((brng % 360) + 360) % 360;
  }

  getMoveInfo(prevCoord, prevTime, coord, time) {
    const t1 = new Date(prevTime || "").getTime();
    const t2 = new Date(time || "").getTime();
    if (!Number.isFinite(t1) || !Number.isFinite(t2) || t2 <= t1) return null;
    const distKm = this.haversineKm(prevCoord, coord);
    const hours = (t2 - t1) / 3600000;
    if (!hours) return null;
    const speedKmh = distKm / hours;
    const bearing = this.bearingDeg(prevCoord, coord);
    return {
      speedKmh: Math.round(speedKmh),
      dirText: this.bearingToDirText(bearing),
      bearing: Math.round(bearing)
    };
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
          levelName: this.getWindMeta(windMs).name,
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
    const now = Date.now();
    if (this.currentStormMap.has(key)) {
      const cached = this.currentStormMap.get(key);
      if (cached && cached.__fetchedAt && cached.data) {
        if (now - Number(cached.__fetchedAt) < this.currentStormTtlMs) return cached.data;
      } else if (cached && (cached.track || cached.forecast)) {
        return cached;
      }
    }
    let trackData = null;
    let forecastData = null;
    try {
      trackData = await this.fetchJson(this.getCurrentTrackUrl(key));
    } catch (_e) { }
    try {
      forecastData = await this.fetchJson(this.getCurrentForecastUrl(key));
    } catch (_e) { }
    const data = {
      meta: {
        code: key,
        name: String(trackData?.properties?.name || trackData?.properties?.display_name || "").trim()
      },
      track: this.parseCurrentTrackGeoJson(trackData),
      forecast: this.parseCurrentForecastJson(forecastData)
    };
    this.currentStormMap.set(key, { __fetchedAt: now, data });
    return data;
  }

  /**提取在线候选编号 */
  extractCurrentCodes(text) {
    const list = String(text || "").match(/\b\d{6}\b/g) || [];
    return Array.from(new Set(list.filter(code => this.isCurrentYearCode(code))));
  }

  /**是否在线活跃 */
  isLiveCurrentData(data) {
    const track = Array.isArray(data?.track) ? data.track : [];
    const current = data?.forecast?.current || null;
    const latestTrack = track.length ? new Date(track[track.length - 1]?.time || "").getTime() : NaN;
    const latestForecast = current
      ? new Date(this.toUtcIsoFromUnix(current?.forecasttime) || this.toUtcIsoFromUnix(current?.basetime) || this.toUtcIsoFromJstText(current?.time)).getTime()
      : NaN;
    const latest = Math.max(Number.isFinite(latestTrack) ? latestTrack : 0, Number.isFinite(latestForecast) ? latestForecast : 0);
    if (!latest) return false;
    return latest >= Date.now() - 96 * 3600 * 1000;
  }

  /**确保当前活跃台风 */
  async ensureCurrentList() {
    const now = Date.now();
    if (this.currentList.length && now - this.currentListLoadedAt < 15 * 60 * 1000) {
      return this.currentList;
    }

    const curYear = new Date().getUTCFullYear();
    const yearCodes = this.historyList
      .map(item => this.getItemCode(item))
      .filter(code => String(code).startsWith(String(curYear)));
    let latestCodes = [];
    try {
      const text = await this.fetchText(this.currentListApi);
      latestCodes = this.extractCurrentCodes(text);
    } catch (_e) { }

    const codeSet = new Set([...yearCodes, ...latestCodes]);
    const seqList = Array.from(codeSet).map(code => this.getCodeSeq(code)).filter(Number.isFinite);
    const maxSeq = seqList.length ? Math.max(...seqList) : 0;
    for (let i = 1; i <= 4; i++) {
      const seq = maxSeq + i;
      if (seq <= 0 || seq > 99) continue;
      codeSet.add(`${curYear}${String(seq).padStart(2, "0")}`);
    }

    const list = [];
    const codes = Array.from(codeSet).sort((a, b) => b.localeCompare(a));
    const rows = await Promise.all(codes.map(async code => {
      const data = await this.ensureCurrentStormData(code).catch(() => null);
      return { code, data };
    }));

    for (const row of rows) {
      if (!this.isLiveCurrentData(row.data)) continue;
      const local = this.historyList.find(item => this.getItemCode(item) === row.code) || null;
      const item = {
        ...(local || {}),
        code: row.code,
        name: String(local?.name || local?.Name || row.data?.meta?.name || "").trim(),
        chineseName: this.getChineseName(local) || "",
        isLive: true
      };
      list.push(item);
    }

    this.currentList = this.mergeItemsByCode(list);
    this.currentListLoadedAt = now;
    return this.currentList;
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

  /**估算风圈半径 */
  estimateWindCircleKm(windMs) {
    const wind = Number(windMs) || 0;
    if (wind >= 51) return { r7: 320, r10: 150 };
    if (wind >= 41.5) return { r7: 280, r10: 120 };
    if (wind >= 32.7) return { r7: 240, r10: 90 };
    if (wind >= 24.5) return { r7: 200, r10: 60 };
    if (wind >= 17.2) return { r7: 150, r10: 0 };
    return { r7: 100, r10: 0 };
  }

  /**构建实时风圈 */
  buildLiveWindCircleFeatures(center, windMs, stormId) {
    if (!Array.isArray(center) || center.length < 2 || !center.every(Number.isFinite)) return [];
    const size = this.estimateWindCircleKm(windMs);
    const list = [];
    if (size.r7 > 0) {
      const item = this.buildCircleFeature("wind-7-circle", center, size.r7 * 1000, {
        stormId: stormId || "",
        label: "7级风圈"
      });
      if (item) list.push(item);
    }
    if (size.r10 > 0) {
      const item = this.buildCircleFeature("wind-10-circle", center, size.r10 * 1000, {
        stormId: stormId || "",
        label: "10级风圈"
      });
      if (item) list.push(item);
    }
    return list;
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
        iconSize: 2
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
      const bf = this.msToBeaufortLevel(windMs);
      const prev = i > 0 ? points[i - 1] : null;
      const prevCoord = prev ? [Number(prev?.lng || prev?.Lng), Number(prev?.lat || prev?.Lat)] : null;
      const move = prevCoord && prevCoord.every(Number.isFinite)
        ? this.getMoveInfo(prevCoord, prev?.timeUtc || prev?.TimeUtc, [lng, lat], item?.timeUtc || item?.TimeUtc)
        : null;
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
          pointVisible: 1,
          popupTitle,
          popupTime: timeText || "-",
          popupCoord: `${lng.toFixed(1)}, ${lat.toFixed(1)}`,
          popupPressure: pressure,
          popupWind: windMs,
          popupBeaufort: bf,
          popupLevel: levelName,
          popupMoveSpeed: move?.speedKmh ?? "",
          popupMoveDir: move?.dirText ?? "",
          popupType: i === points.length - 1 ? "当前位置" : "历史点"
        }
      });
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
        levelName: this.getWindMeta(windMs).name
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
      const bf = this.msToBeaufortLevel(item.windMs);
      const prev = i > 0 ? track[i - 1] : null;
      const move = prev?.coord && Array.isArray(prev.coord)
        ? this.getMoveInfo(prev.coord, prev.time, coord, item.time)
        : null;
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
          pointVisible: 1,
          popupTitle: `${code} ${name}`.trim(),
          popupTime: item.timeText || this.formatLocalTime(item.time),
          popupCoord: this.formatCoord(coord),
          popupPressure: item.pressure ?? "-",
          popupWind: Number(item.windMs) || 0,
          popupBeaufort: bf,
          popupLevel: item.levelName || windMeta.name,
          popupMoveSpeed: move?.speedKmh ?? "",
          popupMoveDir: move?.dirText ?? "",
          popupType: i === track.length - 1 ? "当前台风实况点" : "当前台风轨迹点"
        }
      });
    });

    const historyLine = this.buildPathFeature("history-path", actualCoords, { color: "#fb7185" });
    if (historyLine) features.push(historyLine);

    const currentItem = track[track.length - 1];
    const currentCenter = Array.isArray(currentItem?.coord) ? currentItem.coord : null;
    const agencyColor = "#7c3aed";
    if (currentCenter) {
      const circleSize = this.estimateWindCircleKm(currentItem?.windMs);
      features.push(...this.buildLiveWindCircleFeatures(currentCenter, currentItem?.windMs, stormId));
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
            pointVisible: 1,
            popupTitle: `${code} ${name}`.trim(),
            popupTime: item.timeText || this.formatLocalTime(item.time),
            popupCoord: this.formatCoord(coord),
            popupPressure: item.pressure ?? "-",
            popupWind: Number(item.windMs) || 0,
            popupBeaufort: this.msToBeaufortLevel(item.windMs),
            popupLevel: windMeta.name,
            popupProbKm: item.probKm ?? 0,
            popupType: `${item.period || 0}小时预报点`
          }
        });
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

      const currentPoint = features.find(f => f?.properties?.kind === "track-point" && f?.properties?.stormId === stormId && f?.properties?.isCurrent === 1);
      if (currentPoint?.properties) {
        currentPoint.properties.popupWindCircleEst = 1;
        currentPoint.properties.popupWind7Km = circleSize?.r7 ?? 0;
        currentPoint.properties.popupWind10Km = circleSize?.r10 ?? 0;
      }
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

  /**合并要素结果 */
  mergeFeatureResults(rows) {
    const features = [];
    const summaries = [];
    const currentStorms = [];
    for (const row of Array.isArray(rows) ? rows : []) {
      if (Array.isArray(row?.features) && row.features.length) {
        features.push(...row.features);
      }
      if (row?.summary) {
        summaries.push(row.summary);
      }
      if (Array.isArray(row?.currentStorms) && row.currentStorms.length) {
        currentStorms.push(...row.currentStorms);
      }
    }
    return { features, summaries, currentStorms };
  }

  /**构建全部活跃台风 */
  async buildCurrentListFeatures(code) {
    const key = String(code || "").trim();
    const extras = this.currentList
      .map(item => this.getItemCode(item))
      .filter(itemCode => !!itemCode && itemCode !== key);
    const codes = Array.from(new Set([key, ...extras].filter(Boolean)));
    const rows = await Promise.all(codes.map(async itemCode => {
      const item = this.findItemByCode(itemCode);
      return this.buildCurrentFeatures(item, itemCode).catch(() => ({
        features: [],
        summary: null,
        currentStorms: []
      }));
    }));
    const merged = this.mergeFeatureResults(rows);
    return {
      ...merged,
      summary: rows[0]?.summary || null,
      activeCnt: merged.summaries.length
    };
  }

  /**生成图层 ID 列表 */
  getLayerIds() {
    return [
      this.wind7FillLayerId,
      this.wind7LineLayerId,
      this.wind10FillLayerId,
      this.wind10LineLayerId,
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
    if (!map.getLayer(this.wind7FillLayerId)) {
      map.addLayer({
        id: this.wind7FillLayerId,
        type: "fill",
        source: this.sourceId,
        filter: ["==", ["get", "kind"], "wind-7-circle"],
        paint: {
          "fill-color": "#86efac",
          "fill-opacity": 0.18
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
          "line-color": "#4ade80",
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
          "fill-color": "#67e8f9",
          "fill-opacity": 0.2
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
          "line-color": "#22d3ee",
          "line-width": 1.4,
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
        filter: ["all", ["==", ["get", "kind"], "track-point"], ["==", ["coalesce", ["get", "pointVisible"], 1], 1]],
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
    const windMs = props.popupWind ?? "-";
    const bf = props.popupBeaufort !== undefined && props.popupBeaufort !== null && props.popupBeaufort !== "" ? props.popupBeaufort : "";
    const windText = bf !== "" ? `${windMs} m/s，${bf}级` : `${windMs} m/s`;
    const moveText = (props.popupMoveSpeed && props.popupMoveDir) ? `${props.popupMoveSpeed} 公里/小时，${props.popupMoveDir}` : "";
    const hasCircle = (Number(props.popupWind7Km) || 0) > 0 || (Number(props.popupWind10Km) || 0) > 0;
    const circleText = hasCircle
      ? `7级≈${Number(props.popupWind7Km) || 0} 公里，10级≈${Number(props.popupWind10Km) || 0} 公里${props.popupWindCircleEst ? "（估算）" : ""}`
      : "";
    const probText = (Number(props.popupProbKm) || 0) > 0 ? `${props.popupProbKm} 公里` : "";
    return `
      <div class="typhoon-popup-card">
        <div class="typhoon-popup-head">${props.popupTitle || "台风点位"}</div>
        <div class="typhoon-popup-body">
          <div><b>时间：</b>${props.popupTime || "-"}</div>
          <div><b>坐标：</b>${props.popupCoord || "-"}</div>
          <div><b>中心气压：</b>${props.popupPressure || "-"} hPa</div>
          <div><b>风速风力：</b>${windText}</div>
          <div><b>等级：</b>${props.popupLevel || "-"}</div>
          ${moveText ? `<div><b>移速移向：</b>${moveText}</div>` : ``}
          ${circleText ? `<div><b>风圈：</b>${circleText}</div>` : ``}
          ${probText ? `<div><b>预测圈：</b>${probText}</div>` : ``}
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
  async onPointClick(e) {
    const feature = e?.features?.[0];
    if (!feature) return;
    const props = feature?.properties || {};
    if (Number(props.isCurrent) === 1) {
      const code = String(props.code || "").trim();
      const stormId = String(props.stormId || "").trim();
      await this.refreshCurrentStormIfNeeded(code);
      const pointFeature = stormId ? this.getCurrentPointByStormId(stormId) : null;
      if (pointFeature) {
        this.showPopup(pointFeature, pointFeature.geometry?.coordinates || e.lngLat);
        return;
      }
    }
    this.showPopup(feature, e.lngLat);
  }

  getCurrentPointByStormId(stormId) {
    const { map } = this.runtime;
    const list = map.querySourceFeatures(this.sourceId) || [];
    const found = list.find(f => f?.properties?.kind === "track-point" && f?.properties?.stormId === stormId && Number(f?.properties?.isCurrent) === 1);
    return found || null;
  }

  getCodeFromStormId(stormId) {
    const s = String(stormId || "").trim();
    if (!s) return "";
    if (s.startsWith("current-")) return s.slice("current-".length);
    return s;
  }

  async refreshCurrentStormIfNeeded(code) {
    const key = String(code || "").trim();
    if (!key) return;
    const now = Date.now();
    if (this.liveRefreshAt && now - this.liveRefreshAt < 15000) return;
    this.liveRefreshAt = now;
    this.currentStormMap.delete(key);
    this.currentListLoadedAt = 0;
    await this.refresh();
  }

  onCenterMove(e) {
    const feature = e?.features?.[0];
    if (!feature) return;
    const stormId = feature?.properties?.stormId;
    const pointFeature = this.getCurrentPointByStormId(stormId);
    if (!pointFeature) return;
    this.showPopup(pointFeature, e.lngLat);
  }

  onCenterLeave() {
    const { map } = this.runtime;
    map.getCanvas().style.cursor = "";
    this.hidePopup();
  }

  async onCenterClick(e) {
    const centerFeature = e?.features?.[0];
    if (!centerFeature) return;
    const stormId = centerFeature?.properties?.stormId;
    const code = this.getCodeFromStormId(stormId);
    await this.refreshCurrentStormIfNeeded(code);
    const pointFeature = this.getCurrentPointByStormId(stormId);
    if (!pointFeature) return;
    this.showPopup(pointFeature, pointFeature.geometry?.coordinates || e.lngLat);
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

    map.on("mouseenter", this.centerLayerId, () => {
      map.getCanvas().style.cursor = "pointer";
    });
    map.on("mousemove", this.centerLayerId, this.onCenterMove);
    map.on("mouseleave", this.centerLayerId, this.onCenterLeave);
    map.on("click", this.centerLayerId, this.onCenterClick);

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
    await this.ensureCurrentList();
    this.ensureDefaultSelection();
    if (!this.selectedCode) {
      const first = this.getYearItems(this.selectedYear)[0]
        || this.mergeItemsByCode(this.historyList, this.currentList)[0]
        || this.historyList[0];
      this.selectedCode = this.getItemCode(first);
    }
    this.renderLegend();
    this.showLegend();

    if (!this.selectedCode) {
      addOrUpdateGeoJsonSource(this.runtime.map, this.sourceId, { type: "FeatureCollection", features: [] });
      this.clearDataTime();
      this.setInfoExtra("暂无台风数据");
      return true;
    }

    const selectedItem = this.findItemByCode(this.selectedCode);
    const [typhoon, logs, predicts] = await Promise.all([
      this.fetchTyphoon(this.selectedCode).catch(() => selectedItem),
      this.fetchLogs(this.selectedCode).catch(() => []),
      this.fetchPredict(this.selectedCode).catch(() => [])
    ]);
    const baseTyphoon = typhoon || selectedItem;
    let result = this.buildFeatures(baseTyphoon, logs, predicts);
    let summary = result.summary || null;
    let currentStorms = result.currentStorms || [];
    let activeCnt = 0;
    if (this.isCurrentCode(this.selectedCode)) {
      const liveResult = await this.buildCurrentListFeatures(this.selectedCode);
      if (liveResult?.features?.length) {
        result = liveResult;
        summary = liveResult.summary || null;
        currentStorms = liveResult.currentStorms || [];
        activeCnt = Number(liveResult.activeCnt) || 0;
      }
    } else if (this.shouldUseLiveData(baseTyphoon, logs)) {
      const liveResult = await this.buildCurrentFeatures(baseTyphoon, this.selectedCode);
      if (liveResult?.features?.length) result = liveResult;
      summary = liveResult?.summary || summary;
      currentStorms = liveResult?.currentStorms || currentStorms;
      activeCnt = liveResult?.summary ? 1 : 0;
    }
    const { features } = result;
    await this.ensureCenterImages(currentStorms);
    addOrUpdateGeoJsonSource(this.runtime.map, this.sourceId, { type: "FeatureCollection", features });
    this.ensureLayers();
    this.ensureInteractions();
    this.syncCenterMarkers(currentStorms);

    if (!summary) {
      this.clearDataTime();
      this.setInfoExtra(activeCnt > 0 ? `活跃:${activeCnt}` : `台风:${this.selectedCode}`);
    } else if (summary.mode === "current") {
      this.setDataTimeText(summary.endText || "");
      this.setInfoExtra(activeCnt > 1 ? `活跃:${activeCnt}` : `${summary.code}`);
    } else {
      this.setDataTimeText(summary.endText || "");
      this.setInfoExtra(`${summary.code}`);
    }
    this.setOpacity(this.runtime.getOpacity(this.name));
    this.lastStatus = true;
    this.lastTime = Date.now();
    return true;
  }

  /**设置透明度 */
  setOpacity(opacity) {
    const { map } = this.runtime;
    const lineIds = [this.wind7LineLayerId, this.wind10LineLayerId, this.probLayerId, this.historyLayerId, this.forecastLayerId];
    const fillIds = [this.wind7FillLayerId, this.wind10FillLayerId, this.probFillLayerId];
    const circleIds = [this.pointLayerId];
    const textIds = [this.pointLabelLayerId, this.tailLabelLayerId, this.nameLayerId];
    const iconIds = [this.centerLayerId];
    lineIds.forEach(id => {
      if (map.getLayer(id)) map.setPaintProperty(id, "line-opacity", opacity);
    });
    fillIds.forEach(id => {
      if (!map.getLayer(id)) return;
      let base = 0.08;
      if (id === this.wind7FillLayerId) base = 0.18;
      if (id === this.wind10FillLayerId) base = 0.2;
      map.setPaintProperty(id, "fill-opacity", opacity * base);
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
    this.clearDataTime();
    this.setInfoExtra("");
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
