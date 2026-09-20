"""Run one authenticated local Unity Pipeline command; never print the descriptor token."""
import json
import sys
import urllib.request
from pathlib import Path

root = Path(__file__).resolve().parents[2]
descriptor = json.loads((root / 'Game/Library/Pipeline/.unity-pipeline-port').read_text(encoding='utf-8-sig'))
body = dict(command=sys.argv[1], parameters=json.loads(sys.argv[2]) if len(sys.argv) > 2 else {})
if len(sys.argv) > 4 and sys.argv[4] == 'job':
    body['job'] = True
req = urllib.request.Request(f'http://127.0.0.1:{descriptor["port"]}/api/exec',
    data=json.dumps(body).encode(), headers={'Authorization': 'Bearer ' + descriptor['evalToken'], 'Content-Type': 'application/json'})
with urllib.request.urlopen(req, timeout=60) as response:
    result = response.read().decode()
if len(sys.argv) > 3:
    output = root / sys.argv[3]; output.parent.mkdir(parents=True, exist_ok=True); output.write_text(result, encoding='utf8')
print(result)
