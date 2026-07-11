
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
    this.refreshSeconds = opts.refreshSeconds || 0;
    this.dataInterval = String(opts.dataInterval || "").trim();
    this.lastTime = 0;
    this.lastStatus = false;
    this.dataTimeValue = null;
    this.dataTimeText = "";
    this.infoExtra = "";
    this.lastErrorMessage = "";
    this.visible = false;
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

  setDataInterval(text) {
    this.dataInterval = String(text || "").trim();
  }

  setInfoExtra(text) {
    this.infoExtra = String(text || "").trim();
  }

  setLastError(error) {
    const msg = error instanceof Error
      ? error.message
      : String(error || "").trim();
    this.lastErrorMessage = msg || "图层加载失败";
    this.lastStatus = false;
  }

  clearLastError() {
    this.lastErrorMessage = "";
  }

  formatShortTime(value) {
    const date = value ? new Date(value) : null;
    if (!date || Number.isNaN(date.getTime())) return "";
    const hh = String(date.getHours()).padStart(2, "0");
    const mm = String(date.getMinutes()).padStart(2, "0");
    return `${hh}:${mm}`;
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

  formatAutoRefreshFrequency() {
    const sec = Number(this.refreshSeconds) || 0;
    if (sec <= 0) return "手动";
    return this.formatSecondsAsText(sec);
  }

  formatSecondsAsText(sec) {
    const n = Number(sec) || 0;
    if (n <= 0) return "";
    if (n % 3600 === 0) return `${n / 3600}小时`;
    if (n % 60 === 0) return `${n / 60}分钟`;
    return `${n}秒`;
  }

  getDataIntervalDisplay() {
    return String(this.dataInterval || "").trim();
  }

  getDataTimeDisplay() {
    if (this.dataTimeText) return this.dataTimeText;
    if (this.dataTimeValue === null || this.dataTimeValue === undefined) return "";
    const txt = this.formatLocalTime(this.dataTimeValue);
    return txt || "";
  }

  getDataTimeShortDisplay() {
    if (this.dataTimeValue !== null && this.dataTimeValue !== undefined) {
      return this.formatShortTime(this.dataTimeValue);
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
      dataInterval: this.getDataIntervalDisplay(),
      autoRefresh: this.formatAutoRefreshFrequency(),
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
    const interval = this.getDataIntervalDisplay();
    if (interval) parts.push(interval);
    return parts.length ? parts.join(" | ") : "已开启";
  }

  buildInfoTooltip() {
    if (!this.visible) return "";
    if (this.lastStatus === false) return this.lastErrorMessage || "图层加载失败";

    const parts = [];
    const timeText = this.getDataTimeDisplay();
    if (timeText) parts.push(`数据时间: ${timeText}`);
    const interval = this.getDataIntervalDisplay();
    if (interval) parts.push(`更新频率: ${interval}`);
    if (this.infoExtra) parts.push(this.infoExtra);
    return parts.join("\n");
  }

  /**
   * 
   * @param {*} opacity 
   * @returns 
   */
  async show(opacity = 1) {
    this.visible = true;
    this.clearLastError();
    this.setOpacity(opacity);
    const ok = await this.refresh(true);
    this.lastStatus = ok;
    this.lastTime = Date.now();
    return ok;
  }

  hide() {
    this.visible = false;
    this.clearLastError();
    return true;
  }

  setOpacity(_opacity) {
    this.opacity = _opacity;
  }

  shouldRefresh() {
    if (!this.visible || this.refreshSeconds <= 0) return false;
    return Date.now() - this.lastTime >= this.refreshSeconds * 1000;
  }

  async refresh(_force = false) {
    this.lastTime = Date.now();
    this.clearLastError();
    this.lastStatus = true;
    return true;
  }
}
