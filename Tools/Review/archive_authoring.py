"""Archive a successful targeted authoring run without altering historical full acceptance."""
import argparse
import datetime
import hashlib
import json
import shutil
from pathlib import Path


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def archive(project, output):
    tests = read(project / 'Tools/PlotWeaveMigration/result-authoringworkflowtests.json')
    workflow = read(project / 'Tools/PlotWeaveMigration/authoring-workflow.json')
    if tests['status'] != 'passed' or tests['failed'] or tests['passed'] < 19:
        raise ValueError('Targeted Unity tests have not passed.')
    if workflow['phase'] != 'passed' or not all(workflow.get(k) for k in
            ('sourceUnchanged', 'sourceMemoryUnchanged', 'exchangeMatched', 'unsaved')) or workflow['saveCalls'] != 0:
        raise ValueError('The complete read-only workflow has not passed.')
    sources = tests['sourceFiles']
    for item in sources:
        path = (project / item['path']).resolve()
        if not path.is_relative_to(project) or not path.is_file() or digest(path) != item['sha256']:
            raise ValueError('Source differs from targeted test run: ' + item['path'])
    package = sorted(p for p in (project / 'Packages/com.plotweave.core').rglob('*')
                     if p.is_file() and (p.suffix in ('.cs', '.asmdef') or p.name == 'link.xml'))
    indexed = {s['path']: s['sha256'] for s in sources}
    if any(p.relative_to(project).as_posix() not in indexed for p in package):
        raise ValueError('Unrecorded package source exists.')
    folder = output / 'Doc/Records/Stage19' / tests['id']
    folder.mkdir(parents=True, exist_ok=True)
    (folder / 'native-tests.json').write_text(json.dumps(tests, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    (folder / 'workflow.json').write_text(json.dumps(workflow, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    images = []
    for item in workflow['images']:
        source = (project / item).resolve()
        if not source.is_relative_to(project / 'Doc/Records/Stage19'):
            raise ValueError('Unexpected image path.')
        destination = folder / source.name
        shutil.copyfile(source, destination)
        images.append(destination.relative_to(output).as_posix())
    prefix = folder.relative_to(output).as_posix()
    record = {'scope': 'authoring-input-preview-refresh-and-workspace-entry',
              'status': 'verified-targeted-scope',
              'dateAsiaHongKong': datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=8))).date().isoformat(),
              'archivedAtUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
              'testRunId': tests['id'], 'passed': tests['passed'], 'failed': tests['failed'],
              'workflowRunId': workflow['id'], 'sourceRows': workflow['rows'],
              'coveredTypes': ['PlotWeave.Authoring.Playback.PlotPlaybackCompiler',
                               'PlotWeave.Authoring.Playback.EditorPlotPlayerUI',
                               'PlotWeave.Legacy.Editor.PlotLegacyAuthoringHost'],
              'reports': {'tests': prefix + '/native-tests.json', 'interaction': prefix + '/workflow.json',
                          'guide': 'Doc/阶段19_制作输入与内嵌试跑.md'},
              'images': images, 'verifiedSources': sources,
              'limits': ['Targeted tests and memory-copy UI interaction only; historical full tests/builds were not rerun.',
                         'Exchange callback uses a stub; no workbook import or production workspace save occurred in this UI probe.',
                         'Draft conflict recovery and node reference integrity remain separate open findings.']}
    pointer = output / 'Doc/Records/Current/制作闭环验收.json'
    pointer.parent.mkdir(parents=True, exist_ok=True)
    pointer.write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'record': str(pointer), 'passed': tests['passed'], 'workflow': workflow['id'],
                      'packageSources': len(package), 'images': len(images)}, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--output-root', type=Path, required=True)
    args = parser.parse_args()
    archive(args.project.resolve(), args.output_root.resolve())
