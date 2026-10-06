# Database readiness — 2026-10-06

The user requested completion of the available database work and test credentials. The canonical working directory remains DAS-Collaboration. EAP and OCR are excluded. No customer database or credentials are available; synthetic verification is separate from customer acceptance.

## Findings and decisions

- Six services own separate stores. Five have SQL migrations; EmailWorker has three mapped tables but no migration, snapshot or design-time factory.
- Add an EF Core 9.0.0 SQL Server baseline for EmailWorker, matching its locked runtime. Preserve every existing migration ID in the other stores.
- Development SQLite continues to use EnsureCreated. SQL development initialization uses migrations. Outside Development, initialization remains forbidden. SQL startup with initialization disabled rejects pending migrations and model drift without changing schema.
- An existing EnsureCreated SQL database is not automatically adopted. It needs an audited, operator-approved baseline; this task does not stamp history or migrate customer data.
- Notification SQL development initialization currently uses EnsureCreated then rejects its own missing migration history. Correct this to Migrate while preserving SQLite behavior.
- Design-time factories use inert SQL Server options without application startup, secrets, seed or workers. Standardize the existing embedded Partner factory and remove Auth factory's dependency on runtime configuration.
- Provide an offline six-store schema export/check command with pinned EF tools, model/snapshot parity and idempotent SQL. The command creates a new ignored output directory, never applies SQL or opens a customer connection.
- Extend the isolated SQL core profile to include EmailWorker. The existing five-store restore profile stays explicitly five-store; no claim of EmailWorker backup/restore coverage.

## Acceptance

Offline generation covers six migration chains with no pending model changes. Regression tests cover EmailWorker migration availability, schema shape, production no-write startup, rejection of unbaselined SQL, development initialization and idempotent SQL replay with preserved rows/FKs. Notification development SQL startup succeeds with recorded migration history. Run synthetic SQL tests in a new owned local instance when available. Document actual test counts and retained external gates. Keep local preview usable after backend rebuild.
