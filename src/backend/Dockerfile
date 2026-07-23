# ERP-SYSTEM Backend Dockerfile
# Multi-stage build: SDK → Runtime
# Modular Monolith: Host csproj يستخدم ../Modules و ../Shared

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# استعادة الـ dependencies أولاً (cache layer)
# ننسخ كل الـ csproj حتى لو تغيرت، restore يعتمد على csproj فقط
COPY Host/ERP-SYSTEM.csproj Host/
COPY Modules/ Modules/
COPY Shared/ Shared/
RUN dotnet restore Host/ERP-SYSTEM.csproj

# بناء + publish
COPY . .
RUN dotnet publish Host/ERP-SYSTEM.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# إنشاء مستخدم non-root
RUN groupadd --system --gid 1001 erp && \
    useradd --system --uid 1001 --gid erp --shell /sbin/nologin erpsvc

COPY --from=build --chown=erpsvc:erp /app/publish .

USER erpsvc

ENV ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "ERPSystem.Host.dll"]
