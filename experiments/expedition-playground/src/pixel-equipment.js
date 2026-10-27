(function (D) {
  "use strict";
  D.makeEquipmentArt = function () {
    function item(id, paint) { D.pixelArt(id, 24, 24, paint, [8, 12]); }
    function grip(p) {
      p.rect(7, 11, 4, 7, "d"); p.rect(8, 12, 2, 5, "w"); p.rect(8, 12, 1, 2, "b");
    }
    item("shotgun", function (p) {
      grip(p); p.rect(3, 10, 7, 4, "w"); p.rect(4, 10, 5, 1, "l");
      p.rect(9, 8, 14, 5, "d"); p.rect(10, 8, 12, 1, "m");
      p.rect(13, 9, 10, 1, "s"); p.rect(13, 11, 10, 1, "s");
      p.rect(12, 12, 6, 3, "w"); p.rect(13, 12, 4, 1, "b"); p.rect(20, 7, 1, 1, "m");
    });
    item("nail", function (p) {
      grip(p); p.rect(6, 7, 13, 6, "d"); p.rect(7, 7, 10, 4, "s");
      p.rect(8, 7, 7, 1, "h"); p.rect(8, 9, 4, 2, "l");
      p.rect(18, 9, 5, 3, "d"); p.rect(18, 9, 4, 1, "m");
      p.rect(13, 12, 5, 6, "d"); p.rect(14, 13, 3, 4, "s"); p.rect(14, 14, 1, 2, "m");
    });
    item("shock", function (p) {
      p.rect(4, 11, 9, 3, "d"); p.rect(5, 11, 6, 2, "w");
      p.rect(11, 10, 3, 5, "s"); p.rect(14, 11, 6, 2, "m");
      p.rect(19, 8, 2, 9, "d"); p.rect(20, 8, 3, 2, "h");
      p.rect(20, 15, 3, 2, "h"); p.rect(21, 11, 1, 3, "c");
    });
    item("net", function (p) {
      grip(p); p.rect(5, 8, 14, 6, "d"); p.rect(6, 8, 9, 4, "g");
      p.rect(7, 8, 6, 1, "G"); p.rect(17, 6, 5, 10, "d");
      p.rect(18, 7, 3, 8, "s"); p.rect(18, 8, 1, 6, "m");
      p.line(8, 9, 14, 12, "t"); p.line(8, 12, 14, 9, "t");
    });
    item("foam", function (p) {
      grip(p); p.rect(6, 7, 12, 6, "d"); p.rect(7, 8, 9, 3, "s");
      p.rect(16, 9, 7, 2, "m"); p.rect(22, 8, 1, 4, "h");
      p.rect(12, 12, 7, 8, "d"); p.rect(13, 13, 5, 6, "C");
      p.rect(14, 13, 2, 4, "m"); p.rect(13, 17, 5, 1, "g");
    });
    item("flare", function (p) {
      grip(p); p.rect(7, 7, 12, 6, "d"); p.rect(8, 7, 10, 2, "m");
      p.rect(8, 9, 8, 3, "r"); p.rect(9, 9, 5, 1, "R");
      p.rect(17, 6, 4, 7, "d"); p.rect(18, 7, 2, 1, "h");
    });
    item("charge", function (p) {
      p.rect(6, 7, 12, 10, "d"); p.rect(7, 8, 10, 8, "w");
      for (let x = 8; x < 17; x += 3) { p.rect(x, 9, 2, 6, "r"); p.rect(x, 9, 1, 5, "R"); }
      p.rect(7, 11, 10, 2, "t"); p.line(12, 7, 14, 4, "m"); p.rect(14, 4, 3, 1, "l");
      p.rect(16, 11, 3, 2, "f");
    });
    item("lure", function (p) {
      grip(p); p.rect(6, 8, 14, 6, "d"); p.rect(7, 8, 9, 4, "s");
      p.rect(9, 9, 5, 2, "v"); p.rect(17, 7, 4, 8, "d");
      p.rect(18, 8, 2, 6, "m"); p.rect(8, 5, 1, 3, "m");
      p.rect(11, 4, 1, 4, "s"); p.rect(10, 4, 3, 1, "t");
    });
    item("glow", function (p) {
      p.rect(8, 7, 4, 13, "d"); p.rect(9, 8, 2, 10, "c");
      p.rect(9, 9, 1, 7, "C"); p.rect(8, 7, 4, 2, "m"); p.rect(9, 5, 2, 2, "s");
    });
    item("scanner", function (p) {
      p.rect(6, 5, 12, 14, "d"); p.rect(7, 6, 10, 11, "s");
      p.rect(8, 7, 8, 6, "d"); p.rect(9, 8, 6, 4, "g");
      p.line(9, 11, 11, 9, "C"); p.line(11, 9, 13, 10, "C"); p.rect(15, 14, 1, 1, "f");
      p.rect(8, 14, 4, 1, "m"); p.rect(10, 2, 1, 3, "m"); p.rect(15, 3, 1, 2, "s");
    });
    item("ladder", function (p) {
      p.rect(6, 4, 3, 16, "d"); p.rect(15, 4, 3, 16, "d");
      p.rect(7, 5, 1, 14, "l"); p.rect(16, 5, 1, 14, "l");
      for (let y = 6; y < 19; y += 4) { p.rect(9, y, 6, 2, "s"); p.rect(9, y, 6, 1, "m"); }
    });
    item("support", function (p) {
      p.rect(4, 4, 17, 3, "d"); p.rect(5, 4, 15, 1, "m");
      p.rect(10, 7, 5, 13, "d"); p.rect(11, 7, 3, 12, "s");
      p.rect(11, 7, 1, 11, "m"); p.rect(8, 12, 9, 3, "w"); p.rect(9, 12, 7, 1, "l");
      p.rect(6, 20, 13, 2, "d"); p.rect(7, 20, 11, 1, "s");
    });
    item("winch", function (p) {
      p.rect(4, 18, 17, 3, "d"); p.rect(5, 18, 15, 1, "m");
      p.rect(6, 11, 3, 7, "s"); p.rect(16, 11, 3, 7, "s");
      p.disk(12, 11, 6, "d"); p.disk(12, 11, 4, "s"); p.disk(12, 11, 3, "w");
      p.line(9, 10, 14, 10, "l"); p.line(9, 12, 14, 12, "b");
      p.rect(18, 10, 4, 1, "m"); p.rect(21, 10, 1, 4, "w");
    });
    item("shield", function (p) {
      p.rect(6, 3, 13, 17, "d"); p.rect(7, 4, 11, 15, "s"); p.rect(7, 4, 1, 15, "m");
      p.rect(9, 6, 7, 4, "d"); p.rect(10, 7, 5, 2, "g");
      p.rect(7, 12, 11, 2, "w"); p.rect(7, 12, 11, 1, "l");
      p.rect(5, 20, 4, 2, "d"); p.rect(16, 20, 4, 2, "d");
    });
    item("seismic", function (p) {
      p.rect(6, 17, 13, 3, "d"); p.rect(9, 9, 7, 8, "d"); p.rect(10, 10, 5, 7, "s");
      p.rect(11, 10, 2, 5, "m"); p.rect(9, 7, 7, 3, "w"); p.rect(10, 7, 5, 1, "l");
      p.rect(12, 4, 1, 3, "m"); p.rect(14, 12, 1, 2, "r"); p.rect(7, 17, 11, 1, "m");
    });
    item("sample", function (p) {
      p.rect(7, 6, 10, 14, "d"); p.rect(8, 7, 8, 12, "s");
      p.rect(9, 8, 6, 9, "g"); p.rect(9, 8, 1, 8, "C"); p.rect(11, 12, 3, 4, "G");
      p.rect(7, 5, 10, 3, "d"); p.rect(8, 5, 8, 1, "h"); p.rect(10, 3, 4, 2, "s");
    });
    item("medkit", function (p) {
      p.rect(4, 8, 17, 12, "d"); p.rect(5, 9, 15, 10, "w"); p.rect(5, 9, 15, 1, "l");
      p.rect(9, 5, 7, 3, "d"); p.rect(10, 6, 5, 1, "m");
      p.rect(11, 11, 3, 6, "h"); p.rect(9, 13, 7, 2, "h");
      p.rect(6, 12, 1, 5, "b"); p.rect(18, 12, 1, 5, "b");
    });
  };
})(window.DN);
