(function (D) {
  "use strict";
  D.makePropArt = function () {
    D.pixelArt("crate-prop", 28, 24, function (p) {
      p.rect(2, 3, 24, 20, "d"); p.rect(3, 4, 22, 17, "w");
      for (let y = 5; y < 20; y += 5) { p.rect(4, y, 20, 1, "b"); p.rect(4, y + 3, 20, 1, "d"); }
      p.rect(4, 4, 3, 17, "s"); p.rect(21, 4, 3, 17, "s");
      p.rect(4, 4, 1, 16, "m"); p.rect(21, 4, 1, 16, "m");
      p.rect(3, 10, 22, 3, "d"); p.rect(12, 10, 4, 5, "s"); p.rect(13, 11, 2, 2, "h");
      for (const x of [5, 22]) for (const y of [5, 18]) p.rect(x, y, 1, 1, "h");
    });
    D.pixelArt("sign-prop", 28, 26, function (p) {
      p.rect(12, 10, 3, 16, "d"); p.rect(13, 10, 1, 15, "b");
      p.rect(2, 3, 24, 11, "d"); p.rect(3, 4, 22, 8, "w");
      p.rect(4, 4, 20, 1, "b"); p.line(7, 8, 20, 8, "t");
      p.line(17, 5, 20, 8, "t"); p.line(17, 11, 20, 8, "t");
      p.rect(4, 6, 1, 1, "s"); p.rect(23, 6, 1, 1, "s");
    });
    D.pixelArt("trace-prop", 24, 20, function (p) {
      p.rect(3, 15, 18, 4, "w"); p.rect(5, 15, 9, 1, "b");
      p.line(4, 13, 17, 7, "s", 3); p.line(5, 13, 18, 7, "m");
      p.rect(16, 5, 4, 5, "d"); p.rect(17, 6, 2, 2, "h");
      p.rect(6, 12, 2, 3, "r"); p.rect(19, 16, 2, 2, "m");
    });
    D.pixelArt("lamp-prop", 16, 24, function (p) {
      p.rect(6, 2, 4, 4, "s"); p.rect(7, 1, 2, 2, "m");
      p.rect(3, 5, 10, 16, "d"); p.rect(4, 6, 8, 12, "w");
      p.rect(5, 8, 6, 8, "f"); p.rect(6, 8, 2, 7, "F");
      p.rect(4, 7, 8, 1, "m"); p.rect(4, 17, 8, 2, "s");
      p.rect(7, 8, 1, 9, "s"); p.rect(3, 20, 10, 2, "m");
    });
    for (let variant = 0; variant < 3; variant++) {
      D.pixelArt("fungus" + variant, 24, 24, function (p) {
        const x = 11 + variant;
        p.rect(x - 2, 10, 4, 13, "d"); p.rect(x - 1, 10, 2, 12, "G");
        p.rect(x + 1, 14, 1, 7, "g"); p.rect(4, 7, 15, 5, "d");
        p.rect(6, 5, 11, 2, "d"); p.rect(5, 7, 13, 3, "g");
        p.rect(7, 6, 9, 2, "c"); p.rect(7, 8, 3, 1, "C");
        p.rect(14, 7, 2, 1, "C"); p.rect(7, 11, 9, 1, "C");
        p.rect(2, 16, 6, 2, "g"); p.rect(3, 15, 4, 1, "c"); p.rect(4, 18, 1, 4, "G");
        p.rect(15, 20, 6, 2, "g"); p.rect(16, 19, 4, 1, "c");
      });
    }
    D.pixelArt("sample-prop", 24, 24, function (p) {
      p.rect(6, 9, 13, 13, "d"); p.rect(8, 6, 9, 3, "d");
      p.rect(7, 10, 11, 10, "g"); p.rect(9, 7, 7, 3, "g");
      p.rect(9, 10, 3, 7, "c"); p.rect(10, 9, 2, 4, "C");
      p.rect(14, 12, 3, 6, "G"); p.rect(7, 21, 12, 2, "w");
      p.rect(3, 22, 4, 1, "b"); p.rect(19, 21, 2, 2, "b");
    });
    D.pixelArt("vine-mouth", 24, 18, function (p) {
      p.rect(5, 3, 14, 11, "d"); p.rect(3, 6, 18, 5, "g");
      p.rect(5, 4, 14, 3, "G"); p.rect(7, 6, 10, 5, "w");
      p.rect(9, 7, 6, 4, "d");
      for (const x of [6, 10, 14, 18]) p.rect(x, 6, 1, 2, "t");
      p.line(4, 9, 1, 14, "g"); p.line(19, 9, 22, 14, "g");
    }, [12, 8]);
    D.pixelArt("foam-prop", 40, 18, function (p) {
      for (let i = 0; i < 7; i++) {
        const x = 5 + i * 5, y = 9 + i % 2 * 3;
        p.disk(x, y, 5, "g"); p.disk(x, y - 1, 4, "G"); p.disk(x - 1, y - 2, 2, "C");
      }
    }, [20, 10]);
    D.pixelArt("net-prop", 44, 20, function (p) {
      for (let x = 2; x < 40; x += 5) {
        p.line(x, 3, x + 6, 17, "t"); p.line(x + 3, 3, x - 3, 17, "l");
      }
      p.line(3, 3, 39, 3, "b"); p.line(1, 17, 42, 17, "b");
      p.rect(1, 15, 2, 4, "s"); p.rect(40, 15, 2, 4, "s");
    }, [22, 12]);
    D.pixelArt("exit-prop", 38, 42, function (p) {
      p.rect(2, 1, 34, 40, "d"); p.rect(4, 4, 30, 36, "#1b150f");
      p.rect(3, 3, 5, 38, "w"); p.rect(30, 3, 5, 38, "w");
      p.rect(3, 3, 2, 37, "b"); p.rect(30, 3, 2, 37, "b");
      p.rect(1, 2, 36, 5, "w"); p.rect(2, 2, 34, 1, "l");
      p.line(7, 12, 13, 6, "b"); p.line(24, 6, 30, 12, "b");
      p.rect(14, 11, 10, 5, "s"); p.rect(15, 12, 8, 3, "g");
      p.line(17, 13, 22, 13, "C"); p.rect(17, 12, 1, 3, "C");
      p.rect(1, 38, 36, 3, "d"); p.rect(3, 38, 32, 1, "s");
    });
  };
})(window.DN);
