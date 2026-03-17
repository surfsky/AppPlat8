const puppeteer = require('puppeteer');
module.exports = async (browser) => {
    const page = await browser.newPage();
    page.setDefaultNavigationTimeout(60000); 
    page.setDefaultTimeout(60000);

    // Forward console logs
    page.on('console', msg => console.log('PAGE LOG:', msg.text()));

    await page.goto('http://localhost:5000/Login', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('.slider-btn', { visible: true });
    
    // Slider hack - SLOW DOWN
    const slider = await page.$('.slider-btn');
    const container = await page.$('.slider-container');
    const sliderBox = await slider.boundingBox();
    const containerBox = await container.boundingBox();
    const dragDistance = containerBox.width - sliderBox.width;
    
    // Move to start
    await page.mouse.move(sliderBox.x + sliderBox.width / 2, sliderBox.y + sliderBox.height / 2);
    await page.mouse.down();
    
    // Drag slowly in steps
    const steps = 20;
    const stepSize = (dragDistance + 5) / steps;
    for (let i = 1; i <= steps; i++) {
        await page.mouse.move(
            sliderBox.x + sliderBox.width / 2 + stepSize * i, 
            sliderBox.y + sliderBox.height / 2
        );
        await new Promise(r => setTimeout(r, 50)); // Wait 50ms per step
    }
    
    await new Promise(r => setTimeout(r, 500)); // Pause at end
    await page.mouse.up();
    await new Promise(r => setTimeout(r, 1000)); // Wait for verify
    
    await page.type('input[placeholder="账户"]', 'admin');
    await page.type('input[placeholder="密码"]', 'admin');
    const loginBtn = await page.$('button.el-button--primary');
    await loginBtn.click();
    
    try {
        await page.waitForNavigation({ waitUntil: 'networkidle0', timeout: 60000 });
    } catch (e) {
        console.log('Navigation timeout or already handled');
    }
    
    console.log('Navigating to Icons...');
    await page.goto('http://localhost:5000/Dev/Icons', { waitUntil: 'networkidle0' });
    
    // Debug info from page context
    const debugInfo = await page.evaluate(() => {
        return {
            url: window.location.href,
            hasVue: typeof Vue !== 'undefined',
            hasElementPlus: typeof ElementPlus !== 'undefined',
            hasIcons: typeof ElementPlusIconsVue !== 'undefined',
            iconCount: typeof ElementPlusIconsVue !== 'undefined' ? Object.keys(ElementPlusIconsVue).length : -1,
            appDiv: !!document.getElementById('app'),
            vueDataIcons: document.querySelector('#app') ? 'Unable to inspect Vue data directly without devtools' : 'No App'
        };
    });
    console.log('Debug Info:', debugInfo);

    // Wait for Vue to render icons
    await new Promise(r => setTimeout(r, 3000));
    
    // Check if icons are rendered
    const icons = await page.$$('.el-icon');
    console.log('Number of icons found:', icons.length);
    
    const countText = await page.evaluate(() => {
        const el = document.querySelector('.ml-auto.text-gray-400');
        return el ? el.textContent : null;
    });
    console.log('Count text:', countText);

    if (icons.length > 100) {
        console.log('Icons page loaded successfully with many icons.');
    } else {
        console.error('Too few icons found on the page. Something might be wrong.');
    }

    return page;
};
