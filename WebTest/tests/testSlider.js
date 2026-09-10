const puppeteer = require('puppeteer');

module.exports = async (browser) => {
    const page = await browser.newPage();
    console.log('TestLogin: Starting slider test...');
    await page.goto('http://localhost:5000/Login', { waitUntil: 'domcontentloaded' });
    
    // Wait for slider
    await page.waitForSelector('.slider-btn', { visible: true, timeout: 60000 });
    
    // Get slider dimensions
    const slider = await page.$('.slider-btn');
    const container = await page.$('.slider-container');
    const sliderBox = await slider.boundingBox();
    const containerBox = await container.boundingBox();
    
    // Calculate drag distance
    const dragDistance = containerBox.width - sliderBox.width;
    
    // Ensure we start exactly at the center of the handle
    await page.mouse.move(sliderBox.x + sliderBox.width / 2, sliderBox.y + sliderBox.height / 2);
    await page.mouse.down();
    
    // Move in steps to simulate human behavior and trigger multiple mousemove events for trajectory
    const steps = 50; // Increase steps
    const duration = 1000; // Increase duration to ensure it is slow enough
    
    for (let i = 0; i <= steps; i++) {
        const x = sliderBox.x + (dragDistance * i / steps) + sliderBox.width / 2;
        // Keep Y consistent with slight jitter, but ensure it stays within container
        const y = sliderBox.y + sliderBox.height / 2 + (Math.random() - 0.5) * 2; 
        await page.mouse.move(x, y);
        await new Promise(r => setTimeout(r, duration / steps)); // Wait a bit
    }
    
    // Ensure we reach the end and a bit more to be safe
    await page.mouse.move(sliderBox.x + dragDistance + sliderBox.width / 2 + 5, sliderBox.y + sliderBox.height / 2);
    
    await new Promise(r => setTimeout(r, 200)); // Wait at the end
    await page.mouse.up();
    
    // Wait for verification success (slider text changes or class changes)
    try {
        await page.waitForFunction(() => {
            const text = document.querySelector('.slider-text').innerText;
            return text.includes('验证通过');
        }, { timeout: 5000 });
        console.log('TestLogin2: Slider verification passed.');
    } catch (e) {
        console.error('TestLogin2: Slider verification timed out or failed.');
        // Debug: get text
        const text = await page.$eval('.slider-text', el => el.innerText);
        console.log('Current slider text:', text);
        throw e;
    }
    
    // Fill form
    // Clear inputs first
    await page.$eval('input[placeholder="账户"]', el => el.value = '');
    await page.$eval('input[placeholder="密码"]', el => el.value = '');
    
    await page.type('input[placeholder="账户"]', 'admin');
    await page.type('input[placeholder="密码"]', 'admin'); // Assuming default password or whatever
    
    // Click login
    const loginBtn = await page.$('button.el-button--primary');
    await loginBtn.evaluate(b => b.click());
    
    // Wait for navigation
    try {
        // Wait for redirect to Index
        await page.waitForFunction(() => window.location.href.includes('Index'), { timeout: 10000 });
    } catch (e) {
        console.log('Navigation wait timed out, checking URL anyway...');
    }
    
    const url = page.url();
    if (url.includes('Index') || url.includes('OA')) {
        console.log('TestLogin2: Login successful. Current URL:', url);
    } else {
        // Retry check if navigation happened but url not matched
        console.error('TestLogin2: Login failed. Current URL:', url);
        // Take screenshot
        await page.screenshot({ path: 'files/login2_failed.png' });
    }
    
    return page;
};
