/**
 * Utility functions
 */
export class Utils {
    /**
     * Convert value to boolean with fallback
     * @param {*} value 
     * @param {*} fallback 
     * @returns 
     */
    static toBool(value, fallback = true) {
        if (typeof value === 'boolean') return value;
        if (typeof value === 'string') {
            const v = value.trim().toLowerCase();
            if (v === 'true') return true;
            if (v === 'false') return false;
        }
        return fallback;
    }

    /**
     * Safely get text with a maximum length
     * @param {*} value 
     * @param {number} maxLen 
     * @returns {string}
     */
    static safeText(value, maxLen = 300) {
        if (typeof value !== 'string') return '';
        return value.trim().slice(0, maxLen);
    }

    /**
     * Safely get a value from a set of allowed values
     * @param {*} value 
     * @param {*} allowed 
     * @param {*} fallback 
     * @returns 
     */
    static safeType(value, allowed, fallback) {
        const t = typeof value === 'string' ? value.toLowerCase() : '';
        return allowed.includes(t) ? t : fallback;
    }

    /**
     * Get Global Function by Path
     * @param {*} path 
     * @returns 
     */
    static getGlobalFunction(path) {
        if (typeof path !== 'string' || !path.trim()) return null;
        const keys = path.split('.').map((s) => s.trim()).filter(Boolean);
        let cur = window;
        for (const k of keys) {
            cur = cur?.[k];
        }
        return typeof cur === 'function' ? cur : null;
    }

    /**
     * Resolve Handler Function
     * @param {*} handler 
     * @returns 
     */
    static resolveHandler(handler) {
        if (typeof handler === 'function') return handler;
        if (typeof handler === 'string') return Utils.getGlobalFunction(handler);
        return null;
    }

    /**
     * Format Date String
     * @param {*} s 
     * @param {*} type 
     * @returns 
     */
    static formatDate(s, type) {
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
     * Format Enum Value
     * @param {*} val 
     * @param {*} options 
     * @returns 
     */
    static formatEnum(val, options) {
        if (val === null || val === undefined) return '';
        if (!options || !Array.isArray(options)) return val;
        const item = options.find((o) => o.Id == val || o.Value == val || o === val);
        return item ? item.Title : val;
    }

    /**
     * Get CSRF Token from current document or parent document
     * @returns 
     */
    static getCsrfToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value
            || window.parent?.document?.querySelector('input[name="__RequestVerificationToken"]')?.value
            || window.top?.document?.querySelector('input[name="__RequestVerificationToken"]')?.value
            || '';
    }

    /**
     * Generic request helper that uses axios if available, otherwise falls back to fetch
     * @param {*} url 
     * @param {*} data 
     * @param {*} method 
     * @returns 
     */
    static async request(url, data = null, method = 'POST') {
        const token = Utils.getCsrfToken();
        const headers = {
            RequestVerificationToken: token,
            'X-Requested-With': 'XMLHttpRequest'
        };

        // Use axios if available
        if (window.axios) {
            const response = await window.axios({
                url,
                method,
                data,
                headers
            });
            return response.data;
        }

        // Use fetch
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

    /**
     * Extract user-facing error message from various backend response formats.
     * Supports: msg / message / info / detail / title / error / reason keys,
     * ASP.NET Core ProblemDetails.errors, and raw JSON strings.
     * @param {*} data
     * @param {string} [fallback]
     * @returns {string}
     */
    static extractMessage(data, fallback) {
        if (data == null) return fallback || '';
        if (typeof data === 'string') {
            const s = data.trim();
            if (!s) return fallback || '';
            try {
                const p = JSON.parse(s);
                if (p) return Utils.extractMessage(p, fallback);
            } catch (_) {
                if (s.length < 240) return s;
                return fallback || '';
            }
        }
        if (typeof data !== 'object') return fallback || '';
        const names = ['msg', 'Msg', 'message', 'Message', 'info', 'Info', 'detail', 'Detail', 'title', 'Title', 'error', 'Error', 'reason', 'Reason'];
        for (const k of names) {
            const v = data[k];
            if (typeof v === 'string' && v.trim()) return v;
        }
        if (data.errors && typeof data.errors === 'object') {
            for (const p in data.errors) {
                const list = data.errors[p];
                if (typeof list === 'string' && list) return list;
                if (Array.isArray(list)) {
                    for (const x of list) if (typeof x === 'string' && x) return x;
                }
            }
        }
        return fallback || '';
    }

    /**
     * 取指定 scope 的 window 对象
     * scope: 'Self' | 'Parent' | 'Top'（字符串，后端枚举 camelCase 序列化后）
     */
    static _resolveWindowScope(scope) {
        const s = String(scope || 'Parent').toLowerCase();
        try {
            if (s === 'self') return window;
            if (s === 'top' && window.top && window.top !== window) return window.top;
            if (window.parent && window.parent !== window) return window.parent;
        } catch (_) { }
        return window;
    }

    /**
     * 解析命令并找出要刷新的 Atts 实例（详细日志版）
     * 查找优先级：
     *   ① 若 args 带 uniId：从 top/parent.__attsInstances__ 数组里精确匹配同一个 uniId（Drawer 移动附件场景最强）
     *   ② top/parent.__attsTableInstance__ 快捷方式
     *   ③ 遍历 top.document.querySelectorAll('iframe') → iframe.contentWindow.__attsTableInstance__
     *   ④ 兜底：scope window 广播 __attsMoveToRefresh__ postMessage
     */
    static _findAttsInstanceForRefresh(args) {
        const TAG = '[Utils:refreshData]';
        const result = { inst: null, win: null, matchType: null, logs: [] };
        const push = function () { try { result.logs.push(Array.from(arguments).join(' ')); console.log(TAG, ...arguments); } catch (_) { } };
        push('========================');
        push('开始查找 Atts 列表实例 args =', JSON.stringify(args || {}));
        const uniId = args && args.uniId ? String(args.uniId) : (window && window.__attsMoveToSourceUniId__ ? String(window.__attsMoveToSourceUniId__) : '');
        let topWin = window;
        let parentWin = window;
        try { if (window.top) topWin = window.top; } catch (_) { }
        try { if (window.parent) parentWin = window.parent; } catch (_) { }
        push('当前 window === top?', window === topWin, ', window === parent?', window === parentWin, ', 目标 uniId =', uniId || '(未传)');

        // ① 精确命中 uniId（Drawer 移动场景下 100% 有效）
        const candidates = [topWin, parentWin, window].filter((v, i, a) => a.indexOf(v) === i);
        for (const w of candidates) {
            try {
                if (Array.isArray(w.__attsInstances__) && w.__attsInstances__.length) {
                    push('在', w === topWin ? 'top' : (w === parentWin ? 'parent' : 'self'), '上找到 __attsInstances__ 数组，数量 =', w.__attsInstances__.length);
                    // 先按 uniId 精确匹配
                    if (uniId) {
                        const exact = w.__attsInstances__.find(x => x && x.uniId && String(x.uniId) === String(uniId));
                        if (exact && exact.inst && exact.win) {
                            push('✅ ① uniId 精确匹配命中，win === top?', exact.win === topWin, ', typeof inst._triggerAttsRefresh =', typeof exact.inst._triggerAttsRefresh, ', typeof inst.loadData =', typeof exact.inst.loadData);
                            result.inst = exact.inst; result.win = exact.win; result.matchType = 'uniId-exact';
                            return result;
                        }
                    }
                    // 否则取最近注册的一个
                    const latest = w.__attsInstances__.slice().sort((a, b) => (b.time || 0) - (a.time || 0))[0];
                    if (latest && latest.inst && latest.win) {
                        push('✅ ① 最近注册实例命中（无 uniId 精确匹配），typeof inst.loadData =', typeof latest.inst.loadData);
                        result.inst = latest.inst; result.win = latest.win; result.matchType = 'attsInstances-latest';
                        return result;
                    }
                }
            } catch (e) { push('扫描数组异常（忽略）', e); }
        }

        // ② 快捷方式 __attsTableInstance__
        for (const w of candidates) {
            try {
                const inst = w.__attsTableInstance__;
                if (inst && (typeof inst._triggerAttsRefresh === 'function' || typeof inst.loadData === 'function')) {
                    push('✅ ② __attsTableInstance__ 快捷方式命中（', w === topWin ? 'top' : (w === parentWin ? 'parent' : 'self'), '） typeof loadData =', typeof inst.loadData);
                    result.inst = inst; result.win = w; result.matchType = '__attsTableInstance__';
                    return result;
                }
            } catch (_) { }
        }

        // ③ 遍历 top.document 全部 iframe（命中 Manager3 内嵌的 /Shared/Atts iframe）
        let doc = null; try { if (topWin && topWin.document) doc = topWin.document; } catch (_) { }
        if (doc && typeof doc.querySelectorAll === 'function') {
            const frames = doc.querySelectorAll('iframe');
            push('③ 开始遍历 top.document 下的 iframe，共', frames.length, '个...');
            for (let i = 0; i < frames.length; i++) {
                try {
                    const f = frames[i];
                    let cw = null; try { cw = f.contentWindow; } catch (_) { cw = null; }
                    if (!cw) { push('  · iframe[' + i + '] 无法访问 contentWindow（跨域或未加载，跳过） src =', f.src ? f.src.slice(0, 80) : '(空)'); continue; }
                    const inst = cw.__attsTableInstance__;
                    const has = Array.isArray(cw.__attsInstances__) && cw.__attsInstances__.length;
                    push('  · iframe[' + i + '] src=', (f.src || '').slice(0, 80), ' __attsTableInstance__?', !!inst, ' __attsInstances__ 数量?', has ? cw.__attsInstances__.length : 0);
                    if (inst && (typeof inst._triggerAttsRefresh === 'function' || typeof inst.loadData === 'function')) {
                        // 若带 uniId 再精确匹配一次
                        if (uniId && has) {
                            const exact = cw.__attsInstances__.find(x => x && x.uniId && String(x.uniId) === String(uniId));
                            if (exact && exact.inst) { push('✅ ③ iframe[' + i + '] 按 uniId 精确匹配，typeof inst.loadData =', typeof exact.inst.loadData); result.inst = exact.inst; result.win = cw; result.matchType = 'iframe-uniId-exact'; return result; }
                        }
                        push('✅ ③ iframe[' + i + '] 命中 __attsTableInstance__，typeof inst.loadData =', typeof inst.loadData);
                        result.inst = inst; result.win = cw; result.matchType = 'iframe-__attsTableInstance__';
                        return result;
                    }
                    if (has) {
                        const latest = cw.__attsInstances__.slice().sort((a, b) => (b.time || 0) - (a.time || 0))[0];
                        if (latest && latest.inst) { push('✅ ③ iframe[' + i + '] 命中 __attsInstances__ 最新实例'); result.inst = latest.inst; result.win = cw; result.matchType = 'iframe-attsInstances-latest'; return result; }
                    }
                } catch (e) { push('  · iframe[' + i + '] 遍历异常（忽略）', e); }
            }
            push('③ 遍历完毕（未命中）');
        }
        push('❌ 4 种方式均未找到 Atts 实例；兜底：广播 postMessage + CustomEvent');
        result.matchType = 'broadcast-fallback';
        return result;
    }

    /**
     * 通用单条命令执行（返回 Promise）
     * command: { command: string(ClientCommandType camelCase), args: object, requestId, utc }
     * cmdStr 兼容后端 System.Text.Json CamelCase：
     *   toast / notify / showLoading / closeLoading / openDrawer / closeDrawer
     *   / setControl / refreshData / refreshPage / messageBox / inputBox
     */
    static async applyClientCommand(cmd) {
        if (!cmd) return;
        const cmdStr = String(cmd.command || '').toLowerCase();
        const args = cmd.args || {};
        const EM = (typeof EleManager !== 'undefined') ? EleManager : (window.EleManager || null);
        const EP = (typeof ElementPlus !== 'undefined') ? ElementPlus : (window.ElementPlus || null);
        const toast = (msg, type) => {
            try {
                if (EM && typeof EM.showToast === 'function') { EM.showToast(msg, type); return; }
                if (EP && EP.ElMessage) {
                    const fn = EP.ElMessage[type && typeof EP.ElMessage[type] === 'function' ? type : (msg.type || 'info')];
                    if (fn) fn({ message: String(msg.message || msg || ''), duration: 2600 });
                    else EP.ElMessage(String(msg.message || msg || ''));
                    return;
                }
            } catch (_) { }
            try { console.log('[cmd:toast]', type, msg); } catch (_) { }
        };

        switch (cmdStr) {
            case 'toast':
            case 'notify': {
                const typeMap = { success: 'success', warning: 'warning', warn: 'warning', info: 'info', error: 'error' };
                const t = typeMap[String(args.type || 'info').toLowerCase()] || 'info';
                toast({ message: String(args.message || ''), type: t });
                if (cmdStr === 'notify' && EM && typeof EM.showNotify === 'function') {
                    try { EM.showNotify(args.message, args.type, args.title); } catch (_) { }
                }
                break;
            }
            case 'showloading':
            case 'show-loading':
                if (EM && typeof EM.showLoading === 'function') EM.showLoading(args.text || '加载中...');
                break;
            case 'closeloading':
            case 'close-loading':
                if (EM && typeof EM.closeLoading === 'function') EM.closeLoading();
                break;
            case 'opendrawer':
            case 'open-drawer':
                if (EM && typeof EM.openDrawer === 'function') EM.openDrawer(args || {});
                break;
            case 'closedrawer':
            case 'close-drawer': {
                // 当前页面如果是 Drawer iframe，调用 parent EleManager.closeDrawer() 最稳
                let closed = false;
                try {
                    const parentEM = window.parent && window.parent.EleManager ? window.parent.EleManager : (window.top && window.top.EleManager ? window.top.EleManager : EM);
                    if (parentEM && typeof parentEM.closeDrawer === 'function') { parentEM.closeDrawer(); closed = true; }
                } catch (_) { }
                if (!closed && EM && typeof EM.closeDrawer === 'function') EM.closeDrawer();
                // 同时发一次 __elePageClose 消息（兼容旧链路）
                try {
                    const payload = { __elePageClose: true, code: 0, data: { fromApplyCommands: true } };
                    if (window.parent && window.parent !== window) window.parent.postMessage(payload, '*');
                    if (window.top && window.top !== window) window.top.postMessage(payload, '*');
                } catch (_) { }
                break;
            }
            case 'setcontrol':
            case 'set-control':
                if (EM && typeof EM.setControl === 'function') EM.setControl(args && args.items ? args.items : []);
                break;
            case 'messagebox':
            case 'message-box':
                if (EM && typeof EM.showMessageBox === 'function') EM.showMessageBox(args);
                else if (EP && EP.ElMessageBox) {
                    try { EP.ElMessageBox.alert(args.text || '', args.title || '提示'); } catch (_) { }
                }
                break;
            case 'inputbox':
            case 'input-box':
                if (EM && typeof EM.showInputBox === 'function') EM.showInputBox(args);
                break;
            case 'refreshdata':
            case 'refresh-data': {
                const TAG = '[Utils:applyClientCommand:refreshData]';
                console.log(TAG, '命令开始；args =', JSON.stringify(args || {}));
                const found = Utils._findAttsInstanceForRefresh(args || {});
                const w = Utils._resolveWindowScope(args && args.scope);
                const inst = found.inst;
                if (inst) {
                    console.log(TAG, '命中实例 matchType =', found.matchType);
                    try { if (Array.isArray(inst.selectedIds?.value)) inst.selectedIds.value = []; } catch (_) { }
                    let did = false;
                    if (typeof inst._triggerAttsRefresh === 'function') {
                        try { await inst._triggerAttsRefresh('[cmd:refreshData/' + found.matchType + ']'); did = true; }
                        catch (e) { console.error(TAG, '_triggerAttsRefresh 异常', e); }
                    }
                    if (!did && typeof inst.loadData === 'function') {
                        try {
                            console.log(TAG, '👉 直接调用 inst.loadData()（会发起 ?handler=Data 请求）');
                            await inst.loadData();
                            did = true;
                            console.log(TAG, '✅ inst.loadData() 执行完成，检查服务器日志是否有新的 handler=Data 行');
                        } catch (e) { console.error(TAG, 'loadData 异常', e); }
                    }
                    if (typeof inst.search === 'function') { try { await inst.search(); } catch (_) { } }
                    if (typeof inst.invokeCommand === 'function' && !did) { try { inst.invokeCommand('Search'); } catch (_) { } }
                } else {
                    console.warn(TAG, '未命中任何 Atts 实例；兜底广播 postMessage');
                    // 兜底：给目标 window 广播一个通用刷新事件，让各实例自行订阅
                    try {
                        if (w && typeof w.postMessage === 'function' && w !== window) {
                            w.postMessage({ __attsMoveToRefresh__: true, needsRefresh: true, fromApplyCommands: true }, '*');
                        }
                        if (window && typeof window.dispatchEvent === 'function') {
                            window.dispatchEvent(new CustomEvent('eleui:refresh-data', { detail: args }));
                        }
                    } catch (_) { }
                }
                break;
            }
            case 'refreshpage':
            case 'refresh-page': {
                const w = Utils._resolveWindowScope(args.scope);
                const force = args.forceReload !== false;
                try { if (w && typeof w.location !== 'undefined' && typeof w.location.reload === 'function') w.location.reload(force); } catch (_) { }
                break;
            }
            default:
                try { console.warn('[applyClientCommand] unknown command:', cmdStr, cmd); } catch (_) { }
                break;
        }
    }

    /**
     * 执行后端下发的客户端命令数组（来自 EleManager.BuildClientCommandResult(params ClientCommand[])）
     * res 可以是 {data:{commands:[...]}} 或直接 commands 数组或单 ClientCommand 对象
     * 按 commands 顺序串行执行，toast 后 350ms 再执行下一条（让 toast 先显示）
     */
    static async applyClientCommands(res) {
        if (!res) return;
        let commands = [];
        if (Array.isArray(res)) commands = res;
        else if (Array.isArray(res.commands)) commands = res.commands;
        else if (res.data) {
            if (Array.isArray(res.data.commands)) commands = res.data.commands;
            else if (Array.isArray(res.data.args) || res.data.command) commands = [res.data];
        } else if (res.command || res.args) commands = [res];
        commands = commands.filter(c => c != null);
        for (let i = 0; i < commands.length; i++) {
            try { await Utils.applyClientCommand(commands[i]); }
            catch (e) { try { console.error('[applyClientCommands] error at', i, e); } catch (_) { } }
            // toast 类命令后稍微停一下再执行下一条（如关抽屉、刷数据），避免 UI 动画抖动时 toast 被盖掉
            const c = commands[i];
            const cmdStr = String(c && c.command || '').toLowerCase();
            if (cmdStr === 'toast' || cmdStr === 'notify' || cmdStr === 'messagebox' || cmdStr === 'message-box') {
                await new Promise(r => setTimeout(r, 350));
            } else if (cmdStr === 'closedrawer' || cmdStr === 'close-drawer') {
                await new Promise(r => setTimeout(r, 200));
            }
        }
    }
}

// Expose Utils globally
if (typeof globalThis !== 'undefined') {
    globalThis.Utils = Utils;
}
