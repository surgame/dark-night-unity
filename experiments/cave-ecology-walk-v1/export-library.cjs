"use strict";
const fs=require("node:fs"),path=require("node:path");
const audit=require("./verify-assembly.cjs");
const ROOT=__dirname;
function localModule(witness,context) {
  const {world,room,seed}=witness,clone=audit.clone;
  const localId=id=>id?.startsWith(room.id+"-")?id.slice(room.id.length+1):id;
  const surfaces=world.surfaces.filter(s=>s.roomId===room.id).map(s=>({
    id:localId(s.id),kind:s.kind||"terrain",initialEnabled:s.enabled!==false,
    points:s.points.map(p=>[p[0]-room.x,p[1]-room.y])
  }));
  const slots=world.objects.filter(o=>o.roomId===room.id).map((o,index)=>{
    const value=clone(o);value.id=room.sourceId+":"+o.type+":"+index;delete value.roomId;
    value.x-=room.x;value.y-=room.y;
    if(value.surfaceId)value.surfaceId=localId(value.surfaceId);
    if(value.surfaceIds)value.surfaceIds=value.surfaceIds.map(localId);
    if(value.target){value.target.x-=room.x;value.target.y-=room.y;}
    value.previewOnly=o.type==="checkpoint"||o.type==="sign";
    return value;
  });
  const shape=clone(room.shape);
  shape.ledges.forEach(ledge=>{ledge.id=localId(ledge.id);});
  const plants=clone(room.plants);plants.forEach(plant=>{plant.supportId=localId(plant.supportId);});
  return {
    sourceId:room.sourceId,name:room.name,slot:room.slot,ecology:room.ecology,
    width:room.width,height:room.height,orientation:"normal",profileIndex:room.panel,
    nativePanel:room.authoredArt?null:room.panel,sourceAsset:room.authoredArt?.key||"panel"+room.panel,
    artFamily:room.artFamily||"native-panel-"+room.panel,authoredArt:clone(room.authoredArt||null),
    shape,ports:context.EcologyModuleRules.bindAuthoredPorts(shape,room.ports),
    surfaces,ceilings:[{id:"ceiling",points:clone(shape.roof)}],
    water:clone(room.water),vegetationAnchors:plants,mechanismSlots:slots,
    layerContract:{collision:"road-centre-and-explicit-ledge-top",top:"road.far-to-road.near",
      foregroundLip:"road.near-plus-lipDepth",nearRubble:"visual-occlusion-only",
      background:"complete-frozen-reference",water:"local-source-sink-no-cross-module-flow"},
    witness:{seed,roomIndex:room.index,worldId:world.id,previewState:false}
  };
}
function localBoundaries(world) {
  const definitions={},offsets={};
  for(const [side,boundary] of Object.entries(world.boundaries)) {
    const room=side==="left"?world.rooms[0]:world.rooms.at(-1);
    const offset={x:side==="left"?room.x:boundary.x0,
      y:side==="left"?room.y+room.shape.floor[0][1]:boundary.floor};
    const value=audit.clone(boundary),points=items=>items.map(p=>[p[0]-offset.x,p[1]-offset.y]);
    for(const key of ["x","x0","x1","wallX"])if(Number.isFinite(value[key]))value[key]-=offset.x;
    for(const key of ["floor","roof"])if(Number.isFinite(value[key]))value[key]-=offset.y;
    if(value.shape)for(const key of ["floor","roof"])if(value.shape[key])value.shape[key]=points(value.shape[key]);
    if(value.road)for(const key of ["centre","far","near"])if(value.road[key])value.road[key]=points(value.road[key]);
    const wall=world.walls.find(item=>item.id==="terminal-wall");
    if(side==="right"&&wall)value.collisionWall={x:wall.x-offset.x,y:wall.y-offset.y,w:wall.w,h:wall.h};
    value.socketOrigin={x:0,y:0};definitions[side]=value;offsets[side]=offset;
  }
  return {format:"ecology-boundary-candidates-v2",coordinates:"socket-local-pixel-y-down",
    placement:"Translate each definition by its target socket origin; witness offsets are provenance only.",
    definitions,witness:{seed:world.seed,worldId:world.id,sourceWorldOffsets:offsets}};
}
function main() {
  const args=process.argv.slice(2),value=name=>{const i=args.indexOf(name);return i<0?null:args[i+1];};
  const manifestPath=path.resolve(value("--manifest")||path.join(ROOT,"manifest.json"));
  const manifest=audit.currentManifest(manifestPath),context=audit.runtime(manifest.buildId);
  const collected=audit.collect(context,128);
  if(collected.missing.length)throw new Error("Finite authoring search did not cover sources: "+collected.missing.join(", "));
  const checks=collected.worlds.map(world=>audit.inspectWorld(context,world,{requireBoundaries:true}));
  const failed=checks.filter(check=>!check.passed);
  if(failed.length)throw new Error("Static assembly errors block export: "+JSON.stringify(failed[0].issues.slice(0,5)));
  const base=path.resolve(value("--out-dir")||path.join(ROOT,"production-candidate-v2"));
  const stamp=new Intl.DateTimeFormat("sv-SE",{timeZone:"Asia/Shanghai",year:"numeric",month:"2-digit",
    day:"2-digit",hour:"2-digit",minute:"2-digit",second:"2-digit",hour12:false})
    .format(new Date()).replace(/[- :]/g,"")+"-cn";
  const output=path.join(base,stamp+"-"+manifest.buildId.slice(0,12));
  if(fs.existsSync(output))throw new Error("Output already exists; preserved without overwrite: "+output);
  fs.mkdirSync(path.join(output,"modules"),{recursive:true});fs.mkdirSync(path.join(output,"assets"));
  const identities=[],write=(name,data)=>{
    const file=path.join(output,name);fs.writeFileSync(file,JSON.stringify(data,null,2)+"\n",{flag:"wx"});
    identities.push({file:name,bytes:fs.statSync(file).size,sha256:audit.sha(file)});
  };
  const sources=manifest.sources.map(entry=>({name:entry.name,path:entry.path,sha256:entry.sourceSha256}));
  sources.push({name:"vegetation-sheet",path:manifest.vegetation.source,sha256:manifest.vegetation.sourceSha256});
  for(const source of sources) {
    if(audit.sha(source.path)!==source.sha256)throw new Error("Native artwork changed: "+source.path);
    const filename="assets/"+source.name+path.extname(source.path).toLowerCase();
    fs.copyFileSync(source.path,path.join(output,filename),fs.constants.COPYFILE_EXCL);
    source.exportedPath=filename;source.bytes=fs.statSync(path.join(output,filename)).size;
    identities.push({file:filename,bytes:source.bytes,sha256:source.sha256});
  }
  const library=context.EcologyModuleRules.compileCatalog(context.EcologyWorld.catalog,
    context.EcologyWorld.profiles,{assetIdentity:manifest.buildId});
  const modules=context.EcologyWorld.catalog.map(source=>{
    const data=localModule(collected.records.get(source.id),context);
    const file="modules/"+source.id+".json";write(file,data);
    return {sourceId:source.id,file,slot:source.slot,identity:context.EcologyModuleRules.sha256(
      context.EcologyModuleRules.canonical(data))};
  });
  write("compatibility.json",{
    direction:"source-right-to-target-left",sources:library.candidates.map(c=>c.sourceId),
    allowedConnectorIds:library.links,connectors:library.connectors,
    limits:["Local adjacency only; macro route and gameplay prerequisites remain separate.",
      "No arbitrary rotations or unsupported water sockets; no runtime WFC after terrain changes."]
  });
  write("materials.json",{
    foreground:{source:"assets/rockMaterial.png",identity:"handdrawn-rock-v2",
      addressing:"world-mirror-768-v2",worldPeriod:768},
    backwall:{source:"assets/back.png",identity:"backwall-clean-v1",
      addressing:"world-mirror-623-v2",worldPeriod:623},
    authoredDesigns:{bridgeDesign:{source:"assets/bridgeDesign.png",identity:"bridge-authored-v2",
      placement:"Per-module authoredArt; preserve the whole source at uniform scale."}},
    interfaces:{width:64,clearance:128,signatureVersion:context.EcologyModuleRules.VERSION},
    sourcePolicy:"Native artwork copied without modifying its bytes; normalized shapes remain separate authoring data."
  });
  write("boundaries.json",localBoundaries(collected.worlds[0]));
  write("library.json",{
    format:"ecology-module-candidate-library-v2",status:"Reviewable production candidate; no Unity registration or approval.",
    buildId:manifest.buildId,libraryIdentity:library.identity,
    ruleVersion:context.EcologyModuleRules.VERSION,shapeVersion:context.EcologyTerrainShapes.VERSION,
    sources,modules,sourceCount:modules.length,authoringSearch:{maxSeeds:128,seedsUsed:collected.worlds.length},
    scope:{actorsTested:false,renderedSeamsAccepted:false,spineProjectCreated:false,unityImported:false},
    rebuild:"Build the current fragment, then run node export-library.cjs --manifest <current manifest> --out-dir <new output directory>."
  });
  write("static-verification.json",{buildId:manifest.buildId,
    checks:checks.map(c=>({seed:c.seed,passed:c.passed,summary:c.summary})),missingSources:collected.missing,
    scope:"Shapes, signatures, attachment, water slope and initial event relations; not traversal or visual acceptance."});
  write("manifest.json",{date:"2026-10-31",buildId:manifest.buildId,inputManifest:manifestPath,
    inputManifestSha256:audit.sha(manifestPath),files:identities,
    exporterSources:["export-library.cjs","verify-assembly.cjs"].map(file=>({file,sha256:audit.sha(path.join(ROOT,file))}))});
  console.log(JSON.stringify({output,buildId:manifest.buildId,sources:modules.length,seeds:collected.worlds.length},null,2));
}
if(require.main===module)main();
module.exports={localModule,localBoundaries};
