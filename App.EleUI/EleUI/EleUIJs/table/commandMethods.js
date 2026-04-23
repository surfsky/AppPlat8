export const commandMethods = {
    exportData() {
        try {
            const form = document.createElement('form');
            form.method = 'POST';
            form.action = this.exportHandler;
            form.style.display = 'none';

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
    },

    selectCurrentItem() {
        const selectedRow = (Array.isArray(this.selectedRows.value) && this.selectedRows.value.length > 0)
            ? this.selectedRows.value[0]
            : this.currentRow.value;

        if (!selectedRow) {
            EleManager.showWarning('请先选择一条记录');
            return;
        }

        const id = selectedRow.id ?? selectedRow.Id;
        const name = selectedRow.name ?? selectedRow.Name ?? selectedRow.title ?? selectedRow.Title;

        if (id === null || typeof id === 'undefined') {
            EleManager.showError('当前记录缺少 id，无法回传');
            return;
        }

        const payload = {
            type: 'EleSelector',
            data: [{
                id,
                name: (name === null || typeof name === 'undefined') ? `${id}` : `${name}`
            }]
        };

        EleManager.closePage(payload);
    },

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
    },

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
    },

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
    },

    async invokeCommand(commandName) {
        if (!commandName) return;
        const name = commandName;
        const key = ('' + name).trim().toLowerCase();

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
        if (key === 'select') {
            return this.selectCurrentItem();
        }
        if (name === 'Delete' || name === 'BatchDelete') {
            return this.deleteItems();
        }

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
};
