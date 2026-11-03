(function () {
  "use strict";
  const Q=globalThis.CaveDepthDesign,root=document.getElementById("cave-surface-depth");
  const q=id=>root.querySelector("#"+id),map=q("csd-map");
  const defaults={view:"all",module:"M01",bounds:false,content:true};
  let state={...defaults},resizeFrame=0;
  const esc=v=>String(v).replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&apos;"}[c]));
  q("csd-module").innerHTML=Q.modules.map(m=>`<option value="${m.id}">${m.id} ${m.name}</option>`).join("");
  function restore(snapshot) {
    const v=snapshot?.privateContent;
    if(!v || v.kind!==Q.kind)return;
    state={...defaults,...Object.fromEntries(Object.keys(defaults).filter(k=>typeof v[k]===typeof defaults[k]).map(k=>[k,v[k]]))};
    if(!["all","surface","shallow","middle","deep","single"].includes(state.view))state.view=defaults.view;
    state.module=Q.byId(state.module).id;
  }
  restore(window.openai?.widgetState);
  function save() {
    window.openai?.setWidgetState?.({
      modelContent:{design:Q.kind,...state,name:Q.byId(state.module).name},
      privateContent:{kind:Q.kind,...state}
    }).catch(()=>{});
  }
  function line(points,close=false) {
    let result=`M${points[0][0]},${points[0][1]}`;
    for(let i=1;i<points.length-1;i++){
      const a=points[i-1],b=points[i],c=points[i+1];
      const ab=Math.hypot(b[0]-a[0],b[1]-a[1])||1,bc=Math.hypot(c[0]-b[0],c[1]-b[1])||1;
      const cut=Math.min(.2+(i%3)*.13,ab*.18,bc*.18);
      result+=` L${b[0]-(b[0]-a[0])*cut/ab},${b[1]-(b[1]-a[1])*cut/ab} Q${b[0]},${b[1]} ${b[0]+(c[0]-b[0])*cut/bc},${b[1]+(c[1]-b[1])*cut/bc}`;
    }
    const end=points.at(-1);
    return result+` L${end[0]},${end[1]}`+(close?" Z":"");
  }
  const cavity=s=>line([...s.roof,...s.floor.slice().reverse()],true);
  function shapeBounds(s) {
    const pts=[...s.roof,...s.floor],xs=pts.map(p=>p[0]),ys=pts.map(p=>p[1]);
    const x=Math.min(...xs),y=Math.min(...ys);
    return {x,y,w:Math.max(...xs)-x,h:Math.max(...ys)-y};
  }
  function nodeShape(n) {return Q.world(n,Q.shape(Q.byId(n.id)));}
  function selectedShape(){
    const node=Q.nodes.find(n=>n.id===state.module);
    return node?nodeShape(node):Q.linkShape(Q.links.find(l=>l.id===state.module));
  }
  function camera() {
    if(state.view!=="single")return Q.cameras[state.view];
    const box=shapeBounds(selectedShape());
    return {x:box.x-8,y:box.y-10,w:Math.max(45,box.w+16),h:Math.max(32,box.h+20)};
  }
  function hero(x,y) {
    return `<circle class="edge" cx="${x}" cy="${y-.82}" r=".18"/><path class="edge" d="M${x-.23},${y-.6} h.46 v.35 h-.46 Z M${x-.13},${y-.25} V${y} M${x+.13},${y-.25} V${y}"/>`;
  }
  function contents(n) {
    if(!state.content)return "";
    return Q.contents(Q.byId(n.id)).map(c=>{
      const [x,y]=Q.point(n,[c.x,c.y]);
      if(c.type==="ore")return `<path class="ore" d="M${x-c.r},${y} l${c.r},${-c.r} l${c.r},${c.r} l${-c.r},${c.r} Z"/>`;
      if(c.type==="nest")return `<ellipse class="zone" cx="${x}" cy="${y}" rx="${c.r}" ry="${c.r*.6}"/>`;
      if(c.w)return `<rect class="zone" x="${n.flip?x-c.w:x}" y="${y}" width="${c.w}" height="${c.h}"/>`;
      return "";
    }).join("");
  }
  function render() {
    q("csd-view").value=state.view;q("csd-module").value=state.module;
    q("csd-bounds").checked=state.bounds;q("csd-content").checked=state.content;
    const width=Math.max(260,Math.round(map.getBoundingClientRect().width)),cam=camera();
    const height=Math.max(state.view==="all"?360:260,Math.min(820,(width-42)*cam.h/cam.w+65));
    const left=state.view==="all"?40:12,scale=Math.min((width-left-12)/cam.w,(height-56)/cam.h);
    const tx=left+(width-left-12-cam.w*scale)/2-cam.x*scale,ty=29+(height-56-cam.h*scale)/2-cam.y*scale;
    const air=[],edges=[],details=[],hits=[],labels=[];
    const surfaceSky=[[-100,-100],[350,-100],...Q.surface.slice().reverse()];
    air.push(line(surfaceSky,true));edges.push(line(Q.surface));
    for(const n of Q.nodes){
      const m=Q.byId(n.id),s=nodeShape(n),branch=Q.branch(m);
      air.push(cavity(s));edges.push(line(s.roof),line(s.floor));
      if(branch){const b=Q.world(n,branch);air.push(cavity(b));edges.push(line(b.roof),line(b.floor));}
      details.push(contents(n));
      hits.push(`<path class="cursor-interaction" data-module="${m.id}" d="${cavity(s)}" fill="transparent"/>`);
      const bounds=shapeBounds(s);
      if(state.bounds){
        details.push(`<rect class="guide" x="${bounds.x-1}" y="${bounds.y-2}" width="${bounds.w+2}" height="${bounds.h+4}" stroke-dasharray="3 3"/>`);
        const entry=Q.port(m.id,"entry"),point=Q.point(n,[0,Q.shape(m).roof[0][1]]);
        details.push(`<path class="port" d="M${entry[0]},${entry[1]} L${point[0]},${point[1]}"/>`);
      }
      labels.push({id:m.id,name:m.name,x:bounds.x+bounds.w*.5,y:bounds.y-3});
    }
    for(const l of Q.links){
      const s=Q.linkShape(l),d=cavity(s);
      air.push(d);edges.push(line(s.roof),line(s.floor));
      if(l.id){
        const box=shapeBounds(s);
        hits.push(`<path class="cursor-interaction" data-module="${l.id}" d="${d}" fill="transparent"/>`);
        if(state.bounds)details.push(`<rect class="guide" x="${box.x-1}" y="${box.y-1}" width="${box.w+2}" height="${box.h+2}" stroke-dasharray="3 3"/>`);
        labels.push(l.id==="D01"?{id:l.id,name:Q.byId(l.id).name,x:201,y:68}:{id:l.id,name:Q.byId(l.id).name,x:31,y:123});
      }
    }
    details.push(hero(24,Q.surfaceY(24)));
    const allAir=air.map(d=>`<path d="${d}"/>`).join("");
    const focus=state.bounds?shapeBounds(selectedShape()):null;
    let body=`<defs>
      <clipPath id="csd-camera"><rect x="${left}" y="25" width="${width-left}" height="${height-47}"/></clipPath>
      <mask id="csd-rock" maskUnits="userSpaceOnUse" x="-80" y="-80" width="420" height="360">
        <rect x="-80" y="-80" width="420" height="360" fill="white"/><g fill="black">${allAir}</g>
      </mask>
      </defs><rect class="rock" x="${left}" y="25" width="${width-left}" height="${height-47}"/>`;
    if(state.view==="all"){
      for(const depth of [0,50,100,150]){
        const y=ty+depth*scale;
        body+=`<path class="guide" d="M${left},${y} H${width-4}"/><text x="${left-5}" y="${y+4}" text-anchor="end">${depth}H</text>`;
      }
    }
    body+=`<g clip-path="url(#csd-camera)"><g transform="translate(${tx},${ty}) scale(${scale})">
      ${focus?`<rect class="focus" x="${focus.x-1}" y="${focus.y-2}" width="${focus.w+2}" height="${focus.h+4}"/>`:""}
      <g class="air">${allAir}</g>
      <g mask="url(#csd-rock)">${edges.map(d=>`<path class="edge" d="${d}"/>`).join("")}</g>
      ${details.join("")}${hits.join("")}
    </g></g>`;
    const full=width>=520;
    for(const label of labels){
      if(label.x<cam.x || label.x>cam.x+cam.w || label.y<cam.y || label.y>cam.y+cam.h)continue;
      const text=full?`${label.id} ${label.name}`:label.id,half=text.length*(full?6:4)+3;
      const x=Math.max(left+half,Math.min(width-half,tx+label.x*scale)),y=Math.max(40,Math.min(height-30,ty+label.y*scale));
      body+=`<text class="cursor-interaction" data-module="${label.id}" x="${x}" y="${y}" text-anchor="middle">${esc(text)}</text>`;
    }
    if((state.view==="all" && full) || state.view==="surface" || state.view==="shallow"){
      const x=tx+26*scale,y=ty-14*scale;
      if(x>=left && x<width-15 && y>=27)body+=`<text x="${Math.min(width-70,x)}" y="${y}">地表出发</text>`;
    }
    const titles={all:"地表 → 浅层 → 中层 → 深层",surface:"地面洞口：从露天进入岩层",shallow:"浅层：试采与岔路",middle:"中层：回折下行与错层洞厅",deep:"深层：继续下探与富矿折返",single:`${state.module} ${Q.byId(state.module).name}`};
    body+=`<text x="8" y="17">${esc(titles[state.view])}</text>
      <text x="${width-8}" y="${height-6}" text-anchor="end">正侧视 · 分层下探草图</text>`;
    map.innerHTML=`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" role="img" aria-label="${esc(titles[state.view])}"><title>${esc(titles[state.view])}</title>${body}</svg>`;
    q("csd-selected").textContent=`${state.module} ${Q.byId(state.module).name}`;
    q("csd-detail").textContent=state.view==="single"?(Q.byId(state.module).goal||"独立跨层空间候选"):
      "洞口位于地表；两段回折坡道串起浅、中、深空间，原路向上返回。";
    root.dataset.ready="true";
  }
  for(const [id,key] of [["csd-view","view"],["csd-module","module"],["csd-bounds","bounds"],["csd-content","content"]]){
    q(id).addEventListener("change",e=>{
      state[key]=e.target.type==="checkbox"?e.target.checked:e.target.value;
      if(key==="module")state.view=state.module==="M01"?"surface":"single";
      render();save();
    });
  }
  map.addEventListener("click",e=>{
    const target=e.target.closest("[data-module]");
    if(target){state.module=target.dataset.module;render();save();}
  });
  const observer=new ResizeObserver(()=>{cancelAnimationFrame(resizeFrame);resizeFrame=requestAnimationFrame(render);});
  observer.observe(map);
  const onGlobals=e=>{if(e.detail?.globals?.widgetState){restore(e.detail.globals.widgetState);render();}};
  window.addEventListener("openai:set_globals",onGlobals);
  window.addEventListener("pagehide",()=>{
    observer.disconnect();cancelAnimationFrame(resizeFrame);window.removeEventListener("openai:set_globals",onGlobals);
  },{once:true});
  root._inspect=()=>({state:{...state},camera:camera(),nodes:Q.nodes,links:Q.links,surface:Q.surface,
    entrance:Q.port("M01","entry"),moduleCount:Q.modules.length});
  render();
}());
