(function (root) {
  "use strict";
  class EcologyAssemblyInspector {
    constructor(options) {
      this.canvas=options.canvas;this.context=this.canvas.getContext("2d");
      this.select=options.select;this.renderer=options.renderer;this.world=options.world;
      this.enabled=false;this.index=0;this.allowedPairs=0;this.totalPairs=0;
      this.swatch=document.createElement("span");this.swatch.hidden=true;
      this.canvas.parentNode.append(this.swatch);
    }
    refresh() {
      const world=this.world();
      this.select.replaceChildren(...world.connections.map((link,index)=>{
        const a=world.rooms.find(room=>room.id===link.from)||world.rooms[index];
        const b=world.rooms.find(room=>room.id===link.to)||world.rooms[index+1];
        return new Option((index+1)+" · "+a.name+" / "+b.name,String(index));
      }));
      this.index=Math.min(this.index,Math.max(0,world.connections.length-1));
      const api=root.EcologyModuleRules;
      if(api&&root.EcologyWorld) {
        const library=api.compileCatalog(root.EcologyWorld.catalog,root.EcologyWorld.profiles);
        this.allowedPairs=library.links.reduce((total,row)=>total+row.filter(Boolean).length,0);
        this.totalPairs=library.candidates.length**2;
      }
    }
    pair() {
      const world=this.world(),link=world?.connections[this.index];
      if(!link)return null;
      return {link,a:world.rooms.find(room=>room.id===link.from)||world.rooms[this.index],
        b:world.rooms.find(room=>room.id===link.to)||world.rooms[this.index+1]};
    }
    setEnabled(enabled,index) {
      this.index=Math.max(0,Math.min(Number(index)||0,(this.world()?.connections.length||1)-1));
      this.enabled=!!enabled&&!!this.pair();this.select.value=String(this.index);
      const renderer=this.renderer(),pair=this.pair();
      renderer.inspectionTarget=this.enabled?{
        x:(pair.a.x+pair.a.width+pair.b.x)/2,
        y:Number.isFinite(pair.link.floor)?pair.link.floor-64:(pair.a.y+pair.b.y)/2+200
      }:null;
      if(!this.enabled)this.context.clearRect(0,0,this.canvas.width,this.canvas.height);
    }
    label() {
      const pair=this.pair();return pair?pair.a.name+" / "+pair.b.name:"没有可检查的连接";
    }
    path(points,color,dashed) {
      if(!points.length)return;
      const c=this.context;c.beginPath();c.moveTo(...points[0]);
      points.slice(1).forEach(point=>c.lineTo(...point));c.lineWidth=1.4;
      c.strokeStyle=color;c.setLineDash(dashed?[5,4]:[]);c.stroke();c.setLineDash([]);
    }
    color(token,fallback) {
      this.swatch.style.color="var("+token+","+fallback+")";
      return getComputedStyle(this.swatch).color;
    }
    shapePoints(room,key) {
      const values=room.shape?.[key];
      if(!values||!values.length)return [];
      return Array.from(values,(value,index)=>Array.isArray(value)
        ?[room.x+value[0],room.y+value[1]]:[room.x+index,room.y+value]);
    }
    inspect() {
      const pair=this.pair();
      return {enabled:this.enabled,index:this.index,label:this.label(),
        connectorId:pair?.link.connectorId||pair?.link.contract?.id||null,
        hasAuthoredShapes:!!pair?.a.shape&&!!pair?.b.shape,
        hasPortSignatures:!!pair?.a.ports&&!!pair?.b.ports,
        allowedSourcePairs:this.allowedPairs,totalSourcePairs:this.totalPairs,
        scope:"Current seven placed seams; allowed source pairs are rule compatibility, not art acceptance.",
        declaredMotionBounds:(pair?[pair.a,pair.b]:[]).flatMap(room=>room.plants||[])
          .filter(plant=>!!plant.bounds).length};
    }
    draw(state) {
      const renderer=this.renderer(),c=this.context;
      if(this.canvas.width!==renderer.canvas.width||this.canvas.height!==renderer.canvas.height) {
        this.canvas.width=renderer.canvas.width;this.canvas.height=renderer.canvas.height;
      }
      c.setTransform(1,0,0,1,0,0);c.clearRect(0,0,this.canvas.width,this.canvas.height);
      if(!this.enabled)return;
      const pair=this.pair();if(!pair)return;
      const scale=renderer.canvas.width/renderer.width;
      c.setTransform(scale,0,0,scale,0,0);c.translate(-renderer.camera.x,-renderer.camera.y);
      const green=this.color("--green","#81d49b"),blue=this.color("--blue","#80b9ec");
      const yellow=this.color("--yellow","#dec578");
      const left=pair.a.x+pair.a.width-140,right=pair.b.x+140;
      state.surfaces.filter(surface=>surface.enabled!==false&&surface.points.some(p=>p[0]>=left&&p[0]<=right))
        .forEach(surface=>this.path(surface.points,green,false));
      [pair.a,pair.b].forEach(room=>{
        this.path(this.shapePoints(room,"roof"),blue,false);
        const floor=this.shapePoints(room,"floor");
        for(const side of ["left","right"]) {
          const port=room.ports?.[side],band=port?.signature?.band;
          const edge=side==="left"?room.x:room.x+room.width;
          if(edge<left-2||edge>right+2)continue;
          const floorY=room.y+(port?.origin?.y??(side==="left"?floor[0]?.[1]-room.y:floor.at(-1)?.[1]-room.y));
          if(!Number.isFinite(floorY))continue;
          const width=band?.width||64,x=side==="left"?edge:edge-width;
          const roofY=floorY+(band?.roof?.[0]??-128);
          c.globalAlpha=.12;c.fillStyle=yellow;c.fillRect(x,roofY,width,floorY-roofY);
          c.globalAlpha=1;c.strokeStyle=yellow;c.lineWidth=1.3;c.setLineDash([4,3]);
          c.strokeRect(x,roofY,width,floorY-roofY);c.setLineDash([]);
        }
        (room.plants||[]).forEach(plant=>{
          const x=room.x+plant.x,y=room.y+plant.y;
          if(x<left||x>right)return;
          const bounds=plant.bounds,scale=plant.scale||1;
          const h=(plant.kind==="vine"?67:plant.kind==="mushroom"?36:31)*scale;
          const box=bounds?{x:room.x+bounds.x,y:room.y+bounds.y,w:bounds.w,h:bounds.h}
            :{x:x-26*scale,y:plant.kind==="vine"?y:y-h,w:52*scale,h};
          c.strokeStyle=blue;c.lineWidth=.8;c.setLineDash([2,4]);
          c.strokeRect(box.x,box.y,box.w,box.h);c.setLineDash([]);
        });
      });
    }
  }
  root.EcologyAssemblyInspector=EcologyAssemblyInspector;
})(globalThis);
