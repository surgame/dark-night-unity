import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';
import {allFiles,mapFiles} from './source-files.mjs';

// 将唯一源码组合成无需网络、无需服务器的离线 HTML，不依赖 Unity 或包安装。
const root = path.dirname(fileURLToPath(import.meta.url));
const read = name => fs.readFileSync(path.join(root, 'src', name), 'utf8');
const scripts = allFiles(root);
const javascript = scripts.map(read).join('\n');
new vm.Script(javascript, { filename: 'dark-nights-offline.js' });
const sandbox={window:{}};vm.createContext(sandbox);
vm.runInContext(mapFiles(root).map(read).join('\n'),sandbox);
const D=sandbox.window.DN;
for(const config of D.layouts){
  const m=D.createMap(config.id);
  if(m.id!==config.id||m.tiles.length!==D.cols*D.rows||!m.rooms.length)throw Error('Invalid map contract: '+config.id);
  if(D.blockingRects(m,8.5*D.T-8,16*D.T-30,16,30).length||!D.tileSolid(m,8.5*D.T,16*D.T+1))throw Error('Invalid camp spawn: '+config.id);
}
const html = read('shell.html')
  .replace('/* __STYLE__ */', read('style.css'))
  .replace('/* __SCRIPTS__ */', javascript);
const ids = [...html.matchAll(/\bid="([^"]+)"/g)].map(match => match[1]);
if (ids.length !== new Set(ids).size) throw new Error('Duplicate HTML element IDs');
for (const match of read('app.js').matchAll(/\$\('([^']+)'\)/g)) {
  if (!ids.includes(match[1])) throw new Error(`Missing UI element: ${match[1]}`);
}
fs.mkdirSync(path.join(root, 'dist'), { recursive: true });
fs.writeFileSync(path.join(root, 'dist', 'index.html'), html);
const development=read('shell.html').replace('<style>/* __STYLE__ */</style>','<link rel="stylesheet" href="./src/style.css">')
  .replace('<script>/* __SCRIPTS__ */</script>',scripts.map(file=>'<script src="./src/'+file+'"></script>').join('\n'));
fs.writeFileSync(path.join(root,'dev.html'),development);
console.log(JSON.stringify({ file: path.join(root, 'dist', 'index.html'),development:path.join(root,'dev.html'),bytes: Buffer.byteLength(html),maps:D.layouts.map(c=>({id:c.id,title:c.title})),scripts }));
