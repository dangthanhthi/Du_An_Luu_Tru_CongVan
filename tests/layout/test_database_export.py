import importlib.util
import os
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


def module():
    spec = importlib.util.spec_from_file_location('database_export', ROOT / 'tools/export-database-schema.py')
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


class DatabaseExportTests(unittest.TestCase):
    def test_child_environment_cannot_inherit_live_endpoints_or_secrets(self):
        m = module()
        base = {'PATH': os.environ['PATH'], 'Jwt__Secret': 'foreign-secret', 'ConnectionStrings__Default': 'foreign-db',
                'Database__Initialize': 'true', 'SMTP__Password': 'foreign-secret', 'EmailIntake__WorkerEnabled': 'true'}
        with tempfile.TemporaryDirectory() as temporary:
            env = m.environment(base, Path(temporary))
        for key in ('Jwt__Secret', 'ConnectionStrings__Default', 'SMTP__Password'):
            self.assertNotIn(key, env)
        self.assertEqual('false', env['EmailIntake__WorkerEnabled'])
        self.assertEqual('false', env['Database__Initialize'])
        self.assertEqual('true', base['Database__Initialize'])

    def test_all_six_stores_have_design_factory_and_pinned_compatible_tool(self):
        m = module()
        self.assertEqual(6, len(m.STORES))
        for store in m.STORES:
            self.assertTrue((ROOT / store['project']).is_file())
            self.assertTrue((ROOT / store['factory']).is_file())
            text = (ROOT / store['factory']).read_text(encoding='utf-8-sig')
            self.assertIn('IDesignTimeDbContextFactory', text)
            self.assertNotIn('AddEnvironmentVariables', text)
            self.assertNotIn('GetConnectionString', text)
            self.assertIn(store['ef'], ('9.0.0', '10.0.3'))

    def test_offline_commands_never_apply_or_connect_and_output_is_fresh(self):
        m = module()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            output = m.fresh_output(root, '.artifacts/qa/schema')
            output.mkdir(parents=True)
            with self.assertRaises(ValueError): m.fresh_output(root, '.artifacts/qa/schema')
            with self.assertRaises(ValueError): m.fresh_output(root, '../foreign')
        commands = m.schema_commands('ef', m.STORES[0], Path('schema.sql'))
        self.assertEqual(['has-pending-model-changes', 'list', 'script'], [c[2] for c in commands])
        self.assertIn('--no-connect', commands[1])
        self.assertIn('--idempotent', commands[2])
        self.assertTrue(all('--no-build' in c for c in commands))
        self.assertFalse(any('update' in c or '--connection' in c for c in commands))
