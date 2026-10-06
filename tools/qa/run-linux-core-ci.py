"""Linux core source rehearsal using an existing prepared immutable QA tooling image.
No source push, server/deploy, Docker socket, host secrets/caches or customer connections.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import secrets
import subprocess
from datetime import datetime,timezone

ROOT=Path(__file__).resolve().parents[2];LABEL='das.qa.linux-ci'
def cli(args,**kwargs):return subprocess.run(args,cwd=ROOT,capture_output=True,text=True,timeout=120,**kwargs)
def inspect(name):
    result=cli(['docker','container','inspect',name])
    if not result.returncode:return json.loads(result.stdout)[0]
    # A daemon/permission failure is not evidence that an owned resource is gone.
    inventory=cli(['docker','container','ls','--all','--format','{{.Names}}'])
    if inventory.returncode or name in inventory.stdout.splitlines():raise RuntimeError('Container state could not be verified')
    return None
def volume_exists(name):
    inventory=cli(['docker','volume','ls','--format','{{.Name}}'])
    if inventory.returncode:raise RuntimeError('Volume state could not be verified')
    return name in inventory.stdout.splitlines()
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--source',required=True,type=Path);parser.add_argument('--manifest',required=True,type=Path)
    parser.add_argument('--image',required=True);parser.add_argument('--profile',required=True,choices=('backend','web'));parser.add_argument('--directory',required=True)
    args=parser.parse_args()
    spec=importlib.util.spec_from_file_location('builder',ROOT/'scripts/qa/build-core-images.py');builder=importlib.util.module_from_spec(spec);spec.loader.exec_module(builder)
    for path in (args.source,args.manifest):
        absolute=path.absolute()
        if not absolute.exists() or not absolute.resolve().is_relative_to(ROOT/'.artifacts/qa') or any(builder.linked(p) for p in (absolute,*absolute.parents)):raise ValueError('Owned unlinked QA source/manifest required')
    if not args.source.is_dir() or not args.manifest.is_file() or not re.fullmatch(r'sha256:[0-9a-f]{64}',args.image):raise ValueError('Immutable image/source/manifest required')
    image=json.loads(cli(['docker','image','inspect',args.image],check=True).stdout)[0]
    if image['Id']!=args.image or image['Config']['User']!='1654:1654' or image['Config'].get('Labels',{}).get('das.qa.tooling')!='linux-core-ci':raise ValueError('Prepared nonroot QA tooling image required')
    output=builder.fresh_output(ROOT,args.directory);run_id=secrets.token_hex(6);name='das-linux-ci-'+run_id
    memory=3221225472 if args.profile=='web' else 4294967296
    cpus=2
    report={'passed':False,'productionReady':False,'capturedAtUtc':datetime.now(timezone.utc).isoformat(),'profile':args.profile,'runId':run_id,
            'imageId':args.image,'sourceManifestSha256':sha(args.manifest),'remoteClone':False,'hostedCI':False,'deployed':False,'workersEnabled':False,
            'memoryBytes':memory,'cpus':cpus,'socketMounted':False,'hostCredentialsMounted':False,'registryNetworkEnabled':True,'cleanupPassed':False}
    attempted=False
    try:
        if inspect(name):raise ValueError('Owned resource name occupied')
        attempted=True
        result=cli(['docker','create','--name',name,'--label',LABEL+'='+run_id,'--read-only','--tmpfs','/tmp:rw,noexec,nosuid,size=268435456,uid=1654,gid=1654',
                    '--volume','/workspace','--memory',str(memory),'--memory-swap',str(memory+1073741824),'--cpus',str(cpus),'--cap-drop','ALL','--security-opt','no-new-privileges',
                    '--mount','type=bind,source='+str(args.source.absolute())+',target=/source,readonly',
                    '--mount','type=bind,source='+str(args.manifest.absolute())+',target=/qa/source-manifest.json,readonly',args.image,args.profile])
        if result.returncode:raise RuntimeError('QA container creation failed')
        state=inspect(name)
        if not state['HostConfig']['ReadonlyRootfs'] or state['Config']['User']!='1654:1654' or state['HostConfig']['Memory']!=memory or state['HostConfig'].get('PortBindings'):raise ValueError('QA execution boundary mismatch')
        volume=[m for m in state['Mounts'] if m['Destination']=='/workspace']
        if len(volume)!=1 or volume[0]['Type']!='volume':raise ValueError('Fresh owned workspace volume required')
        report['workspaceVolume']=volume[0]['Name']
        with (output/'container.log').open('x',encoding='utf-8') as log:
            result=subprocess.run(['docker','start','-a',name],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,timeout=3600)
        report['attachExitCode']=result.returncode
        report['containerExitCode']=inspect(name)['State']['ExitCode']
        # Copy reports only, never source/dependencies/caches from the anonymous volume.
        for path,label in (('/workspace/.artifacts/qa/linux-run','linux-run'),('/workspace/.artifacts/qa/core-profile','core-profile')):
            copied=cli(['docker','cp',name+':'+path,str(output/label)])
            if copied.returncode:raise RuntimeError('Missing QA report folder: '+label)
        profile=json.loads((output/'core-profile/summary.json').read_text());integrity=json.loads((output/'linux-run/source-integrity.json').read_text())
        report['profileSummary']=profile;report['sourceIntegrity']=integrity;report['environment']=json.loads((output/'linux-run/environment.json').read_text())
        report['passed']=result.returncode==0 and report['containerExitCode']==0 and profile['passed'] and integrity['passed']
    except Exception as error:report['error']=type(error).__name__+': '+str(error)
    finally:
        try:
            state=inspect(name) if attempted else None
            if state:
                if state['Config'].get('Labels',{}).get(LABEL)!=run_id:raise RuntimeError('Cleanup owner mismatch')
                if cli(['docker','rm','-f','-v',name]).returncode:raise RuntimeError('Owned container/volume cleanup failed')
            volume=report.get('workspaceVolume')
            removed=not volume or not volume_exists(volume)
            report['cleanupPassed']=inspect(name) is None and removed
        except Exception as error:report['cleanupError']=type(error).__name__+': '+str(error)
        if not report['cleanupPassed']:report['passed']=False
        (output/'summary.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'passed':report['passed'],'profile':args.profile,'cleanupPassed':report['cleanupPassed'],'productionReady':False}),flush=True)
    return 0 if report['passed'] else 1
if __name__=='__main__':raise SystemExit(main())
