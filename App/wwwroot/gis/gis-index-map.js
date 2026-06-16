/**
 * 地图相关逻辑
 */
(function () {
    function create(ctx) {
        const map = ctx.map;
        const center = ctx.center;
        const zoom = ctx.zoom;
        const addressApi = window.GisIndexAddress?.create({ map });

        /**应用中文标签*/
        function applyChineseLabels() {
            const style = map.getStyle();
            if (!style || !Array.isArray(style.layers)) return;
            style.layers.forEach(layer => {
                if (layer.type !== 'symbol') return;
                const textField = map.getLayoutProperty(layer.id, 'text-field');
                if (!textField) return;
                if (typeof textField !== 'string') return;
                try {
                    map.setLayoutProperty(layer.id, 'text-field', [
                        'coalesce',
                        ['get', 'name_zh-Hans'],
                        ['get', 'name_zh'],
                        ['get', 'name'],
                        ''
                    ]);
                } catch {
                    // ignore incompatible text-field expression
                }
            });
        }

        /**创建中心坐标控制按钮*/
        function createCenterCoordControl() {
            let container = null;
            const setText = () => {
                if (!container) return;
                const mapCenter = map.getCenter();
                const mapZoom = map.getZoom().toFixed(2);
                container.textContent = `${mapZoom}: ${mapCenter.lng.toFixed(6)}， ${mapCenter.lat.toFixed(6)}`;
            };

            return {
                onAdd() {
                    container = document.createElement('div');
                    container.className = 'mapboxgl-ctrl map-center-ctrl';
                    setText();
                    return container;
                },
                onRemove() {
                    if (container && container.parentNode) {
                        container.parentNode.removeChild(container);
                    }
                    container = null;
                },
                update: setText
            };
        }

        /**创建重置地图控件*/
        function createResetControl(onReset) {
            let container = null;

            return {
                onAdd() {
                    container = document.createElement('div');
                    container.className = 'mapboxgl-ctrl mapboxgl-ctrl-group';

                    const button = document.createElement('button');
                    button.type = 'button';
                    button.className = 'mapboxgl-ctrl-icon gis-reset-btn';
                    button.title = '重置地图';
                    button.setAttribute('aria-label', '重置地图');
                    button.innerHTML = '<i class="fa-solid fa-rotate-right" aria-hidden="true"></i>';
                    button.addEventListener('click', onReset);

                    container.appendChild(button);
                    return container;
                },
                onRemove() {
                    if (container && container.parentNode) {
                        container.parentNode.removeChild(container);
                    }
                    container = null;
                }
            };
        }

        /**确保地图几何助手已加载*/
        async function ensureMapGeometryHelperReady() {
            if (window.MapGeometryHelper && typeof window.MapGeometryHelper.createDisplayLayerManager === 'function') {
                return true;
            }

            const dynamicScriptKey = 'maphelper-runtime-loader';
            if (!document.getElementById(dynamicScriptKey)) {
                const script = document.createElement('script');
                script.id = dynamicScriptKey;
                script.src = `/gis/maphelper.js?v=${Date.now()}`;
                document.head.appendChild(script);
            }

            const started = Date.now();
            while (Date.now() - started < 6000) {
                if (window.MapGeometryHelper && typeof window.MapGeometryHelper.createDisplayLayerManager === 'function') {
                    return true;
                }
                await new Promise(resolve => setTimeout(resolve, 120));
            }

            return false;
        }

        /**重置视图 */
        function resetView() {
            window.GisIndexUI.resetView(map, center, zoom);
        }

        return {
            applyChineseLabels,
            createCenterCoordControl,
            createResetControl,
            ensureMapGeometryHelperReady,
            searchAddress() {
                return addressApi?.searchAddress?.();
            },
            resetView
        };
    }

    window.GisIndexMap = { create };
})();
