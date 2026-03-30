// Encapsulates common logic for List pages using Vue 3 + Element Plus

/**********************************************************************
 * EleTable Class
 **********************************************************************/
export class EleTable {
    constructor(options = {}) {
        const { ref } = Vue;
        this.config = options;

        // State
        this.items = ref([]);
        this.total = ref(0);
        this.pageIndex = ref(0);
        this.filters = ref({});
        this.selectedIds = ref([]);
        this.pageSize = ref(options.pageSize || 10);
        this.sortField = ref(options.defaultSortField || 'Id');
        this.sortDirection = ref(options.defaultSortDirection || 'DESC');
        this.filtersDrawerVisible = ref(false);
        this.dataHandler   = options.dataHandler   || '?handler=Data';
        this.deleteHandler = options.deleteHandler || '?handler=Delete';
        this.exportHandler = options.exportHandler || '?handler=Export';

        // Kept for compatibility with existing extensions that may call startResize.
        this.drawerSize = ref(window.innerWidth < 768 ? '100%' : '50%');
        this.drawerPosition = ref('rtl');
        this.isResizing = ref(false);

        // Dynamic Options (for TreeSelect etc)
        this.options = ref({}); 
    }


    // reload data
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
            
            // 设置数据和总数，兼容不同接口返回结构
            if (res.data.code === 0 || res.data.code === '0') {
                this.items.value = res.data.data.items || res.data.data;
                if (res.data.data.total !== undefined) {
                    this.total.value = res.data.data.total;
                } else if (res.data.extra && res.data.extra.total !== undefined) {
                    this.total.value = res.data.extra.total;
                }
            } else {
                EleManager.showError(res.data.info || res.data.msg || '加载失败');
            }
        } catch (e) {
            console.error(e);
            EleManager.showError('请求异常');
        }
    }

    // 导出数据到 Excel
    exportData() {
        try {
            // 使用表单提交方式，让浏览器原生处理文件下载
            const form = document.createElement('form');
            form.method = 'POST';
            form.action = this.exportHandler;
            form.style.display = 'none';

            // 添加导出参数
            const params = {
                pageIndex: this.pageIndex.value,
                pageSize: this.pageSize.value,
                sortField: this.sortField.value,
                sortDirection: this.sortDirection.value,
                ...this.filters.value
            };

            for (const [key, value] of Object.entries(params)) {
                const input = document.createElement('input');
                input.type = 'hidden';
                input.name = key;
                input.value = value;
                form.appendChild(input);
            }

            // 添加 CSRF 令牌
            const token = Utils.getCsrfToken();
            if (token) {
                const tokenInput = document.createElement('input');
                tokenInput.type = 'hidden';
                tokenInput.name = '__RequestVerificationToken';
                tokenInput.value = token;
                form.appendChild(tokenInput);
            }

            document.body.appendChild(form);
            form.submit();
            document.body.removeChild(form);
            
            EleManager.showSuccess('导出中...');
        } catch (e) {
            console.error(e);
            EleManager.showError('导出失败');
        }
    }    

    // 选择行变化事件
    onSelectionChange(rows) {
        this.selectedIds.value = rows.map(r => r.id);
    }

    //
    // Pager 数据变更事件
    //
    onCurrentChange(currentRow, oldRow) {
        if (currentRow) {
            this.selectedIds.value = [currentRow.id];
        } else {
            this.selectedIds.value = [];
        }
    }

    onSortChange({ prop, order }) {
        if (!prop) return;
        this.sortField.value = prop.charAt(0).toUpperCase() + prop.slice(1);
        this.sortDirection.value = order === 'descending' ? 'DESC' : 'ASC';
        this.loadData();
    }

    handlePageChange(p) {
        this.pageIndex.value = p - 1;
        this.loadData();
    }

    handlePageSizeChange(size) {
        this.pageSize.value = size;
        this.pageIndex.value = 0;
        this.loadData();
    }

    // Open image preview via EleManager from table context.
    openImagePreview(url, initialIndex = 0) {
        const imageUrl = typeof url === 'string' ? url.trim() : '';
        if (!imageUrl) return;

        const manager = (window.top && window.top.EleManager)
            ? window.top.EleManager
            : window.EleManager;

        if (manager && typeof manager.openImageViewer === 'function') {
            manager.openImageViewer([imageUrl], initialIndex);
            return;
        }

        window.open(imageUrl, '_blank', 'noopener');
    }

    // 动态获取选项数据（用于树形选择等）
    async fetchOptions(key, url) {
        if (this.options.value[key]) return;
        try {
            const res = await axios.get(url);
            if (res.data.code === 0 || res.data.code === '0') {
                this.options.value[key] = res.data.data;
            }
        } catch (e) {
            console.error('Fetch options failed:', url, e);
        }
    }

    // 打开表单
    openForm(id, urlBase, drawerTitle = null) { this.openDataDrawer(id, urlBase, 'edit', drawerTitle); }

    // 打开详情
    openView(id, urlBase, drawerTitle = null) { this.openDataDrawer(id, urlBase, 'view', drawerTitle); }

    // 打开表单
    openDataDrawer(id, urlBase, modeParam, drawerTitle = null) {
        const resolvedBase = this.resolveDrawerUrlBase(urlBase);

        // id, mode, selectId
        const mode = (id === 0 || id === '0') ? 'new' : modeParam;
        const selectedId = this.selectedIds.value.length > 0 ? this.selectedIds.value[0] : null;
        const effectiveSelectId = selectedId ?? (id && id !== 0 && id !== '0' ? id : null);
        const query = new URLSearchParams({ id: `${id}`, md: mode });
        if (effectiveSelectId !== null && typeof effectiveSelectId !== 'undefined') {
            query.set('selectId', `${effectiveSelectId}`);
        }

        const separator = resolvedBase.includes('?') ? '&' : '?';
        var url = `${resolvedBase}${separator}${query.toString()}`;
        this.openDrawer(url, null, 'rtl', drawerTitle);
    }

    // Resolve relative FormPage against current page URL before handing it to top-level drawer host.
    resolveDrawerUrlBase(urlBase) {
        if (!urlBase) return '';
        try {
            const absolute = new URL(urlBase, window.location.href);
            return `${absolute.pathname}${absolute.search}${absolute.hash}`;
        } catch (e) {
            return urlBase;
        }
    }

    // 打开抽屉式表单
    openDrawer(url, width=null, position = 'rtl', titleOverride = null) {
        const size = width || (window.innerWidth < 768 ? '100%' : '50%');
        const title = titleOverride || this.config?.drawerTitle || this.config?.title || '编辑';
        this.drawerSize.value = size;
        this.drawerPosition.value = position || 'rtl';
        EleManager.openDrawer({
            title,
            url,
            direction: this.drawerPosition.value,
            size,
            resizable: true,
            closeOnClickModal: false,
            destroyOnClose: true,
            closeHandler: () => this.handleDrawerClose()
        });
    }


    // 删除选中项
    async deleteItems() {
        if (this.selectedIds.value.length === 0) return;
        try {
            await EleManager.confirm('确定删除选中项？');
        } catch {
            return;
        }
        try {
            const res = await axios.post(this.deleteHandler, this.selectedIds.value, {
                headers: { 'RequestVerificationToken': Utils.getCsrfToken() }
            });
            if (res.data.result === true || res.data.code === 0 || res.data.code === '0') {
                EleManager.showSuccess(res.data.info || '删除成功');
                this.selectedIds.value = [];
                this.loadData();
            } else {
                EleManager.showError(res.data.info || res.data.msg || '删除失败');
            }
        } catch (e) {
            EleManager.showError('请求失败');
        }
    }

    // 删除单条记录
    async deleteSingleItem(id) {
        try {
            await EleManager.confirm('确定删除该记录？');
        } catch {
            return;
        }
        try {
            const res = await axios.post(this.deleteHandler, [id], {
                headers: { 'RequestVerificationToken': Utils.getCsrfToken() }
            });
            if (res.data.result === true || res.data.code === 0 || res.data.code === '0') {
                EleManager.showSuccess(res.data.info || '删除成功');
                this.loadData();
            } else {
                EleManager.showError(res.data.info || res.data.msg || '删除失败');
            }
        } catch (e) {
            EleManager.showError('请求失败');
        }
    }



    // 关闭表单对话框并刷新数据
    handleDrawerClose() {
        this.loadData();
    }

    // 触发子表单保存
    triggerChildSave() {
        try {
            this.formFrame.value?.contentWindow?.postMessage('RequestSave', '*');
        } catch {}
    }

    // 导航到指定URL
    navigateTo(url) {
        window.location.href = url;
    }

    // 处理消息事件
    messageHandler(e) {
        if (!e) return;

        // New unified protocol: close payload from EleManager.closePage(...)
        const payload = e.data;
        if (payload && typeof payload === 'object' && payload.__elePageClose === true) {
            EleManager.closeDrawer();
            return;
        }
    }


    // Unified command handler for buttons (Data, Export, Delete, Update, etc.)
    async invokeCommand(commandName) {
        if (!commandName) return;
        const name = commandName;
        const key = ('' + name).trim().toLowerCase();

        // Handle special cases
        if (name === 'Data') {
            return this.loadData();
        }
        if (key === 'search') {
            this.pageIndex.value = 0;
            return this.loadData();
        }
        if (name === 'Export') {
            return this.exportData();
        }
        if (key === 'add') {
            const editPage = this.config?.editPage || 'Form';
            return this.openForm(0, editPage);
        }
        if (name === 'Delete' || name === 'BatchDelete') {
            return this.deleteItems();
        }

        // For other commands, POST with table context data
        // This allows server-side handlers to access selectedIds, filters, etc.
        try {
            const payload = {
                selectedIds: this.selectedIds.value,
                filters: this.filters.value,
                pageIndex: this.pageIndex.value,
                pageSize: this.pageSize.value,
                sortField: this.sortField.value,
                sortDirection: this.sortDirection.value
            };

            const res = await axios.post('?handler=' + name, payload, {
                headers: { 'RequestVerificationToken': Utils.getCsrfToken() }
            });

            if (res.data.code === 0 || res.data.code === '0' || res.data.result === true) {
                EleManager.showSuccess(res.data.msg || res.data.info || '操作成功');
                this.loadData();
            } else {
                EleManager.showError(res.data.msg || res.data.info || '操作失败');
            }
        } catch (e) {
            EleManager.showError('请求失败');
            console.error(e);
        }
    }

    openFiltersDrawer() {
        this.filtersDrawerVisible.value = true;
    }

    closeFiltersDrawer() {
        this.filtersDrawerVisible.value = false;
    }

    async applyFiltersAndSearch() {
        this.filtersDrawerVisible.value = false;
        this.pageIndex.value = 0;
        return this.invokeCommand('Data');
    }

    // Resize Logic
    startResize(e) {
        e.preventDefault();
        this.isResizing.value = true;
        
        const startX = e.clientX;
        const drawerEl = e.target.closest('.el-drawer') || document.querySelector('.el-drawer.open') || document.querySelector('.el-drawer');
        const startWidth = drawerEl ? drawerEl.offsetWidth : 600;

        const overlay = document.createElement('div');
        Object.assign(overlay.style, {
            position: 'fixed', top: '0', left: '0', width: '100vw', height: '100vh',
            zIndex: '99999', cursor: 'col-resize', backgroundColor: 'transparent'
        });
        document.body.appendChild(overlay);

        const doResize = (moveEvent) => {
            if (!this.isResizing.value) return;
            let offset;
            if (this.drawerPosition.value === 'ltr') {
                offset = moveEvent.clientX - startX;
            } else {
                offset = startX - moveEvent.clientX;
            }
            
            const newWidth = startWidth + offset;
            if (newWidth > 300 && newWidth < window.innerWidth - 50) {
                this.drawerSize.value = `${newWidth}px`;
            }
        };

        const stopResize = () => {
            this.isResizing.value = false;
            document.removeEventListener('mousemove', doResize);
            document.removeEventListener('mouseup', stopResize);
            if (overlay && overlay.parentNode) overlay.parentNode.removeChild(overlay);
        };

        document.addEventListener('mousemove', doResize);
        document.addEventListener('mouseup', stopResize);
    }
}


