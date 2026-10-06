"""Check six SQL migration snapshots and export idempotent SQL; never apply SQL or connect to a database."""
import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('database_export_core', ROOT / 'tools/qa/run-core-ci.py')
core = importlib.util.module_from_spec(spec); spec.loader.exec_module(core)
fresh_output = core.fresh_output

STORES = [
    {'name': 'auth', 'folder': 'auth-service', 'assembly': 'AuthService', 'context': 'AuthDbContext', 'ef': '10.0.3', 'factoryName': 'Infrastructure/Persistence/AuthDbContextFactory.cs'},
    {'name': 'document', 'folder': 'document-service', 'assembly': 'DocumentService', 'context': 'DocumentDbContext', 'ef': '10.0.3', 'factoryName': 'Data/DocumentDbContextFactory.cs'},
    {'name': 'files', 'folder': 'files-service', 'assembly': 'FileService.API', 'context': 'FileDbContext', 'ef': '10.0.3', 'factoryName': 'Data/DesignTimeFileDbContextFactory.cs'},
    {'name': 'notification', 'folder': 'notification-service', 'assembly': 'NotificationService', 'context': 'NotificationDbContext', 'ef': '10.0.3', 'factoryName': 'Data/DesignTimeNotificationDbContextFactory.cs'},
    {'name': 'partner', 'folder': 'partner-service', 'assembly': 'PartnerService', 'context': 'PartnerDbContext', 'ef': '10.0.3', 'factoryName': 'Infrastructure/Persistence/PartnerDbContext.cs'},
    {'name': 'email', 'folder': 'email-worker-service', 'assembly': 'EmailWorkerService', 'context': 'EmailWorkerDbContext', 'ef': '9.0.0', 'factoryName': 'Data/DesignTimeEmailWorkerDbContextFactory.cs'},
]
for store in STORES:
    prefix = 'backend/services/' + store['folder'] + '/'
    store.update(project=prefix + store['assembly'] + '.csproj', factory=prefix + store['factoryName'])


def environment(base, output):
    allowed = {'PATH', 'SYSTEMROOT', 'SYSTEMDRIVE', 'WINDIR', 'TEMP', 'TMP', 'HOME', 'USERPROFILE',
               'LOCALAPPDATA', 'APPDATA', 'PROGRAMFILES', 'PROGRAMFILES(X86)', 'PROGRAMDATA', 'PATHEXT'}
    env = core.ci_environment({key: value for key, value in base.items() if key.upper() in allowed}, shutil.which('node') or os.sys.executable)
    env.update({'DOTNET_CLI_HOME': str(output / 'dotnet-home'), 'DOTNET_PROCESSOR_COUNT': '2', 'MSBUILDDISABLENODEREUSE': '1',
                'ASPNETCORE_ENVIRONMENT': 'Production', 'DOTNET_ENVIRONMENT': 'Production', 'Database__Initialize': 'false',
                'Database__InitializeOnStartup': 'false', 'Database__SeedDemoUsers': 'false', 'Database__SeedExamples': 'false'})
    return env


def schema_commands(ef, store, target):
    common = ['--project', str(ROOT / store['project']), '--context', store['context'], '--configuration', 'Debug', '--no-build', '--no-color']
    return [
        [str(ef), 'migrations', 'has-pending-model-changes', *common],
        [str(ef), 'migrations', 'list', '--no-connect', '--json', *common],
        [str(ef), 'migrations', 'script', '--idempotent', '--output', str(target), *common],
    ]


def source_hashes():
    paths = {ROOT / 'backend/Directory.Build.props'}
    for folder in ('backend/services', 'backend/shared', 'database/migrations', 'workflows/business'):
        for path in (ROOT / folder).rglob('*'):
            if path.is_file() and path.suffix in ('.cs', '.csproj', '.json') and not {'bin', 'obj'} & set(path.parts):
                if any(core.linked(p) for p in (path, *path.parents)): raise ValueError('Linked schema source is not allowed')
                paths.add(path)
    return {path.relative_to(ROOT).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted(paths)}


def run(args):
    output = fresh_output(ROOT, args.output); output.mkdir(parents=True)
    env = environment(os.environ, output)
    before = source_hashes(); steps = []; stores = []; errors = []
    (output / 'source-manifest.json').write_text(json.dumps(before, indent=2) + '\n', encoding='utf-8')
    config = output / 'NuGet.Config'
    config.write_text('<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>\n', encoding='utf-8')
    (output / 'sql').mkdir()

    def execute(name, command):
        result = subprocess.run(command, cwd=ROOT, env=env, capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=600)
        text = result.stdout + result.stderr
        (output / (name + '.log')).write_text(text, encoding='utf-8')
        steps.append({'name': name, 'exitCode': result.returncode})
        print(name + ': ' + ('passed' if result.returncode == 0 else 'FAILED'), flush=True)
        if result.returncode: raise RuntimeError(name + ' failed; inspect its local log')
        return result.stdout

    try:
        tools = {}
        for version in sorted({s['ef'] for s in STORES}):
            path = output / ('ef-' + version)
            execute('install-ef-' + version, [args.dotnet, 'tool', 'install', 'dotnet-ef', '--version', version, '--tool-path', str(path), '--configfile', str(config)])
            tools[version] = path / ('dotnet-ef.exe' if os.name == 'nt' else 'dotnet-ef')
        for store in STORES:
            name = store['name']; project = str(ROOT / store['project'])
            execute(name + '-restore', [args.dotnet, 'restore', project, '--locked-mode', '--configfile', str(config), '--verbosity', 'minimal'])
            execute(name + '-build', [args.dotnet, 'build', project, '--no-restore', '--configuration', 'Debug', '--disable-build-servers', '--verbosity', 'minimal'])
            target = output / 'sql' / (name + '.sql')
            commands = schema_commands(tools[store['ef']], store, target)
            execute(name + '-parity', commands[0])
            migrations = json.loads(execute(name + '-migrations', commands[1]))
            if not isinstance(migrations, list) or not migrations: raise ValueError(name + ' has no migrations')
            execute(name + '-script', commands[2])
            data = target.read_bytes()
            if not data or b'__EFMigrationsHistory' not in data or b'IF NOT EXISTS' not in data:
                raise ValueError(name + ' did not produce an idempotent migration script')
            stores.append({'store': name, 'context': store['context'], 'efToolVersion': store['ef'],
                           'migrationIds': [m['id'] for m in migrations], 'pendingModelChanges': False,
                           'script': 'sql/' + name + '.sql', 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()})
    except Exception as failure:
        errors.append(type(failure).__name__ + ': ' + str(failure))
    after = source_hashes()
    changed = sorted(key for key in set(before) | set(after) if before.get(key) != after.get(key))
    passed = len(stores) == len(STORES) and not errors and not changed
    report = {'createdAtUtc': datetime.now(timezone.utc).isoformat(), 'passed': passed, 'scope': 'offline SQL generation and model/snapshot parity only',
              'databaseConnected': False, 'sqlApplied': False, 'productionReady': False, 'stores': stores, 'steps': steps, 'errors': errors, 'sourceChanged': changed}
    (output / 'summary.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'passed': passed, 'storeCount': len(stores), 'sqlApplied': False, 'productionReady': False}), flush=True)
    return 0 if passed else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True, help='New directory below .artifacts/qa/')
    parser.add_argument('--dotnet', default=shutil.which('dotnet') or 'dotnet')
    return run(parser.parse_args())


if __name__ == '__main__': raise SystemExit(main())
