import importlib.util
from pathlib import Path
import tempfile
import json
import unittest

SCRIPT=Path(__file__).resolve().parents[2]/'scripts/qa/license-inventory.py'


class LicenseInventoryTests(unittest.TestCase):
    def module(self):
        self.assertTrue(SCRIPT.is_file(),'Inventory has not been implemented')
        spec=importlib.util.spec_from_file_location('license_inventory',SCRIPT)
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module

    def test_exact_npm_version_and_text_hash_are_required(self):
        module=self.module()
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);package=root/'node_modules/example';package.mkdir(parents=True)
            (package/'package.json').write_text(json.dumps({'name':'example','version':'1.2.3','license':'MIT'}))
            (package/'LICENSE').write_text('Exact license fixture')
            result=module.npm_record(root,'node_modules/example',{'version':'1.2.3','license':'MIT'})
            self.assertEqual('MIT',result['declaredLicense']);self.assertEqual(64,len(result['licenseTexts'][0]['sha256']))
            with self.assertRaises(ValueError):module.npm_record(root,'node_modules/example',{'version':'1.2.4'})
            with self.assertRaises(ValueError):module.npm_record(root,'../outside',{'version':'1.2.3'})

    def test_custom_compound_missing_declarations_stay_reviewable(self):
        module=self.module()
        for declaration in ('SEE LICENSE IN LICENSE.txt','(MIT OR Apache-2.0)',None,{'type':'MIT','url':'https://example.test/license'}):
            result=module.review_class(declaration)
            self.assertNotEqual('Approved',result)
        self.assertEqual('MissingDeclaration',module.review_class(None))
        self.assertEqual('CustomTermsReview',module.review_class('SEE LICENSE IN LICENSE.txt'))

    def test_nuget_file_expression_and_missing_local_package(self):
        module=self.module()
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);package=root/'example/1.0.0';package.mkdir(parents=True)
            (package/'example.nuspec').write_text('<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata><id>Example</id><version>1.0.0</version><license type="file">LICENSE.txt</license></metadata></package>')
            (package/'LICENSE.txt').write_text('Custom license fixture')
            result=module.nuget_record(root,'Example','1.0.0')
            self.assertEqual('file',result['licenseType']);self.assertEqual(1,len(result['licenseTexts']))
            self.assertFalse(result['permissionApproved'])
            self.assertEqual('LocalMetadataMissing',module.nuget_record(root,'Example','2.0.0')['review'])
            with self.assertRaises(ValueError):module.nuget_record(root,'../escape','1.0.0')

    def test_asset_inventory_does_not_invent_provenance(self):
        module=self.module()
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);(root/'public').mkdir();(root/'public/a.svg').write_text('<svg/>')
            (root/'public/config.json').write_text('unrelated')
            rows=module.asset_records(root)
            self.assertEqual(1,len(rows));self.assertEqual('public/a.svg',rows[0]['path'])
            self.assertEqual('OriginAndRightsUnverified',rows[0]['review'])
            self.assertFalse(rows[0]['permissionApproved'])


if __name__=='__main__':unittest.main()
