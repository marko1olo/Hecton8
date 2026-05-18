<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# Mass Lead Verification And Pitch Workflow

Status: operating manual for agents / pre-screenshot
Public stance: single-player-first / no co-op promise
Runtime impact: none

## Goal

Turn raw public lead data into a small number of high-quality, personalized outreach candidates. The point is not to email 5000 creators. The point is to extract 50-150 real fits from 5000 raw candidates without burning reputation.

## Current Data Inputs

- `Docs/Marketing/Data/RAW_PUBLIC_CREATOR_LEADS_2026-05-18.csv` - 7155 raw source rows.
- `Docs/Marketing/Data/UNIQUE_CREATOR_VERIFICATION_QUEUE_2026-05-18.csv` - 4970 unique public channel profiles.
- `Docs/Marketing/Data/PRIORITY_CREATOR_SHORTLIST_FROM_RAW_2026-05-18.csv` - first 250 rows to verify.
- `Docs/Marketing/CreatorOutreach/A_TIER_PERSONALIZED_PITCHES.md` - named A-tier pitch drafts.
- `Docs/Marketing/CreatorOutreach/SEGMENT_PITCH_MATRIX.md` - segment pitch rules.
- `Docs/Marketing/CreatorOutreach/CREATOR_CRM_SCHEMA_AND_SCORING.md` - CRM state model.

## Agent Batch Protocol

Each verification agent gets one batch of 25 leads from the priority shortlist.

For each lead, the agent must record:

- channel name;
- official YouTube/Twitch/site URL;
- country/language;
- current activity check;
- last relevant survival/horror/engineering upload;
- business contact route if public;
- brand safety notes;
- score;
- pitch segment;
- one personalized opener;
- matching HECTON-8 asset needed before outreach;
- final status.

Never record guessed emails.

## Status Values

| Status | Meaning |
|---|---|
| `RAW_PUBLIC_INDEX_NOT_CONTACT_READY` | Default scrape state. Do not contact. |
| `VERIFYING` | Agent is checking source/profile. |
| `VERIFIED_NOT_CONTACTED` | Official route and fit are confirmed, but no outreach sent. |
| `NEEDS_ASSET` | Good lead, but no matching screenshot/clip/demo exists yet. |
| `DO_NOT_CONTACT` | Dead, unsafe, irrelevant, fake, stolen content, scam risk, or bad fit. |
| `CONTACTED_BATCH_01` | First outreach sent. |
| `FOLLOWUP_01_SENT` | One follow-up sent after 7-10 days. |
| `RESPONDED_POSITIVE` | Interested or requested build/materials. |
| `RESPONDED_NEGATIVE` | Declined or no fit. |
| `COVERED` | Published/streamed/mentioned. |

## Scoring

Start at 0.

| Signal | Points |
|---|---:|
| Direct Subnautica/Subnautica Below Zero history | +4 |
| Appears across 4+ adjacent source games | +4 |
| Recent upload within 90 days | +3 |
| Survival/horror/engineering still active | +3 |
| Mid-size creator likely reachable | +3 |
| Strong long-form first-look fit | +2 |
| Regional market where localized pitch exists | +2 |
| Public business contact route found | +2 |
| High brand safety / non-toxic channel | +2 |
| Requires paid slot only | -2 |
| Mostly shorts/reuploads/no commentary | -2 |
| Very large unreachable creator | -2 |
| Co-op-only audience expectation | -2 |
| Dead channel or no relevant content in 12 months | -5 |
| Key reseller/scam/impersonation risk | -10 and `DO_NOT_CONTACT` |

Scores:

- 10+ = A candidate;
- 6-9 = B candidate;
- 3-5 = hold;
- below 3 = do not contact unless special reason.

## Pitch Production Formula

Every verified lead gets this structure:

1. `Specific channel fit`: one sentence naming their content pattern.
2. `HECTON-8 match`: one sentence mapping to pressure/machinery/salvage/black water/Seed Ship.
3. `Boundary`: not a co-op promise, not a clone-war pitch.
4. `Asset`: one real screenshot/clip/demo link.
5. `Ask`: one clear request.

Template:

Hi [Name],

Your channel fits because [specific verified content pattern].

HECTON-8 is a single-player-first underwater survival game about [segment-specific hook]. This is not a "Subnautica killer" pitch and not a co-op promise.

The reason I think it might work for your audience: [one real asset-backed sentence].

Assets: [Steam/screens/clip/demo]

If useful, I can send the demo/press kit when the slice is ready.

## Segment Openers

### Direct Underwater Survival

"Your channel has direct Subnautica/underwater survival history, so I am not going to pitch this as a clone. HECTON-8 is aimed at the colder side of the fantasy: pressure, machinery, salvage, black-water visibility, and a base that behaves like a pressure vessel."

### Survival Route Risk

"Your survival content works because viewers can follow risk and recovery. HECTON-8's relevant angle is the expedition loop: leave safety, salvage under pressure, read warnings, and decide whether the route is still worth it."

### Engineering Base Systems

"Your audience pays attention when systems matter. HECTON-8's pitch for them is base-as-machine: pumps, oxygen, pressure, power, seals, tools, and machinery that visibly changes survival decisions."

### Abyss Horror Pressure

"Your horror audience does not need a bright monster thumbnail. HECTON-8's angle is instruments, sound, darkness, hull stress, and the moment the player realizes the ocean has changed the rules."

## Batch Cadence

Pre-screenshot:

- Verify 25 leads per agent per batch.
- Do not send outreach.
- Mark `NEEDS_ASSET` if fit is good but no matching asset exists.
- Promote only the best 50 to `VERIFIED_NOT_CONTACTED`.

After first screenshots:

- Send 10 critique-first creator messages to B/A candidates who tolerate WIP.
- Send no keys.
- Ask for blunt visual fit, not coverage.

After Steam page:

- Send 20 wishlist-page pitches to verified creators.
- Include one asset and one reason.
- Track replies and Steam traffic.

After demo:

- Send 30-50 demo/key pitches.
- Use Steam keys only through the key policy.
- Stop after two non-responses.

## What Agents Must Not Do

- Do not fabricate emails.
- Do not DM from unofficial accounts.
- Do not offer money unless the human approves budget.
- Do not promise co-op.
- Do not promise performance.
- Do not call HECTON-8 a Subnautica killer.
- Do not pitch a creator whose content you did not check.
- Do not send the same message to 100 people.

## Weekly Output Format

Each agent must append a weekly table:

| Date | Batch | Raw leads checked | Verified | Needs asset | Do not contact | A candidates | Notes |
|---|---|---:|---:|---:|---:|---:|---|

Then list the 10 best verified leads with custom opener and required asset.

