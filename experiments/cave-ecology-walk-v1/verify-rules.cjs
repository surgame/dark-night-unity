"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const crypto = require("node:crypto");
const rules = require("./module-rules.js");
const context = { EcologyModuleRules: rules };
context.globalThis = context;
vm.runInNewContext(fs.readFileSync(path.join(__dirname,"authored-profiles.js"),"utf8"),context);
vm.runInNewContext(fs.readFileSync(path.join(__dirname,"world-data.js"),"utf8"),context);
const { catalog, profiles } = context.EcologyWorld;
const library = rules.compileCatalog(catalog,profiles);
const route = ["camp","narrow","wet","engineering","fungi","cargo","falls","mine"];
const solved = rules.solve({ seed:271,compiled:library,route,locks:{3:"beam-gap"} });
const repeated = rules.solve({ seed:271,compiled:library,route,locks:{3:"beam-gap"} });
assert.equal(rules.sha256("abc"),crypto.createHash("sha256").update("abc").digest("hex"));
assert.equal(rules.sha256("生态接口"),crypto.createHash("sha256").update("生态接口").digest("hex"));
assert.equal(library.candidates.length,catalog.length,"Compilation must retain every authored source.");
assert.deepEqual(solved.selected,repeated.selected,"Same seed and library must reproduce the choice.");
assert(solved.stats.propagations>0&&solved.stats.removed>0,"Actual candidate propagation must occur.");
const pruned=rules.solve({seed:271,compiled:library,route:["wet","mine"],locks:{0:"creek-bank"}});
assert.equal(pruned.selected[1].sourceId,"mineral-bowl","A locked panel must prune its repeated neighbor.");
assert.equal(solved.links.length,solved.selected.length-1);
for(let i=1;i<solved.selected.length;i++) {
  const a=solved.selected[i-1],b=solved.selected[i],link=solved.links[i-1];
  assert(rules.portCompatible(a.ports.right,link.contract.ports.left));
  assert(rules.portCompatible(link.contract.ports.right,b.ports.left));
  assert(a.sourceId==="camp"&&b.sourceId==="narrow"||a.panel!==b.panel&&a.artFamily!==b.artFamily);
}
const changed = JSON.parse(JSON.stringify(solved.selected[2].ports.left));
changed.signature.band.floor[32]+=1;
assert(!rules.portCompatible(solved.selected[1].ports.right,changed),
  "An interior interface mismatch must fail even when endpoint floor and dry tag still match.");
changed.signature.band.floor[32]-=1;
changed.signature.layers.foreground.edge="foreign-ink";
assert(!rules.portCompatible(solved.selected[1].ports.right,changed),"Layer edges are part of compatibility.");
assert.throws(()=>rules.solve({seed:271,compiled:library,route:["wet","mine"],locks:{0:"creek-bank",1:"mineral-hall"}}),
  error=>error.code==="CONTRADICTION");
assert.throws(()=>rules.solve({seed:271,compiled:library,route,locks:{2:"not-authored"}}),
  error=>error.code==="EMPTY_DOMAIN");
const changedCatalog=JSON.parse(JSON.stringify(catalog));
changedCatalog.find(item=>item.id==="beam-gap").socketOverrides={
  right:{socketId:"P96-v2",band:{roof:Array(65).fill(-96)}}
};
const incompatible=rules.compileCatalog(changedCatalog,profiles);
assert.notEqual(incompatible.identity,library.identity);
assert.throws(()=>rules.solve({seed:271,compiled:incompatible,route,locks:{3:"beam-gap"}}),
  error=>error.code==="CONTRADICTION");
assert.equal(new Set(Object.values(solved.seeds)).size,4,"Layout, choice, content and decoration streams differ.");
const familyCatalog=JSON.parse(JSON.stringify(catalog));
familyCatalog.find(item=>item.id==="creek-stones").artFamily="shared-bridge-author";
familyCatalog.find(item=>item.id==="mineral-bowl").artFamily="shared-bridge-author";
const familyLibrary=rules.compileCatalog(familyCatalog,profiles);
assert.throws(()=>rules.solve({seed:271,compiled:familyLibrary,route:["wet","mine"],
  locks:{0:"creek-stones",1:"mineral-bowl"}}),error=>error.code==="CONTRADICTION");
const expandedProfiles=[...profiles,profiles[0]],expandedCatalog=JSON.parse(JSON.stringify(catalog));
expandedCatalog.find(item=>item.id==="beam-gap").panel=profiles.length;
assert.equal(rules.compileCatalog(expandedCatalog,expandedProfiles).candidates.find(item=>
  item.sourceId==="beam-gap").panel,profiles.length,"Author profiles are not limited to the original six panels.");
console.log(JSON.stringify({
  version:rules.VERSION,libraryIdentity:library.identity,sources:catalog.length,
  selected:solved.selected.map(item=>item.sourceId),stats:solved.stats,
  checks:["sha256","source-retention","reproduction","propagation","full-profile-and-layer-matching",
    "bounded-contradiction","unknown-lock","incompatible-socket","independent-streams","shared-art-family","additional-authored-profile"],
  scope:"Module-rule checks only; no rendered seam, terrain collision, actor traversal or production art approval."
},null,2));
