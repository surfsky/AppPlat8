(function (global) {
    'use strict';

    function createEditor(options) {
        var accessToken = options.accessToken || '';
        var initGeoJson = options.initGeoJson || '';
        var mapContainerId = options.mapContainerId || 'map';
        var onError = typeof options.onError === 'function' ? options.onError : function () { };
        var onPaletteChange = typeof options.onPaletteChange === 'function' ? options.onPaletteChange : function () { };
        var labelPriorityFields = normalizeLabelPriorityFields(options.labelPriorityFields);

        var palette = {
            pointColor: '#dc2626',
            lineColor: '#1e3a8a',
            fillColor: '#1d4ed8',
            fillOpacityPercent: 35
        };

        var map = null;
        var draw = null;
        var labelSyncQueued = false;
        var interactionBound = false;
        var lastPickedFeatureId = null;

        function notifyPalette() {
            onPaletteChange({
                pointColor: palette.pointColor,
                lineColor: palette.lineColor,
                fillColor: palette.fillColor,
                fillOpacityPercent: palette.fillOpacityPercent
            });
        }

        function normalizeLabelPriorityFields(fields) {
            var defaults = ['NAME', 'name', 'label', 'SZSQ', 'SZZ', 'SZQX', 'title', 'alias', 'text', 'Label', 'Name', 'Title', 'Alias', 'Text'];
            if (!Array.isArray(fields) || fields.length === 0) return defaults;

            var result = [];
            var seen = new Set();
            fields.forEach(function (f) {
                var key = f === null || f === undefined ? '' : String(f).trim();
                if (!key) return;
                if (seen.has(key)) return;
                seen.add(key);
                result.push(key);
            });
            return result.length > 0 ? result : defaults;
        }

        function isHexColor(v) {
            return typeof v === 'string' && /^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$/.test(v.trim());
        }

        function normalizeColor(v, fallback) {
            return isHexColor(v) ? v.trim() : fallback;
        }

        function normalizeProperties(props) {
            if (!props) return {};
            if (typeof props === 'object') {
                try {
                    return JSON.parse(JSON.stringify(props));
                } catch {
                    return {};
                }
            }
            if (typeof props !== 'string') return {};

            var text = props.trim();
            if (!text) return {};
            try {
                var obj = JSON.parse(text);
                return obj && typeof obj === 'object' ? obj : {};
            } catch {
                return {};
            }
        }

        function getAllFeatures() {
            var fc = draw && typeof draw.getAll === 'function' ? draw.getAll() : { type: 'FeatureCollection', features: [] };
            return Array.isArray(fc.features) ? fc.features : [];
        }

        function sanitizeFeature(feature) {
            if (!feature || !feature.geometry) return null;
            return {
                type: 'Feature',
                properties: normalizeProperties(feature.properties),
                geometry: feature.geometry
            };
        }

        function setFeaturePropertySafe(featureId, key, value) {
            if (!draw || !featureId) return;
            try {
                draw.setFeatureProperty(featureId, key, value);
            } catch {
                // ignore stale ids
            }
        }

        function applyPointStyle(featureId, color) {
            setFeaturePropertySafe(featureId, 'marker-color', color);
            setFeaturePropertySafe(featureId, 'pointColor', color);
        }

        function applyLineStyle(featureId, color) {
            setFeaturePropertySafe(featureId, 'stroke', color);
            setFeaturePropertySafe(featureId, 'lineColor', color);
        }

        function applyPolygonStyle(featureId, stroke, fill) {
            var opacity = palette.fillOpacityPercent / 100;
            setFeaturePropertySafe(featureId, 'stroke', stroke);
            setFeaturePropertySafe(featureId, 'lineColor', stroke);
            setFeaturePropertySafe(featureId, 'fill', fill);
            setFeaturePropertySafe(featureId, 'fillColor', fill);
            setFeaturePropertySafe(featureId, 'fill-opacity', opacity);
            setFeaturePropertySafe(featureId, 'fillOpacity', opacity);
        }

        function applyStyleToFeature(feature, overwrite) {
            if (!feature || !feature.id || !feature.geometry || !feature.geometry.type) return;
            var props = normalizeProperties(feature.properties);
            var type = feature.geometry.type;

            if (type === 'Point') {
                var pExisting = props['marker-color'] || props.pointColor || props.stroke;
                var pColor = overwrite ? palette.pointColor : normalizeColor(pExisting, palette.pointColor);
                applyPointStyle(feature.id, pColor);
                return;
            }

            if (type === 'LineString') {
                var lExisting = props.stroke || props.lineColor;
                var lColor = overwrite ? palette.lineColor : normalizeColor(lExisting, palette.lineColor);
                applyLineStyle(feature.id, lColor);
                return;
            }

            if (type === 'Polygon') {
                var sExisting = props.stroke || props.lineColor;
                var fExisting = props.fill || props.fillColor;
                var stroke = overwrite ? palette.lineColor : normalizeColor(sExisting, palette.lineColor);
                var fill = overwrite ? palette.fillColor : normalizeColor(fExisting, palette.fillColor);
                applyPolygonStyle(feature.id, stroke, fill);
            }
        }

        function applyStyleToTargets(kind) {
            var all = getAllFeatures();
            if (all.length === 0) return;

            var selectedIds = new Set(draw && typeof draw.getSelectedIds === 'function' ? draw.getSelectedIds() : []);
            var targets = selectedIds.size > 0 ? all.filter(function (f) { return selectedIds.has(f.id); }) : all;

            targets.forEach(function (f) {
                var gType = f && f.geometry && f.geometry.type;
                if (kind === 'point' && gType === 'Point') applyStyleToFeature(f, true);
                if (kind === 'line' && gType === 'LineString') applyStyleToFeature(f, true);
                if (kind === 'fill' && gType === 'Polygon') applyStyleToFeature(f, true);
            });

            enhanceDrawLayerColors();
            refresh();
        }

        function syncPaletteFromFeatures() {
            var all = getAllFeatures().map(sanitizeFeature).filter(Boolean);
            var firstPoint = all.find(function (f) { return f.geometry && f.geometry.type === 'Point'; });
            var firstLine = all.find(function (f) { return f.geometry && f.geometry.type === 'LineString'; });
            var firstPolygon = all.find(function (f) { return f.geometry && f.geometry.type === 'Polygon'; });

            if (firstPoint) {
                var pProps = firstPoint.properties || {};
                var pColor = pProps['marker-color'] || pProps.pointColor || pProps.stroke;
                if (isHexColor(pColor)) palette.pointColor = pColor;
            }
            if (firstLine) {
                var lProps = firstLine.properties || {};
                var lColor = lProps.stroke || lProps.lineColor;
                if (isHexColor(lColor)) palette.lineColor = lColor;
            }
            if (firstPolygon) {
                var fProps = firstPolygon.properties || {};
                var stroke = fProps.stroke || fProps.lineColor;
                var fill = fProps.fill || fProps.fillColor;
                var opacity = fProps['fill-opacity'];
                if (opacity === undefined || opacity === null) opacity = fProps.fillOpacity;

                if (isHexColor(stroke)) palette.lineColor = stroke;
                if (isHexColor(fill)) palette.fillColor = fill;

                var n = Number(opacity);
                if (Number.isFinite(n)) {
                    palette.fillOpacityPercent = Math.max(0, Math.min(100, Math.round(n * 100)));
                }
            }
            notifyPalette();
        }

        function ensureAllFeatureStyles() {
            getAllFeatures().forEach(function (f) { applyStyleToFeature(f, false); });
        }

        function getFeatureLabel(feature) {
            var props = normalizeProperties(feature && feature.properties);
            for (var i = 0; i < labelPriorityFields.length; i++) {
                var value = props[labelPriorityFields[i]];
                if (value === null || value === undefined) continue;
                var text = String(value).trim();
                if (text) return text;
            }

            var fallbacks = ['label', 'NAME', 'name', 'SZSQ', 'SZZ', 'SZQX', 'title', 'alias', 'text'];
            for (var j = 0; j < fallbacks.length; j++) {
                var v = props[fallbacks[j]];
                if (v === null || v === undefined) continue;
                var fallbackText = String(v).trim();
                if (fallbackText) return fallbackText;
            }
            return '';
        }

        function ringCentroid(ring) {
            if (!Array.isArray(ring) || ring.length < 3) return null;

            var area2 = 0;
            var cx = 0;
            var cy = 0;

            for (var i = 0; i < ring.length; i++) {
                var p1 = ring[i];
                var p2 = ring[(i + 1) % ring.length];
                if (!Array.isArray(p1) || !Array.isArray(p2)) continue;

                var x1 = Number(p1[0]);
                var y1 = Number(p1[1]);
                var x2 = Number(p2[0]);
                var y2 = Number(p2[1]);
                if (!Number.isFinite(x1) || !Number.isFinite(y1) || !Number.isFinite(x2) || !Number.isFinite(y2)) continue;

                var cross = x1 * y2 - x2 * y1;
                area2 += cross;
                cx += (x1 + x2) * cross;
                cy += (y1 + y2) * cross;
            }

            if (Math.abs(area2) < 1e-12) return null;
            return [cx / (3 * area2), cy / (3 * area2)];
        }

        function bboxCenter(coords) {
            var points = [];

            function collect(v) {
                if (!Array.isArray(v)) return;
                if (v.length >= 2 && typeof v[0] === 'number' && typeof v[1] === 'number') {
                    points.push(v);
                    return;
                }
                v.forEach(collect);
            }

            collect(coords);
            if (points.length === 0) return null;

            var minLng = points[0][0], maxLng = points[0][0], minLat = points[0][1], maxLat = points[0][1];
            points.forEach(function (p) {
                minLng = Math.min(minLng, p[0]);
                maxLng = Math.max(maxLng, p[0]);
                minLat = Math.min(minLat, p[1]);
                maxLat = Math.max(maxLat, p[1]);
            });

            return [(minLng + maxLng) / 2, (minLat + maxLat) / 2];
        }

        function polygonCenter(coords) {
            var outerRing = Array.isArray(coords) ? coords[0] : null;
            return ringCentroid(outerRing) || bboxCenter(coords);
        }

        function multiPolygonCenter(coords) {
            if (!Array.isArray(coords) || coords.length === 0) return null;
            return polygonCenter(coords[0]) || bboxCenter(coords);
        }

        function getLabelAnchorPoint(feature) {
            var geometry = feature && feature.geometry;
            if (!geometry || !geometry.type) return null;

            switch (geometry.type) {
                case 'Point':
                    return Array.isArray(geometry.coordinates) ? geometry.coordinates : null;
                case 'LineString':
                    return bboxCenter(geometry.coordinates);
                case 'Polygon':
                    return polygonCenter(geometry.coordinates);
                case 'MultiPoint':
                case 'MultiLineString':
                    return bboxCenter(geometry.coordinates);
                case 'MultiPolygon':
                    return multiPolygonCenter(geometry.coordinates);
                default:
                    return null;
            }
        }

        function getLabelFeatures() {
            var features = getAllFeatures().map(sanitizeFeature).filter(Boolean);
            var pointFeatures = [];
            var lineFeatures = [];

            features.forEach(function (feature) {
                var label = getFeatureLabel(feature);
                if (!label) return;

                var geometry = feature.geometry;
                if (geometry && (geometry.type === 'LineString' || geometry.type === 'MultiLineString')) {
                    lineFeatures.push({
                        type: 'Feature',
                        properties: { label: label },
                        geometry: geometry
                    });
                    return;
                }

                var anchor = getLabelAnchorPoint(feature);
                if (!anchor || !Array.isArray(anchor) || anchor.length < 2) return;

                pointFeatures.push({
                    type: 'Feature',
                    properties: { label: label },
                    geometry: {
                        type: 'Point',
                        coordinates: anchor
                    }
                });
            });

            return { pointFeatures: pointFeatures, lineFeatures: lineFeatures };
        }

        function ensureLabelLayer() {
            if (!map || !map.isStyleLoaded()) return;

            if (!map.getSource('draw-label-point-source')) {
                map.addSource('draw-label-point-source', {
                    type: 'geojson',
                    data: { type: 'FeatureCollection', features: [] }
                });
            }

            if (!map.getSource('draw-label-line-source')) {
                map.addSource('draw-label-line-source', {
                    type: 'geojson',
                    data: { type: 'FeatureCollection', features: [] }
                });
            }

            if (!map.getLayer('draw-label-line-layer')) {
                map.addLayer({
                    id: 'draw-label-line-layer',
                    type: 'symbol',
                    source: 'draw-label-line-source',
                    layout: {
                        'symbol-placement': 'line',
                        'text-field': ['get', 'label'],
                        'text-size': 15,
                        'text-font': ['Open Sans Semibold', 'Arial Unicode MS Bold'],
                        'text-allow-overlap': true,
                        'text-ignore-placement': true,
                        'text-keep-upright': true,
                        'text-rotation-alignment': 'map',
                        'symbol-spacing': 260
                    },
                    paint: {
                        'text-color': '#0f172a',
                        'text-halo-color': '#ffffff',
                        'text-halo-width': 1.4
                    }
                });
            }

            if (!map.getLayer('draw-label-point-layer')) {
                map.addLayer({
                    id: 'draw-label-point-layer',
                    type: 'symbol',
                    source: 'draw-label-point-source',
                    layout: {
                        'text-field': ['get', 'label'],
                        'text-size': 13,
                        'text-font': ['Open Sans Semibold', 'Arial Unicode MS Bold'],
                        'text-offset': [0, 0],
                        'text-anchor': 'center',
                        'text-allow-overlap': true,
                        'text-ignore-placement': true
                    },
                    paint: {
                        'text-color': '#0f172a',
                        'text-halo-color': '#ffffff',
                        'text-halo-width': 1.2
                    }
                });
            }
        }

        function syncLabelLayer() {
            if (!map || !map.isStyleLoaded()) return;
            ensureLabelLayer();

            var pointSource = map.getSource('draw-label-point-source');
            var lineSource = map.getSource('draw-label-line-source');
            if (!pointSource || typeof pointSource.setData !== 'function') return;
            if (!lineSource || typeof lineSource.setData !== 'function') return;

            var labels = getLabelFeatures();
            pointSource.setData({ type: 'FeatureCollection', features: labels.pointFeatures });
            lineSource.setData({ type: 'FeatureCollection', features: labels.lineFeatures });
        }

        function queueLabelSync() {
            if (labelSyncQueued) return;
            labelSyncQueued = true;
            requestAnimationFrame(function () {
                labelSyncQueued = false;
                syncLabelLayer();
            });
        }

        function explodeGeometryToFeatures(geometry, properties) {
            if (!geometry || !geometry.type) return [];
            var props = properties || {};

            switch (geometry.type) {
                case 'Point':
                case 'LineString':
                case 'Polygon':
                    return [{ type: 'Feature', properties: props, geometry: geometry }];
                case 'MultiPoint':
                    return (geometry.coordinates || []).map(function (c) {
                        return { type: 'Feature', properties: props, geometry: { type: 'Point', coordinates: c } };
                    });
                case 'MultiLineString':
                    return (geometry.coordinates || []).map(function (c) {
                        return { type: 'Feature', properties: props, geometry: { type: 'LineString', coordinates: c } };
                    });
                case 'MultiPolygon':
                    return (geometry.coordinates || []).map(function (c) {
                        return { type: 'Feature', properties: props, geometry: { type: 'Polygon', coordinates: c } };
                    });
                case 'GeometryCollection':
                    return (geometry.geometries || []).flatMap(function (g) { return explodeGeometryToFeatures(g, props); });
                default:
                    return [];
            }
        }

        function explodeFeatureToFeatures(feature) {
            if (!feature || feature.type !== 'Feature') return [];
            return explodeGeometryToFeatures(feature.geometry, normalizeProperties(feature.properties));
        }

        function readInitialRaw() {
            var params = new URLSearchParams(global.location.search || '');
            var key = (params.get('geojsonKey') || '').trim();
            var selectorKey = (params.get('selectorKey') || '').trim();
            var direct = (params.get('geojson') || '').trim();
            var selectorValue = (params.get('selectorValue') || '').trim();

            if (key && global.sessionStorage) {
                var s = (sessionStorage.getItem(key) || '').trim();
                if (s) return s;
            }

            if (key && global.localStorage) {
                var l = (localStorage.getItem(key) || '').trim();
                if (l) return l;
            }

            if (selectorKey && global.localStorage) {
                var c = (localStorage.getItem(selectorKey) || '').trim();
                if (c) return c;
            }

            if (direct) return direct;
            if (selectorValue) return selectorValue;
            return (initGeoJson || '').trim();
        }

        function normalizeRawGeoJson(raw) {
            if (!raw) return '';
            var text = raw.trim();
            if (!text) return '';

            if ((text.startsWith("'") && text.endsWith("'")) || (text.startsWith('"') && text.endsWith('"'))) {
                text = text.substring(1, text.length - 1).trim();
            }

            if (text.includes('%7B') || text.includes('%7D') || text.includes('%22')) {
                try {
                    text = decodeURIComponent(text);
                } catch {
                    // ignore
                }
            }
            return text;
        }

        function parseInitial(raw) {
            if (!raw) return { features: [] };
            try {
                var obj = JSON.parse(normalizeRawGeoJson(raw));
                if (!obj || !obj.type) return { features: [] };

                if (obj.type === 'FeatureCollection') {
                    var feats = (obj.features || []).flatMap(explodeFeatureToFeatures);
                    return { features: feats };
                }
                if (obj.type === 'Feature') {
                    return { features: explodeFeatureToFeatures(obj) };
                }
                if (obj.type === 'GeometryCollection') {
                    return { features: explodeGeometryToFeatures(obj) };
                }

                return { features: explodeGeometryToFeatures(obj) };
            } catch {
                return { features: [] };
            }
        }

        function fitToFeatures(features) {
            if (!map || !features || features.length === 0) return;
            var points = [];

            function collect(coords) {
                if (!Array.isArray(coords) || coords.length === 0) return;
                if (typeof coords[0] === 'number' && typeof coords[1] === 'number') {
                    points.push(coords);
                    return;
                }
                coords.forEach(collect);
            }

            features.forEach(function (f) { collect(f.geometry && f.geometry.coordinates); });
            if (points.length === 0) return;

            var minLng = points[0][0], maxLng = points[0][0], minLat = points[0][1], maxLat = points[0][1];
            points.forEach(function (p) {
                minLng = Math.min(minLng, p[0]);
                maxLng = Math.max(maxLng, p[0]);
                minLat = Math.min(minLat, p[1]);
                maxLat = Math.max(maxLat, p[1]);
            });

            map.fitBounds([[minLng, minLat], [maxLng, maxLat]], { padding: 60, duration: 0 });
        }

        function enhanceDrawLayerColors() {
            if (!map) return;
            var style = map.getStyle();
            var layers = style && Array.isArray(style.layers) ? style.layers : [];

            var lineColorExpr = ['coalesce', ['get', 'stroke'], ['get', 'lineColor'], palette.lineColor];
            var fillColorExpr = ['coalesce', ['get', 'fill'], ['get', 'fillColor'], palette.fillColor];
            var fillOpacityExpr = ['to-number', ['coalesce', ['get', 'fill-opacity'], ['get', 'fillOpacity'], palette.fillOpacityPercent / 100]];
            var pointColorExpr = ['coalesce', ['get', 'marker-color'], ['get', 'pointColor'], ['get', 'stroke'], palette.pointColor];

            layers.forEach(function (layer) {
                var id = layer.id || '';
                try {
                    if (id.includes('gl-draw-line')) {
                        map.setPaintProperty(id, 'line-color', lineColorExpr);
                        map.setPaintProperty(id, 'line-width', 3);
                    }
                    if (id.includes('gl-draw-polygon-fill')) {
                        map.setPaintProperty(id, 'fill-color', fillColorExpr);
                        map.setPaintProperty(id, 'fill-opacity', fillOpacityExpr);
                    }
                    if (id.includes('gl-draw-polygon-stroke')) {
                        map.setPaintProperty(id, 'line-color', lineColorExpr);
                        map.setPaintProperty(id, 'line-width', 3);
                    }
                    if (id.includes('gl-draw-point') && layer.type === 'circle') {
                        map.setPaintProperty(id, 'circle-color', pointColorExpr);
                        map.setPaintProperty(id, 'circle-radius', 5);
                        map.setPaintProperty(id, 'circle-stroke-color', '#ffffff');
                        map.setPaintProperty(id, 'circle-stroke-width', 1.5);
                    }
                } catch {
                    // ignore unsupported layer paint properties
                }
            });
        }

        function buildExportObject() {
            var features = getAllFeatures().map(sanitizeFeature).filter(Boolean);
            if (features.length === 0) throw new Error('请先绘制图形');
            return { type: 'FeatureCollection', features: features };
        }

        function getSelectableDrawLayerIds() {
            if (!map) return [];
            var style = map.getStyle();
            var layers = style && Array.isArray(style.layers) ? style.layers : [];
            return layers
                .map(function (layer) { return layer && layer.id ? layer.id : ''; })
                .filter(function (id) {
                    if (!id || id.indexOf('gl-draw-') < 0) return false;
                    if (id.indexOf('vertex') >= 0 || id.indexOf('midpoint') >= 0) return false;
                    return true;
                });
        }

        function pickFeatureIdAt(point) {
            if (!map || !point) return null;
            var layers = getSelectableDrawLayerIds();
            var hits = [];
            try {
                hits = layers.length > 0
                    ? map.queryRenderedFeatures(point, { layers: layers })
                    : map.queryRenderedFeatures(point);
            } catch {
                hits = [];
            }

            var featureIdSet = new Set(getAllFeatures().map(function (f) { return String(f && f.id); }));

            function collectCandidateIds(hit) {
                var candidates = [];
                if (!hit) return candidates;

                if (hit.id !== undefined && hit.id !== null) {
                    candidates.push(String(hit.id));
                }

                var props = hit.properties || {};
                ['id', 'feature_id', 'user_id'].forEach(function (k) {
                    var v = props[k];
                    if (v === undefined || v === null) return;
                    candidates.push(String(v));
                });
                return candidates;
            }

            for (var j = 0; j < hits.length; j++) {
                var strictCandidates = collectCandidateIds(hits[j]);
                for (var k = 0; k < strictCandidates.length; k++) {
                    if (featureIdSet.has(strictCandidates[k])) return strictCandidates[k];
                }
            }

            for (var i = 0; i < hits.length; i++) {
                var h = hits[i] || {};
                var fallbackCandidates = collectCandidateIds(h);
                for (var n = 0; n < fallbackCandidates.length; n++) {
                    var id = fallbackCandidates[n];
                    if (id && id.trim() !== '') return id;
                }
            }
            return null;
        }

        function applyChineseLabels() {
            if (!map) return;
            var style = map.getStyle();
            if (!style || !Array.isArray(style.layers)) return;

            style.layers.forEach(function (layer) {
                if (!layer || layer.type !== 'symbol') return;
                if ((layer.id || '').startsWith('draw-label-')) return;
                if ((layer.id || '').indexOf('gl-draw-') >= 0) return;
                if (layer.source && layer.source !== 'composite') return;
                var textField = map.getLayoutProperty(layer.id, 'text-field');
                if (!textField) return;

                try {
                    map.setLayoutProperty(layer.id, 'text-field', [
                        'coalesce',
                        ['get', 'name_zh-Hans'],
                        ['get', 'name_zh'],
                        ['get', 'name'],
                        ['get', 'NAME'],
                        ''
                    ]);
                } catch {
                    // ignore layers that don't support text-field expressions
                }
            });
        }

        function getFeatureCenterPoint(feature) {
            if (!feature || !feature.geometry) return null;
            return getLabelAnchorPoint(feature);
        }

        function findNearestFeatureId(anchor, candidates) {
            if (!anchor || !Array.isArray(anchor) || anchor.length < 2) return null;
            if (!Array.isArray(candidates) || candidates.length === 0) return null;

            var ax = Number(anchor[0]);
            var ay = Number(anchor[1]);
            if (!Number.isFinite(ax) || !Number.isFinite(ay)) return null;

            var points = candidates.filter(function (f) {
                return f && f.geometry && f.geometry.type === 'Point';
            });
            var pool = points.length > 0 ? points : candidates;

            var nearestId = null;
            var nearestD2 = Infinity;
            pool.forEach(function (f) {
                var center = getFeatureCenterPoint(f);
                if (!center || center.length < 2) return;
                var dx = Number(center[0]) - ax;
                var dy = Number(center[1]) - ay;
                if (!Number.isFinite(dx) || !Number.isFinite(dy)) return;
                var d2 = dx * dx + dy * dy;
                if (d2 < nearestD2) {
                    nearestD2 = d2;
                    nearestId = f.id;
                }
            });
            return nearestId;
        }

        function bindInteractionEvents() {
            if (!map || !draw || interactionBound) return;
            interactionBound = true;

            var onMapContextMenu = typeof options.onMapContextMenu === 'function' ? options.onMapContextMenu : null;
            var onMapDblClick = typeof options.onMapDblClick === 'function' ? options.onMapDblClick : null;

            if (onMapContextMenu) {
                map.on('contextmenu', function (evt) {
                    var pickedId = pickFeatureIdAt(evt.point);
                    if (pickedId) {
                        lastPickedFeatureId = String(pickedId);
                        try {
                            draw.changeMode('simple_select', { featureIds: [pickedId] });
                            queueLabelSync();
                        } catch {
                            // ignore select failure
                        }
                    }
                    onMapContextMenu({
                        event: evt,
                        point: evt.point,
                        clientX: evt.originalEvent && evt.originalEvent.clientX,
                        clientY: evt.originalEvent && evt.originalEvent.clientY,
                        pickedId: pickedId
                    });
                });
            }

            if (onMapDblClick) {
                map.doubleClickZoom.disable();
                map.on('dblclick', function (evt) {
                    var pickedId = pickFeatureIdAt(evt.point);
                    if (pickedId) {
                        lastPickedFeatureId = String(pickedId);
                        try {
                            draw.changeMode('simple_select', { featureIds: [pickedId] });
                            queueLabelSync();
                        } catch {
                            // ignore select failure
                        }
                    }
                    onMapDblClick({
                        event: evt,
                        point: evt.point,
                        clientX: evt.originalEvent && evt.originalEvent.clientX,
                        clientY: evt.originalEvent && evt.originalEvent.clientY,
                        pickedId: pickedId
                    });
                });
            }
        }

        function findFeatureById(featureId) {
            if (!featureId) return null;
            var all = getAllFeatures();
            for (var i = 0; i < all.length; i++) {
                if (String(all[i] && all[i].id) === String(featureId)) return all[i];
            }
            return null;
        }

        function refresh() {
            try {
                buildExportObject();
            } catch {
                // ignore empty map in editing mode
            }
            syncLabelLayer();
        }

        function init() {
            if (!accessToken) {
                onError('缺少 Mapbox Token，请检查系统配置');
                return;
            }

            mapboxgl.accessToken = accessToken;
            if (typeof mapboxgl.setTelemetryEnabled === 'function') {
                mapboxgl.setTelemetryEnabled(false);
            }

            map = new mapboxgl.Map({
                container: mapContainerId,
                style: 'mapbox://styles/mapbox/streets-v11',
                center: [120.1551, 30.2741],
                zoom: 11
            });

            map.addControl(new mapboxgl.NavigationControl(), 'top-left');
            map.addControl(new mapboxgl.GeolocateControl({
                positionOptions: { enableHighAccuracy: true },
                trackUserLocation: false,
                showUserHeading: true
            }), 'top-left');


            draw = new MapboxDraw({
                displayControlsDefault: false,
                defaultMode: 'simple_select',
                controls: { point: false, line_string: false, polygon: false, trash: false }
            });
            map.addControl(draw, 'top-left');

            map.on('draw.create', function (evt) {
                (evt && evt.features ? evt.features : []).forEach(function (f) { applyStyleToFeature(f, false); });
                enhanceDrawLayerColors();
                refresh();
            });
            map.on('draw.update', refresh);
            map.on('draw.delete', refresh);
            map.on('draw.selectionchange', function (evt) {
                var first = evt && Array.isArray(evt.features) && evt.features.length > 0 ? evt.features[0] : null;
                lastPickedFeatureId = first && first.id !== undefined && first.id !== null ? String(first.id) : null;
                queueLabelSync();
            });
            map.on('draw.modechange', queueLabelSync);
            map.on('draw.render', queueLabelSync);
            map.on('click', function (evt) {
                if (!draw || typeof draw.getMode !== 'function') return;
                var mode = draw.getMode();
                if (mode !== 'simple_select' && mode !== 'direct_select') return;
                var pickedId = pickFeatureIdAt(evt.point);
                if (!pickedId) return;
                lastPickedFeatureId = String(pickedId);
                try {
                    draw.changeMode('simple_select', { featureIds: [pickedId] });
                    queueLabelSync();
                } catch {
                    // ignore
                }
            });

            map.on('load', function () {
                bindInteractionEvents();
                applyChineseLabels();
                enhanceDrawLayerColors();
                ensureLabelLayer();

                var parsed = parseInitial(readInitialRaw());
                if (parsed.features.length > 0) {
                    draw.add({ type: 'FeatureCollection', features: parsed.features });
                    syncPaletteFromFeatures();
                    ensureAllFeatureStyles();
                    enhanceDrawLayerColors();
                    fitToFeatures(parsed.features);
                } else {
                    notifyPalette();
                }

                refresh();
                map.once('idle', syncLabelLayer);
            });

            map.on('style.load', function () {
                applyChineseLabels();
            });
        }

        return {
            init: init,
            drawPoint: function () { if (draw) draw.changeMode('draw_point'); },
            drawLine: function () { if (draw) draw.changeMode('draw_line_string'); },
            drawPolygon: function () { if (draw) draw.changeMode('draw_polygon'); },
            removeSelected: function () {
                if (!draw || typeof draw.getSelectedIds !== 'function') return;
                var allBefore = getAllFeatures();
                var selectedIds = draw.getSelectedIds() || [];
                if ((!Array.isArray(selectedIds) || selectedIds.length === 0) && lastPickedFeatureId) {
                    selectedIds = [lastPickedFeatureId];
                }

                var anchor = null;
                if (Array.isArray(selectedIds) && selectedIds.length > 0) {
                    var firstSelected = allBefore.find(function (f) { return String(f.id) === String(selectedIds[0]); });
                    anchor = getFeatureCenterPoint(firstSelected);
                }

                if (Array.isArray(selectedIds) && selectedIds.length > 0) {
                    draw.delete(selectedIds);
                    var remain = getAllFeatures();
                    var nextId = findNearestFeatureId(anchor, remain);
                    if (nextId !== null && nextId !== undefined) {
                        // draw.delete may update state asynchronously; defer reselect to next frame.
                        requestAnimationFrame(function () {
                            try {
                                draw.changeMode('simple_select', { featureIds: [nextId] });
                                lastPickedFeatureId = String(nextId);
                                queueLabelSync();
                            } catch {
                                lastPickedFeatureId = null;
                            }
                        });
                    } else {
                        lastPickedFeatureId = null;
                    }
                } else {
                    draw.trash();
                }
                refresh();
            },
            clearAll: function () {
                var all = getAllFeatures();
                all.forEach(function (f) { draw.delete(f.id); });
                refresh();
            },
            setPointColor: function (color) { if (isHexColor(color)) palette.pointColor = color; applyStyleToTargets('point'); notifyPalette(); },
            setLineColor: function (color) { if (isHexColor(color)) palette.lineColor = color; applyStyleToTargets('line'); notifyPalette(); },
            setFillColor: function (color) { if (isHexColor(color)) palette.fillColor = color; applyStyleToTargets('fill'); notifyPalette(); },
            setFillOpacityPercent: function (percent) {
                var n = Number(percent);
                if (Number.isFinite(n)) {
                    palette.fillOpacityPercent = Math.max(0, Math.min(100, Math.round(n)));
                }
                applyStyleToTargets('fill');
                notifyPalette();
            },
            getSelectedCount: function () {
                if (!draw || typeof draw.getSelectedIds !== 'function') return 0;
                var selected = draw.getSelectedIds() || [];
                if (Array.isArray(selected) && selected.length > 0) return selected.length;
                return lastPickedFeatureId ? 1 : 0;
            },
            hasActiveSelection: function () {
                if (!draw || typeof draw.getSelectedIds !== 'function') return !!lastPickedFeatureId;
                var selected = draw.getSelectedIds() || [];
                return (Array.isArray(selected) && selected.length > 0) || !!lastPickedFeatureId;
            },
            selectFeatureAt: function (point) {
                if (!draw || !point || !map) return null;
                var pickedId = pickFeatureIdAt(point);
                if (!pickedId) return null;
                lastPickedFeatureId = String(pickedId);
                try {
                    draw.changeMode('simple_select', { featureIds: [pickedId] });
                    queueLabelSync();
                } catch {
                    return null;
                }
                return pickedId;
            },
            getSelectedLabel: function () {
                if (!draw || typeof draw.getSelectedIds !== 'function') return '';
                var selected = draw.getSelectedIds();
                if (!selected || selected.length === 0) return '';
                var feature = findFeatureById(selected[0]);
                return getFeatureLabel(feature);
            },
            setSelectedLabel: function (label) {
                if (!draw || typeof draw.getSelectedIds !== 'function') return;
                var selected = draw.getSelectedIds();
                if (!selected || selected.length === 0) return;
                var text = label === null || label === undefined ? '' : String(label).trim();
                selected.forEach(function (id) {
                    setFeaturePropertySafe(id, 'label', text);
                });
                refresh();
            },
            getSelectedProperties: function () {
                if (!draw || typeof draw.getSelectedIds !== 'function') return {};
                var selected = draw.getSelectedIds();
                if (!selected || selected.length === 0) {
                    if (!lastPickedFeatureId) return {};
                    var pickedFeature = findFeatureById(lastPickedFeatureId);
                    return normalizeProperties(pickedFeature && pickedFeature.properties);
                }
                var feature = findFeatureById(selected[0]);
                return normalizeProperties(feature && feature.properties);
            },
            setSelectedProperties: function (nextProps) {
                if (!draw || typeof draw.getSelectedIds !== 'function') return;
                var selected = draw.getSelectedIds();
                if ((!selected || selected.length === 0) && lastPickedFeatureId) {
                    selected = [lastPickedFeatureId];
                }
                if (!selected || selected.length === 0) return;

                var sanitized = normalizeProperties(nextProps);
                selected.forEach(function (id) {
                    var feature = findFeatureById(id);
                    var existing = normalizeProperties(feature && feature.properties);
                    Object.keys(existing).forEach(function (k) {
                        if (!Object.prototype.hasOwnProperty.call(sanitized, k)) {
                            setFeaturePropertySafe(id, k, '');
                        }
                    });
                    Object.keys(sanitized).forEach(function (k) {
                        setFeaturePropertySafe(id, k, sanitized[k]);
                    });
                });
                refresh();
            },
            getLabelPriorityFields: function () {
                return labelPriorityFields.slice();
            },
            setLabelPriorityFields: function (fields) {
                labelPriorityFields = normalizeLabelPriorityFields(fields);
                queueLabelSync();
            },
            getExportGeoJson: function () {
                return JSON.stringify(buildExportObject());
            }
        };
    }

    function createDisplayLayerManager(options) {
        var map = options.map;
        var sourceId = options.sourceId || 'geometry-source';
        var layerPrefix = options.layerPrefix || 'geometry';

        var fillLayerId = layerPrefix + '-fill-layer';
        var lineLayerId = layerPrefix + '-line-layer';
        var pointLayerId = layerPrefix + '-point-layer';
        var labelLayerId = layerPrefix + '-label-layer';
        var lineLabelLayerId = layerPrefix + '-line-label-layer';

        var data = { type: 'FeatureCollection', features: [] };

        function normalizeProperties(props) {
            if (!props) return {};
            if (typeof props === 'object') {
                try {
                    return JSON.parse(JSON.stringify(props));
                } catch {
                    return {};
                }
            }
            if (typeof props !== 'string') return {};

            var text = props.trim();
            if (!text) return {};
            try {
                var obj = JSON.parse(text);
                return obj && typeof obj === 'object' ? obj : {};
            } catch {
                return {};
            }
        }

        function normalizeRawGeoJson(raw) {
            if (!raw || typeof raw !== 'string') return '';
            var text = raw.trim();
            if (!text) return '';

            if ((text.startsWith("'") && text.endsWith("'")) || (text.startsWith('"') && text.endsWith('"'))) {
                text = text.substring(1, text.length - 1).trim();
            }

            if (text.includes('%7B') || text.includes('%7D') || text.includes('%22')) {
                try {
                    text = decodeURIComponent(text);
                } catch {
                    // ignore malformed uri component
                }
            }

            return text;
        }

        function explodeGeometryToFeatures(geometry, properties) {
            if (!geometry || !geometry.type) return [];
            var props = properties || {};

            switch (geometry.type) {
                case 'Point':
                case 'LineString':
                case 'Polygon':
                    return [{ type: 'Feature', properties: props, geometry: geometry }];
                case 'MultiPoint':
                    return (geometry.coordinates || []).map(function (c) {
                        return { type: 'Feature', properties: props, geometry: { type: 'Point', coordinates: c } };
                    });
                case 'MultiLineString':
                    return (geometry.coordinates || []).map(function (c) {
                        return { type: 'Feature', properties: props, geometry: { type: 'LineString', coordinates: c } };
                    });
                case 'MultiPolygon':
                    return (geometry.coordinates || []).map(function (c) {
                        return { type: 'Feature', properties: props, geometry: { type: 'Polygon', coordinates: c } };
                    });
                case 'GeometryCollection':
                    return (geometry.geometries || []).flatMap(function (g) { return explodeGeometryToFeatures(g, props); });
                default:
                    return [];
            }
        }

        function explodeFeatureToFeatures(feature, fallbackProperties) {
            if (!feature || feature.type !== 'Feature') return [];
            var props = {
                ...(fallbackProperties || {}),
                ...normalizeProperties(feature.properties)
            };
            return explodeGeometryToFeatures(feature.geometry, props);
        }

        function rowToFeatures(row) {
            var geometryId = row.id;
            var fallbackName = row.alias || row.name || ('图形' + geometryId);
            var fallbackProperties = {
                __geometryId: geometryId,
                __name: fallbackName
            };

            var obj;
            try {
                obj = JSON.parse(normalizeRawGeoJson(row.geoJson || row.jsonData));
            } catch {
                return [];
            }

            if (!obj || !obj.type) return [];

            var features;
            if (obj.type === 'FeatureCollection') {
                features = (obj.features || []).flatMap(function (f) { return explodeFeatureToFeatures(f, fallbackProperties); });
            } else if (obj.type === 'Feature') {
                features = explodeFeatureToFeatures(obj, fallbackProperties);
            } else {
                features = explodeGeometryToFeatures(obj, fallbackProperties);
            }

            return features.map(function (f) {
                var props = normalizeProperties(f.properties);
                props.__geometryId = geometryId;
                var labelText = props.label || props.name || props.NAME || props.title || props.Title || props.alias || props.Alias;
                if (labelText === undefined || labelText === null || String(labelText).trim() === '') {
                    props.label = fallbackName;
                }
                return {
                    type: 'Feature',
                    properties: props,
                    geometry: f.geometry
                };
            });
        }

        function ensureSource() {
            var source = map.getSource(sourceId);
            if (source) {
                source.setData(data);
            } else {
                map.addSource(sourceId, {
                    type: 'geojson',
                    data: data
                });
            }
        }

        function ensureLayers() {
            if (!map.getLayer(fillLayerId)) {
                map.addLayer({
                    id: fillLayerId,
                    type: 'fill',
                    source: sourceId,
                    filter: ['any', ['==', ['geometry-type'], 'Polygon'], ['==', ['geometry-type'], 'MultiPolygon']],
                    paint: {
                        'fill-color': ['coalesce', ['get', 'fill'], ['get', 'fillColor'], '#60a5fa'],
                        'fill-opacity': ['to-number', ['coalesce', ['get', 'fill-opacity'], ['get', 'fillOpacity'], 0.25]]
                    }
                });
            }

            if (!map.getLayer(lineLayerId)) {
                map.addLayer({
                    id: lineLayerId,
                    type: 'line',
                    source: sourceId,
                    filter: ['any', ['==', ['geometry-type'], 'LineString'], ['==', ['geometry-type'], 'MultiLineString'], ['==', ['geometry-type'], 'Polygon'], ['==', ['geometry-type'], 'MultiPolygon']],
                    paint: {
                        'line-color': ['coalesce', ['get', 'stroke'], ['get', 'lineColor'], '#2563eb'],
                        'line-width': ['to-number', ['coalesce', ['get', 'stroke-width'], ['get', 'lineWidth'], 2.5]],
                        'line-opacity': 0.95
                    }
                });
            }

            if (!map.getLayer(pointLayerId)) {
                map.addLayer({
                    id: pointLayerId,
                    type: 'circle',
                    source: sourceId,
                    filter: ['any', ['==', ['geometry-type'], 'Point'], ['==', ['geometry-type'], 'MultiPoint']],
                    paint: {
                        'circle-color': ['coalesce', ['get', 'marker-color'], ['get', 'pointColor'], '#ef4444'],
                        'circle-radius': 5,
                        'circle-stroke-color': '#ffffff',
                        'circle-stroke-width': 1.2,
                        'circle-opacity': 0.95
                    }
                });
            }

            if (!map.getLayer(labelLayerId)) {
                map.addLayer({
                    id: labelLayerId,
                    type: 'symbol',
                    source: sourceId,
                    filter: ['any', ['==', ['geometry-type'], 'Point'], ['==', ['geometry-type'], 'MultiPoint'], ['==', ['geometry-type'], 'Polygon'], ['==', ['geometry-type'], 'MultiPolygon']],
                    layout: {
                        'text-field': ['coalesce', ['get', 'label'], ['get', 'NAME'], ['get', 'name'], ['get', 'Title'], ['get', 'title'], ['get', 'Alias'], ['get', 'alias'], ['get', '__name'], ''],
                        'text-size': 14,
                        'text-font': ['Open Sans Semibold', 'Arial Unicode MS Bold'],
                        'text-anchor': 'center',
                        'text-offset': [0, 0],
                        'text-allow-overlap': true,
                        'text-ignore-placement': true
                    },
                    paint: {
                        'text-color': '#0f172a',
                        'text-halo-color': '#ffffff',
                        'text-halo-width': 1.2
                    }
                });
            }

            if (!map.getLayer(lineLabelLayerId)) {
                map.addLayer({
                    id: lineLabelLayerId,
                    type: 'symbol',
                    source: sourceId,
                    filter: ['any', ['==', ['geometry-type'], 'LineString'], ['==', ['geometry-type'], 'MultiLineString']],
                    layout: {
                        'symbol-placement': 'line',
                        'text-field': ['coalesce', ['get', 'label'], ['get', 'NAME'], ['get', 'name'], ['get', 'Title'], ['get', 'title'], ['get', 'Alias'], ['get', 'alias'], ['get', '__name'], ''],
                        'text-size': 14,
                        'text-font': ['Open Sans Semibold', 'Arial Unicode MS Bold'],
                        'text-keep-upright': true,
                        'text-allow-overlap': true,
                        'text-ignore-placement': true,
                        'symbol-spacing': 280
                    },
                    paint: {
                        'text-color': '#0f172a',
                        'text-halo-color': '#ffffff',
                        'text-halo-width': 1.2
                    }
                });
            }
        }

        function setVisible(visible) {
            var value = visible ? 'visible' : 'none';
            [fillLayerId, lineLayerId, pointLayerId, labelLayerId, lineLabelLayerId].forEach(function (id) {
                if (map.getLayer(id)) {
                    map.setLayoutProperty(id, 'visibility', value);
                }
            });
        }

        function setVisibleIds(ids) {
            var list = Array.isArray(ids) ? ids : [];
            var filter = ['in', ['to-string', ['get', '__geometryId']], ['literal', list.map(function (id) { return String(id); })]];
            [fillLayerId, lineLayerId, pointLayerId, labelLayerId, lineLabelLayerId].forEach(function (id) {
                if (!map.getLayer(id)) return;
                var baseFilter = map.getFilter(id);
                if (Array.isArray(baseFilter) && baseFilter[0] === 'all') {
                    map.setFilter(id, ['all', baseFilter[1], filter]);
                    return;
                }

                var typeFilter;
                if (id === fillLayerId) {
                    typeFilter = ['any', ['==', ['geometry-type'], 'Polygon'], ['==', ['geometry-type'], 'MultiPolygon']];
                } else if (id === lineLayerId) {
                    typeFilter = ['any', ['==', ['geometry-type'], 'LineString'], ['==', ['geometry-type'], 'MultiLineString'], ['==', ['geometry-type'], 'Polygon'], ['==', ['geometry-type'], 'MultiPolygon']];
                } else if (id === pointLayerId) {
                    typeFilter = ['any', ['==', ['geometry-type'], 'Point'], ['==', ['geometry-type'], 'MultiPoint']];
                } else if (id === labelLayerId) {
                    typeFilter = ['any', ['==', ['geometry-type'], 'Point'], ['==', ['geometry-type'], 'MultiPoint'], ['==', ['geometry-type'], 'Polygon'], ['==', ['geometry-type'], 'MultiPolygon']];
                } else {
                    typeFilter = ['any', ['==', ['geometry-type'], 'LineString'], ['==', ['geometry-type'], 'MultiLineString']];
                }

                map.setFilter(id, ['all', typeFilter, filter]);
            });
        }

        return {
            setDataFromRows: function (rows) {
                var list = Array.isArray(rows) ? rows : [];
                data = {
                    type: 'FeatureCollection',
                    features: list.flatMap(rowToFeatures)
                };
            },
            render: function () {
                if (!map || !map.isStyleLoaded()) return;
                ensureSource();
                ensureLayers();
            },
            setVisible: setVisible,
            setVisibleIds: setVisibleIds,
            getFeatureCount: function () {
                return Array.isArray(data.features) ? data.features.length : 0;
            }
        };
    }

    global.MapGeometryHelper = {
        createEditor: createEditor,
        createDisplayLayerManager: createDisplayLayerManager
    };
})(window);
