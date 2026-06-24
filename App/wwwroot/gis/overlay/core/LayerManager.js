import { setInfo } from "./utils.js";

 /****************************************************************
 * 地图图层管理器
 ****************************************************************/
export class LayerManager {
  constructor(map, layers) {
    this.map = map;
    this.layers = layers;
    this.layerMap = new Map();
    for (const layer of layers) this.layerMap.set(layer.name, layer);

    this.runtime = {
      map,
      getOpacity: _name => 1,
      isEnabled: name => {
        const el = document.getElementById(name);
        return !!(el && el.checked);
      },
      getInfoId: name => this.getInfoId(name),
      setLayerInfo: (name, text) => {
        const infoId = this.getInfoId(name);
        if (infoId) setInfo(infoId, text);
      }
    };

    for (const layer of layers) layer.bind(this.runtime);
  }

  updateLayerInfo(layer) {
    if (!layer) return;
    const text = typeof layer.buildInfoText === "function" ? layer.buildInfoText() : "已开启";
    this.runtime.setLayerInfo(layer.name, text);
    if (typeof layer.buildDebugInfo === "function") {
      console.debug("[MapLayerInfo]", layer.buildDebugInfo());
    }
  }

  /**
   * 刷新所有可见图层
   * @param {boolean} force 是否强制刷新
   */
  async refreshVisible(force = false) {
    for (const layer of this.layers) {
      if (!this.runtime.isEnabled(layer.name)) continue;
      if (!force && !layer.shouldRefresh()) continue;
      try {
        await layer.refresh(force);
        this.updateLayerInfo(layer);
      } catch (error) {
        console.error(`刷新图层 ${layer.name} 失败`, error);
        try {
          layer.lastStatus = false;
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
        this.updateLayerInfo(layer);
      } else {
        layer.hide();
        this.runtime.setLayerInfo(name, "未开启");
      }
    } catch (error) {
      console.error(`切换图层 ${name} 失败`, error);
      try {
        layer.lastStatus = false;
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
  }

  getInfoId(name) {
    const toggleEl = document.getElementById(name);
    const infoId = toggleEl?.dataset?.infoId;
    return infoId || `${name}Info`;
  }
}
