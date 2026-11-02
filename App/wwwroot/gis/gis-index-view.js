/* =====================================================================
 * 地图视图相关（底图样式、投影、3D模式等）
 * ===================================================================== */
(function () {
    function create(ctx) {
        const map = ctx.map;
        const state = ctx.state;
        const terrainHelper = ctx.terrainHelper;
        const closeViewMenu = ctx.closeViewMenu || (() => {});
        let terrainButton = null;
        let terrainStyleReady = !!map.isStyleLoaded();

        map.on('style.load', () => {
            terrainStyleReady = true;
            syncTerrainStyle();
        });

        function normalizeStyleKey(v) { return String(v || '').trim().toLowerCase(); }
        function resolveProjection(p) { return window.MapFactory.normalizeProjection(p); }
        function resolveStyle(key) { return window.MapFactory.resolveStyle(key, state.mapStyles); }
        function buildTileStyle(info) { return window.MapFactory.buildTileStyle(info, { projection: state.currentProjection }); }
        function isTileStyle(info) { return !!info?.tileTemplate; }

        function syncStyleButtons() {
            const cur = normalizeStyleKey(state.currentStyle);
            document.querySelectorAll('[data-view-style]').forEach(i => { if (i.type === 'radio') i.checked = normalizeStyleKey(i.dataset.viewStyle) === cur; });
        }
        function syncProjectionButtons() {
            const cur = resolveProjection(state.currentProjection);
            document.querySelectorAll('[data-view-projection]').forEach(i => { if (i.type === 'radio') i.checked = resolveProjection(i.dataset.viewProjection) === cur; });
        }
        function sync3DToggleButton() {
            const el = document.getElementById('btn-toggle-3d');
            if (el) el.checked = !!state.is3D;
            if (terrainButton) {
                terrainButton.title = state.is3D ? '关闭三维地形' : '开启三维地形';
                terrainButton.setAttribute('aria-label', terrainButton.title);
                terrainButton.setAttribute('aria-pressed', String(!!state.is3D));
                terrainButton.classList.toggle('maplibregl-ctrl-terrain-enabled', !!state.is3D);
                terrainButton.classList.toggle('maplibregl-ctrl-terrain', !state.is3D);
            }
        }
        function syncRotateToggleButton() { const el = document.getElementById('btn-toggle-rotate'); if (el) el.checked = !!state.isAutoRotate; }

        function enableRotate(opts = {}) {
            state.isAutoRotate = true; syncRotateToggleButton();
            if (window.maphelper?.rotateView) window.maphelper.rotateView(true, map, opts);
            if (opts.closeMenu !== false) closeViewMenu();
        }
        function disableRotate(opts = {}) {
            state.isAutoRotate = false; syncRotateToggleButton();
            if (window.maphelper?.rotateView) window.maphelper.rotateView(false, map, opts);
            if (opts.closeMenu !== false) closeViewMenu();
        }

        function syncProjectionBackdrop(name) {
            const isGlobe = name === 'globe';
            document.body.classList.toggle('gis-projection-globe', isGlobe);
            document.getElementById('map')?.classList.toggle('gis-map-globe', isGlobe);
            try { const canvas = map.getCanvas?.(); if (canvas) canvas.style.backgroundColor = isGlobe ? 'transparent' : ''; } catch {}
        }

        function applyProjection(projection, opts = {}) {
            const name = resolveProjection(projection);
            let cur = '';
            try { cur = resolveProjection(map.getProjection?.()); } catch {}
            if (cur === name && state.currentProjection === name) {
                syncProjectionButtons();
                if (opts.closeMenu !== false) closeViewMenu();
                return;
            }
            state.currentProjection = name;
            try { map.setProjection({ type: name }); } catch {}
            syncProjectionButtons();
            syncProjectionBackdrop(name);
            if (opts.closeMenu !== false) closeViewMenu();
        }

        // -----------------------------------------------------------------
        // 3D 地形
        // -----------------------------------------------------------------

        function syncTerrainStyle() {
            if (!terrainStyleReady) return;
            try {
                terrainHelper.setTerrain(state.is3D ? 1.2 : 0);
            } catch (err) {
                console.error('[GIS] 地形切换失败:', err);
                state.is3D = false;
                terrainHelper.state.currentExaggeration = 0;
            }
            sync3DToggleButton();
        }

        function set3D(enabled, opts = {}) {
            state.is3D = enabled;
            terrainHelper.state.currentExaggeration = enabled ? 1.2 : 0;
            syncTerrainStyle();
            sync3DToggleButton();
            if (opts.adjustCamera !== false && state.is3D === enabled) {
                map.easeTo(enabled ? { pitch: 72, duration: 800 } : { pitch: 0, bearing: 0, duration: 500 });
            }
            if (opts.closeMenu !== false) closeViewMenu();
        }

        function enable3D(opts = {}) { set3D(true, opts); }
        function disable3D(opts = {}) { set3D(false, opts); }

        function createTerrainToggleControl() {
            return {
                onAdd() {
                    const container = document.createElement('div');
                    container.className = 'maplibregl-ctrl maplibregl-ctrl-group';
                    terrainButton = document.createElement('button');
                    terrainButton.type = 'button';
                    terrainButton.innerHTML = '<span class="maplibregl-ctrl-icon" aria-hidden="true"></span>';
                    terrainButton.addEventListener('click', () => set3D(!state.is3D));
                    container.appendChild(terrainButton);
                    sync3DToggleButton();
                    return container;
                },
                onRemove() {
                    terrainButton?.parentElement?.remove();
                    terrainButton = null;
                },
            };
        }

        function switchStyle(type, opts = {}) {
            const style = resolveStyle(type);
            if (!style) return;

            const curName = normalizeStyleKey(state.currentStyle);
            const newName = normalizeStyleKey(style.name);
            if (curName && curName === newName && map.isStyleLoaded()) {
                syncStyleButtons();
                if (opts.closeMenu !== false) closeViewMenu();
                return;
            }

            state.preservedVisibleGeometryIds = new Set(
                (state.geometries || []).filter(g => state.geometryVisibleMap.get(g.id) !== false).map(g => String(g.id))
            );
            state.currentStyle = style.name; syncStyleButtons();

            const styleObj = isTileStyle(style) ? buildTileStyle(style) : null;
            if (styleObj) {
                if (state.is3D) terrainHelper.addTerrainToStyle(styleObj);
                terrainStyleReady = false;
                map.setStyle(styleObj, { diff: false });
            }

            (window.EleManager?.toast)?.(`已切换至${style.title || style.name}`, 'info');
            if (opts.closeMenu !== false) closeViewMenu();
        }

        function applyViewConfig(opts = {}) {
            if (opts.style) switchStyle(opts.style, { closeMenu: false });
            else syncStyleButtons();

            if (opts.projection !== undefined && opts.projection !== null && opts.projection !== '') applyProjection(opts.projection, { closeMenu: false });

            if (opts.enable3D !== undefined && opts.enable3D !== null) {
                const options = { closeMenu: false, adjustCamera: opts.adjustCamera };
                if (opts.enable3D) enable3D(options);
                else disable3D(options);
            }
            if (opts.closeMenu !== false) closeViewMenu();
        }

        return {
            enable3D, disable3D, syncTerrainStyle, createTerrainToggleControl, enableRotate, disableRotate,
            switchStyle, applyProjection, applyViewConfig,
            syncStyleButtons, syncProjectionButtons,
            sync3DToggleButton, syncRotateToggleButton,
            buildTileStyle, isTileStyle, resolveStyle
        };
    }

    window.GisIndexView = { create };
})();