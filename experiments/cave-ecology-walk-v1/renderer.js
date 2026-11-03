(function (root) {
  "use strict";
  const sample=root.EcologyWorld.sample, clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
  const color={ink:"#141a17",rock:"#36342e",soil:"#bc9a61",green:"#76953e",water:"#469e9b",foam:"#bde5cc"};
  class EcologyRenderer {
    constructor(canvas,assets) {
      this.canvas=canvas;this.ctx=canvas.getContext("2d");this.assets=assets;
      this.camera={x:0,y:0};this.particles=[];this.seenEvent=0;this.overview=false;this.focus=null;
      this.roomImages=new Map();this.changedRoads=new Map();this.debug=false;this.motion=true;
      const tile=document.createElement("canvas"),material=assets.rockMaterial;
      tile.width=material?material.width*2:623;tile.height=material?material.height*2:90;
      const tc=tile.getContext("2d");
      if(material) {
        for(let y=0;y<2;y++)for(let x=0;x<2;x++) {
          tc.save();tc.translate(x?tile.width:0,y?tile.height:0);tc.scale(x?-1:1,y?-1:1);
          tc.drawImage(material,0,0);tc.restore();
        }
      } else tc.drawImage(assets.panels[0],0,324,623,90,0,0,623,90);
      this.rockPattern=this.ctx.createPattern(tile,"repeat");
      if(material)this.rockPattern.setTransform(new DOMMatrix().scale(768/material.width));
    }
    setWorld(world) {
      this.world=world;this.roomImages.clear();this.changedRoads.clear();
      this.particles=[];this.seenEvent=0;this.focus=null;this.inspectionTarget=null;
      world.rooms.forEach(room=>{
        const native=root.ECOLOGY_CONTOURS?.[room.panel],author=root.EcologyWorld.profiles[room.panel];
        const authored=room.authoredArt,image=authored?this.assets[authored.key]:this.assets.panels[room.panel];
        if(!image)throw new Error("Missing authored terrain image: "+(authored?.key||room.panel));
        const canvas=document.createElement("canvas");canvas.width=623;
        canvas.height=Math.max(414,Math.ceil((authored?.y||0)+(authored?.drawHeight||414)));
        const c=canvas.getContext("2d");
        c.drawImage(image,authored?.x||0,authored?.y||0,authored?.drawWidth||623,authored?.drawHeight||414);
        c.globalCompositeOperation="destination-out";
        const fade=c.createLinearGradient(0,0,623,0);
        const left=authored?(authored.x||0):64,right=authored?left+authored.drawWidth:559;
        fade.addColorStop(0,"#000");fade.addColorStop(left/623,"#000");
        fade.addColorStop((authored?left+8:112)/623,"transparent");
        fade.addColorStop((authored?right-8:511)/623,"transparent");
        fade.addColorStop(right/623,"#000");fade.addColorStop(1,"#000");
        c.fillStyle=fade;c.fillRect(0,0,623,canvas.height);
        if(authored) {
          const bottom=(authored.y||0)+authored.drawHeight,bed=c.createLinearGradient(0,bottom-12,0,bottom);
          bed.addColorStop(0,"transparent");bed.addColorStop(1,"#000");
          c.fillStyle=bed;c.fillRect(authored.x||0,bottom-12,authored.drawWidth,12);
        }
        const zones=authored?[[0,left+8],[right-8,623]]:[[0,112],[511,623]],cut=authored?null:room.shape?.cut;
        if(cut) {
          const erased=[[cut.a-2,Math.min(...cut.points.map(p=>p[1]))-85],
            [cut.b+2,Math.min(...cut.points.map(p=>p[1]))-85],
            ...cut.points.slice().reverse().map(p=>[p[0],p[1]+22])];
          c.fillStyle="#000";this.trace(erased,c);c.fill();
          zones.push([cut.a-12,cut.b+12]);
          const heights=native?native.floor.map((y,x)=>sample(author.floor,x)-y):[];
          const prominence=heights.map((h,x)=>h-Math.min(...heights.slice(Math.max(0,x-55),Math.min(623,x+56))));
          for(const edge of [cut.a,cut.b]) {
            if(!native||prominence[Math.round(edge)]<4)continue;
            let a=Math.floor(edge),b=Math.ceil(edge);
            while(a>65&&prominence[a]>3)a--;
            while(b<558&&prominence[b]>3)b++;
            const xs=[a,...author.floor.filter(p=>p[0]>a&&p[0]<b).map(p=>p[0]),b];
            const upper=xs.map(x=>[x,Math.max(sample(author.roof,x)+4,sample(author.floor,x)-95)]);
            const lower=xs.slice().reverse().map(x=>[x,sample(author.floor,x)+22]);
            this.trace(upper.concat(lower),c);c.fill();zones.push([a,b]);
          }
        }
        const low=authored?null:room.shape?.lowroof;
        if(low&&native) {
          const b=low.b+36;
          for(let x=Math.max(0,Math.floor(low.a-34));x<=Math.min(622,Math.ceil(b));x++) {
            const y=native.roof[x],blend=c.createLinearGradient(0,y-18,0,y+5);
            blend.addColorStop(0,"transparent");blend.addColorStop(.75,"#000");blend.addColorStop(1,"#000");
            c.fillStyle=blend;c.fillRect(x,y-18,1,24);
          }
        }
        c.globalCompositeOperation="source-over";this.roomImages.set(room.id,canvas);
        this.changedRoads.set(room.id,zones);
      });
      this.camera={x:0,y:world.spawn.y-260};
    }
    trace(points,context=this.ctx) {
      context.beginPath();points.forEach((p,i)=>i?context.lineTo(...p):context.moveTo(...p));context.closePath();
    }
    size() {
      const r=this.canvas.getBoundingClientRect(),dpr=Math.min(2,devicePixelRatio||1);
      const w=Math.round(r.width*dpr),h=Math.round(r.height*dpr);
      if(this.canvas.width!==w||this.canvas.height!==h){this.canvas.width=w;this.canvas.height=h;}
      const art=this.focus&&this.world?.rooms.find(room=>room.id===this.focus)?.authoredArt;
      this.width=art&&!this.inspectionTarget?Math.max(560,art.drawWidth+48):Math.max(560,Math.min(880,r.width));
      this.height=this.width*r.height/r.width;
      return {w:r.width,h:r.height,scale:w/this.width};
    }
    path(points,fill,stroke,width=1.5,close=true) {
      const c=this.ctx;c.beginPath();points.forEach((p,i)=>i?c.lineTo(p[0],p[1]):c.moveTo(p[0],p[1]));
      if(close)c.closePath();if(fill){c.fillStyle=fill;c.fill();}if(stroke){c.strokeStyle=stroke;c.lineWidth=width;c.stroke();}
    }
    local(points,room) {
      return points.map(p=>[p[0]+room.x,p[1]+room.y]);
    }
    mass(polygon,rim,outline=true) {
      if(!polygon||polygon.length<3)return;
      this.path(polygon,this.rockPattern||color.rock,outline?color.ink:null,1.7);
      if(rim)this.soilRim(rim);
    }
    soilRim(points) {
      this.runs(points).forEach(run=>{
        this.path(run.points,null,run.gentle?"#9f875a":"#292b23",run.gentle?1.6:2,false);
      });
    }
    runs(points) {
      const result=[];
      for(let i=1;i<points.length;i++) {
        const a=points[i-1],b=points[i],gentle=Math.abs(b[1]-a[1])/Math.max(.001,b[0]-a[0])<=.85;
        let run=result.at(-1);
        if(!run||run.gentle!==gentle){run={gentle,points:[a],indices:[i-1]};result.push(run);}
        run.points.push(b);run.indices.push(i);
      }
      return result;
    }
    road(floor,authored,room) {
      const far=authored?.far||floor.map(p=>[p[0],p[1]-8]);
      const near=authored?.near||floor.map(p=>[p[0],p[1]+12]);
      return {floor,far:room&&authored?.far?this.local(far,room):far,
        near:room&&authored?.near?this.local(near,room):near,lipDepth:authored?.lipDepth||9};
    }
    roadTop(road) {
      const polygon=road.far.concat(road.near.slice().reverse());
      this.path(polygon,"#494435",null);
      this.runs(road.floor).filter(run=>run.gentle).forEach(run=>{
        const far=run.indices.map(i=>road.far[i]),near=run.indices.map(i=>road.near[i]);
        this.path(far.concat(near.slice().reverse()),"#9e8458",null);
        this.path(far,null,"#5f543b",1.1,false);
        this.path(near,null,"#78613f",1.2,false);
      });
      const first=road.floor[0][0],last=road.floor.at(-1)[0];
      for(let x=Math.ceil(first/53)*53;x<last;x+=53) {
        const slope=Math.abs(sample(road.floor,x+3)-sample(road.floor,x-3))/6;
        if(slope>.85)continue;
        const y=sample(road.floor,x),skew=Math.sin(x*.71)*2;
        this.path([[x-5,y+4],[x+skew,y+2],[x+8,y+3]],null,"#65543b",.75,false);
        this.path([[x+12,y-3],[x+16,y-4]],null,"#c0a674",1,false);
      }
    }
    roadLip(road) {
      this.runs(road.floor).forEach(run=>{
        const edge=run.indices.map(i=>road.near[i]);
        const lower=edge.map(p=>[p[0],p[1]+road.lipDepth]);
        this.path(edge.concat(lower.slice().reverse()),run.gentle?"#453a2d":"#302f27",null);
        this.path(lower,null,"#25271f",1.2,false);
        if(run.gentle)this.path(edge,null,"#9c8052",1.3,false);
      });
    }
    changedRoad(room,draw) {
      const c=this.ctx,matrix=c.getTransform();
      if(!this.roadOverlay)this.roadOverlay=document.createElement("canvas");
      const layer=this.roadOverlay;
      if(layer.width!==this.canvas.width||layer.height!==this.canvas.height) {
        layer.width=this.canvas.width;layer.height=this.canvas.height;
      }
      const paint=layer.getContext("2d");
      paint.setTransform(1,0,0,1,0,0);paint.clearRect(0,0,layer.width,layer.height);
      paint.setTransform(matrix);paint.globalCompositeOperation="source-over";paint.globalAlpha=1;
      try { this.ctx=paint;draw(); } finally { this.ctx=c; }
      const zones=[];
      (this.changedRoads.get(room.id)||[]).slice().sort((a,b)=>a[0]-b[0]).forEach(([a,b])=>{
        const last=zones.at(-1);
        if(last&&a<=last[1])last[1]=Math.max(last[1],b);
        else zones.push([a,b]);
      });
      const mask=paint.createLinearGradient(room.x,0,room.x+623,0);
      let previous=0;
      const stop=(at,alpha)=>{const value=clamp(at/623,previous,1);mask.addColorStop(value,alpha?"#000":"transparent");previous=value;};
      stop(0,zones[0]?.[0]===0);
      zones.forEach(([a,b])=>{
        const art=room.authoredArt,cut=art?null:room.shape?.cut;
        const leftDistance=art?art.x+art.drawWidth-a:cut&&a<cut.a?cut.a-a:24;
        const rightDistance=art?b-art.x:cut&&b>cut.b?b-cut.b:24;
        const leftFade=a===0?0:Math.min(24,Math.max(8,leftDistance),(b-a)/3);
        const rightFade=b===623?0:Math.min(24,Math.max(8,rightDistance),(b-a)/3);
        if(a>0){stop(a,0);stop(a+leftFade,1);}
        if(b<623){stop(b-rightFade,1);stop(b,0);}else stop(623,1);
      });
      if(previous<1)stop(623,0);
      paint.globalCompositeOperation="destination-in";paint.globalAlpha=1;paint.fillStyle=mask;
      paint.fillRect(room.x,this.world.bounds.y-100,623,this.world.bounds.h-this.world.bounds.y+200);
      c.save();c.setTransform(1,0,0,1,0,0);c.drawImage(layer,0,0);c.restore();
    }
    changedRoof(room,draw) {
      const c=this.ctx,art=room.authoredArt;
      const zones=art?[[0,art.x+8],[art.x+art.drawWidth-8,623]]:[[0,112],[511,623]];
      const low=art?null:room.shape?.lowroof;
      if(low)zones.push([low.a-34,low.b+36]);
      c.save();c.beginPath();
      zones.forEach(([a,b])=>c.rect(room.x+a,this.world.bounds.y-100,b-a,
        this.world.bounds.h-this.world.bounds.y+200));
      c.clip();draw();c.restore();
    }
    backwall() {
      const c=this.ctx,b=this.world.bounds;
      const minX=this.overview?0:Math.max(0,this.camera.x-623);
      const maxX=this.overview?b.w:Math.min(b.w,this.camera.x+this.width+623);
      const minY=Math.floor(b.y/623)*623,maxY=b.h;
      for(let x=Math.floor(minX/623)*623;x<maxX;x+=623)
        for(let y=minY;y<maxY;y+=623) {
          const flipX=Math.abs(Math.round(x/623)%2),flipY=Math.abs(Math.round(y/623)%2);
          c.save();c.translate(x+(flipX?623:0),y+(flipY?623:0));c.scale(flipX?-1:1,flipY?-1:1);
          c.drawImage(this.assets.back,0,0,623,623);c.restore();
        }
      c.fillStyle="rgba(17,39,37,.13)";c.fillRect(0,b.y,b.w,b.h-b.y);
    }
    grass(p,t,hero) {
      if(this.assets.plants){this.plantSprite(this.assets.plants[p.hue===2?"fern":"grass"],p,t,hero,"grass");return;}
      const c=this.ctx,near=clamp(1-Math.abs(hero.x-p.x)/48,0,1)*clamp(1-Math.abs(hero.y-p.y)/70,0,1);
      const sway=Math.sin(t*1.7+p.phase)*.055+near*hero.facing*.26;
      c.save();c.translate(p.x,p.y);c.rotate(sway);c.scale(p.scale,p.scale);
      const greens=["#708c3b","#8b9f4a","#4e713c"];
      for(let i=0;i<7;i++) {
        const tilt=(i-3)*.24,h=17+(i%3)*7,tipx=Math.sin(tilt)*h*1.1;
        c.beginPath();c.moveTo(0,0);c.quadraticCurveTo(tipx*.4-4,-h*.55,tipx,-h);
        c.quadraticCurveTo(tipx*.55+4,-h*.55,2,0);c.fillStyle=greens[(i+p.hue)%3];c.fill();
        c.strokeStyle="#263a28";c.lineWidth=1.1;c.stroke();
      }
      c.restore();
    }
    mushroom(p,t,hero) {
      if(this.assets.plants){this.plantSprite(this.assets.plants[["orange","teal","ivory"][p.hue]],p,t,hero,"mushroom");return;}
      const c=this.ctx,near=clamp(1-Math.abs(hero.x-p.x)/32,0,1)*clamp(1-Math.abs(hero.y-p.y)/60,0,1);
      c.save();c.translate(p.x,p.y);c.rotate(Math.sin(t*1.35+p.phase)*.025+near*hero.facing*.13);
      c.scale(p.scale*(1+near*.09),p.scale*(1-near*.1));
      this.path([[-3,0],[-2,-20],[3,-22],[4,0]],"#d0c59b","#26342a",1.2);
      c.beginPath();c.moveTo(-15,-17);c.bezierCurveTo(-12,-38,12,-36,17,-17);
      c.bezierCurveTo(7,-12,-7,-12,-15,-17);c.fillStyle=["#bf734a","#70a19a","#c1b88e"][p.hue];c.fill();
      c.strokeStyle="#28362d";c.lineWidth=1.6;c.stroke();
      this.path([[-12,-17],[11,-17]],null,"#453e2e",1,false);
      c.fillStyle="#dccd9a";c.fillRect(-7,-25,3,2);c.fillRect(4,-29,2,2);c.restore();
    }
    vine(p,t) {
      if(this.assets.plants){this.plantSprite(this.assets.plants.vine,p,t,{x:-1000,y:-1000,facing:1},"vine");return;}
      const c=this.ctx,h=37+p.scale*29,sway=Math.sin(t*.95+p.phase)*5;
      c.save();c.translate(p.x,p.y);c.strokeStyle="#475e32";c.lineWidth=2.4;
      c.beginPath();c.moveTo(0,0);c.bezierCurveTo(-4,h*.3,sway,h*.7,sway,h);c.stroke();
      for(let i=1;i<6;i++) {
        const y=i*h/6,x=sway*(y/h)**2,side=i%2?1:-1;
        this.path([[x,y],[x+side*9,y+3],[x+side*6,y+11],[x,y+6]],i%2?"#7d9446":"#58783b","#263e29",.8);
      }
      c.restore();
    }
    plantSprite(image,p,t,hero,kind) {
      const c=this.ctx,near=clamp(1-Math.abs(hero.x-p.x)/42,0,1)*clamp(1-Math.abs(hero.y-p.y)/62,0,1);
      const h=(kind==="vine"?67:kind==="mushroom"?36:31)*p.scale,w=h*image.width/image.height;
      const squash=kind==="mushroom"?1-near*.12:1,rows=Math.min(96,image.height);
      c.save();c.translate(p.x,p.y);c.scale(1+(1-squash)*.35,squash);
      if(kind==="mushroom")c.rotate(Math.sin(t*1.4+p.phase)*.025+near*hero.facing*.11);
      for(let i=0;i<rows;i++) {
        const weight=kind==="vine"?(i/(rows-1))**1.5:(1-i/(rows-1))**1.7;
        const bend=(Math.sin(t*(kind==="vine"?.9:1.7)+p.phase)*2.1+near*hero.facing*7)*weight;
        const sh=image.height/rows,y=(kind==="vine"?0:-h)+i*h/rows;
        c.drawImage(image,0,i*sh,image.width,Math.min(sh+.5,image.height-i*sh),-w/2+bend,y,w,h/rows+.25);
      }
      c.restore();
    }
    roots(room,t) {
      const c=this.ctx,px=room.x+455,py=room.y+sample(room.profile.roof,455)-12;
      c.save();c.translate(px,py);
      for(let i=0;i<4;i++) {
        const h=35+i*22,sway=Math.sin(t*.8+i)*2.4;
        c.beginPath();c.moveTo((i-2)*18,-12);c.bezierCurveTo(-18+i*14,25,30+i*6+sway,h*.6,20+i*9+sway,h);
        c.strokeStyle="#1c2820";c.lineWidth=8-i;c.stroke();c.strokeStyle="#7a6544";c.lineWidth=5-i*.6;c.stroke();
        this.vine({x:21+i*9,y:h-7,scale:.7,phase:i},t);
      }
      c.restore();
    }
    stream(room,water,t,layer="water") {
      if(water.paths){water.paths.forEach(p=>this.stream(room,{...water,...p,paths:null},t,layer));return;}
      if(!water.points||water.points.length<2)return;
      const c=this.ctx,base=this.local(water.points,room).sort((a,b)=>a[0]-b[0]),points=[];
      const min=base[0][0],max=base.at(-1)[0],span=max-min,width=water.width||water.h||11;
      if(span<=0)return;
      const count=Math.max(8,Math.ceil(span/12));
      for(let i=0;i<=count;i++) {
        const x=min+span*i/count;
        points.push([x,sample(base,x)+Math.sin(i*1.9)*.45]);
      }
      const upper=points.map((p,i)=>[p[0],p[1]-width*.38-Math.sin(i*1.7)*.5]);
      const lower=points.map((p,i)=>[p[0],p[1]+width*.48+Math.sin(i*1.4)*.6]);
      const outline=upper.concat(lower.slice().reverse());
      if(layer==="bed") {
        this.path(upper.map(p=>[p[0],p[1]-3]).concat(lower.slice().reverse().map(p=>[p[0],p[1]+4])),
          "#343c31","#292e24",1.4);
        this.path(upper.map(p=>[p[0],p[1]-2]),null,"#84734b",3.5,false);
        return;
      }
      if(layer==="front") {
        const bank=lower.map((p,i)=>[p[0],p[1]+3+(i%3)*.6]);
        this.mass(lower.concat(bank.slice().reverse()),lower,false);
        (water.stones||[]).forEach(stone=>{
          const x=room.x+stone.x,y=room.y+stone.y,w=stone.w||13;
          this.mass([[x-w,y],[x-w*.6,y-5],[x+w*.4,y-6],[x+w,y],[x+w*.6,y+4],[x-w*.7,y+3]],
            [[x-w*.6,y-5],[x+w*.4,y-6],[x+w,y]]);
        });
        return;
      }
      this.path(outline,"#3e7770","#2d5751",.8);
      const dir=water.dir||((base.at(-1)[1]>=base[0][1])?1:-1);
      c.save();c.beginPath();outline.forEach((p,i)=>i?c.lineTo(...p):c.moveTo(...p));c.closePath();c.clip();
      for(let n=0;n<Math.ceil(span/18);n++) {
        const x=min+((n*37+t*25*dir)%span+span)%span;
        const y=sample(points,x)+(n%3-1)*width*.22;
        this.path([[x-3,y],[x+4,y+.2],[x+10+(n%2)*4,y-.3]],null,
          n%4?"#83b7a0":"#c4dac2",n%4?.85:1.3,false);
      }
      c.restore();
      const end=dir>0?points.at(-1):points[0],start=dir>0?points[0]:points.at(-1);
      const source=typeof water.source==="object"?water.source.kind:water.source;
      const sink=typeof water.sink==="object"?water.sink.kind:water.sink;
      if(source==="spring")this.path([[start[0]-4,start[1]-5],[start[0]+1,start[1]-2],start],null,"#172f29",3,false);
      if(sink==="seep")this.path([end,[end[0]+dir*5,end[1]+2],[end[0]+dir*9,end[1]+6]],null,"#203c32",2,false);
      if(water.pool)this.pool(room,water.pool,t);
    }
    pool(room,pool,t) {
      const c=this.ctx,x=room.x+pool.x,y=room.y+pool.y,rx=pool.rx||pool.w/2||20,ry=pool.ry||5;
      c.fillStyle="#315e55";c.beginPath();c.ellipse(x,y,rx,ry,0,0,Math.PI*2);c.fill();
      c.strokeStyle="#8fbfa8";c.lineWidth=.8;
      for(let n=0;n<3;n++) {
        const phase=(t*.65+n/3)%1;c.globalAlpha=(1-phase)*.6;
        c.beginPath();c.ellipse(x,y,3+phase*rx,1+phase*ry*.6,0,0,Math.PI*2);c.stroke();
      }
      c.globalAlpha=1;
    }
    fall(room,w,t,layer="water") {
      const c=this.ctx,x=room.x+w.x,y=room.y+w.y,bottom=y+w.h;
      if(layer==="front") {
        this.path([[x-13,bottom+4],[x-5,bottom+7],[x+w.w+12,bottom+5]],null,"#796d48",3,false);
        return;
      }
      this.pool(room,w.pool||{x:w.x+w.w/2,y:w.y+w.h,rx:20,ry:5},t);
      c.beginPath();c.moveTo(x,y);c.bezierCurveTo(x-3,y+w.h*.3,x+3,bottom-20,x-3,bottom);
      c.lineTo(x+w.w+4,bottom);c.bezierCurveTo(x+w.w-2,bottom-25,x+w.w+3,y+30,x+w.w,y);
      c.closePath();c.fillStyle="#529f99";c.fill();c.strokeStyle="#285e59";c.lineWidth=1;c.stroke();
      for(let n=0;n<9;n++) {
        const fy=y+(n*23+t*95)%Math.max(1,w.h-10),fx=x+3+(n%3)*3+Math.sin(fy*.09+t)*1.4;
        this.path([[fx,fy],[fx+Math.sin(n+t),fy+10]],null,"#b5e1cb",1.5,false);
      }
      for(let n=0;n<7;n++) {
        const phase=((t*1.6+n/7)%1),rx=x+w.w/2+(n%2?1:-1)*phase*15,ry=bottom-Math.sin(phase*Math.PI)*9;
        c.fillStyle="rgba(183,223,197,"+(1-phase)*.75+")";c.beginPath();c.ellipse(rx,ry,2+phase*2,1.3,phase,0,Math.PI*2);c.fill();
      }
      for(let n=0;n<3;n++){const ph=(t*.8+n/3)%1;c.strokeStyle="rgba(170,222,195,"+(1-ph)*.6+")";
        c.beginPath();c.ellipse(x+w.w/2,bottom+2,5+ph*20,2+ph*3,0,0,Math.PI*2);c.stroke();}
    }
    terrain(room,state,t) {
      const c=this.ctx,shape=room.shape||room.profile,floor=this.local(shape.floor,room),roof=this.local(shape.roof,room);
      const road=this.road(floor,shape.road,room);
      const face=[...road.near,[room.x+623,this.world.bounds.h+100],[room.x,this.world.bounds.h+100]];
      this.mass(face,null,false);this.path(face,"rgba(15,13,10,.17)",null);
      this.mass([[room.x,this.world.bounds.y-100],[room.x+623,this.world.bounds.y-100],...roof.slice().reverse()],null,false);
      this.changedRoad(room,()=>this.roadTop(road));
      c.drawImage(this.roomImages.get(room.id),room.x,room.y);
      this.changedRoof(room,()=>this.path(roof,null,"#25271f",1.7,false));
      const builder=surface=>state.objects.find(object=>object.building&&
        (object.surfaceIds||[object.surfaceId||object.id]).includes(surface.id));
      state.surfaces.filter(s=>s.roomId===room.id&&["ledge","crumble","bridge"].includes(s.kind)&&
        (s.enabled!==false||builder(s))).forEach(s=>{
        if(s.kind==="bridge") {
          const [a,b]=s.points,phase=builder(s)?.building;
          const build=phase?clamp((state.time-phase.startedAt)/phase.duration,0,1):1;
          const n=Math.ceil((b[0]-a[0])/12);
          c.save();c.beginPath();
          c.rect(a[0]-1,Math.min(a[1],b[1])-8,(b[0]-a[0])*build+1,Math.abs(b[1]-a[1])+44);c.clip();
          for(let i=0;i<n*build;i++){const x=a[0]+i*12,y=sample(s.points,x)+Math.sin(t*3+i*.25)*.35;
            this.path([[x,y-4],[x+11,y-4],[x+11,y+6],[x,y+6]],i%2?"#9e794c":"#b38c56",color.ink,1.1);
            this.path([[x,y+6],[x+11,y+6],[x+11,y+10],[x,y+10]],"#5e4933",color.ink,.7);}
          this.path([[a[0],a[1]+7],[a[0]+20,a[1]+17],[b[0]-20,b[1]+17],[b[0],b[1]+7]],null,"#6f6542",2,false);
          c.restore();
        } else {
          const crumb=state.objects.find(o=>o.surfaceId===s.id&&o.type==="crumble");
          c.save();if(crumb?.warning)c.translate(Math.sin(t*45)*1.8,0);
          const authored=(shape.ledges||[]).find(ledge=>ledge.id===s.id||s.id.endsWith("-"+ledge.id));
          if(authored?.backSocket) {
            const socket=Array.isArray(authored.backSocket)?authored.backSocket:authored.backSocket.polygon;
            if(socket) {
              const wall=this.local(socket,room);this.mass(wall,null,false);
              this.path(wall,"rgba(14,23,19,.28)",null);
              const first=wall[0],last=wall.at(-1);
              this.path([[first[0]-2,first[1]+3],[first[0]+8,first[1]+15],last],null,"#232a22",1.2,false);
            }
          }
          if(authored?.bodyPolygon) {
            const ledgeRoad=this.road(s.points,{far:s.points.map(p=>[p[0],p[1]-5]),
              near:s.points.map(p=>[p[0],p[1]+7]),lipDepth:5});
            const body=this.local(authored.bodyPolygon,room);
            body.splice(0,s.points.length,...ledgeRoad.near);
            this.mass(body,null);this.roadTop(ledgeRoad);this.roadLip(ledgeRoad);
          }
          else if(s.kind==="crumble") {
            const a=s.points[0],b=s.points.at(-1);
            const ledgeRoad=this.road(s.points,{far:s.points.map(p=>[p[0],p[1]-4]),
              near:s.points.map(p=>[p[0],p[1]+5]),lipDepth:4});
            this.mass([...ledgeRoad.near,[b[0]-4,b[1]+15],[a[0]+6,a[1]+19]],null);
            this.roadTop(ledgeRoad);this.roadLip(ledgeRoad);
          }
          c.restore();
        }
      });
    }
    connector(link,index) {
      const a=this.world.rooms[index],b=this.world.rooms[index+1],x=a.x+623;
      const floor=link.shape?.floor||(Array.isArray(link.floor)?link.floor:[[x,link.floor],[b.x,link.floor]]);
      const roof=link.shape?.roof||(Array.isArray(link.roof)?link.roof:
        [[x,a.y+sample((a.shape||a.profile).roof,623)],[b.x,b.y+sample((b.shape||b.profile).roof,0)]]);
      const road=this.road(floor,link.shape?.road);
      const face=[...road.near,[b.x,this.world.bounds.h+100],[x,this.world.bounds.h+100]];
      this.mass(face,null,false);this.path(face,"rgba(15,13,10,.17)",null);
      this.mass([[x,this.world.bounds.y-100],[b.x,this.world.bounds.y-100],...roof.slice().reverse()],null,false);
      this.path(roof,null,"#25271f",1.7,false);this.roadTop(road);
    }
    terminal() {
      const cap=this.world.boundaries?.right;if(!cap)return;
      const road=this.road(cap.shape.floor,cap.road),top=this.world.bounds.y-100,bottom=this.world.bounds.h+100;
      const face=[...road.near,[cap.wallX,bottom],[cap.x0,bottom]];
      this.mass(face,null,false);this.path(face,"rgba(15,13,10,.17)",null);
      this.mass([[cap.x0,top],[cap.wallX,top],...cap.shape.roof.slice().reverse()],null,false);
      this.roadTop(road);
      this.mass([[cap.wallX,top],[cap.x1,top],[cap.x1,bottom],[cap.wallX,bottom]],null,false);
      this.path([[cap.wallX,cap.roof],[cap.wallX,cap.floor+12]],null,"#25271f",2,false);
    }
    plantBase(p) {
      if(p.kind==="vine"||p.baseCover===false)return;
      const w=4.5*p.scale;
      this.path([[p.x-w,p.y],[p.x-2,p.y-1.5],[p.x+w,p.y+.5],[p.x+w*.6,p.y+2],[p.x-w*.8,p.y+1.6]],
        "#807047","#3f432e",.65);
    }
    objects(state,t) {
      const c=this.ctx;
      state.objects.forEach(o=>{
        if(o.collected)return;
        if(o.type==="ore"||o.type==="looseOre") {
          c.save();c.translate(o.x,o.y-3);const s=o.primary?1:.76;c.scale(s,s);
          this.path([[-12,-1],[-16,-13],[-8,-24],[-1,-15],[5,-29],[14,-18],[12,-2]],"#c39647","#252a20",1.5);
          this.path([[-8,-24],[-4,-8],[5,-29],[7,-8]],null,"#76562f",1,false);c.restore();
          if(o.primary&&Math.hypot(o.x-state.hero.x,o.y-state.hero.y)<240) {
            c.strokeStyle="rgba(213,183,92,.7)";c.beginPath();c.ellipse(o.x,o.y-15,23+Math.sin(t*2)*2,22,0,0,Math.PI*2);c.stroke();
          }
        } else if(o.type==="supply") {
          for(let n=0;n<3;n++)this.path([[o.x-16,o.y-4-n*5],[o.x+17,o.y-4-n*5]],null,n%2?"#a48b57":"#bca16b",5,false);
          this.path([[o.x-7,o.y-18],[o.x-7,o.y]],null,"#424b37",2,false);
          this.path([[o.x+8,o.y-18],[o.x+8,o.y]],null,"#424b37",2,false);
        } else if(o.type==="chute") {
          this.path([[o.x-12,o.y-5],[o.x,o.y+2],[o.x+12,o.y-5]],null,"#a2935b",3,false);
          this.path([[o.x,o.y-13],[o.x,o.y-4]],null,"#758e72",2,false);
        } else if(o.type==="stash") {
          this.path([[o.x-21,o.y],[o.x-21,o.y-24],[o.x+18,o.y-24],[o.x+18,o.y]],"#886e47","#25291e",2);
          this.path([[o.x-25,o.y-26],[o.x+22,o.y-26]],null,"#b79c69",5,false);
          c.fillStyle="#d9ba69";c.beginPath();c.arc(o.x-45,o.y-46,4+Math.sin(t*2)*.4,0,Math.PI*2);c.fill();
          this.path([[o.x-45,o.y],[o.x-45,o.y-42]],null,"#6b6842",3,false);
        }
      });
    }
    eventEffects(state,t) {
      state.events.filter(e=>e.id>this.seenEvent).forEach(e=>{
        this.seenEvent=Math.max(this.seenEvent,e.id);
        const count=e.type==="crumble"?35:e.type==="mining"?22:e.type==="bridge"?18:7;
        for(let i=0;i<count;i++)this.particles.push({x:e.x,y:e.y-15,vx:Math.sin(i*2.4)*22,vy:-15-(i%6)*6,start:t,life:.45+(i%7)*.09,stone:i%3===0});
      });
      this.particles=this.particles.filter(p=>t-p.start<p.life).slice(-160);
      const c=this.ctx;this.particles.forEach(p=>{
        const age=t-p.start,fade=1-age/p.life;c.globalAlpha=fade;
        const x=p.x+p.vx*age,y=p.y+p.vy*age+70*age*age;
        if(p.stone)this.path([[x-2,y],[x,y-3],[x+3,y+2]],"#a09473","#35382b",.7);
        else {c.fillStyle="#a7956c";c.beginPath();c.ellipse(x,y,2+age*4,1+age*3,0,0,Math.PI*2);c.fill();}
      });c.globalAlpha=1;
      (state.transfers||[]).forEach(transfer=>{
        const ph=clamp((state.time-transfer.startedAt)/transfer.duration,0,1);
        const x=transfer.from.x+(transfer.to.x-transfer.from.x)*ph;
        const y=transfer.from.y+(transfer.to.y-transfer.from.y)*ph;
        this.path([[x-7,y-2],[x-10,y-12],[x,y-18],[x+9,y-9],[x+6,y-1]],"#c39647","#252a20",1.3);
      });
    }
    hero(hero,t) {
      const c=this.ctx,move=hero.grounded?Math.sin(t*10)*Math.min(1,Math.abs(hero.vx)/60):0;
      c.save();c.translate(hero.x,hero.y);c.scale(hero.facing,1);
      c.fillStyle="rgba(12,19,15,.28)";c.beginPath();c.ellipse(0,1,14,3,0,0,Math.PI*2);c.fill();
      c.rotate(move*.025);c.drawImage(this.assets.hero,-20,-42-Math.abs(move),38,42);
      if(hero.carry)this.path([[-10,-44],[-13,-55],[-5,-63],[1,-57],[7,-67],[13,-55],[10,-43]],"#c39b51","#222b21",1.4);
      if(!hero.grounded){c.strokeStyle="#b5d0b0";c.lineWidth=1.3;c.beginPath();c.moveTo(-5,0);c.lineTo(-7,3);c.stroke();}
      c.restore();
    }
    draw(state,clock,dt) {
      const size=this.size(),c=this.ctx,t=this.motion?clock:0,world=this.world,h=state.hero;
      const focused=this.focus&&world.rooms.find(r=>r.id===this.focus);
      let tx=focused?focused.x+312-this.width/2:h.x-this.width*.37,ty=focused?focused.y+215-this.height*.5:h.y-this.height*.68;
      if(focused?.authoredArt&&!this.inspectionTarget) {
        const art=focused.authoredArt;
        tx=focused.x+(art.x||0)+art.drawWidth/2-this.width/2;
        ty=focused.y+(art.y||0)+art.drawHeight/2-this.height/2;
      }
      if(this.inspectionTarget){tx=this.inspectionTarget.x-this.width/2;ty=this.inspectionTarget.y-this.height/2;}
      tx=clamp(tx,0,Math.max(0,world.bounds.w-this.width));ty=clamp(ty,world.bounds.y,world.bounds.h-this.height);
      const ease=1-Math.exp(-dt*8);this.camera.x+=(tx-this.camera.x)*ease;this.camera.y+=(ty-this.camera.y)*ease;
      c.setTransform(size.scale,0,0,size.scale,0,0);c.fillStyle="#202b27";c.fillRect(0,0,this.width,this.height);
      c.save();
      if(this.overview){const z=this.width/world.bounds.w;c.scale(z,z);c.translate(0,80/z-world.bounds.y);}
      else c.translate(-this.camera.x,-this.camera.y);
      const visible=world.rooms.filter(r=>this.overview||r.x+623>this.camera.x-100&&r.x<this.camera.x+this.width+100);
      this.backwall();
      world.connections.forEach((link,index)=>this.connector(link,index));
      const plantVisible=p=>{
        if(!p.supportId)return true;
        const support=state.surfaces.find(s=>s.id===p.supportId);
        return !support||support.enabled!==false;
      };
      visible.forEach(room=>{
        if(room.ecology==="falls")this.roots(room,t);
        room.plants.filter(p=>p.kind==="vine"&&plantVisible(p)).forEach(p=>this.vine({...p,x:p.x+room.x,y:p.y+room.y},t));
        room.plants.filter(p=>p.kind!=="vine"&&p.layer==="back"&&plantVisible(p)).forEach(p=>{
          const placed={...p,x:p.x+room.x,y:p.y+room.y};
          p.kind==="mushroom"?this.mushroom(placed,t,h):this.grass(placed,t,h);
        });
        room.water.filter(w=>w.type==="fall").forEach(w=>this.fall(room,w,t));
        this.terrain(room,state,t);
        room.water.filter(w=>w.type==="stream").forEach(w=>{
          this.stream(room,w,t,"bed");this.stream(room,w,t);
        });
        room.water.filter(w=>w.type==="fall").forEach(w=>
          this.pool(room,w.pool||{x:w.x+w.w/2,y:w.y+w.h,rx:20,ry:5},t));
        room.plants.filter(p=>p.kind!=="vine"&&p.layer!=="back"&&plantVisible(p)).forEach(p=>{
          const placed={...p,x:p.x+room.x,y:p.y+room.y};
          p.kind==="mushroom"?this.mushroom(placed,t,h):this.grass(placed,t,h);
          this.plantBase(placed);
        });
      });
      this.terminal();
      this.objects(state,t);this.hero(h,t);
      world.connections.forEach((link,index)=>{
        const floor=link.shape?.floor||[[world.rooms[index].x+623,link.floor],[world.rooms[index+1].x,link.floor]];
        this.roadLip(this.road(floor,link.shape?.road));
      });
      visible.forEach(room=>{
        const shape=room.shape||room.profile;
        this.changedRoad(room,()=>this.roadLip(this.road(this.local(shape.floor,room),shape.road,room)));
        room.water.forEach(w=>{
          if(w.type==="stream")this.stream(room,w,t,"front");
          else if(w.type==="fall")this.fall(room,w,t,"front");
        });
      });
      const cap=world.boundaries?.right;if(cap)this.roadLip(this.road(cap.shape.floor,cap.road));
      this.eventEffects(state,t);
      if(this.debug) {
        state.surfaces.filter(s=>s.enabled!==false).forEach(s=>this.path(s.points,null,"#b1e5a4",1.2,false));
        state.objects.filter(o=>o.type==="lowroof").forEach(o=>{c.strokeStyle="#dba76d";c.strokeRect(o.x,o.y,o.w,o.h);});
        c.strokeStyle="#c8e8ba";c.strokeRect(h.x-h.w/2,h.y-h.h,h.w,h.h);
      }
      c.restore();
      if(this.overview) {
        const shorts={camp:"营地",narrow:"窄洞",wet:"湿区",engineering:"工程",cargo:"货槽",fungi:"菌厅",falls:"泉瀑",mine:"矿室"};
        c.font="13px sans-serif";c.textAlign="center";c.fillStyle="#ded7b6";
        world.rooms.forEach(r=>c.fillText(shorts[r.slot],(r.x+312)/world.bounds.w*this.width,48));
        c.fillStyle="#9ed1bc";const px=h.x/world.bounds.w*this.width;
        this.path([[px-4,63],[px+4,63],[px,70]],"#8dc1ad",null);
      }
    }
  }
  root.EcologyRenderer=EcologyRenderer;
})(globalThis);
