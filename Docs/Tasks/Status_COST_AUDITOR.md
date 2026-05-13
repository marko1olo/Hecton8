# Status_COST_AUDITOR

PROMPT IDENTIFIED: COST_AUDITOR | DOMAIN: PROJECT_AUDIT_COSTING | TASK COUNT: 1

Evidence class: STATIC_SOURCE / STATIC_DOC / GIT_METADATA / OFFICIAL_WEB_PRICING.

- [x] Task 1: Inspect project authority, mandates, domains, agents, code volume, git history, dialogue/log corpus, and model pricing. Justification: used STATIC_SOURCE/GIT_METADATA plus official pricing; parsed cumulative `total_token_usage` per session to avoid double-counting intermediate events. Alternatives rejected: estimating from file size alone, commit count alone, or chat memory. Microseconds saved: 0 claimed.

Result snapshot:
- Project scan excluding `.git/Library/Temp/obj/bin/.codex*`: 37,289 files, 5,546.78 MB.
- Current first-party scripts: 1,365 `.cs`, 852,489 lines, 35.47 MB.
- Total `.cs`: 4,964 files, 1,632,148 lines.
- Docs: 1,484 files, 59.45 MB. `.agents-skills`: 75 mandate `.txt` files plus README.
- Active batch prompt IDs in `Docs/Tasks/CURRENT_BATCH.md`: 30.
- Active status files: 22. Active rationale files: 22. Active logs: 9.
- Git: 180 commits, first `2026-03-03`, last `2026-05-12`; tracked files 31,888; dirty diff 364 files / 19,959 insertions / 17,320 deletions; untracked 318.
- Codex logs: 663 `.jsonl`, 6,947.50 MB; 644 session files with usage counters.
- Logged usage total: 37,913.42M tokens; input 37,783.998M; cached input 36,240.917M; output 129.162M; reasoning output 45.403M.
- API-equivalent listed-model cost with cache pricing: about $23,377.03. No-cache worst-case for the listed modern models: about $152,664.60.

Missing-log approximation:
- Local Codex usage starts `2026-04-03`; git starts `2026-03-03`.
- Missing pre-log git work: 88 commits / 4,638,267 changed lines.
- Covered usage-window work: 98 commits / 4,916,108 changed lines / 38,174.80M tokens / about $23,632.94.
- Missing estimate by commits: about $21,221.42.
- Missing estimate by churn: about $22,297.29.
- Revised whole-history estimate: about $44,854.36 to $45,930.23. Practical uncertainty band: $35k to $60k.

Correction from user:
- Agents began around March 20, and early use was much lower than current use.
- Superseded broad missing estimate above.
- Corrected missing window: March 20-April 2 = 40 commits / 1,458,116 changed lines.
- Full-current-intensity proxy for that window: $7,009.52 to $9,646.10.
- Lower early-intensity estimate at 25%-60%: $2,081.95 to $4,996.69.
- Corrected likely total: logged floor $23,632.94 plus missing $2,081.95-$4,996.69 = about $25.7k-$28.6k. Practical band: $25k-$32k.
