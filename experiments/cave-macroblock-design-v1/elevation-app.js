(function () {
  "use strict";
  const P=globalThis.CaveBlockDesign,root=document.getElementById("cave-macroblock-elevation");
  const q=id=>root.querySelector("#"+id),map=q("cbe-map");
  const defaults={view:"middle",module:"M04",elevation:true,bounds:false,content:true};
  let state={...defaults},resizeFrame=0,motion=0;
  const esc=v=>String(v).replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&apos;"}[c]));
  q("cbe-module").innerHTML=P.modules.map(m=>`<option value="${m.id}">${m.id} ${m.name}</option>`).join("");
  function restore(snapshot) {
    const v=snapshot?.privateContent;
    if(!v || v.kind!==P.kind)return;
    state={...defaults,...Object.fromEntries(Object.keys(defaults).filter(k=>typeof v[k]===typeof defaults[k]).map(k=>[k,v[k]]))};
    if(!["middle","all","shallow","deep","module"].includes(state.view))state.view=defaults.view;
    state.module=P.byId(state.module).id;
    P.amount=state.elevation?1:0;
  }
  restore(window.openai?.widgetState);
  function save() {
    window.openai?.setWidgetState?.({
      modelContent:{design:P.kind,...state,name:P.byId(state.module).name},
      privateContent:{kind:P.kind,...state}
    }).catch(()=>{});
  }
  function line(points,close=false) {
    let result=`M${points[0][0]},${points[0][1]}`;
    for(let i=1;i<points.length-1;i++){
      const a=points[i-1],b=points[i],c=points[i+1],ab=Math.hypot(b[0]-a[0],b[1]-a[1])||1,bc=Math.hypot(c[0]-b[0],c[1]-b[1])||1;
      const cut=Math.min(.2+(i%3)*.13,ab*.22,bc*.22);
      result+=` L${b[0]-(b[0]-a[0])*cut/ab},${b[1]-(b[1]-a[1])*cut/ab} Q${b[0]},${b[1]} ${b[0]+(c[0]-b[0])*cut/bc},${b[1]+(c[1]-b[1])*cut/bc}`;
    }
    const end=points.at(-1);
    return result+` L${end[0]},${end[1]}`+(close?" Z":"");
  }
  const cavity=s=>line([...s.roof,...s.floor.slice().reverse()],true);
  const shift=(line,n)=>line.map(([x,y])=>[x+n.x,y+n.y]);
  const worldShape=(s,n)=>({roof:shift(s.roof,n),floor:shift(s.floor,n)});
  function hero(x,y) {
    return `<circle class="edge" cx="${x}" cy="${y-.82}" r=".18"/><path class="edge" d="M${x-.23},${y-.6} h.46 v.35 h-.46 Z M${x-.13},${y-.25} V${y} M${x+.13},${y-.25} V${y}"/>`;
  }
  function contents(m,n) {
    if(!state.content)return "";
    return P.contents(m).map(c=>{
      const x=n.x+c.x,y=n.y+c.y;
      if(c.type==="ore")return `<path class="ore" d="M${x-c.r},${y} l${c.r},${-c.r} l${c.r},${c.r} l${-c.r},${c.r} Z"/>`;
      if(c.type==="nest")return `<ellipse class="zone" cx="${x}" cy="${y}" rx="${c.r}" ry="${c.r*.6}"/>`;
      if(c.w)return `<rect class="zone" x="${x}" y="${y}" width="${c.w}" height="${c.h}"/>`;
      return `<path class="zone" d="M${x-1},${y} q-1,-3 0,-3 q2,0 1,3 M${x+1},${y} q0,-4 1,-4 q2,0 1,4"/>`;
    }).join("");
  }
  function fixedCamera() {
    if(state.view==="module") {
      const m=P.byId(state.module),prev=P.amount;
      P.amount=0;const a=P.localBounds(m);
      P.amount=1;const b=P.localBounds(m);P.amount=prev;
      const top=Math.min(a.y,b.y),bottom=Math.max(a.y+a.h,b.y+b.h);
      return {x:-2,y:top-2,w:m.w+4,h:bottom-top+5};
    }
    return {
      all:{x:-7,y:-12,w:380,h:90},
      shallow:{x:-7,y:-4,w:122,h:48},
      middle:{x:100,y:-8,w:162,h:70},
      deep:{x:247,y:-8,w:125,h:90}
    }[state.view];
  }
  function scene() {
    if(state.view==="module")return {nodes:[{id:state.module,x:0,y:0,main:true}],edges:[]};
    return P.layouts();
  }
  function render() {
    q("cbe-view").value=state.view;q("cbe-module").value=state.module;
    q("cbe-elevation").checked=state.elevation;q("cbe-bounds").checked=state.bounds;q("cbe-content").checked=state.content;
    const width=Math.max(260,Math.round(map.getBoundingClientRect().width));
    const height=state.view==="all"?Math.max(235,width*.34):Math.max(280,Math.min(390,width*.56));
    const camera=fixedCamera(),scale=Math.min((width-24)/camera.w,(height-60)/camera.h);
    const tx=(width-camera.w*scale)/2-camera.x*scale,ty=34+(height-60-camera.h*scale)/2-camera.y*scale;
    const current=scene(),air=[],edges=[],details=[];
    for(const n of current.nodes){
      const m=P.byId(n.id),s=worldShape(P.shape(m),n),b=P.branch(m);
      air.push(cavity(s));edges.push(line(s.roof),line(s.floor));
      if(b){const branch=worldShape(b,n);air.push(cavity(branch));edges.push(line(branch.roof),line(branch.floor));}
      details.push(contents(m,n));
      if(state.bounds){
        const box=P.localBounds(m);
        details.push(`<rect class="guide" x="${n.x}" y="${n.y+box.y}" width="${box.w}" height="${box.h}" stroke-dasharray="3 3"/>`);
        details.push(`<path class="port" d="M${s.roof[0][0]},${s.roof[0][1]} V${s.floor[0][1]}"/>`);
        if(m.ports>1)details.push(`<path class="port" d="M${s.roof.at(-1)[0]},${s.roof.at(-1)[1]} V${s.floor.at(-1)[1]}"/>`);
      }
    }
    for(const e of current.edges){
      const a=current.nodes.find(n=>n.id===e.from),b=current.nodes.find(n=>n.id===e.to);
      const s=P.connection(a,b,e.port);
      air.push(cavity(s));edges.push(line(s.roof),line(s.floor));
    }
    const heroNode=current.nodes.find(n=>n.id===state.module) || current.nodes.find(n=>n.main);
    if(heroNode){
      const m=P.byId(heroNode.id),x=m.w*.18;
      details.push(hero(heroNode.x+x,heroNode.y+P.floorY(m,x)));
    }
    const allAir=air.map(d=>`<path d="${d}"/>`).join("");
    const selected=current.nodes.find(n=>n.id===state.module),m=P.byId(state.module);
    let highlight="";
    if(selected && state.bounds){
      const box=P.localBounds(m);
      highlight=`<rect class="focus" x="${selected.x}" y="${selected.y+box.y}" width="${box.w}" height="${box.h}"/>`;
    }
    const clip="cbe-camera",mask="cbe-rock-mask";
    let body=`<defs>
      <clipPath id="${clip}"><rect x="0" y="27" width="${width}" height="${height-51}"/></clipPath>
      <mask id="${mask}" maskUnits="userSpaceOnUse" x="-100" y="-100" width="650" height="400">
        <rect x="-100" y="-100" width="650" height="400" fill="white"/><g fill="black">${allAir}</g>
      </mask>
      </defs><rect class="rock" x="0" y="27" width="${width}" height="${height-51}"/>
      <g clip-path="url(#${clip})"><g transform="translate(${tx},${ty}) scale(${scale})">
        ${highlight}<g class="air">${allAir}</g>
        <g mask="url(#${mask})">${edges.map(d=>`<path class="edge" d="${d}"/>`).join("")}</g>
        ${details.join("")}
      </g></g>`;
    // Labels stay in screen coordinates while both height layouts share a camera.
    for(const n of current.nodes){
      const item=P.byId(n.id),box=P.localBounds(item),cx=n.x+item.w*.5;
      if(cx<camera.x || cx>camera.x+camera.w)continue;
      const x=tx+cx*scale,y=Math.max(43,Math.min(height-33,ty+(n.y+box.y)*scale-7));
      const label=state.view==="module"?"":width<520 || state.view==="all"?item.id:`${item.id} ${item.name}`;
      if(label)body+=`<text x="${Math.max(24,Math.min(width-24,x))}" y="${y}" text-anchor="middle">${esc(label)}</text>`;
    }
    const title={middle:"中段：岔洞下行 → 分层岩台 → 咽喉回升",all:"主干升降＋上折侧支 · 8块主干／2块侧支",shallow:"浅层：缓入 → 试采 → 沿岩层下行",deep:"深部：回升后继续深入 → 矿厅折返",module:`${m.id} ${m.name}`}[state.view];
    const compact={middle:"中段：下行岩台／咽喉回升",all:"整图：主干升降／上折侧支",shallow:"浅层：缓入／试采／长坡",deep:"深部：继续深入／矿厅折返",module:`${m.id} ${m.name}`}[state.view];
    const s=P.shape(m),dy=s.floor.at(-1)[1]-s.floor[0][1];
    const footer=state.view==="module"?`局部两端：${dy>0?"下降":"抬升"} ${Math.abs(dy).toFixed(1)}H`:"原路折返结构 · 高差为设计候选";
    body+=`<text x="8" y="17">${esc(width<400?compact:title)}</text>
      <text x="${width-8}" y="${height-7}" text-anchor="end">${esc(footer)}</text>`;
    map.innerHTML=`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" role="img" aria-label="${esc(title)}"><title>${esc(title)}</title>${body}</svg>`;
    q("cbe-selected").textContent=`${m.id} ${m.name} · ${state.elevation?"高低错落":"原横向高度"}`;
    q("cbe-detail").textContent=state.view==="module"?m.goal:
      "长坡承担整体高差，岩台提供停顿；上折支线与主干分层，少量大落差仍留给特殊点。";
    root.dataset.ready="true";
    root.dataset.moving=motion?"true":"false";
  }
  function animateElevation(){
    cancelAnimationFrame(motion);
    const start=P.amount,end=state.elevation?1:0,time=performance.now();
    if(matchMedia("(prefers-reduced-motion: reduce)").matches){P.amount=end;motion=0;render();return;}
    function tick(now){
      const t=Math.min(1,(now-time)/260),ease=t*t*(3-2*t);
      P.amount=start+(end-start)*ease;
      motion=t<1?requestAnimationFrame(tick):0;render();
    }
    motion=requestAnimationFrame(tick);
  }
  for(const [id,key] of [["cbe-view","view"],["cbe-module","module"],["cbe-elevation","elevation"],["cbe-bounds","bounds"],["cbe-content","content"]]){
    q(id).addEventListener("change",e=>{
      state[key]=e.target.type==="checkbox"?e.target.checked:e.target.value;
      if(key==="module")state.view="module";
      if(key==="elevation")animateElevation();else render();
      save();
    });
  }
  const observer=new ResizeObserver(()=>{
    cancelAnimationFrame(resizeFrame);resizeFrame=requestAnimationFrame(render);
  });
  observer.observe(map);
  const onGlobals=e=>{
    if(e.detail?.globals?.widgetState){cancelAnimationFrame(motion);motion=0;restore(e.detail.globals.widgetState);render();}
  };
  window.addEventListener("openai:set_globals",onGlobals);
  window.addEventListener("pagehide",()=>{
    observer.disconnect();cancelAnimationFrame(motion);cancelAnimationFrame(resizeFrame);
    window.removeEventListener("openai:set_globals",onGlobals);
  },{once:true});
  root._inspect=()=>({state:{...state},amount:P.amount,camera:fixedCamera(),layout:P.layouts(),
    shape:P.shape(P.byId(state.module)),moving:Boolean(motion)});
  render();
}());
