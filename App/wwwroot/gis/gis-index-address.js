/**
 * 地图地址检索相关
 */
(function () {
    function create(ctx) {
        const map = ctx.map;
        const addressSearch = window.GisAddressSearch?.create?.() || null;
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

        function hideAddressSuggestList() {
            const list = document.getElementById('address-suggest-list');
            if (!list) return;
            list.classList.remove('visible');
            list.innerHTML = '';
            pendingAddresses = [];
        }

        async function locateAddressByName(name, fallbackLng, fallbackLat) {
            const result = await addressSearch.resolveAddressItem({
                name,
                lng: fallbackLng,
                lat: fallbackLat
            });
            if (result?.ok) {
                flyToWithMarker(result.lng, result.lat);
                return { ok: true, reason: '' };
            }
            return { ok: false, reason: result?.reason || '未找到该地址坐标' };
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

            const result = await addressSearch.searchKeyword(keyword).catch(error => {
                const msg = error?.message || '未知错误';
                window.EleManager.showError(`地址搜索失败：${msg}`);
                return null;
            });
            if (!result) return;

            if (result.mode === 'coord' && result.coord) {
                hideAddressSuggestList();
                flyToWithMarker(result.coord.lng, result.coord.lat);
                if (input) {
                    input.value = result.normalizedText || addressSearch.formatCoordText(result.coord.lng, result.coord.lat);
                }
                return;
            }

            const list = Array.isArray(result.list) ? result.list : [];
            if (list.length === 0) {
                hideAddressSuggestList();
                window.EleManager.showWarning('未找到该地址');
                return;
            }
            renderAddressSuggestList(list);
        }

        return {
            searchAddress
        };
    }

    window.GisIndexAddress = { create };
})();
