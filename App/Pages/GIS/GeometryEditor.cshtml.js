const { createApp, ref, onMounted, onBeforeUnmount } = Vue;

createApp({
    setup() {
        const cfg = window.__geometryEditorConfig || {};
        const accessToken = cfg.accessToken || '';
        const initGeoJson = cfg.initGeoJson || '';
        const storageKey = '__gis_geometry_editor_reference_gps';
        const parseGpsText = (text) => {
            const raw = String(text || '').replace(/，|；|;/g, ',').trim();
            if (!raw) return null;
            const parts = raw.split(',').map(t => t.trim()).filter(Boolean);
            if (parts.length < 2) return null;
            const lng = Number(parts[0]);
            const lat = Number(parts[1]);
            if (!Number.isFinite(lng) || !Number.isFinite(lat)) return null;
            return [lng, lat];
        };
        const search = new URLSearchParams(window.location.search || '');
        const readRefGpsFromStorage = () => {
            const wins = [window, window.parent, window.top].filter(Boolean);
            for (let i = 0; i < wins.length; i += 1) {
                const win = wins[i];
                try {
                    const storages = [win.sessionStorage, win.localStorage].filter(Boolean);
                    for (let j = 0; j < storages.length; j += 1) {
                        const v = storages[j].getItem(storageKey) || '';
                        if (String(v).trim()) return String(v).trim();
                    }
                } catch {
                    // ignore cross-frame or storage errors
                }
            }
            return '';
        };
        const referenceGps = search.get('gps')
            || readRefGpsFromStorage();
        const referenceCenter = parseGpsText(referenceGps);
        const initialCenter = referenceCenter || (Array.isArray(cfg.initialCenter) ? cfg.initialCenter : [120.6034, 27.5686]);
        const initialZoom = Number.isFinite(Number(cfg.initialZoom)) ? Number(cfg.initialZoom) : 11;

        const pointColor = ref('#dc2626');
        const lineColor = ref('#1e3a8a');
        const fillColor = ref('#1d4ed8');
        const fillOpacityPercent = ref(35);
        const activeTool = ref('browse');
        const showOpacitySlider = ref(false);
        const menuVisible = ref(false);
        const menuX = ref(0);
        const menuY = ref(0);

        let editor = null;
        let keydownHandler = null;
        let mapContextMenuHandler = null;
        let mapDblClickHandler = null;
        let hideMenuHandler = null;
        let touchStartHandler = null;
        let touchMoveHandler = null;
        let touchEndHandler = null;
        let longPressTimer = null;
        let longPressTriggered = false;
        let touchStartPoint = null;
        let suppressDblClickLabelUntil = 0;
        let pendingLabelPromptTimer = null;
        const pageMode = (() => {
            try {
                const md = new URLSearchParams(window.location.search || '').get('md') || '';
                return String(md).trim().toLowerCase();
            } catch {
                return '';
            }
        })();
        const isViewMode = pageMode === 'view' || pageMode === 'readonly' || pageMode === 'read';

        const getManager = () => (window.top && window.top.EleManager) ? window.top.EleManager : window.EleManager;

        const showError = (msg) => {
            const manager = getManager();
            if (manager && typeof manager.showError === 'function') {
                manager.showError(msg);
            } else {
                alert(msg);
            }
        };

        const showWarning = (msg) => {
            const manager = getManager();
            if (manager && typeof manager.showWarning === 'function') {
                manager.showWarning(msg);
            } else {
                alert(msg);
            }
        };
        const enterBrowseMode = () => {
            activeTool.value = 'browse';
            editor?.browseMode?.();
        };

        const drawPoint = () => {
            activeTool.value = 'point';
            editor?.drawPoint?.();
        };

        const drawLine = () => {
            activeTool.value = 'line';
            editor?.drawLine?.();
        };

        const drawPolygon = () => {
            activeTool.value = 'polygon';
            editor?.drawPolygon?.();
        };

        const drawRectangle = () => {
            showOpacitySlider.value = false;
            activeTool.value = 'rectangle';
            editor?.drawRectangle?.();
        };

        const drawCircle = () => {
            showOpacitySlider.value = false;
            activeTool.value = 'circle';
            editor?.drawCircle?.();
        };
        const onPointColorChange = (color) => editor?.setPointColor(color || pointColor.value);
        const onLineColorChange = (color) => editor?.setLineColor(color || lineColor.value);
        const onFillColorChange = (color) => editor?.setFillColor(color || fillColor.value);
        const onFillOpacityChange = (value) => editor?.setFillOpacityPercent(value ?? fillOpacityPercent.value);
        const onOpacitySliderInput = (evt) => {
            const v = Number(evt?.target?.value ?? fillOpacityPercent.value);
            if (!Number.isFinite(v)) return;
            fillOpacityPercent.value = Math.max(0, Math.min(100, Math.round(v)));
            onFillOpacityChange(fillOpacityPercent.value);
        };

        const toggleOpacitySlider = () => {
            showOpacitySlider.value = !showOpacitySlider.value;
        };

        const removeSelected = () => editor?.removeSelected();
        const clearAll = () => editor?.clearAll();
        const hideMenu = () => {
            menuVisible.value = false;
            showOpacitySlider.value = false;
        };

        const showMenuAt = (clientX, clientY) => {
            const menuWidth = 160;
            const menuHeight = 240;
            const x = Math.max(8, Math.min((clientX || 0), window.innerWidth - menuWidth));
            const y = Math.max(8, Math.min((clientY || 0), window.innerHeight - menuHeight));
            menuX.value = x;
            menuY.value = y;
            menuVisible.value = true;
        };

        const createStorageToken = () => `sk_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 8)}`;

        const cleanupSelectorStorage = (maxKeep = 120) => {
            const storages = [window.localStorage, window.sessionStorage].filter(Boolean);
            const shouldManage = (k) => typeof k === 'string' && (k.startsWith('sk_') || k.startsWith('ele_selector_'));
            storages.forEach((storage) => {
                try {
                    const keys = [];
                    for (let i = 0; i < storage.length; i += 1) {
                        const k = storage.key(i);
                        if (shouldManage(k)) keys.push(k);
                    }
                    if (keys.length <= maxKeep) return;
                    keys.sort((a, b) => {
                        const ta = storage.getItem(`${a}__t`) || '0';
                        const tb = storage.getItem(`${b}__t`) || '0';
                        return Number(ta) - Number(tb);
                    });
                    const removeCount = keys.length - maxKeep;
                    for (let i = 0; i < removeCount; i += 1) {
                        const k = keys[i];
                        storage.removeItem(k);
                        storage.removeItem(`${k}__t`);
                    }
                } catch {
                    // ignore
                }
            });
        };

        const writePayloadToStorage = (text) => {
            const token = createStorageToken();
            const storages = [window.localStorage, window.sessionStorage].filter(Boolean);
            for (let i = 0; i < storages.length; i += 1) {
                const storage = storages[i];
                try {
                    storage.setItem(token, text);
                    storage.setItem(`${token}__t`, String(Date.now()));
                    return token;
                } catch {
                    cleanupSelectorStorage(80);
                    try {
                        storage.setItem(token, text);
                        storage.setItem(`${token}__t`, String(Date.now()));
                        return token;
                    } catch {
                        // try next storage
                    }
                }
            }
            return '';
        };

        const openPropsDrawer = () => {
            if (!editor || !editor.hasActiveSelection?.()) {
                showWarning('请先选中一个图形');
                return;
            }
            const props = editor.getSelectedProperties?.() || {};
            const manager = getManager();
            const md = isViewMode ? 'view' : 'edit';
            const serialized = JSON.stringify(props || {});
            let url = `/GIS/PropsEditor?md=${md}`;
            if (typeof window !== 'undefined') {
                const key = writePayloadToStorage(serialized);
                if (key) {
                    url += `&dk=${encodeURIComponent(key)}`;
                } else {
                    url += `&data=${encodeURIComponent(serialized)}`;
                }
            } else {
                url += `&data=${encodeURIComponent(serialized)}`;
            }

            const applySelectorPayload = (selectorData) => {
                if (!selectorData || selectorData.type !== 'ElePicker') return;
                const list = Array.isArray(selectorData.data) ? selectorData.data : [];
                if (list.length === 0) return;
                const raw = list[0] && list[0].id !== undefined ? String(list[0].id) : '';
                if (!raw) return;
                let parsed = {};
                try {
                    const first = JSON.parse(raw);
                    if (first && typeof first === 'object' && !Array.isArray(first)) parsed = first;
                    else if (typeof first === 'string') {
                        const second = JSON.parse(first);
                        if (second && typeof second === 'object' && !Array.isArray(second)) parsed = second;
                    }
                } catch {
                    parsed = {};
                }
                if (typeof editor.setSelectedProperties === 'function') {
                    editor.setSelectedProperties(parsed);
                }
            };

            if (manager && typeof manager.openDrawer === 'function') {
                manager.openDrawer({
                    title: '属性',
                    url,
                    direction: 'rtl',
                    resizable: true,
                    closeOnClickModal: false,
                    destroyOnClose: true,
                    closeHandler: (payload) => {
                        if (payload && payload.data && payload.data.type === 'ElePicker') {
                            applySelectorPayload(payload.data);
                        }
                    }
                });
                return;
            }
            window.open(url, '_blank');
        };

        const isTypingTarget = (el) => {
            if (!el) return false;
            const tag = (el.tagName || '').toLowerCase();
            if (tag === 'input' || tag === 'textarea' || tag === 'select') return true;
            if (el.isContentEditable) return true;
            return typeof el.closest === 'function' && !!el.closest('.el-input,.el-textarea,.el-message-box');
        };

        const askLabelText = async (initialValue) => {
            const manager = getManager();
            if (manager && typeof manager.prompt === 'function') {
                const result = await manager.prompt('请输入标签文本', '设置标签', {
                    inputValue: initialValue || '',
                    inputPlaceholder: '例如: 龙港大道',
                    confirmButtonText: '确定',
                    cancelButtonText: '取消'
                });
                return (result && typeof result.value === 'string') ? result.value : '';
            }
            const value = window.prompt('请输入标签文本', initialValue || '');
            if (value === null || value === undefined) throw new Error('cancel');
            return String(value);
        };

        const confirmClearLabel = async () => {
            const manager = getManager();
            if (manager && typeof manager.confirm === 'function') {
                try {
                    await manager.confirm('标签将被清空，是否继续？', '确认清空');
                    return true;
                } catch {
                    return false;
                }
            }
            return window.confirm('标签将被清空，是否继续？');
        };

        const editSelectedLabel = async () => {
            if (!editor || !editor.hasActiveSelection?.()) {
                showWarning('请先选中一个图形');
                return;
            }

            try {
                const oldLabel = String(editor.getSelectedLabel?.() || '').trim();
                const inputValue = await askLabelText(oldLabel);
                const nextLabel = String(inputValue || '').trim();
                if (nextLabel === '' && oldLabel !== '') {
                    const ok = await confirmClearLabel();
                    if (!ok) return;
                }
                editor.setSelectedLabel?.(nextLabel);
            } catch {
                // user canceled prompt
            }
        };

        const promptLabelForCreatedFeature = async (info) => {
            const tool = String(info?.tool || '').trim().toLowerCase();
            const geometryType = String(info?.geometryType || '').trim().toLowerCase();
            const shouldPrompt = tool === 'rectangle' || tool === 'circle' || geometryType === 'point' || geometryType === 'linestring';
            if (!shouldPrompt) return;

            const featureId = info?.featureId;
            if (featureId !== undefined && featureId !== null) {
                editor?.selectFeatureById?.(featureId);
            }

            suppressDblClickLabelUntil = Date.now() + 450;
            await editSelectedLabel();
        };

        const schedulePromptLabelForCreatedFeature = (info) => {
            if (pendingLabelPromptTimer) {
                window.clearTimeout(pendingLabelPromptTimer);
                pendingLabelPromptTimer = null;
            }
            pendingLabelPromptTimer = window.setTimeout(() => {
                pendingLabelPromptTimer = null;
                promptLabelForCreatedFeature(info).catch(() => {
                    // ignore prompt cancellation/errors
                });
            }, 0);
        };

        const setLabelFromMenu = async () => {
            hideMenu();
            await editSelectedLabel();
        };

        const removeSelectedFromMenu = () => {
            hideMenu();
            removeSelected();
        };

        const openPropsDrawerFromMenu = () => {
            hideMenu();
            openPropsDrawer();
        };

        const duplicateSelected = () => {
            if (!editor || typeof editor.duplicateSelected !== 'function') {
                showWarning('复制功能暂不可用，请刷新后重试');
                return;
            }
            const copiedCount = editor.duplicateSelected();
            if (!copiedCount) {
                showWarning('请先选中一个图形');
            }
        };

        const duplicateSelectedFromMenu = () => {
            hideMenu();
            duplicateSelected();
        };

        const undoEdit = () => {
            if (!editor || typeof editor.undo !== 'function') {
                showWarning('撤销功能暂不可用');
                return;
            }
            const ok = editor.undo();
            if (!ok) {
                showWarning('没有可撤销的操作');
            }
        };

        const redoEdit = () => {
            if (!editor || typeof editor.redo !== 'function') {
                showWarning('恢复功能暂不可用');
                return;
            }
            const ok = editor.redo();
            if (!ok) {
                showWarning('没有可恢复的操作');
            }
        };

        const undoFromMenu = () => {
            hideMenu();
            undoEdit();
        };

        const redoFromMenu = () => {
            hideMenu();
            redoEdit();
        };

        const closeOnly = () => {
            const manager = getManager();
            if (manager && typeof manager.closePage === 'function') {
                manager.closePage({});
                return;
            }
            if (window.parent) {
                window.parent.postMessage({ __elePageClose: true, data: {} }, '*');
            }
            if (window.top && window.top !== window.parent) {
                window.top.postMessage({ __elePageClose: true, data: {} }, '*');
            }
        };

        const confirmGeometry = () => {
            try {
                const geojson = editor?.getExportGeoJson?.();
                if (!geojson) throw new Error('请先绘制图形');

                const payload = {
                    type: 'ElePicker',
                    data: [{ id: geojson, name: geojson }]
                };

                const manager = getManager();
                if (manager && typeof manager.closePage === 'function') {
                    manager.closePage(payload);
                    return;
                }
                if (window.parent) {
                    window.parent.postMessage(payload, '*');
                }
                if (window.top && window.top !== window.parent) {
                    window.top.postMessage(payload, '*');
                }
            } catch (e) {
                showWarning(e?.message || '当前图形不满足导出条件');
            }
        };

        onMounted(() => {
            editor = window.MapGeometryHelper.createEditor({
                accessToken,
                initGeoJson,
                initialCenter,
                initialZoom,
                referenceGps,
                mapContainerId: 'map',
                labelPriorityFields: ['NAME', 'name', 'label', 'SZSQ', 'SZZ', 'SZQX', 'title', 'alias', 'text'],
                onError: showError,
                onMapContextMenu: (ctx) => {
                    const picked = ctx?.pickedId;
                    if (!picked) {
                        hideMenu();
                        return;
                    }
                    if (ctx?.event?.originalEvent) {
                        ctx.event.originalEvent.preventDefault();
                    }
                    showMenuAt(ctx?.clientX ?? 0, ctx?.clientY ?? 0);
                },
                onMapDblClick: (ctx) => {
                    const picked = ctx?.pickedId;
                    if (!picked) return;
                    if (ctx?.event?.originalEvent) {
                        ctx.event.originalEvent.preventDefault();
                    }
                    hideMenu();
                    editSelectedLabel();
                },
                onPaletteChange: (v) => {
                    pointColor.value = v.pointColor;
                    lineColor.value = v.lineColor;
                    fillColor.value = v.fillColor;
                    fillOpacityPercent.value = v.fillOpacityPercent;
                },
                onToolStateChange: (info) => {
                    const tool = String(info?.tool || '').trim().toLowerCase();
                    activeTool.value = tool || 'browse';
                },
                onFeatureCreated: (info) => {
                    schedulePromptLabelForCreatedFeature(info);
                }
            });
            editor.init();

            keydownHandler = (evt) => {
                if (isTypingTarget(evt.target)) return;
                const key = (evt.key || '').toLowerCase();
                const isMac = /mac|iphone|ipad|ipod/i.test(navigator.platform || '');
                const cmdOrCtrl = isMac ? evt.metaKey : evt.ctrlKey;
                const isUndoShortcut = cmdOrCtrl && !evt.shiftKey && key === 'z';
                const isRedoShortcut = (cmdOrCtrl && evt.shiftKey && key === 'z') || (!isMac && evt.ctrlKey && key === 'y');

                if (isUndoShortcut) {
                    evt.preventDefault();
                    undoEdit();
                    hideMenu();
                    return;
                }

                if (isRedoShortcut) {
                    evt.preventDefault();
                    redoEdit();
                    hideMenu();
                    return;
                }

                if (key === 'escape' || evt.keyCode === 27) {
                    const canceled = editor?.cancelDrawing?.();
                    if (canceled) {
                        evt.preventDefault();
                        hideMenu();
                        return;
                    }
                }

                const isDeleteKey = key === 'delete' || key === 'backspace' || evt.keyCode === 46 || evt.keyCode === 8;
                if (!isDeleteKey) return;
                if (!editor || !editor.hasActiveSelection?.()) return;
                evt.preventDefault();
                removeSelected();
                hideMenu();
            };
            window.addEventListener('keydown', keydownHandler);

            mapContextMenuHandler = (evt) => {
                if (longPressTriggered) {
                    evt.preventDefault();
                    evt.stopPropagation();
                    longPressTriggered = false;
                    return;
                }
                if (!editor) return;
                const mapEl = document.getElementById('map');
                if (!mapEl) return;
                const rect = mapEl.getBoundingClientRect();
                const picked = editor.selectFeatureAt?.({
                    x: evt.clientX - rect.left,
                    y: evt.clientY - rect.top
                });
                if (!picked) {
                    hideMenu();
                    return;
                }
                evt.preventDefault();
                showMenuAt(evt.clientX, evt.clientY);
            };

            mapDblClickHandler = (evt) => {
                if (!editor) return;
                if (activeTool.value !== 'browse') return;
                if (Date.now() < suppressDblClickLabelUntil) return;
                const mapEl = document.getElementById('map');
                if (!mapEl) return;
                const rect = mapEl.getBoundingClientRect();
                const picked = editor.selectFeatureAt?.({
                    x: evt.clientX - rect.left,
                    y: evt.clientY - rect.top
                });
                if (!picked) return;
                evt.preventDefault();
                evt.stopPropagation();
                hideMenu();
                editSelectedLabel();
            };

            const mapEl = document.getElementById('map');
            if (mapEl) {
                mapEl.addEventListener('contextmenu', mapContextMenuHandler, true);
                mapEl.addEventListener('dblclick', mapDblClickHandler, true);

                touchStartHandler = (evt) => {
                    if (!editor) return;
                    if (!evt.touches || evt.touches.length !== 1) return;
                    longPressTriggered = false;
                    const touch = evt.touches[0];
                    touchStartPoint = { x: touch.clientX, y: touch.clientY };
                    if (longPressTimer) clearTimeout(longPressTimer);
                    longPressTimer = setTimeout(() => {
                        if (!touchStartPoint || !editor) return;
                        const rect = mapEl.getBoundingClientRect();
                        const picked = editor.selectFeatureAt?.({
                            x: touchStartPoint.x - rect.left,
                            y: touchStartPoint.y - rect.top
                        });
                        if (!picked) {
                            hideMenu();
                            return;
                        }
                        longPressTriggered = true;
                        showMenuAt(touchStartPoint.x, touchStartPoint.y);
                    }, 520);
                };

                touchMoveHandler = (evt) => {
                    if (!touchStartPoint || !evt.touches || evt.touches.length !== 1) return;
                    const touch = evt.touches[0];
                    const dx = Math.abs(touch.clientX - touchStartPoint.x);
                    const dy = Math.abs(touch.clientY - touchStartPoint.y);
                    if (dx > 12 || dy > 12) {
                        if (longPressTimer) clearTimeout(longPressTimer);
                        longPressTimer = null;
                    }
                };

                touchEndHandler = () => {
                    if (longPressTimer) clearTimeout(longPressTimer);
                    longPressTimer = null;
                    touchStartPoint = null;
                    if (longPressTriggered) {
                        setTimeout(() => {
                            longPressTriggered = false;
                        }, 60);
                    }
                };

                mapEl.addEventListener('touchstart', touchStartHandler, { passive: true, capture: true });
                mapEl.addEventListener('touchmove', touchMoveHandler, { passive: true, capture: true });
                mapEl.addEventListener('touchend', touchEndHandler, { passive: true, capture: true });
                mapEl.addEventListener('touchcancel', touchEndHandler, { passive: true, capture: true });
            }

            hideMenuHandler = () => hideMenu();
            document.addEventListener('click', hideMenuHandler);
            document.addEventListener('scroll', hideMenuHandler, true);
        });

        onBeforeUnmount(() => {
            if (keydownHandler) window.removeEventListener('keydown', keydownHandler);

            const mapEl = document.getElementById('map');
            if (mapEl && mapContextMenuHandler) {
                mapEl.removeEventListener('contextmenu', mapContextMenuHandler, true);
            }
            if (mapEl && mapDblClickHandler) {
                mapEl.removeEventListener('dblclick', mapDblClickHandler, true);
            }
            if (mapEl && touchStartHandler) {
                mapEl.removeEventListener('touchstart', touchStartHandler, true);
            }
            if (mapEl && touchMoveHandler) {
                mapEl.removeEventListener('touchmove', touchMoveHandler, true);
            }
            if (mapEl && touchEndHandler) {
                mapEl.removeEventListener('touchend', touchEndHandler, true);
                mapEl.removeEventListener('touchcancel', touchEndHandler, true);
            }
            if (longPressTimer) clearTimeout(longPressTimer);
            if (pendingLabelPromptTimer) window.clearTimeout(pendingLabelPromptTimer);

            if (hideMenuHandler) {
                document.removeEventListener('click', hideMenuHandler);
                document.removeEventListener('scroll', hideMenuHandler, true);
            }
        });

        return {
            pointColor,
            lineColor,
            fillColor,
            fillOpacityPercent,
            activeTool,
            showOpacitySlider,
            menuVisible,
            menuX,
            menuY,
            enterBrowseMode,
            drawPoint,
            drawLine,
            drawPolygon,
            drawRectangle,
            drawCircle,
            onPointColorChange,
            onLineColorChange,
            onFillColorChange,
            onFillOpacityChange,
            onOpacitySliderInput,
            toggleOpacitySlider,
            removeSelected,
            removeSelectedFromMenu,
            duplicateSelectedFromMenu,
            undoFromMenu,
            redoFromMenu,
            setLabelFromMenu,
            openPropsDrawerFromMenu,
            openPropsDrawer,
            clearAll,
            closeOnly,
            confirmGeometry
        };
    }
}).use(ElementPlus, { locale: ElementPlusLocaleZhCn }).mount('#app');
