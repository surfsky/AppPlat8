// GIS3 主逻辑：三维地形球体地图
import * as maplibregl from '/lib/maplibre-gl-v6/maplibre-gl.mjs';
import { IconButtonControl, ToggleButtonControl } from './MapControls.js';
import { MapBaseLayerHelper } from './MapBaseLayerHelper.js';
import { MapTerrainHelper } from './MapTerrainHelper.js';
import { MapStarHelper } from './MapStarHelper.js';


//============================================================
// 全局配置
//============================================================
window.GIS3 = {
    map: null,
    currentBase: 'tianditu_img',
    currentProjection: 'mercator',
    currentTheme: 'light',
    currentExaggeration: 0,
    mapContainer: null,
    zoom: 2.5, 
    pitch: 0, 
    bearing: 0,
    center: [120.6034, 27.5686]
};



//============================================================
// 启动
//============================================================
export function initMap() {
    GIS3.mapContainer = document.getElementById('map');
    const terrainHelper = new MapTerrainHelper(GIS3);
    const baseLayerHelper = new MapBaseLayerHelper(GIS3, terrainHelper);
    const starHelper = new MapStarHelper(GIS3);

    starHelper.init();

    const map = new maplibregl.Map({
        container: 'map',
        style: baseLayerHelper.getBaseStyle(),
        antialias: true, preserveDrawingBuffer: true,
        maxZoom: 18, maxPitch: 85,
        zoom: GIS3.zoom, pitch: GIS3.pitch, bearing: GIS3.bearing,
        center: GIS3.center,
    });
    GIS3.map = map;

    // 地图加载完成事件
    map.on('load', () => {
        setTheme(GIS3.currentTheme, starHelper);
        console.log('[GIS3] ✅ 地图加载完成');
    });

    // 原生控件
    map.addControl(new maplibregl.FullscreenControl(), 'top-right');
    map.addControl(new maplibregl.NavigationControl({ showCompass: true, showZoom: true }), 'top-right');
    map.addControl(new maplibregl.GlobeControl(), 'top-right');
    map.addControl(new maplibregl.ScaleControl({ unit: 'metric' }), 'bottom-left');

    // 底图切换控件
    map.addControl(baseLayerHelper.createBaseLayerControl(), 'top-right');

    // 地形切换控件
    map.addControl(terrainHelper.createTerrainControl(), 'top-right');

    // 主题 Toggle 按钮（默认浅色=太阳，点一下切暗色=月亮）
    map.addControl(new ToggleButtonControl({
        faIconA: 'fa-solid fa-sun', titleA: '切换暗色模式',
        faIconB: 'fa-solid fa-moon', titleB: '切换浅色模式',
        initialState: false,  // false=显示太阳=浅色模式
        onToggle: (isDark) => setTheme(isDark ? 'dark' : 'light', starHelper),
    }), 'top-right');

    // 定位控件
    map.addControl(new maplibregl.GeolocateControl({
        positionOptions: { enableHighAccuracy: true },
        trackUserLocation: true, showAccuracyCircle: true,
    }), 'top-right');

    // 重置视角按钮
    map.addControl(new IconButtonControl({
        faIcon: 'fa-solid fa-undo', title: '重置视角',
        onClick: ()=>GIS3.map?.flyTo({center: GIS3.center, zoom: GIS3.zoom, pitch: GIS3.pitch, bearing: GIS3.bearing, duration: 1000 }, { essential: true }),
    }), 'top-right');

    // MapLibre 6 在投影变更时触发 projectiontransition，样式加载也可能改变投影。
    map.on('projectiontransition', (event) => {
        GIS3.currentProjection = event.newProjection || 'mercator';
        starHelper.updateVisibility();
    });
    map.on('style.load', () => {
        GIS3.currentProjection = map.getProjection()?.type || 'mercator';
        starHelper.updateVisibility();
    });
    map.on('remove', () => starHelper.destroy());

    // 吞掉已知无法消除的噪音警告
    const SILENCED = [
        'decode', 'vertical perspective', 'calculateFogMatrix',
        'not fully supported on vertical perspective', 'globe projection',
        'dimension mismatch', 'same source for hillshade',
    ];
    map.on('error', (e) => {
        const msg = e.error?.message || '';
        if (SILENCED.some(s => msg.includes(s))) return;
        console.error('[MapLibre]', e.error?.message || e.error);
    });
}

//============================================================
// 主题设置
//============================================================
/** 设置主题 */
function setTheme(theme, starHelper) {
    GIS3.currentTheme = theme;
    document.documentElement.dataset.theme = theme;
    starHelper.updateVisibility();
}

