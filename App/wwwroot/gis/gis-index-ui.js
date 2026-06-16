/**
 * 地图UI相关逻辑
 */
(function () {
    let headerDatetimeTimer = null;

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

    /**同步工具栏状态 */
    function syncToolbarUI(state) {
        const toolbar = document.querySelector('.map-toolbar');
        const toggleBtn = document.getElementById('btn-toolbar-toggle');
        if (toolbar) toolbar.classList.toggle('toolbar-collapsed', !!state.toolbarCollapsed);
        if (toggleBtn) {
            toggleBtn.classList.toggle('active', !state.toolbarCollapsed);
            toggleBtn.title = state.toolbarCollapsed ? '展开工具栏' : '收起工具栏';
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
            toggleBtn.title = state.layerCollapsed ? '展开图层列表' : '收起图层列表';
        }
    }

    /**切换图层列表面板状态 */
    function toggleLayerPanel(state) {
        if (state.statsMode) {
            state.statsMode = false;
            state.viewMenuOpen = false;
            syncStatsModeUI(state);
            syncViewMenuUI(state);
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
        const menu = document.getElementById('view-menu');
        const toggleBtn = document.getElementById('btn-view-toggle');
        if (menu) menu.classList.toggle('menu-visible', !!state.viewMenuOpen);
        if (toggleBtn) {
            toggleBtn.classList.toggle('active', !!state.viewMenuOpen);
            toggleBtn.title = state.viewMenuOpen ? '收起视图菜单' : '展开视图菜单';
        }
    }

    /**切换视图菜单状态 */
    function toggleViewMenu(state) {
        state.viewMenuOpen = !state.viewMenuOpen;
        syncViewMenuUI(state);
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
            btn.innerHTML = (scene.icon ? `<i class="${scene.icon}"></i> ` : '') + scene.name;
            btn.onclick = () => onSelect(scene);
            menu.appendChild(btn);
        });
    }

    /**关闭视图菜单 */
    function closeViewMenu(state) {
        state.viewMenuOpen = false;
        syncViewMenuUI(state);
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
            statsBtn.title = state.statsMode ? '关闭统计面板' : '统计';
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
            state.viewMenuOpen = false;
            if (options && typeof options.onCloseDrawer === 'function') {
                options.onCloseDrawer();
            }
        }
        syncToolbarUI(state);
        syncLayerPanelUI(state);
        syncViewMenuUI(state);
        syncStatsModeUI(state);
        if (options && typeof options.onStatsModeChanged === 'function') {
            options.onStatsModeChanged(state.statsMode);
        }
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
        toggleToolbar,
        syncToolbarUI,
        toggleLayerPanel,
        syncLayerPanelUI,
        switchLayerTab,
        syncLayerTabsUI,
        toggleViewMenu,
        closeViewMenu,
        syncViewMenuUI,
        toggleSceneMenu,
        closeSceneMenu,
        syncSceneMenuUI,
        renderSceneMenu,
        toggleStatsMode,
        syncStatsModeUI,
        resetView
    };
})();
