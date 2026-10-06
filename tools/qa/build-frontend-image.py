"""Local frontend candidate: bounded source, locked install, standalone and nonroot.
No registry push, company configuration or live authentication acceptance.
"""
import argparse
from datetime import datetime,timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import secrets
import importlib.util

ROOT=Path(__file__).resolve().parents[2]
REQUIRED=('package.json','package-lock.json','next.config.ts','tsconfig.json','postcss.config.mjs','declarations.d.ts','.npmrc')
EXCLUDED={'node_modules','.next','.git','.artifacts','.secrets','__pycache__','bin','obj'}

def linked(path):return path.is_symlink() or getattr(path,'is_junction',lambda:False)()
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def cli(args,**kwargs):return subprocess.run(args,cwd=ROOT,capture_output=True,text=True,timeout=120,**kwargs)

def export_context(root,target):
    root,target=Path(root).absolute(),Path(target).absolute()
    if '..' in target.parts or target.exists() or not target.is_relative_to(root/'.artifacts/qa') or any(linked(p) for p in (root,target,*root.parents,*target.parents)):
        raise ValueError('Fresh unlinked QA context required')
    if any(not (root/name).is_file() for name in REQUIRED) or any(not (root/name).is_dir() for name in ('src','public')):
        raise ValueError('Missing frontend build source/lock/config')
    # This project needs only its existing peer-dependency switch. Never copy registry credentials/config.
    if (root/'.npmrc').read_text().strip()!='legacy-peer-deps=true':raise ValueError('Unexpected npm configuration; no registry credentials allowed')
    paths=[root/name for name in REQUIRED]
    for scope in ('src','public'):
        for folder,dirs,files in os.walk(root/scope,followlinks=False):
            if linked(Path(folder)):raise ValueError('Linked frontend source')
            kept=[]
            for name in dirs:
                if name in EXCLUDED:continue
                if linked(Path(folder)/name):raise ValueError('Linked frontend source')
                kept.append(name)
            dirs[:]=kept
            for name in files:
                path=Path(folder)/name
                if name.startswith('.env') or path.suffix.lower() in {'.pem','.key','.pfx','.db','.sqlite','.bak'}:continue
                paths.append(path)
    records={}
    for path in paths:
        if any(linked(p) for p in (path,*path.parents)) or not path.resolve().is_relative_to(root.resolve()):raise ValueError('Linked or escaped source')
        records[path.relative_to(root).as_posix()]={'sha256':sha(path),'bytes':path.stat().st_size}
    target.mkdir(parents=True)
    for name,record in records.items():
        dest=target/name;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(root/name,dest)
        if sha(dest)!=record['sha256']:raise ValueError('Source changed during export')
    digest=hashlib.sha256(json.dumps(records,sort_keys=True,separators=(',',':')).encode()).hexdigest()
    return {'files':records,'sourceSha256':digest}

def official_node_digest(refs):
    matches=[ref for ref in refs if isinstance(ref,str) and re.fullmatch(r'node@sha256:[0-9a-f]{64}',ref)]
    if len(matches)!=1:raise ValueError('One official immutable Node base required')
    return matches[0]

def dockerfile(base,source_hash):
    official_node_digest([base])
    if not re.fullmatch('[0-9a-f]{64}',source_hash):raise ValueError('Source hash required')
    return f'''FROM {base} AS runtime
WORKDIR /app
ENV NODE_ENV=production NEXT_TELEMETRY_DISABLED=1 HOSTNAME=0.0.0.0 PORT=3000
LABEL das.qa.frontend="prepared-candidate" das.source-sha256="{source_hash}"
COPY --chown=1000:1000 app ./
RUN mkdir -p /app/.next/cache && chown 1000:1000 /app/.next/cache
USER 1000:1000
EXPOSE 3000
CMD ["node","server.js"]
'''

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--directory',required=True);parser.add_argument('--base',required=True);parser.add_argument('--tooling',required=True)
    args=parser.parse_args();base=official_node_digest([args.base])
    relative=args.directory
    if '\\' in relative or ':' in relative or relative.startswith('/') or any(p in ('','.','..') for p in relative.split('/')):raise ValueError('Invalid output')
    output=ROOT/relative
    if output==ROOT/'.artifacts/qa' or output.exists() or not output.is_relative_to(ROOT/'.artifacts/qa') or any(linked(p) for p in (output,*output.parents)):
        raise ValueError('Fresh owned QA output required')
    image=json.loads(cli(['docker','image','inspect',base],check=True).stdout)[0]
    if base not in image['RepoDigests'] or cli(['docker','run','--rm','--network','none','--read-only','--cap-drop','ALL','--security-opt','no-new-privileges','--user','1000:1000',base,'node','--version'],check=True).stdout.strip().split('.')[0]!='v22':
        raise ValueError('Local official Node22 base required')
    if not re.fullmatch('sha256:[0-9a-f]{64}',args.tooling):raise ValueError('Immutable tooling image required')
    tooling=json.loads(cli(['docker','image','inspect',args.tooling],check=True).stdout)[0]
    if tooling['Id']!=args.tooling or tooling['Config']['User']!='1654:1654' or tooling['Config'].get('Labels',{}).get('das.qa.tooling')!='linux-core-ci':raise ValueError('Prepared nonroot Node22 tooling required')
    context=output/'context-frontend';record=export_context(ROOT,context)
    runtime=output/'context-runtime';runtime.mkdir();app=runtime/'app';app.mkdir()
    recipe=dockerfile(base,record['sourceSha256']);(runtime/'Dockerfile').write_text(recipe,encoding='utf-8')
    record.update({'baseRef':base,'baseImageId':image['Id'],'dockerfileSha256':sha(runtime/'Dockerfile'),'toolingImageId':args.tooling})
    (output/'source-manifest.json').write_text(json.dumps(record,indent=2)+'\n')
    tag='das-qa-frontend:'+output.name
    report={'passed':False,'productionReady':False,'sourceSha256':record['sourceSha256'],'baseRef':base,'pushed':False,'deployed':False,
            'capturedAtUtc':datetime.now(timezone.utc).isoformat(),'compileMemoryLimitEnforced':True,'packagingMemoryLimitEnforced':False,'buildMemoryBytes':3221225472,'buildSwapAllowanceBytes':1073741824,'buildCpus':2,'nextBuildCpus':1,'nodeHeapLimitMiB':2304,'bundler':'webpack','cleanupPassed':False}
    run_id=secrets.token_hex(6);name='das-front-build-'+run_id;attempted=False;volume=None
    spec=importlib.util.spec_from_file_location('linux_ci',ROOT/'scripts/qa/run-linux-core-ci.py');linux=importlib.util.module_from_spec(spec);spec.loader.exec_module(linux)
    # Compile in a fresh owned workspace; the final Docker build only packages its standalone outputs.
    compile_script="""const fs=require('fs'),cp=require('child_process');
if(fs.readdirSync('/workspace').length)throw Error('Fresh workspace required');
fs.cpSync('/source','/workspace',{recursive:true});
for(const args of [['ci','--ignore-scripts','--no-audit','--no-fund','--maxsockets=4','--loglevel=http','--logs-dir=/workspace/npm-debug'],['run','build']]){
 const result=cp.spawnSync(process.execPath,['/usr/local/lib/node_modules/npm/bin/npm-cli.js',...args],{cwd:'/workspace',stdio:'inherit'});
 if(result.status!==0)process.exit(result.status||1);
}
if(!fs.existsSync('/workspace/.next/standalone/server.js'))throw Error('Standalone output missing');
"""
    try:
        if linux.inspect(name):raise ValueError('Build resource occupied')
        attempted=True
        cli(['docker','create','--name',name,'--label','das.qa.frontend-build='+run_id,'--read-only','--memory','3221225472','--memory-swap','4294967296','--cpus','2',
             '--cap-drop','ALL','--security-opt','no-new-privileges','--tmpfs','/tmp:rw,noexec,nosuid,size=268435456,uid=1654,gid=1654',
             '--volume','/workspace','--mount','type=bind,source='+str(context)+',target=/source,readonly','--env','NODE_OPTIONS=--max-old-space-size=2304',
             '--env','CI=true','--entrypoint','node',args.tooling,'-e',compile_script],check=True)
        state=linux.inspect(name);mounts=[m for m in state['Mounts'] if m['Destination']=='/workspace']
        if len(mounts)!=1 or mounts[0]['Type']!='volume' or not state['HostConfig']['ReadonlyRootfs'] or state['Config']['User']!='1654:1654' or state['HostConfig']['Memory']!=3221225472:raise ValueError('Compile boundary mismatch')
        volume=mounts[0]['Name'];report['buildContainer']=name;report['buildVolume']=volume
        with (output/'compile.log').open('x',encoding='utf-8') as log:
            compiled=subprocess.run(['docker','start','-a',name],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,timeout=3600)
        state=linux.inspect(name);report['compileExitCode']=state['State']['ExitCode'];report['oomKilled']=state['State']['OOMKilled']
        report['npmDiagnosticsCaptured']=cli(['docker','cp',name+':/workspace/npm-debug',str(output/'npm-debug')]).returncode==0
        if compiled.returncode or report['compileExitCode']:raise RuntimeError('Bounded frontend compile failed; see compile.log')
        for source,destination in [('/workspace/.next/standalone/.',app),('/workspace/.next/static',app/'.next/static'),('/workspace/public',app/'public')]:
            cli(['docker','cp',name+':'+source,str(destination)],check=True)
        compiled_files={path.relative_to(app).as_posix():{'sha256':sha(path),'bytes':path.stat().st_size} for path in app.rglob('*') if path.is_file()}
        (output/'standalone-manifest.json').write_text(json.dumps({'files':compiled_files,'sourceSha256':record['sourceSha256']},indent=2)+'\n')
        with (output/'build.log').open('x',encoding='utf-8') as log:
            built=subprocess.run(['docker','build','--progress','plain','--pull=false','--tag',tag,str(runtime)],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,timeout=600)
        if built.returncode:raise RuntimeError('Frontend image build failed; see build.log')
        actual=json.loads(cli(['docker','image','inspect',tag],check=True).stdout)[0]
        if actual['Config']['User']!='1000:1000' or actual['Config'].get('Labels',{}).get('das.source-sha256')!=record['sourceSha256']:raise ValueError('Image identity mismatch')
        report['imageId']=actual['Id'];report['tag']=tag;report['entrypoint']=actual['Config']['Cmd']
        report['sourceChanged']=[name for name,value in record['files'].items() if sha(ROOT/name)!=value['sha256']]
        report['passed']=not report['sourceChanged']
    except Exception as error:report['error']=type(error).__name__+': '+str(error)
    finally:
        try:
            state=linux.inspect(name) if attempted else None
            if state:
                if state['Config'].get('Labels',{}).get('das.qa.frontend-build')!=run_id:raise RuntimeError('Build cleanup owner mismatch')
                cli(['docker','rm','-f','-v',name],check=True)
            report['cleanupPassed']=linux.inspect(name) is None and (not volume or not linux.volume_exists(volume))
        except Exception as error:report['cleanupError']=type(error).__name__+': '+str(error)
        if not report['cleanupPassed']:report['passed']=False
    (output/'summary.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(report),flush=True);return 0 if report['passed'] else 1
if __name__=='__main__':raise SystemExit(main())
