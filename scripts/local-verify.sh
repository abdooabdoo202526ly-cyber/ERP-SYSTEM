#!/bin/bash
# local-verify.sh — Run before every push (DEC-054)
# Unit tests + frontend lint/build. No DB needed.
# Estimated time: ~30 seconds.

set -e

echo "🔍 Local verification (no DB)..."
echo ""

# Backend unit tests (skip integration + smoke categories)
echo "📦 Backend unit tests..."
cd "$(dirname "$0")/../src/backend"
dotnet test Tests/ERPSystem.Tests/ERPSystem.Tests.csproj \
  --configuration Release \
  --filter "Category!=Integration&Category!=Smoke" \
  --logger "console;verbosity=normal" \
  --nologo

if [ $? -ne 0 ]; then
  echo "❌ Backend unit tests failed"
  exit 1
fi

cd ../..

# Frontend
echo ""
echo "🎨 Frontend type-check + lint + build..."
cd src/frontend
npm run type-check
npm run lint
npm run build

cd ../..

echo ""
echo "✅ All local checks passed! Safe to push."
echo ""
echo "💡 For integration tests with real DB, run:"
echo "   ./scripts/local-integration.sh"