import { setInfo } from "./utils.js";

 /****************************************************************
 * 地图图层管理器
 ****************************************************************/
export class LayerManager {
  constructor(map, layers) {
    this.map = map;
    this.layers = layers;
    this.layerMap = new Map();
    this.refreshVisiblePromise = null;
    this.autoRefreshTimer = 0;      // 自动刷新定时器ID
    this.autoRefreshMs = 15000;     // 自动刷新时间间隔（毫秒）
    this.autoRefreshBound = false;  // 是否已绑定自动刷新事件
    
    for (const layer of layers) 
      this.layerMap.set(layer.name, layer);

    this.runtime = {
      map,
      getOpacity: _name => 1,
      isEnabled: name => {
        const el = document.getElementById(name);
        return !!(el && el.checked);
      },
      getInfoId: name => this.getInfoId(name),
      setLayerInfo: (name, text, tooltip = "") => {
        const infoId = this.getInfoId(name);
        if (infoId) setInfo(infoId, text, tooltip);
      }
    };

    for (const layer of layers) layer.bind(this.runtime);
  }

  updateLayerInfo(layer) {
    if (!layer) return;
    const text = typeof layer.buildInfoText === "function" ? layer.buildInfoText() : "已开启";
    const tooltip = typeof layer.buildInfoTooltip === "function" ? layer.buildInfoTooltip() : "";
    this.runtime.setLayerInfo(layer.name, text, tooltip);
    if (typeof layer.buildDebugInfo === "function") {
      console.debug("[MapLayerInfo]", layer.buildDebugInfo());
    }
  }

  resolveRefreshState(layer, now = Date.now()) {
    const schedule = layer?.refreshSchedule || null;
    if (!layer?.visible) {
      return { due: false, reason: "hidden" };
    }
    if (!schedule) {
      return { due: false, reason: "no-cron" };
    }
    if (!layer.lastTime) {
      return { due: true, reason: "first-load", nextAt: layer.nextRefreshAt || 0 };
    }

    const nextAt = layer.nextRefreshAt || layer.updateNextRefreshAt(layer.lastTime);
    return {
      due: now >= nextAt,
      reason: now >= nextAt ? "due" : "not-due",
      nextAt
    };
  }

  formatLogTime(ts = Date.now()) {
    const date = new Date(ts);
    const yyyy = date.getFullYear();
    const mm = String(date.getMonth() + 1).padStart(2, "0");
    const dd = String(date.getDate()).padStart(2, "0");
    const hh = String(date.getHours()).padStart(2, "0");
    const mi = String(date.getMinutes()).padStart(2, "0");
    const ss = String(date.getSeconds()).padStart(2, "0");
    return `${yyyy}-${mm}-${dd} ${hh}:${mi}:${ss}`;
  }

  logStyledLine(label, text, style) {
    console.log(`%c${label}%c ${text}`, style, "color:inherit");
  }

  logRefreshRun({ trigger, force, startedAt, durationMs, records }) {
    const handled = records.filter(item => ["success", "failed", "skip-disabled", "skip-not-due", "skip-no-cron"].includes(item.status));
    const successCount = handled.filter(item => item.status === "success").length;
    const failedCount = handled.filter(item => item.status === "failed").length;
    const skippedCount = handled.filter(item => item.status.startsWith("skip-")).length;

    console.groupCollapsed(
      `%c[LayerScheduler]%c ${this.formatLogTime(startedAt)} trigger=${trigger} force=${force} refresh=${handled.length} ok=${successCount} skip=${skippedCount} fail=${failedCount}`,
      "color:#2563eb;font-weight:700",
      "color:inherit"
    );
    records.forEach(item => {
      if (item.status === "success") {
        this.logStyledLine("[LayerScheduler]", `${item.layer} success (${item.durationMs}ms)`, "color:#16a34a;font-weight:600");
        return;
      }
      if (item.status === "failed") {
        this.logStyledLine("[LayerScheduler]", `${item.layer} failed (${item.durationMs}ms): ${item.errorMessage || "未知错误"}`, "color:#dc2626;font-weight:700");
        return;
      }
      if (item.status === "skip-disabled") {
        this.logStyledLine("[LayerScheduler]", `${item.layer} skipped (disabled)`, "color:#9ca3af;font-weight:600");
        return;
      }
      if (item.status === "skip-not-due") {
        const nextText = item.nextAt ? `, next ${this.formatLogTime(item.nextAt)}` : "";
        this.logStyledLine("[LayerScheduler]", `${item.layer} skipped (not due${nextText})`, "color:#f59e0b;font-weight:600");
        return;
      }
      if (item.status === "skip-no-cron") {
        this.logStyledLine("[LayerScheduler]", `${item.layer} skipped (no cron)`, "color:#6b7280;font-weight:600");
        return;
      }
    });
    console.info("[LayerScheduler] summary", {
      activatedAt: this.formatLogTime(startedAt),
      trigger,
      force,
      durationMs,
      handledCount: handled.length,
      successCount,
      skippedCount,
      failedCount
    });
    console.groupEnd();
  }

  /**
   * 刷新所有可见图层
   * @param {boolean} force 是否强制刷新
   */
  async refreshVisible(force = false, trigger = "manual") {
    if (this.refreshVisiblePromise) {
      if (!force) {
        console.info(`[LayerScheduler] ${this.formatLogTime()} trigger=${trigger} skip: refresh in progress`);
        return this.refreshVisiblePromise;
      }
      await this.refreshVisiblePromise;
    }

    this.refreshVisiblePromise = this.runRefreshVisible(force, trigger)
      .finally(() => {
        this.refreshVisiblePromise = null;
      });

    return this.refreshVisiblePromise;
  }

  async runRefreshVisible(force = false, trigger = "manual") {
    const startedAt = Date.now();
    const records = [];
    const now = Date.now();

    for (const layer of this.layers) {
      if (!this.runtime.isEnabled(layer.name)) {
        records.push({ layer: layer.name, status: "skip-disabled" });
        continue;
      }
      const refreshState = this.resolveRefreshState(layer, now);
      if (!force && !refreshState.due) {
        if (refreshState.reason === "no-cron") {
          records.push({ layer: layer.name, status: "skip-no-cron" });
        } else {
          records.push({ layer: layer.name, status: "skip-not-due", nextAt: refreshState.nextAt || 0 });
        }
        continue;
      }

      const layerStartAt = Date.now();
      try {
        await layer.refresh(force);
        if (typeof layer.clearLastError === "function") layer.clearLastError();
        if (typeof layer.updateNextRefreshAt === "function") layer.updateNextRefreshAt(layer.lastTime || Date.now());
        this.updateLayerInfo(layer);
        records.push({
          layer: layer.name,
          status: "success",
          durationMs: Date.now() - layerStartAt
        });
      } catch (error) {
        console.error(`刷新图层 ${layer.name} 失败`, error);
        try {
          if (typeof layer.setLastError === "function") layer.setLastError(error);
          else layer.lastStatus = false;
          this.updateLayerInfo(layer);
        } catch (_e) { }
        records.push({
          layer: layer.name,
          status: "failed",
          durationMs: Date.now() - layerStartAt,
          errorMessage: error?.message || String(error || "")
        });
      }
    }

    this.logRefreshRun({
      trigger,
      force,
      startedAt,
      durationMs: Date.now() - startedAt,
      records
    });
  }

  startAutoRefresh() {
    if (this.autoRefreshTimer) return;
    this.autoRefreshTimer = window.setInterval(() => {
      this.refreshVisible(false, "timer").catch(error => {
        console.error("自动刷新图层失败", error);
      });
    }, this.autoRefreshMs);
    console.info(`[LayerScheduler] ${this.formatLogTime()} auto-refresh started, interval=${this.autoRefreshMs}ms`);

    if (!this.autoRefreshBound) {
      const triggerRefresh = (trigger) => {
        if (document.visibilityState === "visible") {
          this.refreshVisible(false, trigger).catch(error => {
            console.error("页面恢复后刷新图层失败", error);
          });
        }
      };
      document.addEventListener("visibilitychange", () => triggerRefresh("visibilitychange"));
      window.addEventListener("focus", () => triggerRefresh("focus"));
      this.autoRefreshBound = true;
    }
  }

  /**
   * 批量设置激活图层
   * @param {string[]} names 图层名称列表
   */
  async setActiveLayers(names = []) {
    const activeSet = new Set(
      (Array.isArray(names) ? names : [])
        .map(name => String(name || "").trim())
        .filter(Boolean)
    );

    for (const layer of this.layers) {
      const toggleEl = document.getElementById(layer.name);
      const shouldEnable = activeSet.has(layer.name);
      const wasEnabled = this.runtime.isEnabled(layer.name) || !!layer.visible;

      if (toggleEl) toggleEl.checked = shouldEnable;

      try {
        if (shouldEnable) {
          const infoId = this.getInfoId(layer.name);
          if (infoId) setInfo(infoId, "加载中...");
          if (!wasEnabled) await layer.show(1);
          else await layer.refresh(true);
          if (typeof layer.clearLastError === "function") layer.clearLastError();
          if (typeof layer.updateNextRefreshAt === "function") layer.updateNextRefreshAt(layer.lastTime || Date.now());
          this.updateLayerInfo(layer);
        } else {
          if (wasEnabled) layer.hide();
          this.runtime.setLayerInfo(layer.name, "未开启");
        }
      } catch (error) {
        console.error(`设置图层 ${layer.name} 失败`, error);
        try {
          if (typeof layer.setLastError === "function") layer.setLastError(error);
          else layer.lastStatus = false;
          this.updateLayerInfo(layer);
        } catch (_e) { }
      }
    }
  }

  /**
   * 切换图层可见性
   * @param {string} name 图层名称
   */
  async toggle(name) {
    const layer = this.layerMap.get(name);
    if (!layer) return;
    const enabled = this.runtime.isEnabled(name);
    const infoId = this.getInfoId(name);

    try {
      if (enabled) {
        if (infoId) setInfo(infoId, "加载中...");
        await layer.show(1);
        if (typeof layer.clearLastError === "function") layer.clearLastError();
          if (typeof layer.updateNextRefreshAt === "function") layer.updateNextRefreshAt(layer.lastTime || Date.now());
        this.updateLayerInfo(layer);
      } else {
        layer.hide();
        this.runtime.setLayerInfo(name, "未开启");
      }
    } catch (error) {
      console.error(`切换图层 ${name} 失败`, error);
      try {
        if (typeof layer.setLastError === "function") layer.setLastError(error);
        else layer.lastStatus = false;
        this.updateLayerInfo(layer);
      } catch (_e) { }
    }
  }

  /**
   * 设置图层透明度
   * @param {string} name 图层名称
   */
  setOpacity(name) {
    const layer = this.layerMap.get(name);
    if (!layer || !this.runtime.isEnabled(name)) return;
    layer.setOpacity(1);
  }

  /**
   * 绑定UI元素
   */
  bindUi() {
    for (const layer of this.layers) {
      const toggleEl = document.getElementById(layer.name);
      if (toggleEl) toggleEl.addEventListener("change", () => this.toggle(layer.name));
      if (toggleEl) toggleEl.checked = false;
      const infoId = this.getInfoId(layer.name);
      if (document.getElementById(infoId)) setInfo(infoId, "未开启");
    }
    this.startAutoRefresh();
  }

  getInfoId(name) {
    const toggleEl = document.getElementById(name);
    const infoId = toggleEl?.dataset?.infoId;
    return infoId || `${name}Info`;
  }
}
