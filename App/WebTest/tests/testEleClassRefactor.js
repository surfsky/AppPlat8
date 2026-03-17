const puppeteer = require('puppeteer');
const login = require('./logindev.js');

(async () => {
    const browser = await puppeteer.launch({headless: true, args: ['--no-sandbox']});
    try {
        const page = await login(browser);

        // Capture logs
        page.on('console', msg => console.log('PAGE LOG:', msg.text()));
        page.on('pageerror', err => console.log('PAGE ERROR:', err.toString()));

        // Test Table Page
        console.log('Navigating to /OA/Articles');
        await page.goto('http://localhost:5000/OA/Articles', { waitUntil: 'networkidle0' });
        
        // Check if EleTableAppBuilder is defined
        const isDefined = await page.evaluate(() => {
            return typeof EleTableAppBuilder !== 'undefined';
        });
        console.log('EleTableAppBuilder defined:', isDefined);

        // Check if EleTableAppBuilder is working
        const isAppMounted = await page.evaluate(() => {
            return !!document.querySelector('#app')._vnode; // Vue 3 app mounted
        });
        console.log('Vue App Mounted on Table:', isAppMounted);

        // Check for table rows
        try {
            await page.waitForSelector('.el-table__body tr', { timeout: 5000 });
            console.log('Table rows found');
        } catch (e) {
            console.log('No rows found or timeout');
        }
        
    } catch(e) { 
        console.error(e); 
    } finally { 
        await browser.close(); 
    }
})();
