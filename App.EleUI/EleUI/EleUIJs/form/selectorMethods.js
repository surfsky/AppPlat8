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
    resolveDrawerUrlBase(urlBase) {
        if (!urlBase) return '';
        try {
            const absolute = new URL(urlBase, window.location.href);
            return `${absolute.pathname}${absolute.search}${absolute.hash}`;
        } catch (e) {
            return urlBase;
        }
    },

    openSelector(propId, propText, url, multi, title) {
        this.selectorTargetId.value = propId;
        this.selectorTargetText.value = propText;
        this.selectorUrl.value = this.resolveDrawerUrlBase(url);
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
        const keyId = this.selectorTargetId.value.charAt(0).toLowerCase() + this.selectorTargetId.value.slice(1);
        const keyText = this.selectorTargetText.value.charAt(0).toLowerCase() + this.selectorTargetText.value.slice(1);

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
        const keyId = propId.charAt(0).toLowerCase() + propId.slice(1);
        const keyText = propText.charAt(0).toLowerCase() + propText.slice(1);
        this.form.value[keyId] = null;
        this.form.value[keyText] = null;
    }
};
