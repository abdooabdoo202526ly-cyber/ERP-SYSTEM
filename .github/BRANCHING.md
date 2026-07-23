# 🌿 Branching Strategy

> ERP-SYSTEM uses **GitHub Flow + develop branch** (DEC-052).
> See `AGENTS.md` (root) for project conventions.

## 📊 Branch Hierarchy

```
main (production)
  ├── develop (integration)
  │    ├── feature/* (new features)
  │    ├── fix/* (bug fixes)
  │    └── docs/* (documentation)
  └── hotfix/* (critical production fixes)
```

## 🎯 Branch Types

| Branch | Purpose | Created from | Merges to | Lifetime |
|---|---|---|---|---|
| `main` | Production | — | — | Permanent |
| `develop` | Integration | `main` | `main` | Permanent |
| `feature/*` | New feature | `develop` | `develop` | Days |
| `fix/*` | Bug fix | `develop` | `develop` | Hours-Days |
| `hotfix/*` | Critical prod fix | `main` | `main` + `develop` | Hours |
| `docs/*` | Documentation | `develop` | `develop` | Hours-Days |

## 📝 Naming Convention

```
feature/M1-add-login
feature/M2-payment-flow
fix/123-alburj-seeder-bug
fix/456-jwt-token-expiry
hotfix/789-prod-crash
docs/update-readme
```

## 🔄 Workflow

1. **Create branch**:
   ```bash
   git checkout develop
   git pull origin develop
   git checkout -b feature/M1-add-login
   ```

2. **Work + commit**:
   ```bash
   git add .
   git commit -m "feat(auth): add login endpoint"
   git push origin feature/M1-add-login
   ```

3. **Open PR**:
   - From: `feature/M1-add-login`
   - To: `develop`
   - Title: `[M1] Add login endpoint`
   - Body: Use PR template (`.github/pull_request_template.md`)

4. **CI runs** (automatic):
   - Tests + Build + Sync to HF Space
   - Auto-rollback if health fails

5. **Review + merge**:
   - 1+ reviewer approval required
   - Squash and merge (keeps history clean)

6. **Promote to main** (periodically):
   - When develop is stable
   - Create PR: `develop` → `main`
   - Merge commit (preserves history)

## 🛡️ Protection Rules

| Branch | Require PR | Require CI | Force Push | Delete |
|---|---|---|---|---|
| `main` | ✅ Yes | ✅ Yes | ❌ No | ❌ No |
| `develop` | ✅ Yes | ✅ Yes | ❌ No | ❌ No |
| `feature/*` | ❌ No | 🟡 Optional | ✅ Yes | ✅ Yes |
| `fix/*` | ❌ No | 🟡 Optional | ✅ Yes | ✅ Yes |

## 🛠️ Worktree Setup (Tech Lead)

```bash
# Initial setup
git worktree add ../wt-develop develop
git worktree add ../wt-main main

# Create new feature
git worktree add ../wt-feature-M1 -b feature/M1-add-login develop
```

## 🚀 Deployment Flow

```
PR merged to develop
  ↓
GitHub Action: CI + Deploy
  ├── Tests pass
  ├── Build succeeds
  ├── Sync to HF Space
  └── Auto-rollback (if health fails)
       ↓
       HF Space: latest develop code
```

## 📋 Hotfix Process

For critical production bugs:

```bash
git checkout main
git pull origin main
git checkout -b hotfix/critical-bug main
# Fix the bug
git commit -m "fix: critical prod issue"
git push origin hotfix/critical-bug

# Open PR: hotfix/* → main
# After merge: also merge to develop
git checkout develop
git merge main
git push origin develop
```

## 🎯 Sprint-4 follow-up Status

- ✅ Branch protection: `main` + `develop` (DEC-052)
- ✅ PR template
- ✅ This document
- ✅ AGENTS.md updated (DEC-030 + branching note)