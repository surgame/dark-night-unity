(function(root){
  "use strict";
  const api=root.ShapeStudy,lib=root.CaveWB;
  class FlowRenderer extends api.Renderer{
    setup(){
      const {c,w,h}=this.prepare(this.canvas);
      this.c=c;this.w=w;this.h=h;this.p=this.colors();
      this.font=parseFloat(getComputedStyle(this.host).fontSize)||14;
      const scale=Math.min((w-24)/48,(h-66)/28);
      this.viewport={scale,ox:(w-48*scale)/2,oy:8+(h-66-28*scale)/2,w,h};
      c.fillStyle=this.p.bg;c.fillRect(0,0,w,h);c.lineJoin="round";c.lineCap="round";
    }
    world(draw){
      const c=this.c,v=this.viewport;c.save();c.beginPath();c.rect(v.ox,v.oy,48*v.scale,28*v.scale);c.clip();
      c.translate(v.ox,v.oy);c.scale(v.scale,v.scale);draw(c,v.scale);c.restore();
    }
    text(value,x,y,align="left"){
      const c=this.c;c.font=Math.max(11,this.font*.86)+"px sans-serif";c.textAlign=align;c.fillStyle=this.p.fg;c.fillText(value,x,y);
    }
    path(points,color,width=1){
      const c=this.c;c.beginPath();c.moveTo(...points[0]);points.slice(1).forEach(p=>c.lineTo(...p));
      c.strokeStyle=color;c.lineWidth=width;c.stroke();
    }
    terrain(map){
      this.world((c,s)=>{
        c.fillStyle=this.p.fg;c.globalAlpha=.19;this.contours(c,map,true);c.fill("evenodd");c.globalAlpha=1;
        this.boundary(c,map,this.p.fg,1.4/s);
      });
    }
    reference(data,solid=false,anchorOnly=false){
      const v=this.viewport,map=data.before.dual;
      if(!anchorOnly)this.world((c,s)=>{
        c.globalAlpha=solid?.13:.035;c.fillStyle=this.p.fg;this.contours(c,map,true);c.fill("evenodd");
        c.globalAlpha=.38;c.setLineDash([3/s,4/s]);this.boundary(c,map,this.p.fg,1/s);c.setLineDash([]);c.globalAlpha=1;
      });
      const baselineY=api.groundAt(map,12),heroH=3*v.scale,heroW=heroH*this.hero.width/this.hero.height;
      this.c.globalAlpha=.8;this.c.drawImage(this.hero,v.ox+12*v.scale-heroW/2,v.oy+baselineY*v.scale-heroH,heroW,heroH);this.c.globalAlpha=1;
    }
    samples(data,alpha=1){
      this.world((c,s)=>{
        const samples=data.pair.baseSamples.dual;c.globalAlpha=alpha;
        for(let y=0;y<=28;y++)for(let x=0;x<=48;x++){
          const id=y*49+x,near=Math.abs(data.pair.plan.signedDistance(x,y))<1.4;
          if(!near)continue;
          c.beginPath();c.arc(x,y,Math.min(2,Math.max(1.3,s*.12))/s,0,Math.PI*2);
          c.fillStyle=samples[id]>0?this.p.fg:this.p.bg;c.fill();c.strokeStyle=this.p.fg;c.lineWidth=.8/s;c.stroke();
        }c.globalAlpha=1;
      });
    }
    grid(alpha=.7){
      this.world((c,s)=>{
        c.globalAlpha=alpha;c.strokeStyle=this.p.border;c.lineWidth=.6/s;c.beginPath();
        for(let x=0;x<=48;x++){c.moveTo(x,0);c.lineTo(x,28);}
        for(let y=0;y<=28;y++){c.moveTo(0,y);c.lineTo(48,y);}c.stroke();c.globalAlpha=1;
      });
    }
    clipX(progress,draw){
      const c=this.c,v=this.viewport;c.save();c.beginPath();c.rect(v.ox,v.oy,48*v.scale*progress,28*v.scale);c.clip();draw();c.restore();
    }
    tiles(map,fill=true,outline=true){
      this.world((c,s)=>{
        map.tiles.forEach((id,i)=>{
          const tile=lib.catalog[id],x=i%48,y=Math.floor(i/48);if(!tile.mask)return;
          c.save();c.translate(x,y);
          if(fill){c.beginPath();api.polygons(tile).forEach(p=>this.polygon(c,p));c.fillStyle=this.p.fg;c.globalAlpha=.2;c.fill();c.globalAlpha=1;}
          if(outline){for(const segment of tile.segments)this.path(segment.map(h=>h.point),this.p.fg,1.3/s);}
          c.restore();
        });
      });
    }
    field(a,b,t,fill=true){
      this.world((c,s)=>{
        for(let i=0;i<48*28;i++){
          const values=[0,1,2,3].map(j=>a.values[i*4+j]*(1-t)+b.values[i*4+j]*t),tile=api.floatTile(values);
          if(!tile.mask)continue;
          const x=i%48,y=Math.floor(i/48);c.save();c.translate(x,y);
          const count=t===1?b.counts[i]:a.counts[i],resolved=count===1;
          if(fill){c.fillStyle=this.p.fg;c.globalAlpha=resolved?.22:.06;c.beginPath();api.polygons(tile).forEach(p=>this.polygon(c,p));c.fill();c.globalAlpha=1;}
          for(const segment of tile.segments)this.path(segment.map(h=>h.point),resolved?this.p.fg:this.p.b,(resolved?1.5:1)/s);
          c.restore();
        }
      });
    }
    candidates(domains){
      this.world((c,s)=>{
        domains.forEach((options,i)=>{
          const first=lib.catalog[options[0]];if(first.mask===0||first.mask===15)return;
          c.save();c.translate(i%48,Math.floor(i/48));c.globalAlpha=Math.min(.35,1.5/options.length);
          for(const id of options)for(const segment of lib.catalog[id].segments)this.path(segment.map(h=>h.point),this.p.b,1/s);
          c.restore();
        });
      });
    }
    highlight(indices,width=48,offset=0){
      this.world((c,s)=>{
        for(const [i,index] of indices.entries()){
          const x=index%width+offset,y=Math.floor(index/width)+offset;
          c.fillStyle=i===0?this.p.a:this.p.b;c.globalAlpha=.22;c.fillRect(x,y,1,1);c.globalAlpha=1;
          c.strokeStyle=i===0?this.p.a:this.p.b;c.lineWidth=2/s;c.strokeRect(x,y,1,1);
        }
      });
    }
    callout(label,x,y){
      const v=this.viewport,c=this.c,point=[v.ox+x*v.scale,v.oy+y*v.scale];
      const textX=this.w/2,textY=v.oy+11.5*v.scale;
      this.path([point,[textX,textY+8]],this.p.a,1);
      c.beginPath();c.arc(...point,4,0,Math.PI*2);c.strokeStyle=this.p.a;c.lineWidth=1.5;c.stroke();
      c.font=Math.max(11,this.font*.86)+"px sans-serif";
      const width=c.measureText(label).width;c.fillStyle=this.p.bg;c.fillRect(textX-width/2-4,textY-13,width+8,20);
      this.text(label,textX,textY,"center");
    }
    macro(data,domains,active=-1){
      this.world((c,s)=>{
        for(let slot=0;slot<6;slot++){
          const options=domains[slot];c.save();c.globalAlpha=options.length===1?1:Math.min(.4,2/options.length);
          for(const id of options){
            const curves=data.macroCurves[id][slot];
            this.path(curves.roof,options.length===1?this.p.fg:this.p.b,1.4/s);
            this.path(curves.floor,options.length===1?this.p.fg:this.p.b,1.4/s);
          }c.restore();
          c.strokeStyle=this.p.border;c.lineWidth=.8/s;c.setLineDash([3/s,4/s]);c.strokeRect(slot*8,0,8,28);c.setLineDash([]);
          if(slot===active){c.fillStyle=this.p.a;c.globalAlpha=.09;c.fillRect(slot*8,0,8,28);c.globalAlpha=1;}
        }
      });
      const v=this.viewport;
      for(let slot=0;slot<6;slot++)this.text(String(domains[slot].length),v.ox+(slot*8+4)*v.scale,v.oy+27*v.scale,"center");
    }
    decoration(data,method){
      const map=data.before[method];
      this.world((c,s)=>{
        c.strokeStyle=this.p.fg;c.lineWidth=1/s;
        const water=data.pair.plan.water;c.setLineDash([3/s,3/s]);c.globalAlpha=.55;
        c.beginPath();c.moveTo(water.x,10);c.lineTo(water.x,water.level);c.stroke();c.setLineDash([]);
        c.beginPath();c.moveTo(water.x-3,water.level);c.lineTo(water.x+3,water.level);c.stroke();c.globalAlpha=1;
        for(const x of [9,16,37]){
          const y=api.groundAt(map,x);c.beginPath();c.moveTo(x-.4,y);c.lineTo(x-.6,y-.6);
          c.moveTo(x,y);c.lineTo(x+.1,y-.7);c.lineTo(x+.4,y-.3);c.stroke();
        }
      });
    }
    digging(data,method,t){
      const before=data.before[method],after=data.dug[method],v=this.viewport,c=this.c,p=data.digPoint;
      this.terrain(before);
      c.save();c.beginPath();c.arc(v.ox+p.x*v.scale,v.oy+p.y*v.scale,(.05+3*t)*v.scale,0,Math.PI*2);c.clip();
      c.fillStyle=this.p.bg;c.fillRect(0,0,this.w,this.h);this.reference(data,false);this.terrain(after);c.restore();
      this.highlight(after.changed,after.cellWidth||48,after.cellWidth?-.5:0);
      this.world((ctx,s)=>{
        ctx.save();ctx.beginPath();ctx.rect(p.x-2,p.y-2,4,4);ctx.clip();ctx.setLineDash([3/s,4/s]);
        this.boundary(ctx,before,this.p.b,1.5/s);ctx.restore();
        if(t>0&&t<1){
          for(let i=0;i<7;i++){
            const angle=i*Math.PI*2/7,travel=1.4*t,x=p.x+Math.cos(angle)*travel,y=p.y+Math.sin(angle)*travel+t*t;
            ctx.globalAlpha=1-t;ctx.fillStyle=this.p.fg;ctx.fillRect(x,y,2/s,2/s);ctx.globalAlpha=1;
          }
        }
      });
      this.callout(after.changed.length+"格结果变化；原地开挖",p.x,p.y);
    }
    dual(data,stage,t){
      if(stage<4){this.grid(.45);this.samples(data,stage===0?1:.35);}
      if(stage===1)this.clipX(t,()=>this.tiles(data.before.dual,false,true));
      if(stage===2){
        this.tiles(data.before.dual,false,true);
        const i=data.focus;this.highlight([i]);
        this.world((c,s)=>{
          const x=i%48,y=Math.floor(i/48),tile=lib.catalog[data.before.dual.tiles[i]];
          c.save();c.beginPath();c.rect(x,y,Math.max(.001,t),1);c.clip();c.translate(x,y);
          c.fillStyle=this.p.a;c.globalAlpha=.3;c.beginPath();api.polygons(tile).forEach(p=>this.polygon(c,p));c.fill();c.restore();
        });
        this.callout("四角 → mask "+api.details(data.before.dual,i).mask+" → 唯一格型",i%48+.5,Math.floor(i/48)+.5);
      }
      if(stage===3){
        this.tiles(data.before.dual,false,true);
        this.clipX(t,()=>this.tiles(data.before.dual,true,true));
        if(data.focus%48+1>48*t){
          const v=this.viewport,c=this.c,x=data.focus%48,y=Math.floor(data.focus/48);
          c.save();c.beginPath();c.rect(v.ox+x*v.scale,v.oy+y*v.scale,v.scale,v.scale);c.clip();
          this.tiles(data.before.dual,true,true);c.restore();
        }
      }
      if(stage===4){this.terrain(data.before.dual);this.decoration(data,"dual");}
      if(stage===5){this.digging(data,"dual",t);this.decoration(data,"dual");}
    }
    wfc(data,stage,t){
      const e=data.trace.event,m=data.micro;
      if(stage<4){this.grid(.4);this.samples(data,stage===0?1:.2);}
      if(stage===1)this.clipX(t,()=>{this.field(m.initial,m.initial,1);this.candidates(data.trace.initial);});
      if(stage===2){
        this.field(m.initial,m.observed,t);this.highlight([e.at]);
        this.callout(e.candidates.length+"候选 → 1（实际第"+e.number+"次观察）",e.at%48+.5,Math.floor(e.at/48)+.5);
      }
      if(stage===3){
        const frames=data.propagationFrames,index=t*(frames.length-1),a=Math.floor(index),b=Math.min(frames.length-1,a+1);
        this.field(frames[a],frames[b],index-a);
        const seen=e.changes.slice(0,Math.ceil(index)).map(c=>c.at);this.highlight([e.at,...new Set(seen)]);
        this.callout("本格已选；"+new Set(seen).size+"个邻格随约束收缩",e.at%48+.5,Math.floor(e.at/48)+.5);
      }
      if(stage===4){
        const frames=m.frames,start=e.number,index=start+t*(frames.length-1-start),a=Math.floor(index),b=Math.min(frames.length-1,a+1);
        if(t<1)this.field(frames[a],frames[b],index-a);else{this.terrain(data.before.wfc);this.decoration(data,"wfc");}
      }
      if(stage===5){this.digging(data,"wfc",t);this.decoration(data,"wfc");}
    }
    hybrid(data,stage,t){
      const m=data.macro;
      if(stage===0)this.world((c,s)=>{
        c.strokeStyle=this.p.border;c.lineWidth=.8/s;c.setLineDash([3/s,4/s]);
        for(let slot=0;slot<6;slot++)c.strokeRect(slot*8,0,8,28);c.setLineDash([]);
      });
      if(stage===1)this.clipX(t,()=>this.macro(data,m.initial));
      if(stage===2){
        const e=m.events[0];this.macro(data,t<.5?e.before:t<.75?e.observed:e.after,e.at);
        this.callout("段"+(e.at+1)+"选定；邻段 "+e.before[e.at+1]?.length+"→"+e.after[e.at+1]?.length,e.at*8+4,14);
      }
      if(stage===3){
        const at=Math.min(m.events.length-1,Math.floor(t*m.events.length)),e=m.events[at];
        this.macro(data,e.after,e.at);
        this.callout("已确定 "+e.after.filter(d=>d.length===1).length+" / 6 段",e.at*8+4,14);
      }
      if(stage===4){
        if(t<1){this.macro(data,m.events.at(-1).after);this.clipX(t,()=>this.terrain(data.before.hybrid));this.grid(.25);}
        else{this.terrain(data.before.hybrid);this.decoration(data,"hybrid");}
      }
      if(stage===5){this.digging(data,"hybrid",t);this.decoration(data,"hybrid");}
    }
    anchors(data){
      const v=this.viewport,labels=[{x:12,y:api.groundAt(data.before.dual,12),name:"落脚面"},
        {x:30,y:23,name:"浅池"},{x:42,y:16,name:"侧窝"}];
      for(const label of labels){
        const x=v.ox+label.x*v.scale,y=v.oy+label.y*v.scale;
        this.c.globalAlpha=.45;this.path([[x,y+5],[x,this.h-31]],this.p.fg,.7);this.c.globalAlpha=1;
        this.text(label.name,x,this.h-14,"center");
      }
    }
    render(data,method,position){
      this.setup();const stage=Math.ceil(position),t=stage?position-(stage-1):0;
      this.reference(data,stage===0);this[method](data,stage,t);
      this.reference(data,false,true);this.anchors(data);
      this.lastFrame={method,position,stage,t,viewport:{...this.viewport},
        anchors:[12,30,42].map(x=>this.viewport.ox+x*this.viewport.scale)};
    }
  }
  api.FlowRenderer=FlowRenderer;
})(globalThis);
