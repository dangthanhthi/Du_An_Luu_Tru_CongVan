"""QA image entrypoint: copy read-only source to owned volume and run one core profile."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys

parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('profile',choices=('backend','web'));args=parser.parse_args()
source=Path('/source');work=Path('/workspace');manifest=json.loads(Path('/qa/source-manifest.json').read_text())
if manifest.get('kind')!='clean-source-WIP-rehearsal' or not manifest.get('files'):raise ValueError('Source manifest required')
if any(work.iterdir()):raise ValueError('Fresh owned workspace volume required')
def verify(root,exact=False):
    changed=[]
    for name,record in manifest['files'].items():
        path=root/name
        if Path(name).is_absolute() or '..' in Path(name).parts or not path.is_file() or path.is_symlink() or hashlib.sha256(path.read_bytes()).hexdigest()!=record['sha256']:changed.append(name)
    if exact:
        actual={path.relative_to(root).as_posix() for path in root.rglob('*') if path.is_file()}
        changed.extend(sorted(actual-set(manifest['files'])))
        changed.extend(str(path.relative_to(root)) for path in root.rglob('*') if path.is_symlink())
    return changed
if verify(source,exact=True):raise ValueError('Read-only source differs from export manifest')
shutil.copytree(source,work,dirs_exist_ok=True)
output=work/'.artifacts/qa/linux-run';output.mkdir(parents=True)
versions={name:subprocess.run(command,check=True,capture_output=True,text=True).stdout.strip() for name,command in {
    'node':['node','--version'],'npm':['node','/usr/local/lib/node_modules/npm/bin/npm-cli.js','--version'],
    'python':['python3','--version'],'dotnet':['dotnet','--version']}.items()}
if not versions['node'].startswith('v22.') or not versions['dotnet'].startswith('10.'):raise ValueError('Node22/.NET10 QA tooling required')
(output/'environment.json').write_text(json.dumps({'versions':versions,'uid':__import__('os').getuid(),'sourceFiles':len(manifest['files']),'remoteClone':False,'hostedCI':False},indent=2)+'\n')
code=0
if args.profile=='backend':
    with (output/'python-qa.log').open('w') as log:
        python=subprocess.run(['python3','-m','unittest','discover','-s','tests/qa','-v'],cwd=work,stdout=log,stderr=subprocess.STDOUT)
    if python.returncode:code=1
result=subprocess.run(['python3','scripts/qa/run-core-ci.py','--profile',args.profile,'--output','.artifacts/qa/core-profile',
                       '--node','/usr/local/bin/node','--npm-cli','/usr/local/lib/node_modules/npm/bin/npm-cli.js'],cwd=work)
if result.returncode:code=1
after=verify(work)
report={'passed':code==0 and not after,'profile':args.profile,'sourceChanged':after,'productionReady':False,'workersEnabled':False,'customerDataUsed':False}
(output/'source-integrity.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report),flush=True)
sys.exit(0 if report['passed'] else 1)
