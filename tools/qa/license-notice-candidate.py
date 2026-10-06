"""Prepare exact local notice bytes for review; never approve distribution.

No network, extraction, dependency mutation, receipts or permission inference.
The full lock inventory includes dev/optional dependencies, not a runtime SBOM.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import zipfile

MAX_TEXT_BYTES = 2 * 1024 * 1024
MAX_TOTAL_BYTES = 100 * 1024 * 1024


def safe_path(root, relative):
    if not isinstance(relative, str) or not relative or ':' in relative or '\\' in relative:
        raise ValueError('Invalid relative evidence path')
    if any(part in ('', '.', '..') for part in relative.split('/')) or relative.startswith('/'):
        raise ValueError('Escaped evidence path')
    root = Path(root).absolute()
    path = root / relative
    if any(p.is_symlink() or getattr(p, 'is_junction', lambda: False)() for p in (path, *path.parents)):
        raise ValueError('Linked evidence path')
    if not path.resolve().is_relative_to(root.resolve()):
        raise ValueError('Escaped evidence path')
    return path


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def matched(path, digest):
    if not re.fullmatch('[a-f0-9]{64}', digest or '') or not path.is_file() or sha(path) != digest:
        raise ValueError('Evidence hash changed or missing: ' + path.name)


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def package_prefix(row):
    if row['ecosystem'] == 'npm':
        path = row['path']
        if path == '.': return ''
        if not path.startswith('node_modules/'): raise ValueError('Invalid npm path')
        return path + '/'
    name, version = row['name'], row['version']
    if not re.fullmatch('[A-Za-z0-9_.-]+', name) or not re.fullmatch('[A-Za-z0-9_.+-]+', version):
        raise ValueError('Invalid NuGet identity')
    if '..' in name or '..' in version: raise ValueError('Invalid NuGet identity')
    return name.lower() + '/' + version.lower() + '/'


def build_candidate(inventory_path, project_root, npm_tree, nuget_cache, output):
    inventory_path, project_root, npm_tree, nuget_cache, output = map(Path,
        (inventory_path, project_root, npm_tree, nuget_cache, output))
    if output.exists() or output.is_symlink(): raise ValueError('Refuse prior output')
    # Check output ancestry even before any writes.
    safe_path(output.parent, output.name)
    inventory = read(inventory_path)
    matched(project_root / 'package-lock.json', inventory['npmLockSha256'])
    matched(npm_tree / 'package-lock.json', inventory['npmLockSha256'])
    for relative, digest in inventory['nugetLockSha256'].items():
        matched(safe_path(project_root, relative), digest)
    blobs, packages, total_bytes, references = {}, [], 0, 0
    for ecosystem in ('npm', 'nuget'):
        tree = npm_tree if ecosystem == 'npm' else nuget_cache
        for original in inventory[ecosystem]:
            row = dict(original, ecosystem=ecosystem)
            prefix = package_prefix(row)
            metadata_digest = row.get('packageMetadataSha256') if ecosystem == 'npm' else row.get('nuspecSha256')
            if metadata_digest:
                relative = prefix + ('package.json' if ecosystem == 'npm' else row['name'].lower() + '.nuspec')
                matched(safe_path(tree, relative), metadata_digest)
            collected = []
            for text in row.get('licenseTexts', []):
                relative = text['path']
                path = safe_path(tree, relative)
                if not relative.startswith(prefix): raise ValueError('Text outside package')
                within = relative[len(prefix):]
                standard = '/' not in within and within.lower().startswith(('license', 'licence', 'notice', 'copying'))
                explicit = ecosystem == 'nuget' and row.get('licenseType') == 'file' and within == row.get('declaredLicense')
                if not standard and not explicit: raise ValueError('Not a recorded license/notice file')
                if not path.is_file() or path.stat().st_size > MAX_TEXT_BYTES:
                    raise ValueError('Missing or oversized license text')
                content = path.read_bytes(); digest = hashlib.sha256(content).hexdigest()
                if digest != text['sha256'] or len(content) != text['bytes']:
                    raise ValueError('License text drift')
                if digest not in blobs:
                    total_bytes += len(content)
                    if total_bytes > MAX_TOTAL_BYTES: raise ValueError('Notice archive size bound exceeded')
                    blobs[digest] = content
                references += 1
                collected.append({'sourcePath': relative, 'sha256': digest, 'bytes': len(content),
                                  'archivePath': 'texts/' + digest + '.txt'})
            packages.append({'ecosystem': ecosystem, 'name': row.get('name'), 'version': row['version'],
                'packagePath': row.get('path'), 'dev': row.get('dev'), 'optional': row.get('optional'),
                'declaredLicense': row.get('declaredLicense'), 'licenseUrl': row.get('licenseUrl'),
                'review': row.get('review'), 'metadataSha256': metadata_digest,
                'texts': collected, 'textStatus': 'ExactLocalTexts' if collected else 'MissingLocalText',
                'permissionApproved': False})
    output.mkdir(parents=True)
    archive_path = output / 'texts.zip'
    with zipfile.ZipFile(archive_path, 'x', zipfile.ZIP_DEFLATED) as archive:
        for digest, content in sorted(blobs.items()):
            entry = zipfile.ZipInfo('texts/' + digest + '.txt', date_time=(1980, 1, 1, 0, 0, 0))
            entry.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(entry, content)
    report = {'capturedAtUtc': datetime.now(timezone.utc).isoformat(), 'candidateCreated': True,
        'scope': 'Exact local license/notice bytes for full dependency lock review; not runtime SBOM or approved release notices',
        'inventorySha256': sha(inventory_path), 'npmLockSha256': inventory['npmLockSha256'],
        'nugetLockSha256': inventory['nugetLockSha256'], 'packages': packages,
        'packageCount': len(packages), 'textReferences': references, 'uniqueTexts': len(blobs), 'textBytes': total_bytes,
        'packagesWithoutText': sum(not row['texts'] for row in packages), 'archiveSha256': sha(archive_path),
        'permissionApproved': False, 'distributionLicenseGatePassed': False, 'productionReady': False,
        'limitations': ['Missing texts are not fetched or substituted', 'All terms/obligations/notice selection need human review',
                       'Assets, Vuexy receipt and SDK provenance remain separate', 'Checksums are not signing or publisher authentication']}
    (output / 'manifest.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    return report


def verify_candidate(output):
    output = Path(output)
    report = read(output / 'manifest.json')
    if any(report.get(flag) is not False for flag in ('permissionApproved', 'distributionLicenseGatePassed', 'productionReady')):
        raise ValueError('Candidate must not claim approval')
    archive_path = output / 'texts.zip'; matched(archive_path, report['archiveSha256'])
    if (len(report['packages']) != report['packageCount']
            or sum(len(row['texts']) for row in report['packages']) != report['textReferences']
            or sum(not row['texts'] for row in report['packages']) != report['packagesWithoutText']):
        raise ValueError('Package/reference counts mismatch')
    expected = {}
    for row in report['packages']:
        if row.get('permissionApproved') is not False: raise ValueError('Package approval claimed')
        for text in row['texts']:
            digest = text['sha256']
            if not re.fullmatch('[a-f0-9]{64}', digest) or text['archivePath'] != 'texts/' + digest + '.txt':
                raise ValueError('Unsafe archive entry')
            if text['bytes'] > MAX_TEXT_BYTES: raise ValueError('Text too large')
            expected[text['archivePath']] = text
    text_bytes = sum(t['bytes'] for t in expected.values())
    if len(expected) != report['uniqueTexts'] or text_bytes != report['textBytes'] or text_bytes > MAX_TOTAL_BYTES:
        raise ValueError('Archive bounds/count mismatch')
    with zipfile.ZipFile(archive_path) as archive:
        if set(archive.namelist()) != set(expected) or len(archive.namelist()) != len(expected):
            raise ValueError('Unexpected/duplicate archive entries')
        for info in archive.infolist():
            item = expected[info.filename]
            if info.file_size != item['bytes']: raise ValueError('Archive size mismatch')
            content = archive.read(info)
            if hashlib.sha256(content).hexdigest() != item['sha256']: raise ValueError('Archive text drift')
    return {'passed': True, 'uniqueTexts': len(expected), 'permissionApproved': False,
            'distributionLicenseGatePassed': False, 'productionReady': False,
            'meaning': 'Candidate byte integrity only; not terms acceptance, full notice compliance or signed provenance'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='action', required=True)
    build = sub.add_parser('create')
    build.add_argument('--inventory', required=True, type=Path)
    build.add_argument('--npm-tree', required=True, type=Path)
    build.add_argument('--nuget-cache', required=True, type=Path)
    build.add_argument('--directory', required=True)
    verify = sub.add_parser('verify'); verify.add_argument('directory', type=Path)
    args = parser.parse_args(); root = Path(__file__).resolve().parents[2]
    if args.action == 'verify':
        print(json.dumps(verify_candidate(args.directory))); return 0
    if not re.fullmatch(r'\.artifacts/qa/[A-Za-z0-9_-]+(?:/[A-Za-z0-9_-]+)?', args.directory):
        raise ValueError('Fresh QA directory required')
    inventory = safe_path(root, args.inventory.as_posix())
    output = safe_path(root, args.directory)
    report = build_candidate(inventory, root, args.npm_tree, args.nuget_cache, output)
    verification = verify_candidate(output)
    (output / 'verification-report.json').write_text(json.dumps(verification, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: report[key] for key in ('packageCount', 'textReferences', 'uniqueTexts', 'packagesWithoutText', 'permissionApproved')}))
    return 0


if __name__ == '__main__': raise SystemExit(main())
