import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

SCRIPT = Path(__file__).resolve().parents[2] / 'scripts/qa/license-notice-candidate.py'


class LicenseNoticeCandidateTests(unittest.TestCase):
    def module(self):
        self.assertTrue(SCRIPT.is_file(), 'Notice candidate collector has not been implemented')
        spec = importlib.util.spec_from_file_location('license_notice_candidate', SCRIPT)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        return module

    def fixture(self, root):
        cache = root / 'nuget'; cache.mkdir()
        lock = root / 'package-lock.json'; lock.write_text('{"exact":true}')
        data = {'npmLockSha256': hashlib.sha256(lock.read_bytes()).hexdigest(),
                'nugetLockSha256': {}, 'npm': [], 'nuget': [], 'assets': []}
        for name in ('first', 'second'):
            folder = root / 'node_modules' / name; folder.mkdir(parents=True)
            metadata = folder / 'package.json'
            metadata.write_text(json.dumps({'name': name, 'version': '1.0.0', 'license': 'MIT'}))
            text = folder / 'LICENSE'; text.write_bytes(b'Original attribution\r\nExact terms\r\n')
            data['npm'].append({'ecosystem': 'npm', 'name': name, 'version': '1.0.0',
                'path': 'node_modules/' + name, 'installed': True, 'declaredLicense': 'MIT',
                'review': 'DeclarationAndNoticeReview', 'permissionApproved': False,
                'packageMetadataSha256': hashlib.sha256(metadata.read_bytes()).hexdigest(),
                'licenseTexts': [{'path': text.relative_to(root).as_posix(),
                    'sha256': hashlib.sha256(text.read_bytes()).hexdigest(), 'bytes': text.stat().st_size}]})
        data['nuget'].append({'ecosystem': 'nuget', 'name': 'Pending', 'version': '4.0.3',
            'review': 'MissingDeclaration', 'licenseTexts': [], 'licenseUrl': 'https://example.test/current-main'})
        inventory = root / 'inventory.json'; inventory.write_text(json.dumps(data))
        return inventory, cache, data

    def test_exact_bytes_round_trip_deduplicates_and_never_approves(self):
        module = self.module()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); inventory, cache, _ = self.fixture(root)
            report = module.build_candidate(inventory, root, root, cache, root / 'candidate')
            self.assertFalse(report['permissionApproved'])
            self.assertFalse(report['distributionLicenseGatePassed'])
            self.assertFalse(report['productionReady'])
            self.assertEqual(2, report['textReferences'])
            self.assertEqual(1, report['uniqueTexts'])
            with zipfile.ZipFile(root / 'candidate/texts.zip') as archive:
                self.assertEqual(1, len(archive.namelist()))
                self.assertEqual(b'Original attribution\r\nExact terms\r\n', archive.read(archive.namelist()[0]))
            self.assertTrue(module.verify_candidate(root / 'candidate')['passed'])

    def test_text_and_lock_drift_fail_before_output(self):
        module = self.module()
        for drift in ('text', 'lock'):
            with self.subTest(drift=drift), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp); inventory, cache, _ = self.fixture(root)
                target = root / ('node_modules/first/LICENSE' if drift == 'text' else 'package-lock.json')
                target.write_text('Changed bytes')
                with self.assertRaises(ValueError):
                    module.build_candidate(inventory, root, root, cache, root / 'candidate')
                self.assertFalse((root / 'candidate').exists())

    def test_escaped_or_unrelated_text_is_refused(self):
        module = self.module()
        for path in ('../private', 'node_modules/first/package.json'):
            with self.subTest(path=path), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp); inventory, cache, data = self.fixture(root)
                data['npm'][0]['licenseTexts'][0]['path'] = path
                inventory.write_text(json.dumps(data))
                with self.assertRaises(ValueError):
                    module.build_candidate(inventory, root, root, cache, root / 'candidate')
                self.assertFalse((root / 'candidate').exists())

    def test_missing_texts_are_unresolved_without_fetched_substitutes(self):
        module = self.module()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); inventory, cache, _ = self.fixture(root)
            report = module.build_candidate(inventory, root, root, cache, root / 'candidate')
            pending = next(row for row in report['packages'] if row['name'] == 'Pending')
            self.assertEqual([], pending['texts'])
            self.assertEqual('MissingLocalText', pending['textStatus'])
            self.assertFalse(pending['permissionApproved'])
            self.assertEqual(1, report['packagesWithoutText'])

    def test_existing_output_is_preserved_and_tampered_zip_is_detected(self):
        module = self.module()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); inventory, cache, _ = self.fixture(root)
            output = root / 'candidate'
            module.build_candidate(inventory, root, root, cache, output)
            original = (output / 'manifest.json').read_bytes()
            with self.assertRaises(ValueError):
                module.build_candidate(inventory, root, root, cache, output)
            self.assertEqual(original, (output / 'manifest.json').read_bytes())
            (output / 'texts.zip').write_bytes(b'corrupt')
            with self.assertRaises(ValueError): module.verify_candidate(output)

    def test_manifest_cannot_overstate_collected_references(self):
        module = self.module()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); inventory, cache, _ = self.fixture(root)
            output = root / 'candidate'
            module.build_candidate(inventory, root, root, cache, output)
            manifest = output / 'manifest.json'; report = json.loads(manifest.read_text())
            report['textReferences'] = 999
            manifest.write_text(json.dumps(report))
            with self.assertRaises(ValueError): module.verify_candidate(output)


if __name__ == '__main__': unittest.main()
