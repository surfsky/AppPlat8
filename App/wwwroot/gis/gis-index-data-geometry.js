(function () {
    function create(ctx) {
        const state = ctx.state;
        const map = ctx.map;
        const getGeometryLayerManager = ctx.getGeometryLayerManager;
        const onGeometryMarkerClick = ctx.onGeometryMarkerClick || (() => {});
        const renderMenuTree = ctx.renderMenuTree || (() => {});
        const isGeometryDefaultVisible = ctx.isGeometryDefaultVisible || (() => false);
        const isMenuZoomVisible = ctx.isMenuZoomVisible || (() => true);
        const isGeometrySelectable = ctx.isGeometrySelectable || (() => true);
        const getGeometryKind = ctx.getGeometryKind;
        const getGeometryCenter = ctx.getGeometryCenter;
        const getGeometryIcon = ctx.getGeometryIcon;
        const normalizeGeoJson = ctx.normalizeGeoJson;
        const normalizeIconPath = ctx.normalizeIconPath;
        const resolveImageUrlFromFileOrAtt = ctx.resolveImageUrlFromFileOrAtt;
        const toImageSourceCoordinatesFromRegion = ctx.toImageSourceCoordinatesFromRegion;
        const toImageSourceCoordinatesFromRing = ctx.toImageSourceCoordinatesFromRing;
        const toImageSourceCoordinatesFromGeoJson = ctx.toImageSourceCoordinatesFromGeoJson;

        function clearGeometryPointMarkers() {
            state.geometryPointMarkerMap.forEach(marker => marker.remove());
            state.geometryPointMarkerMap.clear();
        }

        function syncGeometryPointMarkerVisibility() {
            const byId = new Map((state.geometries || []).map(item => [String(item?.id), item]));
            state.geometryPointMarkerMap.forEach((marker, id) => {
                const markerEl = marker.getElement();
                if (!markerEl) return;
                const item = byId.get(String(id));
                const isVisible = state.geometryVisibleMap.get(id) !== false && isMenuZoomVisible(item?.menuId);
                const selectable = isGeometrySelectable(item?.menuId);

                markerEl.style.display = isVisible ? '' : 'none';
                markerEl.style.pointerEvents = selectable ? 'auto' : 'none';
                markerEl.style.cursor = selectable ? '' : 'default';
                markerEl.style.opacity = selectable ? '' : '0.92';

                const normalizedId = Number.isFinite(Number(id)) ? Number(id) : id;
                const isSelected = String(state.selectedGeometryId ?? '') === String(normalizedId ?? '');
                markerEl.classList.toggle('is-selected', !!isSelected);
            });
        }

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
            el.addEventListener('click', evt => {
                evt.stopPropagation();
                if (!isGeometrySelectable(item?.menuId)) return;
                onGeometryMarkerClick(item.id);
            });
            return new mapboxgl.Marker({ element: el, anchor: 'center' })
                .setLngLat([gps.lng, gps.lat])
                .addTo(map);
        }

        function createVideoMarker(item, gps) {
            const el = document.createElement('div');
            el.className = 'geometry-video-marker';
            el.style.cssText = 'cursor:pointer;text-align:center;';

            const iconImg = document.createElement('img');
            iconImg.src = '/icons/camera.svg';
            iconImg.alt = '监控';
            iconImg.style.cssText = 'width:36px;height:36px;filter:drop-shadow(0 2px 6px rgba(0,0,0,0.5));';
            iconImg.onerror = function () {
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

            el.addEventListener('click', evt => {
                evt.stopPropagation();
                if (!isGeometrySelectable(item?.menuId)) return;
                onGeometryMarkerClick(item.id);
            });

            return new mapboxgl.Marker({ element: el, anchor: 'bottom' })
                .setLngLat([gps.lng, gps.lat])
                .addTo(map);
        }

        function createFileMarker(item, gps) {
            const el = document.createElement('div');
            el.className = 'geometry-file-marker';
            el.style.cssText = 'cursor:pointer;text-align:center;';

            const fileUrl = resolveImageUrlFromFileOrAtt(item?.file || item?.File, item?.att || item?.Att || '');
            if (fileUrl && /\.(png|jpg|jpeg|gif|webp|bmp|svg)$/i.test(fileUrl)) {
                const img = document.createElement('img');
                img.src = fileUrl;
                img.alt = '附件';
                img.style.cssText = 'max-width:48px;max-height:48px;display:block;margin:0 auto 2px;object-fit:contain;box-shadow:0 2px 6px rgba(0,0,0,0.15);border-radius:4px;';
                el.appendChild(img);
            } else {
                const iconSpan = document.createElement('span');
                iconSpan.textContent = '📄';
                iconSpan.style.cssText = 'font-size:32px;filter:drop-shadow(0 2px 6px rgba(0,0,0,0.5));display:block;';
                el.appendChild(iconSpan);
            }

            const label = document.createElement('span');
            label.className = 'marker-label';
            label.textContent = item.alias || item.name || `点位${item.id}`;
            el.appendChild(label);

            el.addEventListener('click', evt => {
                evt.stopPropagation();
                if (!isGeometrySelectable(item?.menuId)) return;
                onGeometryMarkerClick(item.id);
            });

            return new mapboxgl.Marker({ element: el, anchor: 'bottom' })
                .setLngLat([gps.lng, gps.lat])
                .addTo(map);
        }

        function createImageLayer(item) {
            const imageUrl = resolveImageUrlFromFileOrAtt(item?.file || item?.File, item?.att || item?.Att || '');
            if (!imageUrl) return;

            let coords = toImageSourceCoordinatesFromRegion(item?.region || item?.Region || '');
            const geo = normalizeGeoJson(item?.geoJson);
            if (!coords && (!geo || !Array.isArray(geo.features))) return;

            if (!coords) coords = toImageSourceCoordinatesFromGeoJson(geo);

            if (!coords) {
                for (let i = 0; i < geo.features.length; i++) {
                    const geom = geo.features[i]?.geometry;
                    if (!geom || (geom.type !== 'Polygon' && geom.type !== 'MultiPolygon')) continue;

                    if (geom.type === 'Polygon' && Array.isArray(geom.coordinates) && geom.coordinates.length > 0) {
                        coords = toImageSourceCoordinatesFromRing(geom.coordinates[0]);
                        if (coords) break;
                    }

                    if (geom.type === 'MultiPolygon' && Array.isArray(geom.coordinates) && geom.coordinates.length > 0) {
                        const firstPolygon = geom.coordinates[0];
                        if (Array.isArray(firstPolygon) && firstPolygon.length > 0) {
                            coords = toImageSourceCoordinatesFromRing(firstPolygon[0]);
                            if (coords) break;
                        }
                    }
                }
            }

            if (!coords || coords.length < 4) {
                const gps = getGeometryCenter(item);
                if (!gps) return;
                const d = 0.005;
                coords = [
                    [gps.lng - d, gps.lat + d],
                    [gps.lng + d, gps.lat + d],
                    [gps.lng + d, gps.lat - d],
                    [gps.lng - d, gps.lat - d]
                ];
            }

            const sourceId = 'gis-image-source-' + item.id;
            const layerId = 'gis-image-layer-' + item.id;

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
            state.geometries.forEach(item => {
                if (!item) return;
                if (getGeometryKind(item) !== 'image') return;

                const layerId = 'gis-image-layer-' + item.id;
                if (!map.getLayer(layerId)) return;

                const isVisible = state.geometryVisibleMap.get(item.id) !== false;
                const zoomVisible = isMenuZoomVisible(item.menuId);
                try {
                    map.setLayoutProperty(layerId, 'visibility', isVisible && zoomVisible ? 'visible' : 'none');
                } catch {
                    // ignore layout errors
                }
            });
        }

        function rebuildGeometryPointMarkers() {
            clearGeometryPointMarkers();

            state.geometries.forEach(item => {
                if (!item) return;
                const gk = getGeometryKind(item);
                if (gk === 'image') {
                    const srcId = 'gis-image-source-' + item.id;
                    const lyrId = 'gis-image-layer-' + item.id;
                    if (map.getLayer(lyrId)) map.removeLayer(lyrId);
                    if (map.getSource(srcId)) map.removeSource(srcId);
                }
            });

            state.geometries.forEach(item => {
                if (!item) return;
                const geometryType = getGeometryKind(item);

                if (geometryType === 'image') {
                    createImageLayer(item);
                    return;
                }

                if (geometryType === 'shape') return;

                const gps = getGeometryCenter(item);
                if (!gps) return;

                let marker = null;

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
                    default: {
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
                        el.addEventListener('click', evt => {
                            evt.stopPropagation();
                            if (!isGeometrySelectable(item?.menuId)) return;
                            onGeometryMarkerClick(item.id);
                        });
                        marker = new mapboxgl.Marker(el)
                            .setLngLat([gps.lng, gps.lat])
                            .addTo(map);
                        break;
                    }
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
                .filter(g => state.geometryVisibleMap.get(g.id) !== false && isMenuZoomVisible(g.menuId))
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
                    const shapeRows = state.geometries.filter(g => {
                        const kind = getGeometryKind(g);
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
            clearGeometryPointMarkers,
            syncGeometryPointMarkerVisibility,
            rebuildGeometryPointMarkers,
            applyGeometryVisibility,
            loadGeometries
        };
    }

    window.GisIndexDataGeometry = { create };
})();
