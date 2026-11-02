// Gis3App - 3D 地形工作台主应用（class 组合，无闭包）
class Gis3App {
    constructor(options) {
        this.options = options;
        this.map3d = null;     // Gis3Map
        this.basemap = null;   // Gis3Basemap
        this.terrain = null;   // Gis3Terrain
        this.header = null;    // Gis3Header
        this.toolbar = null;   // Gis3Toolbar
        this.floating = null;  // Gis3Floating
        this.layerPanel = null;// Gis3LayerPanel
        this.viewPanel = null; // Gis3ViewPanel
        this.sceneMenu = null; // 场景菜单 DOM
    }

    async init() {
        // 1. 时钟
        this.header = new Gis3Header(this.options.titleEl);
        this.header.start();

        // 2. 地图实例
        this.map3d = new Gis3Map(this.options);
        const map = this.map3d.create();

        // 3. 底图 + 地形管理器
        this.basemap = new Gis3Basemap(map, this.options.keys);
        this.terrain = new Gis3Terrain(map);

        // 4. 等地图 ready 后加载底图+地形
        this.map3d.onReady(() => {
            this.basemap.init('tianditu-ter');
            // 默认启用本地 DEM
            this.terrain.setSource('local-dem', { maptilerKey: this.options.keys.maptilerKey });
            this.terrain.toggle3D(true);
        });

        // 5. 工具栏
        this.toolbar = new Gis3Toolbar(this.map3d);
        this.toolbar.init();

        // 6. 图层面板（tab 切换）
        this.layerPanel = new Gis3LayerPanel({
            renderBasemap: () => this.viewPanel && this.viewPanel.renderBasemapList(),
            renderTerrain: () => this.viewPanel && this.viewPanel.renderTerrainList()
        });
        this.layerPanel.init();

        // 7. 视图面板（控件绑定 + 列表渲染）
        this.viewPanel = new Gis3ViewPanel({
            map3d: this.map3d,
            terrain: this.terrain,
            basemap: this.basemap
        });
        this.viewPanel.init();

        // 8. 浮动按钮组
        this.floating = new Gis3Floating({
            layer:   () => { this.layerPanel.toggle(); },
            terrain: () => { this.layerPanel.toggle(); this._switchTab('terrain'); },
            scene:   () => { this._toggleSceneMenu(); },
            toolbar: () => { this.toolbar.toggle(); },
            compass: () => {
                // 回到俯视
                map.setBearing(0);
                map.setPitch(0);
            }
        });
        this.floating.init();

        // 9. 场景菜单（简单实现：俯视/全球/中国）
        this._initSceneMenu();
    }

    // 切 tab
    _switchTab(name) {
        const tab = document.querySelector(`.layer-tab-btn[data-tab="${name}"]`);
        if (tab) tab.click();
    }

    // 场景菜单
    _initSceneMenu() {
        this.sceneMenu = document.getElementById('scene-menu');
        if (!this.sceneMenu) return;
        this.sceneMenu.innerHTML = `
            <div class="view-menu-options" style="padding:0.5rem">
                <button class="view-menu-btn" data-preset="top">俯视（2D）</button>
                <button class="view-menu-btn" data-preset="isometric">斜角（3D）</button>
                <button class="view-menu-btn" data-preset="china">中国全景</button>
                <button class="view-menu-btn" data-preset="globe">全球</button>
            </div>`;
        this.sceneMenu.querySelectorAll('[data-preset]').forEach(btn => {
            btn.addEventListener('click', () => {
                this.map3d.flyToPreset(btn.dataset.preset);
                this.sceneMenu.style.display = 'none';
            });
        });
    }
    _toggleSceneMenu() {
        if (!this.sceneMenu) return;
        const cur = this.sceneMenu.style.display;
        this.sceneMenu.style.display = cur === 'none' || cur === '' ? 'block' : 'none';
    }
}
window.Gis3App = Gis3App;