/**
 * 企业检查对象相关逻辑？？？
*/
(function () {
    function create(ctx) {
        const map = ctx.map;
        const state = ctx.state;
        const onOpenDetail = ctx.onOpenDetail || (() => {});

        /**
         * 选择对象
         * @param {Object} item - 对象数据
         * @param {boolean} [openDrawer=true] - 是否打开详情抽屉
         */
        function selectObject(item, openDrawer = true) {
            if (!item) return;
            state.selectedId = item.id;
            if (openDrawer) onOpenDetail(item.id);
        }

        /**
         * 加载对象数据
         */
        async function loadObjects() {
            const resp = await fetch('?handler=LayerData');
            const res = await resp.json();
            if (res.code !== 0 || !Array.isArray(res.data)) return;

            state.objects = res.data;
            buildMarkers(state.objects);
            if (state.objects.length > 0) selectObject(state.objects[0], false);
        }

        /**
         * 构建对象标记
         * @param {Array} items - 对象数据数组
         */
        function buildMarkers(items) {
            state.markerMap.forEach(marker => marker.remove());
            state.markerMap.clear();

            items.forEach(item => {
                const el = document.createElement('div');
                el.style.width = '26px';
                el.style.height = '26px';
                el.style.borderRadius = '999px';
                el.style.background = 'linear-gradient(135deg,#60a5fa,#1d4ed8)';
                el.style.border = '2px solid rgba(255,255,255,0.85)';
                el.style.boxShadow = '0 0 12px rgba(96,165,250,0.7)';
                el.style.cursor = 'pointer';
                el.title = item.name || '检查对象';
                el.addEventListener('click', () => selectObject(item, true));

                const marker = new mapboxgl.Marker(el).setLngLat([item.lng, item.lat]).addTo(map);
                state.markerMap.set(item.id, marker);
            });
        }

        /**
         * 设置对象标记图层是否可见
         * @param {boolean} visible - 是否可见
         */
        function setLayerVisible(visible) {
            state.markerMap.forEach(marker => {
                marker.getElement().style.display = visible ? '' : 'none';
            });
        }

        return {
            selectObject,
            loadObjects,
            buildMarkers,
            setLayerVisible
        };
    }

    window.GisIndexObject = { create };
})();
