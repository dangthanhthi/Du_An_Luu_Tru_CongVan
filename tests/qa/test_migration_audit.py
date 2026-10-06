import importlib.util, pathlib, tempfile, unittest, uuid, hashlib
path=pathlib.Path(__file__).resolve().parents[2]/'scripts/qa/audit-migration-export.py'
spec=importlib.util.spec_from_file_location('migration_audit',path);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
class MigrationAuditTests(unittest.TestCase):
    def fixture(self):
        return {'documents':[{'id':str(uuid.uuid4()),'kind':'INTERNAL','registrationDate':'2026-10-01','sequenceNumber':9999,'registrationNumber':'existing-original-number','companyCode':'HL','departmentCode':'ADM','ownerDepartmentId':str(uuid.uuid4()),'inputterUserId':str(uuid.uuid4()),'originatorUserId':str(uuid.uuid4()),'status':'InProgress','sensitivity':'Normal','issuedDate':None}], 'counters':[{'kind':'INTERNAL','year':2026,'currentValue':9999}]}
    def test_preserves_existing_numbers_and_source(self):
        source=self.fixture();before=repr(source);result=module.audit(source);self.assertTrue(result['passed']);self.assertEqual(before,repr(source));self.assertFalse(result['mutated'])
    def test_duplicate_and_undersized_counter_are_blocked(self):
        source=self.fixture();source['documents'].append(dict(source['documents'][0]));source['counters'][0]['currentValue']=9;result=module.audit(source);self.assertFalse(result['passed']);self.assertIn('INVALID_OR_UNDERSIZED_COUNTER',result['errors'])
    def test_hash_size_and_path_containment(self):
        source=self.fixture()
        with tempfile.TemporaryDirectory() as tmp:
            root=pathlib.Path(tmp);content=b'%PDF-fixture';(root/'fixture.pdf').write_bytes(content);source['documents'][0]['pdf']={'relativePath':'fixture.pdf','sha256':hashlib.sha256(content).hexdigest(),'sizeBytes':len(content)}
            self.assertTrue(module.audit(source,root)['passed']);source['documents'][0]['pdf']['relativePath']='../outside.pdf';self.assertFalse(module.audit(source,root)['passed'])
    def test_missing_mapping_and_counter_do_not_get_fabricated(self):
        source=self.fixture();source['documents'][0]['departmentCode']='';source['counters']=[];self.assertFalse(module.audit(source)['passed'])
if __name__=='__main__':unittest.main()
