import { EleTable } from "./App.EleUI.EleUIJs.EleTable.js";
import { EleAppBuilder } from "./App.EleUI.EleUIJs.EleAppBuilder.js";

/***********************************************************************
 * EleTableAppBuilder Class
 * Handles Vue application mounting for EleTable
 **********************************************************************/
export class EleTableAppBuilder extends EleAppBuilder {
    constructor() {
        super();
    }

    mount(selector, config = {}) {
        const { ref, onMounted, onUnmounted, nextTick } = this.Vue;
        const builder = this;

        const app = this.createConfiguredApp(config, {
            setup() {
                // Initialize EleTable instance with configuration
                const table = new EleTable({
                    dataHandler: config.dataHandler,
                    deleteHandler: config.deleteHandler,
                    pageSize: config.pageSize,
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

                // Custom methods from global mixin
                const extraMethods = {};
                if (typeof userMixin !== 'undefined' && userMixin.methods) {
                    for (const [key, func] of Object.entries(userMixin.methods)) {
                        extraMethods[key] = func.bind(bindings);
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

                // Return all bindings for template access
                return {
                    ...bindings,
                    openForm,
                    openView,
                    Utils: window.Utils,
                    postHandler: inheritedPostHandler,
                    hasEditPower, hasViewPower, hasDeletePower,
                    ...extraMethods
                };
            }
        });

        // Mount the app
        app.mount(selector);
        return app;
    }
}
