//import EleManager from './EleManager.js';

/**
 * Vue + element plus + dotnet razor page 应用构建器
 * 功能：
 * 1. 与服务器端交互，发送命令到服务器端
 * 2. 处理服务器响应，执行服务器发送的命令
 * 3. 将服务器响应数据合并到客户端状态
 * 4. 提供一个全局的postHandler方法，允许组件发送POST请求到服务器，并自动处理响应
 */
export class EleAppBuilder {
    constructor() {
        this.Vue = window.Vue;
    }

    //--------------------------------------------------------------
    //  辅助方法
    //--------------------------------------------------------------
    isCommandPayload(payload) {
        return !!(payload && typeof payload === 'object' && typeof payload.command === 'string');
    }

    hasCommandPayload(container) {
        if (!container || typeof container !== 'object') return false;
        if (this.isCommandPayload(container))            return true;
        if (this.isCommandPayload(container.command))    return true;
        if (Array.isArray(container.commands)) {
            return container.commands.some((item) => this.isCommandPayload(item));
        }
        return false;
    }

    getPostPayload(state = {}) {
        const payload = {};
        for (const [key, value] of Object.entries(state)) {
            if (typeof value !== 'function') {
                payload[key] = value;
            }
        }
        return payload;
    }

    collectFilterDefaults(rootSelector = '#app') {
        const defaults = {};
        const root = document.querySelector(rootSelector) || document.getElementById('app');
        if (!root) return defaults;

        const nodes = root.querySelectorAll('[data-filter-default][data-filter-model], [data-filter-default][v-model^="filters."]');
        nodes.forEach((el) => {
            let key = (el.getAttribute('data-filter-model') || '').trim();
            if (!key) {
                const model = (el.getAttribute('v-model') || '').trim();
                if (!model.startsWith('filters.')) return;
                key = model.substring('filters.'.length).trim();
            }

            if (!key) return;

            const raw = (el.getAttribute('data-filter-default') || '').trim();
            if (raw === 'true' || raw === 'false') {
                defaults[key] = raw === 'true';
                return;
            }

            if (raw !== '' && !Number.isNaN(Number(raw))) {
                defaults[key] = Number(raw);
                return;
            }

            defaults[key] = raw;
        });

        return defaults;
    }

    applyFilterDefaults(filterRef, defaults = {}) {
        if (!filterRef) return;

        if (!filterRef.value || typeof filterRef.value !== 'object') {
            filterRef.value = {};
        }

        for (const [key, value] of Object.entries(defaults || {})) {
            // Apply defaults deterministically during first mount.
            // Some controls (e.g. switch) may emit an initial false before we seed defaults.
            filterRef.value[key] = value;
        }
    }



    //--------------------------------------------------------------
    //  POST请求处理程序
    //--------------------------------------------------------------
    // 发送POST请求到服务器端
    async postHandler(name, payload, state = null) {
        if (!name) return null;
        try {
            const url = new URL(window.location.href);
            url.searchParams.set('handler', name);
            const postUrl = `${url.pathname}${url.search}`;

            const res = await axios.post(postUrl, payload, {
                headers: { 'RequestVerificationToken': Utils.getCsrfToken() }
            });
            const body = res ? res.data : null;
            return this.processPostResponse(body, state);
        } catch (e) {
            const failMsg = (window.Utils && typeof Utils.extractMessage === 'function')
                ? Utils.extractMessage(e?.response?.data, '请求失败')
                : (e?.message || '请求失败');
            EleManager.showError(failMsg);
            throw e;
        }
    }

    /**
     * 处理服务器响应，执行命令，并将数据合并到状态中
     * @param {*} body 服务器响应体
     * @param {*} state 客户端状态对象
     * @returns 处理后的响应体
     */
    processPostResponse(body, state = null) {
        // 处理非对象响应
        if (!body || typeof body !== 'object') {
            return body;
        }

        // 处理标准响应格式 { code, msg, data }
        if (Object.prototype.hasOwnProperty.call(body, 'code')) {
            if (body.code !== 0 && body.code !== '0') {
                EleManager.showError(Utils.extractMessage(body, '操作失败'));
                return body;
            }

            // 处理命令响应
            const commandExists = this.hasCommandPayload(body) || this.hasCommandPayload(body.data);
            if (!commandExists)
                EleManager.showSuccess(Utils.extractMessage(body, '操作成功'));
            this.executeServerCommands(body);       // ？
            this.executeServerCommands(body.data);  // ？

            // 将响应数据合并到状态中（如果有）
            if (state && body.data && typeof body.data === 'object' && !this.isCommandPayload(body.data)) {
                this.mergeServerState(state, body.data);
            }
            return body;
        }

        // 处理非标准响应格式
        this.executeServerCommands(body);
        if (!this.hasCommandPayload(body)) 
            EleManager.showSuccess('操作成功');
        if (state) 
            this.mergeServerState(state, body);
        return body;
    }    

    // 执行服务器命令
    executeServerCommands(container) {
        if (!container || typeof container !== 'object') {
            return;
        }

        if (this.isCommandPayload(container)) {
            EleManager.executeServerCommand(container);
        }
        if (Array.isArray(container.commands)) {
            for (const item of container.commands) {
                if (this.isCommandPayload(item)) {
                    EleManager.executeServerCommand(item);
                }
            }
        }
        if (this.isCommandPayload(container.command)) {
            EleManager.executeServerCommand(container.command);
        }
    }

    // 将服务器响应数据合并到客户端状态
    mergeServerState(target, source) {
        if (!target || typeof target !== 'object') return;
        if (!source || typeof source !== 'object') return;
        try {
            const stateKeys = Object.keys(target);
            const stateKeyMap = new Map(stateKeys.map((k) => [k.toLowerCase(), k]));
            for (const [key, value] of Object.entries(source)) {
                const targetKey = stateKeyMap.get(key.toLowerCase()) || key;
                try {
                    target[targetKey] = value;
                } catch (innerErr) {
                    // 单个字段赋值失败不影响其他字段同步（例如 Vue ref 只读属性冲突等）
                    if (typeof console !== 'undefined' && console.warn) {
                        console.warn('[mergeServerState] 跳过字段', targetKey, innerErr && innerErr.message ? innerErr.message : innerErr);
                    }
                }
            }
        } catch (outerErr) {
            if (typeof console !== 'undefined' && console.error) {
                console.error('[mergeServerState] 服务器状态合并出错：', outerErr && outerErr.stack ? outerErr.stack : outerErr);
            }
        }
    }


    //--------------------------------------------------------------
    //  应用构建器
    //--------------------------------------------------------------
    createConfiguredApp(config = {}, rootOptions = {}) {
        const app = this.Vue.createApp(rootOptions);
        const useLocale = config.useLocale !== false;

        if (useLocale) {
            if (window.dayjs && typeof window.dayjs.locale === 'function') {
                window.dayjs.locale('zh-cn');
            }
            if (window.ElementPlus && window.ElementPlus.dayjs && typeof window.ElementPlus.dayjs.locale === 'function') {
                window.ElementPlus.dayjs.locale('zh-cn');
            }
        }

        if (window.ElementPlus) {
            if (useLocale && window.ElementPlusLocaleZhCn) {
                app.use(window.ElementPlus, { locale: window.ElementPlusLocaleZhCn });
            } else {
                app.use(window.ElementPlus);
            }
        }

        if (typeof globalThis !== 'undefined' && globalThis.EleManager) {
            app.config.globalProperties.$eleManager = globalThis.EleManager;
        }

        if (config.registerIcons !== false && window.ElementPlusIconsVue) {
            for (const [key, component] of Object.entries(window.ElementPlusIconsVue)) {
                app.component(key, component);
            }
        }

        return app;
    }

    mount(selector, config = {}) {
        const { createApp, reactive, toRefs } = this.Vue;
        const builder = this;

        const app = this.createConfiguredApp(config, {
            setup() {
                // 注册应用内全局状态
                const exposed = config.exposeName && typeof window[config.exposeName] === 'object'
                    ? window[config.exposeName]
                    : {};
                const state = reactive({ ...exposed });

                // 处理POST请求
                const postHandler = async (name, payload) => {
                    const data = payload || builder.getPostPayload(state);
                    return builder.postHandler(name, data, state);
                };

                // 处理内置命令
                const invokeCommand = async (name, payload) => {
                    if (!name) return;
                    const command = ('' + name).trim();
                    const key = command.toLowerCase();

                    // Close/Cancel
                    if (key === 'close' || key === 'cancel') {
                        if (typeof state.close === 'function') {
                            return state.close(payload);
                        }
                    }

                    // add
                    if (key === 'add') {
                        if (typeof state.openForm === 'function') {
                            return state.openForm(0);
                        }
                    }

                    // 其他命令默认走服务端 Handler
                    return postHandler(command, payload);
                };

                // 将状态和方法暴露给组件使用
                const bindings = {
                    ...toRefs(state),
                    postHandler,
                    invokeCommand
                };
                for (const [key, value] of Object.entries(state)) {
                    if (typeof value === 'function') {
                        bindings[key] = value.bind(state);
                    }
                }

                // Custom methods from global userMixin (对齐 EleTableAppBuilder 的 userMixin 机制)
                if (typeof userMixin !== 'undefined' && userMixin && userMixin.methods && typeof userMixin.methods === 'object') {
                    for (const [key, func] of Object.entries(userMixin.methods)) {
                        if (typeof func === 'function' && typeof bindings[key] === 'undefined') {
                            bindings[key] = func.bind(bindings);
                        }
                    }
                }

                return bindings;
            }
        });

        // 挂载应用到指定的DOM元素
        app.mount(selector);
        // 把 EleApp 的 setup bindings（含 reactive state / userMixin methods / postHandler / invokeCommand 等）
        // 回写到全局 window.exposeName + 通用 __appBindings，便于外部脚本（popstate、Drawer closeHandler 等）直接调用
        try {
            const name = (config && config.exposeName) ? config.exposeName : 'model';
            const bindings = (app && app._instance && app._instance.setupState)
                ? app._instance.setupState
                : null;
            if (bindings && typeof bindings === 'object') {
                if (!window.__appBindings) window.__appBindings = {};
                window.__appBindings[name] = bindings;
                // 白名单式展开：只把这些必要的核心方法挂到 window，避免 userMixin.methods
                // 里的业务方法（onDeleteMenu/onMenuMoveUp 等）被外部无意间调用导致"请求异常"
                const exposeFnWhitelist = new Set([
                    'close', 'cancel', 'confirm', 'save', 'reset', 'openForm', 'closeForm',
                    'submit', 'search', 'clear', 'refresh', 'reload', 'add', 'edit', 'delete',
                    'postHandler', 'invokeCommand', 'doUiCmd',
                    'managerReloadPage',
                    'mgr2UpdateCurrentMenuId', 'mgr2ApplyTreeRefresh'
                ]);
                for (const [k, v] of Object.entries(bindings)) {
                    if (typeof v === 'function' && typeof window[k] === 'undefined') {
                        if (!exposeFnWhitelist.has(String(k || '').trim())) continue;
                        try { window[k] = v.bind(bindings); } catch (_) {}
                    }
                }
                // 约定：把 bindings 存到 window.<exposeName> 下的 __bindings 属性；
                // 对 Manager 暴露的快捷方式：window.__mgrAppSetup = bindings（userMixin 中的 switchMenu 等可访问）
                if (typeof window.__mgrAppSetup === 'undefined' && typeof bindings.switchMenu === 'function') {
                    window.__mgrAppSetup = bindings;
                }
            }
        } catch (_) {}
        return app;
    }
}
