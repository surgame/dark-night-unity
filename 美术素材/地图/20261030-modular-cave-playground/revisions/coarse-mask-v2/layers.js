(function(root) {
  "use strict";
  const api=root.MacroCave;
  const canvas=(w,h)=>Object.assign(document.createElement("canvas"),{width:w,height:h});

  function foreground(panels,tiles) {
    return panels.map((panel,index)=>{
      const c=canvas(623,414),ctx=c.getContext("2d");
      const placement=[{id:index*2,x:0,y:0}];
      ctx.beginPath();ctx.moveTo(0,0);ctx.lineTo(623,0);
      for(let x=623;x>=0;x--)ctx.lineTo(x,api.surface(placement,tiles,x,"roof"));
      ctx.closePath();
      ctx.moveTo(0,414);ctx.lineTo(623,414);
      for(let x=623;x>=0;x--)ctx.lineTo(x,api.surface(placement,tiles,x));
      ctx.closePath();ctx.clip();ctx.drawImage(panel,0,0);
      return c;
    });
  }
  function backgrounds(image) {
    return Array.from({length:6},(_,i)=>{
      const c=canvas(623,768),ctx=c.getContext("2d");
      const sw=Math.floor(image.width/2),sh=Math.floor(image.height/2);
      const sx=(i%3)*Math.floor((image.width-sw)/2),sy=(i%2)*(image.height-sh);
      ctx.drawImage(image,sx,sy,sw,sh,0,0,623,768);
      return c;
    });
  }
  function coverage(image) {
    const result=[];
    for(let variant=0;variant<4;variant++) {
      const c=canvas(64,64),ctx=c.getContext("2d");
      ctx.drawImage(image,(variant%2)*image.width/2,Math.floor(variant/2)*image.height/2,
        image.width/2,image.height/2,0,0,64,64);
      const pixels=ctx.getImageData(0,0,64,64).data,cells=[];
      for(let cy=0;cy<4;cy++)for(let cx=0;cx<4;cx++) {
        let count=0;
        for(let y=cy*16;y<(cy+1)*16;y++)for(let x=cx*16;x<(cx+1)*16;x++)
          if(pixels[(y*64+x)*4+3]>128)count++;
        cells.push(count>12);
      }
      result.push(cells);
    }
    return result;
  }
  function background(ctx,model,art,camera,viewWidth,viewHeight) {
    ctx.fillStyle="#303334";ctx.fillRect(camera.x,camera.y,viewWidth,viewHeight);
    for(const placed of model.placements) {
      if(placed.x+623<camera.x||placed.x>camera.x+viewWidth)continue;
      ctx.drawImage(art.backs[model.tiles[placed.id].panel],placed.x,placed.y-130);
    }
  }
  function minerals(ctx,world,image,box) {
    for(const site of world.sites) {
      if(site.x+64<box.x||site.x>box.x+box.w||site.y+64<box.y||site.y>box.y+box.h)continue;
      ctx.save();ctx.beginPath();
      let visible=false;
      for(let cy=0;cy<4;cy++)for(let cx=0;cx<4;cx++) {
        const x=site.x+cx*16,y=site.y+cy*16,value=world.ore.get(api.terrainKey(x,y));
        if(value&&value.hp>0){ctx.rect(x,y,16,16);visible=true;}
      }
      if(visible) {
        ctx.clip();ctx.drawImage(image,(site.variant%2)*image.width/2,
          Math.floor(site.variant/2)*image.height/2,image.width/2,image.height/2,
          site.x,site.y,64,64);
      }
      ctx.restore();
    }
  }
  function erase(ctx,world) {
    ctx.save();ctx.globalCompositeOperation="destination-out";ctx.fillStyle="#000000";
    for(const id of world.clear){const [x,y]=api.terrainCoordinates(id);ctx.fillRect(x,y,16,16);}
    ctx.restore();
  }
  function cuts(ctx,world) {
    ctx.save();ctx.strokeStyle="#b09c77";ctx.lineWidth=1.5;
    for(const id of world.clear) {
      const [x,y]=api.terrainCoordinates(id);
      const sides=[[[x,y],[x+16,y],[x+8,y-.1]],[[x+16,y],[x+16,y+16],[x+16.1,y+8]],
        [[x+16,y+16],[x,y+16],[x+8,y+16.1]],[[x,y+16],[x,y],[x-.1,y+8]]];
      for(const [a,b,test] of sides)if(api.terrainSolid(world,test[0],test[1])) {
        ctx.beginPath();ctx.moveTo(...a);ctx.lineTo(...b);ctx.stroke();
      }
    }
    ctx.restore();
  }
  Object.assign(api,{separateForeground:foreground,prepareBackwalls:backgrounds,oreCoverage:coverage,
    paintBackwall:background,paintMinerals:minerals,eraseTerrain:erase,paintCuts:cuts});
})(globalThis);
