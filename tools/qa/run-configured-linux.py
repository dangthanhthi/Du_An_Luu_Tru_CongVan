"""Owned configured Linux core startup/proxy rehearsal. No frontend/live deployment.
Secrets stay only in child environments; logs/summary are curated/redacted.
"""
import argparse
import base64
import copy
import hashlib
import hmac
import importlib.util
import json
import os
from pathlib import Path
import re
import secrets
import subprocess
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[2]
LABEL = 'das.qa.linux-startup'
SERVICES = ('auth','document','files','notification','partner','gateway')
RELAY_PORTS = dict(zip(SERVICES,range(18081,18087)))
API = {'auth':'/api/v2/directory/departments', 'document':'/api/v2/documents?kind=OUTGOING&view=mine',
       'files':'/api/files/11111111-1111-1111-1111-111111111111/info', 'notification':'/api/notifications/my',
       'partner':'/api/partners','gateway':'/api/partners'}
ORIGIN = 'https://das.example.test'


def module(name):
    path = Path(__file__).with_name(name+'.py')
    spec = importlib.util.spec_from_file_location(name.replace('-','_'),path)
    result = importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result


def env_with(base, values):
    overridden = {key.upper() for key in values}
    result = {key:value for key,value in base.items() if key.upper() not in overridden}
    result.update(values)
    return result


def cli(args, *, env=None, timeout=120, allow_failure=False):
    result = subprocess.run(args,cwd=ROOT,env=env,capture_output=True,text=True,timeout=timeout)
    if result.returncode and not allow_failure:raise RuntimeError('QA command failed: '+args[0]+' '+args[1])
    return result


def inspect(kind,name):
    args = ['docker','inspect',name] if kind=='container' else ['docker','network','inspect',name]
    result = cli(args,allow_failure=True)
    return None if result.returncode else json.loads(result.stdout)[0]


def port(name,number):
    value = cli(['docker','port',name,str(number)+'/tcp']).stdout.strip()
    match = re.fullmatch(r'127\.0\.0\.1:(\d+)',value)
    if not match:raise ValueError('Only owned loopback binding allowed')
    return int(match.group(1))


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,*args,**kwargs):return None


def request(address,path,*,origin=None,token=None,method='GET'):
    if not re.fullmatch(r'http://127\.0\.0\.1:\d+',address):raise ValueError('Loopback HTTP only')
    headers={}
    if origin:headers['Origin']=origin
    if token:headers['Authorization']='Bearer '+token
    if method=='OPTIONS':headers.update({'Access-Control-Request-Method':'GET','Access-Control-Request-Headers':'authorization'})
    req=urllib.request.Request(address+path,headers=headers,method=method)
    opener=urllib.request.build_opener(urllib.request.ProxyHandler({}),NoRedirect())
    try:response=opener.open(req,timeout=3)
    except urllib.error.HTTPError as error:response=error
    with response:
        body=response.read(65537)
        if len(body)>65536:raise ValueError('Bounded QA response exceeded')
        return response.status,response.headers,body


def signed_reader(key):
    def encode(value):return base64.urlsafe_b64encode(value).decode().rstrip('=')
    head=encode(b'{"alg":"HS256","typ":"JWT"}')
    body=encode(json.dumps({'sub':'22222222-2222-2222-2222-222222222222','exp':int(time.time())+300},separators=(',',':')).encode())
    signed=head+'.'+body
    return signed+'.'+encode(hmac.new(key.encode(),signed.encode(),hashlib.sha256).digest())


def configured_gateway():
    source=json.loads((ROOT/'Intern-DocumentAdministration-BE/gateway/ocelot.json').read_text(encoding='utf-8-sig'))
    result=copy.deepcopy(source);ports={5001:'auth',5002:'document',5003:'partner',5004:'files',5007:'notification'}
    routes=[]
    for route in result['Routes']:
        if route['UpstreamPathTemplate'].startswith(('/api/ai-ocr','/api/email-worker')):continue
        for endpoint in route['DownstreamHostAndPorts']:
            if endpoint['Port'] not in ports:raise ValueError('Unreviewed QA downstream')
            endpoint.update(Host=ports[endpoint['Port']],Port=8080)
        routes.append(route)
    result['Routes']=routes
    result.setdefault('GlobalConfiguration',{})['BaseUrl']='http://gateway:8080'
    return result


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--images',required=True,type=Path)
    parser.add_argument('--fixture',required=True,type=Path)
    parser.add_argument('--relay',required=True,type=Path)
    parser.add_argument('--directory',required=True)
    args=parser.parse_args();builder=module('build-core-images');runtime=module('verify-core-runtime')
    for path in (args.images,args.fixture,args.relay):
        absolute=path.absolute()
        if any(builder.linked(p) for p in (absolute,*absolute.parents)) or not absolute.resolve().is_relative_to((ROOT/'.artifacts/qa').resolve()) or not path.is_file():raise ValueError('Unlinked existing QA inputs required')
    images=json.loads(args.images.read_text(encoding='utf-8-sig'));runtime.validate_summary(images)
    output=builder.fresh_output(ROOT,args.directory);run_id=secrets.token_hex(6)
    network='das-linux-'+run_id;edge=network+'-edge';names={s:network+'-'+s for s in ('sql','relay',*SERVICES)}
    password='Q!'+secrets.token_hex(16)+'a9';key=secrets.token_hex(32);token=None
    environment=env_with(os.environ.copy(),dict(MSSQL_SA_PASSWORD=password,DAS_LINUX_STARTUP_FIXTURE='synthetic',Jwt__Secret=key))
    report={'passed':False,'productionReady':False,'capturedAtUtc':datetime.now(timezone.utc).isoformat(),'runId':run_id,
            'imageSummarySha256':builder.sha(args.images),'fixtureSha256':builder.sha(args.fixture),'relaySha256':builder.sha(args.relay),
            'internalNetwork':False,'loopbackOnly':True,'workersEnabled':False,'smtpEnabled':False,'customerDataUsed':False,
            'deployed':False,'pushed':False,'hosts':{},'checks':[],'fiveStoreHashesUnchanged':False,'cleanupPassed':False}
    attempted=[];created_networks=[];failure=None
    def redacted(value):
        for secret in (password,key,token):
            if secret:value=value.replace(secret,'[redacted]')
        return value
    def run_logged(command,name,env=None,timeout=180):
        result=cli(command,env=env,timeout=timeout,allow_failure=True)
        (output/name).write_text(redacted(result.stdout+result.stderr),encoding='utf-8')
        if result.returncode:raise RuntimeError('QA step failed: '+name)
        return result
    def check(service,label,path,status,*,origin=None,signed=False,method='GET'):
        code,headers,body=request(report['hosts'][service]['address'],path,origin=origin,token=token if signed else None,method=method)
        if code!=status:raise RuntimeError(f'Unexpected {service}/{label} HTTP{code}; expected{status}')
        allowed=origin==ORIGIN
        grants=headers.get_all('Access-Control-Allow-Origin') or []
        if origin and grants!=([ORIGIN] if allowed else []):raise RuntimeError('Unexpected CORS permission: '+service+'/'+label)
        if origin and not allowed and headers.get('Access-Control-Allow-Credentials'):raise RuntimeError('Unexpected credentials grant: '+service+'/'+label)
        if allowed and service!='gateway' and headers.get('Access-Control-Allow-Credentials')!='true':raise RuntimeError('Missing expected configured credentials: '+service)
        report['checks'].append({'service':service,'case':label,'status':code,'signedSyntheticReader':signed,'originAllowed':allowed if origin else None,'method':method,'passed':True})
        return body
    try:
        # Current bytes and actual image identities, not mutable tags or a stale source summary.
        for service in SERVICES:
            record=images['images'][service];context=builder.export_context(ROOT/'Intern-DocumentAdministration-BE',output/('context-check-'+service),service)
            if context['sourceSha256']!=record['sourceSha256']:raise ValueError('Candidate source drift: '+service)
            actual=json.loads(cli(['docker','image','inspect',record['imageId']]).stdout)[0]
            if actual['Id']!=record['imageId'] or actual['Config']['User']!='1654' or actual['Config']['Entrypoint']!=record['entrypoint'] or actual['Config']['Labels'].get('das.source-sha256')!=record['sourceSha256']:raise ValueError('Candidate identity drift: '+service)
        if any(inspect('network',n) for n in (network,edge)) or any(inspect('container',name) for name in names.values()):raise ValueError('QA resource name already exists')
        cli(['docker','network','create','--internal','--label',LABEL+'='+run_id,network]);created_networks.append(network)
        net=inspect('network',network)
        if not net['Internal'] or net.get('Labels',{}).get(LABEL)!=run_id:raise ValueError('Private owned network required')
        report['internalNetwork']=True
        cli(['docker','network','create','--label',LABEL+'='+run_id,edge]);created_networks.append(edge)
        attempted.append(names['sql'])
        cli(['docker','run','-d','--name',names['sql'],'--label',LABEL+'='+run_id,'--network',network,'--network-alias','sql',
             '--memory','2g','--cpus','2','--env','ACCEPT_EULA=Y','--env','MSSQL_SA_PASSWORD','mcr.microsoft.com/mssql/server:2022-latest'],env=environment)
        sql=inspect('container',names['sql']);report['sqlImageId']=sql['Image']
        if set(sql['NetworkSettings']['Networks'])!={network} or sql['HostConfig'].get('PortBindings'):raise ValueError('SQL must stay internal without published ports')
        for attempt in range(60):
            if 'SQL Server is now ready for client connections' in cli(['docker','logs',names['sql']],allow_failure=True).stdout:break
            time.sleep(1)
        else:raise RuntimeError('Owned SQL readiness timeout')
        # Desktop29 drops publish on internal-only networks. Only this fixed QA relay
        # joins an edge network; application/SQL containers retain internal-only connectivity.
        attempted.append(names['relay'])
        relay_command=['docker','create','--name',names['relay'],'--label',LABEL+'='+run_id,'--network',edge,
                       '--read-only','--tmpfs','/tmp:rw,noexec,nosuid,size=33554432,uid=1654,gid=1654','--memory','128m','--cpus','1',
                       '--cap-drop','ALL','--security-opt','no-new-privileges','--env','DAS_LINUX_STARTUP_RELAY=synthetic',
                       '--volume',str(args.relay.absolute().parent)+':/qa:ro','--entrypoint','dotnet']
        for number in (1433,*RELAY_PORTS.values()):relay_command+=['--publish','127.0.0.1::'+str(number)]
        relay_command+=[images['images']['auth']['imageId'],'/qa/StartupRelay.dll']
        cli(relay_command);cli(['docker','network','connect',network,names['relay']]);cli(['docker','start',names['relay']])
        relay_state=inspect('container',names['relay'])
        if set(relay_state['NetworkSettings']['Networks'])!={network,edge} or relay_state['Config']['User']!='1654' or not relay_state['HostConfig']['ReadonlyRootfs']:raise ValueError('QA relay isolation mismatch')
        report['qaRelay']={'fixedDestinations':7,'nonroot':True,'readOnly':True,'credentialsConfigured':False,'edgeNetworkInternal':False,'notProductionTopology':True}
        sql_port=port(names['relay'],1433)
        environment['DAS_TEST_SQL_CONNECTION']=f'Server=127.0.0.1,{sql_port};Database=master;User Id=sa;Password={password};TrustServerCertificate=true;Connect Timeout=15'
        run_logged(['dotnet',str(args.fixture.absolute()),'prepare',str(output),run_id],'stores-prepare.log',env=environment)
        gateway_path=output/'ocelot-qa.json';gateway_path.write_text(json.dumps(configured_gateway(),indent=2)+'\n',encoding='utf-8')
        report['gatewayConfigSha256']=builder.sha(gateway_path)
        for service in SERVICES:
            name=names[service];attempted.append(name)
            env=env_with(environment,dict(ConnectionStrings__Default=f'Server=sql,1433;Database=das_linux_{run_id}_{service};User Id=sa;Password={password};TrustServerCertificate=true;Connect Timeout=15',Database__Provider='SqlServer',Storage__Path='/app/storage',Directory__SourceId='eap',Cors__AllowedOrigins__0=ORIGIN,Cors__Origins__0=ORIGIN,**builder.DISABLED))
            command=['docker','run','-d','--name',name,'--label',LABEL+'='+run_id,'--network',network,'--network-alias',service,
                     '--read-only','--tmpfs','/tmp:rw,noexec,nosuid,size=67108864,uid=1654,gid=1654','--memory','512m','--cpus','1',
                     '--cap-drop','ALL','--security-opt','no-new-privileges']
            keys=['Jwt__Secret','Database__Provider','ConnectionStrings__Default','Storage__Path','Directory__SourceId','Cors__AllowedOrigins__0','Cors__Origins__0',*builder.DISABLED]
            for key_name in keys:command+=['--env',key_name]
            if service=='gateway':command+=['--volume',str(gateway_path.absolute())+':/app/ocelot.json:ro']
            command+=[images['images'][service]['imageId']];cli(command,env=env)
            address='http://127.0.0.1:'+str(port(names['relay'],RELAY_PORTS[service]))
            for attempt in range(60):
                state=inspect('container',name)
                if not state or not state['State']['Running']:raise RuntimeError('Configured host exited: '+service)
                try:
                    code,headers,body=request(address,'/health')
                    if code==200:break
                    if code in (401,403,404):raise RuntimeError(f'Configured health route rejected: {service} HTTP{code}')
                except (OSError,urllib.error.URLError):pass
                time.sleep(1)
            else:raise RuntimeError('Configured health timeout: '+service)
            health=json.loads(body);data=health.get('data',health)
            if data.get('status')!='healthy':raise RuntimeError('Invalid health response: '+service)
            if service=='notification' and (data['workerEnabled'] or data['smtpEnabled']):raise RuntimeError('Outbound flags unexpectedly enabled')
            if set(state['NetworkSettings']['Networks'])!={network} or state['HostConfig'].get('PortBindings'):raise RuntimeError('Application must stay internal without published ports')
            actual_env=dict(item.split('=',1) for item in state['Config']['Env'])
            production=actual_env.get('ASPNETCORE_ENVIRONMENT')=='Production'
            workers_off=all(actual_env.get(flag)=='false' for flag in builder.DISABLED)
            if not production or not workers_off:raise RuntimeError('Production/disabled-worker environment mismatch')
            report['hosts'][service]={'imageId':images['images'][service]['imageId'],'sourceSha256':images['images'][service]['sourceSha256'],
                                      'address':address,'healthPassed':True,'productionEnvironment':production,'workersDisabled':workers_off,
                                      'nonroot':state['Config']['User']=='1654','readOnly':state['HostConfig']['ReadonlyRootfs']}
            if not report['hosts'][service]['nonroot'] or not report['hosts'][service]['readOnly']:raise RuntimeError('Host isolation mismatch')
            check(service,'anonymous-protected',API[service],401)
            check(service,'configured-origin-preflight',API[service],204,origin=ORIGIN,method='OPTIONS')
            check(service,'other-origin-preflight',API[service],204,origin='https://untrusted.example.test',method='OPTIONS')
            check(service,'other-origin-anonymous',API[service],401,origin='https://untrusted.example.test')
            print(service+': configured Linux health/anonymous/CORS passed',flush=True)
        token=signed_reader(key)
        for service in ('partner','gateway'):
            body=check(service,'signed-reader-empty-partners','/api/partners',200,signed=True,origin=ORIGIN)
            page=json.loads(body)
            if page.get('success') is not True or page['data']['totalCount']!=0:raise RuntimeError('Unexpected synthetic partner data')
        check('gateway','signed-reader-notification-proxy','/api/notifications/my',200,signed=True)
        check('gateway','missing-authority-fail-closed','/api/v2/documents?kind=OUTGOING&view=mine',503,signed=True)
        check('gateway','missing-directory-fail-closed','/api/v2/directory/departments',503,signed=True)
        check('gateway','missing-file-proxy','/api/files/11111111-1111-1111-1111-111111111111/info',404,signed=True)
        run_logged(['dotnet',str(args.fixture.absolute()),'verify',str(output),run_id],'stores-verify.log',env=environment)
        before=json.loads((output/'stores-before.json').read_text());after=json.loads((output/'stores-after.json').read_text())
        if before['tableHashes']!=after['tableHashes'] or len(before['tableHashes'])!=5:raise RuntimeError('Fixture row hashes changed')
        report['fiveStoreHashesUnchanged']=True;report['tableHashes']=after['tableHashes'];report['tableCounts']=after['tableCounts']
        report['passed']=True
    except Exception as error:
        failure=type(error).__name__+': '+redacted(str(error));report['error']=failure
    finally:
        cleanup_errors=[]
        for name in reversed(attempted):
            try:
                current=inspect('container',name)
                if current:
                    if current.get('Config',{}).get('Labels',{}).get(LABEL)!=run_id:raise RuntimeError('Resource owner mismatch')
                    if name!=names['sql']:
                        logs=cli(['docker','logs',name],allow_failure=True)
                        (output/(name.rsplit('-',1)[-1]+'-host.log')).write_text(redacted(logs.stdout+logs.stderr),encoding='utf-8')
                    cli(['docker','rm','-f',name])
            except Exception as error:cleanup_errors.append(type(error).__name__+': '+redacted(str(error)))
        for created_network in reversed(created_networks):
            try:
                current=inspect('network',created_network)
                if current:
                    if current.get('Labels',{}).get(LABEL)!=run_id:raise RuntimeError('Network owner mismatch')
                    cli(['docker','network','rm',created_network])
            except Exception as error:cleanup_errors.append(type(error).__name__+': '+redacted(str(error)))
        try:
            remaining=cli(['docker','ps','-a','--filter','label='+LABEL+'='+run_id,'--format','{{.Names}}'],allow_failure=True)
            remaining_networks=[n for n in created_networks if inspect('network',n)]
            report['cleanupPassed']=not cleanup_errors and remaining.returncode==0 and not remaining.stdout.strip() and not remaining_networks
        except Exception as error:
            cleanup_errors.append(type(error).__name__+': '+redacted(str(error)))
        report['cleanupErrors']=cleanup_errors
        if not report['cleanupPassed']:report['passed']=False
        (output/'summary.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'passed':report['passed'],'healthyLinuxHosts':len(report['hosts']),'checks':len(report['checks']),
                      'fiveStoreHashesUnchanged':report['fiveStoreHashesUnchanged'],'cleanupPassed':report['cleanupPassed'],'productionReady':False}),flush=True)
    if failure:print(failure,flush=True)
    return 0 if report['passed'] else 1


if __name__=='__main__':raise SystemExit(main())
