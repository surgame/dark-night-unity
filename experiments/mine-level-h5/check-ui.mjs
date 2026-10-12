import fs from 'node:fs';
import vm from 'node:vm';
import path from 'node:path';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import {allFiles} from './source-files.mjs';

// 本地 DOM／Canvas 调用替身，只验证接线和绘制调用，不冒充浏览器画面检查。
const root=path.dirname(fileURLToPath(import.meta.url)),html=fs.readFileSync(path.join(root,'src/shell.html'),'utf8');
let drawingCalls=0;
const gradient={addColorStop(){}};
const context=new Proxy({}, {get(target,key){
  if(key in target)return target[key];
  if(key==='createLinearGradient'||key==='createRadialGradient')return()=>gradient;
  return(...args)=>{drawingCalls++;args.forEach(v=>{if(typeof v==='number')assert.ok(Number.isFinite(v),`Invalid canvas ${key} input`);});};
},set(target,key,value){target[key]=value;return true;}});
const all=[],byId=new Map();
class Element{
  constructor(tag='div',id=''){this.tag=tag;this.id=id;this.children=[];this.dataset={};this.listeners={};this.hidden=false;this.checked=false;this.style={};this.attributes={};this.width=960;this.height=600;this.textContent='';this.classList={add(){},remove(){},toggle(){}};all.push(this);if(id)byId.set(id,this);}
  getContext(){return context;}
  getBoundingClientRect(){return{left:0,top:0,width:960,height:600};}
  addEventListener(name,cb){(this.listeners[name]||=[]).push(cb);}
  setAttribute(name,value){this.attributes[name]=value;}
  append(...elements){elements.forEach(e=>{e.parent=this;this.children.push(e);});}
  prepend(e){e.parent=this;this.children.unshift(e);}
  replaceChildren(...elements){this.children=[];this.append(...elements);}
  get lastChild(){return this.children.at(-1);}
  remove(){if(this.parent)this.parent.children.splice(this.parent.children.indexOf(this),1);}
  focus(){}setPointerCapture(){}
  click(){this.fire('click');}
  fire(name,event={}){(this.listeners[name]||[]).forEach(cb=>cb({currentTarget:this,preventDefault(){},...event}));}
}
for(const match of html.matchAll(/<([a-z][a-z0-9]*)\b([^>]*)>/gi)){
  const attr=match[2],id=/\bid="([^"]+)"/.exec(attr)?.[1];
  if(!id&&!attr.includes('data-close')&&!attr.includes('data-hold'))continue;
  const e=new Element(match[1],id);e.hidden=/\bhidden\b/.test(attr);
  for(const d of attr.matchAll(/data-(\w+)="([^"]+)"/g))e.dataset[d[1]]=d[2];
}
const body=new Element('body');
const document={body,hidden:false,getElementById(id){assert.ok(byId.has(id),`Missing element ${id}`);return byId.get(id);},createElement(tag){return new Element(tag);},createTextNode(text){const e=new Element('text');e.textContent=text;return e;},querySelectorAll(q){return all.filter(e=>q==='[data-close]'?e.dataset.close:q==='[data-hold]'?e.dataset.hold:false);},addEventListener(){}};
let frame,appState,exported;
const sandbox={window:{addEventListener(){}},document,performance:{now:()=>0},ResizeObserver:class{constructor(cb){this.cb=cb;}observe(){this.cb();}},requestAnimationFrame:cb=>{frame=cb;},setTimeout(){},URL:{createObjectURL:()=> 'blob:mock-report',revokeObjectURL(){}},Blob:class{constructor(parts){exported=JSON.parse(parts[0]);}}};
vm.createContext(sandbox);
for(const file of allFiles(root)){
  if(file==='app.js'){const original=sandbox.window.DN.newGame;sandbox.window.DN.newGame=id=>(appState=original(id));}
  vm.runInContext(fs.readFileSync(path.join(root,'src',file),'utf8'),sandbox);
}
const D=sandbox.window.DN,$=id=>byId.get(id);
assert.ok($('currentLayout').textContent.includes('折返洞网'));
assert.ok($('layoutCountLabel').textContent.startsWith('10 '));assert.ok($('introMapTitle').textContent.includes('08 · 折返洞网'));
frame(16);
$('introLayoutButton').click();assert.equal($('layoutScreen').hidden,false);assert.equal($('layoutCards').children.length,10);
$('layoutCards').children[0].click();$('layoutStartButton').click();assert.ok($('currentLayout').textContent.includes('原版'));
$('pauseButton').click();$('helpButton').click();assert.equal($('helpScreen').hidden,false);
all.find(e=>e.dataset.close==='helpScreen').click();assert.equal($('pauseScreen').hidden,false);
all.find(e=>e.dataset.close==='pauseScreen').click();assert.equal($('pauseScreen').hidden,true);
$('layoutButton').click();$('layoutCards').children[2].click();$('layoutStartButton').click();assert.ok($('currentLayout').textContent.includes('折返迷宫'));frame(32);
$('mapButton').click();$('hiddenMapToggle').checked=true;$('hiddenMapToggle').fire('change');assert.equal($('mapScreen').hidden,false);
all.find(e=>e.dataset.close==='mapScreen').click();
$('layoutButton').click();$('layoutCards').children[4].click();$('layoutStartButton').click();$('tool4').click();frame(160);assert.ok($('currentLayout').textContent.includes('侵蚀长廊'));assert.equal($('ladderCount').textContent,4);
assert.equal(appState.tool,4);assert.equal(appState.flashlight,true);assert.equal($('ladderSupplyButton').disabled,true);
appState.cargo.iron=9;D.openCamp(appState);assert.equal(appState.bank,9);$('ladderSupplyButton').click();assert.equal(appState.bank,6);assert.equal(appState.ladderKits,5);assert.equal($('ladderCount').textContent,5);
appState.ladderKits=8;D.openCamp(appState);assert.equal($('ladderSupplyButton').disabled,true);$('ladderSupplyButton').click();assert.equal(appState.bank,6);assert.equal(appState.ladderKits,8);
$('exportButton').click();assert.equal(exported.ladderKits,8);assert.equal(exported.playerLadders.length,0);assert.equal(exported.layout,'erosion');
let conceptMaps=0;
for(const id of ['folded','shaft-loop','vein-maze']){
  $('layoutButton').click();const index=D.layouts.findIndex(c=>c.id===id);$('layoutCards').children[index].click();$('layoutStartButton').click();
  assert.equal(appState.map.id,id);assert.equal(appState.map.ladders.length,0);assert.equal(appState.flashlight,true);
  frame(320+conceptMaps*160);const q=appState.map.clues[0];
  Object.assign(appState.player,{x:q.x,y:28*D.T,vx:0,vy:0,grounded:true});appState.camera={x:0,y:0};appState.mouse={active:true,x:q.x,y:q.y,down:false};
  assert.equal(D.nearClue(appState).id,q.id);D.interact(appState);assert.equal(appState.metrics.cluesSeen,1);assert.ok($('toast').textContent.includes(q.note));
  $('mapButton').click();all.find(e=>e.dataset.close==='mapScreen').click();$('exportButton').click();
  assert.equal(exported.layout,id);assert.equal(exported.metrics.cluesSeen,1);assert.ok(exported.events.some(e=>e.kind==='clue-discovered'));
  conceptMaps++;
}
const natural=D.newGame('rift');natural.started=true;natural.panel='';natural.tool=4;D.update(natural,1/60);D.bakeMap(natural.map);D.render(natural,1/60);
const s=D.newGame('maze');D.bakeMap(s.map);const background=s.map.background;D.excavate(s,s.map.walls.values().next().value);D.render(s,1/60);assert.equal(s.map.background,background,'terrain repaint reuses the same canvas');
for(let i=0;i<600;i++)D.record(s,'bounded-log','记录容量核对');assert.equal(s.events.length,512);
console.log(JSON.stringify({prototype:D.version,initialization:'passed',layoutSwitch:'passed',nestedPauseAndHelp:'passed',blueprintAndSpoilers:'passed',ladderToolAndLamp:'passed',ladderSupplyAndCap:'passed',reportLadderFields:'passed',conceptMapCluesAndExport:'passed',conceptMaps,dynamicTerrainDrawingCalls:'passed',boundedJournal:'passed',drawingCalls,layouts:D.layouts.map(c=>{const m=D.createMap(c.id);return{id:c.id,title:c.title,stats:m.stats};}),scope:'Mock DOM/Canvas wiring; no browser screenshots or real input/visual acceptance.'},null,2));
