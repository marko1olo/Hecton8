# Status TOKEN_USAGE_AUDIT
Date: 2026-05-23 16:11 Europe/Samara
Status: COMPLETE - STATIC TELEMETRY/DOCS REFRESHED

- [x] Task 1 - Read prior archive state | Justification: read Batch012 archive status/rationale and Batch009 token-ledger audit before changing docs; DOD practice was evidence continuity. Alternative rejected: chat-only recount without prior-method check. Microseconds saved: 0 audit-only.
- [x] Task 2 - Count Codex tokens | Justification: scanned backup/current JSONL roots, used final per-session `total_token_usage`, and deduped by session id; DOD practice was no repeated telemetry overcount. Alternative rejected: summing `last_token_usage`. Microseconds saved: 0 audit-only.
- [x] Task 3 - Count source lines and Git commits | Justification: counted physical lines in explicit first-party scopes and queried Git commit counts for HEAD/origin/all refs; DOD practice was scope-separated metrics. Alternative rejected: one vague line total mixing code, data, docs, and generated folders. Microseconds saved: 0 audit-only.
- [x] Task 4 - Update token documentation | Justification: added stable `Docs/TOKEN_USAGE_LEDGER.md`, dated report, and stable-doc pointers. Alternative rejected: editing deprecated compute bundle as current authority. Microseconds saved: 0 audit-only.
- [x] Task 5 - Record audit log/rationale | Justification: active status/rationale/log created for this telemetry pass. Alternative rejected: leaving only terminal output. Microseconds saved: 0 audit-only.

## Refresh 2026-05-23 16:11 Europe/Samara

- [x] Task 6 - Re-scan JSONL token roots | Justification: used shared-read streaming over backup/current JSONL roots, including locked current session files; DOD practice was final per-session `total_token_usage`. Alternative rejected: stale 15:05 ledger values. Microseconds saved: 0 audit-only.
- [x] Task 7 - Re-count project source lines | Justification: counted physical lines in the same scoped first-party buckets as the prior report. Alternative rejected: mixing docs/data/generated/transient folders into the primary LOC answer. Microseconds saved: 0 audit-only.
- [x] Task 8 - Re-count Git commits | Justification: queried live `git rev-list` for HEAD, origin/main, and all refs after runtime-fix pushes. Alternative rejected: keeping pre-fix commit counts. Microseconds saved: 0 audit-only.
- [x] Task 9 - Update stable token docs | Justification: refreshed `Docs/TOKEN_USAGE_LEDGER.md`, the dated counter report, governance, and architecture pointers. Alternative rejected: chat-only answer. Microseconds saved: 0 audit-only.
- [x] Task 10 - Record refresh boundary | Justification: logs/rationale now state this is static local filesystem/Git telemetry, not billing/provider proof. Alternative rejected: presenting local JSONL as invoice-grade accounting. Microseconds saved: 0 audit-only.


## Refresh 2026-05-25 07:09 Europe/Samara

- [x] Task 11 - Process health triage | Justification: sampled Code/Unity/dotnet/node/python process deltas and main-window responsiveness before stopping anything; DOD practice was objective process evidence. Alternative rejected: blind mass kill. Microseconds saved: 0 runtime; reclaimed orphan compiler CPU/RAM outside game runtime.
- [x] Task 12 - Stop orphan compiler servers | Justification: stopped only VBCSCompiler dotnet PIDs whose Unity parent had exited and whose compile log had terminal ExitCode 1. Alternative rejected: killing active Unity/csc jobs. Microseconds saved: 0 game runtime; workstation contention reduced.
- [x] Task 13 - Repair observed compile-wall aliases | Justification: current source resolves the Fauna maxRayLength wall through serialized-field/callsite migration to maxProbeLength, and HazardVaultArray through compatibility surfaces without reverting other agents. Alternative rejected: broad refactor or signature churn. Microseconds saved: 0 static compile repair.
- [x] Task 14 - Re-scan token ledger with backup roots | Justification: parsed current and backup JSONL, deduped by session id, and reconstructed day/week deltas. Alternative rejected: stale 2026-05-23 ledger. Microseconds saved: 0 audit-only.
- [x] Task 15 - Refresh token reports and ledger | Justification: wrote stable ledger plus dated md/json reports with price scenarios and LOC ratios. Alternative rejected: chat-only report. Microseconds saved: 0 audit-only.
- [x] Task 16 - Final process guard pass | Justification: verified no Unity/dotnet/csc/MSBuild/VBCSCompiler process remained and VS Code windows were responsive; active node dev servers were left running because they were not orphaned. Alternative rejected: killing unrelated dev servers to lower CPU. Microseconds saved: 0 runtime; avoided false repair.


## Model Forensics Refresh 2026-05-25 07:51 Europe/Samara

- [x] Task 17 - Extract structural model labels | Justification: parsed JSONL `turn_context` model fields instead of text-grepping prompts; DOD practice was evidence-class separation. Alternative rejected: inferring model from extension name or prompt text. Microseconds saved: 0 audit-only.
- [x] Task 18 - Add model-specific cost bounds | Justification: priced only model labels with official standard rates and isolated known-but-unpriced labels. Alternative rejected: pretending local JSONL proves billing SKU or priority tier. Microseconds saved: 0 audit-only.
- [x] Task 19 - Add interpretive token statistics | Justification: added concentration, cache-savings, context-window, daily/session distribution, and LOC-cost diagnostics as derived metrics. Alternative rejected: hiding all shape behind one aggregate total. Microseconds saved: 0 audit-only.
- [x] Task 20 - Reorder token documentation | Justification: kept one stable ledger plus one dated report and moved model/interpretive stats into those surfaces. Alternative rejected: creating scattered side reports. Microseconds saved: 0 audit-only.
