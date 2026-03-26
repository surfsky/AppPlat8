import { Utils } from "./App.EleUI.EleUIJs.Utils.js";
import { DrawerHelper } from "./App.EleUI.EleUIJs.DrawerHelper.js";

/***************************************************************
 * EleManager - Singleton UI Manager
 * Handles global UI interactions like Toast, MessageBox, etc.
 **************************************************************/
class EleManagerCore {
    constructor() {
        if (EleManagerCore.instance) {
            return EleManagerCore.instance;
        }
        EleManagerCore.instance = this;

        this._uiWindow = this._resolveUiHostWindow();
        const ep = this._uiWindow?.ElementPlus || window.ElementPlus;
        
        // Initialize Element Plus shortcuts
        this.ElMessage = ep ? ep.ElMessage : {
            success: (msg) => console.log('Success:', msg),
            error: (msg) => console.error('Error:', msg),
            warning: (msg) => console.warn('Warning:', msg),
            info: (msg) => console.info('Info:', msg)
        };
        
        this.ElMessageBox = ep ? ep.ElMessageBox : {
            confirm: () => Promise.resolve(),
            alert: () => Promise.resolve(),
            prompt: () => Promise.resolve()
        };

        this.ElNotification = ep ? ep.ElNotification : {
            success: () => {},
            error: () => {}
        };

        this.ElLoading = ep ? ep.ElLoading : {
            service: () => ({ close: () => {} })
        };

        this._loadingStyleInjected = false;
        this._loading = null;
        this._imageViewerContainer = null;
        this._imageViewerRender = null;
        this.drawerHelper = new DrawerHelper(this);

        // Server command whitelist, keys are allowed command names.
        this._serverCommandHandlers = {
            notify: (args) => {
                const message = Utils.safeText(args?.message, 300) || '提示信息';
                const title = Utils.safeText(args?.title, 60) || '提示';
                const type = Utils.safeType(args?.type, ['success', 'warning', 'info', 'error'], 'info');
                return this.notify(message, type, { title });
            },
            message: (args) => {
                const message = Utils.safeText(args?.message || args?.text, 300);
                if (!message) throw new Error('message command requires message');
                const type = Utils.safeType(args?.type, ['success', 'warning', 'info', 'error'], 'info');
                return this.message(message, type);
            },
            toast: (args) => {
                const message = Utils.safeText(args?.message || args?.text, 300);
                if (!message) throw new Error('toast command requires message');
                const type = Utils.safeType(args?.type, ['success', 'warning', 'info', 'error'], 'info');
                return this.toast(message, type);
            },
            messagebox: (args) => {
                return this.openMessageBoxFromServer(args || {});
            },
            inputbox: (args) => {
                return this.openInputBoxFromServer(args || {});
            },
            showloading: (args) => {
                const text = Utils.safeText(args?.text, 80) || '加载中...';
                return this.showLoading(text);
            },
            closeloading: () => {
                this.closeLoading();
                return true;
            },
            opendrawer: (args) => {
                return this.openDrawer({
                    title: Utils.safeText(args?.title, 80),
                    content: Utils.safeText(args?.content, 2000),
                    url: Utils.safeText(args?.url, 300),
                    size: Utils.safeText(args?.size, 20),
                    direction: Utils.safeText(args?.direction, 10),
                    showFooter: args?.showFooter,
                    footerButtons: Array.isArray(args?.footerButtons) ? args.footerButtons : undefined,
                    footerAlign: Utils.safeText(args?.footerAlign, 20),
                    closeHandler: Utils.safeText(args?.closeHandler, 120),
                    serverCloseHandler: Utils.safeText(args?.serverCloseHandler, 80)
                });
            },
            closedrawer: () => {
                return this.closeDrawer();
            }
        };
    }

    _resolveUiHostWindow() {
        try {
            if (window.top && window.top !== window && window.top.ElementPlus) {
                return window.top;
            }
        } catch {
            // Cross-origin top window; fallback to current window.
        }
        return window;
    }


    //--------------------------------------------------------------
    //  Message（Toast 顶部提示）
    //--------------------------------------------------------------
    /**
     * Generic message helper
     * @param {string} msg
     * @param {'success'|'warning'|'info'|'error'} type
     * @param {object} options
     */
    message(msg, type = 'info', options = {}) { return this.ElMessage({ message: msg, type, ...options });}
    showSuccess(msg) { this.ElMessage.success(msg || '操作成功'); }
    showError(msg) { this.ElMessage.error(msg || '操作失败'); }
    showWarning(msg) { this.ElMessage.warning(msg || '请注意当前操作'); }
    showInfo(msg) { this.ElMessage.info(msg || '提示信息'); }
    toast(msg, type = 'info', options = {}) { return this.message(msg, type, options);}


    //--------------------------------------------------------------
    // Notify (右侧通知)
    //--------------------------------------------------------------
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

    notifySuccess(msg, options = {}) { return this.notify(msg || '操作成功', 'success', options);}
    notifyError(msg, options = {}) { return this.notify(msg || '操作失败', 'error', options);}
    notifyWarning(msg, options = {}) { return this.notify(msg || '请注意当前操作', 'warning', options);}
    notifyInfo(msg, options = {}) { return this.notify(msg || '提示信息', 'info', options);}    

    //--------------------------------------------------------------
    // MessageBox (弹窗消息框)
    //--------------------------------------------------------------
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

    //--------------------------------------------------------------
    //  Loading
    //--------------------------------------------------------------
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

        const hostDocument = this._uiWindow?.document || document;

        const styleId = 'ele-manager-loading-style';
        if (hostDocument.getElementById(styleId)) return;

        const style = hostDocument.createElement('style');
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
        hostDocument.head.appendChild(style);
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

    //--------------------------------------------------------------
    //  ImageViewer
    //--------------------------------------------------------------
    openImageViewer(imageUrls = [], initialIndex = 0) {
        const hostWindow = this._uiWindow || window;
        const hostDocument = hostWindow.document || document;
        const ep = hostWindow.ElementPlus || window.ElementPlus;
        const vueRuntime = hostWindow.Vue || window.Vue;

        const list = (Array.isArray(imageUrls) ? imageUrls : [imageUrls])
            .map(item => (typeof item === 'string' ? item.trim() : ''))
            .filter(item => !!item);

        if (!list.length) {
            this.showWarning('暂无可预览图片');
            return false;
        }

        const ElImageViewer = ep?.ElImageViewer;
        const createVNode = vueRuntime?.createVNode || vueRuntime?.h;
        const render = vueRuntime?.render;
        if (!ElImageViewer || !createVNode || !render) {
            hostWindow.open(list[0], '_blank', 'noopener');
            return false;
        }

        this.closeImageViewer();

        const indexNum = Number.isFinite(Number(initialIndex)) ? Math.max(0, Math.floor(Number(initialIndex))) : 0;
        const normalizedIndex = indexNum >= list.length ? 0 : indexNum;

        const container = hostDocument.createElement('div');
        container.className = 'ele-image-viewer-host';
        hostDocument.body.appendChild(container);

        const vnode = createVNode(ElImageViewer, {
            urlList: list,
            initialIndex: normalizedIndex,
            teleported: false,
            hideOnClickModal: true,
            closeOnPressEscape: true,
            zIndex: 3000,
            onClose: () => this.closeImageViewer()
        });

        render(vnode, container);
        this._imageViewerContainer = container;
        this._imageViewerRender = render;
        return true;
    }

    closeImageViewer() {
        if (!this._imageViewerContainer) return;
        try {
            if (this._imageViewerRender) {
                this._imageViewerRender(null, this._imageViewerContainer);
            }
        } catch {}

        if (this._imageViewerContainer.parentNode) {
            this._imageViewerContainer.parentNode.removeChild(this._imageViewerContainer);
        }

        this._imageViewerContainer = null;
        this._imageViewerRender = null;
    }

    //------------------------------------------------------------
    // Drawer
    //------------------------------------------------------------
    setDrawer(options = {}) { return this.drawerHelper.setDefaults(options); }
    openDrawer(options = {}) { return this.drawerHelper.open(options);}
    closeDrawer() { return this.drawerHelper.close();}

    closePage(data = {}) {
        const result = {
            code: 0,
            message: 'closed',
            data: (data && typeof data === 'object') ? data : {},
            __elePageClose: true
        };

        const inFrame = (window.parent && window.parent !== window) || (window.top && window.top !== window);

        try {
            if (window.parent && window.parent !== window) {
                window.parent.postMessage(result, '*');
            }
        } catch {}

        try {
            if (window.top && window.top !== window && window.top !== window.parent) {
                window.top.postMessage(result, '*');
            }
        } catch {}

        // In iframe/drawer pages, let parent drawer listener close itself using payload.
        // Avoid immediate closeDrawer() here, otherwise payload may be lost due to race.
        if (inFrame) {
            return true;
        }

        try {
            this.closeDrawer();
            if (window.top && window.top.EleManager && window.top !== window) {
                window.top.EleManager.closeDrawer();
            }
        } catch {
            this.closeDrawer();
        }

        return true;
    }

    //--------------------------------------------------------------
    //  处理服务器端命令：打开消息对话框
    //--------------------------------------------------------------
    openMessageBoxFromServer(args = {}) {
        const text = Utils.safeText(args.text || args.message, 500) || '确认执行此操作吗？';
        const title = Utils.safeText(args.title, 80) || '提示';
        const type = Utils.safeType(args.type, ['success', 'warning', 'info', 'error'], 'info');
        const comfirmButtonText = Utils.safeText(args.comfirmButtonText || args.confirmButtonText, 20) || '确定';
        const cancelButtonText = Utils.safeText(args.cancelButtonText, 20) || '取消';
        const isAlert = !!args.isAlert || !cancelButtonText;
        const clientHandler = Utils.safeText(args.clientHandler, 120);
        const serverHandler = Utils.safeText(args.serverHandler, 80);

        const onConfirm = async () => {
            const clientFn = Utils.getGlobalFunction(clientHandler);
            if (clientFn) {
                try { clientFn('confirm', args); } catch (err) { console.error(err); }
            }
            if (serverHandler) {
                await this.postServerHandler(serverHandler, {
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
            const clientFn = Utils.getGlobalFunction(clientHandler);
            if (clientFn) {
                try { clientFn('cancel', args); } catch (err) { console.error(err); }
            }
            if (serverHandler) {
                await this.postServerHandler(serverHandler, {
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

    //--------------------------------------------------------------
    //  处理服务器端命令：打开输入对话框
    //--------------------------------------------------------------
    openInputBoxFromServer(args = {}) {
        const text = Utils.safeText(args.text || args.message, 500) || '请输入内容';
        const title = Utils.safeText(args.title, 80) || '请输入';
        const type = Utils.safeType(args.type, ['success', 'warning', 'info', 'error'], 'info');
        const comfirmButtonText = Utils.safeText(args.comfirmButtonText || args.confirmButtonText, 20) || '确定';
        const cancelButtonText = Utils.safeText(args.cancelButtonText, 20) || '取消';
        const inputPlaceholder = Utils.safeText(args.inputPlaceholder, 120) || '请输入内容';
        const inputValue = Utils.safeText(args.inputValue, 500);
        const clientHandler = Utils.safeText(args.clientHandler, 120);
        const serverHandler = Utils.safeText(args.serverHandler, 80);
        const inputPattern = Utils.safeText(args.inputPattern, 300);
        const inputErrorMessage = Utils.safeText(args.inputErrorMessage, 200);

        const onConfirm = async (result) => {
            const value = typeof result?.value === 'string' ? result.value : '';
            const clientFn = Utils.getGlobalFunction(clientHandler);
            if (clientFn) {
                try { clientFn('confirm', value, args); } catch (err) { console.error(err); }
            }
            if (serverHandler) {
                await this.postServerHandler(serverHandler, {
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
            const action = Utils.safeText(err?.action, 20) || 'cancel';
            const clientFn = Utils.getGlobalFunction(clientHandler);
            if (clientFn) {
                try { clientFn(action, '', args); } catch (error) { console.error(error); }
            }
            if (serverHandler) {
                await this.postServerHandler(serverHandler, {
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

    //--------------------------------------------------------------
    //  辅助方法
    //--------------------------------------------------------------
    /**
     * Post server handler with payload
     * @param {*} serverHandler 
     * @param {*} payload 
     * @returns 
     */
    async postServerHandler(serverHandler, payload) {
        if (!serverHandler || typeof serverHandler !== 'string') return null;
        const url = '?handler=' + encodeURIComponent(serverHandler.trim());
        const res = await Utils.request(url, payload, 'POST');
        if (res && (res.code === 0 || res.code === '0')) {
            const cmdPayload = res.data;
            if (cmdPayload && typeof cmdPayload === 'object' && cmdPayload.command) {
                this.executeServerCommand(cmdPayload);
            }
        }
        return res;
    }



    //--------------------------------------------------------------
    // 服务器端命令执行器
    //--------------------------------------------------------------
    /**
     * Invoke server action and show notification
     * @param {string} url 
     * @param {string} successMsg 
     */
    invoke(url, successMsg = '操作成功') {
        Utils.request(url).then(res => {
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

            const commandName = Utils.safeText(payload.command, 50).toLowerCase();
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

/***********************************************************************
 * EleManager - Static facade
 * Usage: EleManager.alert(...)
 **********************************************************************/
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
    static closePage(...args) { return EleManager._core.closePage(...args); }
    static openImageViewer(...args) { return EleManager._core.openImageViewer(...args); }
    static closeImageViewer(...args) { return EleManager._core.closeImageViewer(...args); }
    static openMessageBoxFromServer(...args) { return EleManager._core.openMessageBoxFromServer(...args); }
    static openInputBoxFromServer(...args) { return EleManager._core.openInputBoxFromServer(...args); }
    static getCsrfToken(...args) { return Utils.getCsrfToken(...args); }
    static formatDate(...args) { return Utils.formatDate(...args); }
    static formatEnum(...args) { return Utils.formatEnum(...args); }
    static request(...args) { return Utils.request(...args); }
    static invoke(...args) { return EleManager._core.invoke(...args); }
    static executeServerCommand(...args) { return EleManager._core.executeServerCommand(...args); }
}

// Bridge module scope to global scope so Razor inline scripts can call EleManager.xxx directly.
if (typeof globalThis !== 'undefined') {
    globalThis.EleManager = EleManager;
}
