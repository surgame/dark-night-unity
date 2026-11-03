const fs=require("fs"),path=require("path"),vm=require("vm"),crypto=require("crypto"),root=__dirname;
const folder=path.join(root,"records",Date.now()+"-model-review");fs.mkdirSync(folder,{recursive:true});
const scripts=["../dualgrid-cave-exploration-v1/dual-grid.js","plan.js","solver.js","field.js","mesh.js"];
const sha=b=>crypto.createHash("sha256").update(b).digest("hex"),context=vm.createContext({performance});
for(const file of scripts)vm.runInContext(fs.readFileSync(path.join(root,file),"utf8"),context,{filename:file});
const P=context.CavePerf,report={date:"2026-11-01",cases:[],pinnedMeshes:0,
  scope:"Finite graph, port, fixed-room geometry and payload checks; no gameplay acceptance.",
  sources:scripts.map(file=>({file,sha256:sha(fs.readFileSync(path.join(root,file)))}))};
const check=(v,m)=>{if(!v) throw Error(m);};
const equal=(a,b)=>Buffer.from(a.buffer,a.byteOffset,a.byteLength).equals(Buffer.from(b.buffer,b.byteOffset,b.byteLength));
try {
  check(P.catalog.length===243,"Incomplete 4-port catalog");
  for(const profile of ["standard","mixed"]) for(const size of [128,512]) for(const seed of [271,372]) {
    const plan=P.makePlan(size,seed,profile);
    check(plan.nodes.length===size&&plan.pins>0&&plan.loops>0,"Missing 2D branch/loop/anchors");
    const seen=new Set([0]),queue=[0];
    for(let i=0;i<queue.length;i++)for(const n of plan.nodes[queue[i]].neighbors)
      if(n>=0&&!seen.has(n)){seen.add(n);queue.push(n);}
    check(seen.size===size,"Input graph disconnected");
    for(const method of P.methods) {
      const g=P.generate(plan,method),p=P.bake(P.prepare(g)),r=P.resources(p);
      for(const node of plan.nodes) {
        const def=g.choices[node.id];
        if(node.pin) {
          check(def.pin&&def.size===32&&def.key===node.definition.key&&
            def.x===node.x*32&&def.y===node.y*32,"Changed a fixed room");
          if(g.field) {
            const actual=P.meshRegion(g.field.data,g.field.width,node.x*16,node.y*16,16,16,2);
            const expected=P.moduleMesh(node.definition);
            check(equal(actual.triangles,expected.triangles)&&equal(actual.lines,expected.lines),
              "Continuous smoothing changed authored room boundary");
          }
          report.pinnedMeshes++;
        }
      }
      for(const edge of plan.edges) {
        const a=g.choices[edge.a],b=g.choices[edge.b],d=edge.direction,o=(d+2)%4;
        if(method!=="bridges") check(P.port(a.code,d)===P.port(b.code,o),"Adjacent ports differ");
        else {
          const length=32-(a.size+b.size)/2;
          const connector=g.connectors.find(c=>c.x===plan.nodes[edge.a].x*32+16+(d===1?a.size/2:0)&&
            c.y===plan.nodes[edge.a].y*32+16+(d===2?a.size/2:0)&&c.vertical===(d===2));
          check(length===0||connector&&connector.length===length&&
            connector.from===P.port(a.code,d)&&connector.to===P.port(b.code,o),"Adapter end differs");
        }
        if(g.field) {
          const aNode=plan.nodes[edge.a],x=aNode.x*16+8+(d===1?8:0),y=aNode.y*16+8+(d===2?8:0);
          check(g.field.data[y*g.field.width+x]>0,"Continuous smoothing sealed a source port");
        }
      }
      check(r.freeUsed<=r.freeLibrary&&r.presetUsed<=32,"Library accounting invalid");
      check(r.chunkVertices*8===r.chunkBytes&&r.sourceVertices>0,"Buffer payload invalid");
      if(method==="wfc") {
        const again=P.solve(plan);
        check(JSON.stringify(again.ids)===JSON.stringify(g.solution.ids),"WFC not reproducible");
        check(g.solution.stats.observations>0,"No real choices");
        if(profile==="mixed")check(g.solution.stats.removed>0,"Mixed ports did not propagate");
      }
      report.cases.push({profile,size,seed,method,pins:plan.pins,loops:plan.loops,
        resources:r,wfc:g.solution?.stats||null});
    }
  }
  const conflict=P.makePlan(128,271,"mixed"),edge=conflict.edges[0];
  for(const [id,code] of [[edge.a,0],[edge.b,conflict.nodes[edge.b].mask]]) {
    const node=conflict.nodes[id];node.pin=true;node.definition={...P.definition(node.mask,0,"mixed"),code};
  }
  let rejected=false;try{P.solve(conflict);}catch(e){rejected=/冲突/.test(e.message);}
  check(rejected,"Impossible preassigned rooms were silently accepted");report.conflictRejected=true;
  report.ok=true;
}catch(e){report.ok=false;report.failure=e.stack;process.exitCode=1;}
fs.writeFileSync(path.join(folder,"report.json"),JSON.stringify(report,null,2)+"\n");
console.log(JSON.stringify({ok:report.ok,folder,cases:report.cases.length,
  pinnedMeshes:report.pinnedMeshes,failure:report.failure}));
