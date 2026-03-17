/**
 * EleManager - Singleton UI Manager
 * Handles global UI interactions like Toast, MessageBox, etc.
 */
class EleManagerCore {
    constructor() {
        if (EleManagerCore.instance) {
            return EleManagerCore.instance;
        }
        EleManagerCore.instance = this;
        
        // Initialize Element Plus shortcuts
        this.ElMessage = window.ElementPlus ? window.ElementPlus.ElMessage : { 
            success: (msg) => console.log('Success:', msg),
            error: (msg) => console.error('Error:', msg),
            warning: (msg) => console.warn('Warning:', msg),
            info: (msg) => console.info('Info:', msg)
        };
        
        this.ElMessageBox = window.ElementPlus ? window.ElementPlus.ElMessageBox : {
            confirm: () => Promise.resolve(),
            alert: () => Promise.resolve(),
            prompt: () => Promise.resolve()
        };

        this.ElNotification = window.ElementPlus ? window.ElementPlus.ElNotification : {
            success: () => {},
            error: () => {}
        };

        this.ElLoading = window.ElementPlus ? window.ElementPlus.ElLoading : {
            service: () => ({ close: () => {} })
        };

        this._loadingStyleInjected = false;
        this._loading = null;
        this._drawerStyleInjected = false;
        this._drawerDefaults = {
            title: '服务端 Drawer',
            content: '',
            html: false,
            url: '',
            size: '40%',
            direction: 'rtl',
            withHeader: true,
            showClose: true,
            modal: true,
            closeOnClickModal: true,
            destroyOnClose: false,
            showFooter: false,
            footerButtons: [],
            footerAlign: 'end',
            clientCloseHandler: null,
            beforeCloseHandler: null,
            serverCloseHandler: ''
        };
        this._drawerHost = null;

        // Server command whitelist, keys are allowed command names.
        this._serverCommandHandlers = {
            notify: (args) => {
                const message = this._safeText(args?.message, 300) || '提示信息';
                const title = this._safeText(args?.title, 60) || '提示';
                const type = this._safeType(args?.type, ['success', 'warning', 'info', 'error'], 'info');
                return this.notify(message, type, { title });
            },
            message: (args) => {
                const message = this._safeText(args?.message || args?.text, 300);
                if (!message) throw new Error('message command requires message');
                const type = this._safeType(args?.type, ['success', 'warning', 'info', 'error'], 'info');
                return this.message(message, type);
            },
            toast: (args) => {
                const message = this._safeText(args?.message || args?.text, 300);
                if (!message) throw new Error('toast command requires message');
                const type = this._safeType(args?.type, ['success', 'warning', 'info', 'error'], 'info');
                return this.toast(message, type);
            },
            messagebox: (args) => {
                return this.openMessageBoxFromServer(args || {});
            },
            inputbox: (args) => {
                return this.openInputBoxFromServer(args || {});
            },
            showloading: (args) => {
                const text = this._safeText(args?.text, 80) || '加载中...';
                return this.showLoading(text);
            },
            closeloading: () => {
                this.closeLoading();
                return true;
            },
            opendrawer: (args) => {
                return this.openDrawer({
                    title: this._safeText(args?.title, 80),
                    content: this._safeText(args?.content, 2000),
                    url: this._safeText(args?.url, 300),
                    size: this._safeText(args?.size, 20),
                    direction: this._safeText(args?.direction, 10),
                    showFooter: args?.showFooter,
                    footerButtons: Array.isArray(args?.footerButtons) ? args.footerButtons : undefined,
                    footerAlign: this._safeText(args?.footerAlign, 20),
                    clientCloseHandler: this._safeText(args?.clientCloseHandler, 120),
                    serverCloseHandler: this._safeText(args?.serverCloseHandler, 80)
                });
            },
            closedrawer: () => {
                return this.closeDrawer();
            }
        };
    }

    _safeText(value, maxLen = 300) {
        if (typeof value !== 'string') return '';
        return value.trim().slice(0, maxLen);
    }

    _safeType(value, allowed, fallback) {
        const t = typeof value === 'string' ? value.toLowerCase() : '';
        return allowed.includes(t) ? t : fallback;
    }

    /**
     * Show success message
     * @param {string} msg 
     */
    showSuccess(msg) {
        this.ElMessage.success(msg || '操作成功');
    }

    /**
     * Show error message
     * @param {string} msg 
     */
    showError(msg) {
        this.ElMessage.error(msg || '操作失败');
    }

    /**
     * Show warning message
     * @param {string} msg 
     */
    showWarning(msg) {
        this.ElMessage.warning(msg);
    }

    /**
     * Show info message
     * @param {string} msg 
     */
    showInfo(msg) {
        this.ElMessage.info(msg);
    }

    /**
     * Generic message helper
     * @param {string} msg
     * @param {'success'|'warning'|'info'|'error'} type
     * @param {object} options
     */
    message(msg, type = 'info', options = {}) {
        return this.ElMessage({ message: msg, type, ...options });
    }

    toast(msg, type = 'info', options = {}) {
        return this.message(msg, type, options);
    }

    /**
     * Confirm action
     * @param {string} msg 
     * @param {string} title 
     * @returns {Promise}
     */
    confirm(msg, title = '提示') {
        return this.ElMessageBox.confirm(msg, title, {
            confirmButtonText: '确定',
            cancelButtonText: '取消',
            type: 'warning'
        });
    }

    /**
     * Alert dialog
     * @param {string} msg
     * @param {string} title
     * @param {object} options
     */
    alert(msg, title = '提示', options = {}) {
        return this.ElMessageBox.alert(msg, title, {
            confirmButtonText: '确定',
            ...options
        });
    }

    /**
     * Prompt dialog
     * @param {string} msg
     * @param {string} title
     * @param {object} options
     */
    prompt(msg, title = '请输入', options = {}) {
        return this.ElMessageBox.prompt(msg, title, {
            confirmButtonText: '确定',
            cancelButtonText: '取消',
            ...options
        });
    }

    /**
     * Generic notification helper
     * @param {string|object} msgOrOptions
     * @param {'success'|'warning'|'info'|'error'} type
     * @param {object} options
     */
    notify(msgOrOptions, type = 'info', options = {}) {
        if (typeof msgOrOptions === 'object' && msgOrOptions !== null) {
            return this.ElNotification(msgOrOptions);
        }
        return this.ElNotification({
            title: options.title || '提示',
            message: msgOrOptions,
            type,
            ...options
        });
    }

    notifySuccess(msg, options = {}) {
        return this.notify(msg || '操作成功', 'success', options);
    }

    notifyError(msg, options = {}) {
        return this.notify(msg || '操作失败', 'error', options);
    }

    notifyWarning(msg, options = {}) {
        return this.notify(msg || '请注意当前操作', 'warning', options);
    }

    notifyInfo(msg, options = {}) {
        return this.notify(msg || '提示信息', 'info', options);
    }

    /**
     * Open fullscreen loading (singleton)
     * @param {string} text
     * @param {object} options
     */
    showLoading(text = '加载中...', options = {}) {
        this.closeLoading();
        this._ensureLoadingStyle();

        const { customClass: optionClass, ...restOptions } = options || {};
        const customClass = optionClass
            ? `ele-manager-loading ${optionClass}`
            : 'ele-manager-loading';

        this._loading = this.ElLoading.service({
            lock: true,
            text,
            background: 'rgba(0, 0, 0, 0.35)',
            customClass,
            ...restOptions
        });
        return this._loading;
    }

    _ensureLoadingStyle() {
        if (this._loadingStyleInjected) return;
        this._loadingStyleInjected = true;

        const styleId = 'ele-manager-loading-style';
        if (document.getElementById(styleId)) return;

        const style = document.createElement('style');
        style.id = styleId;
        style.textContent = `
.el-loading-mask.ele-manager-loading .el-loading-spinner .el-loading-text {
    color: #fff !important;
    text-shadow: 0 1px 2px rgba(0, 0, 0, 0.25);
}
.el-loading-mask.ele-manager-loading .el-loading-spinner .circular {
    --el-color-primary: #fff;
}
.el-loading-mask.ele-manager-loading .el-loading-spinner .path {
    stroke: #fff !important;
}
`;
        document.head.appendChild(style);
    }

    /**
     * Close fullscreen loading
     */
    closeLoading() {
        if (this._loading && typeof this._loading.close === 'function') {
            this._loading.close();
        }
        this._loading = null;
    }

    setDrawer(options = {}) {
        this._drawerDefaults = {
            ...this._drawerDefaults,
            ...(options && typeof options === 'object' ? options : {})
        };
        return true;
    }

    // Backward compatibility for old API name.
    setDrawerDefaults(options = {}) {
        return this.setDrawer(options);
    }

    _ensureDrawerHost() {
        if (this._drawerHost || !window.Vue || !window.ElementPlus) {
            return !!this._drawerHost;
        }

        this._ensureDrawerStyle();

        const mountEl = document.createElement('div');
        mountEl.id = 'ele-manager-drawer-host';
        document.body.appendChild(mountEl);

        const manager = this;
        const { createApp, reactive } = window.Vue;
        const state = reactive({
            visible: false,
            title: '',
            content: '',
            html: false,
            url: '',
            size: '40%',
            direction: 'rtl',
            withHeader: true,
            showClose: true,
            modal: true,
            closeOnClickModal: true,
            destroyOnClose: false,
            showFooter: false,
            footerButtons: [],
            footerAlignClass: 'justify-end',
            clientCloseHandler: null,
            beforeCloseHandler: null,
            serverCloseHandler: ''
        });

        const app = createApp({
            data() {
                return { state };
            },
            methods: {
                async beforeClose(done) {
                    if (!state.beforeCloseHandler) {
                        done();
                        return;
                    }
                    const fn = manager._resolveHandler(state.beforeCloseHandler);
                    if (!fn) {
                        done();
                        return;
                    }
                    try {
                        await fn(done);
                    } catch (err) {
                        console.error('beforeCloseHandler error:', err);
                        // 出错时不自动关闭，交由用户决定
                    }
                },
                async onClosed() {
                    const action = 'close';
                    if (state.clientCloseHandler) {
                        const fn = manager._resolveHandler(state.clientCloseHandler);
                        if (fn) {
                            try { fn(action); } catch (error) { console.error(error); }
                        }
                    }
                    if (state.serverCloseHandler) {
                        try {
                            await manager._postServerHandler(state.serverCloseHandler, { action });
                        } catch (error) {
                            console.error('drawer server close callback failed:', error);
                        }
                    }
                },
                onFooterClick(btn) {
                    if (btn && btn.handler) {
                        const fn = manager._resolveHandler(btn.handler);
                        if (fn) {
                            try { fn(btn.action || 'click', btn); } catch (error) { console.error(error); }
                        }
                    }
                    if (!btn || !btn.action || btn.action === 'close') {
                        state.visible = false;
                    }
                }
            },
            template: `
<el-drawer
    v-model="state.visible"
    class="ele-manager-drawer"
    :title="state.title"
    :direction="state.direction"
    :before-close="state.beforeCloseHandler ? beforeClose : undefined"
    :size="state.size"
    :with-header="state.withHeader"
    :show-close="state.showClose"
    :modal="state.modal"
    :close-on-click-modal="state.closeOnClickModal"
    :destroy-on-close="state.destroyOnClose"
    @closed="onClosed"
>
    <iframe
        v-if="state.url"
        :src="state.url"
        style="width:100%;height:100%;border:0;min-height:280px;"
    ></iframe>
    <div v-else-if="state.html" v-html="state.content"></div>
    <div v-else class="space-y-3">
        <p>{{ state.content || '暂无内容' }}</p>
    </div>

    <template #footer v-if="state.showFooter">
        <div :class="['w-full flex items-center gap-2', state.footerAlignClass]">
            <el-button
                v-for="(btn, index) in state.footerButtons"
                :key="index"
                :type="btn.type || 'default'"
                :plain="!!btn.plain"
                @click="onFooterClick(btn)"
            >
                {{ btn.text || '按钮' }}
            </el-button>
        </div>
    </template>
</el-drawer>`
        });

        app.use(window.ElementPlus);
        if (window.ElementPlusIconsVue) {
            for (const [key, component] of Object.entries(window.ElementPlusIconsVue)) {
                app.component(key, component);
            }
        }

        app.mount(mountEl);
        this._drawerHost = { state, app, mountEl };
        return true;
    }

    _ensureDrawerStyle() {
        if (this._drawerStyleInjected) return;
        this._drawerStyleInjected = true;

        const styleId = 'ele-manager-drawer-style';
        if (document.getElementById(styleId)) return;

        const style = document.createElement('style');
        style.id = styleId;
        style.textContent = `
.ele-manager-drawer .el-drawer__title,
.el-drawer.ele-manager-drawer .el-drawer__title,
.ele-manager-drawer .el-drawer__header .el-drawer__title {
    font-weight: 700 !important;
}
`;
        document.head.appendChild(style);
    }

    _toBool(value, fallback = true) {
        if (typeof value === 'boolean') return value;
        if (typeof value === 'string') {
            const v = value.trim().toLowerCase();
            if (v === 'true') return true;
            if (v === 'false') return false;
        }
        return fallback;
    }

    _resolveHandler(handler) {
        if (typeof handler === 'function') return handler;
        if (typeof handler === 'string') return this._getGlobalFunction(handler);
        return null;
    }

    openDrawer(options = {}) {
        try {
            this._ensureDrawerHost();
            if (!this._drawerHost) {
                throw new Error('drawer host is not initialized');
            }

            const merged = {
                ...this._drawerDefaults,
                ...(options && typeof options === 'object' ? options : {})
            };
            const align = this._safeType(merged.footerAlign, ['start', 'center', 'end', 'space-between'], 'end');
            const alignMap = {
                start: 'justify-start',
                center: 'justify-center',
                end: 'justify-end',
                'space-between': 'justify-between'
            };

            const buttons = Array.isArray(merged.footerButtons) ? merged.footerButtons : [];
            this._drawerHost.state.title = this._safeText(merged.title, 80) || '服务端 Drawer';
            this._drawerHost.state.content = this._safeText(merged.content, 2000);
            this._drawerHost.state.html = this._toBool(merged.html, false);
            this._drawerHost.state.url = this._safeText(merged.url, 500);
            this._drawerHost.state.size = this._safeText(merged.size, 20) || '40%';
            this._drawerHost.state.direction = this._safeType(merged.direction, ['ltr', 'rtl', 'ttb', 'btt'], 'rtl');
            this._drawerHost.state.withHeader = this._toBool(merged.withHeader, true);
            this._drawerHost.state.showClose = this._toBool(merged.showClose, true);
            this._drawerHost.state.modal = this._toBool(merged.modal, true);
            this._drawerHost.state.closeOnClickModal = this._toBool(merged.closeOnClickModal, true);
            this._drawerHost.state.destroyOnClose = this._toBool(merged.destroyOnClose, false);
            this._drawerHost.state.showFooter = this._toBool(merged.showFooter, buttons.length > 0);
            this._drawerHost.state.footerButtons = buttons;
            this._drawerHost.state.footerAlignClass = alignMap[align] || 'justify-end';
            this._drawerHost.state.clientCloseHandler = typeof merged.clientCloseHandler === 'function'
                ? merged.clientCloseHandler
                : this._safeText(merged.clientCloseHandler, 120);
            this._drawerHost.state.beforeCloseHandler = typeof merged.beforeCloseHandler === 'function'
                ? merged.beforeCloseHandler
                : (merged.beforeCloseHandler ? this._safeText(merged.beforeCloseHandler, 120) : null);
            this._drawerHost.state.serverCloseHandler = this._safeText(merged.serverCloseHandler, 80);
            this._drawerHost.state.visible = true;
            return true;
        } catch (err) {
            console.error('openDrawer failed:', err);
            return false;
        }
    }

    closeDrawer() {
        try {
            this._ensureDrawerHost();
            if (this._drawerHost) {
                this._drawerHost.state.visible = false;
            }
            return true;
        } catch (err) {
            console.error('closeDrawer failed:', err);
            return false;
        }
    }

    _getGlobalFunction(path) {
        if (typeof path !== 'string' || !path.trim()) return null;
        const keys = path.split('.').map(s => s.trim()).filter(Boolean);
        let cur = window;
        for (const k of keys) {
            cur = cur?.[k];
        }
        return typeof cur === 'function' ? cur : null;
    }

    async _postServerHandler(serverHandler, payload) {
        if (!serverHandler || typeof serverHandler !== 'string') return null;
        const url = '?handler=' + encodeURIComponent(serverHandler.trim());
        const res = await this.request(url, payload, 'POST');
        if (res && (res.code === 0 || res.code === '0')) {
            const cmdPayload = res.data;
            if (cmdPayload && typeof cmdPayload === 'object' && cmdPayload.command) {
                this.executeServerCommand(cmdPayload);
            }
        }
        return res;
    }

    openMessageBoxFromServer(args = {}) {
        const text = this._safeText(args.text || args.message, 500) || '确认执行此操作吗？';
        const title = this._safeText(args.title, 80) || '提示';
        const type = this._safeType(args.type, ['success', 'warning', 'info', 'error'], 'info');
        const comfirmButtonText = this._safeText(args.comfirmButtonText || args.confirmButtonText, 20) || '确定';
        const cancelButtonText = this._safeText(args.cancelButtonText, 20) || '取消';
        const isAlert = !!args.isAlert || !cancelButtonText;
        const clientHandler = this._safeText(args.clientHandler, 120);
        const serverHandler = this._safeText(args.serverHandler, 80);

        const onConfirm = async () => {
            const clientFn = this._getGlobalFunction(clientHandler);
            if (clientFn) {
                try { clientFn('confirm', args); } catch (err) { console.error(err); }
            }
            if (serverHandler) {
                await this._postServerHandler(serverHandler, {
                    action: 'confirm',
                    clientHandler,
                    text,
                    title,
                    type
                });
            }
            return true;
        };

        const onCancel = async () => {
            const clientFn = this._getGlobalFunction(clientHandler);
            if (clientFn) {
                try { clientFn('cancel', args); } catch (err) { console.error(err); }
            }
            if (serverHandler) {
                await this._postServerHandler(serverHandler, {
                    action: 'cancel',
                    clientHandler,
                    text,
                    title,
                    type
                });
            }
            return false;
        };

        if (isAlert) {
            return this.ElMessageBox.alert(text, title, {
                type,
                confirmButtonText: comfirmButtonText
            }).then(onConfirm);
        }

        return this.ElMessageBox.confirm(text, title, {
            type,
            confirmButtonText: comfirmButtonText,
            cancelButtonText
        }).then(onConfirm).catch(onCancel);
    }

    openInputBoxFromServer(args = {}) {
        const text = this._safeText(args.text || args.message, 500) || '请输入内容';
        const title = this._safeText(args.title, 80) || '请输入';
        const type = this._safeType(args.type, ['success', 'warning', 'info', 'error'], 'info');
        const comfirmButtonText = this._safeText(args.comfirmButtonText || args.confirmButtonText, 20) || '确定';
        const cancelButtonText = this._safeText(args.cancelButtonText, 20) || '取消';
        const inputPlaceholder = this._safeText(args.inputPlaceholder, 120) || '请输入内容';
        const inputValue = this._safeText(args.inputValue, 500);
        const clientHandler = this._safeText(args.clientHandler, 120);
        const serverHandler = this._safeText(args.serverHandler, 80);
        const inputPattern = this._safeText(args.inputPattern, 300);
        const inputErrorMessage = this._safeText(args.inputErrorMessage, 200);

        const onConfirm = async (result) => {
            const value = typeof result?.value === 'string' ? result.value : '';
            const clientFn = this._getGlobalFunction(clientHandler);
            if (clientFn) {
                try { clientFn('confirm', value, args); } catch (err) { console.error(err); }
            }
            if (serverHandler) {
                await this._postServerHandler(serverHandler, {
                    action: 'confirm',
                    value,
                    clientHandler,
                    text,
                    title,
                    type
                });
            }
            return result;
        };

        const onCancel = async (err) => {
            const action = this._safeText(err?.action, 20) || 'cancel';
            const clientFn = this._getGlobalFunction(clientHandler);
            if (clientFn) {
                try { clientFn(action, '', args); } catch (error) { console.error(error); }
            }
            if (serverHandler) {
                await this._postServerHandler(serverHandler, {
                    action,
                    value: '',
                    clientHandler,
                    text,
                    title,
                    type
                });
            }
            return false;
        };

        const promptOptions = {
            type,
            confirmButtonText: comfirmButtonText,
            cancelButtonText,
            inputPlaceholder,
            inputValue
        };
        if (inputPattern) {
            promptOptions.inputPattern = new RegExp(inputPattern);
        }
        if (inputErrorMessage) {
            promptOptions.inputErrorMessage = inputErrorMessage;
        }
        return this.ElMessageBox.prompt(text, title, promptOptions).then(onConfirm).catch(onCancel);
    }

    /**
     * Get CSRF Token
     */
    getCsrfToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value
            || window.parent?.document?.querySelector('input[name="__RequestVerificationToken"]')?.value
            || window.top?.document?.querySelector('input[name="__RequestVerificationToken"]')?.value
            || '';
    }

    /**
     * Format Date
     */
    formatDate(s, type) {
        if (!s) return '';
        try {
            const d = new Date(s);
            if (isNaN(d.getTime())) return s;

            const pad = (n) => String(n).padStart(2, '0');
            const y = d.getFullYear();
            const m = pad(d.getMonth() + 1);
            const dd = pad(d.getDate());
            const hh = pad(d.getHours());
            const mm = pad(d.getMinutes());
            const ss = pad(d.getSeconds());

            if (type === 'Date') return `${y}-${m}-${dd}`;
            if (type === 'Time') return `${hh}:${mm}:${ss}`;
            if (type === 'DateTime') return `${y}-${m}-${dd} ${hh}:${mm}:${ss}`;
            
            return `${y}-${m}-${dd} ${hh}:${mm}`;
        } catch {
            return s;
        }
    }

    /**
     * 格式化枚举值
     * @param {any} val  原始枚举值
     * @param {Array<{Id:number,Value:any,Title:string}>} options 通过 EnumHelper.GetEnumInfos 输出的数组
     */
    formatEnum(val, options) {
        if (val === null || val === undefined) return '';
        if (!options || !Array.isArray(options)) return val;
        const item = options.find(o => o.Id == val || o.Value == val || o === val);
        return item ? item.Title : val;
    }

    /**
     * Request helper (wraps axios or fetch)
     * @param {string} url 
     * @param {object} data 
     * @param {string} method 
     */
    async request(url, data = null, method = 'POST') {
        try {
            const token = this.getCsrfToken();
            const headers = {
                'RequestVerificationToken': token,
                'X-Requested-With': 'XMLHttpRequest'
            };

            let response;
            if (window.axios) {
                response = await window.axios({
                    url,
                    method,
                    data,
                    headers
                });
                return response.data;
            } else {
                const res = await fetch(url, {
                    method,
                    headers: {
                        ...headers,
                        'Content-Type': 'application/json'
                    },
                    body: data ? JSON.stringify(data) : undefined
                });
                if (!res.ok) throw new Error(res.statusText);
                return await res.json();
            }
        } catch (err) {
            console.error(err);
            throw err;
        }
    }

    /**
     * Invoke server action and show notification
     * @param {string} url 
     * @param {string} successMsg 
     */
    invoke(url, successMsg = '操作成功') {
        this.request(url).then(res => {
            if (res.success || res.code === 0) {
                this.showSuccess(successMsg);
            } else {
                this.showError(res.msg || '操作失败');
            }
        }).catch(err => {
            this.showError('网络请求错误');
        });
    }

    /**
     * Execute server-pushed command in whitelist mode.
     * payload example: { command: 'notify', args: { type, title, message }, requestId, issuedAt }
     * @param {object} payload
     * @returns {boolean} true when executed, false when rejected
     */
    executeServerCommand(payload) {
        try {
            if (!payload || typeof payload !== 'object') {
                this.notifyWarning('无效的服务端命令');
                return false;
            }

            const commandName = this._safeText(payload.command, 50).toLowerCase();
            const args = payload.args && typeof payload.args === 'object' ? payload.args : {};
            if (!commandName) {
                this.notifyWarning('服务端命令为空');
                return false;
            }

            const handler = this._serverCommandHandlers[commandName];
            if (!handler) {
                this.notifyWarning(`不支持的服务端命令: ${commandName}`);
                return false;
            }

            handler(args);
            return true;
        } catch (err) {
            console.error('executeServerCommand failed:', err);
            this.notifyError('服务端命令执行失败');
            return false;
        }
    }
}

/**
 * EleManager - Static facade
 * Usage: EleManager.alert(...)
 */
class EleManager {
    static get _core() {
        if (!EleManager.__core) {
            EleManager.__core = new EleManagerCore();
        }
        return EleManager.__core;
    }

    static showSuccess(...args) { return EleManager._core.showSuccess(...args); }
    static showError(...args) { return EleManager._core.showError(...args); }
    static showWarning(...args) { return EleManager._core.showWarning(...args); }
    static showInfo(...args) { return EleManager._core.showInfo(...args); }
    static message(...args) { return EleManager._core.message(...args); }
    static toast(...args) { return EleManager._core.toast(...args); }
    static confirm(...args) { return EleManager._core.confirm(...args); }
    static alert(...args) { return EleManager._core.alert(...args); }
    static prompt(...args) { return EleManager._core.prompt(...args); }
    static notify(...args) { return EleManager._core.notify(...args); }
    static notifySuccess(...args) { return EleManager._core.notifySuccess(...args); }
    static notifyError(...args) { return EleManager._core.notifyError(...args); }
    static notifyWarning(...args) { return EleManager._core.notifyWarning(...args); }
    static notifyInfo(...args) { return EleManager._core.notifyInfo(...args); }
    static showLoading(...args) { return EleManager._core.showLoading(...args); }
    static closeLoading(...args) { return EleManager._core.closeLoading(...args); }
    static setDrawer(...args) { return EleManager._core.setDrawer(...args); }
    static setDrawerDefaults(...args) { return EleManager._core.setDrawerDefaults(...args); }
    static openDrawer(...args) { return EleManager._core.openDrawer(...args); }
    static closeDrawer(...args) { return EleManager._core.closeDrawer(...args); }
    static openMessageBoxFromServer(...args) { return EleManager._core.openMessageBoxFromServer(...args); }
    static openInputBoxFromServer(...args) { return EleManager._core.openInputBoxFromServer(...args); }
    static getCsrfToken(...args) { return EleManager._core.getCsrfToken(...args); }
    static formatDate(...args) { return EleManager._core.formatDate(...args); }
    static formatEnum(...args) { return EleManager._core.formatEnum(...args); }
    static request(...args) { return EleManager._core.request(...args); }
    static invoke(...args) { return EleManager._core.invoke(...args); }
    static executeServerCommand(...args) { return EleManager._core.executeServerCommand(...args); }
}

// Bridge module scope to global scope so Razor inline scripts can call EleManager.xxx directly.
if (typeof globalThis !== 'undefined') {
    globalThis.EleManager = EleManager;
}
