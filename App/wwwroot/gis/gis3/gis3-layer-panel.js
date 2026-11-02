// Gis3LayerPanel - 左侧图层/tab 面板
class Gis3LayerPanel {
    constructor(options) {
        this.panelEl = document.getElementById('layer-panel');
        this.tabs = document.querySelectorAll('.layer-tab-btn');
        this.tabPanels = document.querySelectorAll('.layer-tab-panel');
        this.renderBasemap = options.renderBasemap || (() => {});
        this.renderTerrain = options.renderTerrain || (() => {});
    }

    init() {
        this.tabs.forEach(tab => {
            tab.addEventListener('click', () => this._switchTab(tab.dataset.tab));
        });
    }

    _switchTab(tabName) {
        this.tabs.forEach(t => {
            const active = t.dataset.tab === tabName;
            t.classList.toggle('active', active);
            t.setAttribute('aria-selected', active ? 'true' : 'false');
        });
        this.tabPanels.forEach(p => {
            const active = p.dataset.tabPanel === tabName;
            p.classList.toggle('active', active);
            p.setAttribute('aria-hidden', active ? 'false' : 'true');
        });
        // 切到对应 tab 时刷新列表
        if (tabName === 'basemap') this.renderBasemap();
        if (tabName === 'terrain') this.renderTerrain();
    }

    // 整体面板显示/隐藏
    toggle() {
        if (!this.panelEl) return false;
        this.panelEl.classList.toggle('panel-hidden');
        return !this.panelEl.classList.contains('panel-hidden');
    }
}
window.Gis3LayerPanel = Gis3LayerPanel;