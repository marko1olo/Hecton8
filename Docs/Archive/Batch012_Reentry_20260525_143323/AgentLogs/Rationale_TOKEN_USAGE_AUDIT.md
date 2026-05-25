# Rationale_TOKEN_USAGE_AUDIT
Date: 2026-05-23 16:11 Europe/Samara
Status: COMPLETE - STATIC LOCAL TELEMETRY REFRESHED

## Decision 1

Problem: User asked for all-time Codex tokens while old sessions were moved into a cleanup backup and current `.codex` still contains overlapping recent sessions.
Solution: Scan backup `old_sessions`, current `sessions`, and current `archived_sessions`; dedupe by `session_meta.id`; keep highest final `total_tokens` for duplicates.
Rejected Alternatives: Backup-only undercounts May 21-23. Current-only loses older moved sessions. Raw root-sum double-counts overlap.
Scalability potential: Low/Middle/High/Ultra runtime tiers unaffected; this is workflow telemetry.
Hardware Impact: 0 us runtime gain; offline filesystem scan only.

## Decision 2

Problem: Codex JSONL files contain repeated token telemetry snapshots.
Solution: Use final per-session `payload.info.total_token_usage` as the accounting row.
Rejected Alternatives: Summing `last_token_usage` or all `total_token_usage` rows was rejected because it counts the same session growth repeatedly.
Scalability potential: Same method remains valid as session count grows because each session contributes one final cumulative row.
Hardware Impact: 0 us runtime gain; prevents accounting error only.

## Decision 3

Problem: Existing compute token docs were moved into deprecated sanitized documentation bundles, while stable docs still pointed at stale compute-report paths.
Solution: Create `Docs/TOKEN_USAGE_LEDGER.md` as current stable token surface and link the dated report from governance/architecture docs.
Rejected Alternatives: Updating deprecated bundles as current authority was rejected because their path explicitly marks them historical. Chat-only answer was rejected because the user asked to update token documentation.
Scalability potential: Future audits have one active ledger instead of searching archived batches.
Hardware Impact: 0 us runtime gain.

## Decision 4

Problem: "Lines of code" can mean C# only, scripts only, non-test, or broad source including shaders/tools.
Solution: Report separate scopes and designate first-party C# under `Assets/_Project` as the primary answer.
Rejected Alternatives: Single broad count was rejected because it hides shader/tool/data inclusion. Comment-stripped SLOC was rejected because no project-local SLOC standard exists in current docs.
Scalability potential: Scope separation prevents future counter drift from being misread as runtime quality.
Hardware Impact: 0 us runtime gain.

## Decision 5

Problem: Runtime-fix work and active Codex sessions made the 15:05 token/LOC/commit snapshot stale on the same day.
Solution: Refresh JSONL token totals with shared-read streaming, refresh source-line scopes, and refresh Git commit counters before answering.
Rejected Alternatives: Reusing the 15:05 snapshot was rejected because current sessions and pushed commits changed. Querying billing/provider data was unavailable, so the evidence boundary remains local filesystem telemetry.
Scalability potential: Low/Middle/High/Ultra runtime tiers unaffected; this is workflow accounting.
Hardware Impact: 0 us runtime gain; offline filesystem/Git scan only.


## Decision 6 - 2026-05-25 process and token refresh

Problem: User requested careful process triage and all-time token/cost accounting while Unity/Codex agents were running concurrently.
Solution: Use read-only process sampling first; stop only orphaned VBCSCompiler dotnet processes after their Unity parent exited and logs had terminal ExitCode 1; then scan local JSONL telemetry and update ledger/report files.
Rejected Alternatives: Blindly killing Code/Unity/dotnet was rejected because active batch jobs were present. Provider invoice claims were rejected because local JSONL lacks billing ids and exact model ids.
Scalability potential: Low/Middle/High/Ultra runtime tiers unaffected; this is workstation/process hygiene and static telemetry.
Hardware Impact: 0 us game runtime gain. Workstation impact: stopped two orphan compiler servers that were consuming multi-GB RAM and high CPU.

## Decision 7 - 2026-05-25 compile-wall compatibility

Problem: Unity compile logs exposed current-source errors from renamed or wrapped data surfaces: FaunaSensorSuite.maxRayLength and HazardVaultArray missing old NativeArray-like surfaces.
Solution: Keep the current Fauna serialized migration routed to maxProbeLength and add/retain HazardVaultArray compatibility surfaces so existing callsites compile without reverting other agents' DataVault direction.
Rejected Alternatives: Reverting foreign patches was rejected. Broad HazardZoneManager rewrite was rejected because request was process triage, not ownership transfer.
Scalability potential: Runtime tiers unchanged; wrapper preserves DataVault route and avoids new allocation path.
Hardware Impact: 0 us measured; compile unblock only, profiler proof absent.

## Decision 8 - 2026-05-25 final process boundary

Problem: CPU remained high after orphan compiler cleanup, but no Unity/dotnet/csc/MSBuild/VBCSCompiler process was alive.
Solution: Treat remaining load as unrelated live VS Code/node developer workload and leave it running; record build guard as skipped at 96 percent CPU.
Rejected Alternatives: Killing responsive VS Code renderers or active `dental-crm` node dev servers was rejected because they were not orphaned and the user asked for careful repair.
Scalability potential: Low/Middle/High/Ultra runtime tiers unaffected; this is workstation hygiene.
Hardware Impact: 0 us game runtime gain; avoids workspace disruption.


## Decision 9 - 2026-05-25 model-price forensics

Problem: User requested more exact model pricing, but local Codex JSONL does not expose invoice SKU, subscription handling, or priority tier, and several exact model labels lack public rate rows.
Solution: Attribute tokens to exact structural model labels, price labels with official standard rates, and isolate known-but-unpriced labels into explicit bounds.
Rejected Alternatives: Treating every session as gpt-5.5 or every session as gpt-5.3-codex was rejected because it hides the evidence boundary.
Scalability potential: Low/Middle/High/Ultra runtime tiers unaffected; this is local telemetry accounting.
Hardware Impact: 0 us runtime gain.

## Decision 10 - 2026-05-25 documentation shape

Problem: Token docs risk becoming scattered across dated reports, ledger, and chat-only claims.
Solution: Keep `Docs/TOKEN_USAGE_LEDGER.md` as the stable summary and `Docs/Reports/TOKEN_USAGE_AUDIT_2026-05-25.md/.json` as the full forensic artifact.
Rejected Alternatives: Creating another standalone model-only report was rejected as documentation sprawl.
Scalability potential: Future audits have one stable entry point and one dated evidence artifact.
Hardware Impact: 0 us runtime gain.
