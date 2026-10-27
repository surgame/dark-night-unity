(function (D) {
  "use strict";
  D.images = {};
  D.loadProjectAssets = function () {
    return Promise.all(Object.entries(D.assetRecords).map(function (entry) {
      return new Promise(function (resolve, reject) {
        const image = new Image();
        image.onload = function () { D.images[entry[0]] = image; resolve(); };
        image.onerror = function () { reject(new Error("素材解码失败：" + entry[0])); };
        image.src = entry[1].uri;
      });
    }));
  };
  D.drawNative = function (ctx, id, x, y, scale, flip, opacity) {
    const image = D.images[id], record = D.assetRecords[id];
    if (!image) return false;
    const anchor = record.anchor || [image.width / 2, image.height];
    ctx.save(); ctx.translate(Math.round(x), Math.round(y));
    ctx.scale(flip ? -scale : scale, scale);
    if (opacity !== undefined) ctx.globalAlpha *= opacity;
    ctx.drawImage(image, -anchor[0], -anchor[1]); ctx.restore(); return true;
  };
})(window.DN);
