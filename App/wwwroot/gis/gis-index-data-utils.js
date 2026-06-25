(function () {
    function create() {
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

            const walk = value => {
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
            const rawType = item?.type ?? item?.Type;
            if (rawType !== undefined && rawType !== null && rawType !== '') {
                const t = Number(rawType);
                if (t === 1) return 'point';
                if (t === 2) return 'shape';
                if (t === 3) return 'text';
                if (t === 4) return 'image';
                if (t === 5) return 'video';
                if (t === 6) return 'file';

                const txt = String(rawType).trim().toLowerCase();
                if (txt === 'point' || txt === '点') return 'point';
                if (txt === 'shape' || txt === '形状') return 'shape';
                if (txt === 'text' || txt === '文字') return 'text';
                if (txt === 'image' || txt === '图片') return 'image';
                if (txt === 'video' || txt === '视频') return 'video';
                if (txt === 'file' || txt === '文件' || txt === 'threed' || txt === '3d' || txt === '三维') return 'file';
            }

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

        function splitMultiUrls(text) {
            if (!text || typeof text !== 'string') return [];
            return text
                .replace(/[\r\n]+/g, ',')
                .split(/[;,，；\s]+/)
                .map(x => (x || '').trim())
                .filter(Boolean);
        }

        function resolveImageUrlFromFileOrAtt(file, att) {
            const url = file || att || '';
            const parts = splitMultiUrls(url);
            if (!parts.length) return '';

            const first = parts[0];
            if (!first) return '';

            if (first.indexOf('/Shared/FileViews/Viewer') >= 0) {
                try {
                    const base = window.location && window.location.origin ? window.location.origin : 'http://localhost';
                    const u = new URL(first, base);
                    const src = (u.searchParams.get('src') || '').trim();
                    if (src) return normalizeIconPath(decodeURIComponent(src));
                } catch {
                    // ignore parse errors
                }
            }

            return normalizeIconPath(first);
        }

        function toImageSourceCoordinatesFromRegion(regionText) {
            if (!regionText || typeof regionText !== 'string') return null;
            const parts = regionText
                .replace(/[，；;]/g, ',')
                .split(',')
                .map(x => Number((x || '').trim()))
                .filter(n => Number.isFinite(n));

            if (parts.length < 4) return null;

            const tlx = parts[0];
            const tly = parts[1];
            const brx = parts[2];
            const bry = parts[3];
            const minLng = Math.min(tlx, brx);
            const maxLng = Math.max(tlx, brx);
            const minLat = Math.min(tly, bry);
            const maxLat = Math.max(tly, bry);

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

            let minLng = Infinity;
            let maxLng = -Infinity;
            let minLat = Infinity;
            let maxLat = -Infinity;
            let count = 0;

            ring.forEach(coord => {
                if (!Array.isArray(coord) || coord.length < 2) return;
                const lng = Number(coord[0]);
                const lat = Number(coord[1]);
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

            return [
                [minLng, maxLat],
                [maxLng, maxLat],
                [maxLng, minLat],
                [minLng, minLat]
            ];
        }

        function toImageSourceCoordinatesFromGeoJson(geo) {
            if (!geo || !Array.isArray(geo.features) || geo.features.length === 0) return null;

            let minLng = Infinity;
            let maxLng = -Infinity;
            let minLat = Infinity;
            let maxLat = -Infinity;
            let count = 0;

            const collect = value => {
                if (!Array.isArray(value) || value.length === 0) return;
                if (typeof value[0] === 'number' && typeof value[1] === 'number') {
                    const lng = Number(value[0]);
                    const lat = Number(value[1]);
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

            geo.features.forEach(feature => {
                const geometry = feature && feature.geometry;
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

        return {
            parseGpsText,
            normalizeGeoJson,
            getCenterFromCoordinates,
            getCenterFromGeoJson,
            getGeometryKind,
            extractIconFromDataJson,
            getGeometryIcon,
            getGeometryCenter,
            normalizeDataUrl,
            normalizeIconPath,
            splitMultiUrls,
            resolveImageUrlFromFileOrAtt,
            toImageSourceCoordinatesFromRegion,
            toImageSourceCoordinatesFromRing,
            toImageSourceCoordinatesFromGeoJson
        };
    }

    window.GisIndexDataUtils = { create };
})();
