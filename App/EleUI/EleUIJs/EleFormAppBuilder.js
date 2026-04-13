import { EleForm } from "./App.EleUI.EleUIJs.EleForm.js";
import { EleAppBuilder } from "./App.EleUI.EleUIJs.EleAppBuilder.js";

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

                // Register message handlers for cross-origin communication
                const msgHandler = (e) => form.messageHandler(e);
                const selHandler = (e) => form.handleSelectorMessage(e);
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
                    window.addEventListener('message', selHandler);
                });
                onUnmounted(() => {
                    if (listWindowScrollHandler)
                        window.removeEventListener('scroll', listWindowScrollHandler);
                    window.removeEventListener('message', msgHandler);
                    window.removeEventListener('message', selHandler);
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

                return { ...bindings };
            }
        });

        // Mount the app
        app.mount(selector);
        return app;
    }
}
