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
        function pickMobile(row) {
            if (row == null) return '';
            const keys = ['mobile', 'Mobile', 'phone', 'Phone', 'tel', 'Tel'];
            for (const k of keys) {
                const v = row[k];
                if (v !== null && v !== undefined && String(v).trim() !== '') return String(v).trim();
            }
            return '';
        }
        function pickRealName(row) {
            if (row == null) return '';
            const keys = ['realName', 'RealName', 'name', 'Name', 'title', 'Title', 'username', 'UserName', 'UserName'];
            for (const k of keys) {
                const v = row[k];
                if (v !== null && v !== undefined && String(v).trim() !== '') return String(v).trim();
            }
            return '';
        }
        function pickDisplayName(row) {
            if (row == null) return '';
            const dnKeys = ['displayName', 'DisplayName', 'display_name'];
            for (const k of dnKeys) {
                const v = row[k];
                if (v !== null && v !== undefined && String(v).trim() !== '') return String(v).trim();
            }
            const real = pickRealName(row);
            const mob = pickMobile(row);
            if (real && mob) return `${real}(${mob})`;
            if (real) return real;
            if (mob) return mob;
            return '';
        }
        // 从 URL 解析 multi 参数
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

        const finalRows = multi ? rows : [rows[0]];
        const data = [];
        for (const r of finalRows) {
            const id = pickId(r);
            if (id === null || typeof id === 'undefined') continue;
            const display = pickDisplayName(r);
            const realName = pickRealName(r);
            const mobile = pickMobile(r);
            const out = {
                id: (typeof id === 'number' || typeof id === 'string' || typeof id === 'bigint') ? id : String(id),
                name: display || realName || String(id)
            };
            if (realName) out.realName = realName;
            if (mobile) out.mobile = mobile;
            if (display) out.displayName = display;
            for (const k of Object.keys(r)) {
                if (['id', 'Id'].includes(k)) continue;
                if (out[k] !== undefined) continue;
                const v = r[k];
                if (v === null || typeof v === 'undefined') continue;
                const tv = typeof v;
                if (tv === 'string' || tv === 'number' || tv === 'boolean' || tv === 'bigint') {
                    out[k] = v;
                } else if (tv === 'object' && (Array.isArray(v) === false) && JSON.stringify(v).length < 500) {
                    try { out[k] = JSON.parse(JSON.stringify(v)); } catch (_) {}
                }
            }
            data.push(out);
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

    // 还原筛选条件到默认值（从 SSR 快照 / data-filter-default 收集），并重新加载数据
    resetFilters() {
        const hostId = this.config?.hostId;
        const hostSelector = hostId ? ('#' + hostId)
            : ((document.querySelector('[id^="app-etbl-"]') ? document.querySelector('[id^="app-etbl-"]').id : '#app'));
        const hostEl = document.querySelector(hostSelector);
        if (hostEl && !hostId) {
            const found = hostEl.id ? ('#' + hostEl.id) : hostSelector;
        }
        // 优先读取 SSR 挂载时预存的快照（见 EleTableAppBuilder/EleListAppBuilder onMounted）
        // 因为 Element Plus 组件 hydrate 完成后会把自定义 HTML attr（data-filter-*）
        // 从 DOM 中 remove，此时再从 DOM collect 只会得到空对象。
        let defaults = (this._snapshotDefaults && typeof this._snapshotDefaults === 'object')
            ? this._snapshotDefaults
            : null;
        if (!defaults) {
            let builder = null;
            if (window.EleAppBuilder && typeof window.EleAppBuilder.prototype?.collectFilterDefaults === 'function') {
                builder = Object.create(window.EleAppBuilder.prototype);
            } else if (window.EleTableAppBuilder && typeof window.EleTableAppBuilder.prototype?.collectFilterDefaults === 'function') {
                builder = Object.create(window.EleTableAppBuilder.prototype);
            } else if (window.EleListAppBuilder && typeof window.EleListAppBuilder.prototype?.collectFilterDefaults === 'function') {
                builder = Object.create(window.EleListAppBuilder.prototype);
            }
            defaults = (builder && typeof builder.collectFilterDefaults === 'function')
                ? builder.collectFilterDefaults(hostSelector)
                : {};
        }
        let builder = null;
        if (window.EleAppBuilder && typeof window.EleAppBuilder.prototype?.applyFilterDefaults === 'function') {
            builder = Object.create(window.EleAppBuilder.prototype);
        } else if (window.EleTableAppBuilder && typeof window.EleTableAppBuilder.prototype?.applyFilterDefaults === 'function') {
            builder = Object.create(window.EleTableAppBuilder.prototype);
        } else if (window.EleListAppBuilder && typeof window.EleListAppBuilder.prototype?.applyFilterDefaults === 'function') {
            builder = Object.create(window.EleListAppBuilder.prototype);
        }
        if (builder && typeof builder.applyFilterDefaults === 'function' && this.filters) {
            builder.applyFilterDefaults(this.filters, defaults);
        } else if (this.filters) {
            if (!this.filters.value || typeof this.filters.value !== 'object') {
                this.filters.value = {};
            }
            for (const [k, v] of Object.entries(defaults || {})) {
                this.filters.value[k] = v;
            }
        }
        return this.loadData(true);
    },

    async invokeCommand(commandName, evt) {
        if (!commandName) return;
        const name = commandName;
        const key = ('' + name).trim().toLowerCase();

        if (name === 'Reset' || key === 'reset') {
            return typeof this.resetFilters === 'function' ? this.resetFilters() : this.loadData(true);
        }
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
        if (name === 'EditBatch') {
            // Compat path: 如果 evt 丢了（老的 invokeCommand 没传事件对象），就近找最近的含 data-batch-fields 的按钮
            let btn = evt?.currentTarget || evt?.srcElement || null;
            if (!btn && document && document.activeElement && document.activeElement.hasAttribute && document.activeElement.hasAttribute('data-batch-fields')) {
                btn = document.activeElement;
            }
            if (!btn || !(btn && typeof btn.getAttribute === 'function' && btn.getAttribute('data-batch-fields'))) {
                // 找最近点击过的按钮：从当前 selection 的 button？兜底：找所有含 data-batch-fields 的第一个
                const candidates = document.querySelectorAll('[data-batch-fields]');
                btn = candidates && candidates.length ? candidates[0] : null;
            }
            return this.openEditBatchDrawer(btn);
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
    },

    // ------------------------------------------------------------
    // EditBatch：从按钮 data-batch-fields 读字段元 → 动态 Vue 表单 Drawer
    // ------------------------------------------------------------
    openEditBatchDrawer(btnEl) {
        if (this.selectedIds.value.length === 0) {
            EleManager.showWarning('请先勾选要批量修改的记录');
            return;
        }
        const raw = btnEl && btnEl.getAttribute ? btnEl.getAttribute('data-batch-fields') : '';
        let fields = [];
        try {
            fields = raw ? JSON.parse(raw) : [];
        } catch (e) {
            console.error('data-batch-fields parse error:', e);
            EleManager.showError('批量按钮字段元数据解析失败');
            return;
        }
        if (!fields || !fields.length) {
            EleManager.showWarning('该按钮未配置要修改的字段');
            return;
        }
        const title = btnEl?.innerText?.trim()?.replace(/\s+/g, ' ') || '批量修改';
        this._mountEditBatchDrawer({
            title,
            fields,
            ids: this.selectedIds.value.slice()
        });
    },

    _mountEditBatchDrawer({ title, fields, ids }) {
        // 创建 Drawer 容器：优先使用 EleManager 统一全局 Drawer（避免父容器 z-index 影响）
        const safeNum = Array.isArray(ids) ? ids.length : 0;
        const instance = this;
        const self = this;
        // 构造表单初始状态 + 更新值抽取
        function buildFormState() {
            const formState = {};
            for (const f of fields) {
                if (f.control === 'picker') {
                    const key = ('' + (f.field || '')).trim();
                    const textKey = f.textFor || key + 'Name';
                    formState[key] = '';
                    formState[textKey] = '';
                } else {
                    formState[(f.field || '').trim()] = f.control === 'switch' ? false : '';
                }
            }
            return formState;
        }
        function collectUpdates(formState) {
            const update = {};
            for (const f of fields) {
                const key = ('' + (f.field || '')).trim();
                if (!key) continue;
                let v = formState[key];
                if (f.control === 'picker') {
                    if (v === null || v === undefined || v === '') continue;
                    if (typeof v === 'number' || typeof v === 'bigint') update[key] = v;
                    else {
                        // 允许数字字符串强转（后端 CheckerId 是 long?，传 number OK）
                        const n = Number(v);
                        update[key] = (typeof n === 'number' && !Number.isNaN(n) && String(n) === String(v).trim()) ? n : String(v);
                    }
                    continue;
                }
                if (f.control === 'switch' || f.control === 'number') {
                    if (v === null || v === undefined || v === '') continue;
                    update[key] = v;
                    continue;
                }
                if (v === null || v === undefined || (typeof v === 'string' && v.trim() === '')) continue;
                update[key] = typeof v === 'string' ? v.trim() : v;
            }
            return update;
        }
        // 构建 Drawer 内容（一个内联 Vue App 挂在 EleManager.openDrawer 的 content 中会复杂；简化方案：沿用之前 host 内联法，但确保 host 存在；另外通过 _hostId 从 EleTable 构造函数注入）
        const hostMountId = (this._hostId || (document.querySelector('[id^="app-etbl-"]') ? document.querySelector('[id^="app-etbl-"]').id : null));
        if (!hostMountId) {
            console.error('EditBatch: host table id not found');
            EleManager.showError('找不到承载表格容器，批量抽屉初始化失败');
            return;
        }
        const host = document.getElementById(hostMountId);
        if (!host) {
            EleManager.showError('找不到承载表格容器');
            return;
        }
        const drawerId = hostMountId + '__batchdrawer';
        const old = document.getElementById(drawerId);
        if (old) old.remove();
        const wrap = document.createElement('div');
        wrap.id = drawerId;
        host.appendChild(wrap);

        const { createApp, ref, reactive } = Vue;

        const app = createApp({
            setup() {
                const visible = ref(true);
                const submitting = ref(false);
                const formState = reactive(buildFormState());
                const getUpdateFields = () => collectUpdates(formState);
                const idsLen = safeNum;
                const instanceRef = instance;
                const defaultSize = window.innerWidth < 768 ? '100%' : '50%';
                function handleClosed() {
                    try { app.unmount(); } catch {}
                    try { if (wrap && wrap.parentNode) wrap.parentNode.removeChild(wrap); } catch {}
                }
                async function submit() {
                    const updates = getUpdateFields();
                    const n = Object.keys(updates).length;
                    if (n === 0) {
                        try {
                            await EleManager.confirm('未填写任何字段（空字段将跳过不更新）。确认要继续吗？', title);
                        } catch { return; }
                    } else {
                        const msg = `将更新 ${safeNum} 条记录的 ${n} 个字段，确认提交？`;
                        try {
                            await EleManager.confirm(msg, title);
                        } catch { return; }
                    }
                    submitting.value = true;
                    try {
                        const res = await axios.post('?handler=BatchSave', {
                            ids: ids,
                            fields: updates
                        }, { headers: { 'RequestVerificationToken': Utils.getCsrfToken() } });
                        if (res.data.code === 0 || res.data.code === '0' || res.data.result === true) {
                            EleManager.showSuccess(Utils.extractMessage(res.data, '批量修改成功'));
                            visible.value = false;
                            if (instanceRef && typeof instanceRef.loadData === 'function') instanceRef.loadData();
                        } else {
                            EleManager.showError(Utils.extractMessage(res.data, '批量修改失败'));
                        }
                    } catch (e) {
                        const s = (window.Utils && typeof Utils.extractMessage === 'function')
                            ? Utils.extractMessage(e?.response?.data, '请求失败')
                            : (e?.message || '请求失败');
                        EleManager.showError(s);
                        console.error(e);
                    } finally {
                        submitting.value = false;
                    }
                }

                function openPickerField(f) {
                    const key = ('' + (f.field || '')).trim();
                    const textKey = f.textFor || (key + 'Name');
                    const url = f.popupUrl || '';
                    if (!url) { EleManager.showWarning(`字段 ${f.label || key} 未配置 PopupUrl`); return; }
                    const multi = !!f.multi;
                    const pickerTitle = f.label || key;
                    // 解析 ElePicker close payload：递归解包多层 data.data.data，兼容 closePage 的包壳
                    function resolvePickerCloseData(payload) {
                        if (!payload || typeof payload !== 'object') return null;
                        if (payload.type === 'ElePicker' || payload.type === 'user-selected') return payload;
                        const nested = payload.data;
                        if (nested && typeof nested === 'object') {
                            if (nested.type === 'ElePicker' || nested.type === 'user-selected') return nested;
                            if (nested.data && typeof nested.data === 'object') {
                                const d2 = nested.data;
                                if (d2.type === 'ElePicker' || d2.type === 'user-selected') return d2;
                                if (d2.data && typeof d2.data === 'object') {
                                    const d3 = d2.data;
                                    if (d3.type === 'ElePicker' || d3.type === 'user-selected') return d3;
                                }
                            }
                        }
                        return null;
                    }
                    // 和 pickerMethods.handlePickerMessage 对齐：兼容多种 rows 排列格式
                    // User Picker 显示优化：优先用 displayName / RealName(Mobile)，兜底 name / Name
                    function makeDisplayName(r) {
                        if (!r || typeof r !== 'object') return '';
                        if (r.displayName && String(r.displayName).trim()) return String(r.displayName).trim();
                        const real = r.realName ?? r.RealName ?? r.name ?? r.Name ?? '';
                        const mob  = r.mobile ?? r.Mobile ?? '';
                        if (real && mob) return `${real}(${mob})`;
                        if (real) return String(real);
                        if (mob) return String(mob);
                        const id = r.id !== undefined ? r.id : r.Id;
                        return id !== undefined ? String(id) : '';
                    }
                    function applyPickerRows(msgData) {
                        if (!msgData) return;
                        const src = msgData.data || msgData;
                        let rows = Array.isArray(src) ? src
                                 : (src && Array.isArray(src.rows)) ? src.rows
                                 : (src && (src.id !== undefined || src.Id !== undefined)) ? [src]
                                 : (src && src.data && (src.data.id !== undefined || src.data.Id !== undefined)) ? [src.data]
                                 : [];
                        if (!rows || rows.length === 0) return;
                        if (multi) {
                            const idsArr   = rows.map(r => (r.id !== undefined ? r.id : r.Id));
                            const names = rows.map(r => makeDisplayName(r));
                            formState[key]     = idsArr.join(',');
                            formState[textKey] = names.join(',');
                        } else {
                            const first = rows[0] || {};
                            const idVal   = first.id   !== undefined ? first.id   : first.Id;
                            const nameVal = makeDisplayName(first);
                            formState[key]     = idVal;
                            formState[textKey] = nameVal;
                        }
                    }
                    let handled = false;
                    const tryHandle = (pickerData) => {
                        if (handled || !pickerData) return;
                        applyPickerRows(pickerData);
                        handled = true;
                        window.removeEventListener('message', onMsg);
                    };
                    const onMsg = (ev) => {
                        try {
                            const outer = (typeof ev.data === 'string') ? JSON.parse(ev.data) : ev.data;
                            if (!outer || typeof outer !== 'object') return;
                            const pickerData = resolvePickerCloseData(outer);
                            if (pickerData) tryHandle(pickerData);
                        } catch {}
                    };
                    window.addEventListener('message', onMsg);
                    const options = {
                        title: pickerTitle,
                        url,
                        direction: 'rtl',
                        destroyOnClose: true,
                        resizable: true,
                        size: (window.innerWidth < 768 ? '100%' : ''),
                        closeHandler: (payload) => {
                            const pickerData = resolvePickerCloseData(payload);
                            if (pickerData) tryHandle(pickerData);
                        }
                    };
                    if (window.EleManager && typeof window.EleManager.openDrawer === 'function') {
                        window.EleManager.openDrawer(options);
                    } else {
                        window.open(url, '_blank', 'noopener');
                    }
                }

                return { visible, submitting, fields, title, formState, submit, openPickerField, idsLen, handleClosed, defaultSize };
            },
            template: `
<el-drawer v-model="visible" :title="title" :size="defaultSize" direction="rtl" :destroy-on-close="true" @closed="handleClosed" custom-class="eleui-batch-edit-drawer">
  <div class="flex flex-col gap-4 p-2">
    <el-alert type="info" :closable="false" show-icon>
      <template #title>已准备 <b>{{ fields.length }}</b> 个字段，将更新 <b>{{ idsLen }}</b> 条记录；<span class="text-gray-600">留空的字段将跳过不更新</span>。</template>
    </el-alert>
    <el-form label-width="110px" @submit.prevent class="mt-2">
      <template v-for="f in fields" :key="f.field || f.label">
        <el-form-item :label="(f.label || f.field || '') + (f.required ? ' *': '')">
          <el-input v-if="f.control === 'input'"    v-model="formState[f.field]" :placeholder="f.placeholder || '留空则不更新'" :disabled="!f.enabled" clearable />
          <el-input v-else-if="f.control === 'textarea'" v-model="formState[f.field]" type="textarea" :rows="f.rows || 3" :placeholder="f.placeholder || '留空则不更新'" :disabled="!f.enabled" />
          <el-input-number v-else-if="f.control === 'number'" v-model="formState[f.field]" :min="f.min" :max="f.max" :step="f.step || 1" :disabled="!f.enabled" style="width:100%" />
          <el-switch v-else-if="f.control === 'switch'" v-model="formState[f.field]" :disabled="!f.enabled" />
          <el-date-picker
              v-else-if="f.control === 'datepicker'"
              v-model="formState[f.field]"
              type="date"
              value-format="YYYY-MM-DD"
              placeholder="留空则不更新"
              :disabled="!f.enabled"
              style="width:100%" />
          <el-date-picker
              v-else-if="f.control === 'datetimepicker'"
              v-model="formState[f.field]"
              type="datetime"
              value-format="YYYY-MM-DD HH:mm:ss"
              placeholder="留空则不更新"
              :disabled="!f.enabled"
              style="width:100%" />
          <el-select v-else-if="f.control === 'select'" v-model="formState[f.field]" clearable placeholder="留空则不更新" :disabled="!f.enabled" style="width:100%">
            <el-option v-for="o in (f.options || [])" :key="o.value" :label="o.label" :value="o.value"></el-option>
          </el-select>
          <div v-else-if="f.control === 'picker'" class="w-full flex gap-2 items-center">
            <el-input v-model="formState[f.textFor || (f.field + 'Name')]" :placeholder="f.placeholder || '点击右侧按钮选择...'" readonly :disabled="!f.enabled" style="flex:1 1 auto" clearable />
            <el-button type="primary" plain :disabled="!f.enabled" @click="openPickerField(f)">选择</el-button>
          </div>
          <el-tag v-else type="danger">未知控件 {{ f.control }}</el-tag>
        </el-form-item>
      </template>
    </el-form>
  </div>
  <template #footer>
    <div class="flex gap-2 justify-end w-full">
      <el-button @click="visible = false">取消</el-button>
      <el-button type="primary" :loading="submitting" @click="submit">提交批量修改</el-button>
    </div>
  </template>
</el-drawer>
`
        });

        app.use(ElementPlus);
        app.mount(wrap);
    }
};
