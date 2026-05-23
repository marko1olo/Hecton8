# 2026-05-23 Token Usage, Codebase, And Commit Counters

Date: 2026-05-23 16:11 Europe/Samara
Status: STATIC LOCAL SNAPSHOT

Evidence boundary: filesystem JSONL, filesystem source scan, and local Git metadata only. No Unity import, compile, profiler, player build, or billing-provider API was run.

## Token Usage

Method: parsed backup and current Codex session JSONL files, used final per-session `total_token_usage`, deduplicated by `session_meta.id`, and rejected naive summing of repeated token telemetry rows.

| Counter | Value |
|---|---:|
| JSONL files scanned | 2,624 |
| Unique session/path keys | 2,624 |
| Sessions with token usage | 2,599 |
| Duplicate records removed | 0 |
| Total tokens | 97,306,917,423 |
| Input tokens | 96,964,451,083 |
| Cached input tokens | 93,059,472,512 |
| Output tokens | 341,949,540 |
| Reasoning output tokens | 108,625,471 |

Selected-root totals after dedupe:

| Root | Selected sessions with usage | Total tokens |
|---|---:|---:|
| `C:\Users\danat\Documents\CodexBackups\codex_cleanup_20260521_194850\old_sessions` | 1,030 | 58,206,516,468 |
| `C:\Users\danat\.codex\sessions` | 1,568 | 39,100,243,852 |
| `C:\Users\danat\.codex\archived_sessions` | 1 | 157,103 |

## Code Lines

Line count type: physical lines, including blank and comment lines. Generated/Unity transient folders such as `.git`, `Library`, `Temp`, `obj`, `bin`, and build folders were excluded.

Primary answer for "lines of code" in first-party project C#: `1,707,768`.

| Scope | Files | Physical lines |
|---|---:|---:|
| C# under `Assets/_Project` | 2,446 | 1,707,768 |
| C# under `Assets/_Project/Scripts` | 2,369 | 1,683,066 |
| Non-test C# under `Assets/_Project` | 2,411 | 1,697,582 |
| First-party source under `Assets/_Project` plus `Tools`, excluding JSON | 3,047 | 1,866,086 |
| First-party source under `Assets/_Project` plus `Tools`, including JSON | 3,082 | 1,894,639 |

Broad first-party source excluding JSON breaks down as:

| Extension | Files | Physical lines |
|---|---:|---:|
| `.cs` | 2,456 | 1,709,756 |
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
| Commits reachable from `HEAD` | 744 |
| Commits reachable from `origin/main` | 744 |
| Commits reachable from all refs | 756 |

The all-refs count includes commits outside the current `main` history.

## Notes

The working tree had many unrelated modified/untracked files before this audit. This report only records counters and token documentation updates; it does not certify compile, runtime, or scene wiring state.
