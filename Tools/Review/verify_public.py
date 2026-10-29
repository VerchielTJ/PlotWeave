"""Check the public checkout without starting Unity or using the network.

The result is a distribution check, not a runtime acceptance or secret audit.
"""
from __future__ import annotations
import argparse, hashlib, json, re, subprocess
from pathlib import Path

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project',type=Path,default=Path(__file__).resolve().parents[2])
    args=parser.parse_args(); root=args.project.resolve()
    result=subprocess.run(['git','ls-files','--cached','--others','--exclude-standard','-z'],cwd=root,capture_output=True,check=True)
    names=sorted(set(n.decode('utf-8') for n in result.stdout.split(b'\0') if n))
    forbidden=('Assets/PlotWeaveLocal/','Tools/PlotWeaveMigration/Inputs/','Library/','Builds/','_Validation/','Tools/ThirdParty/PythonPackages/')
    paths=[name for name in names if name.startswith(forbidden)]
    large=[]; secrets=[]; invalid=[]; checked=0
    patterns={
        'github-token':re.compile(rb'\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{50,})\b'),
        'private-key':re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'),
        'aws-access-key':re.compile(rb'\bAKIA[0-9A-Z]{16}\b'),
        'bearer-value':re.compile(rb'\bBearer[ \t]+[A-Za-z0-9_.-]{30,}'),
    }
    for name in names:
        p=root/name
        if not p.is_file(): continue
        if p.stat().st_size>=95*1024*1024: large.append(name)
        raw=p.read_bytes()
        if b'\0' not in raw[:8192] and p.suffix.lower() not in ('.otf','.ttf','.png','.jpg','.pdf','.xlsx','.bytes'):
            checked+=1
            for kind,pattern in patterns.items():
                if pattern.search(raw): secrets.append({'path':name,'kind':kind})
        if (name.startswith('Packages/') and p.suffix=='.json') or p.suffix=='.asmdef':
            try: json.loads(raw.decode('utf-8-sig'))
            except (ValueError,UnicodeError): invalid.append(name)
    required=['LICENSE','THIRD_PARTY_NOTICES.md','README.md','AI_CONTEXT.md',
        'Packages/com.plotweave.core/LICENSE.md',
        'Packages/com.plotweave.core/ThirdPartyNotices/NotoCJK-LICENSE.txt',
        'Packages/com.plotweave.core/Editor/ThirdParty~/LubanRuntime/LICENSE',
        'Doc/Review/index.html','Doc/Review/class-diagram.svg']
    missing=[name for name in required if not (root/name).is_file()]
    package=json.loads((root/'Packages/com.plotweave.core/package.json').read_text(encoding='utf-8-sig'))
    license_ok=package.get('license')=='MIT' and (root/'LICENSE').read_bytes()==(root/'Packages/com.plotweave.core/LICENSE.md').read_bytes()
    sources=[]
    for p in sorted((root/'Packages/com.plotweave.core').rglob('*')):
        if p.is_file() and (p.suffix in ('.cs','.asmdef') or p.name=='link.xml'):
            sources.append({'path':p.relative_to(root).as_posix(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
    okay=not any([paths,large,secrets,invalid,missing]) and license_ok
    report={'status':'passed' if okay else 'failed','scope':'public-file-boundaries-and-package-metadata',
        'candidateFiles':len(names),'textFilesScanned':checked,'privatePaths':paths,'oversizedFiles':large,
        'highConfidenceSecretPatternFindings':secrets,'invalidJson':invalid,'missingRequiredFiles':missing,
        'mitLicenseMatches':license_ok,'packageSources':len(sources),'verifiedSources':sources,
        'limits':['Pattern scan does not prove absence of every secret or copyrighted work.',
                  'Does not start Unity, run tests, or validate rendered behavior.']}
    print(json.dumps(report,ensure_ascii=False,indent=2))
    return 0 if okay else 1

if __name__=='__main__': raise SystemExit(main())
