# COMPUTE CODEX DIALOGUE AUDIT

Status: AUDIT COMPLETE
Snapshot: 2026-05-15T12:30+04:00
Agent: COMPUTE_LOGISTICS_AUDITOR
Source: `.codex/logs_2.sqlite`, `.codex/state_5.sqlite`, `.codex/sessions/**/*.jsonl`

## Boundary

This audit measures dialogue/log topology. It does not replace token accounting.

Evidence classes:
- `logs_2.sqlite` schema/counts: exact where indexed or bounded.
- `logs_2.sqlite` target/level samples: recent 50,000 rows only.
- JSONL dialogue topology: marker-based string scan over session files, not full semantic JSON AST.

Reason: full semantic grouping over `logs_2.sqlite` and full JSON parse over 8GB JSONL both hit timeout. The honest answer is bounded evidence, not fake precision.

## Local Forensic Surface

| Artifact | Bytes | GiB |
|---|---:|---:|
| `.codex/sessions/**/*.jsonl` | 8,165,855,838 | 7.605 |
| `.codex/logs_2.sqlite` | 3,260,547,072 | 3.037 |
| `.codex/logs_2.sqlite-wal` | 6,217,112 | 0.006 |
| `.codex/state_5.sqlite` | 4,571,136 | 0.004 |
| `.codex/state_5.sqlite-wal` | 4,548,512 | 0.004 |
| Total listed surface | 11,441,805,206 | 10.656 |

The `.codex` evidence surface is already larger than many small game repositories. It is not "logs". It is a local forensic warehouse.

## `logs_2.sqlite` Schema Facts

| Metric | Value |
|---|---:|
| Main DB size | 3,260,547,072 bytes |
| WAL size | 6,217,112 bytes |
| Journal mode | WAL |
| Page size | 4,096 |
| Page count | 796,032 |
| Log rows | 474,415 |
| Rows with `thread_id` | 467,415 |
| Rows without `thread_id` | 7,000 |
| Thread-bound row share | 98.5245% |
| Distinct `thread_id` values in logs | 871 |
| Average rows per logged thread | 536.64 |
| Threads with exactly 1,000 log rows | 298 |
| Threads with >=900 log rows | 309 |
| Earliest indexed timestamp | 2026-05-04T19:42:37Z |
| Latest indexed timestamp | 2026-05-15T08:29:02Z |

The 1,000-row plateau is not natural. It indicates capped retention or capped export per thread. Therefore `logs_2.sqlite` is useful for recent/thread-local trace shape, not complete historical attribution.

## `logs_2.sqlite` Indexes

| Index | Columns / Purpose |
|---|---|
| `idx_logs_thread_id` | `thread_id` |
| `idx_logs_thread_id_ts` | `thread_id, ts DESC, ts_nanos DESC, id DESC` |
| `idx_logs_ts` | `ts DESC, ts_nanos DESC, id DESC` |
| `idx_logs_process_uuid_threadless_ts` | `process_uuid, ts DESC, ts_nanos DESC, id DESC` where `thread_id is null` |

Missing indexes for this audit:
- no index on `target`;
- no index on `level`;
- no index on `estimated_bytes`;
- no index on `module_path/file`.

That is why global grouping by target/level is expensive and must be treated as a separate offline extraction job.

## Recent 50,000 Log Row Sample

### Level Split

| Level | Rows | Share |
|---|---:|---:|
| `INFO` | 20,353 | 40.706% |
| `TRACE` | 16,590 | 33.180% |
| `WARN` | 8,399 | 16.798% |
| `DEBUG` | 4,575 | 9.150% |
| `ERROR` | 83 | 0.166% |

### Target Split

| Rank | Target | Rows |
|---:|---|---:|
| 1 | `codex_otel.log_only` | 9,429 |
| 2 | `codex_otel.trace_safe` | 8,525 |
| 3 | `codex_api::endpoint::responses_websocket` | 7,129 |
| 4 | `codex_core_skills::loader` | 5,153 |
| 5 | `log` | 3,390 |
| 6 | `codex_core_plugins::manifest` | 2,898 |
| 7 | `codex_core::stream_events_utils` | 2,579 |
| 8 | `codex_api::sse::responses` | 1,668 |
| 9 | `hyper_util::client::legacy::connect::http` | 1,434 |
| 10 | `hyper_util::client::legacy::client` | 1,162 |
| 11 | `feedback_tags` | 1,006 |
| 12 | `hyper_util::client::legacy::pool` | 977 |
| 13 | `codex_core::spawn` | 661 |
| 14 | `codex_core::tools::registry` | 629 |
| 15 | `codex_client::default_client` | 485 |

Recent noise is dominated by telemetry, websocket/SSE transport, skills/plugins loading, and tool routing. It is not primarily project-specific gameplay text.

## Indexed Day Counts From `logs_2.sqlite`

| Day | Log rows |
|---|---:|
| 2026-05-15 | 34,596 |
| 2026-05-14 | 19,449 |
| 2026-05-13 | 79,197 |
| 2026-05-12 | 64,852 |
| 2026-05-11 | 100,470 |
| 2026-05-10 | 43,378 |
| 2026-05-09 | 40,857 |
| 2026-05-08 | 5,318 |
| 2026-05-07 | 13,736 |
| 2026-05-06 | 33,671 |
| 2026-05-05 | 36,913 |
| 2026-05-04 | 1,698 |

The log DB only covers 2026-05-04 through 2026-05-15. It cannot explain April token burn.

## JSONL Dialogue Topology

Method: marker-based scan over 765 JSONL files, 2,410,138 lines.

| Marker | Count |
|---|---:|
| `response_item` | 1,553,782 |
| `event_msg` | 839,797 |
| `function_call` | 518,303 |
| `function_call_output` | 518,160 |
| `shell_command` marker | 461,485 |
| `token_count` | 359,232 |
| `message` payload marker | 117,609 |
| `role:assistant` | 102,453 |
| `apply_patch` marker | 81,114 |
| `role:user` | 14,473 |
| `task_started` | 7,879 |
| `role:developer` | 5,625 |
| `turn_aborted` | 326 |
| `spawn_agent` marker | 47 |
| `wait_agent` marker | 28 |

Marker caveat: `shell_command` and `apply_patch` are string markers, not exact executed tool-call counts. They can appear in call payloads, outputs, summaries, or quoted text. They are topology evidence, not billing or correctness proof.

## Dialogue Ratios

| Ratio | Value |
|---|---:|
| Sessions with user role marker | 764 |
| Sessions with assistant role marker | 745 |
| Sessions with function-call marker | 733 |
| User role markers per session | 18.92 |
| Assistant role markers per session | 133.93 |
| Function-call markers per session | 677.52 |
| Shell-command markers per session | 603.25 |
| JSONL lines per session | 3,150.51 |
| Assistant markers per user marker | 7.08 |
| Function-call markers per user marker | 35.81 |
| Shell-command markers per user marker | 31.89 |
| Apply-patch markers per user marker | 5.60 |
| Function-call minus function-output markers | 143 |

The shape is not "chat". It is tool-saturated automation. One user marker drives dozens of tool-call markers.

## Most Dialogue-Heavy Threads

### User Markers

| Rank | User markers | Tokens | Model | Thread | Title |
|---:|---:|---:|---|---|---|
| 1 | 228 | 429,064,399 | `gpt-5.4` | `019d67a6-6823-7b82-94f9-a3167b8e0286` | master plan continuation |
| 2 | 190 | 490,407,394 | `gpt-5.4` | `019d6329-de82-74e2-83ca-450539a61cec` | master plan / vegetation-coral implementation |
| 3 | 140 | 163,646,606 | `gpt-5.5` | `019e17f5-c75f-7870-a623-4edfef2022a9` | internet/CLI check |
| 4 | 107 | 208,015,624 | `gpt-5.4` | `019d5454-b1ad-7990-ab44-6f664329bea1` | MCP visibility |
| 5 | 96 | 261,225,332 | `gpt-5.5` | `019dd8d8-8d18-7fd2-8336-334fd3be0e14` | console/UI check |

### Function-Call Markers

| Rank | Function-call markers | Tokens | Model | Thread | Title |
|---:|---:|---:|---|---|---|
| 1 | 5,450 | 518,697,166 | `gpt-5.5` | `019e1859-0e01-77b2-a8c6-b5586ccc5c8c` | console/UI repair |
| 2 | 4,304 | 468,267,072 | `gpt-5.5` | `019dde7c-df90-7791-b4b4-d49c8450a9be` | split monoliths into services |
| 3 | 4,172 | 490,407,394 | `gpt-5.4` | `019d6329-de82-74e2-83ca-450539a61cec` | master plan / vegetation-coral implementation |
| 4 | 4,105 | 349,084,791 | `gpt-5.5` | `019dfc26-b869-7bf3-a254-de3f0a8111e9` | basin detection engine |
| 5 | 3,972 | 429,064,399 | `gpt-5.4` | `019d67a6-6823-7b82-94f9-a3167b8e0286` | master plan continuation |

### Largest JSONL Files

| Rank | Bytes | Tokens | Model | Thread | Title |
|---:|---:|---:|---|---|---|
| 1 | 188,413,863 | 490,407,394 | `gpt-5.4` | `019d6329-de82-74e2-83ca-450539a61cec` | master plan / vegetation-coral implementation |
| 2 | 167,606,705 | 36,822,671 | `gpt-5.2-codex` | `019d727a-767d-7463-9eef-a5d321053833` | Flora next dialog prompt |
| 3 | 82,769,280 | 208,015,624 | `gpt-5.4` | `019d5454-b1ad-7990-ab44-6f664329bea1` | MCP visibility |
| 4 | 73,579,644 | 408,633,638 | `gpt-5.4` | `019dcf19-407b-75f2-99e4-54d0217d9d14` | C# compile blockers |
| 5 | 73,211,432 | 518,697,166 | `gpt-5.5` | `019e1859-0e01-77b2-a8c6-b5586ccc5c8c` | console/UI repair |

Large file size and high token count do not always match. The 167MB Flora prompt thread has only 36.8M tokens in the SQLite ledger. Transcript bulk can come from pasted text and tool output, not only priced model tokens.

## Verdict

The `.codex` dialogue surface is tool-saturated and retention-capped:
- `logs_2.sqlite` has exact indexed value for recent trace shape, but not complete history.
- JSONL has complete session text, but semantic parsing is expensive enough to require bounded passes.
- The strongest honest signal is ratio-based: 14,473 user markers drove 518,303 function-call markers and 461,485 shell-command markers.

This is not normal conversational prompting. It is a high-frequency automation funnel with long-context memory drag.

STATUS: AUDIT COMPLETE.
