// Regenerate only the current wordmark; preserve the launcher icon and game artwork.
const path = require('path');
const {spawnSync} = require('child_process');
const result = spawnSync('powershell.exe', ['-NoProfile', '-File', path.join(__dirname, 'render-tizenpsx-wordmark.ps1')], {stdio: 'inherit', windowsHide: true});
if (result.error) throw result.error;
process.exit(result.status === null ? 1 : result.status);
