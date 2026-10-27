(function (D) {
  "use strict";
  // Equipment colors are the existing Handheld/manifest.json author palette.
  D.pixelPalette = {
    d: "#393c3b", s: "#656b68", m: "#9ba29b", h: "#d1cebc",
    w: "#69462e", b: "#96704b", l: "#b29566", r: "#984d3f",
    R: "#c17858", t: "#c7b58b", f: "#e8bc67", F: "#f4e2b5",
    a: "#d2b58b", A: "#a88864", g: "#5e6041", G: "#85835b",
    c: "#668e94", C: "#d1cebc", v: "#706047"
  };
  D.pixelSprites = {};
  D.pixelArt = function (id, width, height, paint, anchor) {
    const canvas = document.createElement("canvas");
    canvas.width = width; canvas.height = height;
    const c = canvas.getContext("2d");
    function rect(x, y, w, h, color) {
      c.fillStyle = D.pixelPalette[color] || color;
      c.fillRect(Math.round(x), Math.round(y), w, h);
    }
    function line(x, y, tx, ty, color, thickness) {
      const count = Math.max(Math.abs(tx - x), Math.abs(ty - y), 1);
      for (let i = 0; i <= count; i++) rect(x + (tx - x) * i / count,
        y + (ty - y) * i / count, thickness || 1, thickness || 1, color);
    }
    function disk(x, y, radius, color) {
      for (let row = -radius; row <= radius; row++) {
        const span = Math.floor(Math.sqrt(radius * radius - row * row));
        rect(x - span, y + row, span * 2 + 1, 1, color);
      }
    }
    paint({ rect: rect, line: line, disk: disk });
    const sprite = { canvas: canvas, w: width, h: height, anchor: anchor || [width / 2, height] };
    D.pixelSprites[id] = sprite;
    return sprite;
  };
  D.drawPixel = function (c, id, x, y, scale, flip) {
    const s = D.pixelSprites[id];
    if (!s) return false;
    c.save(); c.translate(Math.round(x), Math.round(y)); c.scale(flip ? -scale : scale, scale);
    c.drawImage(s.canvas, -s.anchor[0], -s.anchor[1]); c.restore(); return true;
  };
  D.drawMineral = function (c, kind, x, y, variant, scale) {
    const img = D.images["mineral-" + kind];
    if (!img) return;
    // Native atlas: 16 columns x 4 rows of 8px variants, no smooth resampling.
    const sx = ((variant || 0) % 16) * 8, sy = 24;
    c.drawImage(img, sx, sy, 8, 8, Math.round(x), Math.round(y), 8 * scale, 8 * scale);
  };
  D.makePixelArt = function () { D.makeEquipmentArt(); D.makePropArt(); if (D.makeFeatureArt) D.makeFeatureArt(); };
})(window.DN);
