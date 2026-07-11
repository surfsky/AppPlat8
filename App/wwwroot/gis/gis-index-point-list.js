/***
 * 点列表面板相关逻辑
*/
(function () {
    function create(ctx) {
        const panelId = ctx.panelId || 'geo-point-list-panel';
        const panelComponentId = ctx.panelComponentId || 'geo-point-list-gis-panel';
        const bodyId = ctx.bodyId || 'geo-point-list-body';
        const map = ctx.map;
        const state = ctx.state;
        const onSelectGeometry = ctx.onSelectGeometry || (() => {});
        const onOpenDetail = ctx.onOpenDetail || (() => {});
        const onLoadMenuItems = ctx.onLoadMenuItems || null;
        const onItemsChanged = ctx.onItemsChanged || (() => {});
        const onClose = ctx.onClose || (() => {});
        let closeTimer = null;
        let currentMenuNode = null;
        let currentKeyword = '';
        let currentIsVisible = '';
        let currentPageIndex = 0;
        let currentPageSize = 20;
        let currentTotal = 0;
        let loading = false;
        let sourceItems = [];

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

        function normalizeDataJsonText(raw) {
            let text = String(raw || '').trim();
            if (!text) return '';
            if ((text.startsWith("'") && text.endsWith("'")) || (text.startsWith('"') && text.endsWith('"'))) {
                text = text.substring(1, text.length - 1).trim();
            }
            return text;
        }

        function isStyleDataKey(key) {
            const name = String(key || '').trim().toLowerCase();
            if (!name) return true;
            return [
                'stroke', 'strock', 'stroke-width', 'fill', 'fill-opacity', 'marker-color',
                'linecolor', 'linewidth', 'fillcolor', 'fillopacity', 'pointcolor',
                'label', 'labelcolor', 'scale', 'icon', 'iconpath',
                'color', 'opacity'
            ].includes(name);
        }

        function getDataJsonValueText(item) {
            const text = normalizeDataJsonText(item?.dataJson);
            if (!text) return '';
            let obj = null;
            try {
                obj = JSON.parse(text);
            } catch {
                return '';
            }
            if (!obj || typeof obj !== 'object' || Array.isArray(obj)) return '';

            const values = Object.entries(obj)
                .filter(([key, value]) => !isStyleDataKey(key) && value !== null && value !== undefined)
                .map(([, value]) => {
                    if (typeof value === 'string') return value.trim();
                    if (typeof value === 'number' || typeof value === 'boolean') return String(value);
                    return '';
                })
                .filter(Boolean);

            return values.join(' / ');
        }

        function flyToPoint(item) {
            if (!map || !item || !item.hasCoord) return;
            map.flyTo({
                center: [item.lng, item.lat],
                speed: 0.85,
                essential: true
            });
        }

        function mergeItems(oldItems, newItems) {
            const list = Array.isArray(oldItems) ? oldItems.slice() : [];
            const exists = new Set(list.map(item => String(item.id)));
            (newItems || []).forEach(item => {
                const key = String(item.id);
                if (exists.has(key)) return;
                exists.add(key);
                list.push(item);
            });
            return list;
        }

        function filterLocalItems(items, keyword) {
            const kw = String(keyword || '').trim().toLowerCase();
            if (!kw) return Array.isArray(items) ? items.slice() : [];
            return (items || []).filter(item => {
                const text = [
                    item?.name,
                    item?.alias,
                    item?.addr,
                    item?.gps,
                    item?.menuName
                ].join(' ').toLowerCase();
                return text.includes(kw);
            });
        }

        function matchVisible(item) {
            if (currentIsVisible === 'true') return item?.isVisible !== false;
            if (currentIsVisible === 'false') return item?.isVisible === false;
            return true;
        }

        function parseGps(item) {
            if (!item) return null;
            const gps = String(item.gps || '').trim();
            if (!gps) return null;
            const parts = gps.replace(/，|；|;/g, ',').split(',').map(x => x.trim()).filter(Boolean);
            if (parts.length < 2) return null;
            const lng = Number(parts[0]);
            const lat = Number(parts[1]);
            if (!Number.isFinite(lng) || !Number.isFinite(lat)) return null;
            return { lng, lat };
        }

        function mapMenuItems(items, startIndex) {
            const start = Number.isFinite(Number(startIndex)) ? Number(startIndex) : 0;
            return (items || []).map((item, idx) => {
                const point = parseGps(item);
                return {
                    ...item,
                    lng: point ? point.lng : null,
                    lat: point ? point.lat : null,
                    hasCoord: !!point,
                    seq: start + idx + 1
                };
            });
        }

        function buildPanelSubtitle(loadedCount, totalCount) {
            const loaded = Math.max(0, Number(loadedCount) || 0);
            const total = Math.max(0, Number(totalCount) || 0);
            return `(${loaded}/${total || loaded})`;
        }

        function syncPanelHeader(menuName, loadedCount, totalCount) {
            const panelComponent = getPanelComponent();
            if (!panelComponent) return;
            panelComponent.setAttribute('title', menuName || '点位清单');
            panelComponent.setAttribute('subtitle', buildPanelSubtitle(loadedCount, totalCount));
        }

        function bindListItemEvents() {
            const body = getBody();
            if (!body) return;

            const searchBtn = body.querySelector('[data-command="search-menu-items"]');
            const searchInput = body.querySelector('[data-role="point-list-search"]');
            const visibleSelect = body.querySelector('[data-role="point-list-visible"]');
            if (searchBtn && searchInput) {
                searchBtn.addEventListener('click', () => {
                    currentKeyword = String(searchInput.value || '').trim();
                    if (visibleSelect) currentIsVisible = String(visibleSelect.value || '');
                    currentPageIndex = 0;
                    loadItems(true);
                });
                searchInput.addEventListener('keydown', e => {
                    if (e.key !== 'Enter') return;
                    currentKeyword = String(searchInput.value || '').trim();
                    if (visibleSelect) currentIsVisible = String(visibleSelect.value || '');
                    currentPageIndex = 0;
                    loadItems(true);
                });
            }
            if (visibleSelect) {
                visibleSelect.addEventListener('change', () => {
                    currentIsVisible = String(visibleSelect.value || '');
                    currentPageIndex = 0;
                    loadItems(true);
                });
            }

            const loadMoreBtn = body.querySelector('[data-command="load-more-items"]');
            if (loadMoreBtn) {
                loadMoreBtn.addEventListener('click', () => {
                    if (loading) return;
                    currentPageIndex += 1;
                    loadItems(false);
                });
            }

            body.onscroll = () => {
                if (loading) return;
                if ((state.pointListItems || []).length >= currentTotal) return;
                const remain = body.scrollHeight - body.scrollTop - body.clientHeight;
                if (remain > 40) return;
                currentPageIndex += 1;
                loadItems(false);
            };

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

            const detailButtons = body.querySelectorAll('.point-list-detail-btn[data-geometry-id]');
            detailButtons.forEach(btn => {
                btn.addEventListener('click', evt => {
                    evt.stopPropagation();
                    const id = Number(btn.getAttribute('data-geometry-id'));
                    if (!Number.isFinite(id)) return;
                    onOpenDetail(id);
                });
            });
        }

        function renderList(menuName, items) {
            syncPanelHeader(menuName, Array.isArray(items) ? items.length : 0, currentTotal || (Array.isArray(items) ? items.length : 0));
            const body = getBody();
            if (!body) return;
            const visibleOptions = `
                <option value=""${currentIsVisible === '' ? ' selected' : ''}>全部</option>
                <option value="true"${currentIsVisible === 'true' ? ' selected' : ''}>可见</option>
                <option value="false"${currentIsVisible === 'false' ? ' selected' : ''}>隐藏</option>
            `;

            if (!Array.isArray(items) || items.length === 0) {
                body.innerHTML = `
                    <div class="point-list-tools">
                        <input class="point-list-search" data-role="point-list-search" value="${escapeHtml(currentKeyword)}" placeholder="检索名称、地址、坐标" />
                        <select class="point-list-visible" data-role="point-list-visible">${visibleOptions}</select>
                        <button type="button" class="point-list-search-btn" data-command="search-menu-items">查询</button>
                    </div>
                    <div class="point-list-empty">该节点暂无点位数据</div>
                `;
                bindListItemEvents();
                return;
            }

            const rowHtml = items
                .map(item => {
                    const title = item.alias || item.name || `点位${item.id}`;
                    const subtitle = item.name && item.alias && item.name !== item.alias ? item.name : '';
                    const addr = item.addr || '';
                    const dataText = getDataJsonValueText(item);
                    const metaText = [addr, dataText].filter(Boolean).join(' / ') || '暂无地址';
                    const disableClass = item.hasCoord ? '' : ' no-coord';
                    const tip = item.hasCoord ? '点击定位到地图中心' : '该点位缺少经纬度';
                    const seq = escapeHtml(item.seq ?? '');

                    return `
                        <div class="point-list-item${disableClass}" data-geometry-id="${item.id}" title="${escapeHtml(tip)}">
                            <div class="point-list-main">
                                <span class="point-list-no">${seq}</span>
                                <span class="point-list-texts">
                                    <span class="point-list-title-row">
                                        <span class="point-list-title">${escapeHtml(title)}</span>
                                        <button type="button" class="point-list-detail-btn" data-geometry-id="${item.id}" title="查看详情">
                                            <i class="fa-regular fa-file-lines"></i>
                                        </button>
                                    </span>
                                    ${subtitle ? `<span class="point-list-subtitle">${escapeHtml(subtitle)}</span>` : ''}
                                    <span class="point-list-meta">${escapeHtml(metaText)}</span>
                                </span>
                            </div>
                        </div>
                    `;
                })
                .join('');

            body.innerHTML = `
                <div class="point-list-tools">
                    <input class="point-list-search" data-role="point-list-search" value="${escapeHtml(currentKeyword)}" placeholder="检索名称、地址、坐标" />
                    <select class="point-list-visible" data-role="point-list-visible">${visibleOptions}</select>
                    <button type="button" class="point-list-search-btn" data-command="search-menu-items">查询</button>
                </div>
                <div class="point-list-wrap">${rowHtml}</div>
                <div class="point-list-pager">
                    <span>第 ${currentPageIndex + 1} 页</span>
                    <button type="button" class="point-list-loadmore-btn" data-command="load-more-items"${items.length >= currentTotal ? ' disabled' : ''}>上滑换页 / 加载更多</button>
                </div>
            `;

            bindListItemEvents();
        }

        async function loadItems(reset) {
            if (!currentMenuNode) return;
            if (loading) return;
            loading = true;

            try {
                if (typeof onLoadMenuItems === 'function') {
                    const res = await onLoadMenuItems({
                        menuId: currentMenuNode.id,
                        keyword: currentKeyword,
                        isVisible: currentIsVisible,
                        pageIndex: currentPageIndex,
                        pageSize: currentPageSize
                    });
                    const code = res?.code ?? res?.Code ?? -1;
                    const data = res?.data ?? res?.Data ?? {};
                    if (code !== 0) {
                        if (!reset) currentPageIndex = Math.max(0, currentPageIndex - 1);
                        if (window.EleManager && typeof window.EleManager.showError === 'function') {
                            window.EleManager.showError(res?.message || res?.msg || '加载点位失败');
                        }
                        return;
                    }

                    const items = Array.isArray(data.items) ? data.items : [];
                    const pageInfo = data.pageInfo || {};
                    const mapIds = Array.isArray(data.mapIds) ? data.mapIds : [];
                    const start = currentPageIndex * currentPageSize;
                    const mappedItems = mapMenuItems(items, start);
                    currentTotal = Number(pageInfo.total ?? mappedItems.length) || mappedItems.length;
                    state.pointListItems = reset ? mappedItems : mergeItems(state.pointListItems, mappedItems);
                    onItemsChanged({
                        menuId: currentMenuNode?.id ?? null,
                        mapIds,
                        isVisible: currentIsVisible,
                        keyword: currentKeyword
                    });
                    renderList(currentMenuNode?.name || '', state.pointListItems);
                    return;
                }

                const filtered = filterLocalItems(sourceItems, currentKeyword).filter(matchVisible);
                currentTotal = filtered.length;
                const start = currentPageIndex * currentPageSize;
                const pageItems = mapMenuItems(filtered.slice(start, start + currentPageSize), start);
                state.pointListItems = reset ? pageItems : mergeItems(state.pointListItems, pageItems);
                onItemsChanged({
                    menuId: currentMenuNode?.id ?? null,
                    mapIds: filtered.map(item => item.id),
                    isVisible: currentIsVisible,
                    keyword: currentKeyword
                });
                renderList(currentMenuNode?.name || '', state.pointListItems);
            } finally {
                loading = false;
            }
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

            currentMenuNode = menuNode || null;
            currentKeyword = '';
            currentIsVisible = '';
            currentPageIndex = 0;
            currentTotal = items.length;
            sourceItems = items;
            state.activePointListMenuId = menuNode?.id ?? null;
            state.activePointListMenuName = menuName;
            state.pointListItems = [];
            syncPanelHeader(menuName, 0, currentTotal || items.length);

            if (closeTimer) {
                clearTimeout(closeTimer);
                closeTimer = null;
            }
            panel.classList.remove('closing');
            panel.classList.add('open');

            loadItems(true);
        }

        function close(options) {
            const panel = getPanel();
            const panelComponent = getPanelComponent();
            if (!panel) return;
            if (!panel.classList.contains('open')) return;
            onClose(options || {});
            if (panelComponent) {
                panelComponent.setAttribute('subtitle', '');
            }
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
