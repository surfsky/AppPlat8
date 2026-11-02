// Gis3Map - MapLibre 地图实例与视图预设
class Gis3Map {
    constructor(options) {
        this.container = options.mapContainer || 'map';
        this.center = options.defaultCenter || [120.6034, 27.5686];
        this.zoom = options.defaultZoom || 5;
        this.keys = options.keys || {};
        this.map = null;
        this._onReadyCbs = [];
    }

    // 创建 MapLibre 地图实例
    create() {
        this.map = new window.maplibregl.Map({
            container: this.container,
            center: this.center,
            zoom: this.zoom,
            pitch: 0,
            bearing: 0,
            attributionControl: false,
            preserveDrawingBuffer: false
        });
        this.map.addControl(new window.maplibregl.NavigationControl({ showCompass: false }), 'top-right');
        this.map.addControl(new window.maplibregl.ScaleControl({ unit: 'metric' }), 'bottom-left');

        this.map.on('load', () => {
            this._onReadyCbs.forEach(cb => cb(this.map));
            this._onReadyCbs = [];
        });
        return this.map;
    }

    // 等待地图 ready
    onReady(cb) {
        if (this.map && this.map.isStyleLoaded && this.map.isStyleLoaded()) cb(this.map);
        else this._onReadyCbs.push(cb);
    }

    // 视角预设
    flyToPreset(preset) {
        const presets = {
            top:      { center: this.center, zoom: this.zoom, pitch: 0, bearing: 0, duration: 1200 },
            isometric:{ center: this.center, zoom: this.zoom + 1, pitch: 50, bearing: 30, duration: 1500 },
            globe:    { center: [104.0, 35.0], zoom: 2, pitch: 25, bearing: 0, duration: 2500 },
            china:    { center: [104.0, 35.0], zoom: 4, pitch: 30, bearing: 0, duration: 2000 }
        };
        const opt = presets[preset] || presets.top;
        if (!this.map) return;
        this.map.flyTo(opt);
    }

    // 定位到指定坐标
    flyTo(lng, lat, zoom = 10) {
        if (!this.map) return;
        this.map.flyTo({ center: [lng, lat], zoom, duration: 1200 });
    }
}

window.Gis3Map = Gis3Map;