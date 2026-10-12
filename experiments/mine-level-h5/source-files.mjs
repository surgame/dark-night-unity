import fs from 'node:fs';
import path from 'node:path';
export function mapFiles(root){
  return ['map.js','map-registry.js','network-terrain.js','natural-terrain.js','natural-content.js','cave-shape.js','cave-terrain.js','cave-content.js'].concat(
    fs.readdirSync(path.join(root,'src/maps')).filter(f=>f.endsWith('.js')).sort().map(f=>'maps/'+f));
}
export function modelFiles(root){return mapFiles(root).concat(['excavation.js','ladder-kit.js','clue-events.js','simulation.js']);}
export function allFiles(root){return modelFiles(root).concat(['rendering.js','app.js']);}
