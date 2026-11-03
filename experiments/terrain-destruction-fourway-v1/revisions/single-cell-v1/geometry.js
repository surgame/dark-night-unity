(function(root) {
  "use strict";
  const T = root.TerrainFour = {}, P = [
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
    [[[.5,0],[1,0],[1,1],[0,1],[0,.5]]],
    [[[0,0],[1,0],[1,1],[0,1]]]
  ];
  const sameSide = (a,b) => a[0]===b[0]&&(a[0]===0||a[0]===1) ||
    a[1]===b[1]&&(a[1]===0||a[1]===1);
  function template(mask) {
    const polygons=[], segments=[];
    for(const source of P[mask]) {
      const polygon=[];
      for(let j=0;j<source.length;j++) {
        const a=source[j], b=source[(j+1)%source.length];
        polygon.push(a);
        if(sameSide(a,b)) continue;
        const dx=b[0]-a[0],dy=b[1]-a[1],length=Math.hypot(dx,dy);
        const bend=mask===3||mask===6||mask===9||mask===12 ? .04 : .13;
        const control=[(a[0]+b[0])/2-dy/length*bend,(a[1]+b[1])/2+dx/length*bend];
        const line=[a];
        for(let s=1;s<=6;s++) {
          const t=s/6, u=1-t, p=[u*u*a[0]+2*u*t*control[0]+t*t*b[0],
            u*u*a[1]+2*u*t*control[1]+t*t*b[1]];
          line.push(p); if(s<6) polygon.push(p);
        }
        segments.push(line);
      }
      polygons.push(polygon);
    }
    return {polygons,segments};
  }
  T.templates = Array.from({length:16},(_,m)=>template(m));
  T.hash = (a,b=0,c=0) => {
    let x=Math.imul(a+1,0x45d9f3b)^Math.imul(b+13,0x119de1f3)^c;
    x=Math.imul(x^(x>>>16),0x45d9f3b);return (x^(x>>>16))>>>0;
  };
  T.signature = values => {
    let h=2166136261;for(const x of values)h=Math.imul(h^x,16777619);return (h>>>0).toString(16);
  };
  T.field = function(w=96,h=60,seed=271) {
    const data=new Uint8Array((w+1)*(h+1));
    const ellipse=(x,y,cx,cy,rx,ry)=>(x-cx)**2/rx**2+(y-cy)**2/ry**2;
    for(let y=0;y<=h;y++)for(let x=0;x<=w;x++) {
      const u=x/w,v=y/h;
      const noise=.018*Math.sin(u*57+seed*.01)+.012*Math.sin(v*49+u*19);
      let air=ellipse(u,v,.17,.49,.20,.15+noise)<1 ||
        ellipse(u,v,.49,.47,.22+noise,.26)<1 ||
        ellipse(u,v,.83,.42,.21,.18+noise)<1 ||
        ellipse(u,v,.60,.78,.17+noise,.10)<1 ||
        ellipse(u,v,.68,.63,.052,.20)<1;
      if(ellipse(u,v,.37,.55,.025,.105)<1 || ellipse(u,v,.78,.52,.034,.06)<1)air=false;
      data[y*(w+1)+x]=air?0:1;
    }
    return {w,h,seed,data};
  };
  T.maskAt = function(f,x,y) {
    const i=y*(f.w+1)+x, d=f.data;
    return d[i]|d[i+1]<<1|d[i+f.w+2]<<2|d[i+f.w+1]<<3;
  };
  T.masks = f => Uint8Array.from({length:f.w*f.h},(_,i)=>T.maskAt(f,i%f.w,(i/f.w)|0));
  T.affected = function(f,index) {
    const x=index%(f.w+1),y=(index/(f.w+1))|0,result=[];
    for(const dy of [-1,0])for(const dx of [-1,0]) {
      const a=x+dx,b=y+dy;if(a>=0&&b>=0&&a<f.w&&b<f.h)result.push(b*f.w+a);
    }
    return result;
  };
  T.inside = function(mask,x,y) {
    for(const p of T.templates[mask].polygons) {
      let inside=false;
      for(let i=0,j=p.length-1;i<p.length;j=i++) {
        const a=p[i],b=p[j];
        if((a[1]>y)!==(b[1]>y)&&x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0])inside=!inside;
      }
      if(inside)return true;
    }
    return false;
  };
  T.hit = function(f,x,y) {
    const px=Math.floor(x),py=Math.floor(y);
    if(px<0||py<0||px>=f.w||py>=f.h||!T.inside(T.maskAt(f,px,py),x-px,y-py))return -1;
    const near=[];
    for(let dy=0;dy<=1;dy++)for(let dx=0;dx<=1;dx++) {
      const nx=px+dx,ny=py+dy,index=ny*(f.w+1)+nx;
      if(f.data[index])near.push({index,d:(x-nx)**2+(y-ny)**2});
    }
    near.sort((a,b)=>a.d-b.d);return near[0]?.index??-1;
  };
  T.clone = f => ({...f,data:f.data.slice()});
  T.chunkSize = 8;
  T.chunkKey = (x,y,w) => ((y/T.chunkSize)|0)*Math.ceil(w/T.chunkSize)+((x/T.chunkSize)|0);
  T.chunkRect = function(key,w,h) {
    const nx=Math.ceil(w/T.chunkSize),x=key%nx*T.chunkSize,y=((key/nx)|0)*T.chunkSize;
    return {x,y,w:Math.min(T.chunkSize,w-x),h:Math.min(T.chunkSize,h-y)};
  };
})(globalThis);
