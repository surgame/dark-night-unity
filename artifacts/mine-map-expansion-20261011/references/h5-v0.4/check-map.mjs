import fs from 'node:fs';
import vm from 'node:vm';
import path from 'node:path';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import {modelFiles} from './source-files.mjs';

// 使用原型真实运动逻辑检查可达性，不启动浏览器、Unity、Play 或 Player。
const root = path.dirname(fileURLToPath(import.meta.url));
const sandbox = { window: {} };
vm.createContext(sandbox);
for (const file of modelFiles(root)) vm.runInContext(fs.readFileSync(path.join(root,'src',file),'utf8'),sandbox);
const D = sandbox.window.DN, dt = 1/60, checks = [];
function state() { const s = D.newGame(); s.started = true; s.panel = ''; return s; }
function move(s, x, y, label) {
  const target = { x: x * D.T, y: y * D.T };
  for (let i=0;i<18000;i++) {
    if (Math.abs(s.player.x-target.x)<14&&Math.abs(s.player.y-target.y)<10) { s.keys={}; checks.push({ check: label, seconds:+s.time.toFixed(2), x:+(s.player.x/D.T).toFixed(2), y:+(s.player.y/D.T).toFixed(2) }); return; }
    const p=s.player,dir=Math.sign(target.x-p.x),ahead=p.x+dir*(p.w/2+10);
    s.keys=Math.abs(target.x-p.x)<7?{}:dir>0?{KeyD:true}:{KeyA:true};
    const upwardPlatform=s.map.platforms.some(r=>ahead>=r.x&&ahead<r.x+r.w&&r.y<p.y-1&&r.y>p.y-86);
    if (p.grounded&&(D.pointSolid(s.map,ahead,p.y-5)||(target.y<p.y-1&&(upwardPlatform||Math.abs(target.x-p.x)<38)))) s.jumpBuffer=.16;
    D.update(s,dt);
  }
  throw Error(`${label}: cannot reach (${x},${y}); stopped at (${(s.player.x/D.T).toFixed(2)},${(s.player.y/D.T).toFixed(2)})`);
}
const s=state();
move(s,40,21,'浅层主路可达');
move(s,70,32,'中层主路可达');
move(s,96,50,'深井主路可达');
move(s,122,67,'深层金矿室可达');
move(s,116.5,67,'无升级返程起点');
move(s,106.5,50,'深层台阶可跳回');
move(s,96,50,'深井可返回');
move(s,70,32,'中层可返回');
move(s,40,21,'浅层可返回');
move(s,8.5,16,'入口可返回');
assert.equal(s.jetpack,false);

const a=state(),o=a.map.ores[0];
move(a,15,16,'第一笔矿无需跳跃可达');
a.mouse={x:o.x-a.camera.x,y:o.y-a.camera.y,down:true,active:true};
for(let i=0;i<240;i++)D.update(a,dt);
assert.equal(a.cargo.iron,3);assert.equal(o.amount,0);
for(let i=0;i<240;i++)D.update(a,dt);
assert.equal(a.cargo.iron,3);checks.push({check:'采空不重复产出',iron:a.cargo.iron});

const b=state();
move(b,70,32,'隐藏支线起点');
b.keys={KeyS:true};for(let i=0;i<35;i++)D.update(b,dt);
move(b,66.5,40,'晶脉裂岩右侧可达');
const g=b.map.gates.find(g=>g.id==='secret');
assert.ok(D.gateDistance(b.player,g)<80);
assert.ok(!D.lineClear(b.map,{x:g.x+40,y:g.y+g.h-36},{x:g.x-20,y:g.y+g.h-36}));
b.tool=3;b.mouse={x:g.x+g.w-b.camera.x,y:b.player.y-20-b.camera.y,down:true,active:true};
for(let i=0;i<100;i++)D.update(b,dt);
assert.ok(g.open);assert.equal(b.charges,1);b.mouse.down=false;
move(b,53,47,'线索与隐藏矿室真实相连');
move(b,70,32,'隐藏矿室可原路返回');
assert.ok(D.lineClear(b.map,{x:g.x+40,y:g.y+g.h-36},{x:g.x-20,y:g.y+g.h-36}));
checks.push({check:'裂岩遮挡随爆破更新',remainingCharges:b.charges});

const c=state();
move(c,122,67,'回程环下层入口可达');
move(c,123,67,'深层裂岩井口可达');
const shortcut=c.map.gates.find(g=>g.id==='shortcut');
c.tool=3;c.mouse={x:c.player.x-c.camera.x,y:shortcut.y-c.camera.y,down:true,active:true};
for(let i=0;i<100;i++)D.update(c,dt);
assert.ok(shortcut.open);assert.ok(c.map.gates.find(g=>g.id==='hatch').open);c.mouse.down=false;
D.interact(c);assert.ok(c.lift);
for(let i=0;i<600&&c.lift;i++)D.update(c,dt);
assert.equal(c.lift,null);assert.ok(Math.abs(c.player.y-16*D.T)<1);assert.equal(c.metrics.liftUses,1);
move(c,8.5,16,'矿车回上层后可达入口');
checks.push({check:'回程环只消耗一份炸药',remainingCharges:c.charges});

console.log(JSON.stringify({prototype:D.version,passed:checks.length,checks,scope:'Headless H5 movement/transactions/occlusion only; no browser visual QA or Unity validation.'},null,2));
