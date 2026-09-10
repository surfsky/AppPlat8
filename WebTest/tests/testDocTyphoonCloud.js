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
    page.setViewport({ width: 1440, height: 960 });
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

    const url = 'http://127.0.0.1:6061/Doc/Map/Cloud.html';
    const shotDir = path.join(__dirname, 'screenshots');
    const shotFile1 = path.join(shotDir, 'Doc_Cloud_typhoon_cma_202527.png');
    const shotFile2 = path.join(shotDir, 'Doc_Cloud_typhoon_live_202607.png');
    fs.mkdirSync(shotDir, { recursive: true });

    try {
        console.log('open:', url);
        await page.goto(url, { waitUntil: 'networkidle2', timeout: 60000 });
        await page.waitForSelector('#typhoon', { timeout: 30000 });
        await page.waitForFunction(() => {
            const map = window.map;
            const meta = document.getElementById('globalMeta')?.innerText || '';
            return !!(map && map.isStyleLoaded && map.isStyleLoaded() && meta.includes('最后刷新'));
        }, { timeout: 30000 });
        await page.click('#typhoon');
        await page.waitForSelector('#typhoonYearSelect', { timeout: 30000 });
        await page.select('#typhoonYearSelect', '2025');
        await page.waitForSelector('#typhoonSelect', { timeout: 30000 });

        const optVal = await page.evaluate(() => {
            const sel = document.getElementById('typhoonSelect');
            const opt = Array.from(sel.options).find(x => x.value === 'history:202527');
            return opt ? opt.value : '';
        });
        if (!optVal) throw new Error('未找到 202527 台风选项');

        await page.select('#typhoonSelect', 'history:202527');
        await page.waitForFunction(() => {
            const map = window.map;
            const src = map && map.getSource && map.getSource('typhoon-source');
            const data = src && src._data;
            if (!data || !Array.isArray(data.features)) return false;
            const cnt = data.features.length;
            const hasLine = data.features.some(x => x?.properties?.kind === 'history-path');
            const hasPoint = data.features.some(x => x?.properties?.kind === 'track-point');
            return cnt > 10 && hasLine && hasPoint;
        }, { timeout: 30000 });

        await new Promise(resolve => setTimeout(resolve, 1200));
        const historyResult = await page.evaluate(() => {
            const info = document.getElementById('typhoonInfo')?.innerText || '';
            const src = window.map.getSource('typhoon-source');
            const data = src && src._data;
            const features = data && Array.isArray(data.features) ? data.features : [];
            return {
                info,
                featureCnt: features.length,
                lineCnt: features.filter(x => x?.properties?.kind === 'history-path').length,
                pointCnt: features.filter(x => x?.properties?.kind === 'track-point').length,
                nameCnt: features.filter(x => x?.properties?.kind === 'name-label').length
            };
        });

        console.log('history:', JSON.stringify(historyResult, null, 2));
        await page.screenshot({ path: shotFile1, fullPage: true });
        console.log('screenshot:', shotFile1);

        await page.select('#typhoonYearSelect', '2026');
        await page.waitForSelector('#typhoonSelect', { timeout: 30000 });
        const currentOpt = await page.evaluate(() => {
            const sel = document.getElementById('typhoonSelect');
            const opt = Array.from(sel.options).find(x => x.value === 'history:202607');
            return opt ? opt.value : '';
        });
        if (!currentOpt) throw new Error('未找到 202607 台风选项');

        await page.select('#typhoonSelect', 'history:202607');
        await page.waitForFunction(() => {
            const map = window.map;
            const src = map && map.getSource && map.getSource('typhoon-source');
            const data = src && src._data;
            if (!data || !Array.isArray(data.features)) return false;
            const hasLine = data.features.some(x => x?.properties?.kind === 'history-path');
            const hasForecast = data.features.some(x => x?.properties?.kind === 'agency-path');
            const hasCircle = data.features.some(x => x?.properties?.kind === 'prob-circle');
            const info = document.getElementById('typhoonInfo')?.innerText || '';
            return hasLine && hasForecast && hasCircle && info.includes('当前台风');
        }, { timeout: 30000 });

        await new Promise(resolve => setTimeout(resolve, 1500));
        const currentResult = await page.evaluate(() => {
            const info = document.getElementById('typhoonInfo')?.innerText || '';
            const src = window.map.getSource('typhoon-source');
            const data = src && src._data;
            const features = data && Array.isArray(data.features) ? data.features : [];
            return {
                info,
                featureCnt: features.length,
                historyCnt: features.filter(x => x?.properties?.kind === 'history-path').length,
                forecastCnt: features.filter(x => x?.properties?.kind === 'agency-path').length,
                probCnt: features.filter(x => x?.properties?.kind === 'prob-circle').length,
                pointCnt: features.filter(x => x?.properties?.kind === 'track-point').length
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
