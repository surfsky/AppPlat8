(function (global) {
    'use strict';

    /**深拷贝简单对象 */
    function cloneJson(obj) {
        if (obj === null || obj === undefined) return obj;
        try {
            return JSON.parse(JSON.stringify(obj));
        } catch {
            return obj;
        }
    }

    /**比较坐标点 */
    function isSamePoint(a, b) {
        if (!Array.isArray(a) || !Array.isArray(b) || a.length < 2 || b.length < 2) return false;
        return Number(a[0]) === Number(b[0]) && Number(a[1]) === Number(b[1]);
    }

    /**规范 ring 闭合 */
    function normalizeRing(ring) {
        if (!Array.isArray(ring)) return [];
        var points = ring
            .filter(function (p) { return Array.isArray(p) && p.length >= 2; })
            .map(function (p) { return [Number(p[0]), Number(p[1])]; })
            .filter(function (p) { return Number.isFinite(p[0]) && Number.isFinite(p[1]); });

        if (points.length < 3) return [];
        if (!isSamePoint(points[0], points[points.length - 1])) {
            points.push([points[0][0], points[0][1]]);
        }
        return points.length >= 4 ? points : [];
    }

    /**计算 ring 包围盒 */
    function getRingBbox(ring) {
        if (!Array.isArray(ring) || ring.length === 0) return null;
        var minLng = ring[0][0];
        var maxLng = ring[0][0];
        var minLat = ring[0][1];
        var maxLat = ring[0][1];
        ring.forEach(function (p) {
            minLng = Math.min(minLng, p[0]);
            maxLng = Math.max(maxLng, p[0]);
            minLat = Math.min(minLat, p[1]);
            maxLat = Math.max(maxLat, p[1]);
        });
        return { minLng: minLng, minLat: minLat, maxLng: maxLng, maxLat: maxLat };
    }

    /**判断包围盒包含 */
    function bboxContains(outerBbox, innerBbox) {
        if (!outerBbox || !innerBbox) return false;
        return outerBbox.minLng <= innerBbox.minLng
            && outerBbox.minLat <= innerBbox.minLat
            && outerBbox.maxLng >= innerBbox.maxLng
            && outerBbox.maxLat >= innerBbox.maxLat;
    }

    /**计算 ring 面积 */
    function getRingArea(ring) {
        if (!Array.isArray(ring) || ring.length < 4) return 0;
        var area = 0;
        for (var i = 0; i < ring.length - 1; i += 1) {
            var a = ring[i];
            var b = ring[i + 1];
            area += (a[0] * b[1]) - (b[0] * a[1]);
        }
        return Math.abs(area / 2);
    }

    /**获取 ring 内测试点 */
    function getRingSamplePoint(ring) {
        if (!Array.isArray(ring) || ring.length < 2) return null;
        for (var i = 0; i < ring.length - 1; i += 1) {
            var p = ring[i];
            if (Array.isArray(p) && p.length >= 2) return [Number(p[0]), Number(p[1])];
        }
        return null;
    }

    /**判断点是否在 ring 内 */
    function isPointInRing(point, ring) {
        if (!Array.isArray(point) || point.length < 2 || !Array.isArray(ring) || ring.length < 4) return false;
        var x = Number(point[0]);
        var y = Number(point[1]);
        var inside = false;
        for (var i = 0, j = ring.length - 1; i < ring.length; j = i, i += 1) {
            var xi = Number(ring[i][0]);
            var yi = Number(ring[i][1]);
            var xj = Number(ring[j][0]);
            var yj = Number(ring[j][1]);
            var intersect = ((yi > y) !== (yj > y))
                && (x < ((xj - xi) * (y - yi)) / ((yj - yi) || Number.EPSILON) + xi);
            if (intersect) inside = !inside;
        }
        return inside;
    }

    /**判断 ring 是否是另一个 ring 的内洞 */
    function isHoleRing(ring, outerRing) {
        var ringBbox = getRingBbox(ring);
        var outerBbox = getRingBbox(outerRing);
        if (!bboxContains(outerBbox, ringBbox)) return false;
        var sample = getRingSamplePoint(ring);
        if (!sample) return false;
        return isPointInRing(sample, outerRing);
    }

    /**规范 Polygon 几何 */
    function normalizePolygonGeometry(geometry) {
        if (!geometry || geometry.type !== 'Polygon') return geometry;
        var rings = (geometry.coordinates || [])
            .map(normalizeRing)
            .filter(function (ring) { return ring.length >= 4; });
        if (rings.length <= 1) {
            return {
                type: 'Polygon',
                coordinates: rings
            };
        }

        var ordered = rings
            .map(function (ring) {
                return {
                    ring: ring,
                    area: getRingArea(ring)
                };
            })
            .sort(function (a, b) { return b.area - a.area; })
            .map(function (item) { return item.ring; });

        var polygons = [];
        ordered.forEach(function (ring) {
            var assigned = false;
            for (var i = 0; i < polygons.length; i += 1) {
                var outerRing = polygons[i][0];
                if (!outerRing) continue;
                if (isHoleRing(ring, outerRing)) {
                    polygons[i].push(ring);
                    assigned = true;
                    break;
                }
            }
            if (!assigned) polygons.push([ring]);
        });

        if (polygons.length <= 1) {
            return {
                type: 'Polygon',
                coordinates: polygons[0] || []
            };
        }

        return {
            type: 'MultiPolygon',
            coordinates: polygons
        };
    }

    /**规范几何对象 */
    function normalizeGeometry(geometry) {
        if (!geometry || typeof geometry !== 'object' || !geometry.type) return geometry;
        if (geometry.type === 'Polygon') return normalizePolygonGeometry(cloneJson(geometry));
        if (geometry.type === 'MultiPolygon') return cloneJson(geometry);
        if (geometry.type === 'GeometryCollection') {
            return {
                type: 'GeometryCollection',
                geometries: (geometry.geometries || []).map(normalizeGeometry).filter(Boolean)
            };
        }
        return cloneJson(geometry);
    }

    /**规范单个 Feature */
    function normalizeFeature(feature) {
        if (!feature || feature.type !== 'Feature') return feature;
        return {
            type: 'Feature',
            id: feature.id,
            properties: cloneJson(feature.properties) || {},
            geometry: normalizeGeometry(feature.geometry)
        };
    }

    /**规范 GeoJson 对象 */
    function normalizeGeoJsonObject(obj) {
        if (!obj || typeof obj !== 'object') return null;
        if (obj.type === 'FeatureCollection') {
            return {
                type: 'FeatureCollection',
                features: (obj.features || []).map(normalizeFeature).filter(Boolean)
            };
        }
        if (obj.type === 'Feature') return normalizeFeature(obj);
        if (obj.type) return normalizeGeometry(obj);
        return null;
    }

    /**规范原始 GeoJson 文本 */
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
                // ignore malformed uri
            }
        }
        return text;
    }

    /**解析并规范 GeoJson */
    function parseGeoJson(raw) {
        if (!raw) return null;
        var obj = raw;
        for (var i = 0; i < 3 && typeof obj === 'string'; i += 1) {
            var text = normalizeRawGeoJson(obj);
            if (!text) return null;
            try {
                obj = JSON.parse(text);
            } catch {
                return null;
            }
        }
        return normalizeGeoJsonObject(obj);
    }

    global.GisGeoJsonHelper = {
        cloneJson: cloneJson,
        normalizeRawGeoJson: normalizeRawGeoJson,
        normalizeGeometry: normalizeGeometry,
        normalizeFeature: normalizeFeature,
        normalizeGeoJsonObject: normalizeGeoJsonObject,
        parseGeoJson: parseGeoJson
    };
})(window);
