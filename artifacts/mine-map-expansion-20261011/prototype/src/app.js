(function () {
  'use strict';
  const D = window.DN, $ = id => document.getElementById(id), canvas = D.view.canvas;
  let s = D.newGame('folded'); D.bakeMap(s.map);
  let selectedLayout=s.map.id;
  $('layoutCountLabel').textContent=D.layouts.length+' 张对照图 / 岔路与线索';
  $('introLayoutButton').textContent='比较 / 选择 '+D.layouts.length+' 张地图';
  const panels = ['introScreen','helpScreen','pauseScreen','mapScreen','campScreen','layoutScreen'];
  let panelHistory = [], last = performance.now(), accumulator = 0, lastHud = 0;
  let layoutWidth = 0, layoutHeight = 0;
  D.showToast = text => { $('toast').textContent = text; $('toast').classList.add('visible'); };
  D.addJournal = text => {
    const li = document.createElement('li'); li.textContent = text;
    const list = $('journal'); list.prepend(li);
    while (list.children.length > 4) list.lastChild.remove();
  };
  function clearInput() { s.keys = {}; s.mouse.down = false; s.touchMining = false; s.jumpBuffer = 0; }
  function panel(id, remember = false) {
    if (remember) { panelHistory.push(s.panel); if(panelHistory.length>8)panelHistory.shift(); }
    clearInput(); s.panel = id;
    panels.forEach(p => { $(p).hidden = p !== id; });
    if (id === 'mapScreen') { $('blueprintTitle').textContent=`${s.map.title} · 结构全览`; D.drawBlueprint(s); }
    if (!id) canvas.focus({ preventScroll: true });
  }
  function closePanel() { panel(panelHistory.length?panelHistory.pop():s.started?'':'introScreen'); }
  function start() { s.started = true; panelHistory = []; panel(''); D.record(s, 'start', `进入${s.map.title}。第一笔矿在飞船右边；裂纹岩可以挖，手电已经配好。`); }
  function reset(layout=s.map.id) {
    s = D.newGame(layout); selectedLayout=layout; D.bakeMap(s.map); last = performance.now(); accumulator = 0; panelHistory = [];
    $('journal').textContent = ''; D.addJournal('飞船灯照着入口。手电已经配好。');
    $('introMapTitle').textContent=`${D.layouts.find(c=>c.id===layout).number} · ${s.map.title}`;
    $('introMapCopy').textContent=D.layouts.find(c=>c.id===layout).description;
    panel('introScreen'); hud();
  }
  function layoutPicker(){if(s.panel==='layoutScreen'){closePanel();return;}selectedLayout=s.map.id;renderChoices();$('layoutResetNote').textContent=s.started?'开始所选地图会重新开始当前远征；可先回入口导出记录。':'新图中多数矿簇藏在浅色裂纹岩里。深色基岩不允许挖穿。';panel('layoutScreen',true);}
  function renderChoices(){
    $('layoutCards').replaceChildren();
    D.layouts.forEach(config=>{
      const button=document.createElement('button');button.type='button';button.className='layout-card';button.setAttribute('aria-pressed',String(config.id===selectedLayout));
      const thumb=document.createElement('canvas');thumb.width=264;thumb.height=156;thumb.setAttribute('aria-hidden','true');D.drawLayoutThumbnail(thumb,D.createMap(config.id));button.append(thumb);
      const title=document.createElement('b');title.textContent=`${config.number} ${config.title}`;button.append(title);
      const tag=document.createElement('span');tag.textContent=config.tag;button.append(tag);const description=document.createElement('p');description.textContent=config.description;button.append(description);
      button.addEventListener('click',()=>{selectedLayout=config.id;renderChoices();});$('layoutCards').append(button);
    });
  }
  $('layoutButton').addEventListener('click',layoutPicker);$('introLayoutButton').addEventListener('click',layoutPicker);
  $('layoutStartButton').addEventListener('click',()=>{reset(selectedLayout);start();});
  $('hiddenMapToggle').addEventListener('change',()=>D.drawBlueprint(s));
  function selectTool(n) { s.tool = n; s.blast = 0; s.buildProgress=0; hud(); if (!s.panel) canvas.focus({ preventScroll: true }); }
  function toggleMode() { s.fast = !s.fast; if (s.started) D.record(s,'mining-mode',s.fast?'改用快速开采：更快，也更响。':'改用稳妥开采：留意附近震动。'); hud(); }
  function toggleLamp() { s.flashlight = !s.flashlight; hud(); }
  function map() { if (s.panel==='mapScreen') closePanel(); else panel('mapScreen',true); }
  function help() { if (s.panel==='helpScreen') closePanel(); else panel('helpScreen',true); }
  function pause() { if (!s.started && s.panel==='introScreen') return; if(s.panel)closePanel();else panel('pauseScreen',true); }
  function formatTime(value) { const n=Math.floor(value); return `${String(Math.floor(n/60)).padStart(2,'0')}:${String(n%60).padStart(2,'0')}`; }
  D.openCamp = function (state) {
    const iron=state.cargo.iron,gold=state.cargo.gold,value=iron+gold*3;
    state.bank+=value;state.delivered.iron+=iron;state.delivered.gold+=gold;state.cargo={iron:0,gold:0};state.health=5;
    if(value){state.metrics.returns++;D.record(state,'deposit',`带回 ${iron} 铁矿、${gold} 金矿。`,{iron,gold,value});}
    $('campTitle').textContent=value?'收获已带回。':'入口整备';
    $('campCopy').textContent=value?`这趟带回 ${value} 试采点。再深入一点，还是收工？`:'拿好矿镐与手电。你可以随时回到这里交矿，状态会恢复。';
    $('campStats').replaceChildren();
    const stats=[['本局探索',formatTime(state.time)],['已带回',`${state.delivered.iron+state.delivered.gold} 矿`],['已发现洞室',`${state.visited.size}/${state.map.rooms.length}`],['墙后发现 / 巢穴',`${state.metrics.hiddenRevealed} / ${state.metrics.nestsOpened}`]];
    stats.forEach(([label,value])=>{const div=document.createElement('div');div.textContent=label;const b=document.createElement('b');b.textContent=value;div.append(b);$('campStats').append(div);});
    updateUpgrade(); panelHistory=[];panel('campScreen');hud();
  };
  function updateUpgrade(){const b=$('upgradeButton');b.disabled=s.jetpack||s.bank<12;b.textContent=s.jetpack?'喷气辅助已装备':`换装喷气辅助 · 12 试采点（现有 ${s.bank}）`;}
  $('upgradeButton').addEventListener('click',()=>{if(s.jetpack||s.bank<12)return;s.bank-=12;s.jetpack=true;s.player.fuel=1;D.record(s,'upgrade','换上喷气辅助。跳跃后继续按住空格，可以飞越更高落差。');updateUpgrade();hud();});
  $('ladderSupplyButton').addEventListener('click',()=>{if(s.bank<3||s.ladderKits>=8)return;s.bank-=3;s.ladderKits++;D.record(s,'ladder-supply','在入口补充了一套梯具。');updateUpgrade();hud();});
  function report(){
    const data={prototype:D.version,layout:s.map.id,layoutTitle:s.map.title,layoutStats:s.map.stats,exportedAt:new Date().toISOString(),durationSeconds:+s.time.toFixed(2),metrics:s.metrics,delivered:s.delivered,cargo:s.cargo,bank:s.bank,ladderKits:s.ladderKits,playerLadders:s.map.ladders.filter(l=>l.playerPlaced),visited:[...s.visited],jetpack:s.jetpack,events:s.events,note:'仅 H5 原型记录；不代表 Unity 正式数值、网络或验收结论。'};
    const url=URL.createObjectURL(new Blob([JSON.stringify(data,null,2)],{type:'application/json'}));
    const a=document.createElement('a');a.href=url;a.download=`dark-nights-${s.map.id}-${Date.now()}.json`;document.body.append(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(url),1000);
  }
  $('startButton').addEventListener('click',start);$('mapButton').addEventListener('click',map);$('helpButton').addEventListener('click',help);$('pauseButton').addEventListener('click',pause);
  $('restartButton').addEventListener('click',()=>reset());$('campRestartButton').addEventListener('click',()=>reset());$('continueButton').addEventListener('click',()=>{panelHistory=[];panel('');});$('exportButton').addEventListener('click',report);
  $('modeButton').addEventListener('click',toggleMode);
  [1,2,3,4].forEach(n=>$(`tool${n}`).addEventListener('click',()=>selectTool(n)));
  document.querySelectorAll('[data-close]').forEach(b=>b.addEventListener('click',closePanel));
  const codes=new Set(['KeyA','KeyD','KeyW','KeyS','KeyX','ArrowLeft','ArrowRight','ArrowUp','ArrowDown','Space','ShiftLeft','ShiftRight','KeyF','KeyE','KeyR','KeyM','KeyH','KeyL','Escape','Digit1','Digit2','Digit3','Digit4']);
  document.addEventListener('keydown',e=>{
    if(!codes.has(e.code)||e.ctrlKey||e.altKey||e.metaKey)return;
    if(s.panel&&!['KeyM','KeyH','KeyL','Escape'].includes(e.code))return;
    e.preventDefault();
    if(e.repeat)return;
    if(e.code==='KeyM'){map();return;}if(e.code==='KeyH'){help();return;}if(e.code==='KeyL'){layoutPicker();return;}if(e.code==='Escape'){pause();return;}
    if(s.panel)return;
    if(e.code==='KeyF')toggleLamp();else if(e.code==='KeyR')toggleMode();else if(e.code==='KeyE')D.interact(s);else if(e.code==='KeyX')D.recoverLadder(s);else if(e.code.startsWith('Digit'))selectTool(+e.code.slice(-1));
    else{s.keys[e.code]=true;if(e.code==='Space'){s.jumpBuffer=.16;if(s.player.grounded)s.jumpAge=0;}}
  });
  document.addEventListener('keyup',e=>{if(codes.has(e.code)){s.keys[e.code]=false;e.preventDefault();}});
  function pointer(e){const r=canvas.getBoundingClientRect();s.mouse.x=(e.clientX-r.left)*D.view.width/r.width;s.mouse.y=(e.clientY-r.top)*D.view.height/r.height;s.mouse.active=true;}
  canvas.addEventListener('pointermove',pointer);
  canvas.addEventListener('pointerdown',e=>{if(s.panel)return;pointer(e);s.mouse.down=e.button===0;canvas.focus({preventScroll:true});canvas.setPointerCapture(e.pointerId);e.preventDefault();});
  canvas.addEventListener('pointerup',()=>{s.mouse.down=false;s.touchMining=false;});
  canvas.addEventListener('pointercancel',clearInput);
  canvas.addEventListener('contextmenu',e=>e.preventDefault());
  window.addEventListener('blur',()=>{clearInput();if(s.started&&!s.panel)panel('pauseScreen',true);});
  document.addEventListener('visibilitychange',()=>{if(document.hidden){clearInput();if(s.started&&!s.panel)panel('pauseScreen',true);}});
  document.querySelectorAll('[data-hold]').forEach(b=>{
    const code=b.dataset.hold;
    b.addEventListener('pointerdown',e=>{if(s.panel)return;e.preventDefault();b.setPointerCapture(e.pointerId);s.keys[code]=true;if(code==='Space'){s.jumpBuffer=.16;s.jumpAge=0;}});
    const release=()=>{s.keys[code]=false;};b.addEventListener('pointerup',release);b.addEventListener('pointercancel',release);
  });
  $('touchMine').addEventListener('pointerdown',e=>{if(s.panel)return;e.preventDefault();e.currentTarget.setPointerCapture(e.pointerId);s.mouse.down=true;s.touchMining=true;});
  ['pointerup','pointercancel'].forEach(evt=>$('touchMine').addEventListener(evt,()=>{s.mouse.down=false;s.touchMining=false;}));
  $('touchUse').addEventListener('click',()=>D.interact(s));$('touchLamp').addEventListener('click',toggleLamp);
  function hud(){
    $('iron').textContent=s.cargo.iron;$('gold').textContent=s.cargo.gold;$('bank').textContent=s.bank;$('charges').textContent=s.charges;
    $('hearts').textContent='● '.repeat(s.health)+'○ '.repeat(5-s.health);
    $('lampStatus').classList.toggle('off',!s.flashlight);$('lampStatus').replaceChildren(document.createTextNode(`手电 ${s.flashlight?'ON':'OFF'} `));const k=document.createElement('kbd');k.textContent='F';$('lampStatus').append(k);
    [1,2,3,4].forEach(n=>$(`tool${n}`).classList.toggle('selected',s.tool===n));
    $('ladderCount').textContent=s.ladderKits;
    $('ladderSupplyButton').disabled=s.bank<3||s.ladderKits>=8;
    $('ladderSupplyButton').textContent=`补充梯具 · 3 试采点（随身 ${s.ladderKits}，现有 ${s.bank} 点）`;
    $('modeName').textContent=s.fast?'快速开采':'稳妥开采';$('modeDetail').textContent=s.fast?'更快 · 高震动':'较慢 · 低震动';$('modeButton').setAttribute('aria-pressed',String(s.fast));
    $('time').textContent=formatTime(s.time);$('depth').textContent=Math.max(0,Math.round((s.player.y/D.T-16)*2));
    $('prompt').textContent=s.prompt;
    const zone=D.zones[s.zone];$('zoneEyebrow').textContent=zone.eyebrow;$('zoneName').textContent=s.map.id==='baseline'?zone.title:`${s.map.title} · ${['浅层','中层','深层'][s.zone]}`;$('zoneDescription').textContent=s.map.id==='baseline'?zone.text:['第一笔表面矿，更多收获藏在裂岩里。','记住分岔；尽头不一定白走。','金矿更集中，挖开巢穴前留好退路。'][s.zone];$('zoneBanner').style.opacity=s.zoneTime>0?'1':'.28';
    $('currentLayout').textContent=`${s.map.title} · 地图对比 v0.4`;
    if(s.toastTime<=0)$('toast').classList.remove('visible');
    const w=s.map.worms.filter(w=>Math.abs(s.player.y-w.floor)<120&&Math.abs(s.player.x-w.anchor)<300).sort((a,b)=>b.noise-a.noise)[0];
    $('noiseFill').style.width=`${w?Math.min(100,w.noise/3.7*100):0}%`;
    $('noiseText').textContent=w&&w.mode==='warning'?'敲击前兆 · 停工换位置！':w&&w.mode==='rush'?'正在冲向原声源 · 退开！':'留意附近岩壁的敲击';
    const goals=[s.delivered.iron+s.delivered.gold>0,s.metrics.firstGold!==null,s.map.id==='baseline'?s.metrics.gateOpened.includes('shortcut'):s.metrics.hiddenRevealed>0];
    const names=['带回第一笔矿','发现一处金矿',s.map.id==='baseline'?'让回程变得更短':'挖出墙后的矿簇'];goals.forEach((done,i)=>{const el=$(`objective${i+1}`);el.textContent=`${done?'●':'○'} ${names[i]}`;el.classList.toggle('complete',done);});
  }
  function resize(){const r=canvas.getBoundingClientRect();if(Math.abs(r.width-layoutWidth)<1&&Math.abs(r.height-layoutHeight)<1)return;layoutWidth=r.width;layoutHeight=r.height;D.view.width=960;D.view.height=Math.round(960*r.height/r.width);canvas.width=D.view.width;canvas.height=D.view.height;}
  new ResizeObserver(resize).observe(canvas);resize();
  const initial=D.layouts.find(c=>c.id===s.map.id);$('introMapTitle').textContent=initial.number+' · '+s.map.title;$('introMapCopy').textContent=initial.description;hud();
  function frame(now){
    const dt=Math.min(.05,(now-last)/1000);last=now;accumulator+=dt;
    while(accumulator>=1/60){D.update(s,1/60);accumulator-=1/60;}
    D.render(s,dt);if(now-lastHud>100){hud();lastHud=now;}requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
})();
