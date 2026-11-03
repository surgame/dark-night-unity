(function () {
  "use strict";
  const G=globalThis.CaveProgressionGeometry={};
  G.rounded=function(points,closed=true){
    const n=points.length,corners=points.map((b,i)=>{
      if(!closed&&(i===0||i===n-1))return [b,b,b];
      const a=points[(i+n-1)%n],c=points[(i+1)%n];
      const ab=Math.hypot(b[0]-a[0],b[1]-a[1])||1,bc=Math.hypot(c[0]-b[0],c[1]-b[1])||1;
      const cut=Math.min(.16+(i%4)*.09,ab*.16,bc*.16);
      return [[b[0]-(b[0]-a[0])*cut/ab,b[1]-(b[1]-a[1])*cut/ab],b,
        [b[0]+(c[0]-b[0])*cut/bc,b[1]+(c[1]-b[1])*cut/bc]];
    });
    let d=`M${corners[0][0].join(",")}`;
    corners.forEach(([a,b,c],i)=>{
      if(i)d+=` L${a.join(",")}`;
      d+=` Q${b.join(",")} ${c.join(",")}`;
    });
    return d+(closed?" Z":"");
  };
  G.corridor=function(points,width){
    const parts=[];
    for(let i=1;i<points.length;i++){
      const a=points[i-1],b=points[i],l=Math.hypot(b[0]-a[0],b[1]-a[1]);
      if(!l)continue;
      const nx=-(b[1]-a[1])*width/(l*2),ny=(b[0]-a[0])*width/(l*2);
      parts.push({polygon:[[a[0]+nx,a[1]+ny],[b[0]+nx,b[1]+ny],[b[0]-nx,b[1]-ny],[a[0]-nx,a[1]-ny]]});
    }
    points.forEach(p=>parts.push({circle:[...p,width/2]}));
    return parts;
  };
  G.air=function(m){
    return [
      ...m.rooms.map(r=>({polygon:r.polygon,room:r.id})),
      ...m.links.flatMap(l=>G.corridor(l.points,l.width).map(p=>({...p,link:l.to}))),
      ...G.corridor(m.entryPath,3.4)
    ];
  };
  G.markup=function(part){
    return part.circle?`<circle cx="${part.circle[0]}" cy="${part.circle[1]}" r="${part.circle[2]}"/>`:
      `<path d="${G.rounded(part.polygon)}"/>`;
  };
  G.inside=function(p,part){
    if(part.circle)return Math.hypot(p[0]-part.circle[0],p[1]-part.circle[1])<part.circle[2];
    const poly=part.polygon;
    let yes=false;
    for(let i=0,j=poly.length-1;i<poly.length;j=i++){
      const a=poly[i],b=poly[j];
      if((a[1]>p[1])!==(b[1]>p[1])&&p[0]<(b[0]-a[0])*(p[1]-a[1])/(b[1]-a[1])+a[0])yes=!yes;
    }
    return yes;
  };
}());
