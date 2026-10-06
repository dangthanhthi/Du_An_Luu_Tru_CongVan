"""Read exact dependency/asset metadata for technical license review, never approval.

No downloads, purchase/account changes or secret/config ingestion. Declaration and
local text hashes establish evidence only; all permissionApproved flags stay false.
"""
import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import xml.etree.ElementTree as ET

PROJECTS = ('services/auth-service/AuthService.csproj', 'services/document-service/DocumentService.csproj',
            'services/files-service/FileService.API.csproj', 'services/notification-service/NotificationService.csproj',
            'services/partner-service/PartnerService.csproj', 'services/email-worker-service/EmailWorkerService.csproj',
            'gateway/Gateway.csproj')
ASSET_SUFFIXES = {'.svg', '.png', '.jpg', '.jpeg', '.gif', '.webp', '.ico', '.woff', '.woff2', '.ttf', '.otf', '.mp4', '.mp3'}


def linked(path):
    return path.is_symlink() or getattr(path, 'is_junction', lambda: False)()


def safe(path, root):
    if any(linked(p) for p in (path, *path.parents)) or not path.resolve().is_relative_to(root.resolve()):
        raise ValueError('Linked or escaped metadata path')


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    digest=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):digest.update(block)
    return digest.hexdigest()


def review_class(declaration):
    if not declaration:return 'MissingDeclaration'
    if not isinstance(declaration,str) or declaration.lower().startswith('see license') or declaration.lower() in ('commercial','unlicensed'):
        return 'CustomTermsReview'
    if re.search(r'\b(AND|OR|WITH)\b',declaration):return 'ExpressionAndObligationReview'
    return 'DeclarationAndNoticeReview'


def texts(folder, root, explicit=None):
    found=[]
    if explicit:
        if ':' in explicit or '\\' in explicit or any(p in ('','.','..') for p in explicit.split('/')):
            raise ValueError('Unsafe license file path')
        found.append(folder/explicit)
    found.extend(p for p in folder.iterdir() if p.is_file() and p.name.lower().startswith(('license','licence','notice','copying')))
    result=[]
    for path in sorted(set(found)):
        safe(path,root)
        if path.is_file():result.append({'path':path.relative_to(root).as_posix(),'sha256':sha(path),'bytes':path.stat().st_size})
    return result


def npm_record(tree, relative, locked):
    tree=Path(tree).absolute()
    if relative and (not relative.startswith('node_modules/') or ':' in relative or '\\' in relative or any(p in ('','.','..') for p in relative.split('/'))):
        raise ValueError('Unsafe npm package path')
    folder=tree/relative;safe(folder,tree)
    result={'ecosystem':'npm','path':relative or '.','version':locked.get('version'),
            'dev':locked.get('dev',False),'optional':locked.get('optional',False),
            'declaredLicense':locked.get('license'),'declarationSource':'package-lock.json',
            'permissionApproved':False,'licenseTexts':[]}
    metadata=folder/'package.json';safe(metadata,tree)
    if not metadata.is_file():
        result.update(name=relative.split('node_modules/')[-1],review='LocalMetadataMissing',installed=False)
        return result
    package=read(metadata)
    if package.get('version')!=locked.get('version'):raise ValueError('Installed npm version differs from lock: '+relative)
    if locked.get('license') and package.get('license') and locked['license']!=package['license']:
        raise ValueError('License declaration differs from lock: '+relative)
    result.update(name=package.get('name'),declaredLicense=package.get('license') or locked.get('license'),
                  declarationSource='matching installed package.json',installed=True,
                  packageMetadataSha256=sha(metadata),licenseTexts=texts(folder,tree))
    result['review']=review_class(result['declaredLicense'])
    return result


def nuget_record(cache, name, version):
    if not re.fullmatch(r'[A-Za-z0-9_.-]+',name) or not re.fullmatch(r'[A-Za-z0-9_.+-]+',version) or '..' in name or '..' in version:
        raise ValueError('Unsafe NuGet identity')
    cache=Path(cache).absolute();folder=cache/name.lower()/version.lower();safe(folder,cache)
    result={'ecosystem':'nuget','name':name,'version':version,'permissionApproved':False,'licenseTexts':[]}
    spec=folder/(name.lower()+'.nuspec');safe(spec,cache)
    if not spec.is_file():result.update(review='LocalMetadataMissing');return result
    metadata=ET.parse(spec).find('.//{*}metadata')
    if metadata is None:raise ValueError('Invalid nuspec')
    if metadata.findtext('{*}id','').lower()!=name.lower() or metadata.findtext('{*}version','').lower()!=version.lower():
        raise ValueError('NuGet identity differs from lock')
    license=metadata.find('{*}license')
    kind=license.get('type') if license is not None else None
    declaration=license.text if license is not None else None
    result.update(licenseType=kind,declaredLicense=declaration,licenseUrl=metadata.findtext('{*}licenseUrl'),
                  nuspecSha256=sha(spec),licenseTexts=texts(folder,cache,declaration if kind=='file' else None),
                  review='FileTextAndObligationReview' if kind=='file' else review_class(declaration))
    return result


def asset_records(root):
    root=Path(root).absolute();result=[]
    for scope in ('public','src/assets'):
        folder=root/scope
        if not folder.exists():continue
        safe(folder,root)
        for directory,dirs,files in os.walk(folder,followlinks=False):
            for name in dirs:safe(Path(directory)/name,root)
            for name in files:
                path=Path(directory)/name
                if path.suffix.lower() not in ASSET_SUFFIXES:continue
                safe(path,root)
                result.append({'path':path.relative_to(root).as_posix(),'sha256':sha(path),'bytes':path.stat().st_size,
                               'review':'OriginAndRightsUnverified','permissionApproved':False})
    return sorted(result,key=lambda r:r['path'])


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--npm-tree',required=True,type=Path)
    parser.add_argument('--nuget-cache',required=True,type=Path)
    parser.add_argument('--directory',required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parents[2]
    if not re.fullmatch(r'\.artifacts/qa/[A-Za-z0-9_-]+(?:/[A-Za-z0-9_-]+)?',args.directory):raise ValueError('Fresh QA directory required')
    output=root/args.directory;safe(output,root)
    if output.exists():raise ValueError('Refuse prior output')
    safe(args.npm_tree.absolute(),root)
    locked=read(root/'package-lock.json');installed_lock=args.npm_tree/'package-lock.json'
    if sha(root/'package-lock.json')!=sha(installed_lock):raise ValueError('Supplied npm tree uses a different lock')
    npm=[npm_record(args.npm_tree,key,value) for key,value in locked['packages'].items()]
    packages={};lock_evidence={}
    backend=root/'Intern-DocumentAdministration-BE'
    for project in PROJECTS:
        path=backend/Path(project).parent/'packages.lock.json';safe(path,backend)
        lock_evidence[path.relative_to(root).as_posix()]=sha(path)
        for framework,dependencies in read(path)['dependencies'].items():
            for name,dependency in dependencies.items():
                if dependency.get('type','').lower()=='project':continue
                key=(name,dependency['resolved'])
                packages.setdefault(key,[]).append(project+' ['+framework+']')
    nuget=[]
    for (name,version),usages in sorted(packages.items()):
        record=nuget_record(args.nuget_cache,name,version);record['usages']=usages;nuget.append(record)
    assets=asset_records(root)
    fonts=[]
    for path in (root/'src/app/layout.tsx',root/'src/app/[lang]/layout.tsx',root/'src/configs/themeConfig.ts'):
        if path.is_file():fonts.append({'path':path.relative_to(root).as_posix(),'sha256':sha(path),'review':'FontConfigurationAndGeneratedAssetsReview'})
    result={'capturedAtUtc':datetime.now(timezone.utc).isoformat(),'inventoryCreated':True,'permissionApproved':False,
            'productionReady':False,'distributionLicenseGatePassed':False,
            'scope':'Exact lock metadata; lock entries include build/dev dependencies, not a runtime SBOM. OCR/model scope deferred.',
            'npmLockSha256':sha(root/'package-lock.json'),'nugetLockSha256':lock_evidence,
            'npm':npm,'nuget':nuget,'assets':assets,'fontConfiguration':fonts,
            'counts':{'npm':len(npm),'nuget':len(nuget),'assets':len(assets),
                      'reviewClasses':dict(Counter(r['review'] for r in [*npm,*nuget,*assets]))}}
    output.mkdir(parents=True)
    (output/'inventory.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    lines=['# Technical license and provenance inventory','',
        'Permission/distribution gate remains **OPEN**. Exact declarations/text hashes are evidence, not approval.',
        f"Npm lock entries: {len(npm)}; NuGet distinct package versions: {len(nuget)}; local assets: {len(assets)}.",'',
        '## Focused reviews','',
        '| Ecosystem | Exact package/version | Declaration | Review |','| --- | --- | --- | --- |']
    for record in [*npm,*nuget]:
        if record['review']!='DeclarationAndNoticeReview' or record.get('name') in ('apexcharts','mapbox-gl','SQLite','SQLitePCLRaw.bundle_e_sqlite3'):
            declaration=json.dumps(record.get('declaredLicense'),ensure_ascii=False).replace('|','\\|')
            lines.append(f"| {record['ecosystem']} | {record.get('name')} {record.get('version')} | {declaration} | {record['review']} |")
    lines.extend(['','All packages still need obligation/notice review. All assets need origin/rights evidence; no receipt was inferred.',
                  'Vuexy Commercial requires purchaser receipt and applicable distribution scope. Custom SDK terms require review for the intended deployment.',
                  'Pinned ApexCharts metadata applies to that exact version; this report does not substitute terms of another version.',
                  'See inventory.json for installed metadata/text hashes, local missing metadata, dev/optional flags, lock usage and asset paths.'])
    (output/'REVIEW.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    print(json.dumps(result['counts']),flush=True)
    return 0


if __name__=='__main__':raise SystemExit(main())
