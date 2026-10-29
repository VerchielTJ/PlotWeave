"""Build an offline human review surface from the machine engineering index.

Standard library only. The input is the source of truth; no network requests,
original game content or author-percentage guesses are added by this renderer.
"""
from __future__ import annotations

import argparse
import html
import json
import math
from pathlib import Path


MODULES = {
    'definitions': ('类型与数据', '枚举、Bean、表头与节点资产'),
    'authoring': ('制作与编辑事务', '工作副本、校验、保存与界面'),
    'generation': ('契约与代码生成', 'Editor / Game 双端读口'),
    'exchange': ('表格与编译交换', 'Asset、六表与真实 Luban'),
    'playback': ('共享播放运行时', '节点、分支、事件与表现接口'),
    'chapters': ('章节、进度与奖励', '主线 / 日常与结算状态'),
    'integration': ('宿主接入边界', '生产传输与项目资源注入'),
}
LABELS = {
    'original-user-concept': '原设计承接',
    'migration-ai-assisted': 'AI 协作迁移',
    'new-ai-assisted-extension': '本轮协作补齐',
    'external-host-contract': '外部宿主契约',
    'unknown-needs-confirmation': '来源待确认',
    'implemented-verified': '已实现并验收',
    'verified': '已实现并验收',
    'scope-verified': '本轮指定流程通过',
    'historical-verified': '历史范围通过 · 待整体验证',
    'historical-scope-verified': '本地历史定向 · 公开副本未重验',
    'public-candidate': 'GitHub 仓库 · MIT 公开候选',
    'implemented-device-pending': '待真机验收',
    'external-injection': '外部注入',
    'not-published': '尚未发布',
    'source-pending': '来源待确认',
    'verification-not-indexed': '验收证据待登记',
}


def esc(value):
    return html.escape(str(value if value is not None else ''), quote=True)


def text(value):
    if isinstance(value, dict):
        return str(value.get('label') or value.get('name') or value.get('id') or '')
    return str(value or '')


def collect_labels(value, labels):
    if isinstance(value, list):
        for entry in value:
            collect_labels(entry, labels)
    elif isinstance(value, dict):
        if value.get('id') and (value.get('label') or value.get('name')):
            labels[str(value['id'])] = str(value.get('label') or value['name'])
        for key, child in value.items():
            if isinstance(child, str) and key not in ('id', 'name', 'description', 'path', 'label', 'note'):
                labels[key] = child
            elif isinstance(child, (dict, list)):
                if isinstance(child, dict) and (child.get('label') or child.get('name')):
                    labels[key] = str(child.get('label') or child['name'])
                collect_labels(child, labels)


def normalize(raw):
    value = dict(raw)
    labels = dict(LABELS)
    collect_labels(value.get('provenance', {}), labels)
    collect_labels(value.get('statuses', {}), labels)
    modules = value.get('modules') or []
    if isinstance(modules, dict):
        modules = [dict(v, id=k) if isinstance(v, dict) else {'id': k, 'label': v} for k, v in modules.items()]
    by_module = {text(m.get('id')): dict(m) for m in modules}
    value['modules'] = []
    for key, default in MODULES.items():
        item = by_module.get(key, {})
        value['modules'].append({'id': key, 'label': item.get('label') or item.get('name') or default[0],
                                 'summary': item.get('summary') or item.get('description') or default[1]})
    classes = value.get('classes') or value.get('nodes') or []
    if not classes:
        raise ValueError('The engineering index has no curated classes')
    ids = set()
    for item in classes:
        if not item.get('id') or item['id'] in ids:
            raise ValueError('Classes require unique nonempty IDs')
        ids.add(item['id'])
        item.setdefault('module', 'integration')
        if item['module'] not in MODULES:
            raise ValueError('Unknown class module: ' + str(item['module']))
        item.setdefault('name', item['id'])
        item.setdefault('kind', 'class')
        item.setdefault('summary', '')
        item.setdefault('designOrigin', 'unknown-needs-confirmation')
        item.setdefault('implementationOrigin', 'unknown-needs-confirmation')
        item.setdefault('status', 'verification-not-indexed')
        item['apis'] = [a if isinstance(a, dict) else {'name': a, 'signature': a} for a in item.get('apis', item.get('methods', []))]
        item.setdefault('evidence', [])
    relations = value.get('relations') or value.get('edges') or []
    for edge in relations:
        if edge.get('from') not in ids or edge.get('to') not in ids:
            raise ValueError('A relation references an unknown curated class')
        edge.setdefault('kind', 'uses')
    value['classes'], value['relations'], value['labels'] = classes, relations, labels
    value.setdefault('title', 'PlotWeave 工程评审地图')
    value.setdefault('sourceSnapshot', {})
    value.setdefault('boundaries', [])
    value.setdefault('readingOrder', [])
    return value


def short(value, count=41):
    value = str(value or '')
    return value if len(value) <= count else value[:count - 1] + '…'


def origin_short(value, axis='design'):
    labels = {
        'original-user-concept': '原版脉络' if axis == 'design' else '原实现待确认',
        'migration-ai-assisted': '迁移承接' if axis == 'design' else 'AI 协作迁移',
        'new-ai-assisted-extension': '本轮新增' if axis == 'design' else 'AI 协作补齐',
        'external-host-contract': '宿主契约',
        'unknown-needs-confirmation': '待确认',
    }
    return labels.get(value, LABELS.get(value, value))


def static_svg(data):
    """Printable module-separated class atlas; structural edges and a core focus."""
    width, y, positions = 1200, 115, {}
    body, backgrounds = [], []
    by_id = {c['id']: c for c in data['classes']}
    focus = next((c['id'] for c in data['classes'] if c['name'] == 'PlotPlayerCore'), data['classes'][0]['id'])
    for module in data['modules']:
        items = [c for c in data['classes'] if c['module'] == module['id']]
        if not items:
            continue
        rows = math.ceil(len(items) / 3)
        height = 68 + rows * 164
        backgrounds.append(f'<rect x="20" y="{y}" width="1160" height="{height}" rx="6" fill="#f5f7f6" stroke="#dce3df"/>')
        backgrounds.append(f'<text x="38" y="{y+28}" class="module">{esc(module["label"])}</text><text x="38" y="{y+49}" class="sub">{esc(module["summary"])}</text>')
        for index, item in enumerate(items):
            x, top = 38 + (index % 3) * 380, y + 65 + (index // 3) * 164
            positions[item['id']] = (x, top, 362, 145)
            focused = item['id'] == focus
            body.append(f'<g><title>{esc(item["qualifiedName"] if item.get("qualifiedName") else item["name"])}</title><rect x="{x}" y="{top}" width="362" height="145" rx="4" fill="white" stroke="{"#087e83" if focused else "#b9c9c4"}" stroke-width="{2 if focused else 1}"/>')
            body.append(f'<text x="{x+12}" y="{top+18}" class="kind">{esc(item["kind"])}</text><text x="{x+12}" y="{top+39}" class="name">{esc(short(item["name"],39))}</text>')
            body.append(f'<line x1="{x}" y1="{top+50}" x2="{x+362}" y2="{top+50}" stroke="#dce3df"/>')
            for row, api in enumerate(item['apis'][:2]):
                signature = api.get('signature') or api.get('name')
                body.append(f'<text x="{x+12}" y="{top+69+row*17}" class="api"><title>{esc(signature)}</title>{esc(short(signature,48))}</text>')
            status = data['labels'].get(item['status'], item['status'])
            design, implementation = origin_short(item['designOrigin']), origin_short(item['implementationOrigin'], 'implementation')
            body.append(f'<text x="{x+350}" y="{top+18}" class="sub" text-anchor="end"><title>功能状态：{esc(status)}</title>{esc(short(status,16))}</text>')
            body.append(f'<text x="{x+12}" y="{top+110}" class="sub"><title>{esc(data["labels"].get(item["designOrigin"],item["designOrigin"]))}</title>设计：{esc(design)}</text>')
            body.append(f'<text x="{x+12}" y="{top+131}" class="sub"><title>{esc(data["labels"].get(item["implementationOrigin"],item["implementationOrigin"]))}</title>实现：{esc(implementation)}</text></g>')
        y += height + 24
    lines = []
    for edge in data['relations']:
        structural = edge['kind'] in ('inheritance', 'inherits', 'implements')
        if not structural and focus not in (edge['from'], edge['to']):
            continue
        a, b = positions[edge['from']], positions[edge['to']]
        sx, sy, ex, ey = a[0] + a[2] / 2, a[1] + a[3], b[0] + b[2] / 2, b[1]
        if sy > ey:
            sx, sy, ex, ey = a[0] + a[2], a[1]+a[3]/2, b[0]+b[2], b[1]+b[3]/2
        middle = (sy + ey) / 2
        marker = 'triangle' if structural else 'arrow'
        dash = ' stroke-dasharray="6 5"' if edge['kind'] in ('implements', 'uses') else ''
        lines.append(f'<path d="M {sx} {sy} C {sx} {middle}, {ex} {middle}, {ex} {ey}" fill="none" stroke="#087e83" stroke-width="1.5" opacity=".65"{dash} marker-end="url(#{marker})"/>')
    caption = '类按模块分区；显示继承/实现关系及 '+by_id[focus]['name']+' 的直接依赖。完整关系在交互页面与机器索引中。'
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{y+24}" viewBox="0 0 {width} {y+24}" role="img" aria-labelledby="title desc">
<title id="title">PlotWeave 核心类图</title><desc id="desc">{esc(caption)}</desc><style>text{{font-family:Segoe UI,Microsoft YaHei,sans-serif;fill:#183430}}.module{{font-size:17px;font-weight:700}}.name{{font-size:15px;font-weight:600}}.sub{{font-size:11px;fill:#596d67}}.api{{font-family:Consolas,monospace;font-size:11px}}.kind{{font-size:10px;fill:#087e83;letter-spacing:1px}}</style>
<defs><marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto"><path d="M 1 1 L 9 5 L 1 9" fill="none" stroke="#087e83"/></marker><marker id="triangle" viewBox="0 0 12 12" refX="11" refY="6" markerWidth="9" markerHeight="9" orient="auto"><path d="M 1 1 L 11 6 L 1 11 Z" fill="white" stroke="#087e83"/></marker></defs>
<rect width="100%" height="100%" fill="#fcfcf9"/><text x="30" y="35" class="module">PLOTWEAVE / 核心类图</text><text x="30" y="60" class="sub">{esc(caption)}</text><text x="30" y="82" class="sub">实线空三角：继承；虚线空三角：实现；虚线箭头：依赖；实线箭头：产出。来源标签描述设计沿革，不表示逐行作者归属。</text>
{''.join(backgrounds)}{''.join(lines)}{''.join(body)}</svg>'''


HTML = r'''<!doctype html>
<html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>PlotWeave 工程评审</title>
<style>
:root{--ink:#173a35;--muted:#64736e;--accent:#087e83;--line:#dbe3dd;--paper:#fcfcf9;--surface:#f2f5f1;--tint:#e5f1ec}*{box-sizing:border-box}body{margin:0;background:var(--paper);color:var(--ink);font-family:Inter,"Segoe UI","Microsoft YaHei",sans-serif;font-size:14px;line-height:1.55}button,input,select{font:inherit;color:inherit}button,a,input,select{outline-offset:4px}button{border:1px solid var(--line);border-radius:5px;background:var(--paper);padding:7px 12px;cursor:pointer}button:hover{border-color:var(--accent);background:var(--surface)}button[aria-pressed=true],button.active{background:var(--accent);color:white;border-color:var(--accent)}a{color:var(--accent);text-decoration:none}a:hover{text-decoration:underline}h1,h2,h3,p{margin:0}h1{font-size:25px;letter-spacing:-.6px;font-weight:650}h2{font-size:18px;font-weight:650}h3{font-size:14px;font-weight:650}.eyebrow{font-size:11px;letter-spacing:2px;color:var(--accent);font-weight:650}.muted{color:var(--muted)}.micro{font-size:11px}.mono,code{font-family:Consolas,"Cascadia Code",monospace}.hero{padding:24px 30px 18px;border-bottom:1px solid var(--line);display:flex;justify-content:space-between;gap:24px}.hero h1{margin:6px 0}.hero p{max-width:760px;font-size:13px;color:var(--muted)}.hero-meta{min-width:230px;font-size:12px;text-align:right;display:grid;gap:5px;align-content:center}.hero-meta strong{color:var(--accent)}.layout{display:grid;grid-template-columns:220px minmax(470px,1fr) 325px;min-height:calc(100vh - 135px)}.sidebar{border-right:1px solid var(--line);padding:21px 18px}.sidebar h2{font-size:12px;letter-spacing:1px;margin-bottom:12px}.reading{display:grid;gap:4px;margin-bottom:25px}.reading button{border-color:transparent;text-align:left;display:grid;grid-template-columns:24px 1fr;gap:6px;padding:9px 7px}.reading button.active{background:var(--tint);color:var(--ink);border-color:var(--line)}.reading .step{color:var(--accent);font-size:11px;padding-top:2px}.reading small{display:block;font-size:10px;color:var(--muted)}.filters{display:grid;gap:13px}.filters label{display:grid;gap:5px;font-size:11px;color:var(--muted)}.filters input,.filters select{width:100%;min-width:0;border:1px solid var(--line);border-radius:4px;background:white;padding:8px 7px;font-size:12px;color:var(--ink)}.sidebar-note{padding-top:20px;font-size:11px;color:var(--muted);border-top:1px solid var(--line);margin-top:22px}.main{padding:20px 22px;min-width:0}.topbar{display:flex;align-items:center;justify-content:space-between;gap:12px;flex-wrap:wrap;margin-bottom:18px}.tabs{display:flex;gap:5px}.tabs button{font-size:12px}.tools{display:flex;gap:5px}.tools button{font-size:11px;padding:6px 9px}.description{display:flex;justify-content:space-between;gap:15px;margin-bottom:12px}.description p{font-size:12px;color:var(--muted);margin-top:4px}.counter{align-self:center;font-size:11px;color:var(--muted);white-space:nowrap}.scope{display:flex;gap:6px;flex-wrap:wrap;margin:10px 0 15px}.scope button{font-size:11px;padding:5px 10px}.diagram-scroll{overflow:auto;border:1px solid var(--line);border-radius:5px;background:white;max-height:720px;min-height:380px}.diagram-scroll svg{display:block;max-width:none;transform-origin:top left}.diagram-scroll svg [data-node-id],.diagram-scroll svg [data-module-id]{cursor:pointer;outline:none}.diagram-scroll svg [tabindex]:focus rect{stroke:var(--accent);stroke-width:3}.legend{display:flex;gap:18px;flex-wrap:wrap;font-size:10px;color:var(--muted);margin-top:10px}.legend i{display:inline-block;width:20px;border-top:1px solid var(--accent);vertical-align:middle;margin-right:5px}.legend .dash{border-top-style:dashed}.surface-note{font-size:11px;color:var(--muted);padding:15px 0 4px}.detail{border-left:1px solid var(--line);padding:22px 20px;background:#f8faf6;min-width:0}.detail-head{display:flex;justify-content:space-between;gap:10px}.detail h2{font-size:18px;overflow-wrap:anywhere;margin:5px 0 9px}.detail .summary{font-size:13px;margin-bottom:17px}.detail section{padding:15px 0;border-top:1px solid var(--line)}.detail h3{font-size:11px;color:var(--muted);letter-spacing:.6px;margin-bottom:8px}.facts{display:grid;gap:9px}.fact{display:grid;grid-template-columns:64px 1fr;gap:9px;font-size:11px}.fact .label{color:var(--muted)}.tag{display:inline-block;border:1px solid #bfd4cc;border-radius:3px;padding:2px 6px;background:white;font-size:10px;line-height:1.5}.tag.origin{color:var(--accent)}.path{font-size:10px;overflow-wrap:anywhere;margin-bottom:8px}.api-list,.evidence-list,.relation-list{display:grid;gap:8px;font-size:11px}.api{padding-bottom:6px;border-bottom:1px solid var(--line);overflow-wrap:anywhere}.relation-item{background:white;border:1px solid var(--line);padding:8px;border-radius:3px}.relation-item button{border:0;padding:0;color:var(--accent);font-family:Consolas,monospace;font-size:11px;text-align:left;overflow-wrap:anywhere}.relation-item small{display:block;color:var(--muted);font-size:10px;margin-top:3px}.ownership{font-size:11px;color:var(--muted)}.detail-actions{display:flex;gap:5px;flex-wrap:wrap;margin-top:10px}.detail-actions button{font-size:11px;padding:5px 8px}.boundaries{display:grid;gap:15px;padding:17px}.boundary{border-bottom:1px solid var(--line);padding:0 0 15px}.boundary:last-child{border-bottom:0}.boundary h3{font-size:14px;margin-bottom:6px}.boundary p{font-size:12px;color:var(--muted)}.boundary .tag{margin-bottom:8px}.notice{border-left:3px solid var(--accent);padding:8px 11px;background:var(--tint);font-size:11px;margin:0 0 15px;color:#35554b}.empty{padding:40px 24px;color:var(--muted);font-size:13px}.footer{border-top:1px solid var(--line);padding:14px 30px;font-size:10px;color:var(--muted);display:flex;justify-content:space-between;gap:20px}.source-paths{display:flex;gap:12px;flex-wrap:wrap}#toast{position:fixed;bottom:20px;left:50%;transform:translateX(-50%);background:var(--ink);color:white;padding:8px 15px;border-radius:5px;font-size:12px;display:none}#toast.show{display:block}
/* Keep intrinsic code/signature widths inside the review columns. */
.tabs,.tools{flex-wrap:wrap;min-width:0;max-width:100%}
.filters,.filters label,.detail section,.detail-head>div,.relation-list,.relation-item,.api-list,.api,.facts,.fact,.fact>span{min-width:0;max-width:100%}
.relation-item button{display:block;width:100%;max-width:100%;min-width:0;white-space:normal;word-break:break-word;overflow-wrap:anywhere}
.relation-item small,.detail .summary,.detail .ownership,.detail a,.fact>span,.footer>span,.source-paths{overflow-wrap:anywhere;word-break:break-word;min-width:0}
.diagram-scroll{min-width:0;width:100%}
.detail{align-self:start;position:sticky;top:12px;max-height:calc(100vh - 160px);min-height:320px;overflow-y:auto;overflow-x:hidden;scrollbar-gutter:stable}
@media(min-width:1700px){.layout{grid-template-columns:245px minmax(700px,1fr) 365px}.diagram-scroll{max-height:820px}.detail{padding:24px}.sidebar{padding:24px}}
@media(max-width:1120px){.layout{grid-template-columns:205px minmax(450px,1fr)}.detail{position:static;max-height:none;min-height:0;overflow:visible;grid-column:1/-1;border-left:0;border-top:1px solid var(--line);display:grid;grid-template-columns:1fr 1fr;gap:0 28px}.detail-head,.detail .summary{grid-column:1/-1}.hero-meta{min-width:200px}.hero{padding:20px}.main{padding:18px}}
@media(max-width:720px){.hero{display:block}.hero-meta{text-align:left;display:flex;flex-wrap:wrap;gap:10px;margin-top:13px}.layout{display:block}.sidebar{border-right:0;border-bottom:1px solid var(--line);padding:13px 18px}.reading{display:flex;overflow:auto;margin-bottom:12px;gap:2px}.reading button{min-width:155px}.reading small{display:none}.filters{grid-template-columns:1fr 1fr}.filters label:first-child{grid-column:1/-1}.sidebar-note{display:none}.detail{display:block;padding:20px}.main{padding:16px}.diagram-scroll{max-height:620px}.footer{padding:15px;display:block}.source-paths{margin-top:8px}h1{font-size:22px}.counter{white-space:normal}}
@media print{body{background:white}.hero{padding:0 0 14px}.layout{display:block}.sidebar,.detail,.tools,.scope,.tabs,.footer,#toast{display:none}.main{padding:14px 0}.diagram-scroll{border:0;max-height:none;overflow:visible;min-height:0}.diagram-scroll svg{width:100%!important;height:auto!important;transform:none!important}.legend{font-size:8px}.hero-meta{font-size:10px}.notice{background:white}.description{margin-bottom:12px}@page{size:A3 landscape;margin:14mm}}
</style></head><body>
<header class="hero"><div><div class="eyebrow">PLOTWEAVE / ENGINEERING REVIEW</div><h1 id="pageTitle">PlotWeave 工程评审地图</h1><p>从数据设计、制作事务到共享运行时，按模块阅读真实类关系。分别查看设计沿革、当前实现与验收状态。</p></div><div class="hero-meta" id="snapshot"></div></header>
<div class="layout"><aside class="sidebar" aria-label="阅读顺序与筛选"><h2>建议阅读顺序</h2><nav class="reading" id="reading"></nav><div class="filters"><label>搜索类名、接口或职责<input id="search" type="search" placeholder="例如 PlotPlayerCore" autocomplete="off"></label><label>模块<select id="moduleFilter"></select></label><label>验收 / 接入状态<select id="statusFilter"></select></label><label>设计来源<select id="originFilter"></select></label><button id="reset">重置全部筛选</button></div><div class="sidebar-note">设计沿革不等于逐行作者。原设计承接与本次 AI 协作实现可以同时成立；未知来源保留待确认。</div></aside>
<main class="main"><div class="topbar"><nav class="tabs" aria-label="视图"><button data-view="overview" aria-pressed="true">模块总览</button><button data-view="classes" aria-pressed="false">核心类图</button><button data-view="boundaries" aria-pressed="false">待办与接入边界</button></nav><div class="tools"><button id="zoomOut" aria-label="缩小图">−</button><button id="fit">适合视口</button><button id="zoomIn" aria-label="放大图">＋</button><button id="export">导出 SVG</button><button id="print">打印</button></div></div><div id="freshness"></div><div class="description"><div><h2 id="viewTitle">多条输入路径，共用播放核心</h2><p id="viewSubtitle">制作预览、Asset/JSON与Luban均可进入共享Core；C2生成是可选支路。</p></div><div class="counter" id="counter"></div></div><div class="scope" role="group" aria-label="设计来源叠层"><button data-scope="all" aria-pressed="true">全部沿革</button><button data-scope="original" aria-pressed="false">原设计承接</button><button data-scope="new" aria-pressed="false">本轮协作补齐</button></div><div class="diagram-scroll" id="diagram" tabindex="0" aria-label="可滚动工程关系图"></div><div class="legend" id="legend"><span><i></i>继承 / 产出</span><span><i class="dash"></i>实现 / 依赖</span><span>点击类：显示一跳关系</span><span>标签：设计沿革 · 实现状态</span></div><p class="surface-note">模块分图保持可读字号。完整关系和结构化来源以 <a href="engineering-index.json">engineering-index.json</a> 为准；<a href="class-diagram.svg">静态类图</a>与<a href="class-diagram.mmd">Mermaid 源文件</a>可独立保存。</p></main>
<aside class="detail" id="detail" aria-label="类与接口详情" aria-live="polite"></aside></div><footer class="footer"><span id="footerNote">所有数据离线嵌入；无联网依赖。历史本地验证不等同于公开副本验收；证据不转换为个人贡献比例。</span><div class="source-paths"><a href="engineering-index.json">机器索引 JSON</a><a href="../../AI_CONTEXT.md">AI 阅读入口</a><a href="../Records/Current/验收总览.json">历史本地验收档案</a><a href="设计来源与状态说明.md">来源说明</a></div></footer><div id="toast" role="status"></div>
<script id="engineering-data" type="application/json">__DATA__</script>
<script>
'use strict';
const D=JSON.parse(document.getElementById('engineering-data').textContent), $=s=>document.querySelector(s), byId=new Map(D.classes.map(c=>[c.id,c]));
const S={view:'overview',module:'all',status:'all',origin:'all',scope:'all',query:'',selected:D.classes.find(c=>c.name==='PlotPlayerCore')?.id||D.classes[0].id,zoom:1,neighbors:false};
const E=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const OL=(id,axis='design')=>({'original-user-concept':axis==='design'?'原版脉络':'原实现待确认','migration-ai-assisted':axis==='design'?'迁移承接':'AI 协作迁移','new-ai-assisted-extension':axis==='design'?'本轮新增':'AI 协作补齐','external-host-contract':'宿主契约','unknown-needs-confirmation':'待确认'}[id]||L(id));
const L=id=>D.labels[id]||String(id||'未确认'), MOD=id=>D.modules.find(m=>m.id===id), trim=(s,n=38)=>String(s||'').length>n?String(s).slice(0,n-1)+'…':String(s||'');
function safePath(p){p=String(p||'').replaceAll('\\','/');return /^(Packages|Assets|Doc|Tools)\//.test(p)||/^(README|AI_CONTEXT)\.md$/.test(p)?'../../'+p.split('/').map(encodeURIComponent).join('/'):null;}
function pathLink(p,label){const url=safePath(p);return url?`<a href="${E(url)}" target="_blank" rel="noopener">${E(label||p)}</a>`:`<span>${E(label||p)}</span>`;}
function toast(t){$('#toast').textContent=t;$('#toast').classList.add('show');clearTimeout(toast.timer);toast.timer=setTimeout(()=>$('#toast').classList.remove('show'),1700);}
function option(value,label){return `<option value="${E(value)}">${E(label)}</option>`;}
function init(){
 $('#pageTitle').textContent=D.title||'PlotWeave 工程评审地图';const p=D.sourceSnapshot||{};
 $('#snapshot').innerHTML=`<span>评审索引 <strong>${E(String(p.reviewDate||D.generatedAt||'').slice(0,10))}</strong></span><span>历史验收 ${E(String(p.acceptanceDate||'').slice(0,10)||'见原生报告')}</span><span>${D.classes.length} 个关键类型 · ${D.relations.length} 条有证据关系</span>`;
 $('#freshness').innerHTML=p.freshnessWarning?`<div class="notice">${E(p.freshnessWarning)}</div>`:'';
 const workflow=D.evidence?.authoringWorkflow;
 if(workflow){const guide=workflow.reports?.find(r=>r.label==='guide')?.path||'Doc/阶段19_制作输入与内嵌试跑.md',minimum=workflow.minimumViewport,comparison=workflow.sourceComparison||{};$('#freshness').innerHTML+=`<div class="notice">原本地制作流程历史定向测试：${E(workflow.passed)} 项通过，${E(workflow.failed)} 项失败；${comparison.publicPackageSourcesMatch?'随包核心源码与该历史记录一致':'随包源码差异见机器索引'}。私有原布局和数据不随公开版本分发，公开副本未重跑此流程。${minimum?`历史视口策略至少 ${E(minimum.width)}×${E(minimum.height)}，更小窗口仅作诊断。`:''} ${pathLink(guide,'查看历史范围摘要')}</div>`;}
 $('#moduleFilter').innerHTML=option('all','全部模块')+D.modules.map(m=>option(m.id,m.label)).join('');
 $('#statusFilter').innerHTML=option('all','全部状态')+[...new Set(D.classes.map(c=>c.status))].map(v=>option(v,L(v))).join('');
 $('#originFilter').innerHTML=option('all','全部设计来源')+[...new Set(D.classes.map(c=>c.designOrigin))].map(v=>option(v,L(v))).join('');
 $('#reading').innerHTML=D.modules.map((m,i)=>`<button data-module="${E(m.id)}"><span class="step">0${i+1}</span><span>${E(m.label)}<small>${E(m.summary)}</small></span></button>`).join('');
 $('#search').addEventListener('input',e=>{S.query=e.target.value;S.neighbors=false;render();});
 for(const [id,key] of [['moduleFilter','module'],['statusFilter','status'],['originFilter','origin']])$('#'+id).addEventListener('change',e=>{S[key]=e.target.value;S.neighbors=false;if(key==='module'&&S.module!=='all')S.view='classes';render();});
 document.addEventListener('click',e=>{const a=e.target.closest('[data-view],[data-module],[data-scope],[data-select]');if(!a)return;if(a.dataset.view){S.view=a.dataset.view;S.neighbors=false;}if(a.dataset.module){S.module=a.dataset.module;S.view='classes';S.neighbors=false;$('#moduleFilter').value=S.module;}if(a.dataset.scope){S.scope=a.dataset.scope;S.neighbors=false;}if(a.dataset.select){S.selected=a.dataset.select;S.module='all';S.query='';S.view='classes';S.neighbors=true;$('#moduleFilter').value='all';$('#search').value='';}render();});
 $('#diagram').addEventListener('click',e=>activateNode(e.target.closest('[data-node-id],[data-module-id]')));
 $('#diagram').addEventListener('keydown',e=>{if(e.key==='Enter'||e.key===' '){const n=e.target.closest('[data-node-id],[data-module-id]');if(n){e.preventDefault();activateNode(n);}}});
 $('#reset').onclick=()=>{Object.assign(S,{module:'all',status:'all',origin:'all',scope:'all',query:'',zoom:1,neighbors:false,view:'overview'});for(const id of ['moduleFilter','statusFilter','originFilter'])$('#'+id).value='all';$('#search').value='';render();};
 $('#zoomIn').onclick=()=>setZoom(S.zoom+.15);$('#zoomOut').onclick=()=>setZoom(S.zoom-.15);$('#fit').onclick=()=>{const svg=$('#diagram svg');if(svg){const box=svg.viewBox.baseVal;setZoom(Math.min(1,($('#diagram').clientWidth-8)/box.width));}};
 $('#print').onclick=()=>window.print();$('#export').onclick=exportSvg;
 render();
}
function activateNode(n){if(!n)return;if(n.dataset.moduleId){S.module=n.dataset.moduleId;$('#moduleFilter').value=S.module;S.view='classes';S.neighbors=false;}else{S.selected=n.dataset.nodeId;S.view='classes';}render();}
function selectedRelations(){return D.relations.filter(r=>r.from===S.selected||r.to===S.selected);}
function filtered(){const q=S.query.trim().toLowerCase(),near=new Set([S.selected,...selectedRelations().flatMap(r=>[r.from,r.to])]);return D.classes.filter(c=>(S.module==='all'||c.module===S.module)&&(S.status==='all'||c.status===S.status)&&(S.origin==='all'||c.designOrigin===S.origin)&&(S.scope==='all'||(S.scope==='original'?c.designOrigin==='original-user-concept':c.designOrigin==='new-ai-assisted-extension'))&&(!q||[c.name,c.qualifiedName,c.summary,c.path,...c.apis.map(a=>a.signature||a.name)].join(' ').toLowerCase().includes(q))&&(!S.neighbors||near.has(c.id)));}
function setZoom(z){S.zoom=Math.max(.4,Math.min(2.5,z));const svg=$('#diagram svg');if(svg){svg.style.width=(Number(svg.dataset.width)*S.zoom)+'px';svg.style.height=(Number(svg.dataset.height)*S.zoom)+'px';}}
const svgDefs=`<defs><marker id="a" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto"><path d="M 1 1 L 9 5 L 1 9" fill="none" stroke="#087e83"/></marker><marker id="t" viewBox="0 0 12 12" refX="11" refY="6" markerWidth="9" markerHeight="9" orient="auto"><path d="M 1 1 L 11 6 L 1 11 Z" fill="white" stroke="#087e83"/></marker></defs>`;
function svgWrap(content,w,h,label){return `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}" data-width="${w}" data-height="${h}" role="img" aria-label="${E(label)}"><style>text{font-family:Segoe UI,Microsoft YaHei,sans-serif;fill:#173a35}.small{font-size:11px;fill:#64736e}.name{font-size:14px;font-weight:600}.api{font-family:Consolas,monospace;font-size:11px}.module{font-size:16px;font-weight:650}.kind{font-size:10px;fill:#087e83}</style>${svgDefs}<rect width="100%" height="100%" fill="#fff"/>${content}</svg>`;}
function overview(items){
 const w=1020,parts=[],counts=new Map(D.modules.map(m=>[m.id,items.filter(c=>c.module===m.id).length]));
 // This is an explicit data-flow illustration, not an inference that code-module dependencies are mandatory execution stages.
 const routeBox=(x,y,width,height,title,subtitle,id,optional=false)=>`<g ${id?'tabindex="0" role="button" data-node-id="'+E(id)+'"':''}><rect x="${x}" y="${y}" width="${width}" height="${height}" rx="4" fill="${optional?'#fff':'#f1f7f3'}" stroke="#a5c9ba" ${optional?'stroke-dasharray="6 4"':''}/><text x="${x+12}" y="${y+22}" class="name">${E(title)}</text><text x="${x+12}" y="${y+42}" class="small">${E(subtitle)}</text></g>`;
 const line=(d,optional=false)=>`<path d="${d}" fill="none" stroke="#087e83" stroke-width="1.4" ${optional?'stroke-dasharray="6 5"':''} marker-end="url(#a)"/>`;
 parts.push(`<text x="28" y="27" class="module">多条输入路径，共用播放核心</text><text x="28" y="47" class="small">数据流示意；不是继承图，也不要求先生成 C2 契约。</text>`);
 parts.push(line('M240 101 L276 101'),line('M506 101 C533 101 530 163 555 163'),line('M506 182 L555 182'),line('M240 263 L276 263'),line('M506 263 C533 263 530 201 555 201'),line('M755 182 L797 182'),line('M424 322 L424 294',true));
 parts.push(routeBox(28,71,212,60,'作者资产 / 制作预览','PlotAuthoringProject','project'));
 parts.push(routeBox(276,71,230,60,'PlotPlaybackCompiler','AuthoringPlotValueSource 直接快照','playback-compiler'));
 parts.push(routeBox(276,152,230,60,'独立 Asset / JSON','加载已有播放快照',null));
 parts.push(routeBox(28,233,212,60,'Luban 二进制','Tables / ByteBuf 解码后的行',null));
 parts.push(routeBox(276,233,230,60,'PlotLubanTableReader','nodeAdapter 可省略，直接读行','luban-reader'));
 parts.push(routeBox(555,141,200,82,'PlotPlaybackDocument','统一的播放数据快照','document'));
 parts.push(routeBox(797,141,195,82,'PlotPlayerCore','同一播放状态机','core'));
 parts.push(routeBox(28,322,478,60,'C2 契约与双端适配器 · 可选支路','按项目接入需要生成；Luban 可通过 nodeAdapter 使用','generator',true));
 parts.push(`<text x="555" y="342" class="small">实线：直接数据路径</text><text x="555" y="364" class="small">虚线：可选生成 / 适配路径</text><line x1="28" y1="402" x2="992" y2="402" stroke="#dbe3dd"/><text x="28" y="427" class="module">代码职责分区</text><text x="28" y="447" class="small">阅读顺序不代表运行时必经顺序；点击模块查看实际类关系。</text>`);
 const rows=[['definitions','authoring'],['generation','exchange'],['playback','chapters'],['integration']];let y=470;
 for(const row of rows){row.forEach((id,i)=>{const m=MOD(id),x=28+i*500,count=counts.get(id),total=D.classes.filter(c=>c.module===id).length,optional=id==='generation';parts.push(`<g tabindex="0" role="button" data-module-id="${id}" aria-label="查看${E(m.label)}分图" opacity="${count?1:.43}"><title>${E(m.summary)}</title><rect x="${x}" y="${y}" width="464" height="107" rx="5" fill="${count?'#f5f7f3':'#f7f8f6'}" stroke="#c2d7cd" ${optional?'stroke-dasharray="6 4"':''}/><text x="${x+16}" y="${y+25}" class="kind">${String(D.modules.indexOf(m)+1).padStart(2,'0')} / ${E(id.toUpperCase())}${optional?' · 含可选生成支路':''}</text><text x="${x+16}" y="${y+51}" class="module">${E(m.label)}</text><text x="${x+16}" y="${y+73}" class="small">${E(m.summary)}</text><text x="${x+16}" y="${y+94}" class="small">匹配 ${count} / ${total} 个关键类型 · 进入分图 →</text></g>`);});y+=128;}
 return svgWrap(parts.join(''),w,y,'PlotWeave多输入播放路径与模块职责，C2契约生成是可选支路');}
function classDiagram(items){const w=1040,positions=new Map(),blocks=[],backgrounds=[];let y=22;for(const m of D.modules){const nodes=items.filter(c=>c.module===m.id);if(!nodes.length)continue;const height=61+Math.ceil(nodes.length/3)*164;backgrounds.push(`<rect x="14" y="${y}" width="1012" height="${height}" rx="5" fill="#f5f7f3" stroke="#e2e8df"/><text x="28" y="${y+27}" class="module">${E(m.label)}</text><text x="28" y="${y+46}" class="small">${E(m.summary)}</text>`);nodes.forEach((c,i)=>{const x=28+(i%3)*333,top=y+59+Math.floor(i/3)*164;positions.set(c.id,{x,y:top,w:319,h:145});const active=c.id===S.selected;blocks.push(`<g tabindex="0" role="button" data-node-id="${E(c.id)}" aria-label="${E(c.name)}，${E(L(c.designOrigin))}，${E(L(c.status))}"><title>${E(c.name+' — '+c.summary)}</title><rect x="${x}" y="${top}" width="319" height="145" rx="4" fill="white" stroke="${active?'#087e83':'#bccdc4'}" stroke-width="${active?2:1}"/><text x="${x+11}" y="${top+17}" class="kind">${E(c.kind)} ${active?' / 当前焦点':''}</text><text x="${x+11}" y="${top+38}" class="name">${E(trim(c.name,35))}</text><line x1="${x}" y1="${top+49}" x2="${x+319}" y2="${top+49}" stroke="#e0e6df"/>${c.apis.slice(0,2).map((a,j)=>`<text x="${x+11}" y="${top+68+j*17}" class="api"><title>${E(a.signature||a.name)}</title>${E(trim(a.signature||a.name,43))}</text>`).join('')}<text x="${x+308}" y="${top+17}" class="small" text-anchor="end"><title>功能状态：${E(L(c.status))}</title>${E(trim(L(c.status),16))}</text><text x="${x+11}" y="${top+110}" class="small"><title>${E(L(c.designOrigin))}</title>设计：${E(OL(c.designOrigin))}</text><text x="${x+11}" y="${top+131}" class="small"><title>${E(L(c.implementationOrigin))}</title>实现：${E(OL(c.implementationOrigin,'implementation'))}</text></g>`);});y+=height+22;}
 const lines=[];let shown=0,hidden=0;for(const r of D.relations){const structural=['inheritance','inherits','implements'].includes(r.kind),focus=r.from===S.selected||r.to===S.selected;if(!positions.has(r.from)||!positions.has(r.to)){if(focus)hidden++;continue;}if(!structural&&!focus)continue;shown++;const a=positions.get(r.from),b=positions.get(r.to);let sx=a.x+a.w/2,sy=a.y+a.h,ex=b.x+b.w/2,ey=b.y;if(Math.abs(a.y-b.y)<30){sx=a.x+a.w;sy=a.y+a.h/2;ex=b.x;ey=b.y+b.h/2;if(sx>ex){sx=a.x;ex=b.x+b.w;}}else if(sy>ey){sx=a.x+a.w;sy=a.y+a.h/2;ex=b.x+b.w;ey=b.y+b.h/2;}const mid=(sy+ey)/2;lines.push(`<path d="M${sx},${sy} C${sx},${mid} ${ex},${mid} ${ex},${ey}" fill="none" stroke="#087e83" stroke-width="1.5" opacity=".64" ${['implements','uses'].includes(r.kind)?'stroke-dasharray="6 5"':''} marker-end="url(#${structural?'t':'a'})"><title>${E(r.kind+'：'+(r.rationale||''))}</title></path>`);}
 $('#counter').textContent=`${items.length} / ${D.classes.length} 类型 · ${shown} 条图中关系${hidden?' · '+hidden+' 条焦点关系在筛选外':''}`;
 return svgWrap(backgrounds.join('')+lines.join('')+blocks.join(''),w,Math.max(300,y),'PlotWeave核心类图，点击类显示其一跳依赖');}
function boundaryRows(){let list=D.boundaries;if(!Array.isArray(list))list=Object.entries(list||{}).map(([id,v])=>typeof v==='object'?{id,...v}:{id,description:v});return list.map((b,i)=>`<article class="boundary"><span class="tag">${E(L(b.status||b.state||b.category||'external-injection'))}</span><h3>${E(b.title||b.label||b.name||b.id||'接入事项 '+(i+1))}</h3><p>${E(b.description||b.summary||b.note||'')}</p>${(b.evidence||[]).map(e=>`<p class="micro">${pathLink(typeof e==='string'?e:e.path,typeof e==='string'?e:e.label||e.path)}</p>`).join('')}</article>`).join('');}
function render(){const items=filtered();document.querySelectorAll('[data-view]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.view===S.view)));document.querySelectorAll('[data-scope]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.scope===S.scope)));document.querySelectorAll('[data-module]').forEach(b=>b.classList.toggle('active',b.dataset.module===S.module));$('#counter').textContent=`${items.length} / ${D.classes.length} 个关键类型匹配`;
 if(S.view==='boundaries'){$('#counter').textContent=D.boundaries.length+' 项接入、验证或来源边界';$('#viewTitle').textContent='已完成之外，哪些仍需宿主或设备';$('#viewSubtitle').textContent='把外部接入、真机验收和发布状态与核心实现完成情况分开。';$('#diagram').innerHTML=`<div class="boundaries">${boundaryRows()||'<p class="muted">索引未列出额外边界，请查看验收档案。</p>'}</div>`;$('#legend').hidden=true;}
 else{$('#legend').hidden=false;$('#legend').innerHTML=S.view==='overview'?'<span><i></i>直接数据路径</span><span><i class="dash"></i>可选生成 / 适配支路</span><span>职责分区不表示必经执行顺序</span>':'<span><i></i>继承 / 产出</span><span><i class="dash"></i>实现 / 依赖</span><span>点击类：显示一跳关系</span><span>标签：设计沿革 · 实现状态</span>';$('#viewTitle').textContent=S.view==='overview'?'多条输入路径，共用播放核心':(S.module==='all'?'核心类图 · 按模块分区':MOD(S.module).label+' · 模块分图');$('#viewSubtitle').textContent=S.view==='overview'?'制作预览、Asset/JSON与Luban均可进入共享Core；C2生成是可选支路。':(S.neighbors?'当前展开焦点类的一跳关联，仍保留来源与状态筛选。':'默认保留继承 / 实现关系；选中类后显示其直接依赖，避免连线遮挡。');$('#diagram').innerHTML=items.length?(S.view==='overview'?overview(items):classDiagram(items)):'<div class="empty">当前筛选没有匹配类型。可清空搜索或重置筛选；接入与真机事项在“待办与接入边界”。</div>';setZoom(S.zoom);}
 detail();}
function detail(){const c=byId.get(S.selected);if(!c)return;const edges=selectedRelations();const evidence=Array.isArray(c.evidence)?c.evidence:[];$('#detail').innerHTML=`<div class="detail-head"><div><div class="eyebrow">TYPE / ${E(c.kind)}</div><h2>${E(c.name)}</h2><div class="muted micro">${E(MOD(c.module)?.label||c.module)}</div></div></div><p class="summary">${E(c.summary)}</p><section><h3>来源与完成状态 · 两条独立轴</h3><div class="facts"><div class="fact"><span class="label">设计沿革</span><span><span class="tag origin">${E(L(c.designOrigin))}</span></span></div><div class="fact"><span class="label">当前实现</span><span>${E(L(c.implementationOrigin))}</span></div><div class="fact"><span class="label">状态</span><span class="tag">${E(L(c.status))}</span></div></div>${c.ownershipNote?`<p class="ownership" style="margin-top:10px">${E(c.ownershipNote)}</p>`:''}</section><section><h3>真实源码位置</h3><div class="path mono">${pathLink(c.path)}${c.line?':'+E(c.line):''}</div><div class="micro muted">${E(c.qualifiedName||c.name)}</div><div class="detail-actions"><button id="copyPath">复制位置</button><button id="showNeighbors">展开直接关联</button></div></section>${c.apis.length?`<section><h3>关键 API</h3><div class="api-list">${c.apis.map(a=>`<div class="api mono">${E(a.signature||a.name)}${a.line?'<span class="muted"> · L'+E(a.line)+'</span>':''}</div>`).join('')}</div></section>`:''}${edges.length?`<section><h3>直接关系 · ${edges.length}</h3><div class="relation-list">${edges.map(r=>{const id=r.from===c.id?r.to:r.from,target=byId.get(id);return `<div class="relation-item"><span class="micro muted">${E(r.kind)} · ${r.from===c.id?'指向':'来自'}</span><br><button data-select="${E(id)}">${E(target.name)}</button><small>${E(r.rationale||'')}</small>${(r.evidence||[]).slice(0,1).map(e=>`<small>${E(e.path||e)}${e.line?':'+E(e.line):''}</small>`).join('')}</div>`;}).join('')}</div></section>`:''}${evidence.length?`<section><h3>证据与验证入口</h3><div class="evidence-list">${evidence.map(e=>{const p=typeof e==='string'?e:e.path;return `<div>${pathLink(p,typeof e==='string'?e:e.label||p)}</div>`;}).join('')}</div></section>`:''}`;
 $('#copyPath').onclick=async()=>{const value=c.path+(c.line?':'+c.line:'');try{await navigator.clipboard.writeText(value);toast('已复制源码位置');}catch{const area=document.createElement('textarea');area.value=value;document.body.append(area);area.select();const ok=document.execCommand('copy');area.remove();toast(ok?'已复制源码位置':'请直接选择上方路径复制');}};
 $('#showNeighbors').onclick=()=>{S.module='all';S.query='';S.neighbors=true;S.view='classes';$('#moduleFilter').value='all';$('#search').value='';render();};}
function exportSvg(){const svg=$('#diagram svg');if(!svg){toast('请先切换到模块图或类图');return;}const clone=svg.cloneNode(true);clone.removeAttribute('style');const blob=new Blob([new XMLSerializer().serializeToString(clone)],{type:'image/svg+xml;charset=utf-8'}),url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=S.view==='overview'?'PlotWeave-module-map.svg':'PlotWeave-class-diagram.svg';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}
init();
</script></body></html>'''


def mermaid(data):
    ids = {c['id']: 'C' + str(i) for i, c in enumerate(data['classes'])}
    lines = ['classDiagram', 'direction LR', '%% Generated from engineering-index.json; not an ownership percentage.']
    for c in data['classes']:
        safe_name = c['name'].replace('"', '').replace('\n', ' ')
        lines += [f'class {ids[c["id"]]}["{safe_name}"] {{']
        if c['kind'] == 'interface':
            lines.append('  <<interface>>')
        for api in c['apis'][:3]:
            name = str(api.get('name') or '').split('(')[0]
            if name and all(ch.isalnum() or ch in '_.' for ch in name):
                lines.append('  +' + name + '()')
        lines.append('}')
    for edge in data['relations']:
        arrow = {'inheritance': '--|>', 'inherits': '--|>', 'implements': '..|>', 'uses': '..>', 'produces': '-->'}.get(edge['kind'], '..>')
        lines.append(f'{ids[edge["from"]]} {arrow} {ids[edge["to"]]} : {edge["kind"]}')
    return '\n'.join(lines) + '\n'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--input', type=Path)
    args = parser.parse_args()
    source = args.input or args.root / 'Doc/Review/engineering-index.json'
    data = normalize(json.loads(source.read_text(encoding='utf-8-sig')))
    output = args.root / 'Doc/Review'
    output.mkdir(parents=True, exist_ok=True)
    payload = json.dumps(data, ensure_ascii=False, separators=(',', ':')).replace('<', '\\u003c').replace('>', '\\u003e').replace('&', '\\u0026')
    outputs = {'index.html': HTML.replace('__DATA__', payload), 'class-diagram.svg': static_svg(data), 'class-diagram.mmd': mermaid(data)}
    for name, content in outputs.items():
        (output / name).write_text(content, encoding='utf-8')
    print(json.dumps({'classes': len(data['classes']), 'relations': len(data['relations']), 'outputs': [str(output / name) for name in outputs], 'networkDependencies': False}, ensure_ascii=False))


if __name__ == '__main__':
    main()
