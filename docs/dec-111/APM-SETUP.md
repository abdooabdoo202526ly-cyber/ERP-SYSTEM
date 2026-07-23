# DEC-111: APM Setup (Lightweight)

**Date**: 2026-07-10
**Status**: ✅ Lightweight APM + Sentry-ready
**Defense Layer**: DL 93

## Approach

Pragmatic APM via existing stack — no external APM SDK required:

| Layer | Mechanism | Purpose |
|---|---|---|
| Logging | Serilog (already in use) | Structured JSON logs (Loki/Elasticsearch compatible) |
| Request Timing | Custom middleware | Slow request warnings (>1s) + error tracking |
| Error Tracking | Global exception middleware (DEC-100) | Surfaces DI/runtime errors |
| Sentry | Config-ready, no-op until DSN set | Drop-in upgrade path |

## Added

### 1. RequestTimingMiddleware

`src/backend/Host/Middleware/RequestTimingMiddleware.cs`

- Stopwatch per request
- Warns on requests > 1 second
- Logs all 4xx/5xx with timing
- Structured fields: Method, Path, StatusCode, ElapsedMs

### 2. Sentry DSN config support

`Sentry:Dsn` in appsettings.json. If set, enriches logs with DSN for Sentry forwarding.
If not set → no-op (current state).

```json
{
  "Sentry": {
    "Dsn": "https://xxx@sentry.io/yyy"
  }
}
```

## Why Not Sentry.AspNetCore

- Adds 2.4 MB dependency
- Risk of API breaking changes
- Existing Serilog already captures errors
- Can be added later without code refactor (just install package + add `app.UseSentry()`)

## Future Upgrade Path

To enable Sentry:
1. `dotnet add package Sentry.AspNetCore`
2. Add `app.UseSentry(o => o.Dsn = config["Sentry:Dsn"])` to pipeline
3. Deploy with `Sentry__Dsn` env var

## Build

✅ dotnet build PASS

## Defense Layer

- DL 93: APM configured (lightweight, upgrade-ready)
