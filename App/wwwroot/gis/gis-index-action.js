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

        function openGeometryDetailDrawer(id) {
            if (!id) return;
            state.selectedGeometryId = id;

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
                map.getCanvas().style.cursor = hits && hits.length > 0 ? 'pointer' : '';
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
