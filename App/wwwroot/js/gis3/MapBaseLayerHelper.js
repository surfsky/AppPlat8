import { PopupControl } from './MapControls.js';

/*********************************************************
 * 底图图层帮助类
 *********************************************************/
export class MapBaseLayerHelper {
    constructor(state, terrainHelper) {
        this.state = state;
        this.terrainHelper = terrainHelper;
    }

    /** 获取天地图 key（cshtml 通过 meta 注入）。 */
    getTiandituKey() {
        const meta = document.querySelector('meta[name="tianditu-key"]');
        return meta ? meta.content : '';
    }

    /** 构建天地图底图 style（type=img 影像 / type=vec 矢量）。 */
    tiandituStyle(key, type) {
        const labelPrefix = type === 'vec' ? 'cva' : 'cia';
        const tiles = [];
        const labelTiles = [];
        for (let i = 0; i <= 7; i++) {
            tiles.push(
                `https://t${i}.tianditu.gov.cn/${type}_w/wmts` +
                `?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0` +
                `&LAYER=${type}&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles` +
                `&TILEMATRIX={z}&TILEROW={y}&TILECOL={x}` + (key ? `&tk=${key}` : '')
            );
            labelTiles.push(
                `https://t${i}.tianditu.gov.cn/${labelPrefix}_w/wmts` +
                `?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0` +
                `&LAYER=${labelPrefix}&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles` +
                `&TILEMATRIX={z}&TILEROW={y}&TILECOL={x}` + (key ? `&tk=${key}` : '')
            );
        }
        const srcName = `base-${type}`;
        const labelName = `label-${type}`;
        return this.terrainHelper.addTerrainToStyle({
            version: 8,
            projection: { type: this.state.currentProjection },
            sources: {
                [srcName]: { type: 'raster', tiles, tileSize: 256, maxzoom: 18 },
                [labelName]: { type: 'raster', tiles: labelTiles, tileSize: 256, maxzoom: 18 },
            },
            layers: [
                { id: srcName, type: 'raster', source: srcName },
                { id: labelName, type: 'raster', source: labelName },
            ],
            sky: {},
        });
    }

    /** OpenStreetMap 底图 style（OSM 光栅瓦片 + DEM 地形）。 */
    osmStyle() {
        return this.terrainHelper.addTerrainToStyle({
            version: 8, projection: { type: this.state.currentProjection },
            sources: {
                'osm-raster': {
                    type: 'raster',
                    tiles: ['https://tile.openstreetmap.org/{z}/{x}/{y}.png'],
                    tileSize: 256,
                    attribution: '© OpenStreetMap contributors',
                    maxzoom: 19,
                },
            },
            layers: [{ id: 'osm-tiles', type: 'raster', source: 'osm-raster' }],
            sky: {},
        });
    }

    getBaseStyle(base = this.state.currentBase) {
        const builders = {
            tianditu_vec: () => this.tiandituStyle(this.getTiandituKey(), 'vec'),
            tianditu_img: () => this.tiandituStyle(this.getTiandituKey(), 'img'),
            osm: () => this.osmStyle(),
            maplibre_demo: () => 'https://demotiles.maplibre.org/style.json',
            carto_dark: () => 'https://basemaps.cartocdn.com/gl/dark-matter-gl-style/style.json',
        };
        return builders[base]();
    }

    switchBase(base) {
        this.state.currentBase = base;
        if (!this.state.map) return;
        this.state.map.setStyle(this.getBaseStyle(), { diff: false });
    }

    createBaseLayerControl() {
        return new PopupControl({
            faIcon: 'fa-solid fa-layer-group', title: '底图',
            options: [
                { label: '天地图矢量', value: 'tianditu_vec' },
                { label: '天地图影像', value: 'tianditu_img' },
                { label: 'OpenStreetMap', value: 'osm' },
                { label: 'MapLibre Demo', value: 'maplibre_demo' },
                { label: 'CartoDB 暗色', value: 'carto_dark' },
            ],
            currentValue: this.state.currentBase,
            onChange: base => this.switchBase(base),
        });
    }
}