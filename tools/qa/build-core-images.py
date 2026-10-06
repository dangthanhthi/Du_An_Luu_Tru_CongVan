"""Build local prepared Linux core images; never push or start application hosts.

Small source-only contexts, locked NuGet graphs, official base digests, nonroot
runtime defaults. Runtime smoke overrides entrypoint with dotnet --list-runtimes.
Not deployment, startup/readiness acceptance or a vulnerability scan.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import uuid
from datetime import datetime, timezone

PROJECTS = {
    'auth': ('services/auth-service/AuthService.csproj', 'AuthService.dll'),
    'document': ('services/document-service/DocumentService.csproj', 'DocumentService.dll'),
    'files': ('services/files-service/FileService.API.csproj', 'FileService.API.dll'),
    'notification': ('services/notification-service/NotificationService.csproj', 'NotificationService.dll'),
    'partner': ('services/partner-service/PartnerService.csproj', 'PartnerService.dll'),
    'gateway': ('gateway/Gateway.csproj', 'Gateway.dll'),
}
DISABLED = {
    'Database__Initialize': 'false', 'Database__InitializeOnStartup': 'false',
    'Database__SeedDemoUsers': 'false', 'Database__SeedExamples': 'false',
    'Reminders__Enabled': 'false', 'Notifications__WorkerEnabled': 'false',
    'Delivery__WorkerEnabled': 'false', 'Smtp__DeliveryEnabled': 'false',
    'PdfProtocol__MaintenanceEnabled': 'false',
}


def linked(path):
    return path.is_symlink() or getattr(path, 'is_junction', lambda: False)()


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def export_context(source, target, service):
    source, target = Path(source).absolute(), Path(target).absolute()
    if service not in PROJECTS or target.exists(): raise ValueError('Unknown service or occupied context')
    if any(linked(p) for p in (source, target, *source.parents, *target.parents)):
        raise ValueError('Linked context boundary')
    project, _ = PROJECTS[service]
    scopes = [Path(project).parent]
    if service in ('document', 'files'): scopes.append(Path('shared/PdfProtocol'))
    required = ['Directory.Build.props']
    for scope in scopes:
        required.extend([str(scope / 'packages.lock.json')])
    required.append(project)
    if any(not (source / name).is_file() for name in required): raise ValueError('Missing project, lock or root props')
    paths = [source / 'Directory.Build.props']
    for scope in scopes:
        for folder, dirs, files in os.walk(source / scope, followlinks=False):
            # Never traverse generated/data trees or linked source directories.
            if linked(Path(folder)): raise ValueError('Linked source directory')
            kept = []
            for name in dirs:
                if name in {'bin', 'obj', 'Uploads', 'uploads', 'storage', 'data', 'Properties', '.git'}: continue
                if linked(Path(folder) / name): raise ValueError('Linked source directory')
                kept.append(name)
            dirs[:] = kept
            for name in files:
                path = Path(folder) / name
                if path.suffix in ('.cs', '.csproj', '.props') or name == 'packages.lock.json' or (service == 'gateway' and path == source / 'gateway/ocelot.json'):
                    paths.append(path)
    if service == 'gateway' and source / 'gateway/ocelot.json' not in paths:
        raise ValueError('Gateway route config is required')
    records = {}
    for path in paths:
        if any(linked(p) for p in (path, *path.parents)) or not path.resolve().is_relative_to(source.resolve()):
            raise ValueError('Linked or escaped source')
        records[path.relative_to(source).as_posix()] = {'sha256': sha(path), 'bytes': path.stat().st_size}
    target.mkdir(parents=True)
    for name, record in records.items():
        dest = target / name; dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source / name, dest)
        if sha(dest) != record['sha256']: raise ValueError('Source changed while exporting')
    digest = hashlib.sha256(json.dumps(records, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
    return {'service': service, 'sourceSha256': digest, 'files': records}


def official_digest(refs, kind):
    prefix = 'mcr.microsoft.com/dotnet/' + kind + '@sha256:'
    matches = [r for r in refs if isinstance(r, str) and re.fullmatch(re.escape(prefix) + '[0-9a-f]{64}', r)]
    if len(matches) != 1: raise ValueError('Expected one official immutable base digest')
    return matches[0]


def dockerfile(service, sdk, runtime, source_digest):
    project, assembly = PROJECTS[service]
    flags = ' \\\n    '.join(k + '=' + v for k, v in DISABLED.items())
    return f'''FROM {sdk} AS build
WORKDIR /src
COPY . .
RUN dotnet restore {project} --locked-mode --source https://api.nuget.org/v3/index.json
RUN dotnet publish {project} --configuration Release --no-restore -m:1 -o /publish

FROM {runtime}
WORKDIR /app
COPY --from=build /publish/ .
RUN mkdir -p /app/storage && chown 1654:1654 /app/storage
ENV ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_HTTP_PORTS=8080 \\\n    {flags}
LABEL das.prepared="true" das.service="{service}" das.source-sha256="{source_digest}"
USER 1654
EXPOSE 8080
ENTRYPOINT ["dotnet", "{assembly}"]
'''


def run(args, log, timeout=1800):
    with log.open('x', encoding='utf-8') as stream:
        try:
            result = subprocess.run(args, stdout=stream, stderr=subprocess.STDOUT, timeout=timeout)
        except subprocess.TimeoutExpired:
            stream.write('\nCOMMAND_TIMEOUT\n'); raise
    if result.returncode: raise RuntimeError(f'Command failed ({result.returncode}); see {log.name}')


def inspect(ref):
    result = subprocess.run(['docker', 'image', 'inspect', ref], capture_output=True, text=True, timeout=30, check=True)
    value = json.loads(result.stdout)
    if len(value) != 1: raise ValueError('Ambiguous image inspection')
    return value[0]


def fresh_output(root, relative):
    if not re.fullmatch(r'\.artifacts/qa/[A-Za-z0-9_-]+(?:/[A-Za-z0-9_-]+)?', relative):
        raise ValueError('Use a simple fresh QA directory')
    output = root / relative
    if output.exists() or any(linked(p) for p in (output, *output.parents)):
        raise ValueError('Occupied or linked QA output')
    if not output.resolve().is_relative_to((root / '.artifacts/qa').resolve()): raise ValueError('Output boundary')
    output.mkdir(parents=True)
    return output


def smoke(image_id, service, output, run_id):
    name = 'das-image-smoke-qa-' + run_id + '-' + service
    try:
        run(['docker', 'run', '--rm', '--name', name, '--network', 'none', '--read-only',
             '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges', '--entrypoint', 'dotnet',
             image_id, '--list-runtimes'], output / (service + '-runtime-smoke.log'), 60)
        text = (output / (service + '-runtime-smoke.log')).read_text(encoding='utf-8')
        if 'Microsoft.AspNetCore.App 10.0.' not in text: raise ValueError('Expected ASP.NET10 runtime')
        native = service != 'gateway'
        command = 'test ! -f /app/appsettings.json && test ! -f /app/appsettings.Development.json'
        if native: command += ' && test -s /app/runtimes/linux-x64/native/libe_sqlite3.so'
        run(['docker', 'run', '--rm', '--name', name, '--network', 'none', '--read-only',
             '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges', '--entrypoint', 'sh',
             image_id, '-c', command], output / (service + '-asset-smoke.log'), 60)
        return {'runtimeSmokePassed': True, 'appsettingsAbsent': True, 'linuxSqliteAssetPresent': native,
                'nativeSqliteFunctionExecuted': False, 'applicationStarted': False}
    finally:
        # --rm normally removes it; explicitly remove only this owned name after a timed-out client.
        result = subprocess.run(['docker', 'ps', '-a', '--filter', 'name=^/' + name + '$', '--format', '{{.Names}}'], capture_output=True, text=True, timeout=30, check=True)
        if result.stdout.strip():
            subprocess.run(['docker', 'rm', '-f', name], capture_output=True, timeout=30, check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory', required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    output = fresh_output(root, args.directory)
    result = {'passed': False, 'productionReady': False, 'deployed': False, 'pushed': False,
              'applicationHostsStarted': False, 'runnerSha256': sha(Path(__file__)),
              'capturedAtUtc': datetime.now(timezone.utc).isoformat(), 'images': {}}
    run_id = uuid.uuid4().hex[:12]
    try:
        bases = {}
        for kind in ('sdk', 'aspnet'):
            tag = 'mcr.microsoft.com/dotnet/' + kind + ':10.0'
            print('Pull official ' + kind, flush=True)
            run(['docker', 'pull', '--platform', 'linux/amd64', tag], output / ('pull-' + kind + '.log'))
            info = inspect(tag)
            if info['Os'] != 'linux' or info['Architecture'] != 'amd64': raise ValueError('Wrong base platform')
            bases[kind] = {'ref': official_digest(info.get('RepoDigests', []), kind), 'imageId': info['Id']}
        result['bases'] = bases
        source = root / 'Intern-DocumentAdministration-BE'
        for service in PROJECTS:
            print('Build ' + service, flush=True)
            context = output / ('context-' + service)
            manifest = export_context(source, context, service)
            (context / 'Dockerfile').write_text(dockerfile(service, bases['sdk']['ref'], bases['aspnet']['ref'], manifest['sourceSha256']), encoding='utf-8')
            manifest['dockerfileSha256'] = sha(context / 'Dockerfile')
            (output / (service + '-source-manifest.json')).write_text(json.dumps(manifest, indent=2), encoding='utf-8')
            tag = 'das-prepared-' + service + ':qa-' + run_id
            run(['docker', 'build', '--platform', 'linux/amd64', '--progress', 'plain', '--tag', tag, str(context)], output / (service + '-build.log'))
            info = inspect(tag); config = info['Config']
            if config['User'] != '1654' or config['Entrypoint'] != ['dotnet', PROJECTS[service][1]]:
                raise ValueError('Runtime UID or entrypoint mismatch')
            env = dict(e.split('=', 1) for e in config['Env'])
            if any(env.get(k) != v for k, v in DISABLED.items()): raise ValueError('Worker/init defaults mismatch')
            if info['Os'] != 'linux' or info['Architecture'] != 'amd64': raise ValueError('Wrong image platform')
            if config['Labels'].get('das.source-sha256') != manifest['sourceSha256']: raise ValueError('Source label mismatch')
            checked = smoke(info['Id'], service, output, run_id)
            if any(sha(source / n) != r['sha256'] for n, r in manifest['files'].items()): raise ValueError('Worktree changed during image build')
            result['images'][service] = {'tag': tag, 'imageId': info['Id'], 'sizeBytes': info['Size'],
                'sourceSha256': manifest['sourceSha256'], 'sourceFiles': len(manifest['files']),
                'entrypoint': config['Entrypoint'], 'uid': config['User'], 'disabledDefaults': DISABLED,
                'os': info['Os'], 'architecture': info['Architecture'], **checked}
            print(service + ': passed', flush=True)
        result['passed'] = len(result['images']) == len(PROJECTS)
    except Exception as exc:
        result['error'] = type(exc).__name__ + ': ' + str(exc)
    finally:
        (output / 'summary.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'passed': result['passed'], 'built': len(result['images']), 'productionReady': False}), flush=True)
    return 0 if result['passed'] else 1


if __name__ == '__main__': raise SystemExit(main())
