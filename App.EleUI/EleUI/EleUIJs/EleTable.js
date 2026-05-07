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
