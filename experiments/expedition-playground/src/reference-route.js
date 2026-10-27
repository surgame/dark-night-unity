(function (D) {
  "use strict";
  D.register("mix", [{
    id: "m-reference", name: "项目实景 · 素材对照", subtitle: "从坡地松林，亲自走进矿洞",
    description: "借用引用会话的坡地—低谷—洞口—地下样板轮廓，以及工程角色、树石、森林、草皮和岩壁材质。这里可自行移动和跳跃，地形轮廓参与碰撞。",
    kit: ["pick", "pistol", "glow", "flare"], layout: "reference",
    tags: ["项目原生素材", "同源轮廓碰撞", "新增实景对照"],
    objective: "经过低谷与洞口，到达地下路标，再原路返回入口。",
    goal: { key: "referenceReturned", count: 1 },
    hint: "一路向右，沿自然坡地下洞，看到“地下对照点”后向左原路返回。F可切手电。",
    boundary: "路线来自另一会话的独立素材试配，不是正式预制地图。轮廓、原生素材来源可追溯；H5碰撞与光照不代表Unity验收。"
  }]);
  D.interpolate = function (points, x) {
    for (let i = 1; i < points.length; i++) {
      if (x <= points[i][0]) {
        const a = points[i - 1], b = points[i];
        return a[1] + (b[1] - a[1]) * D.clamp((x - a[0]) / (b[0] - a[0]), 0, 1);
      }
    }
    return points[points.length - 1][1];
  };
  D.referenceSurface = function (x) {
    return Math.round((D.interpolate(D.referenceProfiles.surfacePoints, x) +
      Math.sin(x * .056) * 1.1 + Math.sin(x * .19) * .6) / 2) * 2;
  };
  D.referenceFloor = function (x) {
    return x < 1050 || x > 2140 ? D.referenceSurface(x) :
      Math.round(D.interpolate(D.referenceProfiles.floorPoints, x));
  };
  D.referenceRoof = function (x) {
    const source = Math.round(D.interpolate(D.referenceProfiles.roofPoints, x));
    return x < 1124 ? Math.min(source, D.referenceFloor(x) - 30) : source;
  };
  D.buildReference = function (w) {
    w.reference = true; w.zoom = 1.5; w.width = 2304; w.height = 760;
    w.solids = []; w.objects = []; w.referenceColumns = [];
    w.addSolid(0, 0, 20, 760, "bedrock"); w.addSolid(2284, 0, 20, 760, "bedrock");
    for (let x = 20; x < 2284; x += 8) {
      const right = Math.min(2284, x + 8);
      const leftY = D.referenceFloor(x), rightY = D.referenceFloor(right);
      const column = {};
      column.floor = w.addSolid(x, Math.min(leftY, rightY), right - x, 760 - Math.min(leftY, rightY), "bedrock",
        { slope: [leftY, rightY], reference: true });
      if (x >= 1050 && x <= 2140) {
        const surface = D.referenceSurface(x);
        const roof = D.referenceRoof(x);
        if (roof > surface) column.roof = w.addSolid(x, surface, 8, roof - surface, "bedrock", { reference: true });
      }
      w.referenceColumns.push(column);
    }
    w.players.forEach(function (p, i) {
      p.x = 100 + i * 35; p.y = D.referenceFloor(p.x);
      p.h = 22; p.r = 6; p.aim = { x: p.x + 200, y: p.y - 12 };
    });
    w.camera = { x: 0, y: 0 };
    w.addObject("exit", 100, D.referenceFloor(100) - 25, { label: "入口 / 回程确认" });
    w.addObject("lamp", 110, D.referenceFloor(110) - 36, { light: 150, color: "#e9bc7d" });
    for (const item of [[350, "坡地松林"], [720, "低地山谷"], [1080, "矿洞入口"], [1980, "地下对照点"]]) {
      w.addObject("sign", item[0], D.referenceFloor(item[0]) - 25,
        { label: item[1], detail: item[1] + "：实际轮廓参与碰撞，天空始终被地下后壁遮住。" });
    }
    for (const x of [1240, 1530, 1820]) {
      const y = D.interpolate(D.referenceProfiles.roofPoints, x) + 16;
      w.addObject("torch", x, y, { light: 115 });
    }
    for (const x of [1700, 1900, 2020]) w.addObject("ore", x, D.referenceFloor(x) - 16, { value: 1 });
  };
  D.referenceSolidAt = function (w, x, y) {
    if (x < 20 || x >= 2284) return w.solids[x < 20 ? 0 : 1];
    const column = w.referenceColumns[Math.floor((x - 20) / 8)];
    if (!column) return undefined;
    if (y >= D.solidTop(column.floor, x) && y <= 760) return column.floor;
    if (column.roof && y >= column.roof.y && y <= column.roof.y + column.roof.h) return column.roof;
    return undefined;
  };
})(window.DN);
