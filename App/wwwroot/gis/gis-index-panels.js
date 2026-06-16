/**
 * 面板相关逻辑
 */
(function () {
    function create(ctx) {
        const state = ctx.state;
        const theme = ctx.theme || 'dark';
        let resizeTimer = null;
        const narrowMql = window.matchMedia('(max-width: 900px)');

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
            panel.setAttribute('height', '100%');
            panel.innerHTML = buildBodyHtml(item);
            parent.appendChild(panel);
            return panel;
        }

        async function renderPanels() {
            const leftCol = document.getElementById('stats-column-left');
            const rightCol = document.getElementById('stats-column-right');
            if (!leftCol || !rightCol) return;

            leftCol.innerHTML = '';
            rightCol.innerHTML = '';

            const data = Array.isArray(state.statsPanels) ? state.statsPanels : [];
            const sorted = data.slice().sort((a, b) => (a.position || a.Position || 0) - (b.position || b.Position || 0));
            const grouped = splitColumns(sorted, narrowMql.matches);

            grouped.left.forEach(item => appendPanel(leftCol, item));
            grouped.right.forEach(item => appendPanel(rightCol, item));

            rightCol.style.display = grouped.right.length > 0 ? '' : 'none';

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
        };
    }

    window.GisIndexPanels = { create };
})();
