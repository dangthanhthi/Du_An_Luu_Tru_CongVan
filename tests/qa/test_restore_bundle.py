import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import subprocess
import sys
import unittest
import uuid

SCRIPT = Path(__file__).resolve().parents[2] / 'scripts/qa/verify-restore-bundle.py'


class RestoreBundleTests(unittest.TestCase):
    def verify(self, manifest, root):
        self.assertTrue(SCRIPT.is_file(), 'Restore bundle verifier has not been implemented')
        spec = importlib.util.spec_from_file_location('restore_bundle', SCRIPT)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        return module.verify(manifest, root)

    def fixture(self, root):
        cut = str(uuid.uuid4())
        records = []
        for component in ('document', 'files', 'notification', 'pdf'):
            relative = f'backups/{component}.bak' if component != 'pdf' else 'storage/test.pdf'
            content = f'synthetic {component}'.encode()
            file = root / relative
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_bytes(content)
            records.append(dict(component=component, path=relative, cutId=cut,
                                sizeBytes=len(content), sha256=hashlib.sha256(content).hexdigest()))
        return dict(version=1, purpose='synthetic-restore-drill', cutId=cut,
                    workers={key: False for key in ('reminders', 'documentNotifications', 'notificationDelivery', 'smtp', 'pdfMaintenance')},
                    artifacts=records)

    def test_complete_bundle_verified_without_mutating_bytes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); manifest = self.fixture(root); before = copy.deepcopy(manifest)
            original = (root / 'storage/test.pdf').read_bytes()
            result = self.verify(manifest, root)
            self.assertTrue(result['integrityPassed']); self.assertFalse(result['mutated'])
            self.assertFalse(result['productionReady']); self.assertEqual(before, manifest)
            self.assertEqual(original, (root / 'storage/test.pdf').read_bytes())

    def test_incomplete_mixed_cut_or_worker_enabled_blocked(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); original = self.fixture(root)
            for alter in (lambda m: m['artifacts'].pop(0),
                          lambda m: m['artifacts'][0].update(cutId=str(uuid.uuid4())),
                          lambda m: m['workers'].update(smtp=True),
                          lambda m: m['workers'].pop('pdfMaintenance')):
                manifest = copy.deepcopy(original); alter(manifest)
                self.assertFalse(self.verify(manifest, root)['integrityPassed'])

    def test_missing_and_tampered_bytes_are_blocked(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); manifest = self.fixture(root)
            (root / 'storage/test.pdf').write_bytes(b'changed')
            self.assertFalse(self.verify(manifest, root)['integrityPassed'])
            (root / 'storage/test.pdf').unlink()
            self.assertFalse(self.verify(manifest, root)['integrityPassed'])

    def test_paths_duplicates_and_unlisted_files_blocked(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); original = self.fixture(root)
            for path in ('../outside.pdf', '/outside.pdf', 'C:/outside.pdf', 'storage\\test.pdf', 'storage/../test.pdf'):
                manifest = copy.deepcopy(original); manifest['artifacts'][-1]['path'] = path
                self.assertFalse(self.verify(manifest, root)['integrityPassed'])
            manifest = copy.deepcopy(original); manifest['artifacts'].append(copy.deepcopy(manifest['artifacts'][0]))
            self.assertFalse(self.verify(manifest, root)['integrityPassed'])
            (root / 'extra.bak').write_bytes(b'extra')
            self.assertFalse(self.verify(original, root)['integrityPassed'])

    def test_invalid_types_and_duplicate_json_keys_blocked(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); original = self.fixture(root)
            for value in (True, -1, 0, '10'):
                manifest = copy.deepcopy(original); manifest['artifacts'][0]['sizeBytes'] = value
                self.assertFalse(self.verify(manifest, root)['integrityPassed'])
            self.assertTrue(SCRIPT.is_file(), 'Restore bundle verifier has not been implemented')
            spec = importlib.util.spec_from_file_location('restore_bundle', SCRIPT); module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
            with self.assertRaises(ValueError):
                module.load_manifest('{"version":1,"version":2}')

    def test_linked_component_is_blocked_even_inside_root(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); manifest = self.fixture(root)
            target = root / 'backups/document.bak'; moved = root / 'document-copy.bak'
            target.rename(moved)
            try:
                target.symlink_to(moved)
            except OSError:
                # Windows ordinary users may lack symlink privilege; junctions are separately rejected.
                self.skipTest('Host does not permit symlinks')
            self.assertFalse(self.verify(manifest, root)['integrityPassed'])

    def test_cli_does_not_overwrite_bundle_or_prior_report(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp) / 'bundle'; root.mkdir(); manifest = self.fixture(root)
            path = root / 'manifest.json'; path.write_text(json.dumps(manifest), encoding='utf-8')
            original = path.read_bytes()
            result = subprocess.run([sys.executable, str(SCRIPT), str(path), '--output', str(path)], capture_output=True)
            self.assertNotEqual(0, result.returncode); self.assertEqual(original, path.read_bytes())
            report = Path(tmp) / 'report.json'; report.write_bytes(b'prior evidence')
            result = subprocess.run([sys.executable, str(SCRIPT), str(path), '--output', str(report)], capture_output=True)
            self.assertNotEqual(0, result.returncode); self.assertEqual(b'prior evidence', report.read_bytes())

    def test_production_label_and_unknown_format_never_pass(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); original = self.fixture(root)
            for alter in (lambda m: m.update(purpose='production-backup'), lambda m: m.update(version=2),
                          lambda m: m.update(cutId=''), lambda m: m['artifacts'][0].update(sha256='invalid')):
                manifest = copy.deepcopy(original); alter(manifest)
                self.assertFalse(self.verify(manifest, root)['integrityPassed'])

    def core_fixture(self, root):
        manifest = self.fixture(root)
        manifest.update(version=2, profile='core-five-stores')
        for component in ('auth', 'partner'):
            relative = f'backups/{component}.bak'
            content = f'synthetic {component}'.encode()
            (root / relative).write_bytes(content)
            manifest['artifacts'].append(dict(component=component, path=relative,
                cutId=manifest['cutId'], sizeBytes=len(content), sha256=hashlib.sha256(content).hexdigest()))
        return manifest

    def test_five_store_profile_requires_auth_and_partner_and_reports_scope(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); original = self.core_fixture(root)
            result = self.verify(original, root)
            self.assertTrue(result['integrityPassed'])
            self.assertEqual('core-five-stores', result['profile'])
            self.assertEqual(['auth', 'document', 'files', 'notification', 'partner'], result['requiredBackups'])
            self.assertFalse(result['productionReady'])
            for component in ('auth', 'partner'):
                manifest = copy.deepcopy(original)
                manifest['artifacts'] = [r for r in manifest['artifacts'] if r['component'] != component]
                self.assertFalse(self.verify(manifest, root)['integrityPassed'])

    def test_legacy_three_store_bundle_is_explicitly_limited(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); manifest = self.fixture(root)
            result = self.verify(manifest, root)
            self.assertTrue(result['integrityPassed'])
            self.assertEqual('legacy-three-stores', result['profile'])
            self.assertEqual(['document', 'files', 'notification'], result['requiredBackups'])

    def test_profile_cannot_downgrade_or_expand_component_requirements(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); original = self.core_fixture(root)
            for alter in (lambda m: m.pop('profile'), lambda m: m.update(profile='legacy-three-stores'),
                          lambda m: m.update(version=1), lambda m: m.update(version=True),
                          lambda m: m.update(profile=['core-five-stores']),
                          lambda m: m['artifacts'][0].update(component='unexpected'),
                          lambda m: m['artifacts'][-1].update(path='backups/auth.bak')):
                manifest = copy.deepcopy(original); alter(manifest)
                self.assertFalse(self.verify(manifest, root)['integrityPassed'])


if __name__ == '__main__': unittest.main()
