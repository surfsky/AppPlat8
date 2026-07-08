(function () {
    function create() {
        /**标准化空白与标点*/
        function normalizeSearchText(text) {
            return String(text || '')
                .replace(/，|；|;/g, ',')
                .replace(/[、|]/g, ',')
                .replace(/\s+/g, ' ')
                .trim();
        }

        /**校验坐标范围*/
        function isValidLngLat(lng, lat) {
            return Number.isFinite(lng)
                && Number.isFinite(lat)
                && Math.abs(lng) <= 180
                && Math.abs(lat) <= 90;
        }

        /**尝试按范围自动纠正经纬顺序*/
        function fixLngLatOrder(lng, lat) {
            if (isValidLngLat(lng, lat)) return { lng, lat };
            if (isValidLngLat(lat, lng)) return { lng: lat, lat: lng };
            return null;
        }

        /**格式化经纬度文本*/
        function formatCoordText(lng, lat) {
            return `${Number(lng).toFixed(6)},${Number(lat).toFixed(6)}`;
        }

        /**解析纯数字经纬度*/
        function parseDecimalPair(text) {
            const raw = normalizeSearchText(text).replace(/\s+/g, ',');
            const parts = raw.split(',').map(x => x.trim()).filter(Boolean);
            if (parts.length < 2) return null;
            const a = Number(parts[0]);
            const b = Number(parts[1]);
            if (!Number.isFinite(a) || !Number.isFinite(b)) return null;
            return fixLngLatOrder(a, b);
        }

        /**将度分秒转为十进制度*/
        function toDecimalDegree(deg, min, sec, sign) {
            const d = Number(deg);
            const m = Number(min || 0);
            const s = Number(sec || 0);
            if (!Number.isFinite(d) || !Number.isFinite(m) || !Number.isFinite(s)) return NaN;
            const value = Math.abs(d) + (Math.abs(m) / 60) + (Math.abs(s) / 3600);
            const finalSign = sign === -1 || d < 0 ? -1 : 1;
            return value * finalSign;
        }

        /**解析带方位前后缀的经纬度片段*/
        function parseAxisPart(part) {
            const raw = String(part || '').trim();
            if (!raw) return null;

            let axis = '';
            let sign = 1;
            if (/[东Ee]/.test(raw)) {
                axis = 'lng';
                sign = 1;
            } else if (/[西Ww]/.test(raw)) {
                axis = 'lng';
                sign = -1;
            } else if (/[北Nn]/.test(raw)) {
                axis = 'lat';
                sign = 1;
            } else if (/[南Ss]/.test(raw)) {
                axis = 'lat';
                sign = -1;
            }

            const nums = raw.match(/-?\d+(?:\.\d+)?/g) || [];
            if (nums.length === 0) return null;

            let value = NaN;
            if (nums.length >= 3 || /[°度′'分″"秒]/.test(raw)) {
                value = toDecimalDegree(nums[0], nums[1], nums[2], sign);
            } else {
                value = Number(nums[0]);
                if (!Number.isFinite(value)) return null;
                if (sign === -1) value = -Math.abs(value);
            }
            if (!Number.isFinite(value)) return null;
            return { axis, value };
        }

        /**解析带经纬方向标记的搜索词*/
        function parseLabeledPair(text) {
            const normalized = normalizeSearchText(text);
            if (!/[EWNS东南西北经纬]/i.test(normalized)) return null;
            const parts = normalized.split(',').map(x => x.trim()).filter(Boolean);
            if (parts.length >= 2) {
                let lng = null;
                let lat = null;
                parts.forEach(part => {
                    const parsed = parseAxisPart(part);
                    if (!parsed) return;
                    if (parsed.axis === 'lng') lng = parsed.value;
                    if (parsed.axis === 'lat') lat = parsed.value;
                });
                if (lng !== null && lat !== null) return fixLngLatOrder(lng, lat);
            }

            const compact = normalized.replace(/\s+/g, '');
            const lngPart = compact.match(/(?:东经|西经|[EWew])[^,，;；]*/);
            const latPart = compact.match(/(?:北纬|南纬|[NSns])[^,，;；]*/);
            if (!lngPart || !latPart) return null;
            const lngParsed = parseAxisPart(lngPart[0]);
            const latParsed = parseAxisPart(latPart[0]);
            if (!lngParsed || !latParsed) return null;
            return fixLngLatOrder(lngParsed.value, latParsed.value);
        }

        /**解析经纬度搜索词*/
        function parseCoordKeyword(text) {
            const byLabel = parseLabeledPair(text);
            if (byLabel) return byLabel;
            return parseDecimalPair(text);
        }

        /**请求地址接口*/
        async function requestApi(endpoint) {
            const resp = await fetch(endpoint, {
                method: 'GET',
                cache: 'no-store',
                headers: {
                    'Cache-Control': 'no-cache',
                    Pragma: 'no-cache'
                }
            });
            const raw = await resp.text();
            let result = null;
            try {
                result = raw ? JSON.parse(raw) : null;
            } catch {
                throw new Error(`接口返回非JSON（HTTP ${resp.status}）`);
            }

            if (!resp.ok) {
                throw new Error(result?.message || result?.info || `HTTP ${resp.status}`);
            }
            return result;
        }

        /**搜索地址或经纬度*/
        async function searchKeyword(keyword) {
            const text = normalizeSearchText(keyword);
            if (!text) {
                return { mode: 'empty', keyword: '', list: [] };
            }

            const coord = parseCoordKeyword(text);
            if (coord) {
                return {
                    mode: 'coord',
                    keyword: text,
                    coord,
                    normalizedText: formatCoordText(coord.lng, coord.lat),
                    list: []
                };
            }

            const endpoint = `/HttpApi/Gis/GetAddrs?name=${encodeURIComponent(text)}`;
            const result = await requestApi(endpoint);
            if (result?.code !== 0) {
                throw new Error(result?.message || result?.info || '地址搜索失败');
            }

            return {
                mode: 'list',
                keyword: text,
                list: Array.isArray(result?.data) ? result.data : []
            };
        }

        /**解析地址项坐标*/
        async function resolveAddressItem(item) {
            const name = String(item?.name || item?.Name || '').trim();
            const fallbackLng = Number(item?.lng ?? item?.Lng);
            const fallbackLat = Number(item?.lat ?? item?.Lat);

            if (isValidLngLat(fallbackLng, fallbackLat)) {
                return {
                    ok: true,
                    lng: fallbackLng,
                    lat: fallbackLat,
                    name,
                    normalizedText: formatCoordText(fallbackLng, fallbackLat)
                };
            }

            try {
                const endpoint = `/HttpApi/Gis/GetAddr?name=${encodeURIComponent(name || '')}`;
                const result = await requestApi(endpoint);
                if (result?.code === 0 && result?.data) {
                    const row = result.data;
                    const lng = Number(row.lng ?? row.Lng);
                    const lat = Number(row.lat ?? row.Lat);
                    if (isValidLngLat(lng, lat)) {
                        return {
                            ok: true,
                            lng,
                            lat,
                            name,
                            normalizedText: formatCoordText(lng, lat)
                        };
                    }
                }
                if (result?.code !== 0) {
                    return { ok: false, reason: result?.message || result?.info || '地址定位失败' };
                }
            } catch {
                // ignore and fallback
            }

            return { ok: false, reason: '未找到该地址坐标' };
        }

        return {
            normalizeSearchText,
            parseCoordKeyword,
            formatCoordText,
            searchKeyword,
            resolveAddressItem
        };
    }

    window.GisAddressSearch = { create };
})();
