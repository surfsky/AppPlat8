import { initPickerState, pickerMethods } from './form/pickerMethods.js';
import { initUploadState, uploadMethods } from './form/uploadMethods.js';
import { initControlState, controlStateMethods } from './form/controlStateMethods.js';
import { initTreePickerState, treePickerMethods } from './form/treePickerMethods.js';
import { listPickerMethods } from './form/listPickerMethods.js';

// Encapsulates common logic for Form pages using Vue 3 + Element Plus
export class EleForm {
    constructor(defaultForm = {}, config = {}) {
        const { ref, computed } = Vue;
        this.config = config;

        this.form = ref({ ...defaultForm });
        this.formRef = ref(null);
        this.originalForm = ref(null);
        this.saving = ref(false);
        this.error = ref('');
        this.success = ref('');
        this.readOnly = ref(false);
        this.dataHandler = config.dataHandler || '?handler=Data';
        this.saveHandler = config.saveHandler || '?handler=Save';

        this.isDirty = computed(() => {
            if (!this.originalForm.value) return false;
            return JSON.stringify(this.form.value) !== JSON.stringify(this.originalForm.value);
        });

        // Embedded EleList controls in form context
        this.eleLists = ref({});
        this.propertyRows = ref({});

        // Split domains: picker, upload, control linkage
        initPickerState(this, Vue);
        initUploadState(this, Vue);
        initControlState(this, Vue);
        initTreePickerState(this, Vue);
    }

    eleListState(key) {
        if (!key) return { items: [], total: 0, loading: false, finished: true };

        const map = this.eleLists.value;
        if (!map[key]) {
            map[key] = {
                items: [],
                total: 0,
                pageIndex: 0,
                pageSize: 10,
                sortField: 'Id',
                sortDirection: 'DESC',
                dataHandler: '?handler=Data',
                loading: false,
                finished: false
            };
        }

        return map[key];
    }

    async initEleList(key, config = {}, scrollEl = null) {
        const state = this.eleListState(key);
        state.dataHandler = config.dataHandler || state.dataHandler;
        state.pageSize = Number(config.pageSize) > 0 ? Number(config.pageSize) : state.pageSize;
        state.sortField = config.sortField || state.sortField;
        state.sortDirection = config.sortDirection || state.sortDirection;
        state.scrollEl = scrollEl || null;

        await this.loadEleList(key, true);
        await Vue.nextTick();
        await this.ensureEleListScrollable(key, scrollEl);
    }

    async loadEleList(key, reset = false) {
        const state = this.eleListState(key);
        if (state.loading) return;

        if (reset) {
            state.pageIndex = 0;
            state.items = [];
            state.finished = false;
        }

        if (state.finished) return;

        state.loading = true;
        try {
            const res = await axios.get(state.dataHandler, {
                params: {
                    pageIndex: state.pageIndex,
                    pageSize: state.pageSize,
                    sortField: state.sortField,
                    sortDirection: state.sortDirection
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
            state.total = Number(total) || 0;

            if (reset) {
                state.items = list;
            } else {
                state.items = [...state.items, ...list];
            }

            state.pageIndex += 1;
            if (list.length < state.pageSize || state.items.length >= state.total) {
                state.finished = true;
            }
        } catch (e) {
            console.error(e);
            EleManager.showError('请求异常');
        } finally {
            state.loading = false;
        }
    }

    onEleListScroll(key, e) {
        const state = this.eleListState(key);
        if (state.loading || state.finished) return;

        const el = e?.target;
        if (!el) return;

        const nearBottom = el.scrollHeight - el.scrollTop - el.clientHeight <= 40;
        if (nearBottom) {
            this.loadEleList(key, false);
        }
    }

    onEleListWindowScroll(key, scrollEl) {
        const state = this.eleListState(key);
        if (state.loading || state.finished) return;

        const el = scrollEl || state.scrollEl;
        if (!el) return;

        const rect = el.getBoundingClientRect();
        const nearBottom = rect.bottom - window.innerHeight <= 60;
        if (nearBottom) {
            this.loadEleList(key, false);
        }
    }

    openLinkInDrawer(url, title = '查看', size = null) {
        if (!url) return;

        const drawerSize = (typeof size === 'string' && size.trim()) ? size.trim() : null;
        EleManager.openDrawer({
            title: title || '查看',
            url,
            direction: 'rtl',
            size: drawerSize,
            resizable: true,
            closeOnClickModal: false,
            destroyOnClose: true
        });
    }

    async ensureEleListScrollable(key, containerEl) {
        if (!containerEl) return;

        for (let i = 0; i < 8; i++) {
            const state = this.eleListState(key);
            if (state.finished || state.loading) break;

            const canScroll = containerEl.scrollHeight > containerEl.clientHeight + 4;
            if (canScroll) break;

            await this.loadEleList(key, false);
            await Vue.nextTick();
        }
    }

    normalizePropertyField(field) {
        if (!field) return '';
        const name = String(field);
        return name.charAt(0).toLowerCase() + name.slice(1);
    }

    parsePropertyJson(raw) {
        if (raw == null) return {};
        const text = String(raw).trim();
        if (!text) return {};
        try {
            const parsed = JSON.parse(text);
            if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
                return parsed;
            }
            return {};
        } catch {
            return {};
        }
    }

    getPropertyRows(field) {
        const key = this.normalizePropertyField(field);
        if (!key) return [];

        const map = this.propertyRows.value;
        if (!Array.isArray(map[key])) {
            const obj = this.parsePropertyJson(this.form?.value ? this.form.value[key] : '');
            map[key] = Object.keys(obj).map((k) => ({
                key: k,
                value: obj[k] == null ? '' : String(obj[k])
            }));
        }
        return map[key];
    }

    syncPropertyJson(field) {
        const key = this.normalizePropertyField(field);
        if (!key || !this.form?.value) return;

        const rows = this.getPropertyRows(key);
        const obj = {};
        for (let i = 0; i < rows.length; i += 1) {
            const row = rows[i] || {};
            const k = String(row.key || '').trim();
            if (!k) continue;
            obj[k] = row.value == null ? '' : String(row.value);
        }
        this.form.value[key] = Object.keys(obj).length > 0 ? JSON.stringify(obj) : '';
    }

    addPropertyRow(field) {
        const rows = this.getPropertyRows(field);
        rows.push({ key: '', value: '' });
        this.syncPropertyJson(field);
    }

    removePropertyRow(field, index) {
        const rows = this.getPropertyRows(field);
        if (index < 0 || index >= rows.length) return;
        rows.splice(index, 1);
        this.syncPropertyJson(field);
    }

    async load() {
        const url = new URL(window.location.href);
        const id = parseInt(url.searchParams.get('id') || '0', 10);
        this.readOnly.value = (url.searchParams.get('md') || '').toLowerCase() === 'view';

        const params = { id };
        for (const [key, value] of url.searchParams.entries()) {
            if (key !== 'id' && key !== 'handler') {
                params[key] = value;
            }
        }

        try {
            const res = await axios.get(this.dataHandler, { params });
            if (res.data.code === 0 || res.data.code === '0') {
                const d = res.data.data || {};
                this.form.value = { ...this.form.value, ...d };
                this.propertyRows.value = {};

                if (typeof d.isTop !== 'undefined') this.form.value.isTop = !!d.isTop;
                this.sanitizeAllStaticSelectValues();
                this.sanitizeAllDynamicSelectValues();
                this.sanitizeAllStaticTreeSelectValues();
                this.sanitizeAllRemoteTreeSelectValues();
                this.originalForm.value = JSON.parse(JSON.stringify(this.form.value));
            } else {
                console.error('Load failed:', res.data);
                this.error.value = res.data.msg || '加载失败';
            }
        } catch (e) {
            console.error('Load exception:', e);
            this.error.value = '加载异常: ' + (e.message || e);
        }
    }

    async close(data = {}) {
        const closeData = (data && typeof data === 'object') ? data : {};
        if (this.isDirty.value && !this.readOnly.value && !this.success.value && !closeData.saved) {
            try {
                const confirmZIndex = (EleManager?.Instance && typeof EleManager.Instance.resolvePopupZIndex === 'function')
                    ? EleManager.Instance.resolvePopupZIndex(120000, 10)
                    : 120000;
                await EleManager.confirm('数据已修改，确定关闭？', '提示', { zIndex: confirmZIndex });
            } catch {
                return;
            }
        }
        try {
            if (EleManager && typeof EleManager.closePage === 'function') {
                EleManager.closePage(closeData);
                return;
            }
        } catch {}
        try { EleManager.closeDrawer(); } catch {}
    }

    /** 刷新当前表单里所有内嵌 EleList（重置分页 + 重新拉数据）。forceReset=true 时强制从 pageIndex=0 开始。**/
    async refreshAllLists(forceReset = true) {
        const map = this.eleLists?.value ? this.eleLists.value : null;
        if (!map) return;
        const keys = Object.keys(map);
        if (!keys.length) return;
        for (const key of keys) {
            try {
                const st = this.eleListState(key);
                if (!st || !st.dataHandler) continue;
                if (forceReset) {
                    st.pageIndex = 0;
                    st.items = [];
                    st.finished = false;
                    st.total = 0;
                }
                await this.loadEleList(key, !!forceReset);
            } catch (e) {
                console.warn('[EleForm.refreshAllLists] key=', key, '异常', e);
            }
        }
        await (Vue && typeof Vue.nextTick === 'function' ? Vue.nextTick() : Promise.resolve());
    }

    async onCloseClick() {
        return this.close();
    }

    formatServerError(actionText, responseData, fallbackText) {
        const action = actionText || '操作失败';
        const fallback = fallbackText || action;
        const data = responseData && typeof responseData === 'object' ? responseData : {};
        const msg = (data.msg || data.info || data.message || data.error || '').toString().trim();
        const code = (data.code === 0 || data.code) ? `${data.code}` : '';

        if (msg && code) return `${action}：${msg}（${code}）`;
        if (msg) return `${action}：${msg}`;
        if (code) return `${action}（${code}）`;
        return fallback;
    }

    async save(options = {}) {
        if (this.readOnly.value) return false;
        const { closeAfterSave = true, newAfterSave = false } = options || {};

        if (this.formRef.value) {
            try {
                await this.formRef.value.validate();
            } catch (e) {
                return false;
            }
        }

        this.error.value = '';
        this.success.value = '';
        this.saving.value = true;
        try {
            const res = await axios.post(this.saveHandler, this.form.value, {
                headers: { 'RequestVerificationToken': EleManager.getCsrfToken() }
            });
            if (res.data.code === 0 || res.data.code === '0') {
                // 兼容三种命令返回：res.data.command / res.data.data.command / res.data.data.commands 数组
                const payloads = [res.data, res.data ? res.data.data : null].filter(Boolean);
                const hasAnyCmd = payloads.some(p => (p && (typeof p.command === 'string' || Array.isArray(p.commands))));
                if (typeof EleManager !== 'undefined' && EleManager) {
                    for (const p of payloads) {
                        if (!p || typeof p !== 'object') continue;
                        if (typeof p.command === 'string') {
                            try { EleManager.executeServerCommand(p); } catch (_) { }
                        }
                        if (Array.isArray(p.commands)) {
                            for (const item of p.commands) {
                                try { EleManager.executeServerCommand(item); } catch (_) { }
                            }
                        }
                    }
                }

                this.originalForm.value = JSON.parse(JSON.stringify(this.form.value));
                if (!hasAnyCmd) {
                    this.success.value = '保存成功';
                    EleManager.showSuccess(res.data.msg || '保存成功');
                }

                if (newAfterSave) {
                    const url = new URL(window.location.href);
                    url.searchParams.set('id', '0');
                    url.searchParams.set('md', 'new');
                    window.location.href = url.toString();
                    return true;
                }

                if (closeAfterSave) {
                    await this.close({ saved: true, _data: res.data });
                    // 子表单关闭后：再给父级 Drawer 广播一次刷新信号（双保险：即使 RefreshData(Parent) 没被 Utils 命中，父级也能通过消息收到）
                    try {
                        const broascastPayload = { __eleRefreshData: true, __attsMoveToRefresh__: true, __eleFormRefreshAll: true, saved: true };
                        if (window.parent && window.parent !== window) {
                            try { window.parent.postMessage(broascastPayload, '*'); } catch (_) { }
                        }
                        if (window.top && window.top !== window) {
                            try { window.top.postMessage(broascastPayload, '*'); } catch (_) { }
                        }
                        if (typeof window.dispatchEvent === 'function') {
                            window.dispatchEvent(new CustomEvent('eleui:refresh-data', { detail: { __eleFormRefreshAll: true, scope: 'parent' } }));
                        }
                    } catch (_) { }
                }
                return true;
            }

            const failMsg = this.formatServerError('保存失败', res.data, '保存失败');
            this.error.value = failMsg;
            EleManager.showError(failMsg);
            return false;
        } catch (e) {
            const status = e?.response?.status;
            const fallback = status ? `保存失败（HTTP ${status}）` : '保存失败，请稍后重试';
            const failMsg = this.formatServerError('保存失败', e?.response?.data, fallback);
            this.error.value = failMsg;
            EleManager.showError(failMsg);
            return false;
        } finally {
            this.saving.value = false;
        }
    }

    async invokeCommand(commandName) {
        if (!commandName) return;
        const name = commandName;

        if (name === 'Save') return this.save();
        if (name === 'SaveClose') return this.save({ closeAfterSave: true });
        if (name === 'SaveNew') return this.save({ closeAfterSave: false, newAfterSave: true });
        if (name === 'Close' || name === 'Cancel') return this.close();

        await this.postHandler(name);
    }

    async postHandler(name) {
        this.error.value = '';
        this.success.value = '';
        this.saving.value = true;
        try {
            const url = new URL(window.location.href);
            url.searchParams.set('handler', name);
            const postUrl = `${url.pathname}${url.search}`;

            const res = await axios.post(postUrl, this.form.value, {
                headers: { 'RequestVerificationToken': EleManager.getCsrfToken() }
            });
            if (res && (res.data && (res.data.code === 0 || res.data.code === '0'))) {
                // 兼容三种命令返回：
                //  1) res.data.command（单命令）
                //  2) res.data.data.command（单命令包在 data）
                //  3) res.data.data.commands（数组，例如 BuildCommandsResult(Toast+CloseDrawer+RefreshData)）
                const payloads = [res.data, res.data ? res.data.data : null].filter(Boolean);
                const hasAnyCmd = payloads.some(p => (p && (typeof p.command === 'string' || Array.isArray(p.commands))));
                if (typeof EleManager !== 'undefined' && EleManager) {
                    for (const p of payloads) {
                        if (!p || typeof p !== 'object') continue;
                        if (typeof p.command === 'string') {
                            try { EleManager.executeServerCommand(p); } catch (_) { }
                        }
                        if (Array.isArray(p.commands)) {
                            for (const item of p.commands) {
                                try { EleManager.executeServerCommand(item); } catch (_) { }
                            }
                        }
                    }
                }
                if (!hasAnyCmd) {
                    this.success.value = res.data.msg || '操作成功';
                    EleManager.showSuccess(res.data.msg || '操作成功');
                }
            } else {
                this.error.value = this.formatServerError('操作失败', res?.data, '操作失败');
                EleManager.showError(this.error.value);
            }
        } catch (e) {
            const status = e?.response?.status;
            const fallback = status ? `操作失败（HTTP ${status}）` : '操作失败，请稍后重试';
            this.error.value = this.formatServerError('操作失败', e?.response?.data, fallback);
            EleManager.showError(this.error.value);
        } finally {
            this.saving.value = false;
        }
    }
}

/**
 * EleForm 专用 message 订阅处理：
 *  1) 收到 __eleRefreshData / __attsMoveToRefresh__ / __eleFormRefreshAll 广播 → 重载主数据 + 刷新全部内嵌 EleList；
 *     （DrawerCloseAction.RefreshData 与 RefreshData(Parent) 命令都会广播这些标记）
 *  2) 收到 __elePageClose 广播 → 关闭父级 Drawer（本页就是 Drawer iframe 时）；
 */
const eleFormMessageMethods = {
    messageHandler(e) {
        if (!e) return;
        const payload = e && e.data;
        if (!payload || typeof payload !== 'object') return;

        // 1) 关闭 Drawer：适用于本页运行在 Drawer iframe 内，上游仅发消息的场景
        if (payload.__elePageClose === true) {
            try { EleManager.closeDrawer(); } catch (_) { }
            return;
        }

        // 2) 刷新数据：任意一个刷新标记命中 → 表单主数据 + 全部内嵌 EleList 一起刷新
        const needRefresh = payload.__eleRefreshData === true
            || payload.__attsMoveToRefresh__ === true
            || payload.__eleFormRefreshAll === true
            || payload.needsRefresh === true;
        if (!needRefresh) return;

        (async () => {
            try {
                // 优先调外部 form.load（如果 EleForm 被挂了 load 方法）
                if (typeof this.load === 'function') {
                    try { await this.load(); } catch (e) { console.warn('[EleForm.messageHandler] form.load 异常', e); }
                }
                // 必走：刷新所有内嵌 EleList（检查对象 / 附件 / CRM 联系人...）
                if (typeof this.refreshAllLists === 'function') {
                    try { await this.refreshAllLists(true); } catch (e) { console.warn('[EleForm.messageHandler] refreshAllLists 异常', e); }
                }
            } catch (_) { }
        })();
    }
};

Object.assign(EleForm.prototype, pickerMethods, uploadMethods, controlStateMethods, treePickerMethods, listPickerMethods, eleFormMessageMethods);
