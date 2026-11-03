(async function () {
  "use strict";
  const root=document.getElementById("macro-cave-playable"),$=id=>root.querySelector("#"+id);
  const api=MacroCave,canvas=$("mc-scene"),ctx=canvas.getContext("2d");
  const tiles=api.catalog(MACRO_PROFILES);
  api.moduleContent=MACRO_CONTENT;
  api.silhouettes=MACRO_SILHOUETTES;
  let solved,placements,player,world,camera,active=false,last=0,accumulator=0,dirty=true;
  let pressed=new Set(),touchAxis=0,mineHeld=false,lastModule=-1,lastTheme="",lastSaved="";
  let state={version:2,seed:309,view:"walk",bounds:false,light:true,trace:0,layer:"all"};
  const shade=document.createElement("canvas"),probe=document.createElement("span");
  probe.hidden=true;root.appendChild(probe);
  const sources=await Promise.all(Object.values(MACRO_ART).map(src=>new Promise((resolve,reject)=>{
    const image=new Image();image.onload=()=>resolve(image);image.onerror=reject;image.src=src;
  })));
  const art={panels:api.preparePanels(sources[0]),hero:sources[1],ore:sources[3]};
  art.fronts=api.separateForeground(art.panels,MACRO_SILHOUETTES);
  art.backs=api.prepareBackwalls(sources[2]);
  const oreCoverage=api.oreCoverage(art.ore);
  function colors() {
    const get=token=>{probe.style.color=`var(${token})`;return getComputedStyle(probe).color;};
    return {foreground:get("--foreground"),background:get("--background"),active:get("--viz-series-1")};
  }
  function rebuild(x=160) {
    solved=api.solve(tiles,state.seed);placements=api.place(solved,tiles);
    world=api.createTerrain(state.seed,placements,tiles,oreCoverage);
    player=api.character(placements,tiles,x,world);state.trace=Math.min(state.trace,solved.trace.length-1);
    pressed.clear();touchAxis=0;mineHeld=false;lastModule=-1;dirty=true;
    $("mc-trace").max=String(solved.trace.length-1);
  }
  function save() {
    if(!window.openai?.setWidgetState)return;
    const index=Math.floor(player.x/api.width);
    const snapshot={...state,x:Math.round(player.x*100)/100,face:player.face,terrain:api.terrainSnapshot(world)};
    lastSaved=JSON.stringify(snapshot);
    window.openai.setWidgetState({
      modelContent:{demo:"macro-cave-playable",seed:state.seed,view:state.view,bounds:state.bounds,
        light:state.light,module:index+1,moduleKey:tiles[placements[index].id].key,
        moduleSequence:solved.ids.map(id=>tiles[id].key),observations:solved.stats.observations,
        layer:state.layer,clearedCells:world.clear.size,harvestedMineralCells:world.harvested},
      privateContent:snapshot
    }).catch(()=>{});
  }
  function restore(snapshot) {
    const incoming=snapshot?.privateContent;
    if(!incoming||![1,2].includes(incoming.version)||JSON.stringify(incoming)===lastSaved)return false;
    const next={version:2,seed:Number(incoming.seed)>>>0,
      view:["walk","atlas","solve"].includes(incoming.view)?incoming.view:"walk",
      bounds:!!incoming.bounds,light:incoming.light!==false,trace:Math.max(0,Math.floor(incoming.trace)||0),
      layer:["all","background","mineral","foreground"].includes(incoming.layer)?incoming.layer:"all"};
    state=next;
    rebuild(Number.isFinite(incoming.x)?incoming.x:160);
    api.restoreTerrain(world,incoming.terrain);
    player=api.character(placements,tiles,player.x,world);
    player.face=incoming.face===-1?-1:1;sync();return true;
  }
  function sync() {
    $("mc-view").value=state.view;$("mc-bounds").checked=state.bounds;$("mc-light").checked=state.light;
    $("mc-layer").value=state.layer;
    $("mc-trace-label").hidden=state.view!=="solve";$("mc-trace").value=String(state.trace);
    $("mc-trace-value").textContent=`${state.trace+1} / ${solved.trace.length}`;
    resize();status();dirty=true;
  }
  function status() {
    if(state.view==="atlas") {
      $("mc-status").textContent="6 个分层构图 · 图层可独立查看 · 矿层状态独立于后壁";
    } else if(state.view==="solve") {
      const event=solved.trace[state.trace],name=event.kind==="observe"?"选择模块":event.kind==="propagate"?"邻居排除":"回退";
      const remaining=event.after?.length||1;
      $("mc-status").textContent=`${name} · 槽位 ${event.at+1} · ${event.before?.length||1} → ${remaining} 个候选`;
    } else {
      const index=Math.floor(player.x/api.width),tile=tiles[placements[index].id];
      $("mc-status").textContent=`种子 ${state.seed} · ${index+1}/16 ${tile.name} · 已采空 ${world.harvested} 矿格${world.message?" · "+world.message:""}`;
    }
  }
  function resize() {
    const w=Math.max(280,root.getBoundingClientRect().width);
    let h=w*9/16;
    if(state.view==="atlas") {
      const cols=w>=600?3:2,cell=(w-(cols-1)*10)/cols;
      h=Math.ceil(6/cols)*(cell*414/623+34);
    }
    if(state.view==="solve")h=(w>=600?4:8)*((w-(w>=600?3:1)*10)/(w>=600?4:2)*414/623+37);
    canvas.style.aspectRatio=`${w} / ${h}`;
    const dpr=Math.min(2,window.devicePixelRatio||1);
    const pw=Math.round(w*dpr),ph=Math.round(h*dpr);
    if(canvas.width!==pw||canvas.height!==ph){canvas.width=pw;canvas.height=ph;}
    dirty=true;
  }
  function render() {
    const rect=canvas.getBoundingClientRect(),dpr=canvas.width/rect.width;
    const size={w:rect.width,h:rect.height},theme=colors();
    ctx.setTransform(dpr,0,0,dpr,0,0);
    const model={tiles,placements,player,state,solved,world};
    if(state.view==="walk")camera=api.paintWalk(ctx,size,model,art,theme,shade);
    else if(state.view==="atlas")api.paintAtlas(ctx,size,model,art,theme);
    else api.paintTrace(ctx,size,model,art,theme);
    dirty=false;
  }
  function activate() {
    active=true;$("mc-play").textContent="操作中";$("mc-play").setAttribute("aria-pressed","true");
    $("mc-play").focus({preventScroll:true});
  }
  function release() {
    pressed.clear();touchAxis=0;mineHeld=false;active=false;
    $("mc-play").textContent="开始漫游";$("mc-play").setAttribute("aria-pressed","false");
    save();
  }
  $("mc-play").onclick=()=>{if(state.view!=="walk"){state.view="walk";sync();}activate();};
  function mine(point) {
    if(state.view!=="walk")return;
    active=true;
    const before=world.message,accepted=api.miningRequest(world,player,point);
    if(accepted||before!==world.message){dirty=true;status();}
  }
  canvas.addEventListener("pointerdown",event=>{
    if(state.view!=="walk")return;
    activate();
    const rect=canvas.getBoundingClientRect();
    mine({x:camera.x+(event.clientX-rect.left)/camera.scale,
      y:camera.y+(event.clientY-rect.top)/camera.scale});
  });
  $("mc-seed").onclick=()=>{
    state.seed=(state.seed+7321)>>>0;state.trace=0;rebuild();sync();save();
  };
  $("mc-view").onchange=event=>{release();state.view=event.target.value;sync();save();};
  $("mc-bounds").onchange=event=>{state.bounds=event.target.checked;dirty=true;save();};
  $("mc-light").onchange=event=>{state.light=event.target.checked;dirty=true;save();};
  $("mc-layer").onchange=event=>{state.layer=event.target.value;dirty=true;save();};
  $("mc-trace").oninput=event=>{state.trace=Number(event.target.value);sync();save();};
  const keyMap={ArrowLeft:-1,KeyA:-1,ArrowRight:1,KeyD:1};
  window.addEventListener("keydown",event=>{
    if(!active||state.view!=="walk"||!root.contains(document.activeElement))return;
    if(event.code in keyMap){event.preventDefault();pressed.add(event.code);}
    if(event.code==="Space"){event.preventDefault();if(!event.repeat)api.jump(player);}
    if(event.code==="KeyJ"){event.preventDefault();pressed.add(event.code);mine();}
  });
  window.addEventListener("keyup",event=>{
    if(pressed.has(event.code)){event.preventDefault();pressed.delete(event.code);save();}
  });
  window.addEventListener("blur",release);
  document.addEventListener("visibilitychange",()=>{if(document.hidden)release();});
  for(const [id,direction] of [["mc-left",-1],["mc-right",1]]) {
    const button=$(id);
    button.addEventListener("pointerdown",event=>{
      if(state.view!=="walk"){state.view="walk";sync();}
      active=true;touchAxis=direction;button.setPointerCapture(event.pointerId);event.preventDefault();
    });
    for(const event of ["pointerup","pointercancel","lostpointercapture"])button.addEventListener(event,()=>{
      touchAxis=0;save();
    });
    button.addEventListener("keydown",event=>{
      if(event.key===" "||event.key==="Enter"){event.preventDefault();active=true;touchAxis=direction;}
    });
    button.addEventListener("keyup",()=>{touchAxis=0;save();});
  }
  $("mc-jump").onclick=()=>{if(state.view==="walk"){active=true;api.jump(player);dirty=true;}};
  $("mc-mine").addEventListener("pointerdown",event=>{
    mineHeld=true;$("mc-mine").setPointerCapture(event.pointerId);event.preventDefault();mine();
  });
  for(const event of ["pointerup","pointercancel","lostpointercapture"])
    $("mc-mine").addEventListener(event,()=>{mineHeld=false;save();});
  $("mc-mine").addEventListener("click",()=>mine());
  new ResizeObserver(resize).observe(root);
  window.addEventListener("openai:set_globals",event=>{
    const globals=event.detail?.globals;
    if(globals&&"widgetState" in globals)restore(globals.widgetState);
    dirty=true;
  });
  rebuild();
  restore(window.openai?.widgetState);sync();
  root._inspect=()=>({state:{...state},player:{...player},camera,
    moduleSequence:solved.ids.map(id=>tiles[id].key),stats:solved.stats,
    seamErrors:placements.slice(1).map((p,i)=>Math.abs(api.surface(placements,tiles,p.x-.0001)-api.surface(placements,tiles,p.x+.0001))),
    socketErrors:solved.ids.slice(1).filter((id,i)=>!api.compatible(tiles[solved.ids[i]],tiles[id])).length,
    trace:solved.trace,active,art:{panels:art.panels.length,sourceSize:[sources[0].width,sources[0].height]},
    terrain:{clear:[...world.clear],rock:[...world.rock],ore:[...world.ore].map(([id,v])=>({id,...v})),
      sites:world.sites,harvested:world.harvested,revision:world.revision,action:world.action,
      message:world.message,serialized:api.terrainSnapshot(world)}});
  root._advance=(seconds,axis=0)=>{
    for(let i=0;i<Math.round(seconds*120);i++) {
      api.miningTick(world,1/120,player);api.step(player,axis,1/120,placements,tiles,world);
    }
    dirty=true;status();render();
  };
  root._teleport=x=>{player=api.character(placements,tiles,x,world);dirty=true;status();render();};
  root._jump=()=>api.jump(player);
  root._mine=point=>{const result=api.miningRequest(world,player,point);dirty=true;status();return result;};
  root._target=point=>api.terrainRay(world,{x:player.x+player.face*13,y:player.y-26},point);
  root._solid=point=>api.terrainSolid(world,point.x,point.y);
  root._save=save;
  root._layerSources=()=>art.fronts.map(c=>c.toDataURL("image/png"));
  root._backgroundSignature=()=>art.backs.map(c=>{
    const pixels=c.getContext("2d").getImageData(0,0,c.width,c.height).data;
    let value=2166136261;
    for(const channel of pixels)value=Math.imul(value^channel,16777619);
    return value>>>0;
  });
  root._foregroundAlpha=(panel,x,y)=>art.fronts[panel].getContext("2d").getImageData(x,y,1,1).data[3];
  root.dataset.ready="true";render();
  function frame(time) {
    const dt=last?Math.min(.05,(time-last)/1000):0;last=time;
    const axis=touchAxis||Math.max(-1,Math.min(1,[...pressed].reduce((sum,key)=>sum+(keyMap[key]||0),0)));
    if(state.view==="walk"&&(active||!player.grounded||world.action)) {
      accumulator+=dt;
      while(accumulator>=1/120) {
        if(mineHeld||pressed.has("KeyJ"))mine();
        if(api.miningTick(world,1/120,player)){dirty=true;status();save();}
        dirty=api.step(player,axis,1/120,placements,tiles,world)||dirty;
        accumulator-=1/120;
      }
      const index=Math.floor(player.x/api.width);
      if(index!==lastModule){lastModule=index;status();}
    } else accumulator=0;
    const theme=getComputedStyle(root).color;
    if(theme!==lastTheme){lastTheme=theme;dirty=true;}
    if(dirty)render();
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
})().catch(error=>{
  const root=document.getElementById("macro-cave-playable");
  root.dataset.error=String(error);root.querySelector("#mc-status").textContent="预览加载失败："+error.message;
  console.error(error);
});
