"""Create an isolated, minimal local Docker context for the G0 OCR spike."""
from pathlib import Path
import json
import hashlib
import shutil
import uuid

root = Path(__file__).resolve().parents[2]
context = root / ".artifacts" / "qa" / ("ocr-linux-context-" + uuid.uuid4().hex[:8])
context.mkdir(parents=True)
manifest = []
for folder in ("Intern-DocumentAdministration-BE/services/ai-ocr-service", "held-out-tests"):
    for source in (root / folder).rglob("*"):
        if not source.is_file():
            continue
        relative = source.relative_to(root)
        if any(part in {"bin", "obj", ".git", ".agents", "PaxHeader", "node_modules"} for part in relative.parts):
            continue
        if source.name.startswith((".env", "appsettings", "._")) or source.suffix in {".db", ".log"}:
            continue
        if source.suffix not in {".cs", ".csproj", ".txt", ".pdf", ".json", ".pdmodel", ".pdiparams", ".info", ".traineddata"}:
            continue
        destination = context / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
        manifest.append({"path": relative.as_posix(), "sha256": hashlib.sha256(source.read_bytes()).hexdigest()})
shutil.copyfile(root / "scripts/qa/ocr-linux.Dockerfile", context / "Dockerfile")
(context / "context-manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
print(context)
