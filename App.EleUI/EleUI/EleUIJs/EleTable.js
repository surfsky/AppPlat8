import { initPaginationState, paginationMethods } from './table/paginationMethods.js';
import { commandMethods } from './table/commandMethods.js';
import { initDrawerState, drawerMethods } from './table/drawerMethods.js';
import { messageMethods } from './table/messageMethods.js';
import { initPickerState, pickerMethods } from './form/pickerMethods.js';

// Encapsulates common logic for List pages using Vue 3 + Element Plus
export class EleTable {
    constructor(options = {}) {
        this.config = options;
        this._hostId = (options.hostId || '').toString().replace(/^#/, '');

        // Split domains: pagination/data loading, command dispatch, drawer, message, picker
        initPaginationState(this, Vue, options);
        initDrawerState(this, Vue);
        initPickerState(this, Vue);
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

    getTreePickerView(modelKey, target, fallbackData = [], idField = 'id', nameField = 'name', childrenField = 'children', multiple = false, collapseTags = false) {
        const model = this.filters?.value ? this.filters.value[modelKey] : null;
        const normalized = this.normalizeTreePickerValue(model, multiple);
        const values = Array.isArray(normalized) ? normalized : (normalized ? [normalized] : []);

        const loaded = Array.isArray(this.options?.value?.[modelKey]) ? this.options.value[modelKey] : null;
        const fallback = loaded || (Array.isArray(fallbackData) ? fallbackData : []);
        const treeData = this.getControlTreeData(target, fallback);

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

    removeTreePickerTag(modelKey, value) {
        if (!this.filters?.value) return;
        const values = this.normalizeTreePickerValue(this.filters.value[modelKey], true);
        this.filters.value[modelKey] = values.filter(item => item !== String(value));
    }

    async openTreePicker(config = {}) {
        if (!config || !config.modelKey || !this.filters?.value) return;
        if (!window.EleManager || typeof EleManager.openDrawer !== 'function') return;

        const modelKey = config.modelKey;
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

        if (source && !this.options?.value?.[modelKey]) {
            await this.fetchOptions(modelKey, source);
        }

        const loadedData = Array.isArray(this.options?.value?.[modelKey]) ? this.options.value[modelKey] : null;
        const fallback = loadedData || fallbackData;
        const treeData = this.getControlTreeData(config.target || `field:${modelKey}`, fallback);
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
        const currentValue = this.normalizeTreePickerValue(this.filters.value[modelKey], multiple);
        const selectedValues = Array.isArray(currentValue)
            ? [...currentValue]
            : (currentValue ? [currentValue] : []);
        if (!chunkSelectable) {
            for (let i = selectedValues.length - 1; i >= 0; i -= 1) {
                if (!isLeafKey(normalizedTreeData, selectedValues[i])) selectedValues.splice(i, 1);
            }
        }

        // 关键：el-tree 的 :current-node-key / :default-checked-keys 用 === 严格匹配节点 id 字段。
        // selectedValues 当前是 String 化的"730"，而 treeData 经 normalizeIds 后是 number 730，
        // 直接传会导致 el-tree 无法高亮/勾选。这里在 treeData 中找到对应节点，
        // 用节点的原始 id 字段值（保持类型一致）作为 state.current / state.values。
        const coerceToNodeId = (rawValue) => {
            if (rawValue === null || rawValue === undefined || rawValue === '') return rawValue;
            const node = this.findTreeNodeById(normalizedTreeData, rawValue, idField, childrenField);
            if (node) return node[idField];
            // 兼容 string/number 互换
            if (typeof rawValue === 'number') {
                const alt = this.findTreeNodeById(normalizedTreeData, String(rawValue), idField, childrenField);
                if (alt) return alt[idField];
            } else if (typeof rawValue === 'string' && /^-?\d+(\.\d+)?$/.test(rawValue)) {
                const alt = this.findTreeNodeById(normalizedTreeData, Number(rawValue), idField, childrenField);
                if (alt) return alt[idField];
            }
            return rawValue;
        };
        const initialValues = selectedValues.map(coerceToNodeId);
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
                    nodes: Array.isArray(normalizedTreeData) ? normalizedTreeData : [],
                    values: [...initialValues],
                    current: !multiple && initialValues.length > 0 ? initialValues[0] : '',
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
                            if (data && data.disabled === true) {
                                const tree = this.$refs.treeRef;
                                if (tree && typeof tree.setCurrentKey === 'function') {
                                    tree.setCurrentKey(state.current === '' ? null : state.current);
                                }
                                return;
                            }
                            if (!data || data[idField] === undefined || data[idField] === null) {
                                state.current = '';
                                return;
                            }
                            // 保持与 el-tree 节点 id 字段类型一致（不 String()），避免下次打开弹窗时类型不匹配
                            state.current = data[idField];
                        },
                        syncChecked() {
                            if (!multiple) return;
                            const tree = this.$refs.treeRef;
                            if (!tree) return;
                            const keys = typeof tree.getCheckedKeys === 'function'
                                ? tree.getCheckedKeys(false)
                                : [];
                            // el-tree.getCheckedKeys() 返回的 key 与 node-key 字段同类型（已被 normalizeIds 处理），
                            // 直接保留原始类型，避免下次打开弹窗时类型不匹配。
                            const selectableKeys = Array.isArray(keys) ? keys : [];
                            state.values = selectableKeys
                                .filter(key => chunkSelectable || isLeafKey(state.nodes, key))
                                .map(key => key);
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
                                const flat = table.flattenTreeNodes(state.nodes, idField, nameField, childrenField, []);
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
                                const flat = table.flattenTreeNodes(state.nodes, idField, nameField, childrenField, []);
                                const picked = view.values.map(value => {
                                    const id = String(value);
                                    const hit = flat.find(item => item.id === id);
                                    if (!hit) return id;
                                    const label = hit.raw && hit.raw.displayName ? String(hit.raw.displayName) : (hit.name || id);
                                    return `${label} (${id})`;
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
                    { text: '取消', icon: 'Close', action: 'close' },
                    {
                        text: '确定',
                        icon: 'Check',
                        type: 'primary',
                        handler: (_action, _btn, drawerCtx) => {
                            const vm = app._instance?.proxy;
                            if (multiple) {
                                const tree = vm?.$refs?.treeRef;
                                const keys = tree && typeof tree.getCheckedKeys === 'function'
                                    ? tree.getCheckedKeys(false)
                                    : state.values;
                                // 直接保留 el-tree 返回的原始类型
                                table.filters.value[modelKey] = (Array.isArray(keys) ? keys : [])
                                    .filter(key => chunkSelectable || isLeafKey(state.nodes, key));
                            } else {
                                // 直接保留 state.current 的原始类型（与 el-tree 节点 id 同类型）
                                table.filters.value[modelKey] = (state.current === null || state.current === undefined || state.current === '')
                                    ? null
                                    : state.current;
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

    /** 解析控件的 JSON 属性（兼容历史上被二次转义的值） */
    parseJsonAttribute(raw) {
        if (!raw || typeof raw !== 'string') return null;

        try {
            return JSON.parse(raw);
        } catch {
            const decoded = raw
                .replace(/&amp;quot;/g, '"')
                .replace(/&quot;/g, '"')
                .replace(/&#34;/g, '"')
                .replace(/&amp;#34;/g, '"')
                .replace(/&#x22;/gi, '"')
                .replace(/&amp;#x22;/gi, '"');
            try {
                return JSON.parse(decoded);
            } catch {
                return null;
            }
        }
    }

    /** 获取控件的树数据：优先取已加载的 options，静态 Items 则从 data-static-items 回读 */
    getControlTreeData(target, fallback = []) {
        let data = fallback;

        if ((!Array.isArray(data) || data.length === 0) && target) {
            try {
                const normalizedTarget = (target || '').toString().trim();
                const nodes = document.querySelectorAll('[data-tree-model][data-static-items]');
                for (const el of nodes) {
                    const controlId = (el.getAttribute('data-ele-control-id') || '').toString().trim();
                    if (controlId !== normalizedTarget) continue;
                    const parsed = this.parseJsonAttribute(el.getAttribute('data-static-items'));
                    if (Array.isArray(parsed)) data = parsed;
                    break;
                }
            } catch {
                // ignore static tree fallback lookup failures
            }
        }

        return this.normalizeIds(Array.isArray(data) ? data : []);
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
    messageMethods,
    pickerMethods
);
