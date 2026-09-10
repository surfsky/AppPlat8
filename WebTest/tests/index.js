const puppeteer = require('puppeteer');
const testLogin = require('./tests/testLogin');
const testConfigButtons = require('./tests/testConfigButtons');
const testArticleCategories = require('./tests/testArticleCategories');

(async () => {
    console.log('Starting Test Suite...');
    const browser = await puppeteer.launch({
        headless: true,
        args: ['--no-sandbox', '--disable-setuid-sandbox']
    });

    try {
        // 1. Login
        const page = await testLogin(browser);

        // 2. Test Config Buttons (Includes Assets Page check)
        await testConfigButtons(browser, page);

        // 3. Test Article Categories
        await testArticleCategories(browser, page);

        console.log('All tests completed.');
    } catch (error) {
        console.error('Test Suite Failed:', error);
    } finally {
        await browser.close();
    }
})();
