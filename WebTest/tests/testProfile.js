const puppeteer = require('puppeteer');

module.exports = async (browser) => {
    const page = await browser.newPage();
    
    // Login first
    await page.goto('http://localhost:5000/Login', { waitUntil: 'domcontentloaded' });
    
    // Slider hack
    const slider = await page.$('.slider-btn');
    if (slider) {
        const sliderBox = await slider.boundingBox();
        const container = await page.$('.slider-container');
        const containerBox = await container.boundingBox();
        
        await page.mouse.move(sliderBox.x + 5, sliderBox.y + 5);
        await page.mouse.down();
        await page.mouse.move(sliderBox.x + containerBox.width, sliderBox.y + 5, { steps: 10 });
        await page.mouse.up();
        await new Promise(r => setTimeout(r, 500));
    }
    
    await page.type('input[placeholder="账户"]', 'admin');
    await page.type('input[placeholder="密码"]', 'admin');
    
    const loginBtn = await page.$('button.el-button--primary');
    if (loginBtn) {
        await loginBtn.click();
        await page.waitForNavigation({ waitUntil: 'networkidle0' });
    }

    console.log('Logged in, navigating to Profile...');
    
    // Go to Profile
    await page.goto('http://localhost:5000/Profile', { waitUntil: 'networkidle0' });
    
    // Check for some content
    const title = await page.evaluate(() => {
        const h1 = document.querySelector('h1');
        return h1 ? h1.textContent.trim() : null;
    });
    
    console.log('Profile Title:', title);
    
    if (title && (title.includes('admin') || title.includes('管理员'))) {
        console.log('Profile Page Verified!');
    } else {
        console.error('Profile Page Verification Failed!');
    }
    
    await page.screenshot({ path: 'AppPlat/WebTest/profile_page.png' });
    
    return page;
};
