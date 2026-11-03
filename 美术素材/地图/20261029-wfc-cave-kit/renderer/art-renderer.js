(function (root) {
  "use strict";
  const kit = root.CaveArt = root.CaveArt || {};
  const U = 128;
  function canvas(w, h) {
    const result = document.createElement("canvas");
    result.width = w; result.height = h;
    return result;
  }
  function reflected(value, size) {
    const t = ((value % (size * 2)) + size * 2) % (size * 2);
    return t < size ? t : size * 2 - t - .001;
  }
  function sample(map, x, y) {
    x = Math.max(0, Math.min(map.width - .00001, x));
    y = Math.max(0, Math.min(map.height - .00001, y));
    const ix = Math.floor(x), iy = Math.floor(y), u = x - ix, v = y - iy;
    const s = CaveWB.catalog[map.tiles[iy * map.width + ix]].values;
    return s[0] * (1-u) * (1-v) + s[1]*u*(1-v) + s[2]*u*v + s[3]*(1-u)*v;
  }
  function smoothContours(contours) {
    return contours.map(contour=>{
      const input=contour.closed?contour.points.slice(0,-1):contour.points, count=input.length,points=[];
      const at=i=>input[(i+count)%count];
      for(let i=0;i<count;i++) {
        const before=at(i-1),corner=at(i),after=at(i+1);
        const a={x:corner.x+(before.x-corner.x)*.4,y:corner.y+(before.y-corner.y)*.4};
        const b={x:corner.x+(after.x-corner.x)*.4,y:corner.y+(after.y-corner.y)*.4};
        // Round the shared outline once, before rasterizing any chunk. Fitting a
        // cubic through every old corner caused near-vertical folds on short ramps.
        for(let j=0;j<=12;j++) {
          const t=j/12,s=1-t;
          points.push({x:s*s*a.x+2*s*t*corner.x+t*t*b.x,y:s*s*a.y+2*s*t*corner.y+t*t*b.y});
        }
      }
      points.push({...points[0]});return {closed:true,points};
    });
  }
  function pathFor(map) {
    const path = new Path2D();
    path.rect(0, 0, map.width * U, map.height * U);
    map.artContours=map.artContours||smoothContours(map.geometry.contours);
    for (const contour of map.artContours) {
      contour.points.forEach((p, i) => i ? path.lineTo(p.x*U,p.y*U) : path.moveTo(p.x*U,p.y*U));
      path.closePath();
    }
    return path;
  }
  function periodicMaterial(image,size=768,overlap=96) {
    const expanded=canvas(size+overlap,size+overlap),ec=expanded.getContext("2d");
    ec.drawImage(image,0,0,expanded.width,expanded.height);
    const horizontal=canvas(size,size+overlap),hc=horizontal.getContext("2d");
    hc.drawImage(expanded,0,0);
    const strip=canvas(overlap,size+overlap),sc=strip.getContext("2d");
    sc.drawImage(expanded,size,0,overlap,size+overlap,0,0,overlap,size+overlap);
    sc.globalCompositeOperation="destination-in";
    let fade=sc.createLinearGradient(0,0,overlap,0);fade.addColorStop(0,"#000");fade.addColorStop(1,"transparent");
    sc.fillStyle=fade;sc.fillRect(0,0,strip.width,strip.height);hc.drawImage(strip,0,0);
    const result=canvas(size,size),rc=result.getContext("2d"),top=canvas(size,overlap),tc=top.getContext("2d");
    rc.drawImage(horizontal,0,0);tc.drawImage(horizontal,0,size,size,overlap,0,0,size,overlap);
    tc.globalCompositeOperation="destination-in";
    fade=tc.createLinearGradient(0,0,0,overlap);fade.addColorStop(0,"#000");fade.addColorStop(1,"transparent");
    tc.fillStyle=fade;tc.fillRect(0,0,top.width,top.height);rc.drawImage(top,0,0);
    return result;
  }
  function prepare(images) {
    const rock = periodicMaterial(images.rock);
    const floor = canvas(images.floor.width, images.floor.height);
    const fc = floor.getContext("2d");
    fc.drawImage(images.floor,0,0);
    fc.globalCompositeOperation="destination-in";
    const fade=fc.createLinearGradient(0,740,0,1024);
    fade.addColorStop(0,"rgba(0,0,0,1)"); fade.addColorStop(1,"rgba(0,0,0,0)");
    fc.fillStyle=fade; fc.fillRect(0,0,floor.width,floor.height);
    const ceiling = canvas(images.ceiling.width,images.ceiling.height);
    const cc=ceiling.getContext("2d"); cc.drawImage(images.ceiling,0,0);
    const pixels=cc.getImageData(0,0,ceiling.width,ceiling.height).data;
    const ceilingAnchors=[];
    for(let x=0;x<ceiling.width;x++) {
      let edge=100;
      while(edge<390 && pixels[(edge*ceiling.width+x)*4+3]>128) edge++;
      ceilingAnchors.push(edge);
    }
    const normalized=canvas(ceiling.width,500),nc=normalized.getContext("2d");
    for(let x=0;x<ceiling.width;x++) nc.drawImage(ceiling,x,0,1,ceiling.height,x,160-ceilingAnchors[x],1,ceiling.height);
    nc.globalCompositeOperation="destination-in";
    const roofFade=nc.createLinearGradient(0,50,0,160);
    roofFade.addColorStop(0,"transparent");roofFade.addColorStop(1,"#000");
    nc.fillStyle=roofFade;nc.fillRect(0,0,normalized.width,normalized.height);
    return {images,rock,floor,ceiling:normalized,ceilingAnchors:ceilingAnchors.map(()=>160)};
  }
  function drawBackdrop(ctx, art, box) {
    const img=art.images.backdrop, w=2048, h=1365.333;
    const x0=Math.floor(box.x/w), x1=Math.ceil((box.x+box.w)/w);
    const y0=Math.floor(box.y/h), y1=Math.ceil((box.y+box.h)/h);
    for(let y=y0;y<y1;y++) for(let x=x0;x<x1;x++) {
      ctx.save(); ctx.translate((x+(x%2!==0?1:0))*w,(y+(y%2!==0?1:0))*h);
      ctx.scale(x%2!==0?-1:1,y%2!==0?-1:1);
      ctx.drawImage(img,0,0,w,h);ctx.restore();
    }
  }
  function edges(map) {
    const result=[];
    const query=canvas(1,1).getContext("2d"),path=map.artPath||pathFor(map);
    map.artContours.forEach(contour=>{
      for(let i=1;i<contour.points.length;i++) {
        let a=contour.points[i-1],b=contour.points[i];
        const len=Math.hypot(b.x-a.x,b.y-a.y);
        let nx=(b.y-a.y)/len, ny=(a.x-b.x)/len;
        if(query.isPointInPath(path,((a.x+b.x)/2+nx*.01)*U,((a.y+b.y)/2+ny*.01)*U,"evenodd")) {nx=-nx;ny=-ny;}
        result.push({a:{x:a.x*U,y:a.y*U},b:{x:b.x*U,y:b.y*U},nx,ny});
      }
    });
    return result;
  }
  function bands(ctx, art, segments, path, box) {
    const visible=segments.filter(s=>Math.max(s.a.x,s.b.x)>=box.x-180 && Math.min(s.a.x,s.b.x)<=box.x+box.w+180 &&
      Math.max(s.a.y,s.b.y)>=box.y-200 && Math.min(s.a.y,s.b.y)<=box.y+box.h+200);
    // The same world X phase is used on both sides of every WFC boundary.
    for(const pass of ["inside","outside"]) {
      ctx.save();
      if(pass==="inside") ctx.clip(path,"evenodd");
      for(const s of visible) {
        if(Math.abs(s.ny)<.26) continue;
        let a=s.a,b=s.b;if(a.x>b.x) [a,b]=[b,a];
        if(b.x-a.x<.01) continue;
        const isFloor=s.ny<0, image=isFloor?art.floor:art.ceiling;
        const scale=1/3, start=Math.max(a.x,box.x-3), end=Math.min(b.x,box.x+box.w+3);
        ctx.globalAlpha=Math.min(1,Math.max(0,(Math.abs(s.ny)-.26)/.3));
        for(let x=Math.ceil((start-1)/2)*2;x+1<end;x+=2) {
          const dx=2, mid=x+1;
          const y=a.y+(b.y-a.y)*(mid-a.x)/(b.x-a.x);
          const sx=reflected(mid/scale,image.width), sourceX=Math.min(image.width-2,Math.floor(sx));
          const anchor=isFloor?560:art.ceilingAnchors[sourceX];
          const verticalScale=isFloor?scale*(1+.15*Math.sin(mid/373)+.08*Math.sin(mid/127)):scale;
          let sy,sh;
          if(isFloor) {sy=pass==="outside"?400:560; sh=pass==="outside"?160:464;}
          else {sy=pass==="outside"?anchor:Math.max(0,anchor-110);sh=pass==="outside"?Math.min(310,image.height-anchor):Math.min(anchor,110);}
          ctx.drawImage(image,sourceX,sy,Math.min(image.width-sourceX,dx/scale),sh,x,y+(sy-anchor)*verticalScale,dx+.01,sh*verticalScale);
        }
      }
      ctx.restore();
    }
    ctx.save();ctx.clip(path,"evenodd");
    for(const s of visible) {
      if(Math.abs(s.ny)>.52) continue;
      ctx.beginPath();ctx.moveTo(s.a.x,s.a.y);ctx.lineTo(s.b.x,s.b.y);
      ctx.strokeStyle="rgba(10,17,14,.66)";ctx.lineWidth=12;ctx.lineJoin="round";ctx.stroke();
      ctx.beginPath();ctx.moveTo(s.a.x-s.nx*9,s.a.y-s.ny*9);ctx.lineTo(s.b.x-s.nx*9,s.b.y-s.ny*9);
      ctx.strokeStyle="rgba(144,137,70,.38)";ctx.lineWidth=5;ctx.stroke();
    }
    ctx.restore();
  }
  function decorations(ctx,art,map,segments,box) {
    for(let i=0;i<segments.length;i++) {
      const s=segments[i];
      if(s.ny>-.55 || (i*17+map.seed)%277!==0)continue;
      const x=(s.a.x+s.b.x)/2,y=(s.a.y+s.b.y)/2;
      if(x<box.x-120||x>box.x+box.w+120||y<box.y-120||y>box.y+box.h+120)continue;
      const img=art.images.ore,half=img.width/2, variant=i%4;
      ctx.drawImage(img,(variant%2)*half,Math.floor(variant/2)*half,half,half,x-42,y-75,100,100);
    }
  }
  function draw(ctx, art, map, box, width, height, options={}) {
    const scale=width/box.w;
    ctx.save();ctx.clearRect(0,0,width,height);ctx.scale(scale,scale);ctx.translate(-box.x,-box.y);
    const path=map.artPath||(map.artPath=pathFor(map));
    const segments=map.artEdges||(map.artEdges=edges(map));
    if(!options.transparent)drawBackdrop(ctx,art,box);
    ctx.fillStyle=ctx.createPattern(art.rock,"repeat");ctx.fill(path,"evenodd");
    ctx.fillStyle="rgba(8,14,12,.28)";ctx.fill(path,"evenodd");
    if(!options.flat)bands(ctx,art,segments,path,box);
    if(!options.noOre && !options.flat)decorations(ctx,art,map,segments,box);
    if(options.hero) {
      const node=map.plan.nodes[Math.min(options.room||0,map.plan.nodes.length-1)], x=node.x*U;
      const candidates=segments.filter(s=>s.ny<-.5&&Math.min(s.a.x,s.b.x)<=x&&Math.max(s.a.x,s.b.x)>=x)
        .map(s=>s.a.y+(s.b.y-s.a.y)*(x-s.a.x)/(s.b.x-s.a.x)).filter(y=>y>=node.y*U).sort((a,b)=>a-b);
      if(candidates.length) {
        const img=art.images.hero,h=58,w=h*img.width/img.height;
        ctx.drawImage(img,x-w/2,candidates[0]-h,w,h);
      }
    }
    ctx.restore();
  }
  function tilePolygons(tile) {
    const corners=[[0,0],[1,0],[1,1],[0,1]], nodes=new Map(), links=new Map();
    function link(a,b) {if(!links.has(a))links.set(a,[]);if(!links.has(b))links.set(b,[]);links.get(a).push(b);links.get(b).push(a);}
    const borderEdges=[0,1,2,3];
    tile.segments.flat().forEach(hit=>nodes.set("e"+hit.edge,hit.point));
    corners.forEach((point,i)=>{if(tile.values[i]>0)nodes.set("c"+i,point);});
    for(let i=0;i<4;i++) {
      const j=(i+1)%4, a=tile.values[i]>0,b=tile.values[j]>0;
      if(a&&b)link("c"+i,"c"+j);
      else if(a!==b)link(a?"c"+i:"c"+j,"e"+borderEdges[i]);
    }
    tile.segments.forEach(segment=>link("e"+segment[0].edge,"e"+segment[1].edge));
    const seen=new Set(),polygons=[];
    for(const start of links.keys()) {
      if(seen.has(start))continue;
      const points=[];let prev=null,at=start;
      while(!seen.has(at)) {seen.add(at);points.push(nodes.get(at));const next=links.get(at).find(n=>n!==prev);prev=at;at=next;}
      polygons.push(points);
    }
    return polygons;
  }
  function tileCanvas(art,id,size=256,paint=true) {
    const output=canvas(size,size),ctx=output.getContext("2d"),path=new Path2D();
    tilePolygons(CaveWB.catalog[id]).forEach(points=>{
      points.forEach((p,i)=>i?path.lineTo(p[0]*size,p[1]*size):path.moveTo(p[0]*size,p[1]*size));path.closePath();
    });
    if(paint) {
      ctx.save();ctx.scale(size/U,size/U);ctx.fillStyle=ctx.createPattern(art.rock,"repeat");
      const small=new Path2D();
      tilePolygons(CaveWB.catalog[id]).forEach(points=>{points.forEach((p,i)=>i?small.lineTo(p[0]*U,p[1]*U):small.moveTo(p[0]*U,p[1]*U));small.closePath();});
      ctx.fill(small);ctx.restore();
    } else {ctx.fillStyle="#ffffff";ctx.fill(path);}
    return output;
  }
  Object.assign(kit,{U,canvas,prepare,draw,edges,tilePolygons,tileCanvas,smoothContours});
})(globalThis);
