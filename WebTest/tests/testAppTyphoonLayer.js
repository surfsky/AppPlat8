const fs = require('fs');
const path = require('path');
const puppeteer = require('puppeteer');

/**Run test */
async function run() {
    const browser = await puppeteer.launch({
        headless: true,
        args: ['--no-sandbox', '--disable-setuid-sandbox']
    });

    const page = await browser.newPage();
    await page.setViewport({ width: 1440, height: 960 });
    page.on('console', async msg => {
        const vals = [];
        for (const arg of msg.args()) {
            try {
                vals.push(await arg.jsonValue());
            } catch (_e) {
                vals.push(msg.text());
            }
        }
        if (vals.length > 0) console.log('[page]', ...vals);
        else console.log('[page]', msg.text());
    });
    page.on('pageerror', err => console.error('[pageerror]', err.message));
    page.on('requestfailed', req => console.error('[requestfailed]', req.url(), req.failure()?.errorText || 'failed'));

    const host = process.env.APP_HOST || 'http://127.0.0.1:6062';
    const url = `${host}/GIS/Index`;
    const shotDir = path.join(__dirname, 'screenshots');
    const shotFile1 = path.join(shotDir, 'App_GIS_typhoon_202527.png');
    const shotFile2 = path.join(shotDir, 'App_GIS_typhoon_202607.png');
    fs.mkdirSync(shotDir, { recursive: true });

    try {
        console.log('open:', url);
        await page.goto(url, { waitUntil: 'networkidle2', timeout: 60000 });
        await page.waitForFunction(() => {
            return !!window.__gisIndexContext && !!window.__gisIndexOverlayApi;
        }, { timeout: 60000 });

        await page.click('#btn-layer-tab-weather');
        await page.waitForSelector('#view-overlay-menu #typhoon', { timeout: 30000 });
        await page.click('#view-overlay-menu #typhoon');
        await page.waitForSelector('#typhoonYearSelect', { timeout: 30000 });
        await page.select('#typhoonYearSelect', '2025');
        await page.waitForSelector('#typhoonSelect', { timeout: 30000 });
        await page.select('#typhoonSelect', '202527');

        await page.waitForFunction(() => {
            const map = window.__gisIndexContext?.map;
            const src = map && map.getSource && map.getSource('typhoon-source');
            const data = src && src._data;
            if (!data || !Array.isArray(data.features)) return false;
            const hasLine = data.features.some(x => x?.properties?.kind === 'history-path');
            const hasPoint = data.features.some(x => x?.properties?.kind === 'track-point');
            const info = document.getElementById('typhoonInfo')?.innerText || '';
            return hasLine && hasPoint && info.includes('202527');
        }, { timeout: 30000 });

        await new Promise(resolve => setTimeout(resolve, 1200));
        const historyResult = await page.evaluate(() => {
            const map = window.__gisIndexContext?.map;
            const src = map && map.getSource && map.getSource('typhoon-source');
            const data = src && src._data;
            const features = data && Array.isArray(data.features) ? data.features : [];
            return {
                info: document.getElementById('typhoonInfo')?.innerText || '',
                featureCnt: features.length,
                lineCnt: features.filter(x => x?.properties?.kind === 'history-path').length,
                pointCnt: features.filter(x => x?.properties?.kind === 'track-point').length,
                forecastCnt: features.filter(x => x?.properties?.kind === 'forecast-path').length,
                nameCnt: features.filter(x => x?.properties?.kind === 'name-label').length,
                legendVisible: !!document.getElementById('typhoonLegend') && !document.getElementById('typhoonLegend').classList.contains('is-hidden')
            };
        });

        console.log('history:', JSON.stringify(historyResult, null, 2));
        await page.screenshot({ path: shotFile1, fullPage: true });
        console.log('screenshot:', shotFile1);

        await page.select('#typhoonYearSelect', '2026');
        await page.waitForSelector('#typhoonSelect', { timeout: 30000 });
        await page.select('#typhoonSelect', '202607');
        await page.waitForFunction(() => {
            const map = window.__gisIndexContext?.map;
            const src = map && map.getSource && map.getSource('typhoon-source');
            const data = src && src._data;
            if (!data || !Array.isArray(data.features)) return false;
            const hasHistory = data.features.some(x => x?.properties?.kind === 'history-path');
            const hasForecast = data.features.some(x => x?.properties?.kind === 'forecast-path');
            const hasProb = data.features.some(x => x?.properties?.kind === 'prob-circle');
            const info = document.getElementById('typhoonInfo')?.innerText || '';
            return hasHistory && hasForecast && hasProb && info.includes('当前台风');
        }, { timeout: 40000 });

        await new Promise(resolve => setTimeout(resolve, 1500));
        const currentResult = await page.evaluate(() => {
            const map = window.__gisIndexContext?.map;
            const src = map && map.getSource && map.getSource('typhoon-source');
            const data = src && src._data;
            const features = data && Array.isArray(data.features) ? data.features : [];
            return {
                info: document.getElementById('typhoonInfo')?.innerText || '',
                featureCnt: features.length,
                lineCnt: features.filter(x => x?.properties?.kind === 'history-path').length,
                pointCnt: features.filter(x => x?.properties?.kind === 'track-point').length,
                forecastCnt: features.filter(x => x?.properties?.kind === 'forecast-path').length,
                probCnt: features.filter(x => x?.properties?.kind === 'prob-circle').length,
                nameCnt: features.filter(x => x?.properties?.kind === 'name-label').length
            };
        });

        console.log('current:', JSON.stringify(currentResult, null, 2));
        await page.screenshot({ path: shotFile2, fullPage: true });
        console.log('screenshot:', shotFile2);
    } finally {
        await browser.close();
    }
}

run().catch(err => {
    console.error(err);
    process.exit(1);
});
