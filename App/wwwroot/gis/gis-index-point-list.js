(function () {
    function create(ctx) {
        const panelId = ctx.panelId || 'geo-point-list-panel';
        const panelComponentId = ctx.panelComponentId || 'geo-point-list-gis-panel';
        const bodyId = ctx.bodyId || 'geo-point-list-body';
        const map = ctx.map;
        const state = ctx.state;
        const onSelectGeometry = ctx.onSelectGeometry || (() => {});
        const onRefreshMenuData = ctx.onRefreshMenuData || (async () => ({ code: -1, message: '未实现刷新' }));
        let closeTimer = null;

        function getPanel() {
            return document.getElementById(panelId);
        }

        function getPanelComponent() {
            return document.getElementById(panelComponentId);
        }

        function getBody() {
            return document.getElementById(bodyId);
        }

        function escapeHtml(text) {
            return String(text ?? '')
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;')
                .replace(/'/g, '&#39;');
        }

        function normalizeDataUrl(url) {
            if (!url || typeof url !== 'string') return '';
            const text = url.trim().replace(/\s+/g, '');
            if (!text.toLowerCase().startsWith('data:')) return text;

            const commaIndex = text.indexOf(',');
            if (commaIndex < 0) return text;

            const head = text.substring(0, commaIndex);
            const payload = text.substring(commaIndex + 1);
            if (!/;base64/i.test(head)) return text;

            const normalizedPayload = payload
                .replace(/-/g, '+')
                .replace(/_/g, '/');
            const padLength = normalizedPayload.length % 4;
            const paddedPayload = padLength === 0
                ? normalizedPayload
                : normalizedPayload + '='.repeat(4 - padLength);

            return `${head},${paddedPayload}`;
        }

        function normalizeIconPath(path) {
            if (!path || typeof path !== 'string') return '';
            const text = path.trim().replace(/\\/g, '/');
            if (!text) return '';
            if (text.startsWith('data:')) return normalizeDataUrl(text);
            if (text.startsWith('blob:')) return text;
            if (text.startsWith('~/')) return `/${text.substring(2).replace(/^\/+/, '')}`;
            if (text.startsWith('http://') || text.startsWith('https://') || text.startsWith('/')) return text;
            return `/${text.replace(/^\/+/, '')}`;
        }

        function flyToPoint(item) {
            if (!map || !item || !item.hasCoord) return;
            const currentZoom = typeof map.getZoom === 'function' ? map.getZoom() : 12;
            const targetZoom = Math.max(14, Math.min(16, currentZoom + 1));
            map.flyTo({
                center: [item.lng, item.lat],
                zoom: targetZoom,
                speed: 0.85,
                essential: true
            });
        }

        function bindListItemEvents() {
            const body = getBody();
            if (!body) return;

            const refreshBtn = body.querySelector('[data-command="refresh-menu-data"]');
            if (refreshBtn) {
                refreshBtn.addEventListener('click', async () => {
                    const menuId = Number(refreshBtn.getAttribute('data-menu-id'));
                    refreshBtn.disabled = true;
                    const oldText = refreshBtn.textContent;
                    refreshBtn.textContent = '刷新中...';
                    try {
                        const res = await onRefreshMenuData(Number.isFinite(menuId) ? menuId : null);
                        if ((res?.code ?? -1) !== 0) {
                            const msg = res?.message || res?.msg || '刷新失败';
                            if (window.EleManager && typeof window.EleManager.showError === 'function') {
                                window.EleManager.showError(msg);
                            }
                            return;
                        }
                    } finally {
                        refreshBtn.disabled = false;
                        refreshBtn.textContent = oldText;
                    }
                });
            }

            const buttons = body.querySelectorAll('.point-list-item[data-geometry-id]');
            buttons.forEach(btn => {
                btn.addEventListener('click', () => {
                    const id = Number(btn.getAttribute('data-geometry-id'));
                    const item = (state.pointListItems || []).find(x => Number(x.id) === id);
                    if (!item) return;

                    flyToPoint(item);
                    onSelectGeometry(id);
                });
            });
        }

        function renderList(menuName, items) {
            const body = getBody();
            if (!body) return;

            if (!Array.isArray(items) || items.length === 0) {
                body.innerHTML = '<div class="point-list-empty">该节点暂无可定位点位</div>';
                return;
            }

            const rowHtml = items
                .map(item => {
                    const title = item.alias || item.name || `点位${item.id}`;
                    const subtitle = item.name && item.alias && item.name !== item.alias ? item.name : '';
                    const addr = item.addr || '暂无地址';
                    const gps = item.gps || '无经纬度';
                    const icon = normalizeIconPath(item.icon);
                    const disableClass = item.hasCoord ? '' : ' no-coord';
                    const disableAttr = item.hasCoord ? '' : ' disabled';
                    const tip = item.hasCoord ? '点击定位到地图中心' : '该点位缺少经纬度';

                    return `
                        <button type="button" class="point-list-item${disableClass}" data-geometry-id="${item.id}" title="${escapeHtml(tip)}"${disableAttr}>
                            <div class="point-list-title-row">
                                <span class="point-list-title-main">
                                    <span class="point-list-icon-wrap${icon ? '' : ' no-icon'}">
                                        ${icon
                                            ? `<img class="point-list-icon" src="${escapeHtml(icon)}" alt="图标" onerror="this.style.display='none'; this.parentElement && this.parentElement.classList.add('img-error');"><span class="point-list-icon-fallback"></span>`
                                            : '<span class="point-list-icon-fallback"></span>'}
                                    </span>
                                    <span class="point-list-title">${escapeHtml(title)}</span>
                                </span>
                                <span class="point-list-id">#${escapeHtml(item.id)}</span>
                            </div>
                            ${subtitle ? `<div class="point-list-subtitle">${escapeHtml(subtitle)}</div>` : ''}
                            <div class="point-list-meta">${escapeHtml(addr)}</div>
                            <div class="point-list-meta">${escapeHtml(gps)}</div>
                        </button>
                    `;
                })
                .join('');

            body.innerHTML = `
                <div class="point-list-summary">
                    <span>${escapeHtml(menuName)} 共 ${items.length} 个点位</span>
                    <button type="button" class="point-list-refresh-btn" data-command="refresh-menu-data" data-menu-id="${escapeHtml(state.activePointListMenuId ?? '')}" title="刷新接口数据并更新统计">⟳ 刷新</button>
                </div>
                <div class="point-list-wrap">${rowHtml}</div>
            `;

            bindListItemEvents();
        }

        function open(menuNode, geometryItems) {
            const panel = getPanel();
            const panelComponent = getPanelComponent();
            const body = getBody();
            if (!panel || !panelComponent || !body) return;

            const menuName = menuNode?.name || `菜单${menuNode?.id || ''}`;
            const items = Array.isArray(geometryItems) ? geometryItems.slice() : [];
            items.sort((a, b) => {
                const left = (a.alias || a.name || '').localeCompare((b.alias || b.name || ''), 'zh-Hans-CN');
                if (left !== 0) return left;
                return Number(a.id || 0) - Number(b.id || 0);
            });

            state.activePointListMenuId = menuNode?.id ?? null;
            state.activePointListMenuName = menuName;
            state.pointListItems = items;

            panelComponent.setAttribute('title', menuName);

            if (closeTimer) {
                clearTimeout(closeTimer);
                closeTimer = null;
            }
            panel.classList.remove('closing');
            panel.classList.add('open');

            renderList(menuName, items);
        }

        function close() {
            const panel = getPanel();
            if (!panel) return;
            if (!panel.classList.contains('open')) return;
            panel.classList.remove('open');
            panel.classList.add('closing');
            if (closeTimer) clearTimeout(closeTimer);
            closeTimer = setTimeout(() => {
                panel.classList.remove('closing');
            }, 260);
        }

        return {
            open,
            close
        };
    }

    window.GisIndexPointList = { create };
})();
