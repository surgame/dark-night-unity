"""文档、链接、快照和工作区核对；不运行H5运动或Unity。"""
import hashlib
import json
import re
import subprocess
from datetime import datetime, timezone
from html.parser import HTMLParser
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
WORKSPACE = ROOT.parents[1]
SOURCE = Path('D:/Developer/MiniGames/Dark Nights/unity-projects')
def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()
class Page(HTMLParser):
    def __init__(self):
        super().__init__()
        self.ids, self.links, self.headings = [], [], []
        self.external = []
    def handle_starttag(self, tag, attrs):
        fields = dict(attrs)
        if 'id' in fields:
            self.ids.append(fields['id'])
        if tag == 'a' and 'href' in fields:
            self.links.append(fields['href'])
        if tag in ('h1', 'h2', 'h3'):
            self.headings.append(tag)
        for key in ('src', 'href'):
            if fields.get(key, '').startswith(('http:', 'https:', '//')):
                self.external.append(fields[key])
def check_link(link, origin, page=None):
    if link.startswith('#'):
        assert page is not None and link[1:] in page.ids, link
        return
    target = (origin.parent / link.split('#', 1)[0]).resolve()
    assert target.is_relative_to(ROOT), (origin, link)
    if target == ROOT / 'evidence/plan-verification.json':
        return
    assert target.exists(), (origin, link)
manifest = json.loads((ROOT / 'references/baseline-manifest.json').read_text(encoding='utf-8'))
for item in manifest['files']:
    saved, original = ROOT / item['snapshot'], Path(item['source'])
    assert sha(saved) == item['sha256'] == sha(original), item['source']
for item in manifest['sourceProtectedDocuments']:
    assert sha(Path(item['path'])) == item['sha256'], item['path']
docs = []
for filename in ('开发执行方案.md', '地图扩展合同.md', 'README.md', 'STATUS.md'):
    path = ROOT / filename
    text = path.read_text(encoding='utf-8')
    assert text.startswith('# ')
    for label, href in re.findall(r'\[([^\]]+)\]\(([^)]+)\)', text):
        check_link(href, path)
    for table in re.findall(r'(?m)(?:^\|.*\n)+', text):
        rows = table.strip().splitlines()
        counts = [len(row.strip().strip('|').split('|')) for row in rows]
        assert len(set(counts)) == 1, (filename, counts)
    docs.append({'path': filename, 'bytes': path.stat().st_size, 'sha256': sha(path)})
pages = []
for filename in ('开发执行方案.html', '地图扩展合同.html'):
    path = ROOT / filename
    parser = Page()
    parser.feed(path.read_text(encoding='utf-8'))
    assert len(parser.ids) == len(set(parser.ids))
    assert parser.headings.count('h1') == 1
    assert not parser.external
    for href in parser.links:
        check_link(href, path, parser)
    pages.append({'path': filename, 'bytes': path.stat().st_size, 'sha256': sha(path), 'sections': parser.headings.count('h2'), 'links': len(parser.links)})
assert 132*2 == 264 and 78*2 == 156 and 264*156 == 41184
assert 264*24 == 6336 and 156*24 == 3744
calculations = {'oldBackgroundRgbaMiB': round(3168*1872*4/1024**2, 2), 'expandedBackgroundRgbaMiB': round(6336*3744*4/1024**2, 2), 'sixteenPaddedChunksRgbaMiB': round(816*816*4*16/1024**2, 2)}
assert calculations == {'oldBackgroundRgbaMiB':22.62, 'expandedBackgroundRgbaMiB':90.49, 'sixteenPaddedChunksRgbaMiB':40.64}, calculations
def git(root, *args):
    return subprocess.check_output(['git', '-C', str(root), *args], text=True, encoding='utf-8').strip()
assert git(WORKSPACE, 'branch', '--show-current') == 'docs-20261011-mine-map-expansion-plan'
assert git(WORKSPACE, 'status', '--short') == ''
assert git(SOURCE, 'branch', '--show-current') == manifest['sourceBranch']
assert git(SOURCE, 'status', '--short') == manifest['sourceStatus']
absent = [p for p in ('Game/Library','Game/Temp','Game/Logs','Game/obj','Library','Temp','Logs','obj','.deps') if not (WORKSPACE / p).exists()]
assert len(absent) == 9, absent
all_files = [p for p in ROOT.rglob('*') if p.is_file()]
assert not (ROOT / 'prototype').exists(), 'Plan-only stage should not create enlarged game code'
report = {'createdAt': datetime.now(timezone.utc).isoformat(), 'scope':'Plan documents and read-only baseline verification only; no H5 movement, browser acceptance or Unity tests.',
          'workspace':str(WORKSPACE), 'branch':manifest['branch'], 'taskRoot':str(ROOT), 'documents':docs, 'html':pages,
          'checks':{'utf8AndStructure':'passed','relativeLinksAndAnchors':'passed','tableColumns':'passed','sizeAndMemoryArithmetic':'passed','snapshotAndOriginalSha':'passed','originalWorkspaceStatus':'preserved','newWorktreeTrackedChanges':'none','unityCacheFolders':'absent'},
          'baselineFiles':len(manifest['files']), 'calculations':calculations, 'newImplementation':'not started',
          'browserDocumentLayout':'Static HTML structure checked; no actual browser rendering or screenshots.',
          'fileCountBeforeReceipt':len(all_files), 'bytesBeforeReceipt':sum(p.stat().st_size for p in all_files)}
out = ROOT / 'evidence/plan-verification.json'
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'documents':len(docs),'html':len(pages),'baselineFiles':report['baselineFiles'],'checks':report['checks'],'calculations':calculations,'report':str(out)}, ensure_ascii=False))
