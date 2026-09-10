const { exec, spawn } = require('child_process');
const path = require('path');
const fs = require('fs');

const PORT = 5000;
// Project root path (assuming WebTest is in AppPlat, and AppPlat is inside AppPlat8)
// Current dir: /.../AppPlat8/AppPlat/WebTest/tests
// Project root should be: /.../AppPlat8/
const PROJECT_ROOT = path.resolve(__dirname, '../../..'); 
console.log('Working directory:', PROJECT_ROOT);
process.chdir(PROJECT_ROOT);

function killPort(port) {
    return new Promise((resolve, reject) => {
        const cmd = process.platform === 'win32' 
            ? `netstat -ano | findstr :${port}` 
            : `lsof -i :${port} -t`;
            
        exec(cmd, (err, stdout, stderr) => {
            if (err || !stdout) {
                // Port likely not in use
                resolve();
                return;
            }
            
            const pids = stdout.split('\n').filter(p => p.trim().length > 0);
            if (pids.length === 0) {
                resolve();
                return;
            }
            
            console.log(`Killing processes on port ${port}: ${pids.join(', ')}`);
            const killCmd = process.platform === 'win32'
                ? `taskkill /F /PID ${pids.join(' /PID ')}`
                : `kill -9 ${pids.join(' ')}`;
                
            exec(killCmd, (err) => {
                if (err) console.error('Error killing process:', err);
                // Give it a moment to release
                setTimeout(resolve, 1000);
            });
        });
    });
}

async function reboot() {
    console.log(`Working directory: ${PROJECT_ROOT}`);
    console.log('Stopping port 5000...');
    await killPort(PORT);
    
    console.log('Compiling...');
    
    // We need to point to the specific project file since SLN is broken
    const projectFile = 'AppPlat/App.csproj';
    
    const build = spawn('dotnet', ['build', projectFile], { stdio: 'inherit', cwd: PROJECT_ROOT });
    
    build.on('close', (code) => {
        if (code !== 0) {
            console.error('Build failed');
            return;
        }
        
        console.log('Build successful. Starting system...');
        
        // Start the app
        const run = spawn('dotnet', ['run', '--project', projectFile, '--urls', `http://localhost:${PORT}`], { 
            stdio: 'inherit', 
            cwd: PROJECT_ROOT 
        });
        
        run.on('error', (err) => {
            console.error('Failed to start:', err);
        });
    });
}

reboot();
