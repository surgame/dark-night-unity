(function(root){
  "use strict";
  const shape=root.ShapeStudy,api=root.StaticCave;
  class Renderer extends shape.Renderer{
    palette(){
      return{...this.colors(),water:this.color("--blue"),moss:this.color("--green"),
        fungus:this.color("--orange"),danger:this.color("--destructive")};
    }
    path(c,points,color,width){
      c.beginPath();c.moveTo(...points[0]);points.slice(1).forEach(p=>c.lineTo(...p));c.strokeStyle=color;c.lineWidth=width;c.stroke();
    }
    terrain(c,map,p,s){
      c.fillStyle=p.fg;c.globalAlpha=.16;this.contours(c,map,true);c.fill("evenodd");c.globalAlpha=1;
      c.save();this.contours(c,map,true);c.clip("evenodd");
      const rng=root.CaveWB.random(964);
      for(let i=0;i<140;i++){
        const x=rng()*48,y=rng()*28,dx=1.5+rng()*2.5,dy=.8+rng()*1.4;
        c.beginPath();this.polygon(c,[[x,y],[x+dx*.8,y-.2],[x+dx,y+dy*.5],[x+dx*.3,y+dy]]);
        c.fillStyle=p.fg;c.globalAlpha=.025+rng()*.035;c.fill();
      }
      c.globalAlpha=.15;c.save();c.translate(0,.25);this.boundary(c,map,p.fg,.65);c.restore();c.restore();c.globalAlpha=1;
      this.boundary(c,map,p.fg,1.5/s);
    }
    water(c,data,p,s,time){
      const map=data.scene.map,x=30,level=data.scene.plan.water.level,top=9.8;
      c.save();this.contours(c,map,false);c.clip("evenodd");
      c.fillStyle=p.water;c.globalAlpha=.14;c.fillRect(26,level,8,5);
      c.fillStyle=p.water;c.globalAlpha=.4;c.beginPath();
      c.moveTo(x-.5,top);c.lineTo(x+.4,top);c.lineTo(x+.6,level);c.lineTo(x-.4,level);c.closePath();c.fill();
      c.globalAlpha=.8;
      for(let i=0;i<9;i++){
        const y=top+((i*1.7+time*5)%(level-top));
        this.path(c,[[x-.28,y],[x-.15,y+.7],[x+.1,y+1.1]],p.water,1.3/s);
      }
      c.globalAlpha=.85;
      const surface=[];
      for(let i=0;i<=40;i++){const xx=26+i*.2;surface.push([xx,level+Math.sin(i*.6-time*3)*.035]);}
      this.path(c,surface,p.water,1.4/s);
      for(let i=0;i<4;i++){
        const radius=.3+((time*.8+i*.27)%1)*1.8;
        c.beginPath();c.ellipse(x,level+.1,radius,.13,0,0,Math.PI*2);c.strokeStyle=p.water;c.lineWidth=.8/s;c.stroke();
      }
      c.restore();
    }
    coat(c,data,slot,piece,p,s){
      if(!piece.left&&!piece.right)return;
      const upper=[],lower=[],map=data.scene.map;
      for(let i=0;i<=12;i++){
        const t=i/12,x=slot.x-1+t*2,y=shape.groundAt(map,x);
        const tag=piece.left*(1-t)+piece.right*t,h=tag*.13;
        upper.push([x,y-h-.015]);lower.unshift([x,y+.06]);
      }
      c.beginPath();this.polygon(c,[...upper,...lower]);c.fillStyle=p.moss;c.globalAlpha=.52;c.fill();c.globalAlpha=1;
      this.path(c,upper,p.moss,1.1/s);
    }
    object(c,slot,piece,p,s,time){
      const sway=Math.sin(time*1.7+slot.i*.7)*.07;
      c.save();c.translate(slot.x,slot.y-.04);
      if(piece.object==="mushroom"){
        c.rotate(sway);this.path(c,[[0,0],[0,-.9]],p.fg,1.2/s);
        c.beginPath();c.ellipse(0,-.88,.55,.3,0,Math.PI,Math.PI*2);c.closePath();
        c.fillStyle=p.fungus;c.globalAlpha=.78;c.fill();c.globalAlpha=1;c.strokeStyle=p.fg;c.lineWidth=.8/s;c.stroke();
        for(const x of [-.23,.2]){c.beginPath();c.arc(x,-.99,.06,0,Math.PI*2);c.fillStyle=p.bg;c.fill();}
      }else if(piece.object==="crystal"){
        c.fillStyle=p.water;c.globalAlpha=.5;
        for(const [x,y] of [[-.2,-.1],[.2,-.25]]){
          c.beginPath();this.polygon(c,[[x-.2,y],[x-.12,y-.6],[x+.13,y-.8],[x+.26,y-.2]]);c.fill();
        }c.globalAlpha=1;
      }else if(piece.object==="grass"||piece.object==="reed"){
        const h=piece.object==="reed"?1.1:.6;c.strokeStyle=p.moss;c.lineWidth=1.2/s;
        for(const x of [-.25,0,.25]){
          c.beginPath();c.moveTo(x,0);c.quadraticCurveTo(x+sway*2,-h*.6,x+sway*3+.1,-h);c.stroke();
        }
      }c.restore();
    }
    debug(c,data,state,p,s){
      const slots=data.scene.slots;
      if(state.layers&&state.mode==="base"){
        c.globalAlpha=.5;c.strokeStyle=p.a;c.lineWidth=.7/s;c.beginPath();
        for(let x=0;x<=48;x++){c.moveTo(x,0);c.lineTo(x,28);}
        for(let y=0;y<=28;y++){c.moveTo(0,y);c.lineTo(48,y);}c.stroke();c.globalAlpha=1;
      }else if(state.layers&&state.mode==="whole"){
        for(const slot of slots){
          c.strokeStyle=p.b;c.lineWidth=1.3/s;c.strokeRect(slot.x-1,slot.y-1.4,2,2.4);
        }
      }else if(state.layers){
        this.boundary(c,data.scene.map,p.a,2/s);
        for(const slot of slots){
          c.strokeStyle=p.b;c.lineWidth=1.3/s;c.strokeRect(slot.x-.8,slot.y-1.25,1.6,1.45);
        }
      }
      if(state.conflict){
        for(const i of [10,11]){
          const slot=slots[i];c.strokeStyle=p.danger;c.lineWidth=2/s;c.strokeRect(slot.x-1,slot.y-1.5,2,2.7);
        }
      }
    }
    draw(data,state,time=0){
      const {c,w,h}=this.prepare(this.canvas),p=this.palette();
      c.fillStyle=p.bg;c.fillRect(0,0,w,h);
      const scale=Math.min((w-24)/48,(h-45)/28),ox=(w-48*scale)/2,oy=8+(h-45-28*scale)/2;
      this.viewport={scale,ox,oy,w,h};
      c.save();c.beginPath();c.rect(ox,oy,48*scale,28*scale);c.clip();c.translate(ox,oy);c.scale(scale,scale);
      const map=data.scene.map;
      this.terrain(c,map,p,scale);this.water(c,data,p,scale,time);
      if(state.mode!=="base"){
        const result=data[state.mode==="whole"?"whole":"hybrid"];
        data.scene.slots.forEach((slot,i)=>{
          const piece=api.pieces[result.eco[i]];this.coat(c,data,slot,piece,p,scale);this.object(c,slot,piece,p,scale,time);
        });
      }
      const hx=10,foot=shape.groundAt(map,hx),heroH=3,heroW=3*this.hero.width/this.hero.height;
      c.drawImage(this.hero,hx-heroW/2,foot-heroH,heroW,heroH);
      if(state.layers||state.conflict)this.debug(c,data,state,p,scale);
      c.restore();
      c.font="12px sans-serif";c.textAlign="center";c.fillStyle=p.fg;
      const label=state.layers?state.mode==="whole"?"框：地形＋生态完整候选":
        state.mode==="hybrid"?"连续轮廓：基础层；小框：生态层":"基础16型拓扑":"同一固定洞形 · 无采掘 · 水／植物为程序动态示意";
      c.fillText(w<480&&!state.layers?"固定洞形 · 无采掘 · 生态动态示意":label,w/2,h-9);
    }
  }
  api.Renderer=Renderer;
})(globalThis);
