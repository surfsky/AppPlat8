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

                // 让表单构建器默认具备基类的服务端交互能力
                const inheritedPostHandler = async (name, payload) => {
                    const data = payload || (form.form ? form.form.value : {});
                    return builder.postHandler(name, data, form.form ? form.form.value : null);
                };
                bindings.postHandler = inheritedPostHandler;
                bindings.invokeCommand = async (name, payload) => inheritedPostHandler(name, payload);

                return { ...bindings };
            }
        });

        // Mount the app
        app.mount(selector);
        return app;
    }
}
