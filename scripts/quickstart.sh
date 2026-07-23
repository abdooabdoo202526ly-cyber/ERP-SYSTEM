#!/usr/bin/env bash
# =============================================================================
# ERP-SYSTEM — Quick Start (one command)
# =============================================================================
# Usage:
#   ./scripts/quickstart.sh           # start everything
#   ./scripts/quickstart.sh --down    # stop everything
#   ./scripts/quickstart.sh --status  # show status
#   ./scripts/quickstart.sh --logs    # tail logs
#   ./scripts/quickstart.sh --reset   # stop + delete all data (DESTRUCTIVE)
# =============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
DOCKER_DIR="$ROOT_DIR/infra/docker"
COMPOSE_FILE="$DOCKER_DIR/docker-compose.dev.yml"
PROJECT_NAME="erp-system"

# Colors
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; BLUE='\033[0;34m'; NC='\033[0m'

step()  { echo -e "${BLUE}▶ $1${NC}"; }
ok()    { echo -e "${GREEN}✅ $1${NC}"; }
warn()  { echo -e "${YELLOW}⚠️  $1${NC}"; }
fail()  { echo -e "${RED}❌ $1${NC}"; exit 1; }

# --- Detect mode ------------------------------------------------------------
MODE="docker"
if ! command -v docker &>/dev/null; then MODE="native"; fi
if ! docker info &>/dev/null 2>&1; then MODE="native"; fi

# --- Helpers ----------------------------------------------------------------
check_docker() {
  if ! command -v docker &>/dev/null; then
    fail "Docker not installed. Install Docker Desktop: https://www.docker.com/products/docker-desktop"
  fi
  if ! docker info &>/dev/null 2>&1; then
    fail "Docker daemon not running. Start Docker Desktop and retry."
  fi
}

check_native_prereqs() {
  command -v dotnet  &>/dev/null || fail ".NET 9 SDK not installed: https://dot.net"
  command -v node    &>/dev/null || fail "Node.js 20+ not installed: https://nodejs.org"
  command -v psql    &>/dev/null || warn "psql CLI not found (DB tools will still work via backend)"
  command -v pg_isready &>/dev/null || warn "PostgreSQL client not found"
}

# --- Wait for service -------------------------------------------------------
wait_for_http() {
  local url="$1"; local label="$2"; local max="${3:-90}"
  step "Waiting for $label ($url)..."
  for i in $(seq 1 $max); do
    if curl -sSf "$url" >/dev/null 2>&1; then
      ok "$label is up (after ${i}s)"
      return 0
    fi
    sleep 1
  done
  fail "$label did not respond within ${max}s at $url"
}

# --- Start (Docker) ---------------------------------------------------------
start_docker() {
  check_docker
  step "Starting full stack via Docker Compose..."
  (cd "$DOCKER_DIR" && docker compose -p "$PROJECT_NAME" -f "$COMPOSE_FILE" up -d --build)
  ok "Containers started"

  step "Waiting for PostgreSQL..."
  for i in $(seq 1 60); do
    if docker compose -p "$PROJECT_NAME" -f "$COMPOSE_FILE" exec -T postgres pg_isready -U erp_user -d neondb >/dev/null 2>&1; then
      ok "PostgreSQL is ready (after ${i}s)"
      break
    fi
    sleep 1
  done

  wait_for_http "http://localhost:5000/health" "Backend API" 120
  wait_for_http "http://localhost:3000"        "Frontend"     120
}

# --- Start (Native) ---------------------------------------------------------
start_native() {
  check_native_prereqs

  step "Checking PostgreSQL on localhost:5432..."
  if ! pg_isready -h localhost -p 5432 >/dev/null 2>&1; then
    warn "PostgreSQL not responding on localhost:5432"
    echo ""
    echo "  Start it manually:"
    echo "    macOS:  brew services start postgresql@15"
    echo "    Linux:  sudo systemctl start postgresql"
    echo "    Windows: start the postgresql-x64-15 service"
    echo ""
    echo "  Then create the databases:"
    echo "    psql -U postgres -c \"CREATE USER erp_user WITH PASSWORD 'erp_password';\""
    echo "    psql -U postgres -c \"CREATE DATABASE neondb OWNER erp_user;\""
    echo "    psql -U postgres -c \"CREATE DATABASE erp_events OWNER erp_user;\""
    fail "PostgreSQL not running"
  fi

  step "Starting Backend..."
  (cd "$ROOT_DIR/src/backend" && \
    ASPNETCORE_ENVIRONMENT=Development \
    nohup dotnet run --project Host --urls "http://localhost:5000" \
    > "$ROOT_DIR/.backend.log" 2>&1 &)
  wait_for_http "http://localhost:5000/health" "Backend API" 120

  step "Starting Frontend..."
  if [ ! -d "$ROOT_DIR/src/frontend/node_modules" ]; then
    (cd "$ROOT_DIR/src/frontend" && npm install)
  fi
  (cd "$ROOT_DIR/src/frontend" && \
    NEXT_PUBLIC_API_URL="http://localhost:5000" \
    nohup npm run dev > "$ROOT_DIR/.frontend.log" 2>&1 &)
  wait_for_http "http://localhost:3000" "Frontend" 90
}

# --- Stop -------------------------------------------------------------------
stop_docker() {
  check_docker
  step "Stopping containers..."
  (cd "$DOCKER_DIR" && docker compose -p "$PROJECT_NAME" -f "$COMPOSE_FILE" down)
  ok "Stopped"
}

stop_native() {
  step "Stopping local processes..."
  pkill -f "dotnet.*Host"  2>/dev/null || true
  pkill -f "next dev"      2>/dev/null || true
  pkill -f "next-server"   2>/dev/null || true
  ok "Stopped"
}

# --- Status -----------------------------------------------------------------
print_status() {
  echo ""
  echo "📊 Container status:"
  if command -v docker &>/dev/null && docker info &>/dev/null 2>&1; then
    (cd "$DOCKER_DIR" && docker compose -p "$PROJECT_NAME" -f "$COMPOSE_FILE" ps) 2>/dev/null || echo "  (no containers running)"
  else
    echo "  (Docker not available)"
  fi
  echo ""
  echo "🌐 Health checks:"
  for svc in \
    "Backend:http://localhost:5000/health" \
    "Swagger:http://localhost:5000/swagger" \
    "Frontend:http://localhost:3000"; do
    name="${svc%%:*}"; url="${svc#*:}"
    if curl -sSf -o /dev/null --max-time 2 "$url" 2>/dev/null; then
      echo "  ✅ $name  $url"
    else
      echo "  ❌ $name  $url  (not responding)"
    fi
  done
}

# --- Reset ------------------------------------------------------------------
reset_data() {
  warn "This will DELETE all data (database, logs, caches). Continue? [y/N]"
  read -r ans
  [[ "$ans" == "y" || "$ans" == "Y" ]] || { echo "Aborted."; exit 0; }
  if command -v docker &>/dev/null && docker info &>/dev/null 2>&1; then
    (cd "$DOCKER_DIR" && docker compose -p "$PROJECT_NAME" -f "$COMPOSE_FILE" down -v)
    ok "All volumes deleted"
  fi
  rm -f "$ROOT_DIR/.backend.log" "$ROOT_DIR/.frontend.log" 2>/dev/null || true
  ok "Reset complete"
}

# --- Main -------------------------------------------------------------------
echo ""
echo "🚀 ERP-SYSTEM Quick Start"
echo "=========================="
echo "  Mode: $MODE"
echo ""

case "${1:-up}" in
  up|--up|start)
    if [ "$MODE" = "docker" ]; then start_docker; else start_native; fi
    echo ""
    echo "🎉 ERP-SYSTEM is up!"
    echo "============================================="
    echo "  🌐 Frontend:    http://localhost:3000"
    echo "  🔌 Backend:     http://localhost:5000"
    echo "  📚 Swagger:     http://localhost:5000/swagger"
    echo "  ❤️  Health:     http://localhost:5000/health"
    echo "============================================="
    echo "  [User] Login:    admin@alfajr.local / Demo1234"
    echo "                  (created automatically on first start by the seed)"
    echo ""
    echo "  📖 Full docs:   cat README-DEV.md"
    echo "  📋 Logs:        ./scripts/quickstart.sh --logs"
    echo "  🛑 Stop:        ./scripts/quickstart.sh --down"
    echo ""
    ;;
  --down|down|stop)
    if [ "$MODE" = "docker" ]; then stop_docker; else stop_native; fi
    ;;
  --status|status)
    print_status
    ;;
  --logs|logs)
    if [ "$MODE" = "docker" ]; then
      (cd "$DOCKER_DIR" && docker compose -p "$PROJECT_NAME" -f "$COMPOSE_FILE" logs -f --tail=50)
    else
      tail -f "$ROOT_DIR/.backend.log" "$ROOT_DIR/.frontend.log" 2>/dev/null
    fi
    ;;
  --reset|reset)
    reset_data
    ;;
  --help|-h|help)
    head -20 "$0"
    ;;
  *)
    fail "Unknown command: $1. Run with --help."
    ;;
esac
