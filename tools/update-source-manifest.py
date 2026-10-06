"""Refresh current hashes after intentional source edits; retain original import provenance."""
from pathlib import Path
import hashlib,json
root=Path(__file__).resolve().parents[1];path=root/'docs/source-layout-manifest.json';data=json.loads(path.read_text())
for row in data['files']:
    p=root/row['destination']
    if not p.is_file():raise ValueError('Removed mapped source: '+row['destination']+';update its layout mapping explicitly')
    content=p.read_bytes();row['sha256']=hashlib.sha256(content).hexdigest();row['bytes']=len(content)
path.write_text(json.dumps(data,indent=2)+'\n');print('Current hashes updated; original import provenance retained')
