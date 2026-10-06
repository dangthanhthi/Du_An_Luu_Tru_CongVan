import importlib.util
from pathlib import Path
import copy
import unittest

SCRIPT=Path(__file__).resolve().parents[2]/'scripts/qa/verify-core-runtime.py'


class CoreRuntimeTests(unittest.TestCase):
    def module(self):
        self.assertTrue(SCRIPT.is_file(),'Runtime verifier has not been implemented')
        spec=importlib.util.spec_from_file_location('core_runtime',SCRIPT)
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module

    def fixture(self,module):
        return {'passed':True,'productionReady':False,'images':{service:{'imageId':'sha256:'+'a'*64,'sourceSha256':'b'*64,'uid':'1654','entrypoint':['dotnet',assembly]} for service,assembly in module.ASSEMBLIES.items()}}

    def test_exact_six_candidate_ids_and_entrypoints_required(self):
        module=self.module();original=self.fixture(module);module.validate_summary(original)
        for alter in (lambda s:s.update(passed=False),lambda s:s.update(productionReady=True),lambda s:s['images'].pop('auth'),
                      lambda s:s['images']['auth'].update(imageId='mutable:tag'),lambda s:s['images']['auth'].update(uid='0'),
                      lambda s:s['images']['auth'].update(entrypoint=['sh','-c','command']),
                      lambda s:s['images'].update(ocr=s['images']['auth'])):
            changed=copy.deepcopy(original);alter(changed)
            with self.assertRaises(ValueError):module.validate_summary(changed)

    def test_missing_config_gate_requires_expected_failure_before_listening(self):
        module=self.module()
        self.assertTrue(module.config_blocked('partner',134,'InvalidOperationException: Jwt:Secret must be provisioned'))
        self.assertFalse(module.config_blocked('partner',0,'Jwt:Secret must be provisioned'))
        self.assertFalse(module.config_blocked('partner',134,'native library missing'))
        self.assertFalse(module.config_blocked('partner',134,'Jwt:Secret must be provisioned\nNow listening on: http://localhost'))


if __name__=='__main__':unittest.main()
