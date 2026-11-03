(function (root) {
  "use strict";
  const profiles = [
    { floor: [[0,258],[65,253],[130,252],[190,258],[245,272],[300,282],[360,264],[430,258],[505,263],[570,263],[623,262]], roof: [[0,121],[60,118],[145,102],[215,111],[290,163],[335,149],[395,112],[470,112],[550,132],[623,131]] },
    { floor: [[0,263],[65,261],[135,268],[205,284],[270,296],[335,312],[420,322],[505,334],[565,335],[623,333]], roof: [[0,129],[90,130],[165,130],[240,150],[330,166],[430,180],[520,192],[623,190]] },
    { floor: [[0,293],[60,291],[125,275],[195,256],[265,238],[335,224],[405,207],[485,193],[550,190],[623,190]], roof: [[0,160],[70,147],[145,121],[220,111],[310,103],[390,90],[470,100],[550,116],[623,118]] },
    { floor: [[0,294],[75,293],[150,290],[225,300],[300,311],[375,304],[450,302],[535,302],[623,303]], roof: [[0,177],[65,150],[125,114],[190,87],[260,61],[335,58],[410,90],[480,131],[550,157],[623,178]] },
    { floor: [[0,265],[70,264],[135,272],[200,280],[270,286],[345,279],[420,267],[500,264],[560,262],[623,265]], roof: [[0,125],[75,119],[150,100],[220,89],[290,52],[355,56],[420,85],[490,107],[560,121],[623,127]] },
    { floor: [[0,286],[65,284],[135,282],[205,295],[275,307],[340,315],[405,307],[475,312],[550,317],[623,318]], roof: [[0,161],[70,156],[140,158],[210,175],[280,214],[340,200],[410,161],[480,151],[550,162],[623,160]] }
  ];
  const authoredBridge=root.ECOLOGY_AUTHORED_PROFILES?.bridge;
  if(!authoredBridge)throw new Error("Missing user-authored bridge profile.");
  profiles.push({floor:authoredBridge.floor,roof:authoredBridge.roof});
  const bridgeArt={key:"bridgeDesign",x:64,y:0,drawWidth:495,drawHeight:330,
    nativeWidth:1536,nativeHeight:1024,sourceSha256:authoredBridge.sourceSha256,
    profileVersion:authoredBridge.version,interfaceOutsideSourceArt:true};
  const bridgeGap=[...authoredBridge.bridgeAnchors,
    Math.max(...authoredBridge.floor.map(p=>p[1]))-
    Math.max(...authoredBridge.bridgeAnchors.map(x=>sample(authoredBridge.floor,x)))];
  const catalog = [
    { id:"camp", name:"营地干台", slot:"camp", panel:0, ecology:"dry", intent:"出发、交付与重新整备" },
    { id:"narrow", name:"原版窄洞", slot:"narrow", panel:0, ecology:"dry", gap:[280,328,27], intent:"短跳试探；浅凹可走回" },
    { id:"creek-stones", name:"苔溪踏石", slot:"wet", panel:1, ecology:"creek", gap:[260,308,30], intent:"浅溪旁练跳；干岸提供退路" },
    { id:"creek-bank", name:"低岸浅滩", slot:"wet", panel:3, ecology:"creek", intent:"开阔缓行区；上侧小矿窝可选" },
    { id:"moss-cleft", name:"苔坡裂隙", slot:"wet", panel:2, ecology:"moss", gap:[340,387,28], intent:"斜坡短断口；湿润裂隙地标" },
    { id:"beam-gap", name:"木梁断口", slot:"engineering", panel:6, artFamily:"bridge-authored-v2",
      authoredArt:bridgeArt,ecology:"moss",gap:bridgeGap,mechanism:"bridge",intent:"原设计两岸与台阶；来侧取梁，修好的桥保留到返程" },
    { id:"broken-shelf", name:"碎岩修桥", slot:"engineering", panel:6, artFamily:"bridge-authored-v2",
      authoredArt:bridgeArt,ecology:"moss",gap:bridgeGap,mechanism:"bridge",crumble:true,intent:"原设计断岸；台阶岩屑预警后搭稳定木梁" },
    { id:"cargo", name:"低顶货槽", slot:"cargo", panel:5, ecology:"dry", intent:"空手穿行；返程先投矿，再取回" },
    { id:"fungal-balcony", name:"伞菇高台", slot:"fungi", panel:4, ecology:"fungi", branch:true, intent:"主路直行；上台探索可选露矿" },
    { id:"fungal-bowl", name:"菌丛凹厅", slot:"fungi", panel:3, ecology:"fungi", gap:[302,348,23], intent:"蘑菇标记潮湿凹地；触碰有回弹" },
    { id:"root-falls", name:"根系泉瀑", slot:"falls", panel:2, ecology:"falls", branch:true, intent:"干岩台升高；旁支藏可选矿点" },
    { id:"double-falls", name:"双阶细瀑", slot:"falls", panel:4, ecology:"falls", intent:"双层落水与叶根形成方向地标" },
    { id:"mineral-bowl", name:"深部矿碗", slot:"mine", panel:4, ecology:"fungi", intent:"高处先观察，露矿引导采下主矿" },
    { id:"mineral-hall", name:"裂面矿厅", slot:"mine", panel:3, ecology:"moss", intent:"主矿在实岩面；带货原路回撤" }
  ];
  const slots = ["camp","narrow","wet","engineering","cargo","fungi","falls","mine"];
  function random(seed) {
    let a = seed >>> 0;
    return function () { a += 0x6D2B79F5; let t = Math.imul(a ^ a >>> 15, 1 | a);
      t ^= t + Math.imul(t ^ t >>> 7, 61 | t); return ((t ^ t >>> 14) >>> 0) / 4294967296; };
  }
  function sample(points, x) {
    for (let i=1;i<points.length;i++) if(x<=points[i][0]) {
      const a=points[i-1],b=points[i],t=(x-a[0])/(b[0]-a[0]); return a[1]+(b[1]-a[1])*t;
    }
    return points[points.length-1][1];
  }
  function section(points, a, b) {
    return [[a,sample(points,a)],...points.filter(p=>p[0]>a&&p[0]<b),[b,sample(points,b)]];
  }
  function create(seed) {
    const shapes=root.EcologyTerrainShapes,rules=root.EcologyModuleRules;
    if(!shapes||!rules)throw new Error("Missing terrain shapes or module rules.");
    const solution=rules.solve({seed,catalog,profiles,assetIdentity:root.ECOLOGY_BUILD_ID||shapes.VERSION});
    const world={seed,rooms:[],surfaces:[],ceilings:[],walls:[],objects:[],
      bounds:{x:0,y:0,w:0,h:900},catalog,slots,solution,shapeVersion:shapes.VERSION};
    const route=solution.route;
    world.slots=route;
    let x=0, endpoint=330;
    function object(room,type,lx,ly,extra) {
      const value=Object.assign({id:room.id+"-"+type+"-"+world.objects.length,type,x:room.x+lx,y:room.y+ly,roomId:room.id},extra||{});
      value.name=value.name||value.label||type;
      world.objects.push(value);return value;
    }
    function surface(room,id,points,extra) {
      const value=Object.assign({id:room.id+"-"+id,points:points.map(p=>[p[0]+room.x,p[1]+room.y]),roomId:room.id},extra||{});
      world.surfaces.push(value);return value.id;
    }
    route.forEach((slot,index)=>{
      const chosen=solution.selected[index],source=catalog.find(c=>c.id===chosen.sourceId);
      const native=source.authoredArt?authoredBridge.native:root.ECOLOGY_CONTOURS?.[source.panel];
      const base=source.authoredArt?{floor:authoredBridge.floor,roof:authoredBridge.roof}:
        shapes.normalized(profiles[source.panel],native);
      const cut=source.authoredArt?{a:bridgeGap[0],b:bridgeGap[1],depth:bridgeGap[2],
        base:Math.max(...base.floor.map(p=>p[1])),points:section(base.floor,bridgeGap[0],bridgeGap[1]),
        floor:base.floor,authored:true}:shapes.cutShape(base,source.gap);
      const p={floor:cut?cut.floor:base.floor,roof:base.roof};
      const room=Object.assign({},source,{id:"r"+index,sourceId:source.id,x,
        y:endpoint-base.floor[0][1],width:623,height:414,index,profile:p,plants:[],water:[],
        ports:chosen.ports,shape:{floor:p.floor,roof:p.roof,road:shapes.road(p.floor),cut,ledges:[],version:shapes.VERSION},
        interface:{band:64,blend:112,material:"handdrawn-rock-v2",geometry:"P128"}});
      const rng=random((solution.seeds.decoration^parseInt(shapes.hash(source.id+index),16))>>>0);
      room.branch=slot==="wet"||slot==="fungi";
      const floor=lx=>sample(p.floor,lx), roof=lx=>sample(p.roof,lx);
      const roofLimit=(a,b)=>Math.max(roof(a),roof(b),...p.roof.filter(q=>q[0]>a&&q[0]<b).map(q=>q[1]));
      world.rooms.push(room);
      if(cut) {
        const {a,b}=cut;
        surface(room,"left",section(p.floor,0,a));
        surface(room,"right",section(p.floor,b,623));
        if(source.mechanism==="bridge") {
          surface(room,"pit",cut.points,{kind:"pit"});
          const bridge=surface(room,"span",[[a,floor(a)],[b,floor(b)]],{enabled:false,kind:"bridge"});
          object(room,"supply",a-83,floor(a-83),{label:"来侧木梁",radius:42});
          object(room,"bridge",a-30,floor(a-30),{surfaceId:bridge,requireSupply:true,label:"搭稳定木梁",radius:58});
          if(source.crumble) {
            const plate=source.authoredArt?authoredBridge.crumblePlate:[[a+9,floor(a)+28],[a+55,floor(a)+28]];
            const crumb=surface(room,"crumb",plate,{kind:"crumble"});
            object(room,"crumble",(plate[0][0]+plate[1][0])/2,plate[0][1],
              {surfaceId:crumb,label:"碎岩先抖后落",warningAfter:.45,breakAfter:1.2});
          }
        } else {
          surface(room,"shallow",cut.points,{kind:"shallow"});
        }
        room.cut=cut;
      } else surface(room,"floor",p.floor);
      object(room,"checkpoint",55,floor(55),{label:room.name,radius:54});
      if(index===0) {
        world.spawn={x:95,y:room.y+floor(95)};
        world.exit={x:95,y:world.spawn.y};
        object(room,"stash",95,floor(95),{label:"营地交付",radius:50});
      }
      if(slot==="cargo") {
        const a=228,b=394,fy=Math.max(floor(a),floor(b));
        const bottom=Math.min(floor(a-12),floor(b+12))-46;
        room.lowroof=shapes.lowRoof(p,a,b,bottom);room.shape.lowroof=room.lowroof;
        p.roof=room.lowroof.roof;room.shape.roof=p.roof;
        object(room,"lowroof",a,fy-95,{w:b-a,h:95,label:"低顶：携矿先投槽"});
        object(room,"chute",a-31,floor(a-31),{target:{x:room.x+b+29,y:room.y+floor(b+29)},radius:40,label:"向右投货槽"});
        object(room,"chute",b+31,floor(b+31),{target:{x:room.x+a-29,y:room.y+floor(a-29)},radius:40,label:"向左投货槽"});
      }
      if(slot==="mine") {
        object(room,"ore",480,floor(480),{label:"远端露矿",primary:true,radius:48});
        object(room,"sign",150,floor(150)-55,{label:"采下主矿 → 原路交付"});
      }
      if(room.branch) {
        const bx=370,by=floor(bx);
        const y1=Math.max(by-27,roofLimit(bx-75,bx-10)+47);
        const y2=Math.max(by-56,roofLimit(bx-13,bx+57)+47);
        const y3=Math.max(by-81,roofLimit(bx+55,bx+142)+47);
        const ledges=[["side-step",[[bx-65,y1],[bx-20,y1]]],
          ["side-step2",[[bx-3,y2],[bx+47,y2]]],["side-balcony",[[bx+65,y3],[bx+132,y3]]]];
        ledges.forEach(([id,points])=>{
          const fullId=surface(room,id,points,{kind:"ledge"});
          room.shape.ledges.push(shapes.ledge(fullId,points));
        });
        object(room,"ore",bx+108,y3,{label:"旁支小矿",primary:false,radius:36});
        room.branch={x:bx,y:by};
      }
      if(["creek","moss","fungi"].includes(source.ecology)&&slot!=="engineering") {
        room.water.push(...shapes.streams(p));
      }
      if(source.ecology==="falls") {
        const locations=source.id==="double-falls"?[210,430]:[456];
        locations.forEach((fx,i)=>{
          const top=roof(fx)+3,waterline=floor(fx)-4;
          room.water.push({id:"fall-"+i,type:"fall",x:fx,y:top,w:i?10:15,h:waterline-top,
            source:{kind:"roofSpring",x:fx,y:top},sink:{kind:"pool",x:fx+7,y:waterline},
            pool:{x:fx+7,y:waterline+1,rx:24,ry:4}});
        });
        room.water.push(...shapes.streams(p,112,511));
      }
      const count=index===0?3:["wet","fungi","falls","mine"].includes(slot)?11:0;
      for(let n=0,attempt=0;n<count&&attempt<80;attempt++) {
        const lx=112+rng()*399,atRoof=n%4===0,scale=.6+rng()*.7;
        if(cut&&lx>cut.a-24&&lx<cut.b+24)continue;
        if(room.lowroof&&lx>room.lowroof.a-35&&lx<room.lowroof.b+35)continue;
        if(room.water.some(w=>w.type==="fall"&&Math.abs(w.x-lx)<29))continue;
        const kind=atRoof?"vine":slot==="fungi"&&n%3!==0?"mushroom":"grass";
        const support=atRoof?room.id+"-ceiling":world.surfaces.find(s=>s.roomId===room.id&&
          !["ledge","bridge","crumble","pit"].includes(s.kind)&&s.points[0][0]<=room.x+lx&&s.points.at(-1)[0]>=room.x+lx)?.id;
        if(!support)continue;
        const ly=atRoof?roof(lx)+1:floor(lx)-1,h=(atRoof?67:kind==="mushroom"?36:31)*scale;
        room.plants.push({x:lx,y:ly,kind,scale,phase:rng()*6.28,hue:n%3,
          supportId:support,layer:atRoof?"back":"front",baseCover:true,
          bounds:{x:lx-28*scale-9,y:atRoof?ly:ly-h,w:56*scale+18,h:h+4}});
        n++;
      }
      world.ceilings.push({id:room.id+"-ceiling",points:p.roof.map(q=>[q[0]+room.x,q[1]+room.y])});
      if(index>0) {
        const prev=world.rooms[index-1],pa=prev.x+623,pb=room.x,py=prev.y+sample(prev.profile.floor,623);
        world.surfaces.push({id:"connector-"+index,kind:"connector",points:[[pa,py],[pb,endpoint]]});
        world.ceilings.push({id:"connector-roof-"+index,points:[[pa,py-128],[pb,endpoint-128]]});
      }
      endpoint=room.y+floor(623); x+=719;
    });
    const endX=x-96;
    world.bounds.w=endX+64;
    world.bounds.h=Math.max(...world.rooms.map(r=>r.y+414))+200;
    world.bounds.y=Math.min(...world.rooms.map(r=>r.y))-50;
    world.id="ecology-walk-v2-"+(root.ECOLOGY_BUILD_ID||shapes.hash(shapes.VERSION+solution.libraryIdentity))+
      "-"+seed+"-"+shapes.hash(world.rooms.map(r=>r.sourceId));
    world.width=world.bounds.w;
    world.height=world.bounds.h;
    world.fallY=world.bounds.h-30;
    world.connections=world.rooms.slice(1).map((r,i)=>{
      const floor=r.y+sample(r.profile.floor,0),x0=world.rooms[i].x+623,x1=r.x;
      return {id:"connector-"+(i+1),from:world.rooms[i].id,to:r.id,type:"dry128",x0,x1,
        floor,roof:floor-128,shape:{floor:[[x0,floor],[x1,floor]],roof:[[x0,floor-128],[x1,floor-128]],
          road:shapes.road([[x0,floor],[x1,floor]])},
        contract:solution.links[i].contract,connectorId:solution.links[i].connectorId};
    });
    world.unchosen=catalog.filter(c=>!world.rooms.some(r=>r.id===c.id||r.name===c.name)).map(c=>c.id);
    world.boundaries={left:{id:"entry-boundary",side:"left",kind:"declared-entrance",x:0,port:"P128-v2",
      contract:{id:"declared-cave-entry-v2",fromPort:"OUTSIDE",toPort:"P128-v2"}},
      right:{id:"terminal-boundary",side:"right",kind:"terminal-cap",x0:endX,x1:endX+64,wallX:endX+26,floor:endpoint,
        roof:endpoint-128,fromPort:"P128-v2",toPort:"CLOSED",shape:{
          floor:[[endX,endpoint],[endX+26,endpoint]],roof:[[endX,endpoint-128],[endX+26,endpoint-128]]},
        road:shapes.road([[endX,endpoint],[endX+26,endpoint]]),
        contract:{id:"dry128-terminal-cap-v2",fromPort:"P128-v2",toPort:"CLOSED",
          width:64,openingLength:26,wallThickness:38}}};
    world.surfaces.push({id:"terminal-floor",kind:"terminal",points:world.boundaries.right.shape.floor});
    world.ceilings.push({id:"terminal-roof",points:world.boundaries.right.shape.roof});
    world.walls.push({id:"terminal-wall",x:endX+26,y:world.bounds.y-100,w:38,h:world.bounds.h-world.bounds.y+200});
    return world;
  }
  root.EcologyWorld={create,catalog,profiles,sample,random};
})(globalThis);
