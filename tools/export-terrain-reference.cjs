// Reproduce the supplied H5 art and freeze terrain vectors. Never run its UI/game module.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const zlib = require('node:zlib');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '..');
const input = path.join(__dirname, 'terrain-reference', 'terrain_generator.html');
const html = fs.readFileSync(input, 'utf8');
const source = html.slice(html.indexOf("'use strict';"), html.indexOf('  D.HULL='));
const cutoff = source.lastIndexOf('(function(D){');
function canvas() {
  const c = { width: 0, height: 0, pixels: null };
  const ensure = () => c.pixels || (c.pixels = new Uint8ClampedArray(c.width * c.height * 4));
  c.getContext = () => ({
    createImageData: (w, h) => ({ data: new Uint8ClampedArray(w * h * 4) }),
    putImageData: im => { c.pixels = new Uint8ClampedArray(im.data); },
    drawImage: (src, x, y) => {
      const p = ensure();
      for (let row = 0; row < src.height; row++)
        p.set(src.pixels.subarray(row * src.width * 4, (row + 1) * src.width * 4), ((y + row) * c.width + x) * 4);
    }
  });
  return c;
}
const context = vm.createContext({window: {}, document: {createElement: () => canvas()}});
vm.runInContext(source.slice(0, cutoff), context, {timeout: 10000});
const D = context.window.DN;
const crc = bytes => {
  let c = -1;
  for (const b of bytes) { c ^= b; for (let i = 0; i < 8; i++) c = (c >>> 1) ^ ((c & 1) ? 0xedb88320 : 0); }
  return (c ^ -1) >>> 0;
};
function chunk(type, bytes) {
  const body = Buffer.concat([Buffer.from(type), bytes]);
  const n = Buffer.alloc(4), sum = Buffer.alloc(4);
  n.writeUInt32BE(bytes.length); sum.writeUInt32BE(crc(body));
  return Buffer.concat([n, body, sum]);
}
function png(c) {
  const h = Buffer.alloc(13); h.writeUInt32BE(c.width); h.writeUInt32BE(c.height, 4); h[8] = 8; h[9] = 6;
  const rows = [];
  for (let y = 0; y < c.height; y++) rows.push(Buffer.from([0]), Buffer.from(c.pixels.subarray(y*c.width*4,(y+1)*c.width*4)));
  return Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',h),chunk('IDAT',zlib.deflateSync(Buffer.concat(rows))),chunk('IEND',Buffer.alloc(0))]);
}
const art = path.join(root, 'Game/Assets/DarkNights/Res/Terrain/TestTerrain/Art');
const tiles = new D.Tiles();
const outputs = new Map([
  [path.join(art, 'terrain-8x8-all.png'), png(tiles.allAtlas())],
  [path.join(art, 'terrain-art.json'), Buffer.from(JSON.stringify(tiles.descriptor(), null, 2))]
]);
const vectors = [];
for (const seed of ['GREYPINE-1616', 'MAP-2026']) for (const s of D.SURFACES) for (const caves of ['modules','organic']) {
  const w = D.generateWorld({seed, surface:s.key, caves});
  vectors.push({seed,surface:s.key,caves,signature:w.signature,sha256:crypto.createHash('sha256').update(w.g.data).digest('hex')});
}
outputs.set(path.join(root, 'tools/terrain-reference/generation-vectors.json'), Buffer.from(JSON.stringify({generatorVersion:D.GENERATOR_VERSION,vectors},null,2)));
outputs.set(path.join(root, 'tools/terrain-reference/room-templates.json'), Buffer.from(JSON.stringify(D.ROOM_TEMPLATES,null,2)));
for (const [file, bytes] of outputs)
  if (fs.existsSync(file) && !fs.readFileSync(file).equals(bytes)) throw new Error('Existing authored/frozen output differs; refusing overwrite: ' + file);
for (const [file, bytes] of outputs) if (!fs.existsSync(file)) {
  fs.mkdirSync(path.dirname(file), {recursive:true}); fs.writeFileSync(file, bytes, {flag:'wx'});
}
console.log(JSON.stringify({atlas:'128x256',terrains:8,variants:4,masks:16,vectors:vectors.length}));
