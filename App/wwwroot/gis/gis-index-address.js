/**
 * 地图地址检索相关
 */
(function () {
    function create(ctx) {
        const map = ctx.map;
        let addressListBound = false;
        let pendingAddresses = [];
        let searchMarker = null;

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

        function flyToWithMarker(lng, lat) {
            map.flyTo({ center: [lng, lat], zoom: 15, speed: 0.9 });
            if (searchMarker) {
                searchMarker.remove();
            }
            searchMarker = new mapboxgl.Marker({ color: '#ef4444' }).setLngLat([lng, lat]).addTo(map);
        }

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

        function hideAddressSuggestList() {
            const list = document.getElementById('address-suggest-list');
            if (!list) return;
            list.classList.remove('visible');
            list.innerHTML = '';
            pendingAddresses = [];
        }

        async function locateAddressByName(name, fallbackLng, fallbackLat) {
            try {
                const endpoint = `/HttpApi/Gis/GetAddr?name=${encodeURIComponent(name || '')}`;
                const result = await requestApi(endpoint);
                if (result?.code === 0 && result?.data) {
                    const row = result.data;
                    const lng = Number(row.lng ?? row.Lng);
                    const lat = Number(row.lat ?? row.Lat);
                    if (Number.isFinite(lng) && Number.isFinite(lat)) {
                        flyToWithMarker(lng, lat);
                        return { ok: true, reason: '' };
                    }
                }
                if (result?.code !== 0) {
                    return { ok: false, reason: result?.message || result?.info || '地址定位失败' };
                }
            } catch {
                // ignore and fallback
            }

            if (Number.isFinite(fallbackLng) && Number.isFinite(fallbackLat)) {
                flyToWithMarker(fallbackLng, fallbackLat);
                return { ok: true, reason: '' };
            }
            return { ok: false, reason: '未找到该地址坐标' };
        }

        async function selectAddressItem(item) {
            const name = (item?.name || item?.Name || '').trim();
            const fallbackLng = Number(item?.lng ?? item?.Lng);
            const fallbackLat = Number(item?.lat ?? item?.Lat);
            hideAddressSuggestList();
            let locateResult = { ok: false, reason: '' };

            if (Number.isFinite(fallbackLng) && Number.isFinite(fallbackLat)) {
                flyToWithMarker(fallbackLng, fallbackLat);
                locateResult = { ok: true, reason: '' };
            } else {
                locateResult = await locateAddressByName(name, fallbackLng, fallbackLat);
            }

            if (!locateResult.ok) {
                window.EleManager.showWarning(locateResult.reason || '未找到该地址坐标');
                return;
            }
            const input = document.getElementById('address-input');
            if (input && name) {
                input.value = name;
            }
        }

        function renderAddressSuggestList(items) {
            const list = document.getElementById('address-suggest-list');
            if (!list) return;
            list.innerHTML = '';
            pendingAddresses = Array.isArray(items) ? items : [];

            if (pendingAddresses.length === 0) {
                list.classList.remove('visible');
                return;
            }

            pendingAddresses.forEach((item, index) => {
                const btn = document.createElement('button');
                btn.type = 'button';
                btn.className = 'address-suggest-item';
                btn.dataset.index = String(index);

                const title = document.createElement('div');
                title.className = 'address-suggest-title';
                title.textContent = item?.name || item?.Name || '未命名地址';

                const desc = document.createElement('div');
                desc.className = 'address-suggest-desc';
                const district = item?.district || item?.District || '';
                const address = item?.address || item?.Address || '';
                desc.textContent = [district, address].filter(Boolean).join(' ');

                btn.appendChild(title);
                btn.appendChild(desc);
                btn.addEventListener('click', () => selectAddressItem(item));
                list.appendChild(btn);
            });

            list.classList.add('visible');
        }

        function ensureAddressListDismiss() {
            if (addressListBound) return;
            addressListBound = true;
            document.addEventListener('click', (e) => {
                const list = document.getElementById('address-suggest-list');
                const input = document.getElementById('address-input');
                const button = document.getElementById('btn-search');
                const target = e.target;
                if (!list || !target) return;
                if (list.contains(target)) return;
                if (input && input.contains(target)) return;
                if (button && button.contains(target)) return;
                hideAddressSuggestList();
            });
        }

        async function searchAddress() {
            ensureAddressListDismiss();
            const input = document.getElementById('address-input');
            const keyword = (input?.value || '').trim();
            if (!keyword) return;

            const coord = parseCoordKeyword(keyword);
            if (coord) {
                hideAddressSuggestList();
                flyToWithMarker(coord.lng, coord.lat);
                if (input) {
                    input.value = `${coord.lng.toFixed(6)},${coord.lat.toFixed(6)}`;
                }
                return;
            }

            const endpoint = `/HttpApi/Gis/GetAddrs?name=${encodeURIComponent(keyword)}`;
            try {
                const result = await requestApi(endpoint);
                if (result?.code !== 0) {
                    window.EleManager.showWarning(result?.message || result?.info || '地址搜索失败');
                    return;
                }

                const list = Array.isArray(result?.data) ? result.data : [];
                if (list.length === 0) {
                    hideAddressSuggestList();
                    window.EleManager.showWarning('未找到该地址');
                    return;
                }

                renderAddressSuggestList(list);
            } catch (error) {
                const msg = error?.message || '未知错误';
                window.EleManager.showError(`地址搜索失败：${msg}`);
            }
        }

        return {
            searchAddress
        };
    }

    window.GisIndexAddress = { create };
})();
