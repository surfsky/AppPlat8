const puppeteer = require('puppeteer');

module.exports = async (browser, page) => {
    if (!page) page = await browser.newPage();
    
    console.log('TestConfigButtons: Checking Config Buttons on various pages...');
    
    const checks = [
        { url: 'http://localhost:5000/OA/Articles', buttonText: '目录管理', name: 'Articles Page' },
        { url: 'http://localhost:5000/OA/Budgets', buttonText: '类别管理', name: 'Budgets Page' },
        { url: 'http://localhost:5000/OA/Events', buttonText: '类别管理', name: 'Events Page' },
        { url: 'http://localhost:5000/OA/Assets', buttonText: '类别管理', name: 'Assets Page' },
        { url: 'http://localhost:5000/Admins/Users', buttonText: '组织管理', name: 'Users Page' }
    ];

    let allPass = true;

    for (const check of checks) {
        console.log(`Checking ${check.name}...`);
        await page.goto(check.url, { waitUntil: 'networkidle0' });
        
        const hasButton = await page.evaluate((text) => {
            const elements = Array.from(document.querySelectorAll('button, span, div, a'));
            return elements.some(el => el.innerText && el.innerText.includes(text) && (el.tagName === 'BUTTON' || el.closest('.f-btn') || el.closest('button')));
        }, check.buttonText);

        if (hasButton) {
            console.log(`PASS: "${check.buttonText}" button found on ${check.name}.`);
        } else {
            console.error(`FAIL: "${check.buttonText}" button NOT found on ${check.name}.`);
            allPass = false;
        }
    }
    
    return allPass;
};
