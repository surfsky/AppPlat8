const puppeteer = require('puppeteer');

module.exports = async (browser, baseUrl) => {
    const page = await browser.newPage();
    const targetUrl = `${baseUrl}/Dev/EleUI/EleForms`;
    console.log(`Navigating to ${targetUrl}`);
    
    // Set viewport to a reasonable size
    await page.setViewport({ width: 1280, height: 1024 });

    await page.goto(targetUrl, { waitUntil: 'networkidle0' });
    
    // Check if redirected to Login
    if (page.url().toLowerCase().includes('login')) {
        console.log('Redirected to Login, performing login...');
        // Handle slider if present (from testIcons.js logic)
        const slider = await page.$('.slider-btn');
        if (slider) {
            console.log('Handling slider...');
            const container = await page.$('.slider-container');
            const sliderBox = await slider.boundingBox();
            const containerBox = await container.boundingBox();
            const dragDistance = containerBox.width - sliderBox.width;
            
            await page.mouse.move(sliderBox.x + sliderBox.width / 2, sliderBox.y + sliderBox.height / 2);
            await page.mouse.down();
            await page.mouse.move(sliderBox.x + sliderBox.width / 2 + dragDistance, sliderBox.y + sliderBox.height / 2, { steps: 10 });
            await page.mouse.up();
            await new Promise(r => setTimeout(r, 1000));
        }

        await page.type('input[placeholder="账户"]', 'admin');
        await page.type('input[placeholder="密码"]', 'admin');
        const loginBtn = await page.$('button.el-button--primary');
        if (loginBtn) {
            await loginBtn.click();
            try {
                await page.waitForNavigation({ waitUntil: 'networkidle0', timeout: 10000 });
            } catch(e) { console.log('Navigation wait timeout'); }
        }
        
        console.log('Login attempt finished, navigating back to target...');
        await page.goto(targetUrl, { waitUntil: 'networkidle0' });
    }

    // Wait for Vue to render
    console.log('Waiting for Vue render...');
    await new Promise(r => setTimeout(r, 3000));

    // Check Icon
    // Look for the icon container or the specific class
    const iconSelector = '.ele-icon-selector i'; 
    try {
        await page.waitForSelector(iconSelector, { timeout: 5000 });
        
        // Evaluate the icon class
        const iconClasses = await page.$$eval(iconSelector, els => els.map(e => e.className));
        console.log('Found icons classes:', iconClasses);
        
        const targetIcon = iconClasses.find(c => c.includes('fas') && c.includes('fa-user'));
        
        if (targetIcon) {
            console.log('✅ SUCCESS: Icon class "fas fa-user" found.');
        } else {
            console.log('❌ FAILURE: Icon class "fas fa-user" NOT found.');
        }
    } catch (e) {
        console.log('❌ FAILURE: Icon selector not found.', e.message);
    }

    // Check EleNumber Prefix/Suffix and ControlPosition
    try {
        await page.waitForSelector('el-input-number', { timeout: 5000 });

        const hasControlRight = await page.$('el-input-number[controls-position="right"]') !== null;
        const pageText = await page.evaluate(() => document.body.innerText || '');
        const hasPrefix = pageText.includes('¥');
        const hasSuffix = pageText.includes('RMB');

        if (hasControlRight) {
            console.log('✅ SUCCESS: controls-position="right" found on EleNumber.');
        } else {
            console.log('❌ FAILURE: controls-position="right" NOT found on EleNumber.');
        }

        if (hasPrefix) {
            console.log('✅ SUCCESS: EleNumber prefix text "¥" rendered.');
        } else {
            console.log('❌ FAILURE: EleNumber prefix text "¥" NOT rendered.');
        }

        if (hasSuffix) {
            console.log('✅ SUCCESS: EleNumber suffix text "RMB" rendered.');
        } else {
            console.log('❌ FAILURE: EleNumber suffix text "RMB" NOT rendered.');
        }
    } catch (e) {
        console.log('❌ FAILURE: EleNumber prefix/suffix/control test failed.', e.message);
    }

    // Take screenshot
    const screenshotPath = 'eleforms_test_result.png';
    await page.screenshot({ path: screenshotPath, fullPage: true });
    console.log(`Screenshot saved to ${screenshotPath}`);
    
    await page.close();
};
