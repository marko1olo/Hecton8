

## Decision 9 - 2026-05-26 model-price forensics

Problem: User requested more exact model pricing, but local Codex JSONL does not expose invoice SKU, subscription handling, or priority tier, and several exact model labels lack public rate rows.
Solution: Attribute tokens to exact structural model labels, price labels with official standard rates, and isolate known-but-unpriced labels into explicit bounds.
Rejected Alternatives: Treating every session as gpt-5.5 or every session as gpt-5.3-codex was rejected because it hides the evidence boundary.
Scalability potential: Low/Middle/High/Ultra runtime tiers unaffected; this is local telemetry accounting.
Hardware Impact: 0 us runtime gain.

## Decision 10 - 2026-05-26 documentation shape

Problem: Token docs risk becoming scattered across dated reports, ledger, and chat-only claims.
Solution: Keep `Docs/TOKEN_USAGE_LEDGER.md` as the stable summary and the current dated `Docs/Reports/TOKEN_USAGE_AUDIT_<date>.md/.json` pair as the full forensic artifact.
Rejected Alternatives: Creating another standalone model-only report was rejected as documentation sprawl.
Scalability potential: Future audits have one stable entry point and one dated evidence artifact.
Hardware Impact: 0 us runtime gain.

## Decision 11 - 2026-05-26 dated artifact correctness

Problem: The token audit script had a fixed 2026-05-25 report name even after the user requested a current recount.
Solution: Generate dated token reports from the current Samara date and keep the stable ledger as the pointer.
Rejected Alternatives: Overwriting an old dated artifact was rejected because it makes historical reports lie.
Scalability potential: Low/Middle/High/Ultra runtime tiers unaffected; this is evidence hygiene.
Hardware Impact: 0 us runtime gain.

## Decision 12 - 2026-05-26 docs gate closure

Problem: The fresh Markdown outputs were valid content but failed the repository UTF-8-SIG policy.
Solution: Write generated Markdown token surfaces with UTF-8-SIG and convert the two current outputs.
Rejected Alternatives: Ignoring `VerifyDocStructure.py` was rejected because root docs require green structure evidence.
Scalability potential: Runtime tiers unaffected; future agents get deterministic documentation surfaces.
Hardware Impact: 0 us runtime gain.

## Decision 13 - 2026-05-26 stable docs refresh

Problem: Current totals existed in reports, but stable entry points still cited 2026-05-23/2026-05-25 token boundaries.
Solution: Update current root, reports, architecture, global map, and root reference docs with exact current counters.
Rejected Alternatives: Editing archived dated reports was rejected because archives are historical evidence snapshots.
Scalability potential: Prevents future cleanup agents from using stale scale assumptions.
Hardware Impact: 0 us runtime gain.

## Decision 14 - 2026-05-26 archive superseded token report

Problem: The 2026-05-25 token audit remained in active `Docs/Reports` after the 2026-05-26 recount.
Solution: Move the old dated pair into `Docs/_Archive/TokenUsage_2026-05-25/` and rerun documentation gates.
Rejected Alternatives: Keeping stale dated totals in active evidence storage was rejected. Rewriting old totals was rejected because that would falsify a historical snapshot.
Scalability potential: Future agents read one current token report and one stable ledger.
Hardware Impact: 0 us runtime gain.


## Decision 15 - 2026-05-26 deep token-stat expansion

Problem: User requested heavier token economics: day/week/chat stats, token density per code character/line, and cost ratios tied to current model prices.
Solution: Extend the generator to count code/doc characters and bytes, add per-period cost curves, add CWD/source/CLI/plan usage buckets, and keep API-equivalent price bounds separate from invoice proof.
Rejected Alternatives: Manual spreadsheet math and one-off Markdown tables were rejected because they rot on the next JSONL change.
Scalability potential: Low/Middle/High/Ultra runtime tiers unaffected; this is local telemetry accounting.
Hardware Impact: 0 us runtime gain.

## Decision 16 - 2026-05-26 evidence boundary

Problem: Current official pricing docs include standard, batch/flex, priority, specialized Codex, prompt caching, and reasoning-token billing rules, but local telemetry still lacks invoice SKU.
Solution: Cite official pricing-source URLs in the report and label all dollar values as API-equivalent estimates; reasoning tokens remain output subcounter, cached input is priced separately and not double-counted.
Rejected Alternatives: Calling the result "actual spend" was rejected because local JSONL lacks invoice IDs, enterprise discounts, and subscription billing route.
Scalability potential: Future audits can swap rate rows without changing telemetry accounting.
Hardware Impact: 0 us runtime gain.

## Decision 17 - 2026-05-26 active-density scan boundary

Problem: Character density scan over archived/deprecated documentation and generated JSON reports exceeded the audit wall-time budget without improving current code-density truth.
Solution: Prune `Archive`, `_Archive`, and `DEPRECATED` directories from active density scans and keep Markdown/TXT/JSON outside `all_repo_source_broad`; active docs still have their own `docs_markdown_text` scope.
Rejected Alternatives: Counting historical archives as current code density was rejected because it makes token-per-code metrics meaningless.
Scalability potential: Future audits retain current active-code economics without dragging stale evidence bundles into code ratios.
Hardware Impact: 0 us runtime gain; audit wall time reduced by minutes.


## Decision 18 - 2026-05-26 GPT-5.5 primary correction

Problem: The deep token report used gpt-5.3-codex as the headline API-equivalent estimate, but operator state says the normal model is GPT-5.5 with xhigh reasoning effort.
Solution: Make gpt-5.5 standard short-context the primary rate row, add gpt-5.5 priority/batch/flex sensitivities, and expose xhigh cost/ratio metrics from structured effort telemetry.
Rejected Alternatives: Keeping gpt-5.3-codex as headline was rejected because it understates GPT-5.5 API-equivalent cost. Guessing invoice spend was rejected because local JSONL lacks invoice SKU, discounts, and subscription route.
Scalability potential: Future audits can swap primary rate rows through constants while preserving old 5.3 comparison data.
Hardware Impact: 0 us runtime gain.


## Decision 19 - 2026-05-26 exact model-effort spend

Problem: The report still had model-only and effort-only slices; the user requested concrete spend by exact model plus reasoning effort.
Solution: Build final-session and temporal-delta `model::effort` cost matrices from structured JSONL fields, pricing only rows whose model has an official public rate and exposing unknown/unpriced leakage separately.
Rejected Alternatives: Multiplying every effort row by GPT-5.5 was rejected because it would falsify unknown model rows. Treating xhigh as a separate rate was rejected because official docs bill reasoning tokens as output, not via an effort multiplier.
Scalability potential: Future reports can pivot by model, effort, or model-effort owner without changing telemetry parsing.
Hardware Impact: 0 us runtime gain.


## Decision 20 - 2026-05-26 input-output economics

Problem: Total tokens and model-effort dollars still did not show whether cost pressure came from paid input, cached input, visible output, or hidden reasoning output.
Solution: Add an input-output economics block with paid/cached/output ratios, cost shares, top output/reasoning sessions and days, and model-effort I/O ratios.
Rejected Alternatives: Reporting only aggregate input/output counts was rejected because it hides cache leverage and output cost share.
Scalability potential: Future audits can locate output-heavy or reasoning-heavy sessions without reparsing raw JSONL manually.
Hardware Impact: 0 us runtime gain.


## Decision 21 - 2026-05-26 explicit code-density economics

Problem: Code-density rows had tokens per character and dollars per character, but the requested units are line and 1000 code characters.
Solution: Add explicit tokens-per-1k-character and dollars-per-1k-character fields to scope economics and show them in the dated report, ledger, and agent log.
Rejected Alternatives: Leaving users to multiply per-character values manually was rejected because it invites inconsistent reporting.
Scalability potential: Future audits can compare code scopes without spreadsheet conversion.
Hardware Impact: 0 us runtime gain.

## Decision 22 - 2026-05-26 token verification boundary

Problem: The operator asked to verify token information specifically, and the current local JSONL roots can change while the audit is running because multiple agents are active.
Solution: Recheck official OpenAI token/pricing rules, run an independent raw JSONL replay against the generated report, regenerate the report after stale-count detection, and record live-session drift separately from report-generator defects.
Rejected Alternatives: Trusting the generated report alone was rejected. Recreating active `Docs/Reports/TOKEN_USAGE_AUDIT_2026-05-26.*` was rejected because DOCS_ACTUALIZATION moved local token telemetry to `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/` as non-authoritative project noise.
Scalability potential: Future token audits can distinguish stable snapshot totals from continuously moving live-agent telemetry.
Hardware Impact: 0 us runtime gain.

## Decision 23 - 2026-05-27 daily token refresh

Problem: The operator requested a new day-over-day update, but local Codex telemetry keeps moving and a raw total alone does not answer what changed.
Solution: Add generated previous-snapshot delta accounting, recheck current official OpenAI token rules, and regenerate `TOKEN_USAGE_AUDIT_2026-05-27` under the existing deprecated telemetry archive.
Rejected Alternatives: Restoring token telemetry into active `Docs/Reports` was rejected because it remains local process telemetry, not project engineering authority. Manual one-off delta math was rejected because it would rot on the next run.
Scalability potential: Future daily refreshes now show totals and deltas from the last dated snapshot without hand calculation.
Hardware Impact: 0 us runtime gain.

## Decision 24 - 2026-05-27 late-day re-refresh and commit boundary

Problem: The operator requested a fresh update and full commit/push after additional local Codex telemetry and workspace changes accumulated.
Solution: Recheck official OpenAI pricing/cache/reasoning pages, regenerate the archived token snapshot from JSONL, and commit the token refresh separately before a full non-1334 workspace checkpoint.
Rejected Alternatives: Treating `xhigh` as a separate price row was rejected because official docs define it as reasoning effort, with reasoning tokens billed as output. Staging paths containing `1334` was rejected because the operator explicitly forbade touching that agent's files.
Scalability potential: Runtime Low/Middle/High/Ultra tiers unaffected; audit evidence remains reproducible and future full checkpoints can preserve explicit ownership exclusions.
Hardware Impact: 0 us runtime gain.

## Decision 25 - 2026-05-27 velocity and burn-rate accounting

Problem: The token audit had totals and previous-snapshot deltas, but the operator asked what speed the project is moving at in tokens, code, and money.
Solution: Add generated velocity fields under `previous_snapshot_delta.velocity`, render them in both the dated report and stable ledger, and keep code burn-rate ratios tied to net primary C# code growth from the same window.
Rejected Alternatives: Reporting velocity only in chat was rejected because it would be unreproducible after the next JSONL write. Dividing by all-time LOC was rejected for velocity because it hides the current window's code-growth burn-rate.
Scalability potential: Runtime Low/Middle/High/Ultra tiers unaffected; future audits can compare production cadence windows without rebuilding spreadsheet logic.
Hardware Impact: 0 us runtime gain.

## Decision 26 - 2026-05-28 fast refresh and dashboard surfaces

Problem: Full all-time token replay exceeded 20 minutes after the overnight agent/file surge, while the operator requested fresh stats, charts, and commit/push in the same pass.
Solution: Kill only the orphaned token-audit process, add `CodexTokenUsageFastRefresh_20260528.py`, and produce the 2026-05-28 token report from the last full snapshot plus post-cutoff positive JSONL deltas. Add `ProjectMetricsDashboard_20260528.py` to generate 29 chart PNGs and a Markdown/JSON dashboard from token, git, and filesystem metrics.
Rejected Alternatives: Starting a second full replay was rejected because it would double disk/CPU pressure. Reporting stale 2026-05-27 totals was rejected because new JSONL deltas were available. Token-only charts were rejected because the request explicitly asked for broader project metrics.
Scalability potential: Low/Middle/High/Ultra runtime tiers are unaffected. The audit path now scales under heavy parallel-agent churn while still preserving the slower full snapshot path for later exact rebaseline.
Hardware Impact: 0 us runtime gain. Audit wall time reduced from >20 minutes stalled to about 3 minutes for token fast refresh and about 5.5 minutes for chart generation.

## Decision 27 - 2026-05-28 apex verification boundary

Problem: The prior final answer contained correct high-level numbers, but it did not provide a standalone apex verification artifact with self-scan counts, exact hashes, CPU compile-throttle sample, and explicit evidence-class downgrades.
Solution: Add `Tools/TokenUsageApexVerification_20260528.py` and generate `Docs/Reports/TOKEN_USAGE_APEX_VERIFICATION_2026-05-28.json/.md/.sha256`. The verifier scans owned offline tooling with Python string/comment literals excluded, validates 29 PNG charts, parses JSON reports, hashes source/report artifacts, and marks runtime 0 B/frame as pending because no Unity profiler/GCMonitor run occurred.
Rejected Alternatives: Claiming Zero-GC completion from text search was rejected. Claiming DataVault compliance without a DataVault migration was rejected. Re-running dotnet build was rejected because TOKEN_USAGE_AUDIT did not touch runtime code and compile throttling forbids unnecessary dotnet/csc load.
Scalability potential: Runtime Low/Middle/High/Ultra tiers are unaffected. Verification quality improves because future token/report refreshes can reuse a deterministic, hash-backed artifact instead of chat-only proof.
Hardware Impact: 0 us runtime gain. Audit safety gain: false runtime claims reduced to explicit `PENDING_RUNTIME_VERIFICATION_FOR_ANY_RUNTIME_CLAIM`.

## Decision 28 - 2026-05-28 polish pricing and verifier hardening

Problem: The apex verifier used fixed 2026-05-28 report paths and the token report exposed only the base GPT-5.5 API-equivalent estimate, leaving long-context surcharge and regional uplift as implicit billing risks.
Solution: Make apex report paths derive from the current Samara date, add GPT-5.5 long-context surcharge and regional +10% sensitivity rows, and regenerate token/dashboard/apex artifacts from live JSONL deltas.
Rejected Alternatives: Keeping only the base GPT-5.5 row was rejected because official pricing has a >272K input surcharge risk. Running `dotnet build` was rejected because TOKEN_USAGE_AUDIT changed offline Python/docs only and active `dotnet`/`VBCSCompiler` processes made compile contention explicit.
Scalability potential: Future daily refreshes can validate the current dated report without stale hard-coded paths and can separate base cost, surcharge risk, and no-cache theoretical ceiling.
Hardware Impact: 0 us runtime gain. Audit precision gain: false certainty around billing context reduced by explicit sensitivity rows.

## Decision 29 - 2026-05-28 post-cutoff long-context precision

Problem: The long-context sensitivity row was a whole-corpus upper bound, which is mathematically safe but too blunt to explain current token burn.
Solution: Add post-cutoff increment-event accounting for deltas whose `input_tokens` exceeds the official 272000 trigger, and add a combined long-context plus regional +10% upper-bound row.
Rejected Alternatives: Treating the whole corpus as exact long-context spend was rejected because local JSONL lacks provider-side per-request classification. Ignoring long-context entirely was rejected because official GPT-5.5 pricing has a higher long-context row.
Scalability potential: Future refreshes can carry an exact post-cutoff surcharge signal while keeping all-time billing proof downgraded until the telemetry contains provider invoice classification.
Hardware Impact: 0 us runtime gain. Audit quality gain: separates base cost, full upper bound, and observed post-cutoff surcharge pressure.

## Decision 30 - 2026-05-28 same-day delta integrity

Problem: Fast refresh compared every same-day rerun against the previous dated report, so "since previous snapshot" metrics actually meant "since previous day" after the first same-day refresh.
Solution: Prefer the existing same-day `TOKEN_USAGE_AUDIT_{date}.json` as the fast-refresh base when present, and record `fast_refresh_base_mode`/`previous_snapshot_mode` in JSON and Markdown.
Rejected Alternatives: Keeping the old behavior was rejected because it made intra-day token velocity misleading. Creating many full duplicate reports was rejected because the existing dated report plus hash-backed apex proof already owns the current mutable snapshot.
Scalability potential: Future same-day refreshes now measure actual incremental churn between agent runs without requiring a full replay.
Hardware Impact: 0 us runtime gain. Audit quality gain: velocity labels now match the snapshot window.

## Decision 31 - 2026-05-28 evidence boundary polish

Problem: The post-cutoff long-context wording was too strong for local Codex JSONL, and the apex chart proof only compared chart counts/signatures without proving that every dashboard-declared chart path existed on disk.
Solution: Downgrade long-context detection to `LOCAL_JSONL_DELTA_LOWER_BOUND_NOT_PROVIDER_INVOICE_CLASSIFICATION`, add chart manifest path bijection checks, and expose final compile-throttle state directly in the Markdown proof.
Rejected Alternatives: Claiming exact provider-side long-context billing was rejected because local JSONL lacks invoice classification. Count-only chart validation was rejected because 29 files on disk can still be the wrong 29 files. Running compile checks under active csc/dotnet contention was rejected by the compilation throttling rule.
Scalability potential: Future report refreshes can detect stale/missing chart assets and keep pricing claims inside their evidence class without slowing Unity runtime.
Hardware Impact: 0 us runtime gain. Audit quality gain: false certainty around pricing and chart integrity is reduced; compile contention is recorded instead of hidden.

## Decision 32 - 2026-05-28 long-range chart windows

Problem: The dashboard contained 96-hour hourly charts plus broad daily/weekly charts, but the operator wanted explicit week/month/two-month token-consumption views with readable labels.
Solution: Add generated 7d, 30d, and 60d daily chart windows for total tokens, GPT-5.5 cost, I/O stack, and cache/output/reasoning ratios. Annotate long-range line charts with start/end/peak/min labels so the PNGs are readable without opening the JSON.
Rejected Alternatives: Keeping only `last_96h` was rejected because it hides monthly burn shape. Adding manual screenshots was rejected because regenerated evidence must come from the Python dashboard pipeline.
Scalability potential: Runtime Low/Middle/High/Ultra tiers unaffected; this is offline reporting. Audit scalability improves because future refreshes produce the same long-range chart set from source telemetry.
Hardware Impact: 0 us runtime gain. Reporting gain: chart count increased from 29 to 41 with 12 explicit long-range PNG artifacts.

## Decision 33 - 2026-05-28 live stats refresh and compile-throttle fix

Problem: The token/dashboard artifacts became stale again after active-agent JSONL churn, and the apex verifier treated compiler-process presence as the only compile-throttle blocker while the mandate also forbids compile work under CPU load above 50 percent.
Solution: Rerun the fast token refresh, dashboard, and apex verifier; patch `TokenUsageApexVerification_20260528.py` so `cpu_total_percent > 50` also produces `SKIPPED_BLOCKED_BY_COMPILER_CONTENTION` and a downgraded evidence class.
Rejected Alternatives: Running `dotnet build` was rejected because CPU was 96 percent with active `csc` and `dotnet`. Leaving the stale 13:55 CPU sample was rejected because evidence must be current to the report. Claiming Python bytecode compile under contention was rejected as false proof.
Scalability potential: Runtime Low/Middle/High/Ultra tiers unaffected; this is offline audit hygiene. Evidence scalability improves because future apex reports cannot silently overclaim compile proof during heavy parallel-agent load.
Hardware Impact: 0 us runtime gain. Audit correctness gain: refreshed total is 113,292,508,044 tokens, chart count remains 41, and compile proof is explicitly downgraded to no-compile under CPU/compiler contention.

## Decision 34 - 2026-05-28 full workspace checkpoint boundary

Problem: The operator requested full commits and pushes after the token refresh, while the workspace contained hundreds of unrelated cross-agent changes and an explicit prohibition against touching paths containing `1334`.
Solution: Commit token evidence first, then stage the remaining workspace as a separate checkpoint after scanning staged paths for `1334` and cleaning only mechanical whitespace gate failures.
Rejected Alternatives: Reverting unrelated staged changes was rejected because they are other agents' work. Pushing without a clean `git diff --cached --check` was rejected after whitespace errors were found. Running `dotnet build` was rejected because TOKEN_USAGE_AUDIT did not own runtime changes and compiler contention was already observed.
Scalability potential: Runtime Low/Middle/High/Ultra tiers unaffected by this audit checkpoint. Repository evidence improves because token telemetry has isolated commits and broad agent work has a separate checkpoint boundary.
Hardware Impact: 0 us runtime gain. Process gain: no protected `1334` path was staged, and whitespace gate was green before the broad commit.

## Decision 35 - 2026-05-29 token stats refresh

Problem: The operator requested a new token-stat update and full commit/push after another day boundary, while local Codex JSONL and the Unity workspace continued to change under parallel agents.
Solution: Verify current official GPT-5.5 pricing/cache context, regenerate the 2026-05-29 token report, dashboard, chart set, CPU sample, and apex verification artifact before any broad workspace checkpoint.
Rejected Alternatives: Reusing the 2026-05-28 report was rejected because the new report shows a 2,950,209,170-token delta. Running `dotnet build` was rejected because TOKEN_USAGE_AUDIT changed offline docs/tools only and CPU sample was 83 percent, above the mandated compile threshold.
Scalability potential: Runtime Low/Middle/High/Ultra tiers unaffected; this is offline telemetry and evidence accounting. Reporting scalability improves because the same 41-chart pipeline now produces dated 2026-05-29 surfaces.
Hardware Impact: 0 us runtime gain. Audit evidence gain: current total is 116,242,717,214 tokens, GPT-5.5 base API-equivalent cost is 90,317.418027 USD, and apex proof is hash-backed with no missing charts.

## Decision 36 - 2026-05-29 text-only token refresh

Problem: The operator requested a fresh token update and commit/push, explicitly without image/chart generation.
Solution: Recheck the official GPT-5.5 pricing/model boundary, run only the fast token refresh, and validate the persisted JSON plus ledger without invoking the dashboard/chart pipeline.
Rejected Alternatives: Running `ProjectMetricsDashboard_20260528.py` was rejected because it regenerates chart PNGs. Running `dotnet build` was rejected because this pass touched offline telemetry/docs only and the request was a token-data checkpoint, not runtime validation.
Scalability potential: Runtime Low/Middle/High/Ultra tiers unaffected. Reporting scalability improves because text-only refreshes can update current token economics quickly without churn in generated image artifacts.
Hardware Impact: 0 us runtime gain. Audit evidence gain: current total is 117,684,788,669 tokens, delta is 1,442,071,455 tokens, GPT-5.5 base API-equivalent cost is 91,354.58078 USD, and ledger presence was machine-checked.

## Decision 37 - 2026-05-30 text-only token refresh

Problem: The operator requested another count/update/commit/push after a date rollover, and the workspace contains live parallel-agent churn plus an untracked Windows-reserved `CON` filename.
Solution: Recheck official GPT-5.5 pricing/model evidence, run only the fast token refresh, validate the persisted JSON/ledger, and handle full checkpoint staging with an explicit protected-path and reserved-path gate.
Rejected Alternatives: Regenerating dashboard images was rejected because the operator asked to count/update/commit/push, not produce new charts. Blind `git add -A` without inspecting `CON` was rejected because Windows device names can break checkout/staging semantics.
Scalability potential: Runtime Low/Middle/High/Ultra tiers unaffected. Reporting scalability improves because dated text-only refreshes can move across date boundaries without chart churn.
Hardware Impact: 0 us runtime gain. Audit evidence gain: current total is 121,116,760,791 tokens, delta is 3,431,972,122 tokens, GPT-5.5 base API-equivalent cost is 93,833.434913 USD, and dashboard/chart paths remained unchanged.

## Decision 38 - 2026-05-30 late-day text-only token refresh

Problem: The operator requested another info update and full commit/push after the 12:10 snapshot, while local Codex JSONL kept moving and the repository still contains active parallel-agent churn.
Solution: Recheck the official GPT-5.5 pricing boundary, rerun only `Tools/CodexTokenUsageFastRefresh_20260528.py`, validate the actual persisted JSON schema, and checkpoint with explicit `1334` and root `CON` staging exclusions.
Rejected Alternatives: Running the dashboard/chart pipeline was rejected because the request did not ask for image/chart regeneration. Keeping the first failed validator result hidden was rejected because it used a stale `summary` schema assumption. Running `dotnet build` was rejected because this is offline telemetry/docs work and compilation throttling forbids unnecessary build pressure.
Scalability potential: Runtime Low/Middle/High/Ultra tiers unaffected. Reporting scalability improves because late-day refreshes can validate the actual report schema while preserving text-only artifact scope.
Hardware Impact: 0 us runtime gain. Audit evidence gain: current total is 123,468,373,111 tokens, delta is 2,351,612,320 tokens, GPT-5.5 base API-equivalent cost is 95,551.436967 USD, and dashboard/chart paths remained unchanged.

## Decision 39 - 2026-05-31 non-specialist scale explainer

Problem: The operator requested updated token stats and a scale explanation that a non-specialist can understand, but previous reports exposed mostly raw token and dollar totals.
Solution: Add a generated `layperson_scale` JSON block and Markdown/ledger section with page, book, reading-time, game-price, workstation-price, cache-share, and burn-rate analogies while marking token-to-word conversion as a rough communication heuristic.
Rejected Alternatives: Writing only a chat explanation was rejected because it would not update the persistent evidence artifact. Generating new PNG charts was rejected because this request asked for status refresh and explanation, not images.
Scalability potential: Runtime Low/Middle/High/Ultra tiers unaffected. Reporting scalability improves because every future text refresh now emits the same human-scale block from source telemetry.
Hardware Impact: 0 us runtime gain. Audit evidence gain: current total is 124,505,240,345 tokens, delta is 1,036,867,234 tokens, GPT-5.5 base API-equivalent cost is 96,330.542859 USD, all-time scale is about 186,757,861 500-word pages / 1,167,237 80k-word books / 710.65 continuous reading years, and current burn is about 93,983 pages per hour.

## Decision 40 - 2026-05-31 maximum graph detail refresh

Problem: The dashboard still over-weighted broad totals and under-exposed cache economics, blended cost quality, heatmap timing, source/file pressure, top output/reasoning owners, current velocity, human-scale burn, and token-vs-git same-day correlations.
Solution: Expand the generated dashboard pipeline to 112 reproducible PNG charts, add 7/14/30/60/90-day windows, add token/cost weekday-hour heatmaps, add cache-savings/no-cache/effective-price/output-share views, add model-effort output/reasoning views, add source economics and largest-file charts, and add correlation-only token-vs-git productivity charts.
Rejected Alternatives: Redrawing only the old 41 charts was rejected because it would not answer the operator's request for maximum detail. Manual PNG creation was rejected because future refreshes need deterministic regeneration. Per-task productivity attribution was rejected because local telemetry only supports same-day correlation, not exact task-to-commit billing proof.
Scalability potential: Runtime Low/Middle/High/Ultra tiers unaffected. Reporting scalability improves because every future dashboard refresh now carries detailed timing/economics/scale/correlation surfaces without one-off spreadsheet work.
Hardware Impact: 0 us runtime gain. Audit evidence gain: refreshed total is 124,526,072,948 tokens; chart manifest is 112/112 exact with 0 missing, 0 extra, and 0 bad PNG signatures. Apex JSON SHA-256 is `bed0d284bb008000b0444cbe2c5d79bd230430beaa3deb60f7bc437431d505bd`. Compile proof is correctly blocked by CPU 100 percent and active `dotnet` PID 20236.

## Decision 41 - 2026-06-02 refresh under heavy workspace load

Problem: The operator requested a full update/commit/push after date rollover, but the live workspace had deleted TOKEN_USAGE_AUDIT memory files and the old fast-refresh hourly replay path reread 244 recent JSONL files, causing empty `exit code -1` runs before any report was written.
Solution: Restore only TOKEN_USAGE_AUDIT status/rationale/log files, keep the current GPT-5.5 pricing evidence boundary, and change the fast refresh to inherit previous hourly buckets while adding exact post-cutoff deltas from the 61 changed JSONL files.
Rejected Alternatives: Treating the empty `exit code -1` as success was rejected. Killing unrelated Python services was rejected after command-line inspection showed uvicorn/bot/MCP processes, not token-audit orphans. Replaying hundreds of stale hourly JSONL files was rejected because the previous snapshot already owns those buckets and the request needs a current delta refresh.
Scalability potential: Runtime Low/Middle/High/Ultra tiers unaffected. Reporting scalability improves because date-rollover refresh no longer depends on rereading hundreds of older hourly JSONL files under parallel-agent load.
Hardware Impact: 0 us runtime gain. Audit evidence gain: current total is 129,368,782,512 tokens, delta is 4,842,709,564 tokens, chart manifest is 112/112 exact, and apex JSON SHA-256 is `086b098b84028901c657eba5ff0711e3807f2d58b7daf6ddc0184912f20980c2`. Compile proof is blocked by CPU 52 percent and active dotnet processes.

## Decision 42 - 2026-06-02 full workspace checkpoint gate

Problem: The operator requested full commit/push, but the staged workspace contained 4169 files of mixed cross-agent changes and `git diff --cached --check` failed on trailing whitespace in Unity `.meta/.mat`, package metadata, and temporary prompt text.
Solution: Strip only trailing spaces/tabs and EOF blank whitespace from the exact staged files reported by the git whitespace gate, restage with `CON` and `*1334*` exclusions, and rerun protected-path plus whitespace validation before commit.
Rejected Alternatives: Reverting unrelated cross-agent changes was rejected because the operator asked to commit/push everything. Staging root `CON` was rejected because it is a Windows-reserved path. Staging `1334` files was rejected by explicit operator instruction. Running `dotnet build` was rejected because compile-throttle evidence already showed CPU/compiler contention and TOKEN_USAGE_AUDIT changed offline reports/tools.
Scalability potential: Runtime Low/Middle/High/Ultra tiers unaffected; this is repository hygiene around a telemetry checkpoint.
Hardware Impact: 0 us runtime gain. Process gain: staged path scan returned `NO_STAGED_1334_OR_CON`; `git diff --cached --check` is clean.
