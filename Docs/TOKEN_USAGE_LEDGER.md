# Codex Token Usage Ledger

Date: 2026-05-23 16:11 Europe/Samara
Status: CURRENT STATIC LOCAL TELEMETRY SNAPSHOT

This file is the current stable token accounting surface. Older compute-audit token reports under `Docs/DEPRECATED/Documentation_Bundles_2026-05-21_SANITIZED/2026-05-16_COMPUTE_AUDIT/` and archived `TOKEN_LEDGER_AUDIT` files are historical snapshots only.

## Current Total

Scope: old backup sessions from `C:\Users\danat\Documents\CodexBackups\codex_cleanup_20260521_194850\old_sessions`, current `C:\Users\danat\.codex\sessions`, and current `C:\Users\danat\.codex\archived_sessions`.

Accounting rule: parse each JSONL, take the final per-session `payload.info.total_token_usage`, deduplicate by `session_meta.id`, and keep the record with the highest final `total_tokens` when duplicate session files exist. Do not sum repeated `last_token_usage` rows.

| Metric | Value |
|---|---:|
| Unique session/path keys | 2,624 |
| Sessions with token usage | 2,599 |
| Sessions without token usage | 25 |
| Duplicate session records removed | 0 |
| Files missing session id | 2 |
| JSON parse/read errors | 0 |
| First selected timestamp UTC | 2026-04-03T17:11:28.591Z |
| Last selected timestamp UTC | 2026-05-23T12:10:08.950Z |
| Total tokens | 97,306,917,423 |
| Input tokens | 96,964,451,083 |
| Cached input tokens | 93,059,472,512 |
| Output tokens | 341,949,540 |
| Reasoning output tokens | 108,625,471 |

`cached_input_tokens` is a telemetry subcounter of input-token reuse, not an extra token class to add on top of `total_tokens`.

## Root Breakdown

| Root | JSONL files | Files with token usage | Selected sessions with usage | Selected total tokens |
|---|---:|---:|---:|---:|
| Backup old sessions | 1,048 | 1,030 | 1,030 | 58,206,516,468 |
| Current sessions | 1,575 | 1,568 | 1,568 | 39,100,243,852 |
| Current archived sessions | 1 | 1 | 1 | 157,103 |

Raw pre-dedup totals are not authoritative because backup/current roots overlap.

## Historical Delta

Prior archived JSONL HECTON/Hades final-token snapshot from 2026-05-18: `54,468,241,841`.

Current deduped all-root snapshot: `97,306,917,423`.

Delta since that archived snapshot: `42,838,675,582` tokens.

## Evidence Boundary

Evidence class: static local filesystem telemetry. This is not billing-provider proof and not a runtime/Unity/profiler artifact.

SQLite was inspected for presence and row counts in the 15:05 audit only; this 16:11 refresh used JSONL session telemetry because `sqlite3` was not available in the local shell.

- backup `logs_2.sqlite`: 837,604 rows in `logs`
- current `logs_2.sqlite`: 250,883 rows in `logs`
- current `state_5.sqlite`: 2,577 rows in `threads`

SQLite was not used as the primary sum to avoid duplicate log-row accounting. JSONL session telemetry exposes final cumulative token counters directly.
