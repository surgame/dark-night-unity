(function (root) {
  "use strict";
  const C = root.CaveWhitebox = {};
  const P = [
    [], [[[0,0],[.5,0],[0,.5]]], [[[.5,0],[1,0],[1,.5]]],
    [[[0,0],[1,0],[1,.5],[0,.5]]], [[[1,.5],[1,1],[.5,1]]],
    [[[0,0],[.5,0],[0,.5]],[[1,.5],[1,1],[.5,1]]],
    [[[.5,0],[1,0],[1,1],[.5,1]]],
    [[[0,0],[1,0],[1,1],[.5,1],[0,.5]]],
    [[[0,.5],[.5,1],[0,1]]], [[[0,0],[.5,0],[.5,1],[0,1]]],
    [[[.5,0],[1,0],[1,.5]],[[0,.5],[.5,1],[0,1]]],
    [[[0,0],[1,0],[1,.5],[.5,1],[0,1]]],
    [[[0,.5],[1,.5],[1,1],[0,1]]],
    [[[0,0],[.5,0],[1,.5],[1,1],[0,1]]],
    [[[0,.5],[.5,0],[1,0],[1,1],[0,1]]],
    [[[0,0],[1,0],[1,1],[0,1]]]
  ];
  const side = (a,b) => a[0] === b[0] && (a[0] === 0 || a[0] === 1) ||
    a[1] === b[1] && (a[1] === 0 || a[1] === 1);
  C.clamp = (x,a,b) => Math.min(b,Math.max(a,x));
  C.mix = (a,b,t) => a+(b-a)*t;
  C.templates = P.map((polygons,mask) => {
    const boundaries = [];
    for (const p of polygons) for (let i=0;i<p.length;i++) {
      const a=p[i],b=p[(i+1)%p.length];
      if (side(a,b)) continue;
      const edge={a,b};
      if (a[0] !== b[0] && a[1] !== b[1]) {
        edge.center=[a[0] === .5 ? b[0] : a[0],a[1] === .5 ? b[1] : a[1]];
        edge.start=Math.atan2(a[1]-edge.center[1],a[0]-edge.center[0]);
        edge.end=Math.atan2(b[1]-edge.center[1],b[0]-edge.center[0]);
        let delta=edge.end-edge.start;
        while (delta > Math.PI) delta-=Math.PI*2;
        while (delta < -Math.PI) delta+=Math.PI*2;
        edge.negative=delta < 0;
      }
      boundaries.push(edge);
    }
    return {mask,polygons,boundaries};
  });
  function segment(ctx,a,b,size) {
    if (side(a,b) || a[0] === b[0] || a[1] === b[1]) {
      ctx.lineTo(b[0]*size,b[1]*size);
      return;
    }
    const cx=a[0] === .5 ? b[0] : a[0],cy=a[1] === .5 ? b[1] : a[1];
    const start=Math.atan2(a[1]-cy,a[0]-cx),end=Math.atan2(b[1]-cy,b[0]-cx);
    let delta=end-start;
    while (delta > Math.PI) delta-=2*Math.PI;
    while (delta < -Math.PI) delta+=2*Math.PI;
    ctx.arc(cx*size,cy*size,.5*size,start,end,delta<0);
  }
  C.paintTile = function (ctx,mask,size,palette) {
    const t=C.templates[mask];
    ctx.beginPath();
    for (const polygon of t.polygons) {
      ctx.moveTo(polygon[0][0]*size,polygon[0][1]*size);
      for (let i=0;i<polygon.length;i++) segment(ctx,polygon[i],polygon[(i+1)%polygon.length],size);
      ctx.closePath();
    }
    ctx.fillStyle=palette.rock;ctx.fill();
    ctx.beginPath();
    for (const e of t.boundaries) {
      ctx.moveTo(e.a[0]*size,e.a[1]*size);
      if (e.center) ctx.arc(e.center[0]*size,e.center[1]*size,size*.5,e.start,e.end,e.negative);
      else ctx.lineTo(e.b[0]*size,e.b[1]*size);
    }
    ctx.strokeStyle=palette.edge;ctx.lineWidth=size*.065;
    ctx.lineCap="round";ctx.lineJoin="round";ctx.stroke();
  };
  // The analytic circle tests match the rendered quarter-circle boundaries.
  C.inside = function (mask,x,y) {
    const circle=(cx,cy)=>(x-cx)**2+(y-cy)**2 <= .25;
    switch (mask) {
      case 0:return false; case 15:return true;
      case 1:return circle(0,0); case 2:return circle(1,0);
      case 4:return circle(1,1); case 8:return circle(0,1);
      case 14:return !circle(0,0); case 13:return !circle(1,0);
      case 11:return !circle(1,1); case 7:return !circle(0,1);
      case 3:return y<=.5; case 12:return y>=.5;
      case 6:return x>=.5; case 9:return x<=.5;
      case 5:return circle(0,0)||circle(1,1);
      case 10:return circle(1,0)||circle(0,1);
      default:return false;
    }
  };
  C.maskAt = function (w,x,y) {
    const i=y*(w.width+1)+x,d=w.data,n=w.width+1;
    return d[i]|d[i+1]<<1|d[i+n+1]<<2|d[i+n]<<3;
  };
  C.solid = function (w,x,y) {
    const ix=Math.floor(x),iy=Math.floor(y);
    if (ix<0 || iy<0 || ix>=w.width || iy>=w.height) return true;
    return C.inside(C.maskAt(w,ix,iy),x-ix,y-iy);
  };
  C.floorAt = function (w,x,from,max=16) {
    let previous=from;
    for (let y=from+.1;y<=from+max;y+=.1) {
      if (C.solid(w,x,y)) {
        let lo=previous,hi=y;
        for (let j=0;j<12;j++) { const mid=(lo+hi)/2; if (C.solid(w,x,mid)) hi=mid; else lo=mid; }
        return lo;
      }
      previous=y;
    }
    return null;
  };
  C.dig = function (world,x,y,radius=1.25) {
    const changed=[],dirty=new Set(),n=world.width+1;
    for (let iy=Math.floor(y-radius);iy<=Math.ceil(y+radius);iy++)
      for (let ix=Math.floor(x-radius);ix<=Math.ceil(x+radius);ix++) {
        if (ix<2 || iy<2 || ix>world.width-2 || iy>world.height-2) continue;
        const index=iy*n+ix;
        if ((ix-x)**2+(iy-y)**2>radius**2 || !world.data[index]) continue;
        world.data[index]=0;changed.push(index);world.cuts.add(index);
        for (const dx of [-1,0]) for (const dy of [-1,0])
          if (ix+dx>=0 && iy+dy>=0 && ix+dx<world.width && iy+dy<world.height)
            dirty.add((iy+dy)*world.width+ix+dx);
      }
    if (changed.length) world.revision++;
    return {changed,dirty:[...dirty]};
  };
})(globalThis);
