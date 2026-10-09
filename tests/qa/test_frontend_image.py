import importlib.util
from pathlib import Path
import tempfile
import unittest

SCRIPT=Path(__file__).resolve().parents[2]/'scripts/qa/build-frontend-image.py'

class FrontendImageTests(unittest.TestCase):
    def module(self):
        self.assertTrue(SCRIPT.is_file(),'Frontend image builder not implemented')
        spec=importlib.util.spec_from_file_location('frontend_image',SCRIPT)
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module

    def fixture(self,root):
        for name in ('package.json','package-lock.json','next.config.ts','tsconfig.json','postcss.config.mjs','declarations.d.ts'):
            (root/name).write_text('{}')
        (root/'.npmrc').write_text('legacy-peer-deps=true\n')
        (root/'src').mkdir();(root/'src/app.ts').write_text('export const value = 1')
        (root/'public').mkdir();(root/'public/image.svg').write_text('<svg/>')

    def test_context_excludes_env_backend_credentials_and_generated_trees(self):
        module=self.module()
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);self.fixture(root)
            for name in ('src/.env.local','src/private.pem','src/node_modules/test.js','src/.secrets/config.json','public/.git/key','Intern-DocumentAdministration-BE/config.cs'):
                path=root/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_text('synthetic excluded input')
            target=root/'.artifacts/qa/front/context'
            record=module.export_context(root,target)
            self.assertIn('src/app.ts',record['files']);self.assertIn('public/image.svg',record['files'])
            self.assertEqual(9,len(record['files']))
            self.assertFalse((target/'src/.env.local').exists())
            self.assertFalse((target/'Intern-DocumentAdministration-BE').exists())
            with self.assertRaises(ValueError):module.export_context(root,root/'.artifacts/qa/../escaped')

    def test_context_rejects_overwrite_outside_QA_missing_input_and_registry_credentials(self):
        module=self.module()
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);self.fixture(root);target=root/'.artifacts/qa/front/context'
            with self.assertRaises(ValueError):module.export_context(root,root/'outside')
            (root/'.npmrc').write_text('//registry.npmjs.org/:_authToken=synthetic-do-not-copy')
            with self.assertRaises(ValueError):module.export_context(root,target)
            self.assertFalse(target.exists())
            (root/'.npmrc').write_text('legacy-peer-deps=true\n')
            (root/'package-lock.json').unlink()
            with self.assertRaises(ValueError):module.export_context(root,target)
            (root/'package-lock.json').write_text('{}')
            module.export_context(root,target)
            with self.assertRaises(ValueError):module.export_context(root,target)

    def test_official_base_digest_rejects_mutable_foreign_and_ambiguous_refs(self):
        module=self.module();valid='node@sha256:'+'a'*64
        self.assertEqual(valid,module.official_node_digest([valid]))
        for refs in (['node:22-bookworm'],['foreign@sha256:'+'a'*64],[valid,'node@sha256:'+'b'*64]):
            with self.assertRaises(ValueError):module.official_node_digest(refs)

    def test_smoke_rejects_root_wrong_entrypoint_and_missing_source_identity(self):
        script=SCRIPT.parent/'run-frontend-image-smoke.py'
        spec=importlib.util.spec_from_file_location('frontend_smoke',script)
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        image_id='sha256:'+'a'*64
        image={'Id':image_id,'Config':{'User':'1000:1000','Cmd':['node','server.js'],
              'Labels':{'das.qa.frontend':'prepared-candidate','das.source-sha256':'b'*64}}}
        self.assertEqual('b'*64,module.image_boundary(image,image_id))
        import copy
        for field,value in [('User','0'),('Cmd',['npm','run','dev']),('Labels',{})]:
            invalid=copy.deepcopy(image);invalid['Config'][field]=value
            with self.assertRaises(ValueError):module.image_boundary(invalid,image_id)
        with self.assertRaises(ValueError):module.image_boundary(image,'sha256:'+'c'*64)

    def test_smoke_cannot_forward_case_variant_host_session_key(self):
        script=SCRIPT.parent/'run-frontend-image-smoke.py'
        spec=importlib.util.spec_from_file_location('frontend_smoke_env',script)
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        base={'NEXTAUTH_SECRET':'synthetic-host-value','nextauth_secret':'synthetic-other-host-value','Other':'keep'}
        original=base.copy();env=module.smoke_environment(base,'synthetic-qa-value')
        self.assertEqual(original,base)
        self.assertEqual(['synthetic-qa-value'],[v for k,v in env.items() if k.upper()=='NEXTAUTH_SECRET'])
        self.assertEqual('keep',env['Other'])

    def test_smoke_initializes_fresh_report_before_container_creation(self):
        import json
        import subprocess
        from unittest.mock import patch
        script=SCRIPT.parent/'run-frontend-image-smoke.py'
        spec=importlib.util.spec_from_file_location('frontend_smoke_output',script)
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        image_id='sha256:'+'a'*64
        image={'Id':image_id,'Config':{'User':'1000:1000','Cmd':['node','server.js'],
              'Labels':{'das.qa.frontend':'prepared-candidate','das.source-sha256':'b'*64}}}
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);output=root/'.artifacts/qa/smoke'
            def fake_cli(args,**kwargs):
                if args[:3]==['docker','image','inspect']:
                    return subprocess.CompletedProcess(args,0,json.dumps([image]),'')
                raise RuntimeError('synthetic container creation failed')
            with patch.object(module,'ROOT',root),patch.object(module,'cli',fake_cli),patch.object(module.linux,'inspect',return_value=None),patch('sys.argv',['smoke','--image',image_id,'--directory','.artifacts/qa/smoke']):
                # Keep scripts available while isolating the output directory.
                (root/'scripts/qa').mkdir(parents=True)
                import shutil
                shutil.copyfile(SCRIPT.parent/'build-core-images.py',root/'scripts/qa/build-core-images.py')
                self.assertEqual(1,module.main())
            report=json.loads((output/'summary.json').read_text())
            self.assertIn('creation failed',report['error'])
            self.assertTrue(report['cleanupPassed'])

    def test_template_page_accepts_streamed_404_behind_initial_session_gate(self):
        script=SCRIPT.parent/'run-frontend-image-smoke.py'
        spec=importlib.util.spec_from_file_location('frontend_smoke_page',script)
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        boundary=getattr(module,'template_page_boundary',None)
        self.assertTrue(callable(boundary),'Smoke must recognize the current SSR session/404 boundary')
        for gate in ('Đang kiểm tra phiên…','Checking session…'):
            html=('<div role="status">'+gate+'</div><script>NEXT_HTTP_ERROR_FALLBACK;404</script>').encode()
            self.assertTrue(boundary(200,html))
        self.assertTrue(boundary(404,b'not found'))
        self.assertTrue(boundary(200,b'<meta name="robots" content="noindex"><script>NEXT_HTTP_ERROR_FALLBACK;404</script>'))

    def test_template_page_rejects_session_shell_or_error_marker_alone(self):
        script=SCRIPT.parent/'run-frontend-image-smoke.py'
        spec=importlib.util.spec_from_file_location('frontend_smoke_page_negative',script)
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        boundary=getattr(module,'template_page_boundary',None)
        self.assertTrue(callable(boundary),'Smoke must require both a closed session gate and 404 evidence')
        for html in (b'<div role="status">Checking session...</div>',b'<script>NEXT_HTTP_ERROR_FALLBACK;404</script>',b'<html>user list</html>'):
            self.assertFalse(boundary(200,html))
        self.assertFalse(boundary(500,b'not found'))

if __name__=='__main__':unittest.main()
