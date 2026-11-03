(function () {
  "use strict";
  const C=globalThis.CompactNaturalMine,root=document.getElementById("compact-natural-mine");
  const q=id=>root.querySelector("#"+id),map=q("cnm-map");
  const defaults={goal:"C",view:"all",selected:"09",route:true,bounds:false,content:true};
  let state={...defaults},motion=0,resize=0,shown=C.goals[2].length;
  const esc=v=>String(v).replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&apos;"}[c]));
  const goal=()=>C.goals.find(g=>g.id===state.goal)||C.goals[0];
  q("cnm-goal").innerHTML=C.goals.map(g=>`<option value="${g.id}">${g.id} ${g.name} · ${g.ratio.toFixed(2)}L</option>`).join("");
  function restore(snapshot){
    const v=snapshot?.privateContent;
    if(!v || v.kind!==C.kind)return;
    state={...defaults,...Object.fromEntries(Object.keys(defaults).filter(k=>typeof v[k]===typeof defaults[k]).map(k=>[k,v[k]]))};
    if(!C.goals.some(g=>g.id===state.goal))state.goal="C";
    if(!C.cameras[state.view])state.view="all";
    state.selected=C.byId(state.selected).id;shown=goal().length;
  }
  restore(window.openai?.widgetState);
  function save(){
    window.openai?.setWidgetState?.({
      modelContent:{design:C.kind,...state,target:goal().name,ratio:goal().ratio},
      privateContent:{kind:C.kind,...state}
    }).catch(()=>{});
  }
  function rounded(points,close=true){
    let out=`M${points[0][0]},${points[0][1]}`;
    for(let i=1;i<points.length-1;i++){
      const a=points[i-1],b=points[i],c=points[i+1],ab=Math.hypot(b[0]-a[0],b[1]-a[1])||1,bc=Math.hypot(c[0]-b[0],c[1]-b[1])||1;
      const cut=Math.min(.12+(i%4)*.08,ab*.18,bc*.18);
      out+=` L${b[0]-(b[0]-a[0])*cut/ab},${b[1]-(b[1]-a[1])*cut/ab} Q${b[0]},${b[1]} ${b[0]+(c[0]-b[0])*cut/bc},${b[1]+(c[1]-b[1])*cut/bc}`;
    }
    const end=points.at(-1);
    return out+` L${end[0]},${end[1]}`+(close?" Z":"");
  }
  function connection(c){
    const a=c.points[0],b=c.points[1],length=Math.hypot(b[0]-a[0],b[1]-a[1]);
    const nx=-(b[1]-a[1])*c.width/(2*length),ny=(b[0]-a[0])*c.width/(2*length);
    return [[a[0]+nx,a[1]+ny],[b[0]+nx,b[1]+ny],[b[0]-nx,b[1]-ny],[a[0]-nx,a[1]-ny]];
  }
  function hero(x,y){
    return `<circle class="edge" cx="${x}" cy="${y-.82}" r=".18"/><path class="edge" d="M${x-.23},${y-.6} h.46 v.35 h-.46 Z M${x-.13},${y-.25} V${y} M${x+.13},${y-.25} V${y}"/>`;
  }
  function contents(){
    if(!state.content)return "";
    return C.contents.map(c=>{
      if(c.kind==="ore")return `<path class="ore" d="M${c.x-c.r},${c.y} l${c.r},${-c.r} l${c.r},${c.r} l${-c.r},${c.r} Z"/>`;
      if(c.kind==="nest")return `<ellipse class="zone" cx="${c.x}" cy="${c.y}" rx="${c.r}" ry="${c.r*.65}"/>`;
      return `<rect class="zone" x="${c.x}" y="${c.y}" width="${c.w}" height="${c.h}"/>`;
    }).join("");
  }
  function routePrefix(distance){
    const points=[C.route[0]];
    for(let i=1;i<C.route.length;i++){
      if(C.lengths[i]<=distance){points.push(C.route[i]);continue;}
      const a=C.route[i-1],b=C.route[i],t=(distance-C.lengths[i-1])/(C.lengths[i]-C.lengths[i-1]);
      points.push([a[0]+(b[0]-a[0])*Math.max(0,t),a[1]+(b[1]-a[1])*Math.max(0,t)]);
      break;
    }
    return points;
  }
  function render(){
    const g=goal(),cam=C.cameras[state.view];
    q("cnm-goal").value=state.goal;q("cnm-view").value=state.view;
    for(const key of ["route","bounds","content"])q("cnm-"+key).checked=state[key];
    const width=Math.max(260,Math.round(map.getBoundingClientRect().width));
    const height=Math.max(285,Math.min(650,(width-28)*cam.h/cam.w+55));
    const scale=Math.min((width-24)/cam.w,(height-52)/cam.h);
    const tx=(width-cam.w*scale)/2-cam.x*scale,ty=29+(height-52-cam.h*scale)/2-cam.y*scale;
    const air=C.rooms.map(r=>rounded(r.polygon));
    C.connections.forEach(c=>air.push(rounded(connection(c))));
    const sky=[[-15,-30],[100,-30],...C.surface.slice().reverse()];
    const skyPath=rounded(sky);
    const allAir=air.concat(skyPath).map(d=>`<path d="${d}"/>`).join("");
    let body=`<defs>
      <clipPath id="cnm-camera"><rect x="0" y="25" width="${width}" height="${height-47}"/></clipPath>
      <mask id="cnm-rock" maskUnits="userSpaceOnUse" x="-40" y="-40" width="180" height="160">
        <rect x="-40" y="-40" width="180" height="160" fill="white"/><g fill="black">${allAir}</g>
      </mask>
      <mask id="cnm-air" maskUnits="userSpaceOnUse" x="-40" y="-40" width="180" height="160">
        <rect x="-40" y="-40" width="180" height="160" fill="black"/><g fill="white">${allAir}</g>
      </mask>
      </defs><rect class="rock" x="0" y="25" width="${width}" height="${height-47}"/>`;
    body+=`<g clip-path="url(#cnm-camera)"><g transform="translate(${tx},${ty}) scale(${scale})">
      <g class="air">${allAir}</g>
      <g mask="url(#cnm-rock)">${air.map(d=>`<path class="edge" d="${d}"/>`).join("")}<path class="edge" d="${rounded(C.surface,false)}"/></g>
      ${contents()}${hero(7,-1.3)}`;
    if(state.bounds){
      body+=C.groups.map(group=>{
        const [x,y,w,h]=group.box;
        return `<rect class="guide" x="${x}" y="${y}" width="${w}" height="${h}" stroke-dasharray="3 3"/>`;
      }).join("");
    }
    if(state.route){
      const d="M"+routePrefix(shown).map(p=>`${p[0]},${p[1]-.5}`).join(" L");
      body+=`<path class="route" mask="url(#cnm-air)" d="${d}"/>`;
    }
    body+=C.rooms.map(r=>`<path data-room="${r.id}" class="cursor-interaction" d="${rounded(r.polygon)}" fill="transparent"/>`).join("");
    body+="</g></g>";
    const full=width>=520;
    for(const r of C.rooms){
      const [lx,ly]=r.label;
      if(lx<cam.x || lx>cam.x+cam.w || ly<cam.y || ly>cam.y+cam.h)continue;
      const text=full?`${r.id} ${r.name}`:r.id,half=text.length*(full?6:4)+2;
      const x=Math.max(half+4,Math.min(width-half-4,tx+lx*scale)),y=Math.max(40,Math.min(height-30,ty+ly*scale-4));
      body+=`<text data-room="${r.id}" class="cursor-interaction" x="${x}" y="${y}" text-anchor="middle">${esc(text)}</text>`;
    }
    for(const mark of C.goals){
      const p=C.route[mark.index];
      if(p[0]<cam.x || p[0]>cam.x+cam.w || p[1]<cam.y || p[1]>cam.y+cam.h)continue;
      const x=tx+p[0]*scale,y=ty+(p[1]-1.2)*scale;
      body+=`<circle class="${mark.id===state.goal?"focus":"air"}" cx="${x}" cy="${y}" r="10"/>
        <text x="${x}" y="${y+4}" text-anchor="middle">${mark.id}</text>`;
    }
    const title={all:"紧凑三层 · 短段内持续遇到内容",shallow:"浅层：试采／错台／浅矿",middle:"中层：下探口／壁矿／侧室",deep:"深层：短风险点／富矿折返"}[state.view];
    body+=`<text x="8" y="17">${esc(title)}</text><text x="${width-8}" y="${height-6}" text-anchor="end">地面入口 · 天然岩层与岩肩</text>`;
    map.innerHTML=`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" role="img" aria-label="${esc(title)}"><title>${esc(title)}</title>${body}</svg>`;
    const i=C.goals.indexOf(g),increase=i?`，比上一目标多约${Math.round((g.length/C.goals[i-1].length-1)*100)}%`:"";
    q("cnm-distance").textContent=`${g.id} ${g.name} · 距洞口累计约${g.ratio.toFixed(2)}L${increase}`;
    q("cnm-detail").textContent=`${C.byId(state.selected).name}：${C.byId(state.selected).goal}`;
    root.dataset.ready="true";root.dataset.moving=motion?"true":"false";
  }
  function animateGoal(){
    cancelAnimationFrame(motion);
    const from=shown,to=goal().length,start=performance.now();
    if(matchMedia("(prefers-reduced-motion: reduce)").matches){shown=to;motion=0;render();return;}
    function tick(now){
      const t=Math.min(1,(now-start)/260),ease=t*t*(3-2*t);
      shown=from+(to-from)*ease;motion=t<1?requestAnimationFrame(tick):0;render();
    }
    motion=requestAnimationFrame(tick);
  }
  q("cnm-goal").addEventListener("change",e=>{
    state.goal=e.target.value;state.selected=goal().room;animateGoal();save();
  });
  q("cnm-view").addEventListener("change",e=>{state.view=e.target.value;render();save();});
  for(const key of ["route","bounds","content"])q("cnm-"+key).addEventListener("change",e=>{
    state[key]=e.target.checked;render();save();
  });
  map.addEventListener("click",e=>{
    const hit=e.target.closest("[data-room]");
    if(hit){state.selected=hit.dataset.room;render();save();}
  });
  const observer=new ResizeObserver(()=>{cancelAnimationFrame(resize);resize=requestAnimationFrame(render);});
  observer.observe(map);
  const onGlobals=e=>{if(e.detail?.globals?.widgetState){cancelAnimationFrame(motion);motion=0;restore(e.detail.globals.widgetState);render();}};
  window.addEventListener("openai:set_globals",onGlobals);
  window.addEventListener("pagehide",()=>{
    observer.disconnect();cancelAnimationFrame(resize);cancelAnimationFrame(motion);window.removeEventListener("openai:set_globals",onGlobals);
  },{once:true});
  root._inspect=()=>({state:{...state},camera:C.cameras[state.view],goals:C.goals,rooms:C.rooms.length,groups:C.groups.length,shown,moving:Boolean(motion)});
  render();
}());
