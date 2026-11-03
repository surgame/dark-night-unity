(function (root) {
  "use strict";
  const api = root.FlatCave = root.FlatCave || {};
  const U = 128;
  const canvas = (w, h) => Object.assign(document.createElement("canvas"), {width:w, height:h});
  // Shared corner rounding comes from the previous art kit, applied before chunking.
  function smooth(contours) {
    return contours.map(contour => {
      const input = contour.closed ? contour.points.slice(0,-1) : contour.points;
      const result = [], n = input.length;
      for (let i=0;i<n;i++) {
        const p=input[i], prev=input[(i+n-1)%n], next=input[(i+1)%n];
        const a={x:p.x+(prev.x-p.x)*.4,y:p.y+(prev.y-p.y)*.4};
        const b={x:p.x+(next.x-p.x)*.4,y:p.y+(next.y-p.y)*.4};
        for (let j=0;j<=12;j++) {
          const t=j/12, s=1-t;
          result.push({x:(s*s*a.x+2*s*t*p.x+t*t*b.x)*U,y:(s*s*a.y+2*s*t*p.y+t*t*b.y)*U});
        }
      }
      result.push({...result[0]});
      return result;
    });
  }
  function build(map) {
    if (map.visual) return map.visual;
    const contours=smooth(map.geometry.contours), solid=new Path2D(), outline=new Path2D();
    solid.rect(0,0,map.width*U,map.height*U);
    for (const points of contours) {
      for (const path of [solid,outline]) {
        points.forEach((p,i)=>i?path.lineTo(p.x,p.y):path.moveTo(p.x,p.y));
        path.closePath();
      }
    }
    const query=canvas(1,1).getContext("2d"), edges=[];
    contours.forEach(points=>{
      for(let i=1;i<points.length;i++) {
        const a=points[i-1], b=points[i], len=Math.hypot(b.x-a.x,b.y-a.y);
        if(len<.001)continue;
        let nx=(b.y-a.y)/len, ny=(a.x-b.x)/len;
        if(query.isPointInPath(solid,(a.x+b.x)/2+nx,(a.y+b.y)/2+ny,"evenodd")){nx=-nx;ny=-ny;}
        edges.push({a,b,nx,ny});
      }
    });
    map.visual={solid,outline,contours,edges};
    return map.visual;
  }
  function floorAt(map,x,y) {
    const candidates=build(map).edges.filter(s=>s.ny<-.45&&Math.min(s.a.x,s.b.x)<=x&&Math.max(s.a.x,s.b.x)>=x)
      .map(s=>s.a.y+(s.b.y-s.a.y)*(x-s.a.x)/(s.b.x-s.a.x)).filter(v=>v>=y);
    return candidates.length?Math.min(...candidates):null;
  }
  function tilePolygons(tile) {
    const corners=[[0,0],[1,0],[1,1],[0,1]], nodes=new Map(), links=new Map();
    function link(a,b) {
      if(!links.has(a))links.set(a,[]);
      if(!links.has(b))links.set(b,[]);
      links.get(a).push(b);links.get(b).push(a);
    }
    tile.segments.flat().forEach(hit=>nodes.set("e"+hit.edge,hit.point));
    corners.forEach((p,i)=>{if(tile.values[i]>0)nodes.set("c"+i,p);});
    // Catalog bottom edge is index 2, left edge index 3, independent of traversal.
    for(let i=0;i<4;i++) {
      const j=(i+1)%4,a=tile.values[i]>0,b=tile.values[j]>0;
      if(a&&b)link("c"+i,"c"+j);
      else if(a!==b)link(a?"c"+i:"c"+j,"e"+i);
    }
    tile.segments.forEach(s=>link("e"+s[0].edge,"e"+s[1].edge));
    const seen=new Set(), result=[];
    for(const start of links.keys()) {
      if(seen.has(start))continue;
      let at=start, prev=null;const points=[];
      while(!seen.has(at)){seen.add(at);points.push(nodes.get(at));const next=links.get(at).find(n=>n!==prev);prev=at;at=next;}
      result.push(points);
    }
    return result;
  }
  Object.assign(api,{U,canvas,smooth,build,floorAt,tilePolygons});
})(globalThis);
