# LOG_COST_AUDITOR

## 2026-05-13 Cost Audit Start

What was wrong: Token spend cannot be proven from codebase alone without billing/export data.
What was done: Started static audit of project size, agent corpus, git history, dialogue/log corpus, and official model pricing.
Cinematic Cheats used: None. Process audit only.
Exact Microseconds saved: 0 claimed.

## 2026-05-13 Cost Audit Result

What was wrong: No billing export was present. Exact dollars cannot be proven from repo state alone.
What was done: Read project authority, selected evidence/cost mandates, scanned code/docs/agents/git, parsed local Codex `.jsonl` token usage counters, and calculated API-equivalent dollars using official OpenAI model rates.
Cinematic Cheats used: None. Accounting only.
Exact Microseconds saved: 0 claimed.

Evidence:
- STATIC_SOURCE: project scan excluding generated/cache directories reported 37,289 files / 5,546.78 MB.
- STATIC_SOURCE: first-party scripts reported 1,365 `.cs` / 852,489 lines / 35.47 MB.
- STATIC_SOURCE: all `.cs` reported 4,964 files / 1,632,148 lines.
- STATIC_DOC: `.agents-skills` reported 75 mandate `.txt` files plus README; `CURRENT_BATCH.md` contained 30 prompt IDs.
- GIT_METADATA: 180 commits; first `2026-03-03`; last `2026-05-12`; dirty diff 364 files, 19,959 insertions, 17,320 deletions; 318 untracked files.
- LOCAL_DIALOGUE_USAGE: 663 Codex `.jsonl` files / 6,947.50 MB; 644 sessions with usage counters.

Token totals:
- All parsed sessions: 37,913.42M total tokens.
- Input: 37,783.998M.
- Cached input: 36,240.917M.
- Output: 129.162M.
- Reasoning output: 45.403M, included in output tokens for cost.

API-equivalent cost:
- `gpt-5.5 xhigh`: 352 sessions, 22,302.64M input, 21,421.90M cached input, 69.65M output, 22.49M reasoning output -> about $17,204.02.
- `gpt-5.4 xhigh`: 110 sessions, 3,108.42M input, 2,976.29M cached input, 14.80M output, 7.11M reasoning output -> about $1,296.39.
- All parsed `gpt-5.4` efforts: about $6,172.62.
- Listed modern models total with cache pricing: about $23,377.03.
- Listed modern models no-cache worst-case: about $152,664.60.

Residual risk:
- This is not an invoice.
- Codex plan/rate-limit metadata in logs is not a dollar charge.
- Price changes after the official pricing snapshot invalidate dollar conversion.

## 2026-05-13 Missing Codex Log Approximation

What was wrong: User confirmed Codex files had been deleted at some point. Previous `$23.4k` estimate was a logged-usage floor, not whole-project history.
What was done: Compared local Codex dialogue coverage against git history.
Cinematic Cheats used: None. Accounting only.
Exact Microseconds saved: 0 claimed.

Coverage evidence:
- First local Codex usage date with `total_token_usage`: `2026-04-03`.
- Last local Codex usage date in scan: `2026-05-13`.
- Git work begins: `2026-03-03`.
- Git before first usage log: `88` commits, `4,638,267` changed lines.
- Git during usage window: `98` commits, `4,916,108` changed lines.
- Git days with commits but no Codex usage inside `2026-04-03..2026-05-13`: none found.
- Current dirty diff during later check: `60` files, `1,187` insertions, `374` deletions, `16` untracked files. This is likely already covered by active `2026-05-13` Codex logs, not a separate missing month.

Covered-window usage:
- Sessions: `650`.
- Tokens: `38,174.80M`.
- API-equivalent cost using known GPT-5.5/GPT-5.4 rates and fallback GPT-5.4 rate for older nonlisted models: `$23,632.94`.
- Cost per commit proxy: `$241.15`.
- Cost per changed-line proxy: `$0.0048`.
- Token per commit proxy: `389.54M`.
- Token per changed-line proxy: `7,765.25`.

Missing-period estimate:
- By commits: missing `34,279.41M` tokens / `$21,221.42`.
- By churn: missing `36,017.29M` tokens / `$22,297.29`.
- Total by commits: `72,454.21M` tokens / `$44,854.36`.
- Total by churn: `74,192.09M` tokens / `$45,930.23`.

Operational answer:
- Hard logged floor: about `$23.6k`.
- Best proxy estimate including deleted early logs: about `$45k`.
- Practical uncertainty band: `$35k..$60k` depending on early model mix, cache behavior, and whether March used the same xhigh workflow.

## 2026-05-13 User Correction: Agents Began Around March 20

What was wrong: The previous missing-log approximation overcounted March 3-March 19 as if Codex-agent usage already existed.
What was done: Recomputed the missing window as March 20-April 2 only, and applied lower early-intensity coefficients.
Cinematic Cheats used: None. Accounting only.
Exact Microseconds saved: 0 claimed.

Corrected evidence:
- Local usage starts: `2026-04-03`.
- Plausible deleted-agent window after user correction: `2026-03-20..2026-04-02`.
- Git in that corrected window: `40` commits, `801,848` added lines, `656,268` deleted lines, `1,458,116` changed lines.
- Covered current-intensity baseline: `$23,632.94` over `98` commits and `4,916,108` changed lines.

Corrected missing estimate:
- Full-current-intensity by commits: `$9,646.10`.
- Full-current-intensity by churn: `$7,009.52`.
- Full-current-intensity average: `$8,327.81`.
- Early lower-use estimate at 25% intensity: `$2,081.95`.
- Early lower-use estimate at 50% intensity: `$4,163.91`.
- Early lower-use estimate at 60% intensity: `$4,996.69`.

Corrected operational answer:
- Logged floor: `$23,632.94`.
- Revised likely total with deleted early logs: about `$25.7k..$28.6k`.
- Practical band after correction: `$25k..$32k`.
- Previous `$45k` estimate is now superseded and should be treated as an upper-heavy model based on a false start-date assumption.
