"""Build review metadata from saved source and evidence. Does not run or alter Unity.

Curated relationships are reviewed assertions, not guesses from identifier frequency.
The complete type inventory is lexical navigation, not a C# semantic model.
"""
from __future__ import annotations
import argparse, datetime, hashlib, json, re
from pathlib import Path

MODULES = [
    ('definitions','类型与数据','枚举、Bean、表头、节点与文本的作者数据'),
    ('authoring','制作与编辑事务','节点制作、工作副本、控件、草稿和保存'),
    ('generation','共同契约与代码生成','Editor/Game适配器与有界嵌套类型生成'),
    ('exchange','表格与Luban','六表交换、严格往返与真实二进制编译'),
    ('playback','共享播放核心','播放状态、分支、事件、视图与资源服务'),
    ('chapters','章节与奖励','目录、解锁、进度、结算及返回'),
    ('integration','宿主接入边界','原布局兼容、生产传输和演示宿主'),
]
ORIGINS = {
    'original-user-concept': '原版设计脉络',
    'migration-ai-assisted': '本次AI协作迁移／重写',
    'new-ai-assisted-extension': '本次AI协作新增／补齐',
    'external-host-contract': '外部宿主契约',
    'unknown-needs-confirmation': '来源待本人确认',
}
STATUSES = {
    'verified': '已实现 · 范围验收通过',
    'external-injection': '接口已实现 · 生产接入待注入',
    'device-pending': '构建通过 · 真机待验收',
    'not-published': '公开提交状态待核对',
    'public-candidate': 'GitHub 仓库 · MIT 公开候选',
    'origin-pending': '仅来源待本人确认',
    'unverified': '缺少当前证据',
    'scope-verified': '本轮指定流程通过',
    'historical-verified': '历史范围通过 · 待整体验证',
    'historical-scope-verified': '本地历史定向证据 · 公开副本未重验',
}
PACKAGE = 'Packages/com.plotweave.core'
ACCEPTANCE = 'Doc/Records/Current/验收总览.json'
AUTHORING_WORKFLOW = 'Doc/Records/Current/制作闭环验收.json'
REPOSITORY = 'https://github.com/VerchielTJ/PlotWeave'

def source_evidence(project, sources):
    """Compare available files; distinguish distribution boundaries from changes."""
    result = {'recordedSourceCount': len(sources), 'matchedSourceCount': 0,
              'changedSources': [], 'excludedPrivateSources': [],
              'missingExternalDependencies': [], 'missingPublicSources': [],
              'invalidSourceEntries': 0}
    package_recorded = set()
    for entry in sources:
        name, digest = (entry.get('path'), entry.get('sha256')) if isinstance(entry, dict) else (None, None)
        if not isinstance(name, str) or not isinstance(digest, str) or not re.fullmatch(r'[a-fA-F0-9]{64}', digest):
            result['invalidSourceEntries'] += 1
            continue
        try:
            path = safe(project, name)
        except (ValueError, OSError):
            result['invalidSourceEntries'] += 1
            continue
        if name.startswith(PACKAGE + '/'):
            package_recorded.add(name)
        if path.is_file():
            if sha(path) == digest.lower():
                result['matchedSourceCount'] += 1
            else:
                result['changedSources'].append(name)
        elif name.startswith(('Assets/PlotWeaveLocal/', 'Tools/PlotWeaveMigration/Inputs/')):
            result['excludedPrivateSources'].append(name)
        elif name.startswith('Packages/') and not name.startswith(PACKAGE + '/'):
            result['missingExternalDependencies'].append(name)
        else:
            result['missingPublicSources'].append(name)
    package_sources = [p.relative_to(project).as_posix() for p in (project / PACKAGE).rglob('*')
                       if p.is_file() and (p.suffix in ('.cs', '.asmdef') or p.name == 'link.xml')]
    result['unrecordedPackageSources'] = sorted(set(package_sources) - package_recorded)
    result['publicPackageSourcesMatch'] = bool(package_recorded and not result['invalidSourceEntries']
        and not result['unrecordedPackageSources']
        and not any(p.startswith(PACKAGE + '/') for p in result['changedSources'] + result['missingPublicSources']))
    result['availableSourcesMatch'] = bool(sources and not result['changedSources']
        and not result['missingPublicSources'] and not result['invalidSourceEntries'])
    result['completeSourceSetMatches'] = bool(result['availableSourcesMatch']
        and not result['excludedPrivateSources'] and not result['missingExternalDependencies'])
    return result

def rendered_evidence(project, evidence):
    """Only real distributed files become links; historical omissions remain notes."""
    if isinstance(evidence, (str, dict)):
        evidence = [evidence]
    result = []
    for item in evidence or []:
        item = {'path': item} if isinstance(item, str) else item
        if not isinstance(item, dict) or not isinstance(item.get('path'), str):
            continue
        try:
            path = safe(project, item['path'])
        except (ValueError, OSError):
            continue
        if path.is_file():
            result.append(item)
    return result

def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def safe(root, name):
    candidate = (root / name).resolve()
    if not candidate.is_relative_to(root.resolve()):
        raise ValueError('Path escapes project: ' + str(name))
    return candidate

def authoring_workflow_evidence(project):
    """Summarise optional scoped evidence; never promote it to full acceptance."""
    path = project / AUTHORING_WORKFLOW
    if not path.is_file():
        return None
    proof = read(path)
    if not isinstance(proof, dict):
        raise ValueError('Authoring workflow evidence must be an object')
    sources = proof.get('verifiedSources', [])
    if not isinstance(sources, list):
        raise ValueError('Authoring workflow verifiedSources must be a list')
    comparison = source_evidence(project, sources)
    reports = proof.get('reports', [])
    if isinstance(reports, dict):
        reports = [{'label': key, 'path': value} for key, value in reports.items()]
    if not isinstance(reports, list):
        raise ValueError('Authoring workflow reports must be a list or object')
    compact_reports = []
    for report in reports[:6]:
        report = {'path': report} if isinstance(report, str) else report
        if not isinstance(report, dict) or not isinstance(report.get('path'), str):
            raise ValueError('Authoring workflow report must contain a path')
        report_path = report['path']
        if not safe(project, report_path).is_file():
            continue
        if len(report_path) > 256:
            raise ValueError('Authoring workflow report path is too long')
        compact_reports.append({'path': report_path, 'label': str(report.get('label', ''))[:120]})
    count = lambda key: proof.get(key) if type(proof.get(key)) is int and proof[key] >= 0 else None
    return {
        'record': AUTHORING_WORKFLOW,
        'scope': str(proof.get('scope', ''))[:240],
        'status': str(proof.get('status', ''))[:80],
        'date': str(proof.get('dateAsiaHongKong', proof.get('date', '')))[:40],
        'reports': compact_reports,
        'testRunId': str(proof.get('testRunId', ''))[:80],
        'passed': count('passed'), 'failed': count('failed'),
        'verifiedSourcesMatch': comparison['completeSourceSetMatches'],
        'verifiedSourceCount': len(sources),
        'sourceComparison': comparison,
        'evidenceOrigin': 'historical-local-source-project',
        'runtimeNativeTestsRerun': False,
        'coveredTypes': [name[:160] for name in proof.get('coveredTypes', [])[:20] if isinstance(name, str)],
        'minimumViewport': proof.get('minimumViewport'),
        'supportedSizeCount': proof.get('supportedSizeCount'),
        'allSizesPassed': proof.get('allSizesPassed'),
        'sourceMatchScope': '40项为原本地工程历史定向结果。仅比较当前随包文件的SHA；私有原数据与UPM依赖未随包分发，公开副本未重跑该历史流程。',
    }

def mask_csharp(text):
    # Preserve offsets/lines, skipping comments and ordinary/verbatim/interpolated strings.
    pattern = r'//[^\r\n]*|/\*[\s\S]*?\*/|\$?@"(?:""|[^"])*"|@\$"(?:""|[^"])*"|\$?"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\''
    return re.sub(pattern, lambda m: ''.join('\n' if c == '\n' else ' ' for c in m.group()), text)

def inventory(project):
    files, types = [], []
    for path in sorted((project / PACKAGE).rglob('*.cs')):
        relative = path.relative_to(project).as_posix()
        source = path.read_text(encoding='utf-8-sig')
        masked = mask_csharp(source)
        spaces = [(m.start(),m.group(1)) for m in re.finditer(r'\bnamespace\s+([\w.]+)',masked)]
        entries = []
        for match in re.finditer(r'\b(class|interface|struct|enum)\s+(@?\w+)',masked):
            ns = next((name for pos,name in reversed(spaces) if pos < match.start()),'')
            line = masked.count('\n',0,match.start())+1
            entry = {'id':relative+':'+str(line), 'name':match.group(2).lstrip('@'),
                     'kind':match.group(1),'namespace':ns,'path':relative,'line':line}
            entries.append(entry); types.append(entry)
        files.append({'path':relative,'sha256':sha(path),'lines':len(source.splitlines()),
                      'area':relative[len(PACKAGE)+1:].split('/')[0], 'types':entries})
    return {'schemaVersion':'1.0','extraction':'lexical-declarations; no semantic binding; nested owners and preprocessor branches are not resolved',
            'scope':PACKAGE,'files':files,'types':types}

def build(project, output):
    review = output / 'Doc/Review'
    curated = read(review / 'architecture-curation.json')
    provenance = read(review / 'provenance.json')
    proof = read(project / ACCEPTANCE)
    inv = inventory(project)
    comparison = source_evidence(project, proof.get('verifiedSources', []))
    changed = comparison['changedSources']
    accepted = {}
    for entry in proof.get('verifiedSources',[]):
        accepted[entry['path']] = entry['sha256'].lower()
    package_sources = sorted(p for p in (project / PACKAGE).rglob('*') if p.is_file() and (p.suffix in ('.cs','.asmdef') or p.name=='link.xml'))
    unrecorded = [p.relative_to(project).as_posix() for p in package_sources if p.relative_to(project).as_posix() not in accepted]
    # The public distribution is a different input set. Historical local tests do
    # not become a fresh public-clone acceptance even if all available hashes match.
    current = False
    fingerprint = hashlib.sha256('\n'.join(p.relative_to(project).as_posix()+':'+sha(p) for p in package_sources).encode()).hexdigest()
    entries = provenance.get('modules', provenance.get('entries', []))
    if isinstance(entries,dict): entries = [dict(v,id=k) for k,v in entries.items()]
    origins = {}
    for item in entries:
        origins.setdefault(item.get('module',item.get('id')),[]).append(item)
    classes = curated.get('classes',curated.get('nodes',[]))
    if not classes: raise ValueError('No curated types')
    class_ids = set()
    for item in classes:
        if item['id'] in class_ids: raise ValueError('Duplicate curated type '+item['id'])
        class_ids.add(item['id'])
        if item['module'] not in {m[0] for m in MODULES}: raise ValueError('Unknown module '+item['module'])
        path = safe(project,item['path'])
        content = path.read_text(encoding='utf-8-sig')
        lineno = int(item['line'])
        # Match a real declaration, not a usage or comment. Also validates line anchors.
        line = mask_csharp(content).splitlines()[lineno-1]
        if not re.search(r'\b(?:class|interface|struct|enum)\s+'+re.escape(item['name'])+r'\b',line):
            raise ValueError('Declaration anchor mismatch '+item['id']+' '+item['path']+':'+str(lineno))
        item.setdefault('qualifiedName',item['name'])
        module_origins = origins.get(item['module'],[])
        relevant = next((o for o in module_origins if item['id'] in o.get('classIds',[]) or item['name'] in o.get('types',[])),None)
        relevant = relevant or (module_origins[0] if len(module_origins)==1 else None)
        features=[f for entry in module_origins for f in entry.get('features',[])
                  if item['id'] in f.get('classIds',[]) or item['name'] in f.get('types',[])]
        matching=features or ([relevant] if relevant else [])
        source_entry=matching[0] if matching else {}
        original = source_entry.get('originalConcept',{})
        implementation = source_entry.get('currentImplementation',{})
        if not isinstance(original,dict): original = {'classification':original}
        if not isinstance(implementation,dict): implementation = {'classification':implementation}
        design = item.get('designOrigin') or source_entry.get('designOrigin') or original.get('classification')
        implementation_origins=list(dict.fromkeys(f.get('implementationOrigin') for f in matching if f.get('implementationOrigin') in ORIGINS))
        current_origin = item.get('implementationOrigin') or ('new-ai-assisted-extension' if 'new-ai-assisted-extension' in implementation_origins else source_entry.get('implementationOrigin')) or implementation.get('classification')
        item['designOrigin'] = design if design in ORIGINS else 'unknown-needs-confirmation'
        item['implementationOrigin'] = current_origin if current_origin in ORIGINS else 'migration-ai-assisted'
        item['implementationOrigins']=implementation_origins or [item['implementationOrigin']]
        needs_confirmation=not matching or any(f.get('needsAuthorConfirmation',False) for f in matching) or item['designOrigin']=='unknown-needs-confirmation'
        item['ownershipNote'] = item.get('ownershipNote') or ' '.join(v for v in [original.get('summary'),implementation.get('summary'),
            '来源待本人确认；这不表示当前功能未实现。' if needs_confirmation else '',
            '标签表示设计沿革及本次协作，不认定逐类作者或贡献比例。'] if v)
        item['provenanceEvidence']={'needsAuthorConfirmation':needs_confirmation,'source':'Doc/Review/provenance.json',
            'features':[{'id':f.get('id'),'title':f.get('title'),'designOrigin':f.get('designOrigin'),
                'implementationOrigin':f.get('implementationOrigin'),
                'originalConcept':f.get('originalConcept',{}).get('summary'),
                'currentImplementation':f.get('currentImplementation',{}).get('summary')} for f in matching]}
        item.setdefault('status','external-injection' if item['name'] in ('IPlotLegacyStoryTransport','PlotLegacyNetworkChapterBackend') else 'verified' if current else 'unverified')
        if not current and item['status']=='verified': item['status']='unverified'
        item.setdefault('implementationStatus','implemented')
        item['verificationScope'] = '公开副本未重新执行历史验收；源码变化与未分发的私有/外部依赖分别记录。'
        item['sourceSha256'] = sha(path)
        item.setdefault('apis',item.get('methods',item.get('responsibilities',[])))
        item['apis'] = [{'name':v,'signature':v} if isinstance(v,str) else v for v in item['apis']]
        evidence=item.get('evidence',[])
        if isinstance(evidence,str): evidence=[{'path':item['path'],'line':lineno,'label':evidence}]
        elif isinstance(evidence,dict): evidence=[evidence]
        item['evidence']=rendered_evidence(project, evidence)
        item['evidence'].append({'path':ACCEPTANCE,'label':'原本地工程10月28日历史范围记录；不代表公开副本重验'})
    relations = curated.get('relations',curated.get('edges',[]))
    for edge in relations:
        if edge['from'] not in class_ids or edge['to'] not in class_ids: raise ValueError('Dangling curated relation '+str(edge))
        if edge.get('kind') not in ('inherits','inheritance','implements','uses','produces','contains','adapts','calls'):
            raise ValueError('Unrecognized relationship kind')
        evidence=edge.get('evidence',[])
        if isinstance(evidence,str): evidence=[{'path':edge.get('path',''),'line':edge.get('line'),'label':evidence}]
        elif isinstance(evidence,dict): evidence=[evidence]
        edge['evidence']=rendered_evidence(project, evidence)
        for entry in edge['evidence']:
            if isinstance(entry,dict) and entry.get('path'):
                ref=safe(project,entry['path'])
                if not ref.is_file(): raise ValueError('Missing relationship evidence '+entry['path'])
                if entry.get('line') and not 0<int(entry['line'])<=len(ref.read_text(encoding='utf-8-sig').splitlines()): raise ValueError('Relationship line out of bounds')
    date = datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=8)))
    data = {'schemaVersion':'1.0','title':'PlotWeave 工程评审导航','generatedAt':date.isoformat(),
        'sourceSnapshot':{'packageFingerprint':fingerprint,'currentAcceptance':ACCEPTANCE,
            'curationSha256':sha(review/'architecture-curation.json'),'provenanceSha256':sha(review/'provenance.json'),'acceptanceSha256':sha(project/ACCEPTANCE),
            'acceptanceDate':proof.get('dateAsiaHongKong'),'reviewDate':date.date().isoformat(),
            'acceptedSourceMatches':current,'changedSources':changed,'unrecordedPackageSources':unrecorded,
            'historicalSourceComparison':comparison,
            'distribution':'public-candidate', 'runtimeNativeTestsRerun':False,
            'freshnessWarning':'此为MIT公开候选副本。277项与40项均为原本地工程历史记录；私有剧情/美术/场景及UPM缓存未分发。源码变化与缺失依赖分列，不把公开副本视为已重跑全量验收。'},
        'modules':[{'id':i,'name':n,'summary':s} for i,n,s in MODULES],
        'classes':classes,'relations':relations,
        'provenance':{'labels':ORIGINS,'source':'Doc/Review/provenance.json','modules':entries,
            'notice':'设计来源与当前实现分别记录。用户对原项目80%本人/20%AI的概述不换算为类、文件或行数比例。'},
        'statuses':[{'id':k,'label':v} for k,v in STATUSES.items()],
        'evidence':{'evidenceOrigin':'historical-local-source-project','runtimeNativeTestsRerun':False,
            'acceptance':ACCEPTANCE,'testRunId':proof.get('testRunId'),'passed':proof.get('passed'),
            'failed':proof.get('failed'),'builds':{k:{'status':v.get('status'),'target':v.get('target'),'sceneCount':len(v.get('scenes',[])),'report':v.get('report'),'deviceRunVerified':v.get('deviceRunVerified',False)} for k,v in proof.get('builds',{}).items()},
            'originalMapping':'Doc/Records/Current/源码对应索引.json','originalPlaceholders':'Doc/Records/Current/原空方法逐项结果.json'},
        'readingOrder':[
            {'title':'1 · 类型如何成为作者数据','modules':['definitions','authoring'],'question':'动态类型、表头与编辑事务如何保持一致？'},
            {'title':'2 · 作者数据如何进入游戏','modules':['generation','exchange'],'question':'共同契约、六表与真实Luban怎样避免语义漂移？'},
            {'title':'3 · 播放与业务如何解耦','modules':['playback','chapters'],'question':'共享Core、视图、章节结算各承担什么职责？'},
            {'title':'4 · 看证据和接入边界','modules':['integration'],'question':'已验收什么；哪些依赖生产宿主、设备或发布流程？'}],
        'boundaries':[
            {'id':'transport','module':'integration','status':'external-injection','title':'生产传输与道具／区域数据','description':'独立接口与受控测试已实现；原生产服务器、原道具库和区域权限未连接。'},
            {'id':'devices','module':'integration','status':'device-pending','title':'Android／iOS真机','description':'Windows及Android构建通过；Android/iOS设备运行未验收，iOS构建也未完成验证。'},
            {'id':'release','module':'integration','status':'public-candidate','title':'GitHub仓库与MIT许可','description':'已创建 '+REPOSITORY+'；自有代码采用MIT，第三方保持原许可。本副本为公开提交候选，实际同步以仓库提交记录为准。历史9场景构建含本地原内容；公开示例使用合成内容。'},
            {'id':'public-validation','module':'integration','status':'unverified','title':'公开副本验证范围','description':'40项定向与277项全量是原本地工程历史记录，公开副本尚未重跑这些流程。仅保留分发代码的逐文件SHA检查；未分发的私有源与UPM缓存不计为代码修改。'},
            {'id':'attribution','status':'origin-pending','title':'设计来源确认','description':'原设计脉络有原模块和用户陈述支持；具体决策、原AI参与范围及逐类作者归属仍由本人确认。这不表示当前功能未实现。'}],
        'scopeNotes':curated.get('limits',[]),
        'inventory':{'path':'Doc/Review/source-inventory.json','sourceFiles':len(inv['files']),'declaredTypes':len(inv['types']),
            'curatedTypes':len(classes),'notice':'主图为精选类型；完整索引为词法导航，含历史原型与测试，不是完成率或代码覆盖率。'}}
    workflow = authoring_workflow_evidence(project)
    if workflow is not None:
        data['evidence']['authoringWorkflow'] = workflow
    covered = set(workflow.get('coveredTypes', [])) if workflow and workflow['status'] == 'verified-targeted-scope' and workflow['failed'] == 0 and workflow['passed'] else set()
    workflow_sources = {e['path']:e['sha256'].lower() for e in read(project / AUTHORING_WORKFLOW).get('verifiedSources', [])} if workflow else {}
    for item in classes:
        if item['qualifiedName'] in covered and workflow_sources.get(item['path']) == item['sourceSha256']:
            item['status'] = 'historical-scope-verified'
            item['verificationScope'] = '该文件与原本地40项定向记录一致；所依赖的原布局/数据不随包分发，公开副本未重跑该历史流程。'
            item['evidence'].append({'path': AUTHORING_WORKFLOW, 'label': '原本地工程40项定向记录；本文件SHA一致'})
        elif item['status'] == 'unverified' and accepted.get(item['path']) == item['sourceSha256']:
            item['status'] = 'historical-verified'
            item['verificationScope'] = '该文件与原本地10月28日历史范围记录一致；公开输入集不同，未重新执行该全量回归。'
    review.mkdir(parents=True,exist_ok=True)
    for name,value in [('source-inventory.json',inv),('engineering-index.json',data)]:
        (review/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'classes':len(classes),'relations':len(relations),'inventoryTypes':len(inv['types']),'sourceMatchesAcceptance':current},ensure_ascii=False))

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project',type=Path,default=Path(__file__).resolve().parents[2])
    parser.add_argument('--output-root',type=Path)
    args=parser.parse_args()
    build(args.project.resolve(),(args.output_root or args.project).resolve())
