"""No-socket negative checks for the prepared QA relay; no application startup."""
import json
import os
from pathlib import Path
import subprocess

ROOT=Path(__file__).resolve().parents[2]
output=ROOT/'.artifacts/qa/linux-startup-20261005'
relay=output/'context-relay/StartupRelay.dll'
checks=[]
for label,mode,args in [('missing-owned-mode',None,[]),('arbitrary-target','synthetic',['example.invalid:80'])]:
    environment={k:v for k,v in os.environ.items() if k.upper()!='DAS_LINUX_STARTUP_RELAY'}
    if mode:environment['DAS_LINUX_STARTUP_RELAY']=mode
    result=subprocess.run(['dotnet',str(relay),*args],cwd=ROOT,env=environment,capture_output=True,text=True,timeout=10)
    checks.append({'case':label,'exitCode':result.returncode,'passed':result.returncode==1 and not result.stdout and not result.stderr})
report={'passed':all(item['passed'] for item in checks),'checks':checks}
(output/'relay-cli-guards.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report))
raise SystemExit(0 if report['passed'] else 1)
