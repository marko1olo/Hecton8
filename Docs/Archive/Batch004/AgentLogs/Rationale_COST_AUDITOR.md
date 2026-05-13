# Rationale_COST_AUDITOR

Problem: Estimate token spend without exact API billing export.
Solution: Use local file sizes, line counts, git metadata, and available agent/dialogue logs as STATIC_SOURCE/GIT_METADATA evidence. Use official OpenAI pricing for model cost basis. Report as ranges, not false precision.
Rejected Alternatives: Do not infer exact spend from commit count alone; do not claim hidden provider usage or cache ratios that are not present in local artifacts.
Scalability potential: Low/Middle/High/Ultra runtime tiers are not touched. This is process accounting only.
Hardware Impact: Runtime impact on i3/MX350 is 0 us/frame because no runtime code is changed.

Problem: Local Codex logs contain cumulative token events, not billing invoices.
Solution: For each `.jsonl` session, parse the last `total_token_usage` record and group by model/reasoning effort. This avoids summing every intermediate token_count event in a long session.
Rejected Alternatives: File-size-to-token conversion was rejected once real usage counters were found. Raw `total_tokens * output price` was rejected because cached input has a separate discounted rate and output already includes reasoning output.
Scalability potential: Low/Middle/High/Ultra runtime tiers unchanged. Process insight: high cache-hit ratio means context reuse is the dominant cost shape.
Hardware Impact: Runtime impact on i3/MX350 is 0 us/frame.

Problem: User reported that Codex files were deleted earlier, so local `.codex` logs undercount historical usage.
Solution: Compare first local Codex usage date with git history. Local usage starts `2026-04-03`; git work starts `2026-03-03`. Use covered window `2026-04-03..2026-05-13` to derive two missing-period estimators: cost per commit and cost per changed line.
Rejected Alternatives: Do not assume missing logs equal zero. Do not use only calendar days because agent intensity is bursty; use git commits and numstat churn as activity proxies.
Scalability potential: Accounting only. Future token savings require smaller prompt payloads, shorter retained context, and lower reasoning effort on non-critical tasks.
Hardware Impact: Runtime impact on i3/MX350 is 0 us/frame.

Problem: Estimator uncertainty.
Solution: Report floor, proxy estimate, and uncertainty band. Covered window produced `$23,632.94` API-equivalent over `98` commits and `4,916,108` changed lines. Pre-log period has `88` commits and `4,638,267` changed lines. Commit proxy estimates missing `$21,221.42`; churn proxy estimates missing `$22,297.29`.
Rejected Alternatives: No single exact number without billing export and deleted session files. No-cache worst case is not normal observed behavior because local logs show very high cached input ratio.
Scalability potential: Not runtime relevant.
Hardware Impact: Runtime impact on i3/MX350 is 0 us/frame.

Problem: User clarified agents started around March 20 and early use was much lower than current use.
Solution: Supersede the broad March 3-April 2 missing-window estimate. Use only March 20-April 2 as plausible deleted-agent window: `40` commits and `1,458,116` changed lines. Full-current-intensity proxy would be `$7,009.52..$9,646.10`; because early use was explicitly lower, apply `25%..60%` intensity -> missing `$2,081.95..$4,996.69`.
Rejected Alternatives: Do not keep the previous `$45k` whole-history estimate as best estimate. Do not count March 3-19 as agent spend without user evidence.
Scalability potential: Accounting only.
Hardware Impact: Runtime impact on i3/MX350 is 0 us/frame.

Problem: GPT-5.5/GPT-5.4 `xhigh` pricing is not a separate tariff.
Solution: Use official model input/cached-input/output rates and treat `xhigh` as a driver of larger output/reasoning token volume.
Rejected Alternatives: Do not invent a multiplier for `xhigh`. Do not apply long-context uplift unless a request crosses the official long-context threshold; local Codex context window observed in sample was 258,400 tokens.
Scalability potential: For future spend reduction, lower effort or context compaction is the practical lever, not runtime code changes.
Hardware Impact: Runtime impact on i3/MX350 is 0 us/frame.
