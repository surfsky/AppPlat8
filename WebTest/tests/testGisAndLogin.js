const puppeteer = require('puppeteer');

module.exports = async (browser) => {
    const page = await browser.newPage();
    page.setViewport({ width: 1280, height: 800 });

    console.log('Testing GIS Dashboard...');
    // Access GIS Dashboard directly (it should not require auth or layout handles it)
    await page.goto('http://localhost:5000/Gis/GisIndex', { waitUntil: 'networkidle0' });
    
    // Check Title
    const title = await page.$eval('h1', el => el.textContent);
    console.log('GIS Page Title:', title);
    if (!title.includes('智慧城市驾驶舱')) console.error('GIS Page Title Mismatch');

    // Check Map
    const map = await page.$('#map');
    if (map) console.log('Map container found');
    else console.error('Map container not found');

    // Check Panels
    const leftPanel = await page.$('.panel-left');
    const rightPanel = await page.$('.panel-right');
    if (leftPanel && rightPanel) console.log('Panels found');
    else console.error('Panels missing');
    
    // Check Login Page 403
    console.log('Testing Login Page Access...');
    await page.goto('http://localhost:5000/Login', { waitUntil: 'networkidle0' });
    const loginTitle = await page.title();
    console.log('Login Page Title:', loginTitle);
    
    // Check if 403 or content loaded
    const bodyText = await page.$eval('body', el => el.textContent);
    if (bodyText.includes('403') || bodyText.includes('Access Denied')) {
        console.error('Login Page returned 403!');
    } else {
        console.log('Login Page loaded successfully (no 403).');
    }

    return page;
};
