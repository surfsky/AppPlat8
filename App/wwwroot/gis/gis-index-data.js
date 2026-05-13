(function () {
    function create(ctx) {
        const state = ctx.state;
        const map = ctx.map;
        const getGeometryLayerManager = ctx.getGeometryLayerManager;
        const onGeometryMarkerClick = ctx.onGeometryMarkerClick || (() => {});
        const onMenuBadgeClick = ctx.onMenuBadgeClick || (() => {});

        function buildMenuNodes() {
            const nodes = state.menus.map(item => ({ ...item, children: [] }));
            const byId = new Map(nodes.map(n => [n.id, n]));
            const roots = [];

            nodes.forEach(node => {
                if (node.parentId && byId.has(node.parentId)) {
                    byId.get(node.parentId).children.push(node);
                } else {
                    roots.push(node);
                }
            });

            const sortFn = (a, b) => (a.sortId || 0) - (b.sortId || 0) || a.id - b.id;
            const sortTree = list => {
                list.sort(sortFn);
                list.forEach(node => sortTree(node.children || []));
            };
            sortTree(roots);
            state.menuRoots = roots;
            state.menuNodeMap = byId;
        }

        function getMenuGeometryIds(node, cache = new Map()) {
            if (!node) return [];
            if (cache.has(node.id)) return cache.get(node.id);

            const own = state.geometryByMenuId.get(node.id) || [];
            const childIds = (node.children || []).flatMap(child => getMenuGeometryIds(child, cache));
            const result = [...own, ...childIds];
            cache.set(node.id, result);
            return result;
        }

        function getNodeGeometryItems(node, cache = new Map()) {
            if (!node) return [];
            const ids = getMenuGeometryIds(node, cache);
            if (!ids.length) return [];

            const byId = new Map((state.geometries || []).map(item => [item.id, item]));
            return ids
                .map(id => byId.get(id))
                .filter(item => !!item)
                .map(item => {
                    const center = getGeometryCenter(item);
                    const geometryType = getGeometryKind(item);
                    return {
                        id: item.id,
                        menuId: item.menuId,
                        name: item.name || '',
                        alias: item.alias || '',
                        addr: item.addr || '',
                        gps: item.gps || '',
                        icon: getGeometryIcon(item),
                        geometryType,
                        lng: center ? center.lng : null,
                        lat: center ? center.lat : null,
                        hasCoord: !!center
                    };
                });
        }

        function setMenuChecked(node, checked, cache = new Map()) {
            const ids = getMenuGeometryIds(node, cache);
            ids.forEach(id => state.geometryVisibleMap.set(id, !!checked));
            applyGeometryVisibility();
        }

        function renderMenuTree() {
            const container = document.getElementById('menu-tree');
            if (!container) return;
            container.innerHTML = '';

            if (!state.menuRoots || state.menuRoots.length === 0) {
                const msg = state.menuLoadErrorMessage || '加载中...';
                container.innerHTML = `<div class="menu-empty">${msg}</div>`;
                return;
            }

            const geometryCache = new Map();
            const renderNode = (node, depth) => {
                const row = document.createElement('div');
                row.className = 'menu-node';
                row.style.marginLeft = `${depth * 14}px`;

                const ids = getMenuGeometryIds(node, geometryCache);
                const visibleCount = ids.filter(id => state.geometryVisibleMap.get(id) !== false).length;

                const checkbox = document.createElement('input');
                checkbox.type = 'checkbox';
                checkbox.className = 'menu-node-check';
                checkbox.disabled = ids.length === 0;
                checkbox.checked = ids.length > 0 && visibleCount === ids.length;
                checkbox.indeterminate = visibleCount > 0 && visibleCount < ids.length;
                checkbox.addEventListener('change', () => setMenuChecked(node, !!checkbox.checked, geometryCache));

                const labelBtn = document.createElement('button');
                labelBtn.type = 'button';
                labelBtn.className = 'menu-node-label';
                labelBtn.textContent = node.name || `菜单${node.id}`;
                labelBtn.addEventListener('click', () => {
                    if (checkbox.disabled) return;
                    checkbox.checked = !checkbox.checked;
                    checkbox.dispatchEvent(new Event('change'));
                });

                row.appendChild(checkbox);
                row.appendChild(labelBtn);

                const count = ids.length;
                const badge = document.createElement('button');
                badge.type = 'button';
                badge.className = `menu-node-count ${count > 0 ? '' : 'empty'}`;
                badge.textContent = `${count}`;
                const nodeLabel = node.name || `菜单${node.id}`;
                badge.title = count > 0 ? `查看${nodeLabel}点位清单` : '无点位';
                badge.disabled = count === 0;
                badge.addEventListener('click', (evt) => {
                    evt.stopPropagation();
                    if (count === 0) return;
                    const items = getNodeGeometryItems(node, geometryCache);
                    onMenuBadgeClick(node, items);
                });
                row.appendChild(badge);

                container.appendChild(row);
                (node.children || []).forEach(child => renderNode(child, depth + 1));
            };

            state.menuRoots.forEach(node => renderNode(node, 0));
        }

        async function loadMenus() {
            state.menuLoadErrorMessage = '';
            try {
                const resp = await fetch('?handler=MenuData');
                if (!resp.ok) {
                    state.menuRoots = [];
                    state.menuNodeMap = new Map();
                    state.menuLoadErrorMessage = `目录加载失败(${resp.status})`;
                    renderMenuTree();
                    return;
                }
                const res = await resp.json();
                const code = res?.code ?? res?.Code ?? 0;
                const rawData = res?.data ?? res?.Data;
                if (code !== 0 || !Array.isArray(rawData)) {
                    state.menuRoots = [];
                    state.menuNodeMap = new Map();
                    state.menuLoadErrorMessage = '目录数据格式不正确';
                    renderMenuTree();
                    return;
                }

                const flatMenus = [];
                const walk = (nodes) => {
                    (nodes || []).forEach(n => {
                        const idRaw = n.id ?? n.Id;
                        const parentIdRaw = n.parentId ?? n.ParentId;
                        const sortIdRaw = n.sortId ?? n.SortId;
                        const id = Number(idRaw);
                        const parentId = parentIdRaw === null || parentIdRaw === undefined || parentIdRaw === ''
                            ? null
                            : Number(parentIdRaw);

                        if (Number.isNaN(id)) return;

                        flatMenus.push({
                            id,
                            parentId,
                            name: n.name ?? n.Name,
                            sortId: Number(sortIdRaw || 0),
                            icon: n.icon ?? n.Icon,
                            isDefaultShow: !!(n.isDefaultShow ?? n.IsDefaultShow)
                        });
                        walk(n.children || n.Children || []);
                    });
                };
                walk(rawData);

                state.menus = flatMenus;
                buildMenuNodes();
                if (flatMenus.length === 0) {
                    state.menuLoadErrorMessage = '加载中...';
                }
                renderMenuTree();
            } catch {
                state.menuRoots = [];
                state.menuNodeMap = new Map();
                state.menuLoadErrorMessage = '目录加载失败';
                renderMenuTree();
            }
        }

        function isGeometryDefaultVisible(menuId) {
            if (!menuId || !state.menuNodeMap || state.menuNodeMap.size === 0) return false;

            let current = state.menuNodeMap.get(menuId);
            while (current) {
                if (current.isDefaultShow === true) return true;
                if (!current.parentId) break;
                current = state.menuNodeMap.get(current.parentId);
            }

            return false;
        }

        function parseGpsText(gps) {
            if (!gps || typeof gps !== 'string') return null;
            const text = gps.replaceAll('，', ',').replaceAll('；', ',').replaceAll(';', ',').trim().replace(/\s+/g, ',');
            const parts = text.split(',').map(x => x.trim()).filter(Boolean);
            if (parts.length < 2) return null;
            const lng = Number(parts[0]);
            const lat = Number(parts[1]);
            if (Number.isNaN(lng) || Number.isNaN(lat)) return null;
            return { lng, lat };
        }

        function normalizeGeoJson(raw) {
            if (!raw) return null;
            let obj = raw;
            for (let i = 0; i < 3 && typeof obj === 'string'; i += 1) {
                const text = obj.trim();
                if (!text) return null;
                try {
                    obj = JSON.parse(text);
                } catch {
                    return null;
                }
            }

            if (!obj || typeof obj !== 'object') return null;
            if (obj.type === 'FeatureCollection') return obj;
            if (obj.type === 'Feature') return { type: 'FeatureCollection', features: [obj] };
            if (obj.type) {
                return {
                    type: 'FeatureCollection',
                    features: [{ type: 'Feature', properties: {}, geometry: obj }]
                };
            }
            return null;
        }

        function getCenterFromCoordinates(coords) {
            let minLng = Infinity;
            let minLat = Infinity;
            let maxLng = -Infinity;
            let maxLat = -Infinity;
            let hasPoint = false;

            const walk = (value) => {
                if (!Array.isArray(value) || value.length === 0) return;
                if (typeof value[0] === 'number' && typeof value[1] === 'number') {
                    const lng = Number(value[0]);
                    const lat = Number(value[1]);
                    if (!Number.isFinite(lng) || !Number.isFinite(lat)) return;
                    hasPoint = true;
                    minLng = Math.min(minLng, lng);
                    minLat = Math.min(minLat, lat);
                    maxLng = Math.max(maxLng, lng);
                    maxLat = Math.max(maxLat, lat);
                    return;
                }
                value.forEach(walk);
            };

            walk(coords);
            if (!hasPoint) return null;
            return { lng: (minLng + maxLng) / 2, lat: (minLat + maxLat) / 2 };
        }

        function getCenterFromGeoJson(raw) {
            const geo = normalizeGeoJson(raw);
            if (!geo || !Array.isArray(geo.features)) return null;

            for (let i = 0; i < geo.features.length; i += 1) {
                const geometry = geo.features[i]?.geometry;
                if (!geometry) continue;
                if (geometry.type === 'Point' && Array.isArray(geometry.coordinates)) {
                    const lng = Number(geometry.coordinates[0]);
                    const lat = Number(geometry.coordinates[1]);
                    if (Number.isFinite(lng) && Number.isFinite(lat)) return { lng, lat };
                }
                const center = getCenterFromCoordinates(geometry.coordinates);
                if (center) return center;
            }

            return null;
        }

        function getGeometryKind(item) {
            const geo = normalizeGeoJson(item?.geoJson);
            if (!geo || !Array.isArray(geo.features) || geo.features.length === 0) {
                return item?.gps ? 'point' : 'unknown';
            }

            const geometryType = String(geo.features[0]?.geometry?.type || '').toLowerCase();
            if (!geometryType) return item?.gps ? 'point' : 'unknown';
            if (geometryType.includes('point')) return 'point';
            if (geometryType.includes('line')) return 'line';
            return 'region';
        }

        function extractIconFromDataJson(dataJson) {
            if (!dataJson) return '';
            let obj = dataJson;
            for (let i = 0; i < 3 && typeof obj === 'string'; i += 1) {
                const text = obj.trim();
                if (!text) return '';
                try {
                    obj = JSON.parse(text);
                } catch {
                    return '';
                }
            }

            if (!obj || typeof obj !== 'object' || Array.isArray(obj)) return '';
            const icon = obj.icon || obj.iconUrl || obj.markerIcon || '';
            return typeof icon === 'string' ? icon.trim() : '';
        }

        function getGeometryIcon(item) {
            const direct = normalizeIconPath(item?.icon);
            if (direct) return direct;
            const fromData = normalizeIconPath(extractIconFromDataJson(item?.dataJson));
            return fromData;
        }

        function getGeometryCenter(item) {
            const gps = parseGpsText(item?.gps);
            if (gps) return gps;
            return getCenterFromGeoJson(item?.geoJson);
        }

        function normalizeDataUrl(url) {
            if (!url || typeof url !== 'string') return '';
            const text = url.trim().replace(/\s+/g, '');
            if (!text.toLowerCase().startsWith('data:')) return text;

            const commaIndex = text.indexOf(',');
            if (commaIndex < 0) return text;

            const head = text.substring(0, commaIndex);
            const payload = text.substring(commaIndex + 1);
            if (!/;base64/i.test(head)) return text;

            const normalizedPayload = payload
                .replace(/-/g, '+')
                .replace(/_/g, '/');
            const padLength = normalizedPayload.length % 4;
            const paddedPayload = padLength === 0
                ? normalizedPayload
                : normalizedPayload + '='.repeat(4 - padLength);

            return `${head},${paddedPayload}`;
        }

        function normalizeIconPath(path) {
            if (!path || typeof path !== 'string') return '';
            const text = path.trim().replace(/\\/g, '/');
            if (!text) return '';
            if (text.startsWith('data:')) return normalizeDataUrl(text);
            if (text.startsWith('blob:')) return text;
            if (text.startsWith('~/')) return `/${text.substring(2).replace(/^\/+/, '')}`;
            if (text.startsWith('http://') || text.startsWith('https://') || text.startsWith('/')) return text;
            return `/${text.replace(/^\/+/, '')}`;
        }

        function clearGeometryPointMarkers() {
            state.geometryPointMarkerMap.forEach(marker => marker.remove());
            state.geometryPointMarkerMap.clear();
        }

        function syncGeometryPointMarkerVisibility() {
            state.geometryPointMarkerMap.forEach((marker, id) => {
                marker.getElement().style.display = state.geometryVisibleMap.get(id) === false ? 'none' : '';
            });
        }

        function rebuildGeometryPointMarkers() {
            clearGeometryPointMarkers();

            state.geometries.forEach(item => {
                if (!item) return;
                const geometryType = getGeometryKind(item);
                if (geometryType !== 'point') return;

                const gps = getGeometryCenter(item);
                if (!gps) return;

                const el = document.createElement('div');
                el.className = 'geometry-point-marker';

                const iconPath = getGeometryIcon(item);
                if (iconPath) {
                    const iconEl = document.createElement('img');
                    iconEl.className = 'marker-icon';
                    iconEl.src = iconPath;
                    iconEl.alt = item.name || item.alias || '点位图标';
                    iconEl.onerror = () => {
                        iconEl.remove();
                        const fallbackDot = document.createElement('span');
                        fallbackDot.className = 'dot-fallback';
                        el.appendChild(fallbackDot);
                    };
                    el.appendChild(iconEl);
                }

                if (!iconPath) {
                    const fallbackDot = document.createElement('span');
                    fallbackDot.className = 'dot-fallback';
                    el.appendChild(fallbackDot);
                }

                const label = document.createElement('span');
                label.className = 'marker-label';
                label.textContent = item.alias || item.name || `点位${item.id}`;
                el.appendChild(label);

                el.addEventListener('click', (evt) => {
                    evt.stopPropagation();
                    onGeometryMarkerClick(item.id);
                });

                const marker = new mapboxgl.Marker(el)
                    .setLngLat([gps.lng, gps.lat])
                    .addTo(map);

                state.geometryPointMarkerMap.set(item.id, marker);
            });

            syncGeometryPointMarkerVisibility();
        }

        function applyGeometryVisibility() {
            const geometryLayerManager = getGeometryLayerManager();
            if (!geometryLayerManager) return;
            geometryLayerManager.render();
            geometryLayerManager.setVisible(true);

            const visibleIds = state.geometries
                .filter(g => state.geometryVisibleMap.get(g.id) !== false)
                .map(g => g.id);
            geometryLayerManager.setVisibleIds(visibleIds);
            syncGeometryPointMarkerVisibility();
            renderMenuTree();
        }

        async function loadGeometries() {
            try {
                const resp = await fetch('?handler=GeometryLayerData');
                const res = await resp.json();
                if (res.code !== 0 || !Array.isArray(res.data)) return;

                state.geometries = res.data;
                state.geometryByMenuId = new Map();
                state.geometries.forEach(item => {
                    const itemId = item.id;
                    const itemKey = String(itemId);
                    const existingVisible = state.geometryVisibleMap.get(itemId);
                    const existingVisibleByKey = state.geometryVisibleMap.get(itemKey);
                    const hasExisting = existingVisible !== undefined || existingVisibleByKey !== undefined;
                    const preservedVisible = state.preservedVisibleGeometryIds.has(itemKey);

                    if (!hasExisting) {
                        state.geometryVisibleMap.set(itemId, preservedVisible ? true : isGeometryDefaultVisible(item.menuId));
                    } else if (preservedVisible) {
                        state.geometryVisibleMap.set(itemId, true);
                    }

                    const menuId = item.menuId === null || item.menuId === undefined || item.menuId === ''
                        ? null
                        : Number(item.menuId);
                    if (!state.geometryByMenuId.has(menuId)) {
                        state.geometryByMenuId.set(menuId, []);
                    }
                    state.geometryByMenuId.get(menuId).push(item.id);
                });

                const geometryLayerManager = getGeometryLayerManager();
                if (geometryLayerManager) {
                    geometryLayerManager.setDataFromRows(state.geometries);
                    geometryLayerManager.render();
                }
                rebuildGeometryPointMarkers();
                applyGeometryVisibility();
                renderMenuTree();
                state.preservedVisibleGeometryIds = new Set();
            } catch {
                // ignore geometry loading failures
            }
        }

        return {
            buildMenuNodes,
            renderMenuTree,
            loadMenus,
            applyGeometryVisibility,
            loadGeometries,
            rebuildGeometryPointMarkers,
            syncGeometryPointMarkerVisibility
        };
    }

    window.GisIndexData = { create };
})();
