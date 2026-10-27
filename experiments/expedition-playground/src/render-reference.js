(function (D) {
  "use strict";
  let front;
  function tiled(ctx, id, top, rate, scale, crop) {
    const img = D.images[id], width = img.width * scale;
    const x0 = -((D.viewCamera.x * rate) % width + width) % width;
    const height = (crop || img.height) * scale;
    const y = Math.round(top - D.viewCamera.y * rate * .35);
    for (let x = x0; x < 960 / (D.viewZoom || 1); x += width) {
      ctx.drawImage(img, 0, 0, img.width, crop || img.height, Math.round(x), y, width, height);
    }
  }
  D.drawReferenceBackdrop = function (ctx, w) {
    ctx.save(); ctx.translate(w.camera.x, w.camera.y); D.viewCamera = w.camera; D.viewZoom = w.zoom || 1;
    const width = 960 / D.viewZoom, height = 540 / D.viewZoom;
    ctx.drawImage(D.images.sky, 0, 0, width, height);
    ctx.fillStyle = "#0a17252a"; ctx.fillRect(0, 0, width, height);
    ctx.save(); ctx.globalCompositeOperation = "screen"; ctx.globalAlpha = .65;
    tiled(ctx, "stars", -128, .018, 2); ctx.restore();
    ctx.fillStyle = "#d7d4b0"; ctx.beginPath();
    ctx.arc(487 - w.camera.x * .012, 55 - w.camera.y * .025, 14, 0, Math.PI * 2); ctx.fill();
    tiled(ctx, "mountain", 106, .07, 2);
    tiled(ctx, "farHills", 152, .16, 2);
    tiled(ctx, "forestClose", 185, .29, 1, 64);
    tiled(ctx, "forestNew", 217, .45, 1, 64);
    ctx.restore();
    ctx.drawImage(D.images.terrainRear, 0, 0, 2304, 760);
    ctx.fillStyle = "#0d131c72";
    ctx.save();
    ctx.beginPath(); ctx.moveTo(0, 760);
    for (let x = 0; x <= 2304; x += 8) ctx.lineTo(x, D.referenceSurface(x));
    ctx.lineTo(2304, 760); ctx.closePath(); ctx.clip(); ctx.fillRect(0, 0, 2304, 760);
    ctx.restore();
    for (let i = 0; i < 17; i++) {
      const x = 55 + i * 59;
      D.drawNative(ctx, i % 5 ? "tree" + i % 3 : "leaf" + i % 3, x, D.referenceSurface(x) + 2, 2);
    }
  };
  D.drawReferenceFront = function (ctx, w) {
    if (!front) {
      front = document.createElement("canvas"); front.width = 2304; front.height = 760;
      const c = front.getContext("2d"); c.imageSmoothingEnabled = false;
      c.drawImage(D.images.terrainFront, 0, 0, 2304, 760);
      for (let x = 1044; x < 1124; x += 8) {
        const roof = D.referenceRoof(x), floor = D.referenceFloor(x);
        c.clearRect(x, roof, 8, Math.max(0, floor - roof));
      }
    }
    ctx.drawImage(front, 0, 0);
    const grass = D.images.grass;
    for (let x = 20; x < 1030; x += 2) {
      const sourceX = Math.floor(x / 2) % grass.width;
      ctx.drawImage(grass, sourceX, 0, 1, 6, x, D.referenceSurface(x) - 2, 2, 12);
    }
    for (const x of [75, 247, 550, 860, 1002, 1275, 1505, 1805]) {
      D.drawNative(ctx, "stone" + Math.floor(x / 80) % 4, x, D.referenceFloor(x) + 2, 2);
    }
  };
})(window.DN);
