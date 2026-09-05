/** Creates a disposable visual comparison save; never rewrites the source save. */
import { cpSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { homedir } from 'node:os';
import { randomUUID } from 'node:crypto';
const repo = resolve(import.meta.dir, '..');
const source = process.argv[2];
if (!source || !source.endsWith('.timber')) throw new Error('Usage: bun scripts/prepare-fire-response-review.ts <template.timber>');
const run = join(homedir(), 'Library/Application Support/Mechanistry/Timberborn/WildfireQA', 'asset-review-' + Date.now());
mkdirSync(run, { recursive: true });
function command(cmd: string[]) {
  const r = Bun.spawnSync(cmd, { cwd: run });
  if (r.exitCode !== 0) throw new Error(r.stderr.toString());
  return r.stdout.toString();
}
command(['unzip', '-q', resolve(source), '-d', run]);
// Trusted local fixture shape, with explicit precondition checks before modification.
const world = JSON.parse(readFileSync(join(run, 'world.json'), 'utf8'));
const size = world.Singletons.MapSize.Size;
if (size.X !== 50 || size.Y !== 50) throw new Error('This layout requires a 50x50 fixture.');
const voxels = world.Singletons.TerrainMap.Voxels.Array.split(' ').map(Number);
const placements = [
  ['FireBell_Folktails', 18, 30, 2, 2], ['NativeLodge', 18, 35, 2, 2],
  ['WardenStation_Ironteeth', 24, 30, 3, 3], ['NativeBarrack', 24, 35, 3, 2],
  ['SmokeFan', 30, 30, 2, 2], ['FireBerm', 33, 30, 1, 1],
  ['BrigadeBucket', 18, 27, 1, 1], ['WardenSprayer', 24, 27, 1, 1],
  ['WardenHelmet', 26, 27, 1, 1], ['WardenCoat', 28, 27, 1, 1],
] as const;
for (const [,x,y,w,d] of placements) for(let dx=0;dx<w;dx++) for(let dy=0;dy<d;dy++) {
  const at=x+dx+(y+dy)*50;
  if (voxels[at+3*2500] !== 1 || voxels[at+4*2500] !== 0) throw new Error(`Unsupported review pad at ${x+dx},${y+dy}`);
}
world.Entities = world.Entities.filter((e: {Components?: {BlockObject?: {Coordinates?: {X:number;Y:number}}}}) => {
  const p=e.Components?.BlockObject?.Coordinates;
  return !p || p.X<16 || p.X>36 || p.Y<26 || p.Y>39;
});
for(const [name,x,y] of placements) world.Entities.push({Id:randomUUID(),Template:'WildfirePreview.'+name,
  Components:{BlockObject:{Coordinates:{X:x,Y:y,Z:4},Orientation:'Cw0'},Constructible:{Finished:true}}});
world.Singletons.CameraService.CameraState={Target:{X:25,Y:4,Z:32},ZoomLevel:.10,HorizontalAngle:0,VerticalAngle:45};
const stamp=new Date();
const timestamp=`${String(stamp.getMonth()+1).padStart(2,'0')}/${String(stamp.getDate()).padStart(2,'0')}/${stamp.getFullYear()} ${String(stamp.getHours()).padStart(2,'0')}:${String(stamp.getMinutes()).padStart(2,'0')}:${String(stamp.getSeconds()).padStart(2,'0')}`;
world.Timestamp=timestamp;
const metadata=JSON.parse(readFileSync(join(run,'save_metadata.json'),'utf8'));
metadata.Timestamp=timestamp;
metadata.Mods.push({Id:'JasonKleinberg.Wildfire.AssetPreview',Name:'Wildfire Asset Preview',Version:'0.1.0'});
writeFileSync(join(run,'world.json'),JSON.stringify(world));
writeFileSync(join(run,'save_metadata.json'),JSON.stringify(metadata));
command(['zip','-q','review.timber','world.json','save_metadata.json','save_thumbnail.jpg','version.txt']);
const dest=join(homedir(),'Documents/Timberborn/ExperimentalSaves','Wildfire Asset Review');
mkdirSync(dest,{recursive:true});
const save=join(dest,`Art Review ${Date.now()}.timber`);
if(existsSync(save)) throw new Error('Refusing to overwrite an existing review save.');
cpSync(join(run,'review.timber'),save);
writeFileSync(join(run,'review-manifest.json'),JSON.stringify({source:resolve(source),save,placements},null,2));
console.log(JSON.stringify({run,save,placements:placements.length}));
