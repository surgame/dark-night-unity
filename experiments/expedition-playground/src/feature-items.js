(function (D) {
  "use strict";
  D.featureTargets = ["vent", "magcore", "amber"];
  D.useFeatureItem = function (w, p, type) {
    if (!["cryo", "pulse", "brace"].includes(type)) return false;
    const origin = D.center(p), range = type === "cryo" ? 195 : type === "pulse" ? 300 : 110;
    const hit = D.ray(w, origin, p.aim, range, type !== "brace",
      type === "cryo" ? ["vent"] : type === "pulse" ? ["magcore"] : false);
    if (type === "brace") {
      if (hit.kind === "solid" && hit.target.kind === "thinbridge" && !hit.target.braced) {
        hit.target.braced = true; hit.target.decay = -1;
        w.count("bridgesBraced");
        w.effect("spark", hit.point.x, hit.point.y, "#d1cebc", { r: 15 });
        w.note("岩钉已加固这一片桥面；已经坍塌的空洞不能修回。", "equipment");
      } else w.note("靠近尚存薄桥，瞄准脚前桥片；同一片不重复加固。", "hint");
      return true;
    }
    w.effect(type === "cryo" ? "coldbeam" : "arc", origin.x, origin.y,
      type === "cryo" ? "#9ba29b" : "#e8bc67",
      { endX: hit.point.x, endY: hit.point.y, life: .25 });
    if (hit.kind === "object" && hit.target.type === "vent") {
      const vent = hit.target;
      vent.frozen = 6;
      if (!vent.everFrozen) { vent.everFrozen = true; w.count("ventsFrozen"); }
      w.note("热口冷凝：6秒安全窗口，到期重新呼吸。", "equipment");
    } else if (hit.kind === "object" && hit.target.type === "magcore") {
      hit.target.suppressed = 8;
      if (!hit.target.everSuppressed) { hit.target.everSuppressed = true; w.count("coresSuppressed"); }
      w.note("磁核压制8秒，铁屑停止聚集。", "equipment");
    } else if (hit.kind === "enemy") {
      if (type === "cryo") {
        hit.target.slow = 4; w.count("cryoSlows");
      } else {
        hit.target.stun = 1.8; hit.target.charge = 0; hit.target.dashing = 0;
        w.count("pulseInterrupts");
      }
    }
    return true;
  };
  D.featureMine = function (w, p, o, cause) {
    if (o.type !== "amber") return false;
    if (o.ruptured || o.ruptureTimer !== undefined) return true;
    if (o.pressure > 0) {
      o.ruptureTimer = 1.1; w.count("amberRuptures");
      w.note("压力囊开裂，热流即将喷出；立即离开右侧囊口！", "hazard");
    } else if (cause === "pick") {
      o.hp--;
      w.effect("chips", o.x, o.y, "#e8bc67", { r: 14 });
      if (o.hp <= 0) {
        o.alive = false; p.cargo += 6;
        p.featureCargo = p.featureCargo || {};
        p.featureCargo.amber = (p.featureCargo.amber || 0) + 6;
        w.count("amberPreserved"); w.count("collected", 6);
        w.note("取得完整琥珀6份；回入口E交付。", "mining");
      }
    } else w.note("已泄压的完整琥珀用矿镐采集，枪击不会自动收取。", "hint");
    return true;
  };
  D.featureInteract = function (w, p) {
    if (!w.feature) return false;
    const valve = w.near(p, "pressureValve", 75);
    if (!valve) return false;
    const amber = w.objects.find(function (o) { return o.type === "amber" && o.alive; });
    if (!amber || amber.ruptured || amber.ruptureTimer !== undefined) {
      w.note("阀门已无可泄压囊体。", "hint"); return true;
    }
    if (amber.pressure > 0) {
      amber.pressure--; w.count("pressureReleases");
      w.effect("dust", valve.x, valve.y - 20, "#9ba29b", { r: 22 });
      w.note(amber.pressure ? "泄压一次，剩余压力：" + amber.pressure + "。再次按E继续。" :
        "压力已释放，前往右侧用矿镐完整采集。", "equipment");
    } else w.note("已完全泄压，前往右侧用矿镐采集琥珀。", "hint");
    return true;
  };
  D.featureDeliver = function (w, p) {
    if (!w.feature || !p.featureCargo) return;
    const keys = { vents: "ventDelivery", fracture: "bridgeDelivery", magnet: "magnetDelivery",
      photoroot: "rootDelivery", amber: "amberDelivery", echo: "echoDelivery", faultline: "faultDelivery" };
    Object.keys(p.featureCargo).forEach(function (tag) {
      if (keys[tag]) w.count(keys[tag], p.featureCargo[tag]);
    });
    p.featureCargo = {};
  };
  D.featureNoise = function (w, p, amount) {
    if (!w.feature || w.feature.type !== "echo" || Math.abs(p.x - 870) > 550) return;
    w.feature.noise = Math.min(100, w.feature.noise + amount);
    w.feature.noiseFlash = .25;
  };
})(window.DN);
