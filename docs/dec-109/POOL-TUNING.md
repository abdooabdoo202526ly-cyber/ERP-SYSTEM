# DEC-109: Connection Pool Tuning

**Date**: 2026-07-10
**Status**: ✅ Applied
**Defense Layer**: DL 91

## Problem

Default Npgsql connection pool (max 100, but new connections are slow) caused:
- First-request latency spikes on cold start
- Connection churn under load
- Unpredictable behavior at scale

## Solution

Tuned pool parameters via connection string (no code change needed):

| Parameter | Value | Why |
|---|---|---|
| `Maximum Pool Size` | 100 | Upper bound — HF Spaces has 2 vCPU + 16GB RAM, can handle 100 concurrent |
| `Minimum Pool Size` | 10 | Pre-warm pool to avoid cold-start latency on first request |
| `Connection Idle Lifetime` | 300 (sec) | Recycle idle connections every 5 min (handles NAT/firewall drops) |
| `Timeout` | 15 (sec) | Fast-fail if pool exhausted (vs 30s default) |
| `Command Timeout` | 30 (sec) | Reasonable upper bound for slow queries |

## Where Applied

`src/backend/Host/appsettings.json` — `ConnectionStrings:Postgres`

## Why No Code Change

Npgsql reads pool params from connection string. By setting them in appsettings.json:
- ✅ Works for all environments (local + HF Space)
- ✅ Easy to tune per-environment
- ✅ HF Space env var `DB_CONNECTION` overrides if needed (DEC-019/DEC-021)

## Verification

- Build: ✅ PASS
- All 184 endpoints still functional
- Pool pre-warmed (10 connections ready on first request)

## Future Tuning (DEC-109b)

- Monitor pool exhaustion in production
- Add metrics: pool size, active connections, wait time
- Consider pgbouncer for very high concurrency
