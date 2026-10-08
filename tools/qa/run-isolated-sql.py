"""Run synthetic DAS SQL checks in a NEW owned Docker instance; never accepts an existing DB.

Requires local Docker Desktop/Linux, .NET 10 and the pinned official SQL image.
Raw logs/TRX/synthetic backups stay in a fresh .artifacts/qa directory, outside Git.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import signal
import socket
import subprocess
import time

ROOT = Path(__file__).resolve().parents[2]
LABEL = 'das.qa.sql.owner'
IMAGE = 'mcr.microsoft.com/mssql/server@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090'
COMPONENTS = ('auth', 'document', 'files', 'notification', 'partner')
spec = importlib.util.spec_from_file_location('das_sql_core', Path(__file__).with_name('run-core-ci.py'))
core = importlib.util.module_from_spec(spec); spec.loader.exec_module(core)
fresh_output = core.fresh_output
trx_counts = core.trx_counts
spec = importlib.util.spec_from_file_location('das_sql_bundle', Path(__file__).with_name('verify-restore-bundle.py'))
bundle_verifier = importlib.util.module_from_spec(spec); spec.loader.exec_module(bundle_verifier)


def child_environment(base, output):
    # No inherited connection strings, authority/SMTP credentials, injected runtimes or remote Docker endpoint.
    allowed = {'PATH', 'SYSTEMROOT', 'SYSTEMDRIVE', 'WINDIR', 'TEMP', 'TMP', 'HOME', 'USERPROFILE',
               'LOCALAPPDATA', 'APPDATA', 'PROGRAMFILES', 'PROGRAMFILES(X86)', 'PROGRAMDATA', 'PATHEXT'}
    env = {k: v for k, v in base.items() if k.upper() in allowed}
    env = core.ci_environment(env, shutil.which('node') or os.sys.executable)
    env.update({'NUGET_PACKAGES': str(output / 'nuget-packages'), 'DOTNET_CLI_HOME': str(output / 'dotnet-home'),
                'DOTNET_PROCESSOR_COUNT': '2', 'MSBUILDDISABLENODEREUSE': '1'})
    return env


def require_owner(data, token, kind, expected_id=None):
    if not re.fullmatch('[a-f0-9]{24}', token): raise ValueError('Invalid ownership token')
    prefix = 'das-sql-qa-' if kind == 'container' else 'das-sql-net-'
    labels = data.get('Config', {}).get('Labels', {}) if kind == 'container' else data.get('Labels', {})
    if (not re.fullmatch('[a-f0-9]{64}', data.get('Id', '')) or
        data.get('Name', '').lstrip('/') != prefix + token or labels.get(LABEL) != token or
        expected_id is not None and data['Id'] != expected_id):
        raise ValueError('Resource ownership mismatch; refusing mutation')


def loopback_port(data):
    ports = data.get('NetworkSettings', {}).get('Ports', {})
    bindings = ports.get('1433/tcp')
    if set(ports) != {'1433/tcp'} or not isinstance(bindings, list) or len(bindings) != 1:
        raise ValueError('Unexpected SQL port bindings: ' + json.dumps(ports))
    binding = bindings[0]
    value = binding.get('HostPort', '')
    if binding.get('HostIp') != '127.0.0.1' or not re.fullmatch('[0-9]{1,5}', value) or not 1 <= int(value) <= 65535:
        raise ValueError('Only a generated loopback SQL port is allowed')
    return int(value)


def connection(port, password):
    if not isinstance(port, int) or not 1 <= port <= 65535 or not re.fullmatch('[A-Za-z0-9_!-]{8,100}', password):
        raise ValueError('Invalid generated SQL endpoint')
    return f'Server=127.0.0.1,{port};Database=master;User ID=sa;Password={password};Encrypt=True;TrustServerCertificate=True;Connect Timeout=15'


def suites(profile, output, service=None):
    if service is not None and profile != 'core': raise ValueError('Service selection is limited to the core profile')
    result = []
    if profile in ('core', 'all'):
        for project in ('DocumentService', 'FileService', 'PartnerService', 'AuthService', 'NotificationService', 'EmailWorkerService'):
            if service is not None and project != service: continue
            result.append({'name': 'core-' + project, 'project': project, 'environment': {},
                           'filter': 'FullyQualifiedName~SqlTests&FullyQualifiedName!~SyntheticLoadSqlTests&FullyQualifiedName!~EmailRestoreSqlTests'})
    if profile in ('load', 'all'):
        result.append({'name': 'load', 'project': 'DocumentService', 'filter': 'FullyQualifiedName~SyntheticLoadSqlTests',
                       'environment': {'DAS_SYNTHETIC_LOAD': 'enabled', 'DAS_LOAD_OUTPUT': str(output / 'load')}})
    if profile in ('restore', 'all'):
        result.append({'name': 'restore', 'project': 'RestoreIntegration', 'filter': 'FullyQualifiedName~SqlRestoreDrillTests',
                       'environment': {'DAS_RESTORE_DRILL': 'synthetic', 'DAS_RESTORE_OUTPUT': str(output / 'restore')}})
        result.append({'name': 'restore-email', 'project': 'EmailWorkerService', 'filter': 'FullyQualifiedName~EmailRestoreSqlTests',
                       'environment': {'DAS_RESTORE_DRILL': 'synthetic', 'DAS_RESTORE_OUTPUT': str(output / 'restore-email')}})
    if not result: raise ValueError('Unknown SQL profile or service')
    return result


def backup_evidence(directory):
    result = {}
    for component in COMPONENTS:
        p = directory / (component + '.bak')
        if not p.is_file() or core.linked(p) or p.stat().st_size == 0: raise ValueError('Missing real synthetic backup evidence')
        digest = hashlib.sha256()
        with p.open('rb') as stream:
            for block in iter(lambda: stream.read(1024 * 1024), b''): digest.update(block)
        result[component] = {'bytes': p.stat().st_size, 'sha256': digest.hexdigest()}
    return result


def make_restore_bundle(output, report):
    """Keep the fixture's real cut identifier and gate the copied backups/PDF with the existing verifier."""
    hashes = report.get('beforeDataSha256', {})
    if (report.get('passed') is not True or report.get('synthetic') is not True or report.get('productionReady') is not False or
        report.get('allWorkersStarted') is not False or type(report.get('noSmtpResendCalls')) is not int or report['noSmtpResendCalls'] != 0 or
        type(report.get('backupCount')) is not int or report['backupCount'] != 5 or report.get('profile') != 'core-five-stores' or
        report.get('components') != list(COMPONENTS) or not isinstance(hashes, dict) or set(hashes) != set(COMPONENTS) or
        any(not isinstance(v, str) or not re.fullmatch('[a-f0-9]{64}', v) for v in hashes.values()) or hashes != report.get('afterDataSha256') or
        not bundle_verifier.valid_cut(report.get('cutId'))):
        raise ValueError('SQL roundtrip evidence is incomplete or unsuccessful')
    bundle = output / 'bundle'; records = []; backups = backup_evidence(bundle / 'backups'); cut = report['cutId']
    for name, data in backups.items():
        records.append({'component': name, 'path': 'backups/'+name+'.bak', 'cutId': cut, 'sizeBytes': data['bytes'], 'sha256': data['sha256']})
    pdfs = list((bundle / 'storage').glob('*.pdf'))
    if len(pdfs) != 1 or core.linked(pdfs[0]) or type(report.get('pdfBytes')) is not int:
        raise ValueError('Expected one fixture PDF')
    pdf = pdfs[0]; sha = bundle_verifier.digest(pdf)
    if pdf.stat().st_size != report['pdfBytes'] or sha != report.get('pdfSha256'): raise ValueError('Restored PDF evidence mismatch')
    records.append({'component': 'pdf', 'path': pdf.relative_to(bundle).as_posix(), 'cutId': cut, 'sizeBytes': pdf.stat().st_size, 'sha256': sha})
    manifest = {'version': 2, 'purpose': 'synthetic-restore-drill', 'profile': 'core-five-stores', 'cutId': cut,
                'workers': {key: False for key in sorted(bundle_verifier.WORKERS)}, 'artifacts': records}
    result = bundle_verifier.verify(manifest, bundle)
    if not result['integrityPassed']: raise ValueError('Restore bundle integrity gate failed')
    with (bundle / 'manifest.json').open('x', encoding='utf-8') as stream: stream.write(json.dumps(manifest, indent=2)+'\n')
    with (output / 'integrity.json').open('x', encoding='utf-8') as stream: stream.write(json.dumps(result, indent=2)+'\n')
    return result


def email_restore_evidence(directory, report, expected_backup_sha256):
    """Validate the independent Email Worker cut and the copy against its container-side digest."""
    flags = {'EmailIntake__WorkerEnabled', 'EmailIntake__ManualScanEnabled', 'Delivery__WorkerEnabled', 'Reminders__Enabled'}
    if not isinstance(report, dict): raise ValueError('Missing Email Worker restore report')
    hashes = report.get('beforeDataSha256'); rows = report.get('rowCounts'); workers = report.get('workerFlags')
    if (report.get('passed') is not True or report.get('synthetic') is not True or report.get('productionReady') is not False or
        report.get('profile') != 'email-worker-store' or report.get('components') != ['emailworker'] or
        type(report.get('backupCount')) is not int or report['backupCount'] != 1 or report.get('allWorkersStarted') is not False or
        not bundle_verifier.valid_cut(report.get('cutId')) or not isinstance(hashes, dict) or set(hashes) != {'emailworker'} or
        not isinstance(hashes['emailworker'], str) or not re.fullmatch('[a-f0-9]{64}', hashes['emailworker']) or
        hashes != report.get('afterDataSha256') or not isinstance(workers, dict) or set(workers) != flags or
        any(v is not False for v in workers.values()) or not isinstance(rows, dict) or
        set(rows) != {'settings', 'scanLogs', 'scanItems', 'migrations'} or any(type(v) is not int for v in rows.values()) or
        rows['settings'] != 1 or rows['scanLogs'] != 1 or rows['scanItems'] != 5 or rows['migrations'] < 1 or
        not isinstance(expected_backup_sha256, str) or not re.fullmatch('[a-f0-9]{64}', expected_backup_sha256)):
        raise ValueError('Email Worker restore evidence is incomplete or unsuccessful')
    for p in (directory, *directory.parents):
        if core.linked(p): raise ValueError('Linked Email Worker backup directory')
    backup = directory / 'emailworker.bak'
    if not backup.is_file() or core.linked(backup) or backup.stat().st_size == 0:
        raise ValueError('Missing real synthetic Email Worker backup')
    digest = bundle_verifier.digest(backup)
    if digest != expected_backup_sha256: raise ValueError('Email Worker backup copy checksum mismatch')
    return {'profile': 'email-worker-store', 'cutId': report['cutId'], 'integrityPassed': True, 'artifactCount': 1,
            'productionReady': False, 'allWorkersStarted': False, 'workerFlags': workers,
            'beforeDataSha256': hashes, 'afterDataSha256': report['afterDataSha256'], 'rowCounts': rows,
            'backup': {'bytes': backup.stat().st_size, 'sha256': digest}}


def process(command, env, cwd=None, timeout=1800):
    options = {'creationflags': subprocess.CREATE_NEW_PROCESS_GROUP} if os.name == 'nt' else {'start_new_session': True}
    p = subprocess.Popen(command, cwd=cwd, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, **options)
    try:
        raw, _ = p.communicate(timeout=timeout)
    except BaseException:
        # Terminate this process tree, never a name-based system-wide dotnet kill.
        if os.name == 'nt':
            subprocess.run(['taskkill', '/PID', str(p.pid), '/T', '/F'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=30)
        else:
            try: os.killpg(p.pid, signal.SIGKILL)
            except ProcessLookupError: pass
        p.communicate(timeout=30)
        raise
    return p.returncode, raw.decode('utf-8', errors='replace')


class Docker:
    def __init__(self, executable, context, env, password):
        self.executable, self.context, self.env, self.password = executable, context, env, password

    def invoke(self, args, timeout=60):
        return process([self.executable, '--context', self.context, *args], self.env, timeout=timeout)

    def call(self, args, timeout=60):
        code, text = self.invoke(args, timeout)
        if code: raise RuntimeError('Docker operation failed: ' + text.replace(self.password, '[REDACTED]')[:1000])
        return text.strip()

    def inspect(self, kind, name):
        code, text = self.invoke([kind, 'inspect', name])
        if code:
            # Only an explicit not-found response means absent. Daemon/permission/timeout errors fail closed.
            if re.search(r'(No such (?:container|network|object)|network .* not found)', text, re.IGNORECASE): return None
            raise RuntimeError('Cannot establish Docker resource state')
        data = json.loads(text)
        if not isinstance(data, list) or len(data) != 1: raise ValueError('Invalid Docker inspect response')
        return data[0]

    def cleanup(self, token):
        name = 'das-sql-qa-' + token
        item = self.inspect('container', name)
        if item is not None:
            require_owner(item, token, 'container')
            if any(m.get('Type') != 'volume' for m in item.get('Mounts', [])):
                raise ValueError('Unexpected mount; refusing automatic cleanup')
            self.call(['rm', '--force', '--volumes', item['Id']])
            if self.inspect('container', name) is not None: raise RuntimeError('Container deletion not confirmed')
        name = 'das-sql-net-' + token
        item = self.inspect('network', name)
        if item is not None:
            require_owner(item, token, 'network')
            if item.get('Containers'): raise ValueError('Network still has attached resources')
            self.call(['network', 'rm', item['Id']])
            if self.inspect('network', name) is not None: raise RuntimeError('Network deletion not confirmed')


def source_hashes(root):
    result = {}
    for folder in ('backend', 'database/migrations', 'workflows', 'tools/qa'):
        for p in sorted((root / folder).rglob('*')):
            if not p.is_file() or any(x in ('bin', 'obj', '__pycache__', '.artifacts', 'uploads') for x in p.relative_to(root).parts): continue
            if p.name.startswith('.env') and not p.name.endswith('.example') or p.suffix.lower() in ('.log', '.db', '.bak', '.pyc', '.pem', '.pfx', '.key'): continue
            if any(core.linked(x) for x in (p, *p.parents)): raise ValueError('Linked source is not allowed')
            result[p.relative_to(root).as_posix()] = hashlib.sha256(p.read_bytes()).hexdigest()
    return result


def run(args, root=ROOT):
    selected = getattr(args, 'service', None)
    selected_suites = suites(args.profile, root / args.output, selected)
    output = fresh_output(root, args.output); output.mkdir(parents=True)
    env = child_environment(os.environ, output)
    password = 'QA_' + secrets.token_hex(24) + '!a9'
    token = secrets.token_hex(12)
    d = None; created = False; cleanup = False; steps = []; evidence = {}; errors = []
    source = source_hashes(root)
    (output / 'source-manifest.json').write_text(json.dumps(source, indent=2) + '\n', encoding='utf-8')
    def execute(name, command, environment, timeout=1800):
        started = time.monotonic(); code = None; error = None; text = ''
        try: code, text = process(command, environment, cwd=root, timeout=timeout)
        except Exception as failure: error = type(failure).__name__
        (output / (name + '.log')).write_text(text.replace(password, '[REDACTED]') + ('\n' + error if error else ''), encoding='utf-8')
        step = {'name': name, 'exitCode': code, 'passed': code == 0 and not error, 'error': error, 'seconds': round(time.monotonic()-started, 3)}
        steps.append(step); print(name + ': ' + ('passed' if step['passed'] else 'FAILED'), flush=True)
        if not step['passed']: raise RuntimeError(name + ' failed; see redacted log')
    try:
        # Explicit selected LOCAL context; do not honor an inherited DOCKER_HOST/context override.
        code, text = process([args.docker, 'context', 'show'], env, timeout=30)
        context = text.strip()
        if code or not re.fullmatch('[A-Za-z0-9_.-]+', context): raise ValueError('Cannot establish local Docker context')
        code, text = process([args.docker, 'context', 'inspect', context], env, timeout=30)
        endpoint = json.loads(text)[0]['Endpoints']['docker']['Host'] if code == 0 else ''
        if not endpoint.startswith(('npipe://', 'unix://')): raise ValueError('Only local Docker socket contexts are allowed')
        d = Docker(args.docker, context, {**env, 'MSSQL_SA_PASSWORD': password, 'SQLCMDPASSWORD': password}, password)
        image = json.loads(d.call(['image', 'inspect', IMAGE]))[0]
        if IMAGE not in image.get('RepoDigests', []) or image.get('Config', {}).get('User') != 'mssql':
            raise ValueError('Pinned official nonroot SQL image is required; runner never pulls')
        name = 'das-sql-qa-' + token; network = 'das-sql-net-' + token
        # Record intent before each create; cleanup also resolves a timed-out create by exact name + label.
        created = True
        network_id = d.call(['network', 'create', '--driver', 'bridge', '--label', LABEL+'='+token, network])
        require_owner(d.inspect('network', network), token, 'network', network_id)
        ident = d.call(['create', '--name', name, '--label', LABEL+'='+token, '--network', network,
                        '--publish', '127.0.0.1::1433', '--memory', '3g', '--cpus', '2', '--pids-limit', '512',
                        '--security-opt', 'no-new-privileges:true', '--cap-drop', 'ALL', '--cap-add', 'NET_BIND_SERVICE',
                        '--env', 'ACCEPT_EULA=Y', '--env', 'MSSQL_PID=Developer', '--env', 'MSSQL_MEMORY_LIMIT_MB=2048',
                        '--env', 'MSSQL_SA_PASSWORD', IMAGE])
        item = d.inspect('container', name); require_owner(item, token, 'container', ident)
        if item.get('Mounts'): raise ValueError('SQL QA requires no host or persistent mounts')
        d.call(['start', ident])
        port = loopback_port(d.inspect('container', ident))
        evidence['sqlImage'] = image['Id']; evidence['loopbackOnly'] = True; evidence['internalNetwork'] = False
        evidence['networkScope'] = 'owned bridge; loopback publish; outbound network is not claimed disabled'
        deadline = time.monotonic() + 180
        while True:
            status, _ = d.invoke(['exec', '--env', 'SQLCMDPASSWORD', ident, '/opt/mssql-tools18/bin/sqlcmd',
                                  '-S', 'localhost', '-U', 'sa', '-C', '-b', '-Q', 'SET NOCOUNT ON; SELECT 1'], timeout=20)
            reachable = False
            if status == 0:
                try:
                    with socket.create_connection(('127.0.0.1', port), timeout=2): reachable = True
                except OSError: pass
            if status == 0 and reachable: break
            if time.monotonic() >= deadline: raise RuntimeError('Owned SQL readiness failed; no network fallback')
            time.sleep(2)
        # Directory belongs to nonroot mssql inside the owned ephemeral instance.
        d.call(['exec', ident, 'mkdir', '-p', '/var/opt/mssql/data/restore-qa'])
        (output / 'load').mkdir(); (output / 'restore/bundle/storage').mkdir(parents=True); (output / 'restore-email').mkdir()
        config = output / 'NuGet.Config'
        config.write_text('<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>\n', encoding='utf-8')
        settings = output / 'qa.runsettings'
        settings.write_text('<RunSettings><RunConfiguration><MaxCpuCount>1</MaxCpuCount></RunConfiguration><xUnit><MaxParallelThreads>2</MaxParallelThreads><ParallelizeTestCollections>false</ParallelizeTestCollections></xUnit></RunSettings>\n', encoding='utf-8')
        restored = set()
        for suite in selected_suites:
            project = suite['project']; relative = Path('backend/tests') / (project+'.Tests') / (project+'.Tests.csproj')
            if project not in restored:
                execute('restore-'+project, [args.dotnet, 'restore', str(relative), '--artifacts-path', str(output / 'build'), '--locked-mode', '--configfile', str(config), '--verbosity', 'minimal'], env)
                restored.add(project)
            results = output / 'trx' / suite['name']; results.mkdir(parents=True)
            test_env = {**env, 'DAS_TEST_SQL_CONNECTION': connection(port, password), **suite['environment']}
            execute('test-'+suite['name'], [args.dotnet, 'test', str(relative), '--artifacts-path', str(output / 'build'), '--no-restore', '--configuration', 'Release',
                    '--settings', str(settings), '--filter', suite['filter'], '--logger', 'trx;LogFileName=tests.trx',
                    '--results-directory', str(results), '--verbosity', 'minimal'], test_env)
            evidence[suite['name']] = trx_counts(results / 'tests.trx')
            if suite['name'] == 'load':
                evidence['loadReport'] = json.loads((output / 'load/load.json').read_text(encoding='utf-8'))
            if suite['name'] == 'restore':
                evidence['restoreReport'] = json.loads((output / 'restore/roundtrip.json').read_text(encoding='utf-8'))
                target = output / 'restore/bundle/backups'; target.mkdir()
                for component in COMPONENTS:
                    d.call(['cp', ident+':/var/opt/mssql/data/restore-qa/'+component+'.bak', str(target / (component+'.bak'))])
                evidence['backups'] = backup_evidence(target)
                evidence['bundleIntegrity'] = make_restore_bundle(output / 'restore', evidence['restoreReport'])
            if suite['name'] == 'restore-email':
                target = output / 'restore-email'
                email_report = json.loads((target / 'roundtrip.json').read_text(encoding='utf-8'))
                source_backup = '/var/opt/mssql/data/restore-qa/emailworker.bak'
                container_sha = d.call(['exec', ident, 'sha256sum', source_backup]).split()[0]
                d.call(['cp', ident+':'+source_backup, str(target / 'emailworker.bak')])
                evidence['emailRestore'] = email_restore_evidence(target, email_report, container_sha)
                with (target / 'integrity.json').open('x', encoding='utf-8') as stream:
                    stream.write(json.dumps(evidence['emailRestore'], indent=2)+'\n')
    except (Exception, KeyboardInterrupt) as failure:
        errors.append(type(failure).__name__ + ': ' + str(failure).replace(password, '[REDACTED]'))
        print('SQL QA failed; preserving evidence', flush=True)
    finally:
        if d is not None and created:
            try:
                item = d.inspect('container', 'das-sql-qa-'+token)
                if item is not None:
                    require_owner(item, token, 'container')
                    (output / 'sql-server.log').write_text(d.call(['logs', item['Id']]).replace(password, '[REDACTED]'), encoding='utf-8')
            except Exception as failure: errors.append('SQL log capture: '+type(failure).__name__)
            try: d.cleanup(token); cleanup = True
            except Exception as failure: errors.append('Cleanup not confirmed: '+type(failure).__name__)
        try:
            after = source_hashes(root)
            changed = [p for p in sorted(set(source) | set(after)) if source.get(p) != after.get(p)]
        except Exception as failure:
            changed = ['source-inventory-unavailable']; errors.append('Source inventory: '+type(failure).__name__)
        if changed: errors.append('Source integrity failed')
        # TRX may embed exception output independently of the console log. Never retain the generated credential.
        for p in output.rglob('*'):
            if p.is_file() and p.suffix in ('.trx', '.log', '.json') and not any(part in ('nuget-packages', 'dotnet-home') for part in p.relative_to(output).parts):
                content = p.read_text(encoding='utf-8')
                if password in content: p.write_text(content.replace(password, '[REDACTED]'), encoding='utf-8')
        report = {'profile': args.profile, 'selectedService': selected, 'passed': bool(steps) and all(s['passed'] for s in steps) and not errors and cleanup,
                  'synthetic': True, 'productionReady': False, 'workersEnabled': False, 'capturedAtUtc': datetime.now(timezone.utc).isoformat(),
                  'cleanupConfirmed': cleanup, 'ownerToken': token, 'errors': errors, 'steps': steps, 'testEvidence': evidence, 'sourceChanged': changed,
                  'scope': 'owned synthetic SQL only; no customer migration, EAP/OCR, transport, HTTP SLA or UAT'}
        (output / 'summary.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({'passed': report['passed'], 'profile': args.profile, 'cleanupConfirmed': cleanup, 'productionReady': False}), flush=True)
    return 0 if report['passed'] else 1


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--profile', required=True, choices=('core', 'load', 'restore', 'all'))
    p.add_argument('--service', choices=('DocumentService', 'FileService', 'PartnerService', 'AuthService', 'NotificationService', 'EmailWorkerService'), help='Optional single service, core profile only')
    p.add_argument('--output', required=True, help='Fresh directory below .artifacts/qa')
    p.add_argument('--dotnet', default=shutil.which('dotnet') or 'dotnet')
    p.add_argument('--docker', default=shutil.which('docker') or 'docker')
    return run(p.parse_args())


if __name__ == '__main__': raise SystemExit(main())
