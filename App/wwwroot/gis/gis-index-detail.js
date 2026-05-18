(function () {
    function resolveManager() {
        if (window.top && window.top.EleManager) return window.top.EleManager;
        return window.EleManager;
    }

    function create(ctx) {
        const panelId = ctx.panelId || 'geo-detail-panel';
        const bodyId = ctx.bodyId || 'geo-detail-body';
        const endpointBuilder = ctx.endpointBuilder || ((id) => `?handler=GeometryDetail&id=${encodeURIComponent(id)}`);
        let closeTimer = null;

        function getPanel() {
            return document.getElementById(panelId);
        }

        function getBody() {
            return document.getElementById(bodyId);
        }

        function escapeHtml(text) {
            return String(text ?? '')
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;')
                .replace(/'/g, '&#39;');
        }

        function openDrawer(url, title, size) {
            if (!url) return;
            const manager = resolveManager();
            if (!manager || typeof manager.openDrawer !== 'function') {
                window.open(url, '_blank');
                return;
            }

            manager.openDrawer({
                title: title || '查看',
                url,
                direction: 'rtl',
                size: size || (window.innerWidth < 768 ? '100%' : '70%'),
                resizable: true,
                closeOnClickModal: false,
                destroyOnClose: true
            });
        }

        function triggerDownload(url) {
            if (!url) return;
            const a = document.createElement('a');
            a.href = url;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
        }

        function normalizeOpenUrl(url) {
            const text = String(url || '').trim();
            if (!text) return '';

            if (/^https?:\/\//i.test(text))
                return text;

            if (text.startsWith('/'))
                return text;

            return `/${text.replace(/^\/+/, '')}`;
        }

        function renderAttListHtml(atts) {
            const list = Array.isArray(atts) ? atts : [];
            if (!list.length)
                return '<div class="geo-att-empty">暂无附件</div>';

            const rows = list.map(att => {
                const name = escapeHtml(att.fileName || att.FileName || '未命名文件');
                const size = escapeHtml(att.fileSizeText || att.FileSizeText || '');
                const previewUrl = escapeHtml(att.previewUrl || att.PreviewUrl || '');
                const downloadUrl = escapeHtml(att.downloadUrl || att.DownloadUrl || '');
                return `
                    <div class="geo-att-item">
                        <a href="javascript:void(0)" class="geo-att-link" data-preview-url="${previewUrl}" data-att-name="${name}">${name}</a>
                        <div class="geo-att-actions">
                            <span class="geo-att-size">${size}</span>
                            <button type="button" class="geo-att-download" data-download-url="${downloadUrl}">下载</button>
                        </div>
                    </div>
                `;
            }).join('');

            return `<div class="geo-att-list">${rows}</div>`;
        }

        /**
         * 获取类型文本
         */
        function getTypeText(type) {
            var map = { 1: '点', 2: '形状', 3: '文字', 4: '图片', 5: '视频', 6: '文件' };
            return map[type] || ('类型' + type);
        }

        function renderGeometryDetailHtml(detail) {
            const rows = Array.isArray(detail.dataRows || detail.DataRows) ? (detail.dataRows || detail.DataRows) : [];
            const pageUrl = normalizeOpenUrl(detail.url ?? detail.Url ?? detail.pageUrl ?? detail.PageUrl);
            const att = detail.att ?? detail.Att;
            const atts = detail.atts ?? detail.Atts;
            const typeText = getTypeText(detail.type ?? detail.Type);
            let rowHtml = `
                <tr><th>ID</th><td>${escapeHtml(detail.id ?? detail.Id)}</td></tr>
                <tr><th>类型</th><td>${escapeHtml(typeText)}</td></tr>
                <tr><th>名称</th><td>${escapeHtml(detail.name ?? detail.Name)}</td></tr>
                <tr><th>简称/别称</th><td>${escapeHtml(detail.alias ?? detail.Alias)}</td></tr>
                <tr><th>地址</th><td>${escapeHtml(detail.addr ?? detail.Addr)}</td></tr>
                <tr><th>经纬度</th><td>${escapeHtml(detail.gps ?? detail.GPS)}</td></tr>
            `;

            if (pageUrl) {
                rowHtml += `<tr><th>链接</th><td><a href="javascript:void(0)" class="geo-more-link" data-page-url="${escapeHtml(pageUrl)}">查看详情</a></td></tr>`;
            }

            if (att) {
                rowHtml += `<tr><th>附件</th><td><a href="${escapeHtml(att)}" target="_blank" style="color:#60a5fa;">${escapeHtml(att)}</a></td></tr>`;
            }

            if (rows.length === 0) {
                rowHtml += '<tr><th>属性数据</th><td>暂无数据</td></tr>';
            } else {
                rows.forEach(row => {
                    rowHtml += `<tr><th>${escapeHtml(row.key ?? row.Key)}</th><td>${escapeHtml(row.value ?? row.Value)}</td></tr>`;
                });
            }

            const tableHtml = `<table class="geo-data-table">${rowHtml}</table>`;
            const attHtml = `
                <div class="geo-att-section">
                    <div class="geo-att-title">附件</div>
                    ${renderAttListHtml(atts)}
                </div>
            `;
            return `${tableHtml}${attHtml}`;
        }

        function bindDetailActions(body) {
            if (!body) return;

            body.querySelectorAll('.geo-more-link').forEach(node => {
                node.addEventListener('click', () => {
                    const url = normalizeOpenUrl(node.getAttribute('data-page-url'));
                    if (!url) return;
                    openDrawer(url, '更多', window.innerWidth < 768 ? '100%' : '72%');
                });
            });

            body.querySelectorAll('.geo-att-link').forEach(node => {
                node.addEventListener('click', () => {
                    const url = normalizeOpenUrl(node.getAttribute('data-preview-url'));
                    const name = node.getAttribute('data-att-name') || '附件预览';
                    if (!url) return;
                    openDrawer(url, name, window.innerWidth < 768 ? '100%' : '72%');
                });
            });

            body.querySelectorAll('.geo-att-download').forEach(node => {
                node.addEventListener('click', () => {
                    const url = node.getAttribute('data-download-url') || '';
                    if (!url) return;
                    triggerDownload(url);
                });
            });
        }

        async function open(id) {
            if (!id) return;
            const panel = getPanel();
            const body = getBody();
            if (!panel || !body) return;

            if (closeTimer) {
                clearTimeout(closeTimer);
                closeTimer = null;
            }
            panel.classList.remove('closing');
            panel.classList.add('open');
            body.innerHTML = '正在加载点位信息...';

            try {
                const resp = await fetch(endpointBuilder(id));
                if (!resp.ok) {
                    body.innerHTML = `加载失败(${resp.status})`;
                    return;
                }

                const res = await resp.json();
                const code = res?.code ?? res?.Code ?? -1;
                const data = res?.data ?? res?.Data;
                if (code !== 0 || !data) {
                    body.innerHTML = escapeHtml(res?.msg ?? res?.Msg ?? '点位详情加载失败');
                    return;
                }

                body.innerHTML = renderGeometryDetailHtml(data);
                bindDetailActions(body);
            } catch {
                body.innerHTML = '点位详情加载失败';
            }
        }

        function close() {
            const panel = getPanel();
            if (!panel) return;
            if (!panel.classList.contains('open')) return;
            panel.classList.remove('open');
            panel.classList.add('closing');
            if (closeTimer) clearTimeout(closeTimer);
            closeTimer = setTimeout(() => {
                panel.classList.remove('closing');
            }, 260);
        }

        return {
            open,
            close,
            renderGeometryDetailHtml,
            escapeHtml
        };
    }

    window.GisIndexDetail = { create };
})();
