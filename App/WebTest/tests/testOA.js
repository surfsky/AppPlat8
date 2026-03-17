const puppeteer = require('puppeteer');
module.exports = async (browser) => {
    const page = await browser.newPage();
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
    try { await page.waitForNavigation({ waitUntil: 'networkidle0', timeout: 5000 }); } catch (e) {}
    
    console.log('Navigating to Articles...');
    await page.goto('http://localhost:5000/OA/Articles', { waitUntil: 'networkidle0' });
    await new Promise(r => setTimeout(r, 1000));
    const articleError = await page.$('.el-message--error');
    if (articleError) console.error('Articles Page Error:', await page.evaluate(el => el.textContent, articleError));
    else console.log('Articles Page Loaded.');

    console.log('Navigating to ProjectForm...');
    await page.goto('http://localhost:5000/OA/ProjectForm?id=0', { waitUntil: 'networkidle0' });
    await new Promise(r => setTimeout(r, 1000));
    const projectError = await page.$('.el-message--error');
    if (projectError) console.error('ProjectForm Page Error:', await page.evaluate(el => el.textContent, projectError));
    else console.log('ProjectForm Page Loaded.');
    
    console.log('Navigating to RoleForm...');
    await page.goto('http://localhost:5000/Admins/RoleForm?id=0', { waitUntil: 'networkidle0' });
    await new Promise(r => setTimeout(r, 1000));
    const roleError = await page.$('.el-message--error');
    if (roleError) console.error('RoleForm Page Error:', await page.evaluate(el => el.textContent, roleError));
    else console.log('RoleForm Page Loaded.');

    return page;
};
