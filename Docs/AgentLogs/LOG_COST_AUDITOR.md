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
