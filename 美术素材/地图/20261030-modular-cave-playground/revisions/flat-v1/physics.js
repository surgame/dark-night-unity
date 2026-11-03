(function (root) {
  "use strict";
  const api = root.MacroCave;
  const height = 52;

  function surface(placements, tiles, x, kind = "floor") {
    const at = Math.max(0, Math.min(placements.length - 1, Math.floor(x / api.width)));
    const placed = placements[at], tile = tiles[placed.id], local = x - placed.x;
    const floor = api.sample(tile.floor, local);
    if (kind === "floor") return placed.y + floor;
    const roof = api.sample(tile.roof, local);
    const distance = Math.min(local, api.width - local);
    const port = local < api.width / 2 ? tile.left : tile.right;
    const clearance = port === "B" ? 72 : 115;
    const blend = Math.max(0, 1 - distance / 40);
    return placed.y + Math.max(roof, (floor - clearance) * blend + roof * (1 - blend));
  }

  function character(placements, tiles, x = 160) {
    const bounded = Math.max(24, Math.min(placements.length * api.width - 24, x));
    return {x: bounded, y: surface(placements, tiles, bounded), vy: 0,
      face: 1, grounded: true, distance: 0, jumped: 0};
  }

  function jump(player) {
    if (!player.grounded) return false;
    player.vy = -280;
    player.grounded = false;
    player.jumped++;
    return true;
  }

  function step(player, axis, dt, placements, tiles) {
    const previousX = player.x, previousY = player.y;
    if (axis) player.face = Math.sign(axis);
    const nextX = Math.max(24, Math.min(placements.length * api.width - 24,
      player.x + axis * 178 * dt));
    const floor = surface(placements, tiles, nextX);
    const roof = Math.max(...[-13, 0, 13].map(dx => surface(placements, tiles, nextX + dx, "roof")));
    if (player.grounded) {
      player.y = floor;
      player.vy = 0;
    } else {
      player.vy += 820 * dt;
      player.y += player.vy * dt;
      if (player.y - height < roof) {
        player.y = roof + height;
        player.vy = Math.max(0, player.vy);
      }
      if (player.y >= floor && player.vy >= 0) {
        player.y = floor;
        player.vy = 0;
        player.grounded = true;
      }
    }
    player.x = nextX;
    player.distance += Math.abs(nextX - previousX);
    return Math.abs(previousX - player.x) > 0.0001 || Math.abs(previousY - player.y) > 0.0001;
  }

  Object.assign(api, {surface, character, jump, step, characterHeight: height});
})(globalThis);
