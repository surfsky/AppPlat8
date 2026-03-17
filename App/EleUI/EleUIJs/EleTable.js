// Encapsulates common logic for List pages using Vue 3 + Element Plus

/**********************************************************************
 * EleTable Class
 **********************************************************************/
export class EleTable {
    constructor(options = {}) {
        const { ref } = Vue;
        this.options = options;

        // State
        this.items = ref([]);
        this.total = ref(0);
        this.pageIndex = ref(0);
        this.filters = ref({});
        this.selectedIds = ref([]);
        this.formUrl = ref('');
        this.drawerVisible = ref(false);
        this.formFrame = ref(null);
        this.pageSize = ref(options.pageSize || 10);
        this.sortField = ref(options.defaultSortField || 'Id');
        this.sortDirection = ref(options.defaultSortDirection || 'DESC');
        this.dataHandler   = options.dataHandler   || '?handler=Data';
        this.deleteHandler = options.deleteHandler || '?handler=Delete';
        this.exportHandler = options.exportHandler || '?handler=Export';

        // Drawer
        this.drawerSize = ref(window.innerWidth < 768 ? '100%' : '50%');
        this.drawerPosition = ref('rtl'); // Default right-to-left
        this.isResizing = ref(false);

        // Resize Listener
        window.addEventListener('resize', () => {
            if (window.innerWidth < 768) {
                this.drawerSize.value = '100%';
            } else if (this.drawerSize.value === '100%') {
                this.drawerSize.value = '50%'; // Restore to default if it was full width
            }
        });

        // Dynamic Options (for TreeSelect etc)
        this.options = ref({}); 
    }

    // Helpers
    getCsrfToken() {
        return typeof EleManager !== 'undefined' ? EleManager.getCsrfToken() : (document.querySelector('input[name="__RequestVerificationToken"]')?.value || '');
    }

    formatDt(s, type) {
        return typeof EleManager !== 'undefined' ? EleManager.formatDate(s, type) : s;
    }

    // 枚举格式化代理到 EleManager
    formatEnum(val, options) {
        return typeof EleManager !== 'undefined' ? EleManager.formatEnum(val, options) : val;
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
                    ...this.options.extraParams,
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
                if (typeof EleManager !== 'undefined') EleManager.showError(res.data.info || res.data.msg || '加载失败');
            }
        } catch (e) {
            console.error(e);
            if (typeof EleManager !== 'undefined') EleManager.showError('请求异常');
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
            const token = this.getCsrfToken();
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
            
            (typeof EleManager !== 'undefined' ? EleManager : null)?.showSuccess('导出中...');
        } catch (e) {
            console.error(e);
            (typeof EleManager !== 'undefined' ? EleManager : null)?.showError('导出失败');
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
    openForm(id, urlBase) {
        this.openDataDrawer(id, urlBase, 'edit');
    }

    // 打开详情
    openView(id, urlBase) {
        this.openDataDrawer(id, urlBase, 'view');
    }

    // 打开表单
    openDataDrawer(id, urlBase, modeParam) {
        // id, mode, selectId
        const mode = (id === 0 || id === '0') ? 'new' : modeParam;
        const selectedId = this.selectedIds.value.length > 0 ? this.selectedIds.value[0] : null;
        const effectiveSelectId = selectedId ?? (id && id !== 0 && id !== '0' ? id : null);
        const query = new URLSearchParams({ id: `${id}`, md: mode });
        if (effectiveSelectId !== null && typeof effectiveSelectId !== 'undefined') {
            query.set('selectId', `${effectiveSelectId}`);
        }

        const separator = urlBase.includes('?') ? '&' : '?';
        var url = `${urlBase}${separator}${query.toString()}`;
        this.openDrawer(url);
    }

    // 打开抽屉式表单
    openDrawer(url, width=null, position = 'rtl') {
        this.formUrl.value = url;
        this.drawerPosition.value = position;
        if (width) {
            this.drawerSize.value = width;
        } else {
            this.drawerSize.value = window.innerWidth < 768 ? '100%' : '50%';
        }

        // 打开抽屉弹窗
        this.drawerVisible.value = true;
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
                headers: { 'RequestVerificationToken': this.getCsrfToken() }
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
                headers: { 'RequestVerificationToken': this.getCsrfToken() }
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
        if (e.data === 'AnnouncementSaved' || e.data === 'ItemSaved') { 
            this.drawerVisible.value = false;
            this.loadData();
        }
        if (e.data === 'CloseForm' || e.data === 'CloseAnnouncementForm') {
            this.drawerVisible.value = false;
        }
    }


    // Unified command handler for buttons (Data, Export, Delete, Update, etc.)
    async invokeCommand(commandName) {
        if (!commandName) return;
        const name = commandName;

        // Handle special cases
        if (name === 'Data') {
            return this.loadData();
        }
        if (name === 'Export') {
            return this.exportData();
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
                headers: { 'RequestVerificationToken': this.getCsrfToken() }
            });

            if (res.data.code === 0 || res.data.code === '0' || res.data.result === true) {
                (typeof EleManager !== 'undefined' ? EleManager : null)?.showSuccess(res.data.msg || res.data.info || '操作成功');
                this.loadData();
            } else {
                (typeof EleManager !== 'undefined' ? EleManager : null)?.showError(res.data.msg || res.data.info || '操作失败');
            }
        } catch (e) {
            (typeof EleManager !== 'undefined' ? EleManager : null)?.showError('请求失败');
            console.error(e);
        }
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


