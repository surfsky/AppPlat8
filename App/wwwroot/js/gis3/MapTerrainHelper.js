import { PopupControl } from './MapControls.js';

/*********************************************************
 * 地形绘制帮助类
 *********************************************************/
export class MapTerrainHelper {
    constructor(state, options = {}) {
        this.state = state;
        this.terrainSourceId = options.terrainSourceId || 'dem-terrain';
        this.hillSourceId = options.hillSourceId || 'dem-hill';
        this.hillLayerId = options.hillLayerId || 'dem-hillshade';
        this.hillBeforeLayerId = options.hillBeforeLayerId;
        this.getDemSource = options.getDemSource || (() => ({
            type: 'raster-dem',
            tiles: ['/tiles/dem/{z}/{x}/{y}.png'],
            tileSize: 256, encoding: 'terrarium', maxzoom: 14,
        }));
    }

    /** 为底图样式添加地形和独立的山体阴影数据源。 */
    addTerrainToStyle(style) {
        style.sources[this.terrainSourceId] ||= this.getDemSource();
        style.sources[this.hillSourceId] ||= this.getDemSource();
        if (!style.layers.some(layer => layer.id === this.hillLayerId)) {
            const hillLayer = {
                id: this.hillLayerId, type: 'hillshade',
                source: this.hillSourceId,
                layout: { visibility: this.state.currentExaggeration > 0 ? 'visible' : 'none' },
                paint: { 'hillshade-exaggeration': 0.5 },
            };
            const before = style.layers.findIndex(layer => layer.id === this.hillBeforeLayerId);
            if (before < 0) style.layers.push(hillLayer);
            else style.layers.splice(before, 0, hillLayer);
        }
        const exaggeration = this.state.currentExaggeration;
        if (exaggeration > 0) {
            style.terrain = { source: this.terrainSourceId, exaggeration };
        }
        return style;
    }

    /** 切换地形夸张度（0=无地形 / 1=1倍 / 2=2倍）。 */
    setTerrain(level) {
        this.state.currentExaggeration = level;
        const map = this.state.map;
        if (!map) return;
        if (level > 0) {
            if (!map.getSource(this.terrainSourceId)) map.addSource(this.terrainSourceId, this.getDemSource());
            if (!map.getSource(this.hillSourceId)) map.addSource(this.hillSourceId, this.getDemSource());
            if (!map.getLayer(this.hillLayerId)) {
                map.addLayer({
                    id: this.hillLayerId, type: 'hillshade', source: this.hillSourceId,
                    paint: { 'hillshade-exaggeration': 0.5 },
                }, this.hillBeforeLayerId && map.getLayer(this.hillBeforeLayerId) ? this.hillBeforeLayerId : undefined);
            }
        }
        map.setTerrain(level === 0 ? null : { source: this.terrainSourceId, exaggeration: level });
        if (map.getLayer(this.hillLayerId)) {
            map.setLayoutProperty(this.hillLayerId, 'visibility', level > 0 ? 'visible' : 'none');
        }
    }

    createTerrainControl() {
        return new PopupControl({
            faIcon: 'fa-solid fa-mountain', title: '三维地形',
            options: [
                { label: '无地形', value: 0 },
                { label: '1 倍地形', value: 1 },
                { label: '2 倍地形', value: 2 },
            ],
            currentValue: this.state.currentExaggeration,
            onChange: level => this.setTerrain(level),
        });
    }
}