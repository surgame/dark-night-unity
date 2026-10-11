import fs from 'node:fs';
import vm from 'node:vm';
import path from 'node:path';
import assert from 'node:assert/strict';
import {fileURLToPath} from 'node:url';
import {modelFiles,mapFiles} from './source-files.mjs';
const root=path.dirname(fileURLToPath(import.meta.url)),out=path.resolve(root,'../../artifacts/mine-concept-pack-20261011');
fs.mkdirSync(out,{recursive:true});
const sandbox={window:{}};vm.createContext(sandbox);
for(const file of modelFiles(root))vm.runInContext(fs.readFileSync(path.join(root,'src',file),'utf8'),sandbox);
const D=sandbox.window.DN,dt=1/60,checks=[],configs=D.layouts.filter(c=>c.kind==='cave'&&c.testPlan);
function state(id){const s=D.newGame(id);s.started=true;s.panel='';s.camera={x:0,y:0};return s;}
function tick(s,n=1){for(let i=0;i<n;i++)D.update(s,dt);}
function stand(s,x,floor){
  Object.assign(s.player,{x:x*D.T,y:floor*D.T,vx:0,vy:0,grounded:true});s.keys={};s.mouse.down=false;tick(s,4);
  assert.ok(s.player.grounded&&D.blockingRects(s.map,s.player.x-8,s.player.y-30,16,30).length===0,s.map.id+' fixture '+x+','+floor);
}
function floorAt(s,x,wanted){const f=D.caveFloorAt(s.map,Math.floor(x),wanted);assert.notEqual(f,null,'missing floor at '+x+','+wanted);return f;}
function walk(s,x,floor,label,exact=true){
  const goal={x:x*D.T,y:floor*D.T};
  for(let i=0;i<2400;i++){
    const p=s.player,dir=Math.sign(goal.x-p.x),ahead=p.x+dir*18;
    if(Math.abs(p.x-goal.x)<6&&(!exact||Math.abs(p.y-goal.y)<3)&&p.grounded){s.keys={};tick(s,8);return;}
    s.keys=Math.abs(goal.x-p.x)<2?{}:dir>0?{KeyD:true}:{KeyA:true};
    if(p.grounded&&(D.pointSolid(s.map,ahead,p.y-5)||(!D.pointSolid(s.map,ahead,p.y+5)&&goal.y<=p.y+30)||(exact&&goal.y<p.y-1&&Math.abs(goal.x-p.x)<40)))s.jumpBuffer=.16;
    tick(s);
  }
  throw Error(s.map.id+' '+label+' stuck '+(s.player.x/D.T).toFixed(2)+','+(s.player.y/D.T).toFixed(2)+' -> '+x+','+floor);
}
function route(s,id,reverse=false){
  const r=s.map.walkRoutes.find(p=>p.id===id);assert.ok(r,id);
  const samples=reverse?[...r.samples].reverse():r.samples;
  for(const p of samples){
    // 三格宽的开放井口须从岸边跳过；不能把井底当成同层的步行目标。
    if(openHole(s,p))continue;
    walk(s,p.x+.5,floorAt(s,p.x,p.floor),id+(reverse?' reverse':''));
  }
}
function openHole(s,p){return s.map.ladderSites.some(q=>Math.abs(q.top/D.T-p.floor)<2&&Math.abs(q.x/D.T-(p.x+.5))<=1.1&&!D.pointSolid(s.map,(p.x+.5)*D.T,p.floor*D.T+.5));}
function open(s,seal){seal.coverIndices.forEach(index=>D.excavate(s,s.map.walls.get(index)));}
function aim(s,x,y,hold=true){s.mouse={active:true,x,y,down:hold};}
function traverse(s,id,up=false){
  const site=s.map.ladderSites.find(q=>q.id===id);assert.ok(site,id);
  route(s,id+(up?'-lower':'-upper'),!up);
  if(up&&!s.map.ladders.some(q=>q.x===site.x)){
    s.tool=4;aim(s,site.x,site.top);const plan=D.planLadder(s);assert.ok(plan.valid,id+' up ladder: '+plan.reason);
    assert.equal(plan.top,site.top);assert.equal(plan.bottom,site.bottom);tick(s,60);s.mouse.down=false;
  }
  if(!up){
    s.tool=4;aim(s,site.x,site.bottom);const plan=D.planLadder(s);
    assert.ok(plan.valid,id+' down ladder: '+plan.reason);assert.equal(plan.top,site.top);assert.equal(plan.bottom,site.bottom);
    const before=s.ladderKits;tick(s,60);s.mouse.down=false;assert.equal(s.ladderKits,before-1);
    for(let i=0;i<90&&Math.abs(s.player.x-site.x)>8;i++){s.keys=site.x>s.player.x?{KeyD:true}:{KeyA:true};tick(s);}
  }
  s.keys=up?{KeyW:true}:{KeyS:true};tick(s,Math.ceil((site.bottom-site.top)/145/dt)+12);s.keys={};
  assert.ok(Math.abs(s.player.y-(up?site.top:site.bottom))<4,id+' traverse');
  if(up){s.jumpBuffer=.16;walk(s,site.exitX/D.T,site.exitFloor,id+' exit');route(s,id+'-upper');}
  else{tick(s,6);route(s,id+'-lower',true);}
}
function buildAndClimb(s,site){
  s.tool=4;aim(s,site.x,site.top);assert.ok(D.planLadder(s).valid,s.map.id+' '+site.id+' '+D.planLadder(s).reason);
  const before=s.ladderKits;tick(s,60);s.mouse.down=false;assert.equal(s.ladderKits,before-1);assert.equal(s.metrics.laddersBuilt,1);
  s.keys={KeyW:true};tick(s,Math.ceil((site.bottom-site.top)/145/dt)+3);s.keys={};
  assert.ok(Math.abs(s.player.y-site.top)<4,s.map.id+' '+site.id+' climb blocked');
  D.recoverLadder(s);assert.equal(s.ladderKits,before-1,'no midair removal');
  s.jumpBuffer=.16;walk(s,site.exitX/D.T,site.exitFloor,site.id+' exit');
  assert.ok(s.player.grounded);D.recoverLadder(s);assert.equal(s.ladderKits,before);assert.equal(s.metrics.laddersRecovered,1);
}
function steps(s,plan){for(const part of plan){
  if(typeof part==='string')route(s,part);
  else if(part.shaft)traverse(s,part.shaft,!!part.up);
  else{if(part.excavate)open(s,s.map.seals.find(q=>q.path===part.path));route(s,part.path,!!part.reverse);}
}}
try{
  const geometry=configs.map(c=>{const m=D.createMap(c.id);return{id:m.id,title:m.title,cols:D.cols,rows:D.rows,tiles:[...m.tiles],rooms:m.rooms,ladderSites:m.ladderSites,clues:m.clues,seals:m.seals,stats:m.stats};});
  fs.writeFileSync(path.join(out,'geometry.json'),JSON.stringify(geometry));
  for(const c of configs){
    const s=state(c.id),m=s.map;
    assert.equal(m.ladders.length,0);assert.ok(m.stats.junctions>=3);assert.ok(m.stats.buried>m.stats.surface);
    assert.ok(m.ores.filter(o=>o.buried).every(o=>m.tiles[o.coverIndex]===2));assert.ok(m.nests.every(n=>m.tiles[n.coverIndex]===2));
    const reset=state(c.id);const first=m.walls.values().next().value;D.excavate(s,first);assert.equal(reset.map.tiles[first.index],2);
    checks.push({map:c.id,check:'零预搭梯／中段岔口／埋矿和巢穴覆盖／重开独立状态',stats:m.stats});
    const main=state(c.id);steps(main,c.testPlan.primary);
    const used=c.testPlan.primary.filter(p=>p.shaft).length;
    assert.equal(main.metrics.laddersBuilt,used);assert.ok(used<=4);assert.equal(main.jetpack,false);assert.equal(main.metrics.rescues,0);
    steps(main,[...c.testPlan.primary].reverse().map(part=>typeof part==='string'?{path:part,reverse:true}:part.shaft?{...part,up:!part.up}:{...part,reverse:!part.reverse,excavate:false}));
    walk(main,8.5,16,'return camp');assert.equal(main.metrics.rescues,0);
    checks.push({map:c.id,check:'入口到深部并原路返回，使用开局梯具／无升级',ladders:used});
    const loop=state(c.id);steps(loop,c.testPlan.primary);steps(loop,c.testPlan.alternateReturn);
    walk(loop,8.5,16,'alternate return');assert.equal(loop.metrics.rescues,0);assert.ok(loop.metrics.laddersBuilt<=4);
    checks.push({map:c.id,check:'深部通过不同支线返回入口，仍使用初始梯具',ladders:loop.metrics.laddersBuilt});
    let legs=0;
    for(const r of m.walkRoutes)for(const reverse of [false,true]){
      const a=state(c.id),seal=a.map.seals.find(q=>q.path===r.id);if(seal)open(a,seal);
      const p=(reverse?[...r.samples].reverse():r.samples).find(q=>!openHole(a,q));stand(a,p.x+.5,floorAt(a,p.x,p.floor));route(a,r.id,reverse);legs++;
    }
    checks.push({map:c.id,check:'全部步行段双向实际运动',legs});
    for(const site of m.ladderSites){
      const a=state(c.id),local=a.map.ladderSites.find(q=>q.id===site.id),seal=a.map.seals.find(q=>q.shaft===site.id);
      if(seal)open(a,seal);stand(a,site.x/D.T,site.bottom/D.T);buildAndClimb(a,local);
      checks.push({map:c.id,check:site.id+' 自建攀爬／跳离／回收',length:(site.bottom-site.top)/D.T});
    }
    for(const q of m.clues){
      const a=state(c.id);let found=false;
      for(let dx=-3;dx<=3&&!found;dx++)for(let f=Math.round(q.y/D.T)+1;f<=Math.round(q.y/D.T)+5&&!found;f++){
        const x=Math.floor(q.x/D.T)+dx,foot=D.caveFloorAt(a.map,x,f);
        if(foot===null||Math.abs(foot-f)>2)continue;
        Object.assign(a.player,{x:(x+.5)*D.T,y:foot*D.T,vx:0,vy:0,grounded:true});
        if(D.blockingRects(a.map,a.player.x-8,a.player.y-30,16,30).length)continue;
        aim(a,q.x,q.y,false);
        if(D.nearClue(a)?.id===q.id){D.inspectClue(a);D.inspectClue(a);assert.equal(a.metrics.cluesSeen,1);found=true;}
      }
      assert.ok(found,c.id+' unreadable clue '+q.name);
    }
    checks.push({map:c.id,check:'各线索有可观察落脚点／观察不重复计数',clues:m.clues.length});
    for(const seal of m.seals){
      const a=state(c.id),local=a.map.seals.find(q=>q.id===seal.id);assert.ok(local.coverIndices.every(i=>a.map.tiles[i]===2));
      open(a,local);assert.ok(local.coverIndices.every(i=>a.map.tiles[i]===0));assert.equal(a.cargo.iron+a.cargo.gold,0);
    }
    const n=state(c.id),nest=n.map.nests[0];D.excavate(n,n.map.walls.get(nest.coverIndex));assert.equal(n.map.worms.length,1);assert.equal(n.map.worms[0].mode,'warning');D.excavate(n,n.map.walls.get(nest.coverIndex));assert.equal(n.map.worms.length,1);
    checks.push({map:c.id,check:'封岩真实可拆／拆岩不发矿／巢穴前兆与一次生成'});
  }
  // 新增定义使用现有生成器注册即可；不修改入口／UI，也不会覆盖已有 ID。
  D.registerLayout({...D.layouts.find(c=>c.id==='folded'),id:'extension-probe',number:'11',title:'扩展核对'});
  assert.equal(D.createMap('extension-probe').id,'extension-probe');
  assert.throws(()=>D.registerLayout({...D.layouts[0]}),/重复地图/);
  assert.throws(()=>D.registerLayout({id:'bad-map',title:'缺少生成器',kind:'missing'}),/无效地图/);
  checks.push({check:'地图扩展注册／重复和未知生成器拒绝',sources:mapFiles(root).length});
  const report={prototype:D.version,passed:checks.length,checks,scope:'Actual H5 movement, ladder/excavation/clue transactions; no real browser or Unity acceptance.'};
  fs.writeFileSync(path.join(out,'concept-check.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
}catch(error){
  const report={prototype:D.version,passed:checks.length,checks,error:error.stack};
  const failure='failure-'+Date.now()+'.json';fs.writeFileSync(path.join(out,failure),JSON.stringify(report,null,2));
  console.error(JSON.stringify({failure,...report},null,2));process.exitCode=1;
}
