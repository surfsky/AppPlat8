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
    page.on('console', msg => console.log('[page]', msg.text()));
    page.on('pageerror', err => console.error('[pageerror]', err.message));

    const host = 'http://127.0.0.1:6063';
    const loginUrl = `${host}/HttpApi/Auths/Login?userName=admin&password=admin&verifyCode=key-987654321`;
    const url = `${host}/GIS/TyphoonLogs?code=202527`;
    const shotDir = path.join(__dirname, 'screenshots');
    const shotFile = path.join(shotDir, 'App_GIS_typhoon_logs_202527.png');
    fs.mkdirSync(shotDir, { recursive: true });

    try {
        console.log('login:', loginUrl);
        await page.goto(loginUrl, { waitUntil: 'domcontentloaded', timeout: 60000 });
        console.log('open:', url);
        await page.goto(url, { waitUntil: 'networkidle2', timeout: 60000 });
        if (page.url().includes('/Login')) {
            throw new Error('打开轨迹页时被重定向到登录页');
        }

        await page.waitForFunction(() => {
            const app = document.querySelector('.el-table');
            const body = document.body.innerText || '';
            return !!app && body.includes('台风轨迹清单') && body.includes('202527');
        }, { timeout: 30000 });

        const result = await page.evaluate(() => {
            const body = document.body.innerText || '';
            return {
                title: document.title || '',
                hasCode: body.includes('202527'),
                hasTime: body.includes('2025-11') || body.includes('2025-12'),
                hasLevel: body.includes('台风') || body.includes('强热带风暴')
            };
        });

        console.log('result:', JSON.stringify(result, null, 2));
        await page.screenshot({ path: shotFile, fullPage: true });
        console.log('screenshot:', shotFile);
    } finally {
        await browser.close();
    }
}

run().catch(err => {
    console.error(err);
    process.exit(1);
});
