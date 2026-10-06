"""Read-only integrity gate for a synthetic DAS restore rehearsal.

No extraction, database connection, storage rewrite, worker startup or secret loading.
Checksums detect changed bytes; this is not authentication of a backup or production acceptance.
Manifest v1 is a limited legacy three-store profile. Version 2 requires the explicit
core-five-stores profile, all five SQL backups, >=1 PDF, one cutId and disabled workers.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import uuid

WORKERS = {'reminders', 'documentNotifications', 'notificationDelivery', 'smtp', 'pdfMaintenance'}
BACKUPS = {'document': 'backups/document.bak', 'files': 'backups/files.bak', 'notification': 'backups/notification.bak'}
CORE_BACKUPS = {**BACKUPS, 'auth': 'backups/auth.bak', 'partner': 'backups/partner.bak'}


def load_manifest(text):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError('DUPLICATE_JSON_KEY')
            result[key] = value
        return result
    return json.loads(text, object_pairs_hook=unique)


def linked(path):
    return path.is_symlink() or getattr(path, 'is_junction', lambda: False)()


def digest(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(chunk)
    return value.hexdigest()


def valid_cut(value):
    try:
        return isinstance(value, str) and str(uuid.UUID(value)) == value and uuid.UUID(value).int != 0
    except ValueError:
        return False


def verify(manifest, root):
    errors = set()
    result = {'integrityPassed': False, 'productionReady': False, 'mutated': False, 'artifactCount': 0, 'errors': []}
    root = Path(root).absolute()
    if not root.is_dir() or any(linked(p) for p in (root, *root.parents)):
        errors.add('UNSAFE_BUNDLE_ROOT')
    root = root.resolve()
    if not isinstance(manifest, dict):
        result['errors'] = ['INVALID_MANIFEST']; return result
    version = manifest.get('version')
    backups = BACKUPS
    profile = 'legacy-three-stores'
    if type(version) is not int or version not in (1, 2) or manifest.get('purpose') != 'synthetic-restore-drill':
        errors.add('INVALID_MANIFEST')
    if version == 2 and type(version) is int:
        backups = CORE_BACKUPS
        profile = 'core-five-stores'
        if manifest.get('profile') != profile: errors.add('INVALID_PROFILE')
    elif 'profile' in manifest:
        # Legacy manifests never advertise the new completeness profile.
        errors.add('INVALID_PROFILE')
    result.update(profile=profile, requiredBackups=sorted(backups))
    cut = manifest.get('cutId')
    if not valid_cut(cut): errors.add('INVALID_CUT')
    workers = manifest.get('workers')
    if not isinstance(workers, dict) or set(workers) != WORKERS or any(value is not False for value in workers.values()):
        errors.add('WORKERS_NOT_EXPLICITLY_DISABLED')
    records = manifest.get('artifacts')
    if not isinstance(records, list) or not len(backups) + 1 <= len(records) <= 10000:
        result['errors'] = sorted(errors | {'INVALID_ARTIFACT_SET'}); return result
    seen, components, expected = set(), [], set()
    for record in records:
        if not isinstance(record, dict): errors.add('INVALID_ARTIFACT'); continue
        component, relative = record.get('component'), record.get('path')
        if not isinstance(component, str) or component not in {*backups, 'pdf'}:
            errors.add('INVALID_COMPONENT'); continue
        components.append(component)
        if record.get('cutId') != cut: errors.add('MIXED_CUT')
        if (not isinstance(relative, str) or not relative or '\\' in relative or ':' in relative or
                any(ord(c) < 32 for c in relative) or relative.startswith('/') or
                any(p in ('', '.', '..') for p in relative.split('/'))):
            errors.add('UNSAFE_PATH'); continue
        if relative.casefold() in seen: errors.add('DUPLICATE_PATH')
        seen.add(relative.casefold()); expected.add(relative)
        if component in backups and relative != backups[component] or component == 'pdf' and (not relative.startswith('storage/') or PurePosixPath(relative).suffix != '.pdf'):
            errors.add('COMPONENT_PATH_MISMATCH')
        size, sha = record.get('sizeBytes'), record.get('sha256')
        if type(size) is not int or size <= 0 or not isinstance(sha, str) or re.fullmatch('[0-9a-f]{64}', sha) is None:
            errors.add('INVALID_INTEGRITY_METADATA'); continue
        path = root / relative
        # Reject links at every path level before reading, even if the link resolves inside the root.
        if any(linked(p) for p in (path, *path.parents)) or not path.resolve().is_relative_to(root):
            errors.add('UNSAFE_PATH'); continue
        try:
            if not path.is_file(): errors.add('MISSING_ARTIFACT')
            elif path.stat().st_size != size or digest(path) != sha: errors.add('INTEGRITY_MISMATCH')
        except OSError:
            errors.add('UNREADABLE_ARTIFACT')
    if any(components.count(c) != 1 for c in backups) or components.count('pdf') < 1:
        errors.add('INCOMPLETE_COMPONENT_SET')
    if 'UNSAFE_BUNDLE_ROOT' not in errors:
        try:
            actual = set()
            for directory, dirs, files in os.walk(root, followlinks=False):
                for name in [*dirs, *files]:
                    if linked(Path(directory) / name): errors.add('UNSAFE_PATH')
                # Windows junctions may be traversed by os.walk even with followlinks=False.
                dirs[:] = [name for name in dirs if not linked(Path(directory) / name)]
                for name in files:
                    relative = (Path(directory) / name).relative_to(root).as_posix()
                    if relative != 'manifest.json': actual.add(relative)
            if actual != expected: errors.add('UNLISTED_OR_MISSING_ARTIFACT')
        except OSError:
            errors.add('UNREADABLE_ARTIFACT')
    result.update(integrityPassed=not errors, artifactCount=len(records), errors=sorted(errors))
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('manifest', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    root = args.manifest.absolute().parent
    # A report must not overwrite any bundle data, including its manifest.
    if args.output and args.output.resolve().is_relative_to(root.resolve()):
        parser.error('Report output must be outside the bundle')
    try:
        if args.manifest.stat().st_size > 8 * 1024 * 1024 or linked(args.manifest):
            raise ValueError('INVALID_MANIFEST')
        result = verify(load_manifest(args.manifest.read_text(encoding='utf-8-sig')), root)
    except (OSError, ValueError):
        result = {'integrityPassed': False, 'productionReady': False, 'mutated': False, 'errors': ['INVALID_MANIFEST']}
    text = json.dumps(result, indent=2) + '\n'
    if args.output:
        # New report only: never overwrite a prior report, link or source file.
        with args.output.open('x', encoding='utf-8') as stream: stream.write(text)
    print(text, end='')
    return 0 if result['integrityPassed'] else 1


if __name__ == '__main__': raise SystemExit(main())
