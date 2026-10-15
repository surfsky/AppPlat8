import { Utils } from "../Utils.js";

/**
 * 按钮任务方法：为 EleButton 的同步/异步服务端操作提供统一的锁定、loading 和流式进度能力。
 */
export const buttonMethods = {
    /**
     * 执行按钮异步任务：期间锁定按钮并显示内嵌 loading，任务结束后自动恢复。
     * @param {Event} event 触发事件，用于定位按钮 DOM 元素
     * @param {Function} taskFn 实际执行的异步任务（返回 Promise）
     * @returns {Promise<any>} 任务结果
     */
    async runButtonTask(event, taskFn) {
        const run = (typeof taskFn === 'function') ? taskFn : () => null;
        return this._withBusyButton(event, async () => await run());
    },

    /**
     * 执行按钮流式任务：期间锁定按钮，弹出进度层，并持续消费服务端 SSE 数据。
     * @param {Event} event 触发事件
     * @param {Function} taskFn 任务函数，签名为 streamApi => Promise
     * @param {Object} options 进度层配置
     * @returns {Promise<any>} 最终任务结果
     */
    async runButtonStreamTask(event, taskFn, options = {}) {
        const run = (typeof taskFn === 'function') ? taskFn : () => null;
        const dialog = this._createStreamDialog(options);

        const streamApi = {
            onOpen: (payload = {}) => dialog.update(this._normalizeStreamState(payload)),
            onMessage: (payload = {}) => dialog.update(this._normalizeStreamState(payload)),
            onProgress: (payload = {}) => dialog.update(this._normalizeStreamState(payload)),
            onComplete: (payload = {}) => dialog.update(this._normalizeStreamState({
                ...payload,
                done: true,
                percent: this._extractStreamPercent(payload) ?? 100
            })),
            onError: (error, payload = null) => {
                const message = this._extractStreamMessage(payload) || error?.message || '处理失败';
                dialog.update({
                    percent: this._extractStreamPercent(payload) ?? dialog.state.percent ?? 0,
                    message,
                    done: false,
                    status: 'exception'
                });
            }
        };

        return this._withBusyButton(event, async () => {
            try {
                const result = await run(streamApi);
                if (result && Object.prototype.hasOwnProperty.call(result, 'code') && result.code !== 0 && result.code !== '0') {
                    const failMsg = Utils.extractMessage(result, '处理失败');
                    const handledError = new Error(failMsg);
                    handledError.__eleAlreadyShown = true;
                    throw handledError;
                }
                streamApi.onComplete(result || {});
                return result;
            } catch (error) {
                streamApi.onError(error);
                if (!error?.__eleAlreadyShown && typeof this.showError === 'function') {
                    this.showError(error?.message || '处理失败');
                }
                throw error;
            } finally {
                window.setTimeout(() => dialog.close(), 260);
            }
        });
    },

    /**
     * 在按钮忙碌期间加锁，结束后自动恢复。
     * @param {Event} event 触发事件
     * @param {Function} runner 实际执行逻辑
     * @returns {Promise<any>} 执行结果
     */
    async _withBusyButton(event, runner) {
        const run = (typeof runner === 'function') ? runner : () => null;
        const btn = this._resolveButtonElement(event);
        if (!btn) return run();
        if (btn.__eleButtonBusy) return;

        btn.__eleButtonBusy = true;
        const unlock = this._lockButton(btn);
        try {
            return await run();
        } finally {
            unlock();
            btn.__eleButtonBusy = false;
        }
    },

    /**
     * 锁定按钮并注入 Element Plus 风格的 loading 图标。
     * @param {HTMLElement} btn 按钮元素
     * @returns {Function} 解锁方法
     */
    _lockButton(btn) {
        const prevDisabled = btn.disabled;
        btn.classList.add('is-loading');
        btn.disabled = true;

        const spinner = document.createElement('span');
        spinner.className = 'el-icon is-loading';
        spinner.setAttribute('aria-hidden', 'true');
        spinner.innerHTML = '<svg viewBox="0 0 1024 1024" class="circular" style="width:1em;height:1em;vertical-align:-0.15em;fill:currentColor"><path d="M512 64a32 32 0 0 1 32 32v192a32 32 0 0 1-64 0V96a32 32 0 0 1 32-32zm0 640a32 32 0 0 1 32 32v192a32 32 0 1 1-64 0V736a32 32 0 0 1 32-32zm448-192a32 32 0 0 1-32 32H736a32 32 0 1 1 0-64h192a32 32 0 0 1 32 32zM288 512a32 32 0 0 1-32 32H64a32 32 0 1 1 0-64h192a32 32 0 0 1 32 32zm210.75-320a32 32 0 0 1 22.63 54.63l-135.76 135.76a32 32 0 1 1-45.25-45.25l135.76-135.76A32 32 0 0 1 498.75 192zM660 764a32 32 0 0 1 0 45.25L524.24 945.03a32 32 0 0 1-45.25-45.25L614.75 764A32 32 0 0 1 660 764zM320 284.75a32 32 0 0 1 45.25 0l135.76 135.76a32 32 0 1 1-45.25 45.25L320 330a32 32 0 0 1 0-45.25zM819.25 720a32 32 0 0 1 0-45.25l135.76-135.76a32 32 0 1 1 45.25 45.25L864.5 720a32 32 0 0 1-45.25 0z"></path></svg>';
        btn.insertBefore(spinner, btn.firstChild);

        return () => {
            btn.classList.remove('is-loading');
            btn.disabled = prevDisabled;
            if (spinner.parentNode === btn) btn.removeChild(spinner);
        };
    },

    /**
     * 创建流式进度弹层。
     * @param {Object} options 进度配置
     * @returns {{ state: object, update: Function, close: Function }} 进度控制器
     */
    _createStreamDialog(options = {}) {
        const title = Utils.safeText(options.title, 80) || '处理中';
        const initialMessage = Utils.safeText(options.message, 200) || '正在处理，请稍候...';
        const uiHost = this._resolveUiHostWindow();
        const hostDocument = uiHost?.document || document;

        if (!hostDocument?.body) {
            return {
                state: { percent: 0, message: initialMessage, status: '' },
                update: () => {},
                close: () => {}
            };
        }

        const managerCore = uiHost?.EleManager?.Instance || null;
        const zIndex = (managerCore && typeof managerCore.resolvePopupZIndex === 'function')
            ? managerCore.resolvePopupZIndex(6004, 2)
            : 6004;

        const overlay = hostDocument.createElement('div');
        overlay.setAttribute('data-ele-stream-dialog', 'true');
        overlay.style.cssText = `position:fixed;inset:0;z-index:${zIndex};display:flex;align-items:center;justify-content:center;background:rgba(0,0,0,0.28);`;

        const panel = hostDocument.createElement('div');
        panel.style.cssText = 'width:420px;max-width:calc(100vw - 32px);background:#fff;border-radius:12px;box-shadow:0 18px 48px rgba(0,0,0,0.18);padding:18px 20px 16px;';

        const titleEl = hostDocument.createElement('div');
        titleEl.style.cssText = 'font-size:16px;font-weight:600;color:#303133;line-height:1.4;margin-bottom:14px;';
        titleEl.textContent = title;

        const messageEl = hostDocument.createElement('div');
        messageEl.style.cssText = 'font-size:13px;color:#606266;line-height:1.6;margin-bottom:12px;';
        messageEl.textContent = initialMessage;

        const progressShell = hostDocument.createElement('div');
        progressShell.style.cssText = 'position:relative;height:18px;border-radius:999px;background:#ebeef5;overflow:hidden;';

        const progressBar = hostDocument.createElement('div');
        progressBar.style.cssText = 'height:100%;width:0%;background:linear-gradient(90deg,#67c23a,#85ce61);transition:width .2s ease;';

        const progressText = hostDocument.createElement('div');
        progressText.style.cssText = 'position:absolute;inset:0;display:flex;align-items:center;justify-content:center;font-size:12px;font-weight:600;color:#fff;mix-blend-mode:normal;';
        progressText.textContent = '0%';

        progressShell.appendChild(progressBar);
        progressShell.appendChild(progressText);
        panel.appendChild(titleEl);
        panel.appendChild(messageEl);
        panel.appendChild(progressShell);
        overlay.appendChild(panel);
        hostDocument.body.appendChild(overlay);

        const state = {
            visible: true,
            title,
            message: initialMessage,
            percent: 0,
            status: ''
        };

        let disposed = false;
        const dispose = () => {
            if (disposed) return;
            disposed = true;
            try { overlay.remove(); } catch (_) {}
        };

        return {
            state,
            update: (patch = {}) => {
                if (!patch || typeof patch !== 'object') return;
                if (typeof patch.title === 'string' && patch.title.trim()) state.title = patch.title.trim();
                if (typeof patch.message === 'string' && patch.message.trim()) state.message = patch.message.trim();
                if (patch.percent != null) state.percent = this._clampPercent(patch.percent);
                if (typeof patch.status === 'string') state.status = patch.status;
                titleEl.textContent = state.title;
                messageEl.textContent = state.message;
                progressBar.style.width = `${state.percent}%`;
                progressText.textContent = `${state.percent}%`;
                if (state.status === 'exception') {
                    progressBar.style.background = 'linear-gradient(90deg,#f56c6c,#f78989)';
                } else if (state.status === 'success') {
                    progressBar.style.background = 'linear-gradient(90deg,#67c23a,#85ce61)';
                } else {
                    progressBar.style.background = 'linear-gradient(90deg,#409eff,#79bbff)';
                }
            },
            close: () => {
                if (disposed) return;
                state.visible = false;
                overlay.style.transition = 'opacity .18s ease';
                overlay.style.opacity = '0';
                window.setTimeout(dispose, 180);
            }
        };
    },

    /**
     * 归一化 SSE 进度状态。
     * @param {Object} payload SSE 数据
     * @returns {{ percent: number|null, message: string, status: string }} 标准状态
     */
    _normalizeStreamState(payload = {}) {
        const percent = this._extractStreamPercent(payload);
        const message = this._extractStreamMessage(payload);
        let status = '';
        if (payload && payload.done === true) status = 'success';
        if (payload && (payload.code != null) && payload.code !== 0 && payload.code !== '0') status = 'exception';
        return { percent, message, status };
    },

    /**
     * 从 SSE 数据中提取百分比。
     * @param {Object} payload SSE 数据
     * @returns {number|null} 百分比
     */
    _extractStreamPercent(payload) {
        if (!payload || typeof payload !== 'object') return null;
        if (payload.percent != null && !Number.isNaN(Number(payload.percent))) return this._clampPercent(payload.percent);
        if (payload.data && payload.data.percent != null && !Number.isNaN(Number(payload.data.percent))) return this._clampPercent(payload.data.percent);
        return null;
    },

    /**
     * 从 SSE 数据中提取提示信息。
     * @param {Object} payload SSE 数据
     * @returns {string} 文案
     */
    _extractStreamMessage(payload) {
        if (!payload) return '';
        if (typeof payload === 'string') return payload.trim();
        if (typeof payload.message === 'string' && payload.message.trim()) return payload.message.trim();
        if (typeof payload.msg === 'string' && payload.msg.trim()) return payload.msg.trim();
        if (payload.data && typeof payload.data.message === 'string' && payload.data.message.trim()) return payload.data.message.trim();
        return '';
    },

    /**
     * 百分比裁剪到 0-100。
     * @param {number|string} value 原始值
     * @returns {number} 裁剪后的值
     */
    _clampPercent(value) {
        const num = Number(value);
        if (!Number.isFinite(num)) return 0;
        return Math.max(0, Math.min(100, Math.round(num)));
    },

    /** 获取弹层宿主窗口。 */
    _resolveUiHostWindow() {
        try {
            if (window.top && window.top !== window && window.top.ElementPlus) return window.top;
        } catch (_) {}
        return window;
    },

    /** 从事件对象解析出按钮 DOM 元素。 */
    _resolveButtonElement(event) {
        const src = event && (event.currentTarget || event.target);
        if (!src) return null;
        if (typeof src.closest !== 'function') return (src.nodeType === 1) ? src : null;
        return src.closest('button') || src.closest('.el-button') || src;
    }
};
