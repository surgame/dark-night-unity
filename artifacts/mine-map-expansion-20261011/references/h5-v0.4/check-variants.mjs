import fs from 'node:fs';
import vm from 'node:vm';
import path from 'node:path';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import {modelFiles} from './source-files.mjs';

const root=path.dirname(fileURLToPath(import.meta.url)),sandbox={window:{}};
vm.createContext(sandbox);
for(const file of modelFiles(root))vm.runInContext(fs.readFileSync(path.join(root,'src',file),'utf8'),sandbox);
const D=sandbox.window.DN,dt=1/60,results=[];
function state(id){const s=D.newGame(id);s.started=true;s.panel='';return s;}
function settle(s,frames=20){s.keys={};s.mouse.down=false;for(let i=0;i<frames;i++)D.update(s,dt);}
function roomPoint(r){return{x:(r.x+8.5)*D.T,y:(r.y+r.h)*D.T};}
function horizontal(s,target){
  for(let i=0;i<1800;i++){
    if(Math.abs(s.player.x-target.x)<7&&Math.abs(s.player.y-target.y)<4){settle(s,10);return;}
    s.keys=Math.abs(s.player.x-target.x)<7?{}:s.player.x<target.x?{KeyD:true}:{KeyA:true};D.update(s,dt);
  }
  throw Error(`Horizontal path blocked at ${s.player.x/D.T},${s.player.y/D.T}`);
}
function edge(s,a,b){
  const target=roomPoint(b),start=roomPoint(a);
  if(start.y===target.y){horizontal(s,target);return;}
  const l=s.map.ladders.find(l=>Math.abs(l.x-start.x)<1&&Math.min(start.y,target.y)===l.top&&Math.max(start.y,target.y)===l.bottom);
  assert.ok(l,'edge must have a ladder');horizontal(s,{x:l.x,y:start.y});
  for(let i=0;i<900&&Math.abs(s.player.y-target.y)>1;i++){s.keys=s.player.y<target.y?{KeyS:true}:{KeyW:true};D.update(s,dt);}
  assert.ok(Math.abs(s.player.y-target.y)<1,`Ladder blocked ${a.node} -> ${b.node}`);settle(s,10);horizontal(s,target);
}
function aim(s,x,y){s.mouse={x:x-s.camera.x,y:y-s.camera.y,down:true,active:true};}
const selected=process.argv.slice(2);
for(const config of D.layouts.filter(c=>c.kind==='network'&&(!selected.length||selected.includes(c.id)))){
  const s=state(config.id),m=s.map;
  assert.ok(m.stats.buried>m.stats.surface*2,'majority of ore is covered');
  assert.ok(m.stats.deadEnds>=2&&m.stats.junctions>=3&&m.stats.loops>=1,'layout must include decisions and dead ends');
  assert.ok(m.ores.filter(o=>o.buried).every(o=>!D.oreExposed(m,o)),'buried ore must initially be invisible');
  assert.ok(m.nests.every(n=>m.tiles[n.coverIndex]===2&&!n.opened),'nests must be covered');
  const seen=new Set([0]),queue=[0];while(queue.length){const n=queue.shift();m.edges.forEach(([a,b])=>{const next=a===n?b:b===n?a:null;if(next!==null&&!seen.has(next)){seen.add(next);queue.push(next);}});}assert.equal(seen.size,m.rooms.length);
  let legs=0;
  for(const [na,nb] of m.edges)for(const [a,b] of [[na,nb],[nb,na]]){
    const leg=state(config.id),ra=leg.map.roomByNode.get(a),rb=leg.map.roomByNode.get(b);Object.assign(leg.player,roomPoint(ra),{vx:0,vy:0,grounded:true});edge(leg,ra,rb);legs++;
  }
  // 用真实按住矿镐输入逐层挖开同一矿位：矿没露出前不能直接取得收益。
  const mine=state(config.id),buried=mine.map.ores.find(o=>o.buried),tx=Math.floor(buried.x/D.T),ty=Math.floor(buried.y/D.T);
  Object.assign(mine.player,{x:buried.x,y:(ty-1)*D.T,vx:0,vy:0,grounded:true});
  aim(mine,buried.x,buried.y);
  assert.equal(D.findTarget(mine)?.type,'wall');
  for(let i=0;i<42;i++){aim(mine,buried.x,buried.y);D.update(mine,dt);}
  assert.equal(mine.cargo.iron+mine.cargo.gold,0,'excavation does not award ore');
  for(let i=0;i<240&&!D.oreExposed(mine.map,buried);i++){aim(mine,buried.x,buried.y);D.update(mine,dt);}
  assert.ok(D.oreExposed(mine.map,buried));assert.ok(mine.metrics.hiddenRevealed>0);
  for(let i=0;i<80;i++){aim(mine,buried.x,buried.y);D.update(mine,dt);}
  assert.ok(mine.cargo.iron+mine.cargo.gold>0,'exposed ore is harvestable');
  mine.mouse.down=false;
  for(let i=0;i<240;i++){mine.keys={KeyD:true};if(mine.player.grounded)mine.jumpBuffer=.16;D.update(mine,dt);if(mine.player.y<=(ty-1)*D.T+.1)break;}
  assert.ok(mine.player.y<=(ty-1)*D.T+1,'excavated pocket is escapable');
  const nest=state(config.id),n=nest.map.nests[0],wall=nest.map.walls.get(n.coverIndex);
  assert.equal(nest.map.worms.length,0);D.excavate(nest,wall);assert.equal(nest.map.worms.length,1);assert.equal(nest.map.worms[0].mode,'warning');assert.ok(nest.map.worms[0].timer>=1.5);
  D.excavate(nest,wall);assert.equal(nest.map.worms.length,1,'nests do not respawn');assert.equal(nest.metrics.nestsOpened,1);
  results.push({layout:config.id,title:config.title,...m.stats,physicalRouteLegs:legs,coveredOreAndExcavation:'passed',pitExit:'passed',nestWarningAndNoRespawn:'passed'});
}
console.log(JSON.stringify({prototype:D.version,results,scope:'H5 headless logic only; no browser rendering or Unity acceptance.'},null,2));
