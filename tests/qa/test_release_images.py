import hashlib
import importlib.util
from pathlib import Path
import tempfile
import re
import shlex
import unittest

SCRIPT = Path(__file__).resolve().parents[2] / 'scripts/qa/build-core-images.py'


class ReleaseImageTests(unittest.TestCase):
    def module(self):
        self.assertTrue(SCRIPT.is_file(), 'Image runner has not been implemented')
        spec = importlib.util.spec_from_file_location('release_images', SCRIPT)
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        return module

    def fixture(self, root):
        for name, content in {
            'Directory.Build.props': '<Project/>',
            'services/auth-service/AuthService.csproj': '<Project/>',
            'services/auth-service/packages.lock.json': '{}',
            'services/auth-service/Program.cs': 'class Program {}',
            'services/auth-service/bin/old.cs': 'old output',
            'services/auth-service/appsettings.json': 'private settings',
            'services/auth-service/.env': 'secret',
            'services/auth-service/private.db': 'database',
            'services/auth-service/partners_seed.json': 'demo',
            'services/ai-ocr-service/model.cs': 'deferred',
        }.items():
            path=root/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_text(content,encoding='utf-8')

    def test_context_is_bounded_and_hashes_actual_source(self):
        module=self.module()
        with tempfile.TemporaryDirectory() as tmp:
            source=Path(tmp)/'source';target=Path(tmp)/'context';self.fixture(source)
            manifest=module.export_context(source,target,'auth')
            self.assertEqual({'Directory.Build.props','services/auth-service/AuthService.csproj','services/auth-service/packages.lock.json','services/auth-service/Program.cs'},set(manifest['files']))
            for name,record in manifest['files'].items():
                self.assertEqual(hashlib.sha256((source/name).read_bytes()).hexdigest(),record['sha256'])
                self.assertEqual((source/name).read_bytes(),(target/name).read_bytes())
            self.assertEqual(64,len(manifest['sourceSha256']))

    def test_missing_lock_unknown_service_or_existing_output_blocks(self):
        module=self.module()
        with tempfile.TemporaryDirectory() as tmp:
            source=Path(tmp)/'source';self.fixture(source)
            with self.assertRaises(ValueError):module.export_context(source,Path(tmp)/'out','ocr')
            (source/'services/auth-service/packages.lock.json').unlink()
            with self.assertRaises(ValueError):module.export_context(source,Path(tmp)/'missing','auth')
            occupied=Path(tmp)/'occupied';occupied.mkdir()
            with self.assertRaises(ValueError):module.export_context(source,occupied,'auth')

    def test_linked_source_does_not_enter_image(self):
        module=self.module()
        with tempfile.TemporaryDirectory() as tmp:
            source=Path(tmp)/'source';self.fixture(source)
            path=source/'services/auth-service/Program.cs';path.unlink()
            outside=Path(tmp)/'outside.cs';outside.write_text('private',encoding='utf-8')
            path.symlink_to(outside)
            with self.assertRaises(ValueError):module.export_context(source,Path(tmp)/'out','auth')

    def test_only_official_digest_refs_are_accepted(self):
        module=self.module();digest='a'*64
        self.assertEqual('mcr.microsoft.com/dotnet/sdk@sha256:'+digest,module.official_digest(['mcr.microsoft.com/dotnet/sdk@sha256:'+digest],'sdk'))
        for refs in ([],['mcr.microsoft.com/dotnet/sdk:10.0'],['untrusted/sdk@sha256:'+digest],['mcr.microsoft.com/dotnet/sdk@sha256:bad']):
            with self.assertRaises(ValueError):module.official_digest(refs,'sdk')

    def test_runtime_environment_directive_is_valid_and_contains_disabled_defaults(self):
        module=self.module()
        text=module.dockerfile('auth','sdk@sha256:'+'a'*64,'runtime@sha256:'+'b'*64,'c'*64)
        logical=text.replace('\\\n',' ')
        env={}
        for line in logical.splitlines():
            if line.startswith('ENV '):
                for token in shlex.split(line[4:]):
                    self.assertRegex(token,r'^[A-Za-z_][A-Za-z0-9_]*=.+$')
                    key,value=token.split('=',1);env[key]=value
        self.assertEqual('Production',env['ASPNETCORE_ENVIRONMENT'])
        self.assertTrue(all(env.get(key)=='false' for key in module.DISABLED))


if __name__=='__main__':unittest.main()
