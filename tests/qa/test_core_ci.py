import importlib.util
from pathlib import Path
import sys
import tempfile
import subprocess
import unittest

SCRIPT = Path(__file__).resolve().parents[2] / 'scripts/qa/run-core-ci.py'


class CoreCiTests(unittest.TestCase):
    def test_ci_child_cannot_inherit_enabled_workers_or_case_variant_SQL_fixture(self):
        module=self.module()
        base={'DELIVERY__WORKERENABLED':'true','reminders__enabled':'true','das_test_sql_connection':'synthetic-only',
              'DAS_RESTORE_OUTPUT':'synthetic-output','Other':'keep','PATH':'old-path','node_options':'--inspect=127.0.0.1:9229'}
        original=base.copy();env=module.ci_environment(base,sys.executable)
        self.assertEqual(original,base)
        for flag in ('DELIVERY__WORKERENABLED','REMINDERS__ENABLED'):
            matches=[v for k,v in env.items() if k.upper()==flag]
            self.assertEqual(['false'],matches)
        self.assertFalse(any(k.upper() in {'DAS_TEST_SQL_CONNECTION','DAS_RESTORE_DRILL','DAS_RESTORE_OUTPUT'} for k in env))
        self.assertEqual('keep',env['Other'])
        self.assertEqual(['--max-old-space-size=2304'],[v for k,v in env.items() if k.upper()=='NODE_OPTIONS'])

    def module(self):
        self.assertTrue(SCRIPT.is_file(), 'Core CI runner has not been implemented')
        spec = importlib.util.spec_from_file_location('core_ci', SCRIPT)
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module); return module

    def test_nuget_audit_requires_real_project_report_and_rejects_errors(self):
        module = self.module()
        for data in ({}, {'projects':[]}, {'version':1,'parameters':'--vulnerable --include-transitive','projects':[{'path':'fixture.csproj'}],'errors':['feed failed']}):
            with self.assertRaises(ValueError): module.nuget_counts(data)
        clear = {'version':1,'parameters':'--vulnerable --include-transitive','projects':[{'path':'fixture.csproj'}]}
        self.assertEqual(0,module.nuget_counts(clear))
        clear['projects'][0]['frameworks']=[{'framework':'net10.0','transitivePackages':[{'id':'fixture','vulnerabilities':[{'severity':'High'}]}]}]
        self.assertEqual(1,module.nuget_counts(clear))

    def test_trx_requires_executed_passes_and_no_failures_or_skips(self):
        module = self.module()
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'test.trx'
            for passed, failed, skipped in [(0,0,0),(2,1,0),(2,0,1)]:
                path.write_text(f'<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><ResultSummary><Counters total="{passed+failed+skipped}" executed="{passed+failed}" passed="{passed}" failed="{failed}" notExecuted="{skipped}" /></ResultSummary></TestRun>')
                with self.assertRaises(ValueError): module.trx_counts(path)
            path.write_text('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><ResultSummary><Counters total="2" executed="2" passed="2" failed="0" notExecuted="0" /></ResultSummary></TestRun>')
            self.assertEqual(2, module.trx_counts(path)['passed'])

    def test_trx_missing_or_malformed_is_not_success(self):
        module = self.module()
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / 'absent.trx'
            for content in (None, '<broken', '<TestRun />'):
                if content is not None: path.write_text(content)
                with self.assertRaises((ValueError, OSError)): module.trx_counts(path)

    def test_tap_empty_failed_or_skipped_suite_rejected(self):
        module = self.module()
        for text in ('', '# tests 0\n# pass 0\n# fail 0\n# skipped 0', '# tests 2\n# pass 1\n# fail 1\n# skipped 0', '# tests 2\n# pass 1\n# fail 0\n# skipped 1'):
            with self.assertRaises(ValueError): module.tap_counts(text)
        self.assertEqual(2, module.tap_counts('# tests 2\n# pass 2\n# fail 0\n# skipped 0\n# cancelled 0')['passed'])

    def test_output_cannot_overwrite_or_escape_QA(self):
        module = self.module()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); valid = '.artifacts/qa/core-run'
            self.assertEqual(root / valid, module.fresh_output(root, valid))
            (root / valid).mkdir(parents=True)
            for name in (valid, '../outside', 'src', '.artifacts/qa/../not-qa', '.artifacts/qa'):
                with self.assertRaises(ValueError): module.fresh_output(root, name)

    def test_source_export_omits_credentials_build_outputs_and_deferred_models(self):
        module = self.module()
        for name in ('src/.env', 'Intern-DocumentAdministration-BE/.env.local', 'src/key.pem', 'node_modules/x.js', 'src/node_modules/x.js', 'Intern-DocumentAdministration-BE/services/document-service/bin/app.dll', 'Intern-DocumentAdministration-BE/services/ai-ocr-service/model.onnx', '.artifacts/qa/report.json'):
            self.assertFalse(module.include_source(name), name)
        for name in ('package-lock.json', '.npmrc', '.eslintrc.cjs', 'src/services/api.ts', 'Intern-DocumentAdministration-BE/.env.example', 'Intern-DocumentAdministration-BE/services/document-service/packages.lock.json'):
            self.assertTrue(module.include_source(name), name)

    def test_locked_install_keeps_network_diagnostics_in_owned_report_directory(self):
        from unittest.mock import patch
        module = self.module()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            cli = root / 'npm-cli.js'; cli.write_text('// never executed')
            calls = []
            def failed_step(name, command, *args):
                calls.append((name, command))
                return {'name': name, 'passed': False, 'exitCode': 1}
            with patch.object(module, 'run_step', side_effect=failed_step):
                report = module.run_profile('web', root, root, sys.executable, str(cli), 'dotnet')
            self.assertFalse(report['passed'])
            self.assertEqual(['npm-ci'], [name for name, _ in calls])
            install = calls[0][1]
            for required in ('ci', '--ignore-scripts', '--no-audit', '--no-fund', '--maxsockets=4', '--loglevel=http', '--logs-dir=' + str(root / 'npm-debug')):
                self.assertIn(required, install)
            self.assertNotIn('--force', install)
            self.assertNotIn('--offline', install)

    def test_failed_process_keeps_nonzero_result_and_log(self):
        module = self.module()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            result = module.run_step('failure', [sys.executable, '-c', 'print("fixture"); raise SystemExit(7)'], root, root, {}, 30)
            self.assertEqual(7, result['exitCode']); self.assertFalse(result['passed'])
            self.assertIn('fixture', (root / 'failure.log').read_text())

    def test_actual_WIP_export_has_hashes_and_excludes_env_and_prior_artifacts(self):
        module = self.module()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            subprocess.run(['git','init','--quiet',str(root)],check=True,capture_output=True)
            (root/'src').mkdir(); (root/'src/example.ts').write_text('export const value = 42')
            (root/'src/.env').write_text('synthetic credential, never export')
            (root/'package.json').write_text('{}')
            subprocess.run(['git','-C',str(root),'add','src','package.json'],check=True,capture_output=True)
            target = module.export_source(root,'.artifacts/qa/export')
            self.assertTrue((target/'src/example.ts').is_file()); self.assertFalse((target/'src/.env').exists())
            self.assertTrue((target.parent/'export-manifest.json').is_file())
            prior = root/'.artifacts/qa/other-manifest.json'; prior.write_text('prior evidence')
            with self.assertRaises(ValueError): module.export_source(root,'.artifacts/qa/other')
            self.assertEqual('prior evidence', prior.read_text())


if __name__ == '__main__': unittest.main()
