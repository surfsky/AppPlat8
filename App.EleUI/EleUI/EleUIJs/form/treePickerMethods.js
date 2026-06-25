export function initTreePickerState(form, vueApi) {
    const { ref } = vueApi;
    form.treePickerTick = ref(0);
}

export const treePickerMethods = {
    /**Normalize tree picker model value */
    normalizeTreePickerValue(value, multiple = false) {
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

    /**Collect flat tree nodes for display */
    flattenTreeNodes(list, idField = 'id', nameField = 'name', childrenField = 'children', rows = []) {
        const nodes = Array.isArray(list) ? list : [];
        for (let i = 0; i < nodes.length; i += 1) {
            const node = nodes[i];
            if (!node || typeof node !== 'object') continue;

            rows.push({
                id: node[idField] === null || node[idField] === undefined ? '' : String(node[idField]),
                name: node[nameField] === null || node[nameField] === undefined ? '' : String(node[nameField]),
                raw: node
            });

            const children = node[childrenField];
            if (Array.isArray(children) && children.length > 0) {
                this.flattenTreeNodes(children, idField, nameField, childrenField, rows);
            }
        }
        return rows;
    },

    /**Build tree picker display payload */
    getTreePickerView(modelKey, target, fallbackData = [], idField = 'id', nameField = 'name', childrenField = 'children', multiple = false, collapseTags = false) {
        const model = this.form?.value ? this.form.value[modelKey] : null;
        const normalized = this.normalizeTreePickerValue(model, multiple);
        const values = Array.isArray(normalized) ? normalized : (normalized ? [normalized] : []);
        const treeData = typeof this.getControlTreeData === 'function'
            ? this.getControlTreeData(target, fallbackData)
            : (Array.isArray(fallbackData) ? fallbackData : []);
        const flat = this.flattenTreeNodes(treeData, idField, nameField, childrenField, []);
        const nameMap = new Map(flat.map(item => [item.id, item.name || item.id]));
        const labels = values.map(v => nameMap.get(String(v)) || String(v));

        return {
            values,
            labels,
            displayLabels: collapseTags && labels.length > 0 ? [labels[0]] : labels,
            hiddenCount: collapseTags && labels.length > 1 ? labels.length - 1 : 0,
            text: labels.join(', ')
        };
    },

    /**Clear tree picker selection */
    clearTreePicker(modelKey, multiple = false, onChange = '') {
        if (!this.form?.value) return;
        this.form.value[modelKey] = multiple ? [] : null;
        this.notifyTreePickerChanged(modelKey, multiple ? [] : null, multiple, onChange);
    },

    /**Post tree picker change callback */
    notifyTreePickerChanged(modelKey, value, multiple = false, onChange = '') {
        if (!onChange || typeof this.postHandler !== 'function') return;

        this.postHandler(onChange, {
            eventName: 'change',
            controlId: `field:${modelKey}`,
            fieldExpress: modelKey,
            value,
            form: this.form?.value || {},
            controlType: 'EleTreePicker',
            multiple: !!multiple
        });
    },

    /**Open root drawer tree picker */
    async openTreePicker(config = {}) {
        if (!config || !config.modelKey || !this.form?.value) return;
        if (!window.EleManager || typeof EleManager.openDrawer !== 'function') return;

        const modelKey = config.modelKey;
        const target = config.target || `field:${modelKey}`;
        const idField = config.idField || 'id';
        const nameField = config.nameField || 'name';
        const childrenField = config.childrenField || 'children';
        const multiple = !!config.multiple;
        const collapseTags = !!config.collapseTags;
        const checkStrictly = config.checkStrictly !== false;
        const title = config.title || config.label || '选择';
        const size = config.size || '';
        const source = config.source || '';
        const fallbackData = Array.isArray(config.fallbackData) ? config.fallbackData : [];
        const placeholder = config.placeholder || '请输入关键字过滤';
        const currentValue = this.normalizeTreePickerValue(this.form.value[modelKey], multiple);

        if (source && !this.options?.value?.[modelKey]) {
            await this.fetchOptions(modelKey, source, idField, childrenField);
        }

        const loadedData = Array.isArray(this.options?.value?.[modelKey]) ? this.options.value[modelKey] : null;
        const fallback = loadedData || fallbackData;
        const treeData = typeof this.getControlTreeData === 'function'
            ? this.getControlTreeData(target, fallback)
            : fallback;
        const selectedValues = Array.isArray(currentValue)
            ? [...currentValue]
            : (currentValue ? [currentValue] : []);
        const form = this;

        EleManager.openDrawer({
            title,
            direction: 'rtl',
            size,
            resizable: true,
            closeOnClickModal: false,
            destroyOnClose: true,
            custom: true,
            bodyClass: 'ele-tree-picker-drawer-host',
            mountHandler(ctx) {
                const { hostWindow, bodyEl, setFooterButtons } = ctx;
                const { createApp, reactive, nextTick } = hostWindow.Vue;

                const state = reactive({
                    keyword: '',
                    nodes: Array.isArray(treeData) ? treeData : [],
                    values: [...selectedValues],
                    current: !multiple && selectedValues.length > 0 ? selectedValues[0] : '',
                    collapseTags
                });

                const app = createApp({
                    data() {
                        return { state };
                    },
                    methods: {
                        /**Filter tree nodes */
                        onFilterChange(value) {
                            state.keyword = value || '';
                            const tree = this.$refs.treeRef;
                            if (tree && typeof tree.filter === 'function') {
                                tree.filter(state.keyword);
                            }
                        },
                        /**Element tree filter predicate */
                        filterNode(value, data) {
                            if (!value) return true;
                            const text = data && data[nameField] !== undefined && data[nameField] !== null
                                ? String(data[nameField])
                                : '';
                            return text.toLowerCase().includes(String(value).toLowerCase());
                        },
                        /**Handle single select click */
                        onNodeClick(data) {
                            if (multiple) return;
                            const val = data && data[idField] !== undefined && data[idField] !== null
                                ? String(data[idField])
                                : '';
                            state.current = val;
                        },
                        /**Handle multiple check change */
                        syncChecked() {
                            if (!multiple) return;
                            const tree = this.$refs.treeRef;
                            if (!tree) return;
                            const keys = typeof tree.getCheckedKeys === 'function'
                                ? tree.getCheckedKeys(false)
                                : [];
                            state.values = Array.isArray(keys) ? keys.map(v => String(v)) : [];
                        },
                        /**Return current selected labels */
                        getSummary() {
                            const view = form.getTreePickerView(
                                modelKey,
                                target,
                                state.nodes,
                                idField,
                                nameField,
                                childrenField,
                                multiple,
                                state.collapseTags
                            );
                            if (multiple) {
                                view.values = [...state.values];
                            } else {
                                view.values = state.current ? [state.current] : [];
                                const picked = view.values.map(v => {
                                    const flat = form.flattenTreeNodes(state.nodes, idField, nameField, childrenField, []);
                                    const hit = flat.find(item => item.id === v);
                                    return hit ? hit.name : v;
                                });
                                view.labels = picked;
                                view.displayLabels = picked;
                                view.hiddenCount = 0;
                                view.text = picked.join(', ');
                            }
                            return view;
                        }
                    },
                    template: `
<div class="ele-tree-picker-panel h-full flex flex-col">
    <div class="px-4 pt-4 pb-3 border-b border-slate-200 bg-white">
        <el-input
            :model-value="state.keyword"
            clearable
            :placeholder="${JSON.stringify(placeholder)}"
            @update:model-value="onFilterChange"
        >
            <template #prefix>
                <el-icon><Search /></el-icon>
            </template>
        </el-input>
        <div class="mt-3 text-xs text-slate-500" v-if="getSummary().text">
            已选：{{ getSummary().text }}
        </div>
    </div>
    <div class="flex-1 min-h-0 overflow-auto bg-white p-2">
        <el-tree
            ref="treeRef"
            node-key="${idField}"
            :data="state.nodes"
            :props="{ label: '${nameField}', children: '${childrenField}' }"
            :default-expand-all="true"
            :expand-on-click-node="${multiple ? 'false' : 'true'}"
            :highlight-current="${multiple ? 'false' : 'true'}"
            :show-checkbox="${multiple ? 'true' : 'false'}"
            :check-strictly="${checkStrictly ? 'true' : 'false'}"
            :check-on-click-node="${multiple ? 'true' : 'false'}"
            :current-node-key="state.current"
            :default-checked-keys="state.values"
            :filter-node-method="filterNode"
            class="w-full"
            @node-click="onNodeClick"
            @check="syncChecked"
        ></el-tree>
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

                nextTick(() => {
                    const vm = app._instance?.proxy;
                    if (multiple) {
                        const tree = vm?.$refs?.treeRef;
                        if (tree && typeof tree.setCheckedKeys === 'function') {
                            tree.setCheckedKeys(state.values);
                        }
                    }
                });

                setFooterButtons([
                    { text: '取消', action: 'close' },
                    {
                        text: '确定',
                        type: 'primary',
                        handler: (_action, _btn, drawerCtx) => {
                            const vm = app._instance?.proxy;
                            let result;
                            if (multiple) {
                                const tree = vm?.$refs?.treeRef;
                                const keys = tree && typeof tree.getCheckedKeys === 'function'
                                    ? tree.getCheckedKeys(false)
                                    : state.values;
                                result = Array.isArray(keys) ? keys.map(v => String(v)) : [];
                                form.form.value[modelKey] = result;
                            } else {
                                result = state.current ? String(state.current) : null;
                                form.form.value[modelKey] = result;
                            }
                            form.notifyTreePickerChanged(modelKey, form.form.value[modelKey], multiple, config.onChange || '');
                            drawerCtx.close({ message: 'selected', data: { type: 'EleTreePicker', value: form.form.value[modelKey] } });
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
