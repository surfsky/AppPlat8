(function () {
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

        function renderGeometryDetailHtml(detail) {
            const rows = Array.isArray(detail.dataRows || detail.DataRows) ? (detail.dataRows || detail.DataRows) : [];
            let rowHtml = `
                <tr><th>ID</th><td>${escapeHtml(detail.id ?? detail.Id)}</td></tr>
                <tr><th>名称</th><td>${escapeHtml(detail.name ?? detail.Name)}</td></tr>
                <tr><th>简称/别称</th><td>${escapeHtml(detail.alias ?? detail.Alias)}</td></tr>
                <tr><th>地址</th><td>${escapeHtml(detail.addr ?? detail.Addr)}</td></tr>
                <tr><th>经纬度</th><td>${escapeHtml(detail.gps ?? detail.GPS)}</td></tr>
            `;

            if (rows.length === 0) {
                rowHtml += '<tr><th>属性数据</th><td>暂无数据</td></tr>';
            } else {
                rows.forEach(row => {
                    rowHtml += `<tr><th>${escapeHtml(row.key ?? row.Key)}</th><td>${escapeHtml(row.value ?? row.Value)}</td></tr>`;
                });
            }

            return `<table class="geo-data-table">${rowHtml}</table>`;
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
