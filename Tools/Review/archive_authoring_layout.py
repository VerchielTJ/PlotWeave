"""Archive targeted authoring layout evidence without promoting historical full acceptance."""
import argparse
import datetime
import hashlib
import json
import re
import shutil
import tempfile
from pathlib import Path


MIGRATION = 'Tools/PlotWeaveMigration'
PACKAGE = 'Packages/com.plotweave.core'
COVERED_TYPES = [
    'PlotWeave.Authoring.Playback.PlotPlaybackCompiler',
    'PlotWeave.Authoring.Playback.EditorPlotPlayerUI',
    'PlotWeave.Legacy.Editor.PlotLegacyAuthoringHost',
    'PlotWeave.Authoring.PlotAuthoringStartup',
    'PlotWeave.Legacy.Editor.PlotAuthoringPopupLayout',
    'PlotWeave.Legacy.Editor.PlotAuthoringViewportLayout',
    'PlotWeave.Authoring.UI.PlotNodeView',
]


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inside(root, name):
    if not isinstance(name, str) or not name:
        raise ValueError('Evidence path is missing.')
    path = (root / name).resolve()
    if not path.is_relative_to(root):
        raise ValueError('Evidence path escapes its project: ' + name)
    return path


def check_sources(project, sources):
    if not isinstance(sources, list) or not sources:
        raise ValueError('The targeted run has no source snapshot.')
    indexed = {}
    for item in sources:
        name, expected = item.get('path'), item.get('sha256')
        if not isinstance(expected, str) or not re.fullmatch(r'[a-fA-F0-9]{64}', expected):
            raise ValueError('Invalid recorded source SHA: ' + str(name))
        source = inside(project, name)
        relative = source.relative_to(project).as_posix()
        if relative in indexed:
            raise ValueError('Duplicate source snapshot entry: ' + relative)
        if not source.is_file() or digest(source) != expected.lower():
            raise ValueError('Source differs from targeted tests or is missing: ' + relative)
        indexed[relative] = expected.lower()
    package = sorted(p for p in (project / PACKAGE).rglob('*')
                     if p.is_file() and (p.suffix in ('.cs', '.asmdef') or p.name == 'link.xml'))
    if not package:
        raise ValueError('The project has no PlotWeave package sources.')
    missing = [p.relative_to(project).as_posix() for p in package
               if p.relative_to(project).as_posix() not in indexed]
    if missing:
        raise ValueError('Unrecorded package source exists: ' + ', '.join(missing[:5]))
    return [{'path': name, 'sha256': value} for name, value in indexed.items()], len(package)


def validate(tests, layout):
    if tests.get('step') != 'authoringlayouttests' or tests.get('status') != 'passed':
        raise ValueError('Latest targeted authoring layout tests have not passed.')
    if type(tests.get('failed')) is not int or tests['failed'] != 0 or type(tests.get('passed')) is not int or tests['passed'] < 1:
        raise ValueError('Targeted test counts are missing or contain failures.')
    if not re.fullmatch(r'[a-fA-F0-9]{32}', str(tests.get('id', ''))):
        raise ValueError('Targeted test run ID is invalid.')
    required = ('completed', 'supportedSizesPassed', 'unsupportedNoticeChecksPassed',
                'gameRestored', 'sourceUnchanged', 'sourceMemoryUnchanged')
    if layout.get('phase') != 'passed-with-minimum-viewport' or not all(layout.get(k) is True for k in required):
        raise ValueError('Layout minimum-viewport policy or restoration has not passed.')
    if type(layout.get('saveCalls')) is not int or layout['saveCalls'] != 0:
        raise ValueError('The layout probe saved a production workspace or omitted the save count.')
    if (layout.get('minimumViewportWidth'), layout.get('minimumViewportHeight')) != (1024, 600):
        raise ValueError('Unexpected minimum viewport policy.')
    if layout.get('allSizesPassed') is not False:
        raise ValueError('The minimum-viewport run must keep unsupported sizes distinct from all-sizes acceptance.')
    results = layout.get('results')
    if not isinstance(results, list) or not results or len(results) != layout.get('expectedSizeCount') or len(results) != layout.get('testedSizeCount'):
        raise ValueError('Layout size matrix is incomplete.')
    if not all(result.get('completed') is True for result in results):
        raise ValueError('A matrix size did not complete.')


def copy_evidence(source, destination):
    if not source.is_file():
        raise FileNotFoundError('Evidence file is missing: ' + str(source))
    if destination.exists():
        if digest(destination) != digest(source):
            raise ValueError('Refusing to replace a different archived artifact: ' + str(destination))
        return
    shutil.copyfile(source, destination)


def layout_input(project, name):
    source = inside(project, name)
    if source.is_file() or not name.startswith('Packages/') or name.startswith(PACKAGE + '/'):
        return source
    # Unity exposes registered registry/builtin packages through virtual Packages paths.
    # Resolve the version pinned by this project, while still requiring the recorded SHA.
    parts = name.split('/', 2)
    if len(parts) != 3:
        return source
    version = read(project / 'Packages/packages-lock.json')['dependencies'].get(parts[1], {}).get('version')
    if not isinstance(version, str) or not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+(?:[-+.][a-zA-Z0-9.-]+)?', version):
        return source
    return inside(project, 'Library/PackageCache/' + parts[1] + '@' + version + '/' + parts[2])


def archive(project, output):
    project, output = project.resolve(), output.resolve()
    tests_path = project / MIGRATION / 'result-authoringlayouttests.json'
    layout_path = project / MIGRATION / 'authoring-layout.json'
    tests, layout = read(tests_path), read(layout_path)
    validate(tests, layout)
    sources, package_count = check_sources(project, tests.get('sourceFiles'))

    # The tests snapshot covers code. The layout probe separately records the real
    # authoring assets it read; reject post-run asset changes before archiving too.
    for item in layout.get('sources', []):
        source = layout_input(project, item.get('path'))
        expected = item.get('after')
        if not isinstance(expected, str) or not re.fullmatch(r'[a-fA-F0-9]{64}', expected) or not source.is_file() or digest(source) != expected.lower():
            raise ValueError('Layout input changed after the probe: ' + str(item.get('path')))

    static_path = project / MIGRATION / 'authoring-static-layout.json'
    static = read(static_path)
    if static.get('status') != 'passed':
        raise ValueError('Static scene layout repair has not passed.')
    optional = [('layoutRecovery', 'authoring-layout-recovery.json', 'layout-recovery.json'),
                ('startupRequest', 'result-openauthoring.json', 'startup-request.json')]
    image_sources = {}
    for result in layout['results']:
        if not isinstance(result.get('images'), list) or (result.get('width') in (1920, 1092, 1024, 720) and not result['images']):
            raise ValueError('Matrix size has no screenshot: ' + str((result.get('width'), result.get('height'))))
        for name in result['images']:
            source = inside(project, name)
            if not source.is_relative_to(project / 'Doc/Records/Stage20') or source.suffix.lower() != '.png' or not source.is_file():
                raise ValueError('Unexpected or missing layout screenshot: ' + str(name))
            if source.name in image_sources and image_sources[source.name] != source:
                raise ValueError('Layout screenshot basenames collide: ' + source.name)
            image_sources[source.name] = source

    folder = output / 'Doc/Records/Stage20' / tests['id']
    folder.mkdir(parents=True, exist_ok=True)
    prefix = folder.relative_to(output).as_posix()
    copy_evidence(tests_path, folder / 'native-tests.json')
    copy_evidence(layout_path, folder / 'layout.json')
    copy_evidence(static_path, folder / 'static-layout.json')
    reports = {'tests': prefix + '/native-tests.json', 'layout': prefix + '/layout.json',
               'staticLayout': prefix + '/static-layout.json', 'guide': 'Doc/阶段20_启动与多尺寸适配.md'}
    for key, name, archived_name in optional:
        source = project / MIGRATION / name
        if source.is_file():
            copy_evidence(source, folder / archived_name)
            reports[key] = prefix + '/' + archived_name
    images = []
    for name, source in image_sources.items():
        copy_evidence(source, folder / name)
        images.append(prefix + '/' + name)

    results = []
    fields = ('width', 'height', 'actualWidth', 'actualHeight', 'completed', 'resolutionMatched',
              'supported', 'outcome', 'rawPassed', 'rawFailureCount', 'densityFailureCount',
              'geometryOrInteractionFailureCount', 'viewportTooSmall', 'unsupportedNoticeVisible', 'noticeText')
    for result in layout['results']:
        summary = {key: result.get(key) for key in fields}
        summary['images'] = [prefix + '/' + Path(name).name for name in result['images']]
        results.append(summary)
    now = datetime.datetime.now(datetime.timezone.utc)
    record = {
        'scope': 'startup-and-authoring-layout-minimum1024x600',
        'status': 'verified-targeted-scope',
        'dateAsiaHongKong': now.astimezone(datetime.timezone(datetime.timedelta(hours=8))).date().isoformat(),
        'archivedAtUtc': now.isoformat(),
        'testRunId': tests['id'], 'passed': tests['passed'], 'failed': tests['failed'],
        'layoutRunId': layout['id'], 'sourceRows': layout.get('rows'),
        'coveredTypes': COVERED_TYPES, 'reports': reports, 'images': images, 'verifiedSources': sources,
        'sourceCheck': {'recordedSourcesMatch': True, 'recordedSourceCount': len(sources),
                        'packageSources': package_count, 'unrecordedPackageSources': 0},
        'minimumViewport': {'width': 1024, 'height': 600, 'policy': layout.get('supportPolicy', '')},
        'expectedSizeCount': layout['expectedSizeCount'], 'testedSizeCount': layout['testedSizeCount'],
        'supportedSizeCount': layout.get('supportedSizeCount'), 'unsupportedSizeCount': layout.get('unsupportedSizeCount'),
        'supportedSizesPassed': layout['supportedSizesPassed'],
        'unsupportedNoticeChecksPassed': layout['unsupportedNoticeChecksPassed'],
        'allRawChecksPassed': layout.get('allRawChecksPassed'), 'allSizesPassed': layout['allSizesPassed'],
        'unsupportedDensityFailures': layout.get('unsupportedDensityFailures'),
        'unsupportedGeometryOrInteractionFailures': layout.get('unsupportedGeometryOrInteractionFailures'),
        'gameRestored': layout['gameRestored'], 'saveCalls': layout['saveCalls'],
        'sourceUnchanged': layout['sourceUnchanged'], 'sourceMemoryUnchanged': layout['sourceMemoryUnchanged'],
        'results': results,
        'limits': [
            'Targeted tests and memory-copy UI matrix only; historical 277 tests and Windows/Android builds were not rerun.',
            'The supported-size policy passed; unsupported-size diagnostics do not mean every matrix size passed.',
            'No workbook content import/export roundtrip or production workspace save was performed.',
            'A startup request result records dispatch only and does not by itself prove a Ready authoring session.',
            'Draft conflict recovery and node reference integrity remain separate open findings.',
        ],
    }
    if 'startupRequest' in reports:
        record['startupRequestEvidence'] = {'report': reports['startupRequest'], 'scope': 'request-only', 'readinessVerified': False}
    pointer = output / 'Doc/Records/Current/制作闭环验收.json'
    pointer.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(mode='w', encoding='utf-8', dir=pointer.parent, suffix='.tmp', delete=False) as temporary:
        temporary.write(json.dumps(record, ensure_ascii=False, indent=2) + '\n')
        temporary_path = Path(temporary.name)
    temporary_path.replace(pointer)
    print(json.dumps({'record': str(pointer), 'testRunId': tests['id'], 'passed': tests['passed'],
                      'layoutRunId': layout['id'], 'packageSources': package_count, 'images': len(images),
                      'supportedSizes': layout.get('supportedSizeCount'), 'allSizesPassed': False}, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--output-root', type=Path, required=True)
    args = parser.parse_args()
    archive(args.project, args.output_root)
