import { Utils } from "./Utils.js";
import { DrawerHelper } from "./DrawerHelper.js";
import { themeMethods } from "./manager/themeMethods.js";
import { messageMethods } from "./manager/messageMethods.js";
import { loadingMethods } from "./manager/loadingMethods.js";
import { imageViewerMethods } from "./manager/imageViewerMethods.js";
import { navigationMethods } from "./manager/navigationMethods.js";
import { serverMethods } from "./manager/serverMethods.js";

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

        this.ElMessage = ep ? ep.ElMessage : {
            success: (msg) => console.log("Success:", msg),
            error: (msg) => console.error("Error:", msg),
            warning: (msg) => console.warn("Warning:", msg),
            info: (msg) => console.info("Info:", msg)
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
        this._themeStorageKey = "eleui-theme-mode";
        this._themeMediaQuery = null;
        this._themeListenerBound = false;
        this.drawerHelper = new DrawerHelper(this);
        this._controlState = {};

        this._serverCommandHandlers = {
            notify: (args) => {
                const message = Utils.safeText(args?.message, 300) || "提示信息";
                const title = Utils.safeText(args?.title, 60) || "提示";
                const type = Utils.safeType(args?.type, ["success", "warning", "info", "error"], "info");
                return this.notify(message, type, { title });
            },
            message: (args) => {
                const message = Utils.safeText(args?.message || args?.text, 300);
                if (!message) throw new Error("message command requires message");
                const type = Utils.safeType(args?.type, ["success", "warning", "info", "error"], "info");
                return this.message(message, type);
            },
            toast: (args) => {
                const message = Utils.safeText(args?.message || args?.text, 300);
                if (!message) throw new Error("toast command requires message");
                const type = Utils.safeType(args?.type, ["success", "warning", "info", "error"], "info");
                return this.toast(message, type);
            },
            messagebox: (args) => this.openMessageBoxFromServer(args || {}),
            inputbox: (args) => this.openInputBoxFromServer(args || {}),
            showloading: (args) => {
                const text = Utils.safeText(args?.text, 80) || "加载中...";
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
                    serverCloseHandler: Utils.safeText(args?.serverCloseHandler, 80),
                    closeAction: Utils.safeText(args?.closeAction, 20)
                });
            },
            closedrawer: () => this.closeDrawer(),
            setcontrol: (args) => this.setControl(args || {})
        };

        this._initThemeObserver();
        this.setTheme(this.getTheme());
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

    setDrawer(options = {}) { return this.drawerHelper.setDefaults(options); }
    setDrawerDefaults(options = {}) { return this.setDrawer(options); }
    openDrawer(options = {}) { return this.drawerHelper.open(options); }
    closeDrawer() { return this.drawerHelper.close(); }
}

Object.assign(
    EleManagerCore.prototype,
    themeMethods,
    messageMethods,
    loadingMethods,
    imageViewerMethods,
    navigationMethods,
    serverMethods
);

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
    static setControl(...args) { return EleManager._core.setControl(...args); }
    static getControlState(...args) { return EleManager._core.getControlState(...args); }
    static goto(...args) { return EleManager._core.goto(...args); }
    static changeMode(...args) { return EleManager._core.changeMode(...args); }
    static setTheme(...args) { return EleManager._core.setTheme(...args); }
    static getTheme(...args) { return EleManager._core.getTheme(...args); }
}

if (typeof globalThis !== "undefined") {
    globalThis.EleManager = EleManager;
}
