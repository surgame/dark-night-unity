(function(root){
  "use strict";
  const api=root.ShapeStudy,library=root.CaveWB;
  class Renderer{
    constructor(canvas,atlas,host,hero){
      this.canvas=canvas;this.atlas=atlas;this.host=host;this.hero=hero;
      this.swatch=document.createElement("span");this.swatch.hidden=true;host.append(this.swatch);
    }
    color(token){
      this.swatch.style.color="var("+token+")";return getComputedStyle(this.swatch).color;
    }
    colors(){
      return{bg:this.color("--background"),fg:this.color("--foreground"),muted:this.color("--muted"),
        border:this.color("--border"),a:this.color("--viz-series-1"),b:this.color("--viz-series-2")};
    }
    prepare(canvas){
      const r=canvas.getBoundingClientRect(),ratio=Math.min(2,devicePixelRatio||1);
      canvas.width=Math.round(r.width*ratio);canvas.height=Math.round(r.height*ratio);
      const c=canvas.getContext("2d");c.setTransform(ratio,0,0,ratio,0,0);
      return{c,w:r.width,h:r.height};
    }
    polygon(c,points){
      c.moveTo(points[0][0],points[0][1]);points.slice(1).forEach(p=>c.lineTo(...p));c.closePath();
    }
    contours(c,map,rect){
      if(map.polys){
        c.beginPath();if(!rect)c.rect(-1,-1,api.W+2,api.H+2);
        map.polys.forEach(points=>this.polygon(c,points));return;
      }
      c.beginPath();if(rect)c.rect(0,0,map.width,map.height);
      map.geometry.contours.forEach(contour=>{
        const p=contour.points;c.moveTo(p[0].x,p[0].y);
        p.slice(1).forEach(v=>c.lineTo(v.x,v.y));c.closePath();
      });
    }
    boundary(c,map,color,width,extendedOnly=false){
      c.strokeStyle=color;c.lineWidth=width;
      if(map.hybridEdges){
        c.beginPath();map.hybridEdges.forEach(([a,b])=>{
          if([a,b].some(p=>p.x<0||p.x>api.W||p.y<0||p.y>api.H))return;
          c.moveTo(a.x,a.y);c.lineTo(b.x,b.y);
        });c.stroke();return;
      }
      if(!extendedOnly){this.contours(c,map,false);c.stroke();return;}
      map.tiles.forEach((id,i)=>{
        const tile=library.catalog[id],x=i%api.W,y=Math.floor(i/api.W);
        tile.segments.forEach(segment=>{
          if(!segment.some(hit=>Math.abs((hit.edge%2?hit.point[1]:hit.point[0])-.5)>.01))return;
          c.beginPath();c.moveTo(x+segment[0].point[0],y+segment[0].point[1]);
          c.lineTo(x+segment[1].point[0],y+segment[1].point[1]);c.stroke();
        });
      });
    }
    draw(pair,state){
      const {c,w,h}=this.prepare(this.canvas),colors=this.colors();
      c.fillStyle=colors.bg;c.fillRect(0,0,w,h);
      const span=state.camera==="close"?28:api.W+1;
      const scale=Math.min(w/span,h/(state.camera==="close"?17:api.H+1));
      const cx=state.camera==="close"?Math.max(14,Math.min(34,pair.plan.focus.x-4)):api.W/2;
      const cy=state.camera==="close"?Math.min(20,pair.plan.focus.y-1):api.H/2;
      const ox=w/2-cx*scale,oy=h/2-cy*scale;
      this.viewport={scale,ox,oy,w,h};
      c.save();c.translate(ox,oy);c.scale(scale,scale);
      const map=pair[state.method];
      c.fillStyle=colors.fg;c.globalAlpha=.17;
      this.contours(c,map,true);c.fill("evenodd");c.globalAlpha=1;
      c.save();this.contours(c,map,true);c.clip("evenodd");c.globalAlpha=.09;
      c.save();c.translate(0,.32);this.boundary(c,map,colors.fg,.65);c.restore();
      c.translate(0,-.32);this.boundary(c,map,colors.fg,.65);c.restore();
      this.boundary(c,map,colors.fg,1.35/scale);
      if(state.overlay){
        this.boundary(c,pair.dual,colors.a,2/scale);
        this.boundary(c,state.method==="dual"?pair.wfc:map,colors.b,1.6/scale);
      }else if(state.method==="wfc")this.boundary(c,map,colors.b,1.8/scale,true);
      const water=pair.plan.water;
      if(water){
        c.save();this.contours(c,map,false);c.clip("evenodd");
        c.fillStyle=colors.fg;c.globalAlpha=.06;c.fillRect(water.x-4,water.level,8,6);
        c.globalAlpha=.55;c.strokeStyle=colors.fg;c.lineWidth=1/scale;c.setLineDash([3/scale,3/scale]);
        c.beginPath();c.moveTo(water.x,10);c.lineTo(water.x,water.level);c.stroke();
        c.setLineDash([]);c.beginPath();c.moveTo(water.x-4,water.level);c.lineTo(water.x+4,water.level);c.stroke();c.restore();
        c.save();this.contours(c,map,false);c.clip("evenodd");
        c.strokeStyle=colors.fg;c.globalAlpha=.28;c.lineWidth=.8/scale;
        for(const dx of [-.22,.22]){
          c.beginPath();c.moveTo(water.x+dx,10);c.lineTo(water.x+dx,water.level-.2);c.stroke();
        }
        for(const [dx,dy] of [[-2,.4],[.5,.8],[-.8,1.2]]){
          c.beginPath();c.moveTo(water.x+dx,water.level+dy);c.lineTo(water.x+dx+.9,water.level+dy);c.stroke();
        }c.restore();
      }
      if(pair.plan.floor){
        for(const x of [9,16,37]){
          const y=api.groundAt(map,x);c.strokeStyle=colors.fg;c.lineWidth=1/scale;
          c.beginPath();c.moveTo(x-.3,y);c.lineTo(x-.6,y-.55);
          c.moveTo(x-.1,y);c.lineTo(x,y-.7);c.moveTo(x+.12,y);c.lineTo(x+.4,y-.45);c.stroke();
        }
        for(const x of [17,39]){
          const y=api.groundAt(map,x);c.fillStyle=colors.bg;c.strokeStyle=colors.fg;c.lineWidth=1/scale;
          c.beginPath();c.moveTo(x,y);c.lineTo(x,y-.7);c.stroke();
          c.beginPath();c.ellipse(x,y-.75,.5,.3,0,Math.PI,Math.PI*2);c.closePath();c.fill();c.stroke();
        }
        pair.plan.markers.filter(m=>m.kind==="ore"||m.kind==="break").forEach(marker=>{
          const offsets=marker.kind==="ore"?[[0,0],[.8,.6],[-.5,.7]]:[[0,0]];
          c.strokeStyle=colors.fg;c.lineWidth=1/scale;c.fillStyle=colors.bg;
          offsets.forEach(([dx,dy])=>{
            const points=[[-.5,0],[-.25,-.4],[.4,-.25],[.55,.2],[0,.5]]
              .map(p=>[p[0]+marker.x+dx,p[1]+marker.y+dy]);
            c.beginPath();this.polygon(c,points);c.globalAlpha=.5;c.fill();c.globalAlpha=1;c.stroke();
          });
        });
      }
      if(state.grid){
        c.strokeStyle=colors.border;c.lineWidth=.65/scale;c.globalAlpha=.8;
        c.beginPath();for(let x=0;x<=api.W;x++){c.moveTo(x,0);c.lineTo(x,api.H);}
        for(let y=0;y<=api.H;y++){c.moveTo(0,y);c.lineTo(api.W,y);}c.stroke();c.globalAlpha=1;
        if(state.method==="hybrid"){
          c.save();c.strokeStyle=colors.b;c.globalAlpha=.5;c.setLineDash([4/scale,5/scale]);
          c.beginPath();for(let x=8;x<api.W;x+=8){c.moveTo(x,0);c.lineTo(x,api.H);}c.stroke();c.restore();
        }
        const seen=new Set();
        map.tiles.forEach((id,index)=>{
          const tile=library.catalog[id];if(tile.mask===0||tile.mask===15)return;
          const x=index%api.W,y=Math.floor(index/api.W);
          [[0,0],[1,0],[1,1],[0,1]].forEach(([dx,dy],i)=>{
            const key=(y+dy)*(api.W+1)+x+dx;if(seen.has(key))return;seen.add(key);
            c.beginPath();c.arc(x+dx,y+dy,(Math.abs(tile.values[i])===3?2.1:1.4)/scale,0,Math.PI*2);
            c.fillStyle=tile.values[i]>0?colors.fg:colors.bg;c.fill();c.strokeStyle=colors.fg;c.stroke();
          });
        });
      }
      if(state.dug){
        c.strokeStyle=colors.a;c.lineWidth=1.4/scale;
        const width=map.cellWidth||api.W,offset=map.cellWidth?-.5:0;
        (map.changed||[]).forEach(index=>c.strokeRect(index%width+offset,Math.floor(index/width)+offset,1,1));
      }
      if(state.selected!==null){
        const x=state.selected%api.W,y=Math.floor(state.selected/api.W);
        c.fillStyle=colors.a;c.globalAlpha=.14;c.fillRect(x,y,1,1);c.globalAlpha=1;
        c.strokeStyle=colors.a;c.lineWidth=2/scale;c.strokeRect(x,y,1,1);
      }
      const hx=pair.plan.heroX,foot=api.groundAt(map,hx),heroH=3,heroW=3*this.hero.width/this.hero.height;
      c.drawImage(this.hero,hx-heroW/2,foot-heroH,heroW,heroH);
      c.restore();
      c.font="12px sans-serif";c.fillStyle=colors.fg;c.textAlign="left";c.fillText("人物标尺：3格",10,18);
      pair.plan.markers.forEach(marker=>{
        const x=ox+marker.x*scale,y=oy+marker.y*scale;
        if(x<20||x>w-60||y<35||y>h-25)return;
        c.strokeStyle=colors.fg;c.lineWidth=1;
        if(marker.kind==="ore"){
          c.save();c.translate(x,y);c.beginPath();this.polygon(c,[[-8,0],[-3,-7],[7,-4],[10,4],[0,8]]);
          c.stroke();c.restore();
        }else{
          c.beginPath();c.arc(x,y,3,0,Math.PI*2);c.stroke();
        }
        c.fillStyle=colors.fg;c.textAlign="left";c.fillText(marker.label,x+9,y-7);
      });
      if(state.overlay){
        c.fillStyle=colors.a;c.fillText("Dual Grid",10,h-12);
        c.fillStyle=colors.b;c.fillText(state.method==="hybrid"?"组合方案":"扩展 WFC",85,h-12);
      }
    }
    drawAtlas(state){
      const hybrid=state.method==="hybrid",count=hybrid?28:16;
      const narrow=this.atlas.getBoundingClientRect().width<480;
      const columns=narrow?4:hybrid?7:8,rows=Math.ceil(count/columns);
      this.atlas.style.aspectRatio=columns+"/"+rows;
      const {c,w,h}=this.prepare(this.atlas),colors=this.colors();
      c.fillStyle=colors.bg;c.fillRect(0,0,w,h);
      const cellW=w/columns,cellH=h/rows,size=Math.min(cellW-16,cellH-24);
      const tiles=state.method!=="wfc"?Array.from({length:16},(_,mask)=>api.tile16(mask)):
        Array.from({length:16},(_,i)=>{
          const values=[i&1?3:1,i&2?3:1,i&4?-3:-1,i&8?-3:-1];
          const id=values.reduce((id,v,j)=>id|(library.levels.indexOf(v)<<(j*2)),0);
          return library.catalog[id];
        });
      if(hybrid)for(let shape=1;shape<=12;shape++)tiles.push({id:shape,shape});
      this.atlasTiles=tiles;
      tiles.forEach((tile,i)=>{
        const x=(i%columns)*cellW+(cellW-size)/2,y=Math.floor(i/columns)*cellH+4;
        c.save();c.translate(x,y);c.scale(size,size);
        c.fillStyle=colors.fg;c.globalAlpha=.24;c.beginPath();
        (tile.shape?[api.slopePolygon(tile.shape)]:api.polygons(tile)).forEach(p=>this.polygon(c,p));c.fill();c.globalAlpha=1;
        c.lineWidth=1/size;c.strokeStyle=colors.border;c.strokeRect(0,0,1,1);
        if(tile.shape){c.beginPath();this.polygon(c,api.slopePolygon(tile.shape));c.strokeStyle=colors.b;c.stroke();}
        (tile.segments||[]).forEach(s=>{
          c.strokeStyle=state.method!=="wfc"?colors.fg:colors.b;c.lineWidth=1.4/size;
          c.beginPath();c.moveTo(...s[0].point);c.lineTo(...s[1].point);c.stroke();
        });
        c.restore();c.font="12px sans-serif";c.fillStyle=colors.fg;c.textAlign="center";
        c.fillText(tile.shape?"坡 "+tile.shape:state.method!=="wfc"?String(i):"ID "+tile.id,x+size/2,y+size+15);
      });
      this.atlasLayout={columns,cellW,cellH};
    }
    pick(clientX,clientY){
      const r=this.canvas.getBoundingClientRect(),v=this.viewport;
      const x=Math.floor((clientX-r.left-v.ox)/v.scale),y=Math.floor((clientY-r.top-v.oy)/v.scale);
      return x>=0&&x<api.W&&y>=0&&y<api.H?y*api.W+x:null;
    }
  }
  api.Renderer=Renderer;
})(globalThis);
