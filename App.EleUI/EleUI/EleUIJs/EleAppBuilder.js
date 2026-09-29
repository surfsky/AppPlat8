//import EleManager from './EleManager.js';
import { initPickerState, pickerMethods } from './form/pickerMethods.js';

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
        const textDefaults = {};
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
            } else if (raw !== '' && !Number.isNaN(Number(raw))) {
                defaults[key] = Number(raw);
            } else if (raw !== '') {
                defaults[key] = raw;
            }

            // ElePicker: also collect TEXT default value (e.g. checkerName -> display name)
            // so the picker shows the user's name / org name instead of the placeholder.
            const textKey = (el.getAttribute('data-filter-text-model') || '').trim();
            const textRaw = (el.getAttribute('data-filter-text-default') || '').trim();
            if (textKey && textRaw !== '') {
                if (textRaw === 'true' || textRaw === 'false')
                    textDefaults[textKey] = textRaw === 'true';
                else if (!Number.isNaN(Number(textRaw)))
                    textDefaults[textKey] = Number(textRaw);
                else
                    textDefaults[textKey] = textRaw;
            }
        });

        // Merge TEXT defaults into the same defaults object so they're applied together
        for (const [k, v] of Object.entries(textDefaults)) {
            defaults[k] = v;
        }

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
        const { createApp, reactive, toRefs, ref, onMounted, onUnmounted, nextTick } = this.Vue;
        const builder = this;

        // SSR 默认值快照：见 EleTableAppBuilder.mount 中的同一根因说明，在 app.mount() 之前
        // 收集 SSR 原始 DOM，避免 Element Plus/自定义组件 hydrate 后把 data-filter-* 删除
        const snapshotDefaults = builder.collectFilterDefaults(selector || '#app');

        const app = this.createConfiguredApp(config, {
            setup() {
                // 注册应用内全局状态
                const exposed = config.exposeName && typeof window[config.exposeName] === 'object'
                    ? window[config.exposeName]
                    : {};
                const state = reactive({ ...exposed });

                // --- Filters + Picker 支持（让 ElePicker / EleSelect 等控件在非 EleTable/EleForm 页面也能正常工作）---
                const filters = ref({});
                // 构建 picker 上下文：需要包含 filters ref，pickerMethods 内部通过 _formHolder(ctx) 查找
                const pickerCtx = { filters };
                initPickerState(pickerCtx, builder.Vue);
                // 关键点：pickerMethods 内部方法之间通过 this.xxx 相互调用（例如 clearPicker -> this.normalizePickerField，
                // openPicker -> this.buildPickerUrl），必须先把整个 pickerMethods 挂载到 pickerCtx 上，
                // 与 EleForm / EleTable 通过 Object.assign(prototype, pickerMethods) 的方式保持一致。
                if (pickerMethods && typeof pickerMethods === 'object') {
                    for (const [k, fn] of Object.entries(pickerMethods)) {
                        if (typeof fn === 'function' && typeof pickerCtx[k] === 'undefined') {
                            pickerCtx[k] = fn;
                        }
                    }
                }
                // 再从 pickerCtx 上取出所有 picker 方法并 bind 到 pickerCtx，交给模板直接调用
                const boundPickerFns = {};
                if (pickerMethods && typeof pickerMethods === 'object') {
                    for (const [k, fn] of Object.entries(pickerMethods)) {
                        if (typeof fn === 'function' && typeof pickerCtx[k] === 'function') {
                            boundPickerFns[k] = pickerCtx[k].bind(pickerCtx);
                        }
                    }
                }

                // Cross-window message 监听：ElePicker 弹出窗口选择完成后会 postMessage 回来
                const pickerMsgHandler = (e) => boundPickerFns.handlePickerMessage && boundPickerFns.handlePickerMessage(e);
                onMounted(async () => {
                    await nextTick();
                    // 优先使用 mount 前快照的 SSR 默认值；如果快照为空，再退化为从 DOM 重新 collect
                    const hasSnapshot = snapshotDefaults && Object.keys(snapshotDefaults).length > 0;
                    const filterDefaults = hasSnapshot
                        ? snapshotDefaults
                        : builder.collectFilterDefaults(selector || '#app');
                    builder.applyFilterDefaults(filters, filterDefaults);
                    window.addEventListener('message', pickerMsgHandler);
                });
                onUnmounted(() => {
                    window.removeEventListener('message', pickerMsgHandler);
                });

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
                    filters,
                    postHandler,
                    invokeCommand,
                    // 把 picker 相关 ref 和方法交给模板，让 ElePicker 能调用 openPicker/clearPicker
                    pickerVisible: pickerCtx.pickerVisible,
                    pickerUrl: pickerCtx.pickerUrl,
                    pickerTitle: pickerCtx.pickerTitle,
                    pickerTargetId: pickerCtx.pickerTargetId,
                    pickerTargetText: pickerCtx.pickerTargetText,
                    pickerMulti: pickerCtx.pickerMulti,
                    ...boundPickerFns,
                    Utils: (typeof window !== 'undefined' && window.Utils) ? window.Utils : (typeof globalThis !== 'undefined' && globalThis.Utils) ? globalThis.Utils : null,
                    openTopImageViewer: (url, list, idx) => {
                        try {
                            if (typeof window !== 'undefined' && window.Utils && typeof window.Utils.openImageViewerTop === 'function') {
                                window.Utils.openImageViewerTop(url, list || null, idx || 0);
                            } else if (url) {
                                (window || globalThis).open(String(url), '_blank', 'noopener');
                            }
                        } catch (e) { try { console.warn('openTopImageViewer error', e); } catch (_) { } }
                    }
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

                // --- 页面局部自定义方法注入：window.__pageExtras.${exposeName || 'page'} ---
                // 当页面脚本需要在 Vue 模板表达式里（如 el-tabs @tab-change）直接调用自定义函数时，
                // 把函数挂到 window.__pageExtras 上，此处会合并到 setup bindings，解决"xxx is not a function"问题
                try {
                    if (typeof window !== 'undefined') {
                        const pageExtrasAll = window.__pageExtras;
                        const scopeKey = (config.exposeName && typeof config.exposeName === 'string') ? config.exposeName : 'page';
                        if (pageExtrasAll && typeof pageExtrasAll === 'object') {
                            const scopeExtras = pageExtrasAll[scopeKey];
                            if (scopeExtras && typeof scopeExtras === 'object') {
                                for (const [k, v] of Object.entries(scopeExtras)) {
                                    if (typeof bindings[k] !== 'undefined') continue;
                                    bindings[k] = (typeof v === 'function') ? v.bind(bindings) : v;
                                }
                            }
                            // 若页面未按 scopeKey 分组，也兜底把 window.__pageExtras 本身的所有 function/值合并
                            // 仅合并 keys 不是字符串合法 scope 名的场景会误触发，这里显式跳过 exposeName 字段本身
                            const knownScopeKeys = new Set(Object.keys(pageExtrasAll).filter(sk => sk && pageExtrasAll[sk] && typeof pageExtrasAll[sk] === 'object' && Object.keys(pageExtrasAll[sk]).length > 0));
                            for (const [k, v] of Object.entries(pageExtrasAll)) {
                                if (knownScopeKeys.has(k)) continue;
                                if (typeof bindings[k] !== 'undefined') continue;
                                bindings[k] = (typeof v === 'function') ? v.bind(bindings) : v;
                            }
                        }
                    }
                } catch (ex) { try { console.warn('[EleAppBuilder] merge __pageExtras error', ex); } catch (_) { } }

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
