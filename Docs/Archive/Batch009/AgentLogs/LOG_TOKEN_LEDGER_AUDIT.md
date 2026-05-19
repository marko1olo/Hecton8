# LOG_TOKEN_LEDGER_AUDIT

## 2026-05-18 Token Ledger Rebase

What was wrong:

- Existing compute token counts stopped at the 2026-05-17 15:39 checkpoint.
- A naive fresh pass initially undercounted HECTON/Hades because `\\?\C:\hades\Hecton8` was excluded by bad path normalization.

What was done:

- Re-read token-accounting mandates and existing compute audit docs.
- Ran a full read-only JSONL pass over `C:\Users\danat\.codex\sessions/**/*.jsonl`.
- Cross-checked live totals with `C:\Users\danat\.codex\state_5.sqlite`.
- Updated the compute audit docs with a 2026-05-18 17:34/17:35 rebase.

Cinematic Cheats used:

- None. Audit/documentation only.

Exact microseconds saved:

- 0 us measured. No runtime code changed.

Key numbers:

- SQLite HECTON/Hades live tokens: 54,517,775,171.
- JSONL HECTON/Hades final tokens: 54,468,241,841.
- Last 24h HECTON/Hades: 2,862,892,706 tokens.
- JSONL HECTON/Hades cache-aware estimate: USD 37,575.94.
- SQLite HECTON/Hades live estimate: USD 37,610.11.
- No-cache equivalent for SQLite HECTON/Hades: USD 246,711.58.
- Cached-input ratio: 96.007%.

Verification:

- `git diff --check` passed after documentation edits.
- Compile not run because only Markdown docs changed.

