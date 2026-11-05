export const messageMethods = {
    messageHandler(e) {
        if (!e) return;

        const payload = e.data;
        if (payload && typeof payload === 'object' && payload.__eleSetControlValue === true) {
            const target = (payload.target || '').toString().trim();
            const lower = target.toLowerCase();
            const field = lower.startsWith('field:')
                ? target.substring('field:'.length)
                : target;
            if (field && this.filters && this.filters.value) {
                this.filters.value[field] = payload.value;
            }
            return;
        }

        if (payload && typeof payload === 'object' && payload.__eleRefreshData === true) {
            this.loadData();
            return;
        }

        if (payload && typeof payload === 'object' && payload.__elePageClose === true) {
            EleManager.closeDrawer();
            return;
        }
    }
};
