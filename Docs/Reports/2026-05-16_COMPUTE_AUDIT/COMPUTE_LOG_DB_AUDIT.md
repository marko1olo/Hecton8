# COMPUTE LOG DB AUDIT

Status: AUDIT COMPLETE
Snapshot: 2026-05-16T06:04+04:00
Source: `C:\Users\danat\.codex\logs_2.sqlite`
Boundary: This is Codex telemetry/log volume, not token usage and not billing.

## Database Metadata

| Metric | Value |
|---|---:|
| SQLite file size | 3,569,434,624 bytes |
| WAL size | 406,367,992 bytes |
| SHM size | 819,200 bytes |
| Page count | 871,444 |
| Page size | 4,096 bytes |
| Freelist pages | 4,213 |
| `PRAGMA quick_check` | ok |
| Rows in `logs` | 486,917 |
| `sum(estimated_bytes)` | 2,970,778,869 |
| Earliest log timestamp | 2026-05-06T01:16:34+04:00 |
| Latest log timestamp | 2026-05-16T05:59:49+04:00 |

Full grouping over the whole 3.57GB DB timed out at 120 seconds. Treat global per-target/per-thread counts from this DB as a separate indexed/offline job. The audit below uses metadata plus the latest 5,000 rows.

## Latest 5,000-Row Sample

Sample rows: latest 5,000 by descending `id`.
Sample window: 2026-05-16T06:01:18+04:00 to 2026-05-16T06:04:21+04:00.
Sample estimated bytes: 33,181,823.

### Level Split

| Level | Rows |
|---|---:|
| INFO | 2,277 |
| TRACE | 1,972 |
| WARN | 438 |
| DEBUG | 305 |
| ERROR | 8 |

### Top Targets

| Target | Rows |
|---|---:|
| `codex_otel.log_only` | 1,258 |
| `codex_otel.trace_safe` | 867 |
| `codex_api::endpoint::responses_websocket` | 776 |
| `codex_api::sse::responses` | 489 |
| `log` | 393 |
| `codex_core_skills::loader` | 260 |
| `codex_core::stream_events_utils` | 154 |
| `codex_core_plugins::manifest` | 147 |
| `hyper_util::client::legacy::client` | 108 |
| `hyper_util::client::legacy::connect::http` | 106 |

### Top Thread Log Rows In Sample

| Rank | Rows | Estimated bytes | Thread title | Thread tokens |
|---:|---:|---:|---|---:|
| 1 | 1,000 | 1,581,183 | Add procedural IK ladder climb | 26,792,437 |
| 2 | 1,000 | 1,506,617 | Build marine snow advection | 31,460,971 |
| 3 | 599 | 2,164,054 | Add Verlet tow cable physics | 37,026,675 |
| 4 | 296 | 6,991,046 | Add indirect flora drawing | 31,048,504 |
| 5 | 289 | 3,689,348 | Add spline docking autopilot | 17,150,968 |
| 6 | 96 | 2,123,143 | Add VR foveated rendering | 19,839,856 |
| 7 | 86 | 1,175,846 | Build hull repair engine | 28,172,557 |
| 8 | 78 | 2,273,274 | Add sensory input to boid shader | 27,997,510 |
| 9 | 63 | 646,540 | Automate H8Memory lifecycle | 27,686,173 |
| 10 | 48 | 1,259,998 | Implement 300-frame state hashing | 25,455,735 |

The 1,000-row values are sample caps, not proof that those threads have exactly 1,000 log rows globally.

## Findings

1. `logs_2.sqlite` is no longer a small side artifact. With WAL included, it occupies about 3.98GB on disk.
2. The latest telemetry sample is dominated by Codex internal telemetry and response streaming targets, not project code.
3. Recent log volume is concentrated in active gpt-5.5 work threads, matching the live token-tail sample directionally.
4. Full forensic grouping needs an indexed/export pass; ad-hoc global grouping is too slow for interactive audit.

## Verdict

`logs_2.sqlite` is useful for operational noise and active-thread evidence. It is not the source of cost truth. Token/cost truth remains the JSONL usage ledger plus SQLite `threads.tokens_used`.

STATUS: AUDIT COMPLETE.
