/**
 * 地图场景相关
 */
(function () {
    function create(ctx) {
        const state = ctx.state;
        const map = ctx.map;
        const viewApi = ctx.viewApi;
        const dataApi = ctx.dataApi;
        const panelApi = ctx.panelApi;
        const closeGeometryDetailPanel = ctx.closeGeometryDetailPanel || (() => {});
        const closePointListPanel = ctx.closePointListPanel || (() => {});

        function isTruthyFlag(val) {
            return val === true || val === 'true' || val === 1 || val === '1';
        }

        function parseCenter(centerStr) {
            const raw = String(centerStr || '').trim();
            if (!raw) return null;

            const parts = raw.split(/[,，\s]+/).filter(Boolean);
            if (parts.length < 2) return null;
            const lng = parseFloat(parts[0]);
            const lat = parseFloat(parts[1]);
            if (Number.isNaN(lng) || Number.isNaN(lat)) return null;
            return [lng, lat];
        }

        function parseNumberOrNull(val) {
            if (val === null || val === undefined || val === '') return null;
            const num = Number(val);
            return Number.isFinite(num) ? num : null;
        }

        function resolveScenePitch(enable3D, scenePitch) {
            const pitchNum = parseNumberOrNull(scenePitch);
            if (isTruthyFlag(enable3D)) {
                return pitchNum && pitchNum > 0 ? pitchNum : 72;
            }
            return pitchNum;
        }

        function isCloseNumber(a, b, tolerance = 0.01) {
            if (a === null || a === undefined || b === null || b === undefined) return true;
            return Math.abs(Number(a) - Number(b)) <= tolerance;
        }

        function isCloseCenter(target, tolerance = 0.0008) {
            if (!Array.isArray(target) || target.length < 2) return true;
            try {
                const center = map.getCenter();
                return Math.abs(Number(center.lng) - Number(target[0])) <= tolerance
                    && Math.abs(Number(center.lat) - Number(target[1])) <= tolerance;
            } catch {
                return false;
            }
        }

        function isSceneViewSettled(targetView) {
            const target = targetView || {};
            return isCloseCenter(target.center)
                && isCloseNumber(map.getZoom(), target.zoom, 0.05)
                && isCloseNumber(map.getPitch(), target.pitch, 0.6)
                && isCloseNumber(map.getBearing(), target.bearing, 0.8);
        }

        function waitForSceneViewSettled(targetView, timeoutMs = 4800) {
            if (!targetView || isSceneViewSettled(targetView)) {
                return Promise.resolve(true);
            }

            return new Promise(resolve => {
                let done = false;
                let timer = null;
                let poller = null;

                const cleanup = () => {
                    if (done) return;
                    done = true;
                    map.off('moveend', onCheck);
                    map.off('zoomend', onCheck);
                    map.off('pitchend', onCheck);
                    map.off('rotateend', onCheck);
                    map.off('idle', onCheck);
                    if (timer) clearTimeout(timer);
                    if (poller) clearInterval(poller);
                };

                const finish = ok => {
                    cleanup();
                    resolve(ok);
                };

                const onCheck = () => {
                    if (isSceneViewSettled(targetView)) {
                        finish(true);
                    }
                };

                map.on('moveend', onCheck);
                map.on('zoomend', onCheck);
                map.on('pitchend', onCheck);
                map.on('rotateend', onCheck);
                map.on('idle', onCheck);
                timer = setTimeout(() => finish(isSceneViewSettled(targetView)), timeoutMs);
                poller = setInterval(onCheck, 120);
                onCheck();
            });
        }

        function buildSceneView(detail) {
            const centerStr = detail?.mapCenter || detail?.MapCenter;
            const zoomVal = detail?.mapZoom ?? detail?.MapZoom;
            const enable3D = detail?.enable3D ?? detail?.Enable3D;
            const scenePitch = detail?.mapPitch ?? detail?.MapPitch;

            const center = parseCenter(centerStr);
            const zoom = parseNumberOrNull(zoomVal);
            const pitch = resolveScenePitch(enable3D, scenePitch);
            return { center, zoom, pitch, bearing: 0 };
        }

        function renderViewStyleMenu() {
            const host = document.getElementById('view-style-list');
            if (!host) return;

            host.innerHTML = '';
            state.mapStyles.forEach(style => {
                const label = document.createElement('label');
                label.className = 'view-menu-option view-menu-radio';

                const input = document.createElement('input');
                input.type = 'radio';
                input.name = 'view-style';
                input.value = style.name;
                input.dataset.viewStyle = style.name;
                input.checked = state.currentStyle === style.name;
                input.addEventListener('change', () => {
                    if (input.checked) viewApi.switchStyle(style.name);
                });

                const text = document.createElement('span');
                text.textContent = style.name;

                label.appendChild(input);
                label.appendChild(text);
                host.appendChild(label);
            });
        }

        function renderProjectionMenu() {
            const host = document.getElementById('view-projection-list');
            if (!host) return;

            const projectionList = [
                { name: '墨卡托投影', value: 'mercator' },
                { name: '球形投影', value: 'globe' }
            ];

            host.innerHTML = '';
            projectionList.forEach(item => {
                const label = document.createElement('label');
                label.className = 'view-menu-option view-menu-radio';

                const input = document.createElement('input');
                input.type = 'radio';
                input.name = 'view-projection';
                input.value = item.value;
                input.dataset.viewProjection = item.value;
                input.checked = String(state.currentProjection || 'mercator') === item.value;
                input.addEventListener('change', () => {
                    if (input.checked) viewApi.applyProjection(item.value);
                });

                const text = document.createElement('span');
                text.textContent = item.name;

                label.appendChild(input);
                label.appendChild(text);
                host.appendChild(label);
            });
        }

        async function loadMapStyles() {
            try {
                const res = await axios.get('/httpapi/gis/GetMapStyles');
                if (res.data.code !== 0) return;

                const list = Array.isArray(res.data.data) ? res.data.data : [];
                state.mapStyles = list
                    .map(item => ({
                        name: item.name || item.Name || '',
                        path: item.path || item.Path || ''
                    }))
                    .filter(item => item.name && item.path);

                if (!state.currentStyle && state.mapStyles.length > 0) {
                    state.currentStyle = state.mapStyles[0].name;
                }

                renderViewStyleMenu();
            } catch (err) {
                console.error('加载地图样式失败', err);
            } finally {
                renderProjectionMenu();
            }
        }

        async function loadScenes() {
            try {
                const res = await axios.get('/httpapi/gis/GetScenes');
                if (res.data.code === 0) {
                    state.scenes = res.data.data;
                    window.GisIndexUI.renderSceneMenu(state, switchScene);
                }
            } catch (err) {
                console.error('加载场景失败', err);
            }
        }

        async function switchScene(scene) {
            try {
                const res = await axios.get('/httpapi/gis/GetSceneDetail?id=' + scene.id);
                if (res.data.code === 0) {
                    const detail = res.data.data;
                    state.currentSceneId = detail.id;
                    state.currentSceneDetail = detail;
                    state.currentSceneView = buildSceneView(detail);
                    const styleName = detail.mapStyle || detail.MapStyle || '';
                    const enable3D = detail.enable3D ?? detail.Enable3D;
                    const autoRotate = detail.autoRotate ?? detail.AutoRotate;
                    const projection = detail.mapProjection ?? detail.MapProjection;
                    const scenePitch = detail.mapPitch ?? detail.MapPitch;

                    if (styleName || enable3D !== null && enable3D !== undefined || projection !== null && projection !== undefined) {
                        viewApi.applyViewConfig({
                            style: styleName,
                            projection,
                            enable3D,
                            closeMenu: false,
                            adjustCamera: false
                        });
                        renderViewStyleMenu();
                        renderProjectionMenu();
                    }

                    if (autoRotate !== null && autoRotate !== undefined) {
                        const enabled = isTruthyFlag(autoRotate);
                        if (enabled) viewApi.enableRotate({ closeMenu: false });
                        else viewApi.disableRotate({ closeMenu: false });
                    }

                    const centerStr = detail.mapCenter || detail.MapCenter;
                    const zoomVal = detail.mapZoom ?? detail.MapZoom;
                    const pitchVal = resolveScenePitch(enable3D, scenePitch);
                    let sceneViewPromise = Promise.resolve(true);

                    if (centerStr) {
                        const center = parseCenter(centerStr);
                        if (center) {
                            const targetView = {
                                center,
                                zoom: (zoomVal !== null && zoomVal !== undefined) ? zoomVal : map.getZoom(),
                                pitch: (pitchVal !== null && pitchVal !== undefined) ? pitchVal : map.getPitch(),
                                bearing: map.getBearing()
                            };
                            map.flyTo({
                                center,
                                zoom: targetView.zoom,
                                pitch: targetView.pitch,
                                duration: 2000
                            });
                            sceneViewPromise = waitForSceneViewSettled(targetView);
                        }
                    } else if (zoomVal !== null && zoomVal !== undefined) {
                        const targetView = {
                            center: [map.getCenter().lng, map.getCenter().lat],
                            zoom: zoomVal,
                            pitch: (pitchVal !== null && pitchVal !== undefined) ? pitchVal : map.getPitch(),
                            bearing: map.getBearing()
                        };
                        map.flyTo({
                            zoom: zoomVal,
                            pitch: targetView.pitch,
                            duration: 2000
                        });
                        sceneViewPromise = waitForSceneViewSettled(targetView);
                    } else if (pitchVal !== null && pitchVal !== undefined) {
                        const targetView = {
                            center: [map.getCenter().lng, map.getCenter().lat],
                            zoom: map.getZoom(),
                            pitch: pitchVal,
                            bearing: map.getBearing()
                        };
                        map.flyTo({
                            pitch: pitchVal,
                            duration: 2000
                        });
                        sceneViewPromise = waitForSceneViewSettled(targetView);
                    }

                    const menuIds = detail.menuIds || detail.MenuIds || [];
                    const layerNames = detail.layerNames || detail.LayerNames || [];

                    // 切场景时先清空旧场景的临时选择和面板状态，再叠加当前场景显式关联的图层。
                    state.pointListFilterMenuId = null;
                    state.pointListFilterIds = null;
                    state.activePointListMenuId = null;
                    state.activePointListMenuName = '';
                    state.selectedGeometryId = null;
                    state.preservedVisibleGeometryIds = new Set();
                    closeGeometryDetailPanel();
                    closePointListPanel();
                    dataApi.setBatchMenusChecked(menuIds, { includeDescendants: false });

                    await sceneViewPromise;

                    const overlayApi = window.__gisIndexOverlayApi;
                    if (overlayApi && typeof overlayApi.setActiveLayers === 'function') {
                        await overlayApi.setActiveLayers(layerNames);
                    }

                    await panelApi.loadPanels(detail.id);

                    window.GisIndexUI.closeSceneMenu(state);
                    window.GisIndexUI.syncSceneMenuUI(state);
                    window.GisIndexUI.renderSceneMenu(state, switchScene);
                }
            } catch (err) {
                console.error('切换场景失败', err);
            }
        }

        function resetCurrentSceneView() {
            const detail = state.currentSceneDetail;
            const view = detail ? buildSceneView(detail) : (state.currentSceneView || null);
            const fallback = state.defaultSceneView || null;

            if (detail) {
                const styleName = detail.mapStyle || detail.MapStyle || '';
                const enable3D = detail.enable3D ?? detail.Enable3D;
                const autoRotate = detail.autoRotate ?? detail.AutoRotate;
                const projection = detail.mapProjection ?? detail.MapProjection;
                viewApi.applyViewConfig({
                    style: styleName,
                    projection,
                    enable3D,
                    closeMenu: false,
                    adjustCamera: false
                });
                if (autoRotate !== null && autoRotate !== undefined) {
                    const enabled = isTruthyFlag(autoRotate);
                    if (enabled) viewApi.enableRotate({ closeMenu: false });
                    else viewApi.disableRotate({ closeMenu: false });
                }
            }

            const targetCenter = view?.center || fallback?.center || null;
            const targetZoom = (view?.zoom !== null && view?.zoom !== undefined) ? view.zoom : (fallback?.zoom ?? null);
            const targetPitch = (view?.pitch !== null && view?.pitch !== undefined)
                ? view.pitch
                : (fallback?.pitch ?? 0);
            const targetBearing = (view?.bearing !== null && view?.bearing !== undefined)
                ? view.bearing
                : (fallback?.bearing ?? 0);

            const easeOptions = {
                pitch: targetPitch,
                bearing: targetBearing,
                duration: 650
            };

            if (targetCenter) easeOptions.center = targetCenter;
            if (targetZoom !== null && targetZoom !== undefined) easeOptions.zoom = targetZoom;

            map.easeTo(easeOptions);
        }

        function toggleSceneMenu() {
            window.GisIndexUI.toggleSceneMenu(state);
        }

        function closeSceneMenu() {
            window.GisIndexUI.closeSceneMenu(state);
        }

        return {
            renderViewStyleMenu,
            renderProjectionMenu,
            loadMapStyles,
            loadScenes,
            switchScene,
            resetCurrentSceneView,
            toggleSceneMenu,
            closeSceneMenu
        };
    }

    window.GisIndexScene = { create };
})();
