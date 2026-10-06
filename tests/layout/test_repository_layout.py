from pathlib import Path
import json,unittest

ROOT=Path(__file__).resolve().parents[2]

class RepositoryLayoutTests(unittest.TestCase):
    def test_one_canonical_frontend_and_backend(self):
        for name in ('frontend/package.json','frontend/package-lock.json','frontend/src/services/api.ts','backend/DocumentAdministration.slnx'):
            self.assertTrue((ROOT/name).is_file(),name)
        for name in ('DAS-Frontend','DAS','Intern-DocumentAdministration-BE','Intern-DocumentAdministration-FE-Web','src','public'):
            self.assertFalse((ROOT/name).exists(),name)
    def test_database_and_workflow_have_owning_service_links(self):
        self.assertTrue(list((ROOT/'database/migrations').rglob('*.cs')))
        self.assertTrue(list((ROOT/'workflows/business').rglob('*.cs')))
        props=(ROOT/'backend/Directory.Build.props').read_text()
        self.assertIn('database',props);self.assertIn('workflows',props);self.assertIn('Compile Include=',props)
    def test_configuration_and_collaboration_entrypoints_exist(self):
        for name in ('README.md','CONTRIBUTING.md','docs/COLLABORATION-STATUS.md','database/README.md','workflows/README.md','tools/run-checks.py','tools/create-check-view.py','.github/workflows/core-ci.yml'):
            self.assertTrue((ROOT/name).is_file(),name)
    def test_no_live_or_generated_payloads_in_manifest(self):
        manifest=json.loads((ROOT/'docs/source-layout-manifest.json').read_text())
        self.assertTrue(manifest['files'])
        for entry in manifest['files']:
            p=Path(entry['destination'])
            self.assertFalse(any(s in p.parts for s in ('node_modules','.next','bin','obj','__pycache__','uploads')))
            self.assertFalse(p.name.startswith('.env') and not p.name.endswith('.example'))
            self.assertNotIn(p.suffix.lower(),('.db','.sqlite','.bak','.pfx','.pem','.key'))
    def test_manifest_text_is_lf_on_every_checkout(self):
        self.assertIn('* text=auto eol=lf',(ROOT/'.gitattributes').read_text())
        manifest=json.loads((ROOT/'docs/source-layout-manifest.json').read_text())
        for row in manifest['files']:
            content=(ROOT/row['destination']).read_bytes()
            if b'\0' in content:continue
            try:content.decode('utf-8')
            except UnicodeError:continue
            self.assertNotIn(b'\r\n',content,row['destination'])
    def test_canonical_source_hashes_match_manifest(self):
        import hashlib
        manifest=json.loads((ROOT/'docs/source-layout-manifest.json').read_text())
        for row in manifest['files']:
            path=ROOT/row['destination']
            self.assertTrue(path.is_file(),row['destination'])
            self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(),row['sha256'],row['destination'])

if __name__=='__main__':unittest.main()
