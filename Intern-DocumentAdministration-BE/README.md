# Document Administration System — Backend

This repository contains the backend API for the **Document Administration System** - an internal system for registering, tracking, and distributing incoming, outgoing, and internal official documents across departments. Built with a **microservices architecture** using .NET and SQL Server.

## 🚀 Quick Start (Docker)

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running.
- [.NET SDK](https://dotnet.microsoft.com/) — check the version in use with `dotnet --version`; keep it consistent across the team.
- [Git](https://git-scm.com/)
- SQL Server Management Studio (SSMS) or Azure Data Studio — optional, for inspecting the database directly.

⚠️ If SQL Server is already installed **natively** on your machine, stop or uninstall it first. It will compete for port `1433` with the containerized SQL Server and cause login failures that look like a wrong password.

### Setup and Deployment

1. **Clone the repository**:

   ```bash
   git clone <repository-url>
   cd Intern-DocumentAdministration-BE
   ```

2. **Configure environment variables**:

   ```bash
   cp .env.example .env
   ```

   `.env` is **local to your machine only** — it is git-ignored, never committed, and each team member creates their own copy after cloning.

   - `DB_SA_PASSWORD` — pick any value that meets SQL Server's complexity rules (≥ 8 characters, mixed case + number + symbol). No need to match anyone else's.
   - `JWT_SECRET` — any string of **at least 32 characters** (256 bits) works while developing alone; HS256 will fail to start otherwise. When running services together for a shared demo/deployment, **all services must use the same `JWT_SECRET`**, since a token issued by `auth-service` needs to be verifiable by every other service.
   - `EMAIL_IMAP_*` / `SMTP_*` — only needed by `email-worker-service` / `notification-service`; use the project's shared test mailbox credentials, provided separately (not through Git).

3. **Start the database**:

   ```bash
   docker-compose up -d sqlserver
   docker ps   # confirm sqlserver is "Up (healthy)"
   ```

4. **Build and start a service** (repeat per service, or start everything once all services have code + a `Dockerfile`):

   ```bash
   docker-compose up -d --build <service-name>
   # or, once all services are ready:
   docker-compose up -d --build
   ```

5. **Apply database migrations** for each service (see [Database Migrations](#-database-migrations)):

   ```bash
   cd services/<service-name>
   dotnet ef database update
   ```

### Verify everything is running

```bash
docker ps
docker logs <service-name> --tail 50
```

Open Swagger for a running service, e.g.:
```
http://localhost:5001/swagger
```

---

## 🛠️ Common Commands

### Development

- `dotnet restore` / `dotnet build` — restore and build a service locally.
- `dotnet run` — run a service directly on the host (requires `docker-compose up -d sqlserver` running first, and a local `appsettings.Development.json` with a valid connection string).
- `dotnet ef migrations add <Name>` — add a new EF Core migration after changing entity classes.
- `dotnet ef database update` — apply pending migrations to the database.
- `dotnet ef migrations script` — preview the raw SQL a migration will run before applying it.

### Docker

- `docker-compose up -d --build <service>` — build and (re)start one service.
- `docker-compose up -d --build` — build and start everything.
- `docker-compose down` — stop and remove containers (keeps data).
- `docker-compose down -v` — stop and remove containers **and volumes** (wipes the database — use to reset from scratch).
- `docker logs <service> --tail 50` — view recent logs for a service.
- `docker ps` — list running containers and their status.

---

## 🏗️ Technical Stack

- **Framework**: ASP.NET Core (C#), .NET 9/10
- **Database**: SQL Server 2022
- **ORM**: Entity Framework Core
- **Auth**: JWT (access token + refresh token), BCrypt password hashing
- **API Gateway**: Ocelot
- **Containerization**: Docker & Docker Compose

## 🧩 Services

| Service | Responsibility | Port (host) |
|---|---|---|
| `gateway` | Single entry point for all client requests, routes to backend services | 8080 |
| `auth-service` | Login, JWT issuance/refresh, User/Role/Department (RBAC) CRUD | 5001 |
| `document-service` | Incoming/Outgoing/Internal document registration, document numbering, status workflow, department access control | 5002 |
| `partner-service` | External Entities (partners) CRUD, soft delete | 5003 |
| `files-service` | File upload/download, storage | 5004 |
| `email-worker-service` | Hourly job: polls the fax inbox, extracts PDF attachments, registers incoming documents | — (background job) |
| `ai-ocr-service` | Reads incoming document PDFs, extracts text, matches sender against the partner list | 5006 |
| `notification-service` | Sends email notifications (e.g. new incoming document alert to the Director's secretary) | 5007 |

Each service owns its own **database schema** (`auth`, `document`, `partner`, `files`, `notification`) inside a single shared SQL Server instance. Cross-service data references are logical only, never enforced with physical foreign keys across schemas — services call each other's APIs, they never query another service's tables directly.

## 📁 Project Structure

```
Intern-DocumentAdministration-BE/
├── docker-compose.yml
├── .env                      # not committed — copy from .env.example
├── .env.example
├── gateway/
│   └── Dockerfile
└── services/
    ├── Dockerfile.template   # copy into each service folder, rename to Dockerfile
    ├── auth-service/
    ├── document-service/
    ├── partner-service/
    ├── files-service/
    ├── email-worker-service/
    ├── ai-ocr-service/
    └── notification-service/
```

## ➕ Adding a New Service

Follow these steps whenever a service's code doesn't exist yet (its folder under `services/` is currently empty).

1. **Scaffold the project** — do not hand-copy files from another service; let .NET generate them:

```bash
   cd services/<service-folder-name>
   dotnet new webapi -n <PascalCaseServiceName> --use-controllers
```

   If this creates an extra nested subfolder, flatten it:

```bash
   mv <PascalCaseServiceName>/* .
   rmdir <PascalCaseServiceName>
```

2. **Install packages** as needed (adjust per service — not every service needs all of these):

```bash
   dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 10.0.0
   dotnet add package Microsoft.EntityFrameworkCore.Design --version 10.0.0
   dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 10.0.0
```

3. **Copy the Docker files**, then edit the one line that needs to change:

```bash
   cp ../Dockerfile.template Dockerfile
   cp ../.dockerignore.template .dockerignore
```

   In `Dockerfile`, change the last line to match this service's actual `.dll` name:
```dockerfile
   ENTRYPOINT ["dotnet", "<PascalCaseServiceName>.dll"]
```

4. **Create the initial migration** and apply it (see Database Migrations below).

5. **Verify locally**, then via Docker:

```bash
   dotnet run                                        # quick local check
   docker-compose up -d --build <service-folder-name>  # full container check
```

## 🗄️ Database Migrations

Each service manages its own schema independently via **EF Core Migrations** — there is no shared migration process across services.

```bash
cd services/<service-name>
dotnet tool install --global dotnet-ef   # once per machine
dotnet ef migrations add <DescriptiveName>
dotnet ef database update
```

[`schema.sql`](./schema.sql) is kept as reference documentation of the intended structure — it is **not** run against a database that already has migrations applied, to avoid conflicting with EF Core's own tracking table (`__EFMigrationsHistory`).

## ⚙️ Environment Notes

| Variable | Used by | Description |
|---|---|---|
| `DB_SA_PASSWORD` | `sqlserver`, all services | SQL Server `sa` password |
| `JWT_SECRET` | `auth-service`, all services validating tokens | Signing key for JWT (HS256 — must be ≥ 32 characters / 256 bits) |
| `EMAIL_IMAP_HOST` / `EMAIL_IMAP_USERNAME` / `EMAIL_IMAP_PASSWORD` | `email-worker-service` | Fax inbox credentials |
| `SMTP_HOST` / `SMTP_USERNAME` / `SMTP_PASSWORD` | `notification-service` | Outgoing email credentials |

- Each service reads configuration from environment variables (via `docker-compose.yml`, sourced from `.env`) when running in Docker, or from `appsettings.Development.json` (git-ignored, not committed) when run directly on the host with `dotnet run`.
- Environment variables use double-underscore nesting (e.g. `Jwt__Secret`, `ConnectionStrings__Default`), matching ASP.NET Core's configuration binding convention.

## 🏥 Health & Swagger Endpoints

Each service exposes:

- **Health check**: `http://localhost:<port>/health`
- **Swagger UI**: `http://localhost:<port>/swagger` (enabled in `Development` environment)

Example for `auth-service`: `http://localhost:5001/swagger`

## 🔒 Security Note

`auth-service` issues JWTs signed with HS256 using `JWT_SECRET` — rotate this value for any real deployment, and never reuse the development value in production. Passwords are hashed with BCrypt before storage; plaintext passwords are never persisted or logged.

## 🌿 Git Workflow

`main` must always stay in a working state — never push directly to it (branch protection is enabled to enforce this). Work happens on a branch **per service**, not per person, so anyone can find the relevant branch by the service name alone:

```
main                              (always working, always reviewed)
├── feature/auth-service
├── feature/document-service
├── feature/partner-service
├── feature/files-service
├── feature/email-worker-service
├── feature/ai-ocr-service
├── feature/notification-service
└── feature/gateway
```

```bash
git checkout main
git pull
git checkout -b feature/<service-name>
# ... commit your work ...
git push -u origin feature/<service-name>
```

Then open a **Pull Request** into `main` on GitHub and request a review before merging — this catches basic mistakes (accidentally committed secrets, missing `.gitignore` entries, broken builds) before they land in the shared history.

## 🩹 Troubleshooting

**Login fails with SQL error 18456 ("Login failed for user 'sa'")**
Most likely a native SQL Server installation is competing for port `1433` with the Docker container. Stop/uninstall the native instance, or remap the container's port and connect on the new one.

**`ArgumentOutOfRangeException: Unable to create KeyedHashAlgorithm for algorithm 'HS256'`**
`JWT_SECRET` is too short. HS256 requires a key of at least 256 bits (32 characters). Lengthen the value in `.env` and rebuild the service (`docker-compose up -d --build <service>`).

**`docker-compose` warns that a variable "is not set, defaulting to a blank string"**
The `.env` file is missing, misnamed (check it wasn't saved as `.env.txt`), or not in the same directory as `docker-compose.yml`.

**Docker build fails with `... not found` for a `mcr.microsoft.com/dotnet/...` image tag**
The SDK and runtime images use different tagging schemes — the runtime (`aspnet`) image does not have patch-level tags like the SDK does. Use `major.minor` only (e.g. `9.0`, not `9.0.306`) for both images, and keep them consistent with the `TargetFramework` in the service's `.csproj`.