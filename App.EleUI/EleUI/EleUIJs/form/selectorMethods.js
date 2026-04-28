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

    buildSelectorUrl(urlBase, propId, keyMode) {
        let url = this.resolveDrawerUrlBase(urlBase);
        const modeRaw = (keyMode || 'Url').toString().trim().toLowerCase();
        const mode = modeRaw === 'url/storage' ? 'auto' : modeRaw;

        const keyId = this.normalizeSelectorField(propId);
        const rawValue = this.form?.value ? (this.form.value[keyId] ?? '') : '';
        const value = rawValue == null ? '' : String(rawValue);

        const useStorage = mode === 'storage' || (mode === 'auto' && value.length > 1200);
        if (useStorage && typeof window !== 'undefined' && window.localStorage) {
            const k = `ele_selector_${keyId}_${Date.now()}_${Math.random().toString(36).slice(2)}`;
            window.localStorage.setItem(k, value);
            url = this.appendQuery(url, 'selectorKey', k);
            url = this.appendQuery(url, 'geojsonKey', k);
            return url;
        }

        url = this.appendQuery(url, 'selectorValue', value);
        url = this.appendQuery(url, keyId, value);
        url = this.appendQuery(url, 'geojson', value);
        return url;
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
                if (payload && payload.data && payload.data.type === 'EleSelector') {
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
        if (msgType !== 'EleSelector' && msgType !== 'user-selected') return;
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
