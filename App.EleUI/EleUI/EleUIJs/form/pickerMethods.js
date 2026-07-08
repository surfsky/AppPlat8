export function initPickerState(form, vueApi) {
    const { ref } = vueApi;
    form.pickerVisible = ref(false);
    form.pickerUrl = ref('');
    form.pickerTitle = ref('');
    form.pickerTargetId = ref('');
    form.pickerTargetText = ref('');
    form.pickerMulti = ref(false);
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
        return `sk_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 8)}`;
    },

    cleanupPickerStorage(maxKeep = 120) {
        if (typeof window === 'undefined') return;
        const storages = [window.localStorage, window.sessionStorage].filter(Boolean);
        const shouldManage = (k) => typeof k === 'string' && (
            k.startsWith('sk_')
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

                keys.sort((a, b) => {
                    const ta = storage.getItem(`${a}__t`) || '0';
                    const tb = storage.getItem(`${b}__t`) || '0';
                    return Number(ta) - Number(tb);
                });

                const removeCount = keys.length - maxKeep;
                for (let i = 0; i < removeCount; i += 1) {
                    const k = keys[i];
                    storage.removeItem(k);
                    storage.removeItem(`${k}__t`);
                }
            } catch {
                // ignore cleanup failures
            }
        });
    },

    writePickerPayloadToStorage(value) {
        if (typeof window === 'undefined') return null;
        const token = this.createStorageToken();
        const text = value == null ? '' : String(value);
        const storages = [window.localStorage, window.sessionStorage].filter(Boolean);

        for (let i = 0; i < storages.length; i += 1) {
            const storage = storages[i];
            try {
                storage.setItem(token, text);
                storage.setItem(`${token}__t`, String(Date.now()));
                return token;
            } catch {
                this.cleanupPickerStorage(80);
                try {
                    storage.setItem(token, text);
                    storage.setItem(`${token}__t`, String(Date.now()));
                    return token;
                } catch {
                    // try next storage
                }
            }
        }
        return null;
    },

    readPickerPayloadFromStorage(key) {
        if (typeof window === 'undefined' || !key) return '';
        const storages = [window.localStorage, window.sessionStorage].filter(Boolean);
        for (let i = 0; i < storages.length; i += 1) {
            const storage = storages[i];
            try {
                const value = storage.getItem(key) || '';
                if (String(value).trim()) return String(value);
            } catch {
                // ignore storage errors
            }
        }
        return '';
    },

    appendGeometryReferenceGps(url, propId) {
        if (!url) return url;
        const keyId = this.normalizePickerField(propId);
        if (keyId !== 'geoJson') return url;

        const rawGps = this.form?.value
            ? (this.form.value.gps ?? this.form.value.Gps ?? '')
            : '';
        const gps = rawGps == null ? '' : String(rawGps).trim();
        if (!gps) return url;
        return this.appendQuery(url, 'gps', gps);
    },

    buildPickerUrl(urlBase, propId, keyMode) {
        let url = this.resolveDrawerUrlBase(urlBase);
        const modeRaw = (keyMode || 'Url').toString().trim().toLowerCase();
        const mode = modeRaw === 'url/storage' ? 'auto' : modeRaw;

        const keyId = this.normalizePickerField(propId);
        const rawValue = this.form?.value ? (this.form.value[keyId] ?? '') : '';
        const value = rawValue == null ? '' : String(rawValue);

        const useStorage = mode === 'storage' || (mode === 'auto' && value.length > 1200);
        if (useStorage) {
            const k = this.writePickerPayloadToStorage(value);
            if (k) {
                url = this.appendQuery(url, 'dk', k);
                return this.appendGeometryReferenceGps(url, keyId);
            }
        }

        url = this.appendQuery(url, 'pickerValue', value);
        url = this.appendQuery(url, keyId, value);
        url = this.appendQuery(url, 'data', value);
        url = this.appendQuery(url, 'geojson', value);

        if (keyId === 'region') {
            const attValue = this.form?.value ? (this.form.value.att ?? '') : '';
            url = this.appendQuery(url, 'att', attValue == null ? '' : String(attValue));
        }
        return this.appendGeometryReferenceGps(url, keyId);
    },

    openPicker(propId, propText, url, multi, title, keyMode = 'Url') {
        this.pickerTargetId.value = propId;
        this.pickerTargetText.value = propText;
        this.pickerUrl.value = this.buildPickerUrl(url, propId, keyMode);
        this.pickerMulti.value = multi;
        this.pickerTitle.value = title || '选择';
        this.pickerVisible.value = true;

        EleManager.openDrawer({
            title: this.pickerTitle.value,
            url: this.pickerUrl.value,
            direction: 'rtl',
            resizable: true,
            closeOnClickModal: false,
            destroyOnClose: true,
            closeHandler: (payload) => {
                if (payload && payload.data && payload.data.type === 'ElePicker') {
                    this.handlePickerMessage({ data: payload.data });
                    return;
                }
                this.pickerVisible.value = false;
            }
        });
    },

    handlePickerMessage(event) {
        if (!event.data) return;

        const msgType = event.data.type;
        if (msgType !== 'ElePicker' && msgType !== 'user-selected') return;
        const data = event.data.data || event.data;

        let rows = [];
        if (Array.isArray(data)) rows = data;
        else if (data.rows) rows = data.rows;
        else if (data.id) rows = [data];
        else if (data.data && data.data.id) rows = [data.data];

        if (!this.pickerVisible.value) return;
        const keyId = this.normalizePickerField(this.pickerTargetId.value);
        let keyText = this.normalizePickerField(this.pickerTargetText.value);
        if (!keyText) keyText = keyId;

        if (this.pickerMulti.value) {
            this.form.value[keyId] = rows.map(r => r.id).join(',');
            this.form.value[keyText] = rows.map(r => r.name).join(',');
        } else if (rows.length > 0) {
            const first = rows[0] || {};
            const dataKey = first.dataKey || first.dk || '';
            const storedValue = dataKey ? this.readPickerPayloadFromStorage(String(dataKey)) : '';
            const nextValue = storedValue || first.id;
            const nextText = storedValue || first.name || first.id;
            this.form.value[keyId] = nextValue;
            this.form.value[keyText] = nextText;

            if (Object.prototype.hasOwnProperty.call(first, 'att') && this.form?.value && Object.prototype.hasOwnProperty.call(this.form.value, 'att')) {
                this.form.value.att = first.att;
            }
        }

        this.pickerVisible.value = false;
        EleManager.closeDrawer();
    },

    clearPicker(propId, propText) {
        const keyId = this.normalizePickerField(propId);
        let keyText = this.normalizePickerField(propText);
        if (!keyText) keyText = keyId;
        this.form.value[keyId] = null;
        this.form.value[keyText] = null;
    }
};
