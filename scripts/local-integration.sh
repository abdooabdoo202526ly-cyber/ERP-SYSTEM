#!/bin/bash
# local-integration.sh — Run integration tests with local Docker (DEC-054)
# Requires Docker.
# Estimated time: ~2 minutes.

set -e

echo "🧪 Integration tests with local Postgres..."

# Start test DB
CONTAINER_NAME="erp-test-postgres-$$"
echo "🚀 Starting Postgres container..."
docker run -d --name "$CONTAINER_NAME" \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=test \
  -e POSTGRES_DB=erp_test \
  -p 5432:5432 \
  postgres:15-alpine

# Wait for DB to be ready
echo "⏳ Waiting for Postgres..."
for i in 1 2 3 4 5 6 7 8 9 10; do
  if docker exec "$CONTAINER_NAME" pg_isready -U postgres >/dev/null 2>&1; then
    echo "✅ Postgres ready"
    break
  fi
  sleep 2
done

# Set test connection string
export ConnectionStrings__Postgres="Host=localhost;Database=erp_test;Username=postgres;Password=test;SSL Mode=Disable"
export Marten__ConnectionString="Host=localhost;Database=erp_test_events;Username=postgres;Password=test;SSL Mode=Disable"
export JwtSettings__Secret="LOCAL_INTEGRATION_TEST_SECRET_xxxxxxxxxxxxxxxxxxxxxxxxxxxx"
export Database__AutoMigrate=true

# Cleanup on exit
cleanup() {
  echo "🧹 Cleaning up..."
  docker stop "$CONTAINER_NAME" >/dev/null 2>&1 || true
  docker rm "$CONTAINER_NAME" >/dev/null 2>&1 || true
}
trap cleanup EXIT

# Run integration tests
echo ""
echo "🧪 Running integration tests..."
cd "$(dirname "$0")/../src/backend"
dotnet test Tests/ERPSystem.Tests/ERPSystem.Tests.csproj \
  --configuration Release \
  --filter "Category=Integration" \
  --logger "console;verbosity=normal" \
  --nologo

echo ""
echo "✅ Integration tests passed!"