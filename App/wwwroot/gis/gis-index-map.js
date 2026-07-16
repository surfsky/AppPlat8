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
                if (!layer || layer.type !== 'symbol') return;
                const layerId = layer.id || '';
                if (layerId.startsWith('draw-label-')) return;
                if (layerId.includes('gl-draw-')) return;
                if (layer.source && layer.source !== 'composite') return;

                // 隐藏底图道路盾牌，避免黄色/绿色编号框干扰业务标注。
                if (layerId === 'road-number-shield' || layerId === 'road-exit-shield') {
                    try {
                        map.setLayoutProperty(layerId, 'visibility', 'none');
                    } catch {
                        // ignore incompatible shield layer visibility
                    }
                    return;
                }

                const textField = map.getLayoutProperty(layer.id, 'text-field');
                if (!textField) return;
                try {
                    map.setLayoutProperty(layer.id, 'text-field', [
                        'coalesce',
                        ['get', 'name_zh-Hans'],
                        ['get', 'name_zh-Hans-CN'],
                        ['get', 'name_zh'],
                        ['get', 'name_zh_CN'],
                        ['get', 'name_zh_TW'],
                        ['get', 'local_name'],
                        ['get', 'name'],
                        ['get', 'NAME'],
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
            let lastText = '';

            /**复制文本 */
            async function copyText(text) {
                const value = String(text || '').trim();
                if (!value) return false;

                try {
                    if (navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
                        await navigator.clipboard.writeText(value);
                        return true;
                    }
                } catch {
                    // ignore and fallback
                }

                try {
                    const input = document.createElement('textarea');
                    input.value = value;
                    input.setAttribute('readonly', 'readonly');
                    input.style.position = 'fixed';
                    input.style.left = '-9999px';
                    input.style.top = '-9999px';
                    document.body.appendChild(input);
                    input.focus();
                    input.select();
                    const ok = document.execCommand('copy');
                    document.body.removeChild(input);
                    return !!ok;
                } catch {
                    return false;
                }
            }

            /**显示复制结果 */
            function showCopyMessage(ok) {
                const msg = ok ? '坐标已复制' : '复制失败';
                const manager = window.top?.EleManager || window.EleManager;
                if (manager && typeof manager.showSuccess === 'function' && ok) {
                    manager.showSuccess(msg);
                    return;
                }
                if (manager && typeof manager.showWarning === 'function' && !ok) {
                    manager.showWarning(msg);
                    return;
                }
                if (!ok) console.warn(msg);
            }

            const setText = () => {
                if (!container) return;
                const mapCenter = map.getCenter();
                const mapZoom = map.getZoom().toFixed(2);
                lastText = `${mapZoom}: ${mapCenter.lng.toFixed(6)}， ${mapCenter.lat.toFixed(6)}`;
                container.textContent = lastText;
                container.setAttribute('title', '双击复制坐标');
            };

            return {
                onAdd() {
                    container = document.createElement('div');
                    container.className = 'mapboxgl-ctrl map-center-ctrl';
                    container.setAttribute('aria-label', '地图中心坐标');
                    container.addEventListener('dblclick', async (e) => {
                        e.preventDefault();
                        e.stopPropagation();
                        const ok = await copyText(lastText);
                        showCopyMessage(ok);
                    });
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

        /**创建全屏控件*/
        function createFullscreenControl() {
            let container = null;
            let button = null;

            function isFullscreen() {
                return document.fullscreenElement === document.documentElement;
            }

            function syncState() {
                if (!button) return;
                const active = isFullscreen();
                button.classList.toggle('is-active', active);
                button.title = active ? '退出全屏' : '进入全屏';
                button.setAttribute('aria-label', active ? '退出全屏' : '进入全屏');
            }

            async function onToggle() {
                try {
                    if (isFullscreen()) {
                        await document.exitFullscreen?.();
                    } else {
                        await document.documentElement.requestFullscreen?.();
                    }
                } catch {
                }
                syncState();
            }

            return {
                onAdd() {
                    container = document.createElement('div');
                    container.className = 'mapboxgl-ctrl mapboxgl-ctrl-group';

                    button = document.createElement('button');
                    button.type = 'button';
                    button.className = 'mapboxgl-ctrl-icon gis-fullscreen-btn';
                    button.innerHTML = `
                        <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
                            <path d="M9 4H5v4" />
                            <path d="M15 4h4v4" />
                            <path d="M19 15v4h-4" />
                            <path d="M5 15v4h4" />
                            <path d="M5 9l5-5" />
                            <path d="M19 9l-5-5" />
                            <path d="M19 15l-5 5" />
                            <path d="M5 15l5 5" />
                        </svg>
                    `;
                    button.addEventListener('click', onToggle);
                    document.addEventListener('fullscreenchange', syncState);
                    syncState();

                    container.appendChild(button);
                    return container;
                },
                onRemove() {
                    document.removeEventListener('fullscreenchange', syncState);
                    if (container && container.parentNode) {
                        container.parentNode.removeChild(container);
                    }
                    button = null;
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
            createFullscreenControl,
            ensureMapGeometryHelperReady,
            searchAddress() {
                return addressApi?.searchAddress?.();
            },
            resetView
        };
    }

    window.GisIndexMap = { create };
})();
