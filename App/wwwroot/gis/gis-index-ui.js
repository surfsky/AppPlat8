(function () {
    let headerDatetimeTimer = null;

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

    function syncToolbarUI(state) {
        const toolbar = document.querySelector('.map-toolbar');
        const toggleBtn = document.getElementById('btn-toolbar-toggle');
        if (toolbar) toolbar.classList.toggle('toolbar-collapsed', !!state.toolbarCollapsed);
        if (toggleBtn) {
            toggleBtn.classList.toggle('active', !state.toolbarCollapsed);
            toggleBtn.title = state.toolbarCollapsed ? '展开工具栏' : '收起工具栏';
        }
    }

    function toggleToolbar(state) {
        state.toolbarCollapsed = !state.toolbarCollapsed;
        syncToolbarUI(state);
    }

    function syncLayerPanelUI(state) {
        const panel = document.getElementById('layer-panel');
        const toggleBtn = document.getElementById('btn-layer-toggle');
        if (panel) panel.classList.toggle('panel-hidden', !!state.layerCollapsed);
        if (toggleBtn) {
            toggleBtn.classList.toggle('active', !state.layerCollapsed);
            toggleBtn.title = state.layerCollapsed ? '展开图层列表' : '收起图层列表';
        }
    }

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

    function syncViewMenuUI(state) {
        const menu = document.getElementById('view-menu');
        const toggleBtn = document.getElementById('btn-view-toggle');
        if (menu) menu.classList.toggle('menu-visible', !!state.viewMenuOpen);
        if (toggleBtn) {
            toggleBtn.classList.toggle('active', !!state.viewMenuOpen);
            toggleBtn.title = state.viewMenuOpen ? '收起视图菜单' : '展开视图菜单';
        }
    }

    function toggleViewMenu(state) {
        state.viewMenuOpen = !state.viewMenuOpen;
        syncViewMenuUI(state);
    }

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

    function toggleSceneMenu(state) {
        state.sceneMenuOpen = !state.sceneMenuOpen;
        syncSceneMenuUI(state);
    }

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

    function closeViewMenu(state) {
        state.viewMenuOpen = false;
        syncViewMenuUI(state);
    }

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
