# Database readiness implementation plan

**Goal:** Finish locally actionable schema/provisioning gaps in the six DAS stores, without customer migration, EAP or OCR.

**Design:** [database-readiness-design](../specs/2026-10-06-database-readiness-design.md).

- [x] Add failing EmailWorker offline schema tests and SQL fixture tests; verify the missing baseline fails offline.
- [x] Add compatible EF Design package, inert factory, generated baseline and snapshot; require SQL migrations without production schema writes.
- [x] Test and fix Notification SQL development initialization; standardize safe Partner/Auth design-time factories. SQL RED confirmed the missing migration history before the fix; final SQL results must be read separately.
- [x] Add/test an offline pinned-tool schema export command for all six stores and extend isolated SQL core coverage to EmailWorker.
- [x] Run locked restore, backend tests, six-store offline parity/scripts, SQL core and layout/tool checks. Preserve redacted evidence in a new ignored QA directory. **Execution is complete; full SQL acceptance is not:** final full core stopped at DocumentService45/46 with allocation-lock timeout. See database checkpoint/evidence for focused checks and source scopes.
- [x] Request independent review, address material findings, update database deployment documentation and checkpoints with actual outcomes and external gates.
- [x] Restore/verify local backend preview, commit essential sources, synchronize and push main without overwriting remote work. Runtime `d34ec11` is published on main; seven backend health checks passed and Next dev is Ready. Concurrent frontend changes are preserved outside this commit.

Verification commands: `dotnet test backend/tests/EmailWorkerService.Tests/EmailWorkerService.Tests.csproj --filter FullyQualifiedName~DatabaseSchemaTests`; `python tools/export-database-schema.py --output .artifacts/qa/database-schema-20261006`; `python tools/qa/run-isolated-sql.py --profile core --output .artifacts/qa/database-sql-20261006`; `python tools/run-checks.py --profile backend --output .artifacts/qa/database-backend-20261006`; `python -m unittest discover -s tests/qa`; `python -m unittest discover -s tests/layout`.

Only fresh evidence from these runs supports completion of this milestone. Production/customer readiness remains blocked by actual exports, mappings, configuration, restore scope, environment verification and acceptance.
