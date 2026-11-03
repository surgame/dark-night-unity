(function (C) {
  "use strict";
  C.makeRenderer=function (root,canvas,atlasCanvas) {
    const ctx=canvas.getContext("2d"),cache=document.createElement("canvas");
    const terrain=cache.getContext("2d"),atlas=document.createElement("canvas");
    const tileSize=64,bake=16;
    let width=1,height=1,dpr=1,world,palette={};
    atlas.width=atlas.height=tileSize*4;
    function colors() {
      const swatch=document.createElement("span");root.append(swatch);
      for (const key of ["air","rock","edge","quiet","ink","soft","inverse"]) {
        swatch.style.color="var(--cw-"+key+")";
        palette[key]=getComputedStyle(swatch).color;
      }
      swatch.remove();
      const a=atlas.getContext("2d");a.clearRect(0,0,atlas.width,atlas.height);
      for (let m=0;m<16;m++) {
        a.save();a.translate(m%4*tileSize,Math.floor(m/4)*tileSize);
        a.beginPath();a.rect(0,0,tileSize,tileSize);a.clip();
        C.paintTile(a,m,tileSize,palette);a.restore();
      }
      if (world) rebuild();
      paintAtlas();
    }
    function drawCell(x,y) {
      const m=C.maskAt(world,x,y);
      terrain.clearRect(x*bake,y*bake,bake,bake);
      if (m) terrain.drawImage(atlas,m%4*tileSize,Math.floor(m/4)*tileSize,
        tileSize,tileSize,x*bake,y*bake,bake,bake);
    }
    function rebuild() {
      cache.width=world.width*bake;cache.height=world.height*bake;
      terrain.fillStyle=palette.rock;terrain.fillRect(0,0,cache.width,cache.height);
      for (let y=0;y<world.height;y++) for (let x=0;x<world.width;x++)
        if (C.maskAt(world,x,y)!==15) drawCell(x,y);
    }
    function resize() {
      const box=canvas.getBoundingClientRect();
      width=Math.max(1,box.width);height=Math.max(1,box.height);
      dpr=Math.min(2,window.devicePixelRatio||1);
      canvas.width=Math.round(width*dpr);canvas.height=Math.round(height*dpr);
      paintAtlas();
    }
    function paintAtlas() {
      const w=Math.min(root.clientWidth,520),cell=w/4,h=cell+23;
      atlasCanvas.width=Math.round(w*dpr);atlasCanvas.height=Math.round(h*4*dpr);
      atlasCanvas.style.width=w+"px";atlasCanvas.style.height=h*4+"px";
      const a=atlasCanvas.getContext("2d");a.setTransform(dpr,0,0,dpr,0,0);
      a.clearRect(0,0,w,h*4);
      if (!palette.ink) return;
      a.font='12px "Microsoft YaHei",sans-serif';a.textAlign="center";
      for (let m=0;m<16;m++) {
        const x=m%4*cell,y=Math.floor(m/4)*h,size=cell-18;
        a.fillStyle=palette.air;a.fillRect(x+8,y+2,size,size);
        a.drawImage(atlas,m%4*tileSize,Math.floor(m/4)*tileSize,tileSize,tileSize,x+8,y+2,size,size);
        a.fillStyle=palette.ink;a.fillText(String(m).padStart(2,"0")+" · "+
          [0,1,2,3].map(i=>m>>i&1).join(""),x+cell/2,y+cell+13);
      }
    }
    const screen=(camera,x,y)=>({x:(x-camera.x)*camera.scale+width/2,
      y:(y-camera.y)*camera.scale+height/2});
    const point=(camera,x,y)=>({x:(x-width/2)/camera.scale+camera.x,
      y:(y-height/2)/camera.scale+camera.y});
    function worldLayers(state) {
      const camera=state.camera,s=camera.scale;
      ctx.save();ctx.translate(width/2-camera.x*s,height/2-camera.y*s);ctx.scale(s,s);
      ctx.drawImage(cache,0,0,world.width,world.height);
      for (const ore of world.ore) {
        if (!C.solid(world,ore.x,ore.y)) continue;
        ctx.save();ctx.translate(ore.x,ore.y);ctx.strokeStyle=palette.edge;
        ctx.fillStyle=palette.air;ctx.lineWidth=.11;
        for (const [x,y,r] of [[0,0,.38],[.6,.4,.25],[-.5,.55,.21]]) {
          ctx.beginPath();ctx.moveTo(x,y-r);ctx.lineTo(x+r,y);ctx.lineTo(x,y+r);
          ctx.lineTo(x-r,y);ctx.closePath();ctx.fill();ctx.stroke();
        }
        ctx.restore();
      }
      for (const p of state.particles) {
        ctx.globalAlpha=C.clamp(p.life*3,0,1);ctx.fillStyle=palette.edge;
        ctx.beginPath();ctx.arc(p.x,p.y,p.r,0,Math.PI*2);ctx.fill();
      }
      ctx.globalAlpha=1;
      const p=state.player,{radius,halfHeight}=C.playerShape;
      ctx.fillStyle=palette.ink;
      ctx.beginPath();ctx.roundRect(p.x-radius,p.y-halfHeight,radius*2,halfHeight*2,radius);ctx.fill();
      ctx.fillStyle=palette.inverse;ctx.beginPath();
      ctx.roundRect(p.x-.26,p.y-.76,.52,.32,.12);ctx.fill();
      ctx.fillStyle=palette.ink;ctx.beginPath();
      ctx.arc(p.x+p.facing*.12,p.y-.60,.045,0,Math.PI*2);ctx.fill();
      if (state.grid) grid(camera);
      if (state.mode==="walk" && state.pointer && !state.pointer.panning) {
        const q=state.pointer.world,near=Math.hypot(q.x-p.x,q.y-p.y)<4.7;
        ctx.strokeStyle=palette.edge;ctx.lineWidth=1/s;
        ctx.setLineDash(near?[]:[3/s,3/s]);ctx.beginPath();
        ctx.arc(q.x,q.y,1.25,0,Math.PI*2);ctx.stroke();ctx.setLineDash([]);
      }
      ctx.restore();
    }
    function grid(camera) {
      const s=camera.scale,x0=Math.max(0,Math.floor(camera.x-width/2/s)),
        x1=Math.min(world.width,Math.ceil(camera.x+width/2/s)),
        y0=Math.max(0,Math.floor(camera.y-height/2/s)),
        y1=Math.min(world.height,Math.ceil(camera.y+height/2/s));
      if (s<5) return;
      ctx.save();ctx.globalAlpha=.3;ctx.strokeStyle=palette.soft;ctx.lineWidth=.7/s;
      ctx.beginPath();
      for (let x=x0;x<=x1;x++) {ctx.moveTo(x,y0);ctx.lineTo(x,y1);}
      for (let y=y0;y<=y1;y++) {ctx.moveTo(x0,y);ctx.lineTo(x1,y);}
      ctx.stroke();
      if (s>11) for (let y=y0;y<=y1;y++) for (let x=x0;x<=x1;x++) {
        ctx.fillStyle=world.data[y*(world.width+1)+x]?palette.ink:palette.inverse;
        ctx.beginPath();ctx.arc(x,y,1.5/s,0,Math.PI*2);ctx.fill();
      }
      ctx.restore();
    }
    function markers(state) {
      if (!state.labels) return;
      const s=state.camera.scale;ctx.font='12px "Microsoft YaHei",sans-serif';
      ctx.textAlign="center";ctx.textBaseline="middle";
      C.sites.forEach((site,i)=>{
        const p=screen(state.camera,site.x,site.y);
        if (p.x<16 || p.x>width-16 || p.y<44 || p.y>height-20) return;
        const selected=site.id===state.site;
        ctx.fillStyle=selected?palette.ink:palette.inverse;
        ctx.strokeStyle=palette.edge;ctx.lineWidth=1;
        ctx.beginPath();ctx.arc(p.x,p.y,10,0,Math.PI*2);ctx.fill();ctx.stroke();
        ctx.fillStyle=selected?palette.inverse:palette.ink;ctx.fillText(i+1,p.x,p.y+.5);
        if (width>=500 && (state.mode!=="walk" || s>6)) {
          const labelY=p.y-20,labelWidth=ctx.measureText(site.name).width+10;
          const lx=C.clamp(p.x,labelWidth/2+3,width-labelWidth/2-3);
          ctx.fillStyle=palette.air;ctx.fillRect(lx-labelWidth/2,labelY-9,labelWidth,18);
          ctx.fillStyle=palette.ink;ctx.fillText(site.name,lx,labelY);
        }
      });
    }
    function render(state) {
      ctx.setTransform(dpr,0,0,dpr,0,0);ctx.clearRect(0,0,width,height);
      ctx.fillStyle=palette.air;ctx.fillRect(0,0,width,height);
      worldLayers(state);markers(state);
    }
    colors();resize();
    return {
      render,resize,colors,screen,point,
      dimensions:()=>({width,height}),
      setWorld(w) {world=w;rebuild();},
      update(indices) {for (const i of indices) drawCell(i%world.width,Math.floor(i/world.width));},
      paintAtlas
    };
  };
})(globalThis.CaveWhitebox);
