const puppeteer = require('puppeteer');
module.exports = async (browser) => {
    const page = await browser.newPage();
    page.setDefaultNavigationTimeout(60000); 
    page.setDefaultTimeout(60000);
    
    page.on('console', msg => console.log('PAGE LOG:', msg.text()));
    page.on('pageerror', err => console.log('PAGE ERROR:', err.toString()));

    // Try accessing directly without login first
    console.log('Navigating to API page directly...');
    await page.goto('http://localhost:5000/Dev/API', { waitUntil: 'networkidle0' });
    
    // Check if redirected to login
    const url = page.url();
    console.log('Current URL:', url);
    
    if (url.includes('Login')) {
        console.log('Redirected to Login. Performing login...');
        await page.waitForSelector('.slider-btn', { visible: true });
        
        // Slider hack - SLOW DOWN
        const slider = await page.$('.slider-btn');
        const container = await page.$('.slider-container');
        const sliderBox = await slider.boundingBox();
        const containerBox = await container.boundingBox();
        const dragDistance = containerBox.width - sliderBox.width;
        
        await page.mouse.move(sliderBox.x + sliderBox.width / 2, sliderBox.y + sliderBox.height / 2);
        await page.mouse.down();
        const steps = 20;
        const stepSize = (dragDistance + 5) / steps;
        for (let i = 1; i <= steps; i++) {
            await page.mouse.move(
                sliderBox.x + sliderBox.width / 2 + stepSize * i, 
                sliderBox.y + sliderBox.height / 2
            );
            await new Promise(r => setTimeout(r, 50));
        }
        await new Promise(r => setTimeout(r, 500));
        await page.mouse.up();
        await new Promise(r => setTimeout(r, 1000));
        
        await page.type('input[placeholder="账户"]', 'admin');
        await page.type('input[placeholder="密码"]', 'admin');
        const loginBtn = await page.$('button.el-button--primary');
        await loginBtn.click();
        
        try { await page.waitForNavigation({ waitUntil: 'networkidle0', timeout: 60000 }); } catch (e) {
             console.log('Navigation timeout or already handled');
        }
        
        // Re-navigate if needed
        if (!page.url().includes('/Dev/API')) {
             console.log('Re-navigating to API page...');
             await page.goto('http://localhost:5000/Dev/API', { waitUntil: 'networkidle0' });
        }
    }
    
    // Wait for Vue
    await new Promise(r => setTimeout(r, 3000));
    
    // Debug info
    const debugInfo = await page.evaluate(() => {
        return {
            url: window.location.href,
            hasApp: !!document.getElementById('app'),
            hasMenu: !!document.querySelector('.menu-list'),
            menuCount: document.querySelectorAll('.menu-list li').length
        };
    });
    console.log('Debug Info:', debugInfo);

    if (debugInfo.hasMenu && debugInfo.menuCount === 4) {
        console.log('API Menu loaded correctly.');
    } else {
        console.error('API Menu not loaded correctly.');
    }

    return page;
};
