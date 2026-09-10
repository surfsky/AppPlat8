const fs = require('fs');
const path = require('path');
const puppeteer = require('puppeteer');

/**Sleep */
function sleep(ms) {
    return new Promise(r => setTimeout(r, ms));
}

/**Ensure dir */
function ensureDir(dir) {
    if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
}

/**Run */
async function run() {
    const host = process.env.APP_HOST || 'http://127.0.0.1:6064';
    const loginUrl = `${host}/HttpApi/Auths/Login?userName=admin&password=admin&verifyCode=key-987654321`;
    const outDir = path.join(__dirname, 'screenshots');
    ensureDir(outDir);

    const browser = await puppeteer.launch({
        headless: true,
        args: ['--no-sandbox', '--disable-setuid-sandbox']
    });
    try {
        const page = await browser.newPage();
        page.on('pageerror', err => console.error('[pageerror]', err.message));
        await page.setViewport({ width: 1600, height: 900, deviceScaleFactor: 1 });
        await page.goto(loginUrl, { waitUntil: 'domcontentloaded', timeout: 60000 });
        await page.goto(`${host}/GIS/Index`, { waitUntil: 'networkidle2', timeout: 60000 });
        await page.waitForFunction(() => !!window.__gisIndexContext && !!window.__gisIndexOverlayApi, { timeout: 60000 });
        await page.evaluate(() => {
            const map = window.__gisIndexContext.map;
            map.setProjection('globe');
            map.easeTo({ center: [128, 18], zoom: 4.6, pitch: 0, bearing: 0, duration: 0 });
        });
        await page.click('#btn-layer-tab-weather');
        await page.waitForSelector('#view-overlay-menu #typhoon', { timeout: 30000 });
        await page.click('#view-overlay-menu #typhoon');
        await page.waitForSelector('#typhoonYearSelect', { timeout: 30000 });
        await page.select('#typhoonYearSelect', '2026');
        await page.waitForSelector('#typhoonSelect', { timeout: 30000 });
        await page.select('#typhoonSelect', '202607');
        await page.waitForFunction(() => {
            const map = window.__gisIndexContext?.map;
            const src = map?.getSource?.('typhoon-source');
            const data = src?._data;
            return Array.isArray(data?.features) && data.features.some(x => x?.properties?.kind === 'storm-center');
        }, { timeout: 40000 });
        await sleep(1200);

        const result = await page.evaluate(() => {
            const map = window.__gisIndexContext.map;
            const src = map.getSource('typhoon-source');
            const feats = src?._data?.features || [];
            const center = feats.find(x => x?.properties?.kind === 'storm-center');
            const current = feats.find(x => x?.properties?.kind === 'track-point' && x?.properties?.isCurrent === 1);
            const centerCoord = center?.geometry?.coordinates || null;
            const currentCoord = current?.geometry?.coordinates || null;
            const p1 = centerCoord ? map.project(centerCoord) : null;
            const p2 = currentCoord ? map.project(currentCoord) : null;
            return {
                projection: map.getProjection?.()?.name || '',
                centerCoord,
                currentCoord,
                centerScreen: p1 ? { x: +p1.x.toFixed(2), y: +p1.y.toFixed(2) } : null,
                currentScreen: p2 ? { x: +p2.x.toFixed(2), y: +p2.y.toFixed(2) } : null,
                delta: p1 && p2 ? {
                    dx: +(p1.x - p2.x).toFixed(2),
                    dy: +(p1.y - p2.y).toFixed(2)
                } : null
            };
        });

        const shot = path.join(outDir, 'App_GIS_typhoon_globe_202607.png');
        await page.screenshot({ path: shot, fullPage: true });
        console.log(JSON.stringify({ ...result, screenshot: shot }, null, 2));
    } finally {
        await browser.close();
    }
}

run().catch(err => {
    console.error(err);
    process.exit(1);
});
