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
            const formatDataDt = (value) => {
                if (!value) return '';
                const dt = new Date(value);
                if (Number.isNaN(dt.getTime())) return '';
                const yy = dt.getFullYear();
                const mm = String(dt.getMonth() + 1).padStart(2, '0');
                const dd = String(dt.getDate()).padStart(2, '0');
                const hh = String(dt.getHours()).padStart(2, '0');
                const mi = String(dt.getMinutes()).padStart(2, '0');
                return `${yy}-${mm}-${dd} ${hh}:${mi}`;
            };
            const renderNode = (node, depth) => {
                const row = document.createElement('div');
                row.className = 'menu-node';
                row.style.marginLeft = `${depth * 14}px`;

                const ids = getMenuGeometryIds(node, geometryCache);
                const visibleCount = ids.filter(id => state.geometryVisibleMap.get(id) !== false).length;
                const menuCount = Number(node.dataCnt ?? 0);
                const count = Number.isFinite(menuCount) && menuCount >= 0 ? menuCount : 0;

                const checkbox = document.createElement('input');
                checkbox.type = 'checkbox';
                checkbox.className = 'menu-node-check';
                checkbox.disabled = count === 0;
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

                const badge = document.createElement('button');
                badge.type = 'button';
                badge.className = `menu-node-count ${count > 0 ? '' : 'empty'}`;
                badge.textContent = `${count}`;
                const nodeLabel = node.name || `菜单${node.id}`;
                const dtText = formatDataDt(node.dataDt);
                badge.title = count > 0
                    ? `查看${nodeLabel}点位清单${dtText ? `（统计时间: ${dtText}）` : ''}`
                    : '无点位';
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
                            isDefaultShow: !!(n.isDefaultShow ?? n.IsDefaultShow),
                            dataCnt: Number(n.dataCnt ?? n.DataCnt ?? 0),
                            dataDt: n.dataDt ?? n.DataDt ?? null
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

        /**
         * 获取点位渲染类型（从 type 字段或 GeoJson 推断）
         * @param {object} item - 点位数据对象
         * @returns {string} 类型名: point/shape/text/video/image/file/unknown
         */
        function getGeometryKind(item) {
            // 优先使用已定义的类型字段 (GeometryType枚举: 1=点,2=形状,3=文字,4=图片,5=视频,6=文件)
            var rawType = item?.type ?? item?.Type;
            if (rawType !== undefined && rawType !== null && rawType !== '') {
                var t = Number(rawType);
                if (t === 1) return 'point';
                if (t === 2) return 'shape';
                if (t === 3) return 'text';
                if (t === 4) return 'image';
                if (t === 5) return 'video';
                if (t === 6) return 'file';

                var txt = String(rawType).trim().toLowerCase();
                if (txt === 'point' || txt === '点') return 'point';
                if (txt === 'shape' || txt === '形状') return 'shape';
                if (txt === 'text' || txt === '文字') return 'text';
                if (txt === 'image' || txt === '图片') return 'image';
                if (txt === 'video' || txt === '视频') return 'video';
                if (txt === 'file' || txt === '文件' || txt === 'threed' || txt === '3d' || txt === '三维') return 'file';
            }

            // 回退：从 GeoJson 推断
            const geo = normalizeGeoJson(item?.geoJson);
            if (!geo || !Array.isArray(geo.features) || geo.features.length === 0) {
                return item?.gps ? 'point' : 'unknown';
            }

            const geometryType = String(geo.features[0]?.geometry?.type || '').toLowerCase();
            if (!geometryType) return item?.gps ? 'point' : 'unknown';
            if (geometryType.includes('point')) return 'point';
            if (geometryType.includes('line')) return 'line';
            return 'shape';
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

        function splitMultiUrls(text) {
            if (!text || typeof text !== 'string') return [];
            return text
                .replace(/[\r\n]+/g, ',')
                .split(/[;,，；\s]+/)
                .map(function (x) { return (x || '').trim(); })
                .filter(Boolean);
        }

        function resolveImageUrlFromAtt(att) {
            var parts = splitMultiUrls(att);
            if (!parts.length) return '';

            var first = parts[0];
            if (!first) return '';

            // 兼容把 FileViewer 地址误存到 Att 的场景：优先提取 src
            if (first.indexOf('/Shared/FileViewer') >= 0) {
                try {
                    var base = window.location && window.location.origin ? window.location.origin : 'http://localhost';
                    var u = new URL(first, base);
                    var src = (u.searchParams.get('src') || '').trim();
                    if (src) return normalizeIconPath(decodeURIComponent(src));
                } catch {
                    // ignore parse errors
                }
            }

            return normalizeIconPath(first);
        }

        function toImageSourceCoordinatesFromRegion(regionText) {
            if (!regionText || typeof regionText !== 'string') return null;
            var parts = regionText
                .replace(/[，；;]/g, ',')
                .split(',')
                .map(function (x) { return Number((x || '').trim()); })
                .filter(function (n) { return Number.isFinite(n); });

            if (parts.length < 4) return null;

            var tlx = parts[0];
            var tly = parts[1];
            var brx = parts[2];
            var bry = parts[3];

            var minLng = Math.min(tlx, brx);
            var maxLng = Math.max(tlx, brx);
            var minLat = Math.min(tly, bry);
            var maxLat = Math.max(tly, bry);

            if (!Number.isFinite(minLng) || !Number.isFinite(maxLng) || !Number.isFinite(minLat) || !Number.isFinite(maxLat)) return null;
            if (Math.abs(maxLng - minLng) < 1e-9 || Math.abs(maxLat - minLat) < 1e-9) return null;

            return [
                [minLng, maxLat],
                [maxLng, maxLat],
                [maxLng, minLat],
                [minLng, minLat]
            ];
        }

        function toImageSourceCoordinatesFromRing(ring) {
            if (!Array.isArray(ring) || ring.length < 4) return null;

            var minLng = Infinity;
            var maxLng = -Infinity;
            var minLat = Infinity;
            var maxLat = -Infinity;
            var count = 0;

            ring.forEach(function (coord) {
                if (!Array.isArray(coord) || coord.length < 2) return;
                var lng = Number(coord[0]);
                var lat = Number(coord[1]);
                if (!Number.isFinite(lng) || !Number.isFinite(lat)) return;
                minLng = Math.min(minLng, lng);
                maxLng = Math.max(maxLng, lng);
                minLat = Math.min(minLat, lat);
                maxLat = Math.max(maxLat, lat);
                count += 1;
            });

            if (count < 4) return null;
            if (!Number.isFinite(minLng) || !Number.isFinite(maxLng) || !Number.isFinite(minLat) || !Number.isFinite(maxLat)) return null;
            if (Math.abs(maxLng - minLng) < 1e-9 || Math.abs(maxLat - minLat) < 1e-9) return null;

            // Mapbox ImageSource 坐标顺序：左上、右上、右下、左下
            return [
                [minLng, maxLat],
                [maxLng, maxLat],
                [maxLng, minLat],
                [minLng, minLat]
            ];
        }

        function toImageSourceCoordinatesFromGeoJson(geo) {
            if (!geo || !Array.isArray(geo.features) || geo.features.length === 0) return null;

            var minLng = Infinity;
            var maxLng = -Infinity;
            var minLat = Infinity;
            var maxLat = -Infinity;
            var count = 0;

            var collect = function (value) {
                if (!Array.isArray(value) || value.length === 0) return;
                if (typeof value[0] === 'number' && typeof value[1] === 'number') {
                    var lng = Number(value[0]);
                    var lat = Number(value[1]);
                    if (!Number.isFinite(lng) || !Number.isFinite(lat)) return;
                    minLng = Math.min(minLng, lng);
                    maxLng = Math.max(maxLng, lng);
                    minLat = Math.min(minLat, lat);
                    maxLat = Math.max(maxLat, lat);
                    count += 1;
                    return;
                }
                value.forEach(collect);
            };

            geo.features.forEach(function (feature) {
                var geometry = feature && feature.geometry;
                if (!geometry) return;
                collect(geometry.coordinates);
            });

            if (count < 4) return null;
            if (!Number.isFinite(minLng) || !Number.isFinite(maxLng) || !Number.isFinite(minLat) || !Number.isFinite(maxLat)) return null;
            if (Math.abs(maxLng - minLng) < 1e-9 || Math.abs(maxLat - minLat) < 1e-9) return null;

            return [
                [minLng, maxLat],
                [maxLng, maxLat],
                [maxLng, minLat],
                [minLng, minLat]
            ];
        }

        function clearGeometryPointMarkers() {
            state.geometryPointMarkerMap.forEach(marker => marker.remove());
            state.geometryPointMarkerMap.clear();
        }

        function syncGeometryPointMarkerVisibility() {
            state.geometryPointMarkerMap.forEach((marker, id) => {
                var markerEl = marker.getElement();
                if (!markerEl) return;

                markerEl.style.display = state.geometryVisibleMap.get(id) === false ? 'none' : '';

                var normalizedId = Number.isFinite(Number(id)) ? Number(id) : id;
                var isSelected = String(state.selectedGeometryId ?? '') === String(normalizedId ?? '');
                markerEl.classList.toggle('is-selected', !!isSelected);
            });
        }

        /**
         * 创建"文字"类型点位标记 - 在地图上显示纯文本标签
         */
        function createTextMarker(item, gps) {
            const el = document.createElement('div');
            el.className = 'geometry-text-marker';
            el.textContent = item.alias || item.name || `点位${item.id}`;
            el.style.cssText = `
                background: rgba(15, 23, 42, 0.75);
                color: #f1f5f9;
                padding: 4px 12px;
                border-radius: 4px;
                font-size: 14px;
                font-weight: 600;
                white-space: nowrap;
                border: 1px solid rgba(96, 165, 250, 0.5);
                backdrop-filter: blur(4px);
                cursor: pointer;
                pointer-events: auto;
            `;
            el.addEventListener('click', (evt) => {
                evt.stopPropagation();
                onGeometryMarkerClick(item.id);
            });
            return new mapboxgl.Marker({ element: el, anchor: 'center' })
                .setLngLat([gps.lng, gps.lat])
                .addTo(map);
        }

        /**
         * 创建"视频"类型点位标记 - 显示监控摄像头图标，点击打开通用视频窗口
         */
        function createVideoMarker(item, gps) {
            const el = document.createElement('div');
            el.className = 'geometry-video-marker';
            el.style.cssText = 'cursor:pointer;text-align:center;';

            const iconImg = document.createElement('img');
            iconImg.src = '/icons/camera.svg';
            iconImg.alt = '监控';
            iconImg.style.cssText = 'width:36px;height:36px;filter:drop-shadow(0 2px 6px rgba(0,0,0,0.5));';
            iconImg.onerror = function() {
                this.style.display = 'none';
                const fallback = document.createElement('span');
                fallback.textContent = '📹';
                fallback.style.cssText = 'font-size:28px;';
                el.appendChild(fallback);
            };
            el.appendChild(iconImg);

            const label = document.createElement('span');
            label.className = 'marker-label';
            label.textContent = item.alias || item.name || `点位${item.id}`;
            el.appendChild(label);

            el.addEventListener('click', (evt) => {
                evt.stopPropagation();
                // 统一交给 action 层分流（视频窗口/详情窗口）
                onGeometryMarkerClick(item.id);
            });

            return new mapboxgl.Marker({ element: el, anchor: 'bottom' })
                .setLngLat([gps.lng, gps.lat])
                .addTo(map);
        }

        /**
         * 创建"文件"类型点位标记 - 显示文件图标，点击后打开文件预览
         */
        function createFileMarker(item, gps) {
            const el = document.createElement('div');
            el.className = 'geometry-file-marker';
            el.style.cssText = 'cursor:pointer;text-align:center;';

            const iconSpan = document.createElement('span');
            iconSpan.textContent = '📄';
            iconSpan.style.cssText = 'font-size:32px;filter:drop-shadow(0 2px 6px rgba(0,0,0,0.5));display:block;';
            el.appendChild(iconSpan);

            const label = document.createElement('span');
            label.className = 'marker-label';
            label.textContent = item.alias || item.name || `点位${item.id}`;
            el.appendChild(label);

            el.addEventListener('click', (evt) => {
                evt.stopPropagation();
                onGeometryMarkerClick(item.id);
            });

            return new mapboxgl.Marker({ element: el, anchor: 'bottom' })
                .setLngLat([gps.lng, gps.lat])
                .addTo(map);
        }

        /**
         * 创建"图片"类型 - 使用 Mapbox ImageSource 显示图片图层
         * GeoJson 应包含一个矩形 Polygon 作为显示区域
         */
        function createImageLayer(item) {
            var imageUrl = resolveImageUrlFromAtt(item?.att || item?.Att || '');
            if (!imageUrl) return;

            // 1) 优先使用 Region 字段
            var coords = toImageSourceCoordinatesFromRegion(item?.region || item?.Region || '');

            // 2) 兜底从 GeoJson 获取外围矩形区域坐标
            var geo = normalizeGeoJson(item?.geoJson);
            if (!coords && (!geo || !Array.isArray(geo.features))) return;

            if (!coords) coords = toImageSourceCoordinatesFromGeoJson(geo);

            // 兼容历史数据：若无法从全量坐标得到外接矩形，则回退到首个 polygon ring。
            if (!coords) {
                for (var i = 0; i < geo.features.length; i++) {
                    var geom = geo.features[i]?.geometry;
                    if (!geom || (geom.type !== 'Polygon' && geom.type !== 'MultiPolygon')) continue;

                    if (geom.type === 'Polygon' && Array.isArray(geom.coordinates) && geom.coordinates.length > 0) {
                        coords = toImageSourceCoordinatesFromRing(geom.coordinates[0]);
                        if (coords) break;
                    }

                    if (geom.type === 'MultiPolygon' && Array.isArray(geom.coordinates) && geom.coordinates.length > 0) {
                        var firstPolygon = geom.coordinates[0];
                        if (Array.isArray(firstPolygon) && firstPolygon.length > 0) {
                            coords = toImageSourceCoordinatesFromRing(firstPolygon[0]);
                            if (coords) break;
                        }
                    }
                }
            }

            if (!coords || coords.length < 4) {
                // 无矩形区域，使用 gps 点扩展一个默认矩形
                var gps = getGeometryCenter(item);
                if (!gps) return;
                var d = 0.005; // 约500m
                coords = [
                    [gps.lng - d, gps.lat + d],
                    [gps.lng + d, gps.lat + d],
                    [gps.lng + d, gps.lat - d],
                    [gps.lng - d, gps.lat - d]
                ];
            }

            var sourceId = 'gis-image-source-' + item.id;
            var layerId = 'gis-image-layer-' + item.id;

            // 确保 source 和 layer 不重复
            if (map.getLayer(layerId)) map.removeLayer(layerId);
            if (map.getSource(sourceId)) map.removeSource(sourceId);

            try {
                map.addSource(sourceId, {
                    type: 'image',
                    url: normalizeIconPath(imageUrl),
                    coordinates: coords
                });
                map.addLayer({
                    id: layerId,
                    type: 'raster',
                    source: sourceId,
                    paint: { 'raster-opacity': 0.85 }
                });
            } catch (e) {
                console.warn('图片图层创建失败:', e);
            }
        }

        function syncImageLayerVisibility() {
            state.geometries.forEach(function (item) {
                if (!item) return;
                if (getGeometryKind(item) !== 'image') return;

                var layerId = 'gis-image-layer-' + item.id;
                if (!map.getLayer(layerId)) return;

                var isVisible = state.geometryVisibleMap.get(item.id) !== false;
                try {
                    map.setLayoutProperty(layerId, 'visibility', isVisible ? 'visible' : 'none');
                } catch {
                    // ignore layout errors
                }
            });
        }

        /**
         * 重建所有几何点位标记（支持多种类型）
         * - point: 显示图标+标签（现有逻辑）
         * - text: 纯文本标签
         * - video: 监控图标，点击打开视频
         * - file: 文件图标，点击打开文件预览
         * - shape: 由 geometryLayerManager 处理
         * - image: 由 createImageLayer 处理
         */
        function rebuildGeometryPointMarkers() {
            clearGeometryPointMarkers();

            // 先清除旧的图片图层
            state.geometries.forEach(item => {
                if (!item) return;
                var gk = getGeometryKind(item);
                if (gk === 'image') {
                    var srcId = 'gis-image-source-' + item.id;
                    var lyrId = 'gis-image-layer-' + item.id;
                    if (map.getLayer(lyrId)) map.removeLayer(lyrId);
                    if (map.getSource(srcId)) map.removeSource(srcId);
                }
            });

            state.geometries.forEach(item => {
                if (!item) return;
                const geometryType = getGeometryKind(item);

                // 图片类型用 ImageSource 渲染
                if (geometryType === 'image') {
                    createImageLayer(item);
                    return;
                }

                // 形状类型由 geometryLayerManager 处理
                if (geometryType === 'shape') return;

                const gps = getGeometryCenter(item);
                if (!gps) return;

                var marker = null;

                switch (geometryType) {
                    case 'text':
                        marker = createTextMarker(item, gps);
                        break;
                    case 'video':
                        marker = createVideoMarker(item, gps);
                        break;
                    case 'file':
                        marker = createFileMarker(item, gps);
                        break;
                    default: // 'point' 或其他
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
                        marker = new mapboxgl.Marker(el)
                            .setLngLat([gps.lng, gps.lat])
                            .addTo(map);
                        break;
                }

                if (marker) {
                    state.geometryPointMarkerMap.set(item.id, marker);
                }
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
            syncImageLayerVisibility();
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
                    // 仅将"形状"类型的数据传给 geometryLayerManager 渲染（点/文字/视频/图片/文件由自定义标记处理）
                    var shapeRows = state.geometries.filter(function(g) {
                        var kind = getGeometryKind(g);
                        return kind === 'shape' || kind === 'region' || kind === 'line';
                    });
                    geometryLayerManager.setDataFromRows(shapeRows.length > 0 ? shapeRows : state.geometries);
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
