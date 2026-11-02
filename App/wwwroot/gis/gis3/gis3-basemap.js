// Gis3Basemap - 底图样式管理
class Gis3Basemap {
    constructor(map, keys) {
        this.map = map;
        this.keys = keys;
        this.current = null;
        this.styles = {
            'tianditu-streets': {
                name: '天地图·街道',
                type: 'raster',
                tiles: [
                    `https://t{s}.tianditu.gov.cn/vec_c/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=vec&STYLE=default&TILEMATRIXSET=c&FORMAT=tiles&TILEMATRIX={z}&TILEROW={y}&TILECOL={x}&tk=${keys.tiandituKey}`
                ],
                tileSize: 256, minzoom: 0, maxzoom: 18, scheme: 'xyz',
                attribution: '© 天地图'
            },
            'tianditu-img': {
                name: '天地图·影像',
                type: 'raster',
                tiles: [
                    `https://t{s}.tianditu.gov.cn/img_c/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=img&STYLE=default&TILEMATRIXSET=c&FORMAT=tiles&TILEMATRIX={z}&TILEROW={y}&TILECOL={x}&tk=${keys.tiandituKey}`
                ],
                tileSize: 256, minzoom: 0, maxzoom: 18, scheme: 'xyz',
                attribution: '© 天地图'
            },
            'tianditu-ter': {
                name: '天地图·地形晕渲',
                type: 'raster',
                tiles: [
                    `https://t{s}.tianditu.gov.cn/ter_c/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=ter&STYLE=default&TILEMATRIXSET=c&FORMAT=tiles&TILEMATRIX={z}&TILEROW={y}&TILECOL={x}&tk=${keys.tiandituKey}`
                ],
                tileSize: 256, minzoom: 0, maxzoom: 14, scheme: 'xyz',
                attribution: '© 天地图'
            },
            'osm': {
                name: 'OpenStreetMap',
                type: 'raster',
                tiles: ['https://tile.openstreetmap.org/{z}/{x}/{y}.png'],
                tileSize: 256, minzoom: 0, maxzoom: 19, scheme: 'xyz',
                attribution: '© OpenStreetMap contributors'
            }
        };
    }

    // 初始化底图（首次加载样式）
    init(defaultStyle = 'tianditu-ter') {
        const style = this._buildStyle(defaultStyle);
        this.map.setStyle(style);
        this.current = defaultStyle;
    }

    // 切换底图
    switchStyle(key) {
        if (!this.styles[key] || this.current === key) return;
        this.map.setStyle(this._buildStyle(key));
        this.current = key;
    }

    // 构建 MapLibre style 对象
    _buildStyle(basemapKey) {
        const bm = this.styles[basemapKey];
        const scheme = bm.scheme || 'xyz';
        const tiles = bm.tiles.map(t => t.replace('{s}', '0'));
        return {
            version: 8,
            sources: {
                'basemap': {
                    type: 'raster',
                    tiles,
                    tileSize: bm.tileSize || 256,
                    minzoom: bm.minzoom || 0,
                    maxzoom: bm.maxzoom || 18,
                    scheme,
                    attribution: bm.attribution || ''
                }
            },
            layers: [{
                id: 'basemap-layer',
                type: 'raster',
                source: 'basemap'
            }]
        };
    }

    // 获取可用底图列表
    getList() {
        return Object.entries(this.styles).map(([k, v]) => ({ key: k, name: v.name, current: k === this.current }));
    }
}

window.Gis3Basemap = Gis3Basemap;