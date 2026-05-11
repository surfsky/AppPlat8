(function () {
    function create(ctx) {
        const map = ctx.map;
        const state = ctx.state;
        const terrainSourceId = ctx.terrainSourceId || 'mapbox-dem';
        const center = ctx.center;
        const zoom = ctx.zoom;
        const closeViewMenu = ctx.closeViewMenu || (() => {});

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

        function createCenterCoordControl() {
            let container = null;
            const setText = () => {
                if (!container) return;
                const mapCenter = map.getCenter();
                const mapZoom = map.getZoom().toFixed(0);
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

        async function ensureMapGeometryHelperReady() {
            if (window.MapGeometryHelper && typeof window.MapGeometryHelper.createDisplayLayerManager === 'function') {
                return true;
            }

            const dynamicScriptKey = 'maphelper-runtime-loader';
            if (!document.getElementById(dynamicScriptKey)) {
                const script = document.createElement('script');
                script.id = dynamicScriptKey;
                script.src = `/js/maphelper.js?v=${Date.now()}`;
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

        function ensureTerrainEnabled() {
            if (!map.getSource(terrainSourceId)) {
                map.addSource(terrainSourceId, {
                    type: 'raster-dem',
                    url: 'mapbox://mapbox.mapbox-terrain-dem-v1',
                    tileSize: 512,
                    maxzoom: 14
                });
            }

            map.setTerrain({ source: terrainSourceId, exaggeration: 1.25 });
            map.setFog({
                color: 'rgb(186, 210, 235)',
                'high-color': 'rgb(36, 92, 223)',
                'horizon-blend': 0.08
            });
        }

        function enable3DBuildings() {
            ensureTerrainEnabled();
            const layers = map.getStyle().layers;
            const labelLayerId = layers.find(l => l.type === 'symbol' && l.layout && l.layout['text-field'])?.id;
            if (!map.getLayer('3d-buildings')) {
                map.addLayer({
                    id: '3d-buildings',
                    source: 'composite',
                    'source-layer': 'building',
                    filter: ['==', 'extrude', 'true'],
                    type: 'fill-extrusion',
                    minzoom: 15,
                    paint: {
                        'fill-extrusion-color': '#94a3b8',
                        'fill-extrusion-height': ['get', 'height'],
                        'fill-extrusion-base': ['get', 'min_height'],
                        'fill-extrusion-opacity': 0.55
                    }
                }, labelLayerId);
            }
            map.easeTo({ pitch: 60, bearing: -18, duration: 600 });
            state.is3D = true;
            const button = document.getElementById('btn-toggle-3d');
            if (button) button.classList.add('active');
        }

        function disable3DBuildings() {
            map.setTerrain(null);
            if (map.getLayer('3d-buildings')) {
                map.removeLayer('3d-buildings');
            }
            map.easeTo({ pitch: 0, bearing: 0, duration: 600 });
            state.is3D = false;
            const button = document.getElementById('btn-toggle-3d');
            if (button) button.classList.remove('active');
        }

        function switchStyle(type) {
            state.preservedVisibleGeometryIds = new Set(
                state.geometries
                    .filter(g => state.geometryVisibleMap.get(g.id) !== false)
                    .map(g => String(g.id))
            );
            state.currentStyle = type;
            const styleMap = {
                street: 'mapbox://styles/mapbox/streets-v12',
                satellite: 'mapbox://styles/mapbox/satellite-streets-v12',
                dark: 'mapbox://styles/mapbox/dark-v11',
                night: 'mapbox://styles/mapbox/navigation-night-v1'
            };
            map.setStyle(styleMap[type] || styleMap.street);
            document.getElementById('btn-style-street')?.classList.toggle('active', type === 'street');
            document.getElementById('btn-style-sat')?.classList.toggle('active', type === 'satellite');
            document.getElementById('btn-style-dark')?.classList.toggle('active', type === 'dark');
            document.getElementById('btn-style-night')?.classList.toggle('active', type === 'night');
            closeViewMenu();
        }

        async function searchAddress() {
            const input = document.getElementById('address-input');
            const keyword = (input?.value || '').trim();
            if (!keyword) return;
            const endpoint = `https://api.mapbox.com/geocoding/v5/mapbox.places/${encodeURIComponent(keyword)}.json?access_token=${encodeURIComponent(mapboxgl.accessToken)}&language=zh-Hans&limit=1`;
            try {
                const resp = await fetch(endpoint);
                const data = await resp.json();
                const feature = data?.features?.[0];
                if (!feature) {
                    window.EleManager.showWarning('未找到该地址');
                    return;
                }
                const [lng, lat] = feature.center;
                map.flyTo({ center: [lng, lat], zoom: 15, speed: 0.9 });
            } catch {
                window.EleManager.showError('地址搜索失败');
            }
        }

        function resetView() {
            window.GisIndexUI.resetView(map, center, zoom);
        }

        return {
            applyChineseLabels,
            createCenterCoordControl,
            createResetControl,
            ensureMapGeometryHelperReady,
            enable3DBuildings,
            disable3DBuildings,
            switchStyle,
            searchAddress,
            resetView
        };
    }

    window.GisIndexMap = { create };
})();
