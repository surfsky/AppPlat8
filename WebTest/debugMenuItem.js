const puppeteer = require('puppeteer');

(async () => {
    // 端口号，请根据实际情况修改
    const PORT = 58853; 
    const BASE_URL = `http://127.0.0.1:${PORT}`;

    console.log(`Connecting to ${BASE_URL}...`);

    const browser = await puppeteer.launch({
        headless: false, // 有头模式，方便观察
        defaultViewport: null,
        args: ['--start-maximized'] // 最大化窗口
    });

    const page = await browser.newPage();

    try {
        // 1. 登录
        console.log('Navigating to Login...');
        await page.goto(`${BASE_URL}/Login`, { waitUntil: 'networkidle0' });

        // 处理滑块验证 (简单模拟，可能需要调整)
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
        // 假设 id=3 存在，或者用 id=0 测试新增
        await page.goto(`${BASE_URL}/Maintains/MenuItemForm?id=0&md=new`, { waitUntil: 'networkidle0' });

        // 3. 检查上级菜单树
        console.log('Checking Menu Tree...');
        // 等待 TreeSelect 加载
        await page.waitForSelector('.el-tree-select', { timeout: 5000 });
        const treeSelect = await page.$('.el-tree-select');
        if (treeSelect) {
            console.log('TreeSelect found.');
            // 点击展开
            await treeSelect.click();
            await new Promise(r => setTimeout(r, 1000));
            // 检查是否有选项
            const options = await page.$$('.el-tree-node');
            console.log(`Found ${options.length} tree nodes.`);
            if (options.length === 0) {
                console.error('FAIL: No tree nodes found in TreeSelect.');
            } else {
                console.log('PASS: Tree nodes found.');
            }
        } else {
            console.error('FAIL: TreeSelect not found.');
        }

        // 4. 检查图标选择
        console.log('Checking Icon Chooser...');
        // 查找“选择”按钮
        // 注意：Element Plus 的 append slot 可能会渲染在 input group 中
        const selectBtn = await page.evaluateHandle(() => {
            const btns = Array.from(document.querySelectorAll('button'));
            return btns.find(b => b.textContent.trim() === '选择');
        });

        if (selectBtn) {
            console.log('Select button found. Clicking...');
            await selectBtn.click();
            await new Promise(r => setTimeout(r, 1000));

            // 检查 Dialog 是否弹出
            const dialog = await page.$('.el-dialog');
            const visible = await page.evaluate(el => el && el.style.display !== 'none', dialog);
            
            if (dialog && visible) {
                console.log('PASS: Icon Dialog is visible.');
                // 检查 iframe
                const iframe = await page.$('iframe[src*="IconChooser"]');
                if (iframe) {
                    console.log('PASS: IconChooser iframe found.');
                } else {
                    console.error('FAIL: IconChooser iframe NOT found.');
                }
            } else {
                console.error('FAIL: Icon Dialog NOT visible.');
            }
        } else {
            console.error('FAIL: Select button not found.');
        }

    } catch (e) {
        console.error('Error:', e);
    } finally {
        // await browser.close();
        console.log('Test finished. Browser kept open for inspection.');
    }
})();
