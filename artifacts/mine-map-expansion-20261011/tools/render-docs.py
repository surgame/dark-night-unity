"""将本包两份Markdown生成离线HTML；只处理实际使用的语法，不执行游戏。"""
import html
import re
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
PAIRS = {'开发执行方案.md': '开发执行方案.html', '地图扩展合同.md': '地图扩展合同.html'}
def inline(value):
    result = html.escape(value)
    def link(match):
        label, href = match.groups()
        href = PAIRS.get(href, href)
        return '<a href="' + href + '">' + label + '</a>'
    return re.sub(r'\[([^\]]+)\]\(([^)]+)\)', link, result)

def render(text):
    lines = text.splitlines()
    body, toc = [], []
    index, sections = 0, 0
    while index < len(lines):
        line = lines[index]
        if not line.strip():
            index += 1
            continue
        heading = re.match(r'^(#{1,3}) (.+)$', line)
        if heading:
            level, title = len(heading[1]), heading[2]
            sections += 1
            anchor = 'section-' + str(sections)
            body.append('<h' + str(level) + ' id="' + anchor + '">' + inline(title) + '</h' + str(level) + '>')
            if level == 2:
                toc.append('<a href="#' + anchor + '">' + inline(title) + '</a>')
            index += 1
            continue
        if line.startswith('    '):
            code = []
            while index < len(lines) and (lines[index].startswith('    ') or not lines[index].strip()):
                code.append(lines[index][4:] if lines[index].startswith('    ') else '')
                index += 1
            body.append('<pre><code>' + html.escape('\n'.join(code).rstrip()) + '</code></pre>')
            continue
        if line.startswith('|') and index + 1 < len(lines) and re.fullmatch(r'\s*\|[\s|:\-]+\|\s*', lines[index + 1]):
            rows = []
            while index < len(lines) and lines[index].startswith('|'):
                rows.append([c.strip() for c in lines[index].strip().strip('|').split('|')])
                index += 1
            head = '<tr>' + ''.join('<th>' + inline(c) + '</th>' for c in rows[0]) + '</tr>'
            cells = ''.join('<tr>' + ''.join('<td>' + inline(c) + '</td>' for c in row) + '</tr>' for row in rows[2:])
            body.append('<div class="table-wrap"><table><thead>' + head + '</thead><tbody>' + cells + '</tbody></table></div>')
            continue
        kind = 'ul' if line.startswith('- ') else 'ol' if re.match(r'^\d+\. ', line) else None
        if kind:
            items = []
            pattern = r'^- ' if kind == 'ul' else r'^\d+\. '
            while index < len(lines) and re.match(pattern, lines[index]):
                items.append('<li>' + inline(re.sub(pattern, '', lines[index])) + '</li>')
                index += 1
            body.append('<' + kind + '>' + ''.join(items) + '</' + kind + '>')
            continue
        paragraph = []
        while index < len(lines) and lines[index].strip():
            paragraph.append(lines[index])
            index += 1
        body.append('<p>' + inline(' '.join(paragraph)) + '</p>')
    return '\n'.join(body), '\n'.join(toc)

STYLE = """*{box-sizing:border-box}html{scroll-behavior:smooth;scroll-padding-top:24px}body{margin:0;background:#f3f6f7;color:#23323b;font:15px/1.85 "Segoe UI","Microsoft YaHei",sans-serif}a{color:#176970}nav a{display:block;text-decoration:none;padding:7px 10px;border-radius:6px;font-size:13px;line-height:1.6}nav a:hover{background:#e4efef}.layout{display:grid;grid-template-columns:220px minmax(0,1fr);max-width:1500px;margin:auto}.sidebar{position:sticky;top:0;height:100vh;overflow:auto;padding:30px 20px;border-right:1px solid #d9e2e6;background:#f7f9fa}.brand{letter-spacing:.12em;color:#176970;font-size:13px;font-weight:750}.sub{font-size:12px;color:#657580;margin:10px 0 22px}.main{min-width:0;padding:30px clamp(18px,4vw,56px) 60px}.actions{display:flex;gap:16px;justify-content:space-between;align-items:center;margin-bottom:18px;font-size:12px;color:#647580}.actions button{background:white;border:1px solid #cbdadc;color:#176970;border-radius:6px;padding:7px 12px;cursor:pointer}.paper{background:white;border:1px solid #d9e2e6;border-radius:12px;padding:clamp(22px,4vw,45px);box-shadow:0 6px 25px #19334008}h1{font-size:30px;line-height:1.35;margin:0 0 22px}h2{font-size:21px;line-height:1.5;margin:38px 0 16px;padding-top:24px;border-top:1px solid #dfe6e9}h3{font-size:17px;margin:24px 0 10px}p{margin:12px 0 18px;overflow-wrap:anywhere}li{margin:8px 0}ul,ol{padding-left:25px}.table-wrap{overflow:auto;margin:18px 0 25px}table{width:100%;border-collapse:collapse;font-size:13px;line-height:1.75}th,td{border-bottom:1px solid #dce5e8;text-align:left;vertical-align:top;padding:10px 12px;min-width:92px;overflow-wrap:anywhere}th{background:#edf4f4;color:#28525a;font-weight:650}tbody tr:nth-child(even){background:#fbfcfd}pre{overflow:auto;line-height:1.65;font-size:13px;background:#f3f6f7;border:1px solid #dce5e8;padding:17px;border-radius:6px}code{font-family:Consolas,"Microsoft YaHei",monospace}.foot{font-size:12px;color:#657780;margin-top:24px}@media(max-width:850px){.layout{display:block}.sidebar{position:static;height:auto;border-right:0;border-bottom:1px solid #d9e2e6;padding:15px}.sub{margin:6px 0 12px}nav{display:flex;gap:3px;overflow:auto}nav a{flex-shrink:0;white-space:nowrap}.main{padding:18px 12px 40px}h1{font-size:25px}.paper{padding:22px 18px}}@media print{body{background:white;font-size:11pt}.layout{display:block}.sidebar,.actions{display:none}.main,.paper{padding:0;border:0;box-shadow:none}h1{font-size:22pt}h2{font-size:16pt;break-after:avoid}h3{break-after:avoid}table{font-size:9pt}.table-wrap{overflow:visible}tr{break-inside:avoid}a{color:inherit}pre{white-space:pre-wrap}}"""
for source, target in PAIRS.items():
    text = (ROOT / source).read_text(encoding='utf-8')
    title = text.splitlines()[0].removeprefix('# ')
    content, toc = render(text)
    page = '<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>' + html.escape(title) + '</title><style>' + STYLE + '</style></head><body><div class="layout"><aside class="sidebar"><div class="brand">DARK NIGHTS</div><div class="sub">地图扩展开发方案<br>2026年10月11日</div><nav>' + toc + '</nav></aside><main class="main"><div class="actions"><span>方案已完成　扩展实现待执行</span><button onclick="window.print()">打印或保存PDF</button></div><article class="paper">' + content + '</article><p class="foot">本地离线文档。五张扩展图按本方案逐切片实施；旧试玩和旧证据以各自版本为限。</p></main></div></body></html>'
    (ROOT / target).write_text(page, encoding='utf-8')
    print(target + ' · ' + str(len(page.encode('utf-8'))) + ' bytes')
