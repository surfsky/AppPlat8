export class EleAppBuilder {
    constructor() {
        this.Vue = window.Vue;
    }

    getCsrfToken() {
        return typeof EleManager !== 'undefined' ? EleManager.getCsrfToken() : '';
    }

    mount(selector, config = {}) {
        const { createApp, reactive, toRefs } = this.Vue;
        const builder = this;

        const app = createApp({
            setup() {
                const exposed = config.exposeName && typeof window[config.exposeName] === 'object'
                    ? window[config.exposeName]
                    : {};

                const state = reactive({ ...exposed });

                const mergeServerState = (source) => {
                    if (!source || typeof source !== 'object') return;
                    const stateKeys = Object.keys(state);
                    const stateKeyMap = new Map(stateKeys.map((k) => [k.toLowerCase(), k]));

                    for (const [key, value] of Object.entries(source)) {
                        const targetKey = stateKeyMap.get(key.toLowerCase()) || key;
                        state[targetKey] = value;
                    }
                };

                const isCommandPayload = (payload) => {
                    return !!(payload && typeof payload === 'object' && typeof payload.command === 'string');
                };

                const executeServerCommands = (container) => {
                    if (typeof EleManager === 'undefined' || typeof EleManager.executeServerCommand !== 'function') {
                        return;
                    }
                    if (!container || typeof container !== 'object') {
                        return;
                    }

                    if (isCommandPayload(container)) {
                        EleManager.executeServerCommand(container);
                    }

                    if (Array.isArray(container.commands)) {
                        for (const item of container.commands) {
                            if (isCommandPayload(item)) {
                                EleManager.executeServerCommand(item);
                            }
                        }
                    }

                    if (isCommandPayload(container.command)) {
                        EleManager.executeServerCommand(container.command);
                    }
                };

                const hasCommandPayload = (container) => {
                    if (!container || typeof container !== 'object') return false;
                    if (isCommandPayload(container)) return true;
                    if (isCommandPayload(container.command)) return true;
                    if (Array.isArray(container.commands)) {
                        return container.commands.some((item) => isCommandPayload(item));
                    }
                    return false;
                };

                const getPostPayload = () => {
                    const payload = {};
                    for (const [key, value] of Object.entries(state)) {
                        if (typeof value !== 'function') {
                            payload[key] = value;
                        }
                    }
                    return payload;
                };

                const postHandler = async (name, payload) => {
                    if (!name) return null;
                    try {
                        const res = await axios.post('?handler=' + encodeURIComponent(name), payload || getPostPayload(), {
                            headers: { 'RequestVerificationToken': builder.getCsrfToken() }
                        });

                        const body = res ? res.data : null;
                        if (!body || typeof body !== 'object') {
                            return body;
                        }

                        if (Object.prototype.hasOwnProperty.call(body, 'code')) {
                            if (body.code !== 0 && body.code !== '0') {
                                if (typeof EleManager !== 'undefined') {
                                    EleManager.showError(body.msg || '操作失败');
                                }
                                return body;
                            }

                            const commandExists = hasCommandPayload(body) || hasCommandPayload(body.data);
                            if (typeof EleManager !== 'undefined' && !commandExists) {
                                EleManager.showSuccess(body.msg || '操作成功');
                            }

                            executeServerCommands(body);
                            executeServerCommands(body.data);

                            if (body.data && typeof body.data === 'object' && !isCommandPayload(body.data)) {
                                mergeServerState(body.data);
                            }
                            return body;
                        }

                        executeServerCommands(body);

                        if (typeof EleManager !== 'undefined' && !hasCommandPayload(body)) {
                            EleManager.showSuccess('操作成功');
                        }

                        mergeServerState(body);
                        return body;
                    } catch (e) {
                        if (typeof EleManager !== 'undefined') {
                            EleManager.showError('请求失败');
                        }
                        throw e;
                    }
                };

                const invokeCommand = async (name, payload) => postHandler(name, payload);

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

                return bindings;
            }
        });

        if (window.ElementPlus) {
            if (config.useLocale && window.ElementPlusLocaleZhCn) {
                app.use(window.ElementPlus, { locale: window.ElementPlusLocaleZhCn });
            } else {
                app.use(window.ElementPlus);
            }
        }

        if (config.registerIcons !== false && window.ElementPlusIconsVue) {
            for (const [key, component] of Object.entries(window.ElementPlusIconsVue)) {
                app.component(key, component);
            }
        }

        app.mount(selector);
        return app;
    }
}