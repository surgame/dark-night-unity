const assert = require("node:assert/strict");
const crypto = require("node:crypto");
const fs = require("node:fs");
const path = require("node:path");
require("./catalog.js");
require("./plan.js");
const api = require("./solver.js");

function hash(map) {
  return crypto.createHash("sha256").update(Buffer.from(map.tiles.buffer)).digest("hex");
}

(async () => {
  assert.equal(api.catalog.length, 256);
  assert.equal(new Set(api.catalog.map(tile => tile.mask)).size, 16);
  const crossings = new Set();
  api.catalog.forEach(tile => tile.segments.flat().forEach(hit => {
    const point = hit.point;
    crossings.add(point[0] === 0 || point[0] === 1 ? point[1] : point[0]);
  }));
  assert.deepEqual(Array.from(crossings).sort(), [.25, .5, .75]);
  api.catalog.filter(tile => tile.mask === 12).forEach(tile => {
    assert(tile.segments.every(segment => segment.airNormal.y < 0), "A floor must face the air above it.");
  });
  api.catalog.filter(tile => tile.mask === 3).forEach(tile => {
    assert(tile.segments.every(segment => segment.airNormal.y > 0), "A ceiling must face the air below it.");
  });
  const results = [];
  let firstHash;
  for (const seed of [20261029, 20268948, 1]) {
    const map = await api.solve(api.makePlan(seed));
    assert.equal(map.check.mismatches, 0);
    assert.equal(map.check.cornerConflicts, 0);
    assert.equal(map.geometry.nonManifold, 0);
    assert(map.geometry.contours.every(contour => contour.closed));
    assert(map.stats.observations > 0 && map.stats.removed > 0);
    assert.equal(map.tiles.length, 3840);
    const digest = hash(map);
    if (!firstHash) firstHash = digest;
    results.push({
      seed, hash: digest, observations: map.stats.observations,
      propagations: map.stats.propagations, edges: map.check.comparisons,
      conflicts: 0, contours: map.geometry.contours.length,
      segments: map.geometry.segments, elapsedMs: map.stats.elapsedMs
    });
  }
  const repeated = await api.solve(api.makePlan(20261029));
  assert.equal(hash(repeated), firstHash);
  assert.equal(new Set(results.map(result => result.hash)).size, 3);
  const freePlan = {
    width: 2, height: 2, seed: 5,
    allowed: new Uint8Array(9).fill(15), target: new Float32Array(9)
  };
  const domains = api.makeDomains(freePlan);
  assert(domains.every(domain => domain.options.length === 256));
  // Only propagation is checked here; an unconstrained outer border is intentionally open.
  const impossible = { ...freePlan, allowed: new Uint8Array(9) };
  await assert.rejects(() => api.solve(impossible), /four bounded attempts/);
  const output = {
    catalog: 256, topologies: 16, crossingFractions: Array.from(crossings).sort(),
    results, deterministic: true, contradictionReported: true,
    scope: "Independent JavaScript geometry and WFC checks; no Unity or actor traversal."
  };
  if (process.argv[2]) {
    fs.mkdirSync(process.argv[2], { recursive: true });
    fs.writeFileSync(path.join(process.argv[2], `algorithm-check-${Date.now()}.json`), JSON.stringify(output, null, 2));
  }
  console.log(JSON.stringify(output));
})().catch(error => { console.error(error); process.exitCode = 1; });
