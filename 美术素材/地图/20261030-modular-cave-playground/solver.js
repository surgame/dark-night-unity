(function (root) {
  "use strict";
  const api = root.MacroCave = root.MacroCave || {};
  const width = 623;

  function random(seed) {
    let value = seed >>> 0;
    return function () {
      value += 0x6D2B79F5;
      let n = value;
      n = Math.imul(n ^ (n >>> 15), n | 1);
      n ^= n + Math.imul(n ^ (n >>> 7), n | 61);
      return ((n ^ (n >>> 14)) >>> 0) / 4294967296;
    };
  }

  function catalog(profiles) {
    const entries = [];
    profiles.forEach((profile, panel) => {
      for (const mirror of [false, true]) {
        const flip = points => mirror
          ? points.map(p => [width - p[0], p[1]]).reverse()
          : points.map(p => p.slice());
        entries.push({
          ...profile, panel, mirror, key: profile.id + (mirror ? "-r" : ""),
          left: mirror ? profile.right : profile.left,
          right: mirror ? profile.left : profile.right,
          floor: flip(profile.floor), roof: flip(profile.roof)
        });
      }
    });
    return entries;
  }

  function compatible(a, b) {
    return a.right === b.left && a.key !== b.key;
  }

  function entropy(options, tiles) {
    let sum = 0, weightedLog = 0;
    for (const id of options) {
      const w = tiles[id].weight;
      sum += w;
      weightedLog += w * Math.log(w);
    }
    return Math.log(sum) - weightedLog / sum;
  }

  function solve(tiles, seed, count = 16) {
    const rng = random(seed), trace = [];
    const stats = { observations: 0, reductions: 0, removed: 0, backtracks: 0 };
    const first = tiles.map((_, i) => i).filter(i => tiles[i].left === "A");
    const last = tiles.map((_, i) => i).filter(i => tiles[i].right === "A");
    const domains = Array.from({length: count}, (_, i) =>
      (i === 0 ? first : i === count - 1 ? last : tiles.map((_, j) => j)).slice());

    function propagate(state, queue) {
      for (let cursor = 0; cursor < queue.length; cursor++) {
        const at = queue[cursor];
        for (const delta of [-1, 1]) {
          const next = at + delta;
          if (next < 0 || next >= count) continue;
          const before = state[next];
          const after = before.filter(id => state[at].some(source =>
            delta === 1 ? compatible(tiles[source], tiles[id]) : compatible(tiles[id], tiles[source])));
          if (!after.length) return false;
          if (after.length === before.length) continue;
          stats.reductions++;
          stats.removed += before.length - after.length;
          state[next] = after;
          trace.push({kind: "propagate", from: at, at: next, before: before.slice(), after: after.slice()});
          queue.push(next);
        }
      }
      return true;
    }

    function search(state) {
      let at = -1, minimum = Infinity;
      state.forEach((domain, i) => {
        if (domain.length < 2) return;
        const value = entropy(domain, tiles) + rng() * 0.00001;
        if (value < minimum) { minimum = value; at = i; }
      });
      if (at < 0) return state;
      const remaining = state[at].slice();
      while (remaining.length) {
        if (stats.backtracks > 128) throw Error("WFC exceeded the bounded backtrack budget.");
        let roll = rng() * remaining.reduce((sum, id) => sum + tiles[id].weight, 0);
        let chosen = remaining[remaining.length - 1];
        for (const id of remaining) {
          roll -= tiles[id].weight;
          if (roll <= 0) { chosen = id; break; }
        }
        const candidate = state.map(domain => domain.slice());
        candidate[at] = [chosen];
        stats.observations++;
        trace.push({kind: "observe", at, before: state[at].slice(), chosen});
        if (propagate(candidate, [at])) {
          const result = search(candidate);
          if (result) return result;
        }
        remaining.splice(remaining.indexOf(chosen), 1);
        stats.backtracks++;
        trace.push({kind: "backtrack", at, chosen});
      }
      return null;
    }

    if (!propagate(domains, domains.map((_, i) => i))) throw Error("Invalid initial module rules.");
    const solved = search(domains);
    if (!solved) throw Error("No compatible module strip was found.");
    const ids = solved.map(options => options[0]);
    for (let i = 1; i < ids.length; i++) {
      if (!compatible(tiles[ids[i - 1]], tiles[ids[i]])) throw Error("Invalid solved socket pair.");
    }
    return {seed, ids, trace, stats};
  }

  function sample(points, x) {
    const bounded = Math.max(0, Math.min(width, x));
    for (let i = 1; i < points.length; i++) {
      if (bounded <= points[i][0]) {
        const a = points[i - 1], b = points[i];
        return a[1] + (b[1] - a[1]) * (bounded - a[0]) / (b[0] - a[0]);
      }
    }
    return points[points.length - 1][1];
  }

  function place(solved, tiles) {
    let elevation = 285;
    return solved.ids.map((id, i) => {
      const tile = tiles[id], y = elevation - sample(tile.floor, 0);
      elevation = y + sample(tile.floor, width);
      return {id, x: i * width, y};
    });
  }

  Object.assign(api, {random, catalog, compatible, solve, sample, place, width});
  if (typeof module !== "undefined") module.exports = api;
})(globalThis);
