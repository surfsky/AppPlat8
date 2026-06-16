/**
 * 地图引导相关
 */
(function () {
    function create(ctx) {
        const map = ctx.map;
        const state = ctx.state;
        const mapApi = ctx.mapApi;
        const viewApi = ctx.viewApi;
        const objectApi = ctx.objectApi;
        const panelApi = ctx.panelApi;

        function startHeaderDatetime() {
            window.GisIndexUI.startHeaderDatetime('header-datetime');
        }

        function positionHeaderDatetime() {
            const headerBar = document.querySelector('.header-bar');
            const headerTitle = document.querySelector('.header-title');
            const dateTime = document.getElementById('header-datetime');
            if (!headerBar || !headerTitle || !dateTime) return;
            if (window.getComputedStyle(dateTime).display === 'none') return;

            const barRect = headerBar.getBoundingClientRect();
            const titleRect = headerTitle.getBoundingClientRect();
            const gap = 18;
            const left = titleRect.right - barRect.left + gap;

            dateTime.style.left = `${Math.round(left)}px`;
            dateTime.style.right = 'auto';
        }

        function addMapControls() {
            map.addControl(new mapboxgl.NavigationControl(), 'top-right');
            map.addControl(new mapboxgl.GeolocateControl({
                positionOptions: { enableHighAccuracy: true },
                trackUserLocation: true,
                showUserHeading: true
            }), 'top-right');
            map.addControl(new mapboxgl.ScaleControl({ unit: 'metric' }), 'bottom-left');
            map.addControl(new mapboxgl.AttributionControl({ compact: true }), 'bottom-right');

            const centerCoordControl = mapApi.createCenterCoordControl();
            map.addControl(centerCoordControl, 'bottom-right');
            map.addControl(mapApi.createResetControl(ctx.resetView), 'top-right');
            map.addControl(new mapboxgl.FullscreenControl({ container: document.documentElement }), 'top-right');
            map.on('move', () => centerCoordControl.update());
        }

        function bindPageEvents() {
            document.getElementById('btn-search').addEventListener('click', ctx.searchAddress);
            document.getElementById('btn-scene-toggle').addEventListener('click', e => {
                e.stopPropagation();
                ctx.toggleSceneMenu();
            });
            document.getElementById('btn-view-toggle').addEventListener('click', e => {
                e.stopPropagation();
                ctx.toggleViewMenu();
            });
            document.getElementById('btn-toolbar-toggle').addEventListener('click', ctx.toggleToolbar);
            document.getElementById('btn-layer-toggle').addEventListener('click', ctx.toggleLayerPanel);
            document.getElementById('btn-stats-toggle').addEventListener('click', ctx.toggleStatsMode);
            document.getElementById('btn-layer-tab-resource').addEventListener('click', () => ctx.switchLayerTab('resource'));
            document.getElementById('btn-layer-tab-weather').addEventListener('click', () => ctx.switchLayerTab('weather'));
            document.getElementById('btn-layer-tab-iot').addEventListener('click', () => ctx.switchLayerTab('iot'));
            document.getElementById('geo-detail-gis-panel').addEventListener('panel-close', ctx.closeGeometryDetailDrawer);
            document.getElementById('geo-point-list-gis-panel').addEventListener('panel-close', ctx.closePointListPanel);
            document.getElementById('address-input').addEventListener('keydown', e => {
                if (e.key === 'Enter') ctx.searchAddress();
            });
            document.getElementById('btn-toggle-3d').addEventListener('change', e => {
                if (e.target.checked) viewApi.enable3D();
                else viewApi.disable3D();
                ctx.closeViewMenu();
            });

            document.addEventListener('click', e => {
                if (state.viewMenuOpen) {
                    const menu = document.getElementById('view-menu');
                    const trigger = document.getElementById('btn-view-toggle');
                    const target = e.target;
                    if (!(menu && menu.contains(target)) && !(trigger && trigger.contains(target))) {
                        ctx.closeViewMenu();
                    }
                }
                if (state.sceneMenuOpen) {
                    const menu = document.getElementById('scene-menu');
                    const trigger = document.getElementById('btn-scene-toggle');
                    const target = e.target;
                    if (!(menu && menu.contains(target)) && !(trigger && trigger.contains(target))) {
                        ctx.closeSceneMenu();
                    }
                }
            });

            window.addEventListener('resize', positionHeaderDatetime);
        }

        function syncInitialUi() {
            ctx.syncToolbarUI();
            ctx.syncLayerPanelUI();
            ctx.syncLayerTabsUI();
            ctx.syncViewMenuUI();
            ctx.syncStatsModeUI();
        }

        function bindMapLifecycle() {
            map.on('load', async () => {
                const mapHelperReady = await mapApi.ensureMapGeometryHelperReady();
                if (!mapHelperReady) {
                    EleManager.showError('地图图形组件加载失败');
                    return;
                }

                ctx.createGeometryLayerManager();
                ctx.bindGeometryMapInteractions();

                await ctx.loadMapStyles();
                await ctx.loadScenes();
                await ctx.loadMenus();
                await ctx.loadGeometries();
                await panelApi.loadPanels();
                mapApi.applyChineseLabels();

                startHeaderDatetime();
                positionHeaderDatetime();
                bindPageEvents();
                syncInitialUi();
                window.dispatchEvent(new CustomEvent('gis:index-ready', { detail: window.__gisIndexContext }));
            });

            map.on('style.load', async () => {
                mapApi.applyChineseLabels();
                objectApi.buildMarkers(state.objects);
                const geometryLayerManager = ctx.getGeometryLayerManager();
                if (geometryLayerManager) {
                    geometryLayerManager.setDataFromRows(state.geometries || []);
                    geometryLayerManager.render();
                    ctx.applyGeometryVisibility();
                }
                if (state.menuNodeMap && state.menuNodeMap.size > 0) {
                    await ctx.loadGeometries();
                }
                viewApi.applyProjection(state.currentProjection, { closeMenu: false });
                map.once('idle', () => ctx.applyGeometryVisibility());
                if (state.is3D) viewApi.enable3D({ closeMenu: false, adjustCamera: false });

                objectApi.setLayerVisible(true);
                ctx.renderMenuTree();
            });
        }

        function initialize() {
            addMapControls();
            bindMapLifecycle();
        }

        return {
            initialize,
            startHeaderDatetime,
            positionHeaderDatetime
        };
    }

    window.GisIndexBootstrap = { create };
})();
