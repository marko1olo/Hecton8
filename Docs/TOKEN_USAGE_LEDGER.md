# Codex Token Usage Ledger

Date: 2026-05-23 15:00 Europe/Samara
Status: CURRENT STATIC LOCAL TELEMETRY SNAPSHOT

This file is the current stable token accounting surface. Older compute-audit token reports under `Docs/DEPRECATED/Documentation_Bundles_2026-05-21_SANITIZED/2026-05-16_COMPUTE_AUDIT/` and archived `TOKEN_LEDGER_AUDIT` files are historical snapshots only.

## Current Total

Scope: old backup sessions from `C:\Users\danat\Documents\CodexBackups\codex_cleanup_20260521_194850\old_sessions`, current `C:\Users\danat\.codex\sessions`, and current `C:\Users\danat\.codex\archived_sessions`.

Accounting rule: parse each JSONL, take the final per-session `payload.info.total_token_usage`, deduplicate by `session_meta.id`, and keep the record with the highest final `total_tokens` when duplicate session files exist. Do not sum repeated `last_token_usage` rows.

| Metric | Value |
|---|---:|
| Unique session/path keys | 2,522 |
| Sessions with token usage | 2,497 |
| Sessions without token usage | 25 |
| Duplicate session records removed | 94 |
| Files missing session id | 2 |
| JSON parse/read errors | 0 |
| First selected timestamp UTC | 2026-04-03T17:10:34.947Z |
| Last selected timestamp UTC | 2026-05-23T10:58:49.936Z |
| Total tokens | 87,322,244,824 |
| Input tokens | 87,014,515,378 |
| Cached input tokens | 83,515,560,960 |
| Output tokens | 307,212,646 |
| Reasoning output tokens | 99,043,826 |

`cached_input_tokens` is a telemetry subcounter of input-token reuse, not an extra token class to add on top of `total_tokens`.

## Root Breakdown

| Root | JSONL files | Files with token usage | Selected sessions with usage | Selected total tokens |
|---|---:|---:|---:|---:|
| Backup old sessions | 1,048 | 1,030 | 1,002 | 57,856,335,910 |
| Current sessions | 1,567 | 1,560 | 1,494 | 29,465,751,811 |
| Current archived sessions | 1 | 1 | 1 | 157,103 |

Raw pre-dedup totals are not authoritative because backup/current roots overlap.

## Historical Delta

Prior archived JSONL HECTON/Hades final-token snapshot from 2026-05-18: `54,468,241,841`.

Current deduped all-root snapshot: `87,322,244,824`.

Delta since that archived snapshot: `32,854,002,983` tokens.

## Evidence Boundary

Evidence class: static local filesystem telemetry. This is not billing-provider proof and not a runtime/Unity/profiler artifact.

SQLite was inspected for presence and row counts only:

- backup `logs_2.sqlite`: 837,604 rows in `logs`
- current `logs_2.sqlite`: 250,883 rows in `logs`
- current `state_5.sqlite`: 2,577 rows in `threads`

SQLite was not used as the primary sum to avoid duplicate log-row accounting. JSONL session telemetry exposes final cumulative token counters directly.
