from pathlib import Path
import importlib.util,json,tempfile,unittest
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]
s=importlib.util.spec_from_file_location('view',ROOT/'tools/create-check-view.py');view=importlib.util.module_from_spec(s);s.loader.exec_module(view)

class CheckViewTests(unittest.TestCase):
    def fixture(self,root):
        content={
          'frontend/package.json':json.dumps({'prisma':{'schema':'../database/prisma/schema.prisma'}}),
          'frontend/src/services/api.ts':'export const fixture = 1\n',
          'frontend/.env':'DO_NOT_COPY_FIXTURE',
          'database/prisma/schema.prisma':'generator client {\n  output          = "../../frontend/node_modules/.prisma/client"\n}\n',
          'database/migrations/document-service/Fixture.cs':'migration fixture',
          'workflows/business/document-service/Registration/Fixture.cs':'workflow fixture',
          'backend/Directory.Build.props':'<Project><PropertyGroup><RestorePackagesWithLockFile>true</RestorePackagesWithLockFile></PropertyGroup><PropertyGroup><DasSourceOwner>document-service</DasSourceOwner></PropertyGroup><ItemGroup><Compile Include="$(MSBuildThisFileDirectory)../database/migrations/$(DasSourceOwner)/**/*.cs" /></ItemGroup></Project>'}
        mapping={
          'database/prisma/schema.prisma':'src/prisma/schema.prisma',
          'database/migrations/document-service/Fixture.cs':'Intern-DocumentAdministration-BE/services/document-service/Migrations/Fixture.cs',
          'workflows/business/document-service/Registration/Fixture.cs':'Intern-DocumentAdministration-BE/services/document-service/Services/Registration/Fixture.cs'}
        for name,text in content.items():p=root/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(text)
        (root/'docs').mkdir();(root/'docs/source-layout-manifest.json').write_text(json.dumps({'files':[{'source':source,'destination':name} for name,source in mapping.items()]}))
    def test_view_preserves_business_bytes_and_maps_path_only_adapters(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);self.fixture(root)
            with patch.object(view,'ROOT',root):target=view.create_view('.artifacts/qa/view')
            self.assertFalse((target/'.env').exists())
            self.assertEqual((target/'src/services/api.ts').read_text(),'export const fixture = 1\n')
            self.assertEqual((target/'Intern-DocumentAdministration-BE/services/document-service/Services/Registration/Fixture.cs').read_text(),'workflow fixture')
            self.assertEqual(json.loads((target/'package.json').read_text())['prisma']['schema'],'./src/prisma/schema.prisma')
            self.assertNotIn('DasSourceOwner',(target/'Intern-DocumentAdministration-BE/Directory.Build.props').read_text())
            report=json.loads((target.parent/'view-manifest.json').read_text())
            self.assertEqual(sum(bool(r['adaptation']) for r in report['files']),3)
            self.assertFalse(report['canonicalSourceModified']);self.assertIn('DasSourceOwner',(root/'backend/Directory.Build.props').read_text())
    def test_legacy_view_can_build_after_canonical_prisma_generation_changes(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);self.fixture(root)
            package=root/'frontend/package.json';value=json.loads(package.read_text())
            value['scripts']={'build':'node scripts/generate-prisma.cjs && next build --webpack','generate:prisma':'node scripts/generate-prisma.cjs'}
            package.write_text(json.dumps(value))
            with patch.object(view,'ROOT',root):target=view.create_view('.artifacts/qa/view')
            scripts=json.loads((target/'package.json').read_text())['scripts']
            self.assertEqual(scripts['build'],'prisma generate && next build --webpack')
            self.assertNotIn('generate:prisma',scripts)
    def test_view_rejects_occupied_and_escaping_targets(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);self.fixture(root)
            with patch.object(view,'ROOT',root):
                for path in ('../outside','other/view','.artifacts/qa','/tmp/view','.artifacts/qa/../escape'):
                    with self.subTest(path=path),self.assertRaises(ValueError):view.create_view(path)
                view.create_view('.artifacts/qa/view')
                with self.assertRaises(ValueError):view.create_view('.artifacts/qa/view')
    def test_local_provenance_label_does_not_displace_backend_source(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);self.fixture(root)
            name='backend/services/document-service/Models/DTOs/V2ReadContracts.cs'
            source=root/name;source.parent.mkdir(parents=True,exist_ok=True)
            source.write_text('namespace DocumentService; public sealed record ReadDetails();')
            manifest=root/'docs/source-layout-manifest.json';value=json.loads(manifest.read_text())
            value['files'].append({'source':'Local read-contract refinement 2026-10-09','destination':name})
            manifest.write_text(json.dumps(value))
            with patch.object(view,'ROOT',root):target=view.create_view('.artifacts/qa/view')
            expected=target/'Intern-DocumentAdministration-BE/services/document-service/Models/DTOs/V2ReadContracts.cs'
            self.assertTrue(expected.is_file(),'Local DTO must remain in the compiling service source scope')
            self.assertEqual(expected.read_bytes(),source.read_bytes())
            self.assertFalse((target/'Local read-contract refinement 2026-10-09').exists())
    def test_view_does_not_accept_unsafe_manifest_paths(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);self.fixture(root)
            path=root/'docs/source-layout-manifest.json';path.write_text(json.dumps({'files':[{'destination':'frontend/src/services/api.ts','source':'../outside.ts'}]}))
            with patch.object(view,'ROOT',root),self.assertRaises(ValueError):view.create_view('.artifacts/qa/view')
            self.assertFalse((root/'.artifacts/qa/view').exists())
    def test_provenance_fallback_does_not_hide_windows_or_parent_paths(self):
        for source in ('..',r'C:\outside.ts',r'\\server\share\file.ts'):
            with self.subTest(source=source),tempfile.TemporaryDirectory() as temp:
                root=Path(temp);self.fixture(root)
                path=root/'docs/source-layout-manifest.json'
                path.write_text(json.dumps({'files':[{'destination':'frontend/src/services/api.ts','source':source}]}))
                with patch.object(view,'ROOT',root),self.assertRaises(ValueError):view.create_view('.artifacts/qa/view')
                self.assertFalse((root/'.artifacts/qa/view').exists())

if __name__=='__main__':unittest.main()
