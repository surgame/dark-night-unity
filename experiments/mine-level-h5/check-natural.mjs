import fs from 'node:fs';
import vm from 'node:vm';
import path from 'node:path';
import assert from 'node:assert/strict';
import {fileURLToPath} from 'node:url';
import {modelFiles} from './source-files.mjs';

// 直接运行原型运动／梯具逻辑；图像为地图数据诊断，不是浏览器截图。
const root=path.dirname(fileURLToPath(import.meta.url)),out=path.resolve(root,'../../artifacts/mine-concept-pack-20261011/natural-compatibility');
fs.mkdirSync(out,{recursive:true});
const sandbox={window:{}};vm.createContext(sandbox);
for(const file of modelFiles(root))vm.runInContext(fs.readFileSync(path.join(root,'src',file),'utf8'),sandbox);
const D=sandbox.window.DN,dt=1/60,checks=[],maps=D.layouts.filter(c=>c.natural).map(c=>D.createMap(c.id));
fs.writeFileSync(path.join(out,'geometry.json'),JSON.stringify(maps.map(m=>({id:m.id,title:m.title,cols:D.cols,rows:D.rows,tiles:[...m.tiles],floorProfile:m.floorProfile,ladderSites:m.ladderSites,stats:m.stats}))));
function state(id){const s=D.newGame(id);s.started=true;s.panel='';s.camera={x:0,y:0};return s;}
function tick(s,count=1){for(let i=0;i<count;i++)D.update(s,dt);}
function stand(s,x,y){Object.assign(s.player,{x,y,vx:0,vy:0,grounded:true});s.keys={};s.mouse.down=false;tick(s,3);assert.ok(s.player.grounded,'fixture stands on actual terrain');}
function aim(s,x,y,down=false){s.mouse={active:true,x,y,down};}
function walk(s,x,y,label){
  for(let i=0;i<18000;i++){
    const p=s.player;
    if(Math.abs(p.x-x)<12&&Math.abs(p.y-y)<8){s.keys={};tick(s,12);checks.push({map:s.map.id,check:label,seconds:+s.time.toFixed(2)});return;}
    const dir=Math.sign(x-p.x),ahead=p.x+dir*(p.w/2+10);
    s.keys=Math.abs(x-p.x)<2?{}:dir>0?{KeyD:true}:{KeyA:true};
    if(p.grounded&&(D.pointSolid(s.map,ahead,p.y-5)||(y<p.y-1&&Math.abs(x-p.x)<38)))s.jumpBuffer=.16;
    tick(s);
  }
  throw Error(`${s.map.id} ${label}: stuck at (${s.player.x/D.T}, ${s.player.y/D.T}), target (${x/D.T}, ${y/D.T})`);
}
try{
  for(const m of maps){
    assert.equal(m.ladders.length,0);assert.equal(m.platforms.length,0);
    assert.ok(new Set(m.floorProfile.filter(Number.isFinite)).size>35);assert.ok(m.stats.buried>m.stats.surface);
    m.ores.filter(o=>o.buried).forEach(o=>{assert.equal(m.tiles[o.coverIndex],2);assert.equal(D.oreExposed(m,o),false);});
    m.nests.forEach(n=>assert.equal(m.tiles[n.coverIndex],2));
    checks.push({map:m.id,check:'自然轮廓／零预搭梯／墙后矿覆盖',stats:m.stats});
    const s=state(m.id);
    for(const x of [30,50,70,90,110,123])walk(s,(x+.5)*D.T,s.map.floorProfile[x]*D.T,`徒步下探 ${x}`);
    for(const x of [110,90,70,50,30,8])walk(s,(x+.5)*D.T,s.map.floorProfile[x]*D.T,`无梯无升级返程 ${x}`);
    assert.equal(s.jetpack,false);assert.equal(s.map.ladders.length,0);assert.equal(s.metrics.rescues,0);
    if(m.loopPath){
      const loop=state(m.id),back=m.ladderSites.find(v=>v.id==='loop-return');walk(loop,back.x,back.bottom,'沿主洞到回环后口');
      loop.tool=4;aim(loop,back.x,back.top,true);tick(loop,60);loop.mouse.down=false;assert.equal(loop.map.ladders.length,1);
      loop.keys={KeyW:true};tick(loop,Math.ceil((back.bottom-back.top)/145/dt)+2);loop.jumpBuffer=.16;loop.keys={};
      walk(loop,70.5*D.T,back.top,'后口搭梯跳上回环');
      for(const x of [69,58,48,38])walk(loop,(x+.5)*D.T,m.loopPath.find(v=>v.x===x).floor*D.T,`上层曲道返回 ${x}`);
      walk(loop,30.5*D.T,m.floorProfile[30]*D.T,'上层回环出口落回主洞');walk(loop,8.5*D.T,16*D.T,'绕行上层后回入口');
      assert.equal(loop.metrics.laddersBuilt,1);
    }
    for(const site of m.ladderSites){
      const a=state(m.id);stand(a,site.x,site.bottom);a.tool=4;aim(a,site.x,site.top,true);
      const plan=D.planLadder(a);assert.ok(plan.valid,`${m.id} ${site.id}: ${plan.reason}`);assert.ok(plan.top<=site.top);
      tick(a,60);a.mouse.down=false;assert.equal(a.map.ladders.length,1);assert.equal(a.ladderKits,3);assert.equal(a.metrics.laddersBuilt,1);
      a.keys={KeyW:true};tick(a,Math.ceil((site.bottom-site.top)/145/dt)+3);a.keys={};
      assert.ok(Math.abs(a.player.y-site.top)<4,`${m.id} ${site.id}: climb reaches upper ledge`);
      D.recoverLadder(a);assert.equal(a.map.ladders.length,1,'cannot dismantle while hanging');
      const tx=Math.floor(site.x/D.T)+(site.left?-1:2),ledge=m.id==='karst'&&site.id==='loop-shaft'?m.loopPath.find(v=>v.x===tx).floor:site.top/D.T;
      a.jumpBuffer=.16;walk(a,(tx+.5)*D.T,ledge*D.T,`${site.id} 跳离梯子登上岩台`);
      assert.ok(a.player.grounded);D.recoverLadder(a);assert.equal(a.map.ladders.length,0);assert.equal(a.ladderKits,4);
      aim(a,site.x,site.bottom,true);const down=D.planLadder(a);assert.ok(down.valid,`${m.id} ${site.id} downward: ${down.reason}`);assert.ok(down.down);
      tick(a,60);a.mouse.down=false;assert.equal(a.ladderKits,3);assert.equal(a.map.ladders.length,1);
      // 先进入梯井，借助实际 S 攀爬到下方，不从高台瞬移。
      a.keys=site.left?{KeyD:true}:{KeyA:true};for(let i=0;i<120&&Math.abs(a.player.x-site.x)>8;i++)tick(a);a.keys={KeyS:true};tick(a,Math.ceil((down.bottom-down.top)/145/dt)+25);a.keys={};tick(a,5);
      assert.ok(Math.abs(a.player.y-site.bottom)<4);assert.ok(a.player.grounded);
      D.recoverLadder(a);assert.equal(a.ladderKits,4);assert.equal(a.metrics.laddersBuilt,2);assert.equal(a.metrics.laddersRecovered,2);
      checks.push({map:m.id,check:`${site.id} 双向搭建／攀爬／站稳回收`,length:(site.bottom-site.top)/D.T});
    }
    const bad=state(m.id),site=m.ladderSites[0];stand(bad,site.x,site.bottom);bad.tool=4;aim(bad,site.x,site.top,true);
    const plan=D.planLadder(bad);assert.ok(D.placeLadder(bad,plan));const kits=bad.ladderKits;
    assert.equal(D.planLadder(bad).valid,false);tick(bad,120);assert.equal(bad.ladderKits,kits,'duplicate hold does not consume kits');
    bad.player.grounded=false;assert.equal(D.planLadder(bad).valid,false);bad.player.grounded=true;
    aim(bad,site.x+120,site.top);assert.equal(D.planLadder(bad).valid,false);bad.ladderKits=0;assert.equal(D.planLadder(bad).valid,false);
    const unsupported=state(m.id),tx=Math.floor(site.x/D.T)+2;stand(unsupported,(tx+.5)*D.T,site.top);unsupported.tool=4;aim(unsupported,site.x,site.top-80,true);assert.equal(D.planLadder(unsupported).valid,false);assert.ok(D.planLadder(unsupported).reason.includes('梯脚'));tick(unsupported,80);assert.equal(unsupported.ladderKits,4);
    const rock=state(m.id);stand(rock,15.5*D.T,16*D.T);rock.tool=4;aim(rock,16.5*D.T,19*D.T,true);tick(rock,80);assert.equal(rock.ladderKits,4);assert.equal(rock.map.ladders.length,0);
    rock.map.ladders=Array.from({length:40},(_,i)=>({id:`cap-${i}`,x:0,top:0,bottom:24}));assert.equal(D.planLadder(rock).valid,false);assert.ok(D.planLadder(rock).reason.includes('40'));
    checks.push({map:m.id,check:'无支撑／远处／重复／悬空／无梯具拒绝，不消耗库存'});
    const e=state(m.id),hidden=e.map.ores.find(o=>o.buried),wall=e.map.walls.get(hidden.coverIndex);
    D.excavate(e,wall);assert.equal(e.cargo.iron+e.cargo.gold,0);assert.ok(D.oreExposed(e.map,hidden));
    assert.equal(e.flashlight,true);const nest=e.map.nests[0];D.excavate(e,e.map.walls.get(nest.coverIndex));assert.equal(e.map.worms.length,1);assert.equal(e.map.worms[0].mode,'warning');assert.equal(e.map.worms[0].timer,1.8);D.excavate(e,e.map.walls.get(nest.coverIndex));assert.equal(e.map.worms.length,1);
    checks.push({map:m.id,check:'挖墙才露矿／拆岩不发矿／巢穴先预警且不重复生成／默认配手电'});
  }
  const report={prototype:D.version,passed:checks.length,checks,scope:'Actual H5 physics and ladder transactions; source geometry illustration. No real browser input/visual/performance or Unity acceptance.'};
  fs.writeFileSync(path.join(out,'natural-check.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
}catch(error){
  const report={prototype:D.version,passed:checks.length,checks,error:error.stack};
  const name=`failure-${Date.now()}.json`;fs.writeFileSync(path.join(out,name),JSON.stringify(report,null,2));console.error(JSON.stringify({failure:name,...report},null,2));process.exitCode=1;
}
