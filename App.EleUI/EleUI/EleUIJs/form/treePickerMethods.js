export function initTreePickerState(form, vueApi) {
    const { ref } = vueApi;
    form.treePickerTick = ref(0);
}

export const treePickerMethods = {
    /**Normalize tree picker nodes and map enabled=false to Element Plus disabled */
    normalizeTreePickerNodes(list, childrenField = 'children', chunkSelectable = true) {
        const nodes = Array.isArray(list) ? list : [];
        return nodes.map(node => {
            if (!node || typeof node !== 'object') return node;
            const clone = { ...node };
            const children = clone[childrenField];
            const hasChildren = Array.isArray(children) && children.length > 0;
            clone.disabled = clone.disabled === true || clone.enabled === false || (!chunkSelectable && hasChildren);
            if (hasChildren) {
                clone[childrenField] = this.normalizeTreePickerNodes(children, childrenField, chunkSelectable);
            }
            return clone;
        });
    },

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
        const nameMap = new Map(flat.map(item => {
            const displayName = item.raw && item.raw.displayName ? String(item.raw.displayName) : (item.name || String(item.id));
            const idText = item.id === null || item.id === undefined ? '' : String(item.id);
            const labelText = idText ? `${displayName} (${idText})` : displayName;
            return [item.id, labelText];
        }));
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

    removeTreePickerTag(modelKey, value, onChange = '') {
        if (!this.form?.value) return;
        const values = this.normalizeTreePickerValue(this.form.value[modelKey], true);
        const remaining = values.filter(item => item !== String(value));
        this.form.value[modelKey] = remaining;
        this.notifyTreePickerChanged(modelKey, remaining, true, onChange);
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
        const chunkSelectable = config.chunkSelectable !== false;
        const collapseTags = !!config.collapseTags;
        const checkStrictly = config.checkStrictly !== false;
        const title = config.title || config.label || '选择';
        const size = config.size || '';
        const source = config.source || '';
        const fallbackData = Array.isArray(config.fallbackData) ? config.fallbackData : [];
        const placeholder = config.placeholder || '请输入关键字过滤';
        const expandLevel = Number.isFinite(Number(config.expandLevel)) ? Number(config.expandLevel) : 5;
        const showAllButton = !!config.showAllButton;
        const currentValue = this.normalizeTreePickerValue(this.form.value[modelKey], multiple);

        if (source && !this.options?.value?.[modelKey]) {
            await this.fetchOptions(modelKey, source, idField, childrenField);
        }

        const loadedData = Array.isArray(this.options?.value?.[modelKey]) ? this.options.value[modelKey] : null;
        const fallback = loadedData || fallbackData;
        const treeData = typeof this.getControlTreeData === 'function'
            ? this.getControlTreeData(target, fallback)
            : fallback;
        const normalizedTreeData = this.normalizeTreePickerNodes(treeData, childrenField, chunkSelectable);
        const isLeafKey = (nodes, key) => {
            if (!Array.isArray(nodes)) return false;
            for (const node of nodes) {
                if (!node || typeof node !== 'object') continue;
                if (String(node[idField]) === String(key)) {
                    const children = node[childrenField];
                    return !Array.isArray(children) || children.length === 0;
                }
                if (isLeafKey(node[childrenField], key)) return true;
            }
            return false;
        };
        const selectedValues = Array.isArray(currentValue)
            ? [...currentValue]
            : (currentValue ? [currentValue] : []);
        if (!chunkSelectable) {
            for (let i = selectedValues.length - 1; i >= 0; i -= 1) {
                if (!isLeafKey(normalizedTreeData, selectedValues[i])) selectedValues.splice(i, 1);
            }
        }
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
                    nodes: Array.isArray(normalizedTreeData) ? normalizedTreeData : [],
                    values: [...selectedValues],
                    current: !multiple && selectedValues.length > 0 ? selectedValues[0] : '',
                    collapseTags,
                    expandKeys: [],
                    baseExpandKeys: [],
                    expandAll: expandLevel < 0,
                    treeKey: 0
                });

                // 收集前 expandLevel 层有子节点的节点 key
                const collectExpandKeys = (nodes, depth, keys) => {
                    if (!Array.isArray(nodes) || depth >= expandLevel) return keys;
                    for (const n of nodes) {
                        const kids = n && n[childrenField];
                        if (Array.isArray(kids) && kids.length > 0) {
                            keys.push(n[idField]);
                            collectExpandKeys(kids, depth + 1, keys);
                        }
                    }
                    return keys;
                };
                if (expandLevel > 0) state.baseExpandKeys = collectExpandKeys(state.nodes, 0, []);
                const collectAllExpandKeys = (nodes, keys = []) => {
                    if (!Array.isArray(nodes)) return keys;
                    for (const node of nodes) {
                        const children = node && node[childrenField];
                        if (Array.isArray(children) && children.length > 0) {
                            keys.push(node[idField]);
                            collectAllExpandKeys(children, keys);
                        }
                    }
                    return keys;
                };
                const allExpandKeys = collectAllExpandKeys(state.nodes);
                if (expandLevel < 0) state.baseExpandKeys = [...allExpandKeys];
                state.expandKeys = [...state.baseExpandKeys];

                const app = createApp({
                    data() {
                        return { state };
                    },
                    methods: {
                        setExpandAll(value) {
                            state.expandAll = !!value;
                            state.expandKeys = state.expandAll ? [...allExpandKeys] : [...state.baseExpandKeys];
                            state.treeKey += 1;
                        },
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
                            if (data && data.disabled === true) {
                                const tree = this.$refs.treeRef;
                                if (tree && typeof tree.setCurrentKey === 'function') {
                                    tree.setCurrentKey(state.current === '' ? null : state.current);
                                }
                                return;
                            }
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
                            const selectableKeys = Array.isArray(keys) ? keys : [];
                            state.values = selectableKeys
                                .filter(key => chunkSelectable || isLeafKey(state.nodes, key))
                                .map(v => String(v));
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
                                const flat = form.flattenTreeNodes(state.nodes, idField, nameField, childrenField, []);
                                view.labels = state.values.map(value => {
                                    const id = String(value);
                                    const hit = flat.find(item => item.id === id);
                                    if (!hit) return id;
                                    const label = hit.raw && hit.raw.displayName ? String(hit.raw.displayName) : (hit.name || id);
                                    return `${label} (${id})`;
                                });
                                view.displayLabels = collapseTags && view.labels.length > 0 ? [view.labels[0]] : view.labels;
                                view.hiddenCount = collapseTags ? view.labels.length - view.displayLabels.length : 0;
                                view.text = view.labels.join(', ');
                            } else {
                                view.values = state.current ? [state.current] : [];
                                const flat = form.flattenTreeNodes(state.nodes, idField, nameField, childrenField, []);
                                const picked = view.values.map(v => {
                                    const id = String(v);
                                    const hit = flat.find(item => item.id === id);
                                    if (!hit) return id;
                                    const displayName = hit.raw && hit.raw.displayName ? String(hit.raw.displayName) : (hit.name || id);
                                    return `${displayName} (${id})`;
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
        <div class="flex items-center gap-3">
        <el-input
            class="flex-1"
            :model-value="state.keyword"
            clearable
            :placeholder='${JSON.stringify(placeholder)}'
            @update:model-value="onFilterChange"
        >
            <template #prefix>
                <el-icon><Search /></el-icon>
            </template>
        </el-input>
        <el-switch v-if="${showAllButton ? 'true' : 'false'}" :model-value="state.expandAll" inline-prompt active-text="展开" inactive-text="恢复" @change="setExpandAll"></el-switch>
        </div>
        <div class="mt-3 text-xs text-slate-500" v-if="getSummary().text">
            已选：{{ getSummary().text }}
        </div>
    </div>
    <div class="flex-1 min-h-0 overflow-auto bg-white p-2">
        <el-tree
            :key="state.treeKey"
            ref="treeRef"
            node-key="${idField}"
            :data="state.nodes"
            :props="{ label: '${nameField}', children: '${childrenField}', disabled: 'disabled' }"
            :default-expand-all="${expandLevel < 0 && !showAllButton ? 'true' : 'false'}"
            :default-expanded-keys="state.expandKeys"
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

                ctx.state.footerAlignClass = 'justify-center';
                setFooterButtons([
                    {
                        text: '确定',
                        icon: 'Check',
                        type: 'primary',
                        handler: (_action, _btn, drawerCtx) => {
                            const vm = app._instance?.proxy;
                            let result;
                            if (multiple) {
                                const tree = vm?.$refs?.treeRef;
                                const keys = tree && typeof tree.getCheckedKeys === 'function'
                                    ? tree.getCheckedKeys(false)
                                    : state.values;
                                result = (Array.isArray(keys) ? keys : [])
                                    .filter(key => chunkSelectable || isLeafKey(state.nodes, key))
                                    .map(v => String(v));
                                form.form.value[modelKey] = result;
                            } else {
                                result = state.current ? String(state.current) : null;
                                form.form.value[modelKey] = result;
                            }
                            form.notifyTreePickerChanged(modelKey, form.form.value[modelKey], multiple, config.onChange || '');
                            drawerCtx.close({ message: 'selected', data: { type: 'EleTreePicker', value: form.form.value[modelKey] } });
                        }
                    },
                    { text: '取消', icon: 'Close', action: 'close' }
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
