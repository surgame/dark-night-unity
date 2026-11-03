(async function(){
  "use strict";
  const root=document.getElementById("terrain-performance-study"),M=TerrainBench,names={baked:"烘焙分块",strip:"边缘变形网格",atlas:"Dual Grid 图集"};
  let map=document.getElementById("tb-map");
  const overlay=document.getElementById("tb-overlay"),ctx=overlay.getContext("2d");
  const view={x:16,y:82,w:48,h:32},state={mode:"strip",hits:[],zones:false},saved=window.openai?.widgetState?.privateContent;
  let art,renderer,a,dirty=[],last=null,busy=false,measurement=window.TERRAIN_MEASURED||null;
  const all=Array.from({length:60},(_,i)=>i),sleep=()=>new Promise(r=>setTimeout(r,0));
  function restore(s){
    if(!s||s.version!==1)return;
    if(names[s.mode])state.mode=s.mode;
    state.hits=Array.isArray(s.hits)?s.hits.slice(0,60).filter(h=>Number.isFinite(h.x)&&Number.isFinite(h.y)&&h.x>=16&&h.x<=64&&h.y>=82&&h.y<=114&&h.r>=.5&&h.r<=3):[];
    state.zones=!!s.zones;
  }
  restore(saved);
  function save(){
    window.openai?.setWidgetState?.({modelContent:{view:"terrain-performance",method:state.mode,destructions:state.hits.length},
      privateContent:{version:1,...state}})?.catch(()=>{});
  }
  function result(){
    const body=document.getElementById("tb-results");body.replaceChildren();
    const source=document.getElementById("tb-runtime").value==="unity"?window.TERRAIN_UNITY:measurement;
    if(!source)return;
    for(const row of source.rows){
      const tr=document.createElement("tr");
      const ms=n=>Number(n).toFixed(2),v=[names[row.mode],ms(row.initial.median),ms((row.static||row.stable).median),ms(row.single.median),ms(row.blast.median),((row.textureBytes??row.resources.textureBytes)/1048576).toFixed(2)];
      v.forEach((text,i)=>{const td=document.createElement("td");td.textContent=text;if(i)td.className="text-end";tr.append(td);});body.append(tr);
    }
    document.getElementById("tb-method-note").textContent=source.sourceLabel||"本机浏览器 WebGL；完成计时含提交、GPU／驱动等待和读回，不是帧率。只测地形，不含光照、碰撞、生态或联网。";
  }
  function draw(){
    const stats=renderer.draw(view);ctx.clearRect(0,0,960,640);ctx.save();ctx.scale(20,20);ctx.translate(-view.x,-view.y);
    if(state.zones){
      ctx.fillStyle="rgba(226,158,67,.13)";ctx.strokeStyle="rgba(255,200,116,.8)";ctx.lineWidth=.045;
      for(const k of dirty){const b=M.bounds(k);ctx.fillRect(b.x,b.y,32,32);ctx.strokeRect(b.x,b.y,32,32);}
    }
    const hit=state.hits.at(-1);if(hit){ctx.strokeStyle="#d4c07a";ctx.lineWidth=.05;ctx.beginPath();ctx.arc(hit.x,hit.y,hit.r+.25,0,Math.PI*2);ctx.stroke();}
    // The original character is a scale reference; it does not simulate movement.
    let floor=96;while(floor<120&&!a[floor*321+21])floor++;
    ctx.drawImage(art.hero,20,floor-.5-3,3*art.hero.width/art.hero.height,3);
    ctx.restore();
    const suffix=last?`本次 ${last.changed} 角点 · ${last.chunks} 区块 · 上传 ${(last.uploadBytes/1024).toFixed(1)} KiB`:"320 × 192 格 · 同一镜头、同一形状";
    document.getElementById("tb-detail").textContent=`${names[state.mode]} · ${stats.draws} 次绘制 / ${stats.vertices} 顶点 · ${suffix}`;
    for(const b of root.querySelectorAll("[data-mode]"))b.setAttribute("aria-pressed",b.dataset.mode===state.mode?"true":"false");
    document.getElementById("tb-zones").checked=state.zones;
  }
  function build(){
    renderer?.dispose();const fresh=map.cloneNode();map.replaceWith(fresh);map=fresh;
    // Contexts are released on mode switches; no hidden three-way residency.
    renderer=new TerrainRenderer(fresh,art,state.mode);a=M.create();dirty=[];
    for(const h of state.hits)dirty=M.damage(a,h.x,h.y,h.r).dirty;
    renderer.rebuild(a,all);last=null;draw();
  }
  function hit(radius){
    const i=state.hits.length;
    const h={x:radius<1?25+i%12:27+(i%5)*3,y:radius<1?108+Math.floor(i/12):108,r:radius};
    if(i>=60)return;
    state.hits.push(h);const d=M.damage(a,h.x,h.y,h.r);dirty=d.dirty;last={...renderer.rebuild(a,dirty),changed:d.changed.length};
    draw();save();
  }
  try{
    art=await TerrainArt.load(window.TERRAIN_IMAGES);build();
    document.getElementById("tb-runtime").value=window.TERRAIN_UNITY?"unity":"browser";result();
    for(const button of root.querySelectorAll("[data-mode]"))button.onclick=()=>{if(busy)return;state.mode=button.dataset.mode;build();save();};
    document.getElementById("tb-hit").onclick=()=>hit(.75);
    document.getElementById("tb-blast").onclick=()=>hit(2.2);
    document.getElementById("tb-reset").onclick=()=>{state.hits=[];build();save();};
    document.getElementById("tb-zones").onchange=e=>{state.zones=e.target.checked;draw();save();};
    document.getElementById("tb-measure").onclick=async()=>{
      if(busy)return;busy=true;root.querySelectorAll("button").forEach(b=>b.disabled=true);
      try{measurement=await TerrainMeasure.measure(art,(mode,round)=>{document.getElementById("tb-progress").textContent=`${names[mode]} · 第 ${round}/3 轮`;});
        document.getElementById("tb-runtime").value="browser";result();document.getElementById("tb-progress").textContent="已完成本机测量";
      }catch(e){document.getElementById("tb-error").hidden=false;document.getElementById("tb-error").textContent=e.message;}
      finally{busy=false;root.querySelectorAll("button").forEach(b=>b.disabled=false);draw();}
    };
    document.getElementById("tb-runtime").onchange=result;
    window.addEventListener("openai:set_globals",e=>{
      if(!e.detail?.globals?.widgetState)return;
      const before=JSON.stringify(state);restore(e.detail.globals.widgetState.privateContent);
      if(JSON.stringify(state)!==before)build();
    });
    root._inspect=()=>({state:structuredClone(state),hash:M.hash(a),view,stats:renderer.draw(view),last,dirty,measurement});
    root._tools={art,measure:TerrainMeasure.measure,unityInput:()=>TerrainMeasure.unityInput(art),draw};
    root.dataset.ready="true";await sleep();
  }catch(e){root.dataset.error=e.stack;document.getElementById("tb-error").hidden=false;document.getElementById("tb-error").textContent=e.message;}
})();
