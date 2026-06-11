
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
    this.lastTime = 0;
    this.lastStatus = false;
    this.visible = false;
    this.runtime = null;
  }

  bind(runtime) {
    this.runtime = runtime;
  }

  /**
   * 
   * @param {*} opacity 
   * @returns 
   */
  async show(opacity = 1) {
    this.visible = true;
    this.setOpacity(opacity);
    const ok = await this.refresh(true);
    this.lastStatus = ok;
    this.lastTime = Date.now();
    return ok;
  }

  hide() {
    this.visible = false;
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
    this.lastStatus = true;
    return true;
  }
}
