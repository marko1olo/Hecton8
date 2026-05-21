<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# Mass Lead Verification And Pitch Workflow

Status: operating manual for agents / pre-screenshot
Public stance: single-player-first scope / proof-first public copy
Runtime impact: none
Product gate: `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`.
Selected V0 route: `Docs/ARCHITECTURE/FIRST_20_MINUTES_ROUTE_BRIEF.md`
(`Copper Wire`: swim -> resource -> collect -> craft -> save/load).
Creator outreach may verify leads before the slice exists, but send-ready status
requires real screenshot/clip/demo proof from that route.

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

Use the live CRM statuses from `Docs/Marketing/Data/CREATOR_VERIFICATION_TEMPLATE.csv`.

| Status | Meaning |
|---|---|
| `VERIFY_BEFORE_CONTACT` | Candidate has enough fit to recheck official route/current activity before any send; not send approval. |
| `NEEDS_ASSET` | Good lead, but no matching screenshot/clip/demo exists yet. |
| `LOW_PRIORITY_VERIFY_LATER` | Archive/revisit later; do not spend route-recheck time before first assets. |
| `DO_NOT_CONTACT` | Dead, unsafe, irrelevant, fake, stolen content, scam risk, or bad fit. |
| `CONTACTED` | First outreach actually sent by a human owner and logged with `outreach_batch`, `sent_date`, `contact_route_verified_for_send`, `asset_ids_sent`, `creator_utility_score`, `send_route_class`, and matching asset/AB-009 gates where required. |
| `REPLIED` | Reply received after a logged send; classify `reply_status_after_send`, `reply_consent_provenance`, and notes without importing it into newsletter/playtest/press routes unless explicit opt-in exists. |
| `DECLINED` | Declined or no fit. |
| `COVERED` | Published/streamed/mentioned after a logged send or verified inbound route; requires `coverage_url` if public coverage exists. |

Future raw sprints must start in a separate raw queue or explicit sprint file. Do not add `RAW_PUBLIC_INDEX_NOT_CONTACT_READY`, `VERIFYING`, or `VERIFIED_NOT_CONTACTED` to the live CRM without a deliberate schema migration.

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
| Requires paid slot only | -2 and keep `paid_creator_permission_gate = BLOCKED_NO_PAID_CREATOR_PROOF` until budget, disclosure, route, asset, and demo/Steam proof pass. |
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
3. `Boundary`: proof-first scope and competitor-neutral positioning.
4. `Asset`: one real screenshot/clip whose metadata, QA, creator utility, and `creator_send_gate` match the recipient; public Steam/presskit/demo links only after the exact destination gates pass (`steam_page_publish_permission_gate`, `press_release_permission_gate`, `demo_public_access_permission_gate`, and destination-specific `public_cta_permission_gate` as applicable); private demo/key/playtest/preview routes only after recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, official inbox custody, disclosure, and exact access-log fields.
5. `Ask`: one clear request.

Template:

Hi [Name],

Your channel fits because [specific verified content pattern].

HECTON-8 is a single-player-first underwater survival game about [segment-specific hook]. Public scope stays inside what the current build can show, and the angle is pressure, machinery, salvage, and black-water route risk.

The reason I think it might work for your audience: [one real asset-backed sentence].

Asset gate: HOLD_PLACEHOLDER_ASSET - replace with exact approved asset IDs/links only after metadata claim checks, creator utility 3/4+, `creator_send_gate` open, route gate passes, and CRM send-log fields are ready.

HOLD_FUTURE_ROUTE_OFFER - mention demo, press kit, build, preview, or material only after exact public CTA, presskit, demo/public access, or recipient/batch private access gates pass and CRM send-log fields are ready.

## Segment Openers

### Direct Underwater Survival

"Your channel has direct underwater-survival history. HECTON-8 is aimed at the colder side of that fantasy: pressure, machinery, salvage, black-water visibility, and a base that behaves like a pressure vessel."

### Survival Route Risk

"Your survival content works because viewers can follow risk and recovery. HECTON-8's relevant angle is the expedition loop: leave safety, salvage under pressure, read warnings, and decide whether the route is still worth it."

### Engineering Base Systems

"Your audience pays attention when systems matter. HECTON-8's pitch for them is base-as-machine: pumps, oxygen, pressure, power, seals, tools, and machinery that visibly changes survival decisions."

### Abyss Horror Pressure

"Your horror audience does not need a bright monster thumbnail. HECTON-8's angle is instruments, sound, darkness, hull stress, and the moment the player realizes the ocean has changed the rules."

## Batch Cadence

Pre-screenshot:

- Do not run default raw-lead verification while CRM-100 has 0 raw rows.
- Use agent time on planned asset readiness, asset-to-CRM matching, and same-day route rechecks only when a matching asset is close to send use.
- Do not send outreach.
- Mark `NEEDS_ASSET` if fit is good but no matching asset exists.
- Do not invent `VERIFIED_NOT_CONTACTED`; use the live CRM statuses in `Data/CREATOR_VERIFICATION_TEMPLATE.csv`.
- Do not mark any lead send-ready until the First 20 Minutes route has matching real assets.

Raw-lead verification reopens only through an explicit source-backed sprint requested after assets reveal a real audience gap.

After first screenshots:

- Prepare up to 10 critique-first creator messages for B/A candidates who tolerate WIP; send only after asset QA, creator utility 3/4+, matching asset `creator_send_gate` is open, pain-backed angles have `pain_freshness_source` and `pain_freshness_checked_at`, the send packet includes one factual `AGENCY_PROOF_CANDIDATE` plus AB-009/KPI decision-read fields if it asks about gameplay/pressure/route risk, same-day official contact-route verification, and CRM send-log fields pass. If payment is involved, the CRM row must also be `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`.
- Send no keys.
- Ask for blunt visual fit, not coverage.

After Steam page:

- Send 20 wishlist-page pitches to send-verified creators only after official route, asset-fit, creator utility, asset `creator_send_gate`, and CRM send-log gates pass. If payment is involved, require `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`.
- Include one asset and one reason.
- Track replies and Steam traffic.

After demo:

- Prepare 30-50 gated demo/key pitch rows; send them only after stable demo/review-build proof exists, recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, official inbox custody, exact access-log fields, disclosure, recipient route verification, creator utility/send gates, paid creator permission gate if payment is involved, and every key/send row is logged.
- Use Steam keys only through the private access/key policy after the same recipient/batch gate passes.
- Stop after two non-responses.

## 2026-05-21 SN2 Pain-Point Fit Rules V6

Status: internal routing only. Do not mention Subnautica 2 pain, EULA, bugs, co-op desync, performance variance, or missing features in creator messages.

The purpose of the SN2 pain refresh is to select the proof asset that makes a creator care without turning HECTON-8 into a competitor-attack pitch.

V6 currentness boundary: the 2026-05-21 official Steam API/appdetails refresh still reads `Very Positive` globally and in English and shows higher review/recommendation volume than V5. Treat SN2-active creator rows as audience-fit evidence, not competitor weakness. If a pain-backed angle uses any SN2-derived bucket, the send packet must name the V6 monitoring/currentness row or a newer same-day row plus the specific private pain bucket in `pain_freshness_source`, fill `pain_freshness_checked_at`, and keep `public_comparison_gate = PRIVATE_ONLY_NO_COMPETITOR_COPY` or stricter.

| Creator segment | Private pain signal to answer | Required HECTON proof | Safe pitch angle |
|---|---|---|---|
| Direct underwater survival | SN2 owns the audience; clone fatigue and thin-loop complaints exist. | `PLAN-SHOT-001` plus `PLAN-SHOT-003` or `PLAN-CLIP-003`. | "Different emotional contract: colder, heavier, salvage under pressure." |
| Engineering/base systems | Base-builder praise/friction means systems need visual clarity. | `PLAN-SHOT-002`, `PLAN-SHOT-005`, or `PLAN-CLIP-001`. | "Base-as-machine, gauges, seals, pumps, failure response." |
| Horror/abyss | Atmosphere praise is strong; defensive-agency complaints make passive fear weak. | `PLAN-SHOT-006` or `PLAN-CLIP-002`. | "Instrument dread and route decisions before the reveal." |
| Indie first-look | EA trust and AI-looking asset risk are high. | Real gameplay asset with build/source fields, no concept-looking still. | "A small honest slice, not a future-feature promise." |
| Regional first-look | Localized pitch risk plus Steam/SN2 familiarity. | Reviewed local one-liner plus `PLAN-SHOT-001` and one base/machinery proof asset. | "Industrial underwater survival with single-player-first, proof-first scope." |
| Systems/progression | Short-content complaints mean scenery is not enough. | A clip that shows action -> consequence -> next decision. | "Route loop, recovery, and machinery pressure." |

Hard gates:

- SN2 pain may choose which planned asset to send; it cannot change the message into "they failed, we fixed it."
- Creator utility must still score 3/4+ and map to the recipient row.
- If the only matching asset is a mood shot, do not send to gameplay-first creators.
- If a creator's recent content is SN2-positive, assume they respect the competitor; pitch difference, not opposition.
- If the current monitoring read remains competitor-positive, lead with HECTON-native proof and creator fit; do not pitch "players are angry" as the reason to care.

## 2026-05-19 First Human-Send Packet After Assets

Status: blocked until real screenshots/Steam page/demo proof exists. This is an execution queue, not permission to contact.

Use this only after:

- official project inbox custody passes `Docs/Marketing/Website/ONE_PAGE_SITE_AND_PRESSKIT_PLAN.md` (`Official Project Inbox Gate V0`);
- `PLAN-SHOT-001`, `PLAN-SHOT-003`, and one base/machinery shot pass QA;
- the first capture session verdict is `KEEP_TESTING`, Campaign 01 verdict is `KEEP`, and AB-001/002/004/009 cold-read responses are logged if the message references screenshot, Steam, gameplay, pressure, route-risk, or proof language;
- no asset in the packet is marked `HOLD_ASSET` or `KILL_ANGLE`;
- every creator-facing asset in the packet has creator utility 3/4+ in `QA/MARKETING_ASSET_QA_CHECKLIST.md` and maps to the recipient's CRM row;
- each asset metadata row has `creator_send_gate` open for that recipient segment, not just a nonzero utility score;
- the packet has `pain_freshness_source` and `pain_freshness_checked_at` for any pain-backed angle; the source must name the current monitoring refresh, for example `Monitoring SN2 Steam API / Public-Source Refresh V6`, plus the exact private bucket used, and the packet must include at least one factual asset with `agency_decision_proof_gate = AGENCY_PROOF_CANDIDATE`, non-empty `agency_decision_notes`, non-pending `viewer_named_decision`, `capture_verdict = KEEP_TESTING` or stronger campaign `KEEP`, and AB-009/KPI `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision` when the message references gameplay proof, pressure decisions, route risk, threat, salvage failure, demo readiness, or first-public feedback;
- official Steam page or presskit URL is mentioned only after the exact `steam_page_publish_permission_gate` or `press_release_permission_gate` passes plus destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`;
- YouTube About/site contact route is verified by the human owner where required;
- the final email contains no multiplayer-scope promise, clone-war framing, or unsupported performance claim;
- final text passes `Docs/Marketing/Roadmap/PUBLIC_ROADMAP_LANGUAGE_AND_PROMISE_POLICY.md` Promise Lint;
- `send_route_class` is chosen before the message is sent: `NO_LINK_CREATOR_FEEDBACK`, `PUBLIC_CTA_CREATOR`, or `PRIVATE_ACCESS_CREATOR`;
- exact sent row is logged back into `Docs/Marketing/Data/CREATOR_VERIFICATION_TEMPLATE.csv`.

CRM copy-like fields are drafting hints, not send approval. `personalized_opener`, `pitch_angle`, and `next_action` must be rechecked against Promise Lint and the live asset verdicts before paste/send.

### Wave A - Screenshot/Steam Proof, No Key

Ask: "Would this be a fit for future coverage or feedback when the preview slice is ready?" Do not ask for coverage now.

| Send order | Creator | Current CRM gate | Required asset before send | Creator utility gate | Pitch angle | Notes |
|---:|---|---|---|---|---|---|
| 1 | Kage848 | `VERIFY_BEFORE_CONTACT` | `PLAN-SHOT-001`, `PLAN-SHOT-003`; Steam link only after `steam_page_publish_permission_gate` plus `public_cta_permission_gate`; presskit link only after `press_release_permission_gate` plus `public_cta_permission_gate` | 3/4+ for direct underwater survival and salvage-route read | UNDERWATER_SURVIVAL | CRM/draft marks SN2-active as of 2026-05-19; recheck current channel and official contact route on send day; no outreach before required asset proof and CTA/access route gate. |
| 2 | AldemarHD | `VERIFY_BEFORE_CONTACT` | German-ready screenshot pack | 3/4+ plus localization QA pass | REGIONAL_FIRST_LOOK | Use German draft; no auto-translated hype. |
| 3 | Zombyra | `VERIFY_BEFORE_CONTACT` | German base/machinery screenshot | 3/4+ plus localization QA pass | REGIONAL_FIRST_LOOK | Explicit single-player-first scope line. |
| 4 | SpielbaerLP | `VERIFY_BEFORE_CONTACT` | German machinery/base shot or clip | 3/4+ plus gameplay proof, not static mood | REGIONAL_FIRST_LOOK | Long-form fit; do not send without gameplay proof. |
| 5 | Keith Ballard | `VERIFY_BEFORE_CONTACT` | Screenshot/Steam proof and no-pressure opener | 3/4+ for underwater-survival clarity | UNDERWATER_SURVIVAL | Confirm official YouTube RSS/About first. |
| 6 | Accurize2 | `VERIFY_BEFORE_CONTACT` | Survival screenshot; Steam link only after `steam_page_publish_permission_gate` plus `public_cta_permission_gate`; presskit link only after `press_release_permission_gate` plus `public_cta_permission_gate` | 3/4+ for survival route and identity | UNDERWATER_SURVIVAL | Verify official route and CTA/access route gate before contact. |
| 7 | Aavak | `NEEDS_ASSET` | Machinery/base gameplay clip | 3/4+ for base systems and visible consequence | BASE_SYSTEMS | Do not use Twitch chat/DM as cold route. |
| 8 | Wanderbots | `NEEDS_ASSET` | Real gameplay clip, no AI-looking asset risk | 3/4+ for indie discovery and player verb | INDIE_DEMO | Confirm official contact policy and AI-asset boundary. |
| 9 | GameEdged | `NEEDS_ASSET` | Steam page plus survival/base clip | 3/4+ for survival-route risk | SURVIVAL_ROUTE_RISK | Verify official YouTube route after asset exists. |
| 10 | Splattercatgaming | `NEEDS_ASSET` | Strong indie first-look clip/Steam page | 3/4+ for indie discovery and first-look clarity | INDIE_DEMO | Needs asset strength; avoid generic email. |

### Wave B - Demo/Preview Only

Hold until stable demo/preview build exists: Neyreyan, Farket, IGP, Praetorian HiJynx, paulsoaresjr, TotalXclipse, Welonz, Dhalucard, STAF_52, Crowmeda.

### Send Log Required Fields

Append or update the CRM row with:

```text
outreach_batch:
sent_date:
contact_route_verified_for_send:
asset_ids_sent:
creator_utility_score:
utm_content:
reply_deadline:
followup_allowed: yes/no
reply_status_after_send:
send_route_class:
reply_consent_provenance:
coverage_url:
```

### Current Send-State Safety Check V0

Snapshot expectation until the first real asset-send pass:

- CRM send-log fields stay empty across all 100 rows: `outreach_batch`, `sent_date`, `contact_route_verified_for_send`, `asset_ids_sent`, `creator_utility_score`, `send_route_class`, `reply_consent_provenance`, and `reply_status_after_send`.
- Asset metadata stays blocked for planned rows: 13 `creator_send_gate = BLOCKED_PLANNED_CAPTURE` and 13 `creator_utility_score = 0`.
- Agency metadata remains pre-capture: exactly three planned rows are `AGENCY_PROOF_CANDIDATE` before QA (`PLAN-SHOT-006`, `PLAN-CLIP-001`, `PLAN-CLIP-003`), and no send can treat that planned status as real proof.
- AB-009/KPI decision-read fields remain empty until a valid blind read exists. A planned candidate with `agency_decision_notes` is still not send proof without non-pending `viewer_named_decision`, `capture_verdict = KEEP_TESTING` or stronger campaign `KEEP`, and `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`.
- Any non-empty send-log field before a named asset passes QA, `creator_send_gate` opens, pain-backed angles have source/date freshness proof, required agency proof is factual, metadata handoff fields are non-pending, AB-009/KPI decision-read fields exist where required, official route is rechecked, `send_route_class` is chosen, and a human owner actually sends the message is a HOLD condition, not progress.
- Empty send fields mean "no outreach has happened yet"; they do not authorize filling drafts, sending DMs, or creating account/browser actions.

One follow-up only, and only if a new asset or demo exists.

## 2026-05-19 CRM-100 Asset Unlock Map V0

CRM snapshot as of 2026-05-19: 100 rows, 0 raw. Distribution: 23 `VERIFY_BEFORE_CONTACT`, 22 `NEEDS_ASSET`, 52 `LOW_PRIORITY_VERIFY_LATER`, 3 `DO_NOT_CONTACT`. Recheck the live CSV before using this for send decisions.

The next useful creator work is not more names. It is proving the planned assets below and then rechecking exact official contact routes.

| Planned asset | Unlocks first | Creator rows affected | Why this asset matters | Still blocked by |
|---|---|---|---|---|
| `PLAN-SHOT-001` Identity hero | Warm screenshot/Steam proof | Kage848, Keith Ballard, Accurize2, CohhCarnage, Dad's Gaming Addiction, Games4Kickz, Timm Plays Games | Direct underwater-survival creators need to see the project is not bright reef clone art. | QA score 10/12, Steam/presskit URL if mentioned, human contact-route reveal. |
| `PLAN-SHOT-002` Pressure room | Base/system proof | NOOBLETS, Nerdzeitalter, Boubers, Aavak, TotalXclipse | Engineering/base-system creators need machinery, gauges, leaks, and pressure-vessel logic, not scenery. | Real gameplay state, not decorative room; official route recheck. |
| `PLAN-SHOT-003` Salvage contact | First outreach hook | Kage848, Jade PG, GameEdged, EnterElysium, Welonz, paulsoaresjr | Survival-route creators need one readable action: tool, target, hazard, reward. | Player verb readable without caption; Steam link only after `steam_page_publish_permission_gate` plus `public_cta_permission_gate`; presskit link only after `press_release_permission_gate` plus `public_cta_permission_gate` if used. |
| `PLAN-SHOT-004` Heavy machine | Machine fantasy proof | NOOBLETS, TotalXclipse, Aavak, BringTheParty, Nerdzeitalter | Systems audiences need heavy interaction and mass. This shot also separates HECTON-8 from generic underwater horror. | Reject if toy-like, clean sci-fi, or static prop. |
| `PLAN-SHOT-005` Base stress | Base-as-risk proof | Aavak, TotalXclipse, Wanderbots, GameEdged, Splattercatgaming | The base must look like a pressure vessel under failure with an actionable response path. | Failure must be actionable and fair. |
| `PLAN-SHOT-006` Threat silhouette | Abyss pressure proof | Neyreyan, IGP, Insym VODS, Game Advisor, Splattercatgaming | Horror/pressure rows need dread through instruments, floodlight, and scale, not a monster-pose thumbnail. | Do not send if threat reads as random terrain or AI-looking concept. |
| `PLAN-SHOT-007` Seed Ship signal | Narrative/system hook | Wanderbots, Welonz, Praetorian HiJynx, EnterElysium | Indie/long-form creators need a reason this is more than a survival clone. | Use only if the build shows system interference honestly. |
| `PLAN-CLIP-001` Pressure leak decision | First gameplay proof | Aavak, TotalXclipse, GameEdged, Wanderbots, Splattercatgaming | A 10-20s clip can prove player action, consequence, and machinery better than screenshots. | No route crash, no unclear objective, no performance claim. |
| `PLAN-CLIP-002` Sonar saw it first | Horror/abyss proof | Neyreyan, IGP, Insym VODS, Game Advisor, Crowmeda | The clip should sell sound/instrument dread to horror-adjacent audiences. | Audio/readability must work without caption. |
| `PLAN-CLIP-003` Salvage failure | Survival-route proof | Kage848, GameEdged, paulsoaresjr, Welonz, Jade PG | Demonstrates route risk and consequence, not mood. | Escalation must follow player action. |
| `PLAN-CLIP-004` Heavy machine startup | Engineering/base proof | NOOBLETS, Nerdzeitalter, Aavak, TotalXclipse, BringTheParty | Proves machinery interaction and weight for systems audiences. | Reject if it looks like an animation showcase without gameplay consequence. |

### Regional Unlock Notes

| Region/language | Rows | Required before send |
|---|---|---|
| German | AldemarHD, Zombyra, SpielbaerLP, HelyaLP, Hirnsturz, KeysJore, Dhalucard, TobinatorLetsPlay, Emmis Zockt, Boubers, Nerdzeitalter | `PLAN-SHOT-001` plus one machinery/base shot, German-safe one-pager, localization QA, official route recheck. |
| French | STAF_52 | Screenshot/Steam proof plus reviewed French one-liner; no machine-translated send. |
| Spanish / ES-or-EN | Crowmeda | Strong demo/clip and Spanish-safe pitch; no early screenshot-only contact. |
| Slovenian-or-English | Kokoplays MB | English-safe route is acceptable only after official contact and current content fit are confirmed. |

### Capture Priority From Creator Utility

1. `PLAN-SHOT-001` plus `PLAN-SHOT-003`: unlocks the largest direct-survival warm packet.
2. `PLAN-SHOT-002` or `PLAN-SHOT-005`: unlocks engineering/base-system differentiation.
3. `PLAN-CLIP-001`: unlocks the first real gameplay message to `NEEDS_ASSET` rows.
4. `PLAN-CLIP-002`: unlocks horror/abyss rows.
5. `PLAN-SHOT-007`: use later unless the build genuinely supports Seed Ship/system interference.

Do not promote any row to send-ready from this table alone. The asset must pass QA and exact contact route must be verified after the asset exists.

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
