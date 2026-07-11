/**
 * 面板相关逻辑
 */
(function () {
    function create(ctx) {
        const state = ctx.state;
        const theme = ctx.theme || 'dark';
        let resizeTimer = null;
        const narrowMql = window.matchMedia('(max-width: 900px)');
        const defaultPanelHeight = 260;

        function splitColumns(panels, isNarrow) {
            const left = [];
            const right = [];
            if (isNarrow) {
                (panels || []).forEach(item => left.push(item));
                return { left, right };
            }
            (panels || []).forEach((item, idx) => {
                if (idx % 2 === 0) left.push(item);
                else right.push(item);
            });
            return { left, right };
        }

        function getPanelHeight(item) {
            const value = Number(item.panelHeight || item.PanelHeight || 0);
            if (!Number.isFinite(value) || value <= 0) return defaultPanelHeight;
            return Math.min(960, Math.max(160, Math.round(value)));
        }

        function getColumnShell(id) {
            const shell = document.getElementById(id);
            if (!shell) return null;
            const body = shell.querySelector('.stats-column-scroll');
            const hint = shell.querySelector('.stats-scroll-hint');
            return { shell, body, hint };
        }

        function updateScrollHint(shell) {
            if (!shell || !shell.body) return;
            const canScroll = shell.body.scrollHeight - shell.body.clientHeight > 6;
            const nearEnd = shell.body.scrollTop + shell.body.clientHeight >= shell.body.scrollHeight - 6;
            shell.shell.classList.toggle('can-scroll', !!canScroll);
            shell.shell.classList.toggle('scroll-end', !canScroll || nearEnd);
        }

        function bindScrollHint(shell) {
            if (!shell || !shell.body || shell.body.__statsScrollBound) return;
            shell.body.addEventListener('scroll', () => updateScrollHint(shell), { passive: true });
            shell.body.__statsScrollBound = true;
        }

        function buildBodyHtml(item) {
            const hasChart = !!(item.chartJson || item.ChartJson);
            const hasContent = !!(item.content || item.Content);
            if (hasChart && hasContent) {
                return `
                    <div class="gis-panel-chart" data-panel-id="${item.id}"></div>
                    <div class="gis-panel-content-text">${item.content || item.Content || ''}</div>
                `;
            }
            if (hasChart) {
                return `<div class="gis-panel-chart gis-panel-chart-full" data-panel-id="${item.id}"></div>`;
            }
            return `<div class="gis-panel-content-text">${item.content || item.Content || ''}</div>`;
        }

        function appendPanel(parent, item) {
            const panel = document.createElement('gis-panel');
            panel.className = 'stats-gis-panel';
            panel.setAttribute('title', item.title || item.Title || '未命名面板');
            panel.setAttribute('info', item.info || item.Info || '');
            panel.setAttribute('width', '100%');
            panel.setAttribute('height', `${getPanelHeight(item)}px`);
            panel.innerHTML = buildBodyHtml(item);
            parent.appendChild(panel);
            return panel;
        }

        async function renderPanels() {
            const leftShell = getColumnShell('stats-column-left');
            const rightShell = getColumnShell('stats-column-right');
            if (!leftShell?.body || !rightShell?.body) return;

            bindScrollHint(leftShell);
            bindScrollHint(rightShell);

            leftShell.body.innerHTML = '';
            rightShell.body.innerHTML = '';

            const data = Array.isArray(state.statsPanels) ? state.statsPanels : [];
            const sorted = data.slice().sort((a, b) => (a.position || a.Position || 0) - (b.position || b.Position || 0));
            const grouped = splitColumns(sorted, narrowMql.matches);

            grouped.left.forEach(item => appendPanel(leftShell.body, item));
            grouped.right.forEach(item => appendPanel(rightShell.body, item));

            rightShell.shell.style.display = grouped.right.length > 0 ? '' : 'none';

            requestAnimationFrame(() => {
                updateScrollHint(leftShell);
                updateScrollHint(rightShell);
            });

            const chartTargets = document.querySelectorAll('.gis-panel-chart[data-panel-id]');
            for (let i = 0; i < chartTargets.length; i += 1) {
                const el = chartTargets[i];
                const panelId = Number(el.getAttribute('data-panel-id'));
                const item = sorted.find(t => Number(t.id || t.Id) === panelId);
                if (!item || !window.GisChartHelper) continue;
                await window.GisChartHelper.renderChart(el, item.chartJson || item.ChartJson, theme);
            }
        }

        function resizeCharts() {
            const chartTargets = document.querySelectorAll('.gis-panel-chart[data-panel-id]');
            chartTargets.forEach(el => {
                if (el.__chartInstance && typeof el.__chartInstance.resize === 'function') {
                    el.__chartInstance.resize();
                }
            });
        }

        async function loadPanels(sceneId = null) {
            try {
                let url = `?handler=PanelData&theme=${encodeURIComponent(theme)}`;
                if (sceneId) url += `&sceneId=${sceneId}`;
                const resp = await fetch(url);
                if (!resp.ok) {
                    state.statsPanels = [];
                    await renderPanels();
                    return;
                }
                const res = await resp.json();
                const code = res?.code ?? res?.Code ?? -1;
                const data = res?.data ?? res?.Data;
                state.statsPanels = code === 0 && Array.isArray(data) ? data : [];
                await renderPanels();
            } catch {
                state.statsPanels = [];
                await renderPanels();
            }
        }

        window.addEventListener('resize', () => {
            if (resizeTimer) clearTimeout(resizeTimer);
            resizeTimer = setTimeout(() => resizeCharts(), 80);
        });

        if (typeof narrowMql.addEventListener === 'function') {
            narrowMql.addEventListener('change', () => {
                renderPanels().then(() => resizeCharts());
            });
        } else if (typeof narrowMql.addListener === 'function') {
            narrowMql.addListener(() => {
                renderPanels().then(() => resizeCharts());
            });
        }

        return {
            loadPanels,
            renderPanels,
            resizeCharts,
            updateScrollHint,
        };
    }

    window.GisIndexPanels = { create };
})();
