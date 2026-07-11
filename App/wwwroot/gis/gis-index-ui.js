/**
 * 地图UI相关逻辑
 */
(function () {
    let headerDatetimeTimer = null;
    let floatingTooltipsMounted = false;
    const layerPanelWidthKey = 'gis:index:layer-panel-width';
    const layerPanelMinWidth = 280;
    const layerPanelMaxWidth = 640;

    /**更新顶部时间显示 */
    function startHeaderDatetime(outputId) {
        const output = document.getElementById(outputId || 'header-datetime');
        if (!output) return;

        const render = () => {
            const now = new Date();
            const y = now.getFullYear();
            const m = String(now.getMonth() + 1).padStart(2, '0');
            const d = String(now.getDate()).padStart(2, '0');
            const hh = String(now.getHours()).padStart(2, '0');
            const mm = String(now.getMinutes()).padStart(2, '0');
            const ss = String(now.getSeconds()).padStart(2, '0');
            output.textContent = `${y}年${m}月${d}日\n${hh}:${mm}:${ss}`;
        };

        render();
        if (headerDatetimeTimer) clearInterval(headerDatetimeTimer);
        headerDatetimeTimer = setInterval(render, 1000);
    }

    function ensureFloatingTooltips() {
        if (floatingTooltipsMounted) return;
        if (!window.Vue || !window.ElementPlus) return;

        const items = [
            { id: 'btn-site-toggle', content: '网站', placement: 'bottom' },
            { id: 'btn-layer-toggle', content: '图层', placement: 'bottom' },
            { id: 'btn-stats-toggle', content: '统计', placement: 'bottom' },
            { id: 'btn-scene-toggle', content: '场景', placement: 'bottom' },
            { id: 'btn-toolbar-toggle', content: '工具栏', placement: 'bottom' }
        ];

        items.forEach(item => {
            const el = document.getElementById(item.id);
            if (el) el.removeAttribute('title');
        });

        const host = document.createElement('div');
        host.id = 'gis-floating-tooltips-host';
        host.style.position = 'fixed';
        host.style.left = '-9999px';
        host.style.top = '-9999px';
        host.style.width = '0';
        host.style.height = '0';
        host.style.overflow = 'hidden';
        document.body.appendChild(host);

        const { createApp, h, ref, onMounted } = window.Vue;
        const ElTooltip = window.ElementPlus.ElTooltip;

        const app = createApp({
            setup() {
                const btnRefs = items.reduce((acc, item) => {
                    acc[item.id] = ref(null);
                    return acc;
                }, {});

                onMounted(() => {
                    items.forEach(item => {
                        btnRefs[item.id].value = document.getElementById(item.id);
                    });
                });

                const renderTooltip = (item) => {
                    return h(ElTooltip, {
                        content: item.content,
                        placement: item.placement || 'right',
                        effect: 'dark',
                        showAfter: 80,
                        hideAfter: 0,
                        enterable: false,
                        teleported: true,
                        popperClass: 'gis-toolbar-tooltip',
                        virtualTriggering: true,
                        virtualRef: btnRefs[item.id].value
                    }, {
                        default: () => h('span')
                    });
                };

                return () => h('div', {}, items.map(renderTooltip));
            }
        });

        if (window.ElementPlusLocaleZhCn) app.use(window.ElementPlus, { locale: window.ElementPlusLocaleZhCn });
        else app.use(window.ElementPlus);

        app.mount(host);
        floatingTooltipsMounted = true;
    }

    /**同步工具栏状态 */
    function syncToolbarUI(state) {
        const toolbar = document.querySelector('.map-toolbar');
        const toggleBtn = document.getElementById('btn-toolbar-toggle');
        if (toolbar) toolbar.classList.toggle('toolbar-collapsed', !!state.toolbarCollapsed);
        if (toggleBtn) {
            toggleBtn.classList.toggle('active', !state.toolbarCollapsed);
        }
    }

    /**切换工具栏状态 */
    function toggleToolbar(state) {
        state.toolbarCollapsed = !state.toolbarCollapsed;
        syncToolbarUI(state);
    }

    /**同步图层列表面板状态 */
    function syncLayerPanelUI(state) {
        const panel = document.getElementById('layer-panel');
        const toggleBtn = document.getElementById('btn-layer-toggle');
        if (panel) panel.classList.toggle('panel-hidden', !!state.layerCollapsed);
        if (toggleBtn) {
            toggleBtn.classList.toggle('active', !state.layerCollapsed);
        }
    }

    /**切换图层列表面板状态 */
    function toggleLayerPanel(state) {
        if (state.statsMode) {
            state.statsMode = false;
            syncStatsModeUI(state);
        }
        state.layerCollapsed = !state.layerCollapsed;
        syncLayerPanelUI(state);
    }

    /**同步图层列表选项卡状态 */
    function syncLayerTabsUI(state) {
        const activeTab = state.activeLayerTab || 'resource';
        document.querySelectorAll('.layer-tab-btn').forEach(btn => {
            const isActive = btn.dataset.tab === activeTab;
            btn.classList.toggle('active', isActive);
            btn.setAttribute('aria-selected', isActive ? 'true' : 'false');
        });
        document.querySelectorAll('.layer-tab-panel').forEach(panel => {
            const isActive = panel.dataset.tabPanel === activeTab;
            panel.classList.toggle('active', isActive);
            panel.setAttribute('aria-hidden', isActive ? 'false' : 'true');
        });
    }

    /**切换图层列表选项卡状态 */
    function switchLayerTab(state, tab) {
        state.activeLayerTab = tab || 'resource';
        syncLayerTabsUI(state);
    }

    /**同步视图菜单状态 */
    function syncViewMenuUI(state) {
        if (state && state.activeLayerTab === 'view') {
            syncLayerTabsUI(state);
        }
    }

    /**切换视图菜单状态 */
    function toggleViewMenu(state) {
        state.layerCollapsed = false;
        syncLayerPanelUI(state);
        switchLayerTab(state, 'view');
    }

    /**同步场景菜单状态 */
    function syncSceneMenuUI(state) {
        const menu = document.getElementById('scene-menu');
        const toggleBtn = document.getElementById('btn-scene-toggle');
        if (menu) {
            menu.style.display = state.sceneMenuOpen ? 'block' : 'none';
            menu.classList.toggle('menu-visible', !!state.sceneMenuOpen);
        }
        if (toggleBtn) {
            toggleBtn.classList.toggle('active', !!state.sceneMenuOpen);
            
            // 更新场景图标
            const currentScene = state.scenes.find(s => s.id === state.currentSceneId);
            const iconEl = toggleBtn.querySelector('.toggle-icon');
            if (iconEl) {
                if (currentScene && currentScene.icon) {
                    iconEl.className = currentScene.icon + ' toggle-icon';
                } else {
                    iconEl.className = 'fa-solid fa-mountain-sun toggle-icon';
                }
            }
        }
    }

    /**切换场景菜单状态 */
    function toggleSceneMenu(state) {
        state.sceneMenuOpen = !state.sceneMenuOpen;
        syncSceneMenuUI(state);
    }

    /**渲染场景菜单 */
    function renderSceneMenu(state, onSelect) {
        const menu = document.getElementById('scene-menu');
        if (!menu) return;
        menu.innerHTML = '';
        state.scenes.forEach(scene => {
            const btn = document.createElement('button');
            btn.className = 'view-menu-item' + (state.currentSceneId === scene.id ? ' active' : '');
            btn.innerHTML = (scene.icon ? `<i class="${scene.icon}"></i>` : '') + `<span>${scene.name}</span>`;
            btn.onclick = () => {
                closeSceneMenu(state);
                onSelect(scene);
            };
            menu.appendChild(btn);
        });
    }

    /**关闭视图菜单 */
    function closeViewMenu(state) {
        if (!state) return;
        if ((state.activeLayerTab || 'resource') === 'view') {
            switchLayerTab(state, 'resource');
        }
    }

    /**同步统计模式状态 */
    function syncStatsModeUI(state) {
        const overlay = document.getElementById('stats-overlay');
        const statsBtn = document.getElementById('btn-stats-toggle');
        const layerPanel = document.getElementById('layer-panel');
        const toolbar = document.querySelector('.map-toolbar');

        if (overlay) {
            overlay.classList.toggle('stats-visible', !!state.statsMode);
        }

        if (statsBtn) {
            statsBtn.classList.toggle('stats-active', !!state.statsMode);
        }

        if (state.statsMode) {
            if (layerPanel) layerPanel.classList.add('panel-hidden');
            if (toolbar) toolbar.classList.add('toolbar-collapsed');
        }
    }

    /**切换统计模式状态 */
    function toggleStatsMode(state, options) {
        state.statsMode = !state.statsMode;
        if (state.statsMode) {
            state.toolbarCollapsed = true;
            state.layerCollapsed = true;
            if (options && typeof options.onCloseDrawer === 'function') {
                options.onCloseDrawer();
            }
        }
        syncToolbarUI(state);
        syncLayerPanelUI(state);
        syncStatsModeUI(state);
        if (options && typeof options.onStatsModeChanged === 'function') {
            options.onStatsModeChanged(state.statsMode);
        }
    }

    /**读取图层面板宽度 */
    function getSavedLayerPanelWidth() {
        try {
            const raw = window.localStorage.getItem(layerPanelWidthKey);
            const value = Number(raw || 0);
            return Number.isFinite(value) && value > 0 ? value : 0;
        } catch {
            return 0;
        }
    }

    /**应用图层面板宽度 */
    function applyLayerPanelWidth(width) {
        const panel = document.getElementById('layer-panel');
        if (!panel) return;
        if (window.innerWidth <= 900) {
            panel.style.width = '';
            return;
        }
        const next = Math.min(layerPanelMaxWidth, Math.max(layerPanelMinWidth, Math.round(Number(width) || 320)));
        panel.style.width = `${next}px`;
    }

    /**初始化图层面板拖拽宽度 */
    function initLayerPanelResize(options = {}) {
        const panel = document.getElementById('layer-panel');
        const handle = document.getElementById('layer-panel-resize-handle');
        if (!panel || !handle || handle.dataset.resizeReady === 'true') {
            applyLayerPanelWidth(getSavedLayerPanelWidth() || 320);
            return;
        }

        const savedWidth = getSavedLayerPanelWidth();
        if (savedWidth > 0) {
            applyLayerPanelWidth(savedWidth);
        }

        const onResizeDone = typeof options.onResizeDone === 'function' ? options.onResizeDone : null;
        let startX = 0;
        let startWidth = 0;
        let dragging = false;

        const onMove = (evt) => {
            if (!dragging) return;
            const next = startWidth + (evt.clientX - startX);
            applyLayerPanelWidth(next);
            document.body.classList.add('layer-panel-resizing');
        };

        const onUp = () => {
            if (!dragging) return;
            dragging = false;
            document.body.classList.remove('layer-panel-resizing');
            window.removeEventListener('pointermove', onMove);
            window.removeEventListener('pointerup', onUp);
            window.removeEventListener('pointercancel', onUp);
            const width = Math.round(panel.getBoundingClientRect().width || 320);
            try {
                window.localStorage.setItem(layerPanelWidthKey, String(width));
            } catch {
            }
            if (onResizeDone) onResizeDone(width);
        };

        handle.addEventListener('pointerdown', (evt) => {
            if (window.innerWidth <= 900) return;
            evt.preventDefault();
            evt.stopPropagation();
            startX = evt.clientX;
            startWidth = panel.getBoundingClientRect().width || 320;
            dragging = true;
            handle.setPointerCapture?.(evt.pointerId);
            window.addEventListener('pointermove', onMove);
            window.addEventListener('pointerup', onUp);
            window.addEventListener('pointercancel', onUp);
        });

        window.addEventListener('resize', () => {
            applyLayerPanelWidth(getSavedLayerPanelWidth() || panel.getBoundingClientRect().width || 320);
        });

        handle.dataset.resizeReady = 'true';
    }

    /**重置视图 */
    function resetView(map, center, zoom) {
        if (!map) return;
        map.easeTo({
            center,
            zoom,
            pitch: 0,
            bearing: 0,
            duration: 500
        });
    }

    /**关闭场景菜单 */
    function closeSceneMenu(state) {
        state.sceneMenuOpen = false;
        syncSceneMenuUI(state);
    }

    window.GisIndexUI = {
        startHeaderDatetime,
        ensureFloatingTooltips,
        toggleToolbar,
        syncToolbarUI,
        toggleLayerPanel,
        syncLayerPanelUI,
        switchLayerTab,
        syncLayerTabsUI,
        toggleViewMenu,
        closeViewMenu,
        syncViewMenuUI,
        applyLayerPanelWidth,
        initLayerPanelResize,
        toggleSceneMenu,
        closeSceneMenu,
        syncSceneMenuUI,
        renderSceneMenu,
        toggleStatsMode,
        syncStatsModeUI,
        resetView
    };
})();
