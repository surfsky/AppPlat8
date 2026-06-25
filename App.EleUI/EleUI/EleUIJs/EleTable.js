import { initPaginationState, paginationMethods } from './table/paginationMethods.js';
import { commandMethods } from './table/commandMethods.js';
import { initDrawerState, drawerMethods } from './table/drawerMethods.js';
import { messageMethods } from './table/messageMethods.js';

// Encapsulates common logic for List pages using Vue 3 + Element Plus
export class EleTable {
    constructor(options = {}) {
        this.config = options;

        // Split domains: pagination/data loading, command dispatch, drawer, message
        initPaginationState(this, Vue, options);
        initDrawerState(this, Vue);
    }

    // Dynamic options (for TreeSelect etc)
    async fetchOptions(key, url) {
        if (this.options.value[key]) return;
        try {
            const res = await axios.get(url);
            if (res.data.code === 0 || res.data.code === '0') {
                const data = this.normalizeIds(res.data.data);
                this.options.value[key] = data;
                this.sanitizeTreeModelValue(key, data);
            }
        } catch (e) {
            console.error('Fetch options failed:', url, e);
        }
    }

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
    }

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
    }

    getTreePickerView(modelKey, _target, fallbackData = [], idField = 'id', nameField = 'name', childrenField = 'children', multiple = false, collapseTags = false) {
        const model = this.filters?.value ? this.filters.value[modelKey] : null;
        const normalized = this.normalizeTreePickerValue(model, multiple);
        const values = Array.isArray(normalized) ? normalized : (normalized ? [normalized] : []);

        const loaded = Array.isArray(this.options?.value?.[modelKey]) ? this.options.value[modelKey] : null;
        const treeData = loaded || (Array.isArray(fallbackData) ? fallbackData : []);

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
    }

    clearTreePicker(modelKey, multiple = false, _onChange = '') {
        if (!this.filters?.value) return;
        this.filters.value[modelKey] = multiple ? [] : null;
    }

    async openTreePicker(config = {}) {
        if (!config || !config.modelKey || !this.filters?.value) return;
        if (!window.EleManager || typeof EleManager.openDrawer !== 'function') return;

        const modelKey = config.modelKey;
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

        if (source && !this.options?.value?.[modelKey]) {
            await this.fetchOptions(modelKey, source);
        }

        const loadedData = Array.isArray(this.options?.value?.[modelKey]) ? this.options.value[modelKey] : null;
        const treeData = loadedData || fallbackData;
        const currentValue = this.normalizeTreePickerValue(this.filters.value[modelKey], multiple);
        const selectedValues = Array.isArray(currentValue)
            ? [...currentValue]
            : (currentValue ? [currentValue] : []);
        const table = this;

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
                        onFilterChange(value) {
                            state.keyword = value || '';
                            const tree = this.$refs.treeRef;
                            if (tree && typeof tree.filter === 'function') {
                                tree.filter(state.keyword);
                            }
                        },
                        filterNode(value, data) {
                            if (!value) return true;
                            const text = data && data[nameField] !== undefined && data[nameField] !== null
                                ? String(data[nameField])
                                : '';
                            return text.toLowerCase().includes(String(value).toLowerCase());
                        },
                        onNodeClick(data) {
                            if (multiple) return;
                            const val = data && data[idField] !== undefined && data[idField] !== null
                                ? String(data[idField])
                                : '';
                            state.current = val;
                        },
                        syncChecked() {
                            if (!multiple) return;
                            const tree = this.$refs.treeRef;
                            if (!tree) return;
                            const keys = typeof tree.getCheckedKeys === 'function'
                                ? tree.getCheckedKeys(false)
                                : [];
                            state.values = Array.isArray(keys) ? keys.map(v => String(v)) : [];
                        },
                        getSummary() {
                            const view = table.getTreePickerView(
                                modelKey,
                                '',
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
                                    const flat = table.flattenTreeNodes(state.nodes, idField, nameField, childrenField, []);
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
                            if (multiple) {
                                const tree = vm?.$refs?.treeRef;
                                const keys = tree && typeof tree.getCheckedKeys === 'function'
                                    ? tree.getCheckedKeys(false)
                                    : state.values;
                                const result = Array.isArray(keys) ? keys.map(v => String(v)) : [];
                                table.filters.value[modelKey] = result;
                            } else {
                                const result = state.current ? String(state.current) : null;
                                table.filters.value[modelKey] = result;
                            }
                            drawerCtx.close({ message: 'selected', data: { type: 'EleTreePicker', value: table.filters.value[modelKey] } });
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

    // Keep id/value type consistent for remote trees (e.g. "10" -> 10),
    // otherwise el-tree-select may show raw id instead of label.
    normalizeIds(obj) {
        if (Array.isArray(obj)) {
            return obj.map(o => this.normalizeIds(o));
        }
        if (obj && typeof obj === 'object') {
            const out = {};
            for (const [k, v] of Object.entries(obj)) {
                if (k === 'id' || k === 'value') {
                    if (typeof v === 'string' && /^-?\d+(\.\d+)?$/.test(v)) out[k] = Number(v);
                    else out[k] = v;
                } else if (k === 'children' && Array.isArray(v)) {
                    out[k] = v.map(c => this.normalizeIds(c));
                } else {
                    out[k] = this.normalizeIds(v);
                }
            }
            return out;
        }
        return obj;
    }

    findTreeNodeById(treeData, value, idField = 'id', childrenField = 'children') {
        if (!Array.isArray(treeData)) return null;
        for (const node of treeData) {
            if (node && node[idField] === value) return node;
            const child = this.findTreeNodeById(node && node[childrenField], value, idField, childrenField);
            if (child) return child;
        }
        return null;
    }

    sanitizeTreeModelValue(modelKey, treeData, idField = 'id', childrenField = 'children') {
        if (!this.filters || !this.filters.value || !modelKey) return;
        const current = this.filters.value[modelKey];
        if (current === undefined || current === null || current === '') return;

        if (Array.isArray(current)) {
            const next = current
                .map(v => {
                    const node = this.findTreeNodeById(treeData, v, idField, childrenField);
                    return node ? node[idField] : v;
                })
                .filter(v => v !== undefined && v !== null && v !== '');
            this.filters.value[modelKey] = next;
            return;
        }

        const matched = this.findTreeNodeById(treeData, current, idField, childrenField);
        if (matched) {
            this.filters.value[modelKey] = matched[idField];
            return;
        }

        // Fallback for numeric/string mismatch: try equivalent string/number value.
        if (typeof current === 'number' || typeof current === 'string') {
            const alt = typeof current === 'number' ? String(current) : Number(current);
            const altMatched = this.findTreeNodeById(treeData, alt, idField, childrenField);
            if (altMatched) this.filters.value[modelKey] = altMatched[idField];
        }
    }
}

Object.assign(
    EleTable.prototype,
    paginationMethods,
    commandMethods,
    drawerMethods,
    messageMethods
);
