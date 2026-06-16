(function () {
    function create(ctx) {
        const utils = window.GisIndexDataUtils.create();
        let menuApi = null;
        let geometryApi = null;

        menuApi = window.GisIndexDataMenu.create({
            state: ctx.state,
            onMenuBadgeClick: ctx.onMenuBadgeClick,
            applyGeometryVisibility: () => geometryApi?.applyGeometryVisibility?.(),
            getGeometryCenter: utils.getGeometryCenter,
            getGeometryKind: utils.getGeometryKind,
            getGeometryIcon: utils.getGeometryIcon
        });

        geometryApi = window.GisIndexDataGeometry.create({
            state: ctx.state,
            map: ctx.map,
            getGeometryLayerManager: ctx.getGeometryLayerManager,
            onGeometryMarkerClick: ctx.onGeometryMarkerClick,
            renderMenuTree: () => menuApi?.renderMenuTree?.(),
            isGeometryDefaultVisible: menuApi.isGeometryDefaultVisible,
            getGeometryKind: utils.getGeometryKind,
            getGeometryCenter: utils.getGeometryCenter,
            getGeometryIcon: utils.getGeometryIcon,
            normalizeGeoJson: utils.normalizeGeoJson,
            normalizeIconPath: utils.normalizeIconPath,
            resolveImageUrlFromFileOrAtt: utils.resolveImageUrlFromFileOrAtt,
            toImageSourceCoordinatesFromRegion: utils.toImageSourceCoordinatesFromRegion,
            toImageSourceCoordinatesFromRing: utils.toImageSourceCoordinatesFromRing,
            toImageSourceCoordinatesFromGeoJson: utils.toImageSourceCoordinatesFromGeoJson
        });

        return {
            buildMenuNodes: () => menuApi.buildMenuNodes(),
            renderMenuTree: () => menuApi.renderMenuTree(),
            loadMenus: () => menuApi.loadMenus(),
            setBatchMenusChecked: menuIds => menuApi.setBatchMenusChecked(menuIds),
            applyGeometryVisibility: () => geometryApi.applyGeometryVisibility(),
            loadGeometries: () => geometryApi.loadGeometries(),
            rebuildGeometryPointMarkers: () => geometryApi.rebuildGeometryPointMarkers(),
            syncGeometryPointMarkerVisibility: () => geometryApi.syncGeometryPointMarkerVisibility()
        };
    }

    window.GisIndexData = { create };
})();
