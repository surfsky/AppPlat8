const puppeteer = require('puppeteer');

(async () => {
    const PORT = 50808; // 动态端口
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
        }

        // 2. 访问 MenuItemForm
        console.log('Navigating to MenuItemForm...');
        await page.goto(`${BASE_URL}/Maintains/MenuItemForm?id=3`, { waitUntil: 'networkidle0' });

        // 3. 打开图标选择器
        console.log('Opening Icon Chooser...');
        const addBtn = await page.waitForSelector('.ele-icon-selector .border-dashed', { timeout: 5000 });
        if (addBtn) {
            await addBtn.click();
            await new Promise(r => setTimeout(r, 2000)); // 等待弹窗和 iframe 加载
            
            // 4. 选择图标
            // 需要进入 iframe
            // Puppeteer 处理 iframe 比较麻烦，需要找到 frame
            const frames = page.frames();
            const iconFrame = frames.find(f => f.url().includes('IconChooser'));
            
            if (iconFrame) {
                console.log('IconChooser frame found.');
                // 等待图标渲染
                await iconFrame.waitForSelector('.icon-item');
                const firstIcon = await iconFrame.$('.icon-item');
                if (firstIcon) {
                    console.log('Clicking first icon...');
                    await firstIcon.click();
                    await new Promise(r => setTimeout(r, 1000)); // 等待关闭和回填
                    
                    // 5. 验证是否回填
                    // 检查页面上是否有 .ele-icon-selector .relative (预览模式)
                    const preview = await page.$('.ele-icon-selector .relative');
                    if (preview) {
                        console.log('PASS: Icon preview is visible.');
                        // 获取图标类名
                        const iconClass = await page.evaluate(() => {
                            const i = document.querySelector('.ele-icon-selector i');
                            return i ? i.className : null;
                        });
                        console.log(`Icon Class: ${iconClass}`);
                        
                        await page.screenshot({ path: 'AppPlat/WebTest/icon_selected.png' });
                    } else {
                        console.error('FAIL: Icon preview NOT visible.');
                        await page.screenshot({ path: 'AppPlat/WebTest/icon_failed.png' });
                    }
                } else {
                    console.error('FAIL: No icons found in chooser.');
                }
            } else {
                console.error('FAIL: IconChooser frame not found.');
            }
        } else {
            console.error('FAIL: Add button not found.');
        }

    } catch (e) {
        console.error('Error:', e);
    } finally {
        console.log('Test finished.');
        // await browser.close();
    }
})();
