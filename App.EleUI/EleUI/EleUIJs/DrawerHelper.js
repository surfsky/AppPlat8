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
            custom: false,
            bodyClass: '',
            mountHandler: null,
            size: '',
            direction: 'rtl',
            withHeader: true,
            showClose: true,
            showMaximize: true,
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
            closeAction: 'none',
            zIndex: 5000
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

.ele-manager-drawer-header.is-mobile .ele-manager-drawer-actions {
    margin-left: auto;
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

.ele-manager-drawer-max-btn {
    border: 0;
    background: transparent;
    color: #606266;
    cursor: pointer;
    font-size: 18px;
    line-height: 1;
    padding: 0;
    width: 24px;
    height: 24px;
    display: inline-flex;
    align-items: center;
    justify-content: center;
}

.ele-manager-drawer-max-btn:hover {
    color: #303133;
}

.ele-manager-drawer-refresh-btn {
    border: 0;
    background: transparent;
    color: #606266;
    cursor: pointer;
    font-size: 18px;
    line-height: 1;
    padding: 0;
    width: 24px;
    height: 24px;
    display: inline-flex;
    align-items: center;
    justify-content: center;
}

.ele-manager-drawer-refresh-btn:hover {
    color: #303133;
}

.ele-manager-drawer-actions {
    display: inline-flex;
    align-items: center;
    gap: 4px;
}

.el-drawer.ele-manager-drawer.is-custom-body .el-drawer__body {
    padding: 0 !important;
    overflow: hidden;
}

.ele-manager-drawer-body-host {
    width: 100%;
    height: 100%;
    min-height: 280px;
    overflow: hidden;
}

/* Keep message/notification above drawer overlays and headers. */
.el-overlay.is-message-box,
.el-overlay-message-box,
.el-message,
.el-notification {
    z-index: 300000 !important;
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
            const bodyId = `ele-manager-drawer-body-${Date.now()}-${Math.floor(Math.random() * 100000)}`;
            const state = reactive({
                visible: false,
                title: Utils.safeText(merged.title, 80) || '服务端 Drawer',
                content: Utils.safeText(merged.content, 2000),
                html: Utils.toBool(merged.html, false),
                url: Utils.safeText(merged.url, 500),
                custom: Utils.toBool(merged.custom, typeof merged.mountHandler === 'function'),
                bodyClass: Utils.safeText(merged.bodyClass, 200),
                bodyId,
                size: this.normalizeSize(merged.size, hostWindow),
                direction: Utils.safeType(merged.direction, ['ltr', 'rtl', 'ttb', 'btt'], 'rtl'),
                withHeader: Utils.toBool(merged.withHeader, true),
                showClose: Utils.toBool(merged.showClose, true),
                showMaximize: Utils.toBool(merged.showMaximize, true),
                resizable: Utils.toBool(merged.resizable, true),
                zIndex: Number.isFinite(Number(merged.zIndex)) ? Math.max(2000, Math.round(Number(merged.zIndex))) : 5000,
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
                closeAction: Utils.safeText(merged.closeAction, 20),
                isMaximized: false,
                normalSize: this.normalizeSize(merged.size, hostWindow)
            });

            const instance = {
                id,
                state,
                app: null,
                mountEl,
                hostWindow,
                closeMessageHandler: null,
                customCleanup: null,
                escKeyHandler: null
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
                        const close = (payload = null) => {
                            if (payload !== null && typeof payload !== 'undefined') {
                                state.closePayload = helper.normalizeClosePayload(payload);
                            }
                            state.visible = false;
                        };
                        const context = {
                            action: btn?.action || 'click',
                            button: btn || null,
                            state,
                            instance,
                            hostWindow,
                            close,
                            setPayload: (payload) => {
                                state.closePayload = helper.normalizeClosePayload(payload);
                            },
                            setFooterButtons: (items) => {
                                state.footerButtons = Array.isArray(items) ? items : [];
                                state.showFooter = state.footerButtons.length > 0;
                            }
                        };
                        if (btn && btn.handler) {
                            const fn = Utils.resolveHandler(btn.handler);
                            if (fn) {
                                try { fn(context.action, btn, context); } catch (error) { console.error(error); }
                            }
                        }
                        if (!btn || !btn.action || btn.action === 'close') {
                            state.visible = false;
                        }
                    },
                    toggleMaximize() {
                        if (state.isMaximized) {
                            state.size = state.normalSize || helper.getDefaultSize(hostWindow);
                            state.isMaximized = false;
                            return;
                        }
                        state.normalSize = state.size || helper.getDefaultSize(hostWindow);
                        state.size = '100%';
                        state.isMaximized = true;
                    },
                    reloadFrame() {
                        if (!state.url) return;
                        try {
                            const iframe = hostWindow.document.querySelector(`#${id} iframe[data-ele-drawer-iframe="1"]`);
                            if (!iframe) return;
                            try {
                                iframe.contentWindow?.location?.reload();
                            } catch {
                                iframe.src = iframe.src;
                            }
                        } catch (error) {
                            console.error('reload drawer iframe failed:', error);
                        }
                    }
                },

                //  <el-drawer ... resizable>
                template: `
<el-drawer
    v-model="state.visible"
    :class="['ele-manager-drawer', state.custom ? 'is-custom-body' : '']"
    v-bind="state.resizable ? { resizable: '' } : {}"
    :direction="state.direction"
    :z-index="state.zIndex"
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
            <span class="ele-manager-drawer-actions" v-if="state.url">
                <button class="ele-manager-drawer-refresh-btn" type="button" @click="reloadFrame()" aria-label="Refresh">
                    <span>↻</span>
                </button>
            </span>
        </div>
        <div v-else class="ele-manager-drawer-header">
            <span class="ele-manager-drawer-title">{{ state.title }}</span>
            <span class="ele-manager-drawer-actions">
                <button v-if="state.url" class="ele-manager-drawer-refresh-btn" type="button" @click="reloadFrame()" aria-label="Refresh">
                    <span>↻</span>
                </button>
                <button v-if="state.showMaximize" class="ele-manager-drawer-max-btn" type="button" @click="toggleMaximize()" :aria-label="state.isMaximized ? 'Restore' : 'Maximize'">
                    <span>{{ state.isMaximized ? '❐' : '□' }}</span>
                </button>
                <button v-if="state.showClose" class="ele-manager-drawer-close-btn" type="button" @click="handleCloseClick()" aria-label="Close">
                    <span>×</span>
                </button>
            </span>
        </div>
    </template>

    <iframe
        v-if="state.url"
        :src="state.url"
        data-ele-drawer-iframe="1"
        style="width:100%;height:100%;border:0;min-height:280px;"
    ></iframe>
    <div v-else-if="state.custom" :id="state.bodyId" :class="['ele-manager-drawer-body-host', state.bodyClass]"></div>
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

            const mountHandler = typeof merged.mountHandler === 'function'
                ? merged.mountHandler
                : Utils.resolveHandler(merged.mountHandler);
            if (state.custom && mountHandler) {
                hostWindow.Vue.nextTick(() => {
                    try {
                        const bodyEl = hostWindow.document.getElementById(state.bodyId);
                        if (!bodyEl) return;
                        const context = {
                            hostWindow,
                            bodyEl,
                            state,
                            instance,
                            app,
                            close: (payload = null) => {
                                if (payload !== null && typeof payload !== 'undefined') {
                                    state.closePayload = helper.normalizeClosePayload(payload);
                                }
                                state.visible = false;
                            },
                            setPayload: (payload) => {
                                state.closePayload = helper.normalizeClosePayload(payload);
                            },
                            setFooterButtons: (items) => {
                                state.footerButtons = Array.isArray(items) ? items : [];
                                state.showFooter = state.footerButtons.length > 0;
                            }
                        };
                        const cleanup = mountHandler(context);
                        if (typeof cleanup === 'function') {
                            instance.customCleanup = cleanup;
                        }
                    } catch (error) {
                        console.error('drawer mountHandler failed:', error);
                    }
                });
            }

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

            /** ESC 键：与右上角关闭按钮 × 走完全相同的关闭链路 handleCloseClick()。
             *  注意：需要同时在 hostWindow（父窗口）和 drawer iframe 的 contentWindow（如果有）上绑定，
             *  因为当焦点在 iframe 里面时，key 事件不会冒泡到父窗口。另外多层 drawer 叠加时只让栈顶的那个响应。 */
            try {
                var topAppRef = null;
                try { topAppRef = app._instance; } catch (_) { topAppRef = null; }
                instance.escKeyHandler = function (event) {
                    var e = event || window.event;
                    if (!e) return;
                    var key = (typeof e.key === 'string') ? e.key : '';
                    var code = (typeof e.code === 'string') ? e.code : '';
                    var keyCode = Number(e.keyCode || e.which || 0);
                    var isEsc = (key === 'Escape') || (code === 'Escape') || (keyCode === 27);
                    if (!isEsc) return;

                    // 只让当前栈顶 Drawer 响应 ESC，避免多层时一起关
                    var topInstance = helper._instances[helper._instances.length - 1];
                    if (!topInstance || topInstance.id !== instance.id) return;

                    // 如果此时有打开的 MessageBox / dialog / picker / select dropdown 等，
                    // 让 Element Plus 原生先处理（它们是 body 下的高 z-index 节点，用户按 Esc 先关它们）
                    var hw = hostWindow || window;
                    if (hw && hw.document) {
                        var overlaySelector = [
                            '.el-overlay.is-message-box',
                            '.el-overlay-message-box',
                            '.el-message-box__wrapper',
                            '.v-modal',
                            '.el-dialog__wrapper',
                            '.el-drawer.is-rtl',
                            '.el-select-dropdown.is-multiple',
                            '.el-select-dropdown',
                            '.el-picker-panel',
                            '.el-time-panel',
                            '.el-date-picker',
                            '.el-color-dropdown',
                            '.el-cascader__dropdown',
                            '.el-dropdown-menu',
                            '.el-tooltip__popper',
                            '.el-popover',
                            '[class*=el-notification]',
                            '[class*=el-message]'
                        ].join(',');
                        try {
                            var nodes = hw.document.querySelectorAll(overlaySelector);
                            if (nodes && nodes.length) {
                                for (var i = 0; i < nodes.length; i++) {
                                    var n = nodes[i];
                                    if (!n || n.offsetParent === null) continue;
                                    var cs = (n.getBoundingClientRect && n.getBoundingClientRect()) || null;
                                    if (!cs) continue;
                                    if (cs.width <= 0 || cs.height <= 0) continue;
                                    // 有当前栈 drawer 以外的 overlay 弹出层打开着，就把这次 ESC 留给它们自己处理
                                    if (n.closest && n.closest('.ele-manager-drawer') && n.closest('.ele-manager-drawer').getAttribute('data-ele-drawer-host-id') !== instance.id) continue;
                                    e.stopImmediatePropagation();
                                    e.preventDefault();
                                    return;
                                }
                            }
                        } catch (_) { /* ignore */ }
                    }

                    try {
                        e.preventDefault();
                        e.stopPropagation();
                    } catch (_) { /* ignore */ }

                    // 走和点击关闭按钮完全一致的 handleCloseClick() 入口
                    // 兼容 Vue 3：app._instance.proxy / app._container.__vue_app__ / 暴露到 data 上的 methods
                    var closeFn = null;
                    try {
                        var ctx = (topAppRef && topAppRef.proxy) || null;
                        if (!ctx && instance.mountEl && instance.mountEl.__vue_app__) {
                            ctx = instance.mountEl.__vue_app__._instance && instance.mountEl.__vue_app__._instance.proxy;
                        }
                        if (!ctx && instance.mountEl) {
                            var wp = instance.mountEl.__vueParentComponent || instance.mountEl._vnode || null;
                            if (wp && wp.component && wp.component.proxy) ctx = wp.component.proxy;
                        }
                        if (ctx && typeof ctx.handleCloseClick === 'function') closeFn = function () { return ctx.handleCloseClick(); };
                    } catch (_) { closeFn = null; }

                    if (typeof closeFn === 'function') {
                        closeFn();
                        return;
                    }

                    // 兜底：若拿不到组件实例的 methods 引用（极少见场景），退化为直接执行与 handleCloseClick 等价逻辑：closeConfirm 提示 → visible=false
                    // 这里直接内联实现一份同样逻辑，确保 ESC 效果和 × 按钮 100% 等价。
                    (async function () {
                        if (state.closeConfirm) {
                            try {
                                await (hostWindow || window).ElementPlus.ElMessageBox.confirm(
                                    state.closeConfirm,
                                    '提示',
                                    { type: 'warning', confirmButtonText: '确定', cancelButtonText: '取消' }
                                );
                            } catch (_cancel) {
                                return;
                            }
                        }
                        state.visible = false;
                    })();
                };

                // 父窗口绑一次（焦点在抽屉非 iframe 部分时生效）
                hostWindow.addEventListener('keydown', instance.escKeyHandler, true);

                // 若有 iframe（url mode），再给 iframe 内容也绑：
                // 1) 首次挂载时尝试捕获；2) iframe load 后再绑一次（同域可访问）
                (function bindIframeEsc() {
                    if (!state.url) return;
                    function tryBind(iframeEl) {
                        if (!iframeEl) return;
                        var iw = null;
                        try { iw = iframeEl.contentWindow; } catch (_) { iw = null; }
                        if (!iw) return;
                        try { iw.addEventListener('keydown', instance.escKeyHandler, true); } catch (_) { /* cross-origin */ }
                    }
                    function findAndBind() {
                        try {
                            var el = (hostWindow || window).document.querySelector('#' + instance.id + ' iframe[data-ele-drawer-iframe="1"]');
                            if (!el) return;
                            tryBind(el);
                            el.addEventListener('load', function () { tryBind(el); }, false);
                        } catch (_) { /* ignore */ }
                    }
                    try { findAndBind(); } catch (_) { /* ignore */ }
                    // 延迟再试一次，确保 iframe DOM 已插入
                    setTimeout(findAndBind, 0);
                    setTimeout(findAndBind, 250);
                    setTimeout(findAndBind, 1000);
                })();
            } catch (err) {
                console.warn('attach drawer ESC key handler failed:', err);
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

        // 解绑 ESC 键：父窗口 + iframe 内容窗口
        try {
            if (instance.escKeyHandler) {
                if (instance.hostWindow) {
                    try { instance.hostWindow.removeEventListener('keydown', instance.escKeyHandler, true); } catch (_) { /* ignore */ }
                }
                // 解绑 iframe contentWindow 的 keydown（同源时）
                try {
                    if (instance.mountEl && instance.hostWindow) {
                        var iframes = instance.hostWindow.document.querySelectorAll('#' + instance.id + ' iframe[data-ele-drawer-iframe="1"]');
                        if (iframes && iframes.length) {
                            for (var i = 0; i < iframes.length; i++) {
                                try {
                                    var ci = iframes[i].contentWindow;
                                    if (ci) ci.removeEventListener('keydown', instance.escKeyHandler, true);
                                } catch (_) { /* cross-origin */ }
                            }
                        }
                    }
                } catch (_) { /* ignore */ }
            }
        } catch (err) {
            console.error('drawer ESC key listener cleanup failed:', err);
        }

        try {
            if (typeof instance.customCleanup === 'function') {
                instance.customCleanup();
            }
        } catch (err) {
            console.error('drawer custom cleanup failed:', err);
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
