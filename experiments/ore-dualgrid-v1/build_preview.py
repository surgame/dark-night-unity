"""Package grid painting art proof; actual Unity import is intentionally separate."""
import base64,json,sys
from io import BytesIO
from pathlib import Path
import numpy as np
from PIL import Image
from build_tiles import decode,over,image

root=Path(__file__).resolve().parent
destination=Path(sys.argv[1])
def uri(p):return 'data:image/png;base64,'+base64.b64encode(p.read_bytes()).decode()
ref=json.loads((root/'reference.json').read_text())
wall=decode(ref['base'])
for k in [2,1,0]:wall=over(wall,decode(ref['layers'][k]))
bg=image(wall[104:224,165:349]);stream=BytesIO();bg.save(stream,format='PNG')
grid=np.zeros((16,24),dtype='uint8');grid[3:8,3:9]=1;grid[4:6,4:6]=0
grid[6:11,7:14]=1;grid[9:13,11:19]=1;grid[3,14]=1;grid[4,15]=1;grid[3:5,20]=1
payload={'atlases':[uri(root/'atlases'/f'{k}-merged.png') for k in ['copper','iron','gold','silver','diamond']],
         'background':'data:image/png;base64,'+base64.b64encode(stream.getvalue()).decode(),
         'context':uri(root/'preview'/'cave-grid-ores.png'),'grid':grid.tolist()}
html=(root/'preview.template.html').read_text(encoding='utf-8').replace('__GRID_DATA__',json.dumps(payload,separators=(',',':')))
assert len(html.encode())<1_000_000
destination.parent.mkdir(exist_ok=True,parents=True);destination.write_text(html,encoding='utf-8')
print(json.dumps({'path':str(destination),'bytes':len(html.encode())}))
