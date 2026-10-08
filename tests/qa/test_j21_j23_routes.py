"""DAS's gateway registrations, separate from live gateway acceptance."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]


class ReadContractRoutes(unittest.TestCase):
    def setUp(self):
        self.routes = json.loads(((ROOT / 'backend' if (ROOT / 'backend').exists() else ROOT / 'Intern-DocumentAdministration-BE') / 'gateway/ocelot.json').read_text('utf-8-sig'))['Routes']

    def assert_registration(self, path, verbs):
        candidates = [r for r in self.routes if r['UpstreamPathTemplate'] == path]
        self.assertEqual(1, len(candidates), f'Missing or duplicate DAS registration for {path}')
        route = candidates[0]
        self.assertEqual(path, route['DownstreamPathTemplate'])
        self.assertTrue(set(verbs).issubset(route['UpstreamHttpMethod']))
        self.assertEqual(5002, route['DownstreamHostAndPorts'][0]['Port'])
        self.assertNotIn('POST', route['UpstreamHttpMethod'])
        self.assertNotIn('DELETE', route['UpstreamHttpMethod'])

    def test_my_staff_tasks_has_specific_read_route_without_changing_legacy_route(self):
        self.assert_registration('/api/v2/my-staff/tasks', ['GET', 'OPTIONS'])
        self.assert_registration('/api/v2/my-staff', ['GET', 'OPTIONS'])

    def test_catalog_options_read_route_is_separate_from_mutation_wildcard(self):
        self.assert_registration('/api/v2/admin/catalogs/options', ['GET', 'OPTIONS'])
        wildcard = next(r for r in self.routes if r['UpstreamPathTemplate'] == '/api/v2/admin/catalogs/{everything}')
        self.assertIn('PUT', wildcard['UpstreamHttpMethod'])

    def test_catalog_root_supports_admin_browse_and_preserves_create(self):
        root = next(r for r in self.routes if r['UpstreamPathTemplate'] == '/api/v2/admin/catalogs')
        self.assertTrue({'GET', 'POST', 'OPTIONS'}.issubset(root['UpstreamHttpMethod']))
        self.assertEqual(5002, root['DownstreamHostAndPorts'][0]['Port'])

    def test_document_history_has_read_only_specific_route(self):
        self.assert_registration('/api/v2/documents/{documentId}/history', ['GET', 'OPTIONS'])


if __name__ == '__main__':
    unittest.main()
