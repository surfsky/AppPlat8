export function initPaginationState(table, vueApi, options = {}) {
    const { ref } = vueApi;

    table.items = ref([]);
    table.total = ref(0);
    table.pageIndex = ref(0);
    table.filters = ref({});
    table.selectedIds = ref([]);
    table.selectedRows = ref([]);
    table.currentRow = ref(null);
    table.pageSize = ref(options.pageSize || 20);
    table.sortField = ref(options.defaultSortField || 'Id');
    table.sortDirection = ref(options.defaultSortDirection || 'ASC');
    table.filtersDrawerVisible = ref(false);

    table.dataHandler = options.dataHandler || '?handler=Data';
    table.deleteHandler = options.deleteHandler || '?handler=Delete';
    table.exportHandler = options.exportHandler || '?handler=Export';

    table.options = ref({});

    table.autoRefreshEnabled = ref(false);
    table.autoRefreshInterval = ref(30);
    table._autoRefreshTimer = null;
    table._autoRefreshTriggle = 'Data';
}

function serializeParams(params) {
    const list = [];
    const obj = params || {};
    for (const key of Object.keys(obj)) {
        const value = obj[key];
        if (value === undefined || value === null || value === '') continue;
        const k = encodeURIComponent(key);
        if (Array.isArray(value)) {
            for (const item of value) {
                if (item === undefined || item === null || item === '') continue;
                list.push(`${k}=${encodeURIComponent(item)}`);
            }
            continue;
        }
        list.push(`${k}=${encodeURIComponent(value)}`);
    }
    return list.join('&');
}

export const paginationMethods = {
    async loadData(options = {}) {
        const silent = !!options.silent;
        const dataHandler = options.handler || this.dataHandler;
        try {
            const res = await axios.get(dataHandler, {
                params: {
                    pageIndex: this.pageIndex.value,
                    pageSize: this.pageSize.value,
                    sortField: this.sortField.value,
                    sortDirection: this.sortDirection.value,
                    ...this.config.extraParams,
                    ...this.filters.value
                },
                paramsSerializer: serializeParams
            });

            if (res.data.code === 0 || res.data.code === '0') {
                this.items.value = res.data.data?.items || res.data.data || [];

                const pager = res.data.pager || res.data.extra || null;
                if (pager && pager.total !== undefined) {
                    this.total.value = pager.total;
                    if (pager.pageIndex !== undefined && pager.pageIndex !== null) {
                        this.pageIndex.value = Number(pager.pageIndex) || 0;
                    }
                    if (pager.pageSize !== undefined && pager.pageSize !== null) {
                        this.pageSize.value = Number(pager.pageSize) || this.pageSize.value;
                    }
                } else if (res.data.data && res.data.data.total !== undefined) {
                    this.total.value = res.data.data.total;
                } else {
                    this.total.value = Array.isArray(this.items.value) ? this.items.value.length : 0;
                }

                if (silent) {
                    const count = this.total.value;
                    const time = new Date().toLocaleTimeString('zh-CN', { hour12: false });
                    EleManager.showInfo(`数据已更新（共 ${count} 条 · ${time}）`, { duration: 2000 });
                }
            } else {
                EleManager.showError(res.data.info || res.data.msg || '加载失败');
            }
        } catch (e) {
            console.error(e);
            EleManager.showError('请求异常');
        }
    },

    onSelectionChange(rows) {
        this.selectedRows.value = Array.isArray(rows) ? rows : [];
        this.selectedIds.value = rows.map(r => r.id);
    },

    onCurrentChange(currentRow, oldRow) {
        this.currentRow.value = currentRow || null;
        if (currentRow) {
            this.selectedRows.value = [currentRow];
            this.selectedIds.value = [currentRow.id];
        } else {
            this.selectedRows.value = [];
            this.selectedIds.value = [];
        }
    },

    onSortChange({ prop, order }) {
        if (!prop) return;
        this.sortField.value = prop.charAt(0).toUpperCase() + prop.slice(1);
        this.sortDirection.value = order === 'descending' ? 'DESC' : 'ASC';
        this.loadData();
    },

    handlePageChange(p) {
        this.pageIndex.value = p - 1;
        this.loadData();
    },

    handlePageSizeChange(size) {
        this.pageSize.value = size;
        this.pageIndex.value = 0;
        this.loadData();
    },

    openFiltersDrawer() {
        this.filtersDrawerVisible.value = true;
    },

    closeFiltersDrawer() {
        this.filtersDrawerVisible.value = false;
    },

    async applyFiltersAndSearch() {
        this.filtersDrawerVisible.value = false;
        this.pageIndex.value = 0;
        return this.invokeCommand('Data');
    },

    startAutoRefresh() {
        this.stopAutoRefresh();
        if (!this.autoRefreshEnabled.value) return;
        const intervalMs = (this.autoRefreshInterval.value || 30) * 1000;
        const triggle = this._autoRefreshTriggle || 'Data';
        const handler = '?handler=' + triggle;
        this._autoRefreshTimer = setInterval(() => {
            if (this.autoRefreshEnabled.value) {
                this.loadData({ silent: true, handler });
            }
        }, intervalMs);
    },

    stopAutoRefresh() {
        if (this._autoRefreshTimer) {
            clearInterval(this._autoRefreshTimer);
            this._autoRefreshTimer = null;
        }
    },

    toggleAutoRefresh(val, interval, triggle) {
        this.autoRefreshEnabled.value = val;
        if (interval !== undefined) this.autoRefreshInterval.value = interval || 30;
        if (triggle !== undefined) this._autoRefreshTriggle = triggle;
        if (val) {
            this.startAutoRefresh();
        } else {
            this.stopAutoRefresh();
        }
    }
};
