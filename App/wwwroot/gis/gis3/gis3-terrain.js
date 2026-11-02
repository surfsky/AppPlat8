// Gis3Terrain - 3D 地形/DEM 管理
class Gis3Terrain {
    constructor(map) {
        this.map = map;
        this.currentSource = null;
        this.hillshadeOn = true;
        this.terrainOn = true;
        this.exaggeration = 1.5;
        this.pitch = 45;

        // 地形源列表（MapLibre 原生只支持 raster-dem，terrarium 或 mapbox 编码）
        this.sources = {
            'local-dem': {
                name: '本地 DEM（已下载）',
                type: 'raster-dem',
                url: '/tiles/dem/tiles.json',
                encoding: 'terrarium'
            },
            'mapterhorn': {
                name: 'Mapterhorn 全球 DEM（在线）',
                type: 'raster-dem',
                tiles: ['https://tiles.mapterhorn.com/{z}/{x}/{y}.png'],
                tileSize: 256,
                minzoom: 0,
                maxzoom: 8,
                encoding: 'terrarium'
            },
            'maptiler': {
                name: 'MapTiler 全球 DEM（需 Key）',
                type: 'raster-dem',
                tiles: (key) => [`https://api.maptiler.com/tiles/terrain-rgb/{z}/{x}/{y}.png?key=${key}`],
                tileSize: 256,
                minzoom: 0,
                maxzoom: 14,
                encoding: 'mapbox'
            }
        };
    }

    // 设置地图对象（需要等 MapLibre 加载完才可以 setTerrain）
    setMap(map) { this.map = map; }

    // 激活/切换地形源
    async setSource(key, extra = {}) {
        if (!this.map || !this.map.addSource) return;
        if (this.currentSource === key) return;
        const src = this.sources[key];
        if (!src) return;
        try {
            // 移除旧地形
            if (this.currentSource) {
                const oldSrc = this.map.getSource('terrain-dem');
                if (oldSrc) {
                    try { this.map.removeLayer('hillshade'); } catch(e){}
                    try { this.map.removeSource('terrain-dem'); } catch(e){}
                }
            }

            // 构建 source 配置
            let sourceCfg = { type: 'raster-dem', encoding: src.encoding };
            if (src.url) sourceCfg.url = src.url;
            else if (src.tiles) {
                sourceCfg.tiles = typeof src.tiles === 'function' ? src.tiles(extra.maptilerKey || '') : src.tiles;
                sourceCfg.tileSize = src.tileSize || 256;
                sourceCfg.minzoom = src.minzoom || 0;
                sourceCfg.maxzoom = src.maxzoom || 12;
            }
            this.map.addSource('terrain-dem', sourceCfg);
            this.map.setTerrain({ source: 'terrain-dem', exaggeration: this.exaggeration });
            this.currentSource = key;

            // 晕渲阴影图层
            if (this.hillshadeOn) this.addHillshade();
        } catch (err) {
            console.warn('[Gis3Terrain] setSource 失败:', key, err);
            // 回退：移除地形避免地图黑屏
            try { this.map.setTerrain(null); } catch(e){}
            this.currentSource = null;
        }
    }

    // 增加晕渲阴影 hillshade 图层
    addHillshade() {
        if (!this.map || !this.currentSource) return;
        if (this.map.getLayer('hillshade')) return;
        try {
            this.map.addLayer({
                id: 'hillshade',
                type: 'hillshade',
                source: 'terrain-dem',
                paint: {
                    'hillshade-illumination-direction': 315,
                    'hillshade-illumination-anchor': 'viewport',
                    'hillshade-exaggeration': this.exaggeration * 0.3
                }
            }, 'basemap-layer');
        } catch(e) { /* ignore */ }
    }

    // 移除晕渲阴影
    removeHillshade() {
        try { this.map.removeLayer('hillshade'); } catch(e){}
    }

    // 开关 3D 地形
    toggle3D(on) {
        this.terrainOn = on;
        if (on && this.currentSource) {
            try {
                this.map.setTerrain({ source: 'terrain-dem', exaggeration: this.exaggeration });
                this.map.setPitch(this.pitch);
                if (this.hillshadeOn) this.addHillshade();
            } catch(e) {}
        } else {
            try {
                this.map.setTerrain(null);
                this.map.setPitch(0);
                this.removeHillshade();
            } catch(e) {}
        }
    }

    // 开关晕渲阴影
    toggleHillshade(on) {
        this.hillshadeOn = on;
        if (on) this.addHillshade();
        else this.removeHillshade();
    }

    // 设置地形夸张
    setExaggeration(val) {
        this.exaggeration = parseFloat(val);
        if (this.currentSource && this.terrainOn) {
            try {
                this.map.setTerrain({ source: 'terrain-dem', exaggeration: this.exaggeration });
                // 同步晕渲夸张
                if (this.map.getLayer('hillshade')) {
                    this.map.setPaintProperty('hillshade', 'hillshade-exaggeration', this.exaggeration * 0.3);
                }
            } catch(e){}
        }
    }

    // 设置俯仰角度
    setPitch(val) {
        this.pitch = parseInt(val);
        if (this.terrainOn && this.map) {
            try { this.map.setPitch(this.pitch); } catch(e){}
        }
    }

    // 获取可用地形源列表
    getList(maptilerKey) {
        return Object.entries(this.sources).map(([k, v]) => ({
            key: k,
            name: v.name + (k === 'maptiler' && (!maptilerKey || maptilerKey.length < 8) ? ' (需 Key)' : ''),
            current: k === this.currentSource
        }));
    }
}

window.Gis3Terrain = Gis3Terrain;