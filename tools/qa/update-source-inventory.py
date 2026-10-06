"""Refresh materialized source metadata; retain historical/reference rows explicitly."""
from pathlib import Path
import csv
import hashlib
import subprocess
import tempfile
import os

root = Path(__file__).resolve().parents[2]
inventory = root / "docs/superpowers/plans/2026-10-03-commercialization/SOURCE-INVENTORY.csv"
with inventory.open(encoding="utf-8-sig", newline="") as stream:
    reader = csv.DictReader(stream)
    fields = reader.fieldnames
    rows = list(reader)
by_path = {row["path"]: row for row in rows}
files = subprocess.check_output(
    ["git", "-c", "core.quotepath=false", "ls-files", "--cached", "--others", "--exclude-standard"],
    cwd=root, text=True, encoding="utf-8").splitlines()
text_suffixes = {".cs", ".csproj", ".slnx", ".props", ".ts", ".tsx", ".js", ".cjs", ".mjs", ".json", ".md", ".ps1", ".py", ".bat", ".yml", ".yaml", ".css", ".scss", ".csv"}
updated = added = 0
for name in sorted(set(files)):
    path = root / name
    if path == inventory or not path.is_file():
        continue
    if path.suffix not in text_suffixes and path.name not in {".gitignore", ".gitattributes", ".npmrc", "ocr-linux.Dockerfile"}:
        continue
    content = path.read_text(encoding="utf-8-sig")
    if name not in by_path:
        if name.startswith("tests/pending-ocr/"):
            category = "deferred-qa"
        elif name.startswith("Intern-DocumentAdministration-BE/tests/") or name.startswith("held-out-tests/") or name.startswith("scripts/qa/") or name.startswith("tests/frontend/") or name.startswith("tests/qa/"):
            category = "active-qa"
        elif name.startswith("Intern-DocumentAdministration-BE/services/"):
            category = "active-backend"
        elif name.startswith("docs/"):
            category = "execution-docs"
        elif name.startswith("src/") or name in {"package.json", "package-lock.json", "next-env.d.ts"}:
            category = "active-web"
        elif name.startswith("archive/"):
            category = "reference"
        else:
            category = "active-tooling"
        row = dict.fromkeys(fields, "")
        row.update(path=name, category=category, Owner="A")
        rows.append(row)
        by_path[name] = row
        added += 1
    row = by_path[name]
    row.update(lines=str(len(content.splitlines())), characters=str(len(content)),
               sha256_text=hashlib.sha256(content.encode("utf-8")).hexdigest())
    updated += 1
assert len(by_path) == len(rows), "Inventory paths must be unique"
# Keep the existing inventory intact if writing fails or is interrupted.
temporary = None
try:
    with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", newline="",
                                     dir=inventory.parent, prefix=".inventory-", suffix=".tmp", delete=False) as stream:
        temporary = Path(stream.name)
        writer = csv.DictWriter(stream, fields, quoting=csv.QUOTE_ALL, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary, inventory)
finally:
    if temporary is not None and temporary.exists():
        temporary.unlink()
print(f"inventory rows={len(rows)}, materialized text refreshed={updated}, added={added}")
