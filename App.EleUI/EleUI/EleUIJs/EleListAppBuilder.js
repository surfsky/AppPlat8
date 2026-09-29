import { EleList } from "./EleList.js";
import { EleAppBuilder } from "./EleAppBuilder.js";

export class EleListAppBuilder extends EleAppBuilder {
    constructor() {
        super();
    }

    mount(selector, config = {}) {
        const { ref, onMounted, onUnmounted, nextTick } = this.Vue;
        const builder = this;

        // SSR 默认值快照：见 EleTableAppBuilder.mount 中的同一根因说明，同样在 app.mount() 之前
        // 收集 SSR 原始 DOM，避免 Element Plus/自定义组件 hydrate 后把 data-filter-* 删除
        const snapshotDefaults = builder.collectFilterDefaults(selector || '#app');

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
                    const hasSnapshot = snapshotDefaults && Object.keys(snapshotDefaults).length > 0;
                    const filterDefaults = hasSnapshot
                        ? snapshotDefaults
                        : builder.collectFilterDefaults(selector || '#app');
                    builder.applyFilterDefaults(list.filters, filterDefaults);
                    // 把 SSR 快照挂到 EleList 实例上，供 commandMethods.js 里的
                    // EleList.resetFilters() 优先使用（若其内部有使用）
                    list._snapshotDefaults = filterDefaults;
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
                    Utils: (typeof window !== 'undefined' && window.Utils) ? window.Utils : (typeof globalThis !== 'undefined' && globalThis.Utils) ? globalThis.Utils : null,
                    openTopImageViewer: (url, list, idx) => {
                        try {
                            if (typeof window !== 'undefined' && window.Utils && typeof window.Utils.openImageViewerTop === 'function') {
                                window.Utils.openImageViewerTop(url, list || null, idx || 0);
                                return;
                            }
                            // 兜底：无 Utils 时新标签页打开第一张
                            if (url) (window || globalThis).open(String(url), '_blank', 'noopener');
                        } catch (e) { try { console.warn('openTopImageViewer error', e); } catch (_) { } }
                    }
                };
            }
        });

        app.mount(selector);
        return app;
    }
}
