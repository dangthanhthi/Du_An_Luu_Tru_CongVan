"""Create an ignored, source-only compatibility view for legacy QA, with byte provenance.
Canonical application code stays in frontend/backend/database/workflows.
"""
from pathlib import Path
import argparse,hashlib,json,shutil,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[1]
EXCLUDED={'node_modules','.next','.artifacts','bin','obj','__pycache__','uploads'}
def linked(p):return p.is_symlink() or getattr(p,'is_junction',lambda:False)()
def sha(data):return hashlib.sha256(data).hexdigest()
def create_view(relative):
    if not isinstance(relative,str) or '\\' in relative or ':' in relative or any(p in ('','.','..') for p in relative.split('/')):raise ValueError('Invalid view path')
    target=ROOT/relative
    if target==ROOT/'.artifacts/qa' or not target.is_relative_to(ROOT/'.artifacts/qa') or target.exists() or any(linked(p) for p in (target,*target.parents)):raise ValueError('Fresh owned unlinked QA directory required')
    baseline=json.loads((ROOT/'docs/source-layout-manifest.json').read_text())
    for row in baseline['files']:
        source=row['source']
        if not isinstance(source,str) or not source or '\\' in source or ':' in source or Path(source).is_absolute() or '..' in Path(source).parts:
            raise ValueError('Unsafe manifest source path')
    # Locally added files can have a provenance note rather than an old path.
    # Such notes must use the canonical-scope fallback, never a view filename.
    inverse={r['destination']:r['source'] for r in baseline['files'] if '/' in r['source']}
    inventory=[];destinations=set()
    for scope in ('frontend','backend','database','workflows/business','tools/qa','tests/qa'):
        for p in sorted((ROOT/scope).rglob('*')):
            if not p.is_file() or any(s in EXCLUDED for s in p.relative_to(ROOT).parts):continue
            if any(linked(s) for s in (p,*p.parents)):raise ValueError('Linked canonical source rejected')
            if p.name.startswith('.env') and not p.name.endswith('.example') or p.suffix.lower() in ('.db','.sqlite','.bak','.log','.pyc','.pem','.pfx','.key','.pdf'):continue
            name=p.relative_to(ROOT).as_posix();destination=inverse.get(name)
            if not destination:
                if name.startswith('frontend/'):destination=name.removeprefix('frontend/')
                elif name.startswith('backend/'):destination='Intern-DocumentAdministration-BE/'+name.removeprefix('backend/')
                elif name.startswith('tools/qa/'):destination='scripts/qa/'+name.removeprefix('tools/qa/')
                elif name.startswith('tests/qa/'):destination=name
                elif name.startswith('workflows/business/'):
                    parts=Path(name).parts;destination='Intern-DocumentAdministration-BE/services/'+parts[2]+'/Services/Workflows/'+Path(*parts[3:]).as_posix()
                elif name.startswith('database/migrations/'):
                    parts=Path(name).parts;destination='Intern-DocumentAdministration-BE/services/'+parts[2]+'/Migrations/'+Path(*parts[3:]).as_posix()
                else:continue
            if destination in destinations or Path(destination).is_absolute() or '..' in Path(destination).parts:raise ValueError('Duplicate/unsafe view destination')
            destinations.add(destination);data=p.read_bytes();original=sha(data);adaptation=None
            if name=='frontend/package.json':
                value=json.loads(data);value['prisma']['schema']='./src/prisma/schema.prisma'
                if value.get('scripts',{}).get('build')=='node scripts/generate-prisma.cjs && next build --webpack':
                    value['scripts']['build']='prisma generate && next build --webpack';value['scripts'].pop('generate:prisma',None)
                data=(json.dumps(value,indent=2)+'\n').encode();adaptation='Prisma schema/generation paths in compatibility view'
            elif name=='database/prisma/schema.prisma':
                text=data.decode();text=text.replace('  output          = "../../frontend/node_modules/.prisma/client"\n','');data=text.encode();adaptation='Prisma default client output for compatibility root'
            elif name=='backend/Directory.Build.props':
                tree=ET.fromstring(data)
                for group in list(tree):
                    if any(c.tag=='DasSourceOwner' or c.tag=='Compile' and '$(MSBuildThisFileDirectory)../' in c.get('Include','') for c in group):tree.remove(group)
                ET.indent(tree,space='  ');data=ET.tostring(tree,encoding='utf-8');adaptation='Workflow/migration source restored to owning service folders in view'
            inventory.append((name,destination,original,data,adaptation))
    target.mkdir(parents=True)
    records=[]
    for source,destination,original,data,adaptation in inventory:
        p=target/destination;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(data)
        if sha((ROOT/source).read_bytes())!=original or sha(p.read_bytes())!=sha(data):raise ValueError('Source changed while creating view')
        records.append({'source':source,'destination':destination,'sourceSha256':original,'viewSha256':sha(data),'adaptation':adaptation})
    report=target.parent/(target.name+'-manifest.json')
    report.write_text(json.dumps({'kind':'source-only-legacy-qa-view','canonicalSourceModified':False,'files':records},indent=2)+'\n')
    return target
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--output',required=True);a=p.parse_args();print(create_view(a.output))
