
import { CronSchedule } from "./CronSchedule.js";

/****************************************************************
 * 地图图层基类
 ****************************************************************/
export class MapLayer {
  constructor(opts) {
    this.name = opts.name;
    this.title = opts.title;
    this.descript = opts.descript || "";
    this.api = opts.api || "";
    this.key = opts.key || "";
    this.refreshCron = String(opts.refreshCron || "").trim();
    this.refreshSchedule = this.refreshCron ? CronSchedule.parse(this.refreshCron) : null;
    this.refreshText = this.refreshSchedule
      ? this.refreshSchedule.describe()
      : String(opts.refreshText || "").trim();
    this.nextRefreshAt = this.refreshSchedule ? this.refreshSchedule.nextAfter(Date.now())?.getTime() || 0 : 0;
    this.lastTime = 0;
    this.lastStatus = false;
    this.dataTimeValue = null;
    this.dataTimeText = "";
    this.infoExtra = "";
    this.lastErrorMessage = "";
    this.visible = false;
    this.opacity = 0.8;
    this.runtime = null;
  }

  bind(runtime) {
    this.runtime = runtime;
  }

  setDataTime(value) {
    this.dataTimeValue = value;
    this.dataTimeText = "";
  }

  setDataTimeText(text) {
    this.dataTimeText = String(text || "").trim();
    this.dataTimeValue = null;
  }

  clearDataTime() {
    this.dataTimeValue = null;
    this.dataTimeText = "";
  }

  setRefreshCron(expression) {
    this.refreshCron = String(expression || "").trim();
    this.refreshSchedule = this.refreshCron ? CronSchedule.parse(this.refreshCron) : null;
    this.refreshText = this.refreshSchedule ? this.refreshSchedule.describe() : "";
    this.updateNextRefreshAt();
  }

  setInfoExtra(text) {
    this.infoExtra = String(text || "").trim();
  }

  setLastError(error) {
    const msg = error instanceof Error
      ? error.message
      : String(error || "").trim();
    this.lastErrorMessage = msg || "图层加载失败";
    this.lastErrorAt = Date.now();
    this.lastStatus = false;
  }

  clearLastError() {
    this.lastErrorMessage = "";
    this.lastErrorAt = 0;
  }

  formatLocalTime(value) {
    const date = value ? new Date(value) : null;
    if (!date || Number.isNaN(date.getTime())) return "";
    const y = date.getFullYear();
    const m = String(date.getMonth() + 1).padStart(2, "0");
    const d = String(date.getDate()).padStart(2, "0");
    const hh = String(date.getHours()).padStart(2, "0");
    const mm = String(date.getMinutes()).padStart(2, "0");
    return `${y}-${m}-${d} ${hh}:${mm}`;
  }

  getRefreshTextDisplay() {
    return String(this.refreshText || "").trim();
  }

  getRefreshCronDisplay() {
    return String(this.refreshCron || "").trim();
  }

  updateNextRefreshAt(referenceTime = this.lastTime || Date.now()) {
    if (!this.refreshSchedule) {
      this.nextRefreshAt = 0;
      return 0;
    }
    const next = this.refreshSchedule.nextAfter(new Date(referenceTime || Date.now()));
    this.nextRefreshAt = next ? next.getTime() : 0;
    return this.nextRefreshAt;
  }

  getDataTimeDisplay() {
    if (this.dataTimeText) return this.dataTimeText;
    if (this.dataTimeValue === null || this.dataTimeValue === undefined) return "";
    const txt = this.formatLocalTime(this.dataTimeValue);
    return txt || "";
  }

  getDataTimeShortDisplay() {
    if (this.dataTimeValue !== null && this.dataTimeValue !== undefined) {
      const date = new Date(this.dataTimeValue);
      if (!date || Number.isNaN(date.getTime())) return "";
      const hh = String(date.getHours()).padStart(2, "0");
      const mm = String(date.getMinutes()).padStart(2, "0");
      return `${hh}:${mm}`;
    }
    const txt = String(this.dataTimeText || "").trim();
    if (!txt) return "";
    const m = txt.match(/(?:\s|^)(\d{2}:\d{2})(?::\d{2})?(?:\s|$)/);
    return m ? m[1] : "";
  }

  buildDebugInfo() {
    return {
      name: this.name,
      title: this.title,
      visible: this.visible,
      status: this.lastStatus === false ? "error" : (this.visible ? "on" : "off"),
      dataTime: this.getDataTimeDisplay(),
      refreshCron: this.getRefreshCronDisplay(),
      refreshText: this.getRefreshTextDisplay(),
      nextRefreshAt: this.formatLocalTime(this.nextRefreshAt),
      extra: this.infoExtra || "",
      error: this.lastErrorMessage || "",
      lastRefresh: this.formatLocalTime(this.lastTime)
    };
  }

  buildInfoText() {
    if (!this.visible) return "未开启";
    if (this.lastStatus === false) return "加载失败";
    const parts = [];
    const dataText = this.getDataTimeShortDisplay();
    if (dataText) parts.push(dataText);
    const interval = this.getRefreshTextDisplay();
    if (interval) parts.push(interval);
    return parts.length ? parts.join(" | ") : "已开启";
  }

  buildInfoTooltip() {
    if (!this.visible) return "";
    if (this.lastStatus === false) return this.lastErrorMessage || "图层加载失败";

    const parts = [];
    const timeText = this.getDataTimeDisplay();
    if (timeText) parts.push(`数据时间: ${timeText}`);
    const refreshedAt = this.formatLocalTime(this.lastTime);
    if (refreshedAt) parts.push(`最近刷新: ${refreshedAt}`);
    const interval = this.getRefreshTextDisplay();
    if (interval) parts.push(`更新频率: ${interval}`);
    if (this.infoExtra) parts.push(this.infoExtra);
    return parts.join("\n");
  }

  /**获取图层运行状态信息 */
  getRuntimeState() {
    const safeOpacity = Number.isFinite(Number(this.opacity)) ? Number(this.opacity) : 0.8;
    return {
      name: this.name,
      title: this.title || "",
      visible: !!this.visible,
      status: !this.visible ? "off" : (this.lastStatus === false ? "error" : "on"),
      opacity: safeOpacity,
      dataTime: this.getDataTimeDisplay(),
      updatePeriod: this.getRefreshTextDisplay() || (this.getRefreshCronDisplay() || ""),
      updatePeriodCron: this.getRefreshCronDisplay(),
      lastRefreshAt: this.formatLocalTime(this.lastTime),
      lastRefreshAtMs: Number(this.lastTime) || 0,
      nextRefreshAt: this.formatLocalTime(this.nextRefreshAt || 0),
      nextRefreshAtMs: Number(this.nextRefreshAt) || 0,
      lastErrorAt: Number(this.lastErrorAt) || 0,
      lastErrorMessage: String(this.lastErrorMessage || "").trim()
    };
  }

  /**确保 lastStatus===false 时有错误文案（兜底：有些子类只 return false 不写 error） */
  ensureLastErrorSynced(defaultMsg = "图层加载失败") {
    if (this.lastStatus === false && !String(this.lastErrorMessage || "").trim()) {
      this.lastErrorMessage = String(defaultMsg || "图层加载失败");
      if (!this.lastErrorAt) this.lastErrorAt = this.lastTime || Date.now();
    }
  }

  /**主动触发一次配置面板的手动刷新（成功/失败都会重算面板信息） */
  async refreshForConfig(opacity = 0.8) {
    const start = Date.now();
    try {
      let ok = true;
      if (this.visible) {
        ok = await this.refresh(true);
      } else {
        const safe = Number.isFinite(Number(opacity)) ? Number(opacity) : 0.8;
        ok = await this.show(safe);
      }
      this.ensureLastErrorSynced("图层刷新失败");
      if (ok) {
        if (typeof this.clearLastError === "function") this.clearLastError();
        return {
          ok: true,
          durationMs: Date.now() - start,
          error: ""
        };
      }
      const msg = String(this.lastErrorMessage || "").trim() || "图层刷新失败";
      return {
        ok: false,
        durationMs: Date.now() - start,
        error: msg
      };
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e || "图层刷新失败");
      if (typeof this.setLastError === "function") this.setLastError(msg || "图层刷新失败");
      return {
        ok: false,
        durationMs: Date.now() - start,
        error: msg || "图层刷新失败"
      };
    }
  }

  /**设置图层不透明度并返回下一次 opacity（不抛错） */
  applyOpacity(opacity) {
    const next = Math.max(0, Math.min(1, Number.isFinite(Number(opacity)) ? Number(opacity) : 0.8));
    try {
      this.setOpacity(next);
    } catch (e) {
      console.error("applyOpacity failed:", e);
    }
    this.opacity = next;
    return next;
  }

  /**
   *
   * @param {*} opacity
   * @returns
   */
  async show(opacity = 0.8) {
    this.visible = true;
    this.clearLastError();
    this.setOpacity(Number.isFinite(Number(opacity)) ? Number(opacity) : 0.8);
    const ok = await this.refresh(true);
    this.lastStatus = !!ok;
    this.lastTime = this.lastTime || Date.now();
    if (ok) this.updateNextRefreshAt(this.lastTime);
    this.ensureLastErrorSynced("图层加载失败");
    return !!ok;
  }

  hide() {
    this.visible = false;
    this.clearLastError();
    return true;
  }

  setOpacity(_opacity) {
    this.opacity = _opacity;
  }

  async refresh(_force = false) {
    this.lastTime = this.lastTime || Date.now();
    this.clearLastError();
    this.lastStatus = true;
    this.updateNextRefreshAt(this.lastTime);
    return true;
  }
}
