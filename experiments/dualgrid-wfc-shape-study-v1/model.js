(function(root){
  "use strict";
  const source=root.CaveWB,api=root.ShapeStudy={};
  const W=48,H=28,clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
  const corners=[[0,0],[1,0],[1,1],[0,1]];
  function line(points,x){
    for(let i=1;i<points.length;i++)if(x<=points[i][0]){
      const a=points[i-1],b=points[i],t=(x-a[0])/(b[0]-a[0]);
      return a[1]+(b[1]-a[1])*t;
    }
    return points.at(-1)[1];
  }
  function makePlan(seed,scene){
    const rng=source.random(seed),shift=()=>Math.round((rng()-.5)*2);
    let floor,roof,signedDistance,heroX=12,focus={x:28,y:18},pocket=null,rooms=[];
    if(scene==="network"){
      const old=source.makePlan(seed,W,H);signedDistance=old.signedDistance;
      rooms=old.rooms;
      heroX=old.nodes[1]?.x||10;focus={x:heroX+7,y:old.nodes[1]?.y||14};
    }else{
      floor=scene==="shoulder"
        ?[[0,14],[7,14],[12,18],[18,18],[23,20],[28,20],[34,16],[40,16],[48,13]]
        :[[0,11],[5,14],[10,16],[15,16],[19,19],[24,22],[28,23],[33,23],[37,19],[41,14],[48,11]];
      roof=scene==="shoulder"
        ?[[0,8],[7,6],[13,7],[19,12],[25,13],[32,10],[38,5],[48,7]]
        :[[0,6],[7,3],[14,3],[21,5],[27,9],[31,10],[35,8],[41,4],[48,6]];
      floor=floor.map((p,i)=>[p[0],p[1]+(i>0&&i<floor.length-1?shift():0)]);
      roof=roof.map((p,i)=>[p[0],p[1]+(i>0&&i<roof.length-1?shift():0)]);
      pocket=scene==="basin"?{x:42,y:16,rx:3.6,ry:2.1}:null;
      signedDistance=(x,y)=>{
        const main=Math.max(line(roof,x)-y,y-line(floor,x));
        const side=pocket?(Math.hypot((x-pocket.x)/pocket.rx,(y-pocket.y)/pocket.ry)-1)*pocket.ry:999;
        return Math.max(Math.min(main,side),1.2-x,x-W+1.2);
      };
    }
    const target=new Float32Array((W+1)*(H+1)),allowed=new Uint8Array(target.length);
    for(let y=0;y<=H;y++)for(let x=0;x<=W;x++){
      const i=y*(W+1)+x,d=signedDistance(x,y),solid=d>=0;
      target[i]=clamp(d*2.8,-3,3);
      // Both methods keep identical corner occupancy; WFC chooses edge offsets only.
      allowed[i]=Math.abs(d)>.95?(solid?8:1):(solid?12:3);
    }
    return {seed:seed>>>0,scene,width:W,height:H,target,allowed,signedDistance,
      floor,roof,heroX,focus,pocket,water:scene==="basin"?{x:30,level:21.8}:null,
      markers:floor?[{x:heroX,y:line(floor,heroX),label:"落脚面",kind:"stand"},
        {x:21,y:line(floor,21)+1,label:"可变岩肩",kind:"break"},
        {x:44,y:line(floor,44)+6,label:"矿层位",kind:"ore"},
        ...(pocket?[{x:pocket.x,y:pocket.y,label:"侧窝",kind:"pocket"},
          {x:30,y:11,label:"瀑布位",kind:"water"},{x:30,y:22.4,label:"浅池",kind:"water"}]:[])]:
        rooms.slice(1,4).map((room,i)=>({x:room.x,y:room.y,label:["主厅","侧窝","矿窝"][i],kind:"pocket"}))};
  }
  function idFromValues(values){
    return values.reduce((id,v,i)=>id|(source.levels.indexOf(v)<<(i*2)),0);
  }
  function assemble(plan,samples,stats){
    const tiles=new Uint16Array(W*H);
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){
      const values=corners.map(([dx,dy])=>samples[(y+dy)*(W+1)+x+dx]);
      tiles[y*W+x]=idFromValues(values);
    }
    const map={width:W,height:H,seed:plan.seed,plan,tiles,stats:stats||null};
    map.check=source.verify(map);map.geometry=source.extractContours(map);
    if(map.check.mismatches||map.check.cornerConflicts||map.geometry.nonManifold)
      throw new Error("Shared edge or closed contour contract failed.");
    return map;
  }
  function dual(plan){
    const samples=Int8Array.from(plan.target,v=>v>=0?1:-1);
    return assemble(plan,samples);
  }
  async function generate(seed,scene,options={}){
    const plan=makePlan(seed,scene),a=dual(plan);
    const b=await source.solve(plan,options);
    const sa=a.check.samples,sb=b.check.samples;
    if(sa.some((v,i)=>(v>0)!==(sb[i]>0)))throw new Error("Comparison changed corner occupancy.");
    const hybridPlan=api.makeMacroPlan(plan),hybridSource={stats:hybridPlan.macroLayout.stats};
    const hybridSamples=Int8Array.from(hybridPlan.target);
    const hybridBase=assemble(hybridPlan,Int8Array.from(hybridSamples,v=>v>0?1:-1));
    const pair={plan,dual:a,wfc:b,hybridPlan,hybridSource,hybridSamples,hybridBase,
      baseSamples:{dual:Int8Array.from(sa),wfc:Int8Array.from(sb),hybrid:Int8Array.from(hybridSamples)}};
    pair.hybrid=api.makeHybrid(pair);
    return pair;
  }
  function dig(pair,enabled){
    if(!enabled){
      pair.dual=assemble(pair.plan,pair.baseSamples.dual);
      pair.wfc=assemble(pair.plan,pair.baseSamples.wfc,pair.wfc.stats);
      pair.hybridSamples=Int8Array.from(pair.baseSamples.hybrid);
      pair.hybridBase=assemble(pair.hybridPlan,Int8Array.from(pair.hybridSamples,v=>v>0?1:-1));
      pair.hybrid=api.makeHybrid(pair);return;
    }
    const x=19,ground=Math.max(groundAt(pair.dual,x+.01),groundAt(pair.hybridBase,x+.01));
    let y=clamp(Math.ceil(ground+.15),2,H-2);
    while(y<H-2&&(pair.baseSamples.dual[y*(W+1)+x]<0||pair.baseSamples.hybrid[y*(W+1)+x]<0))y++;
    pair.digPoint={x,y};
    for(const key of ["dual","wfc"]){
      const samples=Int8Array.from(pair.baseSamples[key]);samples[y*(W+1)+x]=-1;
      const before=pair[key],next=assemble(pair.plan,samples,before.stats);
      next.changed=Array.from(next.tiles,(id,i)=>id!==before.tiles[i]?i:-1).filter(i=>i>=0);
      pair[key]=next;
    }
    const original=pair.hybrid;
    pair.hybridSamples=Int8Array.from(pair.baseSamples.hybrid);pair.hybridSamples[y*(W+1)+x]=-1;
    pair.hybridBase=assemble(pair.hybridPlan,Int8Array.from(pair.hybridSamples,v=>v>0?1:-1));
    pair.hybrid=api.makeHybrid(pair);
    pair.hybrid.changed=Array.from(pair.hybrid.flags,(flag,i)=>
      flag!==original.flags[i]||pair.hybrid.cells[i]!==original.cells[i]?i:-1).filter(i=>i>=0);
  }
  function groundAt(map,x){
    const expected=map.plan.floor?line(map.plan.floor,x):H*.65,ys=[];
    const segments=map.hybridEdges||map.geometry.contours.flatMap(contour=>
      contour.points.slice(1).map((p,i)=>[contour.points[i],p]));
    segments.forEach(([a,b])=>{
        if(x>=Math.min(a.x,b.x)&&x<=Math.max(a.x,b.x)&&Math.abs(b.x-a.x)>1e-6){
          ys.push(a.y+(b.y-a.y)*(x-a.x)/(b.x-a.x));
        }
    });
    if(!ys.length)return expected;
    if(map.plan.scene==="network"){
      const cavity=[];for(let y=0;y<H;y++)if(map.plan.signedDistance(x,y)<0)cavity.push(y);
      const centre=cavity.length?cavity[Math.floor(cavity.length/2)]:expected;
      return ys.filter(y=>y>centre).sort((a,b)=>a-b)[0]??expected;
    }
    return ys.sort((a,b)=>Math.abs(a-expected)-Math.abs(b-expected))[0];
  }
  function toMask(mask){return(mask&3)|((mask&4)<<1)|((mask&8)>>1);}
  function tile16(mask){
    return source.catalog[idFromValues([mask&1?1:-1,mask&2?1:-1,mask&8?1:-1,mask&4?1:-1])];
  }
  function polygons(tile){
    if(!tile.mask)return [];
    const polygon=[];
    corners.forEach((p,i)=>{
      if(tile.values[i]>0)polygon.push(p);
      const j=(i+1)%4;
      if((tile.values[i]>0)!==(tile.values[j]>0)){
        const t=tile.values[i]/(tile.values[i]-tile.values[j]);
        polygon.push([p[0]+(corners[j][0]-p[0])*t,p[1]+(corners[j][1]-p[1])*t]);
      }
    });
    if(tile.mask!==5&&tile.mask!==10)return[polygon];
    const positives=[0,1,2,3].filter(i=>tile.values[i]>0);
    const isolated=positives.every(i=>{
      const incident=[[3,0],[0,1],[1,2],[2,3]][i];
      return tile.segments.some(s=>incident.every(edge=>s.some(h=>h.edge===edge)));
    });
    return isolated?positives.map(i=>{
      const incident=[[3,0],[0,1],[1,2],[2,3]][i];
      return[corners[i],...incident.map(edge=>tile.segments.flat().find(hit=>hit.edge===edge).point)];
    }):[polygon];
  }
  function details(map,index){
    const tile=source.catalog[map.tiles[index]],positions=tile.segments.flat().map(hit=>{
      const horizontal=hit.edge===0||hit.edge===2;
      return horizontal?hit.point[0]:hit.point[1];
    });
    return {index,x:index%W,y:Math.floor(index/W),id:tile.id,mask:toMask(tile.mask),
      values:[tile.values[0],tile.values[1],tile.values[3],tile.values[2]],positions:[...new Set(positions)]};
  }
  function metrics(map){
    if(map.hybridEdges)return{...metrics(map.base),slopeCells:Array.from(map.flags).filter(Boolean).length,
      slopeTypes:[...new Set(map.flags)].filter(Boolean).sort((a,b)=>a-b),
      segments:map.hybridEdges.length,changed:map.changed?.length||0};
    const active=new Set(),offsets=new Set();
    map.tiles.forEach((id,i)=>{
      const t=source.catalog[id];if(t.mask!==0&&t.mask!==15)active.add(id);
      details(map,i).positions.forEach(v=>offsets.add(v));
    });
    return {boundaryConfigurations:active.size,positions:[...offsets].sort(),segments:map.geometry.segments,
      mismatches:map.check.mismatches,cornerConflicts:map.check.cornerConflicts,
      nonManifold:map.geometry.nonManifold,changed:map.changed?.length||0};
  }
  Object.assign(api,{W,H,makePlan,generate,dual,assemble,dig,groundAt,toMask,tile16,polygons,details,metrics});
})(globalThis);
