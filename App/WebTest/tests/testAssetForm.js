const puppeteer = require('puppeteer');

(async () => {
  const browser = await puppeteer.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox']
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 1280, height: 800 });

  try {
    console.log('Logging in via API...');
    // Use API login with verifyCode backdoor
    await page.goto('http://localhost:5000/HttpApi/Auths/Login?userName=admin&password=admin&verifyCode=key-987654321', { waitUntil: 'domcontentloaded' });
    
    // The API returns JSON, so we are logged in if cookie is set.
    // Let's verify by checking content or just proceeding.
    const content = await page.content();
    console.log('Login API Response:', content);

    console.log('Navigating to AssetForm...');
    const targetUrl = 'http://localhost:5000/oa/assetForm?id=3';
    await page.goto(targetUrl, { waitUntil: 'domcontentloaded', timeout: 60000 });
    
    // Check if redirected to login
    if (page.url().includes('/Login') && !page.url().includes('HttpApi')) {
         throw new Error('Login failed, redirected to login page');
    }

    // Wait a bit for Vue to render
    await new Promise(r => setTimeout(r, 2000));

    console.log('Taking screenshot...');
    await page.screenshot({ path: '../files/assetForm.png', fullPage: true });
    
    // Check if drawer exists (hidden)
    const drawer = await page.$('.el-drawer');
    console.log('Drawer found:', !!drawer);

    // Check if form exists
    const form = await page.$('.el-form');
    console.log('Form found:', !!form);

    console.log('Test finished. Check ../files/assetForm.png');

  } catch (error) {
    console.error('Test failed:', error);
    await page.screenshot({ path: '../files/error_snapshot.png', fullPage: true });
    console.log('Saved error snapshot to ../files/error_snapshot.png');
  } finally {
    await browser.close();
  }
})();
