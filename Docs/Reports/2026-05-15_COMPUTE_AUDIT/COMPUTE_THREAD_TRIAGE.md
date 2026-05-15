# COMPUTE THREAD TRIAGE

Status: AUDIT COMPLETE
Snapshot: 2026-05-15T03:07+04:00
Source: `C:\Users\danat\.codex\state_5.sqlite`
Method: read-only SQLite query over `threads.tokens_used`
Attribution detail: `COMPUTE_THREAD_ATTRIBUTION.md`
Validation detail: `COMPUTE_VALIDATION_FORENSICS.md`
Top-100 value detail: `COMPUTE_THREAD_VALUE_AUDIT.md`

## Verdict

The audit target is not 764 threads. The target is the heavy head.

| Slice | Tokens | Share |
|---|---:|---:|
| Top 1 | 518,697,166 | 1.181% |
| Top 5 | 2,315,069,669 | 5.272% |
| Top 10 | 3,958,374,314 | 9.014% |
| Top 30 | 9,492,793,103 | 21.618% |
| Top 50 | 13,855,912,207 | 31.554% |
| Top 100 | 21,944,967,637 | 49.975% |
| Top 125 | 24,808,168,646 | 56.495% |
| Top 250 | 33,698,725,001 | 76.741% |
| Top 500 | 41,801,802,422 | 95.194% |
| All 764 | 43,912,005,185 | 100.000% |

Top-100 minimum entry: 126,213,552 tokens.

## Top-100 Shape

| Dimension | Finding |
|---|---|
| Top-100 token mass | 21,944,967,637 |
| Top-100 share | 49.975% |
| Blended cost proxy | USD 14,604.60 |
| `gpt-5.5` share inside top-100 | 17,327,646,181 tokens, 78.96% |
| `gpt-5.4` share inside top-100 | 4,616,829,537 tokens, 21.04% |
| `C:\hades` root CWD share | 12,108,287,000 tokens, 55.18% |
| `C:\hades\Hecton8` CWD share | 9,836,188,718 tokens, 44.82% |
| `danger-full-access` + `never` share | 19,634,146,655 tokens, 89.47% |
| Prompt IDs found in top-100 | 1 real ID, 99 missing IDs |

Cost proxy uses the project-wide effective blended price, USD 0.665510 per 1M total tokens. It is not a per-thread invoice because the SQLite thread row does not expose cached-input/output split.

## Updated-Day Concentration

| Updated day UTC | Threads | Tokens | Top-100 share |
|---|---:|---:|---:|
| 2026-05-09 | 30 | 6,267,605,674 | 28.56% |
| 2026-05-03 | 15 | 3,689,132,771 | 16.81% |
| 2026-05-01 | 8 | 1,530,226,410 | 6.97% |
| 2026-05-14 | 6 | 1,401,728,221 | 6.39% |
| 2026-04-29 | 6 | 1,352,752,124 | 6.16% |
| 2026-05-06 | 5 | 1,166,164,394 | 5.31% |
| 2026-04-09 | 2 | 919,471,793 | 4.19% |
| 2026-05-05 | 5 | 915,424,948 | 4.17% |
| 2026-05-10 | 3 | 912,082,367 | 4.16% |
| 2026-05-07 | 3 | 706,052,158 | 3.22% |

2026-05-09 is the heaviest updated day in top-100. That is the first forensic day to inspect if attribution is required.

## Top-30 Threads

Titles are evidence hints, not proof of productive output. Non-ASCII titles are intentionally left as `[non-ascii title]` to keep this root triage stable.

| # | Thread | Tokens | Model | Updated UTC | CWD | Title hint |
|---:|---|---:|---|---|---|---|
| 1 | `019e1859-0e01-77b2-a8c6-b5586ccc5c8c` | 518,697,166 | `gpt-5.5` | 2026-05-14 10:56 | `C:\hades` | `[non-ascii title] UI` |
| 2 | `019d6329-de82-74e2-83ca-450539a61cec` | 490,407,394 | `gpt-5.4` | 2026-04-09 13:02 | `C:\hades\Hecton8` | `MASTER_RELEASE_WORK_PLAN` |
| 3 | `019dde7c-df90-7791-b4b4-d49c8450a9be` | 468,267,072 | `gpt-5.5` | 2026-05-03 17:53 | `C:\hades\Hecton8` | `Split monoliths into services` |
| 4 | `019d67a6-6823-7b82-94f9-a3167b8e0286` | 429,064,399 | `gpt-5.4` | 2026-04-09 11:13 | `C:\hades\Hecton8` | `MASTER_RELEASE_WORK_PLAN` |
| 5 | `019dcf19-407b-75f2-99e4-54d0217d9d14` | 408,633,638 | `gpt-5.4` | 2026-04-29 22:03 | `C:\hades\Hecton8` | `Fix C# compile blockers` |
| 6 | `019dfc26-b869-7bf3-a254-de3f0a8111e9` | 349,084,791 | `gpt-5.5` | 2026-05-10 11:37 | `C:\hades` | `Add basin detection engine` |
| 7 | `019def23-b6e4-7d72-9992-a10a17f0d7db` | 340,869,732 | `gpt-5.5` | 2026-05-06 07:10 | `C:\hades` | `[non-ascii title]` |
| 8 | `019dfd9c-337f-7842-81b5-e4b862462b87` | 333,924,928 | `gpt-5.5` | 2026-05-09 14:01 | `C:\hades` | `Wire quest progression` |
| 9 | `019dda15-a011-7a12-a62c-1bc748a269a3` | 310,515,372 | `gpt-5.5` | 2026-05-03 17:00 | `C:\hades\Hecton8` | `XENO-BOTANY master prompt` |
| 10 | `019dda14-db04-74b0-91a0-e1088c40bc88` | 308,909,822 | `gpt-5.5` | 2026-05-03 17:34 | `C:\hades\Hecton8` | `Add procedural flora distribution` |
| 11 | `019dfd94-84ae-7b53-b689-cc893af60675` | 306,217,896 | `gpt-5.5` | 2026-05-09 14:05 | `C:\hades` | `Optimize fabricator UI` |
| 12 | `019dfd9d-e339-7601-9dca-e945563ed5ff` | 306,165,757 | `gpt-5.5` | 2026-05-09 13:08 | `C:\hades` | `Update predator AI stealth` |
| 13 | `019def93-fcb8-7960-8196-412d6f9ef869` | 296,179,586 | `gpt-5.5` | 2026-05-07 15:04 | `C:\hades` | `Add abyssal visual effects` |
| 14 | `019dd8e6-6149-7bb3-8900-cc0f69f9b12f` | 292,161,127 | `gpt-5.5` | 2026-05-03 17:37 | `C:\hades\Hecton8` | `[non-ascii title]` |
| 15 | `019dfc29-331e-7c21-b2ba-b6af81f9445d` | 290,450,970 | `gpt-5.5` | 2026-05-09 14:03 | `C:\hades` | `Update headless simulation` |
| 16 | `019dfd93-edaf-78d1-a75b-2786eb254071` | 288,545,985 | `gpt-5.5` | 2026-05-10 11:04 | `C:\hades` | `Audit procedural audio DSP` |
| 17 | `019dfe4e-0a73-7eb1-a5ba-d201ae041c1c` | 283,175,153 | `gpt-5.5` | 2026-05-09 14:37 | `C:\hades` | `Unify bootstrap governance` |
| 18 | `019ddea2-fe00-7c62-b0c3-25b81e28794c` | 282,042,919 | `gpt-5.5` | 2026-05-03 14:50 | `C:\hades\Hecton8` | `Remove Instance and PDA events` |
| 19 | `019dcffb-783a-7690-b720-8ac0ceb29c3b` | 277,725,417 | `gpt-5.5` | 2026-05-01 12:12 | `C:\hades\Hecton8` | `Fix native buffer race and AUP drift` |
| 20 | `019d925a-0bf6-7c02-832f-bd2ef5cf13ca` | 275,724,279 | `gpt-5.4` | 2026-04-20 20:38 | `C:\hades\Hecton8` | `[non-ascii title]` |
| 21 | `019dfd50-3d75-7c21-a49c-d1369048a927` | 274,451,591 | `gpt-5.5` | 2026-05-10 12:44 | `C:\hades` | `[non-ascii title] docs` |
| 22 | `019dda12-f963-7133-9e7e-65774a6601c2` | 274,181,378 | `gpt-5.5` | 2026-05-03 17:41 | `C:\hades\Hecton8` | `Add item interaction matrix` |
| 23 | `019dfd9d-3d69-75b1-86ac-d6837fbe922c` | 272,789,160 | `gpt-5.5` | 2026-05-09 14:21 | `C:\hades` | `Optimize HUD render graph` |
| 24 | `019d9259-d4c0-7751-b30a-ba423b90929e` | 269,031,291 | `gpt-5.4` | 2026-04-21 12:27 | `C:\hades\Hecton8` | `[non-ascii title]` |
| 25 | `019dda14-b01c-7423-a02d-d7cd84914afb` | 262,370,401 | `gpt-5.5` | 2026-05-03 17:14 | `C:\hades\Hecton8` | `Add base module integrity states` |
| 26 | `019dd8d8-8d18-7fd2-8336-334fd3be0e14` | 261,225,332 | `gpt-5.5` | 2026-05-01 08:15 | `C:\hades\Hecton8` | `[non-ascii title]` |
| 27 | `019dabac-3c47-74f2-8150-6da883dd6b88` | 259,318,739 | `gpt-5.4` | 2026-04-22 15:50 | `C:\hades\Hecton8` | `[non-ascii title] boids` |
| 28 | `019def94-ec22-78d1-b0e3-1b61c192a31a` | 257,037,410 | `gpt-5.5` | 2026-05-07 07:14 | `C:\hades` | `Implement async habitat stress` |
| 29 | `019dfe4f-4f6f-7e40-8126-43f1b5a93a20` | 253,340,615 | `gpt-5.5` | 2026-05-09 13:24 | `C:\hades` | `Implement black box telemetry` |
| 30 | `019dcffb-ddcd-78e1-8504-eef0baa9a02d` | 252,283,783 | `gpt-5.5` | 2026-05-01 08:38 | `C:\hades\Hecton8` | `Harden save/load pipeline` |

## Honest Boundary

This file identifies expensive threads. It does not prove waste.

To convict a thread as waste, collect all four:
- thread id to changed files;
- meaningful LOC delta;
- compile/test result after the thread;
- H-Phi or other project-quality delta.

Without those four, the only valid label is `HIGH-BURN CANDIDATE`.

Top-30 rollout JSONL attribution is preserved in `COMPUTE_THREAD_ATTRIBUTION.md`.
