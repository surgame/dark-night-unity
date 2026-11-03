"use strict";
const fs=require("node:fs"),path=require("node:path"),vm=require("node:vm"),crypto=require("node:crypto");
const ROOT=__dirname;
const SOURCE_FILES=["contours.js","authored-profiles.js","module-rules.js","terrain-shapes.js","world-data.js"];
const sha=file=>crypto.createHash("sha256").update(fs.readFileSync(file)).digest("hex");
const clone=value=>JSON.parse(JSON.stringify(value));
function runtime(buildId) {
  const context={TextEncoder,ECOLOGY_BUILD_ID:buildId||"unbuilt-static-geometry-audit"};
  context.globalThis=context;vm.createContext(context);
  for(const file of SOURCE_FILES)vm.runInContext(fs.readFileSync(path.join(ROOT,file),"utf8"),context,{filename:file});
  return context;
}
function currentManifest(filename) {
  const manifest=JSON.parse(fs.readFileSync(filename,"utf8"));
  if(!manifest.buildId)throw new Error("Build manifest has no current build identity.");
  const changed=manifest.sourceFiles.filter(item=>!fs.existsSync(item.path)||sha(item.path)!==item.sha256);
  if(changed.length)throw new Error("Rebuild before auditing/exporting; changed sources: "+changed.map(item=>path.basename(item.path)).join(", "));
  if(!fs.existsSync(manifest.output)||sha(manifest.output)!==manifest.outputSha256)
    throw new Error("The built fragment differs from its manifest.");
  for(const item of manifest.sources)if(sha(item.path)!==item.sourceSha256)
    throw new Error("Source artwork changed after build: "+item.name);
  return manifest;
}
function inspectWorld(context,world,options) {
  options=options||{};
  const issues=[],rules=context.EcologyModuleRules,sample=context.EcologyTerrainShapes.sample;
  const check=(condition,code,details)=>{if(!condition)issues.push({code,...details});};
  const near=(a,b)=>Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<.00001;
  const onLine=(points,x)=>Array.isArray(points)&&points.length>1&&x>=points[0][0]-.00001&&x<=points.at(-1)[0]+.00001
    ?sample(points,x):null;
  for(const room of world.rooms) {
    const prefix={sourceId:room.sourceId,roomId:room.id};
    const ports=rules.verifyAuthoredPorts(room.shape,room.ports,{requireRoad:options.requireRoad!==false});
    if(!ports.passed)issues.push({code:"PORT_SHAPE",...prefix,details:ports.issues.slice(0,8),count:ports.issues.length});
    const expectedFloor=room.shape.floor,expectedRoof=room.shape.roof;
    check(rules.canonical(room.profile.floor)===rules.canonical(expectedFloor),"PROFILE_FLOOR_SOURCE",prefix);
    check(rules.canonical(room.profile.roof)===rules.canonical(expectedRoof),"PROFILE_ROOF_SOURCE",prefix);
    const ceiling=world.ceilings.find(value=>value.id===room.id+"-ceiling");
    check(!!ceiling,"MISSING_CEILING",prefix);
    const collision=world.surfaces.filter(value=>value.roomId===room.id&&
      !["ledge","bridge","crumble"].includes(value.kind)&&value.enabled!==false);
    const xs=new Set(expectedFloor.map(p=>p[0]));for(let x=0;x<=room.width;x+=4)xs.add(x);xs.add(room.width);
    for(const x of xs) {
      const wx=room.x+x,expected=room.y+sample(expectedFloor,x);
      check(collision.some(value=>near(onLine(value.points,wx),expected)),"FLOOR_COLLISION_SOURCE",{...prefix,x,expected});
      if(ceiling)check(near(onLine(ceiling.points,wx),room.y+sample(expectedRoof,x)),"ROOF_COLLISION_SOURCE",{...prefix,x});
    }
    if(room.shape.road) {
      const road=room.shape.road;
      for(const x of xs) {
        const centre=sample(expectedFloor,x);
        check(near(sample(road.centre,x),centre),"ROAD_CENTRE_SOURCE",{...prefix,x});
        check(near(sample(road.far,x),centre-8),"ROAD_FAR_OFFSET",{...prefix,x});
        check(near(sample(road.near,x),centre+12),"ROAD_NEAR_OFFSET",{...prefix,x});
      }
      check(road.lipDepth===9,"ROAD_LIP_DEPTH",prefix);
    }
    const cut=room.shape.cut;
    if(cut) {
      check(cut.a>=64&&cut.b<=room.width-64,"CUT_INTERFACE_OVERLAP",prefix);
      if(cut.authored) {
        const art=room.authoredArt,profile=context.ECOLOGY_AUTHORED_PROFILES?.bridge;
        check(!!art&&!!profile,"AUTHORED_CUT_SOURCE",prefix);
        if(art&&profile) {
          check(art.key==="bridgeDesign"&&art.sourceSha256===profile.sourceSha256&&
            art.profileVersion===profile.version,"AUTHORED_ART_IDENTITY",prefix);
          check(near(art.x,profile.insetX)&&near(art.y,0)&&near(art.drawWidth,profile.displayWidth)&&
            near(art.drawHeight,profile.displayHeight)&&near(room.width,profile.moduleWidth)&&
            art.interfaceOutsideSourceArt===true,"AUTHORED_ART_CORE",prefix);
          check(art.nativeWidth===profile.nativeSize[0]&&art.nativeHeight===profile.nativeSize[1]&&
            near(art.drawWidth/art.nativeWidth,art.drawHeight/art.nativeHeight),"AUTHORED_ART_ASPECT",prefix);
          check(art.x>=64&&art.x+art.drawWidth<=room.width-64&&art.y+art.drawHeight<=room.height,
            "AUTHORED_ART_COLLARS",prefix);
          check(rules.canonical(expectedFloor)===rules.canonical(profile.floor)&&
            rules.canonical(expectedRoof)===rules.canonical(profile.roof)&&
            rules.canonical(cut.floor)===rules.canonical(profile.floor),"AUTHORED_PROFILE_SOURCE",prefix);
          check(near(cut.a,profile.bridgeAnchors[0])&&near(cut.b,profile.bridgeAnchors[1]),
            "AUTHORED_BRIDGE_ANCHORS",prefix);
        }
        check(Array.isArray(cut.points)&&cut.points.length>=2&&
          near(cut.points[0][0],cut.a)&&near(cut.points.at(-1)[0],cut.b)&&
          cut.points.every(p=>p[0]>=64&&p[0]<=room.width-64&&near(p[1],sample(expectedFloor,p[0]))),
          "AUTHORED_CUT_FLOOR",prefix);
        const bridge=world.surfaces.find(value=>value.id===room.id+"-span");
        check(!!bridge&&bridge.kind==="bridge"&&bridge.enabled===false&&
          near(bridge.points[0][0],room.x+cut.a)&&near(bridge.points.at(-1)[0],room.x+cut.b)&&
          bridge.points.every(p=>near(p[1],room.y+sample(expectedFloor,p[0]-room.x))),
          "AUTHORED_BRIDGE_SPAN",prefix);
      } else {
        check(Array.isArray(cut.mask)&&cut.mask.every(p=>p[0]>=64&&p[0]<=room.width-64),
          "CUT_MASK_INTERFACE_OVERLAP",prefix);
      }
    }
    if(room.lowroof) {
      check(room.lowroof.a>=64&&room.lowroof.b<=room.width-64,"LOWROOF_INTERFACE_OVERLAP",prefix);
      check(rules.canonical(room.lowroof.roof)===rules.canonical(expectedRoof),"LOWROOF_SHAPE_SOURCE",prefix);
      for(const point of room.lowroof.underside||[])
        check(near(sample(expectedRoof,point[0]),point[1]),"LOWROOF_UNDERSIDE_SOURCE",{...prefix,x:point[0]});
      for(const wall of world.walls.filter(value=>value.x>=room.x&&value.x<room.x+room.width))
        for(let x=wall.x;x<=wall.x+wall.w;x+=4)
          check(wall.y+wall.h<=room.y+sample(expectedRoof,x-room.x)+.00001,"LOWROOF_GHOST_WALL",{...prefix,x:x-room.x});
    }
    for(const ledge of room.shape.ledges||[]) {
      const physical=world.surfaces.find(value=>value.id===ledge.id);
      check(!!physical&&physical.points.every((p,index)=>near(p[0],room.x+ledge.points[index][0])&&
        near(p[1],room.y+ledge.points[index][1])),"LEDGE_COLLISION_SOURCE",{...prefix,ledgeId:ledge.id});
      check(ledge.bodyPolygon.length>=4&&ledge.backSocket.length>=4,"LEDGE_ATTACHMENT_SHAPE",{...prefix,ledgeId:ledge.id});
    }
    for(const water of room.water||[]) {
      const details={...prefix,waterId:water.id};
      check(!!water.source?.kind&&!!water.sink?.kind,"WATER_TERMINALS",details);
      if(water.type==="stream") {
        check(water.dir===1||water.dir===-1,"WATER_DIRECTION",details);
        for(let i=1;i<water.points.length;i++) {
          const a=water.points[i-1],b=water.points[i];
          check((b[1]-a[1])*water.dir>=-.025001,"WATER_REVERSE_SEGMENT",{...details,segment:i});
          check(b[0]>a[0],"WATER_POINT_ORDER",{...details,segment:i});
        }
        const first=water.dir===1?water.points[0]:water.points.at(-1);
        const last=water.dir===1?water.points.at(-1):water.points[0];
        check(near(water.source.x,first[0])&&near(water.source.y,first[1]),"WATER_SOURCE_PATH",details);
        check(near(water.sink.x,last[0])&&near(water.sink.y,last[1]),"WATER_SINK_PATH",details);
        check(water.points.every(p=>p[0]>=64&&p[0]<=room.width-64),"WATER_INTERFACE_OVERLAP",details);
      } else if(water.type==="fall") {
        check(water.h>0&&water.source.y<water.sink.y,"FALL_DIRECTION",details);
        check(near(water.y,water.source.y)&&near(water.y+water.h,water.sink.y),"FALL_TERMINALS",details);
        check(water.x>=64&&water.x+(water.w||water.width)<=room.width-64,"FALL_INTERFACE_OVERLAP",details);
      } else check(false,"UNKNOWN_WATER_TYPE",details);
    }
    for(const plant of room.plants||[]) {
      const bounds=plant.bounds,details={...prefix,x:plant.x,kind:plant.kind};
      check(!!bounds&&[bounds.x,bounds.y,bounds.w,bounds.h].every(Number.isFinite),"PLANT_BOUNDS",details);
      if(bounds)check(bounds.x>=64&&bounds.x+bounds.w<=room.width-64,"PLANT_INTERFACE_OVERLAP",details);
      const support=[...world.surfaces,...world.ceilings].find(value=>value.id===plant.supportId);
      check(!!support,"PLANT_SUPPORT",details);
      if(support) {
        const y=onLine(support.points,room.x+plant.x);
        check(y!==null&&Math.abs(room.y+plant.y-y)<=1.01,"PLANT_SUPPORT_ATTACHMENT",details);
      }
      check(["front","back"].includes(plant.layer)&&plant.baseCover===true,"PLANT_LAYER_ATTACHMENT",details);
    }
  }
  for(const [index,link] of world.connections.entries()) {
    const a=world.rooms[index],b=world.rooms[index+1],surface=world.surfaces.find(s=>s.id===link.id);
    const ceiling=world.ceilings.find(s=>s.id==="connector-roof-"+(index+1));
    check(rules.portCompatible(a.ports.right,link.contract.ports.left)&&
      rules.portCompatible(link.contract.ports.right,b.ports.left),"CONNECTOR_SIGNATURE",{index});
    check(near(link.x0,a.x+a.width)&&near(link.x1,b.x),"CONNECTOR_EXTENT",{index});
    for(const x of [link.x0,(link.x0+link.x1)/2,link.x1]) {
      check(near(onLine(surface?.points,x),onLine(link.shape.floor,x)),"CONNECTOR_FLOOR_SOURCE",{index,x});
      check(near(onLine(ceiling?.points,x),onLine(link.shape.roof,x)),"CONNECTOR_ROOF_SOURCE",{index,x});
    }
  }
  const boundaries=Array.isArray(world.boundaries)?world.boundaries:Object.values(world.boundaries||{});
  if(options.requireBoundaries)check(boundaries.length>0,"MISSING_BOUNDARY_CONTRACT",{});
  for(const boundary of boundaries) {
    check(!!boundary.contract,"BOUNDARY_CONTRACT",{id:boundary.id});
    if(boundary.kind==="terminal-cap") {
      check(!!boundary.shape&&boundary.contract.fromPort==="P128-v2"&&boundary.contract.toPort==="CLOSED",
        "TERMINAL_BOUNDARY_CONTRACT",{id:boundary.id});
      const ground=world.surfaces.find(s=>s.id==="terminal-floor"),ceiling=world.ceilings.find(s=>s.id==="terminal-roof");
      if(boundary.shape)for(const x of [boundary.x0,boundary.wallX]) {
        check(near(onLine(ground?.points,x),onLine(boundary.shape.floor,x)),"TERMINAL_FLOOR_SOURCE",{id:boundary.id,x});
        check(near(onLine(ceiling?.points,x),onLine(boundary.shape.roof,x)),"TERMINAL_ROOF_SOURCE",{id:boundary.id,x});
      }
    }
  }
  return {seed:world.seed,worldId:world.id,passed:!issues.length,issues,
    summary:{rooms:world.rooms.length,connections:world.connections.length,portColumnsPerSide:65,
      water:world.rooms.reduce((n,r)=>n+r.water.length,0),plants:world.rooms.reduce((n,r)=>n+r.plants.length,0),
      boundaries:boundaries.length,roadDepthLayers:world.rooms.filter(r=>r.shape.road).length}};
}
function collect(context,maxSeeds) {
  const records=new Map(),worlds=[];
  for(let seed=0;seed<maxSeeds&&records.size<context.EcologyWorld.catalog.length;seed++) {
    const world=context.EcologyWorld.create(seed);worlds.push(world);
    for(const room of world.rooms)if(!records.has(room.sourceId))records.set(room.sourceId,{seed,world,room});
  }
  const missing=context.EcologyWorld.catalog.filter(item=>!records.has(item.id)).map(item=>item.id);
  return {records,worlds,missing};
}
function main() {
  const args=process.argv.slice(2),value=name=>{const i=args.indexOf(name);return i<0?null:args[i+1];};
  const manifestFile=value("--manifest"),manifest=manifestFile?currentManifest(path.resolve(manifestFile)):null;
  const context=runtime(manifest?.buildId),collected=collect(context,128);
  const inputFile=path.join(ROOT,"authored-profile-input.json");
  const authoredInput=JSON.parse(fs.readFileSync(inputFile,"utf8"));
  const nativeFile=path.resolve(ROOT,authoredInput.source),authored=context.ECOLOGY_AUTHORED_PROFILES.bridge;
  const identityIssues=[];
  if(sha(inputFile)!==authored.inputSha256)identityIssues.push({code:"AUTHORED_INPUT_STALE"});
  if(sha(nativeFile)!==authored.sourceSha256)identityIssues.push({code:"AUTHORED_IMAGE_STALE"});
  const audits=collected.worlds.map(world=>inspectWorld(context,world,{
    requireRoad:!args.includes("--allow-pending-road"),requireBoundaries:!!manifest}));
  const report={date:"2026-10-31",buildId:manifest?.buildId||null,
    scope:"Static authoring geometry only; no actor traversal, Unity, rendered art or multiplayer acceptance.",
    source:[...SOURCE_FILES,"verify-assembly.cjs"].map(file=>({file,sha256:sha(path.join(ROOT,file))})),
    authoredInputs:[inputFile,path.join(ROOT,"trace-authored.py"),nativeFile]
      .map(file=>({file,sha256:sha(file)})),identityIssues,
    seeds:collected.worlds.length,sourcesCovered:collected.records.size,missingSources:collected.missing,
    checks:audits.map(a=>({seed:a.seed,passed:a.passed,...a.summary,issues:a.issues})),
    passed:!collected.missing.length&&!identityIssues.length&&audits.every(a=>a.passed)};
  const output=value("--out");
  if(output){fs.mkdirSync(path.dirname(path.resolve(output)),{recursive:true});fs.writeFileSync(output,JSON.stringify(report,null,2)+"\n",{flag:"wx"});}
  console.log(JSON.stringify({passed:report.passed,seeds:report.seeds,sourcesCovered:report.sourcesCovered,
    missingSources:report.missingSources,issueCount:identityIssues.length+audits.reduce((n,a)=>n+a.issues.length,0),
    firstIssues:[...identityIssues,...audits.flatMap(a=>a.issues.map(issue=>({seed:a.seed,...issue})))].slice(0,12),
    output:output||null},null,2));
  if(!report.passed)process.exitCode=1;
}
module.exports={runtime,currentManifest,inspectWorld,collect,sha,SOURCE_FILES,clone};
if(require.main===module)main();
