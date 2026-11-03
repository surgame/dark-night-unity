(function () {
  "use strict";
  const P=globalThis.CaveBlockDesign,root=document.getElementById("cave-macroblock-design");
  const query=id=>root.querySelector("#"+id),map=query("cbd-map");
  const defaults={view:"module",module:"M04",bounds:true,content:true};
  let state={...defaults},raf=0;
  const esc=v=>String(v).replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&apos;"}[c]));
  query("cbd-module").innerHTML=P.modules.map(m=>`<option value="${m.id}">${m.id} ${m.name} · ${m.stage}</option>`).join("");
  function restore(saved) {
    const v=saved?.privateContent;
    if(!v || v.kind!==P.kind)return;
    state={view:["assembly","module","port"].includes(v.view)?v.view:defaults.view,
      module:P.byId(v.module).id,bounds:typeof v.bounds==="boolean"?v.bounds:true,
      content:typeof v.content==="boolean"?v.content:true};
  }
  restore(window.openai?.widgetState);
  function save() {
    window.openai?.setWidgetState?.({
      modelContent:{design:P.kind,...state,name:P.byId(state.module).name},
      privateContent:{kind:P.kind,...state}
    }).catch(()=>{});
  }
  // Corner relief varies by vertex so long ledges survive the schematic rounding.
  function path(points,close=false) {
    if(points.length<2)return "";
    let out=`M${points[0][0]},${points[0][1]}`;
    for(let i=1;i<points.length-1;i++){
      const a=points[i-1],b=points[i],c=points[i+1];
      const cut=Math.min(.18+(i%3)*.12,Math.hypot(b[0]-a[0],b[1]-a[1])*.2,Math.hypot(c[0]-b[0],c[1]-b[1])*.2);
      const l1=Math.hypot(b[0]-a[0],b[1]-a[1])||1,l2=Math.hypot(c[0]-b[0],c[1]-b[1])||1;
      out+=` L${b[0]-(b[0]-a[0])*cut/l1},${b[1]-(b[1]-a[1])*cut/l1} Q${b[0]},${b[1]} ${b[0]+(c[0]-b[0])*cut/l2},${b[1]+(c[1]-b[1])*cut/l2}`;
    }
    const end=points[points.length-1];
    return out+` L${end[0]},${end[1]}`+(close?" Z":"");
  }
  function content(m) {
    if(!state.content)return "";
    return P.contents(m).map(c=>{
      if(c.type==="ore")return `<path class="ore" d="M${c.x-c.r},${c.y} l${c.r},${-c.r} l${c.r},${c.r} l${-c.r},${c.r} Z"/>`;
      if(c.type==="nest")return `<ellipse class="zone" cx="${c.x}" cy="${c.y}" rx="${c.r}" ry="${c.r*.6}"/>`;
      if(c.type==="event")return `<rect class="zone" x="${c.x}" y="${c.y}" width="${c.w}" height="${c.h}"/>`;
      if(c.type==="water")return `<path class="water" d="M${c.x},${c.y} h${c.w} v${c.h} h3 v1 h-7 v-1 h2 Z"/>`;
      if(c.type==="tool")return `<rect class="zone" x="${c.x}" y="${c.y}" width="${c.w}" height="${c.h}"/>`;
      return `<path class="zone" d="M${c.x-1},${c.y} q-1,-3 0,-3 q2,0 1,3 M${c.x+1},${c.y} q0,-4 1,-4 q2,0 1,4"/>`;
    }).join("");
  }
  function terrain(m) {
    const s=P.shape(m),shape=path([...s.roof,...s.floor.slice().reverse()],true);
    const b=P.branch(m);
    if(!b)return `<path class="air" d="${shape}"/><path class="edge" d="${path(s.roof)}"/><path class="edge" d="${path(s.floor)}"/>`;
    const branch=path([...b.roof,...b.floor.slice().reverse()],true),a=`cbd-${m.id}-main`,c=`cbd-${m.id}-branch`;
    // Black and white are alpha-mask values, independent of the displayed theme.
    return `<defs>
      <mask id="${a}" maskUnits="userSpaceOnUse" x="-1" y="-1" width="${m.w+2}" height="${m.h+2}">
        <rect x="-1" y="-1" width="${m.w+2}" height="${m.h+2}" fill="white"/><path d="${branch}" fill="black"/>
      </mask>
      <mask id="${c}" maskUnits="userSpaceOnUse" x="-1" y="-1" width="${m.w+2}" height="${m.h+2}">
        <rect x="-1" y="-1" width="${m.w+2}" height="${m.h+2}" fill="white"/><path d="${shape}" fill="black"/>
      </mask>
      </defs><path class="air" d="${shape}"/><path class="air" d="${branch}"/>
      <path class="edge" d="${path(s.roof)} ${path(s.floor)}" mask="url(#${a})"/>
      <path class="edge" d="${path(b.roof)} ${path(b.floor)}" mask="url(#${c})"/>`;
  }
  function guides(m) {
    if(!state.bounds)return "";
    const s=P.shape(m),end=m.w;
    let text=`<rect class="guide" width="${m.w}" height="${m.h}" stroke-dasharray="3 3"/><path class="port" d="M0,${s.roof[0][1]} V${s.floor[0][1]}"/>`;
    if(m.ports>1)text+=`<path class="port" d="M${end},${s.roof.at(-1)[1]} V${s.floor.at(-1)[1]}"/>`;
    const b=P.branch(m);
    if(b)text+=`<path class="port" d="M${end},${b.roof.at(-1)[1]} V${b.floor.at(-1)[1]}"/>`;
    return text;
  }
  function hero(x,y) {
    return `<path class="edge" d="M${x-.15},${y} v-.38 M${x+.15},${y} v-.38 M${x-.25},${y-.38} h.5 v-.25 h-.5 Z"/><circle class="edge" cx="${x}" cy="${y-.83}" r=".17"/>`;
  }
  function svg(w,h,body,label) {
    return `<svg class="cbd-canvas" xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}" role="img" aria-label="${esc(label)}"><title>${esc(label)}</title>${body}</svg>`;
  }
  function moduleView(w) {
    const m=P.byId(state.module),h=Math.max(240,Math.min(380,w*.54));
    const margin=25,scale=Math.min((w-margin*2)/m.w,(h-70)/m.h);
    const x=(w-m.w*scale)/2,y=35;
    const s=P.shape(m),body=`
      <g transform="translate(${x},${y}) scale(${scale})">
        <rect class="rock" width="${m.w}" height="${m.h}"/>
        ${terrain(m)}${content(m)}${guides(m)}
        ${hero(m.w*.18,m.h*.72)}
      </g>
      <text x="8" y="17">${m.id} ${esc(m.name)}</text>
      <text x="${w-8}" y="${h-10}" text-anchor="end">${m.w}H × ${m.h}H · H＝角色碰撞高度</text>`;
    return svg(w,h,body,`${m.name}正侧视设计示意，${m.goal} ${m.slots}`);
  }
  function assembly(w) {
    const h=Math.max(230,Math.min(350,w*.46)),scale=(w-26)/306,ox=13,oy=45;
    const placed=id=>P.placements.find(p=>p.id===id);
    let body=`<rect class="rock" x="0" y="28" width="${w}" height="${h-52}"/>`;
    body+=`<g transform="translate(${ox},${oy}) scale(${scale})">`;
    // All corridors are schematic links; no random solver or collision is implied.
    for(const [from,to] of P.edges) {
      const a=placed(from),b=placed(to),ma=P.byId(from),mb=P.byId(to);
      if(a.y===20 && to==="M08" || to==="M10") {
        const x=a.x+ma.w*.45,y=a.y+ma.h*.6;
        const bx=b.x+mb.w*.4,by=b.y+mb.h*.6;
        body+=`<path class="air" d="M${x-1.5},${y} L${bx-1.5},${by} h3 L${x+1.5},${y} Z"/>`;
      } else {
        const sa=P.shape(ma),sb=P.shape(mb),ax=a.x+ma.w,ay=a.y+sa.floor.at(-1)[1],by=b.y+sb.floor[0][1];
        body+=`<path class="air" d="M${ax-.2},${ay-2.5} L${b.x+.2},${by-2.5} V${by} L${ax-.2},${ay} Z"/>`;
      }
    }
    for(const p of P.placements) {
      const m=P.byId(p.id);
      body+=`<g data-module="${m.id}" class="cursor-interaction" transform="translate(${p.x},${p.y})">${terrain(m)}${content(m)}`;
      if(state.bounds)body+=`<rect class="${state.module===m.id?"focus":"guide"}" width="${m.w}" height="${m.h}" stroke-dasharray="3 3"/>`;
      body+=`</g>`;
    }
    body+="</g>";
    const wide=w>=520;
    for(let i=0;i<P.placements.length;i++) {
      const p=P.placements[i],m=P.byId(p.id),x=ox+(p.x+m.w/2)*scale;
      const label=wide?`${m.id} ${m.name}`:m.id;
      let y=p.id==="M08"?36:p.id==="M10"?h-9:(i%2? h-38: h-59);
      body+=`<text x="${Math.max(20,Math.min(w-20,x))}" y="${y}" text-anchor="middle">${esc(label)}</text>`;
    }
    body+=`<text x="8" y="16">8块主干＋2块侧支 · 人工组合示意</text>`;
    return svg(w,h,body,"首张组合示意：洞口、试采台、窄路、岔洞、岩台厅、塌方、富矿与折返厅；另有矿窝及侧巢支线。");
  }
  function portView(w) {
    const h=w<460?330:270,half=(w-32)/2;
    let body=`<text x="8" y="17">S口：建议净空2.5H · 两侧各1H接口带</text>`;
    const y=52,scale=Math.min((w-32)/16,12),start=(w-16*scale)/2;
    body+=`<g transform="translate(${start},${y}) scale(${scale})">
      <rect class="rock" width="16" height="9"/>
      <path class="air" d="M0,2.5 H16 V5 H0 Z"/>
      <path class="edge" d="M0,2.5 H16 M0,5 H16"/>
      <rect class="focus" x="7" width="2" height="9"/>
      <path class="port" d="M8,2.5 V5"/>
      <path class="guide" d="M7,0 V9 M9,0 V9"/>
      ${hero(4,5)}
    </g>`;
    body+=`<text x="${w/2}" y="${y+9*scale+20}" text-anchor="middle">形状／切向／材质相位／水口分别匹配</text>`;
    const rows=w<460?6:3,cols=w<460?1:2,top=y+9*scale+50;
    P.connectors.forEach((c,i)=>{
      const x=8+(i%cols)*(half+8),yy=top+Math.floor(i/cols)*22;
      body+=`<text x="${x}" y="${yy}">${c[0]} ${c[1]}</text>`;
    });
    return svg(w,Math.max(h,top+rows*22+5),body,"标准接口剖面及六类有限连接组件。尺寸是设计建议，未验证角色通行。");
  }
  function render() {
    query("cbd-view").value=state.view;query("cbd-module").value=state.module;
    query("cbd-bounds").checked=state.bounds;query("cbd-content").checked=state.content;
    const w=Math.max(260,Math.round(map.getBoundingClientRect().width));
    map.innerHTML=state.view==="module"?moduleView(w):state.view==="port"?portView(w):assembly(w);
    const m=P.byId(state.module);
    query("cbd-selected").textContent=state.view==="port"?"接口合同 · 6类有限连接组件":`${m.id} ${m.name} · ${m.stage} · ${m.w}H × ${m.h}H`;
    query("cbd-purpose").textContent=state.view==="port"?"S＝普通口2.5H；N＝窄口1.5H；W＝宽口4H。均为设计起始建议。":m.goal;
    query("cbd-slots").textContent=state.view==="port"?"刚性放置与显式水平镜像；变化区和生态运动包络单独记录。":`插槽：${m.slots}　关系：${m.relation}`;
    query("cbd-caption").textContent=state.view==="assembly"?
      "主干按原路折返组织；切到单块可比较形状与角色尺。示意尚未执行拼装／通行验证。":
      state.view==="module"?"边框为作者占地，洞形不必规则；矿层、巢穴及变化区独立。尺寸待白模校准。":
      "端口同名仍需核对完整剖面；过渡库无法连接时返回布局调整，不无限变形。";
    root.dataset.ready="true";
  }
  for(const [id,key] of [["cbd-view","view"],["cbd-module","module"],["cbd-bounds","bounds"],["cbd-content","content"]]){
    query(id).addEventListener("change",e=>{
      state[key]=e.target.type==="checkbox"?e.target.checked:e.target.value;
      if(key==="module" && state.view==="assembly" && !P.placements.some(p=>p.id===state.module))state.view="module";
      render();save();
    });
  }
  map.addEventListener("click",e=>{
    const target=e.target.closest("[data-module]");
    if(target){state.module=target.dataset.module;render();save();}
  });
  const observer=new ResizeObserver(()=>{cancelAnimationFrame(raf);raf=requestAnimationFrame(render);});
  observer.observe(map);
  const onGlobals=e=>{
    const saved=e.detail?.globals?.widgetState;
    if(saved){restore(saved);render();}
  };
  window.addEventListener("openai:set_globals",onGlobals);
  window.addEventListener("pagehide",()=>{
    observer.disconnect();cancelAnimationFrame(raf);window.removeEventListener("openai:set_globals",onGlobals);
  },{once:true});
  root._inspect=()=>({state:{...state},modules:P.modules.length,first:P.modules.filter(m=>m.stage==="首做").length,
    core:P.modules.filter(m=>m.stage!=="扩展").length,placements:P.placements.length,edges:P.edges.length,
    caption:query("cbd-caption").textContent});
  render();
}());
