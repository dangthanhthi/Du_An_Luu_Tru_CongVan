import importlib.util, pathlib, tempfile, unittest, uuid, hashlib
root=pathlib.Path(__file__).resolve().parents[2]
path=root/'tools/qa/audit-migration-export.py'
if not path.exists():path=root/'scripts/qa/audit-migration-export.py'
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
    def test_top_level_nonobject_is_a_structured_failure(self):
        for value in ([],None,True,42,'export'):
            with self.subTest(value=value):
                report=module.audit(value);self.assertFalse(report['passed']);self.assertIn('INVALID_EXPORT_SCHEMA',report['errors']);self.assertFalse(report['mutated'])
    def test_invalid_counter_years_are_rejected(self):
        for value in (True,False,0,-1,10000,2026.0,'2026',None):
            with self.subTest(value=value):
                source=self.fixture();source['counters'].append({'kind':'OUTGOING','year':value,'currentValue':0})
                self.assertIn('INVALID_OR_UNDERSIZED_COUNTER',module.audit(source)['errors'])
    def test_optional_pdf_must_be_null_or_valid_metadata_even_without_root(self):
        for value in ({},[],False,0,'file.pdf',{'relativePath':'file.pdf','sha256':'bad','sizeBytes':10}):
            with self.subTest(value=value):
                source=self.fixture();source['documents'][0]['pdf']=value;self.assertFalse(module.audit(source)['passed'])
        source=self.fixture();source['documents'][0]['pdf']=None;self.assertTrue(module.audit(source)['passed'])
    def test_pdf_size_and_hash_metadata_are_validated_without_opening_files(self):
        for field,value in [('sizeBytes',True),('sizeBytes',4),('sizeBytes',25*1024*1024+1),('sizeBytes',10.0),('sizeBytes','10'),('sha256',None),('sha256','g'*64),('sha256','a'*63)]:
            with self.subTest(field=field,value=value):
                source=self.fixture();pdf={'relativePath':'file.pdf','sha256':'a'*64,'sizeBytes':10};pdf[field]=value;source['documents'][0]['pdf']=pdf
                self.assertFalse(module.audit(source)['passed'])
    def test_pdf_paths_are_portable_relative_paths_even_without_root(self):
        for value in ('','/tmp/file.pdf','C:/file.pdf',r'C:\file.pdf','../file.pdf','nested/../file.pdf','./file.pdf','nested//file.pdf',r'nested\file.pdf','file.pdf:stream'):
            with self.subTest(value=value):
                source=self.fixture();source['documents'][0]['pdf']={'relativePath':value,'sha256':'a'*64,'sizeBytes':10};self.assertFalse(module.audit(source)['passed'])
    def test_matching_non_pdf_bytes_are_rejected_and_source_is_unchanged(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=pathlib.Path(tmp);content=b'plain text is not PDF';p=root/'file.pdf';p.write_bytes(content)
            source=self.fixture();source['documents'][0]['pdf']={'relativePath':'file.pdf','sha256':hashlib.sha256(content).hexdigest(),'sizeBytes':len(content)};before=repr(source)
            self.assertFalse(module.audit(source,root)['passed']);self.assertEqual(p.read_bytes(),content);self.assertEqual(before,repr(source))
    def test_pdf_metadata_size_mismatch_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=pathlib.Path(tmp);content=b'%PDF-1.7\nfixture';(root/'file.pdf').write_bytes(content)
            source=self.fixture();source['documents'][0]['pdf']={'relativePath':'file.pdf','sha256':hashlib.sha256(content).hexdigest(),'sizeBytes':len(content)+1}
            self.assertFalse(module.audit(source,root)['passed'])
    def test_pdf_links_inside_root_are_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=pathlib.Path(tmp);content=b'%PDF-1.7\nfixture';target=root/'file.pdf';target.write_bytes(content);link=root/'link.pdf'
            try:link.symlink_to(target)
            except OSError as exc:self.skipTest('Host cannot create symlink: '+type(exc).__name__)
            source=self.fixture();source['documents'][0]['pdf']={'relativePath':'link.pdf','sha256':hashlib.sha256(content).hexdigest(),'sizeBytes':len(content)}
            self.assertFalse(module.audit(source,root)['passed'])
    def test_valid_pdf_without_bytes_has_warning_and_does_not_claim_live_acceptance(self):
        source=self.fixture();source['documents'][0]['pdf']={'relativePath':'nested/file.pdf','sha256':'A'*64,'sizeBytes':10}
        report=module.audit(source);self.assertTrue(report['passed']);self.assertIn('ROW_1:PDF_BYTES_NOT_VERIFIED',report['warnings']);self.assertFalse(report['liveAcceptance'])
    def test_malformed_document_and_counter_rows_return_errors(self):
        source={'documents':[None,False,[]],'counters':[None,False,[]]};report=module.audit(source)
        self.assertFalse(report['passed']);self.assertEqual(len(report['errors']),6)
if __name__=='__main__':unittest.main()
