"use strict";
const fs = require("fs");
const path = require("path");
const vm = require("vm");
const crypto = require("crypto");
const assert = require("assert");
const root = __dirname;
const runtime = { window: {}, TextEncoder };
vm.createContext(runtime);
for (const file of ["contours.js", "authored-profiles.js", "module-rules.js", "terrain-shapes.js", "world-data.js", "engine.js"])
  vm.runInContext(fs.readFileSync(path.join(root, file), "utf8"), runtime, { filename: file });
const World = runtime.EcologyWorld;
const Engine = runtime.window.EcologyEngine;
const hash = file => crypto.createHash("sha256").update(fs.readFileSync(path.join(root, file))).digest("hex");
const nearly = (a, b) => Math.abs(a - b) < 0.000001;
const report = {
  date: "2026-10-31",
  scope: "Offline browser-physics verification using automatic button inputs only. No hero teleportation, Unity, Player, multiplayer, formal collision or production art acceptance.",
  source: ["contours.js", "authored-profiles.js", "module-rules.js", "terrain-shapes.js", "world-data.js", "engine.js", "verify-engine.cjs"].map(file => ({ file, sha256: hash(file) })),
  physics: [], ceilings: [], static: [], failures: []
};

function inspectWorld(seed) {
  const world = World.create(seed);
  assert.strictEqual(world.catalog.length, 14, "Expected fourteen catalog entries.");
  assert.strictEqual(new Set(world.catalog.map(item => item.id)).size, 14);
  assert.strictEqual(world.rooms.length, 8, "Expected eight selected rooms.");
  assert.strictEqual(new Set(world.rooms.map(room => room.id)).size, 8);
  assert.deepStrictEqual(Array.from(world.rooms, room => room.slot), Array.from(World.create(seed).slots));
  assert.strictEqual(world.rooms[0].slot, "camp");
  assert.strictEqual(world.rooms[1].slot, "narrow");
  assert.strictEqual(world.rooms[7].slot, "mine");
  assert(world.rooms.findIndex(room => room.slot === "engineering") <
    world.rooms.findIndex(room => room.slot === "cargo"), "Route bridge must precede cargo handling.");
  for (const ecology of ["wet", "fungi", "falls"])
    assert.strictEqual(world.rooms.filter(room => room.slot === ecology).length, 1);
  assert(typeof world.id === "string" && world.id.length, "World ID is required for seed-bound restores.");
  for (const field of ["width", "height", "fallY"])
    assert(Number.isFinite(world[field]) && world[field] > 0, "Missing world." + field);
  for (const surface of world.surfaces) {
    assert(surface.points.length >= 2);
    surface.points.forEach((point, index) => {
      assert(point.length === 2 && point.every(Number.isFinite), "Non-finite surface coordinate.");
      if (index) assert(point[0] > surface.points[index - 1][0], "Surface must run left to right.");
    });
  }
  assert(world.ceilings?.length >= 8, "Expected native ceiling contours for the eight rooms.");
  for (const ceiling of world.ceilings) ceiling.points.forEach((point, index) => {
    assert(point.length === 2 && point.every(Number.isFinite), "Non-finite ceiling coordinate.");
    if (index) assert(point[0] > ceiling.points[index - 1][0], "Ceiling must run left to right.");
  });
  const ports = [];
  for (let index = 1; index < world.rooms.length; index++) {
    const previous = world.rooms[index - 1], current = world.rooms[index];
    const from = previous.y + World.sample(previous.profile.floor, 623);
    const to = current.y + World.sample(current.profile.floor, 0);
    const connection = world.surfaces.find(surface => surface.id === "connector-" + index);
    assert(nearly(from, to), "Adjacent floor ports differ in height.");
    assert(connection && nearly(connection.points[0][1], from) &&
      nearly(connection.points[1][1], to), "Connector differs from room floor ports.");
    assert(nearly(connection.points[0][0], previous.x + 623) &&
      nearly(connection.points[1][0], current.x), "Connector does not reach room edges.");
    ports.push({ from: previous.id, to: current.id, floorY: from });
  }
  assert.strictEqual(world.objects.filter(object => object.type === "ore" && object.primary).length, 1);
  return { world, summary: {
    seed, worldId: world.id, catalog: world.catalog.length, selected: world.rooms.length,
    selection: world.rooms.map(room => ({ id: room.id, name: room.name, slot: room.slot })),
    surfaceCount: world.surfaces.length, finiteSurfaceCoordinates: true,
    nativeCeilings: world.ceilings.length, finiteCeilingCoordinates: true,
    floorPortHeightsEqual: true, ports
  } };
}

function verifyCeiling(name, points, expectedCeiling) {
  const engine = new Engine({
    id: "ceiling-probe-" + name, width: 240, height: 300, fallY: 400,
    spawn: { x: 100, y: 200 }, objects: [],
    surfaces: [{ id: "floor", points: [[0, 200], [240, 200]] }],
    ceilings: [{ id: "roof", points }]
  });
  let minHead = 200 - 41, bumped = false;
  for (let step = 0; step < 120; step++) {
    const snapshot = engine.step(1 / 120, { jump: true });
    minHead = Math.min(minHead, snapshot.hero.y - snapshot.hero.h);
    if (!snapshot.hero.grounded && snapshot.hero.vy === 0 &&
      nearly(snapshot.hero.y - snapshot.hero.h, expectedCeiling)) bumped = true;
  }
  assert(minHead >= expectedCeiling - 0.000001, "Jump crossed ceiling " + name);
  assert(bumped, "Probe did not actually hit ceiling " + name);
  assert(engine.hero.grounded && nearly(engine.hero.y, 200), "Probe did not land after ceiling hit.");
  return { name, expectedCeiling, minHead, bumped, landed: true, buttonInputOnly: true };
}

function walk(seed) {
  const { world, summary } = inspectWorld(seed);
  const engine = new Engine(world), visitedOut = new Set(), visitedBack = new Set();
  const jumped = new Set(), trace = [];
  let lastEventId = 0, previousInteract = false, returned = false;
  let steps = 0, recoveries = 0, mining = 0, deliveries = 0, chuteTransfers = 0;
  let maxX = engine.hero.x, lastProgressX = engine.hero.x, stagnant = 0;
  for (; steps < 20000 && !engine.completed; steps++) {
    const state = engine.snapshot(), hero = state.hero;
    const returning = state.flags.primaryCollected;
    if (returning && !returned) {
      returned = true;
      trace.push({ step: steps, type: "turn-for-home", x: hero.x, y: hero.y });
    }
    const room = world.rooms.find(item => hero.x >= item.x && hero.x <= item.x + 623);
    if (room) (returning ? visitedBack : visitedOut).add(room.id);
    const target = state.target;
    let action = false;
    if (!previousInteract && target) {
      if (!returning) action = target.type === "supply" ||
        target.type === "bridge" || target.type === "beam" ||
        (target.type === "ore" && target.primary);
      else action = (hero.carry && (target.type === "stash" ||
        (target.type === "chute" && target.target.x < target.x))) ||
        (!hero.carry && target.type === "looseOre" && target.primary);
    }
    let jump = false;
    if (!returning && hero.grounded && room?.cut && room.mechanism !== "bridge" &&
      hero.x >= room.x + room.cut.a - 19 && hero.x <= room.x + room.cut.a - 1 &&
      !jumped.has(room.id)) {
      jump = true;
      jumped.add(room.id);
      trace.push({ step: steps, type: "jump", roomId: room.id, x: hero.x, y: hero.y });
    }
    const input = { left: returning, right: !returning, jump, interact: action };
    if (action) trace.push({ step: steps, type: "input-E", target: target.id,
      targetType: target.type, x: hero.x, y: hero.y });
    const next = engine.step(1 / 60, input);
    previousInteract = action;
    for (const event of next.events.filter(item => item.id > lastEventId)) {
      lastEventId = Math.max(lastEventId, event.id);
      trace.push({ step: steps, ...event });
      if (event.type === "chute") chuteTransfers++;
      if (event.type === "mining") mining++;
      if (event.type === "delivery") deliveries++;
    }
    // Recovery is an authored prototype rule, and is reported separately from walking.
    if (Math.abs(next.hero.x - hero.x) > 30 || Math.abs(next.hero.y - hero.y) > 60) {
      recoveries++;
      (report.recoveryDiagnostics||=[]).push({seed,step:steps,room:room?.sourceId,
        before:hero,after:next.hero,logs:next.logs});
    }
    maxX = Math.max(maxX, next.hero.x);
    if (Math.abs(next.hero.x - lastProgressX) > 2) {
      lastProgressX = next.hero.x;
      stagnant = 0;
    } else stagnant++;
    assert(stagnant < 360, "No horizontal progress for six seconds: " + JSON.stringify({
      seed, step: steps, returning, hero: next.hero, target: next.target,
      prompt: next.prompt, flags: next.flags, logs: next.logs
    }));
  }
  const final = engine.snapshot();
  assert(final.completed && final.flags.primaryDelivered, "Did not deliver the primary ore within step budget.");
  assert.strictEqual(visitedOut.size, 8, "Did not walk through every outbound room.");
  assert.strictEqual(visitedBack.size, 8, "Did not revisit every room on return.");
  assert.strictEqual(chuteTransfers, 1, "Expected one return cargo-chute transfer.");
  assert.strictEqual(mining, 1, "Expected exactly the primary mining action.");
  assert.strictEqual(deliveries, 1);
  assert.strictEqual(recoveries, 0, "Primary route unexpectedly used automatic fall recovery.");
  assert(final.flags.bridgeBuilt, "Did not construct the route bridge.");
  const bridges = final.objects.filter(object => object.type === "bridge" || object.type === "beam");
  assert(bridges.length > 0 && bridges.every(object => object.built &&
    engine.linkedSurfaces(object).every(surface => surface.enabled !== false)),
  "Constructed bridges did not remain available after the return trip.");
  const restored = new Engine(world);
  assert(restored.restoreState(engine.exportState()) && restored.completed, "Completed state did not restore.");
  const wrongSeed = new Engine(World.create(seed + 1));
  assert(!wrongSeed.restoreState(engine.exportState()), "State restored into a different seeded world.");
  return {
    ...summary, steps, seconds: final.time, route: "Eight rooms outbound, then the same eight rooms home.",
    buttonInputOnly: true, heroTeleportsByVerifier: 0, authoredFallRecoveries: recoveries,
    visitedOutbound: Array.from(visitedOut), visitedReturn: Array.from(visitedBack),
    maxX, jumps: jumped.size, mining, chuteTransfers, deliveries,
    flags: final.flags, completed: final.completed, sessionRestore: true,
    constructedBridgesRemainEnabled: true,
    crossSeedRestoreRejected: true, finalHero: final.hero, trace
  };
}

for (const [name, points, expected] of [
  ["flat", [[0, 140], [240, 140]], 140],
  ["slanted-peak", [[0, 135], [90, 135], [100, 150], [110, 135], [240, 135]], 150],
  ["off-center-tooth", [[0, 135], [104, 135], [108, 155], [112, 135], [240, 135]], 155]
]) {
  try { report.ceilings.push(verifyCeiling(name, points, expected)); }
  catch (error) { report.failures.push({ kind: "ceiling", name, error: String(error.stack || error) }); }
}
const selectedSeed=process.argv.find(value=>value.startsWith("--seed="));
for (const seed of selectedSeed?[Number(selectedSeed.slice(7))]:[271, 902, 1643]) {
  try { report.physics.push(walk(seed)); }
  catch (error) { report.failures.push({ kind: "physics", seed, error: String(error.stack || error) }); }
}
for (const seed of [0, 1, 2, 3, 4, 5, 13, 42, 77, 101, 4096, 9999, 65535, 2147483647, 4294967295]) {
  try { report.static.push(inspectWorld(seed).summary); }
  catch (error) { report.failures.push({ kind: "static", seed, error: String(error.stack || error) }); }
}
report.summary = {
  passed: report.failures.length === 0, physicsSeeds: report.physics.length,
  ceilingProbes: report.ceilings.length, staticSeeds: report.static.length, automaticInputsOnly: true,
  distinctRouteOrders: new Set([...report.physics, ...report.static]
    .map(item => item.selection.map(room => room.slot).join("/"))).size,
  limitation: "Floor endpoint checks do not certify full socket geometry, alpha seams, overhead clearance, formal gameplay balance or Unity acceptance."
};
const records=path.join(root,"verification-records");
fs.mkdirSync(records,{recursive:true});
const output = process.argv.find(value=>value.startsWith("--out="))?.slice(6) ||
  path.join(records,"repair-engine-"+Date.now()+".json");
if (fs.existsSync(output)) {
  const previous = JSON.parse(fs.readFileSync(output, "utf8"));
  if (!previous.summary?.passed || previous.previousAttempts) {
    const { previousAttempts, ...attempt } = previous;
    report.previousAttempts = [...(previousAttempts || []), attempt];
  }
}
fs.writeFileSync(output, JSON.stringify(report, null, 2) + "\n");
console.log(JSON.stringify({
  summary: report.summary,
  routes: report.physics.map(item => ({
    seed: item.seed, steps: item.steps, seconds: item.seconds,
    recoveries: item.authoredFallRecoveries, completed: item.completed
  })),
  failures: report.failures.map(item => ({ seed: item.seed, kind: item.kind, error: item.error.slice(0, 2200) })),
  report: output
}, null, 2));
process.exitCode = report.summary.passed ? 0 : 1;
