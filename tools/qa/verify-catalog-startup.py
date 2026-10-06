"""Local isolated HTTP/SQLite smoke; legacy test JWT, never EAP acceptance."""
import base64
import hashlib
import hmac
import json
import os
from pathlib import Path
import socket
import sqlite3
import subprocess
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / '.artifacts' / 'qa' / 'catalog-startup-smoke'
OUT.mkdir(parents=True, exist_ok=True)
BE = ROOT / 'Intern-DocumentAdministration-BE' / 'services'
KEY = 'local-qa-only-test-signing-material-0000000000'
results = []

def check(name, action):
    action()
    results.append({'name': name, 'status': 'Passed'})

def token(manage=True):
    def encode(value):
        return base64.urlsafe_b64encode(json.dumps(value, separators=(',', ':')).encode()).rstrip(b'=')
    payload = {'sub': '11111111-1111-4111-8111-111111111111', 'exp': int(time.time()) + 300}
    if manage:
        payload['das_capability'] = 'CatalogManage'
    body = encode({'alg': 'HS256', 'typ': 'JWT'}) + b'.' + encode(payload)
    signature = base64.urlsafe_b64encode(hmac.new(KEY.encode(), body, hashlib.sha256).digest()).rstrip(b'=')
    return (body + b'.' + signature).decode()

def request(port, endpoint, method='GET', data=None, bearer=None):
    headers = {'Content-Type': 'application/json'}
    if bearer:
        headers['Authorization'] = 'Bearer ' + bearer
    req = urllib.request.Request(f'http://127.0.0.1:{port}{endpoint}', data=None if data is None else json.dumps(data).encode(), headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=5) as response:
            return response.status, json.load(response)
    except urllib.error.HTTPError as error:
        body = error.read()
        return error.code, json.loads(body) if body else None

def launch(service, assembly, port, database):
    with socket.socket() as probe:
        probe.bind(('127.0.0.1', port))
    env = os.environ.copy()
    env.update({'ASPNETCORE_ENVIRONMENT': 'Development', 'ASPNETCORE_URLS': f'http://127.0.0.1:{port}',
                'Database__Provider': 'Sqlite', 'Database__Initialize': 'true', 'Database__SeedDemoUsers': 'false',
                'ConnectionStrings__Default': 'Data Source=' + str(database), 'Jwt__Secret': KEY})
    log = open(OUT / (service + '.log'), 'w', encoding='utf-8')
    process = subprocess.Popen(['dotnet', str(BE / service / 'bin' / 'Debug' / 'net10.0' / (assembly + '.dll'))],
                               cwd=BE / service, env=env, stdout=log, stderr=log,
                               creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
    deadline = time.monotonic() + 25
    while time.monotonic() < deadline:
        if process.poll() is not None:
            log.close()
            raise AssertionError(service + ' failed startup; inspect its test log')
        try:
            if request(port, '/health')[0] == 200:
                return process, log
        except (OSError, urllib.error.URLError):
            pass
        time.sleep(.2)
    process.terminate(); process.wait(timeout=5); log.close()
    raise AssertionError(service + ' startup timed out')

def assert_production_rejects(service, assembly, setting):
    env = os.environ.copy()
    env.update({'ASPNETCORE_ENVIRONMENT': 'Production', 'Database__Provider': 'SqlServer',
                'Database__Initialize': 'false', 'Database__SeedDemoUsers': 'false',
                'ConnectionStrings__Default': 'Server=127.0.0.1;Database=qa_unused;Integrated Security=true', 'Jwt__Secret': KEY})
    env.update(setting)
    result = subprocess.run(['dotnet', str(BE / service / 'bin' / 'Debug' / 'net10.0' / (assembly + '.dll'))],
                            cwd=BE / service, env=env, capture_output=True, text=True, timeout=15,
                            creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
    assert result.returncode != 0 and 'limited to Development' in result.stderr

processes = []
try:
    # Unique filenames keep earlier evidence and never touch the default databases.
    run_id = str(time.time_ns())
    auth_db, doc_db = OUT / ('auth-' + run_id + '.db'), OUT / ('document-' + run_id + '.db')
    processes.append(launch('auth-service', 'AuthService', 3212, auth_db))
    processes.append(launch('document-service', 'DocumentService', 3213, doc_db))
    def no_demo():
        with sqlite3.connect(auth_db) as connection:
            assert connection.execute('SELECT COUNT(*) FROM Users').fetchone()[0] == 0
            assert connection.execute('SELECT COUNT(*) FROM Roles').fetchone()[0] == 0
            assert connection.execute('SELECT COUNT(*) FROM DirectoryProjections').fetchone()[0] == 0
        with sqlite3.connect(doc_db) as connection:
            assert connection.execute('SELECT COUNT(*) FROM BusinessCatalogEntries').fetchone()[0] == 21
            assert connection.execute("SELECT COUNT(*) FROM DistributionTargets WHERE MappingState='Pending'").fetchone()[0] == 14
    check('startup-honors-isolated-path-and-creates-no-demo-identities', no_demo)
    def authz():
        assert request(3213, '/api/v2/catalogs')[0] == 401
        assert request(3213, '/api/v2/admin/catalogs', 'POST', {'group': 'categories', 'code': 'DENIED', 'name': 'Denied'}, token(False))[0] == 403
    check('catalog-HTTP-authentication-and-capability-enforced', authz)
    access = token()
    def lifecycle():
        status, envelope = request(3213, '/api/v2/catalogs', bearer=access)
        assert status == 200 and len(envelope['data']) == 6 and envelope['data']['sensitivity'][0]['code'] in ['Normal', 'Confidential']
        status, created = request(3213, '/api/v2/admin/catalogs', 'POST', {'group': 'categories', 'code': ' smoke ', 'name': ' Smoke category '}, access)
        assert status == 201
        row = created['data']; assert row['code'] == 'SMOKE' and row['version'] == 1
        edit = {'name': 'Reviewed', 'sortOrder': 2, 'isActive': False, 'version': 1}
        assert request(3213, '/api/v2/admin/catalogs/' + row['id'], 'PUT', edit, access)[0] == 200
        assert request(3213, '/api/v2/admin/catalogs/' + row['id'], 'PUT', edit, access)[0] == 409
        assert request(3213, '/api/v2/catalogs?groups=categories', bearer=access)[1]['data']['categories'] == []
        assert request(3213, '/api/v2/catalogs/' + row['id'], bearer=access)[1]['data']['isActive'] is False
        with sqlite3.connect(doc_db) as connection:
            assert connection.execute('SELECT COUNT(*) FROM CatalogAuditEvents').fetchone()[0] == 2
    check('real-HTTP-create-CAS-deactivate-historical-read-and-transactional-audit', lifecycle)
    check('production-rejects-SQLite', lambda: assert_production_rejects('document-service', 'DocumentService', {'Database__Provider': 'Sqlite', 'ConnectionStrings__Default': 'Data Source=unused.db'}))
    check('production-rejects-startup-migration', lambda: assert_production_rejects('document-service', 'DocumentService', {'Database__Initialize': 'true'}))
    check('production-rejects-demo-seeding', lambda: assert_production_rejects('auth-service', 'AuthService', {'Database__SeedDemoUsers': 'true'}))
except Exception as error:
    results.append({'name': 'smoke-aborted', 'status': 'Failed', 'message': str(error)})
finally:
    for process, log in reversed(processes):
        process.terminate()
        try: process.wait(timeout=5)
        except subprocess.TimeoutExpired: process.kill(); process.wait(timeout=5)
        log.close()
    report = {'fixture': 'Real local services and isolated SQLite; synthetic legacy JWT. Not live EAP or production SQL acceptance.',
              'utc': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()), 'results': results}
    (OUT / 'results.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report, indent=2))
    raise SystemExit(1 if any(x['status'] == 'Failed' for x in results) else 0)
