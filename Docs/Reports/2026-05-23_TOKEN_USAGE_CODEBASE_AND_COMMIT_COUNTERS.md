# 2026-05-23 Token Usage, Codebase, And Commit Counters

Date: 2026-05-23 15:05 Europe/Samara
Status: STATIC LOCAL SNAPSHOT

Evidence boundary: filesystem JSONL, filesystem source scan, and local Git metadata only. No Unity import, compile, profiler, player build, or billing-provider API was run.

## Token Usage

Method: parsed backup and current Codex session JSONL files, used final per-session `total_token_usage`, deduplicated by `session_meta.id`, and rejected naive summing of repeated token telemetry rows.

| Counter | Value |
|---|---:|
| JSONL files scanned | 2,616 |
| Unique session/path keys | 2,522 |
| Sessions with token usage | 2,497 |
| Duplicate records removed | 94 |
| Total tokens | 87,322,244,824 |
| Input tokens | 87,014,515,378 |
| Cached input tokens | 83,515,560,960 |
| Output tokens | 307,212,646 |
| Reasoning output tokens | 99,043,826 |

Selected-root totals after dedupe:

| Root | Selected sessions with usage | Total tokens |
|---|---:|---:|
| `C:\Users\danat\Documents\CodexBackups\codex_cleanup_20260521_194850\old_sessions` | 1,002 | 57,856,335,910 |
| `C:\Users\danat\.codex\sessions` | 1,494 | 29,465,751,811 |
| `C:\Users\danat\.codex\archived_sessions` | 1 | 157,103 |

## Code Lines

Line count type: physical lines, including blank and comment lines. Generated/Unity transient folders such as `.git`, `Library`, `Temp`, `obj`, `bin`, and build folders were excluded.

Primary answer for "lines of code" in first-party project C#: `1,701,001`.

| Scope | Files | Physical lines |
|---|---:|---:|
| C# under `Assets/_Project` | 2,422 | 1,701,001 |
| C# under `Assets/_Project/Scripts` | 2,345 | 1,676,299 |
| Non-test C# under `Assets/_Project` | 2,315 | 1,665,540 |
| First-party source under `Assets/_Project` plus `Tools`, excluding JSON | 3,015 | 1,859,225 |
| First-party source under `Assets/_Project` plus `Tools`, including JSON | 3,041 | 1,887,259 |

Broad first-party source excluding JSON breaks down as:

| Extension | Files | Physical lines |
|---|---:|---:|
| `.cs` | 2,424 | 1,702,895 |
| `.shader` | 148 | 40,377 |
| `.compute` | 46 | 13,561 |
| `.hlsl` | 12 | 5,831 |
| `.asmdef` | 178 | 3,930 |
| `.py` | 194 | 85,002 |
| `.ps1` | 13 | 7,629 |

## Git Commits At Audit Capture

These counters were captured before publishing this documentation report. Use live `git rev-list` output for the post-commit count.

| Counter | Value |
|---|---:|
| Current branch | `main` |
| Commits reachable from `HEAD` | 735 |
| Commits reachable from `origin/main` | 735 |
| Commits reachable from all refs | 747 |

The all-refs count includes commits outside the current `main` history.

## Notes

The working tree had many unrelated modified/untracked files before this audit. This report only records counters and token documentation updates; it does not certify compile, runtime, or scene wiring state.
