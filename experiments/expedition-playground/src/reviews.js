(function (D) {
  "use strict";
  const key = "darknights-independent-expedition-lab-v1";
  D.reviews = {}; D.runs = {}; D.storageAvailable = true;
  try {
    const saved = JSON.parse(localStorage.getItem(key) || "{}");
    D.reviews = saved.reviews || {}; D.runs = saved.runs || {};
  } catch (error) { D.storageAvailable = false; }
  D.persist = function () {
    try {
      localStorage.setItem(key, JSON.stringify({ reviews: D.reviews, runs: D.runs }));
      D.storageAvailable = true;
    } catch (error) { D.storageAvailable = false; }
  };
  D.rememberRun = function (w) {
    const old = D.runs[w.scene.id];
    const run = w.snapshot();
    run.identity = D.buildIdentity || D.version;
    run.everComplete = run.complete || !!(old && old.everComplete && old.identity === run.identity);
    D.runs[w.scene.id] = run;
    D.persist();
  };
  D.reviewScene = function (w, decision, notes) {
    D.reviews[w.scene.id] = { decision: decision, notes: notes,
      identity: D.buildIdentity || D.version, at: new Date().toISOString(), importApproved: false };
    D.persist();
  };
  D.exportReport = function (w) {
    D.rememberRun(w);
    const report = {
      schema: "darknights-independent-lab-review-v1",
      build: D.buildIdentity || D.version, exportedAt: new Date().toISOString(),
      scope: "独立H5候选评审。人工意见不等于Unity导入授权；所有正式功能、规则、网络、存档和美术仍须另行接入核对。",
      scenarios: D.catalog.map(function (scene) {
        return { id: scene.id, name: scene.name, category: scene.category,
          objective: scene.objective, boundary: scene.boundary,
          review: D.reviews[scene.id] || { decision: "pending", notes: "", importApproved: false },
          lastRun: D.runs[scene.id] || null };
      })
    };
    const blob = new Blob([JSON.stringify(report, null, 2)], { type: "application/json;charset=utf-8" });
    const url = URL.createObjectURL(blob), anchor = document.createElement("a");
    const date = new Intl.DateTimeFormat("sv-SE", { timeZone: "Asia/Shanghai", year: "numeric",
      month: "2-digit", day: "2-digit" }).format(new Date());
    anchor.href = url; anchor.download = "dark-nights-lab-review-" + date + ".json";
    document.body.appendChild(anchor); anchor.click(); anchor.remove();
    setTimeout(function () { URL.revokeObjectURL(url); }, 3000);
    return report;
  };
})(window.DN);
