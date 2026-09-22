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

        function switchSceneInternal(scene) {
            // 调用注入的 loadScenes/switchScene 上下文函数
            if (ctx && typeof ctx.loadScenes === 'function') {
                return (async () => {
                    if (!state.scenes || !state.scenes.length) await ctx.loadScenes();
                    return await (typeof window.switchScene === 'function'
                        ? window.switchScene(scene)
                        : (ctx.switchScene && ctx.switchScene(scene)));
                })();
            }
            return Promise.resolve(true);
        }

        function setPageLoading(visible, text) {
            const host = document.getElementById('gis-index-loading');
            if (!host) return;
            if (text) {
                const textEl = host.querySelector('.gis-index-loading-text');
                if (textEl) textEl.textContent = String(text);
            }
            host.classList.toggle('visible', !!visible);
        }

        function startHeaderDatetime() {
            window.GisIndexUI.startHeaderDatetime('header-datetime');
        }

        function positionHeaderDatetime() {
            const headerCenter = document.querySelector('.header-center');
            const dateTime = document.getElementById('header-datetime');
            if (!headerCenter || !dateTime) return;
            if (window.getComputedStyle(dateTime).display === 'none') return;
            dateTime.style.left = '';
            dateTime.style.right = '';
            dateTime.style.top = '';
            dateTime.style.transform = '';
        }

        function addMapControls() {
            map.addControl(new mapboxgl.NavigationControl(), 'top-right');
            map.addControl(new mapboxgl.GeolocateControl({
                positionOptions: { enableHighAccuracy: true },
                trackUserLocation: true,
                showUserHeading: true
            }), 'top-right');
            map.addControl(new mapboxgl.AttributionControl({ compact: true }), 'bottom-right');

            const centerCoordControl = mapApi.createCenterCoordControl();
            map.addControl(centerCoordControl, 'bottom-right');
            map.addControl(mapApi.createResetControl(ctx.resetView), 'top-right');
            map.addControl(mapApi.createFullscreenControl(), 'top-right');
            map.on('move', () => centerCoordControl.update());
        }

        function bindPageEvents() {
            window.GisIndexUI.ensureFloatingTooltips();
            document.getElementById('btn-search').addEventListener('click', ctx.searchAddress);
            document.getElementById('btn-scene-toggle').addEventListener('click', e => {
                e.stopPropagation();
                ctx.toggleSceneMenu();
            });
            document.getElementById('btn-toolbar-toggle').addEventListener('click', ctx.toggleToolbar);
            document.getElementById('btn-layer-toggle').addEventListener('click', ctx.toggleLayerPanel);
            document.getElementById('btn-stats-toggle').addEventListener('click', ctx.toggleStatsMode);
            document.getElementById('btn-layer-tab-resource').addEventListener('click', () => ctx.switchLayerTab('resource'));
            document.getElementById('btn-layer-tab-view').addEventListener('click', () => ctx.switchLayerTab('view'));
            document.getElementById('btn-layer-tab-weather').addEventListener('click', () => ctx.switchLayerTab('weather'));
            document.getElementById('btn-layer-tab-iot').addEventListener('click', () => ctx.switchLayerTab('iot'));
            document.getElementById('geo-detail-gis-panel').addEventListener('panel-close', ctx.closeGeometryDetailDrawer);
            document.getElementById('geo-detail-gis-panel').addEventListener('click', e => {
                const btn = e.target && e.target.closest ? e.target.closest('[data-panel-edit-geometry]') : null;
                if (!btn) return;
                if (typeof ctx.openGeometryEditDrawer === 'function') {
                    ctx.openGeometryEditDrawer();
                }
            });
            document.getElementById('geo-point-list-gis-panel').addEventListener('panel-close', ctx.closePointListPanel);
            document.getElementById('address-input').addEventListener('keydown', e => {
                if (e.key === 'Enter') ctx.searchAddress();
            });
            document.getElementById('btn-toggle-3d').addEventListener('change', e => {
                if (e.target.checked) viewApi.enable3D();
                else viewApi.disable3D();
            });
            document.getElementById('btn-toggle-rotate').addEventListener('change', e => {
                if (e.target.checked) viewApi.enableRotate();
                else viewApi.disableRotate();
            });

            document.addEventListener('click', e => {
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
            ctx.syncStatsModeUI();
            if (typeof window.GisIndexUI.initLayerPanelResize === 'function') {
                window.GisIndexUI.initLayerPanelResize();
            }
        }

        function bindMapLifecycle() {
            map.on('load', async () => {
                setPageLoading(true, '地图与面板加载中...');
                const mapHelperReady = await mapApi.ensureMapGeometryHelperReady();
                if (!mapHelperReady) {
                    EleManager.showError('地图图形组件加载失败');
                    setPageLoading(false);
                    return;
                }

                ctx.createGeometryLayerManager();
                ctx.bindGeometryMapInteractions();

                await ctx.loadMapStyles();
                await ctx.loadScenes();

                // -- 根据场景清单，默认选中并应用 IsDefault=true 的场景
                if (Array.isArray(state.scenes) && state.scenes.length > 0) {
                    let defScene = state.scenes.find(s => {
                        const flag = s.isDefault ?? s.IsDefault;
                        return flag === true || flag === 1 || flag === 'true' || flag === 'True';
                    });
                    if (!defScene) defScene = state.scenes[0];
                    const alreadyHasCurrent = state.currentSceneId != null && state.currentSceneId !== ''
                        && state.scenes.some(s => Number(s.id ?? s.Id) === Number(state.currentSceneId));
                    if (!alreadyHasCurrent && defScene) {
                        try {
                            await ctx.loadMenus();
                            await ctx.loadGeometries();
                            // 应用默认场景：场景切换会负责 样式/投影/3D/旋转/相机/图层 全量设置
                            const toSwitch = { id: defScene.id ?? defScene.Id, name: defScene.name ?? defScene.Name };
                            await switchSceneInternal(toSwitch);
                        } catch (e) {
                            console.error('应用默认场景失败', e);
                        }
                    }
                }

                if (state.menuNodeMap == null || state.menuNodeMap.size === 0) await ctx.loadMenus();
                if (state.geometries == null || state.geometries.length === 0) await ctx.loadGeometries();

                await panelApi.loadPanels();
                mapApi.applyChineseLabels();

                startHeaderDatetime();
                positionHeaderDatetime();
                bindPageEvents();
                syncInitialUi();
                window.dispatchEvent(new CustomEvent('gis:index-ready', { detail: window.__gisIndexContext }));
                setTimeout(() => setPageLoading(false), 120);
            });

            map.on('style.load', async () => {
                mapApi.applyChineseLabels();
                objectApi.buildMarkers(state.objects);
                if (state.menuNodeMap && state.menuNodeMap.size > 0) {
                    await ctx.loadGeometries();
                }

                const restoreGeometryLayers = () => {
                    const geometryLayerManager = ctx.getGeometryLayerManager();
                    if (!geometryLayerManager || !map.isStyleLoaded()) return;
                    geometryLayerManager.setDataFromRows(
                        typeof ctx.getGeometryRowsForDisplay === 'function'
                            ? ctx.getGeometryRowsForDisplay()
                            : (state.geometries || [])
                    );
                    geometryLayerManager.render();
                    ctx.applyGeometryVisibility();
                };

                restoreGeometryLayers();
                viewApi.applyProjection(state.currentProjection, { closeMenu: false });
                requestAnimationFrame(restoreGeometryLayers);
                map.once('idle', restoreGeometryLayers);
                if (state.is3D) viewApi.enable3D({ closeMenu: false, adjustCamera: false });
                if (state.isAutoRotate) viewApi.enableRotate({ closeMenu: false });
                else viewApi.disableRotate({ closeMenu: false });

                objectApi.setLayerVisible(true);
                ctx.renderMenuTree();
            });
        }

        function initialize() {
            setPageLoading(true, '地图初始化中...');
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
