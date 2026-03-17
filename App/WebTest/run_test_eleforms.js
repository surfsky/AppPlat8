const puppeteer = require('puppeteer');
const testEleForms = require('./tests/testEleForms');

(async () => {
    console.log('Launching browser...');
    const browser = await puppeteer.launch({
        headless: true,
        args: ['--no-sandbox', '--disable-setuid-sandbox']
    });
    
    // Use the port provided by user or default to 5000
    // User provided: http://127.0.0.1:57390/Dev/EleUI/EleForms
    // So base url is http://127.0.0.1:57390
    const baseUrl = process.argv[2] || 'http://127.0.0.1:57390';
    
    try {
        await testEleForms(browser, baseUrl);
    } catch (e) {
        console.error('Test execution failed:', e);
    } finally {
        await browser.close();
        console.log('Browser closed.');
    }
})();
