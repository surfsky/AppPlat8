export const messageMethods = {
    messageHandler(e) {
        if (!e) return;

        const payload = e.data;
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
