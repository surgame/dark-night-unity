(function (D) {
  "use strict";
  const cache = {};
  D.icon = function (type) {
    if (!cache[type]) {
      const original = { pick: "pickaxeicon", pistol: "pistolicon", bomb: "bombicon" }[type];
      cache[type] = original ? D.assetRecords[original].uri : D.pixelSprites[type].canvas.toDataURL();
    }
    return '<img class="pixel-icon" width="32" height="32" alt="" aria-hidden="true" src="' + cache[type] + '">';
  };
})(window.DN);
