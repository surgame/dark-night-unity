(function (root) {
  "use strict";
  const VERSION = "ecology-module-rules-v2";
  const BAND = 64, CLEARANCE = 128;
  const ECOLOGIES = ["dry", "moss", "creek", "fungi", "falls"];
  const clone = value => JSON.parse(JSON.stringify(value));

  class EcologyRuleError extends Error {
    constructor(code, message, details) {
      super(message);
      this.name = "EcologyRuleError";
      this.code = code;
      this.details = details || {};
    }
  }
  function fail(code, message, details) { throw new EcologyRuleError(code, message, details); }
  function canonical(value) {
    if (Array.isArray(value)) return "[" + value.map(canonical).join(",") + "]";
    if (value && typeof value === "object")
      return "{" + Object.keys(value).sort().map(key => JSON.stringify(key) + ":" + canonical(value[key])).join(",") + "}";
    return JSON.stringify(value);
  }
  function sha256(text) {
    const bytes = new TextEncoder().encode(text), length = Math.ceil((bytes.length + 9) / 64) * 64;
    const buffer = new Uint8Array(length); buffer.set(bytes); buffer[bytes.length] = 128;
    const view = new DataView(buffer.buffer), bits = bytes.length * 8;
    view.setUint32(length - 8, Math.floor(bits / 4294967296), false);
    view.setUint32(length - 4, bits >>> 0, false);
    const hashes = [0x6a09e667,0xbb67ae85,0x3c6ef372,0xa54ff53a,0x510e527f,0x9b05688c,0x1f83d9ab,0x5be0cd19];
    const constants = [
      0x428a2f98,0x71374491,0xb5c0fbcf,0xe9b5dba5,0x3956c25b,0x59f111f1,0x923f82a4,0xab1c5ed5,
      0xd807aa98,0x12835b01,0x243185be,0x550c7dc3,0x72be5d74,0x80deb1fe,0x9bdc06a7,0xc19bf174,
      0xe49b69c1,0xefbe4786,0x0fc19dc6,0x240ca1cc,0x2de92c6f,0x4a7484aa,0x5cb0a9dc,0x76f988da,
      0x983e5152,0xa831c66d,0xb00327c8,0xbf597fc7,0xc6e00bf3,0xd5a79147,0x06ca6351,0x14292967,
      0x27b70a85,0x2e1b2138,0x4d2c6dfc,0x53380d13,0x650a7354,0x766a0abb,0x81c2c92e,0x92722c85,
      0xa2bfe8a1,0xa81a664b,0xc24b8b70,0xc76c51a3,0xd192e819,0xd6990624,0xf40e3585,0x106aa070,
      0x19a4c116,0x1e376c08,0x2748774c,0x34b0bcb5,0x391c0cb3,0x4ed8aa4a,0x5b9cca4f,0x682e6ff3,
      0x748f82ee,0x78a5636f,0x84c87814,0x8cc70208,0x90befffa,0xa4506ceb,0xbef9a3f7,0xc67178f2
    ];
    const rotate = (value, count) => (value >>> count) | (value << (32 - count));
    const words = new Int32Array(64);
    for (let offset = 0; offset < length; offset += 64) {
      for (let i = 0; i < 16; i++) words[i] = view.getInt32(offset + i * 4, false);
      for (let i = 16; i < 64; i++) {
        const a = words[i - 15], b = words[i - 2];
        words[i] = (words[i - 16] + (rotate(a,7) ^ rotate(a,18) ^ a >>> 3) +
          words[i - 7] + (rotate(b,17) ^ rotate(b,19) ^ b >>> 10)) | 0;
      }
      let [a,b,c,d,e,f,g,h] = hashes;
      for (let i = 0; i < 64; i++) {
        const first = (h + (rotate(e,6) ^ rotate(e,11) ^ rotate(e,25)) +
          ((e & f) ^ (~e & g)) + constants[i] + words[i]) | 0;
        const second = ((rotate(a,2) ^ rotate(a,13) ^ rotate(a,22)) +
          ((a & b) ^ (a & c) ^ (b & c))) | 0;
        h=g; g=f; f=e; e=(d+first)|0; d=c; c=b; b=a; a=(first+second)|0;
      }
      [a,b,c,d,e,f,g,h].forEach((value, i) => { hashes[i] = (hashes[i] + value) | 0; });
    }
    return hashes.map(value => (value >>> 0).toString(16).padStart(8,"0")).join("");
  }
  function seedValue(seed) {
    if (!Number.isInteger(seed) || seed < 0 || seed > 4294967295)
      fail("INVALID_SEED", "Seed must be an unsigned 32-bit integer.");
    return seed >>> 0;
  }
  function deriveSeed(seed, role) { return parseInt(sha256(VERSION + "|" + seedValue(seed) + "|" + role).slice(0,8),16); }
  function random(seed) {
    let state = seed >>> 0;
    return function () {
      state += 0x6D2B79F5; let t = Math.imul(state ^ state >>> 15, 1 | state);
      t ^= t + Math.imul(t ^ t >>> 7, 61 | t);
      return ((t ^ t >>> 14) >>> 0) / 4294967296;
    };
  }
  function makeRoute(seed) {
    const ecology = ["wet","fungi","falls"], rng = random(deriveSeed(seed,"route"));
    for (let i = ecology.length - 1; i > 0; i--) {
      const j = Math.floor(rng() * (i + 1)); [ecology[i],ecology[j]] = [ecology[j],ecology[i]];
    }
    return ["camp","narrow",ecology[0],"engineering",ecology[1],"cargo",ecology[2],"mine"];
  }
  function merge(base, patch) {
    const result = clone(base);
    Object.keys(patch || {}).forEach(key => {
      const value = patch[key];
      result[key] = value && typeof value === "object" && !Array.isArray(value)
        ? merge(result[key] || {}, value) : clone(value);
    });
    return result;
  }
  function defaultSignature() {
    return {
      socketId:"P128-v2", kind:"dry", coordinates:"pixel-y-down-edge-inward-v1",
      band:{width:BAND,columns:Array.from({length:BAND+1},(_,i)=>i),
        floor:Array(BAND+1).fill(0),roof:Array(BAND+1).fill(-CLEARANCE)},
      layers:{
        foreground:{material:"handdrawn-rock-v2",alpha:"opaque-outside-air-v2",
          edge:"canonical-ink-v2",phase:"world-mirror-768-v2"},
        rim:{material:"warm-soil-v2",edge:"canonical-rim-v2"},
        backwall:{material:"backwall-clean-v1",coverage:"continuous",phase:"world-mirror-623-v2"},
        ecology:{coverage:"empty-interface"}
      },
      water:{kind:"none",direction:"none",level:null},
      roadTop:{farOffset:-8,nearOffset:12,frontLipDepth:9,projection:"sideview-depth-v2"}
    };
  }
  function makePort(side, floorY, override) {
    const signature = merge(defaultSignature(), override || {}), band = signature.band;
    const arrays = [band.columns,band.floor,band.roof];
    if (!Number.isInteger(band.width) || band.width < 1 || band.width > 128 ||
      arrays.some(values => !Array.isArray(values) || values.length !== band.width + 1 ||
        values.some(value => !Number.isFinite(value))) ||
      band.columns.some((value,index) => value !== index) ||
      band.floor.some((value,index) => value <= band.roof[index]) ||
      !signature.socketId || !signature.coordinates ||
      !["foreground","rim","backwall","ecology"].every(layer => signature.layers[layer]) ||
      !signature.water || !signature.roadTop ||
      !["farOffset","nearOffset","frontLipDepth"].every(key=>Number.isFinite(signature.roadTop[key])) ||
      signature.roadTop.nearOffset<=signature.roadTop.farOffset || !signature.roadTop.projection)
      fail("INVALID_PORT", "Incomplete floor, roof, road-top, or layer socket signature.", {side});
    return {side,normal:{x:side==="left"?-1:1,y:0},origin:{x:side==="left"?0:623,y:floorY},
      signature,signatureHash:sha256(canonical(signature))};
  }
  function portCompatible(right, left) {
    return !!right && !!left && right.side==="right" && left.side==="left" &&
      right.normal.x===-left.normal.x && right.normal.y===-left.normal.y &&
      canonical(right.signature)===canonical(left.signature);
  }
  function authoredSample(points,x) {
    if(!Array.isArray(points)||points.length<2)return NaN;
    for(let i=1;i<points.length;i++) {
      const a=points[i-1],b=points[i];
      if(x>=a[0]&&x<=b[0]&&b[0]>a[0])return a[1]+(b[1]-a[1])*(x-a[0])/(b[0]-a[0]);
    }
    return NaN;
  }
  function verifyAuthoredPorts(shape,ports,options) {
    const issues=[],epsilon=options?.epsilon??.000001,width=options?.width??623;
    function compare(actual,expected,side,layer,column) {
      if(!Number.isFinite(actual)||Math.abs(actual-expected)>epsilon)
        issues.push({side,layer,column,expected,actual:Number.isFinite(actual)?actual:null});
    }
    for(const side of ["left","right"]) {
      const port=ports?.[side],band=port?.signature?.band;
      if(!band){issues.push({side,layer:"port",message:"Missing complete port signature."});continue;}
      band.columns.forEach((column,index)=>{
        const x=side==="left"?column:width-column,origin=port.origin.y;
        compare(authoredSample(shape?.floor,x)-origin,band.floor[index],side,"floor",column);
        compare(authoredSample(shape?.roof,x)-origin,band.roof[index],side,"roof",column);
        const road=shape?.road,signature=port.signature.roadTop;
        if(options?.requireRoad!==false) {
          compare(authoredSample(road?.centre,x)-origin,band.floor[index],side,"road.centre",column);
          compare(authoredSample(road?.far,x)-origin,band.floor[index]+signature.farOffset,side,"road.far",column);
          compare(authoredSample(road?.near,x)-origin,band.floor[index]+signature.nearOffset,side,"road.near",column);
        }
      });
      if(options?.requireRoad!==false&&shape?.road?.lipDepth!==port.signature.roadTop.frontLipDepth)
        issues.push({side,layer:"road.lipDepth",expected:port.signature.roadTop.frontLipDepth,actual:shape?.road?.lipDepth??null});
      if(options?.requireRoad!==false&&shape?.road?.projection!==port.signature.roadTop.projection)
        issues.push({side,layer:"road.projection",expected:port.signature.roadTop.projection,actual:shape?.road?.projection??null});
    }
    return {passed:!issues.length,issues,samplesPerSide:65,
      shapeIdentity:sha256(canonical({floor:shape?.floor||null,roof:shape?.roof||null,road:shape?.road||null}))};
  }
  function bindAuthoredPorts(shape,ports,options) {
    const verification=verifyAuthoredPorts(shape,ports,options);
    if(!verification.passed)fail("AUTHORED_PORT_MISMATCH","Authored shape differs from its declared port.",verification);
    const bound=clone(ports);
    for(const side of ["left","right"])bound[side].binding={
      shapeIdentity:verification.shapeIdentity,signatureHash:bound[side].signatureHash,
      sampledColumns:65,roadIncluded:options?.requireRoad!==false};
    return bound;
  }
  function connectorId(a, b) {
    if (!ECOLOGIES.includes(a) || !ECOLOGIES.includes(b)) return null;
    if (a==="dry" || b==="dry" || a===b) return "dry128-standard";
    if (a==="moss" || b==="moss" || a==="fungi"&&b==="falls" || a==="falls"&&b==="fungi")
      return "dry128-moss";
    return "dry128-damp";
  }
  function compileCatalog(catalog, profiles, options) {
    options = options || {};
    if (!Array.isArray(catalog) || !catalog.length || !Array.isArray(profiles))
      fail("INVALID_LIBRARY", "A non-empty module catalog and profiles are required.");
    const ids = new Set();
    const candidates = catalog.map((source, sourceIndex) => {
      const profile = profiles[source.panel];
      if (!source.id || ids.has(source.id) || !source.slot || !ECOLOGIES.includes(source.ecology) ||
        !profile || !["floor","roof"].every(key => Array.isArray(profile[key]) && profile[key].length >= 2 &&
          profile[key].every(point => Array.isArray(point) && point.length===2 && point.every(Number.isFinite))))
        fail("INVALID_SOURCE", "Invalid identity, ecology, or authored profile.", {sourceId:source.id,sourceIndex});
      ids.add(source.id);
      const weight = source.weight === undefined ? 1 : source.weight;
      if (!Number.isFinite(weight) || weight <= 0) fail("INVALID_WEIGHT","Module weight must be positive.",{sourceId:source.id});
      return {sourceId:source.id,sourceIndex,panel:source.panel,
        artFamily:source.artFamily||"native-panel-"+source.panel,slot:source.slot,ecology:source.ecology,
        orientation:"normal",weight,shapeIdentity:sha256(canonical(profile)),
        normalization:{contractId:"canonical-dry128-64-v2",bandWidth:BAND,clearance:CLEARANCE},
        ports:{left:makePort("left",profile.floor[0][1],source.socketOverrides?.left),
          right:makePort("right",profile.floor[profile.floor.length-1][1],source.socketOverrides?.right)}};
    });
    const connectors = ["dry128-standard","dry128-moss","dry128-damp"].map(id => ({
      id,width:96,height:CLEARANCE,bandWidth:BAND,waterPolicy:"local-sources-and-sinks-no-crossing",
      ecologyTransition:id.replace("dry128-",""),ports:{left:makePort("left",0),right:makePort("right",0)}
    }));
    const connectorMap = Object.fromEntries(connectors.map(item => [item.id,item]));
    const links = candidates.map(a => candidates.map(b => {
      const opening = a.sourceId==="camp" && b.sourceId==="narrow";
      if (!opening && (a.sourceId===b.sourceId || a.panel===b.panel || a.artFamily===b.artFamily)) return null;
      const id = connectorId(a.ecology,b.ecology), connector = connectorMap[id];
      return connector && portCompatible(a.ports.right,connector.ports.left) &&
        portCompatible(connector.ports.right,b.ports.left) ? id : null;
    }));
    const identity = sha256(canonical({version:VERSION,catalog,profiles,candidates,connectors,links,
      assetIdentity:options.assetIdentity || null}));
    return {version:VERSION,identity,sourceIdentity:identity,candidates,connectors,links};
  }
  function entropy(domain, candidates) {
    let total=0,weightedLogs=0;
    domain.forEach(id => {const weight=candidates[id].weight;total+=weight;weightedLogs+=weight*Math.log(weight);});
    return Math.log(total)-weightedLogs/total;
  }
  function propagate(domains, library, initial, stats) {
    const queue=initial.slice(),queued=new Set(queue);
    for(let cursor=0;cursor<queue.length;cursor++) {
      const index=queue[cursor];queued.delete(index);
      for(const neighbor of [index-1,index+1]) {
        if(neighbor<0||neighbor>=domains.length)continue;
        const next=domains[neighbor].filter(target=>domains[index].some(source=>
          neighbor>index?library.links[source][target]:library.links[target][source]));
        stats.propagations++;
        if(!next.length)return {index:neighbor,from:index};
        if(next.length===domains[neighbor].length)continue;
        stats.removed+=domains[neighbor].length-next.length;domains[neighbor]=next;
        if(!queued.has(neighbor)){queued.add(neighbor);queue.push(neighbor);}
      }
    }
    return null;
  }
  function solve(options) {
    options=options||{};
    const seed=seedValue(options.seed),library=options.compiled||
      compileCatalog(options.catalog,options.profiles,{assetIdentity:options.assetIdentity});
    if(library.version!==VERSION)fail("LIBRARY_VERSION","Unsupported module-rule library version.");
    const route=options.route?options.route.slice():makeRoute(seed),locks=options.locks||{};
    if(!route.length||route.some(slot=>typeof slot!=="string"))fail("INVALID_ROUTE","Route slots must be explicit.");
    const maximum=options.maxAttempts===undefined?4:options.maxAttempts;
    if(!Number.isInteger(maximum)||maximum<1||maximum>16)fail("INVALID_ATTEMPTS","Attempt limit must be 1..16.");
    const initial=route.map((slot,index)=>library.candidates.map((candidate,id)=>({candidate,id}))
      .filter(item=>item.candidate.slot===slot&&
        (locks[index]===undefined||item.candidate.sourceId===locks[index])).map(item=>item.id));
    const empty=initial.findIndex(domain=>!domain.length);
    if(empty>=0)fail("EMPTY_DOMAIN","No authored candidate satisfies a route slot.",{index:empty,slot:route[empty],lock:locks[empty]});
    const stats={attempts:0,observations:0,propagations:0,removed:0,initialOptions:initial.map(domain=>domain.length)};
    let contradiction=null;
    for(let attempt=0;attempt<maximum;attempt++) {
      stats.attempts=attempt+1;
      const rng=random(deriveSeed(seed,"collapse:"+attempt)),domains=initial.map(domain=>domain.slice());
      contradiction=propagate(domains,library,domains.map((_,index)=>index),stats);
      while(!contradiction) {
        let best=-1,minimum=Infinity;
        domains.forEach((domain,index)=>{
          if(domain.length<2)return;
          const value=entropy(domain,library.candidates)+index*1e-9;
          if(value<minimum){minimum=value;best=index;}
        });
        if(best<0) {
          const selected=domains.map(domain=>clone(library.candidates[domain[0]]));
          const links=selected.slice(1).map((item,index)=>{
            const connector=library.links[domains[index][0]][domains[index+1][0]];
            if(!connector)fail("INTERNAL_ADJACENCY","Collapsed solution contains an incompatible edge.",{index});
            return {fromIndex:index,toIndex:index+1,connectorId:connector,
              contract:clone(library.connectors.find(value=>value.id===connector))};
          });
          return {version:VERSION,libraryIdentity:library.identity,route,selected,links,stats,
            seeds:{layout:deriveSeed(seed,"route"),modules:deriveSeed(seed,"modules"),
              content:deriveSeed(seed,"content"),decoration:deriveSeed(seed,"decoration")}};
        }
        const domain=domains[best],sum=domain.reduce((total,id)=>total+library.candidates[id].weight,0);
        let roll=rng()*sum,chosen=domain[domain.length-1];
        for(const id of domain){roll-=library.candidates[id].weight;if(roll<=0){chosen=id;break;}}
        stats.removed+=domain.length-1;domains[best]=[chosen];stats.observations++;
        contradiction=propagate(domains,library,[best],stats);
      }
    }
    fail("CONTRADICTION","No compatible module arrangement within the bounded attempt limit.",
      {seed,route,attempts:stats.attempts,contradiction,stats});
  }
  const api={VERSION,BAND,CLEARANCE,EcologyRuleError,compileCatalog,solve,makeRoute,portCompatible,
    verifyAuthoredPorts,bindAuthoredPorts,
    deriveSeed,random,canonical,sha256};
  root.EcologyModuleRules=api;
  if(typeof module!=="undefined"&&module.exports)module.exports=api;
})(globalThis);
