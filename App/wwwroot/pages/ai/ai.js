/**
 * Pages/AI/ai.js — AI 对话交互模块
 *
 * 职责：
 *   - 管理对话历史（多轮上下文记忆）
 *   - HTTP 流式请求与响应读取
 *   - Markdown 渲染 & 引用提取
 *   - 错误处理
 *   - "正在思考…" 占位提示
 *
 * 依赖：window.marked（由 Chat.cshtml 外部加载的 marked.min.js 提供）
 */
'use strict';

// ---------------------------------------------------------------
// AIChat — 单例类
// ---------------------------------------------------------------
class AIChat {

    // ------------------------------------------------------------------
    // 构造函数
    // ------------------------------------------------------------------
    constructor(options) {
        if (!options || !options.containerEl) {
            throw new Error('AIChat: containerEl is required');
        }

        /** 聊天消息容器 DOM */
        this.containerEl = options.containerEl;
        /** statusEl — 状态文本 DOM（如“AI 流式响应中…”） */
        this.statusEl = options.statusEl || null;

        /** 回调：滚动是否在底部 { isNearBottom(), scrollToBottom(force) } 或直接传 DOM */
        this._scroll = this._makeScrollHelper(options);

        // 外部感知回调
        /** (status: 'idle'|'thinking'|'streaming'|'error') => void */
        this.onStatusChange = options.onStatusChange || (() => {});
        /** 每个 bubble 创建时回调 */
        this.onBubbleCreated = options.onBubbleCreated || (() => {});
        /** (text) => void — AI 回答完成后的纯文本回调（可用于 TTS 播报） */
        this.onResponseText = options.onResponseText || (() => {});

        // ----- 对话历史 -----
        this._history = [];   // [{ role:'user'|'assistant', content:string, attachments?:[...] }]
        this._maxHistoryTurns = options.maxHistoryTurns || 20;
        this._maxImageHistoryTurns = options.maxImageHistoryTurns || 2;  // 只保留最近 N 轮的图片 dataUrl

        // ----- 运行时状态 -----
        this._isStreaming = false;
        this._abortController = null;
    }

    // ==================================================================
    // 公开 API
    // ==================================================================

    /** 是否正在流式输出中 */
    get isStreaming() { return this._isStreaming; }

    /** 获取对话历史（只读副本） */
    getHistory() { return this._history.slice(); }

    /** 清空对话历史 */
    clearHistory() {
        this._history = [];
    }

    /**
     * 中止当前请求
     */
    abort() {
        this._isStreaming = false;
        if (this._abortController) {
            try { this._abortController.abort(); } catch (_) { /* ignore */ }
            this._abortController = null;
        }
    }

    /**
     * 发送消息
     * @param {Object} params
     * @param {string} params.message        — 用户输入文本
     * @param {Array}  [params.attachments]  — [{ name, contentType, dataUrl?, textContent? }]
     * @param {string} params.endpoint       — POST URL
     * @param {number} [params.configId]     — AI 配置 ID
     * @param {string} [params.systemPrompt] — 系统提示词
     * @param {number} [params.temperature]  — 温度参数
     * @param {Object} [params.createBubble] — bubble 工厂：createBubble('user'|'ai') => { el, setContent(html), setError(text), setThinking?(html) }
     */
    async send(params) {
        if (this._isStreaming) return;

        const {
            message,
            attachments = [],
            endpoint,
            configId = 0,
            systemPrompt = '',
            temperature = 0.7,
            createBubble,
            requestHeaders = {}     // 额外 HTTP header，如防伪 token
        } = params;

        const msgText = (message || (attachments.length > 0 ? '请结合附件进行分析。' : '')).trim();
        if (!msgText && attachments.length === 0) return;

        // 1. 创建用户气泡（紧接在 send 开始创建，避免 async 后才出）
        const userAttachments = attachments.map(t => ({ name: t.name, contentType: t.contentType, dataUrl: t.dataUrl }));
        const userBubble = createBubble('user');
        userBubble.setContent(msgText);
        if (userAttachments.length > 0) {
            this._renderAttachments(userBubble.el, userAttachments);
        }
        this.onBubbleCreated(userBubble.el);

        // 2. 写入历史
        this._history.push({ role: 'user', content: msgText, attachments: attachments.map(a => ({ name: a.name, contentType: a.contentType, dataUrl: a.dataUrl, textContent: a.textContent })) });

        // 3. 状态 → thinking
        this._isStreaming = true;
        this.onStatusChange('thinking');

        // 4. 创建 AI 气泡 + 思考占位
        const aiBubble = createBubble('ai');
        aiBubble.setContent(this._buildThinkingHtml());
        this.onBubbleCreated(aiBubble.el);
        this._scroll.scrollToBottom(true);

        // 5. 构建请求体（含对话历史）
        const reqBody = {
            configId: configId || 0,
            systemPrompt: systemPrompt || '',
            temperature: Number(temperature || 0.7),
            message: msgText,
            attachments: attachments.map(t => ({ name: t.name, contentType: t.contentType, dataUrl: t.dataUrl, textContent: t.textContent })),
            messages: this._buildPayloadMessages(msgText, attachments)
        };

        // 6. fetch
        const controller = new AbortController();
        this._abortController = controller;

        try {
            const fetchOpts = {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', ...requestHeaders },
                body: JSON.stringify(reqBody),
                signal: controller.signal
            };

            const resp = await fetch(endpoint, fetchOpts);

            // 6a. HTTP 错误
            if (!resp.ok) {
                this._handleHttpError(resp, aiBubble);
                this._history.pop(); // 回滚刚才写入的 user 消息
                return;
            }

            // 6b. 浏览器不支持流
            if (!resp.body || !resp.body.getReader) {
                aiBubble.setError('当前浏览器不支持流式读取');
                this._history.pop();
                return;
            }

            // 7. 流式读取
            this.onStatusChange('streaming');
            const reader = resp.body.getReader();
            const decoder = new TextDecoder('utf-8');
            let finalText = '';
            let aborted = false;
            let upstreamError = null;
            let firstContentChunk = true;

            while (true) {
                const { done, value } = await reader.read();
                if (done) break;
                if (controller.signal.aborted) {
                    aborted = true;
                    try { reader.cancel(); } catch (_) { /* ignore */ }
                    break;
                }

                finalText += decoder.decode(value, { stream: true });

                // ---- 哨兵：__ERROR__: -----
                const errIdx = finalText.indexOf('__ERROR__:');
                if (errIdx >= 0) {
                    const endIdx = finalText.indexOf('__END__', errIdx);
                    if (endIdx > errIdx) {
                        try {
                            upstreamError = JSON.parse(finalText.substring(errIdx + '__ERROR__:'.length, endIdx).trim());
                        } catch (_) { /* ignore */ }
                        finalText = finalText.substring(0, errIdx) + finalText.substring(endIdx + '__END__'.length);
                    }
                }

                // ---- 去除 citations 哨兵后渲染 ----
                const { cleanText, citations } = splitCitations(finalText);
                const rendered = renderMarkdown(cleanText) + buildCitationsHtml(citations);

                if (firstContentChunk) {
                    firstContentChunk = false;
                    // 首 token 到达 → 直接把思考占位替换为真实内容
                }
                aiBubble.setContent(rendered);
                this._scroll.scrollToBottom();
            }

            // 8. 处理上游错误哨兵
            if (upstreamError) {
                this._handleUpstreamError(finalText, upstreamError, aiBubble);
                this._history.pop();
                return;
            }

            // 9. 完成
            const { cleanText: finalClean, citations: finalCitations } = splitCitations(finalText);
            if (aborted) {
                const abortedText = finalClean.trim()
                    ? finalClean + '\n\n_(已停止响应)_'
                    : '';
                aiBubble.setContent(
                    renderMarkdown(abortedText || '') + buildCitationsHtml(finalCitations) + (abortedText ? '' : '<p>(已停止响应)</p>')
                );
                this._history.pop(); // 只保留了部分响应，不算完整对话
            } else if (!finalClean.trim()) {
                aiBubble.setContent('<p>(空响应)</p>');
                this._history.pop();
            } else {
                aiBubble.setContent(renderMarkdown(finalClean) + buildCitationsHtml(finalCitations));
                // 完整对话，写入 history
                this._history.push({ role: 'assistant', content: finalClean });
                // TTS 回调
                this.onResponseText(finalClean);
            }

        } catch (e) {
            if (e?.name !== 'AbortError') {
                aiBubble.setError('网络错误：' + (e?.message || e));
            }
            this._history.pop();
        } finally {
            this._isStreaming = false;
            this._abortController = null;
            this.onStatusChange('idle');
        }
    }

    // ==================================================================
    // 私有方法
    // ==================================================================

    /** 构建 "思考中" HTML */
    _buildThinkingHtml() {
        return '<div class="thinking-indicator">'
            + '<span class="thinking-dot"></span>'
            + '<span class="thinking-dot"></span>'
            + '<span class="thinking-dot"></span>'
            + '<span class="thinking-text">AI 正在思考...</span>'
            + '</div>';
    }

    /**
     * 把附件渲染到气泡内容区底部
     */
    _renderAttachments(bubbleEl, attachments) {
        if (!attachments || attachments.length === 0) return;
        const contentEl = bubbleEl.querySelector('.content');
        if (!contentEl) return;

        const wrap = document.createElement('div');
        wrap.className = 'msg-attachments';
        for (const item of attachments) {
            const node = document.createElement('div');
            node.className = 'msg-attachment';

            const isImage = (item.contentType || '').startsWith('image/') && !!item.dataUrl;
            if (isImage) {
                node.classList.add('image');
                const thumb = document.createElement('img');
                thumb.className = 'msg-attachment-thumb';
                thumb.src = item.dataUrl;
                thumb.alt = item.name || '附件';
                thumb.addEventListener('click', () => {
                    // 交给外部 openImagePreview
                    if (typeof window._AIChatOpenImagePreview === 'function') {
                        window._AIChatOpenImagePreview(item.dataUrl, item.name || '图片附件');
                    }
                });
                node.appendChild(thumb);
            }

            const nameEl = document.createElement('span');
            nameEl.className = 'msg-attachment-name';
            nameEl.textContent = item.name || '附件';
            nameEl.title = item.name || '附件';
            node.appendChild(nameEl);
            wrap.appendChild(node);
        }
        contentEl.appendChild(wrap);
    }

    /**
     * 构建发往后端的 messages[] 数组
     *
     * 规则：
     *  - 包含完整的对话历史 + 当前消息
     *  - 图片 dataUrl 只保留最近 maxImageHistoryTurns 轮用户消息中的
     *  - 超出轮次的图片转成文本占位："用户曾发送图片: xxx.png"
     *  - 总轮数受 maxHistoryTurns 限制
     */
    _buildPayloadMessages(currentMessage, currentAttachments) {
        const messages = [];

        // 计算哪些用户轮次保留图片（按从后往前数）
        const userTurnsWithImages = [];
        for (let i = this._history.length - 1; i >= 0; i--) {
            const h = this._history[i];
            if (h.role === 'user' && h.attachments && h.attachments.some(a => (a.contentType || '').startsWith('image/') && !!a.dataUrl)) {
                userTurnsWithImages.push(i);
            }
        }

        // 历史消息（限制总轮数）
        const maxItems = this._maxHistoryTurns * 2; // 每轮 user+assistant
        const start = Math.max(0, this._history.length - maxItems);
        for (let i = start; i < this._history.length; i++) {
            const h = this._history[i];
            const msg = { role: h.role, content: h.content };

            if (h.role === 'user' && h.attachments && h.attachments.length > 0) {
                // 检查这轮是否应保留图片 dataUrl
                const userTurnIdx = userTurnsWithImages.indexOf(i);
                const keepImages = userTurnIdx >= 0 && userTurnIdx < this._maxImageHistoryTurns;

                if (keepImages) {
                    msg.attachments = h.attachments.map(a => ({
                        name: a.name,
                        contentType: a.contentType,
                        dataUrl: a.dataUrl || '',
                        textContent: a.textContent || ''
                    }));
                } else {
                    // 超限轮次：去掉图片 dataUrl，转文本占位
                    const imageAtts = h.attachments.filter(a => (a.contentType || '').startsWith('image/') && !!a.dataUrl);
                    const nonImageAtts = h.attachments.filter(a => !(a.contentType || '').startsWith('image/') || !a.dataUrl);

                    if (imageAtts.length > 0) {
                        const names = imageAtts.map(a => a.name || '图片').join('、');
                        msg.content = (h.content || '') + '\n[用户曾发送图片: ' + names + ']';
                    }

                    if (nonImageAtts.length > 0) {
                        msg.attachments = nonImageAtts.map(a => ({
                            name: a.name,
                            contentType: a.contentType,
                            dataUrl: '',  // 非图片文件不传 dataUrl（base64 也不小）
                            textContent: a.textContent || ''
                        }));
                    }
                }
            }

            messages.push(msg);
        }

        // 当前消息
        const currentMsg = { role: 'user', content: currentMessage };
        if (currentAttachments && currentAttachments.length > 0) {
            currentMsg.attachments = currentAttachments.map(a => ({
                name: a.name,
                contentType: a.contentType,
                dataUrl: a.dataUrl || '',
                textContent: a.textContent || ''
            }));
        }
        messages.push(currentMsg);

        return messages;
    }

    /** 处理 HTTP 错误响应 */
    async _handleHttpError(resp, aiBubble) {
        this.onStatusChange('error');
        let errText = '';
        try {
            const errJson = await resp.json();
            const msg = errJson.message || errJson.msg || '未知错误';
            const detail = errJson.data?.upstreamMessage || errJson.data?.body || '';
            errText = detail ? `请求失败：${msg}\n${detail}` : `请求失败：${msg}`;
        } catch (_) {
            errText = `请求失败：HTTP ${resp.status}`;
        }
        aiBubble.setError(errText);
    }

    /** 处理上游流式错误哨兵 */
    _handleUpstreamError(finalText, upstreamError, aiBubble) {
        this.onStatusChange('error');
        const trimmed = (finalText || '').trim();
        if (!trimmed) {
            aiBubble.setError(
                (upstreamError.code ? '[' + upstreamError.code + '] ' : '')
                + (upstreamError.error || '上游返回错误')
            );
        } else {
            const errMd = '\n\n**⚠️ '
                + (upstreamError.code ? '[' + upstreamError.code + '] ' : '')
                + (upstreamError.error || '上游返回错误')
                + '**';
            aiBubble.setContent(renderMarkdown(trimmed) + errMd);
        }
    }

    /** 创建滚动辅助 */
    _makeScrollHelper(options) {
        const el = options.containerEl;

        function isNearBottom(threshold = 40) {
            return (el.scrollHeight - el.scrollTop - el.clientHeight) <= threshold;
        }

        function scrollToBottom(force) {
            // shouldAutoScroll 由外部管理（监听 scroll 事件写在 Chat.cshtml 里）
            if (force || !options._autoScrollGetter || options._autoScrollGetter()) {
                el.scrollTop = el.scrollHeight;
            }
        }

        return { isNearBottom, scrollToBottom };
    }

} // end class AIChat


// =====================================================================
// 工具函数 — Markdown 渲染、引用提取、HTML 安全清理
// =====================================================================

/**
 * HTML 属性 & 特殊字符转义
 */
function escapeHtmlAttr(s) {
    return String(s || '').replace(/[<>&"]/g, ch => ({ '<': '&lt;', '>': '&gt;', '&': '&amp;', '"': '&quot;' }[ch]));
}

/**
 * 安全过滤：只允许白名单标签和属性
 */
function sanitizeRenderedHtml(html) {
    const parser = new DOMParser();
    const doc = parser.parseFromString(html || '', 'text/html');
    const allowedTags = new Set([
        'A', 'P', 'BR', 'STRONG', 'EM', 'UL', 'OL', 'LI', 'BLOCKQUOTE',
        'CODE', 'PRE', 'TABLE', 'THEAD', 'TBODY', 'TR', 'TH', 'TD',
        'H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'HR',
        // thinking 指示器
        'DIV', 'SPAN'
    ]);
    const allowedClassPrefixes = ['thinking-', 'citations', 'citation']; // 允许的 CSS 类前缀

    const walker = document.createTreeWalker(doc.body, NodeFilter.SHOW_ELEMENT);
    const toReplace = [];
    while (walker.nextNode()) {
        const el = walker.currentNode;
        if (!allowedTags.has(el.tagName)) {
            toReplace.push(el);
            continue;
        }

        for (const attr of [...el.attributes]) {
            const name = attr.name.toLowerCase();
            if (name.startsWith('on') || name === 'style') {
                el.removeAttribute(attr.name);
                continue;
            }
            if (name === 'class') {
                const classes = (attr.value || '').split(/\s+/);
                const filtered = classes.filter(c => {
                    const isAllowed = allowedClassPrefixes.some(prefix => c.lastIndexOf(prefix, 0) === 0);
                    if (isAllowed) {
                        return true;
                    }
                    // msg-attachment 系列
                    if (c.lastIndexOf('msg-attach', 0) === 0) return true;
                    return false;
                });
                if (filtered.length > 0) {
                    el.setAttribute(attr.name, filtered.join(' '));
                } else {
                    el.removeAttribute(attr.name);
                }
                continue;
            }
            if (name === 'href') {
                const href = (attr.value || '').trim();
                if (!/^(https?:|mailto:|#|\/)/i.test(href)) {
                    el.removeAttribute(attr.name);
                }
            } else if (name !== 'target' && name !== 'rel') {
                el.removeAttribute(attr.name);
            }
        }

        if (el.tagName === 'A') {
            el.setAttribute('target', '_blank');
            el.setAttribute('rel', 'noopener noreferrer nofollow');
        }
    }

    for (const el of toReplace) {
        const text = doc.createTextNode(el.textContent || '');
        el.replaceWith(text);
    }
    return doc.body.innerHTML;
}

/**
 * 用 DOMParser 清理 marked 输出中产生的"碎片"：
 *  1) 空段落（textContent 为空、或只含 <br>）直接删除
 *  2) 块级元素后紧跟的所有 <br> 和空白文本节点全部清除
 */
function collapseBlankLines(html) {
    if (!html) return '';
    const parser = new DOMParser();
    const doc = parser.parseFromString(html, 'text/html');
    const body = doc.body;
    if (!body) return '';
    const blockSelector = 'h1, h2, h3, h4, h5, h6, table, pre, blockquote, hr, ul, ol';

    body.querySelectorAll('p').forEach(p => {
        const text = (p.textContent || '').replace(/[\u00a0\s]/g, '');
        if (text) return;
        let onlyBr = p.children.length > 0;
        for (let i = 0; i < p.children.length; i++) {
            if (p.children[i].tagName !== 'BR') { onlyBr = false; break; }
        }
        if (p.children.length === 0 || onlyBr) p.remove();
    });

    body.querySelectorAll(blockSelector).forEach(el => {
        let next = el.nextSibling;
        while (next && (
            (next.nodeType === 1 && next.tagName === 'BR') ||
            (next.nodeType === 3 && !next.textContent.trim())
        )) {
            const toRemove = next;
            next = next.nextSibling;
            toRemove.remove();
        }
    });

    return body.innerHTML;
}

/**
 * Markdown → 安全 HTML
 */
function renderMarkdown(text) {
    let raw = (text || '');
    raw = raw.replace(/\r\n/g, '\n');
    raw = raw.replace(/\n{3,}/g, '\n\n');
    raw = raw.replace(/^[ \t]*\n+/, '');
    raw = raw.replace(/[ \t]+(\n|$)/g, '\n');
    raw = raw.replace(/\s+$/g, '');

    if (!window.marked) {
        return raw.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/\n/g, '<br>');
    }

    let html;
    try {
        html = window.marked.parse(raw, {
            gfm: true,
            tables: true,
            breaks: true,
            smartLists: true,
            smartypants: true
        });
    } catch (e) {
        console.warn('marked parse failed', e);
        return raw.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/\n/g, '<br>');
    }

    html = collapseBlankLines(html);
    return sanitizeRenderedHtml(html);
}

/**
 * 从原始文本中分割出 citations 哨兵
 */
function splitCitations(rawText) {
    const markerStart = '__CITATIONS__:';
    const markerEnd = '__END__';
    const startIdx = (rawText || '').indexOf(markerStart);
    if (startIdx < 0) return { cleanText: rawText || '', citations: [] };
    const endIdx = (rawText || '').indexOf(markerEnd, startIdx);
    if (endIdx < 0) return { cleanText: rawText || '', citations: [] };
    const jsonText = rawText.substring(startIdx + markerStart.length, endIdx).trim();
    const cleanText = (rawText.substring(0, startIdx) + rawText.substring(endIdx + markerEnd.length))
        .replace(/^\s+|\s+$/g, '')
        .trimEnd();
    let citations = [];
    try {
        const parsed = JSON.parse(jsonText);
        if (Array.isArray(parsed)) citations = parsed;
    } catch (_) { /* ignore */ }
    return { cleanText, citations };
}

/**
 * 生成参考文献（搜索来源）HTML
 */
function buildCitationsHtml(citations) {
    if (!citations || citations.length === 0) return '';
    const items = citations.map((c, idx) => {
        const url = (c?.url || '').trim();
        const title = (c?.title || url || '引用').trim();
        if (!url) return '';
        const safeTitle = title.replace(/[<>&"]/g, ch => ({ '<': '&lt;', '>': '&gt;', '&': '&amp;', '"': '&quot;' }[ch]));
        return '<li><a href="' + escapeHtmlAttr(url) + '" target="_blank" rel="noopener noreferrer nofollow">'
            + escapeHtmlAttr(url) + ' - ' + safeTitle
            + '</a></li>';
    }).filter(Boolean).join('');
    if (!items) return '';
    return '<div class="citations"><div class="citations-title">搜索来源</div><ol>' + items + '</ol></div>';
}
