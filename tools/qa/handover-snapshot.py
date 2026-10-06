"""Local DAS handover snapshot. Verify is read-only except its own report.
No checkout reset, zip extraction, patch application, commit, push or deployment.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import zipfile
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[2]
DESKTOP = Path("C:/Users/MSIIIIII/Desktop/Dự án taskmanager")

def git(*args):
    return subprocess.run(["git", *args], cwd=ROOT, check=True, capture_output=True).stdout

def digest(data):
    return hashlib.sha256(data).hexdigest()

def now():
    return datetime.now(timezone.utc).isoformat()

def local(name):
    path = (ROOT / name).resolve()
    if not path.is_relative_to(ROOT):
        raise ValueError("Path outside worktree")
    return path

def split_paths(data):
    return [s.decode("utf-8") for s in data.split(b"\0") if s]

def file_record(path, root_kind, archive_name):
    data = path.read_bytes()
    return {"path": str(path), "kind": root_kind, "archiveName": archive_name,
            "sha256": digest(data), "bytes": len(data)}

def create(bundle):
    if not bundle.is_relative_to(ROOT / ".artifacts" / "handover"):
        raise ValueError("Snapshot must stay inside .artifacts/handover")
    ignored = subprocess.run(["git", "check-ignore", "--quiet", str(bundle)],
                             cwd=ROOT, capture_output=True)
    if ignored.returncode != 0:
        raise ValueError("Snapshot output must be Git-ignored")
    tracked = split_paths(git("diff", "--name-only", "--relative", "-z", "HEAD"))
    untracked = split_paths(git("ls-files", "--others", "--exclude-standard", "-z"))
    names = sorted(set(tracked + untracked))
    for name in names:
        parts = Path(name).parts
        if any(p.lower() in {".secrets", ".git"} for p in parts) or (
            Path(name).name.startswith(".env") and not Path(name).name.endswith(".example")
        ) or Path(name).suffix.lower() in {".pfx", ".pem", ".key"}:
            raise ValueError("Credential-like path in WIP; review before snapshot")
    head = git("rev-parse", "HEAD").decode().strip()
    branch = git("branch", "--show-current").decode().strip()
    status_hash = digest(git("status", "--porcelain=v1", "-z"))
    records = []
    for name in names:
        path = local(name)
        if not path.exists():
            records.append({"path": name, "state": "Deleted"})
        elif path.is_file():
            data = path.read_bytes()
            records.append({"path": name, "state": "Untracked" if name in untracked else "TrackedChanged",
                            "sha256": digest(data), "bytes": len(data)})
        else:
            raise ValueError("Non-file WIP entry; review before snapshot")
    refs = set()
    for base in ["docs/commercialization", "docs/superpowers/plans/2026-10-03-commercialization",
                 ".artifacts/qa/g1-persistence", ".artifacts/qa/g1-ui-red",
                 ".artifacts/qa/g3-numbering-20261004", ".artifacts/qa/g3-persistence-20261004",
                 ".artifacts/mentor-input/20261004-new", ".artifacts/qa/g3-editing-20261004",
                 ".artifacts/qa/g3-kind-details-20261004", ".artifacts/qa/g3-relations-20261004",
                 ".artifacts/qa/g3-pdf-upload-20261004", ".artifacts/qa/g3-current-pdf-20261005",
                 ".artifacts/qa/g3-http-ui-20261005", ".artifacts/qa/g3-external-entities-20261005", ".artifacts/qa/subsequent-20261005", ".artifacts/qa/task-workflow-20261005", ".artifacts/qa/reminder-fanout-20261005", ".artifacts/qa/restore-drill-20261005"]:
        refs.update(p for p in local(base).rglob("*") if p.is_file())
    for name in ["AGENTS.md", "docs/HANDOVER.md", "README.md", "package.json", "package-lock.json",
                 ".artifacts/qa/template-surface-20261006/check-pages.py",
                 ".artifacts/qa/linux-core-ci-20261006/browser-fixture.py",
                 ".artifacts/qa/license-notice-20261006/candidate-v1/texts.zip",
                 "next.config.ts", "next.config.mjs", "docker-compose.yml",
                 "Intern-DocumentAdministration-BE/AGENTS.md",
                 "Intern-DocumentAdministration-BE/ARCHITECTURE.md",
                 "Intern-DocumentAdministration-BE/API_CONTRACT.md"]:
        path = local(name)
        if path.is_file():
            refs.add(path)
    # Preserve reports/manifests, never the clean-source rehearsal's dependency/build trees.
    for report_root in ("release-ci-20261005", "security-remediation-20261005", "core-restore-20261005",
                        "core-images-20261005", "license-inventory-20261005", "runtime-load-20261005", "production-db-20261005", "cors-boundary-20261005", "linux-startup-20261005",
                        "linux-core-ci-20261005", "frontend-image-20261005", "template-route-boundary-20261005", "typecheck-scope-20261005", "template-data-boundary-20261005",
                        "linux-core-ci-20261006", "frontend-image-20261006", "user-menu-20261006", "template-surface-20261006", "license-notice-20261006", "session-concurrency-20261006"):
        for folder, directories, files in os.walk(local(".artifacts/qa/" + report_root)):
            directories[:] = [name for name in directories if not name.startswith(("clean-source", "context-"))
                               and name not in {"node_modules", "bin", "obj", ".next", "__pycache__"}]
            refs.update(Path(folder) / name for name in files
                        if Path(name).suffix.lower() in {".json", ".log", ".trx", ".md"})
    reference_records = [file_record(p, "WorktreeReference", p.relative_to(ROOT).as_posix())
                         for p in sorted(refs)]
    external = [
        ("external/Desktop-HANDOVER.md", DESKTOP / "HANDOVER.md"),
        ("external/DAS-Requirement-v2.docx", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS Requirement - v2 - Thương mại.docx"),
        ("external/User-Requirements-v5.docx", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "User Requirements v5.docx"),
        ("external/DAS-Questions-v1.docx", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS_V2_Cau_hoi v1.docx"),
        ("external/EAP-Meeting-20261004.pdf", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "Tóm_Tắt_Biên_Bản_Họp_Tích_Hợp_Hệ_Thống_Quản_Lý_Công_Văn_Và_Giao_Việc_Lên_Nền_Tảng_Tập_Trung_(EAP)_1.pdf"),
        ("external/DAS-Mentor-v1-update.md", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS_Cap_nhat_mentor_v1_20261004.md"),
        ("external/DAS-Relations-Lifecycle-checkpoint.md", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS_Checkpoint_lien_ket_huy_khoi_phuc_20261004.md"),
        ("external/DAS-Pdf-upload-checkpoint.md", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS_Checkpoint_tai_PDF_20261004.md"),
        ("external/DAS-Current-Pdf-checkpoint.md", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS_Checkpoint_PDF_hien_hanh_20261005.md"),
        ("external/DAS-Http-UI-checkpoint.md", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS_Checkpoint_API_UI_v2_20261005.md"),
        ("external/DAS-External-Entities-checkpoint.md", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS_Checkpoint_External_Entities_20261005.md"),
        ("external/DAS-Subsequent-checkpoint.md", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS_Checkpoint_Bao_cao_Nhac_han_TMS_20261005.md"),
        ("external/DAS-Document-Task-checkpoint.md", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS_Checkpoint_Tao_task_cong_van_20261005.md"),
        ("external/DAS-Reminder-Fanout-checkpoint.md", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS_Checkpoint_Nhac_han_hang_doi_20261005.md"),
        ("external/DAS-Restore-Drill-checkpoint.md", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS_Checkpoint_Sao_luu_Khoi_phuc_20261005.md"),
        ("external/DAS-Core-CI-checkpoint.md", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS_Checkpoint_CI_Release_20261005.md"),
        ("external/DAS-Dependency-checkpoint.md", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS_Checkpoint_Bao_mat_Dependency_20261005.md"),
        ("external/DAS-Five-store-restore-checkpoint.md", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS_Checkpoint_Khoi_phuc_5_kho_20261005.md"),
        ("external/DAS-Images-license-checkpoint.md", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS_Checkpoint_Image_License_20261005.md"),
        ("external/DAS-Runtime-load-checkpoint.md", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS_Checkpoint_Runtime_Tai_SQL_20261005.md"),
        ("external/DAS-Production-DB-checkpoint.md", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS_Checkpoint_DB_Production_20261005.md"),
        ("external/DAS-All-Stages-progress.md", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS_Tien_do_toan_bo_giai_doan_20261005.md"),
        ("external/DAS-CORS-checkpoint.md", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS_Checkpoint_CORS_Production_20261005.md"),
        ("external/DAS-Linux-startup-checkpoint.md", DESKTOP / "CanChinhSuaDeThuongMaiHoa" / "DAS_Checkpoint_Khoi_dong_Linux_20261005.md"),
        ("external/EAP-guide.pdf", Path("C:/Users/MSIIIIII/Downloads/eap-external-application-integration-guide.pdf")),
        ("external/Gemini-MCP-Sua-timeout.md", Path("C:/Users/MSIIIIII/Documents/Codex/2026-10-03/g/outputs/Gemini-MCP-Sua-timeout.md"))
    ]
    requirement_folder = DESKTOP / "CanChinhSuaDeThuongMaiHoa"
    for name, path in [
        ("external/DAS-Resume-20261006.md", requirement_folder / "DAS_Checkpoint_Tiep_tuc_20261006.md"),
        ("external/DAS-Template-boundary-20261006.md", requirement_folder / "DAS_Checkpoint_API_Menu_20261006.md"),
        ("external/DAS-Template-evidence-20261006.json", requirement_folder / "DAS_Evidence_API_Menu_20261006.json"),
        ("external/DAS-Launcher-checkpoint-20261006.md", requirement_folder / "DAS_Checkpoint_Dieu_huong_Trang_mau_20261006.md"),
        ("external/DAS-Launcher-evidence-20261006.json", requirement_folder / "DAS_Evidence_Dieu_huong_Trang_mau_20261006.json"),
        ("external/DAS-Resume-checkpoint.md", requirement_folder / "DAS_Checkpoint_Tiep_tuc_20261005.md"),
        ("external/DAS-Linux-CI-checkpoint.md", requirement_folder / "DAS_Checkpoint_CI_Linux_20261005.md"),
        ("external/DAS-Frontend-image-checkpoint.md", requirement_folder / "DAS_Checkpoint_Frontend_Linux_20261005.md"),
        ("external/DAS-Template-data-progress.md", requirement_folder / "DAS_Checkpoint_API_mau_Dang_thuc_hien_20261005.md"),
        ("external/DAS-Night-cache.md", requirement_folder / "DAS_Cache_tiep_tuc_ngay_mai_20261005.md"),
        ("external/DAS-Night-evidence.json", requirement_folder / "DAS_Evidence_nghi_cuoi_ngay_20261005.json"),
        ("external/DAS-Release-gates.md", requirement_folder / "DAS_Dieu_kien_phat_hanh_20261005.md"),
        ("external/DAS-Linux-CI-evidence.json", requirement_folder / "DAS_Evidence_CI_Linux_20261005.json"),
        ("external/DAS-Frontend-image-evidence.json", requirement_folder / "DAS_Evidence_Frontend_Linux_20261005.json"),
        ("external/DAS-Plan-comparison.md", requirement_folder / "DAS_Doi_chieu_ke_hoach_moi_20261005.md"),
        ("external/DAS-New-team-plan.docx", Path("C:/Users/MSIIIIII/Downloads/Ke_hoach_phan_chia_cong_viec_DAS.docx"))
    ]:
        if path.is_file(): external.append((name, path))
    for p in sorted(requirement_folder.rglob("*")):
        if p.is_file() and p.suffix.lower() in {".png", ".jpg", ".jpeg", ".gz"}:
            external.append(("external/requirements/" + p.relative_to(requirement_folder).as_posix(), p))
    mentor_report = DESKTOP / "Intern-DocumentAdministration-BE" / "das_v2_confirmation_report.html"
    if mentor_report.is_file():
        external.append(("external/das_v2_confirmation_report.html", mentor_report))
    reference_records.extend(file_record(p, "ExternalReference", name) for name, p in external)
    bundle.mkdir(parents=True, exist_ok=False)
    patch = git("diff", "--binary", "--full-index", "HEAD", "--")
    (bundle / "working-tree.patch").write_bytes(patch)
    with zipfile.ZipFile(bundle / "untracked-files.zip", "x", zipfile.ZIP_DEFLATED) as archive:
        for name in untracked:
            archive.write(local(name), name)
    with zipfile.ZipFile(bundle / "references-and-evidence.zip", "x", zipfile.ZIP_DEFLATED) as archive:
        for rec in reference_records:
            archive.write(rec["path"], rec["archiveName"])
    manifest = {
        "schemaVersion": 1, "createdAtUtc": now(), "worktree": str(ROOT),
        "branch": branch, "baseCommit": head, "statusSha256": status_hash,
        "scope": "All tracked HEAD deltas and every non-ignored untracked file in the specified worktree; selected references/evidence and external requirement inputs.",
        "notIncluded": "Full transcript/model cache, ignored dependency/build/runtime files, MCP RAM jobs, unrelated Desktop WIP and production data.",
        "patchSha256": digest(patch), "files": records, "references": reference_records,
        "artifacts": {name: digest((bundle / name).read_bytes()) for name in
                      ["working-tree.patch", "untracked-files.zip", "references-and-evidence.zip"]}
    }
    (bundle / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"bundle": str(bundle), "wipFiles": len(records),
                      "referenceFiles": len(reference_records), "baseCommit": head}, ensure_ascii=False))

def verify(bundle):
    manifest = json.loads((bundle / "manifest.json").read_text(encoding="utf-8"))
    errors = []
    if Path(manifest["worktree"]).resolve() != ROOT:
        errors.append("Wrong worktree")
    if git("rev-parse", "HEAD").decode().strip() != manifest["baseCommit"]:
        errors.append("HEAD changed")
    if git("branch", "--show-current").decode().strip() != manifest["branch"]:
        errors.append("Branch changed")
    if digest(git("status", "--porcelain=v1", "-z")) != manifest["statusSha256"]:
        errors.append("Git status changed since snapshot")
    for rec in manifest["files"]:
        path = local(rec["path"])
        if rec["state"] == "Deleted":
            if path.exists():
                errors.append("Deleted file reappeared: " + rec["path"])
        elif not path.is_file() or digest(path.read_bytes()) != rec["sha256"]:
            errors.append("WIP missing or changed: " + rec["path"])
    for rec in manifest["references"]:
        path = Path(rec["path"])
        if not path.is_file() or digest(path.read_bytes()) != rec["sha256"]:
            errors.append("Reference missing or changed: " + rec["archiveName"])
    for name, sha in manifest["artifacts"].items():
        path = bundle / name
        if not path.is_file() or digest(path.read_bytes()) != sha:
            errors.append("Artifact missing or changed: " + name)
    for name, records in [
        ("untracked-files.zip", [r for r in manifest["files"] if r["state"] == "Untracked"]),
        ("references-and-evidence.zip", manifest["references"])
    ]:
        with zipfile.ZipFile(bundle / name) as archive:
            if archive.testzip() is not None:
                errors.append("ZIP CRC failure: " + name)
            expected = {r["path"] if name == "untracked-files.zip" else r["archiveName"]: r["sha256"] for r in records}
            if set(archive.namelist()) != set(expected):
                errors.append("ZIP entries differ: " + name)
            for entry, sha in expected.items():
                if entry not in archive.namelist() or digest(archive.read(entry)) != sha:
                    errors.append("ZIP content differs: " + entry)
    patch = bundle / "working-tree.patch"
    if patch.stat().st_size:
        check = subprocess.run(["git", "apply", "--check", "--reverse", str(patch)],
                               cwd=ROOT, capture_output=True)
        if check.returncode:
            errors.append("Tracked patch no longer matches current worktree")
    report = {"verifiedAtUtc": now(), "passed": not errors, "worktree": str(ROOT),
              "baseCommit": manifest["baseCommit"], "wipFiles": len(manifest["files"]),
              "referenceFiles": len(manifest["references"]), "errors": errors,
              "meaning": "Handover integrity only; not product acceptance or a new test-suite run."}
    (bundle / "verification-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False))
    return 0 if not errors else 1

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=["create", "verify"])
    parser.add_argument("bundle", type=Path)
    args = parser.parse_args()
    target = args.bundle if args.bundle.is_absolute() else ROOT / args.bundle
    target = target.resolve()
    if not target.is_relative_to(ROOT / ".artifacts" / "handover"):
        raise SystemExit("Bundle path outside handover artifacts")
    if args.mode == "create":
        create(target)
    else:
        raise SystemExit(verify(target))

