# 🚨 ROLLBACK RUNBOOK

> Emergency procedures for ERP-SYSTEM production issues.
> Sprint-4 follow-up (DEC-051).


## 🧪 Local Development Workflow (DEC-054)

### قبل كل push:

```bash
# Unit tests + frontend build (~30 sec)
./scripts/local-verify.sh

# Integration tests with Docker (~2 min)
./scripts/local-integration.sh
```

### Push:

```bash
git push origin <branch>
# → CI Fast runs (2-3 min, NO deploy)
```

### Deploy (when needed):

- ✅ **Auto**: PR merge to develop → CI + Deploy workflow
- ⚠️ **Manual**: GitHub Actions → "CI + Deploy" → Run workflow

### Test Pyramid Summary:

| Tier | Where | When | Time |
|---|---|---|---|
| Unit | Local | Before push | 30s |
| Integration | CI Fast (every push) | Auto | 2-3m |
| Smoke | CI Deploy (on HF) | Merge to develop | 10-15m |

---


---

## When Auto-Rollback Triggers

The `auto-rollback` job in `.github/workflows/ci-deploy.yml` automatically:

1. Waits 5 minutes for HF Space to build + start
2. Health checks `/api/health/live` (6 retries × 30s)
3. On failure → creates a GitHub issue documenting the incident

**The issue contains**:
- Current SHA (broken commit)
- Previous SHA (rollback target)
- Timestamp + trigger info
- Manual rollback command

---

## 🔍 Step-by-Step Recovery

### 1. Verify the Issue

```bash
# Check the issue auto-created by the workflow
gh issue list --label rollback --state open

# Or visit: https://github.com/anas600/ERP-SYSTEM/issues?q=is%3Aissue+label%3Arollback
```

### 2. Read Deep Diagnostics

```bash
# Liveness — is the process alive?
curl -sI https://anas-assaket-erp-system.hf.space/api/health/live

# Startup — did the process start?
curl -s https://anas-assaket-erp-system.hf.space/api/health/startup | jq

# Deep — DB + migrations + config
curl -s https://anas-assaket-erp-system.hf.space/api/health/startup-deep | jq
```

### 3. Check HF Space Logs

Visit: https://huggingface.co/spaces/Anas-Assaket/erp-system → Logs tab

Common failure patterns:
- `Build failed` → Dockerfile or build issue
- `App started but crashed` → runtime exception
- `Cannot connect to DB` → connection string or Neon outage

### 4. Manual Rollback (if auto-rollback didn't work)

```bash
# Find last working commit
git log --oneline -10

# Force push the previous good SHA to HF Space
PREV_SHA=339d26a   # replace with actual good SHA
git push --force \
  https://USER:TOKEN@huggingface.co/spaces/Anas-Assaket/erp-system \
  ${PREV_SHA}:main
```

### 5. Fix Forward (preferred over rollback)

```bash
# 1. Identify the bug from logs
# 2. Fix in develop branch
git checkout develop
# ... edit files ...
git add .
git commit -m "fix: [describe the bug]"
git push origin develop

# 3. CI + Deploy will run automatically:
#    tests → sync-to-hf → auto-rollback verifies health
```

---

## 🚨 Health Check Failure Indicators

| Indicator | Likely Cause |
|-----------|--------------|
| `database.status != "healthy"` | Neon outage, wrong connection string, or DB pool exhausted |
| `seed_al_burj_default != false` | Seeder flag regression — DEC-009 incident risk |
| `db_connection_set != true` | `DB_CONNECTION` env var missing in HF Space |
| `jwt_secret_set != true` | `JwtSettings__Secret` env var missing or empty |
| `runtime.stage = RUNNING_APP_STARTING` (forever) | HF build stuck — check Space logs |
| `runtime.stage = CRASHED` | App crashed during startup |

---

## 🛠️ Manual Health Check Commands

```bash
# Live (quick check)
curl -I https://anas-assaket-erp-system.hf.space/api/health/live

# Startup (with uptime)
curl -s https://anas-assaket-erp-system.hf.space/api/health/startup | jq

# Deep (full diagnostics)
curl -s https://anas-assaket-erp-system.hf.space/api/health/startup-deep | jq
```

---

## 🛡️ Defense Layers (DEC-009 Prevention)

In addition to auto-rollback, the system has 4 layers preventing runaway seeders:

| Layer | Mechanism |
|-------|-----------|
| 1 | Config flag `SeedAlBurjScenario=false` (default) |
| 2 | `Program.cs` doesn't register `AlBurjHostedService` |
| 3 | `POST /api/admin/seed/alburj` returns 501 Not Implemented |
| 4 | `AlBurjSeederHostedService` class doesn't exist |

The AlBurj incident is **architecturally impossible** to recur.

---

## 📡 Observability Tools

- **Structured logs**: Serilog JSON in production (Console + Sentry optional)
- **X-Request-ID**: Every request gets one (header + log context)
- **TenantId/UserId enrichers**: Multi-tenant filtering
- **Sentry** (optional): Set `Sentry__Dsn` env var to enable

To check a specific request across logs:
1. Get the `X-Request-ID` from the response header
2. Search logs for `RequestId=<value>`

---

## 🆘 Escalation

If rollback doesn't restore service:

1. **Check HF Space status**: https://huggingface.co/spaces/Anas-Assaket/erp-system
2. **Check Neon DB**: https://console.neon.tech (eu-central-1)
3. **Check GitHub Actions**: https://github.com/anas600/ERP-SYSTEM/actions
4. **Manual HF Space restart**: Settings → Factory reboot (last resort)

---

## 📋 Quick Reference

| Action | Command |
|--------|---------|
| Health (live) | `curl -I https://anas-assaket-erp-system.hf.space/api/health/live` |
| Health (deep) | `curl -s https://anas-assaket-erp-system.hf.space/api/health/startup-deep` |
| List rollback issues | `gh issue list --label rollback` |
| Last 5 commits | `git log --oneline -5` |
| Manual rollback | `git push --force https://USER:TOKEN@huggingface.co/spaces/Anas-Assaket/erp-system SHA:main` |
| Re-run workflow | `gh workflow run ci-deploy.yml` |

---

## 🎯 When to Use Each Recovery Path

| Situation | Action |
|-----------|--------|
| Health endpoint returns 404 | Wait 5 min, re-check (HF build might still be in progress) |
| Health returns 200 but UI broken | Frontend issue — rollback likely won't help; check Next.js logs |
| Health returns 500 | Auto-rollback triggered; check the created issue |
| Auth/login broken | Check `JwtSettings__Secret` env var in HF Space |
| DB queries failing | Check Neon status; verify `DB_CONNECTION` is correct |
| App crashes on startup | Check HF Space logs; look for stack trace |

---

**Maintainer**: Mavis (Tech Lead)
**Last updated**: 2026-07-06 (DEC-051)
**Sprint**: 4 follow-up — Improvement 4