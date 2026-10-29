"""Read-only bounded JSON interface for humans and AI. No Unity or network required."""
from __future__ import annotations
import argparse, hashlib, json, sys
from pathlib import Path
from build_index import source_evidence

def run():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--project',type=Path,default=Path(__file__).resolve().parents[2])
    p.add_argument('--limit',type=int,default=12,help='Maximum returned records (1..50)')
    sub=p.add_subparsers(dest='command',required=True)
    sub.add_parser('summary');sub.add_parser('status');sub.add_parser('verify')
    f=sub.add_parser('find');f.add_argument('query')
    f=sub.add_parser('show');f.add_argument('type')
    f=sub.add_parser('module');f.add_argument('id')
    f=sub.add_parser('relations');f.add_argument('type')
    args=p.parse_args();root=args.project.resolve();limit=max(1,min(args.limit,50))
    read=lambda name:json.loads((root/name).read_text(encoding='utf-8-sig'))
    data=read('Doc/Review/engineering-index.json');classes=data['classes']
    brief=lambda c:{k:c.get(k) for k in ('id','name','kind','module','path','line','summary','designOrigin','implementationOrigin','status')}
    if args.command=='summary':
        out={k:data[k] for k in ('title','sourceSnapshot','inventory','readingOrder','evidence')}
        out['modules']=data['modules'];out['provenanceNotice']=data['provenance']['notice'];out['scopeNotes']=data.get('scopeNotes',[])
    elif args.command=='status':out={'sourceSnapshot':data['sourceSnapshot'],'statuses':data['statuses'],'boundaries':data['boundaries'],'evidence':data['evidence'],'scopeNotes':data.get('scopeNotes',[])}
    elif args.command=='verify':
        proof=read('Doc/Records/Current/验收总览.json');expected={e['path']:e['sha256'].lower() for e in proof.get('verifiedSources',[])}
        comparison=source_evidence(root,proof.get('verifiedSources',[]))
        changed=comparison['changedSources']
        package=root/'Packages/com.plotweave.core'
        sources=sorted(f for f in package.rglob('*') if f.is_file() and (f.suffix in ('.cs','.asmdef') or f.name=='link.xml'))
        additions=[f.relative_to(root).as_posix() for f in sources if f.relative_to(root).as_posix() not in expected]
        fingerprint=hashlib.sha256('\n'.join(f.relative_to(root).as_posix()+':'+hashlib.sha256(f.read_bytes()).hexdigest() for f in sources).encode()).hexdigest()
        fresh=fingerprint==data['sourceSnapshot']['packageFingerprint']
        metadata=[]
        for name,key in [('architecture-curation.json','curationSha256'),('provenance.json','provenanceSha256')]:
            path=root/'Doc/Review'/name
            if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest()!=data['sourceSnapshot'].get(key):metadata.append(name)
        if hashlib.sha256((root/'Doc/Records/Current/验收总览.json').read_bytes()).hexdigest()!=data['sourceSnapshot'].get('acceptanceSha256'):metadata.append('Doc/Records/Current/验收总览.json')
        index_ok=bool(not metadata and fresh)
        okay=bool(comparison['completeSourceSetMatches'] and not additions and index_ok and expected and proof.get('failed')==0 and proof.get('status')=='verified-with-explicit-boundaries')
        out={'status':'matched' if okay else 'historical-input-set-not-fully-distributed',
             'indexStatus':'matched' if index_ok else 'stale',
             'indexMatchesPackage':fresh,'indexInputsMatch':not metadata,'changedIndexInputs':metadata,
             'acceptanceDate':proof.get('dateAsiaHongKong'),'changedSources':changed,
             'unrecordedPackageSources':additions,'historicalSourceComparison':comparison,
             'runtimeNativeTestsRerun':False,
             'note':'Hash comparison only. Index freshness, changed code, excluded private inputs and missing UPM cache sources are separate facts. Historical local Unity tests were not rerun by this command.'}
        print(json.dumps(out,ensure_ascii=False,indent=2));return 0 if okay else 2
    elif args.command=='find':
        q=args.query.casefold();found=[c for c in classes if q in ' '.join(str(c.get(k,'')) for k in ('id','name','qualifiedName','summary','module')).casefold()]
        inv=read('Doc/Review/source-inventory.json')
        symbols=[s for s in inv['types'] if q in (s['name']+' '+s['namespace']+' '+s['path']).casefold()]
        out={'curatedTotal':len(found),'curated':[brief(c) for c in found[:limit]],'inventoryTotal':len(symbols),'inventory':symbols[:limit],'limit':limit}
    elif args.command=='module':
        found=[brief(c) for c in classes if c['module']==args.id]
        out={'module':args.id,'total':len(found),'classes':found[:limit],'limit':limit}
    else:
        q=args.type.casefold();found=[c for c in classes if q in (c['id'].casefold(),c['name'].casefold(),c.get('qualifiedName','').casefold())]
        if len(found)!=1:out={'error':'Type must match exactly one curated type','matches':[brief(c) for c in found[:limit]],'hint':'Use find first.'}
        else:
            c=found[0];edges=[e for e in data['relations'] if c['id'] in (e['from'],e['to'])]
            out={'class':c,'relations':edges[:limit],'relationTotal':len(edges),'limit':limit} if args.command=='show' else {'type':c['id'],'relations':edges[:limit],'total':len(edges),'limit':limit}
    print(json.dumps(out,ensure_ascii=False,indent=2));return 2 if 'error' in out else 0

if __name__=='__main__':
    try:sys.exit(run())
    except (OSError,ValueError,KeyError) as error:
        print(json.dumps({'error':str(error),'hint':'Read AI_CONTEXT.md or regenerate the review index.'},ensure_ascii=False),file=sys.stderr);sys.exit(2)
