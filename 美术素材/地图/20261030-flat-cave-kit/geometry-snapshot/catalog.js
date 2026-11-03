(function (root) {
  "use strict";
  const api = root.CaveWB = root.CaveWB || {};
  const levels = [-3, -1, 1, 3];
  const corners = [[0, 0], [1, 0], [1, 1], [0, 1]];
  const edgeCorners = [[0, 1], [1, 2], [3, 2], [0, 3]];
  const edgeNames = ["N", "E", "S", "W"];
  const catalog = [];

  function crossing(a, b, values) {
    const t = values[a] / (values[a] - values[b]);
    return [
      corners[a][0] + (corners[b][0] - corners[a][0]) * t,
      corners[a][1] + (corners[b][1] - corners[a][1]) * t
    ];
  }

  function makeTile(id) {
    const indices = [id & 3, (id >> 2) & 3, (id >> 4) & 3, (id >> 6) & 3];
    const values = indices.map(index => levels[index]);
    const mask = values.reduce((sum, value, i) => sum | (value > 0 ? 1 << i : 0), 0);
    const edges = edgeCorners.map(pair => indices[pair[0]] * 4 + indices[pair[1]]);
    const hits = [];
    edgeCorners.forEach((pair, edge) => {
      if ((values[pair[0]] > 0) !== (values[pair[1]] > 0)) {
        hits.push({ edge, point: crossing(pair[0], pair[1], values) });
      }
    });
    let segments = [];
    if (hits.length === 2) segments = [[hits[0], hits[1]]];
    if (hits.length === 4) {
      // The bilinear saddle decision belongs to the tile, so both render modes agree.
      const q = values[0] * values[2] - values[1] * values[3];
      const connectPositive = q === 0 ? mask === 5 : (mask === 5 ? q > 0 : q < 0);
      const pairA = mask === 5 ? !connectPositive : connectPositive;
      const pairs = pairA ? [[0, 3], [1, 2]] : [[0, 1], [2, 3]];
      segments = pairs.map(pair => pair.map(edge => hits.find(hit => hit.edge === edge)));
    }
    segments.forEach(segment => {
      const a = segment[0].point, b = segment[1].point;
      const mx = (a[0] + b[0]) / 2, my = (a[1] + b[1]) / 2;
      const length = Math.hypot(b[0] - a[0], b[1] - a[1]);
      const nx = (b[1] - a[1]) / length, ny = (a[0] - b[0]) / length;
      const nearest = corners.map((point, i) => ({
        i, dot: (point[0] - mx) * nx + (point[1] - my) * ny,
        distance: (point[0] - mx) ** 2 + (point[1] - my) ** 2
      })).filter(item => Math.abs(item.dot) > .00001).sort((left, right) => left.distance - right.distance)[0];
      const direction = (nearest.dot > 0) === (values[nearest.i] < 0) ? 1 : -1;
      segment.airNormal = { x: nx * direction, y: ny * direction };
    });
    return { id, indices, values, mask, edges, segments };
  }

  for (let id = 0; id < 256; id++) catalog.push(makeTile(id));

  function edgeKey(x, y, edge) {
    if (edge === 0) return `H:${x}:${y}`;
    if (edge === 1) return `V:${x + 1}:${y}`;
    if (edge === 2) return `H:${x}:${y + 1}`;
    return `V:${x}:${y}`;
  }

  function extractContours(map) {
    const endpoints = new Map();
    const lines = [];
    for (let y = 0; y < map.height; y++) {
      for (let x = 0; x < map.width; x++) {
        const tile = catalog[map.tiles[y * map.width + x]];
        tile.segments.forEach(segment => {
          const ends = segment.map(hit => ({
            key: edgeKey(x, y, hit.edge),
            x: x + hit.point[0], y: y + hit.point[1]
          }));
          const line = { ends, airNormal: segment.airNormal, used: false };
          lines.push(line);
          ends.forEach(end => {
            if (!endpoints.has(end.key)) endpoints.set(end.key, []);
            endpoints.get(end.key).push(line);
          });
        });
      }
    }
    const contours = [];
    lines.forEach(first => {
      if (first.used) return;
      const points = [first.ends[0]];
      const normals = [];
      let current = first;
      let end = first.ends[1];
      let closed = false;
      while (current && !current.used) {
        current.used = true;
        normals.push(current.airNormal);
        points.push(end);
        if (end.key === points[0].key) { closed = true; break; }
        const next = endpoints.get(end.key).find(line => !line.used);
        if (!next) break;
        current = next;
        end = next.ends[0].key === end.key ? next.ends[1] : next.ends[0];
      }
      contours.push({ points, normals, closed });
    });
    return {
      contours,
      segments: lines.length,
      nonManifold: Array.from(endpoints.values()).filter(list => list.length !== 2).length
    };
  }

  function verify(map) {
    let mismatches = 0;
    let comparisons = 0;
    let cornerConflicts = 0;
    const samples = new Int8Array((map.width + 1) * (map.height + 1)).fill(99);
    map.tiles.forEach((id, i) => {
      const x = i % map.width;
      const y = Math.floor(i / map.width);
      const tile = catalog[id];
      for (const [direction, offset] of [[1, 1], [2, map.width]]) {
        if ((direction === 1 && x + 1 === map.width) || (direction === 2 && y + 1 === map.height)) continue;
        comparisons++;
        if (tile.edges[direction] !== catalog[map.tiles[i + offset]].edges[(direction + 2) % 4]) mismatches++;
      }
      corners.forEach(([dx, dy], c) => {
        const at = (y + dy) * (map.width + 1) + x + dx;
        if (samples[at] !== 99 && samples[at] !== tile.values[c]) cornerConflicts++;
        samples[at] = tile.values[c];
      });
    });
    return { comparisons, mismatches, cornerConflicts, samples };
  }

  Object.assign(api, { levels, catalog, edgeNames, extractContours, verify });
  if (typeof module !== "undefined") module.exports = api;
})(typeof globalThis !== "undefined" ? globalThis : this);
