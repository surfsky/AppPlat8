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
                        const enabled = autoRotate === true || autoRotate === 'true' || autoRotate === 1 || autoRotate === '1';
                        if (enabled) viewApi.enableRotate({ closeMenu: false });
                        else viewApi.disableRotate({ closeMenu: false });
                    }

                    const centerStr = detail.mapCenter || detail.MapCenter;
                    const zoomVal = detail.mapZoom ?? detail.MapZoom;
                    const pitchVal = (enable3D === true || enable3D === 'true')
                        ? (scenePitch !== null && scenePitch !== undefined ? scenePitch : 72)
                        : scenePitch;

                    if (centerStr) {
                        const parts = centerStr.split(/[,，\s]+/).filter(Boolean);
                        if (parts.length >= 2) {
                            const lng = parseFloat(parts[0]);
                            const lat = parseFloat(parts[1]);
                            if (!isNaN(lng) && !isNaN(lat)) {
                                map.flyTo({
                                    center: [lng, lat],
                                    zoom: (zoomVal !== null && zoomVal !== undefined) ? zoomVal : map.getZoom(),
                                    pitch: (pitchVal !== null && pitchVal !== undefined) ? pitchVal : map.getPitch(),
                                    duration: 2000
                                });
                            }
                        }
                    } else if (zoomVal !== null && zoomVal !== undefined) {
                        map.flyTo({
                            zoom: zoomVal,
                            pitch: (pitchVal !== null && pitchVal !== undefined) ? pitchVal : map.getPitch(),
                            duration: 2000
                        });
                    } else if (pitchVal !== null && pitchVal !== undefined) {
                        map.flyTo({
                            pitch: pitchVal,
                            duration: 2000
                        });
                    }

                    const menuIds = detail.menuIds || detail.MenuIds || [];
                    dataApi.setBatchMenusChecked(menuIds);

                    await panelApi.loadPanels(detail.id);

                    window.GisIndexUI.closeSceneMenu(state);
                    window.GisIndexUI.syncSceneMenuUI(state);
                    window.GisIndexUI.renderSceneMenu(state, switchScene);
                }
            } catch (err) {
                console.error('切换场景失败', err);
            }
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
            toggleSceneMenu,
            closeSceneMenu
        };
    }

    window.GisIndexScene = { create };
})();
