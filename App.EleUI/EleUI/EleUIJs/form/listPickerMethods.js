export const listPickerMethods = {
    /**Normalize list picker model value */
    normalizeListPickerValue(value, multiple = false) {
        if (multiple) {
            if (Array.isArray(value)) {
                return value
                    .map(v => (v === null || v === undefined ? '' : String(v)))
                    .filter(v => !!v);
            }
            if (value === null || value === undefined || value === '') return [];
            return [String(value)];
        }

        if (Array.isArray(value)) {
            const first = value.find(v => v !== null && v !== undefined && v !== '');
            return first === undefined ? '' : String(first);
        }
        return value === null || value === undefined ? '' : String(value);
    },

    /**Build list picker display payload */
    getListPickerView(modelKey, target, fallbackOptions = [], multiple = false, collapseTags = false) {
        const model = this.form?.value ? this.form.value[modelKey] : null;
        const normalized = this.normalizeListPickerValue(model, multiple);
        const values = Array.isArray(normalized) ? normalized : (normalized ? [normalized] : []);
        const options = typeof this.getControlOptions === 'function'
            ? this.getControlOptions(target, fallbackOptions)
            : (Array.isArray(fallbackOptions) ? fallbackOptions : []);

        const labelMap = new Map(
            (Array.isArray(options) ? options : []).map(item => [
                item?.value === null || item?.value === undefined ? '' : String(item.value),
                item?.label === null || item?.label === undefined ? '' : String(item.label)
            ])
        );
        const labels = values.map(v => labelMap.get(String(v)) || String(v));

        return {
            values,
            labels,
            displayLabels: collapseTags && labels.length > 0 ? [labels[0]] : labels,
            hiddenCount: collapseTags && labels.length > 1 ? labels.length - 1 : 0,
            text: labels.join(', ')
        };
    },

    /**Clear list picker selection */
    clearListPicker(modelKey, multiple = false, onChange = '') {
        if (!this.form?.value) return;
        this.form.value[modelKey] = multiple ? [] : null;
        this.notifyListPickerChanged(modelKey, this.form.value[modelKey], multiple, onChange);
    },

    /**Post list picker change callback */
    notifyListPickerChanged(modelKey, value, multiple = false, onChange = '') {
        if (!onChange || typeof this.postHandler !== 'function') return;

        this.postHandler(onChange, {
            eventName: 'change',
            controlId: `field:${modelKey}`,
            fieldExpress: modelKey,
            value,
            form: this.form?.value || {},
            controlType: 'EleListPicker',
            multiple: !!multiple
        });
    },

    /**Open root drawer list picker */
    async openListPicker(config = {}) {
        if (!config || !config.modelKey || !this.form?.value) return;
        if (!window.EleManager || typeof EleManager.openDrawer !== 'function') return;

        const modelKey = config.modelKey;
        const target = config.target || `field:${modelKey}`;
        const multiple = !!config.multiple;
        const collapseTags = !!config.collapseTags;
        const title = config.title || config.label || '选择';
        const size = config.size || '';
        const placeholder = config.placeholder || '请输入关键字过滤';
        const fallbackOptions = Array.isArray(config.fallbackOptions) ? config.fallbackOptions : [];
        const currentValue = this.normalizeListPickerValue(this.form.value[modelKey], multiple);
        const selectedValues = Array.isArray(currentValue)
            ? [...currentValue]
            : (currentValue ? [currentValue] : []);
        const form = this;

        const sourceOptions = typeof this.getControlOptions === 'function'
            ? this.getControlOptions(target, fallbackOptions)
            : fallbackOptions;

        const getRawValue = (raw) => {
            const match = (Array.isArray(sourceOptions) ? sourceOptions : [])
                .find(item => String(item?.value) === String(raw));
            return match ? match.value : raw;
        };

        EleManager.openDrawer({
            title,
            direction: 'rtl',
            size: size || (window.innerWidth < 768 ? '100%' : '420px'),
            resizable: true,
            closeOnClickModal: false,
            destroyOnClose: true,
            custom: true,
            bodyClass: 'ele-list-picker-drawer-host',
            mountHandler(ctx) {
                const { hostWindow, bodyEl, setFooterButtons } = ctx;
                const { createApp, reactive, computed } = hostWindow.Vue;

                const state = reactive({
                    keyword: '',
                    options: Array.isArray(sourceOptions) ? sourceOptions : [],
                    values: [...selectedValues],
                    current: !multiple && selectedValues.length > 0 ? selectedValues[0] : '',
                    collapseTags
                });

                const app = createApp({
                    setup() {
                        const filteredOptions = computed(() => {
                            const keyword = String(state.keyword || '').trim().toLowerCase();
                            if (!keyword) return state.options;
                            return state.options.filter(item => {
                                const label = item?.label === null || item?.label === undefined ? '' : String(item.label);
                                const value = item?.value === null || item?.value === undefined ? '' : String(item.value);
                                return label.toLowerCase().includes(keyword) || value.toLowerCase().includes(keyword);
                            });
                        });

                        const selectedText = computed(() => {
                            const view = form.getListPickerView(modelKey, target, state.options, multiple, collapseTags);
                            if (multiple) {
                                view.values = [...state.values];
                                view.labels = state.values.map(v => {
                                    const hit = state.options.find(item => String(item?.value) === String(v));
                                    return hit?.label ?? String(v);
                                });
                                view.displayLabels = collapseTags && view.labels.length > 0 ? [view.labels[0]] : view.labels;
                                view.hiddenCount = collapseTags && view.labels.length > 1 ? view.labels.length - 1 : 0;
                                view.text = view.labels.join(', ');
                            } else {
                                const current = state.current ? String(state.current) : '';
                                const hit = state.options.find(item => String(item?.value) === current);
                                view.values = current ? [current] : [];
                                view.labels = hit ? [hit.label] : (current ? [current] : []);
                                view.displayLabels = view.labels;
                                view.hiddenCount = 0;
                                view.text = view.labels.join(', ');
                            }
                            return view;
                        });

                        return { state, filteredOptions, selectedText };
                    },
                    methods: {
                        isChecked(value) {
                            return state.values.includes(String(value));
                        },
                        toggleItem(item) {
                            const value = item?.value === null || item?.value === undefined ? '' : String(item.value);
                            if (!value) return;

                            if (multiple) {
                                const idx = state.values.findIndex(v => v === value);
                                if (idx >= 0) state.values.splice(idx, 1);
                                else state.values.push(value);
                                return;
                            }

                            state.current = value;
                        }
                    },
                    template: `
<div class="h-full flex flex-col bg-white">
    <div class="px-4 pt-4 pb-3 border-b border-slate-200 bg-white">
        <el-input
            v-model="state.keyword"
            clearable
            :placeholder="${JSON.stringify(placeholder)}">
            <template #prefix>
                <el-icon><Search /></el-icon>
            </template>
        </el-input>
        <div class="mt-3 text-xs text-slate-500" v-if="selectedText.text">
            已选：{{ selectedText.text }}
        </div>
    </div>
    <div class="flex-1 min-h-0 overflow-auto p-3 bg-slate-50">
        <div class="flex flex-col gap-2">
            <button
                v-for="item in filteredOptions"
                :key="String(item.value)"
                type="button"
                class="w-full text-left bg-white border rounded px-3 py-3 transition flex items-center justify-between hover:border-blue-300"
                :class="multiple
                    ? (isChecked(item.value) ? 'border-blue-500 bg-blue-50' : 'border-slate-200')
                    : (String(state.current) === String(item.value) ? 'border-blue-500 bg-blue-50' : 'border-slate-200')"
                @click="toggleItem(item)">
                <div class="min-w-0">
                    <div class="text-sm text-slate-800 truncate">{{ item.label }}</div>
                    <div class="text-xs text-slate-400 truncate" v-if="String(item.value) !== String(item.label)">{{ item.value }}</div>
                </div>
                <el-icon class="text-blue-500" v-if="multiple ? isChecked(item.value) : (String(state.current) === String(item.value))">
                    <Check />
                </el-icon>
            </button>
            <el-empty v-if="filteredOptions.length === 0" description="暂无数据"></el-empty>
        </div>
    </div>
</div>`
                });

                app.use(hostWindow.ElementPlus, hostWindow.ElementPlusLocaleZhCn ? { locale: hostWindow.ElementPlusLocaleZhCn } : undefined);
                if (hostWindow.ElementPlusIconsVue) {
                    for (const [key, component] of Object.entries(hostWindow.ElementPlusIconsVue)) {
                        app.component(key, component);
                    }
                }
                app.mount(bodyEl);

                setFooterButtons([
                    { text: '取消', action: 'close' },
                    {
                        text: '确定',
                        type: 'primary',
                        handler: (_action, _btn, drawerCtx) => {
                            if (multiple) {
                                form.form.value[modelKey] = state.values.map(v => getRawValue(v));
                            } else {
                                form.form.value[modelKey] = state.current ? getRawValue(state.current) : null;
                            }
                            form.notifyListPickerChanged(modelKey, form.form.value[modelKey], multiple, config.onChange || '');
                            drawerCtx.close({ message: 'selected', data: { type: 'EleListPicker', value: form.form.value[modelKey] } });
                        }
                    }
                ]);

                return () => {
                    try {
                        app.unmount();
                    } catch {
                    }
                };
            }
        });
    }
};
