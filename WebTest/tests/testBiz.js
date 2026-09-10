const puppeteer = require('puppeteer');
module.exports = async (browser) => {
    const page = await browser.newPage();
    await page.goto('http://localhost:5000/Login', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('.slider-btn', { visible: true });
    
    // Slider hack
    const slider = await page.$('.slider-btn');
    const container = await page.$('.slider-container');
    const sliderBox = await slider.boundingBox();
    const containerBox = await container.boundingBox();
    const dragDistance = containerBox.width - sliderBox.width;
    await page.mouse.move(sliderBox.x + sliderBox.width / 2, sliderBox.y + sliderBox.height / 2);
    await page.mouse.down();
    await page.mouse.move(sliderBox.x + dragDistance + sliderBox.width / 2 + 5, sliderBox.y + sliderBox.height / 2);
    await new Promise(r => setTimeout(r, 200));
    await page.mouse.up();
    await new Promise(r => setTimeout(r, 500));
    
    await page.type('input[placeholder="账户"]', 'admin');
    await page.type('input[placeholder="密码"]', 'admin');
    const loginBtn = await page.$('button.el-button--primary');
    await loginBtn.click();
    try { await page.waitForNavigation({ waitUntil: 'networkidle0', timeout: 5000 }); } catch (e) {}
    
    const pages = [
        '/Biz/CheckObjects',
        '/Biz/CheckTasks',
        '/Biz/CheckLogs',
        '/Biz/CheckHazards'
    ];

    for (const url of pages) {
        console.log(`Navigating to ${url}...`);
        await page.goto(`http://localhost:5000${url}`, { waitUntil: 'networkidle0' });
        await new Promise(r => setTimeout(r, 1000));
        
        const error = await page.$('.el-message--error');
        if (error) {
            const text = await page.evaluate(el => el.textContent, error);
            console.error(`${url} Page Error:`, text);
        } else {
            console.log(`${url} Page Loaded.`);
        }
        
        // Check if table exists
        const table = await page.$('.el-table');
        if (!table) console.warn(`${url} table not found (might be intentional if empty)`);
    }

    return page;
};
