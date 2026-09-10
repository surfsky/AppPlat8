const puppeteer = require('puppeteer');

/**Sleep */
function sleep(ms) {
    return new Promise(r => setTimeout(r, ms));
}

/**Open app page */
async function openApp(page, url) {
    await page.goto(url, { waitUntil: 'networkidle2', timeout: 60000 });
    await page.waitForFunction(() => !!window.__gisIndexContext && !!window.__gisIndexOverlayApi, { timeout: 60000 });
    await page.click('#btn-layer-tab-weather');
    await page.waitForSelector('#view-overlay-menu #typhoon', { timeout: 30000 });
    await page.click('#view-overlay-menu #typhoon');
    await page.waitForSelector('#typhoonYearSelect', { timeout: 30000 });
    await page.select('#typhoonYearSelect', '2026');
    await page.waitForSelector('#typhoonSelect', { timeout: 30000 });
    await page.select('#typhoonSelect', '202607');
}

/**Open doc page */
async function openDoc(page, url) {
    await page.goto(url, { waitUntil: 'networkidle2', timeout: 60000 });
    await page.waitForFunction(() => !!window.map, { timeout: 60000 });
    await page.waitForSelector('#layer-cmaTyphoon', { timeout: 30000 });
    const checked = await page.$eval('#layer-cmaTyphoon', el => !!el.checked);
    if (!checked) await page.click('#layer-cmaTyphoon');
    await page.waitForSelector('#typhoonYearSelect', { timeout: 30000 });
    await page.select('#typhoonYearSelect', '2026');
    await page.waitForSelector('#typhoonSelect', { timeout: 30000 });
    await page.select('#typhoonSelect', 'history:202607');
}

/**Collect marker info */
async function collect(page, kind) {
    await page.waitForFunction(() => !!document.querySelector('.typhoon-center-marker svg'), { timeout: 40000 });
    await sleep(1200);
    return await page.evaluate((kind) => {
        const map = kind === 'app' ? window.__gisIndexContext?.map : window.map;
        const marker = document.querySelector('.typhoon-center-marker');
        const svg = marker?.querySelector('svg');
        const eye = marker?.querySelector('#eye');
        const markerRect = marker?.getBoundingClientRect?.();
        const eyeRect = eye?.getBoundingClientRect?.();
        const src = map?.getSource?.('typhoon-source');
        const data = src?._data;
        const features = Array.isArray(data?.features) ? data.features : [];
        const curPoint = features.find(x => x?.properties?.kind === 'track-point' && x?.properties?.isCurrent === 1);
        const center = curPoint?.geometry?.coordinates || null;
        const screen = center && map?.project ? map.project(center) : null;
        return {
            kind,
            markerRect: markerRect ? {
                left: markerRect.left,
                top: markerRect.top,
                width: markerRect.width,
                height: markerRect.height,
                cx: markerRect.left + markerRect.width / 2,
                cy: markerRect.top + markerRect.height / 2
            } : null,
            eyeRect: eyeRect ? {
                left: eyeRect.left,
                top: eyeRect.top,
                width: eyeRect.width,
                height: eyeRect.height,
                cx: eyeRect.left + eyeRect.width / 2,
                cy: eyeRect.top + eyeRect.height / 2
            } : null,
            projected: screen ? { x: screen.x, y: screen.y } : null,
            deltaMarker: markerRect && screen ? {
                dx: Number((markerRect.left + markerRect.width / 2 - screen.x).toFixed(2)),
                dy: Number((markerRect.top + markerRect.height / 2 - screen.y).toFixed(2))
            } : null,
            deltaEye: eyeRect && screen ? {
                dx: Number((eyeRect.left + eyeRect.width / 2 - screen.x).toFixed(2)),
                dy: Number((eyeRect.top + eyeRect.height / 2 - screen.y).toFixed(2))
            } : null,
            markerHtml: marker?.innerHTML?.slice(0, 300) || '',
            viewBox: svg?.getAttribute('viewBox') || '',
            markerStyle: marker ? {
                width: getComputedStyle(marker).width,
                height: getComputedStyle(marker).height,
                display: getComputedStyle(marker).display
            } : null
        };
    }, kind);
}

/**Run */
async function run() {
    const browser = await puppeteer.launch({
        headless: true,
        args: ['--no-sandbox', '--disable-setuid-sandbox']
    });
    try {
        const page1 = await browser.newPage();
        page1.on('pageerror', err => console.error('[app-pageerror]', err.message));
        await openApp(page1, process.env.APP_HOST || 'http://127.0.0.1:6062/GIS/Index');
        const appResult = await collect(page1, 'app');
        console.log('app=', JSON.stringify(appResult, null, 2));

        const page2 = await browser.newPage();
        page2.on('pageerror', err => console.error('[doc-pageerror]', err.message));
        await openDoc(page2, process.env.DOC_HOST || 'http://127.0.0.1:6061/Doc/Map/Cloud.html');
        const docResult = await collect(page2, 'doc');
        console.log('doc=', JSON.stringify(docResult, null, 2));
    } finally {
        await browser.close();
    }
}

run().catch(err => {
    console.error(err);
    process.exit(1);
});
