# ERP-SYSTEM Dockerfile for Hugging Face Spaces
# Stack: ASP.NET Core 9 (Backend) + Next.js 14 (Frontend) + Caddy (Reverse Proxy)
# Multi-stage build for optimal image size

# ==========================================
# Stage 1: Build Backend (.NET 9)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS backend-build
WORKDIR /src

# استعادة الـ dependencies أولاً (cache layer)
COPY src/backend/Host/ERP-SYSTEM.csproj src/backend/Host/
COPY src/backend/Modules src/backend/Modules
COPY src/backend/Shared src/backend/Shared
RUN dotnet restore src/backend/Host/ERP-SYSTEM.csproj

# نسخ باقي الكود + build + publish
COPY src/backend/ src/backend/
RUN dotnet publish src/backend/Host/ERP-SYSTEM.csproj \
    -c Release \
    -o /app/api \
    --no-restore \
    /p:UseAppHost=false

# ==========================================
# Stage 2: Build Frontend (Next.js 14)
# ==========================================
FROM node:20-alpine AS frontend-build
WORKDIR /app

# استعادة الـ dependencies أولاً
COPY src/frontend/package.json src/frontend/package-lock.json ./
RUN npm ci --no-audit --no-fund

# نسخ باقي الكود + build
COPY src/frontend/ ./
RUN npm run build

# ==========================================
# Stage 3: Runtime (ASP.NET + Node + Caddy)
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install Node.js 20, supervisor, Caddy, and PostgreSQL client (for DB test)
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        supervisor \
        curl \
        ca-certificates \
        postgresql-client && \
    curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y --no-install-recommends nodejs && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Install Caddy (للـ reverse proxy)
RUN curl -1sLf "https://github.com/caddyserver/caddy/releases/download/v2.8.4/caddy_2.8.4_linux_amd64.tar.gz" \
    -o caddy.tar.gz && \
    tar -xzf caddy.tar.gz caddy && \
    mv caddy /usr/local/bin/caddy && \
    chmod +x /usr/local/bin/caddy && \
    rm caddy.tar.gz

# نسخ الـ API (من Stage 1)
COPY --from=backend-build /app/api /app/api

# نسخ الـ Frontend (من Stage 2)
COPY --from=frontend-build /app/.next/standalone /app/web
COPY --from=frontend-build /app/.next/static /app/web/.next/static
COPY --from=frontend-build /app/public /app/web/public

# إنشاء مجلدات caddy و supervisord اللازمة (إذا لم تكن موجودة)
RUN mkdir -p /etc/caddy /var/log/supervisor /etc/supervisor/conf.d

# Caddyfile للـ reverse proxy
# نستخدم on_demand health check: Caddy ينتظر Backend يبدأ قبل توجيه الـ traffic
RUN cat > /etc/caddy/Caddyfile << 'EOF'
{
    auto_https off
    admin off
}
:7860 {
    encode gzip zstd

    # Forward API requests to .NET backend
    handle /api/* {
        reverse_proxy 127.0.0.1:5000 {
            header_up Host {host}
            header_up X-Real-IP {remote}
            # log backend errors for visibility
            transport http {
                dial_timeout 5s
                response_header_timeout 30s
            }
        }
    }

    # Forward all other requests to Next.js
    handle {
        reverse_proxy 127.0.0.1:3000 {
            header_up Host {host}
            header_up X-Real-IP {remote}
            transport http {
                dial_timeout 5s
                response_header_timeout 30s
            }
        }
    }

    log {
        output stdout
        level info
    }
}
EOF

# entrypoint.sh: يحوّل المتغيرات البسيطة من HF إلى متغيرات .NET الكاملة
RUN cat > /app/entrypoint.sh << 'ENTRYEOF'
#!/bin/bash
set -e

echo "=== ERP-SYSTEM Starting ==="

# دعم طريقتين لإعداد env vars:
#   1) Simple names (DB_CONNECTION, EVENTS_CONNECTION, JWT_SECRET) — HF لا يقبل "__"
#   2) Full .NET names (JwtSettings__Secret, ConnectionStrings__Postgres) — لو HF قبل "__" في Secret

# .NET env vars config: "SectionName__Key" overrides appsettings.json
# نحترم الـ env vars الموجودة أولاً، ثم fallback إلى الأسماء البسيطة
export ConnectionStrings__Postgres="${ConnectionStrings__Postgres:-${DB_CONNECTION:-}}"
export Marten__ConnectionString="${Marten__ConnectionString:-${EVENTS_CONNECTION:-}}"
export JwtSettings__Secret="${JwtSettings__Secret:-${JWT_SECRET:-}}"
export JwtSettings__Issuer="${JwtSettings__Issuer:-${JWT_ISSUER:-ERP-SYSTEM}}"
export JwtSettings__Audience="${JwtSettings__Audience:-${JWT_AUDIENCE:-ERP-SYSTEM-Users}}"
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}"

# التحقق من المتغيرات المطلوبة
if [ -z "$ConnectionStrings__Postgres" ]; then
    echo "❌ ERROR: DB_CONNECTION (or ConnectionStrings__Postgres) is not set!"
    echo "   Available env vars matching DB:"
    env | grep -i db || echo "   (none found)"
    exit 1
fi
if [ -z "$JwtSettings__Secret" ]; then
    echo "❌ ERROR: JWT_SECRET (or JwtSettings__Secret) is not set!"
    echo "   Available env vars matching JWT:"
    env | grep -i jwt || echo "   (none found)"
    exit 1
fi
if [ -z "$Marten__ConnectionString" ]; then
    echo "❌ ERROR: EVENTS_CONNECTION (or Marten__ConnectionString) is not set!"
    echo "   Available env vars matching EVENTS:"
    env | grep -i event || echo "   (none found)"
    exit 1
fi

echo "✅ Environment variables mapped successfully"
echo "  - DB: ${ConnectionStrings__Postgres:0:80}..."
echo "  - Events: ${Marten__ConnectionString:0:80}..."
echo "  - JWT Issuer: ${JwtSettings__Issuer}"
echo "  - JWT Audience: ${JwtSettings__Audience}"
echo "  - JWT Secret length: ${#JwtSettings__Secret}"

# اختبار الاتصال بـ DB قبل تشغيل التطبيقات
echo ""
echo "🔍 Testing database connection..."
if command -v psql > /dev/null 2>&1; then
    if psql "$ConnectionStrings__Postgres" -c "SELECT 1" > /dev/null 2>&1; then
        echo "✅ Database connection OK"
    else
        echo "⚠️ Database connection failed (will let app try anyway)"
    fi
else
    echo "ℹ️ psql not available, skipping connection test"
fi

# تشغيل supervisord
exec supervisord -n -c /etc/supervisor/supervisord.conf
ENTRYEOF
RUN chmod +x /app/entrypoint.sh

# Supervisor config لإدارة الـ processes
RUN cat > /etc/supervisor/conf.d/erp.conf << 'EOF'
[program:api]
command=dotnet /app/api/ERPSystem.Host.dll
directory=/app/api
environment=ASPNETCORE_ENVIRONMENT="Production",ASPNETCORE_URLS="http://*:5000",DOTNET_RUNNING_IN_CONTAINER="true"
autostart=true
autorestart=true
startretries=10
stdout_logfile=/dev/stdout
stdout_logfile_maxbytes=0
stderr_logfile=/dev/stderr
stderr_logfile_maxbytes=0
priority=10

[program:frontend]
command=node /app/web/server.js
directory=/app/web
environment=NODE_ENV=production,PORT=3000,HOSTNAME=0.0.0.0
autostart=true
autorestart=true
startretries=10
stdout_logfile=/dev/stdout
stdout_logfile_maxbytes=0
stderr_logfile=/dev/stderr
stderr_logfile_maxbytes=0
priority=20

[program:caddy]
command=caddy run --config /etc/caddy/Caddyfile
autostart=true
autorestart=true
startretries=10
stdout_logfile=/dev/stdout
stdout_logfile_maxbytes=0
stderr_logfile=/dev/stderr
stderr_logfile_maxbytes=0
priority=30
EOF

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:7860/api/health/live || exit 1

EXPOSE 7860

# تشغيل entrypoint.sh الذي يحوّل المتغيرات ثم يشغل supervisord
CMD ["/app/entrypoint.sh"]
