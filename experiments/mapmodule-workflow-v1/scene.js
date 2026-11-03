function mapModuleScene(svg, options, unique) {
  const selected=mmSchemes.find(s=>s.id===options.variant)||mmSchemes[2];
  const second=mmSchemes.find(s=>s.id===options.second)||selected;
  const width=Math.max(260,svg.parentElement.clientWidth),height=options.lab?390:320;
  const [from,to]=options.scope==="single"?[0,6.4]:[0,12.8];
  const scale=Math.min((width-28)/(to-from),(height-68)/3.2),top=(height-3.2*scale)/2;
  const offset=(width-(to-from)*scale)/2;
  svg.setAttribute("viewBox",`0 0 ${width} ${height}`);
  svg.setAttribute("height",height);
  svg.dataset.scale=scale;
  svg.dataset.variant=selected.id;
  svg.dataset.second=second.id;
  svg.dataset.scope=options.scope;
  const id=unique.replace(/[^a-z0-9-]/g,"");
  const defs=`<defs><clipPath id="${id}-viewport"><rect x="${offset}" y="${top}" width="${(to-from)*scale}" height="${3.2*scale}"/></clipPath><clipPath id="${id}-left"><rect x="0" y="0" width="6.4" height="3.2"/></clipPath><clipPath id="${id}-right"><rect x="6.4" y="0" width="6.4" height="3.2"/></clipPath></defs>`;
  const ground=(scheme,part)=>{
    let plants="";
    if(options.ecology) {
      plants=scheme.plants.map(p=>p.kind==="grass"
        ?`<path class="mm-grass" transform="translate(${p.x} ${p.y}) scale(${p.scale})" d="M-.14 0Q-.2 -.13 -.14 -.21L-.04 -.06Q-.08 -.24 0 -.32Q.05 -.2 .03 -.06Q.11 -.22 .18 -.2Q.13 -.05 .1 0Z"/>`
        :`<g transform="translate(${p.x} ${p.y})"><path class="mm-fungus" d="M-.018 0V-.18H.018V0ZM-.16 -.17Q-.13 -.33 0 -.34Q.13 -.32 .16 -.18Z"/></g>`).join("");
      plants+=scheme.roots.map(p=>`<path fill="none" stroke="var(--mm-grass)" stroke-width=".025" d="M${p.x} ${p.y}q-.08 .1 0 ${p.length}m.08 ${-p.length}q.12 .12 .04 ${p.length*.8}"/>`).join("");
      if(scheme.water) {
        const w=scheme.water;
        plants+=`<path class="mm-water" opacity=".55" d="M${w.x-.05} ${w.top}h.1V${w.bottom}h-.1Z M${w.left} ${w.surface}H${w.right}V3.2H${w.left}Z"/>`;
      }
    }
    const hero=scheme.heroes[part];
    let shape="";
    if(options.collision) shape=`<polyline class="mm-shape-overlay" points="${scheme.floor.map(p=>p.join(",")).join(" ")}"/>`;
    const actor=options.lab&&part===1?"":`<image data-mm-hero x="${hero.x-.48*232/251/2}" y="${hero.y-.48}" width="${.48*232/251}" height=".48" href="${mmHero}"/>`;
    return `<g clip-path="url(#${id}-${part?"right":"left"})">${plants}<path class="mm-rock" d="${scheme.floorSolid}"/><path class="mm-rock" d="${scheme.ceilingSolid}"/><path class="mm-edge" d="${scheme.floorPath}"/><path class="mm-edge" d="${scheme.ceilingPath}"/>${scheme.shelf?`<path class="mm-rock" d="${scheme.shelf}"/><path class="mm-edge" d="${scheme.shelf}"/>`:""}${shape}${actor}</g>`;
  };
  let grid="";
  for(let x=14;x<width;x+=32) grid+=`<path class="mm-grid" d="M${x} 28V${height-28}"/>`;
  for(let y=28;y<height-20;y+=32) grid+=`<path class="mm-grid" d="M14 ${y}H${width-14}"/>`;
  if(options.lab&&!options.bounds&&!options.collision)grid="";
  let bounds="";
  if(options.bounds) {
    bounds=`<rect class="mm-module-bound" x="${offset}" y="${top}" width="${6.4*scale}" height="${3.2*scale}"/>`;
    if(options.scope!=="single") bounds+=`<rect class="mm-module-bound" x="${offset+6.4*scale}" y="${top}" width="${6.4*scale}" height="${3.2*scale}"/>`;
  }
  const clash=options.scope!=="single"&&selected.id!==second.id;
  const joinX=offset+6.4*scale;
  const join=clash?`<path stroke="var(--mm-error)" stroke-width="2" d="M${joinX} ${top}V${top+3.2*scale}"/><text x="${width/2}" y="${height-13}" text-anchor="middle">接口高度／开口不匹配</text>`:"";
  svg.innerHTML=`<title>${selected.name}与${second.name}的地图模块草案</title><desc>扁平正侧视。左侧地表经过洞檐进入洞穴。模块与原主角同比例显示，地形为界面布局用方案草图。</desc>${defs}${grid}<g clip-path="url(#${id}-viewport)"><g transform="translate(${offset} ${top}) scale(${scale})"><rect class="mm-air" width="12.8" height="3.2"/>${ground(selected,0)}${ground(second,1)}</g>${bounds}${join}</g>`;
}

function mapModuleEmpty(canvas,title,detail) {
  canvas.innerHTML=`<div class="mm-empty"><h3>${title}</h3><p>${detail}</p></div>`;
}

function mapModuleRemember(root,design,states) {
  if(window.openai&&window.openai.setWidgetState) {
    window.openai.setWidgetState({modelContent:{design,layouts:states},privateContent:null}).catch(()=>{});
  }
}

function mapModuleReadState(design) {
  const saved=window.openai&&window.openai.widgetState;
  return saved&&saved.design===design&&saved.layouts?saved.layouts:{};
}
