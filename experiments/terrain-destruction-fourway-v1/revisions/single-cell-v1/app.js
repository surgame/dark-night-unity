(function(T) {
  "use strict";
  const root=document.getElementById("terrain-fourway"),el=id=>root.querySelector("#tf-"+id);
  const canvas=el("gpu"),overlay=el("overlay"),ctx=overlay.getContext("2d"),buttons=[...root.querySelectorAll("[data-method]")];
  const saved=window.openai?.widgetState?.privateContent;
  const accepts=s=>s?.version===1&&T.methods.includes(s.method)&&[96,320].includes(s.size);
  let state={version:1,kind:"terrain-fourway",size:96,method:"dual",zoom:1,debug:false,...(accepts(saved)?saved:{})};
  state.zoom=[1,2,4].includes(state.zoom)?state.zoom:1;state.debug=!!state.debug;
  T.references??={};if(T.reference&&!T.references["96"])T.references["96"]=T.reference;
  let gpu,field,adapters,camera,hover=null,report=T.references[String(state.size)]??null,busy=false,count=0,particles=[],frame=0;
  const measuredHere=new Set();
  const media=matchMedia("(prefers-reduced-motion: reduce)"),pointers=new Map();
  const mix=(a,b,t)=>a.map((x,i)=>x+(b[i]-x)*t),rgb=a=>"rgb("+a.map(x=>Math.round(x*255)).join(",")+")";
  const ms=x=>x<(report?.timerResolutionMs??.1)*.5?"<"+(report?.timerResolutionMs??.1).toFixed(2):x.toFixed(2),
    mib=x=>(x/1048576).toFixed(2);
  function theme() {
    const probe=document.createElement("span"),sample=document.createElement("canvas"),c=sample.getContext("2d",{willReadFrequently:true});
    sample.width=sample.height=1;root.append(probe);
    const read=name=>{
      probe.style.color="var("+name+")";c.fillStyle=getComputedStyle(probe).color;c.fillRect(0,0,1,1);
      return [...c.getImageData(0,0,1,1).data].slice(0,3).map(n=>n/255);
    };
    const bg=read("--background"),fg=read("--foreground"),orange=read("--orange"),blue=read("--blue"),yellow=read("--yellow"),
      green=read("--green"),series=read("--viz-series-1"),second=read("--viz-series-2");
    probe.remove();const dark=bg.reduce((n,v)=>n+v,0)<1.5;
    const ink=dark?mix(bg,fg,.035):mix(bg,fg,.86);
    return {background:mix(ink,blue,.14),rock:mix(mix(orange,bg,.40),ink,.38),
      rim:mix(mix(orange,yellow,.50),bg,.35),ink,
      ecology:green,fg,bg,series,second};
  }
  let palette=theme();
  function encodeEdits() {
    const original=T.field(field.w,field.h),bytes=new Uint8Array(Math.ceil(field.data.length/8));
    for(let i=0;i<field.data.length;i++)if(original.data[i]&&!field.data[i])bytes[i>>3]|=1<<(i&7);
    return btoa(String.fromCharCode(...bytes));
  }
  function persist() {
    const privateContent={...state,x:camera.x,y:camera.y,edits:encodeEdits()};
    window.openai?.setWidgetState?.({modelContent:{view:"terrain-fourway",method:state.method,size:state.size,
      destructions:count,benchmark:report?{renderer:report.renderer,software:report.software,success:report.success}:null},
      privateContent})?.catch(()=>{});
  }
  let saveTimer=0;
  function saveSoon() {clearTimeout(saveTimer);saveTimer=setTimeout(persist,180);}
  function clampCamera() {
    camera.w=field.w/state.zoom;camera.h=field.h/state.zoom;
    camera.x=Math.max(0,Math.min(field.w-camera.w,camera.x));
    camera.y=Math.max(0,Math.min(field.h-camera.h,camera.y));
  }
  function load(restore=true) {
    gpu?.clear();field=T.field(state.size,state.size===320?192:60);
    if(restore&&state.edits) {
      try {
        const bytes=Uint8Array.from(atob(state.edits),c=>c.charCodeAt(0));
        if(bytes.length===Math.ceil(field.data.length/8))
          for(let i=0;i<field.data.length;i++)if(bytes[i>>3]&(1<<(i&7)))field.data[i]=0;
      }catch(_){state.edits="";}
    }
    const original=T.field(field.w,field.h);count=original.data.reduce((n,v,i)=>n+(v&&!field.data[i]),0);
    adapters=Object.fromEntries(T.methods.map(method=>[method,T.adapter(field,method)]));
    for(const a of Object.values(adapters))gpu.upload(a);
    camera={x:restore&&Number.isFinite(state.x)?state.x:field.w*(1-1/state.zoom)/2,
      y:restore&&Number.isFinite(state.y)?state.y:field.h*(1-1/state.zoom)/2,w:field.w,h:field.h};
    clampCamera();draw();refresh();renderReport();
  }
  function draw() {
    if(gpu.gl.isContextLost())return;
    gpu.draw(adapters[state.method],camera,palette,true);paintOverlay();
  }
  function screen(x,y) {return [(x-camera.x)/camera.w*overlay.width,(y-camera.y)/camera.h*overlay.height];}
  function world(event) {
    const r=overlay.getBoundingClientRect();
    return {x:camera.x+(event.clientX-r.left)/r.width*camera.w,y:camera.y+(event.clientY-r.top)/r.height*camera.h};
  }
  const hero=new Image();hero.src=T.hero??"";hero.onload=()=>paintOverlay();
  function paintOverlay() {
    if(!camera)return;
    ctx.clearRect(0,0,overlay.width,overlay.height);
    const a=adapters[state.method],sx=overlay.width/camera.w,sy=overlay.height/camera.h;
    if(state.debug&&a.last) {
      ctx.fillStyle=rgb(palette.series);ctx.strokeStyle=rgb(palette.series);ctx.lineWidth=1;
      for(const key of a.last.chunks) {
        const r=T.chunkRect(key,a.w,a.h),p=screen(r.x,r.y);
        ctx.globalAlpha=.12;ctx.fillRect(p[0],p[1],r.w*sx,r.h*sy);
        ctx.globalAlpha=.7;ctx.strokeRect(p[0],p[1],r.w*sx,r.h*sy);
      }
      ctx.globalAlpha=.7;ctx.strokeStyle=rgb(palette.second);
      for(const key of a.last.solveKeys??[]) {
        const p=screen(key%a.w,(key/a.w)|0);ctx.strokeRect(p[0],p[1],sx,sy);
      }
      ctx.globalAlpha=1;
    }
    const hx=field.w*.17;let hy=field.h*.48;
    while(hy<field.h-1&&!T.inside(T.maskAt(field,Math.floor(hx),Math.floor(hy)),hx%1,hy%1))hy+=.04;
    if(hero.complete&&hero.naturalWidth) {
      const size=field.h*.095,p=screen(hx,hy),height=size*sy,width=height*hero.naturalWidth/hero.naturalHeight;
      ctx.drawImage(hero,p[0]-width/2,p[1]-height,width,height);
    }
    if(hover&&!busy) {
      const index=T.hit(field,hover.x,hover.y);
      if(index>=0) {
        const x=index%(field.w+1),y=(index/(field.w+1))|0,p=screen(x-.5,y-.5);
        ctx.strokeStyle=rgb(palette.fg);ctx.lineWidth=1.5;ctx.strokeRect(p[0],p[1],sx,sy);
      }
    }
    const now=performance.now();
    particles=particles.filter(p=>now-p.start<360);
    ctx.fillStyle=rgb(palette.rim);
    for(const p of particles) {
      const t=(now-p.start)/360,q=screen(p.x+p.vx*t,p.y+p.vy*t+t*t*2);
      ctx.globalAlpha=1-t;ctx.fillRect(q[0],q[1],Math.max(2,sx*.12),Math.max(2,sy*.12));
    }
    ctx.globalAlpha=1;
    if(particles.length&&!frame)frame=requestAnimationFrame(()=>{frame=0;paintOverlay();});
  }
  function refresh() {
    for(const b of buttons){const selected=b.dataset.method===state.method;
      b.setAttribute("aria-pressed",String(selected));b.classList.toggle("btn-primary",selected);}
    el("description").textContent=T.descriptions[state.method];
    el("count").textContent=count+" 格已破坏";
    const last=adapters[state.method].last;
    el("hit").textContent=last?
      "本次 "+last.patches.length+" 个形状格 · "+last.chunks.length+" 个区块 · 求解 "+last.nodes+
      " 节点 · 回溯 "+last.backtracks+" · "+(last.fallback?"已回退普通岩面":last.expansions?"扩展 "+last.expansions+" 次":"局部完成")+
      " · CPU 更新 "+ms(last.select+last.geometry)+" ms":"四方案联动破坏 · 单方案性能另用独立副本测量";
  }
  function breakAt(index,render=true) {
    if(busy||gpu.gl.isContextLost()||index<0||!field.data[index])return false;
    field.data[index]=0;count++;
    for(const a of Object.values(adapters)) {
      const result=T.breakCell(a,index);if(!result)throw Error("联动破坏输入不一致。");
      result.upload=gpu.upload(a,result.chunks).ms;
    }
    if(!media.matches)for(let j=0;j<6;j++)particles.push({x:index%(field.w+1),y:(index/(field.w+1))|0,
      vx:(T.hash(index,j)%100/100-.5)*3,vy:-(T.hash(j,index)%100/100)*1.8,start:performance.now()});
    if(particles.length>80)particles=particles.slice(-80);
    if(render){draw();refresh();saveSoon();}return true;
  }
  function renderReport() {
    const body=el("results");body.replaceChildren();
    for(const method of T.methods) {
      const tr=document.createElement("tr"),s=report?.summary?.[method];
      for(const [i,text] of [T.names[method],s?ms(s.initial.p50):"—",s?ms(s.static.p50):"—",s?ms(s.select.p50):"—",s?ms(s.geometry.p50):"—",
        s?ms(s.upload.p50):"—",s?ms(s.total.p50)+" / "+ms(s.total.p95):"—"].entries()) {
        const td=document.createElement(i===0?"th":"td");td.textContent=text;if(i)td.className="text-end";tr.append(td);
      }
      body.append(tr);
    }
    el("bench-meta").textContent=report?
      (measuredHere.has(report.width)?"本机重测":"参考测量")+" "+
      new Date(report.date).toLocaleDateString("zh-CN",{timeZone:"Asia/Shanghai"})+" · "+
      (report.software?"软件渲染器 · ":"")+report.width+"×"+report.height+" 格 · "+report.canvas.join("×")+
      " 输出 · 每方案 "+report.summary.dual.total.n+" 次有效破坏 · 计时粒度 "+(report.timerResolutionMs??.1).toFixed(2)+" ms · "+report.renderer:
      "尚未测量 · 基准不会改变上方已敲碎的地图";
    el("resource").textContent=report?T.methods.map(m=>{
      const s=report.summary[m];return T.names[m]+"："+s.payload.vertices.toLocaleString()+" 顶点 / "+mib(s.payload.bytes)+" MiB";
    }).join("；")+"。网格有效载荷各计单份，共享纹理 "+mib(report.textureBytes)+" MiB；不含驱动及 JS 对象。":"";
  }
  async function benchmark(options={}) {
    if(busy)return null;busy=true;
    const controls=[...root.querySelectorAll("button,select,input")];controls.forEach(c=>c.disabled=true);
    const target=document.createElement("canvas");target.width=960;target.height=600;
    try {
      report=await T.benchmark({w:field.w,h:field.h,palette,canvas:target,...options},p=>{
        el("progress").textContent=T.names[p.method]+" · "+(p.kind==="scatter"?"散点":"连续")+" · 第 "+(p.round+1)+"/"+p.rounds+" 轮";
      });
      T.references[String(field.w)]=report;measuredHere.add(field.w);
      renderReport();el("progress").textContent="评估完成";persist();return report;
    }catch(error){el("error").hidden=false;el("error").textContent=error.message;throw error;}
    finally{busy=false;controls.forEach(c=>c.disabled=false);draw();}
  }
  buttons.forEach(b=>b.addEventListener("click",()=>{state.method=b.dataset.method;draw();refresh();saveSoon();}));
  el("size").value=String(state.size);el("zoom").value=String(state.zoom);el("debug").checked=state.debug;
  el("size").addEventListener("change",()=>{state.size=Number(el("size").value);state.edits="";state.zoom=1;el("zoom").value="1";
    report=T.references[String(state.size)]??null;load(false);saveSoon();});
  el("zoom").addEventListener("change",()=>{const cx=camera.x+camera.w/2,cy=camera.y+camera.h/2;
    state.zoom=Number(el("zoom").value);camera.x=cx-field.w/state.zoom/2;camera.y=cy-field.h/state.zoom/2;
    clampCamera();draw();saveSoon();});
  el("debug").addEventListener("change",()=>{state.debug=el("debug").checked;paintOverlay();saveSoon();});
  el("reset").addEventListener("click",()=>{state.edits="";particles=[];load(false);saveSoon();});
  el("bench").addEventListener("click",()=>benchmark().catch(()=>{}));
  overlay.addEventListener("contextmenu",e=>e.preventDefault());
  overlay.addEventListener("pointerdown",e=>{
    if(busy)return;e.preventDefault();overlay.setPointerCapture(e.pointerId);
    hover=world(e);pointers.set(e.pointerId,{x:e.clientX,y:e.clientY,pan:e.button===2,world:hover});
    if(pointers.size>1)for(const p of pointers.values())p.pan=true;
    if(e.button===0&&pointers.size===1&&e.pointerType!=="touch")breakAt(T.hit(field,hover.x,hover.y));
  });
  overlay.addEventListener("pointermove",e=>{
    const old=pointers.get(e.pointerId);hover=world(e);
    if(old&&(old.pan||pointers.size>1)) {
      const r=overlay.getBoundingClientRect();camera.x-=(e.clientX-old.x)/r.width*camera.w;
      camera.y-=(e.clientY-old.y)/r.height*camera.h;clampCamera();draw();saveSoon();
    }else if(old&&(e.buttons&1)) {
      const from=old.world??hover,steps=Math.max(1,Math.ceil(Math.hypot(hover.x-from.x,hover.y-from.y)/.4));
      let changed=false;
      for(let i=1;i<=steps;i++) {
        const x=from.x+(hover.x-from.x)*i/steps,y=from.y+(hover.y-from.y)*i/steps;
        changed=breakAt(T.hit(field,x,y),false)||changed;
      }
      if(changed){draw();refresh();saveSoon();}
    }
    if(old)pointers.set(e.pointerId,{...old,x:e.clientX,y:e.clientY,world:hover});paintOverlay();
  });
  for(const name of ["pointerup","pointercancel","lostpointercapture"])
    overlay.addEventListener(name,e=>{
      const p=pointers.get(e.pointerId);
      if(name==="pointerup"&&e.pointerType==="touch"&&p&&!p.pan&&pointers.size===1) {
        const at=world(e);breakAt(T.hit(field,at.x,at.y));
      }
      pointers.delete(e.pointerId);
    });
  overlay.addEventListener("pointerleave",()=>{hover=null;paintOverlay();});
  function resize() {
    if(canvas.width!==960||canvas.height!==600){canvas.width=overlay.width=960;canvas.height=overlay.height=600;}
    if(adapters&&!busy)draw();
  }
  new ResizeObserver(resize).observe(root);
  function retheme(){palette=theme();if(adapters&&!busy)draw();}
  matchMedia("(prefers-color-scheme: dark)").addEventListener("change",retheme);
  new MutationObserver(retheme).observe(document.documentElement,{attributes:true,attributeFilter:["class","style","data-theme"]});
  window.addEventListener("openai:set_globals",event=>{
    const next=event.detail?.globals?.widgetState?.privateContent;
    if(accepts(next)&&!busy){state={...state,...next};state.zoom=[1,2,4].includes(state.zoom)?state.zoom:1;
      state.debug=!!state.debug;el("size").value=String(state.size);
      el("zoom").value=String(state.zoom);el("debug").checked=state.debug;load(true);}
  });
  root._inspect=()=>({state:{...state,edits:undefined},count,camera,renderer:gpu.renderer,report,
    methods:Object.fromEntries(T.methods.map(m=>[m,{geometry:T.signature(adapters[m].masks),
      occupancy:T.signature(adapters[m].field.data),ports:T.checkPorts(adapters[m].masks,adapters[m].variants,field.w,field.h),
      variant:T.signature(adapters[m].variants),payload:T.payload(adapters[m]),last:adapters[m].last}]))});
  root._bench=benchmark;root._break=breakAt;
  root._target=()=>{const index=T.workload(field,1)[0];return {index,x:index%(field.w+1),y:(index/(field.w+1))|0};};
  root._pixels=()=>{const gl=gpu.gl,data=new Uint8Array(canvas.width*canvas.height*4);gl.readPixels(0,0,canvas.width,canvas.height,gl.RGBA,gl.UNSIGNED_BYTE,data);return [...data];};
  canvas.addEventListener("webglcontextlost",event=>{event.preventDefault();root.dataset.error="WebGL 上下文丢失";
    el("error").hidden=false;el("error").textContent="图形上下文丢失，当前测量无效；等待恢复。";});
  canvas.addEventListener("webglcontextrestored",()=>{
    try{gpu=T.gpu(canvas);for(const a of Object.values(adapters))gpu.upload(a);
      delete root.dataset.error;el("error").hidden=true;draw();}
    catch(error){el("error").textContent=error.message;}
  });
  try {gpu=T.gpu(canvas);resize();load(true);root.dataset.ready="true";}
  catch(error){root.dataset.error=error.message;el("error").hidden=false;el("error").textContent=error.message;}
})(globalThis.TerrainFour);
