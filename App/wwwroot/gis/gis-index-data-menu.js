(function () {
    function create(ctx) {
        const state = ctx.state;
        const onMenuBadgeClick = ctx.onMenuBadgeClick || (() => {});
        const applyGeometryVisibility = ctx.applyGeometryVisibility || (() => {});
        const getGeometryCenter = ctx.getGeometryCenter;
        const getGeometryKind = ctx.getGeometryKind;
        const getGeometryIcon = ctx.getGeometryIcon;

        function buildMenuNodes() {
            const nodes = state.menus.map(item => ({ ...item, children: [] }));
            const byId = new Map(nodes.map(n => [n.id, n]));
            const roots = [];

            nodes.forEach(node => {
                if (node.parentId && byId.has(node.parentId)) {
                    byId.get(node.parentId).children.push(node);
                } else {
                    roots.push(node);
                }
            });

            const sortFn = (a, b) => (a.sortId || 0) - (b.sortId || 0) || a.id - b.id;
            const sortTree = list => {
                list.sort(sortFn);
                list.forEach(node => sortTree(node.children || []));
            };
            sortTree(roots);
            state.menuRoots = roots;
            state.menuNodeMap = byId;

            if (!state.menuCollapseInitialized) {
                state.collapsedMenuIds = new Set(
                    nodes.filter(node => Array.isArray(node.children) && node.children.length > 0).map(node => node.id)
                );
                state.menuCollapseInitialized = true;
            }
        }

        function getMenuGeometryIds(node, cache = new Map()) {
            if (!node) return [];
            if (cache.has(node.id)) return cache.get(node.id);

            const own = state.geometryByMenuId.get(node.id) || [];
            const childIds = (node.children || []).flatMap(child => getMenuGeometryIds(child, cache));
            const result = [...own, ...childIds];
            cache.set(node.id, result);
            return result;
        }

        function getNodeGeometryItems(node, cache = new Map()) {
            if (!node) return [];
            const ids = getMenuGeometryIds(node, cache);
            if (!ids.length) return [];

            const byId = new Map((state.geometries || []).map(item => [item.id, item]));
            return ids
                .map(id => byId.get(id))
                .filter(item => !!item)
                .map(item => {
                    const center = getGeometryCenter(item);
                    const geometryType = getGeometryKind(item);
                    return {
                        id: item.id,
                        menuId: item.menuId,
                        name: item.name || '',
                        alias: item.alias || '',
                        addr: item.addr || '',
                        gps: item.gps || '',
                        icon: getGeometryIcon(item),
                        geometryType,
                        lng: center ? center.lng : null,
                        lat: center ? center.lat : null,
                        hasCoord: !!center
                    };
                });
        }

        function setMenuChecked(node, checked, cache = new Map()) {
            const ids = getMenuGeometryIds(node, cache);
            ids.forEach(id => state.geometryVisibleMap.set(id, !!checked));
            applyGeometryVisibility();
        }

        function setBatchMenusChecked(menuIds) {
            const cache = new Map();
            const checkedSet = new Set(menuIds.map(Number));

            if (state.geometries) {
                state.geometries.forEach(g => state.geometryVisibleMap.set(g.id, false));
            }

            checkedSet.forEach(menuId => {
                const node = state.menuNodeMap.get(menuId);
                if (node) {
                    const ids = getMenuGeometryIds(node, cache);
                    ids.forEach(id => state.geometryVisibleMap.set(id, true));
                }
            });

            applyGeometryVisibility();
            renderMenuTree();
        }

        function isMenuCollapsed(nodeId) {
            return !!state.collapsedMenuIds?.has(nodeId);
        }

        function toggleMenuCollapsed(nodeId) {
            if (!state.collapsedMenuIds) state.collapsedMenuIds = new Set();
            if (state.collapsedMenuIds.has(nodeId)) state.collapsedMenuIds.delete(nodeId);
            else state.collapsedMenuIds.add(nodeId);
            state.lastMenuToggle = {
                nodeId,
                collapsed: state.collapsedMenuIds.has(nodeId)
            };
            renderMenuTree();
        }

        function renderMenuTree() {
            const container = document.getElementById('menu-tree');
            if (!container) return;
            container.innerHTML = '';

            if (!state.menuRoots || state.menuRoots.length === 0) {
                const msg = state.menuLoadErrorMessage || '加载中...';
                container.innerHTML = `<div class="menu-empty">${msg}</div>`;
                return;
            }

            const geometryCache = new Map();
            const formatDataDt = value => {
                if (!value) return '';
                const dt = new Date(value);
                if (Number.isNaN(dt.getTime())) return '';
                const yy = dt.getFullYear();
                const mm = String(dt.getMonth() + 1).padStart(2, '0');
                const dd = String(dt.getDate()).padStart(2, '0');
                const hh = String(dt.getHours()).padStart(2, '0');
                const mi = String(dt.getMinutes()).padStart(2, '0');
                return `${yy}-${mm}-${dd} ${hh}:${mi}`;
            };

            const renderNode = (node, depth) => {
                const row = document.createElement('div');
                row.className = 'menu-node';
                row.style.marginLeft = `${depth * 14}px`;
                const children = node.children || [];
                const hasChildren = children.length > 0;
                const isCollapsed = hasChildren && isMenuCollapsed(node.id);

                const ids = getMenuGeometryIds(node, geometryCache);
                const visibleCount = ids.filter(id => state.geometryVisibleMap.get(id) !== false).length;
                const menuCount = Number(node.dataCnt ?? 0);
                const count = Number.isFinite(menuCount) && menuCount >= 0 ? menuCount : 0;

                if (hasChildren) {
                    const shouldAnimate = state.lastMenuToggle && state.lastMenuToggle.nodeId === node.id;
                    const animationClass = shouldAnimate
                        ? (state.lastMenuToggle.collapsed ? 'animating-collapse' : 'animating-expand')
                        : '';
                    const toggleBtn = document.createElement('button');
                    toggleBtn.type = 'button';
                    toggleBtn.className = `menu-node-toggle ${isCollapsed ? 'collapsed' : 'expanded'} ${animationClass}`.trim();
                    toggleBtn.title = isCollapsed ? '展开子节点' : '折叠子节点';
                    toggleBtn.setAttribute('aria-label', toggleBtn.title);
                    toggleBtn.innerHTML = '<span class="menu-node-toggle-icon" aria-hidden="true">▶</span>';
                    toggleBtn.addEventListener('click', evt => {
                        evt.stopPropagation();
                        toggleMenuCollapsed(node.id);
                    });
                    row.appendChild(toggleBtn);
                } else {
                    const togglePlaceholder = document.createElement('span');
                    togglePlaceholder.className = 'menu-node-toggle empty';
                    togglePlaceholder.setAttribute('aria-hidden', 'true');
                    row.appendChild(togglePlaceholder);
                }

                const checkbox = document.createElement('input');
                checkbox.type = 'checkbox';
                checkbox.className = 'menu-node-check';
                checkbox.disabled = count === 0;
                checkbox.checked = ids.length > 0 && visibleCount === ids.length;
                checkbox.indeterminate = visibleCount > 0 && visibleCount < ids.length;
                checkbox.addEventListener('change', () => setMenuChecked(node, !!checkbox.checked, geometryCache));

                const labelBtn = document.createElement('button');
                labelBtn.type = 'button';
                labelBtn.className = 'menu-node-label';
                labelBtn.textContent = node.name || `菜单${node.id}`;
                labelBtn.addEventListener('click', () => {
                    if (checkbox.disabled) return;
                    checkbox.checked = !checkbox.checked;
                    checkbox.dispatchEvent(new Event('change'));
                });

                row.appendChild(checkbox);
                row.appendChild(labelBtn);

                const badge = document.createElement('button');
                badge.type = 'button';
                badge.className = `menu-node-count ${count > 0 ? '' : 'empty'}`;
                badge.textContent = `${count}`;
                const nodeLabel = node.name || `菜单${node.id}`;
                const dtText = formatDataDt(node.dataDt);
                badge.title = count > 0
                    ? `查看${nodeLabel}点位清单${dtText ? `（统计时间: ${dtText}）` : ''}`
                    : '无点位';
                badge.disabled = count === 0;
                badge.addEventListener('click', evt => {
                    evt.stopPropagation();
                    if (count === 0) return;
                    const items = getNodeGeometryItems(node, geometryCache);
                    onMenuBadgeClick(node, items);
                });
                row.appendChild(badge);

                container.appendChild(row);
                if (!isCollapsed) {
                    children.forEach(child => renderNode(child, depth + 1));
                }
            };

            state.menuRoots.forEach(node => renderNode(node, 0));
            state.lastMenuToggle = null;
        }

        async function loadMenus() {
            state.menuLoadErrorMessage = '';
            try {
                const resp = await fetch('?handler=MenuData');
                if (!resp.ok) {
                    state.menuRoots = [];
                    state.menuNodeMap = new Map();
                    state.menuLoadErrorMessage = `目录加载失败(${resp.status})`;
                    renderMenuTree();
                    return;
                }
                const res = await resp.json();
                const code = res?.code ?? res?.Code ?? 0;
                const rawData = res?.data ?? res?.Data;
                if (code !== 0 || !Array.isArray(rawData)) {
                    state.menuRoots = [];
                    state.menuNodeMap = new Map();
                    state.menuLoadErrorMessage = '目录数据格式不正确';
                    renderMenuTree();
                    return;
                }

                const flatMenus = [];
                const walk = nodes => {
                    (nodes || []).forEach(n => {
                        const idRaw = n.id ?? n.Id;
                        const parentIdRaw = n.parentId ?? n.ParentId;
                        const sortIdRaw = n.sortId ?? n.SortId;
                        const id = Number(idRaw);
                        const parentId = parentIdRaw === null || parentIdRaw === undefined || parentIdRaw === ''
                            ? null
                            : Number(parentIdRaw);

                        if (Number.isNaN(id)) return;

                        flatMenus.push({
                            id,
                            parentId,
                            name: n.name ?? n.Name,
                            sortId: Number(sortIdRaw || 0),
                            icon: n.icon ?? n.Icon,
                            isDefaultShow: !!(n.isDefaultShow ?? n.IsDefaultShow),
                            dataCnt: Number(n.dataCnt ?? n.DataCnt ?? 0),
                            dataDt: n.dataDt ?? n.DataDt ?? null
                        });
                        walk(n.children || n.Children || []);
                    });
                };
                walk(rawData);

                state.menus = flatMenus;
                buildMenuNodes();
                if (flatMenus.length === 0) {
                    state.menuLoadErrorMessage = '加载中...';
                }
                renderMenuTree();
            } catch {
                state.menuRoots = [];
                state.menuNodeMap = new Map();
                state.menuLoadErrorMessage = '目录加载失败';
                renderMenuTree();
            }
        }

        function isGeometryDefaultVisible(menuId) {
            if (!menuId || !state.menuNodeMap || state.menuNodeMap.size === 0) return false;

            let current = state.menuNodeMap.get(menuId);
            while (current) {
                if (current.isDefaultShow === true) return true;
                if (!current.parentId) break;
                current = state.menuNodeMap.get(current.parentId);
            }

            return false;
        }

        return {
            buildMenuNodes,
            renderMenuTree,
            loadMenus,
            setBatchMenusChecked,
            isGeometryDefaultVisible
        };
    }

    window.GisIndexDataMenu = { create };
})();
