/**
 * 地图操作相关（点位详情、点位列表、统计模式等）
 */
(function () {
    function resolveManager() {
        if (window.top && window.top.EleManager) return window.top.EleManager;
        return window.EleManager;
    }

    function create(ctx) {
        const map = ctx.map;
        const state = ctx.state;
        const geometryDisplayLayerIds = ctx.geometryDisplayLayerIds || [];
        const onOpenGeometryDetail = ctx.onOpenGeometryDetail;
        const onCloseGeometryDetail = ctx.onCloseGeometryDetail;
        const onClosePointList = ctx.onClosePointList;
        const onStatsModeChanged = ctx.onStatsModeChanged;
        const onGeometrySelectedChanged = ctx.onGeometrySelectedChanged;
        const isGeometrySelectable = ctx.isGeometrySelectable || (() => true);

        function toggleStatsMode() {
            const manager = resolveManager();
            window.GisIndexUI.toggleStatsMode(state, {
                onCloseDrawer: () => {
                    if (typeof onCloseGeometryDetail === 'function') {
                        onCloseGeometryDetail();
                    }
                    if (typeof onClosePointList === 'function') {
                        onClosePointList();
                    }
                    if (manager && typeof manager.closeDrawer === 'function') {
                        manager.closeDrawer();
                    }
                },
                onStatsModeChanged
            });
        }

        function openCheckObjectDrawer(id) {
            if (!id) return;
            const manager = resolveManager();
            if (!manager || typeof manager.openDrawer !== 'function') {
                window.open(`/Checks/CheckObjectForm?id=${id}&md=view`, '_blank');
                return;
            }

            manager.openDrawer({
                title: '检查对象详情',
                url: `/Checks/CheckObjectForm?id=${id}&md=view`,
                direction: 'rtl',
                size: window.innerWidth < 768 ? '100%' : '25%',
                resizable: true,
                closeOnClickModal: false,
                destroyOnClose: true
            });
        }

        function parseGeometryType(item) {
            if (!item) return 0;
            const raw = item.type ?? item.Type;
            if (raw === null || raw === undefined) return 0;

            const num = Number(raw);
            if (Number.isFinite(num)) return num;

            const text = String(raw).trim().toLowerCase();
            if (!text) return 0;
            if (text === 'video' || text === '视频') return 5;
            if (text === 'image' || text === '图片') return 4;
            if (text === 'point' || text === '点') return 1;
            if (text === 'shape' || text === '形状') return 2;
            if (text === 'text' || text === '文字') return 3;
            if (text === 'file' || text === '文件' || text === 'threed' || text === '3d' || text === '三维') return 6;
            return 0;
        }

        function findGeometryById(id) {
            if (!Array.isArray(state.geometries) || !state.geometries.length) return null;
            return state.geometries.find(x => Number(x?.id) === Number(id)) || null;
        }

        /**图形是否允许点击*/
        function canSelectGeometry(id) {
            const item = findGeometryById(id);
            if (!item) return true;
            return isGeometrySelectable(item.menuId);
        }

        function splitMultiUrls(src) {
            const text = String(src || '').trim();
            if (!text) return [];

            return text
                .split(/[\n,;，；]+/)
                .map(x => x.trim())
                .filter(Boolean);
        }

        function collectVideoUrls(item) {
            const parts = splitMultiUrls(item?.url ?? item?.Url ?? '');
            if (!parts.length) return [];

            return parts.map((url, idx) => ({
                url,
                name: (item?.name || item?.Name || '监控视频') + (parts.length > 1 ? ` ${idx + 1}` : '')
            }));
        }

        function collectFileUrl(item) {
            const parts = splitMultiUrls(item?.att ?? item?.Att ?? '');
            return parts.length ? parts[0] : '';
        }

        function normalizeFileUrl(url) {
            const text = String(url || '').trim().replace(/\\/g, '/');
            if (!text) return '';
            if (/^https?:\/\//i.test(text) || text.startsWith('/')) return text;
            return `/${text.replace(/^\/+/, '')}`;
        }

        function buildFilePreviewUrl(fileUrl, name) {
            const src = normalizeFileUrl(fileUrl);
            if (!src) return '';
            const encodedSrc = encodeURIComponent(src);
            const encodedName = encodeURIComponent(String(name || '附件预览'));
            return `/Shared/FileViewer?src=${encodedSrc}&name=${encodedName}`;
        }

        function openFileDrawer(previewUrl, title) {
            if (!previewUrl) return;

            const manager = resolveManager();
            if (!manager || typeof manager.openDrawer !== 'function') {
                window.open(previewUrl, '_blank');
                return;
            }

            manager.openDrawer({
                title: title || '文件预览',
                url: previewUrl,
                direction: 'rtl',
                size: window.innerWidth < 768 ? '100%' : '72%',
                resizable: true,
                closeOnClickModal: false,
                destroyOnClose: true
            });
        }

        function openVideoDrawer(urls, title) {
            const manager = resolveManager();
            const list = Array.isArray(urls) ? urls.filter(x => !!x?.url) : [];
            try {
                localStorage.setItem('gis_video_urls', JSON.stringify(list));
            } catch {
                // ignore localStorage failures (private mode / quota)
            }

            let pageUrl = '/Shared/Video';
            if (list.length === 1) {
                pageUrl += `?url=${encodeURIComponent(list[0].url)}`;
            } else if (list.length > 1) {
                pageUrl += `?urls=${encodeURIComponent(JSON.stringify(list))}`;
            }

            if (!manager || typeof manager.openDrawer !== 'function') {
                window.open(pageUrl, '_blank');
                return;
            }

            manager.openDrawer({
                title: title || '监控视频',
                url: pageUrl,
                direction: 'rtl',
                size: window.innerWidth < 768 ? '100%' : '70%',
                resizable: true,
                closeOnClickModal: false,
                destroyOnClose: true
            });
        }

        function openGeometryDetailDrawer(id) {
            if (!id) return;
            state.selectedGeometryId = id;
            if (typeof onGeometrySelectedChanged === 'function') {
                onGeometrySelectedChanged(id);
            }

            const geometry = findGeometryById(id);
            const geometryType = parseGeometryType(geometry);
            if (geometryType === 5) {
                openVideoDrawer(collectVideoUrls(geometry), geometry?.name || geometry?.Name || '监控视频');
                return;
            }

            if (geometryType === 6) {
                const fileUrl = collectFileUrl(geometry);
                if (!fileUrl) {
                    const manager = resolveManager();
                    if (manager && typeof manager.showWarning === 'function')
                        manager.showWarning('未配置文件地址');
                    return;
                }

                const previewUrl = buildFilePreviewUrl(fileUrl, geometry?.name || geometry?.Name || '文件预览');
                openFileDrawer(previewUrl, geometry?.name || geometry?.Name || '文件预览');
                return;
            }

            if (typeof onOpenGeometryDetail === 'function') {
                onOpenGeometryDetail(id);
                return;
            }

            const manager = resolveManager();
            if (!manager || typeof manager.openDrawer !== 'function') {
                window.open(`/GIS/GeometryInfo?id=${id}`, '_blank');
                return;
            }

            manager.openDrawer({
                title: '点位信息',
                url: `/GIS/GeometryInfo?id=${id}`,
                direction: 'rtl',
                size: window.innerWidth < 768 ? '100%' : '38%',
                resizable: true,
                closeOnClickModal: false,
                destroyOnClose: true
            });
        }

        function closeGeometryDetailDrawer() {
            if (typeof onCloseGeometryDetail === 'function') {
                onCloseGeometryDetail();
                return;
            }

            const manager = resolveManager();
            if (manager && typeof manager.closeDrawer === 'function') {
                manager.closeDrawer();
            }
        }

        function getActiveGeometryDisplayLayerIds() {
            return geometryDisplayLayerIds.filter(id => map.getLayer(id));
        }

        function parseGpsText(gps) {
            if (!gps || typeof gps !== 'string') return null;
            const text = gps.replaceAll('，', ',').replaceAll('；', ',').replaceAll(';', ',').trim().replace(/\s+/g, ',');
            const parts = text.split(',').map(x => x.trim()).filter(Boolean);
            if (parts.length < 2) return null;
            const lng = Number(parts[0]);
            const lat = Number(parts[1]);
            if (!Number.isFinite(lng) || !Number.isFinite(lat)) return null;
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

        function getCenterFromFeature(feature) {
            if (!feature || !feature.geometry) return null;
            const geometry = feature.geometry;
            if (geometry.type === 'Point' && Array.isArray(geometry.coordinates)) {
                const lng = Number(geometry.coordinates[0]);
                const lat = Number(geometry.coordinates[1]);
                if (Number.isFinite(lng) && Number.isFinite(lat)) {
                    return { lng, lat };
                }
            }
            return getCenterFromCoordinates(geometry.coordinates);
        }

        function getCenterFromStateGeometry(geometryId) {
            const item = (state.geometries || []).find(g => Number(g.id) === Number(geometryId) || String(g.id) === String(geometryId));
            if (!item) return null;

            const gps = parseGpsText(item.gps);
            if (gps) return gps;

            const geo = normalizeGeoJson(item.geoJson);
            if (!geo || !Array.isArray(geo.features)) return null;

            for (let i = 0; i < geo.features.length; i += 1) {
                const center = getCenterFromFeature(geo.features[i]);
                if (center) return center;
            }

            return null;
        }

        function focusGeometryCenter(geometryId, feature) {
            const center = getCenterFromFeature(feature) || getCenterFromStateGeometry(geometryId);
            if (!center) return;
            const currentZoom = typeof map.getZoom === 'function' ? map.getZoom() : 12;
            const zoom = Math.max(currentZoom, 13.5);
            map.easeTo({ center: [center.lng, center.lat], zoom, duration: 520 });
        }

        function bindGeometryMapInteractions() {
            if (state.geometryInteractionBound) return;
            state.geometryInteractionBound = true;

            map.on('click', (e) => {
                const layers = getActiveGeometryDisplayLayerIds();
                if (!layers.length) return;

                const hits = map.queryRenderedFeatures(e.point, { layers });
                const feature = (hits || []).find(f => f && f.properties && f.properties.__geometryId !== undefined);
                if (!feature) return;

                const rawId = feature.properties.__geometryId;
                const geometryId = Number.isNaN(Number(rawId)) ? rawId : Number(rawId);
                if (!canSelectGeometry(geometryId)) return;
                focusGeometryCenter(geometryId, feature);
                openGeometryDetailDrawer(geometryId);
            });

            map.on('mousemove', (e) => {
                const layers = getActiveGeometryDisplayLayerIds();
                if (!layers.length) {
                    map.getCanvas().style.cursor = '';
                    return;
                }

                const hits = map.queryRenderedFeatures(e.point, { layers });
                const hit = (hits || []).find(f => {
                    const rawId = f?.properties?.__geometryId;
                    if (rawId === undefined) return false;
                    const geometryId = Number.isNaN(Number(rawId)) ? rawId : Number(rawId);
                    return canSelectGeometry(geometryId);
                });
                map.getCanvas().style.cursor = hit ? 'pointer' : '';
            });
        }

        return {
            toggleStatsMode,
            openCheckObjectDrawer,
            openGeometryDetailDrawer,
            closeGeometryDetailDrawer,
            getActiveGeometryDisplayLayerIds,
            bindGeometryMapInteractions
        };
    }

    window.GisIndexAction = { create };
})();
