(function (D) {
  "use strict";
  function ores(w, x, y, count, kind, extra) {
    for (let i = 0; i < count; i++) {
      w.addObject(kind || "ore", x + i % 3 * 32, y - Math.floor(i / 3) * 28,
        Object.assign({ value: 1, color: kind === "crystal" ? "#8fd0d4" : "#d5af61" }, extra));
    }
  }
  function hidden(w, crystal) {
    w.wall(590, 328, 5, 6, "rock");
    ores(w, 646, 482, 6, crystal ? "crystal" : "ore", { hidden: true });
    w.addObject("trace", 478, 498, { label: "旧钻头", points: [655, 470] });
    w.addObject("ore", 587, 478, { value: 1, color: "#d8b16e" });
  }
  function high(w) {
    w.addSolid(820, 300, 320, 220, "bedrock");
    ores(w, 884, 279, 5);
    w.addObject("sign", 740, 481, { label: "↑ 高处矿台", detail: "向上搭梯，顶端向右跳。" });
    w.addObject("mark", 827, 340, { label: "照明弹目标" });
  }
  function collapse(w, pillar) {
    w.addSolid(582, 72, 236, 198, "bedrock");
    w.addObject("collapse", 700, 270, { r: 100, width: 220, stage: "idle",
      timer: 0, pillar: !!pillar, label: "不稳定顶板" });
    ores(w, pillar ? 900 : 665, 493, 6);
    if (pillar) {
      const column = w.addObject("pillar", 700, 455, { r: 27, hp: 4, value: 4 });
      w.objects.find(function (o) { return o.type === "collapse"; }).pillarId = column.id;
    }
  }
  function nest(w, shell) {
    w.addObject("nest", 790, 491, { r: 29, hidden: !!shell,
      stage: shell ? "hidden" : "warning", timer: shell ? 0 : 2.5, quota: 2, spawned: 0, sealed: 0 });
    if (shell) w.wall(758, 456, 2, 2, "shell", { hp: 2, maxHp: 2 });
    ores(w, 685, 492, 4);
    w.addObject("sign", 550, 491, { label: "薄壳与侧巢", detail: "开壳后先退开，前兆结束才出虫。" });
  }
  function pit(w, winch) {
    w.solids = w.solids.filter(function (s) { return s.y !== 520; });
    w.addSolid(0, 520, 640, 152, "bedrock");
    w.addSolid(640, 620, 340, 52, "bedrock");
    w.addSolid(980, 520, 556, 152, "bedrock");
    w.addSolid(640, 556, 64, 64, "bedrock");
    w.addSolid(704, 588, 64, 32, "bedrock");
    w.addSolid(916, 556, 64, 64, "bedrock");
    w.addSolid(852, 588, 64, 32, "bedrock");
    ores(w, 790, 594, 3);
    if (winch) {
      w.addObject("winchBase", 540, 496, { attached: false, label: "绞盘操作点" });
      w.addObject("crate", 815, 601, { startY: 601, targetY: 425, linked: false, raised: false });
    }
  }
  D.buildScene = function (w) {
    const layout = w.scene.layout;
    if (layout === "reference") { D.buildReference(w); return; }
    if (D.buildFeatureScene && D.buildFeatureScene(w)) return;
    w.addObject("sign", 290, 492, { label: "实验区 →", detail: w.scene.hint });
    if (["arena", "pin"].includes(layout)) {
      w.enemy(700, 520); w.enemy(900, 520); w.enemy(1080, 520);
      if (layout === "pin") {
        w.addSolid(1060, 340, 48, 180, "bedrock");
        w.enemies = []; w.enemy(1004, 520, { stationary: true });
      }
      ores(w, 770, 492, 4);
    }
    if (layout === "wall") { w.wall(630, 328, 4, 6); ores(w, 820, 492, 5); }
    if (["hidden", "crystal", "resonance", "traces"].includes(layout)) {
      hidden(w, layout === "crystal");
      if (layout === "resonance") w.addObject("resonance", 470, 490, { r: 23 });
    }
    if (layout === "high") high(w);
    if (["pit", "winch"].includes(layout)) pit(w, layout === "winch");
    if (["collapse", "pillar"].includes(layout)) collapse(w, layout === "pillar");
    if (layout === "nest") nest(w, w.scene.id !== "w-foam");
    if (layout === "sample") {
      for (let i = 0; i < 3; i++) w.addObject("sample", 620 + i * 75, 492, { value: 4, r: 20 });
    }
    if (layout === "rescue") { w.enemy(940, 520); ores(w, 700, 492, 3); }
    if (["hanging", "boulder"].includes(layout)) {
      for (let y = 72; y < 220; y += 4) {
        const half = Math.round((20 + Math.pow((220 - y) / 148, 1.6) * 68) / 2) * 2;
        w.addSolid(815 - half, y, half * 2, Math.min(4, 220 - y), "bedrock");
      }
      w.addSolid(799, 220, 32, 32, "connector", { hp: 2, maxHp: 2 });
      w.addSolid(799, 252, 32, 32, "connector", { hp: 2, maxHp: 2 });
      w.addObject("hanging", 815, 306, { r: 45, falling: false, landed: false,
        vy: 0, ore: layout === "hanging", originalY: 306 });
      if (layout === "boulder") w.enemy(815, 520, { stationary: true, hp: 100 });
      w.addObject("sign", 610, 488, { label: "↑ 连接岩", detail: "浅色连接岩承受上方重量。" });
    }
    if (layout === "mycelium") {
      w.wall(640, 360, 2, 5, "mycelium", { hp: 2, maxHp: 2 });
      ores(w, 770, 492, 5, "ore", { fromMycelium: true });
    }
    if (layout === "garden") {
      for (let i = 0; i < 5; i++) w.addObject("fungus", 510 + i * 115, 495, { value: 1, light: 130 });
    }
    if (layout === "cocoons") {
      w.addObject("cocoon", 560, 490, { active: false, checked: false });
      w.addObject("cocoon", 770, 490, { active: true, checked: false });
      w.addObject("cocoon", 1000, 490, { active: false, checked: false });
      ores(w, 1080, 492, 4);
    }
    if (layout === "mimic") {
      w.addObject("mimic", 710, 491, { asleep: true, r: 22, identified: false });
      ores(w, 570, 492, 3);
    }
    if (layout === "vine") {
      w.addObject("vine", 790, 310, { tipY: 486, r: 22, caught: 0, hp: 3 });
      w.enemy(1040, 520);
      ores(w, 1000, 490, 3);
    }
    if (layout === "cache") {
      w.wall(620, 424, 3, 3);
      w.addObject("cache", 665, 487, { hidden: true, damaged: false });
      w.addObject("cache", 1040, 487, { damaged: true });
      w.addObject("mark", 617, 480, { label: "露出的金属角" });
    }
    if (layout === "heavy") w.addObject("heavy", 770, 495, { r: 34, hp: 6, value: 8, attached: false });
    if (layout === "refuge") {
      w.addObject("lamp", 730, 400, { light: 310, color: "#83beaa" });
      w.addObject("sign", 730, 487, { label: "微光洞室", refuge: true, detail: "左：入口　右：旧矿道。本室没有伏击。" });
      ores(w, 900, 492, 4);
    }
    if (layout === "miner") { collapse(w, true); w.wall(1000, 456, 2, 2); ores(w, 1150, 490, 6); }
    if (layout === "hunt") {
      nest(w, true); w.addObject("vine", 1080, 300, { tipY: 486, r: 22, caught: 0, hp: 3 });
      w.addObject("cocoon", 1200, 490, { active: true, checked: false });
    }
    if (layout === "explore") { hidden(w, true); high(w); }
    if (layout === "cargo") { pit(w, true); w.enemy(1080, 520); }
    w.addObject("sign", 1390, 491, { label: "实验边界", detail: "可随时R重置本场景；不影响其他场景和评审。" });
  };
})(window.DN);
