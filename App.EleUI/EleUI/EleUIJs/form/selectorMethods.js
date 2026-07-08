export function initSelectorState(form, vueApi) {
    const { ref } = vueApi;
    form.selectorVisible = ref(false);
    form.selectorUrl = ref('');
    form.selectorTitle = ref('');
    form.selectorTargetId = ref('');
    form.selectorTargetText = ref('');
    form.selectorMulti = ref(false);
}

export const selectorMethods = {
    normalizeSelectorField(field) {
        if (!field) return '';
        return field.charAt(0).toLowerCase() + field.slice(1);
    },

    resolveDrawerUrlBase(urlBase) {
        if (!urlBase) return '';
        try {
            const absolute = new URL(urlBase, window.location.href);
            return `${absolute.pathname}${absolute.search}${absolute.hash}`;
        } catch (e) {
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

    cleanupSelectorStorage(maxKeep = 120) {
        if (typeof window === 'undefined') return;
        const storages = [window.localStorage, window.sessionStorage].filter(Boolean);
        const shouldManage = (k) => typeof k === 'string' && (k.startsWith('sk_') || k.startsWith('ele_selector_'));

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

    writeSelectorPayloadToStorage(value) {
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
                this.cleanupSelectorStorage(80);
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

    // Append reference gps for geometry editor
    appendGeometryReferenceGps(url, propId) {
        if (!url) return url;
        const keyId = this.normalizeSelectorField(propId);
        if (keyId !== 'geoJson') return url;

        const rawGps = this.form?.value
            ? (this.form.value.gps ?? this.form.value.Gps ?? '')
            : '';
        const gps = rawGps == null ? '' : String(rawGps).trim();
        if (!gps) return url;
        return this.appendQuery(url, 'gps', gps);
    },

    buildSelectorUrl(urlBase, propId, keyMode) {
        let url = this.resolveDrawerUrlBase(urlBase);
        const modeRaw = (keyMode || 'Url').toString().trim().toLowerCase();
        const mode = modeRaw === 'url/storage' ? 'auto' : modeRaw;

        const keyId = this.normalizeSelectorField(propId);
        const rawValue = this.form?.value ? (this.form.value[keyId] ?? '') : '';
        const value = rawValue == null ? '' : String(rawValue);

        const useStorage = mode === 'storage' || (mode === 'auto' && value.length > 1200);
        if (useStorage) {
            const k = this.writeSelectorPayloadToStorage(value);
            if (k) {
                url = this.appendQuery(url, 'dk', k);
                return this.appendGeometryReferenceGps(url, keyId);
            }
        }

        url = this.appendQuery(url, 'selectorValue', value);
        url = this.appendQuery(url, keyId, value);
        url = this.appendQuery(url, 'data', value);
        url = this.appendQuery(url, 'geojson', value);

        // GIS: region editor also needs current attachment/image path.
        if (keyId === 'region') {
            const attValue = this.form?.value ? (this.form.value.att ?? '') : '';
            url = this.appendQuery(url, 'att', attValue == null ? '' : String(attValue));
        }
        return this.appendGeometryReferenceGps(url, keyId);
    },

    openSelector(propId, propText, url, multi, title, keyMode = 'Url') {
        this.selectorTargetId.value = propId;
        this.selectorTargetText.value = propText;
        this.selectorUrl.value = this.buildSelectorUrl(url, propId, keyMode);
        this.selectorMulti.value = multi;
        this.selectorTitle.value = title || '选择';
        this.selectorVisible.value = true;

        EleManager.openDrawer({
            title: this.selectorTitle.value,
            url: this.selectorUrl.value,
            direction: 'rtl',
            resizable: true,
            closeOnClickModal: false,
            destroyOnClose: true,
            closeHandler: (payload) => {
                if (payload && payload.data && payload.data.type === 'ElePicker') {
                    this.handleSelectorMessage({ data: payload.data });
                    return;
                }
                this.selectorVisible.value = false;
            }
        });
    },

    handleSelectorMessage(event) {
        if (!event.data) return;

        const msgType = event.data.type;
        if (msgType !== 'ElePicker' && msgType !== 'user-selected') return;
        const data = event.data.data || event.data;

        let rows = [];
        if (Array.isArray(data)) rows = data;
        else if (data.rows) rows = data.rows;
        else if (data.id) rows = [data];
        else if (data.data && data.data.id) rows = [data.data];

        if (!this.selectorVisible.value) return;
        const keyId = this.normalizeSelectorField(this.selectorTargetId.value);
        let keyText = this.normalizeSelectorField(this.selectorTargetText.value);
        if (!keyText) keyText = keyId;

        if (this.selectorMulti.value) {
            this.form.value[keyId] = rows.map(r => r.id).join(',');
            this.form.value[keyText] = rows.map(r => r.name).join(',');
        } else if (rows.length > 0) {
            this.form.value[keyId] = rows[0].id;
            this.form.value[keyText] = rows[0].name;

            // Optional extra field synchronization for custom selectors.
            if (Object.prototype.hasOwnProperty.call(rows[0], 'att') && this.form?.value && Object.prototype.hasOwnProperty.call(this.form.value, 'att')) {
                this.form.value.att = rows[0].att;
            }
        }

        this.selectorVisible.value = false;
        EleManager.closeDrawer();
    },

    clearSelector(propId, propText) {
        const keyId = this.normalizeSelectorField(propId);
        let keyText = this.normalizeSelectorField(propText);
        if (!keyText) keyText = keyId;
        this.form.value[keyId] = null;
        this.form.value[keyText] = null;
    }
};
