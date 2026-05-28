

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
