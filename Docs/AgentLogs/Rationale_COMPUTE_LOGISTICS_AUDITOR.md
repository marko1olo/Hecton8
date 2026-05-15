# Rationale_COMPUTE_LOGISTICS_AUDITOR

## Decision 0 - Evidence Boundary

Problem: The task asks for economic and token accounting over active code, docs, agent logs, and `.codex` history. These are filesystem/static-document facts, not Unity runtime facts.

Solution: Use CLI filesystem scans, byte counts, timestamps, and JSON/JSONL transcript parsing where available. Mark evidence classes explicitly in the final report.

Rejected Alternatives: Treating historical report text as verified truth was rejected because QA_Evidence_Text_Filter_Audit forbids stale proof and unsupported verification language.

Scalability potential: Low devices gain no runtime change. Middle/High/Ultra tiers gain process clarity only by identifying compute waste and report bloat.

Hardware Impact: Runtime gain on i3/MX350 is 0 microseconds because no gameplay code is changed. Process savings are counted separately as avoided audit rework.

## Decision 1 - Prompt Source

Problem: Batch protocol requires extraction from CURRENT_BATCH.md, but the active prompt was supplied inline in chat.

Solution: CLI checked `Docs/Tasks/CURRENT_BATCH.md`; the ID was not found. The inline XML block is therefore the operative assignment and the missing batch entry is recorded as evidence debt.

Rejected Alternatives: Searching archive batches as active authority was rejected because AGENTS.md forbids reading previous-batch logs unless explicitly ordered.

Scalability potential: Keeps the audit bounded to the current task and prevents stale neighboring prompts from polluting metrics.

Hardware Impact: 0 runtime microseconds. Avoids human review time lost to wrong-agent task bleed.

## Decision 2 - LOC Method

Problem: The task requested `cloc`, but `cloc` is not installed in PATH.

Solution: Used a PowerShell CLI scanner over `Assets/_Project/Scripts/**/*.cs`, streaming files line-by-line and subtracting blank plus comment-only lines. Inline comments on code lines were kept as meaningful code lines because they still carry executable source.

Rejected Alternatives: Stale May 13 report counters were rejected because current filesystem churn changed script counts. Pure `wc -l` was rejected because it cannot subtract comments and blanks.

Scalability potential: Low/Middle/High/Ultra runtime tiers are unaffected. Process scalability improves because the audit can be rerun without installing extra tooling.

Hardware Impact: 0 runtime microseconds on i3/MX350. The only gain is audit reproducibility.

## Decision 3 - Domain Weight Classification

Problem: The 85-domain authority map is semantic, while the filesystem is namespace/folder/file based and includes fused legacy hubs.

Solution: Report both namespace-domain weight and top-file outliers. `Hecton8.World` is the heaviest namespace domain; `HectonPlayerMovement.cs` is the heaviest single fused file.

Rejected Alternatives: Hard-mapping every file into one of 85 domains by keyword would create fake precision and pollute the report.

Scalability potential: Low tier benefits from identifying large fused systems that are harder to budget. High/Ultra tiers can use the same map to target visual-overkill domains without bloating core execution.

Hardware Impact: 0 direct runtime microseconds. Indirectly identifies risk surfaces where later profiling may recover frame time.

## Decision 4 - Token Ledger Source

Problem: `.codex` JSONL contains many repeated `token_count` events. Summing `last_token_usage` over every event overcounts the same turn.

Solution: For each JSONL session, use the final `total_token_usage` and sum across sessions. Cross-check against `state_5.sqlite.threads.tokens_used`.

Rejected Alternatives: The first naive `last_token_usage` sum was rejected because it reached 50.2B tokens by counting repeated telemetry snapshots. Estimating only from file bytes was rejected because `.codex` exposes a better token ledger.

Scalability potential: Low/Middle/High/Ultra runtime tiers unaffected. Audit scalability improves because future reports can avoid the repeated-event trap.

Hardware Impact: 0 runtime microseconds. Prevents false financial deltas in management reports.

## Decision 5 - Shadow Cost Boundary

Problem: The prompt provides GPT-5.5 Spud rates but does not specify cached-input discounts.

Solution: Report raw sticker cost using all input tokens and a separate lower bound using non-cached input only. Output is charged once using `output_tokens`; `reasoning_output_tokens` is treated as a subset of output, not added a second time.

Rejected Alternatives: Adding reasoning output on top of output would double-count. Claiming actual invoice cost would be false because no billing export was read.

Scalability potential: Low tier: highlights context bloat as process debt. High/Ultra: supports deciding where expensive long-context agents are justified by visual or architectural yield.

Hardware Impact: 0 runtime microseconds on i3/MX350. Process impact: exposes the economic cost of repeated full-context turns.

## Decision 6 - Cadence Sources

Problem: `Docs/Tasks` timestamps only show current batch/task file churn; they do not capture every interactive prompt.

Solution: Report two cadence layers: `Docs/Tasks` filesystem bursts and `.codex` `user_message` bursts. Use `.codex` for human prompt frequency, and `Docs/Tasks` for current agent-workflow file churn.

Rejected Alternatives: Treating LastWriteTime as full prompt truth was rejected because many prompts never write task files. Treating `.codex` alone as batch-agent truth was rejected because agent status files are the active workflow artifact.

Scalability potential: Low tier process benefits from knowing whether pressure comes from actual user prompts or from agent bookkeeping. High/Ultra process can justify more parallel agents only when output yield beats prompt burst debt.

Hardware Impact: 0 runtime microseconds. Audit impact: prevents false "prompts per minute" claims.

## Decision 7 - Velocity Model

Problem: The prompt asks for last-14-days compression, but filesystem state alone does not prove exact creation date for every LOC.

Solution: Use a clearly labeled 14-day compression model: current 775,435 meaningful LOC divided by 14 days. Keep it as an economic model, not a git-proven LOC delta.

Rejected Alternatives: Git-only LOC delta was rejected because active workspace churn and generated/uncommitted files are part of the project surface. Claiming exact historical authorship was rejected as unsupported.

Scalability potential: Low/Middle/High/Ultra runtime tiers unaffected. Management-scale value: exposes that the code volume is beyond normal human cadence even under conservative meaningful-LOC counting.

Hardware Impact: 0 runtime microseconds. Process savings quantified against human-year baselines only.

## Decision 8 - H-Phi Correlation Verdict

Problem: The mission asks whether higher H-Phi correlates with higher token burn.

Solution: Scan `HECTON_PHI_REPORT.md` for H-Phi values and `.codex` SQLite/JSONL for token burn. Report the correlation as `NOT PROVEN` because the two datasets do not share a valid key: no thread id, agent id, timestamped H-Phi delta, or LOC delta joins token spend to score movement.

Rejected Alternatives: A cumulative time plot was rejected because any cumulative token counter rises with time and would fake correlation. Assigning H-Phi gains to high-token threads by title was rejected as evidence fraud.

Scalability potential: Low tier process avoids wasting agents on fake metric chasing. High/Ultra process can still use H-Phi as a static hygiene trend, but only when paired with token and code-delta attribution.

Hardware Impact: 0 runtime microseconds. Prevents bad management decisions from a fake correlation.

## Decision 9 - Waste Detection Boundary

Problem: The prompt demands marking agents over 1M tokens without LOC or H-Phi gain as "Compute Thieves".

Solution: Separate convictions from candidates. Current active Status/LOG/Rationale agent files do not show any named agent over 1M estimated document tokens. `.codex` threads do show massive burn, but the state DB does not prove which threads increased LOC or H-Phi. Therefore the report flags high-burn candidates and refuses hard accusation.

Rejected Alternatives: Naming "Compute Thieves" from token count alone was rejected because token burn without output attribution is not proof of waste.

Scalability potential: Low/Middle/High/Ultra process gains a real triage list: first investigate threads over 250M tokens, then bind them to diffs and metric deltas.

Hardware Impact: 0 runtime microseconds. Process impact: reduces forensic noise and directs follow-up to measurable high-burn sessions.

## Decision 10 - Verification Boundary

Problem: The workflow asks for compile checks, but this task edited only Markdown audit artifacts and an unrelated `dotnet` process was already active in the shared workspace.

Solution: Run `git diff --check` against touched files and avoid launching a competing compile owner. Record compile as not applicable to runtime code for this task.

Rejected Alternatives: Running a parallel `dotnet build` was rejected because Unity-generated projects share temp outputs and another process was already active. Claiming compile verification without running it was rejected.

Scalability potential: Low/Middle/High/Ultra runtime tiers unaffected. Process scalability improves by avoiding false build contention during documentation-only audits.

Hardware Impact: 0 runtime microseconds. Avoided possible build-output lock noise.

## Decision 11 - Live Ledger Addendum

Problem: The user asked to continue and update honestly. `.codex/state_5.sqlite` changed after the initial report because other Codex work continued.

Solution: Add a continuation addendum rather than silently replacing the first capture. Record both the earlier figure and the new live figure with timestamp context and concentration analysis.

Rejected Alternatives: Treating the new value as a correction was rejected because the first value was valid at capture time. Ignoring the new value was rejected because the user explicitly asked for continued updating.

Scalability potential: Low/Middle/High/Ultra process gains a more accurate audit habit: live ledgers need timestamped snapshots, not mutable "truth".

Hardware Impact: 0 runtime microseconds. Process impact: prevents false deltas from concurrent agent churn.

## Decision 12 - `logs_2.sqlite` Scope Limit

Problem: `logs_2.sqlite` is 3.2GB and broad grouping by target did not finish within 120 seconds.

Solution: Keep prior schema/count fact and mark detailed log grouping as partial evidence pending indexed/offline extraction. Do not block the useful SQLite thread ledger on the heavier log DB.

Rejected Alternatives: Running longer blind scans in the active workspace was rejected because it would waste local IO and still might race live writes. Claiming log target breakdown without a completed query was rejected.

Scalability potential: Future audit should export a compact indexed slice before attempting target/module aggregation.

Hardware Impact: 0 runtime microseconds. Avoided prolonged disk pressure on the active machine.

## Decision 13 - Cache-Aware Reprice And Throughput

Problem: The first report used the prompt's GPT-5.5 Spud constants and a zero-cost cached-input lower bound. The user then asked for actual token/sec, token/min, token/hour, token/day, cache-aware price, and tokens per code byte.

Solution: Re-price by model using current official OpenAI standard API rates and the JSONL final usage split: input, cached input, output, and reasoning output. Run a separate positive-delta JSONL pass to measure throughput over time without summing repeated telemetry snapshots.

Rejected Alternatives: Reusing the prompt constant was rejected because the user explicitly requested current pricing with cache. Treating cached input as free was rejected because official pricing charges cached input at a discounted rate. Summing every `last_token_usage` event was rejected because it double-counts repeated turn snapshots.

Scalability potential: Low tier process can identify context bloat by cost per byte and token per LOC. Middle/High/Ultra process can reserve expensive long-context work for tasks that produce measurable code, compile, or H-Phi deltas.

Hardware Impact: 0 runtime microseconds on i3/MX350. Audit IO cost was local only; no Unity runtime path changed.

## Decision 14 - Root Brief Preservation

Problem: The full compute report is detailed and buried under `Docs/Reports`. The user explicitly requested concise, clear information closer to the repository root so the audit facts are not lost.

Solution: Create `COMPUTE_AUDIT_BRIEF.md` at project root. Keep it short: hard numbers, evidence rules, current verdict, and paths to the full report/status/rationale/log.

Rejected Alternatives: Duplicating the full report was rejected because it would create two long truth sources. Writing only to chat was rejected because chat is not durable project memory.

Scalability potential: Low/Middle/High/Ultra process gains a stable audit entry point. Future agents can read one near-root file before drilling into detailed ledgers.

Hardware Impact: 0 runtime microseconds on i3/MX350. No runtime asset, C# file, scene, prefab, or project setting changed.

## Decision 15 - Top-Thread Triage

Problem: The root brief named top-100 `.codex` threads as the next audit target, but the evidence was not yet preserved in a near-root artifact.

Solution: Query `.codex/state_5.sqlite` read-only and create `COMPUTE_THREAD_TRIAGE.md`. Focus on concentration, top-100 shape, updated-day concentration, and top-30 thread IDs. Use `HIGH-BURN CANDIDATE` only.

Rejected Alternatives: Scanning all 764 threads equally was rejected because top 100 holds about half the token mass. Calling expensive threads "waste" was rejected because the SQLite ledger does not prove file diffs, LOC delta, compile status, or H-Phi movement.

Scalability potential: Low/Middle/High/Ultra process gains a concrete audit queue. Investigating the top 30 first covers 21.618% of total burn; top 100 covers 49.975%.

Hardware Impact: 0 runtime microseconds on i3/MX350. No runtime code or Unity asset changed.

## Decision 16 - Rollout Attribution

Problem: Top-thread triage identified expensive thread IDs, but token concentration alone does not show what files those threads attempted to change.

Solution: Parse the top-30 rollout JSONL files read-only. Extract `apply_patch` file targets, patch churn lines, tool-call counts, and command evidence buckets. Preserve the result in `COMPUTE_THREAD_ATTRIBUTION.md`.

Rejected Alternatives: Treating title text as attribution was rejected because titles are weak evidence. Treating patch payload churn as final LOC delta was rejected because JSONL includes retries, superseded edits, and work that may have been overwritten by later agents.

Scalability potential: Low/Middle/High/Ultra process gains a practical collision map. The hot patch files are now known before further agents pile more edits onto fused surfaces.

Hardware Impact: 0 runtime microseconds on i3/MX350. No C# source, scene, prefab, shader, or project setting was changed by this audit pass.

## Decision 17 - Collision-Risk Snapshot

Problem: Hot patch targets from attribution are useful only if compared against the current dirty workspace. Concurrent agents are modifying runtime scripts while this audit is running.

Solution: Compare `git status --porcelain` against the hot target list and create `COMPUTE_COLLISION_RISK.md`. Record dirty script paths and hot-target intersections without editing or reverting them.

Rejected Alternatives: Running a compile immediately was rejected because the workspace contains active unrelated runtime edits by other agents. Reverting or normalizing those files was rejected because they are not owned by this audit agent.

Scalability potential: Low/Middle/High/Ultra process gains a live collision gate. The next integrator can prioritize `SpatialAudioManager.cs` because it is both historically hot and currently dirty.

Hardware Impact: 0 runtime microseconds on i3/MX350. This is a documentation-only risk snapshot.

## Decision 18 - Validation Forensics

Problem: Rollout attribution proves patch activity, but not whether the expensive threads attempted or passed validation.

Solution: Parse validation-relevant calls and outputs from the top-30 rollout JSONL files. Count `git diff --check`, `dotnet`/`msbuild`, Unity-related commands/tools, exit codes, CS compiler errors, compile-fail strings, build-success strings, and test-success/fail strings. Preserve the result in `COMPUTE_VALIDATION_FORENSICS.md`.

Rejected Alternatives: The first broad parse attempted to inspect too much shell output and timed out. Re-running the same broad parser was rejected. Claiming current compile status from historical logs was rejected because the workspace is now dirty and concurrent agents have changed files since those rollouts.

Scalability potential: Low/Middle/High/Ultra process gains a validation debt map. Future integration should prioritize threads with both high token burn and high non-zero validation output.

Hardware Impact: 0 runtime microseconds on i3/MX350. No runtime file changed.

## Decision 19 - File Burn Attribution

Problem: Thread-level attribution still leaves the integrator asking which files absorbed the most compute.

Solution: Allocate each top-30 thread's token count across its `apply_patch` file targets by per-thread patch-hit share. Record weighted token burn, cost proxy, patch hits, thread count, current LOC, and dirty status in `COMPUTE_FILE_BURN_ATTRIBUTION.md`.

Rejected Alternatives: Assigning full thread tokens to every touched file was rejected because it overcounts massively. Using file mention frequency outside patch payloads was rejected because search/read commands are weaker evidence than actual patch targets. Treating weighted burn as final value was rejected because retries and overwritten patches remain possible.

Scalability potential: Low/Middle/High/Ultra process gets a concrete hot-file queue. Future compile and ownership reviews should start with the top weighted targets rather than all 1,647 patch targets.

Hardware Impact: 0 runtime microseconds on i3/MX350. No runtime code changed.

## Decision 20 - Root Audit Index

Problem: The audit now has multiple root files. Without a read-order index, later agents can read only one layer and overclaim value, waste, or C++ migration status.

Solution: Create `COMPUTE_AUDIT_INDEX.md` with read order, hard boundaries, forbidden overclaims, and the next honest verification gate.

Rejected Alternatives: Extending the brief with another long section was rejected because the brief is already the short summary. Depending on chat history was rejected because context compression is expected.

Scalability potential: Low/Middle/High/Ultra process gains a stable navigation point. Future agents spend less time rediscovering which audit file is authoritative for which claim.

Hardware Impact: 0 runtime microseconds on i3/MX350. Documentation-only.

## Decision 21 - Top-100 Value And C++ Transfer Evidence

Problem: Top-30 attribution and validation forensics showed expensive work traces, but the user requested continued certainty and specifically repeated the C++ transfer requirement. Token concentration alone cannot prove productive value or migration state.

Solution: Parse the top-100 rollout JSONL files read-only with normalized path accounting, then classify threads by visible work evidence, external-path dominance, dirty-workspace collision, and C++ patch target presence. Preserve the result in `COMPUTE_THREAD_VALUE_AUDIT.md`, refresh `COMPUTE_COLLISION_RISK.md`, and cross-check the project tree for existing C++ source files.

Rejected Alternatives: Treating top-100 patch churn as final LOC delta was rejected because rollout patches include retries and superseded edits. Calling code-heavy threads "verified value" was rejected because current compile/H-Phi joins are absent. Claiming C++ migration completion from user pressure was rejected because the parse found zero C++ patch targets in the top-100 rollout patches.

Scalability potential: Low/Middle tiers gain process protection by blocking more agents from piling edits onto hot dirty files without attribution. High/Ultra process can focus expensive agents only on threads with code work trace plus validation deltas, not on external-path contamination or token mass alone.

Hardware Impact: 0 runtime microseconds on i3/MX350. No C# source, C++ source, scene, prefab, shader, project setting, or Unity asset was changed. Process impact: identifies `SargassumMicroFaunaBoids.cs` and `HabitatGraphManager.cs` as live collision gates and marks C++ transfer as `NOT VERIFIED / NO PATCH EVIDENCE`.

## Decision 22 - Rate Efficiency Recheck

Problem: The user explicitly asked to keep working and compute tokens per second, minute, hour, day, cache-aware price, token-per-byte, and other useful indicators. The ledger is live, so older totals were already stale as current-state facts.

Solution: Run a read-only optimized JSONL scan that only parses `token_count` and user-message rows, join session paths to `state_5.sqlite` for model attribution, and preserve the result in `COMPUTE_RATE_EFFICIENCY_AUDIT.md`. Keep three cost scenarios: model-aware lower bound, all-GPT-5.5 standard, and all-GPT-5.5 long-context.

Rejected Alternatives: Reusing the previous 43.78B snapshot was rejected because `.codex` had moved. Treating all unknown sessions as exact GPT-5.5 billing was rejected because 20 JSONL sessions did not map cleanly to SQLite model rows. Treating the model-aware lower bound as an invoice was rejected because no billing export was read.

Scalability potential: Low/Middle/High/Ultra process gains a hard rate dashboard. Future work can see when prompt velocity jumps from normal burn into pathological burst mode and can decide whether to pause agents before piling more context onto hot files.

Hardware Impact: 0 runtime microseconds on i3/MX350. No C# source, scene, prefab, shader, project setting, or Unity asset was changed. Process impact: identifies 57,503.86 tokens per meaningful LOC and 1,070.477 tokens per script source byte as the current context-recursion signature.

## Decision 23 - Codex Dialogue Topology

Problem: Token totals explain cost but not dialogue/tool shape. The user asked to keep studying `.codex` dialogs and logs, and the prior `logs_2.sqlite` grouping attempts had timed out.

Solution: Inspect `logs_2.sqlite` schema/indexes read-only, use indexed and bounded queries only, then run a marker-based scan over JSONL sessions. Preserve the result in `COMPUTE_CODEX_DIALOGUE_AUDIT.md` with explicit boundaries between exact indexed facts, recent samples, and marker counts.

Rejected Alternatives: Full grouping by `target`/`level` over the whole 3.2GB log DB was rejected after timeout because there are no target/level indexes. Full semantic JSON parse over 8GB JSONL was rejected after timeout. Treating marker counts as exact executed tool-call counts was rejected because markers can occur in payloads, outputs, summaries, or quoted text.

Scalability potential: Low/Middle/High/Ultra process gains a realistic view of automation density. Future audits can see that the main risk is not just token mass, but one user marker driving dozens of tool markers and transport/log events.

Hardware Impact: 0 runtime microseconds on i3/MX350. No C# source, scene, prefab, shader, project setting, or Unity asset was changed. Process impact: identifies `logs_2.sqlite` as capped recent evidence and JSONL as the durable but expensive dialogue source.

## Decision 24 - Timaert/Samosbor Transfer Boundary

Problem: The user ordered Timaert/Samosbor docs, tasks, and logs to live in the Timaert folder, not Hecton. Exact Timaert/Samosbor/TMA labels were not present under Hecton, while active Hecton agents were still mutating docs/logs.

Solution: Keep all transferred material in the existing quarantined Timaert import tree under `C:\Timaert\timaert_c\Docs\Imported\Hecton8\2026-05-15_docs_tasks_logs`. Refresh selected documentation/log scopes non-destructively, preserve source-relative paths, and record manifests in Timaert. Do not delete Hecton sources and do not write Timaert project docs into Hecton.

Rejected Alternatives: Moving files out of Hecton was rejected because the current matches are Hecton docs without exact Timaert labels and deletion would destroy Hecton provenance. Copying into active Timaert `Docs\Tasks` or `Docs\AgentLogs` was rejected because it would pollute live Timaert agent state. Chasing a permanent zero-delta state was rejected because concurrent Hecton agents kept writing new logs/status files during verification.

Scalability potential: Low/Middle/High/Ultra process gains isolation: Timaert can inspect Hecton-imported docs without contaminating active Timaert task/log state, and Hecton no longer has to be used as a storage target for Timaert documentation.

Hardware Impact: 0 runtime microseconds on i3/MX350. No C# source, C++ source, Unity asset, or Timaert runtime source was changed. Documentation import only.

## Decision 25 - Rolling Token Burn Ledger

Problem: The user asked for continued token accounting by second, minute, hour, day, cost per minute/hour/day, cache, and model mix. The previous rate audit was already stale and the first continuation script failed to map models because it used an obsolete `codex_threads` table name.

Solution: Inspect the current `state_5.sqlite` schema, use `threads.rollout_path`, `threads.model`, and `threads.tokens_used`, then run a read-only JSONL pass over token-count rows. Preserve the current rolling 1h/6h/24h/7d/14d/30d burn and cost rates in `COMPUTE_TOKEN_BURN_RATE_LEDGER.md`. Add a light SQLite tail check after the full pass to capture live drift without repeating the full JSONL scan.

Rejected Alternatives: Treating all unmatched sessions as GPT-5.5 was rejected because 40 final-usage sessions did not cleanly map to SQLite. Treating the first failed `unknown`-only scan as valid was rejected because it erased the actual model split. Claiming invoice precision was rejected because no billing export was read.

Scalability potential: Low/Middle/High/Ultra process gains an explicit spend throttle. Future agents can see that the last 24h alone burned 3.237B tokens and about USD 1.04k cache-aware, and that the live tail added another 102.15M tokens at 27,749.37 tokens/sec after the full scan.

Hardware Impact: 0 runtime microseconds on i3/MX350. No runtime code changed. Process impact: identifies 96.003% cache dependence and 57,636.87 tokens per meaningful script LOC as the current context-recursion signature.

## Decision 26 - Live Burn Source Sampling

Problem: Rolling totals show that tokens are still being consumed, but they do not identify which active threads are currently burning tokens.

Solution: Use a two-point SQLite sample over `threads.tokens_used`, compare per-thread deltas, and write `COMPUTE_LIVE_BURN_SOURCES.md`. Use blended cache-aware rates from the full JSONL ledger because SQLite deltas do not contain input/cache/output splits.

Rejected Alternatives: Running another full JSONL parse immediately was rejected because it takes minutes and is unnecessary for a short live attribution sample. Keeping the failed 120-second stdout sample was rejected because Windows codepage encoding destroyed the output. Calling active threads waste was rejected because token delta does not prove value or non-value.

Scalability potential: Low/Middle/High/Ultra process gains a live throttle map. Instead of pausing all agents blindly, an integrator can inspect the 11 active threads and decide whether the top two, which produced 50.59% of the sample burn, are doing necessary work.

Hardware Impact: 0 runtime microseconds on i3/MX350. No runtime code changed. Process impact: current live burn measured at 30,099.39 tokens/sec and USD 1.38/min cache-aware.

## Decision 27 - Model Bucket Reconciliation

Problem: The current token ledger had a large `unknown` model bucket. That made the model-aware cost too low and hid the real GPT-5.5 share.

Solution: Reconcile JSONL session files with SQLite using exact `rollout_path` first and UUID fallback from `rollout-...UUID.jsonl` to `threads.id`. Re-run the final-usage JSONL scan and write `COMPUTE_MODEL_BUCKET_RECONCILIATION.md`.

Rejected Alternatives: Keeping the path-only `unknown` bucket was rejected because 17 final-usage sessions resolved cleanly by UUID. Guessing all unknown as GPT-5.5 without a SQLite join was rejected because it would be right by accident, not evidence. Mixing the partial live correction into the old full snapshot was rejected because timestamps differed.

Scalability potential: Low/Middle/High/Ultra process gains a cleaner cost model. Future rate scans must use path-or-UUID matching; otherwise active sessions are underpriced and attribution degrades.

Hardware Impact: 0 runtime microseconds on i3/MX350. No runtime code changed. Process impact: unknown final-usage model bucket reduced to 0 tokens; corrected model-aware estimate is USD 30,613.26.

## Decision 28 - Corrected Rolling Rates

Problem: Model bucket reconciliation fixed final-usage attribution, but rolling 1h/6h/24h cost windows still preserved the older path-only underpricing.

Solution: Re-run the JSONL positive-delta window scan with path-or-UUID model matching, then write `COMPUTE_CORRECTED_ROLLING_RATES.md`. Use per-model blended cache-aware cost from the corrected scan to price deltas by window.

Rejected Alternatives: Editing the old rolling ledger in place was rejected because it would erase historical evidence of the under-attribution. Keeping old last-24h USD 1,039.59 as current was rejected because the corrected window is USD 2,601.80.

Scalability potential: Low/Middle/High/Ultra process gains a more reliable live spend throttle. Window costs now reflect actual `gpt-5.5` dominance instead of a cheap `unknown` proxy.

Hardware Impact: 0 runtime microseconds on i3/MX350. No runtime code changed. Process impact: latest corrected 24h burn is 3.399B tokens and USD 2.60k cache-aware.

## Decision 29 - Live Burn Trend Sampling

Problem: A single live sample identifies active threads, but it does not show volatility across adjacent minutes.

Solution: Sample `state_5.sqlite.threads.tokens_used` for three consecutive 60-second intervals, price each interval with corrected `gpt-5.5` blended rates, and write `COMPUTE_LIVE_BURN_TREND.md`.

Rejected Alternatives: Re-running another full JSONL pass was rejected because the question here is short-window live trend, not historical final usage. Treating the three-minute day equivalent as a real daily invoice was rejected because short-window extrapolation is volatile.

Scalability potential: Low/Middle/High/Ultra process gains a live throttle trend. The top five active threads produced 63.87% of the sample burn, so intervention can be targeted instead of global.

Hardware Impact: 0 runtime microseconds on i3/MX350. No runtime code changed. Process impact: three-minute live burn measured at 56,671.11 tokens/sec and USD 2.60/min cache-aware.

## Decision 30 - Five-Minute Live Burn Forecast

Problem: The user ordered continued honest token counting. The previous three-minute sample captured volatility but did not give a better short-window stop-loss projection.

Solution: Sample `state_5.sqlite.threads.tokens_used` for five consecutive 60-second intervals, price the deltas with the corrected global cache-aware/no-cache blends, and write `COMPUTE_LIVE_BURN_5MIN_FORECAST.md`.

Rejected Alternatives: Re-running the full JSONL parser was rejected because this pass needed current tail rate, not historical final usage. Using one minute as the forecast was rejected because the five intervals ranged from 19.26k to 92.72k tokens/sec. Calling the top threads waste was rejected because token burn still lacks final diff, validation, and quality-delta joins.

Scalability potential: Low/Middle/High/Ultra process gains a practical stop-loss dashboard. At the measured five-minute rate, 100M tokens arrive in 30 minutes, USD 100 cache-aware burn arrives in 44.72 minutes, and the top 10 threads carry 74.54% of live burn.

Hardware Impact: 0 runtime microseconds on i3/MX350. No runtime code changed. Process impact: five-minute live burn measured at 55,562.22 tokens/sec, USD 2.236/min cache-aware, USD 14.714/min no-cache equivalent.
