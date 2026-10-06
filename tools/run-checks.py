"""Public check entrypoint for the organized repository; no deployment or live workers."""
from pathlib import Path
import argparse,importlib.util,json,secrets,shutil,subprocess,sys
ROOT=Path(__file__).resolve().parents[1]
def load(name,path):
    s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--profile',required=True,choices=('qa','backend','web','audit'));p.add_argument('--output',default='.artifacts/qa/checks');p.add_argument('--node',default=shutil.which('node') or 'node');p.add_argument('--npm-cli');p.add_argument('--dotnet',default=shutil.which('dotnet') or 'dotnet');a=p.parse_args()
    core=load('core',ROOT/'tools/qa/run-core-ci.py');output=core.fresh_output(ROOT,a.output);output.mkdir(parents=True)
    if a.profile=='qa':
        view=load('view',ROOT/'tools/create-check-view.py').create_view('.artifacts/qa/check-view-'+secrets.token_hex(6))
        with (output/'python-qa.log').open('x',encoding='utf-8') as log:
            result=subprocess.run([sys.executable,'-m','unittest','discover','-s','tests/qa','-v'],cwd=view,stdout=log,stderr=subprocess.STDOUT)
        passed=result.returncode==0;(output/'summary.json').write_text(json.dumps({'passed':passed,'scope':'Legacy QA with documented source/path view;canonical source unchanged','productionReady':False,'view':view.relative_to(ROOT).as_posix()},indent=2)+'\n')
    else:
        core.BACKEND=Path('backend')
        core.PROJECTS=[(name,Path(str(project).replace('Intern-DocumentAdministration-BE','backend',1))) for name,project in core.PROJECTS]
        core.SERVICE_PROJECTS=[(name,Path(str(project).replace('Intern-DocumentAdministration-BE','backend',1))) for name,project in core.SERVICE_PROJECTS]
        project=ROOT/'frontend' if a.profile=='web' else ROOT
        if a.profile=='web':
            original_step=core.run_step
            def run_step(name,command,root,output,environment,timeout=1800):
                if name=='prisma-generate':command=[a.node,str(ROOT/'frontend/scripts/generate-prisma.cjs')]
                return original_step(name,command,root,output,environment,timeout)
            core.run_step=run_step
        if a.profile=='audit':
            command=core.npm_command(a.node,a.npm_cli)
            core.npm_command=lambda *_:command+['--prefix',str(ROOT/'frontend')]
        passed=core.run_profile(a.profile,project,output,a.node,a.npm_cli,a.dotnet)['passed']
    print(json.dumps({'passed':passed,'profile':a.profile,'productionReady':False}));return 0 if passed else 1
if __name__=='__main__':raise SystemExit(main())
