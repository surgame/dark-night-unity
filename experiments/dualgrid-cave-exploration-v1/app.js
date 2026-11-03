(function (C) {
  "use strict";
  const root=document.getElementById("dg-cave-whitebox"),find=id=>root.querySelector("#cw-"+id);
  if (!root) return;
  const canvas=find("scene"),renderer=C.makeRenderer(root,canvas,find("atlas"));
  const controller=new AbortController(),signal=controller.signal;
  const on=(el,type,fn,options={})=>el.addEventListener(type,fn,{...options,signal});
  let saved=C.readSaved(),world=C.makeWorld(C.decodeCuts(saved?.cuts)),last=0,accumulator=0;
  let dirty=true,active=false,drag=null,held=false,saveTimer=0,nextReadout=0;
  const input={left:false,right:false},pointers=new Map();
  const state={mode:"overview",camera:{x:124,y:70,scale:1},grid:false,labels:true,site:"entry",
    visited:new Set(),player:C.makePlayer(world,saved?.player),particles:[],pointer:null};
  const reduced=window.matchMedia("(prefers-reduced-motion: reduce)");
  C.sites.forEach((site,i)=>find("site").add(new Option((i+1)+" · "+site.name,site.id)));
  function fit() {
    const d=renderer.dimensions();state.camera={x:world.width/2,y:world.height/2,
      scale:Math.min((d.width-28)/world.width,(d.height-34)/world.height)};
  }
  function baseScale() {
    const d=renderer.dimensions();return Math.min(d.width/53,d.height/32);
  }
  function follow(snap=false,dt=.016) {
    const p=state.player,cam=state.camera,d=renderer.dimensions(),scale=baseScale();
    const x=C.clamp(p.x+p.facing*5,d.width/2/scale,world.width-d.width/2/scale);
    const y=C.clamp(p.y-5,d.height/2/scale,world.height-d.height/2/scale);
    const t=snap||reduced.matches?1:1-Math.exp(-dt*9);
    cam.x=C.mix(cam.x,x,t);cam.y=C.mix(cam.y,y,t);cam.scale=C.mix(cam.scale,scale,t);
  }
  function message(text) {find("feedback").textContent=text;}
  function ui() {
    root.dataset.mode=state.mode;
    find("overview").setAttribute("aria-pressed",state.mode==="overview");
    find("walk").setAttribute("aria-pressed",state.mode==="walk");
    find("grid").checked=state.grid;find("labels").checked=state.labels;
    find("site").value=state.site;
    find("detail").textContent=C.sites.find(s=>s.id===state.site).detail;
    find("counter").textContent="发现 "+state.visited.size+" / "+C.sites.length;
    find("view").textContent=state.mode==="walk"?"角色试走":state.mode==="overview"?"全图观察":"局部观察";
    dirty=true;
  }
  function saveSoon() {
    clearTimeout(saveTimer);saveTimer=setTimeout(()=>C.saveState(state,world),250);
  }
  function setMode(mode) {
    state.mode=mode;input.left=input.right=false;held=false;
    if (mode==="overview") fit();
    if (mode==="walk") {active=true;follow(true);}
    ui();saveSoon();
  }
  function focus(site) {
    state.site=site.id;state.mode="inspect";state.camera={
      x:site.x,y:site.y,scale:baseScale()*site.zoom};
    input.left=input.right=false;held=false;message("镜头聚焦 · 角色位置保持");ui();saveSoon();
  }
  function restore(value) {
    const s=C.rootState(value);if (!s) return;
    world=C.makeWorld(C.decodeCuts(s.cuts));renderer.setWorld(world);
    state.player=C.makePlayer(world,s.player);
    state.mode=["overview","inspect","walk"].includes(s.mode)?s.mode:"overview";
    state.grid=!!s.grid;state.labels=s.labels!==false;
    state.site=C.sites.some(p=>p.id===s.site)?s.site:"entry";
    state.visited=new Set((s.visited||[]).filter(id=>C.sites.some(p=>p.id===id)));
    if (s.camera && [s.camera.x,s.camera.y,s.camera.scale].every(Number.isFinite))
      state.camera={x:C.clamp(s.camera.x,0,world.width),y:C.clamp(s.camera.y,0,world.height),
        scale:C.clamp(s.camera.scale,.5,50)};
    if (state.mode==="overview") fit();if (state.mode==="walk") follow(true);
    state.particles=[];input.left=input.right=false;held=false;ui();
  }
  function mine(point) {
    const p=state.player;
    if (p.mining>0) return;
    p.mining=.16;
    if (Math.hypot(point.x-p.x,point.y-p.y)>4.7) {message("岩壁太远 · 靠近后采掘");return;}
    const result=C.dig(world,point.x,point.y);
    if (!result.changed.length) return;
    renderer.update(result.dirty);
    if (!reduced.matches) for (let i=0;i<7;i++) state.particles.push({
      x:point.x,y:point.y,vx:Math.sin(i*13+world.revision)*2.4,vy:-2-i*.24,
      r:.055+i*.008,life:.34+i*.025});
    state.particles=state.particles.slice(-84);message("已敲碎 "+world.cuts.size+" 个逻辑样本");
    dirty=true;saveSoon();
  }
  function nearRock() {
    const p=state.player,candidates=[];
    for (let y=Math.floor(p.y-3);y<=Math.ceil(p.y+4);y++)
      for (let x=Math.floor(p.x-3);x<=Math.ceil(p.x+3);x++) {
        if (x<2 || y<2 || x>world.width-2 || y>world.height-2) continue;
        if (!world.data[y*(world.width+1)+x] || Math.hypot(x-p.x,y-p.y)>4) continue;
        candidates.push({x,y,score:Math.hypot(x-p.x-p.facing,y-p.y-1.5)});
      }
    candidates.sort((a,b)=>a.score-b.score);
    return candidates[0]||{x:p.x+p.facing,y:p.y+2};
  }
  function cursor(e) {
    const b=canvas.getBoundingClientRect();
    return {x:e.clientX-b.left,y:e.clientY-b.top};
  }
  on(find("overview"),"click",()=>setMode("overview"));
  on(find("walk"),"click",e=>{setMode("walk");e.currentTarget.blur();});
  on(find("entry"),"click",e=>{
    state.player=C.makePlayer(world);setMode("walk");message("返回入口 · 采掘结果保留");
    e.currentTarget.blur();
  });
  on(find("reset"),"click",()=>{
    world=C.makeWorld();renderer.setWorld(world);state.player=C.makePlayer(world);
    state.visited.clear();state.particles=[];state.site="entry";
    setMode("overview");message("洞穴已还原");
  });
  on(find("site"),"change",e=>focus(C.sites.find(s=>s.id===e.target.value)));
  on(find("grid"),"change",e=>{state.grid=e.target.checked;dirty=true;saveSoon();});
  on(find("labels"),"change",e=>{state.labels=e.target.checked;dirty=true;saveSoon();});
  on(find("atlas-open"),"toggle",()=>renderer.paintAtlas());
  on(canvas,"contextmenu",e=>e.preventDefault());
  on(canvas,"pointerdown",e=>{
    active=true;canvas.setPointerCapture(e.pointerId);
    const q=cursor(e);pointers.set(e.pointerId,q);
    state.pointer={...q,world:renderer.point(state.camera,q.x,q.y),panning:false};
    if (pointers.size>1 || e.button===2 || state.mode!=="walk" || e.pointerType==="touch") {
      const multi=pointers.size>1;
      drag={x:q.x,y:q.y,camera:{...state.camera},moved:false,pinch:null,
        walkTap:state.mode==="walk" && e.pointerType==="touch" && !multi,multi};
      state.pointer.panning=true;held=false;
      if (pointers.size>1) {state.mode="inspect";ui();}
    } else if (e.button===0) {held=true;mine(state.pointer.world);}
  });
  on(canvas,"pointermove",e=>{
    const q=cursor(e);if (pointers.has(e.pointerId)) pointers.set(e.pointerId,q);
    state.pointer={...q,world:renderer.point(state.camera,q.x,q.y),panning:!!drag};
    if (drag) {
      if (pointers.size>1) {
        const a=[...pointers.values()],distance=Math.hypot(a[0].x-a[1].x,a[0].y-a[1].y);
        if (!drag.pinch) drag.pinch={distance,scale:state.camera.scale};
        state.camera.scale=C.clamp(drag.pinch.scale*distance/Math.max(1,drag.pinch.distance),.6,40);
      } else {
        state.camera.x=drag.camera.x-(q.x-drag.x)/state.camera.scale;
        state.camera.y=drag.camera.y-(q.y-drag.y)/state.camera.scale;
      }
      if (Math.hypot(q.x-drag.x,q.y-drag.y)>4) {
        drag.moved=true;state.mode="inspect";input.left=input.right=false;ui();
      }
    }
    dirty=true;
  });
  function release(e) {
    pointers.delete(e.pointerId);
    if (drag?.walkTap && !drag.moved && !drag.multi && !pointers.size) mine(state.pointer.world);
    if (drag && !drag.moved && !drag.multi && state.mode!=="walk" && e.button===0) {
      const q=cursor(e),hit=C.sites.find(s=>{
        const p=renderer.screen(state.camera,s.x,s.y);return Math.hypot(q.x-p.x,q.y-p.y)<19;
      });
      if (hit) focus(hit);
    }
    held=false;drag=null;
    if (state.pointer) state.pointer.panning=false;
    saveSoon();
  }
  on(canvas,"pointerup",release);on(canvas,"pointercancel",release);
  on(canvas,"pointerleave",()=>{if (!pointers.size) state.pointer=null;dirty=true;});
  on(canvas,"wheel",e=>{
    e.preventDefault();const q=cursor(e),before=renderer.point(state.camera,q.x,q.y);
    state.mode="inspect";input.left=input.right=false;
    state.camera.scale=C.clamp(state.camera.scale*Math.exp(-e.deltaY*.0014),.6,40);
    const after=renderer.point(state.camera,q.x,q.y);
    state.camera.x+=before.x-after.x;state.camera.y+=before.y-after.y;
    ui();saveSoon();
  },{passive:false});
  on(window,"keydown",e=>{
    if (e.ctrlKey || e.metaKey || e.altKey) return;
    if (!active || state.mode!=="walk" || /INPUT|SELECT|TEXTAREA|BUTTON/.test(e.target.tagName)) return;
    if (["ArrowLeft","ArrowRight","Space","KeyA","KeyD"].includes(e.code)) e.preventDefault();else return;
    if (e.code==="ArrowLeft"||e.code==="KeyA") input.left=true;
    if (e.code==="ArrowRight"||e.code==="KeyD") input.right=true;
    if (e.code==="Space"&&!e.repeat) state.player.jumpBuffer=.12;
  });
  on(window,"keyup",e=>{
    if (!active || !["ArrowLeft","ArrowRight","KeyA","KeyD","Space"].includes(e.code)) return;
    if (e.code==="ArrowLeft"||e.code==="KeyA") input.left=false;
    if (e.code==="ArrowRight"||e.code==="KeyD") input.right=false;
    saveSoon();
  });
  on(window,"blur",()=>{input.left=input.right=false;held=false;});
  on(document,"pointerdown",e=>{if (!root.contains(e.target)) {active=false;input.left=input.right=false;}});
  root.querySelectorAll("[data-hold]").forEach(button=>{
    on(button,"pointerdown",e=>{e.preventDefault();button.setPointerCapture(e.pointerId);input[button.dataset.hold]=true;});
    const stop=()=>{input[button.dataset.hold]=false;saveSoon();};
    on(button,"pointerup",stop);on(button,"pointercancel",stop);
  });
  on(find("touch-jump"),"pointerdown",e=>{e.preventDefault();state.player.jumpBuffer=.12;});
  on(find("touch-mine"),"click",()=>mine(nearRock()));
  on(window,"openai:set_globals",e=>{
    const s=C.rootState(e.detail?.globals?.widgetState);
    if (s && JSON.stringify(s)!==C.lastWrittenState) restore(s);
  });
  const resize=new ResizeObserver(()=>{
    renderer.resize();if (state.mode==="overview") fit();
    if (state.mode==="walk") follow(true);dirty=true;
  });
  resize.observe(find("scene"));
  const theme=new MutationObserver(()=>{renderer.colors();dirty=true;});
  theme.observe(document.documentElement,{attributes:true,attributeFilter:["class","style","data-theme"]});
  on(window.matchMedia("(prefers-color-scheme: dark)"),"change",()=>{renderer.colors();dirty=true;});
  renderer.setWorld(world);fit();ui();if (saved) restore(saved);
  function frame(time) {
    if (!root.isConnected) {controller.abort();resize.disconnect();theme.disconnect();clearTimeout(saveTimer);return;}
    const dt=Math.min(.067,last?(time-last)/1000:0);last=time;
    if (!document.hidden && state.mode==="walk") {
      accumulator+=dt;
      while (accumulator>=1/120) {C.stepPlayer(world,state.player,input,1/120);accumulator-=1/120;}
      if (held && state.pointer) mine(state.pointer.world);
      follow(false,dt);dirty=true;
      for (const site of C.sites) if (Math.hypot(site.x-state.player.x,site.y-state.player.y)<(site.radius||9))
        state.visited.add(site.id);
      if (time>nextReadout) {state.site=C.nearestSite(state.player.x,state.player.y).id;ui();nextReadout=time+250;}
    } else accumulator=0;
    for (const p of state.particles) {p.x+=p.vx*dt;p.vy+=14*dt;p.y+=p.vy*dt;p.life-=dt;dirty=true;}
    state.particles=state.particles.filter(p=>p.life>0);
    if (dirty) {renderer.render(state);dirty=false;}
    requestAnimationFrame(frame);
  }
  root._inspect=()=>({ready:true,mode:state.mode,site:state.site,visited:[...state.visited],
    player:{x:state.player.x,y:state.player.y,grounded:state.player.grounded},
    camera:{...state.camera},dug:world.cuts.size,revision:world.revision,
    size:[world.width,world.height],insideRock:C.playerCollides(world,state.player.x,state.player.y)});
  root._digTarget=()=>{
    const q=nearRock();
    return {...renderer.screen(state.camera,q.x,q.y),world:q};
  };
  requestAnimationFrame(frame);
})(globalThis.CaveWhitebox);
