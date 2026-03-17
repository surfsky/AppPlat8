import { EleForm } from "./App.EleUI.EleUIJs.EleForm.js";

/************************************************************************
 * EleFormAppBuilder Class
 * Handles Vue application mounting for EleForm
 ***********************************************************************/
export class EleFormAppBuilder {
    constructor() {
        this.Vue = window.Vue;
    }

    mount(selector, config = {}) {
        const { createApp, ref, onMounted, onUnmounted, nextTick } = this.Vue;

        const app = createApp({
            mixins: config.mixins || [],
            setup() {
                const defaultForm = config.defaultForm || {};
                const form = new EleForm(defaultForm, {
                    dataHandler: config.dataHandler,
                    saveHandler: config.saveHandler
                });

                // Register message handlers for cross-origin communication
                const msgHandler = (e) => form.messageHandler(e);
                const selHandler = (e) => form.handleSelectorMessage(e);
                onMounted(() => {
                    form.load();
                    window.addEventListener('message', msgHandler);
                    window.addEventListener('message', selHandler);
                });
                onUnmounted(() => {
                    window.removeEventListener('message', msgHandler);
                    window.removeEventListener('message', selHandler);
                });

                // Add tree select options fetching on mount
                onMounted(async () => {
                    await nextTick();
                    const treeSelects = document.querySelectorAll('[data-source]');
                    treeSelects.forEach(el => {
                        const src = el.getAttribute('data-source');
                        const key = el.getAttribute('data-key');
                        const idField = el.getAttribute('data-tree-id-field') || 'id';
                        const childrenField = el.getAttribute('data-tree-children-field') || 'children';
                        if (key && src) {
                            form.fetchOptions(key, src, idField, childrenField);
                        }
                    });
                    form.sanitizeAllStaticTreeSelectValues();
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

                return { ...bindings };
            }
        });

        // Register ElementPlus plugins if available
        if (window.ElementPlus) {
            const locale = window.ElementPlusLocaleZhCn;
            app.use(window.ElementPlus, { locale });
        }
        if (window.ElementPlusIconsVue) {
            for (const [key, component] of Object.entries(window.ElementPlusIconsVue)) {
                app.component(key, component);
            }
        }

        // Mount the app
        app.mount(selector);
        return app;
    }
}
