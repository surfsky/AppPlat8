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
}

export const paginationMethods = {
    async loadData() {
        try {
            const res = await axios.get(this.dataHandler, {
                params: {
                    pageIndex: this.pageIndex.value,
                    pageSize: this.pageSize.value,
                    sortField: this.sortField.value,
                    sortDirection: this.sortDirection.value,
                    ...this.config.extraParams,
                    ...this.filters.value
                }
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
    }
};
