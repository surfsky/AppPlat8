import { EleTable } from "./EleTable.js";
import { EleAppBuilder } from "./EleAppBuilder.js";

/***********************************************************************
 * EleTableAppBuilder Class
 * Handles Vue application mounting for EleTable
 **********************************************************************/
export class EleTableAppBuilder extends EleAppBuilder {
    constructor() {
        super();
    }

    // 全局实例注册表：hostId => { resetFilters, loadData, invokeCommand, openForm, openView, ... }
    static get instances() {
        if (typeof window.__eleTableAppInstances === 'undefined') {
            window.__eleTableAppInstances = new Map();
        }
        return window.__eleTableAppInstances;
    }

    // 根据容器 Id 取回已 mount 的表格实例暴露接口（给页内 inline script 调用）
    static getInstance(hostId) {
        if (!hostId) return null;
        const instances = EleTableAppBuilder.instances;
        if (instances.has(hostId)) return instances.get(hostId);
        // 兼容：从实际 DOM 的 data 属性查找（用户传的是容器 id）
        const cleanId = String(hostId).replace(/^#/, '');
        return instances.get(cleanId) || null;
    }

    mount(selector, config = {}) {
        const { ref, onMounted, onUnmounted, nextTick, getCurrentInstance } = this.Vue;
        const builder = this;

        // SSR 后 Element Plus 组件 hydrate 完成后会把自定义 HTML attr（例如 data-filter-*）
        // 从 DOM 中 remove，导致 onMounted + nextTick 之后再 collectFilterDefaults 根本
        // 拿不到任何属性，表现为“日期控件初始值一直为空”。解决方式：在 app.mount() 执行
        // 之前立即快照当前 SSR 原始 DOM 的 data-filter-default/data-filter-model，
        // 后续 onMounted / resetFilters 都复用这份稳定快照。
        const snapshotDefaults = builder.collectFilterDefaults(selector || '#app');

        const app = this.createConfiguredApp(config, {
            setup() {
                // Initialize EleTable instance with configuration
                const hostIdRaw = (selector || '').toString().replace(/^#/, '');
                const table = new EleTable({
                    dataHandler: config.dataHandler,
                    deleteHandler: config.deleteHandler,
                    pageSize: config.pageSize,
                    hostId: hostIdRaw,
                    ...config
                });

                // Register message handler for cross-origin communication
                const msgHandler = (e) => table.messageHandler(e);

                // 取得当前组件实例 proxy，用于访问 setupState + $refs.eleTableHost（SSR 模板里 ref="eleTableHost"）
                // 用途：MoveUp/MoveDown 后本地重排 / loadData 全量刷新后 → 调用 $refs.eleTableHost.toggleRowSelection
                // 把原来勾中的行重新勾选回来，避免"连点下移选中丢失"
                const inst = getCurrentInstance();
                const proxy = inst && inst.proxy ? inst.proxy : null;
                const getTableHost = () => {
                    if (proxy && proxy.$refs && proxy.$refs.eleTableHost) return proxy.$refs.eleTableHost;
                    try {
                        const rootProxy = inst && inst.root && inst.root.proxy ? inst.root.proxy : null;
                        if (rootProxy && rootProxy.$refs && rootProxy.$refs.eleTableHost) return rootProxy.$refs.eleTableHost;
                    } catch (_) {}
                    return null;
                };
                const restoreSelectedByIds = (ids, preferSnapshotRows) => {
                    if (!table || !Array.isArray(ids) || ids.length === 0) return;
                    const idSet = new Set(ids.map(id => (id === null || id === undefined) ? '__nil__' : String(id)));
                    const rows = Array.isArray(table.items?.value) ? table.items.value : [];
                    const host = getTableHost();
                    const pickedRows = [];
                    // 优先按 snapshotRows（patch 前内存对象引用）：toggleRowSelection 对同一个引用生效最快
                    if (Array.isArray(preferSnapshotRows)) {
                        for (const r of preferSnapshotRows) {
                            if (!r) continue;
                            const k = (r.id != null) ? String(r.id) : (r.Id != null ? String(r.Id) : '__nil__');
                            if (k !== '__nil__' && idSet.has(k)) pickedRows.push(r);
                        }
                    }
                    // 再兜底：在 items.value（可能 patch 后重新排序的新对象引用）里按 id 找行也勾选
                    for (const r of rows) {
                        if (!r) continue;
                        const k = (r.id != null) ? String(r.id) : (r.Id != null ? String(r.Id) : '__nil__');
                        if (k !== '__nil__' && idSet.has(k)) pickedRows.push(r);
                    }
                    if (pickedRows.length === 0) return;
                    const dedup = new Map();
                    for (const r of pickedRows) {
                        const k = (r.id != null) ? String(r.id) : (r.Id != null ? String(r.Id) : ('__obj__' + Math.random()));
                        if (!dedup.has(k)) dedup.set(k, r);
                    }
                    const uniqueRows = Array.from(dedup.values());
                    // 先按 ids 顺序写回 selectedIds / selectedRows.value（UI 展示"已选中N条"的计数）
                    table.selectedIds.value   = ids.filter(x => x != null).map(x => (typeof x === 'string' ? x : x));
                    table.selectedRows.value  = uniqueRows;
                    if (host && typeof host.toggleRowSelection === 'function') {
                        nextTick(() => {
                            try {
                                for (const r of uniqueRows) {
                                    try { host.toggleRowSelection(r, true); } catch (_) {}
                                }
                            } catch (_) {}
                        });
                    }
                };
                // 在 table 上挂一次性钩子：loadData 完成 items.value 更新后恢复选中
                // （用于 sort-change 触发 handler=Data 再加载的场景，避免选中再次丢）
                const origLoadData = typeof table.loadData === 'function' ? table.loadData.bind(table) : null;
                if (origLoadData) {
                    table.loadData = async function wrappedLoadData(options) {
                        const snapshot = Array.isArray(table.selectedIds?.value) ? [...table.selectedIds.value] : [];
                        const snapRows = Array.isArray(table.selectedRows?.value) ? [...table.selectedRows.value] : [];
                        const needRestore = snapshot.length > 0;
                        if (needRestore) table._restoreSelectedAfterNextLoad = { ids: snapshot, rows: snapRows, time: Date.now() };
                        try { return await origLoadData(options); }
                        finally {
                            const pending = table._restoreSelectedAfterNextLoad;
                            table._restoreSelectedAfterNextLoad = null;
                            if (needRestore && pending && Array.isArray(pending.ids)) {
                                nextTick(() => restoreSelectedByIds(pending.ids, pending.rows));
                            }
                        }
                    };
                }

                onMounted(async () => {
                    await nextTick();
                    // 优先使用 mount 前快照的 SSR 默认值（此时 DOM 属性完好）；
                    // 如果快照为空（例如 CSR 首次渲染），再退化为从 DOM 重新 collect。
                    const hasSnapshot = snapshotDefaults && Object.keys(snapshotDefaults).length > 0;
                    const filterDefaults = hasSnapshot
                        ? snapshotDefaults
                        : builder.collectFilterDefaults(selector || '#app');
                    builder.applyFilterDefaults(table.filters, filterDefaults);
                    // 把 SSR 快照挂到 EleTable 实例上，供 commandMethods.js 里的
                    // EleTable.resetFilters() 优先使用，避免 Element Plus 组件 hydrate 后
                    // 将 data-filter-default 属性删除导致重置失效。
                    table._snapshotDefaults = filterDefaults;
                    table.loadData();
                    window.addEventListener('message', msgHandler);

                    // mount 完成后：把 resetFilters / loadData / invokeCommand 等接口暴露到全局 instances
                    // （供页内脚本或 EleTableAppBuilder.getInstance('xxx').resetFilters() 调用）
                    try {
                        const exposed = {
                            resetFilters: () => (typeof table.resetFilters === 'function'
                                ? table.resetFilters()
                                : (() => {
                                    const d = (snapshotDefaults && Object.keys(snapshotDefaults).length > 0)
                                        ? snapshotDefaults
                                        : builder.collectFilterDefaults(selector || '#app');
                                    builder.applyFilterDefaults(table.filters, d);
                                    return table.loadData(true);
                                })()),
                            loadData: (resetPage) => table.loadData(resetPage),
                            invokeCommand: (name, evt) => (typeof table.invokeCommand === 'function'
                                ? table.invokeCommand(name, evt) : Promise.resolve()),
                            openForm: (id, url, title) => table.openForm(id, url, title),
                            openView: (id, url, title) => table.openView(id, url, title),
                            restoreSelected: (ids, rows) => restoreSelectedByIds(ids, rows),
                            // 调试/拦截器需要直接读写 filters 响应式状态时使用；
                            // 禁止在正常业务代码里绕过 v-model 直接赋值。
                            __debug: {
                                getFilters: () => table.filters,
                                setFilter: (k, v) => { if (table.filters?.value && typeof k === 'string') table.filters.value[k] = v; },
                                getTableHost: () => getTableHost(),
                                getProxy: () => proxy
                            }
                        };
                        EleTableAppBuilder.instances.set(hostIdRaw, exposed);
                    } catch (_) {}
                });

                // Auto-fetch options logic
                onMounted(async () => {
                    await nextTick();
                    const treeSelects = document.querySelectorAll('[data-source]');
                    treeSelects.forEach(el => {
                        const src = el.getAttribute('data-source');
                        const key = el.getAttribute('data-key');
                        if (key && src) {
                            table.fetchOptions(key, src);
                        }
                    });
                });
                onUnmounted(() => {
                    window.removeEventListener('message', msgHandler);
                });
                onUnmounted(() => {
                    table.stopAutoRefresh();
                });

                // Form page URL configuration
                const editPage = config.editPage || 'Form';
                const openForm = (id, urlBase, drawerTitle) => table.openForm(id, urlBase || editPage, drawerTitle);
                const openView = (id, urlBase, drawerTitle) => table.openView(id, urlBase || editPage, drawerTitle);

                // Permissions
                const hasEditPower = ref(false);
                const hasViewPower = ref(false);
                const hasDeletePower = ref(false);
                if (config.permissionHandler) {
                    onMounted(() => {
                        axios.get(config.permissionHandler).then((res) => {
                            if (res.data && res.data.code === 0) {
                                const d = res.data.data;
                                hasEditPower.value = !!d.canEdit;
                                hasViewPower.value = !!d.canView;
                                hasDeletePower.value = !!d.canDelete;
                            }
                        });
                    });
                }

                // Expose EleTable members to template
                const bindings = {};
                for (const key of Object.keys(table)) {
                    bindings[key] = table[key];
                }
                const proto = Object.getPrototypeOf(table);
                for (const key of Object.getOwnPropertyNames(proto)) {
                    if (key !== 'constructor') {
                        bindings[key] = table[key].bind(table);
                    }
                }

                // Inherited postHandler with additional context
                // 为了最大化向后兼容 + 新 Payload 机制工作可靠：
                //   (1) 如果 payload 是 "FilterInfo|PageInfo|SelectInfo|ContextInfo" 种类字符串：按 kinds 收集结构化 envelope {Filter, Page, Select, Context}
                //   (2) 无论 (1) 是否触发，始终再收集一份扁平标准字段 {selectedIds, filters, pageIndex, pageSize, sortField, sortDirection, uniId}
                //   (3) 最终发送两者的合并对象，让后端 ElePayloadEnvelope 永远能反序列化出非 null
                // 注意：故意不发送 rows/Rows，避免与后端 STJ CamelCase 属性命名冲突。
                const KIND_TO_KEYS = {
                    FilterInfo: 'Filter',
                    PageInfo:   'Page',
                    SelectInfo: 'Select',
                    ContextInfo:'Context'
                };
                const getCurrentSelectedIds = () => Array.isArray(table.selectedIds?.value) ? [...table.selectedIds.value] : [];
                const getCurrentUrl = () => { try { return new URL(window.location.href); } catch (_) { return { searchParams: new URLSearchParams(), href: window.location.href, pathname: window.location.pathname }; } };

                // 收集结构化 envelope（按 ElePayloadKinds 逐项构造）
                const collectPayloadEnvelope = (kindsStr) => {
                    let kinds = (kindsStr || '').trim();
                    if (!kinds) return null;
                    const parts = kinds.split(/[|,;\s]+/).filter(Boolean);
                    if (parts.length === 0) return null;

                    const currentSelectedIds = getCurrentSelectedIds();
                    const url = getCurrentUrl();

                    const envelope = {};
                    const set = (k, v) => { const key = KIND_TO_KEYS[k]; if (key) envelope[key] = v; };

                    for (const part of parts) {
                        const k = (part || '').trim();
                        if (!k) continue;
                        switch (k) {
                            case 'FilterInfo':
                                set(k, table.filters && table.filters.value ? table.filters.value : {});
                                break;
                            case 'PageInfo':
                                set(k, {
                                    sortField:     (table.sortField && table.sortField.value) || '',
                                    sortDirection: (table.sortDirection && table.sortDirection.value) || '',
                                    pageIndex:     (table.pageIndex && table.pageIndex.value) || 0,
                                    pageSize:      (table.pageSize && table.pageSize.value) || 50
                                });
                                break;
                            case 'SelectInfo':
                                set(k, {
                                    Ids:   [...currentSelectedIds],
                                    Count: currentSelectedIds.length,
                                    FirstId: currentSelectedIds.length > 0 ? currentSelectedIds[0] : null
                                });
                                break;
                            case 'ContextInfo':
                                set(k, {
                                    UniId: (url.searchParams && url.searchParams.get('uniId')) || '',
                                    Url:   url.href,
                                    Path:  url.pathname
                                });
                                break;
                        }
                    }
                    return Object.keys(envelope).length > 0 ? envelope : null;
                };
                // 收集扁平标准字段（EleTable 默认 postHandler 约定形态，任何情况下都带）
                const collectFlatPayload = () => {
                    const url = getCurrentUrl();
                    return {
                        selectedIds:   getCurrentSelectedIds(),
                        filters:       (table.filters && table.filters.value) ? table.filters.value : {},
                        pageIndex:     (table.pageIndex && table.pageIndex.value) || 0,
                        pageSize:      (table.pageSize && table.pageSize.value) || 50,
                        sortField:     (table.sortField && table.sortField.value) || '',
                        sortDirection: (table.sortDirection && table.sortDirection.value) || '',
                        uniId:         (url.searchParams && url.searchParams.get('uniId')) ? url.searchParams.get('uniId') : ''
                    };
                };
                const inheritedPostHandler = async (name, payload) => {
                    let structuredEnv = null;
                    let flatBase = collectFlatPayload();

                    // 约定：当 payload 是字符串且命中 PayloadKinds 关键字时，按 kinds 收集结构化
                    if (typeof payload === 'string' && /FilterInfo|PageInfo|SelectInfo|ContextInfo/i.test(payload)) {
                        structuredEnv = collectPayloadEnvelope(payload);
                    }

                    let finalData;
                    if (typeof payload === 'string') {
                        finalData = structuredEnv ? { ...structuredEnv, ...flatBase } : flatBase;
                    } else if (payload && typeof payload === 'object') {
                        finalData = { ...payload, ...flatBase };
                    } else {
                        finalData = flatBase;
                    }
                    const resp = await builder.postHandler(name, finalData, null);
                    // ---------- Test 按钮：完整 data 宽度占满窗口（88vw + pre 自动换行）----------
                    if (name && String(name).toLowerCase() === 'test') {
                        try {
                            const pretty = (resp && resp.data)
                                ? (typeof resp.data === 'string' ? resp.data : JSON.stringify(resp.data, null, 2))
                                : (typeof resp === 'string' ? resp : JSON.stringify(resp, null, 2));
                            const escaped = String(pretty).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
                            const html = `<pre style="margin:0;padding:12px;border-radius:6px;background:#fafafa;color:#1f2937;font-size:12px;line-height:1.55;white-space:pre-wrap;word-break:break-all;overflow:auto;max-height:70vh;">${escaped}</pre>`;
                            const EM = window.EleManager || window.$eleManager;
                            if (EM && typeof EM.coreElMessageboxAlert === 'function') {
                                try { await EM.coreElMessageboxAlert(pretty, 'Test：完整 ElePayload 返回数据'); }
                                catch (_) { /* cancel */ }
                            } else if (window.ElementPlus && window.ElementPlus.ElMessageBox && typeof window.ElementPlus.ElMessageBox.alert === 'function') {
                                try {
                                    await window.ElementPlus.ElMessageBox.alert(html, 'Test：完整 ElePayload 返回数据', {
                                        confirmButtonClass: 'el-button--primary',
                                        dangerouslyUseHTMLString: true,
                                        width:              '88vw',
                                        draggable:          true,
                                        customClass:        'ele-test-payload-dialog',
                                        closeOnClickModal:  false
                                    });
                                } catch (_) { /* cancel */ }
                            } else {
                                window.alert('Test：完整 ElePayload 返回数据\n' + pretty);
                            }
                        } catch (err) {
                            console.warn('[EleTable.TestResult]', err, resp);
                        }
                    }
                    // ---------- MoveUp/MoveDown：按响应体 sortChanges **只更新 sortId 字段**，无全量 reload ----------
                    else if (resp && typeof resp === 'object' && resp.code === 0) {
                        const n = (name || '').toString().trim().toLowerCase();
                        if (n === 'moveup' || n === 'movedown') {
                            const data = resp.data || {};
                            const changes = Array.isArray(data.sortChanges) ? data.sortChanges : [];
                            if (changes.length > 0 && table && Array.isArray(table.items?.value)) {
                                // 快照：当前勾选状态，用于 patch 重排后把勾选恢复（否则连点"下移"每次都要重新勾选，很麻烦）
                                const snapshotIds  = Array.isArray(table.selectedIds?.value)  ? [...table.selectedIds.value]  : [];
                                const snapshotRows = Array.isArray(table.selectedRows?.value) ? [...table.selectedRows.value] : [];

                                const rows = table.items.value;
                                const byId = new Map();
                                for (let i = 0; i < rows.length; i++) {
                                    const r = rows[i];
                                    if (r && (r.id != null || r.Id != null)) {
                                        const k = r.id != null ? String(r.id) : String(r.Id);
                                        byId.set(k, r);
                                    }
                                }
                                let patched = 0;
                                for (const ch of changes) {
                                    if (!ch || ch.id == null || ch.sortId == null) continue;
                                    const k = String(ch.id);
                                    const r = byId.get(k);
                                    if (!r) continue;
                                    // 兼容：行里 sortId / SortId 两种命名（STJ CamelCase 默认会是 sortId）
                                    if ('sortId' in r) r.sortId = ch.sortId;
                                    if ('SortId' in r) r.SortId = ch.sortId;
                                    // 兜底：两者都不在（字段名不一致）→ 强制写 camel 版让 EleTable Column prop="sortId" 命中
                                    if (!('sortId' in r) && !('SortId' in r)) r.sortId = ch.sortId;
                                    patched += 1;
                                }
                                if (patched > 0) {
                                    // 触发 EleTable 本地重排：如果当前就是 SortId asc/desc，onSortChange 会重新 loadData →
                                    // 为了满足"不 reload"，用手动 replace+保持相同 pageIndex 的方式：
                                    // 1) 保证 sortField/sortDirection.value 与后端约定一致 = SortId ASC
                                    // 2) 将 items.value 原地按 SortId asc（或用户当前方向）重排；Vue 响应式 → 表格立刻刷新
                                    try {
                                        const field = (data.sortField || table.sortField?.value || 'SortId').toString().trim();
                                        const isAsc = (typeof data.asc === 'boolean')
                                            ? data.asc
                                            : !((table.sortDirection?.value || 'ASC').toString().toUpperCase() === 'DESC');
                                        const cmpFieldCandidates = [
                                            field,
                                            field.charAt(0).toLowerCase() + field.slice(1),
                                            field.charAt(0).toUpperCase() + field.slice(1)
                                        ];
                                        const pickVal = (r) => {
                                            for (const f of cmpFieldCandidates) {
                                                if (r && (f in r) && r[f] != null) {
                                                    const v = r[f];
                                                    if (typeof v === 'number' || typeof v === 'bigint') return Number(v);
                                                    const n = Number(v);
                                                    if (!Number.isNaN(n)) return n;
                                                    return String(v);
                                                }
                                            }
                                            return Number.MAX_SAFE_INTEGER;
                                        };
                                        table.items.value = [...rows].sort((a, b) => {
                                            const av = pickVal(a);
                                            const bv = pickVal(b);
                                            if (av === bv) {
                                                const ai = a?.id != null ? Number(a.id) : 0;
                                                const bi = b?.id != null ? Number(b.id) : 0;
                                                return bi - ai;
                                            }
                                            if (typeof av === 'number' && typeof bv === 'number') return isAsc ? av - bv : bv - av;
                                            return isAsc ? String(av).localeCompare(String(bv)) : String(bv).localeCompare(String(av));
                                        });
                                    } catch (sortErr) {
                                        console.warn('[EleTable.MoveResult] patch后本地重排失败，降级为不重排：', sortErr);
                                    }
                                }
                                // ✅ 核心：patch + 重排之后，把原来的勾选状态恢复（勾选行继续保持选中，便于连点下移）
                                if (snapshotIds.length > 0) {
                                    try { restoreSelectedByIds(snapshotIds, snapshotRows); } catch (err) { console.warn('[EleTable.MoveResult] 恢复选中失败', err); }
                                }
                            }
                        }
                    }
                    return resp;
                };

                // Build a shared method context so page-level custom methods can
                // access postHandler/openForm/openView/permission flags consistently.
                const methodContext = {
                    ...bindings,
                    openForm,
                    openView,
                    Utils: window.Utils,
                    postHandler: inheritedPostHandler,
                    hasEditPower,
                    hasViewPower,
                    hasDeletePower
                };

                // Custom methods from global mixin
                const extraMethods = {};
                if (typeof userMixin !== 'undefined' && userMixin.methods) {
                    for (const [key, func] of Object.entries(userMixin.methods)) {
                        if (typeof func === 'function') {
                            extraMethods[key] = func.bind(methodContext);
                        }
                    }
                }

                // Return all bindings for template access
                return {
                    ...methodContext,
                    ...extraMethods
                };
            }
        });

        // Mount the app
        app.mount(selector);
        return app;
    }
}
