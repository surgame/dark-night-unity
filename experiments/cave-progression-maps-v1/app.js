(function () {
  "use strict";
  const C=globalThis.CaveProgressionMaps,G=globalThis.CaveProgressionGeometry;
  const root=document.getElementById("cave-progression-maps"),q=id=>root.querySelector("#"+id);
  const defaults={advanced:"all",expert:"all",route:false,groups:false};
  let state={...defaults},resize=0;
  const cameras={},motions={};
  const esc=v=>String(v).replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&apos;"}[c]));
  function camera(m){
    const r=m.byId(state[m.id]);
    return r?{x:r.x-27,y:r.y-17,w:54,h:34}:m.extent;
  }
  function restore(snapshot){
    const s=snapshot?.privateContent;
    if(!s||s.kind!==C.kind)return;
    state={...defaults};
    C.maps.forEach(m=>{if(s[m.id]==="all"||m.byId(s[m.id]))state[m.id]=s[m.id];});
    for(const key of ["route","groups"])if(typeof s[key]==="boolean")state[key]=s[key];
  }
  restore(window.openai?.widgetState);
  function save(){
    window.openai?.setWidgetState?.({
      modelContent:{design:C.kind,...state,selections:C.maps.map(m=>({map:m.id,room:state[m.id],name:m.byId(state[m.id])?.name||"整图"}))},
      privateContent:{kind:C.kind,...state}
    }).catch(()=>{});
  }
  function render(m){
    const host=q(`cpm-${m.id}-map`),cam=cameras[m.id]||camera(m);
    const width=Math.max(260,Math.round(host.getBoundingClientRect().width));
    const height=Math.max(260,(width-24)*cam.h/cam.w+46);
    const scale=(width-24)/cam.w,tx=12-cam.x*scale,ty=24+(height-46-cam.h*scale)/2-cam.y*scale;
    const prefix="cpm-"+m.id,parts=G.air(m);
    const sky={polygon:[[-60,-30],[200,-30],...m.surface.slice().reverse()]};
    const air=parts.concat(sky).map(G.markup).join("");
    let out=`<defs>
      <clipPath id="${prefix}-clip"><rect x="0" y="23" width="${width}" height="${height-45}"/></clipPath>
      <mask id="${prefix}-rock" maskUnits="userSpaceOnUse" x="-50" y="-40" width="240" height="200"><rect x="-50" y="-40" width="240" height="200" fill="white"/><g fill="black">${air}</g></mask>
      </defs><rect class="rock" x="0" y="23" width="${width}" height="${height-45}"/>
      <g clip-path="url(#${prefix}-clip)"><g transform="translate(${tx},${ty}) scale(${scale})">
      <g class="air" data-air="${m.id}">${air}</g>
      <g mask="url(#${prefix}-rock)">${parts.map(p=>p.circle?`<circle class="edge" cx="${p.circle[0]}" cy="${p.circle[1]}" r="${p.circle[2]}"/>`:`<path class="edge" d="${G.rounded(p.polygon)}"/>`).join("")}
        <path class="edge" d="${G.rounded(m.surface,false)}"/></g>`;
    const selected=m.byId(state[m.id]);
    if(selected)out+=`<path class="selected" d="${G.rounded(selected.polygon)}"/>`;
    for(const r of m.rooms){
      const floor=r.y+r.h*.5,x=r.x-r.w*.13,y=floor+.45;
      out+=`<path class="ore" d="M${x-1},${y} l1,-1.2 l1,1.2 l-1,1.2 Z"/>`;
      if(r.main&&r.content==="ore")out+=`<path class="ore" d="M${x+1.8},${y+.9} l1.3,-1.5 l1.3,1.5 l-1.3,1.5 Z"/>`;
      if(r.content==="nest")out+=`<ellipse class="zone" cx="${r.x+r.w*.23}" cy="${r.y+r.h*.25}" rx="1.6" ry="1.1"/>`;
      if(r.content==="event")out+=`<rect class="zone" x="${r.x-r.w*.1}" y="${r.y-r.h*.25}" width="3" height="${r.h*.65}"/>`;
    }
    if(state.groups)out+=m.groups.map(g=>`<rect class="group" x="${g.box[0]}" y="${g.box[1]}" width="${g.box[2]}" height="${g.box[3]}"/>`).join("");
    if(state.route)out+=`<path data-route="${m.id}" class="route" d="M${m.route.map(p=>p.join(",")).join(" L")}"/>`;
    out+=m.rooms.map(r=>`<path data-map="${m.id}" data-room="${r.id}" class="cursor-interaction" d="${G.rounded(r.polygon)}" fill="transparent"/>`).join("");
    out+="</g></g>";
    const labels=[];
    const visible=m.rooms.filter(r=>r.x>cam.x&&r.x<cam.x+cam.w&&r.y>cam.y&&r.y<cam.y+cam.h);
    for(const r of visible){
      if(width<420&&state[m.id]==="all"&&!m.landmarks.includes(r.id)&&r.children.length<2)continue;
      const x=tx+r.x*scale,y=ty+r.y*scale;
      const detail=state[m.id]!=="all"&&width>=420;
      const text=detail?`${r.id} ${r.name}`:r.id;
      const half=Math.min(width/2-8,text.length*(detail?6:4)+3);
      let label={text,x:Math.max(half+5,Math.min(width-half-5,x)),y:y+4,half};
      for(let attempt=0;attempt<5&&labels.some(l=>Math.abs(l.y-label.y)<15&&Math.abs(l.x-label.x)<l.half+label.half+5);attempt++){
        label.y+=attempt%2?-30:15;
      }
      label.y=Math.max(39,Math.min(height-29,label.y));
      labels.push(label);
      if(m.landmarks.includes(r.id)&&state[m.id]==="all")out+=`<circle class="landmark" cx="${label.x}" cy="${label.y-4}" r="12"/>`;
      out+=`<text class="cursor-interaction" data-map="${m.id}" data-room="${r.id}" text-anchor="middle" x="${label.x}" y="${label.y}">${esc(text)}</text>`;
    }
    const ex=tx+m.entrance[0]*scale,ey=ty-2.7*scale;
    if(m.entrance[0]>cam.x&&m.entrance[0]<cam.x+cam.w&&cam.y<0){
      out+=`<text text-anchor="middle" x="${Math.max(33,Math.min(width-33,ex))}" y="${Math.max(39,ey)}">地面入口</text>`;
    }
    out+=`<text x="8" y="16">${state[m.id]==="all"?"全图 · H为角色高度":"局部 · "+esc(selected?.name||"")}</text>`;
    const unit=state[m.id]==="all"?10:5,bar=unit*scale;
    out+=`<path class="edge" d="M${width-bar-12},${height-10} h${bar}"/><text text-anchor="end" x="${width-12}" y="${height-17}">${unit}H</text>`;
    host.innerHTML=`<svg width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" role="img" aria-label="${esc(m.name)}"><title>${esc(m.name)}</title><desc>${m.stats.rooms}处洞室，${m.stats.forks}处作者分岔，原路返回。</desc>${out}</svg>`;
    q(`cpm-${m.id}-room`).value=state[m.id];
    q(`cpm-${m.id}-detail`).textContent=selected?`${selected.id} ${selected.name}：${selected.purpose}`:
      m.id==="advanced"?"浅岔、侧巢与左翼支洞都可单独探索；部分支路会再转一段。":"浅岔与左翼支洞内部继续分岔；相近高度的洞室可能隔岩相邻。";
    root.dataset.ready="true";
  }
  function moveCamera(m){
    cancelAnimationFrame(motions[m.id]);
    const from=cameras[m.id]||m.extent,to=camera(m),start=performance.now();
    if(matchMedia("(prefers-reduced-motion: reduce)").matches){cameras[m.id]={...to};motions[m.id]=0;render(m);return;}
    function tick(now){
      const t=Math.min(1,(now-start)/240),s=t*t*(3-2*t);
      cameras[m.id]=Object.fromEntries(["x","y","w","h"].map(k=>[k,from[k]+(to[k]-from[k])*s]));
      motions[m.id]=t<1?requestAnimationFrame(tick):0;render(m);
    }
    motions[m.id]=requestAnimationFrame(tick);
  }
  C.maps.forEach(m=>{
    cameras[m.id]={...camera(m)};
    const select=q(`cpm-${m.id}-room`);
    select.innerHTML=`<option value="all">整图</option>`+m.rooms.map(r=>`<option value="${r.id}">${r.id} ${r.name}</option>`).join("");
    select.addEventListener("change",e=>{state[m.id]=e.target.value;moveCamera(m);save();});
    const s=m.stats;
    q(`cpm-${m.id}-stats`).textContent=`${s.rooms}处洞室 · ${s.forks}处分岔 · 支洞最长${s.sideDepth}段 · ${s.layers}个层带`;
    q(`cpm-${m.id}-map`).addEventListener("click",e=>{
      const hit=e.target.closest("[data-room]");
      if(hit){state[m.id]=hit.dataset.room;moveCamera(m);save();}
    });
    render(m);
  });
  for(const key of ["route","groups"]){
    q("cpm-"+key).checked=state[key];
    q("cpm-"+key).addEventListener("change",e=>{state[key]=e.target.checked;C.maps.forEach(render);save();});
  }
  const observer=new ResizeObserver(()=>{
    cancelAnimationFrame(resize);resize=requestAnimationFrame(()=>C.maps.forEach(render));
  });
  C.maps.forEach(m=>observer.observe(q(`cpm-${m.id}-map`)));
  const onGlobals=e=>{
    if(!e.detail?.globals?.widgetState)return;
    restore(e.detail.globals.widgetState);
    C.maps.forEach(m=>{cancelAnimationFrame(motions[m.id]);motions[m.id]=0;cameras[m.id]={...camera(m)};render(m);});
    for(const key of ["route","groups"])q("cpm-"+key).checked=state[key];
  };
  window.addEventListener("openai:set_globals",onGlobals);
  window.addEventListener("pagehide",()=>{
    observer.disconnect();cancelAnimationFrame(resize);
    Object.values(motions).forEach(cancelAnimationFrame);
    window.removeEventListener("openai:set_globals",onGlobals);
  },{once:true});
  root._inspect=()=>({
    state:{...state},cameras:JSON.parse(JSON.stringify(cameras)),moving:Object.values(motions).some(Boolean),
    maps:C.maps.map(m=>({id:m.id,stats:m.stats,extent:m.extent}))
  });
}());
