/**
 * 地图地址检索相关
 */
(function () {
    function create(ctx) {
        const map = ctx.map;
        let addressListBound = false;
        let pendingAddresses = [];
        let searchMarker = null;

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
