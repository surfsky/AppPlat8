/**
 * 地图视图相关（底图样式、投影、3D模式等）
 */
(function () {
    function create(ctx) {
        const map = ctx.map;
        const state = ctx.state;
        const terrainSourceId = ctx.terrainSourceId || 'terrain-dem';
        const hillshadeSourceId = 'terrain-hillshade';
        const hillshadeLayerId = 'hillshade';
        const closeViewMenu = ctx.closeViewMenu || (() => {});
        let pending3DRequestId = 0;

        function normalizeStyleKey(value) {
            return String(value || '').trim().toLowerCase();
        }

        function resolveProjection(projection) {
            return window.MapFactory.normalizeProjection(projection);
        }

        function resolveStyle(styleKey) {
            return window.MapFactory.resolveStyle(styleKey, state.mapStyles);
        }

        function buildTileStyle(styleInfo) {
            return window.MapFactory.buildTileStyle(styleInfo, { projection: state.currentProjection });
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
                if (!state.is3D) applySky(false);
                return;
            }
            applySky(true);
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

        let _skyOn = null;

        function applyProjection(projection, options = {}) {
            const projectionName = resolveProjection(projection);
            // 幂等保护：已经是目标投影就跳过，避免 MapLibre 内部 setStyle 通道再次触发 style.load
            let current = '';
            try { current = resolveProjection(map.getProjection?.()); } catch {}
            if (current === projectionName && state.currentProjection === projectionName) {
                syncProjectionButtons();
                if (options.closeMenu !== false) closeViewMenu();
                return;
            }
            state.currentProjection = projectionName;
            try { map.setProjection({ type: projectionName }); } catch {}
            syncProjectionButtons();
            syncProjectionBackdrop(projectionName);
            if (options.closeMenu !== false) {
                closeViewMenu();
            }
        }

        function applySky(on) {
            // 幂等保护：已经设为目标状态就跳过 setSky，避免 MapLibre 内部走 setStyle 通道
            if (_skyOn === !!on) return;
            _skyOn = !!on;
            try {
                map.setSky(on ? {
                    'sky-color': '#4f8bdc',
                    'horizon-color': 'rgb(186, 210, 235)',
                    'fog-color': 'rgb(186, 210, 235)',
                    'sky-horizon-blend': 0.5,
                    'horizon-fog-blend': 0.5,
                    'fog-ground-blend': 0.3,
                    'atmosphere-blend': ['interpolate', ['linear'], ['zoom'], 0, 1, 5, 1, 7, 0]
                } : null);
            } catch {
                _skyOn = null;
            }
        }

        // DEM 瓦片自托管于 /tiles/dem（Data/download_dem_tiles.py 生成）；缺失时只调整视角，不启用地形
        async function ensureTerrainEnabled() {
            const _log = (...a) => console.log('[3D] ensureTerrainEnabled', ...a);
            try {
                if (!(await window.MapFactory.hasDem())) {
                    console.warn('[3D] 未找到本地 DEM 瓦片（/tiles/dem），3D 地形未启用');
                    return false;
                }
                _log('DEM 存在，开始配置 source/layer/terrain/sky');
                if (!map.getSource(terrainSourceId)) {
                    map.addSource(terrainSourceId, window.MapFactory.getTerrainSource());
                    _log('added source', terrainSourceId);
                }
                if (!map.getSource(hillshadeSourceId)) {
                    map.addSource(hillshadeSourceId, window.MapFactory.getTerrainSource());
                    _log('added source', hillshadeSourceId);
                }
                if (!map.getLayer(hillshadeLayerId)) {
                    map.addLayer({
                        id: hillshadeLayerId,
                        type: 'hillshade',
                        source: hillshadeSourceId,
                        layout: { visibility: 'visible' },
                        paint: {
                            'hillshade-shadow-color': '#3a3a3a',
                            'hillshade-highlight-color': '#ffffff',
                            'hillshade-accent-color': '#666666'
                        }
                    });
                    _log('added hillshade layer');
                }

                // 关键：开启 terrain 后 raster 图层需要 raster-elevation:0 贴地，否则被地形 z-buffer 裁掉
                const _elevateRasters = () => {
                    const style = map.getStyle();
                    if (!style?.layers) return;
                    style.layers.forEach(layer => {
                        if (layer.type === 'raster') {
                            try {
                                map.setPaintProperty(layer.id, 'raster-elevation', 0);
                            } catch {}
                        }
                    });
                };
                _elevateRasters();

                const curTerrain = map.getStyle?.()?.terrain;
                const needTerrain = !curTerrain || curTerrain.source !== terrainSourceId;
                if (needTerrain) {
                    _log('setTerrain({source:', terrainSourceId, ', exaggeration:1.8})');
                    map.setTerrain({ source: terrainSourceId, exaggeration: 1.8 });
                } else {
                    _log('terrain 已配置，跳过 setTerrain');
                }
                _log('setSky(true)');
                applySky(true);
                _log('ensureTerrainEnabled OK');
                return true;
            } catch (err) {
                console.error('[3D] 启用地形失败', err);
                state.is3D = false;
                sync3DToggleButton();
                return false;
            }
        }

        // 将地形相关 source/terrain/sky 注入到任意 style 对象中（setStyle 前调用，避免切换时丢失）
        function injectTerrainIntoStyle(styleObj) {
            if (!styleObj || typeof styleObj !== 'object') return styleObj;
            try {
                const terrainSource = window.MapFactory.getTerrainSource();
                styleObj.sources = styleObj.sources || {};
                if (!styleObj.sources[terrainSourceId]) styleObj.sources[terrainSourceId] = terrainSource;
                if (!styleObj.sources[hillshadeSourceId]) styleObj.sources[hillshadeSourceId] = terrainSource;
                styleObj.terrain = { source: terrainSourceId, exaggeration: 1.8 };
                if (!('sky' in styleObj)) styleObj.sky = {};
                if (!Array.isArray(styleObj.layers)) styleObj.layers = [];
                // 关键：terrain 模式下所有 raster 图层需要 raster-elevation:0 贴地
                styleObj.layers.forEach(layer => {
                    if (layer && layer.type === 'raster') {
                        layer.paint = layer.paint || {};
                        if (layer.paint['raster-elevation'] === undefined) {
                            layer.paint['raster-elevation'] = 0;
                        }
                    }
                });
                const hasHillshade = styleObj.layers.some(l => l && l.id === hillshadeLayerId);
                if (!hasHillshade) {
                    const insertIdx = styleObj.layers.findIndex(l => l && l.id === 'raster-tiles');
                    const hillshadeLayer = {
                        id: hillshadeLayerId,
                        type: 'hillshade',
                        source: hillshadeSourceId,
                        layout: { visibility: 'visible' },
                        paint: {
                            'hillshade-shadow-color': '#3a3a3a',
                            'hillshade-highlight-color': '#ffffff',
                            'hillshade-accent-color': '#666666'
                        }
                    };
                    if (insertIdx >= 0) styleObj.layers.splice(insertIdx + 1, 0, hillshadeLayer);
                    else styleObj.layers.push(hillshadeLayer);
                }
            } catch (err) {
                console.error('[3D] 注入地形到样式失败', err);
            }
            return styleObj;
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

        // 建筑矢量源（OpenMapTiles 结构）未配置时返回 false
        function ensureBuildingSource() {
            if (map.getSource('buildings')) return true;
            const source = window.MapFactory.getBuildingSource();
            if (!source) return false;
            map.addSource('buildings', source);
            return true;
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

            await ensureTerrainEnabled();
            if (requestId !== pending3DRequestId || !state.is3D) {
                return;
            }
            if (ensureBuildingSource() && !map.getLayer('3d-buildings')) {
                map.addLayer({
                    id: '3d-buildings',
                    source: 'buildings',
                    'source-layer': 'building',
                    type: 'fill-extrusion',
                    minzoom: 15,
                    paint: {
                        'fill-extrusion-color': '#94a3b8',
                        'fill-extrusion-height': ['coalesce', ['get', 'render_height'], 8],
                        'fill-extrusion-base': ['coalesce', ['get', 'render_min_height'], 0],
                        'fill-extrusion-opacity': 0.55
                    }
                });
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
            try { map.setTerrain(null); } catch {}
            if (state.currentProjection !== 'globe') applySky(false);
            try { if (map.getLayer(hillshadeLayerId)) map.removeLayer(hillshadeLayerId); } catch {}
            try { if (map.getLayer('3d-buildings')) map.removeLayer('3d-buildings'); } catch {}
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

            // 天地图 / OSM：构造内联 MapLibre Style Spec 对象加载（天地图需 tiandituKey）
            const styleObj = isTileStyle(style) ? buildTileStyle(style) : null;
            if (styleObj) {
                // 3D 模式下将地形 source/terrain/sky/hillshade 直接注入新样式，避免 setStyle 后异步间隙丢失
                if (state.is3D) injectTerrainIntoStyle(styleObj);
                map.setStyle(styleObj);
            }
            const manager = window.top?.EleManager || window.EleManager;
            if (manager && typeof manager.toast === 'function') {
                manager.toast(`已切换至${style.title || style.name}`, 'info');
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