(function () {
    function create(ctx) {
        const map = ctx.map;
        const state = ctx.state;
        const terrainSourceId = ctx.terrainSourceId || 'mapbox-dem';
        const center = ctx.center;
        const zoom = ctx.zoom;
        const closeViewMenu = ctx.closeViewMenu || (() => {});
        let addressListBound = false;
        let pendingAddresses = [];
        let searchMarker = null;

        function flyToWithMarker(lng, lat) {
            map.flyTo({ center: [lng, lat], zoom: 15, speed: 0.9 });
            if (searchMarker) {
                searchMarker.remove();
            }
            searchMarker = new mapboxgl.Marker({ color: '#ef4444' }).setLngLat([lng, lat]).addTo(map);
        }

        function applyChineseLabels() {
            const style = map.getStyle();
            if (!style || !Array.isArray(style.layers)) return;
            style.layers.forEach(layer => {
                if (layer.type !== 'symbol') return;
                const textField = map.getLayoutProperty(layer.id, 'text-field');
                if (!textField) return;
                if (typeof textField !== 'string') return;
                try {
                    map.setLayoutProperty(layer.id, 'text-field', [
                        'coalesce',
                        ['get', 'name_zh-Hans'],
                        ['get', 'name_zh'],
                        ['get', 'name'],
                        ''
                    ]);
                } catch {
                    // ignore incompatible text-field expression
                }
            });
        }

        function createCenterCoordControl() {
            let container = null;
            const setText = () => {
                if (!container) return;
                const mapCenter = map.getCenter();
                const mapZoom = map.getZoom().toFixed(0);
                container.textContent = `${mapZoom}: ${mapCenter.lng.toFixed(6)}， ${mapCenter.lat.toFixed(6)}`;
            };

            return {
                onAdd() {
                    container = document.createElement('div');
                    container.className = 'mapboxgl-ctrl map-center-ctrl';
                    setText();
                    return container;
                },
                onRemove() {
                    if (container && container.parentNode) {
                        container.parentNode.removeChild(container);
                    }
                    container = null;
                },
                update: setText
            };
        }

        function createResetControl(onReset) {
            let container = null;

            return {
                onAdd() {
                    container = document.createElement('div');
                    container.className = 'mapboxgl-ctrl mapboxgl-ctrl-group';

                    const button = document.createElement('button');
                    button.type = 'button';
                    button.className = 'mapboxgl-ctrl-icon gis-reset-btn';
                    button.title = '重置地图';
                    button.setAttribute('aria-label', '重置地图');
                    button.innerHTML = '<i class="fa-solid fa-rotate-right" aria-hidden="true"></i>';
                    button.addEventListener('click', onReset);

                    container.appendChild(button);
                    return container;
                },
                onRemove() {
                    if (container && container.parentNode) {
                        container.parentNode.removeChild(container);
                    }
                    container = null;
                }
            };
        }

        async function ensureMapGeometryHelperReady() {
            if (window.MapGeometryHelper && typeof window.MapGeometryHelper.createDisplayLayerManager === 'function') {
                return true;
            }

            const dynamicScriptKey = 'maphelper-runtime-loader';
            if (!document.getElementById(dynamicScriptKey)) {
                const script = document.createElement('script');
                script.id = dynamicScriptKey;
                script.src = `/gis/maphelper.js?v=${Date.now()}`;
                document.head.appendChild(script);
            }

            const started = Date.now();
            while (Date.now() - started < 6000) {
                if (window.MapGeometryHelper && typeof window.MapGeometryHelper.createDisplayLayerManager === 'function') {
                    return true;
                }
                await new Promise(resolve => setTimeout(resolve, 120));
            }

            return false;
        }

        function ensureTerrainEnabled() {
            if (!map.getSource(terrainSourceId)) {
                map.addSource(terrainSourceId, {
                    type: 'raster-dem',
                    url: 'mapbox://mapbox.mapbox-terrain-dem-v1',
                    tileSize: 512,
                    maxzoom: 14
                });
            }

            map.setTerrain({ source: terrainSourceId, exaggeration: 1.25 });
            map.setFog({
                color: 'rgb(186, 210, 235)',
                'high-color': 'rgb(36, 92, 223)',
                'horizon-blend': 0.08
            });
        }

        function enable3DBuildings() {
            ensureTerrainEnabled();
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
            map.easeTo({ pitch: 60, bearing: -18, duration: 600 });
            state.is3D = true;
            const button = document.getElementById('btn-toggle-3d');
            if (button) button.classList.add('active');
        }

        function disable3DBuildings() {
            map.setTerrain(null);
            if (map.getLayer('3d-buildings')) {
                map.removeLayer('3d-buildings');
            }
            map.easeTo({ pitch: 0, bearing: 0, duration: 600 });
            state.is3D = false;
            const button = document.getElementById('btn-toggle-3d');
            if (button) button.classList.remove('active');
        }

        function switchStyle(type) {
            state.preservedVisibleGeometryIds = new Set(
                state.geometries
                    .filter(g => state.geometryVisibleMap.get(g.id) !== false)
                    .map(g => String(g.id))
            );
            state.currentStyle = type;
            const styleMap = {
                street: 'mapbox://styles/mapbox/streets-v12',
                terrain: 'mapbox://styles/mapbox/satellite-v9',
                satellite: 'mapbox://styles/mapbox/satellite-streets-v12',
                dark: 'mapbox://styles/mapbox/dark-v11',
                night: 'mapbox://styles/mapbox/navigation-night-v1'
            };
            map.setStyle(styleMap[type] || styleMap.street);
            document.getElementById('btn-style-street')?.classList.toggle('active', type === 'street');
            document.getElementById('btn-style-terrain')?.classList.toggle('active', type === 'terrain');
            document.getElementById('btn-style-sat')?.classList.toggle('active', type === 'satellite');
            document.getElementById('btn-style-dark')?.classList.toggle('active', type === 'dark');
            document.getElementById('btn-style-night')?.classList.toggle('active', type === 'night');
            closeViewMenu();
        }

        function hideAddressSuggestList() {
            const list = document.getElementById('address-suggest-list');
            if (!list) return;
            list.classList.remove('visible');
            list.innerHTML = '';
            pendingAddresses = [];
        }

        function renderAddressSuggestList(items) {
            const list = document.getElementById('address-suggest-list');
            if (!list) return;
            list.innerHTML = '';
            pendingAddresses = Array.isArray(items) ? items : [];

            if (pendingAddresses.length === 0) {
                list.classList.remove('visible');
                return;
            }

            pendingAddresses.forEach((item, index) => {
                const btn = document.createElement('button');
                btn.type = 'button';
                btn.className = 'address-suggest-item';
                btn.dataset.index = String(index);

                const title = document.createElement('div');
                title.className = 'address-suggest-title';
                title.textContent = item?.name || item?.Name || '未命名地址';

                const desc = document.createElement('div');
                desc.className = 'address-suggest-desc';
                const district = item?.district || item?.District || '';
                const address = item?.address || item?.Address || '';
                desc.textContent = [district, address].filter(Boolean).join(' ');

                btn.appendChild(title);
                btn.appendChild(desc);
                btn.addEventListener('click', () => selectAddressItem(item));
                list.appendChild(btn);
            });

            list.classList.add('visible');
        }

        async function requestApi(endpoint) {
            const resp = await fetch(endpoint, {
                method: 'GET',
                cache: 'no-store',
                headers: {
                    'Cache-Control': 'no-cache',
                    Pragma: 'no-cache'
                }
            });
            const raw = await resp.text();
            let result = null;
            try {
                result = raw ? JSON.parse(raw) : null;
            } catch {
                throw new Error(`接口返回非JSON（HTTP ${resp.status}）`);
            }

            if (!resp.ok) {
                throw new Error(result?.message || result?.info || `HTTP ${resp.status}`);
            }
            return result;
        }

        async function locateAddressByName(name, fallbackLng, fallbackLat) {
            try {
                const endpoint = `/HttpApi/Gis/GetAddr?name=${encodeURIComponent(name || '')}`;
                const result = await requestApi(endpoint);
                if (result?.code === 0 && result?.data) {
                    const row = result.data;
                    const lng = Number(row.lng ?? row.Lng);
                    const lat = Number(row.lat ?? row.Lat);
                    if (Number.isFinite(lng) && Number.isFinite(lat)) {
                        flyToWithMarker(lng, lat);
                        return { ok: true, reason: '' };
                    }
                }
                if (result?.code !== 0) {
                    return { ok: false, reason: result?.message || result?.info || '地址定位失败' };
                }
            } catch {
                // ignore and fallback
            }

            if (Number.isFinite(fallbackLng) && Number.isFinite(fallbackLat)) {
                flyToWithMarker(fallbackLng, fallbackLat);
                return { ok: true, reason: '' };
            }
            return { ok: false, reason: '未找到该地址坐标' };
        }

        async function selectAddressItem(item) {
            const name = (item?.name || item?.Name || '').trim();
            const fallbackLng = Number(item?.lng ?? item?.Lng);
            const fallbackLat = Number(item?.lat ?? item?.Lat);
            hideAddressSuggestList();
            let locateResult = { ok: false, reason: '' };

            // 优先使用候选项自带坐标，避免点击后再次调用接口导致报错。
            if (Number.isFinite(fallbackLng) && Number.isFinite(fallbackLat)) {
                flyToWithMarker(fallbackLng, fallbackLat);
                locateResult = { ok: true, reason: '' };
            } else {
                locateResult = await locateAddressByName(name, fallbackLng, fallbackLat);
            }

            if (!locateResult.ok) {
                window.EleManager.showWarning(locateResult.reason || '未找到该地址坐标');
                return;
            }
            const input = document.getElementById('address-input');
            if (input && name) {
                input.value = name;
            }
        }

        function ensureAddressListDismiss() {
            if (addressListBound) return;
            addressListBound = true;
            document.addEventListener('click', (e) => {
                const list = document.getElementById('address-suggest-list');
                const input = document.getElementById('address-input');
                const button = document.getElementById('btn-search');
                const target = e.target;
                if (!list || !target) return;
                if (list.contains(target)) return;
                if (input && input.contains(target)) return;
                if (button && button.contains(target)) return;
                hideAddressSuggestList();
            });
        }

        async function searchAddress() {
            ensureAddressListDismiss();
            const input = document.getElementById('address-input');
            const keyword = (input?.value || '').trim();
            if (!keyword) return;

            const endpoint = `/HttpApi/Gis/GetAddrs?name=${encodeURIComponent(keyword)}`;
            try {
                const result = await requestApi(endpoint);
                if (result?.code !== 0) {
                    window.EleManager.showWarning(result?.message || result?.info || '地址搜索失败');
                    return;
                }

                const list = Array.isArray(result?.data) ? result.data : [];
                if (list.length === 0) {
                    hideAddressSuggestList();
                    window.EleManager.showWarning('未找到该地址');
                    return;
                }

                renderAddressSuggestList(list);
            } catch (error) {
                const msg = error?.message || '未知错误';
                window.EleManager.showError(`地址搜索失败：${msg}`);
            }
        }

        function resetView() {
            window.GisIndexUI.resetView(map, center, zoom);
        }

        return {
            applyChineseLabels,
            createCenterCoordControl,
            createResetControl,
            ensureMapGeometryHelperReady,
            enable3DBuildings,
            disable3DBuildings,
            switchStyle,
            searchAddress,
            resetView
        };
    }

    window.GisIndexMap = { create };
})();
