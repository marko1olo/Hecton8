

## 2026-05-26 TOKEN_USAGE_AUDIT model-price/statistics refresh

What was wrong -> Prior token report priced broad scenarios but did not separate structurally observed model labels from unknown historical sessions.
What was done -> Added model attribution, model-cost bounds, cache-savings, Pareto/Gini/session/day/context-window/LOC-cost diagnostics, and refreshed ledger/report from 2,803 JSONL files.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Static telemetry and docs only.
Token report -> Docs/Reports/TOKEN_USAGE_AUDIT_2026-05-26.md and .json. Total tokens 99,155,128,232; all-as-gpt-5.3-codex standard API-equivalent $28,209.83; model-bound known+unpriced-as-gpt-5.5 standard $72,474.01.
Evidence -> STATIC_LOCAL_CODEX_JSONL_AND_FILESYSTEM plus official OpenAI pricing pages. Runtime/Unity PlayMode proof absent.

## 2026-05-26 TOKEN_USAGE_AUDIT GPT-5.5 validation close

What was wrong -> GPT-5.5 primary correction needed proof that no old 5.3-primary wording survived in active token surfaces.
What was done -> Validated Python syntax, parsed JSON report, checked stale primary strings, reran documentation structure gate, and checked scoped diff whitespace.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Static telemetry and docs only.
Validation -> py_compile OK; JSON primary_price_key=gpt-5.5_standard_short_context_equivalent; active token docs stale 5.3-primary grep returned no matches; historical log entries still preserve old audit values; VerifyDocStructure pass=true activeDocCount=693 brokenLinkFiles=0 encodingWithoutUtf8Sig=0; git diff --check returned only the existing LF-to-CRLF warning for the script.

## 2026-05-26 TOKEN_USAGE_AUDIT documentation corpus refresh

What was wrong -> Stable docs still pointed at older token/doc boundaries, and fresh Markdown outputs violated the active UTF-8-SIG gate.
What was done -> Made token audit output date-current, regenerated current token ledger/report, updated stable doc entry points, and reran doc validators.
Cinematic Cheats used -> None; documentation/tooling only.
Exact Microseconds saved -> 0 us game runtime.
Current token total -> 99,155,128,232 local Codex JSONL tokens from 2,679 usage sessions across 2,803 JSONL files.
Current source/doc stats -> 2,498 first-party Assets/_Project C# files / 1,795,528 lines; broad source 15,168 files / 12,220,007 lines; active docs 704.
Validation -> VerifyDocStructure pass=true; OOP_Doc_Scanner finalPass=true; superseded 2026-05-25 token audit archived.


## 2026-05-26 TOKEN_USAGE_AUDIT deep statistics refresh

What was wrong -> Existing token report had totals, model forensics, and LOC ratios, but lacked character-level code density, per-period dollar curves, and chat/client concentration metrics requested by the user.
What was done -> Added byte/character/nonblank-line counts, tokens-per-character, dollars-per-line/character, daily/weekly/monthly primary and observed-model cost bounds, CWD/source/originator/plan/CLI breakdowns, and session-level cost columns. Refreshed ledger/report from 2,844 JSONL files.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Static telemetry and docs only.
Token report -> Docs/Reports/TOKEN_USAGE_AUDIT_2026-05-26.md and .json. Total tokens 100,491,276,996; all-as-gpt-5.3-codex standard API-equivalent $28,579.82; observed-model high bound $73,483.33; primary tokens/code-char 1,286.4153.
Evidence -> STATIC_LOCAL_CODEX_JSONL_AND_FILESYSTEM plus official OpenAI pricing pages. Runtime/Unity PlayMode proof absent.

## 2026-05-26 TOKEN_USAGE_AUDIT validation close

What was wrong -> First full run exceeded 304s because broad density counted archived/deprecated docs and generated JSON as current source density.
What was done -> Pruned archive/deprecated traversal, scoped broad source to code/shader/tool extensions, regenerated token report, validated JSON load, section presence, Python syntax, and doc structure.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Audit wall-time reduced; no profiler claim.
Validation -> JSON load OK; `VerifyDocStructure.py` pass=true activeDocCount=693 brokenLinkFiles=0 encodingWithoutUtf8Sig=0; `python -m py_compile Tools/CodexTokenUsageAudit_20260525.py` passed.


## 2026-05-26 TOKEN_USAGE_AUDIT GPT-5.5 primary correction

What was wrong -> The previous primary cost lens was gpt-5.3-codex, while operator correction states GPT-5.5/xhigh is the normal current route.
What was done -> Rebased primary economics to gpt-5.5 standard short-context API-equivalent, added GPT-5.5 priority/batch/flex rows, kept gpt-5.3-codex as secondary, and added xhigh/session/model-effort economics. Refreshed ledger/report from 2,851 JSONL files.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Static telemetry and docs only.
Token report -> Docs/Reports/TOKEN_USAGE_AUDIT_2026-05-26.md and .json. Total tokens 100,614,712,693; all-as-GPT-5.5 standard API-equivalent $78,263.97; xhigh GPT-5.5 standard equivalent $69,994.75; primary tokens/code-char 1,286.7027.
Evidence -> STATIC_LOCAL_CODEX_JSONL_AND_FILESYSTEM plus official OpenAI pricing pages. Runtime/Unity PlayMode proof absent.


## 2026-05-26 TOKEN_USAGE_AUDIT exact model-effort spend matrix

What was wrong -> Prior report could say GPT-5.5 and xhigh separately, but not exact spend for each concrete model+effort combination.
What was done -> Added final-session and delta `model::effort` matrices with rates, spend, cache savings, cost/session, reasoning density, top-bucket share, and unpriced leakage. Refreshed ledger/report from 2,852 JSONL files.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Static telemetry and docs only.
Interesting stats -> total 100,689,779,441; all-as-GPT-5.5 standard $78,321.32; exact gpt-5.5::xhigh $67,712.43; top model-effort bucket gpt-5.5::xhigh share 87.3842%; unpriced model-effort tokens 99,128,081; primary tokens/code-char 1,287.4992.
Evidence -> STATIC_LOCAL_CODEX_JSONL_AND_FILESYSTEM plus official OpenAI pricing pages. Runtime/Unity PlayMode proof absent.

## 2026-05-26 TOKEN_USAGE_AUDIT model-effort validation close

What was wrong -> Exact model-effort spend matrix needed proof that generated JSON, Markdown, and ledger remained parseable after adding dense tables.
What was done -> Parsed JSON model_effort_final_standard_cost_rows, printed all 14 final buckets, reran Python syntax validation, documentation structure validation, and scoped diff whitespace check.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Static telemetry and docs only.
Validation -> py_compile OK; JSON rows=14 top=gpt-5.5::xhigh; VerifyDocStructure pass=true activeDocCount=694 brokenLinkFiles=0 encodingWithoutUtf8Sig=0; git diff --check returned only the existing LF-to-CRLF warning for the script.


## 2026-05-26 TOKEN_USAGE_AUDIT input-output economics

What was wrong -> Prior report did not isolate input/output pressure enough; total tokens were dominated by cached input and hid output/reasoning behavior.
What was done -> Added paid/cached/output ratios, output cost share, reasoning-output cost, effective dollars per 1M output tokens, top output sessions/days, top reasoning sessions/days, and I/O ratios inside model-effort matrices. Refreshed ledger/report from 2,852 JSONL files.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Static telemetry and docs only.
Interesting stats -> input/output 286.98:1; paid-input/output 11.23:1; cached/output 275.75:1; output cost share 13.3926%; reasoning/output 31.5984%; effective GPT-5.5 spend per 1M output tokens $224.00; top output session 019e42c1-57ec-7701-a1d7-7b5fbb073503 output 4,094,167; top reasoning session 019e42fa-4ec0-7e32-8384-f0756a3470c0 reasoning 1,164,831.
Evidence -> STATIC_LOCAL_CODEX_JSONL_AND_FILESYSTEM plus official OpenAI pricing pages. Runtime/Unity PlayMode proof absent.

## 2026-05-26 TOKEN_USAGE_AUDIT input-output validation close

What was wrong -> Input/output economics added dense generated tables and needed parser/doc-gate proof.
What was done -> Validated Python syntax, asserted JSON input_output_stats and top output/reasoning sessions, reran documentation structure validation, and checked scoped diff whitespace.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Static telemetry and docs only.
Validation -> py_compile OK; JSON_IO_OK input/output=286.9791253423206; VerifyDocStructure pass=true activeDocCount=695 brokenLinkFiles=0 encodingWithoutUtf8Sig=0; git diff --check returned only the existing LF-to-CRLF warning for the script.


## 2026-05-26 TOKEN_USAGE_AUDIT code-density economics

What was wrong -> Prior report exposed per-character economics but did not show the requested per-line and per-1000-code-character units directly.
What was done -> Added explicit tokens/line, tokens/1k chars, output tokens/1k chars, GPT-5.5 dollars/1k lines, GPT-5.5 dollars/1k chars, and secondary Codex/observed-high dollars/1k chars to generated reports. Refreshed ledger/report from 2,852 JSONL files.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Static telemetry and docs only.
Interesting stats -> primary code lines 1,815,942; code chars 78,226,198; tokens/line 55,505.35; tokens/1k code chars 1,288,500.43; output tokens/1k code chars 4,474.30; GPT-5.5 dollars/1k LOC $43.17; GPT-5.5 dollars/1k code chars $1.00; input/output 286.98:1; top output session 019e42c1-57ec-7701-a1d7-7b5fbb073503 output 4,094,167; top reasoning session 019e42fa-4ec0-7e32-8384-f0756a3470c0 reasoning 1,164,831.
Evidence -> STATIC_LOCAL_CODEX_JSONL_AND_FILESYSTEM plus official OpenAI pricing pages. Runtime/Unity PlayMode proof absent.

## 2026-05-26 TOKEN_USAGE_AUDIT code-density validation close

What was wrong -> Explicit per-line and per-1000-character units needed parser/doc-gate proof after regeneration.
What was done -> Validated Python syntax, asserted JSON code-density fields, reran documentation structure validation, and checked scoped diff whitespace.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Static telemetry and docs only.
Validation -> py_compile OK; JSON_CODE_DENSITY_OK tokens/line=55505.346231873045 tokens/1k chars=1288500.4259953934 GPT-5.5 $/1k chars=1.0022494011533067; VerifyDocStructure pass=true activeDocCount=695 brokenLinkFiles=0 encodingWithoutUtf8Sig=0; git diff --check returned only the existing LF-to-CRLF warning for the script.

## 2026-05-26 TOKEN_USAGE_AUDIT token verification pass

What was wrong -> The archived report was no longer current after additional Codex JSONL files were written; user requested verification of token facts rather than another interpretation pass.
What was done -> Rechecked official OpenAI pricing/caching/reasoning docs, ran an independent JSONL replay, detected stale report deltas, redirected the generator to the DOCS_ACTUALIZATION archive path, and regenerated `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-26.*` plus `TOKEN_USAGE_LEDGER.md`.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Static telemetry and docs only.
Official rule check -> GPT-5.5 standard short-context API-equivalent rates used: $5.00/1M uncached input, $0.50/1M cached input, $30.00/1M output. GPT-5.5 priority sensitivity: $12.50/$1.25/$75.00. Reasoning effort `xhigh` is not a separate price row; reasoning tokens are treated as output-side usage in local estimates.
Independent replay before regeneration -> stale by +25 JSONL files, +22 usage sessions, +1,600,310,854 total tokens, +5,713,311 output tokens, +1,658,286 reasoning-output tokens.
Regenerated snapshot -> generated Samara 2026-05-26T21:33:27+04:00; file_count 2,877; sessions_with_usage 2,749; total_tokens 102,429,788,087; input 102,072,929,659; cached input 98,080,053,504; output 355,824,828; reasoning output 112,292,502.
Cost snapshot -> GPT-5.5 standard API-equivalent $79,679.15; GPT-5.5 priority sensitivity $199,197.88; gpt-5.3-codex secondary $29,133.09; exact `gpt-5.5::xhigh` standard $69,056.88 across 1,780 sessions and 89,710,233,499 tokens.
Post-regeneration replay -> file/session/duplicate/missing-id/parse-error counters matched exactly. Token totals had live drift of +80,286,278 total tokens because 17 active JSONL files emitted token_count events after report cutoff 2026-05-26T17:35:39.754Z; this is live telemetry movement, not a parser mismatch.
Evidence -> STATIC_LOCAL_CODEX_JSONL_AND_FILESYSTEM plus official OpenAI docs. Still not invoice proof: local JSONL lacks billing SKU, invoice IDs, enterprise discounts, subscription/internal routes.

## 2026-05-27 TOKEN_USAGE_AUDIT late-day validation close

What was wrong -> Late-day regenerated token surfaces and the requested full commit needed parser/doc-gate proof; `VerifyDocStructure.py` exposed three unrelated active report files without UTF-8-SIG.
What was done -> Validated Python syntax, asserted JSON totals/deltas/model-effort owner, ran scoped diff whitespace checks, converted the three active report files to UTF-8-SIG without content changes, and reran documentation structure validation.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Static telemetry and docs only.
Validation -> py_compile OK; JSON_RE_REFRESH_OK total=107773673063 sessions=2801 top=gpt-5.5::xhigh cost=$83835.954913; VerifyDocStructure pass=true activeDocCount=691 brokenLinkFiles=0 encodingWithoutUtf8Sig=0 duplicateHeaderFiles=0; scoped git diff --check passed with only LF-to-CRLF warnings on status/rationale/log files.

## 2026-05-26 TOKEN_USAGE_AUDIT verification validation close

What was wrong -> Verification artifacts needed a final parser/doc-gate close. `VerifyDocStructure.py` also exposed an unrelated duplicate-header/UTF-8-SIG gate in active docs.
What was done -> Fixed the active doc gate without changing token totals, reran Python syntax validation, JSON/BOM/null-byte assertions, scoped diff whitespace check, and documentation structure validation.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Static telemetry and docs only.
Validation -> py_compile OK; JSON_VERIFY_OK total=102429788087 GPT-5.5 standard=$79679.152367; DOC_BYTES_OK for token report/ledger and active doc gate files; VerifyDocStructure pass=true activeDocCount=687 brokenLinkFiles=0 encodingWithoutUtf8Sig=0 duplicateHeaderFiles=0; git diff --check passed with only LF-to-CRLF warnings.

## 2026-05-27 TOKEN_USAGE_AUDIT daily refresh

What was wrong -> The 2026-05-26 snapshot no longer represented the current local Codex telemetry after another day of multi-agent activity.
What was done -> Rechecked official OpenAI pricing/cache/reasoning docs, added generated previous-snapshot delta reporting, and regenerated `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-27.md/.json` plus the archived ledger.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Static telemetry and docs only.
Current snapshot -> generated Samara 2026-05-27T13:47:33+04:00; file_count 2,921; sessions_with_usage 2,788; total_tokens 105,869,637,268; input 105,501,553,097; cached input 101,372,633,216; output 367,050,571; reasoning output 115,519,808.
What changed since 2026-05-26T21:33:27+04:00 -> +44 JSONL files; +39 sessions with usage; +3,439,849,181 total tokens; +3,428,623,438 input; +3,292,579,712 cached input; +11,225,743 output; +3,227,306 reasoning output.
Cost delta -> GPT-5.5 standard API-equivalent +$2,663.28; GPT-5.5 priority sensitivity +$6,658.20; gpt-5.3-codex secondary +$971.44.
Model-effort delta -> top bucket remained `gpt-5.5::xhigh`; +3,410,901,203 tokens; +30 sessions; +$2,638.44 standard API-equivalent.
Current cost snapshot -> GPT-5.5 standard $82,342.43; GPT-5.5 priority $205,856.08; gpt-5.3-codex secondary $30,104.53; exact `gpt-5.5::xhigh` $71,695.32 across 1,810 sessions and 93,121,134,702 tokens.
Code density snapshot -> primary code 1,852,195 lines / 79,718,053 chars; 57,159.01 tokens/line; 1,328,050.97 tokens/1k chars; GPT-5.5 $44.46/1k LOC and $1.03/1k chars.
Evidence -> STATIC_LOCAL_CODEX_JSONL_AND_FILESYSTEM plus official OpenAI docs. Still not invoice proof: local JSONL lacks billing SKU, invoice IDs, enterprise discounts, subscription/internal routes.

## 2026-05-27 TOKEN_USAGE_AUDIT daily refresh validation close

What was wrong -> The refreshed token report needed parser/doc-gate proof before commit and push.
What was done -> Validated Python syntax, asserted JSON totals/deltas/model-effort owner, checked UTF-8-SIG/null-byte state, ran scoped diff whitespace check, fixed four unrelated active report BOM gates, and reran `VerifyDocStructure.py`.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Static telemetry and docs only.
Validation -> py_compile OK; JSON_20260527_OK total=105869637268 GPT-5.5 standard=$82342.433143; DOC_BYTES_20260527_OK; VerifyDocStructure pass=true activeDocCount=680 brokenLinkFiles=0 encodingWithoutUtf8Sig=0 duplicateHeaderFiles=0; git diff --check passed with only LF-to-CRLF warning for the Python script.

## 2026-05-27 TOKEN_USAGE_AUDIT late-day re-refresh

What was wrong -> The 13:47 2026-05-27 snapshot was stale by the evening because active Codex JSONL telemetry kept moving.
What was done -> Rechecked official OpenAI pricing/cache/reasoning pages, regenerated `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-27.md/.json`, refreshed the archived ledger, and prepared commits with explicit 1334 exclusion.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Static telemetry and docs only.
Current snapshot -> generated Samara 2026-05-27T20:42:16+04:00; file_count 2,934; sessions_with_usage 2,801; total_tokens 107,773,673,063; input 107,399,653,529; cached input 103,189,309,056; output 372,985,934; reasoning output 117,263,392.
What changed since 2026-05-26T21:33:27+04:00 -> +57 JSONL files; +52 sessions with usage; +5,343,884,976 total tokens; +5,326,723,870 input; +5,109,255,552 cached input; +17,161,106 output; +4,970,890 reasoning output.
Cost snapshot -> GPT-5.5 standard API-equivalent $83,835.95; GPT-5.5 priority sensitivity $209,589.89; gpt-5.3-codex secondary $30,648.03; exact `gpt-5.5::xhigh` $73,174.62 across 1,817 sessions and 95,008,760,975 tokens.
Cost delta -> GPT-5.5 standard +$4,156.80; GPT-5.5 priority +$10,392.01; gpt-5.3-codex secondary +$1,514.94; top model-effort `gpt-5.5::xhigh` +$4,117.74.
Input/output stats -> input/output 287.95:1; uncached-input/output 11.29:1; cached-input/output 276.66:1; reasoning/output 31.44%; output-side GPT-5.5 standard cost $11,189.58.
Primary code density -> first-party project C# 1,866,854 lines / 80,343,622 chars; 57,730.10 tokens/line; 1,341,409.19 tokens/1k chars; GPT-5.5 $44.91/1k LOC and $1.04/1k chars.
Evidence -> STATIC_LOCAL_CODEX_JSONL_AND_FILESYSTEM plus official OpenAI docs. Still not invoice proof: local JSONL lacks billing SKU, invoice IDs, enterprise discounts, subscription/internal routes.


## 2026-05-27 23:03 Europe/Samara - Token velocity refresh

What was wrong:
- The 20:42 token report was stale after more local Codex JSONL writes.
- Previous report had deltas but did not persist velocity/burn-rate metrics as generated JSON fields.

What was done:
- Rechecked official OpenAI pricing, prompt-cache, and reasoning docs on 2026-05-27.
- Extended `Tools/CodexTokenUsageAudit_20260525.py` with `previous_snapshot_delta.velocity`.
- Regenerated `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-27.json`, matching Markdown report, and stable `TOKEN_USAGE_LEDGER.md`.
- Current snapshot: 108,244,387,543 total tokens; 107,868,828,212 input; 103,642,537,600 cached input; 374,525,731 output; 117,698,876 reasoning output; 2,804 usage sessions; 2,940 JSONL files.

Snapshot delta since `2026-05-26T21:33:27.098408+04:00`:
- +5,814,599,456 total tokens.
- +5,795,898,553 input tokens.
- +5,562,484,096 cached input tokens.
- +18,700,903 output tokens.
- +5,406,374 reasoning output tokens.
- +55 usage sessions.
- +42,022 primary C# lines.
- +1,767,377 primary C# characters.
- +$4,509.34 GPT-5.5 standard API-equivalent.

Velocity and burn-rate:
- 228,009,054.25 tokens/hour; 3,800,150.90 tokens/minute; 63,335.85 tokens/second.
- 5,472,217,302.10 tokens/day pace.
- 733,322.26 output tokens/hour.
- 212,001.23 reasoning output tokens/hour.
- 1,647.82 primary C# lines/hour; 39,547.61 primary C# lines/day pace.
- 69,304.51 primary C# characters/hour; 1,663,308.21 chars/day pace.
- 138,370.36 total tokens per net primary C# line.
- 445.03 output tokens per net primary C# line.
- 3,289,959.90 total tokens per 1k net primary C# characters.
- GPT-5.5 standard API-equivalent burn: $176.83/hour; $4,243.82/day pace; $0.11 per net primary C# line; $2.55 per 1k net primary C# chars.

Cinematic Cheats used:
- None. Audit-only telemetry and documentation generation.

Exact Microseconds saved:
- 0 runtime microseconds. Engineering audit work only; no game-frame path touched.

## 2026-05-28 05:38 Europe/Samara - Project metrics dashboard

What was wrong:
- The full all-time token replay exceeded the 20-minute command window after the overnight workspace surge.
- The existing token report had tables but no persistent visual dashboard.
- Project metrics were token-heavy and did not visualize git churn, file-type load, docs artifacts, or current source density.

What was done:
- Stopped only orphan PID `58052` running `Tools\CodexTokenUsageAudit_20260525.py`.
- Added fast incremental refresh: `Tools/CodexTokenUsageFastRefresh_20260528.py`.
- Added dashboard generator: `Tools/ProjectMetricsDashboard_20260528.py`.
- Generated `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_AUDIT_2026-05-28.json` and `.md`.
- Refreshed `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/TOKEN_USAGE_LEDGER.md`.
- Generated `Docs/Reports/PROJECT_METRICS_DASHBOARD_2026-05-28.md` and `.json`.
- Generated 29 PNG charts in `Docs/Reports/MetricCharts/2026-05-28/`.

Current token snapshot:
- Total tokens: 110,159,445,798.
- Input tokens: 109,776,871,191.
- Cached input tokens: 105,482,603,520.
- Output tokens: 381,541,007.
- Reasoning output tokens: 119,780,195.
- Usage sessions: 2,856.
- GPT-5.5 standard API-equivalent: $85,658.87.

Delta since previous snapshot:
- +1,915,058,255 total tokens.
- +1,908,042,979 input tokens.
- +1,839,065,920 cached input tokens.
- +7,015,276 output tokens.
- +2,081,319 reasoning output tokens.
- Fast refresh scanned 62 changed JSONL files and found 52 post-cutoff usage sessions.

Velocity:
- 297,297,721.33 tokens/hour.
- 82,582.70 tokens/second.
- $228.26/hour GPT-5.5 standard API-equivalent.
- 2,975.37 primary C# lines/hour.

Charts generated:
- Hourly token, cost, I/O stack, output/reasoning, and ratio charts.
- Daily token, cost, I/O stack, output/reasoning, and ratio charts.
- Weekly token, cost, and I/O charts.
- Model-effort token/cost charts.
- Top sessions and CWD token charts.
- Source scope line and token-density charts.
- File extension count/byte charts.
- Docs artifact count chart.
- Git commit/churn/day/week and weekday-hour heatmap charts.

Cinematic Cheats used:
- Fast incremental telemetry refresh instead of repeated full JSONL replay under active parallel-agent churn.

Exact Microseconds saved:
- 0 runtime microseconds. Audit-only tooling and documentation.
- Audit wall time avoided: full replay was still running after 20 minutes; fast token refresh completed in about 186 seconds.

## 2026-05-28 12:14 Europe/Samara - Apex verification pass

What was wrong:
- Prior proof was split across chat, command output, generated report files, and git history.
- There was no standalone machine-readable apex verification artifact for TOKEN_USAGE_AUDIT.
- Zero-GC and DataVault claims needed explicit evidence-class downgrades because this domain changed offline Python tooling and docs, not Unity runtime hot paths.

What was done:
- Added `Tools/TokenUsageApexVerification_20260528.py`.
- Generated `Docs/Reports/TOKEN_USAGE_APEX_CPU_SAMPLE_2026-05-28.json`.
- Generated `Docs/Reports/TOKEN_USAGE_APEX_VERIFICATION_2026-05-28.json`.
- Generated `Docs/Reports/TOKEN_USAGE_APEX_VERIFICATION_2026-05-28.md`.
- Generated SHA files for both apex verification outputs.

Proof:
- Final JSON SHA-256: `35e82aea75bb4b2ef9cb79a215add562c21806c2142ecc2220a8c89b57001d24`.
- Markdown SHA-256: `426aa9620f2d766244ca8090a3e9b3b71bd239b61947577cfa3ae034702cece3`.
- Token report JSON SHA-256: `9dabab3032221ffb823c42c181635b51606de54f1b8b0aec4049f1741856b674`.
- Dashboard JSON SHA-256: `4c68107ee801033f4464640515a4217cbf0fc3813f380ea1564da0374cafb156`.
- Static hot-path symbol hits in owned tooling: 0.
- Static C# hot-path forbidden text hits in owned tooling: 0.
- Static GlobalDataVault/SignalBus/GlobalRegistry route hits in owned tooling code tokens: 0.
- Chart count on disk: 29.
- PNG signature check: all true.
- CPU sample before final Python compile: 20% total CPU, 0 dotnet/csc processes.
- Final compile check: `python -m py_compile Tools\CodexTokenUsageAudit_20260525.py Tools\CodexTokenUsageFastRefresh_20260528.py Tools\ProjectMetricsDashboard_20260528.py Tools\TokenUsageApexVerification_20260528.py`.

Known faults:
- No Unity Editor import, PlayMode, profiler, GCMonitor, player build, RenderDoc, or device capture was run by TOKEN_USAGE_AUDIT.
- Runtime 0 B/frame remains `PENDING_RUNTIME_VERIFICATION_FOR_ANY_RUNTIME_CLAIM`.
- DataVault lock proof is not applicable: TOKEN_USAGE_AUDIT migrated no fields to GlobalDataVault and secured no BufferID constants.
- Workspace was live-dirty from other agents after push; those files are outside TOKEN_USAGE_AUDIT ownership.

Cinematic Cheats used:
- Evidence fake-first: static/doc/hash proof for offline audit tooling instead of pretending a runtime simulation/profiler claim exists.

Exact Microseconds saved:
- 0 runtime microseconds. Audit-only verification.

## 2026-05-28 12:55 Europe/Samara - Polish re-audit and pricing-risk patch

What was wrong:
- Apex verifier paths were date-fixed to `2026-05-28`; that would validate stale artifacts after the next date rollover.
- Token cost headline had base GPT-5.5 API-equivalent pricing but did not surface official long-context surcharge or regional +10% risk as first-class numbers.
- A clean bytecode compile proof was not available during polish because `dotnet` and `VBCSCompiler` were already active.

What was done:
- Updated `Tools/TokenUsageApexVerification_20260528.py` to derive token/dashboard/apex paths from current Samara date.
- Updated `Tools/CodexTokenUsageFastRefresh_20260528.py` and `Tools/CodexTokenUsageAudit_20260525.py` with GPT-5.5 long-context and regional +10% sensitivity rows.
- Updated `Tools/ProjectMetricsDashboard_20260528.py` to show pricing sensitivity in dashboard Markdown/JSON.
- Regenerated `TOKEN_USAGE_AUDIT_2026-05-28`, `TOKEN_USAGE_LEDGER.md`, `PROJECT_METRICS_DASHBOARD_2026-05-28`, 29 charts, and `TOKEN_USAGE_APEX_VERIFICATION_2026-05-28`.

Evidence:
- Token total: `110,775,514,778`.
- Delta tokens since previous snapshot: `2,531,127,235`.
- Tokens/hour: `184,544,812.69385818`.
- GPT-5.5 base API-equivalent: `$86,143.412684`.
- GPT-5.5 long-context cache-aware sensitivity upper bound: `$166,527.892498`.
- GPT-5.5 regional +10% sensitivity: `$94,757.7539524`.
- Chart count: `29`.
- Static hot-path symbol hits in owned tooling: `0`.
- Static C# forbidden text hits in owned tooling: `0`.
- Static global-authority hits in owned tooling: `0`.
- Final JSON SHA-256: `b36fb4fe72ce680d91c1edd4a613a33acfdfade6d3f15615791210086d797433`.
- Markdown SHA-256: `982f18264b0cce389ffd9bb9e542fa84a21c960bf9aed00c6aa3e5b4709192c7`.
- Token report JSON SHA-256: `83c30566ea02958be6d73dc96f9675c10376854cac8d84651872827b3714a3e3`.
- Dashboard JSON SHA-256: `0b571bda9b5a6674019c884750fdbbc3446ef10ddc068b3b51ee641b1259063d`.

Compilation/resource throttling:
- CPU sample before compile proof attempt ended at `2026-05-28T12:45:54.9946069+04:00`.
- CPU: `30.41%`.
- Compiler contention: `dotnet` pid `25684`, `VBCSCompiler` pid `27088`.
- `dotnet build`: not invoked.
- Unity build/import/playmode: not invoked.
- Python bytecode compile: `SKIPPED_BLOCKED_BY_COMPILER_CONTENTION`.

Cinematic cheats and scalability:
- No runtime visual/physics/audio system was changed.
- `GlobalQualityWeight` runtime scaling is not applicable to this offline audit tooling.
- The audit itself improved scalability by carrying base price, surcharge risk, and region uplift without changing gameplay truth or runtime authority.

Exact microseconds saved:
- Runtime: `0 us`.
- Audit wall-time saved versus full replay remains bounded by fast incremental refresh; polish refresh completed with fast report plus dashboard generation, not a full all-time replay.

Known faults:
- Runtime 0 B/frame remains `PENDING_RUNTIME_VERIFICATION_FOR_ANY_RUNTIME_CLAIM`.
- Local JSONL still lacks provider invoice id, billing region, exact per-request long-context classification, enterprise discount, and subscription route.
- No `Docs/AgentLogs/Dump_TOKEN_USAGE_AUDIT.bin` was generated because TOKEN_USAGE_AUDIT hit no runtime crash/NaN fault.

## 2026-05-28 13:12 Europe/Samara - Long-context precision polish

What was wrong:
- Whole-corpus long-context upper bound was safe but too blunt: it did not say whether fresh post-cutoff telemetry actually contained `input_tokens > 272000` events.
- Regional +10% and long-context were separate sensitivities but the combined worst-case row was not explicit.

What was done:
- Added `post_cutoff_long_context_event_count`, `post_cutoff_long_context_event_usage`, and `post_cutoff_long_context_event_surcharge_delta_usd` to `pricing_context_rules`.
- Added `gpt_5_5_long_context_regional_10pct_upper_bound_usd`.
- Regenerated token report, ledger, dashboard, 29 charts, apex report, and SHA files.

Evidence:
- Token total: `110,860,414,953`.
- Input tokens: `110,475,153,995`.
- Cached input tokens: `106,153,131,008`.
- Output tokens: `384,227,358`.
- Reasoning output tokens: `120,535,145`.
- GPT-5.5 base API-equivalent: `$86,213.501179`.
- GPT-5.5 long-context cache-aware upper bound: `$166,663.591988`.
- GPT-5.5 long-context + regional 10% upper bound: `$183,329.95118680003`.
- GPT-5.5 regional +10% sensitivity: `$94,834.85129690001`.
- Post-cutoff long-context-like event count: `0`.
- Post-cutoff long-context surcharge delta: `$0.0`.
- Tokens/hour: `186,601,807.34837893`.
- Final JSON SHA-256: `e7bf9a1f58306005295a0d6e3797f763f53ae98c55032ae5d644be8516c7913f`.
- Markdown SHA-256: `3c18ae6fe23262a73dafc73572b49a843a3be0343f6dc637cc95827fe9a43316`.

Verification:
- `python -m py_compile Tools\CodexTokenUsageAudit_20260525.py Tools\CodexTokenUsageFastRefresh_20260528.py Tools\ProjectMetricsDashboard_20260528.py Tools\TokenUsageApexVerification_20260528.py` passed.
- Static hot-path method scan over owned tooling: `0`.
- Static forbidden C# hot-path text scan over owned tooling: `0`.
- `python Tools\VerifyDocStructure.py`: pass true.
- Scoped `git diff --check`: no whitespace errors.

Compilation/resource throttling:
- CPU sample: `2026-05-28T13:10:11.3131574+04:00`.
- CPU: `28.26%`.
- Compiler processes: `0`.
- `dotnet build`: not invoked.
- Unity build/import/playmode: not invoked.

Known faults:
- All-time exact long-context invoice classification remains absent; only post-cutoff delta-event classification is exact from local JSONL.
- Runtime 0 B/frame remains `PENDING_RUNTIME_VERIFICATION_FOR_ANY_RUNTIME_CLAIM`.
- No `Docs/AgentLogs/Dump_TOKEN_USAGE_AUDIT.bin` was generated; no runtime crash/NaN fault occurred in this offline domain.

## 2026-05-28 13:25 Europe/Samara - Same-day delta integrity patch

What was wrong:
- Fast refresh used the previous dated report as its base even when a current same-day report already existed.
- Therefore repeated same-day "since previous snapshot" velocity could actually mean "since yesterday", which is misleading.

What was done:
- Changed `Tools/CodexTokenUsageFastRefresh_20260528.py` so `find_previous_report()` prefers the existing same-day report before falling back to older dated reports.
- Added `fast_refresh_base_mode` and `previous_snapshot_mode` to the JSON/Markdown surfaces.
- Regenerated token report, ledger, dashboard, 29 charts, apex report, and SHA files.

Evidence:
- Base mode: `same_day_existing_report`.
- Token total: `110,930,291,612`.
- Delta tokens since actual previous same-day snapshot: `69,876,659`.
- Tokens/hour: `286,959,768.73930275`.
- GPT-5.5 base API-equivalent: `$86,268.181008`.
- Chart count: `29`.
- Final JSON SHA-256: `05710ccf8398ec766a129d04a86e37cb28fc2a4f7f2bf586535c49f274b3012c`.

Verification:
- CPU sample before compile: `2026-05-28T13:19:09.5014520+04:00`, CPU `9.98%`, compiler processes `0`.
- `python -m py_compile Tools\CodexTokenUsageAudit_20260525.py Tools\CodexTokenUsageFastRefresh_20260528.py Tools\ProjectMetricsDashboard_20260528.py Tools\TokenUsageApexVerification_20260528.py` passed.
- Static hot-path and forbidden-text scans remain `0`.
- `dotnet build`: not invoked.
- Unity build/import/playmode: not invoked.

Known faults:
- Runtime 0 B/frame remains `PENDING_RUNTIME_VERIFICATION_FOR_ANY_RUNTIME_CLAIM`.
- Local JSONL still cannot prove invoice SKU, region, enterprise discount, or all-time exact long-context classification.
