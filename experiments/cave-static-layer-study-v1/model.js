(function(root){
  "use strict";
  const shape=root.ShapeStudy,lib=root.CaveWB,api=root.StaticCave={},slotCount=20;
  const pieces=[
    {id:0,name:"裸岩",left:0,right:0,weight:1.2},
    {id:1,name:"苔起",left:0,right:1,weight:.8},
    {id:2,name:"苔带",left:1,right:1,weight:2},
    {id:3,name:"苔止",left:1,right:0,weight:.8},
    {id:4,name:"湿起",left:1,right:2,weight:.8},
    {id:5,name:"湿岸",left:2,right:2,weight:1.8},
    {id:6,name:"湿止",left:2,right:1,weight:.8},
    {id:7,name:"蘑菇",left:1,right:1,weight:1.6,object:"mushroom"},
    {id:8,name:"晶簇",left:0,right:0,weight:.7,object:"crystal"},
    {id:9,name:"草簇",left:1,right:1,weight:1,object:"grass"},
    {id:10,name:"芦苇",left:2,right:2,weight:.9,object:"reed"}
  ];
  const fullCatalog=[];
  for(let mask=0;mask<16;mask++)for(const piece of pieces)
    fullCatalog.push({id:fullCatalog.length,mask,eco:piece.id});
  function makeScene(form){
    const plan=shape.makePlan(form?902:271,"basin"),map=shape.dual(plan);
    const slots=Array.from({length:slotCount},(_,i)=>{
      const x=2+i*2,y=shape.groundAt(map,x),tx=Math.floor(x),ty=Math.min(27,Math.floor(y));
      const at=ty*48+tx,mask=shape.toMask(lib.catalog[map.tiles[at]].mask);
      const grade=Math.abs(shape.groundAt(map,x+.5)-shape.groundAt(map,x-.5));
      return{i,x,y,at,mask,grade};
    });
    return{form,plan,map,slots,geometry:Array.from(map.tiles).join(",")};
  }
  function ecoOptions(slot){
    return pieces.filter(p=>!p.object||p.object==="crystal"||slot.grade<.85).map(p=>p.id);
  }
  function setup(scene,mode,conflict){
    const whole=mode==="whole",width=whole?scene.map.tiles.length:scene.slots.length;
    const domains=Array.from({length:width},(_,i)=>whole?[shape.toMask(lib.catalog[scene.map.tiles[i]].mask)*11]:[]);
    const slotIndex=new Map(),priorities=Array(width).fill(10000);
    scene.slots.forEach((slot,i)=>{
      const node=whole?slot.at:i;slotIndex.set(node,i);priorities[node]=i;
      let allowed=ecoOptions(slot);
      if(i===0)allowed=allowed.filter(id=>pieces[id].left===0);
      if(i===slotCount-1)allowed=allowed.filter(id=>pieces[id].right===0);
      if(i===14)allowed=allowed.filter(id=>id===5||id===10);
      if(i===5)allowed=allowed.filter(id=>id===7);
      if(conflict&&i===10)allowed=[0];
      if(conflict&&i===11)allowed=[5];
      domains[node]=allowed.map(id=>whole?slot.mask*11+id:id);
    });
    const arcs=Array.from({length:width},()=>[]);
    if(whole)for(let y=0;y<28;y++)for(let x=0;x<48;x++){
      const at=y*48+x;
      if(x<47){arcs[at].push({to:at+1,type:"E"});arcs[at+1].push({to:at,type:"W"});}
      if(y<27){arcs[at].push({to:at+48,type:"S"});arcs[at+48].push({to:at,type:"N"});}
    }
    for(let i=0;i<slotCount-1;i++){
      const a=whole?scene.slots[i].at:i,b=whole?scene.slots[i+1].at:i+1;
      arcs[a].push({to:b,type:"right"});arcs[b].push({to:a,type:"left"});
    }
    return{domains,arcs,priorities,slotIndex,whole};
  }
  function compatible(a,b,type,whole){
    const ecoA=pieces[whole?fullCatalog[a].eco:a],ecoB=pieces[whole?fullCatalog[b].eco:b];
    if(type==="right")return ecoA.right===ecoB.left;
    if(type==="left")return ecoA.left===ecoB.right;
    const ca=shape.tile16(fullCatalog[a].mask).values,cb=shape.tile16(fullCatalog[b].mask).values;
    if(type==="E")return ca[1]===cb[0]&&ca[2]===cb[3];
    if(type==="W")return ca[0]===cb[1]&&ca[3]===cb[2];
    if(type==="S")return ca[3]===cb[0]&&ca[2]===cb[1];
    return ca[0]===cb[3]&&ca[1]===cb[2];
  }
  const ecoId=(id,whole)=>whole?fullCatalog[id].eco:id;
  function entropy(domain,whole){
    let sum=0,logs=0;
    for(const id of domain){const w=pieces[ecoId(id,whole)].weight;sum+=w;logs+=w*Math.log(w);}
    return Math.log(sum)-logs/sum;
  }
  function solve(scene,mode,seed,conflict=false){
    const stats={observations:0,propagations:0,removed:0,attempts:0};
    for(let attempt=0;attempt<4;attempt++){
      stats.attempts=attempt+1;
      const graph=setup(scene,mode,conflict),{domains,arcs,whole,priorities}=graph;
      const rng=lib.random(seed^Math.imul(attempt+1,0x9e3779b1));
      function propagate(queue){
        let cursor=0;
        while(cursor<queue.length){
          const at=queue[cursor++],from=domains[at];if(!from.length)return false;
          for(const arc of arcs[at]){
            stats.propagations++;
            const next=domains[arc.to].filter(b=>from.some(a=>compatible(a,b,arc.type,whole)));
            if(!next.length)return false;
            if(next.length!==domains[arc.to].length){
              stats.removed+=domains[arc.to].length-next.length;domains[arc.to]=next;queue.push(arc.to);
            }
          }
        }
        return true;
      }
      if(!propagate(domains.map((_,i)=>i)))continue;
      let failed=false;
      while(true){
        let best=-1,minimum=Infinity;
        domains.forEach((d,i)=>{
          if(d.length<2)return;
          const h=entropy(d,whole)+priorities[i]*1e-8;
          if(h<minimum){minimum=h;best=i;}
        });
        if(best<0)break;
        const options=domains[best];let roll=rng()*options.reduce((n,id)=>n+pieces[ecoId(id,whole)].weight,0),chosen=options.at(-1);
        for(const id of options){roll-=pieces[ecoId(id,whole)].weight;if(roll<=0){chosen=id;break;}}
        domains[best]=[chosen];stats.observations++;
        if(!propagate([best])){failed=true;break;}
      }
      if(failed)continue;
      const eco=scene.slots.map((slot,i)=>ecoId(domains[whole?slot.at:i][0],whole));
      const tiles=whole?Uint16Array.from(domains.map(d=>shape.tile16(fullCatalog[d[0]].mask).id)):scene.map.tiles.slice();
      if(tiles.some((v,i)=>v!==scene.map.tiles[i]))throw new Error("Author geometry changed.");
      for(let i=1;i<eco.length;i++)if(pieces[eco[i-1]].right!==pieces[eco[i]].left)throw new Error("Ecology port mismatch.");
      return{mode,eco,tiles,stats,geometry:Array.from(tiles).join(","),signature:eco.join(",")};
    }
    throw new Error("干岩 D 与湿岸 W 不能直接相接；四次有界尝试均无解。");
  }
  function generate(form,seed,conflict=false){
    const scene=makeScene(form),whole=solve(scene,"whole",seed,conflict),hybrid=solve(scene,"hybrid",seed,conflict);
    if(whole.signature!==hybrid.signature)throw new Error("Equivalent encodings diverged.");
    return{scene,whole,hybrid,seed,form,identical:true};
  }
  Object.assign(api,{pieces,fullCatalog,makeScene,setup,solve,generate,ecoOptions});
})(globalThis);
