(function () {
  "use strict";
  const P=globalThis.CaveBlockDesign;
  const originalShape=P.shape,originalBranch=P.branch,originalContents=P.contents;
  P.kind="cave-macroblock-elevation-v1";
  P.deltas={M01:3,M02:2,M03:7,M04:8,M05:6,M06:6,M07:1.5,M08:2,
    M09:9,M10:-3,M11:-7,M12:4,M13:-6,M14:3,M15:5,M16:0};
  P.amount=1;
  const stops=[[0,0],[.08,0],[.24,.23],[.34,.23],[.51,.58],[.62,.58],[.88,1],[1,1]];
  P.offset=function(m,x,amount=P.amount) {
    const t=Math.max(0,Math.min(1,x/m.w));
    let i=1;
    while(i<stops.length-1 && stops[i][0]<t)i++;
    const a=stops[i-1],b=stops[i],v=(t-a[0])/(b[0]-a[0]);
    return (P.deltas[m.id]||0)*amount*(a[1]+(b[1]-a[1])*v);
  };
  const shifted=(m,line)=>line.map(([x,y])=>[x,y+P.offset(m,x)]);
  P.shape=m=>{
    const s=originalShape(m);
    return {roof:shifted(m,s.roof),floor:shifted(m,s.floor)};
  };
  P.branch=m=>{
    const b=originalBranch(m);
    return b?{roof:shifted(m,b.roof),floor:shifted(m,b.floor)}:null;
  };
  P.contents=m=>originalContents(m).map(c=>({...c,y:c.y+P.offset(m,c.x)}));
  P.floorY=(m,x)=>{
    const pts=P.shape(m).floor;
    let i=1;
    while(i<pts.length-1 && pts[i][0]<x)i++;
    const a=pts[i-1],b=pts[i];
    return a[1]+(b[1]-a[1])*(x-a[0])/(b[0]-a[0]||1);
  };
  P.localBounds=m=>{
    const s=P.shape(m),b=P.branch(m);
    const all=[...s.roof,...s.floor,...(b?[...b.roof,...b.floor]:[])];
    return {x:0,y:Math.min(0,...all.map(p=>p[1]))-1,w:m.w,h:Math.max(m.h,...all.map(p=>p[1]))+2};
  };
  const main=["M01","M02","M03","M05","M04","M11","M09","M12"];
  P.layouts=function(amount=P.amount) {
    const prev=P.amount;P.amount=amount;
    let x=0,floor=12;
    const nodes=[];
    main.forEach((id,i)=>{
      const m=P.byId(id),s=P.shape(m),y=floor-s.floor[0][1];
      nodes.push({id,x,y,main:true});
      floor=y+s.floor.at(-1)[1]+(i===4?-2:2)*amount;
      x+=m.w+(i===3||i===4||i===6?20:12);
    });
    const edges=main.slice(1).map((id,i)=>({from:main[i],to:id,port:"main",kind:"坡段"}));
    for(const [from,to,gap,rise] of [["M05","M08",22,8],["M04","M10",24,10]]) {
      const a=nodes.find(n=>n.id===from),ma=P.byId(from),mb=P.byId(to);
      const branch=P.branch(ma),s=P.shape(mb);
      nodes.push({id:to,x:a.x+ma.w+gap,y:a.y+branch.floor.at(-1)[1]-s.floor[0][1]-rise*amount,main:false});
      edges.push({from,to,port:"branch",kind:"上折侧支"});
    }
    P.amount=prev;
    return {nodes,edges};
  };
  P.connection=function(a,b,type) {
    const ma=P.byId(a.id),mb=P.byId(b.id);
    const source=type==="branch"?P.branch(ma):P.shape(ma),target=P.shape(mb);
    const start=[a.x+ma.w,a.y+source.floor.at(-1)[1]];
    const end=[b.x,b.y+target.floor[0][1]];
    const high=type==="branch"?1.5:2.5;
    const span=end[0]-start[0],dy=end[1]-start[1];
    // Flat end bands preserve horizontal interface tangents on both modules.
    const floor=[start,[start[0]+1,start[1]],
      [start[0]+span*.34,start[1]+dy*.36],[start[0]+span*.5,start[1]+dy*.36],
      [end[0]-1,end[1]],end];
    const endHeight=2.5;
    const roof=floor.map(([px,py])=>{
      const t=(px-start[0])/(span||1);
      return [px,py-(high+(endHeight-high)*t)];
    });
    return {roof,floor,start,end};
  };
}());
