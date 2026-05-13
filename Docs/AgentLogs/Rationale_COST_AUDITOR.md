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

Problem: GPT-5.5/GPT-5.4 `xhigh` pricing is not a separate tariff.
Solution: Use official model input/cached-input/output rates and treat `xhigh` as a driver of larger output/reasoning token volume.
Rejected Alternatives: Do not invent a multiplier for `xhigh`. Do not apply long-context uplift unless a request crosses the official long-context threshold; local Codex context window observed in sample was 258,400 tokens.
Scalability potential: For future spend reduction, lower effort or context compaction is the practical lever, not runtime code changes.
Hardware Impact: Runtime impact on i3/MX350 is 0 us/frame.
