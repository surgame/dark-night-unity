(async function () {
  "use strict";
  const root=document.getElementById("painted-wfc-cave"), canvas=root.querySelector("#art-scene"), mini=root.querySelector("#art-minimap");
  const ctx=canvas.getContext("2d"), mc=mini.getContext("2d"), U=CaveArt.U, cache=new Map();
  const controls={seed:root.querySelector("#art-seed"),mode:root.querySelector("#art-mode"),grid:root.querySelector("#art-grid")};
  const status=root.querySelector("#art-status");
  let map,art,width=736,height=450,miniWidth=736,room=3,busy=false;
  let state={seed:271,mode:"scene",grid:false,cx:29,cy:20,span:12};
  const saved=window.openai?.widgetState?.privateContent;
  function restore(value) {
    if(!value||value.version!==1)return;
    if([271,902,1643].includes(+value.seed))state.seed=+value.seed;
    if(["scene","geometry","catalog","sources"].includes(value.mode))state.mode=value.mode;
    state.grid=!!value.grid;
    if(Number.isFinite(value.cx))state.cx=Math.max(0,Math.min(80,value.cx));
    if(Number.isFinite(value.cy))state.cy=Math.max(0,Math.min(48,value.cy));
    if(Number.isFinite(value.span))state.span=Math.max(5,Math.min(100,value.span));
  }
  restore(saved);
  const images={};
  await Promise.all(Object.entries(CAVE_SOURCES).map(async([key,url])=>{
    const image=new Image();image.src=url;await image.decode();images[key]=image;
  }));
  art=CaveArt.prepare(images);
  const box=()=>({x:(state.cx-state.span/2)*U,y:state.cy*U-state.span*U*height/width/2,w:state.span*U,h:state.span*U*height/width});
  function save() {
    window.openai?.setWidgetState({modelContent:{seed:state.seed,view:state.mode,grid:state.grid},privateContent:{version:1,...state}})?.catch(()=>{});
  }
  function theme(name) {return getComputedStyle(root).getPropertyValue("--"+name).trim();}
  function resize() {
    width=Math.max(260,canvas.clientWidth);height=canvas.clientHeight;miniWidth=mini.clientWidth;
    const ratio=Math.min(devicePixelRatio||1,2);
    canvas.width=Math.round(width*ratio);canvas.height=Math.round(height*ratio);ctx.setTransform(ratio,0,0,ratio,0,0);
    mini.width=Math.round(miniWidth*ratio);mini.height=Math.round(96*ratio);mc.setTransform(ratio,0,0,ratio,0,0);
    draw();
  }
  function focus(index) {
    room=index%map.plan.nodes.length;
    const node=map.plan.nodes[room];state.cx=node.x;state.cy=node.y+.2;state.span=width<480?7:12;
  }
  function geometry(context,b,w,h) {
    context.save();context.clearRect(0,0,w,h);
    context.fillStyle=theme("muted");context.fillRect(0,0,w,h);
    context.scale(w/b.w,w/b.w);context.translate(-b.x,-b.y);
    context.fillStyle=theme("foreground");context.globalAlpha=.66;context.fill(map.artPath,"evenodd");context.globalAlpha=1;
    context.strokeStyle=theme("background");context.lineWidth=2*w/b.w;
    for(const s of map.artEdges){context.beginPath();context.moveTo(s.a.x,s.a.y);context.lineTo(s.b.x,s.b.y);context.stroke();}
    context.restore();
  }
  function gallery(sources) {
    ctx.clearRect(0,0,width,height);ctx.fillStyle=theme("muted");ctx.fillRect(0,0,width,height);
    const n=sources?6:16,cols=sources?3:4,rows=Math.ceil(n/cols),cw=width/cols,ch=height/rows;
    const names=["rock","floor","ceiling","backdrop","ore","hero"],labels=["岩体正面","地表与厚岩壁","悬顶与垂根","冷色后壁","矿簇装饰","原角色比例"];
    for(let i=0;i<n;i++) {
      const x=i%cols*cw,y=Math.floor(i/cols)*ch;
      const id=[0,3,12,15,48,51,60,63,192,195,204,207,240,243,252,255][i];
      const image=sources?images[names[i]]:CaveArt.tileCanvas(art,id,128,true);
      const scale=Math.min((cw-12)/image.width,(ch-32)/image.height);
      ctx.drawImage(image,x+(cw-image.width*scale)/2,y+4,image.width*scale,image.height*scale);
      ctx.fillStyle=theme("foreground");ctx.font="12px sans-serif";ctx.textAlign="center";
      ctx.fillText(sources?labels[i]:"形状 "+i,x+cw/2,y+ch-8);
    }
  }
  function drawMini() {
    mc.clearRect(0,0,miniWidth,96);
    const item=cache.get(state.seed);
    if(!item?.overview)return;
    const scale=Math.min(miniWidth/item.overview.width,96/item.overview.height);
    const w=item.overview.width*scale,h=item.overview.height*scale,left=(miniWidth-w)/2,top=(96-h)/2;
    mc.drawImage(item.overview,left,top,w,h);
    const b=box();mc.strokeStyle=theme("foreground");mc.lineWidth=1.5;
    mc.strokeRect(left+b.x/(map.width*U)*w,top+b.y/(map.height*U)*h,b.w/(map.width*U)*w,b.h/(map.height*U)*h);
  }
  function draw() {
    if(!map||!art)return;
    controls.seed.value=state.seed;controls.mode.value=state.mode;controls.grid.checked=state.grid;
    const b=box();
    if(state.mode==="catalog"||state.mode==="sources")gallery(state.mode==="sources");
    else {
      if(state.mode==="geometry")geometry(ctx,b,width,height);
      else CaveArt.draw(ctx,art,map,b,width,height,{hero:true,room});
      if(state.grid) {
        const scale=width/b.w;ctx.save();ctx.strokeStyle=theme("foreground");ctx.globalAlpha=.46;ctx.lineWidth=1;
        for(let x=Math.ceil(b.x/U);x<(b.x+b.w)/U;x++){ctx.beginPath();ctx.moveTo((x*U-b.x)*scale,0);ctx.lineTo((x*U-b.x)*scale,height);ctx.stroke();}
        for(let y=Math.ceil(b.y/U);y<(b.y+b.h)/U;y++){ctx.beginPath();ctx.moveTo(0,(y*U-b.y)*scale);ctx.lineTo(width,(y*U-b.y)*scale);ctx.stroke();}
        ctx.restore();
      }
    }
    drawMini();
    status.textContent=state.mode==="catalog"?"16 类拓扑 · 256 种角点组合 · 大模块边长对应 4 格":
      state.mode==="sources"?"5 张生成源图 + 原角色比例参考":
      "320 × 192 格 · WFC "+map.stats.observations+" 次选择 · 接边冲突 "+map.check.mismatches+" · 拖动画面 / 滚轮缩放";
    root.dataset.ready="true";
  }
  async function select(seed, keepCamera=false) {
    if(busy)return;busy=true;root.dataset.ready="false";status.textContent="WFC 约束求解与美术装配…";
    try {
      state.seed=seed;
      if(!cache.has(seed)) {
        const plan=CaveWB.makePlan(seed,80,48);
        const next=await CaveWB.solve(plan,{pause:()=>new Promise(resolve=>setTimeout(resolve,0))});
        const overview=CaveArt.canvas(1600,960);
        CaveArt.draw(overview.getContext("2d"),art,next,{x:0,y:0,w:80*U,h:48*U},1600,960,{noOre:false});
        cache.set(seed,{map:next,overview});
      }
      map=cache.get(seed).map;if(!keepCamera)focus(3);draw();
    } catch(error){status.textContent=error.message;root.dataset.error=error.message;throw error;}
    finally {busy=false;}
  }
  controls.seed.onchange=async()=>{await select(+controls.seed.value);save();};
  controls.mode.onchange=()=>{state.mode=controls.mode.value;draw();save();};
  controls.grid.onchange=()=>{state.grid=controls.grid.checked;draw();save();};
  root.querySelector("#art-all").onclick=()=>{state.cx=40;state.cy=24;state.span=Math.max(80,48*width/height)*1.03;draw();save();};
  root.querySelector("#art-local").onclick=()=>{focus(room+1);state.mode="scene";draw();save();};
  let drag=null;
  canvas.onpointerdown=e=>{if(!["scene","geometry"].includes(state.mode))return;drag={x:e.clientX,y:e.clientY,cx:state.cx,cy:state.cy};canvas.setPointerCapture(e.pointerId);};
  canvas.onpointermove=e=>{if(!drag)return;state.cx=drag.cx-(e.clientX-drag.x)*state.span/width;state.cy=drag.cy-(e.clientY-drag.y)*state.span/width;draw();};
  canvas.onpointerup=()=>{drag=null;save();};canvas.onpointercancel=()=>{drag=null;};
  canvas.addEventListener("wheel",e=>{if(!["scene","geometry"].includes(state.mode))return;e.preventDefault();state.span=Math.max(5,Math.min(100,state.span*Math.exp(e.deltaY*.001)));draw();save();},{passive:false});
  mini.onclick=e=>{
    const rect=mini.getBoundingClientRect(),scale=Math.min(rect.width/80,96/48),left=(rect.width-80*scale)/2,top=(96-48*scale)/2;
    state.cx=Math.max(0,Math.min(80,(e.clientX-rect.left-left)/scale));state.cy=Math.max(0,Math.min(48,(e.clientY-rect.top-top)/scale));
    state.span=width<480?7:12;state.mode="scene";draw();save();
  };
  window.addEventListener("openai:set_globals",async event=>{const next=event.detail?.globals?.widgetState?.privateContent;if(!next)return;restore(next);await select(state.seed,true);});
  new ResizeObserver(resize).observe(canvas);
  matchMedia("(prefers-color-scheme: dark)").addEventListener("change",draw);
  await select(state.seed,!!saved);resize();
  root._inspect=()=>({state:{...state},room,busy,width,height,seed:map.seed,stats:map.stats,check:{comparisons:map.check.comparisons,mismatches:map.check.mismatches,cornerConflicts:map.check.cornerConflicts},contours:map.geometry.contours.length});
  root._art={images,art,cache,select,get map(){return map;},draw,focus};
})();
