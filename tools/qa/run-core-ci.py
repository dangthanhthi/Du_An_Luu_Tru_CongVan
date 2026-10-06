"""Repeatable prepared DAS checks, without production credentials, workers or deployment.

Profiles are intentionally separate: functional backend/web and blocking dependency audit.
SQL/Docker/live gateway/EAP/OCR/fax/UAT/license gates are not silently included.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import time
import xml.etree.ElementTree as ET

BACKEND = Path('Intern-DocumentAdministration-BE')
PROJECTS = [(name, BACKEND / 'tests' / (name + '.Tests') / (name + '.Tests.csproj')) for name in (
    'DocumentService', 'FileService', 'PartnerService', 'AuthService', 'NotificationService', 'EmailWorkerService', 'PdfIntegration', 'ReminderIntegration')]
SERVICE_PROJECTS = [(name, BACKEND / 'services' / folder / project) for name, folder, project in (
    ('document', 'document-service', 'DocumentService.csproj'), ('files', 'files-service', 'FileService.API.csproj'),
    ('partner', 'partner-service', 'PartnerService.csproj'), ('auth', 'auth-service', 'AuthService.csproj'),
    ('notification', 'notification-service', 'NotificationService.csproj'), ('email', 'email-worker-service', 'EmailWorkerService.csproj'))]


def linked(path):
    return path.is_symlink() or getattr(path, 'is_junction', lambda: False)()


def fresh_output(root, relative):
    root = Path(root).resolve()
    if not isinstance(relative, str) or '\\' in relative or ':' in relative or relative.startswith('/') or any(p in ('', '.', '..') for p in relative.split('/')):
        raise ValueError('Invalid output path')
    target = root / relative
    boundary = root / '.artifacts/qa'
    if target == boundary or not target.is_relative_to(boundary) or target.exists() or any(linked(p) for p in (target, *target.parents)):
        raise ValueError('Use a fresh non-link directory inside .artifacts/qa')
    return target


def trx_counts(path):
    try:
        counters = ET.parse(path).find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters')
        if counters is None: raise ValueError('Missing TRX counters')
        result = {key: int(counters.attrib[key]) for key in ('total', 'executed', 'passed', 'failed', 'notExecuted')}
    except (ET.ParseError, KeyError, TypeError) as error:
        raise ValueError('Invalid TRX') from error
    if result['passed'] <= 0 or result['failed'] != 0 or result['notExecuted'] != 0 or result['total'] != result['passed'] or result['executed'] != result['passed']:
        raise ValueError('TRX must contain executed passing tests and no failures/skips')
    return result


def tap_counts(text):
    def counter(name, default=None):
        matches = re.findall(r'^# ' + name + r'\s+(\d+)\s*$', text, flags=re.MULTILINE)
        if len(matches) != 1:
            if default is not None and len(matches) == 0: return default
            raise ValueError('Missing or duplicate TAP counters')
        return int(matches[0])
    result = {'total':counter('tests'), 'passed':counter('pass'), 'failed':counter('fail'), 'skipped':counter('skipped'), 'cancelled':counter('cancelled',0)}
    if result['total'] <= 0 or result['passed'] != result['total'] or any(result[key] != 0 for key in ('failed','skipped','cancelled')):
        raise ValueError('TAP must contain executed passing tests and no failures/skips')
    return result


def nuget_counts(data):
    try:
        if data.get('version') != 1 or data.get('errors') or not all(flag in data.get('parameters','') for flag in ('--vulnerable','--include-transitive')):
            raise ValueError('Missing or failed NuGet audit report')
        projects = data['projects']
        if not isinstance(projects,list) or len(projects) != 1 or not projects[0].get('path'):
            raise ValueError('Missing NuGet project evidence')
        # --vulnerable legitimately omits frameworks when the project has no findings.
        frameworks = projects[0].get('frameworks',[])
        if not isinstance(frameworks,list): raise ValueError('Invalid NuGet frameworks')
        count = 0
        for frame in frameworks:
            for kind in ('topLevelPackages','transitivePackages'):
                packages = frame.get(kind,[])
                if not isinstance(packages,list): raise ValueError('Invalid NuGet packages')
                count += sum(bool(package.get('vulnerabilities')) for package in packages)
        return count
    except (AttributeError,KeyError,TypeError) as error:
        raise ValueError('Invalid NuGet audit report') from error


def include_source(name):
    path = PurePosixPath(name)
    if path.is_absolute() or any(p in ('', '.', '..', 'node_modules', '.next', '.git', '.artifacts', 'bin', 'obj', '__pycache__') for p in path.parts): return False
    if path.name.startswith('.env') and not path.name.endswith('.example') or path.suffix.lower() in ('.pem','.pfx','.key','.db','.bak','.log','.trx','.pyc','.tsbuildinfo'): return False
    if name.startswith('Intern-DocumentAdministration-BE/services/ai-ocr-service/') or name.startswith('Intern-DocumentAdministration-BE/tests/AiOcrService.'):
        return False
    roots = ('src/','public/','tests/frontend/','tests/qa/','scripts/qa/','Intern-DocumentAdministration-BE/','.github/workflows/')
    return name.startswith(roots) or name in ('package.json','package-lock.json','.npmrc','.eslintrc.cjs','tsconfig.json','next.config.ts','next.config.mjs','declarations.d.ts','postcss.config.mjs','.gitignore','.gitattributes')


def export_source(root, relative):
    target = fresh_output(root, relative)
    manifest_path = target.parent / (target.name + '-manifest.json')
    if manifest_path.exists(): raise ValueError('Source export manifest already exists')
    names = subprocess.check_output(['git','ls-files','--cached','--others','--exclude-standard','-z'], cwd=root).decode('utf-8').split('\0')
    files = []
    for name in sorted(set(filter(None, names))):
        if not include_source(name): continue
        path = root / name
        if not path.exists(): continue  # Tracked WIP deletion stays deleted.
        if not path.is_file() or not path.resolve().is_relative_to(root) or any(linked(p) for p in (path,*path.parents)):
            raise ValueError('Unsafe source export path')
        files.append((name,path))
    target.mkdir(parents=True)
    manifest = {'kind':'clean-source-WIP-rehearsal','remoteClone':False,'root':str(root),'export':str(target),'files':{}}
    for name,path in files:
        data = path.read_bytes(); output = target / name; output.parent.mkdir(parents=True,exist_ok=True); output.write_bytes(data)
        manifest['files'][name] = {'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)}
    # Keep export inventory outside the build tree to avoid TypeScript/build input contamination.
    with manifest_path.open('x',encoding='utf-8') as stream:
        stream.write(json.dumps(manifest,indent=2)+'\n')
    print(f'Exported {len(files)} current source files; no node_modules/bin/obj/env credentials or deferred OCR backend',flush=True)
    return target


def run_step(name, command, root, output, environment, timeout=1800):
    if re.fullmatch('[A-Za-z0-9_-]+', name) is None: raise ValueError('Invalid step name')
    started = time.monotonic(); code = None; error = None
    with (output / (name + '.log')).open('x',encoding='utf-8') as log:
        try:
            process = subprocess.run(command,cwd=root,env=environment,stdout=log,stderr=subprocess.STDOUT,timeout=timeout,check=False)
            code = process.returncode
        except (OSError,subprocess.TimeoutExpired) as failure:
            error = type(failure).__name__; log.write('\nCI process unavailable or timed out: ' + error + '\n')
    result = {'name':name,'exitCode':code,'passed':code==0 and error is None,'error':error,'seconds':round(time.monotonic()-started,3),'log':name+'.log'}
    print(f"{name}: {'passed' if result['passed'] else 'FAILED'}",flush=True)
    return result


def npm_command(node, cli):
    if cli: return [node,str(Path(cli).resolve())]
    executable = shutil.which('npm')
    if not executable: raise ValueError('npm is unavailable; supply --npm-cli')
    path = Path(executable)
    if path.suffix.lower() in ('.cmd','.ps1','.bat'): path = path.parent / 'node_modules/npm/bin/npm-cli.js'
    else: path = path.resolve()
    if not path.is_file(): raise ValueError('Cannot resolve npm JS CLI')
    return [node,str(path)]


def ci_environment(base, node):
    values = {'Reminders__Enabled':'false','Reminders__TransportEnabled':'false','Notifications__WorkerEnabled':'false',
        'Notifications__TransportEnabled':'false','Delivery__WorkerEnabled':'false','Smtp__DeliveryEnabled':'false',
        'PdfProtocol__MaintenanceEnabled':'false','NEXT_TELEMETRY_DISABLED':'1','CI':'true',
        'DOTNET_NOLOGO':'true','DOTNET_CLI_TELEMETRY_OPTOUT':'1','NODE_OPTIONS':'--max-old-space-size=2304'}
    removed = {key.upper() for key in values} | {'DAS_TEST_SQL_CONNECTION','DAS_RESTORE_DRILL','DAS_RESTORE_OUTPUT','PATH'}
    env = {key:value for key,value in base.items() if key.upper() not in removed}
    env.update(values)
    previous_path = next((value for key,value in base.items() if key.upper()=='PATH'),'')
    env['PATH'] = str(Path(node).resolve().parent) + os.pathsep + previous_path
    return env


def locked_install_args(output):
    # Keep debug logs with the owned reports so cleanup cannot erase network evidence.
    # Fewer sockets reduce pressure on the local Docker/registry connection, not lock integrity.
    return ['ci', '--ignore-scripts', '--no-audit', '--no-fund', '--maxsockets=4',
            '--loglevel=http', '--logs-dir=' + str(output / 'npm-debug')]


def run_profile(profile, root, output, node, npm_cli, dotnet):
    env = ci_environment(os.environ.copy(), node)
    steps = []; evidence = {}; npm = npm_command(node,npm_cli) if profile in ('web','audit') else None

    def execute(name,args):
        result = run_step(name,args,root,output,env)
        steps.append(result)
        return result['passed']

    def failure(name,error):
        steps.append({'name':name,'passed':False,'error':str(error)})

    if profile == 'backend':
        for name,project in PROJECTS:
            if not execute('restore-'+name,[dotnet,'restore',str(project),'--locked-mode','--verbosity','minimal']): continue
            if execute('test-'+name,[dotnet,'test',str(project),'--no-restore','--configuration','Release','--filter','FullyQualifiedName!~SqlTests','--logger','trx;LogFileName='+name+'.trx','--results-directory',str(output),'--verbosity','minimal']):
                try: evidence[name] = trx_counts(output/(name+'.trx'))
                except (ValueError,OSError) as error: failure('evidence-'+name,error)
        gateway = str(BACKEND/'gateway/Gateway.csproj')
        if execute('restore-gateway',[dotnet,'restore',gateway,'--locked-mode','--verbosity','minimal']):
            execute('build-gateway',[dotnet,'build',gateway,'--no-restore','--configuration','Release','--verbosity','minimal'])
    elif profile == 'web':
        if execute('npm-ci',npm+locked_install_args(output)):
            execute('prisma-generate',[node,str(root/'node_modules/prisma/build/index.js'),'generate'])
            execute('next-typegen',[node,str(root/'node_modules/next/dist/bin/next'),'typegen'])
            execute('typecheck',npm+['run','typecheck'])
            execute('lint',npm+['run','lint'])
            test_files = sorted(str(p.relative_to(root)) for p in (root/'tests/frontend').glob('*.test.ts'))
            if not test_files: failure('frontend-tests','No frontend test files')
            elif execute('frontend-tests',[node,str(root/'node_modules/tsx/dist/cli.mjs'),'--test','--test-reporter=tap',*test_files]):
                try: evidence['frontend'] = tap_counts((output/'frontend-tests.log').read_text(encoding='utf-8'))
                except (ValueError,OSError) as error: failure('frontend-evidence',error)
            execute('next-build',npm+['run','build'])
    elif profile == 'audit':
        if execute('audit-install',npm+locked_install_args(output)):
            # npm exits nonzero for findings at/above high. Always retain the public advisory JSON.
            execute('npm-audit',npm+['audit','--audit-level=high','--json'])
        for name,project in SERVICE_PROJECTS:
            if not execute('audit-restore-'+name,[dotnet,'restore',str(project),'--locked-mode','--verbosity','minimal']): continue
            label='nuget-audit-'+name
            if execute(label,[dotnet,'list',str(project),'package','--include-transitive','--vulnerable','--format','json']):
                try:
                    data=json.loads((output/(label+'.log')).read_text(encoding='utf-8'))
                    count=nuget_counts(data)
                    evidence[name]={'vulnerablePackageCount':count}
                    if count: failure('nuget-findings-'+name,'Vulnerable package findings; see audit log')
                except (KeyError,ValueError,OSError) as error: failure('nuget-evidence-'+name,error)
    else: raise ValueError('Unknown profile')
    result={'profile':profile,'passed':bool(steps) and all(x['passed'] for x in steps),'productionReady':False,
        'capturedAtUtc':datetime.now(timezone.utc).isoformat(),'steps':steps,'testEvidence':evidence,
        'scope':'prepared core checks; SQL/live gateway/EAP/OCR/fax/license/UAT excluded','workersEnabled':False}
    (output/'summary.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    return result


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--profile',choices=('backend','web','audit'))
    parser.add_argument('--output',default='.artifacts/qa/core-ci')
    parser.add_argument('--export',help='Create a fresh clean-source WIP export under .artifacts/qa; does not execute checks')
    parser.add_argument('--node',default=shutil.which('node') or 'node')
    parser.add_argument('--npm-cli')
    parser.add_argument('--dotnet',default=shutil.which('dotnet') or 'dotnet')
    args=parser.parse_args(); root=Path(__file__).resolve().parents[2]
    if args.export:
        if args.profile: parser.error('Export and profile are separate steps')
        export_source(root,args.export); return 0
    if not args.profile: parser.error('Choose --profile or --export')
    output=fresh_output(root,args.output); output.mkdir(parents=True)
    result=run_profile(args.profile,root,output,args.node,args.npm_cli,args.dotnet)
    print(json.dumps({'profile':args.profile,'passed':result['passed'],'productionReady':False}),flush=True)
    return 0 if result['passed'] else 1


if __name__=='__main__': raise SystemExit(main())
