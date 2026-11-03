"use strict";
const assert = require("assert");
const crypto = require("crypto");
const fs = require("fs");
const path = require("path");
const vm = require("vm");
const runtime = { window: {} };
vm.createContext(runtime);
vm.runInContext(fs.readFileSync(path.join(__dirname, "engine.js"), "utf8"), runtime);
const Engine = runtime.window.EcologyEngine;
const copy = value => JSON.parse(JSON.stringify(value));
const report = {
  date: "2026-10-31",
  scope: "Focused offline phase checks with small fixtures; no Unity, visual acceptance or movement tuning.",
  source: ["engine.js", "renderer.js", "verify-phases.cjs"].map(file => ({
    file, sha256: crypto.createHash("sha256").update(fs.readFileSync(path.join(__dirname, file))).digest("hex")
  })),
  checks: [], failures: []
};
function check(name, action) {
  try { action(); report.checks.push({ name, ok: true }); }
  catch (error) { report.failures.push({ name, message: error.stack }); }
}
function advance(engine, duration) {
  const until = engine.time + duration;
  while (engine.time + 1e-10 < until) engine.step(Math.min(0.05, until - engine.time), {});
}
function interact(engine) {
  engine.step(0, {});
  engine.step(0, { interact: true });
  engine.step(0, {});
}
function fixture(kind) {
  const world = { id: "phase-fixture-" + kind, spawn: { x: 40, y: 100 },
    width: 500, height: 300, fallY: 400, ceilings: [], walls: [], objects: [], surfaces: [] };
  if (kind === "bridge") {
    world.surfaces = [
      { id: "left", points: [[0,100],[90,100]] },
      { id: "span", kind: "bridge", points: [[90,100],[210,100]], enabled: false },
      { id: "right", points: [[210,100],[500,100]] }
    ];
    world.objects = [
      { id: "supply", type: "supply", x: 40, y: 100 },
      { id: "builder", type: "bridge", x: 75, y: 100, surfaceId: "span", requireSupply: true }
    ];
  } else {
    world.surfaces = [{ id: "floor", points: [[0,100],[500,100]] }];
    world.objects = [
      { id: "phase-ore", type: "ore", x: 40, y: 100, primary: true, name: "phase ore" },
      { id: "chute", type: "chute", x: 60, y: 100, target: { x: 42, y: 100 } }
    ];
  }
  return world;
}
check("Bridge support waits for construction, including a saved midpoint", () => {
  const world = fixture("bridge"), engine = new Engine(world);
  interact(engine);
  assert.strictEqual(engine.flags.hasBeam, true);
  interact(engine);
  assert(engine.objects.find(object => object.id === "builder").building);
  assert.strictEqual(engine.flags.hasBeam, false);
  assert.strictEqual(engine.flags.bridgeBuilt, false);
  assert.strictEqual(engine.groundAt(150, 100, 1), null);
  advance(engine, 0.4);
  const saved = engine.exportState(), restored = new Engine(world);
  assert(restored.restoreState(saved));
  assert.strictEqual(restored.objects.find(object => object.id === "builder").building.startedAt, 0);
  const invalid = copy(saved);
  invalid.enabled[1] = true;
  assert.strictEqual(restored.restoreState(invalid), false);
  advance(restored, 0.24);
  assert.strictEqual(restored.surfaces[1].enabled, false);
  assert.strictEqual(restored.target(), null);
  advance(restored, 0.01);
  assert.strictEqual(restored.surfaces[1].enabled, true);
  assert(restored.groundAt(150, 100, 1));
  assert.strictEqual(restored.flags.bridgeBuilt, true);
  assert.strictEqual(restored.objects.find(object => object.id === "builder").building, null);
  assert.strictEqual(restored.events.filter(event => event.type === "bridge").length, 1);
  interact(restored);
  advance(restored, 0.7);
  assert.strictEqual(restored.events.filter(event => event.type === "bridge").length, 1);
});
check("In-flight cargo cannot be picked up; identity and remaining duration survive restore", () => {
  const world = fixture("cargo"), engine = new Engine(world);
  interact(engine);
  assert.strictEqual(engine.hero.carry.cargoId, "phase-ore");
  interact(engine);
  assert.strictEqual(engine.hero.carry, null);
  assert.strictEqual(engine.transfers.length, 1);
  assert.strictEqual(engine.objects.filter(object => object.type === "looseOre").length, 0);
  advance(engine, 0.4);
  const saved = engine.exportState(), transferId = saved.transfers[0].id;
  const cargoId = saved.transfers[0].cargo.id, restored = new Engine(world);
  assert(restored.restoreState(saved));
  assert(restored.restoreState(saved));
  assert.strictEqual(restored.transfers[0].id, transferId);
  assert.strictEqual(restored.transfers[0].cargo.id, cargoId);
  const invalid = copy(saved);
  invalid.transfers[0].to.x = null;
  assert.strictEqual(restored.restoreState(invalid), false);
  interact(restored);
  assert.strictEqual(restored.hero.carry, null);
  advance(restored, 0.44);
  assert.strictEqual(restored.transfers.length, 1);
  assert.strictEqual(restored.objects.filter(object => object.type === "looseOre").length, 0);
  advance(restored, 0.01);
  assert.strictEqual(restored.transfers.length, 0);
  const arrived = restored.objects.filter(object => object.type === "looseOre");
  assert.strictEqual(arrived.length, 1);
  assert.strictEqual(arrived[0].id, cargoId);
  assert.strictEqual(arrived[0].cargoId, "phase-ore");
  assert.strictEqual(arrived[0].x, 42);
  interact(restored);
  assert.strictEqual(restored.hero.carry.id, cargoId);
  assert.strictEqual(restored.hero.carry.cargoId, "phase-ore");
  advance(restored, 1);
  assert.strictEqual(restored.objects.filter(object => object.id === cargoId).length, 1);
  assert.strictEqual(restored.events.filter(event => event.type === "chute-arrive").length, 1);
});
check("Phase snapshots do not expose mutable construction or cargo state", () => {
  const bridge = new Engine(fixture("bridge"));
  interact(bridge);interact(bridge);
  bridge.snapshot().objects.find(object => object.building).building.duration = 9;
  assert.strictEqual(bridge.objects.find(object => object.building).building.duration, 0.65);
  const cargo = new Engine(fixture("cargo"));
  interact(cargo);interact(cargo);
  cargo.snapshot().transfers[0].cargo.cargoId = "changed";
  assert.strictEqual(cargo.transfers[0].cargo.cargoId, "phase-ore");
});
check("Horizontal motion respects the authored ceiling and permits escape from an initial overlap", () => {
  const world = fixture("cargo");
  world.objects = [];
  world.ceilings = [{ id: "roof", points: [[0,0],[80,0],[100,70],[200,70],[220,0],[500,0]] }];
  const engine = new Engine(world);
  for (let index = 0; index < 25; index++) engine.step(0.05, { right: true });
  assert(engine.hero.x < 95, "Hero entered a ceiling lower than its head.");
  assert(engine.hero.y - engine.hero.h >= engine.ceilingAt(engine.hero.x) - 0.01);
  const overlapWorld = copy(world);
  overlapWorld.spawn.x = 140;
  const escaping = new Engine(overlapWorld);
  for (let index = 0; index < 25; index++) escaping.step(0.05, { right: true });
  assert(escaping.hero.x > 240, "An initially overlapping hero could not leave toward increasing clearance.");
  assert(escaping.hero.y - escaping.hero.h >= escaping.ceilingAt(escaping.hero.x));
});
check("Cargo clearance uses actual roof geometry instead of the lowroof metadata rectangle", () => {
  const metadataWorld = fixture("cargo");
  metadataWorld.objects.push({ id: "hint", type: "lowroof", x: 0, y: 0, w: 500, h: 300 });
  metadataWorld.ceilings = [{ id: "roof", points: [[0,20],[500,20]] }];
  const clear = new Engine(metadataWorld);
  assert.strictEqual(clear.cargoSpaceBlocked(), false);
  interact(clear);
  assert(clear.hero.carry, "A hint rectangle prevented pickup under a clear roof.");
  let minimumY = clear.hero.y;
  for (let index = 0; index < 30; index++) {
    clear.step(1 / 60, { jump: index === 0 });
    minimumY = Math.min(minimumY, clear.hero.y);
  }
  assert(minimumY >= 79 - 1e-6, "Carried cargo crossed the ceiling during a jump.");
  const narrowWorld = fixture("cargo");
  narrowWorld.ceilings = [{ id: "roof", points: [[0,0],[80,0],[100,50],[220,50],[240,0],[500,0]] }];
  const loaded = new Engine(narrowWorld);
  interact(loaded);
  for (let index = 0; index < 25; index++) loaded.step(0.05, { right: true });
  assert(loaded.hero.x < 100, "Loaded hero passed through a roof below the carried cargo.");
  assert(loaded.hero.y - loaded.hero.h - 18 >= loaded.ceilingAt(loaded.hero.x) - 0.01);
  const empty = new Engine(narrowWorld);
  for (let index = 0; index < 25; index++) empty.step(0.05, { right: true });
  assert(empty.hero.x > 150, "Empty hero was blocked by a passage tall enough for the body.");
  narrowWorld.spawn.x = 150;
  narrowWorld.objects = [{ id: "low-ore", type: "ore", x: 150, y: 100 }];
  const pickup = new Engine(narrowWorld);
  assert.strictEqual(pickup.cargoSpaceBlocked(), true);
  interact(pickup);
  assert.strictEqual(pickup.hero.carry, null, "Cargo could be picked up through a low roof.");
});
check("Airborne motion cannot enter an ascending solid slope and one-way platforms remain passable", () => {
  const world = { id: "ascending-solid-fixture", spawn: { x: 65, y: 105 },
    width: 300, height: 300, fallY: 500, ceilings: [], walls: [], objects: [],
    surfaces: [{ id: "ramp", kind: "shallow", points: [[0,120],[80,120],[100,80],[300,80]] }] };
  const engine = new Engine(world);
  assert.strictEqual(engine.hero.grounded, false);
  for (let index = 0; index < 39; index++) engine.step(1 / 60, { right: true });
  const floor = engine.surfaceY(engine.surfaces[0], engine.hero.x);
  assert(engine.hero.x > 90, "Slope contact prevented the normal landing and ascent.");
  assert(engine.hero.y <= floor + 0.1, "Airborne hero entered the rising ground and fell through it.");
  assert.strictEqual(engine.hero.grounded, true);
  for (const kind of ["ledge", "crumble", "bridge"]) {
    const oneWay = copy(world);
    oneWay.id = "one-way-" + kind;
    oneWay.spawn = { x: 80, y: 110 };
    oneWay.surfaces[0].kind = kind;
    const passage = new Engine(oneWay);
    passage.horizontalMove(10);
    assert.strictEqual(passage.hero.x, 90, kind + " gained an unintended solid side wall.");
  }
});
report.ok = report.failures.length === 0;
const recordDirectory = path.join(__dirname, "verification-records");
fs.mkdirSync(recordDirectory, { recursive: true });
const reportPath = path.join(recordDirectory, "phases-" + new Date().toISOString().replace(/[:.]/g, "-") + ".json");
fs.writeFileSync(reportPath, JSON.stringify(report, null, 2) + "\n");
console.log(JSON.stringify({ ok: report.ok, checks: report.checks.length, failures: report.failures.length,
  report: reportPath }));
if (!report.ok) process.exitCode = 1;
