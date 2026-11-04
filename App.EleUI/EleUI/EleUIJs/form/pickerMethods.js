export function initPickerState(form, vueApi) {
    const { ref } = vueApi;
    form.pickerVisible = ref(false);
    form.pickerUrl = ref('');
    form.pickerTitle = ref('');
    form.pickerTargetId = ref('');
    form.pickerTargetText = ref('');
    form.pickerMulti = ref(false);
    form.pickerDataKey = ref('');
    form.pickerKeyMode = ref('url');
}

function _formHolder(ctx) {
    if (ctx?.filters?.value) return ctx.filters;
    return ctx?.form ?? null;
}

export const pickerMethods = {
    normalizePickerField(field) {
        if (!field) return '';
        return field.charAt(0).toLowerCase() + field.slice(1);
    },

    resolveDrawerUrlBase(urlBase) {
        if (!urlBase) return '';
        try {
            const absolute = new URL(urlBase, window.location.href);
            return `${absolute.pathname}${absolute.search}${absolute.hash}`;
        } catch {
            return urlBase;
        }
    },

    appendQuery(url, key, value) {
        const joinChar = url.includes('?') ? '&' : '?';
        return `${url}${joinChar}${encodeURIComponent(key)}=${encodeURIComponent(value ?? '')}`;
    },

    createStorageToken() {
        return `dk_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 8)}`;
    },

    cleanupPickerStorage(maxKeep = 120) {
        if (typeof window === 'undefined') return;
        const storages = [window.localStorage, window.sessionStorage].filter(Boolean);
        const shouldManage = (k) => typeof k === 'string' && (
            k.startsWith('dk_')
            || k.startsWith('ele_picker_')
        );

        storages.forEach((storage) => {
            try {
                const keys = [];
                for (let i = 0; i < storage.length; i += 1) {
                    const k = storage.key(i);
                    if (shouldManage(k)) keys.push(k);
                }
                if (keys.length <= maxKeep) return;

                const removeCount = keys.length - maxKeep;
                for (let i = 0; i < removeCount; i += 1) {
                    storage.removeItem(keys[i]);
                }
            } catch {
                // ignore cleanup failures
            }
        });
    },

    writePickerPayloadToStorage(value) {
        if (typeof window === 'undefined') return null;
        const token = this.createStorageToken();
        return this.savePickerPayloadToStorage(token, value) ? token : null;
    },

    savePickerPayloadToStorage(key, value) {
        if (typeof window === 'undefined' || !key) return false;
        const text = value == null ? '' : String(value);
        const storages = [window.localStorage, window.sessionStorage].filter(Boolean);

        for (let i = 0; i < storages.length; i += 1) {
            const storage = storages[i];
            try {
                storage.setItem(key, text);
                return true;
            } catch {
                this.cleanupPickerStorage(80);
                try {
                    storage.setItem(key, text);
                    return true;
                } catch {
                    // try next storage
                }
            }
        }
        return false;
    },

    peekPickerPayloadFromStorage(key) {
        if (typeof window === 'undefined' || !key) return '';
        const storages = [window.localStorage, window.sessionStorage].filter(Boolean);
        for (let i = 0; i < storages.length; i += 1) {
            const storage = storages[i];
            try {
                const value = storage.getItem(key);
                if (value !== null && value !== undefined && String(value).trim()) {
                    return String(value);
                }
            } catch {
                // ignore storage errors
            }
        }
        return '';
    },

    /// 读取大数据模式的载荷，读取后立即删除对应存储键（data key 模式只消费一次，避免本地存储膨胀）
    consumePickerPayloadFromStorage(key) {
        if (typeof window === 'undefined' || !key) return '';
        const storages = [window.localStorage, window.sessionStorage].filter(Boolean);
        for (let i = 0; i < storages.length; i += 1) {
            const storage = storages[i];
            try {
                const value = storage.getItem(key);
                if (value !== null && value !== undefined) {
                    storage.removeItem(key);
                    if (String(value).trim()) return String(value);
                }
            } catch {
                // ignore storage errors
            }
        }
        return '';
    },

    discardPickerPayloadFromStorage(key) {
        if (typeof window === 'undefined' || !key) return;
        const storages = [window.localStorage, window.sessionStorage].filter(Boolean);
        for (let i = 0; i < storages.length; i += 1) {
            try {
                storages[i].removeItem(key);
            } catch {
                // ignore storage errors
            }
        }
    },

    resolvePickerCloseData(payload) {
        if (!payload || typeof payload !== 'object') return null;
        if (payload.type === 'ElePicker' || payload.type === 'user-selected') return payload;

        const nested = payload.data;
        if (!nested || typeof nested !== 'object') return null;
        if (nested.type === 'ElePicker' || nested.type === 'user-selected') return nested;
        if (nested.data && typeof nested.data === 'object') {
            const inner = nested.data;
            if (inner.type === 'ElePicker' || inner.type === 'user-selected') return inner;
        }
        return null;
    },

    appendGeometryReferenceGps(url, propId) {
        if (!url) return url;
        const keyId = this.normalizePickerField(propId);
        if (keyId !== 'geoData' && keyId !== 'geoJson') return url;

        const holder = _formHolder(this);
        const rawGps = holder?.value ? (holder.value.gps ?? holder.value.Gps ?? '') : '';
        const gps = rawGps == null ? '' : String(rawGps).trim();
        if (!gps) return url;
        return this.appendQuery(url, 'gps', gps);
    },

    buildPickerUrl(urlBase, propId, keyMode) {
        let url = this.resolveDrawerUrlBase(urlBase);
        const mode = (keyMode || 'Url').toString().trim().toLowerCase();

        const keyId = this.normalizePickerField(propId);
        const holder = _formHolder(this);
        const rawValue = holder?.value ? (holder.value[keyId] ?? '') : '';
        const value = rawValue == null ? '' : String(rawValue);
        const useStorage = mode === 'storage' || (mode === 'auto' && value.length > 1200);
        if (useStorage) {
            const k = this.writePickerPayloadToStorage(value);
            if (k) {
                url = this.appendQuery(url, 'dk', k);
                return {
                    url: this.appendGeometryReferenceGps(url, keyId),
                    dataKey: k,
                    mode: 'storage'
                };
            }
        }

        // 小数据模式：通过 URL 参数 dv（data value）传递
        url = this.appendQuery(url, 'dv', value);

        if (keyId === 'region') {
            const holder = _formHolder(this);
            const attValue = holder?.value ? (holder.value.att ?? '') : '';
            url = this.appendQuery(url, 'att', attValue == null ? '' : String(attValue));
        }
        return {
            url: this.appendGeometryReferenceGps(url, keyId),
            dataKey: '',
            mode: 'url'
        };
    },

    openPicker(propId, propText, url, multi, title, keyMode = 'Url') {
        const pickerRequest = this.buildPickerUrl(url, propId, keyMode);
        this.pickerTargetId.value = propId;
        this.pickerTargetText.value = propText;
        this.pickerUrl.value = pickerRequest.url;
        this.pickerMulti.value = multi;
        this.pickerTitle.value = title || '选择';
        this.pickerDataKey.value = pickerRequest.dataKey || '';
        this.pickerKeyMode.value = pickerRequest.mode || 'url';
        this.pickerVisible.value = true;

        EleManager.openDrawer({
            title: this.pickerTitle.value,
            url: this.pickerUrl.value,
            direction: 'rtl',
            resizable: true,
            closeOnClickModal: false,
            destroyOnClose: true,
            closeHandler: (payload) => {
                const pickerData = this.resolvePickerCloseData(payload);
                if (pickerData) {
                    this.handlePickerMessage({ data: pickerData }, { force: true });
                    return;
                }
                if (this.pickerKeyMode.value === 'storage' && this.pickerDataKey.value) {
                    this.discardPickerPayloadFromStorage(String(this.pickerDataKey.value));
                }
                this.pickerDataKey.value = '';
                this.pickerKeyMode.value = 'url';
                this.pickerVisible.value = false;
            }
        });
    },

    handlePickerMessage(event, options = {}) {
        if (!event.data) return;

        const force = options.force === true;
        const msgType = event.data.type;
        if (msgType !== 'ElePicker' && msgType !== 'user-selected') return;
        const data = event.data.data || event.data;

        let rows = [];
        if (Array.isArray(data)) rows = data;
        else if (data.rows) rows = data.rows;
        else if (data.id) rows = [data];
        else if (data.data && data.data.id) rows = [data.data];

        if (!force && !this.pickerVisible.value) return;
        const keyId = this.normalizePickerField(this.pickerTargetId.value);
        let keyText = this.normalizePickerField(this.pickerTargetText.value);
        if (!keyText) keyText = keyId;

        const holder = _formHolder(this);

        // 兼容 User 选择器的 DisplayName 格式：优先 displayName / RealName(Mobile)，兜底 name
        function makeDisplayName(r) {
            if (!r || typeof r !== 'object') return '';
            if (r.displayName && String(r.displayName).trim()) return String(r.displayName).trim();
            const real = r.realName ?? r.RealName ?? r.name ?? r.Name ?? '';
            const mob  = r.mobile ?? r.Mobile ?? '';
            if (real && mob) return `${real}(${mob})`;
            if (real) return String(real);
            if (mob) return String(mob);
            const id = r.id !== undefined ? r.id : r.Id;
            return id !== undefined ? String(id) : '';
        }

        if (this.pickerMulti.value) {
            const ids = rows.map(r => {
                const rowKey = r?.dataKey || r?.dk || '';
                if (!rowKey) return r?.id;
                return this.consumePickerPayloadFromStorage(String(rowKey)) || r?.id;
            });
            holder.value[keyId] = ids.join(',');
            holder.value[keyText] = rows.map(r => makeDisplayName(r)).join(',');
        } else if (rows.length > 0) {
            const first = rows[0] || {};
            const dataKey = first.dataKey || first.dk || this.pickerDataKey.value || '';
            // data key 模式：读取本地存储中的完整载荷后立即删除，避免本地存储膨胀
            const storedValue = dataKey ? this.consumePickerPayloadFromStorage(String(dataKey)) : '';
            const displayText = makeDisplayName(first) || first.name || first.id;
            const nextValue = storedValue || first.id;
            const nextText = keyText === keyId
                ? (storedValue || displayText)
                : (displayText || storedValue || first.id);
            holder.value[keyId] = nextValue;
            holder.value[keyText] = nextText;

            if (Object.prototype.hasOwnProperty.call(first, 'att') && holder?.value && Object.prototype.hasOwnProperty.call(holder.value, 'att')) {
                holder.value.att = first.att;
            }
        }

        this.pickerDataKey.value = '';
        this.pickerKeyMode.value = 'url';
        this.pickerVisible.value = false;
        EleManager.closeDrawer();
    },

    clearPicker(propId, propText) {
        const keyId = this.normalizePickerField(propId);
        let keyText = this.normalizePickerField(propText);
        if (!keyText) keyText = keyId;
        const holder = _formHolder(this);
        holder.value[keyId] = null;
        holder.value[keyText] = null;
    }
};
