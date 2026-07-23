# 🚀 ERP-SYSTEM — Local Development Guide

One-command setup for running the full ERP system on your machine.

## ⚡ TL;DR — One command

### macOS / Linux
```bash
cd erp-system
./scripts/quickstart.sh
```

### Windows (PowerShell)
```powershell
cd erp-system
.\scripts\quickstart.ps1
```

That's it. The script auto-detects Docker and brings up the full stack (PostgreSQL + Redis + Backend + Frontend). It waits for each service to be healthy, runs migrations + seed data, and prints the URLs.

## 📋 Commands

| Action | macOS/Linux | Windows |
|---|---|---|
| **Start** | `./scripts/quickstart.sh` | `.\scripts\quickstart.ps1` |
| **Stop** | `./scripts/quickstart.sh --down` | `.\scripts\quickstart.ps1 -Down` |
| **Status** | `./scripts/quickstart.sh --status` | `.\scripts\quickstart.ps1 -Status` |
| **Logs** | `./scripts/quickstart.sh --logs` | `.\scripts\quickstart.ps1 -Logs` |
| **Reset (delete all data)** | `./scripts/quickstart.sh --reset` | `.\scripts\quickstart.ps1 -Reset` |

## 🌐 URLs

Once running:

| Service | URL |
|---|---|
| Frontend (Next.js) | http://localhost:3000 |
| Backend API | http://localhost:5000 |
| Swagger docs | http://localhost:5000/swagger |
| Health check | http://localhost:5000/health |
| PostgreSQL | `localhost:5432` (user: `erp_user`, pass: `erp_password`) |
| Redis | `localhost:6379` |

## 👤 Default Login

The seed data creates an admin user on first run:

| Field | Value |
|---|---|
| Email | `admin@alfajr.local` |
| Password | `Demo1234` |

A "realistic" demo dataset is also created (companies, customers, vendors, products, projects, journal entries) so you can explore immediately.

## 🛠 What the script does

1. **Detects your environment** — Docker available? Uses Docker Compose. Otherwise falls back to native mode (requires PostgreSQL installed locally).
2. **Starts the stack** — PostgreSQL 15, Redis 7, .NET 9 backend, Next.js 14 frontend.
3. **Waits for health** — checks `pg_isready`, `/health`, then frontend port.
4. **Migrations + seed run automatically** on backend startup (`Database__AutoMigrate: true`, `SeedRealisticScenario: true` in `appsettings.json`).
5. **Prints URLs + credentials** when ready.

## 🔧 Manual Setup (if the script doesn't work)

### Option A — Docker (recommended)
```bash
cd infra/docker
docker compose -f docker-compose.dev.yml up -d --build
docker compose -f docker-compose.dev.yml logs -f
```

### Option B — Native (no Docker)

**Prereqs:** .NET 9 SDK, Node 20+, PostgreSQL 15+ running locally.

1. **Create the database:**
   ```bash
   psql -U postgres <<SQL
   CREATE USER erp_user WITH PASSWORD 'erp_password';
   CREATE DATABASE neondb OWNER erp_user;
   CREATE DATABASE erp_events OWNER erp_user;
   SQL
   ```

2. **Start the backend** (auto-migrates + seeds):
   ```bash
   cd src/backend
   ASPNETCORE_ENVIRONMENT=Development dotnet run --project Host --urls http://localhost:5000
   ```

3. **Start the frontend** (in another terminal):
   ```bash
   cd src/frontend
   npm install
   NEXT_PUBLIC_API_URL=http://localhost:5000 npm run dev
   ```

4. Open http://localhost:3000 and log in.

## 🐛 Troubleshooting

### Port already in use
```bash
# Find what's using the port
lsof -i :5000    # macOS/Linux
netstat -ano | findstr :5000   # Windows

# Stop conflicting process, or change the port in appsettings.json
```

### Docker won't start
- Make sure Docker Desktop is running (whale icon in system tray)
- On Linux, ensure your user is in the `docker` group: `sudo usermod -aG docker $USER` (log out/in)
- Reset Docker: Docker Desktop → Troubleshoot → Reset to factory defaults

### Frontend can't reach backend
- Check `NEXT_PUBLIC_API_URL` in `.env.local` (create if missing):
  ```
  NEXT_PUBLIC_API_URL=http://localhost:5000
  ```
- Check the backend is actually up: `curl http://localhost:5000/health`

### Migrations failed
- The backend auto-runs migrations on startup. If they fail, check the logs:
  ```bash
  ./scripts/quickstart.sh --logs
  ```
- To re-run manually:
  ```bash
  docker compose -f infra/docker/docker-compose.dev.yml exec api \
    dotnet run --project /app/Host -- --migrate
  ```

### Database is corrupted / wrong state
```bash
./scripts/quickstart.sh --reset    # ⚠️ deletes everything
./scripts/quickstart.sh            # fresh start
```

## 📁 Project Structure (quick reference)

```
erp-system/
├── infra/
│   └── docker/
│       ├── docker-compose.dev.yml   # full stack
│       └── init-scripts/            # postgres init
├── src/
│   ├── backend/                     # .NET 9 (Modules pattern)
│   │   ├── Host/                    # API host + Program.cs
│   │   ├── Modules/                 # one folder per domain
│   │   └── Shared/                  # cross-cutting
│   └── frontend/                    # Next.js 14 (App Router)
│       ├── app/                     # pages
│       ├── components/
│       └── lib/
├── scripts/
│   ├── quickstart.sh               # ← the one command
│   └── quickstart.ps1              # ← Windows version
└── RUNBOOK.md                       # operations
```

## 🚢 Production

The `docker-compose.dev.yml` is for **local development only**. Production deployment:

- Use real secrets (never commit `JwtSettings__Secret`)
- Replace `neondb` connection string with your managed Postgres
- Enable HTTPS, set up a reverse proxy (Caddy / nginx)
- Set `Database__AutoMigrate: false` and run migrations as a separate step
- See `RUNBOOK.md` for full deployment guide

---

**Tip:** Pin your terminal in the project root and use `./scripts/quickstart.sh --logs` to watch the whole stack at once.
