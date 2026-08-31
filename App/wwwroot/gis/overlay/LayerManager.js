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
      getOpacity: name => {
        const layer = this.layerMap.get(name);
        if (layer && Number.isFinite(Number(layer.opacity))) return Number(layer.opacity);
        return 0.8;
      },
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

  /**解析 EleManager（考虑 iframe） */
  resolveEleManager() {
    if (window.top && window.top.EleManager) return window.top.EleManager;
    return window.EleManager;
  }

  /**解析可用的 Vue/ElementPlus 宿主窗口 */
  resolveHostWindow() {
    if (window.top && window.top.Vue && window.top.ElementPlus) return window.top;
    return window;
  }

  /**
   * 打开图层配置抽屉
   * @param {string} layerName 图层名称
   */
  openLayerConfig(layerName) {
    const layer = this.layerMap.get(layerName);
    if (!layer) return;
    const manager = this.resolveEleManager();
    const hostWindow = this.resolveHostWindow();
    if (!manager || typeof manager.openDrawer !== "function") {
      console.warn("EleManager.openDrawer 不可用");
      return;
    }
    if (!hostWindow || !hostWindow.Vue || !hostWindow.ElementPlus) {
      console.warn("Vue/ElementPlus 环境不可用");
      return;
    }

    const self = this;
    const initialState = typeof layer.getRuntimeState === "function"
      ? layer.getRuntimeState()
      : this.buildFallbackRuntimeState(layer);
    const title = `【${layer.title || layer.name}】配置`;

    manager.openDrawer({
      title,
      direction: "rtl",
      size: window.innerWidth < 768 ? "100%" : "440px",
      resizable: true,
      closeOnClickModal: true,
      destroyOnClose: true,
      custom: true,
      bodyClass: "layer-config-drawer",
      mountHandler(ctx) {
        const { bodyEl } = ctx;
        const { createApp, reactive, nextTick } = hostWindow.Vue;
        const managerApi = manager;

        const state = reactive({
          layerName,
          opacity: Number.isFinite(Number(initialState.opacity)) ? Number(initialState.opacity) : 0.8,
          info: { ...initialState },
          loading: false
        });

        const fmtTime = (ts) => {
          if (!ts) return "";
          const date = new Date(ts);
          if (Number.isNaN(date.getTime())) return "";
          const y = date.getFullYear();
          const m = String(date.getMonth() + 1).padStart(2, "0");
          const d = String(date.getDate()).padStart(2, "0");
          const hh = String(date.getHours()).padStart(2, "0");
          const mm = String(date.getMinutes()).padStart(2, "0");
          const ss = String(date.getSeconds()).padStart(2, "0");
          return `${y}-${m}-${d} ${hh}:${mm}:${ss}`;
        };

        const displayOrEmpty = (text) => {
          const t = String(text || "").trim();
          return t || "—";
        };

        const loadState = () => {
          const next = typeof layer.getRuntimeState === "function"
            ? layer.getRuntimeState()
            : self.buildFallbackRuntimeState(layer);
          state.info = { ...next };
          if (Number.isFinite(Number(next.opacity))) {
            state.opacity = Number(next.opacity);
          }
        };

        const onOpacityChange = (val) => {
          const next = Number.isFinite(Number(val)) ? Number(val) : 0.8;
          try {
            if (typeof layer.applyOpacity === "function") {
              layer.applyOpacity(next);
            } else if (typeof layer.setOpacity === "function") {
              layer.setOpacity(next);
              layer.opacity = next;
            }
          } catch (e) {
            console.error("应用透明度失败", e);
          }
          state.opacity = next;
        };

        const doRefresh = async () => {
          if (state.loading) return;
          state.loading = true;
          try {
            const result = typeof layer.refreshForConfig === "function"
              ? await layer.refreshForConfig(state.opacity)
              : await self.fallbackRefreshForConfig(layer, state.opacity);
            if (typeof layer.updateNextRefreshAt === "function") {
              try { layer.updateNextRefreshAt(layer.lastTime || Date.now()); } catch (_e) {}
            }
            self.updateLayerInfo(layer);
            nextTick(() => loadState());
            if (result?.ok) {
              if (managerApi && typeof managerApi.showSuccess === "function") {
                managerApi.showSuccess(`刷新成功（用时 ${result.durationMs || 0}ms）`);
              }
            } else {
              const msg = result?.error || "刷新失败";
              if (managerApi && typeof managerApi.showError === "function") {
                managerApi.showError(msg);
              }
            }
          } catch (e) {
            console.error("手动刷新失败", e);
            const msg = e instanceof Error ? e.message : String(e || "刷新失败");
            if (managerApi && typeof managerApi.showError === "function") {
              managerApi.showError(msg);
            }
          } finally {
            state.loading = false;
          }
        };

        const app = createApp({
          data() {
            return { state };
          },
          computed: {
            lastErrorAtText() {
              const ts = Number(state.info.lastErrorAt) || 0;
              return ts ? fmtTime(ts) : "";
            }
          },
          methods: {
            onOpacityChange,
            doRefresh,
            displayOrEmpty
          },
          template: `
<div class="layer-config-drawer p-5 h-full overflow-auto bg-white">
  <div class="section-block">
    <h4 class="section-title">基础信息</h4>
    <el-descriptions :column="1" border size="default">
      <el-descriptions-item label="图层名称">{{ state.info.name || state.layerName }}</el-descriptions-item>
      <el-descriptions-item label="数据时间">{{ displayOrEmpty(state.info.dataTime) }}</el-descriptions-item>
      <el-descriptions-item label="数据更新周期">{{ displayOrEmpty(state.info.updatePeriod) }}</el-descriptions-item>
      <el-descriptions-item label="上次刷新时间">{{ displayOrEmpty(state.info.lastRefreshAt) }}</el-descriptions-item>
      <el-descriptions-item label="下次刷新时间">{{ displayOrEmpty(state.info.nextRefreshAt) }}</el-descriptions-item>
    </el-descriptions>
  </div>

  <div class="section-block">
    <h4 class="section-title">显示配置</h4>
    <el-descriptions :column="1" border size="default">
      <el-descriptions-item label="不透明度" align="right" label-align="left">
        <el-input-number
          v-model="state.opacity"
          :min="0"
          :max="1"
          :step="0.05"
          :precision="2"
          size="default"
          style="width: 160px;"
          controls-position="right"
          @change="onOpacityChange"
        />
      </el-descriptions-item>
    </el-descriptions>
  </div>

  <div class="section-block">
    <h4 class="section-title">错误信息</h4>
    <el-descriptions :column="1" border size="default">
      <el-descriptions-item label="最后错误时间">
        <span v-if="lastErrorAtText" class="text-red-600">{{ lastErrorAtText }}</span>
        <span v-else class="error-empty">无</span>
      </el-descriptions-item>
      <el-descriptions-item label="最后错误内容">
        <span v-if="state.info.lastErrorMessage" class="error-text">{{ state.info.lastErrorMessage }}</span>
        <span v-else class="error-empty">无</span>
      </el-descriptions-item>
    </el-descriptions>
  </div>

  <div class="section-block refresh-actions">
    <el-button type="primary" :loading="state.loading" @click="doRefresh">
      <template #icon><i class="fa-solid fa-rotate-right"></i></template>
      立即刷新
    </el-button>
  </div>
</div>`
        });

        app.use(hostWindow.ElementPlus, hostWindow.ElementPlusLocaleZhCn ? { locale: hostWindow.ElementPlusLocaleZhCn } : undefined);
        if (hostWindow.ElementPlusIconsVue) {
          try {
            for (const [key, component] of Object.entries(hostWindow.ElementPlusIconsVue)) {
              app.component(key, component);
            }
          } catch (_e) {}
        }
        try {
          app.mount(bodyEl);
        } catch (e) {
          console.error("挂载图层配置抽屉失败", e);
        }
      }
    });
  }

  /**兜底：当图层未实现 getRuntimeState 时，手动构造状态对象 */
  buildFallbackRuntimeState(layer) {
    const formatLocal = (value) => {
      const date = value ? new Date(value) : null;
      if (!date || Number.isNaN(date.getTime())) return "";
      const y = date.getFullYear();
      const m = String(date.getMonth() + 1).padStart(2, "0");
      const d = String(date.getDate()).padStart(2, "0");
      const hh = String(date.getHours()).padStart(2, "0");
      const mm = String(date.getMinutes()).padStart(2, "0");
      return `${y}-${m}-${d} ${hh}:${mm}`;
    };
    const getDataTime = () => {
      if (typeof layer.getDataTimeDisplay === "function") return layer.getDataTimeDisplay() || "";
      return "";
    };
    const getRefreshText = () => {
      if (typeof layer.getRefreshTextDisplay === "function" && layer.getRefreshTextDisplay()) return layer.getRefreshTextDisplay();
      if (typeof layer.getRefreshCronDisplay === "function" && layer.getRefreshCronDisplay()) return layer.getRefreshCronDisplay();
      return layer.refreshText || layer.refreshCron || "";
    };
    return {
      name: layer.name,
      visible: !!layer.visible,
      status: !layer.visible ? "off" : (layer.lastStatus === false ? "error" : "on"),
      opacity: Number.isFinite(Number(layer.opacity)) ? Number(layer.opacity) : 0.8,
      dataTime: getDataTime(),
      updatePeriod: getRefreshText(),
      updatePeriodCron: typeof layer.getRefreshCronDisplay === "function" ? (layer.getRefreshCronDisplay() || "") : "",
      lastRefreshAt: formatLocal(layer.lastTime),
      lastRefreshAtMs: Number(layer.lastTime) || 0,
      nextRefreshAt: formatLocal(layer.nextRefreshAt || 0),
      nextRefreshAtMs: Number(layer.nextRefreshAt) || 0,
      lastErrorAt: Number(layer.lastErrorAt) || 0,
      lastErrorMessage: String(layer.lastErrorMessage || "").trim()
    };
  }

  /**兜底：手动刷新（未实现 refreshForConfig 时） */
  async fallbackRefreshForConfig(layer, opacity) {
    const start = Date.now();
    try {
      const safeOpacity = Number.isFinite(Number(opacity)) ? Number(opacity) : 0.8;
      let ok = true;
      if (layer.visible) {
        if (typeof layer.refresh === "function") ok = !!(await layer.refresh(true));
      } else if (typeof layer.show === "function") {
        ok = !!(await layer.show(safeOpacity));
      }
      if (ok) {
        if (typeof layer.clearLastError === "function") layer.clearLastError();
      } else {
        this.ensureLayerErrorSynced(layer, "图层刷新失败");
      }
      return {
        ok: !!ok,
        durationMs: Date.now() - start,
        error: !!ok ? "" : (String(layer.lastErrorMessage || "").trim() || "图层刷新失败")
      };
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e || "图层刷新失败");
      if (typeof layer.setLastError === "function") layer.setLastError(msg);
      return { ok: false, durationMs: Date.now() - start, error: msg };
    }
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

  /**兜底：确保 lastStatus===false 时 lastError 有内容（有些子类 refresh 只 return false） */
  ensureLayerErrorSynced(layer, defaultMsg = "图层加载失败") {
    if (!layer) return;
    try {
      if (typeof layer.ensureLastErrorSynced === "function") {
        layer.ensureLastErrorSynced(defaultMsg);
        return;
      }
      if (layer.lastStatus === false && !String(layer.lastErrorMessage || "").trim()) {
        layer.lastErrorMessage = String(defaultMsg || "图层加载失败");
        if (!layer.lastErrorAt) layer.lastErrorAt = layer.lastTime || Date.now();
      }
    } catch (_e) { /* ignore */ }
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
        const ok = await layer.refresh(force);
        if (!!ok) {
          if (typeof layer.clearLastError === "function") layer.clearLastError();
          if (typeof layer.updateNextRefreshAt === "function") layer.updateNextRefreshAt(layer.lastTime || Date.now());
          this.updateLayerInfo(layer);
          records.push({
            layer: layer.name,
            status: "success",
            durationMs: Date.now() - layerStartAt
          });
        } else {
          this.ensureLayerErrorSynced(layer, "图层加载失败");
          this.updateLayerInfo(layer);
          records.push({
            layer: layer.name,
            status: "failed",
            durationMs: Date.now() - layerStartAt,
            errorMessage: String(layer.lastErrorMessage || "").trim() || "图层加载失败"
          });
        }
      } catch (error) {
        console.error(`刷新图层 ${layer.name} 失败`, error);
        try {
          if (typeof layer.setLastError === "function") layer.setLastError(error);
          else layer.lastStatus = false;
          this.ensureLayerErrorSynced(layer, "图层加载失败");
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
          let ok = true;
          if (!wasEnabled) ok = await layer.show(1);
          else ok = await layer.refresh(true);
          if (!!ok) {
            if (typeof layer.clearLastError === "function") layer.clearLastError();
            if (typeof layer.updateNextRefreshAt === "function") layer.updateNextRefreshAt(layer.lastTime || Date.now());
            this.updateLayerInfo(layer);
          } else {
            this.ensureLayerErrorSynced(layer, "图层加载失败");
            this.updateLayerInfo(layer);
          }
        } else {
          if (wasEnabled) layer.hide();
          this.runtime.setLayerInfo(layer.name, "未开启");
        }
      } catch (error) {
        console.error(`设置图层 ${layer.name} 失败`, error);
        try {
          if (typeof layer.setLastError === "function") layer.setLastError(error);
          else layer.lastStatus = false;
          this.ensureLayerErrorSynced(layer, "图层加载失败");
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
        const ok = await layer.show(1);
        if (!!ok) {
          if (typeof layer.clearLastError === "function") layer.clearLastError();
          if (typeof layer.updateNextRefreshAt === "function") layer.updateNextRefreshAt(layer.lastTime || Date.now());
          this.updateLayerInfo(layer);
        } else {
          this.ensureLayerErrorSynced(layer, "图层加载失败");
          this.updateLayerInfo(layer);
        }
      } else {
        layer.hide();
        this.runtime.setLayerInfo(name, "未开启");
      }
    } catch (error) {
      console.error(`切换图层 ${name} 失败`, error);
      try {
        if (typeof layer.setLastError === "function") layer.setLastError(error);
        else layer.lastStatus = false;
        this.ensureLayerErrorSynced(layer, "图层加载失败");
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
    layer.setOpacity(0.8);
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
