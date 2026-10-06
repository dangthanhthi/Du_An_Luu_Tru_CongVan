"""Bounded local core image probes, no external network, DB or production configuration.

Executes a portable native SQLite in-memory probe, then expects unconfigured
application entrypoints to fail before listening. No successful app hosts are allowed.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import uuid

ASSEMBLIES={'auth':'AuthService.dll','document':'DocumentService.dll','files':'FileService.API.dll',
            'notification':'NotificationService.dll','partner':'PartnerService.dll','gateway':'Gateway.dll'}
EXPECTED={'auth':'Database:Provider','document':'Database:Provider','files':'Database:Provider',
          'notification':'JWT signing key','partner':'Jwt:Secret','gateway':'Jwt:Secret'}


def validate_summary(summary):
    if summary.get('passed') is not True or summary.get('productionReady') is not False or set(summary.get('images',{}))!=set(ASSEMBLIES):
        raise ValueError('Only the complete prepared core candidate is supported')
    for service,record in summary['images'].items():
        if not re.fullmatch('sha256:[0-9a-f]{64}',record.get('imageId','')) or not re.fullmatch('[0-9a-f]{64}',record.get('sourceSha256','')):
            raise ValueError('Immutable image/source IDs required')
        if record.get('uid')!='1654' or record.get('entrypoint')!=['dotnet',ASSEMBLIES[service]]:
            raise ValueError('Expected nonroot service entrypoint')


def config_blocked(service,code,log):
    return code!=0 and EXPECTED[service] in log and 'InvalidOperationException' in log and 'Now listening on' not in log


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def local_tool():
    path=Path(__file__).with_name('build-core-images.py')
    spec=importlib.util.spec_from_file_location('core_image_builder',path)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module


def owned_run(image,args,output,name,*,probe=None,entrypoint=None,env=None):
    command=['docker','run','--rm','--name',name,'--network','none','--read-only','--memory','512m',
             '--cpus','1','--cap-drop','ALL','--security-opt','no-new-privileges']
    if probe:command+=['--volume',str(probe.absolute())+':/probe:ro']
    if entrypoint:command+=['--entrypoint',entrypoint]
    if env:command+=['--env',env]
    command+=[image,*args]
    try:
        with output.open('x',encoding='utf-8') as stream:
            result=subprocess.run(command,stdout=stream,stderr=subprocess.STDOUT,timeout=45)
        return result.returncode,output.read_text(encoding='utf-8')
    finally:
        current=subprocess.run(['docker','ps','-a','--filter','name=^/'+name+'$','--format','{{.Names}}'],capture_output=True,text=True,timeout=30,check=True)
        if current.stdout.strip():subprocess.run(['docker','rm','-f',name],capture_output=True,timeout=30,check=True)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--images',required=True,type=Path)
    parser.add_argument('--probe',required=True,type=Path)
    parser.add_argument('--directory',required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parents[2];builder=local_tool()
    for path in (args.images,args.probe):
        absolute=path.absolute()
        if any(builder.linked(p) for p in (absolute,*absolute.parents)) or not absolute.resolve().is_relative_to((root/'.artifacts/qa').resolve()):
            raise ValueError('Inputs must be unlinked QA artifacts in this worktree')
    summary=json.loads(args.images.read_text(encoding='utf-8-sig'));validate_summary(summary)
    required=['NativeProbe.dll','NativeProbe.deps.json','NativeProbe.runtimeconfig.json']
    if any(not (args.probe/n).is_file() or builder.linked(args.probe/n) for n in required):raise ValueError('Portable probe output is missing')
    output=builder.fresh_output(root,args.directory)
    result={'passed':False,'productionReady':False,'capturedAtUtc':datetime.now(timezone.utc).isoformat(),
            'imageSummarySha256':sha(args.images),'probeArtifactSha256':{n:sha(args.probe/n) for n in required},
            'probeSourceSha256':{n:sha(root/'tests/qa/native-probe'/n) for n in ('Program.cs','NativeProbe.csproj','packages.lock.json')},
            'services':{},'successfulApplicationHostsStarted':False,'externalNetworkEnabled':False,'smtpCalls':0}
    run_id=uuid.uuid4().hex[:12]
    try:
        for service,record in summary['images'].items():
            info=builder.inspect(record['imageId']);config=info['Config']
            if config['User']!='1654' or config['Entrypoint']!=record['entrypoint'] or config['Labels'].get('das.source-sha256')!=record['sourceSha256']:
                raise ValueError('Candidate inspection differs: '+service)
            entry={'imageId':record['imageId'],'nativeProbe':None}
            if service!='gateway':
                code,log=owned_run(record['imageId'],['/probe/NativeProbe.dll','/app/runtimes/linux-x64/native/libe_sqlite3.so','3.53.4'],output/(service+'-native.log'),
                                   'das-runtime-qa-'+run_id+'-'+service,probe=args.probe,entrypoint='dotnet')
                if code:raise RuntimeError('Native probe failed: '+service)
                parsed=json.loads(log.strip())
                if parsed.get('passed') is not True or parsed.get('sqliteVersion')!='3.53.4' or parsed.get('queryResult')!='10' or parsed.get('inMemoryOnly') is not True:
                    raise ValueError('Invalid native result: '+service)
                entry['nativeProbe']=parsed
            code,log=owned_run(record['imageId'],[],output/(service+'-missing-config.log'),'das-runtime-qa-'+run_id+'-'+service+'-config')
            if not config_blocked(service,code,log):raise RuntimeError('Missing config was not the expected startup blocker: '+service)
            entry['missingConfigBlockedBeforeListening']=True;entry['startupExitCode']=code
            if service=='partner':
                code,log=owned_run(record['imageId'],[],output/'partner-short-key.log','das-runtime-qa-'+run_id+'-partner-short',env='Jwt__Secret=short')
                if not config_blocked(service,code,log) or '32 UTF8 bytes' not in log:raise RuntimeError('Weak key did not block Partner startup')
                entry['shortKeyBlockedBeforeListening']=True
            result['services'][service]=entry;print(service+': native/config checks passed',flush=True)
        result['passed']=len(result['services'])==6
    except Exception as exc:result['error']=type(exc).__name__+': '+str(exc)
    finally:(output/'summary.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'passed':result['passed'],'services':len(result['services']),'productionReady':False}),flush=True)
    return 0 if result['passed'] else 1


if __name__=='__main__':raise SystemExit(main())
