import { readFile, mkdir, writeFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import path from "node:path";
import vm from "node:vm";
import assert from "node:assert/strict";
import { runtimeFiles, browserFiles } from "./source-files.mjs";

const root = path.dirname(fileURLToPath(import.meta.url));
const context = vm.createContext({ window: {}, console });
for (const file of browserFiles) {
  const text = await readFile(path.join(root, "src", file), "utf8");
  new vm.Script(text, { filename: file });
  if (runtimeFiles.includes(file)) vm.runInContext(text, context);
  assert.ok(!/\b(fetch|XMLHttpRequest|WebSocket)\s*\(/.test(text), "No external data dependency: " + file);
}
const D = context.window.DN, results = [];
const test = (name, run) => {
  try { run(); results.push({ name, passed: true }); }
  catch (error) { results.push({ name, passed: false, reason: error.message }); }
};
const world = id => new D.World(D.catalog.find(s => s.id === id));
const frames = (w, seconds, inputs = [{}, {}]) => {
  for (let i = 0; i < Math.ceil(seconds * 60); i++) D.step(w, inputs, 1 / 60);
};
function walk(w, x, both = false) {
  for (let i = 0; i < 20 * 60 && Math.abs(w.players[0].x - x) > 4; i++) {
    const input = { left: w.players[0].x > x, right: w.players[0].x < x };
    frames(w, 1 / 60, [input, both ? input : {}]);
  }
  assert.ok(Math.abs(w.players[0].x - x) < 7, "ordinary input reaches interaction point");
}
test("48 independent scene definitions and valid equipment", () => {
  assert.equal(D.catalog.length, 48);
  for (const scene of D.catalog) {
    assert.ok(scene.kit.length <= 4 && scene.kit.length >= 3);
    scene.kit.forEach(item => assert.ok(D.items[item]));
    assert.ok(scene.objective && scene.hint && scene.description && scene.boundary);
    assert.ok(new D.World(scene).objects.length > 2);
  }
});
test("All 48 scenes simulate without input, nonfinite state, or unbounded enemy spawning", () => {
  for (const scene of D.catalog) {
    const w = new D.World(scene); frames(w, 35);
    assert.ok(w.enemies.length <= 5, scene.id);
    assert.ok(w.players.every(p => Number.isFinite(p.x) && Number.isFinite(p.y)), scene.id);
    assert.ok(w.events.length <= 180);
  }
});
test("Directional explosion destroys forward rock and preserves rear rock", () => {
  const w = world("w-charge");
  const back = w.addSolid(470, 420, 32, 32, "rock");
  const forward = w.addSolid(570, 420, 32, 32, "rock");
  D.blast(w, 540, 436, { x: 1, y: 0 }, w.players[0], true);
  assert.equal(back.hp, 2); assert.ok(forward.hp <= 0);
});
test("Bedrock is protected from mining and explosions", () => {
  const w = world("w-charge"), floor = w.solids[0];
  D.breakSolid(w, floor, 100, "pick"); D.blast(w, 200, 505, { x: 0, y: 1 }, w.players[0], false);
  assert.equal(floor.hp, 2);
});
test("Either broken hanging connector detaches the entire unsupported neck after a warning", () => {
  for (const index of [0, 1]) {
    const w = world("s-hanging"), neck = w.solids.filter(s => s.kind === "connector");
    assert.equal(neck.length, 2);
    D.breakSolid(w, neck[index], 10, "pick");
    frames(w, .2);
    const rock = w.objects.find(o => o.type === "hanging");
    assert.ok(rock.warning > 0 && !rock.falling);
    assert.ok(neck.every(s => s.hp <= 0));
    frames(w, 2);
    assert.ok(rock.landed); assert.equal(w.stats.hangingSolved, 1);
  }
});
test("Solid rock occludes rays and movement", () => {
  const w = world("w-charge"), p = w.players[0];
  frames(w, 6, [{ right: true }, {}]);
  assert.ok(p.x <= 630 - p.r + .01);
  const ray = D.ray(w, { x: 570, y: 500 }, { x: 900, y: 500 }, 330, true, true);
  assert.equal(ray.kind, "solid");
});
test("Hidden objects remain unavailable before their foreground rock is removed", () => {
  const w = world("t-scanner"), ore = w.objects.find(o => o.type === "ore" && o.hidden);
  assert.equal(w.visible(ore), false);
  w.scanUntil = w.time + 5;
  assert.equal(w.visible(ore), false);
  for (const solid of w.solids) if (solid.kind === "rock") D.breakSolid(w, solid, 10, "pick");
  assert.equal(w.visible(ore), true);
});
test("One insect cannot satisfy two distinct net captures", () => {
  const w = world("w-net"), e = w.enemies[0];
  D.catchNet(w, e, { id: 900 }); D.catchNet(w, e, { id: 901 });
  assert.equal(w.stats.netCatches, 1);
});
test("Deployed objects and event feedback have finite bounds", () => {
  const w = world("t-glow");
  for (let i = 0; i < 80; i++) {
    w.addObject("glow", 400, 510, { ttl: 50 });
    w.note("bounded note"); w.effect("dust", 400, 500, "#aaa");
  }
  frames(w, .1);
  assert.ok(w.objects.filter(o => o.alive && o.type === "glow").length <= 24);
  for (let i = 0; i < 300; i++) w.note("bounded log");
  assert.equal(w.events.length, 180); assert.ok(w.effects.length <= 170);
});
test("Foam seal expires and original finite nest spawning resumes", () => {
  const w = world("w-foam"), nest = w.objects.find(o => o.type === "nest");
  nest.sealed = 1.5; const before = nest.spawned;
  frames(w, 1); assert.equal(nest.spawned, before);
  frames(w, 18); assert.equal(nest.sealed, 0); assert.equal(nest.spawned, nest.quota);
});
test("Support delays collapse; expiration still permits falling rubble", () => {
  const w = world("t-support"), p = w.players[0];
  walk(w, 660);
  w.selected = w.scene.kit.indexOf("support"); D.useItem(w, p, "support", 1 / 60);
  const c = w.objects.find(o => o.type === "collapse");
  frames(w, 5); assert.equal(c.stage, "warning");
  frames(w, 23); assert.equal(c.stage, "fallen");
  assert.ok(w.solids.some(s => s.kind === "rubble"));
});
test("Ladder holds position after releasing climb and can jump off", () => {
  const w = world("t-ladder"), p = w.players[0];
  const ladder = w.addObject("ladder", p.x, p.y, { top: 240 });
  frames(w, 1, [{ up: true }, {}]); const height = p.y;
  frames(w, 1); assert.ok(Math.abs(p.y - height) < 1);
  frames(w, .4, [{ jump: true, right: true }, {}]);
  assert.ok(p.x > ladder.x + 30);
});
test("A raised winch crate holds height when E is released", () => {
  const w = world("t-winch"), p = w.players[0];
  walk(w, 540);
  D.useItem(w, p, "winch", 1 / 60);
  frames(w, 1, [{ interact: true }, {}]);
  const crate = w.objects.find(o => o.type === "crate"), height = crate.y;
  frames(w, 2); assert.equal(crate.y, height); assert.ok(height < crate.startY);
});
test("Two operators attach, carry and deliver an intact heavy mineral", () => {
  const w = world("s-heavy"); w.coop = true;
  walk(w, 738, true);
  const heavy = w.objects.find(o => o.type === "heavy");
  frames(w, 1 / 60, [{ interact: true, interactPressed: true }, {}]);
  assert.equal(heavy.attached, true);
  walk(w, 135);
  frames(w, 1 / 60, [{ interact: true, interactPressed: true }, {}]);
  assert.equal(w.stats.heavyDelivered, 1); assert.ok(w.stats.delivered >= 8);
});
test("First medic scene rescues its actual downed operator with held input", () => {
  const w = world("t-medkit");
  walk(w, 420);
  frames(w, 2.4, [{ fire: true }, {}]);
  assert.equal(w.players[1].down, false); assert.equal(w.stats.revives, 1);
});
test("Expired effects, thrown items, pause and isolated reset", () => {
  const w = world("t-glow"), second = world("t-glow");
  w.count("demoCount"); assert.equal(second.stats.demoCount, undefined);
  w.paused = true; frames(w, 5); assert.equal(w.time, 0);
  w.paused = false; w.effect("ring", 400, 300, "#aaa", { life: .2 });
  frames(w, 2); assert.equal(w.effects.length, 0);
});
const report = { type: "mechanism-boundaries-and-source-checks", count: results.length,
  passed: results.filter(r => r.passed).length, results };
if (process.argv[2]) {
  const output = path.resolve(process.argv[2]);
  await mkdir(path.dirname(output), { recursive: true }); await writeFile(output, JSON.stringify(report, null, 2) + "\n");
}
console.log(JSON.stringify(report, null, 2));
if (report.passed !== report.count) process.exitCode = 1;
