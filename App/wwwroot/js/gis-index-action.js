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

        function toggleStatsMode() {
            const manager = resolveManager();
            window.GisIndexUI.toggleStatsMode(state, {
                onCloseDrawer: () => {
                    if (manager && typeof manager.closeDrawer === 'function') {
                        manager.closeDrawer();
                    }
                }
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
