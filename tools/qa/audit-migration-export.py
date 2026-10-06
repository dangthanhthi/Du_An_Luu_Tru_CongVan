"""Read-only preflight for a normalized DAS export; never connects to/mutates a DB.

Input JSON: documents[] and counters[]. Each document includes id, kind,
registrationDate, sequenceNumber, registrationNumber, companyCode,
departmentCode, ownerDepartmentId, inputterUserId, originatorUserId, sensitivity,
status, issuedDate, and optional pdf {relativePath, sha256, sizeBytes}.
Schema is a staging format; legacy extraction/mapping awaits actual source data.
"""
from pathlib import Path
from datetime import date
import argparse, hashlib, json, re, uuid

MAX_PDF_BYTES = 25 * 1024 * 1024

def linked(path):
    return path.is_symlink() or getattr(path, 'is_junction', lambda: False)()

def validate_pdf(pdf, pdf_root):
    if not isinstance(pdf, dict): raise ValueError('INVALID_PDF_METADATA')
    relative, expected_hash, expected_size = pdf.get('relativePath'), pdf.get('sha256'), pdf.get('sizeBytes')
    if not isinstance(relative, str) or not relative or any(c in relative for c in '\\:*?"<>|\x00') or any(ord(c) < 32 for c in relative) or any(part in ('', '.', '..') for part in relative.split('/')):
        raise ValueError('INVALID_PDF_RELATIVE_PATH')
    if not isinstance(expected_hash, str) or not re.fullmatch(r'[a-fA-F0-9]{64}', expected_hash) or type(expected_size) is not int or not 5 <= expected_size <= MAX_PDF_BYTES:
        raise ValueError('INVALID_PDF_METADATA')
    if pdf_root is None: return 'PDF_BYTES_NOT_VERIFIED'
    root = Path(pdf_root).absolute()
    if any(linked(p) for p in (root, *root.parents)): raise ValueError('PDF_LINK_NOT_ALLOWED')
    root = root.resolve()
    path = root / relative
    if any(linked(p) for p in (path, *path.parents)): raise ValueError('PDF_LINK_NOT_ALLOWED')
    if not path.resolve().is_relative_to(root) or not path.is_file(): raise ValueError('PDF_PATH_MISSING_OR_OUTSIDE_ROOT')
    if path.stat().st_size != expected_size: raise ValueError('PDF_HASH_OR_SIZE_MISMATCH')
    digest, size = hashlib.sha256(), 0
    with path.open('rb') as stream:
        signature = stream.read(5)
        if signature != b'%PDF-': raise ValueError('PDF_SIGNATURE_INVALID')
        digest.update(signature); size = len(signature)
        while block := stream.read(min(1024 * 1024, expected_size - size + 1)):
            size += len(block)
            if size > expected_size: raise ValueError('PDF_HASH_OR_SIZE_MISMATCH')
            digest.update(block)
    if size != expected_size or digest.hexdigest() != expected_hash.lower(): raise ValueError('PDF_HASH_OR_SIZE_MISMATCH')
    return 'SCANNER_AND_CURRENT_CLAIM_REQUIRE_LIVE_CHECK'

def audit(source, pdf_root=None):
    errors, warnings, ids, sequences, numbers, highwater = [], [], set(), set(), set(), {}
    invalid = {'passed':False,'errors':['INVALID_EXPORT_SCHEMA'],'warnings':[],'documents':0,'highwater':[],'mutated':False,'liveAcceptance':False}
    if not isinstance(source, dict): return invalid
    records=source.get('documents')
    counters=source.get('counters')
    if not isinstance(records,list) or not isinstance(counters,list):
        return invalid
    for index,row in enumerate(records):
        prefix=f'ROW_{index+1}:'
        try:
            identity=uuid.UUID(row['id'])
            if not identity.int or identity in ids: raise ValueError('DUPLICATE_OR_EMPTY_ID')
            ids.add(identity)
            kind=row['kind']
            if kind not in ('INCOMING','OUTGOING','INTERNAL'): raise ValueError('INVALID_KIND')
            registered=date.fromisoformat(row['registrationDate'])
            sequence=row['sequenceNumber']
            if isinstance(sequence,bool) or not isinstance(sequence,int) or not 1<=sequence<=99999: raise ValueError('INVALID_SEQUENCE')
            key=(kind,registered.year,sequence)
            if key in sequences: raise ValueError('DUPLICATE_SEQUENCE')
            sequences.add(key)
            number=row['registrationNumber']
            if not isinstance(number,str) or not number.strip() or number in numbers: raise ValueError('DUPLICATE_OR_EMPTY_NUMBER')
            numbers.add(number)
            highwater[(kind,registered.year)]=max(sequence,highwater.get((kind,registered.year),0))
            if row['companyCode'] not in ('HL','HV','HLHV'): raise ValueError('INVALID_COMPANY')
            if kind!='INCOMING' and not re.fullmatch(r'[A-Z][A-Z0-9&-]{0,31}',row.get('departmentCode','')): raise ValueError('MISSING_DEPARTMENT_MAPPING')
            for field in ('ownerDepartmentId','inputterUserId','originatorUserId'):
                if not uuid.UUID(row[field]).int: raise ValueError('EMPTY_'+field.upper())
            if row['sensitivity'] not in ('Normal','Confidential') or row['status'] not in ('InProgress','Distributed','Cancelled'): raise ValueError('INVALID_STATE')
            if row.get('issuedDate'): date.fromisoformat(row['issuedDate'])
            if row['status']=='Cancelled': warnings.append(prefix+'RESTORE_HISTORY_REQUIRES_RECONCILIATION')
            pdf=row.get('pdf')
            if pdf is not None:
                warnings.append(prefix+validate_pdf(pdf,pdf_root))
            elif row['status']=='Distributed': warnings.append(prefix+'DISTRIBUTED_WITHOUT_PDF')
        except (KeyError,TypeError,ValueError,AttributeError,OSError) as exc:
            code=str(exc) if isinstance(exc,ValueError) and str(exc).isupper() else 'INVALID_OR_MISSING_FIELD'
            errors.append(prefix+code)
    seen=set()
    for row in counters:
        try:
            key=(row['kind'],row['year']);current=row['currentValue']
            if key in seen or key[0] not in ('INCOMING','OUTGOING','INTERNAL') or type(key[1]) is not int or not 1<=key[1]<=9999 or isinstance(current,bool) or not isinstance(current,int) or current<highwater.get(key,0) or not 0<=current<=99999: raise ValueError()
            seen.add(key)
        except (KeyError,TypeError,ValueError):errors.append('INVALID_OR_UNDERSIZED_COUNTER')
    if set(highwater)-seen:errors.append('MISSING_COUNTERS')
    return {'passed':not errors,'documents':len(records),'errors':errors,'warnings':warnings,'highwater':[{'kind':k[0],'year':k[1],'sequence':v} for k,v in sorted(highwater.items())], 'mutated':False, 'liveAcceptance':False}

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('source',type=Path);parser.add_argument('--pdf-root',type=Path);parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
    if args.output.resolve()==args.source.resolve():parser.error('Output must not overwrite source.')
    if args.pdf_root and args.output.resolve().is_relative_to(args.pdf_root.resolve()):parser.error('Output must not be written inside the source PDF tree.')
    report=audit(json.loads(args.source.read_text(encoding='utf-8-sig')),args.pdf_root)
    report['sourceSha256']=hashlib.sha256(args.source.read_bytes()).hexdigest()
    args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({'passed':report['passed'],'documents':report['documents'],'errors':len(report['errors']),'warnings':len(report['warnings'])}));raise SystemExit(0 if report['passed'] else 1)
