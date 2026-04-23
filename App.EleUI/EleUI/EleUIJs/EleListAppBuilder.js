import { EleList } from "./EleList.js";
import { EleAppBuilder } from "./EleAppBuilder.js";

export class EleListAppBuilder extends EleAppBuilder {
    constructor() {
        super();
    }

    mount(selector, config = {}) {
        const { ref, onMounted, onUnmounted, nextTick } = this.Vue;
        const builder = this;

        const app = this.createConfiguredApp(config, {
            setup() {
                const listScrollEl = ref(null);
                const list = new EleList({
                    dataHandler: config.dataHandler,
                    pageSize: config.pageSize,
                    defaultSortField: config.defaultSortField,
                    defaultSortDirection: config.defaultSortDirection,
                    ...config
                });

                const onWindowScroll = () => list.onWindowScroll();

                onMounted(async () => {
                    await nextTick();
                    const filterDefaults = builder.collectFilterDefaults(selector || '#app');
                    builder.applyFilterDefaults(list.filters, filterDefaults);
                    await list.loadData(true);
                    await nextTick();
                    await list.ensureScrollable(listScrollEl.value);
                    window.addEventListener('scroll', onWindowScroll, { passive: true });
                });

                onUnmounted(() => {
                    window.removeEventListener('scroll', onWindowScroll);
                });

                const bindings = {};
                for (const key of Object.keys(list)) {
                    bindings[key] = list[key];
                }

                const proto = Object.getPrototypeOf(list);
                for (const key of Object.getOwnPropertyNames(proto)) {
                    if (key !== 'constructor') {
                        bindings[key] = list[key].bind(list);
                    }
                }

                return {
                    ...bindings,
                    listScrollEl,
                    Utils: window.Utils
                };
            }
        });

        app.mount(selector);
        return app;
    }
}
