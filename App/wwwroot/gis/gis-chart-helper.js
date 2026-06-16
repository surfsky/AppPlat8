/**
 * 图表绘制相关
 */
(function () {
    function safeParseJson(text, fallbackValue) {
        if (!text || typeof text !== 'string') return fallbackValue;
        try {
            return JSON.parse(text);
        } catch {
            return fallbackValue;
        }
    }

    function getByPath(obj, path) {
        if (!path || !obj) return obj;
        const parts = String(path).split('.').filter(Boolean);
        let current = obj;
        for (let i = 0; i < parts.length; i += 1) {
            if (current == null) return null;
            current = current[parts[i]];
        }
        return current;
    }

    function normalizeSeries(series) {
        if (!Array.isArray(series)) return [];
        return series.map((item, idx) => {
            if (Array.isArray(item)) {
                return { name: `系列${idx + 1}`, data: item };
            }
            if (item && typeof item === 'object') {
                return {
                    name: item.name || `系列${idx + 1}`,
                    data: Array.isArray(item.data) ? item.data : []
                };
            }
            return { name: `系列${idx + 1}`, data: [] };
        });
    }

    function buildOption(chartCfg, theme) {
        const cfg = chartCfg || {};
        const type = cfg.type || 'line';
        const source = cfg.source || {};
        const json = source.json || {};
        const isDark = (theme || 'dark') !== 'light';
        const textColor = isDark ? '#f8fafc' : '#334155';
        const subTextColor = isDark ? '#e2e8f0' : '#64748b';
        const splitLineColor = isDark ? 'rgba(226,232,240,0.28)' : 'rgba(100,116,139,0.16)';
        const axisLineColor = isDark ? 'rgba(226,232,240,0.68)' : 'rgba(100,116,139,0.35)';
        const tooltipBg = isDark ? 'rgba(15,23,42,0.92)' : 'rgba(255,255,255,0.96)';
        const tooltipText = isDark ? '#f8fafc' : '#0f172a';
        const tooltipBorder = isDark ? 'rgba(148,163,184,0.45)' : 'rgba(148,163,184,0.35)';

        if (type === 'pie') {
            const pieData = Array.isArray(json.data) ? json.data : [];
            return {
                title: { text: cfg.title || '', left: 'center', textStyle: { color: textColor, fontSize: 14 } },
                tooltip: {
                    trigger: 'item',
                    backgroundColor: tooltipBg,
                    borderColor: tooltipBorder,
                    textStyle: { color: tooltipText }
                },
                legend: { bottom: 0, textStyle: { color: subTextColor } },
                series: [{
                    type: 'pie',
                    radius: ['38%', '68%'],
                    center: ['50%', '44%'],
                    data: pieData,
                    label: { color: textColor }
                }]
            };
        }

        const categories = Array.isArray(json.categories) ? json.categories : [];
        const series = normalizeSeries(json.series).map(s => ({
            name: s.name,
            type,
            data: s.data,
            smooth: type === 'line'
        }));

        return {
            title: { text: cfg.title || '', left: 'center', textStyle: { color: textColor, fontSize: 14 } },
            tooltip: {
                trigger: 'axis',
                backgroundColor: tooltipBg,
                borderColor: tooltipBorder,
                textStyle: { color: tooltipText }
            },
            legend: { top: 28, textStyle: { color: subTextColor } },
            grid: { left: 36, right: 16, top: 62, bottom: 28, containLabel: true },
            xAxis: {
                type: 'category',
                data: categories,
                axisLabel: { color: subTextColor },
                axisLine: { lineStyle: { color: axisLineColor } },
                axisTick: { lineStyle: { color: axisLineColor } }
            },
            yAxis: {
                type: 'value',
                axisLabel: { color: subTextColor },
                axisLine: { lineStyle: { color: axisLineColor } },
                axisTick: { lineStyle: { color: axisLineColor } },
                splitLine: { lineStyle: { color: splitLineColor } }
            },
            series
        };
    }

    async function resolveSourceJson(chartCfg) {
        const cfg = chartCfg || {};
        const source = cfg.source || {};
        if ((source.mode || 'json') !== 'api') {
            return source.json || {};
        }

        const api = source.api || {};
        if (!api.url) return {};

        try {
            const resp = await fetch(api.url, { method: api.method || 'GET' });
            if (!resp.ok) return {};
            const raw = await resp.json();
            const data = getByPath(raw, api.dataPath || 'data');
            if (data && typeof data === 'object') return data;
            return {};
        } catch {
            return {};
        }
    }

    async function renderChart(container, chartJsonText, theme) {
        if (!container || !window.echarts) return false;
        const cfg = typeof chartJsonText === 'string' ? safeParseJson(chartJsonText, null) : chartJsonText;
        if (!cfg || typeof cfg !== 'object') return false;

        const sourceJson = await resolveSourceJson(cfg);
        const runtimeCfg = {
            ...cfg,
            source: {
                ...(cfg.source || {}),
                json: sourceJson
            }
        };
        const option = buildOption(runtimeCfg, theme);
        if (cfg.options && typeof cfg.options === 'object') {
            Object.assign(option, cfg.options);
        }

        if (container.__chartInstance && typeof container.__chartInstance.dispose === 'function') {
            container.__chartInstance.dispose();
        }
        const chart = echarts.init(container, theme === 'light' ? null : 'dark');
        chart.setOption(option, true);
        container.__chartInstance = chart;
        return true;
    }

    window.GisChartHelper = {
        safeParseJson,
        buildOption,
        renderChart,
    };
})();
