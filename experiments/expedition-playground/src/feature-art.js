(function (D) {
  "use strict";
  D.makeFeatureArt = function () {
    function tool(id, paint) { D.pixelArt(id, 24, 24, paint, [8, 12]); }
    function grip(p) {
      p.rect(6, 10, 5, 8, "d"); p.rect(7, 12, 3, 5, "w");
      p.rect(7, 12, 1, 4, "l");
    }
    tool("cryo", function (p) {
      grip(p); p.rect(5, 7, 13, 6, "d"); p.rect(6, 8, 11, 3, "s");
      p.rect(6, 7, 9, 1, "h"); p.rect(16, 8, 7, 4, "d");
      p.rect(17, 9, 5, 2, "m"); p.rect(21, 7, 2, 6, "s");
      p.rect(12, 12, 6, 8, "d"); p.rect(13, 13, 4, 6, "c");
      p.rect(13, 13, 1, 5, "h"); p.rect(12, 16, 6, 2, "s");
      p.line(5, 8, 3, 5, "m"); p.rect(3, 4, 5, 1, "m");
    });
    tool("pulse", function (p) {
      grip(p); p.rect(5, 7, 13, 6, "d"); p.rect(6, 8, 10, 4, "w");
      p.rect(6, 7, 8, 1, "l"); p.rect(14, 8, 3, 5, "s");
      for (let x = 17; x <= 22; x += 2) p.rect(x, 6, 1, 10, "b");
      p.rect(17, 9, 6, 3, "d"); p.rect(18, 10, 5, 1, "h");
      p.rect(10, 4, 3, 3, "d"); p.rect(11, 5, 1, 1, "f");
      p.rect(11, 14, 4, 3, "s");
    });
    tool("brace", function (p) {
      grip(p); p.rect(7, 6, 10, 7, "d"); p.rect(8, 7, 8, 4, "w");
      p.rect(8, 7, 5, 1, "l"); p.rect(15, 8, 8, 3, "s");
      p.rect(16, 8, 6, 1, "h"); p.rect(22, 7, 1, 5, "m");
      p.disk(14, 16, 5, "d"); p.disk(14, 16, 3, "s");
      p.rect(13, 14, 1, 4, "m"); p.rect(16, 3, 2, 4, "m");
    });
    D.pixelArt("vent-prop", 32, 20, function (p) {
      for (let i = 0; i < 5; i++) {
        p.disk(5 + i * 5, 15 - i % 2 * 2, 5, "d");
        p.rect(3 + i * 5, 11 - i % 2 * 2, 5, 2, "w");
        p.rect(3 + i * 5, 11 - i % 2 * 2, 3, 1, "b");
      }
      p.rect(6, 13, 21, 3, "#261d14");
      p.rect(10, 13, 3, 1, "r"); p.rect(18, 14, 4, 1, "R");
    });
    D.pixelArt("core-prop", 32, 32, function (p) {
      p.disk(16, 16, 14, "d"); p.disk(15, 15, 11, "s");
      p.rect(8, 7, 5, 8, "m"); p.rect(9, 7, 3, 3, "h");
      p.rect(18, 10, 8, 4, "w"); p.rect(19, 10, 5, 1, "b");
      p.rect(11, 20, 10, 6, "d"); p.rect(12, 20, 2, 5, "m");
      p.line(14, 9, 11, 20, "d"); p.line(11, 20, 22, 17, "d");
      p.rect(15, 14, 5, 5, "b"); p.rect(16, 15, 2, 2, "f");
      p.rect(5, 18, 3, 4, "w"); p.rect(23, 21, 3, 4, "m");
    }, [16, 16]);
    D.pixelArt("root-knot", 28, 22, function (p) {
      p.disk(14, 11, 10, "d"); p.disk(14, 10, 8, "g");
      for (let i = 0; i < 5; i++) {
        p.line(5 + i * 4, 4, 9 + i * 3, 19, i % 2 ? "G" : "w");
      }
      p.rect(11, 8, 6, 4, "d"); p.rect(12, 8, 2, 3, "c");
      p.rect(5, 5, 2, 2, "G"); p.rect(19, 11, 3, 2, "b");
    }, [14, 10]);
    D.pixelArt("valve-prop", 24, 32, function (p) {
      p.rect(9, 15, 6, 17, "d"); p.rect(10, 16, 2, 15, "s");
      p.rect(8, 24, 13, 4, "d"); p.rect(9, 25, 12, 1, "m");
      p.disk(12, 11, 9, "d"); p.disk(12, 11, 7, "r"); p.disk(12, 11, 5, "d");
      p.line(6, 11, 18, 11, "R"); p.line(12, 5, 12, 17, "R");
      p.rect(11, 10, 3, 3, "m"); p.rect(6, 5, 3, 1, "l");
    });
    D.pixelArt("amber-prop", 36, 34, function (p) {
      p.disk(18, 17, 15, "d"); p.disk(18, 17, 13, "w");
      p.disk(17, 16, 11, "b"); p.disk(17, 16, 8, "l");
      p.rect(10, 10, 4, 9, "f"); p.rect(11, 9, 2, 4, "F");
      p.rect(21, 20, 5, 5, "w"); p.rect(17, 17, 4, 8, "b");
      p.line(23, 5, 27, 15, "d"); p.line(27, 15, 25, 25, "d");
      p.rect(8, 25, 6, 5, "s"); p.rect(9, 25, 3, 1, "m");
    }, [18, 17]);
  };
})(window.DN);
