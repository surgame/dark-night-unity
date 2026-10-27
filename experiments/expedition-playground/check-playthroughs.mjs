import { readFile, mkdir, writeFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import path from "node:path";
import vm from "node:vm";
import assert from "node:assert/strict";
import { runtimeFiles } from "./source-files.mjs";

const root = path.dirname(fileURLToPath(import.meta.url));
const context = vm.createContext({ window: {}, console });
for (const file of runtimeFiles) vm.runInContext(await readFile(path.join(root, "src", file), "utf8"), context, { filename: file });
const D = context.window.DN;
const output = process.argv[2];
const results = [];
assert.equal(D.catalog.length, 48);
assert.equal(new Set(D.catalog.map(s => s.id)).size, 48);
assert.deepEqual(Array.from(D.categories, g => D.catalog.filter(s => s.category === g.id).length), [10, 10, 22, 6]);
function driver(id) {
  const scene = D.catalog.find(s => s.id === id);
  const w = new D.World(scene);
  const p = () => w.players[w.active];
  const run = (seconds, controls = {}) => {
    for (let i = 0; i < Math.ceil(seconds * 60); i++) {
      const input = typeof controls === "function" ? controls(i) : controls;
      const inputs = [{}, {}]; inputs[w.active] = input; D.step(w, inputs, 1 / 60);
      assert.ok(w.players.every(q => Number.isFinite(q.x) && Number.isFinite(q.y)), "finite player position");
    }
  };
  const select = type => {
    const i = scene.kit.indexOf(type); assert.ok(i >= 0, id + " missing " + type); w.selected = i;
  };
  const walk = (x, controls = {}, timeout = 14) => {
    for (let i = 0; i < timeout * 60 && Math.abs(p().x - x) > 4; i++) {
      const movement = { left: p().x > x, right: p().x < x };
      run(1 / 60, { ...movement, ...controls });
    }
    return Math.abs(p().x - x) < 7;
  };
  const use = (type, seconds, aim) => {
    select(type);
    run(seconds, () => ({ fire: true, aim: typeof aim === "function" ? aim() : aim || { x: p().x + 150, y: p().y - 20 } }));
  };
  const interact = (seconds = 1 / 60) => {
    run(1 / 60, { interact: true, interactPressed: true });
    if (seconds > 1 / 60) run(seconds - 1 / 60, { interact: true });
    run(1 / 60);
  };
  const object = type => w.objects.find(o => o.alive && o.type === type);
  const digWalk = (x, y = 500) => {
    select("pick"); return walk(x, { fire: true, aim: { x: x + 65, y } }, 25);
  };
  const mine = (o, seconds = 1.5) => {
    if (!o || !o.alive) return;
    walk(o.x - 60); use("pick", seconds, { x: o.x, y: o.y });
  };
  const deliver = () => { walk(135); interact(); };
  return { w, p, run, select, walk, use, interact, object, digWalk, mine, deliver };
}
const solvers = {
  "w-shotgun": d => {
    d.walk(470); d.use("shotgun", 16, () => {
      const e = d.w.enemies.find(e => e.alive); return e ? { x: e.x, y: e.y - 10 } : { x: 1000, y: 500 };
    });
  },
  "w-nail": d => { d.walk(850); d.use("nail", .2, { x: 1004, y: 510 }); d.run(.2); },
  "w-shock": d => {
    d.walk(570); d.select("shock"); d.run(18, () => {
      const e = d.w.enemies.filter(e => e.alive).sort((a, b) => Math.abs(a.x - d.p().x) - Math.abs(b.x - d.p().x))[0];
      if (!e) return {};
      return { left: e.x < d.p().x - 60, right: e.x > d.p().x + 60,
        fire: e.charge > 0 || e.dashing > 0, aim: { x: e.x, y: e.y - 10 } };
    });
  },
  "w-net": d => { d.walk(470); d.use("net", 12, () => ({ x: d.p().x + 90, y: 520 })); },
  "w-foam": d => {
    d.walk(600); d.use("foam", .1, { x: 790, y: 491 }); d.run(.8);
    const ore = d.object("ore"); d.mine(ore); d.run(.2);
  },
  "w-flare": d => { d.walk(530); d.use("flare", .1, { x: 850, y: 350 }); d.run(1); },
  "w-charge": d => { d.walk(565); d.use("charge", .1, { x: 660, y: 500 }); d.interact(); d.run(.3); },
  "w-lure": d => { d.walk(440); d.use("lure", .1, { x: 930, y: 450 }); d.run(2.5); },
  "t-glow": d => {
    d.walk(630); d.use("glow", .1, { x: 810, y: 620 }); d.run(2);
    d.walk(800); d.run(.5);
  },
  "t-scanner": d => {
    d.walk(530); d.use("scanner", .1, { x: 650, y: 480 }); d.digWalk(620, 482);
    d.use("pick", 6, { x: 646, y: 482 }); d.run(.3);
  },
  "t-ladder": d => {
    d.walk(775); d.use("ladder", .1, { x: 790, y: 200 });
    const ladder = d.object("ladder"); d.walk(ladder.x); d.run(1.9, { up: true });
    d.run(.6, { right: true, jump: true }); d.walk(865); d.run(.3);
  },
  "t-support": d => {
    d.walk(610); d.use("support", .1); d.mine(d.object("ore"), 1.8);
  },
  "t-winch": d => { d.walk(520); d.use("winch", .1); d.interact(3); },
  "t-shield": d => { d.walk(465); d.use("shield", .1); d.run(12); },
  "t-seismic": d => { d.walk(460); d.use("seismic", .1); d.walk(300); d.run(2); },
  "t-sample": d => { d.walk(565); d.use("sample", .1); d.deliver(); },
  "t-medkit": d => { d.walk(380); d.use("medkit", 2.5); },
  "s-hanging": d => {
    d.walk(745); d.use("ladder", .1, { x: 750, y: 160 }); d.walk(d.object("ladder").x);
    d.run(1.4, { up: true }); d.use("pick", 2, { x: 815, y: 306 });
  },
  "s-pillar": d => { d.walk(640); d.use("pick", 1.6, { x: 700, y: 455 }); d.walk(470); },
  "s-crystal": d => { d.digWalk(620, 482); for (const o of d.w.objects.filter(o => o.type === "crystal").slice(0, 3)) d.mine(o, 3); },
  "s-resonance": d => {
    d.walk(415); d.use("pick", .1, { x: 470, y: 490 }); d.digWalk(620, 482);
    d.use("pick", 6, { x: 646, y: 482 });
  },
  "s-boulder": d => { d.walk(650); d.use("nail", 1.7, { x: 815, y: 235 }); d.run(2); },
  "s-mycelium": d => { d.digWalk(700); d.mine(d.object("ore"), 2); },
  "s-garden": d => { for (const o of d.w.objects.filter(o => o.type === "fungus").slice(0, 3)) d.mine(o); d.deliver(); },
  "s-cocoons": d => {
    d.walk(515); d.interact(); d.walk(1020); d.mine(d.object("ore"), 2);
  },
  "s-mimic": d => { d.walk(570); d.use("scanner", .1, { x: 710, y: 490 }); d.mine(d.object("ore"), 2); },
  "s-vine": d => {
    d.walk(740); d.use("pick", 1.5, { x: 790, y: 486 });
  },
  "s-cache": d => { d.digWalk(665); d.use("pick", 1, { x: 665, y: 487 }); d.interact(); },
  "s-traces": d => {
    d.walk(430); d.interact(); d.digWalk(620, 482); d.use("pick", 6, { x: 646, y: 482 });
  },
  "s-heavy": d => { d.walk(710); d.use("pick", 2.4, { x: 770, y: 495 }); d.deliver(); },
  "s-refuge": d => { d.walk(690); d.interact(); d.mine(d.object("ore"), 2); },
  "s-nest": d => {
    d.digWalk(835, 493); d.walk(570);
    d.use("pistol", 20, () => {
      const e = d.w.enemies.find(e => e.alive);
      return e ? { x: e.x, y: e.y - 10 } : { x: 800, y: 400 };
    });
  },
  "s-collapse": d => {
    d.walk(610); d.walk(450); d.run(5);
    d.digWalk(810); d.run(.3);
  },
  "m-miner": d => {
    d.walk(640); d.use("support", .1); d.use("pick", 1.5, { x: 700, y: 455 });
    for (const o of d.w.objects.filter(o => o.type === "ore").slice(0, 2)) d.mine(o, 2);
    d.deliver();
  },
  "m-hunt": d => {
    d.digWalk(835, 493); d.walk(570); d.use("net", 8, { x: 710, y: 518 });
    for (const o of d.w.objects.filter(o => o.type === "ore").slice(0, 3)) d.mine(o, 2);
    d.deliver();
  },
  "m-explore": d => {
    d.walk(530); d.use("scanner", .1, { x: 650, y: 482 }); d.digWalk(770, 482);
    for (const o of d.w.objects.filter(o => o.type === "crystal").slice(0, 4)) d.mine(o, 3);
    d.walk(775); d.use("ladder", .1, { x: 790, y: 180 }); d.walk(d.object("ladder").x);
    d.run(1.9, { up: true }); d.run(.6, { right: true, jump: true }); d.walk(865);
    d.walk(775); d.run(1.5); d.deliver();
  },
  "m-cargo": d => { d.walk(380); d.use("medkit", 2.5); d.walk(520); d.use("winch", .1); d.interact(3); d.deliver(); },
  "m-reference": d => {
    assert.ok(d.walk(2000, {}, 24), "walk through source slope and cave");
    assert.ok(d.walk(135, {}, 24), "return along source slope and cave");
  }
};
for (const scene of D.catalog.filter(s => !s.feature)) {
  const d = driver(scene.id);
  const start = performance.now();
  try {
    solvers[scene.id](d);
    D.updateGoals(d.w);
    assert.ok(d.w.finished, "objective not completed");
    results.push({ id: scene.id, name: scene.name, passed: true, simulatedSeconds: +d.w.time.toFixed(2),
      milliseconds: Math.round(performance.now() - start), stats: { ...d.w.stats } });
  } catch (error) {
    results.push({ id: scene.id, name: scene.name, passed: false, reason: error.message, state: d.w.snapshot() });
  }
}
const report = { type: "headless-input-playthroughs", scope: "original-38-scenes", excludedNewFeatures: 10, directStateSetup: false,
  note: "通过同一模拟的逐帧移动、瞄准、使用、交互完成目标；不直接注入目标计数。不替代真实浏览器手感或人工认可。",
  count: results.length, passed: results.filter(r => r.passed).length, results };
if (output) {
  await mkdir(path.dirname(path.resolve(output)), { recursive: true });
  await writeFile(output, JSON.stringify(report, null, 2) + "\n", "utf8");
}
console.log(JSON.stringify({ count: report.count, passed: report.passed,
  failures: results.filter(r => !r.passed).map(r => ({ id: r.id, reason: r.reason, stats: r.state.stats,
    p: r.state.player[0] })) }, null, 2));
if (report.passed !== report.count) process.exitCode = 1;
