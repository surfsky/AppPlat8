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
        function pickId(row) {
            if (row == null) return null;
            if (row.id !== null && typeof row.id !== 'undefined') return row.id;
            if (row.Id !== null && typeof row.Id !== 'undefined') return row.Id;
            return null;
        }
        function pickName(row) {
            if (row == null) return '';
            const v = row.name ?? row.Name ?? row.realName ?? row.RealName ?? row.title ?? row.Title;
            return (v === null || typeof v === 'undefined') ? '' : `${v}`;
        }
        // 从 URL 解析 multi 参数（允许 ?multi=true / 1 / yes 或 ElePicker 打开时带 multi）
        function resolveMulti() {
            try {
                const qs = new URLSearchParams(window.location.search);
                const m = (qs.get('multi') || '').toString().trim().toLowerCase();
                return m === 'true' || m === '1' || m === 'yes' || m === 'on';
            } catch (_) { return false; }
        }
        const multi = resolveMulti();
        const hasSelectedRows = Array.isArray(this.selectedRows.value) && this.selectedRows.value.length > 0;
        let rows = [];
        if (hasSelectedRows) {
            rows = this.selectedRows.value.slice();
        } else if (this.currentRow.value) {
            rows = [this.currentRow.value];
        }

        if (!rows.length) {
            EleManager.showWarning(multi ? '请先选择记录' : '请先选择一条记录');
            return;
        }

        // 非 multi 场景只取第一行（兼容单选）
        const finalRows = multi ? rows : [rows[0]];
        const data = [];
        for (const r of finalRows) {
            const id = pickId(r);
            if (id === null || typeof id === 'undefined') continue;
            const nm = pickName(r);
            // 脱 Proxy，避免 postMessage 跨窗口传 Proxy 失败
            data.push({
                id: (typeof id === 'number' || typeof id === 'string' || typeof id === 'bigint') ? id : String(id),
                name: nm || String(id)
            });
        }
        if (!data.length) {
            EleManager.showError('所选记录缺少 id，无法回传');
            return;
        }

        const payload = {
            type: 'ElePicker',
            data
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
                EleManager.showSuccess(Utils.extractMessage(res.data, '删除成功'));
                this.selectedIds.value = [];
                this.loadData();
            } else {
                EleManager.showError(Utils.extractMessage(res.data, '删除失败'));
            }
        } catch (e) {
            const s = (window.Utils && typeof Utils.extractMessage === 'function')
                ? Utils.extractMessage(e?.response?.data, '请求失败')
                : (e?.message || '请求失败');
            EleManager.showError(s);
            console.error(e);
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
                EleManager.showSuccess(Utils.extractMessage(res.data, '删除成功'));
                this.loadData();
            } else {
                EleManager.showError(Utils.extractMessage(res.data, '删除失败'));
            }
        } catch (e) {
            const s = (window.Utils && typeof Utils.extractMessage === 'function')
                ? Utils.extractMessage(e?.response?.data, '请求失败')
                : (e?.message || '请求失败');
            EleManager.showError(s);
            console.error(e);
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
                EleManager.showSuccess(Utils.extractMessage(res.data, '操作成功'));
                this.loadData();
            } else {
                EleManager.showError(Utils.extractMessage(res.data, '操作失败'));
            }
        } catch (e) {
            const s = (window.Utils && typeof Utils.extractMessage === 'function')
                ? Utils.extractMessage(e?.response?.data, '请求失败')
                : (e?.message || '请求失败');
            EleManager.showError(s);
            console.error(e);
        }
    }
};
