(function(root) {
  "use strict";
  const api=root.FlatCave;
  function prepare(images) {
    const rock=api.periodicOpaque(images.rock,768),pattern=rock.getContext("2d").createPattern(rock,"repeat");
    pattern.setTransform(new DOMMatrix().translate(137,89).rotate(17).scale(.9));
    return {images,rock,pattern,surfaces:api.prepareSurface(images)};
  }
  function backdrop(ctx,art,box) {
    const img=art.images.backdrop,w=2200,h=w*img.height/img.width;
    for(let y=Math.floor(box.y/h);y<=Math.floor((box.y+box.h)/h);y++)
      for(let x=Math.floor(box.x/w);x<=Math.floor((box.x+box.w)/w);x++) {
        ctx.save();ctx.translate((x+(x%2?1:0))*w,(y+(y%2?1:0))*h);
        ctx.scale(x%2?-1:1,y%2?-1:1);ctx.drawImage(img,0,0,w,h);ctx.restore();
      }
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
    ctx.fillStyle=art.pattern;ctx.fill(visual.solid,"evenodd");
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
  Object.assign(api,{prepare,backdrop,ore,hero,draw,tileCanvas});
})(globalThis);
