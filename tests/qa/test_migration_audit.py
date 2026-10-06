import importlib.util, pathlib, tempfile, unittest, uuid, hashlib, json, subprocess, sys, os
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

    def run_cli(self, source, output, pdf_root=None):
        command=[sys.executable,'-X','utf8',str(path),str(source),'--output',str(output)]
        if pdf_root is not None:command+=['--pdf-root',str(pdf_root)]
        return subprocess.run(command,capture_output=True,text=True,encoding='utf-8',timeout=20)

    def test_cli_success_hashes_exact_input_and_preserves_inputs(self):
        with tempfile.TemporaryDirectory() as tmp:
            base=pathlib.Path(tmp);source=base/'export.json';output=base/'reports'/'audit.json'
            content=b'\xef\xbb\xbf'+json.dumps(self.fixture(),ensure_ascii=False,indent=2).encode('utf-8');source.write_bytes(content)
            result=self.run_cli(source,output);self.assertEqual(result.returncode,0,result.stderr)
            report=json.loads(output.read_text(encoding='utf-8'));self.assertTrue(report['passed']);self.assertFalse(report['mutated']);self.assertFalse(report['liveAcceptance'])
            self.assertEqual(report['sourceSha256'],hashlib.sha256(content).hexdigest());self.assertEqual(source.read_bytes(),content)

    def test_cli_invalid_json_has_sanitized_failure_report_and_exact_hash(self):
        for content in (b'{"private-content":"SECRET_FIXTURE",',b'\xff',b'{"documents":[],"documents":[],"counters":[]}',b'{"documents":[],"counters":[],"invalid":NaN}'):
            with self.subTest(content=content),tempfile.TemporaryDirectory() as tmp:
                base=pathlib.Path(tmp);source=base/'export.json';output=base/'audit.json';source.write_bytes(content)
                result=self.run_cli(source,output);self.assertEqual(result.returncode,1,result.stderr)
                self.assertTrue(output.is_file(),'Malformed input must produce a structured failure report')
                report=json.loads(output.read_text(encoding='utf-8'));self.assertEqual(report['errors'],['INVALID_EXPORT_JSON']);self.assertFalse(report['passed']);self.assertFalse(report['mutated']);self.assertFalse(report['liveAcceptance'])
                self.assertEqual(report['sourceSha256'],hashlib.sha256(content).hexdigest());self.assertNotIn('SECRET_FIXTURE',result.stdout+result.stderr);self.assertNotIn('Traceback',result.stderr);self.assertEqual(source.read_bytes(),content)

    def test_cli_missing_input_returns_structured_failure_without_fabricated_hash(self):
        with tempfile.TemporaryDirectory() as tmp:
            base=pathlib.Path(tmp);result=self.run_cli(base/'missing.json',base/'audit.json')
            self.assertEqual(result.returncode,1,result.stderr);self.assertTrue((base/'audit.json').is_file(),'Read failure must produce a structured report');report=json.loads((base/'audit.json').read_text(encoding='utf-8'))
            self.assertEqual(report['errors'],['EXPORT_READ_FAILED']);self.assertIsNone(report['sourceSha256']);self.assertFalse(report['mutated']);self.assertNotIn('Traceback',result.stderr)

    def test_cli_existing_report_cannot_be_overwritten(self):
        with tempfile.TemporaryDirectory() as tmp:
            base=pathlib.Path(tmp);source=base/'export.json';source.write_text(json.dumps(self.fixture()),encoding='utf-8');output=base/'audit.json';content=b'preserve existing report';output.write_bytes(content)
            result=self.run_cli(source,output);self.assertEqual(result.returncode,2,result.stderr);self.assertEqual(output.read_bytes(),content)

    def test_cli_output_hardlink_cannot_mutate_export_source(self):
        with tempfile.TemporaryDirectory() as tmp:
            base=pathlib.Path(tmp);source=base/'export.json';content=json.dumps(self.fixture()).encode('utf-8');source.write_bytes(content);output=base/'alias.json';os.link(source,output)
            result=self.run_cli(source,output);self.assertEqual(source.read_bytes(),content);self.assertEqual(output.read_bytes(),content);self.assertEqual(result.returncode,2,result.stderr)

    def test_cli_output_hardlink_outside_pdf_tree_cannot_mutate_pdf(self):
        with tempfile.TemporaryDirectory() as tmp:
            base=pathlib.Path(tmp);pdf_root=base/'pdf';pdf_root.mkdir();pdf=pdf_root/'original.pdf';content=b'%PDF-1.7\nfixture';pdf.write_bytes(content);output=base/'alias.json';os.link(pdf,output)
            source=base/'export.json';source.write_text(json.dumps(self.fixture()),encoding='utf-8')
            result=self.run_cli(source,output,pdf_root);self.assertEqual(pdf.read_bytes(),content);self.assertEqual(output.read_bytes(),content);self.assertEqual(result.returncode,2,result.stderr)

    def test_cli_output_inside_pdf_tree_is_rejected_without_writing(self):
        with tempfile.TemporaryDirectory() as tmp:
            base=pathlib.Path(tmp);pdf_root=base/'pdf';pdf_root.mkdir();source=base/'export.json';source.write_text(json.dumps(self.fixture()),encoding='utf-8');output=pdf_root/'audit.json'
            result=self.run_cli(source,output,pdf_root);self.assertEqual(result.returncode,2,result.stderr);self.assertFalse(output.exists());self.assertEqual(list(pdf_root.iterdir()),[])

    def test_cli_output_under_symlink_parent_is_rejected_without_writing(self):
        with tempfile.TemporaryDirectory() as tmp:
            base=pathlib.Path(tmp);real=base/'real';real.mkdir();linked=base/'linked'
            try:linked.symlink_to(real,target_is_directory=True)
            except OSError as exc:self.skipTest('Host cannot create symlink: '+type(exc).__name__)
            source=base/'export.json';source.write_text(json.dumps(self.fixture()),encoding='utf-8');result=self.run_cli(source,linked/'audit.json')
            self.assertEqual(result.returncode,2,result.stderr);self.assertEqual(list(real.iterdir()),[])

    def test_cli_parent_traversal_cannot_create_directories_in_pdf_tree(self):
        with tempfile.TemporaryDirectory() as tmp:
            base=pathlib.Path(tmp);pdf_root=base/'pdf';pdf_root.mkdir();pdf=pdf_root/'original.pdf';content=b'%PDF-1.7\nfixture';pdf.write_bytes(content)
            source=base/'export.json';source.write_text(json.dumps(self.fixture()),encoding='utf-8');output=pdf_root/'newdir'/'..'/'..'/'reports'/'audit.json'
            result=self.run_cli(source,output,pdf_root)
            self.assertEqual(result.returncode,2,result.stderr);self.assertEqual(list(pdf_root.iterdir()),[pdf]);self.assertEqual(pdf.read_bytes(),content);self.assertFalse((base/'reports').exists())

    def test_cli_ads_output_cannot_add_stream_to_source(self):
        with tempfile.TemporaryDirectory() as tmp:
            base=pathlib.Path(tmp);source=base/'export.json';content=json.dumps(self.fixture()).encode('utf-8');source.write_bytes(content);output=base/'export.json:audit'
            result=self.run_cli(source,output);self.assertEqual(result.returncode,2,result.stderr);self.assertEqual(source.read_bytes(),content)
            with self.assertRaises(FileNotFoundError):output.read_bytes()

    def test_cli_device_and_aliased_output_names_do_not_claim_a_report(self):
        for name in ('NUL','CON.json','COM1.txt','LPT9.log','audit.json.','reports./audit.json','audit.json '):
            with self.subTest(name=name),tempfile.TemporaryDirectory() as tmp:
                base=pathlib.Path(tmp);source=base/'export.json';content=json.dumps(self.fixture()).encode('utf-8');source.write_bytes(content)
                result=self.run_cli(source,base/name);self.assertEqual(result.returncode,2,result.stderr);self.assertEqual(source.read_bytes(),content);self.assertEqual(list(base.iterdir()),[source])

    def test_cli_extended_windows_namespace_cannot_bypass_portable_output_policy(self):
        with tempfile.TemporaryDirectory() as tmp:
            base=pathlib.Path(tmp);source=base/'export.json';content=json.dumps(self.fixture()).encode('utf-8');source.write_bytes(content)
            output=pathlib.Path('\\\\?\\'+str(base/'audit.json')) if os.name=='nt' else base/'\\\\?\\C:\\audit.json'
            result=self.run_cli(source,output);self.assertEqual(result.returncode,2,result.stderr);self.assertEqual(source.read_bytes(),content);self.assertEqual(list(base.iterdir()),[source])
if __name__=='__main__':unittest.main()
