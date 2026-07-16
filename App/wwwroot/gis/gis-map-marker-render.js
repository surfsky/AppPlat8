/**
 * Render point markers in dock and normal modes.
 *
 * Usage:
 * const markerRender = new MapMarkerRender({
 *   dock: [
 *     { position: "top", margin: 20 },
 *     { position: "bottom", margin: 20 },
 *     { position: "left", margin: 16 },
 *     { position: "right", margin: 16 }
 *   ],
 *   dockStyle: {
 *     labelBg: "rgba(8, 18, 36, 0.84)",
 *     labelWidth: 170,
 *     cardHeight: 56,
 *     fields: {
 *       no: "id",
 *       title: "name",
 *       sub: ["type", "area"]
 *     }
 *   },
 *   normalStyle: {
 *     labelBg: "rgba(8, 18, 36, 0.84)",
 *     content: "{id}.{name}"
 *   },
 *   pointClick: (item) => {
 *     console.log(item);
 *   }
 * });
 * map.addControl(markerRender.RenderControl(), "top-right");
 * markerRender.render({
 *   map,
 *   points,
 *   renderMode: "normal",
 *   renderMarker: true,
 *   renderLabel: true,
 *   renderLine: true
 * });
 */
class MapMarkerRender {
  /**Create marker renderer */
  constructor(config = {}) {
    this.map = null;
    this.points = [];
    this.entries = [];
    this.flags = { marker: true, label: true, leader: true, normal: true };
    this.boundRender = () => this.requestRender();
    this.boundMoveStart = () => this.handleMoveStart();
    this.boundMoveEnd = () => this.handleMoveEnd();
    this.rafId = 0;
    this.rowGap = 10;
    this.colGap = 10;
    this.cardHeight = 56;
    this.markerSize = 18;
    this.eventsBound = false;
    this.isInteracting = false;
    this.styleId = "mmr-style";
    this.controls = [];
    this.allDocks = [];
    this.activeDockSet = new Set();
    this.leaderMarkup = "";
    this.pointState = new Map();
    this.boundDocPointerDown = (evt) => this.handleDocumentPointerDown(evt);
    this.ensureStyles();
    this.setConfig(config);
  }

  /**Update renderer config */
  setConfig(config = {}) {
    const next = config && typeof config === "object" ? config : {};
    const dockStyle = this.buildDockStyle(next.dockStyle);
    const normalStyle = this.buildNormalStyle(next.normalStyle, dockStyle);
    const docks = this.normalizeDocks(next.dock);
    this.allDocks = docks;
    this.syncActiveDocks(docks);
    this.config = {
      dock: docks,
      dockStyle,
      normalStyle,
      pointClick: typeof next.pointClick === "function" ? next.pointClick : null,
      performance: {
        hideLeaderOnMove: next?.performance?.hideLeaderOnMove !== false
      }
    };
    this.rowGap = dockStyle.rowGap;
    this.colGap = dockStyle.colGap;
    this.cardHeight = dockStyle.cardHeight;
    this.markerSize = this.getActiveMarkerSize();
    this.applyStyleVars();
    return this;
  }

  /**Render markers, labels and lines */
  render(args = {}) {
    const next = args && typeof args === "object" ? args : {};
    const mode = this.normalizeRenderMode(next.renderMode);
    this.map = next.map || null;
    this.points = Array.isArray(next.points) ? next.points : [];
    this.flags = {
      marker: next.renderMarker !== false,
      label: next.renderLabel !== false,
      leader: next.renderLine !== false,
      normal: mode === "normal"
    };
    this.markerSize = this.getActiveMarkerSize();

    this.ensureOverlay();
    this.applyStyleVars();
    this.ensureMapEvents();
    this.syncEntries();
    this.updateControlStates();
    this.emitRenderModeChange();
    this.requestRender();
    return this;
  }

  /**Replace point list only */
  setPoints(points = []) {
    this.points = Array.isArray(points) ? points : [];
    this.syncEntries();
    this.requestRender();
    return this;
  }

  /**Update render options only */
  setRenderOptions(options = {}) {
    const next = options && typeof options === "object" ? options : {};
    const mode = this.normalizeRenderMode(next.renderMode ?? (this.flags?.normal ? "normal" : "dock"));
    this.flags = {
      marker: next.renderMarker !== false,
      label: next.renderLabel !== false,
      leader: next.renderLine !== false,
      normal: mode === "normal"
    };
    this.markerSize = this.getActiveMarkerSize();
    this.applyStyleVars();
    this.updateControlStates();
    this.emitRenderModeChange();
    this.requestRender();
    return this;
  }

  /**Set per-point visibility */
  setPointVisible(id, visible = true) {
    const state = this.ensurePointState(id);
    state.visible = visible !== false;
    this.requestRender();
    return this;
  }

  /**Set per-point selected state */
  setPointSelected(id, selected = true) {
    const state = this.ensurePointState(id);
    state.selected = selected === true;
    this.requestRender();
    return this;
  }

  /**Set per-point clickable state */
  setPointClickable(id, clickable = true) {
    const state = this.ensurePointState(id);
    state.clickable = clickable !== false;
    this.requestRender();
    return this;
  }

  /**Set per-point custom meta state */
  setPointMeta(id, meta = {}) {
    const state = this.ensurePointState(id);
    state.meta = meta && typeof meta === "object" ? { ...(state.meta || {}), ...meta } : (state.meta || {});
    this.requestRender();
    return this;
  }

  /**Get per-entry state */
  getEntryState(entry) {
    if (!entry) return null;
    return this.pointState.get(String(entry.id)) || null;
  }

  /**Ensure per-point state exists */
  ensurePointState(id) {
    const key = String(id ?? "");
    const current = this.pointState.get(key) || {};
    if (!this.pointState.has(key)) this.pointState.set(key, current);
    return current;
  }

  /**Get per-point state */
  getPointState(item, idx) {
    const key = String(this.getPointId(item, idx));
    return this.pointState.get(key) || null;
  }

  /**Normalize render mode */
  normalizeRenderMode(mode) {
    const val = String(mode ?? "normal").trim().toLowerCase();
    return val === "normal" ? "normal" : "dock";
  }

  /**Destroy renderer */
  destroy() {
    cancelAnimationFrame(this.rafId || 0);
    this.rafId = 0;

    if (this.map) {
      this.map.off("move", this.boundRender);
      this.map.off("zoom", this.boundRender);
      this.map.off("resize", this.boundRender);
      this.map.off("movestart", this.boundMoveStart);
      this.map.off("zoomstart", this.boundMoveStart);
      this.map.off("dragstart", this.boundMoveStart);
      this.map.off("pitchstart", this.boundMoveStart);
      this.map.off("rotatestart", this.boundMoveStart);
      this.map.off("moveend", this.boundMoveEnd);
      this.map.off("zoomend", this.boundMoveEnd);
      this.map.off("dragend", this.boundMoveEnd);
      this.map.off("pitchend", this.boundMoveEnd);
      this.map.off("rotateend", this.boundMoveEnd);
      this.map.off("idle", this.boundMoveEnd);
    }
    window.removeEventListener("resize", this.boundRender);

    this.entries.forEach(entry => this.removeEntry(entry));
    this.entries = [];

    if (this.overlayEl?.parentNode) this.overlayEl.parentNode.removeChild(this.overlayEl);
    this.overlayEl = null;
    this.labelLayer = null;
    this.leaderLayer = null;
    this.leaderMarkup = "";
    document.removeEventListener("pointerdown", this.boundDocPointerDown);
    this.eventsBound = false;
    this.controls = [];
    this.map = null;
  }

  /**Build final style config */
  buildDockStyle(style = {}) {
    const next = style && typeof style === "object" ? style : {};
    const fields = next?.fields && typeof next.fields === "object" ? next.fields : {};
    return {
      markerSize: this.parseSize(next.markerSize, 18),
      labelBg: String(next.labelBg ?? "rgba(8, 18, 36, 0.82)"),
      labelBorder: String(next.labelBorder ?? "rgba(147, 197, 253, 0.22)"),
      labelShadow: String(next.labelShadow ?? "0 14px 32px rgba(2, 6, 23, 0.36)"),
      labelRadius: this.parseSize(next.labelRadius, 16),
      labelPaddingY: this.parseSize(next.labelPaddingY, 10),
      labelPaddingX: this.parseSize(next.labelPaddingX, 12),
      labelText: String(next.labelText ?? "#f8fafc"),
      labelSubText: String(next.labelSubText ?? "#cbd5e1"),
      labelBadgeBg: String(next.labelBadgeBg ?? "linear-gradient(135deg, rgba(96, 165, 250, 0.24) 0%, rgba(37, 99, 235, 0.34) 100%)"),
      labelBadgeText: String(next.labelBadgeText ?? "#dbeafe"),
      labelHoverBg: String(next.labelHoverBg ?? "rgba(15, 30, 56, 0.92)"),
      labelWidth: this.parseSize(next.labelWidth, 220),
      cardHeight: this.parseSize(next.cardHeight, 56),
      rowGap: this.parseSize(next.rowGap, 10),
      colGap: this.parseSize(next.colGap, 10),
      titleSize: this.parseSize(next.titleSize, 14),
      subSize: this.parseSize(next.subSize, 12),
      lineColor: String(next.lineColor ?? "rgba(255, 255, 255, 0.96)"),
      guideColor: String(next.guideColor ?? "rgba(255, 255, 255, 0.28)"),
      lineWidth: this.parseSize(next.lineWidth, 2.6),
      dotRadius: this.parseSize(next.dotRadius, 4.8),
      anchorDotRadius: this.parseSize(next.anchorDotRadius, 4.2),
      fields: {
        no: fields.no ?? "id",
        title: fields.title ?? "name",
        sub: fields.sub ?? ["type", "area"]
      }
    };
  }

  /**Build normal mode style config */
  buildNormalStyle(style = {}, dockStyle = null) {
    const next = style && typeof style === "object" ? style : {};
    return {
      markerSize: this.parseSize(next.markerSize, dockStyle?.markerSize ?? 18),
      labelBg: String(next.labelBg ?? dockStyle?.labelBg ?? "rgba(8, 18, 36, 0.82)"),
      labelBorder: String(next.labelBorder ?? dockStyle?.labelBorder ?? "rgba(147, 197, 253, 0.22)"),
      labelShadow: String(next.labelShadow ?? dockStyle?.labelShadow ?? "0 14px 32px rgba(2, 6, 23, 0.36)"),
      labelRadius: this.parseSize(next.labelRadius, 10),
      labelPaddingY: this.parseSize(next.labelPaddingY, 4),
      labelPaddingX: this.parseSize(next.labelPaddingX, 8),
      labelText: String(next.labelText ?? dockStyle?.labelText ?? "#f8fafc"),
      labelHoverBg: String(next.labelHoverBg ?? dockStyle?.labelHoverBg ?? "rgba(15, 30, 56, 0.92)"),
      textSize: this.parseSize(next.textSize, 12),
      maxWidth: this.parseSize(next.maxWidth, 180),
      offsetY: this.parseSize(next.offsetY, 8),
      content: next.content ?? null
    };
  }

  /**Get dock mode style */
  getDockStyle() {
    return this.config?.dockStyle || {};
  }

  /**Get normal mode style */
  getNormalStyle() {
    return this.config?.normalStyle || {};
  }

  /**Get active marker size */
  getActiveMarkerSize() {
    const style = this.flags?.normal ? this.getNormalStyle() : this.getDockStyle();
    return this.parseSize(style?.markerSize, 18);
  }

  /**Normalize dock config list */
  normalizeDocks(docks) {
    const source = Array.isArray(docks) && docks.length > 0
      ? docks
      : [
          { position: "top", margin: 20 },
          { position: "bottom", margin: 20 },
          { position: "left", margin: 20 },
          { position: "right", margin: 20 }
        ];

    const seen = new Set();
    const list = [];
    source.forEach(item => {
      const dock = this.normalizeDock(item);
      if (!dock || seen.has(dock.position)) return;
      seen.add(dock.position);
      list.push(dock);
    });
    return list;
  }

  /**Normalize single dock item */
  normalizeDock(item) {
    const pos = String(item?.position || "").trim().toLowerCase();
    if (!["top", "bottom", "left", "right"].includes(pos)) return null;
    return {
      position: pos,
      margin: this.parseSize(item?.margin, 20)
    };
  }

  /**Parse px-like size */
  parseSize(value, fallback) {
    if (typeof value === "number" && Number.isFinite(value)) return value;
    if (typeof value === "string") {
      const num = parseFloat(value);
      if (Number.isFinite(num)) return num;
    }
    return fallback;
  }

  /**Keep active dock selection in sync */
  syncActiveDocks(docks) {
    const positions = Array.isArray(docks) ? docks.map(s => s.position) : [];
    const prev = new Set(Array.from(this.activeDockSet || []).filter(pos => positions.includes(pos)));
    this.activeDockSet = prev.size > 0 ? prev : new Set(positions);
  }

  /**Get active dock list */
  getActiveDocks() {
    return this.allDocks.filter(dock => this.activeDockSet.has(dock.position));
  }

  /**Set dock visibility */
  setDockVisible(position, visible = true) {
    const pos = String(position || "").trim().toLowerCase();
    if (!this.hasDock(pos)) return this;
    if (!["top", "bottom", "left", "right"].includes(pos)) return this;
    if (visible) {
      this.activeDockSet.add(pos);
    } else {
      if (this.activeDockSet.size <= 1 && this.activeDockSet.has(pos)) return this;
      this.activeDockSet.delete(pos);
    }
    this.updateControlStates();
    this.requestRender();
    return this;
  }

  /**Check whether dock exists in config */
  hasDock(position) {
    return this.allDocks.some(dock => dock.position === position);
  }

  /**Ensure overlay host exists */
  ensureOverlay() {
    if (!this.map) return;
    const app = this.getAppEl();
    if (!app) return;
    this.ensureStyles();

    if (!this.overlayEl) {
      this.overlayEl = document.createElement("div");
      this.overlayEl.className = "mmr-overlay";
      this.overlayEl.innerHTML = `
        <svg class="mmr-leader-layer"></svg>
        <div class="mmr-label-layer"></div>
      `;
      app.appendChild(this.overlayEl);
      this.labelLayer = this.overlayEl.querySelector(".mmr-label-layer");
      this.leaderLayer = this.overlayEl.querySelector(".mmr-leader-layer");
      this.applyStyleVars();
    }
  }

  /**Inject renderer stylesheet */
  ensureStyles() {
    if (document.getElementById(this.styleId)) return;
    const style = document.createElement("style");
    style.id = this.styleId;
    style.textContent = `
      .mmr-overlay {
        position: absolute;
        inset: 0;
        pointer-events: none;
        overflow: hidden;
        z-index: 8;
      }
      .mmr-leader-layer,
      .mmr-label-layer {
        position: absolute;
        inset: 0;
      }
      .mmr-leader-layer {
        overflow: visible;
      }
      .mmr-label-layer {
        z-index: 2;
        pointer-events: none;
      }
      .mmr-map-marker {
        position: absolute;
        width: 0;
        height: 0;
        overflow: visible;
      }
      .mmr-marker-icon {
        position: absolute;
        left: 0;
        top: 0;
        width: var(--mmr-marker-size);
        height: var(--mmr-marker-size);
        transform: translate(-50%, -50%) scale(var(--mmr-marker-scale, 1));
        transform-origin: center center;
        display: flex;
        align-items: center;
        justify-content: center;
        overflow: visible;
      }
      .mmr-marker-glyph {
        position: relative;
        width: 100%;
        height: 100%;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        color: #ffffff;
        font-size: calc(var(--mmr-marker-size) * 0.56);
        line-height: 1;
      }
      .mmr-marker-glyph svg,
      .mmr-marker-glyph img {
        width: 100%;
        height: 100%;
        display: block;
      }
      .mmr-marker-glyph img {
        object-fit: contain;
      }
      .mmr-marker-glyph svg {
        fill: currentColor;
      }
      .mmr-marker-glyph.is-empty::before {
        content: "";
        width: 12px;
        height: 12px;
        border-radius: 999px;
        background: #60a5fa;
        border: 2px solid #ffffff;
        box-shadow: 0 2px 8px rgba(15, 23, 42, 0.3);
      }
      .mmr-marker-inline {
        position: absolute;
        left: 0;
        top: calc(var(--mmr-marker-size) / 2 + 8px);
        transform: translateX(-50%);
        min-width: 0;
        max-width: min(var(--mmr-normal-max-width), 40vw);
        padding: var(--mmr-normal-label-padding-y) var(--mmr-normal-label-padding-x);
        border-radius: var(--mmr-normal-label-radius);
        background: transparent;
        border: 0;
        box-shadow: none;
        color: var(--mmr-normal-label-text);
        font-size: var(--mmr-normal-text-size);
        font-weight: 700;
        line-height: 1.2;
        letter-spacing: 0.01em;
        text-align: center;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
        text-shadow:
          -1px -1px 0 rgba(255, 255, 255, 0.96),
          0 -1px 0 rgba(255, 255, 255, 0.96),
          1px -1px 0 rgba(255, 255, 255, 0.96),
          -1px 0 0 rgba(255, 255, 255, 0.96),
          1px 0 0 rgba(255, 255, 255, 0.96),
          -1px 1px 0 rgba(255, 255, 255, 0.96),
          0 1px 0 rgba(255, 255, 255, 0.96),
          1px 1px 0 rgba(255, 255, 255, 0.96);
        display: none;
      }
      .mmr-marker-inline.is-clickable {
        cursor: pointer;
      }
      .mmr-marker-icon.is-clickable {
        cursor: pointer;
      }
      .mmr-marker-inline.is-clickable:hover {
        background: transparent;
      }
      .mmr-marker-icon.is-clickable:hover {
        filter: drop-shadow(0 0 8px rgba(59, 130, 246, 0.26));
      }
      .mmr-map-marker.is-normal .mmr-marker-inline {
        display: block;
      }
      .mmr-map-marker.is-selected .mmr-marker-glyph,
      .mmr-map-marker.is-selected .mmr-marker-inline {
        filter: drop-shadow(0 0 8px rgba(59, 130, 246, 0.32));
      }
      .mmr-map-marker.hide-icon .mmr-marker-icon {
        display: none;
      }
      .mmr-map-marker.hide-all {
        display: none;
      }
      .mmr-label-card {
        position: absolute;
        min-height: var(--mmr-dock-card-height);
        padding: var(--mmr-dock-label-padding-y) var(--mmr-dock-label-padding-x);
        display: flex;
        align-items: center;
        gap: 10px;
        border-radius: var(--mmr-dock-label-radius);
        background: var(--mmr-dock-label-bg);
        border: 1px solid var(--mmr-dock-label-border);
        box-shadow: var(--mmr-dock-label-shadow);
        backdrop-filter: blur(8px);
        transform: translateZ(0);
        pointer-events: auto;
        cursor: default;
        transition: background-color 0.18s ease, transform 0.18s ease;
      }
      .mmr-label-card.is-clickable {
        cursor: pointer;
      }
      .mmr-label-card.is-clickable:hover {
        background: var(--mmr-dock-label-hover-bg);
        transform: translateY(-1px);
      }
      .mmr-label-card.is-selected {
        border-color: rgba(96, 165, 250, 0.68);
        box-shadow: 0 0 0 1px rgba(96, 165, 250, 0.22), var(--mmr-dock-label-shadow);
      }
      .mmr-label-card.mmr-side-left {
        text-align: left;
        justify-content: flex-start;
      }
      .mmr-label-card.mmr-side-right {
        text-align: left;
        justify-content: flex-start;
      }
      .mmr-label-card.mmr-side-top,
      .mmr-label-card.mmr-side-bottom {
        justify-content: center;
        text-align: center;
      }
      .mmr-label-no {
        flex: 0 0 auto;
        min-width: 28px;
        height: 28px;
        padding: 0 8px;
        border-radius: 999px;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        background: var(--mmr-dock-label-badge-bg);
        color: var(--mmr-dock-label-badge-text);
        font-size: 12px;
        font-weight: 700;
      }
      .mmr-label-main {
        min-width: 0;
        flex: 1 1 auto;
        text-align: left;
      }
      .mmr-label-card.mmr-side-top .mmr-label-main,
      .mmr-label-card.mmr-side-bottom .mmr-label-main {
        text-align: center;
      }
      .mmr-label-title {
        font-size: var(--mmr-dock-title-size);
        font-weight: 700;
        line-height: 1.25;
        color: var(--mmr-dock-label-text);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }
      .mmr-label-sub {
        margin-top: 3px;
        font-size: var(--mmr-dock-sub-size);
        line-height: 1.35;
        color: var(--mmr-dock-label-sub-text);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }
      .mmr-leader-line {
        fill: none;
        stroke: var(--mmr-dock-line-color);
        stroke-width: var(--mmr-dock-line-width);
        stroke-linecap: round;
        stroke-linejoin: round;
        filter: drop-shadow(0 0 4px rgba(59, 130, 246, 0.28));
      }
      .mmr-leader-guide {
        fill: none;
        stroke: var(--mmr-dock-guide-color);
        stroke-width: 1.4;
        stroke-dasharray: 3 5;
        opacity: 0.7;
      }
      .mmr-leader-dot {
        fill: #dbeafe;
        stroke: rgba(37, 99, 235, 0.72);
        stroke-width: 2.4;
      }
      .mmr-leader-anchor-dot {
        fill: var(--mmr-dock-line-color);
        opacity: 0.98;
        stroke: rgba(191, 219, 254, 0.9);
        stroke-width: 1.4;
      }
      .mmr-mode-ctrl {
        display: flex;
        align-items: center;
        justify-content: center;
        background: rgba(3, 16, 80, 0.86) !important;
        color: #dbeafe !important;
        border: 0 !important;
        border-bottom: 1px solid rgba(10, 100, 180, 0.45) !important;
        box-shadow: none !important;
        text-shadow: none !important;
        outline: none !important;
        -webkit-appearance: none;
        appearance: none;
      }
      .mmr-mode-ctrl svg {
        width: 18px;
        height: 18px;
        fill: currentColor;
      }
      .mapboxgl-ctrl-top-right,
      .mapboxgl-ctrl-top-left,
      .mapboxgl-ctrl-bottom-right,
      .mapboxgl-ctrl-bottom-left {
        z-index: 1300 !important;
      }
      .mmr-ctrl-wrap {
        position: relative;
        overflow: visible !important;
        z-index: 1200 !important;
      }
      .mmr-ctrl-btns {
        position: relative;
        z-index: 1;
      }
      .mmr-ctrl-pop {
        position: absolute;
        top: 0;
        right: calc(100% + 10px);
        min-width: 220px;
        padding: 12px;
        border-radius: 16px;
        background: rgba(8, 18, 36, 0.94);
        border: 1px solid rgba(148, 163, 184, 0.24);
        box-shadow: 0 18px 36px rgba(2, 6, 23, 0.4);
        backdrop-filter: blur(12px);
        color: #e2e8f0;
        pointer-events: auto;
        display: none;
        z-index: 1201;
      }
      .mmr-ctrl-pop[hidden] {
        display: none !important;
      }
      .mmr-ctrl-pop.is-open {
        display: block;
      }
      .mmr-ctrl-groupbox + .mmr-ctrl-groupbox {
        margin-top: 10px;
      }
      .mmr-ctrl-groupbox {
        padding: 0;
        border: 0;
        background: transparent;
        box-shadow: none;
      }
      .mmr-ctrl-groupbox.is-hidden {
        display: none;
      }
      .mmr-ctrl-group-title {
        margin-bottom: 8px;
        font-size: 12px;
        line-height: 1.2;
        color: #94a3b8;
        font-weight: 600;
      }
      .mmr-ctrl-groupbuttons {
        display: flex;
        gap: 0;
        padding: 0;
        border-radius: 12px;
        background: rgba(3, 10, 24, 0.88);
        overflow: hidden;
      }
      .mmr-ctrl-option,
      .mmr-dock-chip {
        appearance: none;
        -webkit-appearance: none;
        flex: 1 1 0;
        min-width: 0;
        border: 0;
        border-right: 1px solid rgba(148, 163, 184, 0.22);
        background: transparent;
        color: #cbd5e1;
        border-radius: 0;
        padding: 10px 12px;
        font-size: 12px;
        line-height: 1;
        font-weight: 600;
        cursor: pointer;
        transition: all 0.18s ease;
        position: relative;
        outline: none;
        box-shadow: none;
        background-image: none;
      }
      .mmr-ctrl-groupbuttons > button,
      .mmr-ctrl-groupbuttons > button + button {
        border-top: 0 !important;
        box-shadow: none !important;
      }
      .mmr-ctrl-option:last-child,
      .mmr-dock-chip:last-child {
        border-right: 0;
      }
      .mmr-ctrl-option:first-child,
      .mmr-dock-chip:first-child {
        border-top-left-radius: 9px;
        border-bottom-left-radius: 9px;
      }
      .mmr-ctrl-option:last-child,
      .mmr-dock-chip:last-child {
        border-top-right-radius: 9px;
        border-bottom-right-radius: 9px;
      }
      .mmr-ctrl-option:hover,
      .mmr-dock-chip:hover {
        background: rgba(30, 41, 59, 0.42);
        color: #eff6ff;
      }
      .mmr-ctrl-option.is-active,
      .mmr-dock-chip.is-active {
        background: linear-gradient(180deg, rgba(37, 99, 235, 0.58) 0%, rgba(29, 78, 216, 0.72) 100%);
        color: #eff6ff;
        z-index: 1;
      }
      .mmr-dock-chip.is-disabled {
        opacity: 0.42;
      }
      .mmr-mode-ctrl.is-normal {
        color: #1d4ed8;
      }
      .mmr-mode-ctrl:hover,
      .mmr-mode-ctrl:focus,
      .mmr-mode-ctrl:focus-visible,
      .mmr-mode-ctrl:active {
        background: rgba(14, 66, 146, 0.66) !important;
        color: #e0f2fe !important;
        outline: none !important;
        box-shadow: none !important;
      }
    `;
    document.head.appendChild(style);
  }

  /**Apply style vars onto overlay host */
  applyStyleVars() {
    const host = this.getAppEl();
    if (!host || !this.config) return;
    const dock = this.getDockStyle();
    const normal = this.getNormalStyle();
    const vars = {
      "--mmr-marker-size": `${this.getActiveMarkerSize()}px`,
      "--mmr-dock-card-height": `${dock.cardHeight}px`,
      "--mmr-dock-label-bg": dock.labelBg,
      "--mmr-dock-label-hover-bg": dock.labelHoverBg,
      "--mmr-dock-label-border": dock.labelBorder,
      "--mmr-dock-label-shadow": dock.labelShadow,
      "--mmr-dock-label-radius": `${dock.labelRadius}px`,
      "--mmr-dock-label-padding-y": `${dock.labelPaddingY}px`,
      "--mmr-dock-label-padding-x": `${dock.labelPaddingX}px`,
      "--mmr-dock-label-text": dock.labelText,
      "--mmr-dock-label-sub-text": dock.labelSubText,
      "--mmr-dock-label-badge-bg": dock.labelBadgeBg,
      "--mmr-dock-label-badge-text": dock.labelBadgeText,
      "--mmr-dock-title-size": `${dock.titleSize}px`,
      "--mmr-dock-sub-size": `${dock.subSize}px`,
      "--mmr-dock-line-color": dock.lineColor,
      "--mmr-dock-guide-color": dock.guideColor,
      "--mmr-dock-line-width": `${dock.lineWidth}px`,
      "--mmr-normal-label-bg": normal.labelBg,
      "--mmr-normal-label-hover-bg": normal.labelHoverBg,
      "--mmr-normal-label-border": normal.labelBorder,
      "--mmr-normal-label-shadow": normal.labelShadow,
      "--mmr-normal-label-radius": `${normal.labelRadius}px`,
      "--mmr-normal-label-padding-y": `${normal.labelPaddingY}px`,
      "--mmr-normal-label-padding-x": `${normal.labelPaddingX}px`,
      "--mmr-normal-label-text": normal.labelText,
      "--mmr-normal-text-size": `${normal.textSize}px`,
      "--mmr-normal-max-width": `${normal.maxWidth}px`
    };
    Object.entries(vars).forEach(([key, value]) => {
      host.style.setProperty(key, value);
      if (this.overlayEl) this.overlayEl.style.setProperty(key, value);
    });
  }

  /**Ensure map events are bound once */
  ensureMapEvents() {
    if (!this.map || this.eventsBound) return;
    this.map.on("move", this.boundRender);
    this.map.on("zoom", this.boundRender);
    this.map.on("resize", this.boundRender);
    this.map.on("movestart", this.boundMoveStart);
    this.map.on("zoomstart", this.boundMoveStart);
    this.map.on("dragstart", this.boundMoveStart);
    this.map.on("pitchstart", this.boundMoveStart);
    this.map.on("rotatestart", this.boundMoveStart);
    this.map.on("moveend", this.boundMoveEnd);
    this.map.on("zoomend", this.boundMoveEnd);
    this.map.on("dragend", this.boundMoveEnd);
    this.map.on("pitchend", this.boundMoveEnd);
    this.map.on("rotateend", this.boundMoveEnd);
    this.map.on("idle", this.boundMoveEnd);
    window.addEventListener("resize", this.boundRender);
    this.eventsBound = true;
  }

  /**Get app host element */
  getAppEl() {
    return document.getElementById("map-host")
      || document.getElementById("app")
      || this.map?.getContainer?.()?.parentElement
      || this.map?.getContainer?.();
  }

  /**Handle moving start */
  handleMoveStart() {
    this.isInteracting = true;
    if (this.config?.performance?.hideLeaderOnMove) this.clearLeaders();
  }

  /**Handle moving end */
  handleMoveEnd() {
    this.isInteracting = false;
    this.requestRender();
  }

  /**Sync entries with point list */
  syncEntries() {
    const next = [];
    this.points.forEach((item, idx) => {
      const id = this.getPointId(item, idx);
      const old = this.entries.find(entry => entry.id === id);
      if (old) {
        old.item = item;
        this.updateLabelContent(old, idx);
        next.push(old);
        return;
      }
      next.push(this.createEntry(item, idx));
    });

    this.entries
      .filter(entry => !next.includes(entry))
      .forEach(entry => this.removeEntry(entry));

    this.entries = next;
    const validKeys = new Set(this.points.map((item, idx) => String(this.getPointId(item, idx))));
    Array.from(this.pointState.keys()).forEach(key => {
      if (!validKeys.has(key)) this.pointState.delete(key);
    });
  }

  /**Get stable point id */
  getPointId(item, idx) {
    return item?.id ?? item?.name ?? `pt_${idx}`;
  }

  /**Create render entry */
  createEntry(item, idx) {
    const entry = {
      id: this.getPointId(item, idx),
      item,
      marker: null,
      markerEl: null,
      labelEl: null,
      box: null,
      dock: null,
      hidden: false
    };
    const markerEl = this.createMarkerEl(entry);
    const marker = new mapboxgl.Marker({
      element: markerEl,
      anchor: "center"
    }).setLngLat(this.getPointCoords(item)).addTo(this.map);
    entry.marker = marker;
    entry.markerEl = markerEl;
    entry.labelEl = this.createLabelEl(entry);
    this.updateLabelContent(entry, idx);
    this.updateMarkerContent(entry, idx);
    return entry;
  }

  /**Create marker dom */
  createMarkerEl(entry) {
    const el = document.createElement("div");
    el.className = "mmr-map-marker";
    el.innerHTML = `
      <div class="mmr-marker-icon"><div class="mmr-marker-glyph"></div></div>
      <div class="mmr-marker-inline"></div>
    `;
    const iconEl = el.querySelector(".mmr-marker-icon");
    const inlineEl = el.querySelector(".mmr-marker-inline");
    if (this.config.pointClick && iconEl) {
      iconEl.classList.add("is-clickable");
      iconEl.addEventListener("click", (evt) => {
        evt.stopPropagation();
        if (this.isPointClickable(entry) === false) return;
        this.config.pointClick?.(entry.item, entry, evt);
      });
    }
    if (this.config.pointClick && inlineEl) {
      inlineEl.classList.add("is-clickable");
      inlineEl.addEventListener("click", (evt) => {
        evt.stopPropagation();
        if (this.isPointClickable(entry) === false) return;
        this.config.pointClick?.(entry.item, entry, evt);
      });
    }
    return el;
  }

  /**Create label dom */
  createLabelEl(entry) {
    const el = document.createElement("div");
    el.className = "mmr-label-card";
    el.innerHTML = `
      <span class="mmr-label-no"></span>
      <div class="mmr-label-main">
        <div class="mmr-label-title"></div>
        <div class="mmr-label-sub"></div>
      </div>
    `;
    if (this.config.pointClick) {
      el.classList.add("is-clickable");
      el.addEventListener("click", (evt) => {
        if (this.isPointClickable(entry) === false) return;
        this.config.pointClick?.(entry.item, entry, evt);
      });
    }
    this.labelLayer.appendChild(el);
    return el;
  }

  /**Refresh label text */
  updateLabelContent(entry, idx) {
    const item = entry.item;
    if (!entry.labelEl) return;
    const noEl = entry.labelEl.querySelector(".mmr-label-no");
    const titleEl = entry.labelEl.querySelector(".mmr-label-title");
    const subEl = entry.labelEl.querySelector(".mmr-label-sub");
    if (noEl) noEl.textContent = this.getLabelNo(item, idx);
    if (titleEl) titleEl.textContent = this.getLabelTitle(item, idx);
    if (subEl) subEl.textContent = this.getLabelSub(item);
  }

  /**Refresh normal mode marker label */
  updateMarkerContent(entry, idx) {
    const normalStyle = this.getNormalStyle();
    const iconEl = entry.markerEl?.querySelector(".mmr-marker-icon");
    const glyphEl = entry.markerEl?.querySelector(".mmr-marker-glyph");
    const inlineEl = entry.markerEl?.querySelector(".mmr-marker-inline");
    const scale = this.getPointScale(entry.item);
    if (iconEl) {
      iconEl.style.setProperty("--mmr-marker-scale", String(scale));
    }
    if (glyphEl) {
      this.renderMarkerGlyph(glyphEl, entry.item?.icon);
      const hasGlyph = glyphEl.childNodes.length > 0 || String(glyphEl.textContent || "").trim() !== "";
      glyphEl.classList.toggle("is-empty", hasGlyph === false);
      glyphEl.style.display = "inline-flex";
    }
    if (!inlineEl) return;
    inlineEl.style.top = `${(this.getActiveMarkerSize() * scale) / 2 + normalStyle.offsetY}px`;
    inlineEl.textContent = this.getNormalContent(entry.item, idx);
  }

  /**Get label number text */
  getLabelNo(item, idx) {
    const val = this.getFieldValue(item, this.config?.dockStyle?.fields?.no);
    return String(val ?? idx + 1);
  }

  /**Get label title text */
  getLabelTitle(item, idx) {
    const val = this.getFieldValue(item, this.config?.dockStyle?.fields?.title);
    return String(val ?? `点位${idx + 1}`);
  }

  /**Build secondary label text */
  getLabelSub(item) {
    const val = this.getFieldValue(item, this.config?.dockStyle?.fields?.sub);
    if (Array.isArray(val)) return val.filter(Boolean).join(" · ");
    return String(val ?? "");
  }

  /**Build normal mode text */
  getNormalContent(item, idx) {
    const content = this.config?.normalStyle?.content;
    if (typeof content === "function") {
      return String(content(item, idx) ?? "");
    }
    if (typeof content === "string" && content.trim()) {
      return this.formatContent(content, item, idx);
    }
    return this.getLabelTitle(item, idx);
  }

  /**Format content template */
  formatContent(template, item, idx) {
    return String(template || "").replace(/\{([^{}]+)\}/g, (_, token) => {
      const key = String(token || "").trim();
      if (!key) return "";
      if (key === "index") return String(idx + 1);
      const val = this.getFieldValue(item, key);
      if (Array.isArray(val)) return val.filter(Boolean).join(" · ");
      return val == null ? "" : String(val);
    });
  }

  /**Read nested field value */
  getFieldValue(item, field) {
    if (typeof field === "function") return field(item);
    if (Array.isArray(field)) {
      return field
        .map(key => this.getFieldValue(item, key))
        .filter(v => v !== null && v !== undefined && String(v).trim() !== "");
    }
    if (typeof field !== "string" || !field.trim()) return null;
    const parts = field.split(".").map(s => s.trim()).filter(Boolean);
    let cur = item;
    for (const key of parts) {
      if (cur == null) return null;
      cur = cur[key];
    }
    return cur;
  }

  /**Remove render entry */
  removeEntry(entry) {
    try {
      entry?.marker?.remove?.();
    } catch (_) {}
    if (entry?.labelEl?.parentNode) entry.labelEl.parentNode.removeChild(entry.labelEl);
  }

  /**Get point lnglat */
  getPointCoords(item) {
    if (Array.isArray(item?.coords)) return item.coords;
    if (Array.isArray(item?.lngLat)) return item.lngLat;
    if (Number.isFinite(item?.lng) && Number.isFinite(item?.lat)) return [item.lng, item.lat];
    return [0, 0];
  }

  /**Schedule render */
  requestRender() {
    cancelAnimationFrame(this.rafId || 0);
    this.rafId = requestAnimationFrame(() => this.updateView());
  }

  /**Return per-point scale */
  getPointScale(item) {
    const scale = Number(item?.scale);
    return Number.isFinite(scale) && scale > 0 ? scale : 1;
  }

  /**Render marker glyph */
  renderMarkerGlyph(glyphEl, icon) {
    glyphEl.innerHTML = "";
    glyphEl.textContent = "";
    if (!icon) return;
    const text = String(icon).trim();
    if (!text) return;

    if (text.startsWith("<svg")) {
      glyphEl.innerHTML = text;
      return;
    }

    if (/^(https?:)?\/\//i.test(text) || /^data:image\//i.test(text) || /(\.svg|\.png|\.jpg|\.jpeg|\.webp|\.gif)(\?.*)?$/i.test(text)) {
      const img = document.createElement("img");
      img.src = text;
      img.alt = "";
      glyphEl.appendChild(img);
      return;
    }

    glyphEl.textContent = text;
  }

  /**Toggle render mode */
  toggleRenderMode() {
    this.flags.normal = !this.flags.normal;
    this.markerSize = this.getActiveMarkerSize();
    this.applyStyleVars();
    this.updateControlStates();
    this.requestRender();
    return this.flags.normal;
  }

  /**Set render mode */
  setRenderMode(renderNormal = false) {
    this.flags.normal = renderNormal === true;
    this.markerSize = this.getActiveMarkerSize();
    this.applyStyleVars();
    this.updateControlStates();
    this.emitRenderModeChange();
    this.requestRender();
    return this;
  }

  /**Notify host when render mode changes */
  emitRenderModeChange() {
    const mode = this.flags?.normal === true ? "normal" : "dock";
    try {
      window.dispatchEvent(new CustomEvent("map-marker-render-mode-change", {
        detail: { mode, renderer: this }
      }));
    } catch {
    }
  }

  /**Create mapbox control for toggling render mode */
  RenderControl() {
    const renderer = this;
    return {
      onAdd(map) {
        this._map = map;
        renderer.ensureStyles();
        this._container = document.createElement("div");
        this._container.className = "mapboxgl-ctrl mmr-ctrl-wrap";

        this._btns = document.createElement("div");
        this._btns.className = "mapboxgl-ctrl-group mmr-ctrl-btns";

        this._button = document.createElement("button");
        this._button.type = "button";
        this._button.className = "mapboxgl-ctrl-icon gis-reset-btn mmr-mode-ctrl";
        this._button.innerHTML = `
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M12 2.5c-3.58 0-6.5 2.87-6.5 6.42 0 4.67 5.1 10.56 5.31 10.81a1.6 1.6 0 0 0 2.38 0c.21-.25 5.31-6.14 5.31-10.81C18.5 5.37 15.58 2.5 12 2.5zm0 9.1a2.68 2.68 0 1 1 0-5.36 2.68 2.68 0 0 1 0 5.36z"></path>
          </svg>
        `;
        this._panel = renderer.createControlPanel(this);
        this._onClick = (evt) => renderer.toggleControlPanel(this, evt);
        this._button.addEventListener("click", this._onClick);
        this._btns.appendChild(this._button);
        this._container.appendChild(this._btns);
        this._container.appendChild(this._panel);

        renderer.controls.push(this);
        renderer.updateControlStates();
        return this._container;
      },
      onRemove() {
        renderer.closeControlPanel(this);
        const idx = renderer.controls.indexOf(this);
        if (idx >= 0) renderer.controls.splice(idx, 1);
        if (this._button && this._onClick) this._button.removeEventListener("click", this._onClick);
        if (this._container?.parentNode) this._container.parentNode.removeChild(this._container);
        this._map = undefined;
      }
    };
  }

  /**Refresh control active state */
  updateControlStates() {
    const title = this.flags.normal ? "点位控制：当前为普通模式" : "点位控制：当前为停靠模式";
    this.controls.forEach(ctrl => {
      const btn = ctrl?._button;
      if (!btn) return;
      btn.title = title;
      btn.classList.toggle("is-normal", this.flags.normal === true);
      btn.setAttribute("aria-expanded", ctrl?._panelOpen === true ? "true" : "false");
      ctrl?._dockBtnTop?.classList.toggle("is-active", this.activeDockSet.has("top"));
      ctrl?._dockBtnBottom?.classList.toggle("is-active", this.activeDockSet.has("bottom"));
      ctrl?._dockBtnLeft?.classList.toggle("is-active", this.activeDockSet.has("left"));
      ctrl?._dockBtnRight?.classList.toggle("is-active", this.activeDockSet.has("right"));
      const single = this.activeDockSet.size <= 1;
      ctrl?._dockBtnTop?.classList.toggle("is-disabled", !this.hasDock("top") || (single && this.activeDockSet.has("top")));
      ctrl?._dockBtnBottom?.classList.toggle("is-disabled", !this.hasDock("bottom") || (single && this.activeDockSet.has("bottom")));
      ctrl?._dockBtnLeft?.classList.toggle("is-disabled", !this.hasDock("left") || (single && this.activeDockSet.has("left")));
      ctrl?._dockBtnRight?.classList.toggle("is-disabled", !this.hasDock("right") || (single && this.activeDockSet.has("right")));
      ctrl?._modeDock?.classList.toggle("is-active", this.flags.normal !== true);
      ctrl?._modeNormal?.classList.toggle("is-active", this.flags.normal === true);
      ctrl?._dockGroup?.classList.toggle("is-hidden", this.flags.normal === true);
      if (ctrl?._panel) ctrl._panel.hidden = ctrl?._panelOpen !== true;
      ctrl?._panel?.classList.toggle("is-open", ctrl?._panelOpen === true);
    });
  }

  /**Create control popup panel */
  createControlPanel(ctrl) {
    const panel = document.createElement("div");
    panel.className = "mmr-ctrl-pop";
    panel.hidden = true;
    panel.innerHTML = `
      <div class="mmr-ctrl-groupbox">
        <div class="mmr-ctrl-group-title">点位展示方式</div>
        <div class="mmr-ctrl-groupbuttons">
          <button type="button" class="mmr-ctrl-option" data-mode="normal">普通</button>
          <button type="button" class="mmr-ctrl-option" data-mode="dock">停靠</button>
        </div>
      </div>
      <div class="mmr-ctrl-groupbox" data-role="dock-group">
        <div class="mmr-ctrl-group-title">显示区域</div>
        <div class="mmr-ctrl-groupbuttons">
          <button type="button" class="mmr-dock-chip" data-dock="top">上</button>
          <button type="button" class="mmr-dock-chip" data-dock="bottom">下</button>
          <button type="button" class="mmr-dock-chip" data-dock="left">左</button>
          <button type="button" class="mmr-dock-chip" data-dock="right">右</button>
        </div>
      </div>
    `;
    panel.addEventListener("pointerdown", (evt) => evt.stopPropagation());
    ctrl._panelOpen = false;
    ctrl._modeNormal = panel.querySelector('[data-mode="normal"]');
    ctrl._modeDock = panel.querySelector('[data-mode="dock"]');
    ctrl._dockGroup = panel.querySelector('[data-role="dock-group"]');
    ctrl._dockBtnTop = panel.querySelector('[data-dock="top"]');
    ctrl._dockBtnBottom = panel.querySelector('[data-dock="bottom"]');
    ctrl._dockBtnLeft = panel.querySelector('[data-dock="left"]');
    ctrl._dockBtnRight = panel.querySelector('[data-dock="right"]');
    ctrl._modeNormal?.addEventListener("click", () => this.setRenderMode(true));
    ctrl._modeDock?.addEventListener("click", () => this.setRenderMode(false));
    ctrl._dockBtnTop?.addEventListener("click", () => this.handleDockControlClick("top"));
    ctrl._dockBtnBottom?.addEventListener("click", () => this.handleDockControlClick("bottom"));
    ctrl._dockBtnLeft?.addEventListener("click", () => this.handleDockControlClick("left"));
    ctrl._dockBtnRight?.addEventListener("click", () => this.handleDockControlClick("right"));
    return panel;
  }

  /**Toggle popup panel */
  toggleControlPanel(ctrl, evt) {
    evt?.stopPropagation?.();
    const next = ctrl?._panelOpen !== true;
    this.controls.forEach(item => {
      item._panelOpen = item === ctrl ? next : false;
    });
    this.syncControlPanelListener();
    this.updateControlStates();
  }

  /**Close popup panel */
  closeControlPanel(ctrl = null) {
    let changed = false;
    this.controls.forEach(item => {
      if (ctrl && item !== ctrl) return;
      if (item._panelOpen) changed = true;
      item._panelOpen = false;
    });
    this.syncControlPanelListener();
    if (changed) this.updateControlStates();
  }

  /**Sync outside click listener */
  syncControlPanelListener() {
    const hasOpen = this.controls.some(ctrl => ctrl?._panelOpen);
    document.removeEventListener("pointerdown", this.boundDocPointerDown);
    if (hasOpen) document.addEventListener("pointerdown", this.boundDocPointerDown);
  }

  /**Close popup on outside pointer */
  handleDocumentPointerDown(evt) {
    const target = evt?.target;
    const hit = this.controls.some(ctrl => ctrl?._container?.contains?.(target));
    if (hit) return;
    this.closeControlPanel();
  }

  /**Handle dock option click */
  handleDockControlClick(position) {
    const enabled = this.activeDockSet.has(position);
    this.setDockVisible(position, !enabled);
  }

  /**Update all layers */
  updateView() {
    if (!this.map || !this.overlayEl) return;

    this.updateMarkerVisibility();
    if (this.flags.normal) {
      this.clearOverlay();
      return;
    }

    this.layoutLabels();
    this.renderLabels();
    if (this.isInteracting && this.config?.performance?.hideLeaderOnMove) {
      this.clearLeaders();
      return;
    }
    this.renderLeaders();
  }

  /**Show or hide marker dom */
  updateMarkerVisibility() {
    this.entries.forEach((entry, idx) => {
      entry.marker.setLngLat(this.getPointCoords(entry.item));
      this.updateMarkerContent(entry, idx);
      const pointState = this.getEntryState(entry) || this.getPointState(entry.item, idx);
      const visible = pointState?.visible !== false;
      const showInline = this.flags.normal && this.flags.label;
      entry.markerEl.classList.toggle("is-normal", showInline);
      entry.markerEl.classList.toggle("hide-icon", !visible || !this.flags.marker);
      entry.markerEl.classList.toggle("hide-all", !visible || (!this.flags.marker && !showInline));
      entry.markerEl.classList.toggle("is-selected", pointState?.selected === true);
      const iconEl = entry.markerEl?.querySelector(".mmr-marker-icon");
      const inlineEl = entry.markerEl?.querySelector(".mmr-marker-inline");
      const clickable = this.isPointClickable(entry);
      iconEl?.classList.toggle("is-clickable", clickable);
      inlineEl?.classList.toggle("is-clickable", clickable);
      if (iconEl) iconEl.style.pointerEvents = clickable ? "auto" : "none";
      if (inlineEl) inlineEl.style.pointerEvents = clickable ? "auto" : "none";
    });
  }

  /**Clear overlay layers */
  clearOverlay() {
    if (this.labelLayer) {
      this.entries.forEach(entry => {
        if (entry.labelEl) entry.labelEl.style.display = "none";
      });
    }
    this.clearLeaders();
  }

  /**Clear leader svg */
  clearLeaders() {
    this.leaderMarkup = "";
    if (this.leaderLayer) this.leaderLayer.innerHTML = "";
  }

  /**Place labels on configured docks */
  layoutLabels() {
    const stage = this.getStageSize();
    const groups = this.groupEntriesByDock(stage);
    const docks = this.getActiveDocks();
    const topDock = docks.find(d => d.position === "top");
    const bottomDock = docks.find(d => d.position === "bottom");
    const leftDock = docks.find(d => d.position === "left");
    const rightDock = docks.find(d => d.position === "right");

    const topMeta = topDock
      ? this.layoutHorizontalDock(groups.top || [], topDock, stage)
      : { usedHeight: 0 };
    const bottomMeta = bottomDock
      ? this.layoutHorizontalDock(groups.bottom || [], bottomDock, stage)
      : { usedHeight: 0 };

    const verticalRange = {
      startY: topMeta.usedHeight > 0 ? topMeta.usedHeight + this.rowGap : 0,
      endY: bottomMeta.usedHeight > 0 ? stage.h - bottomMeta.usedHeight - this.rowGap : stage.h
    };

    if (leftDock) this.layoutVerticalDock(groups.left || [], leftDock, stage, verticalRange);
    if (rightDock) this.layoutVerticalDock(groups.right || [], rightDock, stage, verticalRange);
  }

  /**Get current stage size */
  getStageSize() {
    const app = this.getAppEl();
    return {
      w: app?.clientWidth || 0,
      h: app?.clientHeight || 0
    };
  }

  /**Get effective dock margin */
  getDockMargin(dock) {
    const margin = this.parseSize(dock?.margin, 20);
    if (this.flags?.normal === true) return margin;
    if (dock?.position === "top") return Math.max(18, margin - 70);
    return margin;
  }

  /**Get edge priority band size */
  getDockBandSize(dock, stage) {
    const width = this.getDockStyle().labelWidth;
    const margin = this.getDockMargin(dock);
    if (dock?.position === "top") {
      return Math.max(this.cardHeight + margin + this.rowGap * 3, Math.round(stage.h * 0.36));
    }
    if (dock?.position === "bottom") {
      return Math.max(this.cardHeight + margin + this.rowGap * 2, Math.round(stage.h * 0.22));
    }
    return Math.max(Math.round(width * 0.7), Math.round(stage.w * 0.18), margin + width * 0.5);
  }

  /**Normalize layout range within stage */
  normalizeLayoutRange(start, end, stageSize, margin, minSize) {
    const safeMargin = Math.max(0, this.parseSize(margin, 0));
    const safeMin = Math.max(1, this.parseSize(minSize, 1));
    const limit = Math.max(safeMin, stageSize - safeMargin);
    let startVal = Number.isFinite(start) ? start : safeMargin;
    let endVal = Number.isFinite(end) ? end : stageSize - safeMargin;

    startVal = Math.max(safeMargin, startVal);
    endVal = Math.min(stageSize - safeMargin, endVal);

    if (endVal - startVal < safeMin) {
      startVal = safeMargin;
      endVal = Math.min(stageSize - safeMargin, Math.max(safeMargin + safeMin, limit));
    }

    return {
      start: startVal,
      end: Math.max(startVal + safeMin, endVal)
    };
  }

  /**Group visible entries by dock */
  groupEntriesByDock(stage) {
    const groups = { top: [], bottom: [], left: [], right: [] };
    this.entries.forEach((entry, idx) => {
      const pointState = this.getEntryState(entry) || this.getPointState(entry.item, idx);
      if (pointState?.visible === false) {
        entry.hidden = true;
        entry.box = null;
        entry.point = null;
        return;
      }

      const point = this.getAnchorPoint(entry);
      if (!this.isPointVisible(point, stage)) {
        entry.hidden = true;
        entry.box = null;
        entry.point = point;
        return;
      }

      entry.hidden = false;
      entry.point = point;
      entry.dock = this.pickDock(point, stage);
      if (!entry.dock) return;
      groups[entry.dock.position].push(entry);
    });

    groups.top.sort((a, b) => a.point.x - b.point.x);
    groups.bottom.sort((a, b) => a.point.x - b.point.x);
    groups.left.sort((a, b) => a.point.y - b.point.y);
    groups.right.sort((a, b) => a.point.y - b.point.y);
    Object.values(groups).forEach(list => {
      list.forEach((entry, i) => {
        entry.dockIndex = i;
      });
    });
    return groups;
  }

  /**Check point visibility */
  isPointVisible(point, stage) {
    return point.x >= -24 && point.y >= -24 && point.x <= stage.w + 24 && point.y <= stage.h + 24;
  }

  /**Pick nearest configured dock */
  pickDock(point, stage) {
    const active = this.getActiveDocks();
    if (active.length === 0) return null;
    const dockMap = new Map(active.map(dock => [dock.position, dock]));
    const topDock = dockMap.get("top");
    const bottomDock = dockMap.get("bottom");
    const leftDock = dockMap.get("left");
    const rightDock = dockMap.get("right");

    if (topDock && point.y <= this.getDockBandSize(topDock, stage)) return topDock;
    if (bottomDock && point.y >= stage.h - this.getDockBandSize(bottomDock, stage)) return bottomDock;
    if (leftDock && point.x <= this.getDockBandSize(leftDock, stage)) return leftDock;
    if (rightDock && point.x >= stage.w - this.getDockBandSize(rightDock, stage)) return rightDock;

    const docks = active.map(dock => ({
      dock,
      dist: this.getDockDistance(dock.position, point, stage, dock) / Math.max(1, this.getDockBandSize(dock, stage))
    }));
    const dockOrder = ["top", "bottom", "left", "right"];
    docks.sort((a, b) => a.dist - b.dist || dockOrder.indexOf(a.dock.position) - dockOrder.indexOf(b.dock.position));
    return docks[0]?.dock || null;
  }

  /**Get point-to-edge distance */
  getDockDistance(position, point, stage, dock = null) {
    const margin = this.getDockMargin(dock || { position });
    if (position === "top") return Math.abs(point.y - margin);
    if (position === "bottom") return Math.abs(stage.h - margin - point.y);
    if (position === "left") return Math.abs(point.x - margin);
    return Math.abs(stage.w - margin - point.x);
  }

  /**Get leader start anchor */
  getAnchorPoint(entry) {
    if (this.flags.marker) {
      const appRect = this.getAppEl()?.getBoundingClientRect?.();
      const rect = entry.markerEl?.querySelector(".mmr-marker-icon")?.getBoundingClientRect?.()
        || entry.markerEl?.getBoundingClientRect?.();
      if (appRect && rect) {
        return {
          x: rect.left - appRect.left + rect.width / 2,
          y: rect.top - appRect.top + rect.height / 2
        };
      }
    }
    return this.map.project(this.getPointCoords(entry.item));
  }

  /**Layout top or bottom docks */
  layoutHorizontalDock(list, dock, stage, range = null) {
    const width = this.getDockStyle().labelWidth;
    const height = this.cardHeight;
    const dockMargin = this.getDockMargin(dock);
    const xRange = this.normalizeLayoutRange(
      range?.startX ?? dockMargin,
      range?.endX ?? (stage.w - dockMargin),
      stage.w,
      dockMargin,
      width
    );
    const startX = xRange.start;
    const maxX = xRange.end;
    const availableW = Math.max(width, maxX - startX);
    const availableH = Math.max(height, stage.h - dockMargin * 2);
    const maxCols = Math.max(1, Math.floor((availableW + this.colGap) / (width + this.colGap)));
    const maxRows = Math.max(1, Math.floor((availableH + this.rowGap) / (height + this.rowGap)));
    const usedRows = list.length === 0 ? 0 : Math.min(maxRows, Math.ceil(list.length / maxCols));
    const rowCounts = Array.from({ length: usedRows }, (_, row) => {
      const start = row * maxCols;
      return Math.min(maxCols, Math.max(0, list.length - start));
    });

    let idx = 0;
    rowCounts.forEach((count, row) => {
      const rowWidth = count * width + Math.max(0, count - 1) * this.colGap;
      const rowStartX = startX + Math.max(0, (availableW - rowWidth) / 2);
      const rawY = dock.position === "top"
        ? dockMargin + row * (height + this.rowGap)
        : stage.h - dockMargin - height - row * (height + this.rowGap);
      const y = Math.min(
        Math.max(rawY, dockMargin),
        Math.max(dockMargin, stage.h - dockMargin - height)
      );

      for (let col = 0; col < count && idx < list.length; col += 1, idx += 1) {
        const entry = list[idx];
        const x = rowStartX + col * (width + this.colGap);
        entry.box = { x, y, w: width, h: height };
        entry.dockRow = row;
        entry.dockCol = col;
      }
    });

    const usedHeight = usedRows > 0
      ? dockMargin + usedRows * height + Math.max(0, usedRows - 1) * this.rowGap
      : 0;
    return {
      usedRows,
      usedHeight,
      startX,
      endX: maxX
    };
  }

  /**Layout left or right docks */
  layoutVerticalDock(list, dock, stage, range = null) {
    const width = this.getDockStyle().labelWidth;
    const height = this.cardHeight;
    const dockMargin = this.getDockMargin(dock);
    const yRange = this.normalizeLayoutRange(
      range?.startY ?? dockMargin,
      range?.endY ?? (stage.h - dockMargin),
      stage.h,
      dockMargin,
      height
    );
    const startY = yRange.start;
    const maxY = yRange.end;
    const availableH = Math.max(height, maxY - startY);
    const availableW = Math.max(width, stage.w - dockMargin * 2);
    const maxRows = Math.max(1, Math.floor((availableH + this.rowGap) / (height + this.rowGap)));
    const maxCols = Math.max(1, Math.floor((availableW + this.colGap) / (width + this.colGap)));
    const usedCols = list.length === 0 ? 0 : Math.min(maxCols, Math.ceil(list.length / maxRows));
    const colCounts = Array.from({ length: usedCols }, (_, col) => {
      const start = col * maxRows;
      return Math.min(maxRows, Math.max(0, list.length - start));
    });

    let idx = 0;
    colCounts.forEach((count, col) => {
      const colHeight = count * height + Math.max(0, count - 1) * this.rowGap;
      const colStartY = startY + Math.max(0, (availableH - colHeight) / 2);
      const rawX = dock.position === "left"
        ? dockMargin + col * (width + this.colGap)
        : stage.w - dockMargin - width - col * (width + this.colGap);
      const x = dock.position === "left"
        ? Math.min(rawX, Math.max(dockMargin, stage.w - dockMargin - width))
        : Math.max(rawX, dockMargin);

      for (let row = 0; row < count && idx < list.length; row += 1, idx += 1) {
        const entry = list[idx];
        const y = colStartY + row * (height + this.rowGap);
        entry.box = { x, y, w: width, h: height };
        entry.dockCol = col;
        entry.dockRow = row;
      }
    });

    const usedWidth = usedCols > 0
      ? dockMargin + usedCols * width + Math.max(0, usedCols - 1) * this.colGap
      : 0;
    return {
      usedCols,
      usedWidth
    };
  }

  /**Render label dom */
  renderLabels() {
    const stage = this.getStageSize();
    this.entries.forEach((entry, idx) => {
      const el = entry.labelEl;
      const pointState = this.getEntryState(entry) || this.getPointState(entry.item, idx);
      const visible = pointState?.visible !== false;
      const clickable = this.isPointClickable(entry);
      if (!visible || !this.flags.label || entry.hidden || !entry.box) {
        el.style.display = "none";
        return;
      }

      const box = { ...entry.box };
      const dockPos = entry.dock?.position || "top";
      const dockMargin = this.getDockMargin(entry.dock);
      const row = Number.isFinite(entry.dockRow) ? entry.dockRow : 0;
      if (dockPos === "top") {
        box.y = dockMargin + row * (box.h + this.rowGap);
      } else if (dockPos === "bottom") {
        box.y = stage.h - dockMargin - box.h - row * (box.h + this.rowGap);
      }

      el.style.display = "flex";
      el.style.width = `${box.w}px`;
      el.style.left = `${box.x}px`;
      el.style.top = `${box.y}px`;
      el.style.pointerEvents = clickable ? "auto" : "none";
      el.className = `mmr-label-card mmr-side-${dockPos}${clickable ? " is-clickable" : ""}${pointState?.selected === true ? " is-selected" : ""}`;
    });
  }

  /**Render leader svg */
  renderLeaders() {
    const stage = this.getStageSize();
    this.leaderLayer.setAttribute("width", String(stage.w));
    this.leaderLayer.setAttribute("height", String(stage.h));
    this.leaderLayer.setAttribute("viewBox", `0 0 ${stage.w} ${stage.h}`);

    if (!this.flags.leader || !this.flags.label) {
      this.leaderMarkup = "";
      this.leaderLayer.innerHTML = "";
      return;
    }

    const lines = [];
    this.entries.forEach((entry, idx) => {
      const pointState = this.getEntryState(entry) || this.getPointState(entry.item, idx);
      if (pointState?.visible === false || entry.hidden || !entry.box || !entry.point) return;
      const anchor = this.getLabelAnchor(entry);
      const start = this.getLeaderStartPoint(entry, entry.point);
      const points = this.getLeaderPoints(entry, start, anchor);
      const mainPath = this.buildPolylinePath(points);
      lines.push(`
        <path class="mmr-leader-guide" d="${mainPath}" />
        <path class="mmr-leader-line" d="${mainPath}" />
        ${this.flags.marker ? "" : `<circle class="mmr-leader-dot" cx="${start.x}" cy="${start.y}" r="${this.getDockStyle().dotRadius}"></circle>`}
        <circle class="mmr-leader-anchor-dot" cx="${anchor.x}" cy="${anchor.y}" r="${this.getDockStyle().anchorDotRadius}"></circle>
      `);
    });
    const markup = lines.join("");
    if (markup === this.leaderMarkup) return;
    this.leaderMarkup = markup;
    this.leaderLayer.innerHTML = markup;
    this.animateLeaders();
  }

  /**Animate leader lines with simple draw effect */
  animateLeaders() {
    const lines = Array.from(this.leaderLayer?.querySelectorAll?.(".mmr-leader-line") || []);
    lines.forEach(line => this.animateLeaderLine(line));
  }

  /**Animate single leader line */
  animateLeaderLine(line) {
    const len = Number(line?.getTotalLength?.());
    if (!Number.isFinite(len) || len <= 0) return;
    line.style.transition = "none";
    line.style.strokeDasharray = `${len}`;
    line.style.strokeDashoffset = `${len}`;
    line.getBoundingClientRect();
    requestAnimationFrame(() => {
      line.style.transition = "stroke-dashoffset 260ms ease-out";
      line.style.strokeDashoffset = "0";
    });
  }

  /**Check whether point can be clicked */
  isPointClickable(entry) {
    const pointState = this.getEntryState(entry);
    if (this.config.pointClick == null) return false;
    return pointState?.clickable !== false;
  }

  /**Get label anchor on card edge */
  getLabelAnchor(entry) {
    const { box } = entry;
    const pos = entry.dock?.position;
    const offset = this.getDockStyle().anchorDotRadius + 2;
    if (pos === "left") return { x: box.x + box.w, y: box.y + box.h / 2 };
    if (pos === "right") return { x: box.x, y: box.y + box.h / 2 };
    if (pos === "bottom") return { x: box.x + box.w / 2, y: box.y };
    return { x: box.x + box.w / 2, y: box.y + box.h + offset };
  }

  /**Get leader start point outside marker icon */
  getLeaderStartPoint(entry, point) {
    const pos = entry.dock?.position;
    const offset = (this.getDockStyle().markerSize * this.getPointScale(entry.item)) / 2 + 2;
    if (pos === "left") return { x: point.x - offset, y: point.y };
    if (pos === "right") return { x: point.x + offset, y: point.y };
    if (pos === "bottom") return { x: point.x, y: point.y + offset };
    return { x: point.x, y: point.y - offset };
  }

  /**Get leader route points using straight, slanted, straight segments */
  getLeaderPoints(entry, start, anchor) {
    const pos = entry.dock?.position;
    const idx = Number(entry?.dockIndex ?? 0);
    const spread = (idx % 4) * 8;
    const primary = 20 + spread;
    const tail = 14 + spread * 0.5;

    if (pos === "left") {
      const p1 = { x: start.x - primary, y: start.y };
      const p2 = { x: anchor.x + tail, y: anchor.y };
      return [start, p1, p2, anchor];
    }
    if (pos === "right") {
      const p1 = { x: start.x + primary, y: start.y };
      const p2 = { x: anchor.x - tail, y: anchor.y };
      return [start, p1, p2, anchor];
    }
    if (pos === "bottom") {
      const p1 = { x: start.x, y: start.y + primary };
      const p2 = { x: anchor.x, y: anchor.y - tail };
      return [start, p1, p2, anchor];
    }

    const p1 = { x: start.x, y: start.y - primary };
    const p2 = { x: anchor.x, y: anchor.y + tail };
    return [start, p1, p2, anchor];
  }

  /**Build polyline path string */
  buildPolylinePath(points) {
    const list = Array.isArray(points) ? points.filter(Boolean) : [];
    if (list.length === 0) return "";
    const [first, ...rest] = list;
    return `M ${first.x} ${first.y}` + rest.map(p => ` L ${p.x} ${p.y}`).join("");
  }
}

window.MapMarkerRender = MapMarkerRender;
