(function(M,C) {
  "use strict";
  M.createRenderer=function(root,canvas) {
    const ctx=canvas.getContext("2d"),library=root.querySelector("#mc-library");
    let width=0,height=0,dpr=1,palette={},current=null,cache=null,samples=null;
    function color(token) {
      const swatch=document.createElement("span");
      swatch.style.color="var("+token+")";root.append(swatch);
      const result=getComputedStyle(swatch).color;swatch.remove();return result;
    }
    function colors() {
      palette={air:color("--background"),rock:color("--mc-rock"),edge:color("--mc-edge"),
        faint:color("--mc-faint"),ink:color("--foreground"),soft:color("--muted-foreground")};
      if(current) setWorld(current);
    }
    function resize() {
      const box=canvas.getBoundingClientRect();
      width=box.width;height=box.height;dpr=Math.min(devicePixelRatio||1,2);
      canvas.width=Math.round(width*dpr);canvas.height=Math.round(height*dpr);
    }
    function polygon(context,points,ox=0,oy=0) {
      points.forEach((p,i)=>i?context.lineTo(p[0]+ox,p[1]+oy):context.moveTo(p[0]+ox,p[1]+oy));
      context.closePath();
    }
    function cavity(context,shape) {
      const points=shape.top.concat(shape.bottom.slice().reverse());
      context.beginPath();polygon(context,points);
    }
    function setWorld(world) {
      current=world;cache=null;samples=world.outline;
      if(!world.grid) return;
      const unit=16;cache=document.createElement("canvas");
      cache.width=M.width*unit;cache.height=M.height*unit;
      const cc=cache.getContext("2d");
      cc.fillStyle=palette.air;cc.fillRect(0,0,cache.width,cache.height);
      for(let y=0;y<M.height;y++) for(let x=0;x<M.width;x++) {
        const mask=C.maskAt(world.grid,x,y);if(!mask) continue;
        cc.save();cc.translate(x*unit,y*unit);
        if(world.method==="dual") {
          cc.beginPath();cc.rect(0,0,unit,unit);cc.clip();C.paintTile(cc,mask,unit,palette);
        } else {
          cc.scale(unit,unit);cc.beginPath();
          const polys=mask===15?[[[0,0],[1,0],[1,1],[0,1]]]:
            world.grid.polygons.get(y*M.width+x);
          polys.forEach(poly=>polygon(cc,poly));cc.fillStyle=palette.rock;cc.fill();
          if(mask!==15) {
            cc.beginPath();
            polys.forEach(poly=>poly.forEach((a,i)=>{
              const b=poly[(i+1)%poly.length];
              const shared=a[0]===b[0]&&(a[0]===0||a[0]===1)||
                a[1]===b[1]&&(a[1]===0||a[1]===1);
              if(!shared) {cc.moveTo(...a);cc.lineTo(...b);}
            }));
            cc.strokeStyle=palette.edge;cc.lineWidth=.065;cc.stroke();
          }
        }
        cc.restore();
      }
    }
    function screen(camera,x,y) {
      return {x:(x-camera.x)*camera.scale+width/2,y:(y-camera.y)*camera.scale+height/2};
    }
    function point(camera,x,y) {
      return {x:camera.x+(x-width/2)/camera.scale,y:camera.y+(y-height/2)/camera.scale};
    }
    function structural(state) {
      const s=state.camera.scale;
      if(current.grid) {
        if(s<8) return;
        ctx.strokeStyle=palette.faint;ctx.lineWidth=.65/s;ctx.beginPath();
        const a=point(state.camera,0,0),b=point(state.camera,width,height);
        for(let x=Math.max(0,Math.floor(a.x));x<=Math.min(M.width,b.x);x++) {
          ctx.moveTo(x,Math.max(0,a.y));ctx.lineTo(x,Math.min(M.height,b.y));
        }
        for(let y=Math.max(0,Math.floor(a.y));y<=Math.min(M.height,b.y);y++) {
          ctx.moveTo(Math.max(0,a.x),y);ctx.lineTo(Math.min(M.width,b.x),y);
        }
        ctx.stroke();return;
      }
      if(!current.blocks.length) return;
      ctx.save();ctx.fillStyle=palette.ink;ctx.globalAlpha=.055;
      current.transitions.forEach(t=>ctx.fillRect(t.start,0,t.end-t.start,M.height));
      ctx.restore();
      ctx.strokeStyle=palette.soft;ctx.lineWidth=1/s;ctx.setLineDash([4/s,5/s]);
      current.blocks.forEach((block,i)=>{
        if(!i) return;
        const x=block.start,p=current.profile(x);
        ctx.beginPath();ctx.moveTo(x,p[0]-4/s);ctx.lineTo(x,p[1]+4/s);ctx.stroke();
      });
      ctx.setLineDash([]);
    }
    function character(p,s) {
      ctx.fillStyle=palette.ink;ctx.strokeStyle=palette.ink;ctx.lineWidth=1/s;
      ctx.beginPath();ctx.roundRect(p.x-.38,p.y-.97,.76,1.94,.32);ctx.fill();
      ctx.fillStyle=palette.air;ctx.beginPath();ctx.roundRect(p.x-.26,p.y-.76,.52,.38,.12);ctx.fill();
      ctx.fillStyle=palette.ink;ctx.beginPath();ctx.arc(p.x+.09*p.facing,p.y-.57,.05,0,Math.PI*2);ctx.fill();
    }
    function labels(state) {
      if(!state.seams||current.grid||width<480) return;
      ctx.font='12px "Microsoft YaHei",sans-serif';ctx.textAlign="center";
      M.slots.forEach((slot,i)=>{
        const x=(slot.start+slot.end)/2,p=current.profile(x),at=screen(state.camera,x,(p[0]+p[1])/2);
        const label=current.blocks.length?slot.name+" · "+String.fromCharCode(65+current.blocks[i].variant):slot.name;
        const w=ctx.measureText(label).width;
        if(at.x<w/2+4||at.x>width-w/2-4||at.y<42||at.y>height-24) return;
        ctx.fillStyle=palette.air;ctx.fillRect(at.x-w/2-5,at.y-12,w+10,22);
        ctx.fillStyle=palette.soft;ctx.fillText(label,at.x,at.y+3);
      });
    }
    function render(state) {
      const cam=state.camera,s=cam.scale;
      ctx.setTransform(dpr,0,0,dpr,0,0);ctx.fillStyle=palette.rock;ctx.fillRect(0,0,width,height);
      ctx.save();ctx.translate(width/2-cam.x*s,height/2-cam.y*s);ctx.scale(s,s);
      if(cache) ctx.drawImage(cache,0,0,M.width,M.height);
      else {
        cavity(ctx,samples);ctx.fillStyle=palette.air;ctx.fill();
        ctx.strokeStyle=palette.edge;ctx.lineWidth=1.35/s;ctx.stroke();
      }
      if(state.reference&&state.method!=="dual") {
        ctx.strokeStyle=palette.soft;ctx.lineWidth=1/s;ctx.setLineDash([5/s,4/s]);
        const baseline=state.baseline;
        for(let y=0;y<M.height;y++) for(let x=0;x<M.width;x++) {
          const mask=C.maskAt(baseline.grid,x,y);if(!mask||mask===15) continue;
          for(const e of C.templates[mask].boundaries) {
            ctx.beginPath();ctx.moveTo(x+e.a[0],y+e.a[1]);
            if(e.center) ctx.arc(x+e.center[0],y+e.center[1],.5,e.start,e.end,e.negative);
            else ctx.lineTo(x+e.b[0],y+e.b[1]);
            ctx.stroke();
          }
        }
        ctx.setLineDash([]);
      }
      if(state.seams) structural(state);
      character(state.player,s);ctx.restore();labels(state);
    }
    function drawLibrary(world) {
      const w=library.getBoundingClientRect().width;if(!w) return;
      const columns=w>=650?4:w>=350?2:1,cellWidth=w/columns,cellHeight=118;
      const rows=Math.ceil(M.slots.length/columns),lc=library.getContext("2d");
      library.height=rows*cellHeight*dpr;library.width=w*dpr;library.style.height=rows*cellHeight+"px";
      lc.setTransform(dpr,0,0,dpr,0,0);lc.clearRect(0,0,w,rows*cellHeight);
      M.slots.forEach((slot,i)=>{
        const b=world.blocks[i]||M.block(slot,{left:1,right:1,variant:0,id:-1});
        const ox=(i%columns)*cellWidth,oy=Math.floor(i/columns)*cellHeight;
        const shape=M.sample(x=>b.profile(M.clamp(x,b.start,b.end)));
        const points=shape.top.filter(p=>p[0]>=b.start&&p[0]<=b.end)
          .concat(shape.bottom.filter(p=>p[0]>=b.start&&p[0]<=b.end).reverse());
        const top=Math.min(...points.map(p=>p[1])),bottom=Math.max(...points.map(p=>p[1]));
        const scale=Math.min((cellWidth-20)/(b.end-b.start),78/(bottom-top));
        lc.save();lc.translate(ox+cellWidth/2-(b.start+b.end)/2*scale,oy+66-(top+bottom)/2*scale);lc.scale(scale,scale);
        lc.fillStyle=palette.rock;lc.fillRect(b.start,top,b.end-b.start,bottom-top);
        lc.beginPath();polygon(lc,points);lc.fillStyle=palette.air;lc.fill();
        lc.strokeStyle=palette.edge;lc.lineWidth=1/scale;lc.stroke();lc.restore();
        lc.font='12px "Microsoft YaHei",sans-serif';lc.textAlign="center";lc.fillStyle=palette.ink;
        lc.fillText(slot.name+(world.blocks.length?" · "+String.fromCharCode(65+b.variant):""),ox+cellWidth/2,oy+16);
      });
    }
    colors();resize();
    return {resize,colors,setWorld,render,drawLibrary,screen,point,
      dimensions:()=>({width,height})};
  };
})(globalThis.MacroCave,globalThis.CaveWhitebox);
