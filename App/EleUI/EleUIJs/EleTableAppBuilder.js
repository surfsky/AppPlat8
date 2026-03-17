import { EleTable } from "./App.EleUI.EleUIJs.EleTable.js";

/***********************************************************************
 * EleTableAppBuilder Class
 * Handles Vue application mounting for EleTable
 **********************************************************************/
export class EleTableAppBuilder {
    constructor() {
        this.Vue = window.Vue;
    }

    mount(selector, config = {}) {
        const { createApp, ref, onMounted, onUnmounted, nextTick } = this.Vue;

        const app = createApp({
            setup() {
                // Initialize EleTable instance with configuration
                const table = new EleTable({
                    dataHandler: config.dataHandler,
                    deleteHandler: config.deleteHandler,
                    pageSize: config.pageSize || 10,
                    ...config
                });

                // Register message handler for cross-origin communication
                const msgHandler = (e) => table.messageHandler(e);
                onMounted(() => {
                    table.loadData();
                    window.addEventListener('message', msgHandler);
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

                // Form page URL configuration
                const editPage = config.editPage || 'Form';
                const openForm = (id) => table.openForm(id, editPage);
                const openView = (id) => table.openView(id, editPage);

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

                // Custom methods from global mixin
                const extraMethods = {};
                if (typeof userMixin !== 'undefined' && userMixin.methods) {
                    for (const [key, func] of Object.entries(userMixin.methods)) {
                        extraMethods[key] = func.bind(bindings);
                    }
                }

                // Return all bindings for template access
                return {
                    ...bindings,
                    openForm,
                    openView,
                    hasEditPower, hasViewPower, hasDeletePower,
                    ...extraMethods
                };
            }
        });

        // Register Element Plus and its icons if available
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
