import { EleForm } from "./EleForm.js";
import { EleAppBuilder } from "./EleAppBuilder.js";

/************************************************************************
 * EleFormAppBuilder Class
 * Handles Vue application mounting for EleForm
 ***********************************************************************/
export class EleFormAppBuilder extends EleAppBuilder {
    constructor() {
        super();
    }

    mount(selector, config = {}) {
        const { onMounted, onUnmounted, nextTick } = this.Vue;
        const builder = this;

        const app = this.createConfiguredApp(config, {
            mixins: config.mixins || [],
            setup() {
                const initData = config.initData || {};
                const form = new EleForm(initData, {
                    dataHandler: config.dataHandler,
                    saveHandler: config.saveHandler
                });

                // ----------------------------------------------------
                // 收到 refresh 信号时：重新拉表单主数据 + 刷新所有内嵌 EleList
                // （解决子抽屉保存后，父级 Drawer 里处理历史 EleList 不刷新的问题）
                // ----------------------------------------------------
                const shouldRefreshByDetail = (detail) => {
                    if (!detail) return false;
                    if (detail.__eleFormRefreshAll === true) return true;
                    const scope = String(detail.scope || '').toLowerCase();
                    if (scope === 'self' || scope === '') return true;
                    return false;
                };
                const runFormRefresh = async () => {
                    try {
                        if (typeof form.load === 'function') await form.load();
                    } catch (e) { console.warn('[EleFormAppBuilder] form.load() 刷新失败', e); }
                    try {
                        if (typeof form.refreshAllLists === 'function') await form.refreshAllLists(true);
                    } catch (e) { console.warn('[EleFormAppBuilder] form.refreshAllLists() 刷新失败', e); }
                };
                const customEvtHandler = (e) => {
                    if (!shouldRefreshByDetail(e && e.detail)) return;
                    runFormRefresh().catch(() => {});
                };
                const winMsgHandler = (e) => {
                    const d = e && e.data;
                    if (!d) return;
                    if (d.__eleFormRefreshAll === true) { runFormRefresh().catch(()=>{}); return; }
                    if (d.__eleRefreshData === true || d.__attsMoveToRefresh__ === true) { runFormRefresh().catch(()=>{}); return; }
                };

                // Register message handlers for cross-origin communication
                const msgHandler = (e) => form.messageHandler(e);
                const pickerHandler = (e) => form.handlePickerMessage(e);
                let listWindowScrollHandler = null;
                onMounted(async () => {
                    if (config.autoLoad === false) {
                        const url = new URL(window.location.href);
                        if (typeof config.readOnly !== 'undefined') {
                            form.readOnly.value = !!config.readOnly;
                        } else {
                            form.readOnly.value = (url.searchParams.get('md') || '').toLowerCase() === 'view';
                        }
                        form.originalForm.value = JSON.parse(JSON.stringify(form.form.value || {}));
                    } else {
                        await form.load();
                    }

                    await nextTick();
                    const treeSelects = document.querySelectorAll('[data-source]');
                    const jobs = [];
                    treeSelects.forEach(el => {
                        const src = el.getAttribute('data-source');
                        const key = el.getAttribute('data-key');
                        const idField = el.getAttribute('data-tree-id-field') || 'id';
                        const childrenField = el.getAttribute('data-tree-children-field') || 'children';
                        if (key && src) {
                            jobs.push(form.fetchOptions(key, src, idField, childrenField));
                        }
                    });
                    await Promise.all(jobs);
                    form.sanitizeAllStaticSelectValues();
                    form.sanitizeAllDynamicSelectValues();
                    form.sanitizeAllStaticTreeSelectValues();
                    form.sanitizeAllRemoteTreeSelectValues();

                    const listHosts = document.querySelectorAll('[data-ele-list-key]');
                    for (const host of listHosts) {
                        const key = host.getAttribute('data-ele-list-key');
                        if (!key) continue;

                        const dataHandler = host.getAttribute('data-ele-list-handler') || '?handler=Data';
                        const pageSize = Number(host.getAttribute('data-ele-list-page-size') || '10');
                        const sortField = host.getAttribute('data-ele-list-sort-field') || 'Id';
                        const sortDirection = host.getAttribute('data-ele-list-sort-direction') || 'DESC';
                        const scrollEl = host.querySelector('[data-ele-list-scroll]');

                        await form.initEleList(key, {
                            dataHandler,
                            pageSize,
                            sortField,
                            sortDirection
                        }, scrollEl);
                    }

                    listWindowScrollHandler = () => {
                        for (const host of listHosts) {
                            const key = host.getAttribute('data-ele-list-key');
                            if (!key) continue;
                            const scrollEl = host.querySelector('[data-ele-list-scroll]');
                            form.onEleListWindowScroll(key, scrollEl);
                        }
                    };
                    window.addEventListener('scroll', listWindowScrollHandler, { passive: true });

                    window.addEventListener('message', msgHandler);
                    window.addEventListener('message', pickerHandler);
                    window.addEventListener('message', winMsgHandler);
                    if (typeof window.addEventListener === 'function') {
                        window.addEventListener('eleui:refresh-data', customEvtHandler);
                    }

                    // 把当前表单注册到全局，方便 Utils.refreshData(Parent) 从父级窗口找到并刷新
                    try {
                        if (!Array.isArray(window.__eleFormInstances__)) window.__eleFormInstances__ = [];
                        const curUrl = (typeof window.location !== 'undefined' && window.location) ? window.location.toString() : '';
                        const up = new URL(curUrl, window.location.origin);
                        const uniId = (up.searchParams.get('uniId') || up.searchParams.get('hazardId') || up.searchParams.get('id') || '').toString();
                        const entry = {
                            form,
                            time: Date.now(),
                            path: up.pathname || '',
                            uniId,
                            url: curUrl,
                            title: (typeof document !== 'undefined' && document.title) ? document.title : ''
                        };
                        window.__eleFormInstances__.push(entry);
                        if (window.__eleFormInstances__.length > 8) window.__eleFormInstances__.splice(0, window.__eleFormInstances__.length - 8);
                        if (uniId && !window.__attsMoveToSourceUniId__) window.__attsMoveToSourceUniId__ = uniId;
                    } catch (_) { }
                });
                onUnmounted(() => {
                    if (listWindowScrollHandler)
                        window.removeEventListener('scroll', listWindowScrollHandler);
                    window.removeEventListener('message', msgHandler);
                    window.removeEventListener('message', pickerHandler);
                    window.removeEventListener('message', winMsgHandler);
                    if (typeof window.removeEventListener === 'function') {
                        window.removeEventListener('eleui:refresh-data', customEvtHandler);
                    }
                    try {
                        if (Array.isArray(window.__eleFormInstances__)) {
                            const idx = window.__eleFormInstances__.findIndex(x => x && x.form === form);
                            if (idx >= 0) window.__eleFormInstances__.splice(idx, 1);
                        }
                    } catch (_) { }
                });

                // Expose EleForm members to template
                const bindings = {};
                for (const key of Object.keys(form)) {
                    bindings[key] = form[key];
                }
                const proto = Object.getPrototypeOf(form);
                for (const key of Object.getOwnPropertyNames(proto)) {
                    if (key !== 'constructor') {
                        bindings[key] = form[key].bind(form);
                    }
                }

                // 让表单构建器默认具备基类的服务端交互能力
                const inheritedPostHandler = async (name, payload) => {
                    const data = payload || (form.form ? form.form.value : {});
                    return builder.postHandler(name, data, form.form ? form.form.value : null);
                };
                form.postHandler = inheritedPostHandler;
                bindings.postHandler = inheritedPostHandler;
                bindings.invokeCommand = async (name, payload) => form.invokeCommand(name, payload);

                return {
                    ...bindings,
                    Utils: (typeof window !== 'undefined' && window.Utils) ? window.Utils : (typeof globalThis !== 'undefined' && globalThis.Utils) ? globalThis.Utils : null,
                    openTopImageViewer: (url, list, idx) => {
                        try {
                            if (typeof window !== 'undefined' && window.Utils && typeof window.Utils.openImageViewerTop === 'function') {
                                window.Utils.openImageViewerTop(url, list || null, idx || 0);
                            } else if (typeof form.openImageViewerTop === 'function') {
                                form.openImageViewerTop(url, list || null, idx || 0);
                            }
                        } catch (e) { try { console.warn('openTopImageViewer error', e); } catch (_) { } }
                    }
                };
            }
        });

        // Mount the app
        app.mount(selector);
        return app;
    }
}
