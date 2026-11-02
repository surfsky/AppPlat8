// Gis3ViewPanel - 视图控制（投影、3D、晕渲、夸张、俯仰、预设视角）
class Gis3ViewPanel {
    constructor(options) {
        this.map3d = options.map3d;         // Gis3Map 实例
        this.terrain = options.terrain;     // Gis3Terrain 实例
        this.basemap = options.basemap;     // Gis3Basemap 实例
    }

    init() {
        // 启用 3D 开关
        const toggle3d = document.getElementById('btn-toggle-3d');
        if (toggle3d) toggle3d.addEventListener('change', () => this.terrain.toggle3D(toggle3d.checked));

        // 晕渲阴影开关
        const toggleHill = document.getElementById('btn-toggle-hillshade');
        if (toggleHill) toggleHill.addEventListener('change', () => this.terrain.toggleHillshade(toggleHill.checked));

        // 地形夸张滑块
        const exaggeration = document.getElementById('terrain-exaggeration');
        const exaggerationVal = document.getElementById('terrain-exaggeration-val');
        if (exaggeration) {
            exaggeration.addEventListener('input', () => {
                const v = parseFloat(exaggeration.value);
                if (exaggerationVal) exaggerationVal.textContent = v.toFixed(1);
                this.terrain.setExaggeration(v);
            });
        }

        // 俯仰角度滑块
        const pitch = document.getElementById('map-pitch');
        const pitchVal = document.getElementById('map-pitch-val');
        if (pitch) {
            pitch.addEventListener('input', () => {
                const v = parseInt(pitch.value);
                if (pitchVal) pitchVal.textContent = v;
                this.terrain.setPitch(v);
            });
        }

        // 视角预设按钮
        document.querySelectorAll('[data-preset]').forEach(btn => {
            btn.addEventListener('click', () => {
                const preset = btn.dataset.preset;
                this.map3d.flyToPreset(preset);
                // 同步 checkbox/滑块
                if (preset === 'top') {
                    toggle3d && (toggle3d.checked = false);
                    this.terrain.toggle3D(false);
                } else {
                    toggle3d && (toggle3d.checked = true);
                    this.terrain.toggle3D(true);
                }
            });
        });

        // 底图列表渲染（点击事件委托到 list 容器）
        const basemapList = document.getElementById('basemap-list');
        if (basemapList) {
            basemapList.addEventListener('click', (e) => {
                const item = e.target.closest('[data-basemap]');
                if (item) {
                    this.basemap.switchStyle(item.dataset.basemap);
                    this.renderBasemapList();
                }
            });
        }

        // 地形源列表
        const terrainList = document.getElementById('terrain-list');
        if (terrainList) {
            terrainList.addEventListener('click', (e) => {
                const item = e.target.closest('[data-terrain]');
                if (item) {
                    this.terrain.setSource(item.dataset.terrain, { maptilerKey: this.terrain.keys?.maptilerKey || '' });
                    this.renderTerrainList();
                }
            });
        }
    }

    // 渲染底图列表
    renderBasemapList() {
        const list = document.getElementById('basemap-list');
        if (!list) return;
        list.innerHTML = '';
        this.basemap.getList().forEach(item => {
            const div = document.createElement('label');
            div.className = 'view-menu-option';
            div.dataset.basemap = item.key;
            div.innerHTML = `<input type="radio" name="basemap" value="${item.key}" ${item.current ? 'checked' : ''}/><span>${item.name}</span>`;
            list.appendChild(div);
        });
    }

    // 渲染地形源列表
    renderTerrainList() {
        const list = document.getElementById('terrain-list');
        if (!list) return;
        list.innerHTML = '';
        this.terrain.getList().forEach(item => {
            const div = document.createElement('label');
            div.className = 'view-menu-option';
            div.dataset.terrain = item.key;
            div.innerHTML = `<input type="radio" name="terrain" value="${item.key}" ${item.current ? 'checked' : ''}/><span>${item.name}</span>`;
            list.appendChild(div);
        });
    }
}
window.Gis3ViewPanel = Gis3ViewPanel;