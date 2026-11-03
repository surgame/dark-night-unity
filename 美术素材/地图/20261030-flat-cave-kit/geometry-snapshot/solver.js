(function (root) {
  "use strict";
  const api = root.CaveWB = root.CaveWB || {};
  const directions = [[0, -1], [1, 0], [0, 1], [-1, 0]];

  function makeDomains(plan) {
    const domains = [];
    const stride = plan.width + 1;
    for (let y = 0; y < plan.height; y++) {
      for (let x = 0; x < plan.width; x++) {
        const points = [y * stride + x, y * stride + x + 1, (y + 1) * stride + x + 1, (y + 1) * stride + x];
        const options = [];
        const weights = new Float64Array(256);
        api.catalog.forEach(tile => {
          if (!tile.indices.every((value, i) => plan.allowed[points[i]] & (1 << value))) return;
          let error = 0;
          tile.values.forEach((value, i) => { error += (value - plan.target[points[i]]) ** 2; });
          const saddle = tile.mask === 5 || tile.mask === 10;
          weights[tile.id] = Math.max(1e-8, Math.exp(-error * .53) * (saddle ? .18 : 1));
          options.push(tile.id);
        });
        domains.push({ options, weights, entropy: 0, queued: false });
      }
    }
    return domains;
  }

  function entropy(domain) {
    if (domain.options.length < 2) return 0;
    let sum = 0;
    let logSum = 0;
    domain.options.forEach(id => {
      const weight = domain.weights[id];
      sum += weight;
      logSum += weight * Math.log(weight);
    });
    return Math.log(sum) - logSum / sum;
  }

  function propagate(domains, plan, queue, stats) {
    let cursor = 0;
    while (cursor < queue.length) {
      const at = queue[cursor++];
      const source = domains[at];
      source.queued = false;
      const x = at % plan.width;
      const y = Math.floor(at / plan.width);
      for (let direction = 0; direction < 4; direction++) {
        const nx = x + directions[direction][0];
        const ny = y + directions[direction][1];
        if (nx < 0 || ny < 0 || nx >= plan.width || ny >= plan.height) continue;
        const neighborAt = ny * plan.width + nx;
        const neighbor = domains[neighborAt];
        let allowedEdges = 0;
        source.options.forEach(id => { allowedEdges |= 1 << api.catalog[id].edges[direction]; });
        const opposite = (direction + 2) % 4;
        const next = neighbor.options.filter(id => allowedEdges & (1 << api.catalog[id].edges[opposite]));
        stats.propagations++;
        if (!next.length) return false;
        if (next.length === neighbor.options.length) continue;
        stats.removed += neighbor.options.length - next.length;
        neighbor.options = next;
        neighbor.entropy = entropy(neighbor);
        if (!neighbor.queued) { neighbor.queued = true; queue.push(neighborAt); }
      }
    }
    return true;
  }

  async function solve(plan, options = {}) {
    const started = performance.now();
    const pause = options.pause || (() => Promise.resolve());
    const stats = { observations: 0, propagations: 0, removed: 0, attempts: 0 };
    for (let attempt = 0; attempt < 4; attempt++) {
      stats.attempts = attempt + 1;
      const rng = api.random(plan.seed ^ Math.imul(attempt + 1, 0x9E3779B1));
      const domains = makeDomains(plan);
      domains.forEach(domain => { domain.entropy = entropy(domain); domain.queued = true; });
      if (!propagate(domains, plan, domains.map((_, i) => i), stats)) continue;
      let contradiction = false;
      while (true) {
        if (options.cancelled?.()) throw new Error("cancelled");
        let best = -1;
        let minimum = Infinity;
        domains.forEach((domain, i) => {
          if (domain.options.length < 2) return;
          const value = domain.entropy + ((i * .61803398875) % 1) * .000001;
          if (value < minimum) { minimum = value; best = i; }
        });
        if (best < 0) {
          const map = {
            width: plan.width, height: plan.height, seed: plan.seed, plan,
            tiles: Uint16Array.from(domains.map(domain => domain.options[0])),
            stats: { ...stats, elapsedMs: performance.now() - started }
          };
          map.check = api.verify(map);
          map.geometry = api.extractContours(map);
          if (map.check.mismatches || map.check.cornerConflicts || map.geometry.nonManifold) {
            throw new Error("The solved geometry violated a shared-boundary invariant.");
          }
          return map;
        }
        const domain = domains[best];
        let total = domain.options.reduce((sum, id) => sum + domain.weights[id], 0);
        let roll = rng() * total;
        let chosen = domain.options[domain.options.length - 1];
        for (const id of domain.options) {
          roll -= domain.weights[id];
          if (roll <= 0) { chosen = id; break; }
        }
        domain.options = [chosen];
        domain.entropy = 0;
        domain.queued = true;
        stats.observations++;
        if (!propagate(domains, plan, [best], stats)) { contradiction = true; break; }
        if (stats.observations % 48 === 0) {
          options.progress?.({
            resolved: domains.filter(item => item.options.length === 1).length,
            total: domains.length, observations: stats.observations
          });
          await pause();
        }
      }
      if (!contradiction) break;
    }
    throw new Error("WFC contradiction after four bounded attempts; no fallback map was installed.");
  }

  Object.assign(api, { solve, makeDomains });
  if (typeof module !== "undefined") module.exports = api;
})(typeof globalThis !== "undefined" ? globalThis : this);
