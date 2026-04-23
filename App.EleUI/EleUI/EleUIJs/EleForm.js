import { initSelectorState, selectorMethods } from './form/selectorMethods.js';
import { initUploadState, uploadMethods } from './form/uploadMethods.js';
import { initControlState, controlStateMethods } from './form/controlStateMethods.js';

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

        // Split domains: selector, upload, control linkage
        initSelectorState(this, Vue);
        initUploadState(this, Vue);
        initControlState(this, Vue);
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

                for (const key of Object.keys(this.form.value)) {
                    if (key.endsWith('Id')) {
                        const v = this.form.value[key];
                        if (v !== null && typeof v !== 'undefined') {
                            this.form.value[key] = v.toString();
                        }
                    }
                }

                if (typeof d.isTop !== 'undefined') this.form.value.isTop = !!d.isTop;
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
                await EleManager.confirm('数据已修改，确定关闭？');
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
                this.success.value = '保存成功';
                EleManager.showSuccess(res.data.msg || '保存成功');
                this.originalForm.value = JSON.parse(JSON.stringify(this.form.value));
                if (newAfterSave) {
                    const url = new URL(window.location.href);
                    url.searchParams.set('id', '0');
                    url.searchParams.set('md', 'new');
                    window.location.href = url.toString();
                    return true;
                }

                if (closeAfterSave) {
                    await this.close({ saved: true });
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
                this.success.value = res.data.msg || '操作成功';
                EleManager.showSuccess(res.data.msg || '操作成功');
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

Object.assign(EleForm.prototype, selectorMethods, uploadMethods, controlStateMethods);
