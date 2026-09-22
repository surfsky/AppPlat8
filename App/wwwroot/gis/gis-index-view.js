/**
 * 地图视图相关（底图样式、投影、3D模式等）
 */
(function () {
    function create(ctx) {
        const map = ctx.map;
        const state = ctx.state;
        const terrainSourceId = ctx.terrainSourceId || 'mapbox-dem';
        const closeViewMenu = ctx.closeViewMenu || (() => {});
        let pending3DRequestId = 0;

        function normalizeStyleKey(value) {
            return String(value || '').trim().toLowerCase();
        }

        function getFallbackStyles() {
            return [
                { name: 'TiandituSatellite', title: '天地图遥感', path: 'tianditu://satellite', tileTemplate: 'https://t{s}.tianditu.gov.cn/img_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=img&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILECOL={x}&TILEROW={y}&TILEMATRIX={z}&tk={tk}', labelTemplate: 'https://t{s}.tianditu.gov.cn/cia_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=cia&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILECOL={x}&TILEROW={y}&TILEMATRIX={z}&tk={tk}', subdomains: ['0','1','2','3','4','5','6','7'], tileSize: 256, maxZoom: 18, attribution: '影像：国家基础地理信息中心 Tianditu', aliases: ['tianditusatellite','tianditu-satellite','tianditu-yingxiang','tianditu-img'] },
                { name: 'TiandituStreets',   title: '天地图街道', path: 'tianditu://streets',   tileTemplate: 'https://t{s}.tianditu.gov.cn/vec_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=vec&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILECOL={x}&TILEROW={y}&TILEMATRIX={z}&tk={tk}', labelTemplate: 'https://t{s}.tianditu.gov.cn/cva_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=cva&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILECOL={x}&TILEROW={y}&TILEMATRIX={z}&tk={tk}', subdomains: ['0','1','2','3','4','5','6','7'], tileSize: 256, maxZoom: 18, attribution: '矢量：国家基础地理信息中心 Tianditu', aliases: ['tianditustreets','tianditu-streets','tianditu-vector','tianditu-vec'] },
                { name: 'OpenStreetMap',    title: 'OpenStreetMap', path: 'osm://default', tileTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png', subdomains: [], tileSize: 256, maxZoom: 19, attribution: '© OpenStreetMap contributors', aliases: ['osm','openstreetmap'] },
                { name: 'SatelliteStreets', title: 'Mapbox 卫星街道', path: 'mapbox://styles/mapbox/satellite-streets-v12', aliases: ['satellitestreets', 'satellite-streets', 'satellite_streets'] },
                { name: 'Streets',          title: 'Mapbox 街道', path: 'mapbox://styles/mapbox/streets-v11', aliases: ['street', 'streets'] },
                { name: 'Satellite',        title: 'Mapbox 卫星', path: 'mapbox://styles/mapbox/satellite-v9', aliases: ['satellite', 'terrain', 'sat'] },
                { name: 'Dark',             title: 'Mapbox 暗色', path: 'mapbox://styles/mapbox/dark-v10', aliases: ['dark'] },
                { name: 'Light',            title: 'Mapbox 明亮', path: 'mapbox://styles/mapbox/light-v10', aliases: ['light'] },
                { name: 'Outdoors',         title: 'Mapbox 户外', path: 'mapbox://styles/mapbox/outdoors-v11', aliases: ['outdoors'] },
                { name: 'Navigation',       title: 'Mapbox 导航', path: 'mapbox://styles/mapbox/navigation-v1', aliases: ['navigation', 'night'] }
            ];
        }

        function resolveProjection(projection) {
            if (projection === null || projection === undefined || projection === '') {
                return 'mercator';
            }

            const raw = String(projection).trim();
            const key = raw.toLowerCase();
            const mapping = {
                '0': 'mercator',
                mercator: 'mercator',
                '1': 'globe',
                globe: 'globe',
                '2': 'equirectangular',
                equirectangular: 'equirectangular',
                '3': 'naturalEarth',
                naturalearth: 'naturalEarth',
                '4': 'winkelTripel',
                winkeltripel: 'winkelTripel'
            };

            return mapping[key] || 'mercator';
        }

        function resolveStyle(styleKey) {
            const key = normalizeStyleKey(styleKey);
            const styles = Array.isArray(state.mapStyles) && state.mapStyles.length > 0
                ? state.mapStyles
                : getFallbackStyles();

            let match = styles.find(item =>
                normalizeStyleKey(item.name) === key || normalizeStyleKey(item.path) === key
            );
            if (match) {
                return {
                    name: match.name,
                    title: match.title || match.Title || match.name,
                    path: match.path,
                    tileTemplate: match.tileTemplate || match.TileTemplate,
                    labelTemplate: match.labelTemplate || match.LabelTemplate,
                    subdomains: match.subdomains || match.Subdomains,
                    tileSize: match.tileSize || match.TileSize,
                    maxZoom: match.maxZoom || match.MaxZoom,
                    attribution: match.attribution || match.Attrib
                };
            }

            match = getFallbackStyles().find(item =>
                item.aliases.some(alias => normalizeStyleKey(alias) === key)
            );
            if (match) {
                return {
                    name: match.name,
                    title: match.title || match.name,
                    path: match.path,
                    tileTemplate: match.tileTemplate,
                    labelTemplate: match.labelTemplate,
                    subdomains: match.subdomains,
                    tileSize: match.tileSize,
                    maxZoom: match.maxZoom,
                    attribution: match.attribution
                };
            }

            if (styles.length > 0) {
                const s0 = styles[0];
                return {
                    name: s0.name,
                    title: s0.title || s0.Title || s0.name,
                    path: s0.path,
                    tileTemplate: s0.tileTemplate || s0.TileTemplate,
                    labelTemplate: s0.labelTemplate || s0.LabelTemplate,
                    subdomains: s0.subdomains || s0.Subdomains,
                    tileSize: s0.tileSize || s0.TileSize,
                    maxZoom: s0.maxZoom || s0.MaxZoom,
                    attribution: s0.attribution || s0.Attrib
                };
            }

            return null;
        }

        function fillTileTemplate(template, tk, defaultSubdomain) {
            if (!template) return '';
            let s = String(template);
            if (tk) s = s.replace(/\{tk\}/g, encodeURIComponent(String(tk)));
            // {s} 占位符：
            //   - subdomains 有值 → 保留 {s}，让 MapLibre 自动做子域轮询；
            //   - subdomains 无值 → 保底替换为 '0'（或传入的 defaultSubdomain），避免 URL 含字面量 {s}。
            if (s.indexOf('{s}') >= 0 && defaultSubdomain !== null) {
                const sub = (typeof defaultSubdomain === 'string' && defaultSubdomain.length > 0) ? defaultSubdomain : '0';
                s = s.replace(/\{s\}/g, sub);
            }
            return s;
        }

        function buildTileStyle(styleInfo) {
            if (!styleInfo || !styleInfo.tileTemplate) return null;
            const keys = window.__gisKeys || {};
            const tk = keys.tiandituKey || '';
            const subdomains = Array.isArray(styleInfo.subdomains) && styleInfo.subdomains.length > 0
                ? styleInfo.subdomains
                : null;
            // 关键：仅在 subdomains 为 null 时，才在 URL 中把 {s} 预替换为 0 保底；
            //     如果有 subdomains，则保留 {s} 让 MapLibre 自己做子域轮询。
            const template = fillTileTemplate(styleInfo.tileTemplate, tk, subdomains ? null : '0');
            const labelTpl = styleInfo.labelTemplate ? fillTileTemplate(styleInfo.labelTemplate, tk, subdomains ? null : '0') : '';
            const tileSize = Number(styleInfo.tileSize) || 256;
            const maxZoom = Number(styleInfo.maxZoom) || 18;

            const sources = {
                'raster-tiles': {
                    type: 'raster',
                    tiles: [template],
                    tileSize,
                    maxzoom: maxZoom,
                    attribution: styleInfo.attribution || ''
                }
            };
            if (subdomains) sources['raster-tiles'].subdomains = subdomains;

            const layers = [
                {
                    id: 'raster-tiles',
                    type: 'raster',
                    source: 'raster-tiles',
                    minzoom: 0,
                    maxzoom: 24
                }
            ];

            if (labelTpl) {
                sources['raster-labels'] = {
                    type: 'raster',
                    tiles: [labelTpl],
                    tileSize,
                    maxzoom: maxZoom
                };
                if (subdomains) sources['raster-labels'].subdomains = subdomains;
                layers.push({
                    id: 'raster-labels',
                    type: 'raster',
                    source: 'raster-labels',
                    minzoom: 0,
                    maxzoom: 24
                });
            }

            return {
                version: 8,
                sources,
                layers
            };
        }

        function isTileStyle(styleInfo) {
            return !!styleInfo?.tileTemplate;
        }

        function syncStyleButtons() {
            const current = normalizeStyleKey(state.currentStyle);
            document.querySelectorAll('[data-view-style]').forEach(input => {
                if (input.type === 'radio') {
                    input.checked = normalizeStyleKey(input.dataset.viewStyle) === current;
                }
            });
        }

        function syncProjectionButtons() {
            const current = resolveProjection(state.currentProjection);
            document.querySelectorAll('[data-view-projection]').forEach(input => {
                if (input.type === 'radio') {
                    input.checked = resolveProjection(input.dataset.viewProjection) === current;
                }
            });
        }

        function sync3DToggleButton() {
            const input = document.getElementById('btn-toggle-3d');
            if (input) input.checked = !!state.is3D;
        }

        function syncRotateToggleButton() {
            const input = document.getElementById('btn-toggle-rotate');
            if (input) input.checked = !!state.isAutoRotate;
        }

        function enableRotate(options = {}) {
            state.isAutoRotate = true;
            syncRotateToggleButton();
            if (window.maphelper && typeof window.maphelper.rotateView === 'function') {
                window.maphelper.rotateView(true, map, options);
            }
            if (options.closeMenu !== false) {
                closeViewMenu();
            }
        }

        function disableRotate(options = {}) {
            state.isAutoRotate = false;
            syncRotateToggleButton();
            if (window.maphelper && typeof window.maphelper.rotateView === 'function') {
                window.maphelper.rotateView(false, map, options);
            }
            if (options.closeMenu !== false) {
                closeViewMenu();
            }
        }

        function syncProjectionBackdrop(projectionName) {
            const isGlobe = projectionName === 'globe';
            document.body.classList.toggle('gis-projection-globe', isGlobe);
            const mapEl = document.getElementById('map');
            if (mapEl) mapEl.classList.toggle('gis-map-globe', isGlobe);
            try {
                const canvas = typeof map.getCanvas === 'function' ? map.getCanvas() : null;
                if (canvas) {
                    canvas.style.backgroundColor = isGlobe ? 'transparent' : '';
                }
            } catch {
            }
            if (!isGlobe) {
                if (!state.is3D && typeof map.setFog === 'function') {
                    try {
                        map.setFog(null);
                    } catch {
                    }
                }
                return;
            }
            if (typeof map.setFog === 'function') {
                try {
                    map.setFog({
                        color: 'rgb(186, 210, 235)',
                        'high-color': 'rgb(36, 92, 223)',
                        'horizon-blend': 0.08,
                        'space-color': 'rgb(2, 6, 23)',
                        'star-intensity': 0.35
                    });
                } catch {
                }
            }
            try {
                const style = map.getStyle();
                const bgLayerId = style?.layers?.find(l => l?.type === 'background')?.id;
                if (bgLayerId) {
                    map.setPaintProperty(bgLayerId, 'background-color', '#020617');
                    map.setPaintProperty(bgLayerId, 'background-opacity', 1);
                }
            } catch {
            }
        }

        function applyProjection(projection, options = {}) {
            const projectionName = resolveProjection(projection);
            state.currentProjection = projectionName;
            if (typeof map.setProjection === 'function') {
                map.setProjection(projectionName);
            }
            syncProjectionButtons();
            syncProjectionBackdrop(projectionName);
            if (options.closeMenu !== false) {
                closeViewMenu();
            }
        }

        function ensureTerrainEnabled() {
            if (!map.getSource(terrainSourceId)) {
                map.addSource(terrainSourceId, {
                    type: 'raster-dem',
                    url: 'mapbox://mapbox.mapbox-terrain-dem-v1',
                    tileSize: 512,
                    maxzoom: 15
                });
            }

            map.setTerrain({ source: terrainSourceId, exaggeration: 1.8 });
            map.setFog({
                color: 'rgb(186, 210, 235)',
                'high-color': 'rgb(36, 92, 223)',
                'horizon-blend': 0.08
            });
        }

        function isTerrainStyleReady() {
            try {
                const style = map.getStyle();
                return !!style
                    && map.isStyleLoaded()
                    && Array.isArray(style.layers)
                    && style.layers.length > 0;
            } catch {
                return false;
            }
        }

        function canEnable3D() {
            try {
                return !!map.getSource('composite');
            } catch {
                return false;
            }
        }

        async function waitForTerrainStyleReady(timeoutMs = 5000) {
            if (isTerrainStyleReady()) {
                return true;
            }

            return await new Promise(resolve => {
                let done = false;
                let timer = null;
                let poller = null;

                const cleanup = () => {
                    if (done) return;
                    done = true;
                    map.off('style.load', onReadyCheck);
                    map.off('idle', onReadyCheck);
                    map.off('sourcedata', onReadyCheck);
                    if (timer) clearTimeout(timer);
                    if (poller) clearInterval(poller);
                };
                const finish = ok => {
                    cleanup();
                    resolve(ok);
                };
                const onReadyCheck = () => {
                    if (isTerrainStyleReady()) {
                        finish(true);
                    }
                };

                map.on('style.load', onReadyCheck);
                map.on('idle', onReadyCheck);
                map.on('sourcedata', onReadyCheck);
                timer = setTimeout(() => finish(false), timeoutMs);
                poller = setInterval(onReadyCheck, 120);
                onReadyCheck();
            });
        }

        async function enable3D(options = {}) {
            const requestId = ++pending3DRequestId;
            const adjustCamera = options.adjustCamera !== false;
            state.is3D = true;
            sync3DToggleButton();
            const ready = await waitForTerrainStyleReady(options.timeoutMs || 5000);
            if (!ready) {
                if (requestId === pending3DRequestId) {
                    state.is3D = false;
                    sync3DToggleButton();
                    console.warn('3D 地图未能在限定时间内完成地形样式加载');
                }
                return;
            }
            if (requestId !== pending3DRequestId || !state.is3D) {
                return;
            }

            ensureTerrainEnabled();
            if (canEnable3D()) {
                const layers = map.getStyle().layers;
                const labelLayerId = layers.find(l => l.type === 'symbol' && l.layout && l.layout['text-field'])?.id;
                if (!map.getLayer('3d-buildings')) {
                    map.addLayer({
                        id: '3d-buildings',
                        source: 'composite',
                        'source-layer': 'building',
                        filter: ['==', 'extrude', 'true'],
                        type: 'fill-extrusion',
                        minzoom: 15,
                        paint: {
                            'fill-extrusion-color': '#94a3b8',
                            'fill-extrusion-height': ['get', 'height'],
                            'fill-extrusion-base': ['get', 'min_height'],
                            'fill-extrusion-opacity': 0.55
                        }
                    }, labelLayerId);
                }
            }
            if (adjustCamera) {
                map.easeTo({ pitch: 72, duration: 1000 });
            }
            if (options.closeMenu !== false) {
                closeViewMenu();
            }
        }

        function disable3D(options = {}) {
            pending3DRequestId++;
            const adjustCamera = options.adjustCamera !== false;
            map.setTerrain(null);
            if (map.getLayer('3d-buildings')) {
                map.removeLayer('3d-buildings');
            }
            state.is3D = false;
            sync3DToggleButton();
            if (adjustCamera) {
                map.easeTo({ pitch: 0, bearing: 0, duration: 600 });
            }
            if (options.closeMenu !== false) {
                closeViewMenu();
            }
        }

        function switchStyle(type, options = {}) {
            const style = resolveStyle(type);
            if (!style) return;

            state.preservedVisibleGeometryIds = new Set(
                state.geometries
                    .filter(g => state.geometryVisibleMap.get(g.id) !== false)
                    .map(g => String(g.id))
            );
            state.currentStyle = style.name;
            syncStyleButtons();

            if (isTileStyle(style)) {
                // OSM / 天地图：构造内联 MapLibre Style Spec 对象加载（天地图需 tiandituKey）
                const styleObj = buildTileStyle(style);
                if (styleObj) {
                    map.setStyle(styleObj);
                }
            } else {
                // Mapbox：直接加载 style path（需要 mapbox accessToken）
                map.setStyle(style.path);
            }
            if (options.closeMenu !== false) {
                closeViewMenu();
            }
        }

        function applyViewConfig(options = {}) {
            const hasStyle = options.style !== null && options.style !== undefined && options.style !== '';
            const has3D = options.enable3D !== null && options.enable3D !== undefined;
            const hasProjection = options.projection !== null && options.projection !== undefined && options.projection !== '';

            if (hasStyle) {
                switchStyle(options.style, { closeMenu: false });
            } else {
                syncStyleButtons();
            }

            if (hasProjection) {
                applyProjection(options.projection, { closeMenu: false });
            }

            if (has3D) {
                if (options.enable3D) {
                    state.is3D = true;
                    sync3DToggleButton();
                    enable3D({
                        closeMenu: false,
                        adjustCamera: options.adjustCamera
                    });
                } else {
                    const was3D = state.is3D || !!map.getLayer('3d-buildings');
                    state.is3D = false;
                    sync3DToggleButton();
                    if (was3D) {
                        disable3D({
                            closeMenu: false,
                            adjustCamera: options.adjustCamera
                        });
                    }
                }
            }

            if (options.closeMenu !== false) {
                closeViewMenu();
            }
        }

        return {
            enable3D,
            disable3D,
            enableRotate,
            disableRotate,
            switchStyle,
            applyProjection,
            applyViewConfig,
            syncStyleButtons,
            syncProjectionButtons,
            sync3DToggleButton,
            syncRotateToggleButton,
            buildTileStyle,
            isTileStyle,
            resolveStyle
        };
    }

    window.GisIndexView = { create };
})();
