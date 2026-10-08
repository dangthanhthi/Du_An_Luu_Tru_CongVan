"""Resource boundaries for the canonical isolated SQL runner (no Docker required)."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import uuid
import hashlib
from unittest.mock import patch
from argparse import Namespace

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = next((ROOT / p for p in ('tools/qa/run-isolated-sql.py', 'scripts/qa/run-isolated-sql.py') if (ROOT / p).is_file()), ROOT / 'tools/qa/run-isolated-sql.py')


def module():
    spec = importlib.util.spec_from_file_location('isolated_sql', SCRIPT)
    m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m)
    return m


class IsolatedSqlTests(unittest.TestCase):
    def test_child_environment_drops_foreign_connections_credentials_and_transport(self):
        m = module()
        base = {'PATH': 'safe-path', 'SystemRoot': 'C:/Windows', 'DAS_TEST_SQL_CONNECTION': 'foreign',
                'das_synthetic_load': 'enabled', 'DAS_RESTORE_OUTPUT': 'foreign', 'SMTP__Password': 'secret',
                'Reminders__Enabled': 'true', 'DOCKER_HOST': 'tcp://foreign', 'NUGET_AUTH_TOKEN': 'secret'}
        original = base.copy()
        env = m.child_environment(base, Path('/qa'))
        self.assertEqual(original, base)
        for key in ('DAS_TEST_SQL_CONNECTION', 'DAS_SYNTHETIC_LOAD', 'DAS_RESTORE_OUTPUT', 'SMTP__PASSWORD', 'DOCKER_HOST', 'NUGET_AUTH_TOKEN'):
            self.assertFalse(any(k.upper() == key for k in env))
        for key in ('Reminders__Enabled', 'Delivery__WorkerEnabled', 'EmailIntake__WorkerEnabled', 'EmailIntake__ManualScanEnabled'):
            self.assertEqual('false', env[key])

    def test_output_cannot_reuse_escape_or_follow_link(self):
        m = module()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for name in ('../outside', '.artifacts/qa/../outside', '.artifacts/qa', '.artifacts/qa/x:stream', '/outside'):
                with self.assertRaises(ValueError): m.fresh_output(root, name)
            good = '.artifacts/qa/run'
            self.assertEqual(root / good, m.fresh_output(root, good))
            (root / good).mkdir(parents=True)
            with self.assertRaises(ValueError): m.fresh_output(root, good)

    def test_ownership_requires_matching_id_label_and_generated_name(self):
        m = module(); token = 'a' * 24; ident = 'b' * 64
        data = {'Id': ident, 'Name': '/das-sql-qa-' + token, 'Config': {'Labels': {m.LABEL: token}}}
        m.require_owner(data, token, 'container', ident)
        for bad in (
            {**data, 'Id': 'c' * 64}, {**data, 'Name': '/unrelated'},
            {**data, 'Config': {'Labels': {m.LABEL: 'another'}}}, {**data, 'Config': {}}):
            with self.assertRaises(ValueError): m.require_owner(bad, token, 'container', ident)

    def test_endpoint_rejects_non_loopback_duplicate_or_invalid_port(self):
        m = module()
        for binds in (None, [], [{'HostIp': '0.0.0.0', 'HostPort': '1234'}],
                      [{'HostIp': '127.0.0.1', 'HostPort': '0'}],
                      [{'HostIp': '127.0.0.1', 'HostPort': '1234'}] * 2):
            with self.assertRaises(ValueError): m.loopback_port({'NetworkSettings': {'Ports': {'1433/tcp': binds}}})
        self.assertEqual(1234, m.loopback_port({'NetworkSettings': {'Ports': {'1433/tcp': [{'HostIp': '127.0.0.1', 'HostPort': '1234'}]}}}))

    def test_cleanup_never_deletes_foreign_resource_and_does_not_claim_success(self):
        m = module(); calls = []; token = 'a' * 24
        class Fake(m.Docker):
            def inspect(self, kind, name):
                return {'Id': 'b' * 64, 'Name': name, 'Config': {'Labels': {m.LABEL: 'foreign'}}} if kind == 'container' else None
            def call(self, args, **kwargs): calls.append(args)
        d = Fake('/docker', 'desktop-linux', {}, 'secret')
        with self.assertRaises(ValueError): d.cleanup(token)
        self.assertEqual([], calls)

    def test_inspect_failure_is_not_absence_and_blocks_delete(self):
        m = module(); calls = []
        class Fake(m.Docker):
            def inspect(self, kind, name): raise RuntimeError('daemon unavailable')
            def call(self, args, **kwargs): calls.append(args)
        with self.assertRaises(RuntimeError): Fake('/docker', 'desktop-linux', {}, 'secret').cleanup('a' * 24)
        self.assertEqual([], calls)

    def test_failed_create_still_runs_owned_cleanup_and_cannot_report_success(self):
        m = module(); cleanups = []
        class Fake(m.Docker):
            def call(self, args, **kwargs):
                if args[:2] == ['image', 'inspect']:
                    return json.dumps([{'Id': 'b' * 64, 'RepoDigests': [m.IMAGE], 'Config': {'User': 'mssql'}}])
                raise RuntimeError('create timed out')
            def inspect(self, kind, name): return None
            def cleanup(self, token): cleanups.append(token)
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            args = Namespace(profile='core', output='.artifacts/qa/failure', docker='docker', dotnet='dotnet')
            with patch.object(m, 'Docker', Fake), patch.object(m, 'process', side_effect=[
                (0, 'desktop-linux'), (0, json.dumps([{'Endpoints': {'docker': {'Host': 'npipe://local'}}}]))]):
                self.assertEqual(1, m.run(args, root))
            report = json.loads((root / args.output / 'summary.json').read_text())
            self.assertEqual(1, len(cleanups)); self.assertTrue(report['cleanupConfirmed'])
            self.assertFalse(report['passed']); self.assertIn('create timed out', report['errors'][0])

    def test_cleanup_verifies_deletion_instead_of_trusting_rm_exit(self):
        m = module(); token = 'a' * 24; calls = []
        class Fake(m.Docker):
            def inspect(self, kind, name):
                if kind == 'network': return None
                return {'Id': 'b' * 64, 'Name': '/' + name, 'Config': {'Labels': {m.LABEL: token}}}
            def call(self, args, **kwargs): calls.append(args); return ''
        with self.assertRaises(RuntimeError): Fake('/docker', 'desktop-linux', {}, 'secret').cleanup(token)
        self.assertEqual([['rm', '--force', '--volumes', 'b' * 64]], calls)

    def test_sql_connection_always_targets_generated_loopback_master(self):
        m = module()
        self.assertEqual('Server=127.0.0.1,1234;Database=master;User ID=sa;Password=QA_safe123!;Encrypt=True;TrustServerCertificate=True;Connect Timeout=15', m.connection(1234, 'QA_safe123!'))
        for port, password in ((0, 'safe'), (65536, 'safe'), (1234, 'secret;Server=foreign')):
            with self.assertRaises(ValueError): m.connection(port, password)

    def test_profiles_do_not_accidentally_select_skipped_load_or_restore(self):
        m = module()
        core = m.suites('core', Path('/qa'))
        self.assertEqual(6, len(core))
        self.assertIn('EmailWorkerService', [s['project'] for s in core])
        self.assertTrue(all('!~SyntheticLoadSqlTests' in s['filter'] for s in core))
        self.assertEqual({}, core[0]['environment'])
        load = m.suites('load', Path('/qa'))
        self.assertEqual('enabled', load[0]['environment']['DAS_SYNTHETIC_LOAD'])
        restore = m.suites('restore', Path('/qa'))
        self.assertEqual('synthetic', restore[0]['environment']['DAS_RESTORE_DRILL'])
        self.assertEqual(9, len(m.suites('all', Path('/qa'))))
        self.assertTrue(all('!~EmailRestoreSqlTests' in s['filter'] for s in core))
        self.assertEqual(['restore', 'restore-email'], [s['name'] for s in restore])
        self.assertEqual('EmailWorkerService', restore[1]['project'])
        self.assertEqual('FullyQualifiedName~EmailRestoreSqlTests', restore[1]['filter'])
        self.assertEqual(Path('/qa/restore-email'), Path(restore[1]['environment']['DAS_RESTORE_OUTPUT']))

    def test_focused_core_profile_cannot_silently_select_load_restore_or_unknown_service(self):
        m = module()
        selected = m.suites('core', Path('/qa'), 'EmailWorkerService')
        self.assertEqual(['EmailWorkerService'], [s['project'] for s in selected])
        for profile in ('all', 'restore', 'load'):
            with self.assertRaises(ValueError): m.suites(profile, Path('/qa'), 'EmailWorkerService')
        with self.assertRaises(ValueError): m.suites('core', Path('/qa'), 'ForeignService')

    def test_trx_rejects_empty_failed_or_skipped_execution(self):
        m = module()
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'test.trx'
            for total, passed, failed, skipped in ((0, 0, 0, 0), (2, 1, 1, 0), (2, 1, 0, 1)):
                path.write_text(f'<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><ResultSummary><Counters total="{total}" executed="{passed+failed}" passed="{passed}" failed="{failed}" notExecuted="{skipped}" /></ResultSummary></TestRun>')
                with self.assertRaises(ValueError): m.trx_counts(path)

    def test_backup_evidence_requires_five_real_nonempty_files(self):
        m = module()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            with self.assertRaises(ValueError): m.backup_evidence(root)
            for name in m.COMPONENTS: (root / (name + '.bak')).write_bytes(b'synthetic backup bytes')
            self.assertEqual(5, len(m.backup_evidence(root)))
            (root / 'auth.bak').write_bytes(b'')
            with self.assertRaises(ValueError): m.backup_evidence(root)

    def restore_fixture(self, root, m):
        bundle = root / 'bundle'; (bundle / 'backups').mkdir(parents=True); (bundle / 'storage').mkdir()
        for name in m.COMPONENTS: (bundle / 'backups' / (name + '.bak')).write_bytes(b'synthetic backup')
        pdf = b'%PDF-synthetic'; (bundle / 'storage' / 'fixture.pdf').write_bytes(pdf)
        hashes = {name: 'a' * 64 for name in m.COMPONENTS}
        return {'passed': True, 'synthetic': True, 'productionReady': False, 'cutId': str(uuid.uuid4()),
                'profile': 'core-five-stores', 'components': list(m.COMPONENTS), 'backupCount': 5,
                'beforeDataSha256': hashes, 'afterDataSha256': hashes.copy(), 'allWorkersStarted': False,
                'noSmtpResendCalls': 0, 'pdfBytes': len(pdf), 'pdfSha256': hashlib.sha256(pdf).hexdigest()}

    def test_bundle_verification_preserves_real_cut_and_detects_changed_or_extra_artifact(self):
        m = module()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); report = self.restore_fixture(root, m)
            result = m.make_restore_bundle(root, report)
            self.assertTrue(result['integrityPassed']); self.assertEqual(6, result['artifactCount'])
            manifest = json.loads((root / 'bundle/manifest.json').read_text())
            self.assertEqual(report['cutId'], manifest['cutId']); self.assertEqual(2, manifest['version'])
            (root / 'bundle/backups/auth.bak').write_bytes(b'changed')
            self.assertFalse(m.bundle_verifier.verify(manifest, root / 'bundle')['integrityPassed'])
            (root / 'bundle/extra.pdf').write_bytes(b'extra')
            self.assertIn('UNLISTED_OR_MISSING_ARTIFACT', m.bundle_verifier.verify(manifest, root / 'bundle')['errors'])

    def test_restore_bundle_cannot_claim_acceptance_when_SQL_report_not_successful(self):
        m = module()
        for change in ({'passed': False}, {'allWorkersStarted': True}, {'noSmtpResendCalls': 1},
                       {'backupCount': 3}, {'pdfSha256': 'b' * 64}, {'afterDataSha256': {}}):
            with self.subTest(change=change), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp); report = self.restore_fixture(root, m); report.update(change)
                with self.assertRaises(ValueError): m.make_restore_bundle(root, report)

    def email_restore_fixture(self, root):
        (root / 'emailworker.bak').write_bytes(b'owned synthetic backup')
        return {'passed': True, 'synthetic': True, 'productionReady': False,
                'profile': 'email-worker-store', 'components': ['emailworker'],
                'cutId': str(uuid.uuid4()), 'backupCount': 1, 'allWorkersStarted': False,
                'beforeDataSha256': {'emailworker': 'a' * 64}, 'afterDataSha256': {'emailworker': 'a' * 64},
                'rowCounts': {'settings': 1, 'scanLogs': 1, 'scanItems': 5, 'migrations': 1},
                'workerFlags': {k: False for k in ('EmailIntake__WorkerEnabled', 'EmailIntake__ManualScanEnabled', 'Delivery__WorkerEnabled', 'Reminders__Enabled')}}

    def test_email_restore_gate_keeps_separate_cut_and_detects_backup_changes(self):
        m = module()
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp); report=self.email_restore_fixture(root)
            expected=hashlib.sha256(b'owned synthetic backup').hexdigest()
            evidence=m.email_restore_evidence(root, report, expected)
            self.assertEqual(report['cutId'], evidence['cutId'])
            self.assertFalse(evidence['productionReady'])
            self.assertEqual(1, evidence['artifactCount'])
            self.assertEqual(hashlib.sha256(b'owned synthetic backup').hexdigest(), evidence['backup']['sha256'])
            (root / 'emailworker.bak').write_bytes(b'changed')
            with self.assertRaises(ValueError): m.email_restore_evidence(root, report, expected)

    def test_email_restore_gate_rejects_incomplete_or_enabled_worker_report_and_missing_backup(self):
        m = module()
        for change in ({'passed': False}, {'productionReady': True}, {'allWorkersStarted': True},
                       {'backupCount': True}, {'backupCount': 6}, {'components': ['auth','emailworker']},
                       {'afterDataSha256': {'emailworker':'b'*64}}, {'beforeDataSha256': {}},
                       {'cutId': '../escape'}, {'workerFlags': {}},
                       {'rowCounts': {'settings': 1,'scanLogs': 1,'scanItems': 4,'migrations': 1}}):
            with self.subTest(change=change), tempfile.TemporaryDirectory() as tmp:
                root=Path(tmp); report=self.email_restore_fixture(root); report.update(change)
                with self.assertRaises(ValueError): m.email_restore_evidence(root,report,hashlib.sha256(b'owned synthetic backup').hexdigest())
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp); report=self.email_restore_fixture(root)
            report['workerFlags']['EmailIntake__WorkerEnabled']=True
            with self.assertRaises(ValueError): m.email_restore_evidence(root,report,hashlib.sha256(b'owned synthetic backup').hexdigest())
            report['workerFlags']['EmailIntake__WorkerEnabled']=False
            (root/'emailworker.bak').unlink()
            with self.assertRaises(ValueError): m.email_restore_evidence(root,report,hashlib.sha256(b'owned synthetic backup').hexdigest())


if __name__ == '__main__': unittest.main()
