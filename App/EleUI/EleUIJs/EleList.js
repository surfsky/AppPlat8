export class EleList {
    constructor(options = {}) {
        const { ref } = Vue;
        this.config = options;

        this.items = ref([]);
        this.total = ref(0);
        this.pageIndex = ref(0);
        this.pageSize = ref(options.pageSize || 10);
        this.sortField = ref(options.defaultSortField || 'Id');
        this.sortDirection = ref(options.defaultSortDirection || 'DESC');
        this.filters = ref({});
        this.loading = ref(false);
        this.finished = ref(false);

        this.dataHandler = options.dataHandler || '?handler=Data';
    }

    async loadData(reset = false) {
        if (this.loading.value) return;

        if (reset) {
            this.pageIndex.value = 0;
            this.items.value = [];
            this.finished.value = false;
        }

        if (this.finished.value) return;

        this.loading.value = true;
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

            if (res.data.code !== 0 && res.data.code !== '0') {
                EleManager.showError(res.data.msg || res.data.info || '加载失败');
                return;
            }

            const payload = res.data.data;
            const pageItems = payload?.items || payload || [];
            const list = Array.isArray(pageItems) ? pageItems : [];

            const pager = res.data.pager || res.data.extra || null;
            const total = pager?.total ?? payload?.total ?? 0;
            this.total.value = Number(total) || 0;

            if (reset) {
                this.items.value = list;
            } else {
                this.items.value = [...this.items.value, ...list];
            }

            this.pageIndex.value += 1;

            if (list.length < this.pageSize.value || this.items.value.length >= this.total.value) {
                this.finished.value = true;
            }
        } catch (e) {
            console.error(e);
            EleManager.showError('请求异常');
        } finally {
            this.loading.value = false;
        }
    }

    onListScroll(e) {
        if (this.loading.value || this.finished.value) return;
        const el = e?.target;
        if (!el) return;

        const nearBottom = el.scrollHeight - el.scrollTop - el.clientHeight <= 40;
        if (nearBottom) {
            this.loadData(false);
        }
    }

    async invokeCommand(commandName) {
        const key = (commandName || '').trim().toLowerCase();
        if (!key) return;

        if (key === 'search' || key === 'data' || key === 'refresh') {
            return this.loadData(true);
        }
    }
}
