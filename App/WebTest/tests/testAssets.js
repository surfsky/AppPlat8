const puppeteer = require('puppeteer');
const fs = require('fs');
const path = require('path');

module.exports = async (browser) => {
    const page = await browser.newPage();
    page.setDefaultNavigationTimeout(60000); 
    page.setDefaultTimeout(60000);
    
    // Set screen size
    await page.setViewport({width: 1280, height: 800});

    // Login
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
    
    try { await page.waitForNavigation({ waitUntil: 'networkidle0', timeout: 10000 }); } catch (e) {}

    const pages = [
        { url: '/OA/Assets', name: '固定资产' }
    ];

    const screenshotDir = path.join(__dirname, 'screenshots');
    if (!fs.existsSync(screenshotDir)){
        fs.mkdirSync(screenshotDir);
    }

    for (const p of pages) {
        console.log(`Navigating to ${p.name} (${p.url})...`);
        await page.goto(`http://localhost:5000${p.url}`, { waitUntil: 'networkidle0' });
        await new Promise(r => setTimeout(r, 1000));
        
        const error = await page.$('.el-message--error');
        if (error) {
            const text = await page.evaluate(el => el.textContent, error);
            console.error(`${p.name} Page Error:`, text);
        } else {
            // Check for 500 error on page
            const h1 = await page.$('h1');
            if (h1) {
                const text = await page.evaluate(el => el.textContent, h1);
                if (text.includes('500') || text.includes('Error')) {
                    console.error(`${p.name} Page 500 Error`);
                } else {
                    console.log(`${p.name} Page Loaded.`);
                }
            } else {
                console.log(`${p.name} Page Loaded.`);
            }
        }
        
        await page.screenshot({ path: path.join(screenshotDir, `${p.name}_Test.png`) });
    }

    return page;
};
