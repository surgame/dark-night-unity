(function (D) {
  "use strict";
  D.drawBackground = function (ctx, w) {
    const layout = w.scene.layout === "nest" ? (w.scene.id === "w-foam" ? "nest-open" : "nest-covered") : w.scene.layout;
    ctx.drawImage(D.images["backdrop-" + layout], 0, 0, w.width, w.height);
    // Only previously occupied expedition rooms carry timber remains.
    if (["refuge", "cache", "traces", "miner"].includes(w.scene.layout)) {
      const center = w.scene.layout === "refuge" ? 730 : 640;
      ctx.fillStyle = "#261d14";
      ctx.fillRect(center - 92, 350, 6, 170); ctx.fillRect(center + 86, 350, 6, 170);
      ctx.fillRect(center - 96, 345, 192, 8);
      ctx.fillStyle = "#463526";
      ctx.fillRect(center - 92, 352, 2, 166); ctx.fillRect(center + 86, 352, 2, 166);
      ctx.fillRect(center - 94, 346, 188, 2);
    }
    for (let i = 0; i < 9; i++) {
      const x = 355 + i * 113;
      if (w.solidAt(x, 521) && !w.solidAt(x, 518)) D.drawNative(ctx, "rubble" + i % 4, x, 521, 1, false, .7);
    }
  };
  D.label = function (ctx, text, x, y, color, size) {
    ctx.font = (size || 12) + 'px "Microsoft YaHei", sans-serif';
    ctx.textAlign = "center"; ctx.textBaseline = "middle";
    const width = ctx.measureText(text).width + 14;
    ctx.fillStyle = "rgba(12,22,22,.82)"; ctx.fillRect(x - width / 2, y - 10, width, 20);
    ctx.fillStyle = color || "#cfdbc9"; ctx.fillText(text, x, y + 1);
  };
  D.drawScan = function (ctx, w) {
    if (!w.scanUntil || w.time > w.scanUntil) return;
    const origin = w.scanOrigin;
    ctx.save(); ctx.globalAlpha = Math.min(.8, (w.scanUntil - w.time) * .5);
    w.objects.forEach(function (o) {
      if (!o.alive || !o.hidden || D.distance(origin, o) > 275) return;
      ctx.strokeStyle = "#86d4ca"; ctx.lineWidth = 2; ctx.setLineDash([4, 6]);
      ctx.strokeRect(o.x - 13, o.y - 15, 26, 26);
      ctx.fillStyle = "#9bdfd1"; ctx.fillRect(o.x - 3, o.y - 3, 6, 6);
    });
    ctx.restore();
  };
})(window.DN);
