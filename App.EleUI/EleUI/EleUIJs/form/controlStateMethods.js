export function initControlState(form, vueApi) {
    const { ref, reactive } = vueApi;
    form.controlStateVersions = reactive({});
    form.options = ref({});
}

export const controlStateMethods = {
    parseJsonAttribute(raw) {
        if (!raw || typeof raw !== 'string') return null;

        try {
            return JSON.parse(raw);
        } catch {
            // Compatibility for historical double-escaped values like &amp;quot; and &quot;
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
    },

    getObjectValueIgnoreCase(obj, keys = []) {
        if (!obj || typeof obj !== 'object' || !Array.isArray(keys) || keys.length === 0) {
            return undefined;
        }

        const dict = {};
        for (const k of Object.keys(obj)) {
            dict[k.toLowerCase()] = obj[k];
        }

        for (const key of keys) {
            const hit = dict[(key || '').toLowerCase()];
            if (hit !== undefined) return hit;
        }

        return undefined;
    },

    messageHandler(e) {
        if (e?.data?.__eleControlPatched) {
            const targets = Array.isArray(e.data.targets) ? e.data.targets : [];
            for (const raw of targets) {
                const key = this.normalizeControlTarget(raw);
                if (!key) continue;
                this.controlStateVersions[key] = (this.controlStateVersions[key] || 0) + 1;
            }

            this.sanitizeAllDynamicSelectValues();
            return;
        }

        if (e?.data?.__eleSetControlValue) {
            const target = (e.data.target || '').toString();
            const value = e.data.value;
            this.setControlValue(target, value);
            return;
        }

        if (e && e.data === 'RequestSave') {
            this.save();
        }
    },

    normalizeControlTarget(target) {
        const raw = (target || '').toString().trim();
        if (!raw) return '';
        const lower = raw.toLowerCase();
        if (lower.startsWith('field:') || lower.startsWith('controlid:')) {
            return raw;
        }
        return `field:${raw}`;
    },

    getControlPatch(target) {
        const key = this.normalizeControlTarget(target);
        if (!key || !EleManager || typeof EleManager.getControlState !== 'function') {
            return null;
        }

        void this.controlStateVersions[key];
        return EleManager.getControlState(key);
    },

    resolveControlDisabled(target, baseDisabled = false) {
        const patch = this.getControlPatch(target);
        if (patch && typeof patch.enabled === 'boolean') {
            return !patch.enabled;
        }
        return !!baseDisabled;
    },

    resolveControlVisible(target, baseVisible = true) {
        const patch = this.getControlPatch(target);
        if (patch && typeof patch.visible === 'boolean') {
            return !!patch.visible;
        }
        return !!baseVisible;
    },

    normalizeSelectOptions(options) {
        if (!Array.isArray(options)) return [];
        return options.map(item => {
            if (item && typeof item === 'object') {
                const hasLabel = this.getObjectValueIgnoreCase(item, ['label']) !== undefined;
                const hasValue = this.getObjectValueIgnoreCase(item, ['value']) !== undefined;
                if (hasLabel && hasValue) {
                    return {
                        label: this.getObjectValueIgnoreCase(item, ['label']),
                        value: this.getObjectValueIgnoreCase(item, ['value'])
                    };
                }

                const label = this.getObjectValueIgnoreCase(item, ['label', 'text', 'name', 'title', 'value']) ?? '';
                const value = this.getObjectValueIgnoreCase(item, ['value', 'id', 'key', 'code']) ?? label;
                return { label, value };
            }

            return { label: item, value: item };
        });
    },

    getControlOptions(target, fallback = []) {
        const patch = this.getControlPatch(target);
        const data = patch && Object.prototype.hasOwnProperty.call(patch, 'data') ? patch.data : fallback;
        return this.normalizeSelectOptions(data);
    },

    tryMatchSelectOptionValue(currentValue, options) {
        if (!Array.isArray(options) || options.length === 0) return undefined;

        // 1) strict match first
        const strict = options.find(opt => opt && Object.prototype.hasOwnProperty.call(opt, 'value') && opt.value === currentValue);
        if (strict) return strict.value;

        // 2) loose match for number/string (and other primitive textual equivalents)
        if (currentValue === null || currentValue === undefined) return undefined;
        const currentText = String(currentValue);
        const loose = options.find(opt => opt && Object.prototype.hasOwnProperty.call(opt, 'value') && opt.value !== null && opt.value !== undefined && String(opt.value) === currentText);
        return loose ? loose.value : undefined;
    },

    sanitizeSelectModelValue(modelKey, optionList) {
        if (!modelKey || !this.form?.value) return;

        const current = this.form.value[modelKey];
        if (current === null || current === undefined || current === '') return;

        const options = this.normalizeSelectOptions(optionList);
        if (options.length === 0) return;

        if (Array.isArray(current)) {
            const normalized = current.map(v => {
                const matched = this.tryMatchSelectOptionValue(v, options);
                return matched !== undefined ? matched : v;
            });
            this.form.value[modelKey] = normalized;
            return;
        }

        const matched = this.tryMatchSelectOptionValue(current, options);
        if (matched !== undefined) {
            this.form.value[modelKey] = matched;
        }
    },

    sanitizeAllStaticSelectValues() {
        const nodes = document.querySelectorAll('[data-select-model][data-static-options]');
        nodes.forEach(el => {
            const modelKey = el.getAttribute('data-select-model');
            const json = el.getAttribute('data-static-options');
            if (!modelKey || !json) return;

            try {
                const options = this.parseJsonAttribute(json);
                if (!Array.isArray(options)) return;
                this.sanitizeSelectModelValue(modelKey, options);
            } catch (e) {
                console.warn('parse static select options failed', e);
            }
        });
    },

    sanitizeAllDynamicSelectValues() {
        const nodes = document.querySelectorAll('[data-select-model]');
        nodes.forEach(el => {
            const modelKey = el.getAttribute('data-select-model');
            if (!modelKey) return;

            const target = el.getAttribute('data-select-target') || `field:${modelKey}`;

            let fallback = [];
            const json = el.getAttribute('data-static-options');
            if (json) {
                try {
                    const parsed = this.parseJsonAttribute(json);
                    fallback = Array.isArray(parsed) ? parsed : [];
                } catch {
                    fallback = [];
                }
            }

            const options = this.getControlOptions(target, fallback);
            this.sanitizeSelectModelValue(modelKey, options);
        });
    },

    getControlTreeData(target, fallback = []) {
        const patch = this.getControlPatch(target);
        const data = patch && Object.prototype.hasOwnProperty.call(patch, 'data') ? patch.data : fallback;
        const list = Array.isArray(data) ? data : [];
        return this.normalizeIds(list);
    },

    setControlValue(target, value) {
        const normalized = this.normalizeControlTarget(target);
        if (!normalized) return;

        if (normalized.toLowerCase().startsWith('field:')) {
            const field = normalized.substring('field:'.length);
            if (!field) return;
            this.form.value[field] = value;
        }
    },

    normalizeIds(obj) {
        if (Array.isArray(obj)) {
            return obj.map(o => this.normalizeIds(o));
        }
        if (obj && typeof obj === 'object') {
            const out = {};
            for (const k of Object.keys(obj)) {
                const v = obj[k];
                if (k.toLowerCase().endsWith('id')) {
                    out[k] = v !== null && v !== undefined ? v.toString() : v;
                } else {
                    out[k] = this.normalizeIds(v);
                }
            }
            return out;
        }
        return obj;
    },

    hasTreeValue(nodes, targetValue, idField = 'id', childrenField = 'children') {
        if (!Array.isArray(nodes) || nodes.length === 0 || targetValue === null || targetValue === undefined || targetValue === '') {
            return false;
        }

        const target = targetValue.toString();
        const walk = (list) => {
            for (const node of (list || [])) {
                if (!node || typeof node !== 'object') continue;
                const nodeValue = node[idField];
                if (nodeValue !== null && nodeValue !== undefined && nodeValue.toString() === target) {
                    return true;
                }
                const children = node[childrenField];
                if (Array.isArray(children) && children.length > 0 && walk(children)) {
                    return true;
                }
            }
            return false;
        };

        return walk(nodes);
    },

    sanitizeTreeModelValue(modelKey, treeData, idField = 'id', childrenField = 'children') {
        if (!modelKey || !this.form.value) return;
        const current = this.form.value[modelKey];
        if (current === null || current === undefined || current === '') return;

        if (Array.isArray(current)) {
            const normalized = current
                .map(v => (v === null || v === undefined ? '' : v.toString()))
                .filter(v => !!v)
                .filter(v => this.hasTreeValue(treeData, v, idField, childrenField));
            this.form.value[modelKey] = normalized;
            return;
        }

        if (!this.hasTreeValue(treeData, current, idField, childrenField)) {
            this.form.value[modelKey] = null;
            return;
        }

        this.form.value[modelKey] = current.toString();
    },

    sanitizeAllStaticTreeSelectValues() {
        const nodes = document.querySelectorAll('[data-tree-model][data-static-items]');
        nodes.forEach(el => {
            const modelKey = el.getAttribute('data-tree-model');
            const idField = el.getAttribute('data-tree-id-field') || 'id';
            const childrenField = el.getAttribute('data-tree-children-field') || 'children';
            const json = el.getAttribute('data-static-items');
            if (!modelKey || !json) return;
            try {
                const treeData = JSON.parse(json);
                this.sanitizeTreeModelValue(modelKey, treeData, idField, childrenField);
            } catch (e) {
                console.warn('parse static tree items failed', e);
            }
        });
    },

    sanitizeAllRemoteTreeSelectValues() {
        const nodes = document.querySelectorAll('[data-tree-model][data-source]');
        nodes.forEach(el => {
            const modelKey = el.getAttribute('data-tree-model') || el.getAttribute('data-key');
            const key = el.getAttribute('data-key');
            const idField = el.getAttribute('data-tree-id-field') || 'id';
            const childrenField = el.getAttribute('data-tree-children-field') || 'children';
            if (!modelKey || !key) return;

            const treeData = this.options.value[key];
            if (!Array.isArray(treeData) || treeData.length === 0) return;

            this.sanitizeTreeModelValue(modelKey, treeData, idField, childrenField);
        });
    },

    async fetchOptions(key, url, idField = 'id', childrenField = 'children') {
        if (this.options.value[key]) return;
        try {
            const res = await axios.get(url);
            if (res.data.code === 0 || res.data.code === '0') {
                let data = res.data.data;
                data = this.normalizeIds(data);
                this.options.value[key] = data;
                this.sanitizeTreeModelValue(key, data, idField, childrenField);
            }
        } catch (e) {
            console.error('Fetch options failed:', url, e);
        }
    }
};
