# DEC-091 + DEC-092 Retrospective: Generated File Locations

## 🎯 Original Location (DEC-091/092)

The generated DTOs and Repository scaffolds were placed under:
```
src/backend/Tools/EntityDtoGen/sample-output/
├── *Dto.g.cs (32 files)
└── repos/
    └── *Repository.g.cs (32 files)
```

## ⚠️ Issue

The `sample-output/` location signals "demo / not-for-production" — which is misleading
because these `.g.cs` files are designed to be **production code** with `namespace ERPSystem.Generated`.

## 🛠️ Attempted Fix (DEC-091/092 Retro)

**Target location**:
- `src/backend/Shared/Generated/DTOs/`
- `src/backend/Shared/Generated/Repos/`

**Why Shared/Generated/?**
- `Host/ERP-SYSTEM.csproj` includes `..\Shared\**\*.cs` automatically, so generated files would be compiled into the main app.
- `Tools/` is intentionally a CLI artifact folder — not part of the build pipeline.

## 🛑 Why Deferred

During the retro, the move was attempted and **build failed** because generated Repository
files reference entity classes (e.g. `Vendor`, `Warehouse`, `Item`) that live in module-specific
namespaces:

```csharp
// Generated Repository references:
using ERPSystem.Modules.Procurement.Entities;  // needed for Vendor
using ERPSystem.Modules.Inventory.Entities;   // needed for Item, Warehouse
```

Each of the 32 repos needs explicit `using` directives to its entity's namespace.
This is a 32-file edit that wasn't part of the original DEC-091/092 deliverables.

## ✅ Resolution

1. **Reverted** the wholesale move (preserves build stability)
2. **This document** captures the planned production location
3. **DEC-099+** (future) will:
   - Add `using` directives to all 32 repos
   - Move files to `Shared/Generated/`
   - Verify build passes

## 🎯 Workaround (Current)

Generated files remain at `Tools/EntityDtoGen/sample-output/` for now. They are NOT compiled
into the production build (they're in a CLI tool project). No consumers reference them yet
(verified via `grep`).

When DEC-093+ executes (replace manual DTOs/Repos), the production move will happen in the
same PR — avoiding the temporary inconsistency.

---

## 🛡️ Defense Layer 63

DEC-091/092/093 file locations documented for future execution.

---

Refs: DEC-091 (DTO codegen CLI), DEC-092 (Repository scaffolds), DEC-093 (replace plan)
EOF
