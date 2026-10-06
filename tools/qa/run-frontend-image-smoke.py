"""Bounded local anonymous frontend image checks; no login/business writes or EAP.
Own container/cache volume only; loopback port, synthetic ephemeral session key.
"""
import argparse
from datetime import datetime,timezone
import hashlib
import http.client
import importlib.util
import json
import os
from pathlib import Path
import re
import secrets
import subprocess
import time

ROOT=Path(__file__).resolve().parents[2];LABEL='das.qa.frontend-smoke'
spec=importlib.util.spec_from_file_location('linux_ci',ROOT/'scripts/qa/run-linux-core-ci.py')
linux=importlib.util.module_from_spec(spec);spec.loader.exec_module(linux)
def cli(args,**kwargs):return subprocess.run(args,cwd=ROOT,capture_output=True,text=True,timeout=120,**kwargs)
def smoke_environment(base,key):
    env={name:value for name,value in base.items() if name.upper()!='NEXTAUTH_SECRET'}
    env['NEXTAUTH_SECRET']=key
    return env

def image_boundary(image,expected):
    config=image['Config']
    if image['Id']!=expected or config['User']!='1000:1000' or config.get('Labels',{}).get('das.qa.frontend')!='prepared-candidate' or config.get('Cmd')!=['node','server.js']:
        raise ValueError('Prepared immutable nonroot frontend image required')
    digest=config.get('Labels',{}).get('das.source-sha256','')
    if not re.fullmatch('[0-9a-f]{64}',digest):raise ValueError('Frontend source hash required')
    return digest

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--image',required=True);parser.add_argument('--directory',required=True)
    parser.add_argument('--include-template-data',action='store_true',help='Check the ten disabled static template GET APIs on a new candidate')
    parser.add_argument('--include-template-pages',action='store_true',help='Check representative template pages stop rendering after their data actions are disabled')
    args=parser.parse_args()
    if args.include_template_pages and not args.include_template_data:raise ValueError('Template page checks require the template API boundary checks')
    if not re.fullmatch('sha256:[0-9a-f]{64}',args.image):raise ValueError('Immutable image ID required')
    source_hash=image_boundary(json.loads(cli(['docker','image','inspect',args.image],check=True).stdout)[0],args.image)
    builder_spec=importlib.util.spec_from_file_location('builder',ROOT/'scripts/qa/build-core-images.py')
    builder=importlib.util.module_from_spec(builder_spec);builder_spec.loader.exec_module(builder)
    output=builder.fresh_output(ROOT,args.directory)
    run_id=secrets.token_hex(6);name='das-front-smoke-'+run_id;key=secrets.token_hex(48)
    env=smoke_environment(os.environ,key)
    report={'passed':False,'productionReady':False,'capturedAtUtc':datetime.now(timezone.utc).isoformat(),'imageId':args.image,'sourceSha256':source_hash,
            'container':name,'runId':run_id,'checks':[],'cleanupPassed':False,'deployed':False,'realLogin':False,'businessWrites':False,'smtpTmsEnabled':False,
            'runtimeEgressAllowed':True,'memoryBytes':805306368,'cpus':1,'hostCredentialsMounted':False,'syntheticSessionSecret':True,'templateDataChecks':args.include_template_data,'templatePageChecks':args.include_template_pages}
    created=False;volume=None
    def request(method,path,body=None):
        connection=http.client.HTTPConnection('127.0.0.1',port,timeout=5)
        try:
            connection.request(method,path,body,{'Content-Type':'application/json'} if body else {})
            response=connection.getresponse();payload=response.read(2097153)
            if len(payload)>2097152:raise ValueError('HTTP response limit exceeded')
            return response.status,{name.lower():value for name,value in response.getheaders()},payload
        finally:connection.close()
    def check(label,condition,**details):
        report['checks'].append({'name':label,'passed':bool(condition),**details})
        if not condition:raise RuntimeError('HTTP/package boundary failed: '+label)
    try:
        if linux.inspect(name):raise ValueError('Resource occupied')
        cli(['docker','create','--name',name,'--label',LABEL+'='+run_id,'--read-only','--cap-drop','ALL','--security-opt','no-new-privileges',
             '--memory','768m','--cpus','1','--tmpfs','/tmp:rw,noexec,nosuid,size=67108864,uid=1000,gid=1000','--volume','/app/.next/cache',
             '--publish','127.0.0.1::3000','--env','NEXTAUTH_SECRET','--env','NEXTAUTH_URL=http://localhost:3000',args.image],env=env,check=True)
        created=True;state=linux.inspect(name)
        mounts=state['Mounts'];cache=[m for m in mounts if m['Destination']=='/app/.next/cache']
        if len(mounts)!=1 or len(cache)!=1 or cache[0]['Type']!='volume':raise ValueError('Only owned cache volume allowed')
        volume=cache[0]['Name'];report['cacheVolume']=volume
        if not state['HostConfig']['ReadonlyRootfs'] or state['Config']['User']!='1000:1000' or state['HostConfig']['Memory']!=805306368:raise ValueError('Runtime boundary mismatch')
        cli(['docker','start',name],check=True)
        binding=cli(['docker','port',name,'3000/tcp'],check=True).stdout.strip()
        if not re.fullmatch(r'127\.0\.0\.1:[0-9]+',binding):raise ValueError('Loopback-only binding required')
        port=int(binding.rsplit(':',1)[1]);report['port']=port
        deadline=time.monotonic()+60
        while True:
            try:status,headers,html=request('GET','/vi/login');break
            except (ConnectionError,OSError,http.client.HTTPException):
                if time.monotonic()>=deadline:raise TimeoutError('Frontend startup deadline')
                time.sleep(1)
        check('login SSR',status==200 and b'<html' in html,status=status)
        chunks=re.findall(rb'(?:src|href)="(/_next/static/[^"<>]+)"',html)
        if not chunks:raise ValueError('Login page contains no bundled static resources')
        path=chunks[0].decode('utf-8');status,headers,content=request('GET',path)
        check('built static resource',status==200 and bool(content),status=status,bytes=len(content),sha256=hashlib.sha256(content).hexdigest())
        status,headers,content=request('GET','/images/avatars/1.png')
        check('public asset',status==200 and content.startswith(b'\x89PNG'),status=status,bytes=len(content))
        status,headers,content=request('GET','/')
        check('root redirect',status==307 and headers.get('location')=='/vi/dashboards/overview',status=status)
        status,headers,content=request('GET','/api/auth/session')
        check('anonymous session',status==200 and json.loads(content)=={},status=status)
        for path,expected,code in [('/api/login',410,'TEMPLATE_LOGIN_DISABLED'),('/api/email/test',503,'INTEGRATION_DEFERRED'),('/api/email/scan',503,'INTEGRATION_DEFERRED'),('/api/ocr/analyze',503,'INTEGRATION_DEFERRED')]:
            status,headers,content=request('POST',path,'{"username":"admin","password":"password"}')
            body=json.loads(content)
            check(path,status==expected and body.get('code')==code and not body.get('accessToken') and not body.get('role') and headers.get('cache-control')=='no-store' and 'set-cookie' not in headers,status=status)
        if args.include_template_data:
            for route in ('apps/academy','apps/user-list','apps/invoice','apps/permissions','apps/ecommerce','apps/logistics','pages/faq','pages/widget-examples','pages/pricing','pages/profile'):
                path='/api/'+route;status,headers,content=request('GET',path)
                check(path,status==410 and json.loads(content)=={'code':'TEMPLATE_DATA_DISABLED'} and headers.get('cache-control')=='no-store' and 'set-cookie' not in headers,status=status)
        if args.include_template_pages:
            for path in ('/vi/apps/user/list','/vi/apps/roles','/vi/apps/permissions','/vi/apps/ecommerce/dashboard','/vi/apps/academy/dashboard'):
                status,headers,content=request('GET',path);lower=content.lower()
                # Next can already have streamed a 200 shell when a not-found interrupt occurs.
                not_found=status==404 or (status==200 and b'content="noindex"' in lower and
                    any(marker in lower for marker in (b'next_http_error_fallback;404',b'this page could not be found',b'page not found')))
                check(path,not_found and 'set-cookie' not in headers,status=status,notFoundBoundary=bool(not_found),bytes=len(content))
        script="const f=require('fs');const paths=f.readdirSync('/app',{recursive:true});const bad=paths.filter(p=>/\\.(pem|key|pfx|db|sqlite|bak)$/.test(p)||p.split('/').some(n=>n.startsWith('.env')&&n!=='.env.example'));console.log(JSON.stringify({server:f.existsSync('/app/server.js'),static:f.existsSync('/app/.next/static'),public:f.existsSync('/app/public'),uid:process.getuid(),credentialOrDatabaseFiles:bad}));"
        package=json.loads(cli(['docker','exec',name,'node','-e',script],check=True).stdout)
        report['package']=package
        check('standalone package boundary',package['uid']==1000 and package['server'] and package['static'] and package['public'] and not package['credentialOrDatabaseFiles'])
        report['passed']=True
    except Exception as error:report['error']=type(error).__name__+': '+str(error).replace(key,'[redacted]')
    finally:
        try:
            state=linux.inspect(name)
            if state:
                if not created or state['Config'].get('Labels',{}).get(LABEL)!=run_id:raise RuntimeError('Owner mismatch')
                log=cli(['docker','logs',name]);(output/'container.log').write_text((log.stdout+log.stderr).replace(key,'[redacted]'),encoding='utf-8')
                cli(['docker','rm','-f','-v',name],check=True)
            report['cleanupPassed']=linux.inspect(name) is None and (not volume or not linux.volume_exists(volume))
        except Exception as error:report['cleanupError']=type(error).__name__+': '+str(error).replace(key,'[redacted]')
        if not report['cleanupPassed']:report['passed']=False
        (output/'summary.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps({'passed':report['passed'],'checks':len(report['checks']),'cleanupPassed':report['cleanupPassed'],'productionReady':False}),flush=True)
    return 0 if report['passed'] else 1
if __name__=='__main__':raise SystemExit(main())
