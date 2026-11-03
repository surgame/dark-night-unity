(function(root){
  "use strict";
  const api=root.ShapeStudy,lib=root.CaveWB;
  class StepRenderer extends api.Renderer{
    surface(){
      const v=this.prepare(this.canvas);Object.assign(this,v);
      this.palette=this.colors();this.baseFont=parseFloat(getComputedStyle(this.host).fontSize)||14;
      this.c.fillStyle=this.palette.bg;this.c.fillRect(0,0,this.w,this.h);
      this.c.lineJoin="round";this.c.lineCap="round";return v;
    }
    text(value,x,y,align="left",small=false){
      const c=this.c;c.font=(small?Math.max(11,this.baseFont*.86):this.baseFont)+"px sans-serif";
      c.fillStyle=this.palette.fg;c.textAlign=align;c.fillText(value,x,y);
    }
    line(points,color=this.palette.border,width=1,dashed=false){
      const c=this.c;c.strokeStyle=color;c.lineWidth=width;c.setLineDash(dashed?[4,4]:[]);
      c.beginPath();c.moveTo(...points[0]);points.slice(1).forEach(p=>c.lineTo(...p));c.stroke();c.setLineDash([]);
    }
    tile(tile,x,y,size,highlight=false){
      const c=this.c;c.save();c.translate(x,y);c.scale(size,size);
      c.beginPath();api.polygons(tile).forEach(p=>this.polygon(c,p));
      c.fillStyle=this.palette.fg;c.globalAlpha=.18;c.fill();c.globalAlpha=1;
      c.lineWidth=1/size;c.strokeStyle=this.palette.border;c.strokeRect(0,0,1,1);
      c.strokeStyle=highlight?this.palette.a:this.palette.fg;c.lineWidth=(highlight?2:1.2)/size;
      for(const segment of tile.segments){
        c.beginPath();c.moveTo(...segment[0].point);c.lineTo(...segment[1].point);c.stroke();
        for(const hit of segment){c.beginPath();c.arc(...hit.point,2.5/size,0,Math.PI*2);c.fillStyle=c.strokeStyle;c.fill();}
      }
      c.restore();
    }
    region(map,cx,cy,spanX,spanY,render){
      const c=this.c,scale=Math.min((this.w-32)/spanX,(this.h-100)/spanY);
      const ox=this.w/2-cx*scale,oy=(this.h+10)/2-cy*scale;
      this.viewport={scale,ox,oy};c.save();c.beginPath();c.rect(0,38,this.w,this.h-80);c.clip();
      c.translate(ox,oy);c.scale(scale,scale);
      c.fillStyle=this.palette.fg;c.globalAlpha=.14;this.contours(c,map,true);c.fill("evenodd");c.globalAlpha=1;
      this.boundary(c,map,this.palette.fg,1.5/scale);
      render?.(c,scale,{minX:cx-spanX/2,maxX:cx+spanX/2,minY:cy-spanY/2,maxY:cy+spanY/2});
      c.restore();
    }
    grid(c,scale,bounds,offset=0){
      c.strokeStyle=this.palette.border;c.lineWidth=.7/scale;c.beginPath();
      for(let x=Math.floor(bounds.minX);x<=bounds.maxX;x++){c.moveTo(x+offset,bounds.minY);c.lineTo(x+offset,bounds.maxY);}
      for(let y=Math.floor(bounds.minY);y<=bounds.maxY;y++){c.moveTo(bounds.minX,y+offset);c.lineTo(bounds.maxX,y+offset);}
      c.stroke();
    }
    worldText(value,x,y,scale){
      const c=this.c;c.font=Math.max(11,this.baseFont*.86)/scale+"px sans-serif";c.textAlign="center";
      const width=c.measureText(value).width;c.fillStyle=this.palette.bg;
      c.fillRect(x-width/2-2/scale,y-11/scale,width+4/scale,14/scale);
      c.fillStyle=this.palette.fg;c.fillText(value,x,y);
    }
    raw(data,method){
      const {pair,focus}=data,x=focus%48,y=Math.floor(focus/48),span=this.w<480?4:8;
      const c=this.c,scale=Math.min((this.w-56)/span,(this.h-100)/6);
      const ox=this.w/2-(x+.5)*scale,oy=(this.h+10)/2-(y+.5)*scale;
      this.text(method==="dual"?"每个逻辑位置只记：空 / 实":"空实先固定；边缘另有幅值候选",12,22);
      c.save();c.beginPath();c.rect(0,38,this.w,this.h-80);c.clip();c.translate(ox,oy);c.scale(scale,scale);
      const bounds={minX:x-span/2,maxX:x+span/2+1,minY:y-2,maxY:y+3};
      this.grid(c,scale,bounds,-.5);
      for(let py=Math.max(0,y-2);py<=Math.min(28,y+3);py++)for(let px=Math.max(0,Math.floor(x-span/2));px<=Math.min(48,x+span/2+1);px++){
        const i=py*49+px,solid=pair.baseSamples.dual[i]>0;
        c.fillStyle=this.palette.fg;c.globalAlpha=solid?.14:.02;c.fillRect(px-.48,py-.48,.96,.96);c.globalAlpha=1;
        c.beginPath();c.arc(px,py,3.5/scale,0,Math.PI*2);c.fillStyle=solid?this.palette.fg:this.palette.bg;c.fill();
        c.strokeStyle=this.palette.fg;c.lineWidth=1/scale;c.stroke();
        const allowed=pair.plan.allowed[i],values=lib.levels.filter((_,index)=>allowed&(1<<index));
        this.worldText(method==="dual"?(solid?"实":"空"):"{"+values.join(",")+"}",px,py+.28,scale);
      }
      c.strokeStyle=this.palette.a;c.lineWidth=2/scale;c.strokeRect(x,y,1,1);c.restore();
      this.text("选中渲染格 ("+x+","+y+")；四角读取相邻逻辑位置",this.w/2,this.h-18,"center",true);
    }
    candidatesAtlas(data,method){
      const options=method==="dual"?Array.from({length:16},(_,i)=>api.tile16(i)):
        data.trace.event.candidates.map(id=>lib.catalog[id]);
      const columns=method==="wfc"&&options.length<=4?2:4,rows=Math.ceil(options.length/columns);
      const cellW=this.w/columns,cellH=(this.h-80)/rows,top=44;
      const mask=api.toMask(lib.catalog[data.pair.dual.tiles[data.focus]].mask);
      this.text(method==="dual"?"16种二值拓扑；当前四角只命中一种":"库共256配置；当前格实际剩 "+options.length+" 个候选",12,22);
      options.forEach((tile,i)=>{
        const size=Math.min(160,cellW-24,cellH-28),x=(i%columns)*cellW+(cellW-size)/2,y=top+Math.floor(i/columns)*cellH;
        const selected=method==="dual"&&i===mask;
        this.tile(tile,x,y,size,selected);
        if(method==="wfc"){
          this.text(String(tile.values[0]),x+7,y+15,"left",true);
          this.text(String(tile.values[1]),x+size-7,y+15,"right",true);
          this.text(String(tile.values[3]),x+7,y+size-7,"left",true);
          this.text(String(tile.values[2]),x+size-7,y+size-7,"right",true);
        }
        this.text(method==="dual"?"mask "+i:"ID "+tile.id,x+size/2,y+size+17,"center",true);
        if(selected)this.text("命中",x+size/2,y-7,"center",true);
      });
      this.text(method==="dual"?"凹凸由空实布局决定；坡形、美术变体另做":"相同空实拓扑，不同幅值让共边交点移动",this.w/2,this.h-18,"center",true);
    }
    lookup(data){
      const tile=lib.catalog[data.pair.dual.tiles[data.focus]],mask=api.toMask(tile.mask);
      const narrow=this.w<480,size=narrow?125:190;
      const a={x:narrow?(this.w-size)/2:35,y:narrow?55:110};
      const b={x:narrow?(this.w-size)/2:this.w-size-35,y:narrow?285:110};
      this.text("四角直接决定唯一结果",12,22);this.tile(tile,a.x,a.y,size);
      const labels=["NW · "+(tile.values[0]>0?1:0),"NE · "+(tile.values[1]>0?1:0),
        "SW · "+(tile.values[3]>0?1:0),"SE · "+(tile.values[2]>0?1:0)];
      [[a.x,a.y-12],[a.x+size,a.y-12],[a.x,a.y+size+20],[a.x+size,a.y+size+20]]
        .forEach(([x,y],i)=>this.text(labels[i],x,y,i%2?"right":"left",true));
      this.tile(api.tile16(mask),b.x,b.y,size,true);
      this.text("mask "+mask+" / 唯一格型",b.x+size/2,b.y+size+21,"center");
      if(narrow){
        this.line([[this.w/2,218],[this.w/2,258]],this.palette.a,2);
        this.text("直接查表",this.w/2+15,244,"left",true);
      }else{
        this.line([[a.x+size+15,a.y+size/2],[b.x-15,b.y+size/2]],this.palette.a,2);
        this.text("查表",this.w/2,a.y+size/2-12,"center");
      }
    }
    stitch(data){
      const at=data.focus,x=at%48,y=Math.floor(at/48);
      this.text("相邻格读取同一组边端点",12,22);
      this.region(data.before.dual,x+.5,y+.5,this.w<480?3.5:6,4,(c,scale,bounds)=>{
        this.grid(c,scale,bounds);
        for(let py=y-1;py<=y+1;py++)for(let px=x-1;px<=x+1;px++){
          if(px<0||py<0||px>=48||py>=28)continue;
          const tile=lib.catalog[data.before.dual.tiles[py*48+px]];
          for(const segment of tile.segments)for(const hit of segment){
            c.beginPath();c.arc(px+hit.point[0],py+hit.point[1],3/scale,0,Math.PI*2);
            c.fillStyle=this.palette.a;c.fill();
          }
        }
        c.strokeStyle=this.palette.a;c.lineWidth=2/scale;c.strokeRect(x,y,1,1);
        this.worldText("当前格",x+.5,y+.55,scale);
      });
      this.text("边口固定 1/2；无需熵、候选传播或重试",this.w/2,this.h-18,"center",true);
    }
    domains(data,propagated){
      const e=data.trace.event,x=e.at%48,y=Math.floor(e.at/48);
      const current=propagated?e.after:e.observed,span=this.w<480?3.4:6;
      const scale=Math.min((this.w-36)/span,(this.h-100)/5),c=this.c;
      const ox=this.w/2-(x+.5)*scale,oy=(this.h+10)/2-(y+.5)*scale;
      this.text(propagated?"共边不兼容 → 邻格删除候选":"最低加权熵格 → 按权重选一个候选",12,22);
      c.save();c.beginPath();c.rect(0,38,this.w,this.h-80);c.clip();c.translate(ox,oy);c.scale(scale,scale);
      for(let py=Math.max(0,y-2);py<=Math.min(27,y+2);py++)for(let px=Math.max(0,x-Math.ceil(span/2));px<=Math.min(47,x+Math.ceil(span/2));px++){
        const i=py*48+px,options=current[i];
        c.strokeStyle=this.palette.border;c.lineWidth=.8/scale;c.strokeRect(px,py,1,1);
        c.fillStyle=this.palette.fg;c.globalAlpha=.045;
        for(const id of options){
          c.save();c.translate(px,py);c.beginPath();api.polygons(lib.catalog[id]).forEach(p=>this.polygon(c,p));c.fill();c.restore();
        }c.globalAlpha=1;
        const before=e.before[i].length,after=current[i].length,changed=propagated&&before!==after;
        if(i===e.at||changed){
          c.strokeStyle=i===e.at?this.palette.a:this.palette.b;c.lineWidth=2/scale;c.strokeRect(px+.04,py+.04,.92,.92);
        }
        this.worldText(changed?before+"→"+after:String(after),px+.5,py+.55,scale);
      }
      const chosen=lib.catalog[e.chosen];c.save();c.translate(x,y);
      c.strokeStyle=this.palette.a;c.lineWidth=2/scale;
      chosen.segments.forEach(s=>{c.beginPath();c.moveTo(...s[0].point);c.lineTo(...s[1].point);c.stroke();});c.restore();
      if(propagated){
        const seen=new Set();
        for(const change of e.changes){
          if(seen.has(change.at))continue;seen.add(change.at);
          const a={x:change.source%48+.5,y:Math.floor(change.source/48)+.5};
          const b={x:change.at%48+.5,y:Math.floor(change.at/48)+.5};
          const dx=(b.x-a.x)*.07,dy=(b.y-a.y)*.07;
          const start={x:a.x+(b.x-a.x)*.38,y:a.y+(b.y-a.y)*.38};
          const end={x:a.x+(b.x-a.x)*.62,y:a.y+(b.y-a.y)*.62};
          c.strokeStyle=this.palette.b;c.lineWidth=1.3/scale;
          c.beginPath();c.moveTo(start.x,start.y);c.lineTo(end.x,end.y);c.stroke();
          c.beginPath();c.moveTo(end.x-dx-dy,end.y-dy+dx);c.lineTo(end.x,end.y);c.lineTo(end.x-dx+dy,end.y-dy-dx);c.stroke();
        }
      }
      c.restore();
      this.text(propagated?"框内数字：传播前→后；连线表示实际传播方向":"数字：该格剩余候选数；着色格已选 ID "+e.chosen,this.w/2,this.h-18,"center",true);
    }
    macroGlyph(tile,x,y,w,h){
      const c=this.c,m=api.macroKinds[tile.kind],ports=dataPorts;
      const floor=[],roof=[];
      for(let i=0;i<=16;i++){
        const t=i/16,common=ports[tile.left]*(1-t)+ports[tile.right]*t,bump=Math.sin(Math.PI*t)**2;
        floor.push([x+t*w,y+h*.67+(common+bump*m.floor)*h*.2]);
        roof.push([x+t*w,y+h*.3+(common+bump*m.roof)*h*.2]);
      }
      c.fillStyle=this.palette.fg;c.globalAlpha=.16;
      c.beginPath();this.polygon(c,[[x,y],...roof,[x+w,y]]);c.fill();
      c.beginPath();this.polygon(c,[...floor,[x+w,y+h],[x,y+h]]);c.fill();c.globalAlpha=1;
      this.line(roof,this.palette.fg);this.line(floor,this.palette.fg);
      this.line([[x,y],[x,y+h]],this.palette.border);
      this.line([[x+w,y],[x+w,y+h]],this.palette.border);
    }
    macroAtlas(data){
      const e=data.macro.events[0],options=data.macro.initial[e.at],cols=3,cellW=this.w/cols,cellH=105;
      this.text("27种配置；首槽过滤后 "+options.length+" 候选",12,22);
      options.forEach((id,i)=>{
        const t=data.macro.catalog[id],x=i%cols*cellW+10,y=50+Math.floor(i/cols)*cellH,w=cellW-20;
        this.macroGlyph(t,x,y,w,58);
        this.text(api.macroKinds[t.kind].name,x+w/2,y-7,"center",true);
        this.text("L"+dataPorts[t.left]+" / R"+dataPorts[t.right],x+w/2,y+75,"center",true);
      });
      this.text("接口数值必须相等；选的是整段轮廓配置",this.w/2,this.h-18,"center",true);
    }
    macroLayout(data,observed){
      const e=data.macro.events[0],domains=observed?e.after:data.macro.initial;
      const c=this.c,plan=data.pair.plan,scale=Math.min((this.w-28)/48,(this.h-(observed?250:130))/28);
      const ox=this.w/2-24*scale,oy=55;
      this.text(observed?"大段选形 → 相邻大段按接口筛选":"先分 6 段；每段宽 8 个逻辑格",12,22);
      c.save();c.translate(ox,oy);c.scale(scale,scale);
      c.fillStyle=this.palette.fg;c.globalAlpha=.1;this.contours(c,data.before.dual,true);c.fill("evenodd");c.globalAlpha=1;
      this.boundary(c,data.before.dual,this.palette.fg,1/scale);
      for(let slot=0;slot<6;slot++){
        c.strokeStyle=this.palette.border;c.lineWidth=1/scale;c.setLineDash([4/scale,4/scale]);c.strokeRect(slot*8,0,8,28);c.setLineDash([]);
        if(observed&&(slot===e.at||e.before[slot].length!==domains[slot].length)){
          c.fillStyle=slot===e.at?this.palette.a:this.palette.b;c.globalAlpha=.12;c.fillRect(slot*8,0,8,28);c.globalAlpha=1;
        }
      }c.restore();
      const y=oy+28*scale+25;
      for(let slot=0;slot<6;slot++){
        const x=ox+(slot*8+4)*scale;
        this.text("段"+(slot+1),x,y,"center",true);
        this.text(observed&&e.before[slot].length!==domains[slot].length?e.before[slot].length+"→"+domains[slot].length:String(domains[slot].length),
          x,y+21,"center",true);
      }
      if(observed){
        const tile=data.macro.catalog[e.chosen],narrow=this.w<480;
        const glyphW=narrow?170:250,glyphH=70,glyphX=(this.w-glyphW)/2,glyphY=Math.max(y+65,this.h-120);
        this.macroGlyph(tile,glyphX,glyphY,glyphW,glyphH);
        this.text("段"+(e.at+1)+"："+api.macroKinds[tile.kind].name+" · L"+dataPorts[tile.left]+" / R"+dataPorts[tile.right],
          this.w/2,glyphY-12,"center",true);
      }else{
        const labels=plan.markers.filter(m=>["water","ore","pocket"].includes(m.kind));
        for(const marker of labels){
          const x=ox+marker.x*scale,yy=oy+marker.y*scale;
          if(x>this.w-45)continue;
          this.text(marker.label,x,yy,"center",true);
        }
      }
      this.text(observed?"重复选择与传播，直到6段全部确定":"数字为候选数量；不是运行时拼接房间",this.w/2,this.h-12,"center",true);
    }
    project(data){
      const i=data.slopeAt,map=data.before.hybrid,x=i%49,y=Math.floor(i/49);
      this.text("冻结空实 → 16型基础＋现有12种坡形",12,22);
      this.region(map,x,y,this.w<480?4:7,5,(c,scale,bounds)=>{
        this.grid(c,scale,bounds,-.5);
        for(let py=Math.max(0,y-2);py<=Math.min(28,y+2);py++)for(let px=Math.max(0,x-3);px<=Math.min(48,x+3);px++){
          const id=py*49+px,flag=map.flags[id];if(!map.cells[id])continue;
          if(flag){
            c.fillStyle=this.palette.a;c.globalAlpha=.22;c.beginPath();
            this.polygon(c,api.slopePolygon(flag).map(p=>[p[0]+px-.5,p[1]+py-.5]));c.fill();c.globalAlpha=1;
          }
          this.worldText(flag?"坡"+flag:"实",px,py+.1,scale);
        }
      });
      this.text("图中编号是独立坡形标志；大段选形不再变化",this.w/2,this.h-18,"center",true);
    }
    finished(data,method){
      this.draw({...data.pair,...data.before},{method,camera:"whole",grid:false,overlay:false,dug:false,selected:null});
      this.c=this.canvas.getContext("2d");this.palette=this.colors();
      this.text(method==="dual"?"直接查表完成":method==="wfc"?"全部候选收敛，冻结结果":"宏观组合已冻结，小格承接地形表现",this.w/2,this.h-14,"center",true);
    }
    dig(data,method){
      const point=data.digPoint,map=data.dug[method],before=data.before[method],count=map.changed.length;
      this.text(method==="hybrid"?"挖格后只重算小格与邻近坡形":"移除1个逻辑格 → 4个渲染格变化",12,22);
      this.region(map,point.x,point.y,this.w<480?4.6:8,5.7,(c,scale,bounds)=>{
        this.grid(c,scale,bounds,method==="hybrid"?-.5:0);
        c.save();c.setLineDash([4/scale,4/scale]);this.boundary(c,before,this.palette.b,1.7/scale);c.restore();
        const width=map.cellWidth||48,offset=map.cellWidth?-.5:0;
        for(const at of map.changed){
          const x=at%width+offset,y=Math.floor(at/width)+offset;
          c.fillStyle=this.palette.a;c.globalAlpha=.13;c.fillRect(x,y,1,1);c.globalAlpha=1;
          c.strokeStyle=this.palette.a;c.lineWidth=1.8/scale;c.strokeRect(x,y,1,1);
        }
        c.beginPath();c.arc(point.x,point.y,5/scale,0,Math.PI*2);c.fillStyle=this.palette.a;c.fill();
        this.worldText("移除",point.x,point.y-.35,scale);
      });
      this.text(count+"格变化 · 虚线：开挖前 · WFC不重求解",this.w/2,this.h-18,"center",true);
    }
    render(data,state){
      this.surface();
      const {method,step}=state;
      if(step===4)return this.finished(data,method);
      if(step===5)return this.dig(data,method);
      if(method==="dual"){
        if(step===0)this.raw(data,method);
        if(step===1)this.candidatesAtlas(data,method);
        if(step===2)this.lookup(data);
        if(step===3)this.stitch(data);
      }else if(method==="wfc"){
        if(step===0)this.raw(data,method);
        if(step===1)this.candidatesAtlas(data,method);
        if(step===2)this.domains(data,false);
        if(step===3)this.domains(data,true);
      }else{
        if(step===0)this.macroLayout(data,false);
        if(step===1)this.macroAtlas(data);
        if(step===2)this.macroLayout(data,true);
        if(step===3)this.project(data);
      }
    }
  }
  const dataPorts=[-.5,0,.5];
  api.StepRenderer=StepRenderer;
})(globalThis);
