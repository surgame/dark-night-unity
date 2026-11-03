(function (root) {
  "use strict";
  const api = root.MacroCave;
  const palette = {stone: "#28251f", seam: "#171916", warm: "rgba(255,218,157,.13)"};
  const makeCanvas = (w, h) => Object.assign(document.createElement("canvas"), {width:w, height:h});
  const frontBuffer=makeCanvas(1,1);

  function prepare(atlas) {
    return Array.from({length:6}, (_, i) => {
      const c = makeCanvas(623, 414);
      c.getContext("2d").drawImage(atlas, (i % 2) * 627 + 2, Math.floor(i / 2) * 418 + 2,
        623, 414, 0, 0, 623, 414);
      return c;
    });
  }

  function drawModule(ctx, panels, tile, x, y, scale = 1) {
    ctx.save();
    ctx.translate(x + (tile.mirror ? api.width * scale : 0), y);
    ctx.scale(tile.mirror ? -scale : scale, scale);
    ctx.drawImage(panels[tile.panel], 0, 0);
    const top=ctx.createLinearGradient(0,0,0,52);
    top.addColorStop(0,palette.stone);top.addColorStop(1,"rgba(40,37,31,0)");
    ctx.fillStyle=top;ctx.fillRect(0,0,623,52);
    const bottom=ctx.createLinearGradient(0,364,0,414);
    bottom.addColorStop(0,"rgba(40,37,31,0)");bottom.addColorStop(1,palette.stone);
    ctx.fillStyle=bottom;ctx.fillRect(0,364,623,50);
    ctx.restore();
  }

  function portal(ctx, panels, x, floor, port) {
    const gap = port === "B" ? 72 : 115;
    function stamp(panel, points, centerX, anchorY, targetY) {
      ctx.save();
      ctx.translate(x - centerX * .55, targetY - anchorY * .55);
      ctx.scale(.55,.55);
      ctx.beginPath();
      points.forEach((p,i)=>i?ctx.lineTo(p[0],p[1]):ctx.moveTo(p[0],p[1]));
      ctx.closePath();
      ctx.save();ctx.clip();ctx.drawImage(panels[panel],0,0);ctx.restore();
      ctx.strokeStyle=palette.seam;ctx.lineWidth=2;ctx.stroke();
      ctx.restore();
    }
    stamp(5,[[203,8],[297,0],[392,12],[410,51],[402,105],[379,143],
      [338,184],[284,211],[245,195],[212,177],[188,148],[186,100],[197,60]],
      300,211,floor-gap);
    stamp(3,[[170,304],[237,309],[300,319],[339,350],[330,402],
      [285,414],[226,402],[194,362]],250,310,floor);
    ctx.strokeStyle = palette.seam;
    ctx.lineWidth = 1.4;
    ctx.beginPath();
    ctx.moveTo(x + 1, floor - gap + 1);
    ctx.lineTo(x - 9, floor - gap + 30);
    ctx.lineTo(x + 5, floor - 42);
    ctx.lineTo(x + 1, floor - 2);
    ctx.stroke();
  }

  function beamPoints(player, placements, tiles, world) {
    const origin = {x: player.x + player.face * 17, y: player.y - 22};
    const direction = player.face > 0 ? -.09 : Math.PI + .09;
    const points = [origin];
    for (let i = 0; i <= 44; i++) {
      const angle = direction - .36 + .72 * i / 44;
      let point = origin;
      for (let distance = 4; distance <= 330; distance += 4) {
        const x = origin.x + Math.cos(angle) * distance;
        const y = origin.y + Math.sin(angle) * distance;
        point = {x, y};
        if (x < 0 || x > placements.length * api.width || api.terrainSolid(world,x,y)) break;
      }
      points.push(point);
    }
    return points;
  }

  function lighting(ctx, size, camera, player, placements, tiles, shade, world) {
    const sctx = shade.getContext("2d");
    if (shade.width !== size.w || shade.height !== size.h) {
      shade.width = size.w; shade.height = size.h;
    }
    sctx.clearRect(0,0,size.w,size.h);
    sctx.fillStyle = "rgba(0,0,0,.46)";
    sctx.fillRect(0,0,size.w,size.h);
    sctx.save();
    sctx.scale(camera.scale,camera.scale);
    sctx.translate(-camera.x,-camera.y);
    sctx.globalCompositeOperation = "destination-out";
    let gradient = sctx.createRadialGradient(player.x,player.y-24,10,player.x,player.y-24,125);
    gradient.addColorStop(0,"rgba(0,0,0,.8)");
    gradient.addColorStop(1,"rgba(0,0,0,0)");
    sctx.fillStyle=gradient;sctx.fillRect(player.x-125,player.y-149,250,250);
    const points=beamPoints(player,placements,tiles,world), origin=points[0];
    sctx.beginPath();
    points.forEach((p,i)=>i?sctx.lineTo(p.x,p.y):sctx.moveTo(p.x,p.y));
    sctx.closePath();sctx.clip();
    gradient=sctx.createRadialGradient(origin.x,origin.y,0,origin.x,origin.y,335);
    gradient.addColorStop(0,"rgba(0,0,0,.94)");
    gradient.addColorStop(.7,"rgba(0,0,0,.65)");
    gradient.addColorStop(1,"rgba(0,0,0,0)");
    sctx.fillStyle=gradient;sctx.fillRect(origin.x-340,origin.y-340,680,680);
    sctx.restore();
    ctx.drawImage(shade,0,0);
    ctx.save();ctx.scale(camera.scale,camera.scale);ctx.translate(-camera.x,-camera.y);
    ctx.beginPath();
    points.forEach((p,i)=>i?ctx.lineTo(p.x,p.y):ctx.moveTo(p.x,p.y));
    ctx.closePath();ctx.fillStyle=palette.warm;ctx.fill();ctx.restore();
  }

  function walk(ctx, size, model, art, colors, shade) {
    const {tiles,placements,player,state,world}=model;
    const viewWidth = size.w < 420 ? 590 : 700, scale = size.w / viewWidth;
    const viewHeight=size.h/scale;
    const camera={x:Math.max(0,Math.min(placements.length*api.width-viewWidth,player.x-viewWidth*.37)),
      y:player.y-viewHeight*.71,scale};
    ctx.clearRect(0,0,size.w,size.h);
    ctx.fillStyle=state.layer==="all"?palette.stone:colors.background;ctx.fillRect(0,0,size.w,size.h);
    ctx.save();ctx.scale(scale,scale);ctx.translate(-camera.x,-camera.y);
    if(state.layer==="all"||state.layer==="background")
      api.paintBackwall(ctx,model,art,camera,viewWidth,viewHeight);
    if(state.layer==="all"||state.layer==="mineral")
      api.paintMinerals(ctx,world,art.ore,{x:camera.x,y:camera.y,w:viewWidth,h:viewHeight});
    ctx.restore();
    if(frontBuffer.width!==Math.ceil(size.w)||frontBuffer.height!==Math.ceil(size.h)) {
      frontBuffer.width=Math.ceil(size.w);frontBuffer.height=Math.ceil(size.h);
    }
    const front=frontBuffer.getContext("2d");
    front.setTransform(1,0,0,1,0,0);front.clearRect(0,0,frontBuffer.width,frontBuffer.height);
    front.scale(scale,scale);front.translate(-camera.x,-camera.y);
    for(const placed of placements) {
      if(placed.x+api.width<camera.x||placed.x>camera.x+viewWidth)continue;
      front.fillStyle=palette.stone;
      for(const kind of ["floor","roof"]) {
        const edge=kind==="floor"?camera.y+viewHeight+50:camera.y-50;
        front.beginPath();front.moveTo(placed.x,edge);front.lineTo(placed.x+623,edge);
        for(let x=623;x>=0;x-=1)front.lineTo(placed.x+x,api.surface(placements,tiles,placed.x+x,kind));
        front.closePath();front.fill();
      }
      drawModule(front,art.fronts,tiles[placed.id],placed.x,placed.y);
    }
    for(let i=1;i<placements.length;i++) {
      const placed=placements[i];
      if(placed.x<camera.x-90||placed.x>camera.x+viewWidth+90)continue;
      const tile=tiles[placed.id], floor=placed.y+api.sample(tile.floor,0);
      portal(front,art.fronts,placed.x,floor,tile.left);
    }
    api.eraseTerrain(front,world);api.paintCuts(front,world);
    if(state.layer==="all"||state.layer==="foreground")ctx.drawImage(frontBuffer,0,0);
    if(state.layer!=="all")return camera;
    ctx.save();ctx.scale(scale,scale);ctx.translate(-camera.x,-camera.y);
    const bob=player.grounded?Math.sin(player.distance*.16)*.9:0;
    ctx.save();ctx.translate(player.x,player.y);
    ctx.scale(player.face,1);
    ctx.rotate(player.grounded?Math.sin(player.distance*.08)*.012:0);
    const h=api.characterHeight,w=h*art.hero.width/art.hero.height;
    ctx.drawImage(art.hero,-w/2,-h+bob,w,h);
    ctx.fillStyle="#a99c6a";ctx.strokeStyle=palette.seam;ctx.lineWidth=1.5;
    ctx.fillRect(14,-25,17,6);ctx.strokeRect(14,-25,17,6);
    if(world.action) {
      const progress=Math.min(1,(world.clock-(world.action.impact-.288))/.288);
      ctx.save();ctx.translate(9,-22);ctx.rotate(-1.4+progress*2.3);
      ctx.strokeStyle="#9f8053";ctx.lineWidth=4;ctx.beginPath();ctx.moveTo(0,0);ctx.lineTo(28,0);ctx.stroke();
      ctx.strokeStyle="#a8adb0";ctx.lineWidth=4;ctx.beginPath();ctx.moveTo(24,-10);ctx.lineTo(31,7);ctx.stroke();
      ctx.restore();
    }
    ctx.restore();ctx.restore();
    if(state.light)lighting(ctx,size,camera,player,placements,tiles,shade,world);
    const edge=ctx.createLinearGradient(0,0,0,size.h);
    edge.addColorStop(0,"rgba(0,0,0,.62)");
    edge.addColorStop(.2,"rgba(0,0,0,0)");
    edge.addColorStop(.83,"rgba(0,0,0,0)");
    edge.addColorStop(1,"rgba(0,0,0,.52)");
    ctx.fillStyle=edge;ctx.fillRect(0,0,size.w,size.h);
    if(state.bounds) {
      ctx.save();ctx.scale(scale,scale);ctx.translate(-camera.x,-camera.y);
      for(const placed of placements) {
        if(placed.x+api.width<camera.x||placed.x>camera.x+viewWidth)continue;
        const tile=tiles[placed.id];
        ctx.strokeStyle=colors.active;ctx.lineWidth=1.4/scale;ctx.setLineDash([6/scale,4/scale]);
        ctx.beginPath();ctx.moveTo(placed.x,camera.y);ctx.lineTo(placed.x,camera.y+viewHeight);ctx.stroke();
        ctx.setLineDash([]);
        for(const kind of ["floor","roof"]) {
          ctx.beginPath();
          for(let x=0;x<=api.width;x+=7) {
            const wx=placed.x+x,y=kind==="floor"?api.terrainGround(world,wx):api.terrainRoof(world,wx);
            x?ctx.lineTo(wx,y):ctx.moveTo(wx,y);
          }
          ctx.stroke();
        }
      }
      ctx.restore();
      ctx.fillStyle=colors.background;ctx.fillRect(6,5,Math.min(size.w-12,300),24);
      ctx.font="12px sans-serif";ctx.fillStyle=colors.foreground;
      const index=Math.floor(player.x/api.width),tile=tiles[placements[index].id];
      ctx.fillText(`${index+1}/16 · ${tile.key} · ${tile.left} → ${tile.right}`,12,21);
    }
    return camera;
  }

  function atlas(ctx,size,model,art,colors) {
    const cols=size.w>=600?3:2, gap=10, cell=(size.w-gap*(cols-1))/cols;
    const imageHeight=cell*414/623;
    ctx.clearRect(0,0,size.w,size.h);
    for(let i=0;i<6;i++) {
      const x=(i%cols)*(cell+gap),y=Math.floor(i/cols)*(imageHeight+34);
      if(model.state.layer==="all"||model.state.layer==="background")
        ctx.drawImage(art.backs[i],0,130,623,414,x,y,cell,imageHeight);
      if(model.state.layer==="all"||model.state.layer==="foreground")
        ctx.drawImage(art.fronts[i],x,y,cell,imageHeight);
      if(model.state.layer==="mineral") {
        ctx.drawImage(art.ore,(i%4%2)*art.ore.width/2,Math.floor(i%4/2)*art.ore.height/2,
          art.ore.width/2,art.ore.height/2,x+cell*.2,y,cell*.6,imageHeight);
      }
      const tile=model.tiles[i*2];
      if(model.state.bounds) {
        ctx.save();ctx.translate(x,y);ctx.scale(cell/623,cell/623);
        ctx.strokeStyle=colors.active;ctx.lineWidth=1.5*623/cell;
        for(const kind of ["floor","roof"]) {
          ctx.beginPath();tile[kind].forEach((p,j)=>j?ctx.lineTo(p[0],p[1]):ctx.moveTo(p[0],p[1]));ctx.stroke();
        }
        ctx.restore();
      }
      ctx.font="12px sans-serif";ctx.fillStyle=colors.foreground;
      ctx.fillText(`${i+1} ${tile.name}  ${tile.left}→${tile.right}`,x,y+imageHeight+19);
    }
  }

  function trace(ctx,size,model,art,colors) {
    const event=model.solved.trace[model.state.trace] || model.solved.trace[0];
    const cols=size.w>=600?4:2,gap=10,cell=(size.w-gap*(cols-1))/cols;
    const rowH=size.h/Math.ceil(16/cols);
    const current=model.solved.ids.map(id=>[id]);
    for(let i=0;i<16;i++)current[i]=model.tiles.map((_,j)=>j).filter(id =>
      (i!==0||model.tiles[id].left==="A")&&(i!==15||model.tiles[id].right==="A"));
    for(let i=0;i<=model.state.trace;i++) {
      const e=model.solved.trace[i];if(!e)break;
      if(e.kind==="observe")current[e.at]=[e.chosen];
      if(e.kind==="propagate")current[e.at]=e.after.slice();
    }
    ctx.clearRect(0,0,size.w,size.h);
    current.forEach((options,i)=>{
      const x=(i%cols)*(cell+gap),y=Math.floor(i/cols)*rowH;
      const tile=model.tiles[options[0]],imageH=Math.min(cell*414/623,rowH-37);
      ctx.globalAlpha=options.length===1?1:.48;
      drawModule(ctx,art.fronts,tile,x,y,Math.min(cell/623,imageH/414));
      ctx.globalAlpha=1;
      ctx.font="12px sans-serif";ctx.fillStyle=colors.foreground;
      ctx.fillText(`${i+1} · ${options.length===1?tile.key:options.length+" 个候选"}`,x,y+imageH+17);
      if(event&&event.at===i){ctx.strokeStyle=colors.active;ctx.lineWidth=2;ctx.strokeRect(x+1,y+1,cell-2,imageH-2);}
    });
  }

  Object.assign(api,{preparePanels:prepare,paintWalk:walk,paintAtlas:atlas,paintTrace:trace});
})(globalThis);
