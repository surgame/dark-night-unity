(function(root) {
  "use strict";
  const api=root.FlatCave;
  function periodic(image,size) {
    const overlap=80,expanded=api.canvas(size+overlap,size+overlap),ec=expanded.getContext("2d");
    ec.drawImage(image,0,0,expanded.width,expanded.height);
    const horizontal=api.canvas(size,size+overlap),hc=horizontal.getContext("2d");
    hc.drawImage(expanded,0,0);
    const strip=api.canvas(overlap,size+overlap),sc=strip.getContext("2d");
    sc.drawImage(expanded,size,0,overlap,size+overlap,0,0,overlap,size+overlap);
    sc.globalCompositeOperation="destination-in";
    let fade=sc.createLinearGradient(0,0,overlap,0);fade.addColorStop(0,"#000");fade.addColorStop(1,"transparent");
    sc.fillStyle=fade;sc.fillRect(0,0,strip.width,strip.height);hc.drawImage(strip,0,0);
    const result=api.canvas(size,size),rc=result.getContext("2d"),top=api.canvas(size,overlap),tc=top.getContext("2d");
    rc.drawImage(horizontal,0,0);tc.drawImage(horizontal,0,size,size,overlap,0,0,size,overlap);
    tc.globalCompositeOperation="destination-in";
    fade=tc.createLinearGradient(0,0,0,overlap);fade.addColorStop(0,"#000");fade.addColorStop(1,"transparent");
    tc.fillStyle=fade;tc.fillRect(0,0,top.width,top.height);rc.drawImage(top,0,0);
    return result;
  }
  function prepare(images) {
    return {images,rock:periodic(images.rock,768),surfaces:api.prepareSurface(images)};
  }
  function backdrop(ctx,art,box) {
    const img=art.images.backdrop,w=2200,h=w*img.height/img.width;
    for(let y=Math.floor(box.y/h);y<=Math.floor((box.y+box.h)/h);y++)
      for(let x=Math.floor(box.x/w);x<=Math.floor((box.x+box.w)/w);x++) {
        ctx.save();ctx.translate((x+(x%2?1:0))*w,(y+(y%2?1:0))*h);
        ctx.scale(x%2?-1:1,y%2?-1:1);ctx.drawImage(img,0,0,w,h);ctx.restore();
      }
  }
  const patchAt=x=>Math.sin(x/117)+.52*Math.sin(x/43)+.28*Math.cos(x/251);
  function edges(ctx,visual,box) {
    const candidates=visual.edges.filter(s=>Math.max(s.a.x,s.b.x)>box.x-50&&Math.min(s.a.x,s.b.x)<box.x+box.w+50&&
      Math.max(s.a.y,s.b.y)>box.y-50&&Math.min(s.a.y,s.b.y)<box.y+box.h+50);
    ctx.save();ctx.clip(visual.solid,"evenodd");
    ctx.lineJoin="round";ctx.lineCap="round";
    for(const sand of [false,true]) {
      ctx.beginPath();
      for(const s of candidates) {
        if(s.ny>-.4)continue;
        const mx=(s.a.x+s.b.x)/2,my=(s.a.y+s.b.y)/2;
        if(sand&&patchAt(mx+my*.17)<.65)continue;
        ctx.moveTo(s.a.x,s.a.y);ctx.lineTo(s.b.x,s.b.y);
      }
      ctx.strokeStyle=sand?"#b49a66":"#80796a";ctx.lineWidth=sand?29:23;ctx.stroke();
    }
    ctx.strokeStyle="#222825";ctx.lineWidth=5;ctx.stroke(visual.outline);
    ctx.restore();
  }
  function ore(ctx,art,x,y,size,variant=0) {
    const image=art.images.ore,w=image.width/2,h=image.height/2;
    ctx.drawImage(image,(variant%2)*w,Math.floor(variant/2)*h,w,h,x-size/2,y-size/2,size,size);
  }
  function hero(ctx,art,x,feet,height=68) {
    const image=art.images.hero,w=height*image.width/image.height;
    ctx.drawImage(image,x-w/2,feet-height,w,height);
  }
  function exposedOre(ctx,art,map,visual,box) {
    ctx.save();ctx.clip(visual.solid,"evenodd");
    for(let i=5;i<visual.edges.length;i+=67) {
      const s=visual.edges[i],x=(s.a.x+s.b.x)/2-s.nx*17,y=(s.a.y+s.b.y)/2-s.ny*17;
      if(x<box.x-70||x>box.x+box.w+70||y<box.y-70||y>box.y+box.h+70)continue;
      if(s.ny<-.8||((i+map.seed)%7)>2)continue;
      ore(ctx,art,x,y,65,i%4);
    }
    ctx.restore();
  }
  function draw(ctx,art,map,box,width,height,options={}) {
    const visual=api.build(map),scale=width/box.w;
    ctx.save();ctx.clearRect(0,0,width,height);ctx.scale(scale,scale);ctx.translate(-box.x,-box.y);
    if(!options.transparent)backdrop(ctx,art,box);
    ctx.fillStyle=ctx.createPattern(art.rock,"repeat");ctx.fill(visual.solid,"evenodd");
    api.surfaceBands(ctx,art,visual,box);
    if(!options.noOre)exposedOre(ctx,art,map,visual,box);
    if(options.hero) {
      const node=map.plan.nodes[options.room||0],x=node.x*api.U,y=api.floorAt(map,x,node.y*api.U);
      if(y!==null)hero(ctx,art,x,y);
    }
    ctx.restore();
  }
  function tileCanvas(art,id,size=128,paint=true) {
    const c=api.canvas(size,size),ctx=c.getContext("2d"),p=new Path2D();
    api.tilePolygons(CaveWB.catalog[id]).forEach(points=>{
      points.forEach((v,i)=>i?p.lineTo(v[0]*size,v[1]*size):p.moveTo(v[0]*size,v[1]*size));p.closePath();
    });
    ctx.fillStyle=paint?ctx.createPattern(art.rock,"repeat"):"#ffffff";ctx.fill(p);
    return c;
  }
  Object.assign(api,{prepare,backdrop,edges,ore,hero,draw,tileCanvas});
})(globalThis);
