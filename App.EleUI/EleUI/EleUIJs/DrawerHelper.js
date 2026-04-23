import { Utils } from "./Utils.js";

export class DrawerHelper {
    constructor(manager) {
        this.manager = manager;
        this._styleInjected = false;
        this._hostWindow = this.getHostWindow();
        const hostWindow = this._hostWindow || window;
        if (!hostWindow.__eleManagerDrawerInstances) {
            hostWindow.__eleManagerDrawerInstances = [];
        }
        this._instances = hostWindow.__eleManagerDrawerInstances;
        this._defaults = {
            title: '服务端 Drawer',
            content: '',
            html: false,
            url: '',
            size: '',
            direction: 'rtl',
            withHeader: true,
            showClose: true,
            resizable: true,
            modal: true,
            closeOnClickModal: true,
            destroyOnClose: false,
            showFooter: false,
            footerButtons: [],
            footerAlign: 'end',
            closeConfirm: '',
            closeHandler: null,
            beforeCloseHandler: null,
            serverCloseHandler: '',
            closeAction: 'none'
        };
    }

    getHostWindow() {
        try {
            if (window.top && window.top.document && window.top.Vue && window.top.ElementPlus) {
                return window.top;
            }
        } catch {
            // Cross-origin or inaccessible top window.
        }
        return window;
    }

    setDefaults(options = {}) {
        this._defaults = {
            ...this._defaults,
            ...(options && typeof options === 'object' ? options : {})
        };
        return true;
    }

    ensureStyle(hostWindow = this._hostWindow || this.getHostWindow()) {
        if (this._styleInjected) return;
        this._styleInjected = true;

        const styleId = 'ele-manager-drawer-style';
        if (hostWindow.document.getElementById(styleId)) return;

        const style = hostWindow.document.createElement('style');
        style.id = styleId;
        style.textContent = `
.ele-manager-drawer .el-drawer__title,
.el-drawer.ele-manager-drawer .el-drawer__title,
.ele-manager-drawer .el-drawer__header .el-drawer__title {
    font-weight: 700 !important;
}

.ele-manager-drawer .el-drawer__header {
    margin-bottom: 0 !important;
}

.ele-manager-drawer-header {
    width: 100%;
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
}

.ele-manager-drawer-header.is-mobile {
    justify-content: flex-start;
    gap: 10px;
}

.ele-manager-drawer-title {
    font-weight: 700;
    font-size: 18px;
    line-height: 1.3;
}

.ele-manager-drawer-close-btn {
    border: 0;
    background: transparent;
    color: #606266;
    cursor: pointer;
    font-size: 22px;
    line-height: 1;
    padding: 0;
    width: 24px;
    height: 24px;
    display: inline-flex;
    align-items: center;
    justify-content: center;
}

.ele-manager-drawer-close-btn:hover {
    color: #303133;
}
`;
        hostWindow.document.head.appendChild(style);
    }

    normalizeSize(rawSize, hostWindow) {
        if (rawSize === null || typeof rawSize === 'undefined') {
            return this.getDefaultSize(hostWindow);
        }

        const txt = ('' + rawSize).trim().toLowerCase();
        if (!txt || txt === 'auto') {
            return this.getDefaultSize(hostWindow);
        }
        return Utils.safeText(rawSize, 20) || this.getDefaultSize(hostWindow);
    }

    getDefaultSize(hostWindow) {
        const viewportWidth = hostWindow?.innerWidth || window.innerWidth || 1280;
        if (viewportWidth <= 768) {
            return '100%';
        }
        const minDrawerWidth = 420;
        const drawerWidth = Math.max(Math.round(viewportWidth * 0.5), minDrawerWidth);
        return `${drawerWidth}px`;
    }

    normalizeClosePayload(payload) {
        return {
            code: typeof payload?.code === 'number' ? payload.code : 0,
            message: typeof payload?.message === 'string' ? payload.message : 'closed',
            data: (payload && typeof payload?.data === 'object' && payload.data !== null) ? payload.data : {}
        };
    }

    open(options = {}) {
        try {
            const hostWindow = this._hostWindow || this.getHostWindow();
            if (!hostWindow.Vue || !hostWindow.ElementPlus) {
                throw new Error('drawer host dependencies are not initialized');
            }

            this.ensureStyle(hostWindow);

            const merged = {
                ...this._defaults,
                ...(options && typeof options === 'object' ? options : {})
            };
            const align = Utils.safeType(merged.footerAlign, ['start', 'center', 'end', 'space-between'], 'end');
            const alignMap = {
                start: 'justify-start',
                center: 'justify-center',
                end: 'justify-end',
                'space-between': 'justify-between'
            };

            const buttons = Array.isArray(merged.footerButtons) ? merged.footerButtons : [];
            const mountEl = hostWindow.document.createElement('div');
            const id = 'ele-manager-drawer-host-' + Date.now() + '-' + Math.floor(Math.random() * 100000);
            mountEl.id = id;
            hostWindow.document.body.appendChild(mountEl);

            const { createApp, reactive } = hostWindow.Vue;
            const manager = this.manager;
            const helper = this;
            const state = reactive({
                visible: false,
                title: Utils.safeText(merged.title, 80) || '服务端 Drawer',
                content: Utils.safeText(merged.content, 2000),
                html: Utils.toBool(merged.html, false),
                url: Utils.safeText(merged.url, 500),
                size: this.normalizeSize(merged.size, hostWindow),
                direction: Utils.safeType(merged.direction, ['ltr', 'rtl', 'ttb', 'btt'], 'rtl'),
                withHeader: Utils.toBool(merged.withHeader, true),
                showClose: Utils.toBool(merged.showClose, true),
                resizable: Utils.toBool(merged.resizable, true),
                modal: this._instances.length === 0 ? Utils.toBool(merged.modal, true) : false,
                closeOnClickModal: Utils.toBool(merged.closeOnClickModal, true),
                destroyOnClose: Utils.toBool(merged.destroyOnClose, false),
                showFooter: Utils.toBool(merged.showFooter, buttons.length > 0),
                footerButtons: buttons,
                footerAlignClass: alignMap[align] || 'justify-end',
                isMobile: (hostWindow?.innerWidth || window.innerWidth || 1280) <= 768,
                closePayload: null,
                closeConfirm: Utils.safeText(merged.closeConfirm, 200),
                closeHandler: typeof merged.closeHandler === 'function'
                    ? merged.closeHandler
                    : Utils.safeText(merged.closeHandler, 120),
                beforeCloseHandler: typeof merged.beforeCloseHandler === 'function'
                    ? merged.beforeCloseHandler
                    : (merged.beforeCloseHandler ? Utils.safeText(merged.beforeCloseHandler, 120) : null),
                serverCloseHandler: Utils.safeText(merged.serverCloseHandler, 80),
                closeAction: Utils.safeText(merged.closeAction, 20)
            });

            const instance = {
                id,
                state,
                app: null,
                mountEl,
                hostWindow,
                closeMessageHandler: null
            };

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
                        const fn = Utils.resolveHandler(state.beforeCloseHandler);
                        if (!fn) {
                            done();
                            return;
                        }
                        try {
                            await fn(done);
                        } catch (err) {
                            console.error('beforeCloseHandler error:', err);
                        }
                    },
                    async onClosed() {
                        const payload = helper.normalizeClosePayload(state.closePayload);
                        if (state.closeHandler) {
                            const fn = Utils.resolveHandler(state.closeHandler);
                            if (fn) {
                                try { fn(payload); } catch (error) { console.error(error); }
                            }
                        }
                        if (state.serverCloseHandler) {
                            try {
                                await manager.postServerHandler(state.serverCloseHandler, payload);
                            } catch (error) {
                                console.error('drawer server close callback failed:', error);
                            }
                        }

                        if (manager && typeof manager.handleDrawerCloseAction === 'function') {
                            manager.handleDrawerCloseAction(state.closeAction, payload);
                        }

                        helper.disposeInstance(instance);
                    },
                    async handleCloseClick() {
                        if (state.closeConfirm) {
                            try {
                                await hostWindow.ElementPlus.ElMessageBox.confirm(
                                    state.closeConfirm,
                                    '提示',
                                    { type: 'warning', confirmButtonText: '确定', cancelButtonText: '取消' }
                                );
                            } catch {
                                return; // 用户取消
                            }
                        }
                        state.visible = false;
                    },
                    onFooterClick(btn) {
                        if (btn && btn.handler) {
                            const fn = Utils.resolveHandler(btn.handler);
                            if (fn) {
                                try { fn(btn.action || 'click', btn); } catch (error) { console.error(error); }
                            }
                        }
                        if (!btn || !btn.action || btn.action === 'close') {
                            state.visible = false;
                        }
                    }
                },

                //  <el-drawer ... resizable>
                template: `
<el-drawer
    v-model="state.visible"
    class="ele-manager-drawer"
    v-bind="state.resizable ? { resizable: '' } : {}"
    :direction="state.direction"
    :before-close="state.beforeCloseHandler ? beforeClose : undefined"
    :size="state.size"
    :with-header="state.withHeader"
    :show-close="false"
    :modal="state.modal"
    :close-on-click-modal="state.closeOnClickModal"
    :destroy-on-close="state.destroyOnClose"
    @closed="onClosed"
>
    <template #header v-if="state.withHeader">
        <div v-if="state.isMobile" class="ele-manager-drawer-header is-mobile">
            <button v-if="state.showClose" class="ele-manager-drawer-close-btn" type="button" @click="handleCloseClick()" aria-label="Close">
                <span>←</span>
            </button>
            <span class="ele-manager-drawer-title">{{ state.title }}</span>
        </div>
        <div v-else class="ele-manager-drawer-header">
            <span class="ele-manager-drawer-title">{{ state.title }}</span>
            <button v-if="state.showClose" class="ele-manager-drawer-close-btn" type="button" @click="handleCloseClick()" aria-label="Close">
                <span>×</span>
            </button>
        </div>
    </template>

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

            if (hostWindow.dayjs && typeof hostWindow.dayjs.locale === 'function') {
                hostWindow.dayjs.locale('zh-cn');
            }
            if (hostWindow.ElementPlus && hostWindow.ElementPlus.dayjs && typeof hostWindow.ElementPlus.dayjs.locale === 'function') {
                hostWindow.ElementPlus.dayjs.locale('zh-cn');
            }

            if (hostWindow.ElementPlusLocaleZhCn) {
                app.use(hostWindow.ElementPlus, { locale: hostWindow.ElementPlusLocaleZhCn });
            } else {
                app.use(hostWindow.ElementPlus);
            }
            if (hostWindow.ElementPlusIconsVue) {
                for (const [key, component] of Object.entries(hostWindow.ElementPlusIconsVue)) {
                    app.component(key, component);
                }
            }

            app.mount(mountEl);
            instance.app = app;

            try {
                instance.closeMessageHandler = (e) => {
                    if (!e) return;
                    const topInstance = helper._instances[helper._instances.length - 1];
                    if (!topInstance || topInstance.id !== instance.id) return;
                    const payload = e.data;
                    if (!payload || typeof payload !== 'object' || payload.__elePageClose !== true) return;
                    state.closePayload = helper.normalizeClosePayload(payload);
                    state.visible = false;
                };
                hostWindow.addEventListener('message', instance.closeMessageHandler);
            } catch (err) {
                console.warn('attach drawer close message listener failed:', err);
            }

            this._instances.push(instance);

            state.visible = true;
            return true;
        } catch (err) {
            console.error('openDrawer failed:', err);
            return false;
        }
    }

    disposeInstance(instance) {
        if (!instance) return;
        const idx = this._instances.findIndex((x) => x.id === instance.id);
        if (idx >= 0) {
            this._instances.splice(idx, 1);
        }

        try {
            if (instance.hostWindow && instance.closeMessageHandler) {
                instance.hostWindow.removeEventListener('message', instance.closeMessageHandler);
            }
        } catch (err) {
            console.error('drawer message listener cleanup failed:', err);
        }

        try {
            if (instance.app && typeof instance.app.unmount === 'function') {
                instance.app.unmount();
            }
        } catch (err) {
            console.error('drawer unmount failed:', err);
        }

        try {
            if (instance.mountEl && instance.mountEl.parentNode) {
                instance.mountEl.parentNode.removeChild(instance.mountEl);
            }
        } catch (err) {
            console.error('drawer mount element cleanup failed:', err);
        }
    }

    close() {
        try {
            const topInstance = this._instances[this._instances.length - 1];
            if (topInstance && topInstance.state) {
                topInstance.state.visible = false;
            }
            return true;
        } catch (err) {
            console.error('closeDrawer failed:', err);
            return false;
        }
    }
}
