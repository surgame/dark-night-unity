(function (D) {
  "use strict";
  function cluster(w, x, y, n, tag) {
    for (let i = 0; i < n; i++) w.addObject("ore", x + i * 27, y - i % 2 * 8,
      { value: 1, featureLoot: tag, hp: 2, r: 17 });
  }
  function roof(w, x, width, depth) {
    for (let dx = 0; dx < width; dx += 8) {
      const edge = 146 + Math.round(Math.sin(dx / width * Math.PI) * depth / 2) * 2;
      w.addSolid(x + dx, 95, 8, edge - 95, "bedrock");
    }
  }
  function vent(w, x, phase) {
    w.addObject("vent", x, 512, { phase, r: 20, frozen: 0, mode: "cool" });
  }
  function bridge(w, x, width) {
    w.solids = w.solids.filter(function (s) { return !(s.y === 520 && s.w > 1000); });
    w.addSolid(0, 520, x, 152, "bedrock");
    w.addSolid(x + width, 520, w.width - x - width, 152, "bedrock");
    w.addSolid(x, 624, width, 48, "bedrock");
    w.addSolid(x, 552, 28, 72, "bedrock");
    w.addSolid(x + 28, 588, 30, 36, "bedrock");
    w.addSolid(x + width - 28, 552, 28, 72, "bedrock");
    w.addSolid(x + width - 58, 588, 30, 36, "bedrock");
    for (let dx = 0; dx < width; dx += 32) {
      w.addSolid(x + dx, 520, Math.min(32, width - dx), 14, "thinbridge",
        { hp: 2, maxHp: 2, wear: 0, braced: false, dynamic: true, decay: -1 });
    }
  }
  function root(w, x, top) {
    const door = w.addSolid(x - 11, top + 22, 22, 520 - top - 22, "rootgate",
      { dynamic: true, hp: 3, maxHp: 3 });
    roof(w, x - 64, 128, top - 149);
    w.addObject("photoroot", x, top + 3, { doorId: door.id, opening: 0, r: 17 });
  }
  function core(w, x) {
    w.addSolid(x - 34, 430, 68, 30, "bedrock");
    w.addObject("magcore", x, 477, { r: 23, suppressed: 0, mode: "off" });
  }
  D.buildFeatureScene = function (w) {
    if (!w.scene.feature) return false;
    const type = w.scene.layout;
    w.feature = { type, noise: 0, awake: false, noiseFlash: 0 };
    w.zoom = 1.35; w.viewY = 290;
    w.camera = { x: 270, y: 225 };
    roof(w, 430, 250, 60); roof(w, 1120, 300, 145);
    if (type === "vents") {
      [610, 766, 922].forEach(function (x, i) { vent(w, x, i * 2.3); });
      w.addSolid(704, 490, 30, 30, "bedrock");
      w.addSolid(859, 490, 30, 30, "bedrock");
      cluster(w, 1040, 495, 4, "vents");
    }
    if (type === "fracture") {
      bridge(w, 620, 192); roof(w, 680, 220, 80);
      cluster(w, 1000, 496, 4, "fracture");
    }
    if (type === "magnet") {
      core(w, 850);
      w.addSolid(590, 500, 55, 20, "bedrock");
      cluster(w, 1045, 493, 4, "magnet");
    }
    if (type === "photoroot") {
      root(w, 690, 370); root(w, 925, 350);
      w.addObject("fungus", 785, 492, { light: 90, value: 1 });
      cluster(w, 1100, 496, 4, "photoroot");
    }
    if (type === "amber") {
      roof(w, 760, 260, 180);
      w.addSolid(800, 505, 130, 15, "bedrock");
      w.addObject("pressureValve", 690, 494, { r: 18 });
      w.addObject("amber", 858, 479, { r: 25, hp: 3, pressure: 3, ruptured: false });
    }
    if (type === "echo") {
      roof(w, 670, 500, 130);
      w.addObject("echoPod", 870, 501, { r: 26 });
      for (let i = 0; i < 8; i++) w.addObject("echoShard", 590 + i * 65, 517, { r: 5 });
      cluster(w, 1140, 495, 4, "echo");
    }
    if (type === "faultline") {
      vent(w, 555, 0);
      bridge(w, 650, 192);
      root(w, 945, 365);
      core(w, 1130);
      cluster(w, 1240, 495, 4, "faultline");
    }
    w.addObject("sign", 480, 496, { label: "局部洞穴 →", detail: w.scene.hint });
    return true;
  };
})(window.DN);
