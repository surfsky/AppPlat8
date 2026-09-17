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
        const { ref, onMounted, onUnmounted, nextTick } = this.Vue;
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
                            // 调试/拦截器需要直接读写 filters 响应式状态时使用；
                            // 禁止在正常业务代码里绕过 v-model 直接赋值。
                            __debug: {
                                getFilters: () => table.filters,
                                setFilter: (k, v) => { if (table.filters?.value && typeof k === 'string') table.filters.value[k] = v; }
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
                const inheritedPostHandler = async (name, payload) => {
                    return builder.postHandler(name, payload || {
                        selectedIds: table.selectedIds.value,
                        filters: table.filters.value,
                        pageIndex: table.pageIndex.value,
                        pageSize: table.pageSize.value,
                        sortField: table.sortField.value,
                        sortDirection: table.sortDirection.value
                    });
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
