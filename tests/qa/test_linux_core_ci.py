import importlib.util
from pathlib import Path
import subprocess
import unittest
from unittest.mock import patch

SCRIPT=Path(__file__).resolve().parents[2]/'scripts/qa/run-linux-core-ci.py'

class LinuxCoreCiTests(unittest.TestCase):
    def module(self):
        spec=importlib.util.spec_from_file_location('linux_ci',SCRIPT)
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module

    def result(self,code,stdout='',stderr=''):
        return subprocess.CompletedProcess([],code,stdout,stderr)

    def test_daemon_failure_is_not_container_absence(self):
        module=self.module()
        with patch.object(module,'cli',return_value=self.result(1,stderr='Cannot connect to Docker daemon')):
            with self.assertRaises(RuntimeError):module.inspect('owned-container')

    def test_failed_inspect_of_existing_container_is_not_absence(self):
        module=self.module()
        with patch.object(module,'cli',side_effect=[self.result(1),self.result(0,'owned-container\n')]):
            with self.assertRaises(RuntimeError):module.inspect('owned-container')

    def test_missing_container_requires_successful_inventory(self):
        module=self.module()
        with patch.object(module,'cli',side_effect=[self.result(1),self.result(0,'unrelated-container\n')]) as command:
            self.assertIsNone(module.inspect('owned-container'))
            self.assertEqual(2,command.call_count)

    def test_missing_volume_requires_successful_inventory(self):
        module=self.module()
        with patch.object(module,'cli',return_value=self.result(1,stderr='daemon offline')):
            with self.assertRaises(RuntimeError):module.volume_exists('owned-volume')
        with patch.object(module,'cli',return_value=self.result(0,'unrelated-volume\n')):
            self.assertFalse(module.volume_exists('owned-volume'))
        with patch.object(module,'cli',return_value=self.result(0,'owned-volume\n')):
            self.assertTrue(module.volume_exists('owned-volume'))

if __name__=='__main__':unittest.main()
