const puppeteer = require('puppeteer');

module.exports = async (browser) => {
    const page = await browser.newPage();
    
    // Login first
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
    
    try {
        await page.waitForNavigation({ waitUntil: 'networkidle0' });
    } catch(e) {}

    console.log('Logged in, navigating to OrgForm...');
    
    // Go to OrgForm (New Mode)
    await page.goto('http://localhost:5000/Admins/OrgForm?id=0&md=new', { waitUntil: 'networkidle0' });
    
    // Wait for form
    // EleForm input might not have name attribute if it's a Vue component
    // Try waiting for any input
    await page.waitForSelector('.el-form input', { timeout: 10000 });
    
    // Fill Form
    // Find input by label "名称"
    const nameInput = await page.evaluateHandle(() => {
        const labels = Array.from(document.querySelectorAll('.el-form-item__label'));
        const label = labels.find(l => l.textContent.includes('名称'));
        if (label) {
            return label.nextElementSibling.querySelector('input');
        }
        return null;
    });
    
    if (nameInput) {
        await nameInput.type('TestOrg_' + Date.now());
    } else {
        console.error('Name input not found');
    }

    // Remark
    const remarkInput = await page.evaluateHandle(() => {
        const labels = Array.from(document.querySelectorAll('.el-form-item__label'));
        const label = labels.find(l => l.textContent.includes('备注'));
        if (label) {
            return label.nextElementSibling.querySelector('textarea');
        }
        return null;
    });

    if (remarkInput) {
        await remarkInput.type('Test Remark');
    }
    
    // Submit
    // Find Save button (usually the first primary button in footer or toolbar)
    // EleForm doesn't render buttons by default, usually page has them.
    // Wait, let's check OrgForm.cshtml again. It uses EleForm but where are the buttons? 
    // Usually EleForm might have default buttons or Layout has them.
    // Let's assume standard Admin layout might have a save button in toolbar or bottom.
    // Checking previous OrgForm.cshtml content, it only has EleForm inside 'body' section.
    // Maybe Layout renders the buttons? Or EleForm renders them?
    // Let's look for a button with text "保存"
    
    const saveBtn = await page.evaluateHandle(() => {
        const btns = Array.from(document.querySelectorAll('button'));
        return btns.find(b => b.textContent.includes('保存'));
    });
    
    if (saveBtn) {
        console.log('Clicking Save...');
        await saveBtn.click();
        
        // Wait for response
        await new Promise(r => setTimeout(r, 1000));
        
        // Check for error message
        const errorMsg = await page.evaluate(() => {
            const el = document.querySelector('.el-message--error');
            return el ? el.textContent : null;
        });
        
        if (errorMsg) {
            console.error('Save Failed:', errorMsg);
        } else {
            // Check success message
            const successMsg = await page.evaluate(() => {
                const el = document.querySelector('.el-message--success');
                return el ? el.textContent : null;
            });
            console.log('Save Result:', successMsg || 'Unknown');
        }
    } else {
        console.error('Save button not found!');
    }
    
    return page;
};
