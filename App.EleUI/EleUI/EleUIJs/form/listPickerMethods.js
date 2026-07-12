export const listPickerMethods = {
    /**Pick better option when duplicate values exist */
    preferListPickerOption(current, next) {
        if (!current) return next;
        if (!next) return current;

        const currentLabel = current?.label === null || current?.label === undefined ? '' : String(current.label);
        const nextLabel = next?.label === null || next?.label === undefined ? '' : String(next.label);
        const currentHasGroup = currentLabel.includes('/');
        const nextHasGroup = nextLabel.includes('/');

        if (currentHasGroup !== nextHasGroup) {
            return currentHasGroup ? next : current;
        }

        if (nextLabel.length > 0 && (currentLabel.length === 0 || nextLabel.length < currentLabel.length)) {
            return next;
        }

        return current;
    },

    /**Remove duplicate options by value */
    dedupeListPickerOptions(options = []) {
        const map = new Map();
        for (const item of (Array.isArray(options) ? options : [])) {
            const key = item?.value === null || item?.value === undefined ? '' : String(item.value);
            if (!key) continue;

            const prev = map.get(key);
            map.set(key, this.preferListPickerOption(prev, item));
        }
        return Array.from(map.values());
    },

    /**Read static list picker options from current page */
    getListPickerFallbackOptions(modelKey, target) {
        if (typeof document === 'undefined') return [];

        const selectors = [];
        if (modelKey && target) {
            selectors.push(`.ele-list-picker-wrapper[data-ele-field="${String(modelKey)}"][data-select-target="${String(target)}"]`);
        }
        if (modelKey) {
            selectors.push(`.ele-list-picker-wrapper[data-select-model="${String(modelKey)}"]`);
        }

        for (const selector of selectors) {
            try {
                const el = document.querySelector(selector);
                const raw = el?.getAttribute('data-static-options') || '';
                if (!raw) continue;
                const parsed = JSON.parse(raw);
                if (Array.isArray(parsed)) return parsed;
            } catch {
            }
        }

        return [];
    },

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
        const fallback = Array.isArray(fallbackOptions) && fallbackOptions.length > 0
            ? fallbackOptions
            : this.getListPickerFallbackOptions(modelKey, target);
        const options = typeof this.getControlOptions === 'function'
            ? this.getControlOptions(target, fallback)
            : fallback;
        const finalOptions = this.dedupeListPickerOptions(options);

        const labelMap = new Map(
            finalOptions.map(item => [
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
        const allowCreate = !!config.allowCreate;
        const filterable = config.filterable !== false;
        const collapseTags = !!config.collapseTags;
        const title = config.title || config.label || '选择';
        const size = config.size || '';
        const placeholder = config.placeholder || '请输入关键字过滤';
        const fallbackOptions = Array.isArray(config.fallbackOptions) && config.fallbackOptions.length > 0
            ? config.fallbackOptions
            : this.getListPickerFallbackOptions(modelKey, target);
        const currentValue = this.normalizeListPickerValue(this.form.value[modelKey], multiple);
        const selectedValues = Array.isArray(currentValue)
            ? [...currentValue]
            : (currentValue ? [currentValue] : []);
        const form = this;

        const sourceOptions = typeof this.getControlOptions === 'function'
            ? this.getControlOptions(target, fallbackOptions)
            : fallbackOptions;
        const finalOptions = this.dedupeListPickerOptions(sourceOptions);

        const getRawValue = (raw) => {
            const match = finalOptions
                .find(item => String(item?.value) === String(raw));
            return match ? match.value : raw;
        };

        EleManager.openDrawer({
            title,
            direction: 'rtl',
            size: size || (window.innerWidth < 768 ? '100%' : '50%'),
            footerAlign: 'center',
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
                    options: finalOptions,
                    values: [...selectedValues],
                    current: !multiple && selectedValues.length > 0 ? selectedValues[0] : '',
                    collapseTags,
                    allowCreate,
                    filterable
                });

                const app = createApp({
                    setup() {
                        const filteredOptions = computed(() => {
                            const keyword = String(state.keyword || '').trim().toLowerCase();
                            const baseOptions = !keyword
                                ? state.options
                                : state.options.filter(item => {
                                    const label = item?.label === null || item?.label === undefined ? '' : String(item.label);
                                    const value = item?.value === null || item?.value === undefined ? '' : String(item.value);
                                    return label.toLowerCase().includes(keyword) || value.toLowerCase().includes(keyword);
                                });

                            if (!state.allowCreate) return baseOptions;
                            if (!keyword) return baseOptions;

                            const exists = state.options.some(item => {
                                const label = item?.label === null || item?.label === undefined ? '' : String(item.label);
                                const value = item?.value === null || item?.value === undefined ? '' : String(item.value);
                                return label.toLowerCase() === keyword || value.toLowerCase() === keyword;
                            });
                            if (exists) return baseOptions;

                            return [
                                {
                                    label: String(state.keyword || '').trim(),
                                    value: String(state.keyword || '').trim(),
                                    __custom: true
                                },
                                ...baseOptions
                            ];
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
            v-if="state.filterable"
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
    <div class="flex-1 min-h-0 overflow-auto bg-white">
        <div class="divide-y divide-slate-200">
            <button
                v-for="(item, idx) in filteredOptions"
                :key="String(item.value)"
                type="button"
                class="relative w-full text-left px-4 py-3 transition flex items-center gap-3 bg-white hover:bg-slate-50"
                :class="multiple
                    ? (isChecked(item.value) ? 'bg-blue-50/90 shadow-[inset_4px_0_0_0_#2563eb]' : '')
                    : (String(state.current) === String(item.value) ? 'bg-blue-50/90 shadow-[inset_4px_0_0_0_#2563eb]' : '')"
                @click="toggleItem(item)">
                <div class="w-8 shrink-0 text-sm font-semibold text-center"
                    :class="multiple
                        ? (isChecked(item.value) ? 'text-blue-700' : 'text-slate-400')
                        : (String(state.current) === String(item.value) ? 'text-blue-700' : 'text-slate-400')">
                    {{ idx + 1 }}
                </div>
                <div class="min-w-0 flex-1">
                    <div class="text-sm truncate font-medium"
                        :class="multiple
                            ? (isChecked(item.value) ? 'text-blue-900' : 'text-slate-800')
                            : (String(state.current) === String(item.value) ? 'text-blue-900' : 'text-slate-800')">{{ item.label }}</div>
                </div>
                <el-icon class="shrink-0 text-blue-600" v-if="multiple ? isChecked(item.value) : (String(state.current) === String(item.value))">
                    <Check />
                </el-icon>
            </button>
        </div>
        <el-empty v-if="filteredOptions.length === 0" description="暂无数据" class="py-12"></el-empty>
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
