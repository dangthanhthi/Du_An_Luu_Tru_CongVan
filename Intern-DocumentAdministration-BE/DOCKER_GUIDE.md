# Docker Build & Database Seeding Guide
### Intern-DocumentAdministration-BE

This guide explains how to fully clean your Docker environment, rebuild all backend services from scratch, and load the SQL seed data (`seed_all.sql`) into SQL Server.

---

## 1. Prerequisites

- Docker Desktop (or Docker Engine) with Docker Compose v2 installed
- A `.env` file in the project root (copy from `.env.example` and fill in real values):

```bash
cp .env.example .env
```

Make sure `.env` contains at least:

```
DB_SA_PASSWORD=YourStrongPasswordHere
JWT_SECRET=REPLACE_WITH_RANDOM_STRING_AT_LEAST_32_CHARACTERS_LONG
```

---

## 2. Clean everything (cache, containers, images, volumes)

Run these from the project root (where `docker-compose.yml` is located).

**Stop and remove containers, networks, and volumes defined in this project:**

```bash
docker compose down -v --remove-orphans
```

**Remove all images built by this project:**

```bash
docker compose down --rmi all -v --remove-orphans
```

**Clear Docker build cache (buildx cache) — this is what causes "stale code" issues:**

```bash
docker builder prune -af
```

**Remove dangling images, stopped containers, unused networks (safe general cleanup):**

```bash
docker system prune -f
```

**Full nuclear option — removes ALL unused Docker data on the machine, including all images/volumes not currently used by a running container (use with caution, affects other projects too):**

```bash
docker system prune -af --volumes
```

> ⚠️ `docker system prune -af --volumes` deletes volumes for **every** project on the machine, not just this one. Only run it if you're sure no other project's data is needed.

**(Optional) Also remove local build artifacts from each .NET service, so nothing stale gets copied into the image context:**

```bash
find . -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
```

---

## 3. Build everything from scratch

Force a full rebuild with no cache, then start all services:

```bash
docker compose build --no-cache
docker compose up -d
```

Or as a single combined command:

```bash
docker compose up -d --build --force-recreate
```

Check that every container is up and healthy:

```bash
docker compose ps
docker compose logs -f
```

Services and ports (from `docker-compose.yml`):

| Service | Port |
|---|---|
| gateway | 8080 |
| auth-service | 5001 |
| document-service | 5002 |
| partner-service | 5003 |
| files-service | 5004 |
| ai-ocr-service | 5006 |
| notification-service | 5007 |
| email-worker-service | 5008 |
| sqlserver | 1434 (host) → 1433 (container) |

---

## 4. Load the SQL seed data (`seed_all.sql`)

The recommended order (also noted inside `seed_all.sql`) is: start **only** SQL Server first, load the seed file, then start the rest of the services.

### Step 1 — Start SQL Server only

```bash
docker compose up -d sqlserver
```

Wait until it reports healthy:

```bash
docker compose ps sqlserver
```

### Step 2 — Copy `seed_all.sql` into the container

```bash
docker cp seed_all.sql sqlserver:/tmp/seed_all.sql
```

### Step 3 — Run the seed file with `sqlcmd` inside the container

Replace `YourStrongPasswordHere` with the same value you set for `DB_SA_PASSWORD` in `.env`.

```bash
docker exec -it sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "YourStrongPasswordHere" -C \
  -i /tmp/seed_all.sql
```

- `-C` trusts the server certificate (needed since SQL Server 2022 images use encrypted connections by default).
- The script is idempotent — it creates the `DocumentManagementDb` database if missing and uses `IF NOT EXISTS` checks, so it can safely be re-run.

### Step 4 — Start the rest of the services

```bash
docker compose up -d --build
```

---

## 5. One-shot "reset everything and reload data" script

For convenience, here is the full sequence combined:

```bash
# 1. Clean
docker compose down -v --remove-orphans
docker compose down --rmi all -v --remove-orphans
docker builder prune -af

# 2. Start DB only
docker compose up -d sqlserver

# 3. Wait for SQL Server healthcheck, then seed
docker cp seed_all.sql sqlserver:/tmp/seed_all.sql
docker exec -it sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "YourStrongPasswordHere" -C \
  -i /tmp/seed_all.sql

# 4. Build and start everything else
docker compose up -d --build --force-recreate
```

---

## 6. Troubleshooting

- **`Login failed for user 'sa'`**: the password in `.env` (`DB_SA_PASSWORD`) doesn't match what SQL Server was initialized with. Since the password is baked into the `sqlserver_data` volume on first run, you must remove the volume (`docker compose down -v`) and recreate the container for a new password to take effect.
- **Port already in use**: another process is bound to 1434/5001-5008/8080 on the host. Stop it, or edit the `ports:` mapping in `docker-compose.yml`.
- **Seed script errors on re-run**: safe to ignore duplicate-key/`IF NOT EXISTS` messages; the script is designed to be idempotent, but if you truly want a fresh DB, drop it first: run `DROP DATABASE DocumentManagementDb;` in `sqlcmd`, then re-run `seed_all.sql`.
- **Stale code still running after rebuild**: confirm you used `--no-cache` on `docker compose build`, and that `find . -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +` was run so old compiled DLLs aren't copied into the build context.
