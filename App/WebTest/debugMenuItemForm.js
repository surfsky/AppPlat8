const puppeteer = require('puppeteer');

(async () => {
    // 端口号，请根据实际情况修改
    const PORT = 5000; 
    const BASE_URL = `http://127.0.0.1:${PORT}`;

    console.log(`Connecting to ${BASE_URL}...`);

    const browser = await puppeteer.launch({
        headless: false,
        defaultViewport: null,
        args: ['--start-maximized']
    });

    const page = await browser.newPage();

    try {
        // 1. 登录
        console.log('Navigating to Login...');
        await page.goto(`${BASE_URL}/Login`, { waitUntil: 'networkidle0' });

        // 处理滑块验证
        const slider = await page.$('.slider-btn');
        if (slider) {
            console.log('Handling slider...');
            const sliderBox = await slider.boundingBox();
            const container = await page.$('.slider-container');
            const containerBox = await container.boundingBox();
            
            await page.mouse.move(sliderBox.x + 5, sliderBox.y + 5);
            await page.mouse.down();
            await page.mouse.move(sliderBox.x + containerBox.width, sliderBox.y + 5, { steps: 10 });
            await page.mouse.up();
            await new Promise(r => setTimeout(r, 1000));
        }

        await page.type('input[placeholder="账户"]', 'admin');
        await page.type('input[placeholder="密码"]', 'admin');
        
        const loginBtn = await page.$('button.el-button--primary');
        if (loginBtn) {
            await loginBtn.click();
            await page.waitForNavigation({ waitUntil: 'networkidle0' });
            console.log('Login submitted.');
        } else {
            console.error('Login button not found.');
        }

        // 2. 访问 MenuItemForm
        console.log('Navigating to MenuItemForm...');
        await page.goto(`${BASE_URL}/Maintains/MenuItemForm?id=0&md=new`, { waitUntil: 'networkidle0' });

        // 3. 检查 MenuTree 数据
        console.log('Checking MenuTree data...');
        const menuTreeData = await page.evaluate(() => {
            return window.MenuTree;
        });

        if (menuTreeData && Array.isArray(menuTreeData) && menuTreeData.length > 0) {
            console.log(`PASS: MenuTree data found with ${menuTreeData.length} root nodes.`);
            console.log('Sample node:', menuTreeData[0]);
            
            // Check property names (should be camelCase)
            if (menuTreeData[0].hasOwnProperty('label') && menuTreeData[0].hasOwnProperty('value')) {
                console.log('PASS: Node has correct properties (label, value).');
            } else {
                console.error('FAIL: Node missing expected properties (label, value). Found:', Object.keys(menuTreeData[0]));
            }
        } else {
            console.error('FAIL: MenuTree data is empty or invalid.');
        }

    } catch (e) {
        console.error('Error:', e);
    } finally {
        // await browser.close();
        console.log('Test finished. Browser kept open for inspection.');
    }
})();
