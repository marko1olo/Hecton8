# Segment Pitch Matrix

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This document is active only as static documentation/source/platform-orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current official platform rules, current verification artifacts, and the latest DOC_GLOBAL reports.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, public Steam page, public demo, wishlist performance, creator outreach readiness, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters, platform rules, dates, creator availability, contact routes, and marketing claims inside this file are subordinate to fresh official sources and current project proof.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Status: segment-specific outreach logic
Rule: customize before sending

## Matrix

| Segment | What They Care About | HECTON-8 Angle | Asset Needed | Pitch Risk | Do Not Say |
|---|---|---|---|---|---|
| Underwater survival veterans | underwater survival, creatures, bases, mystery | darker industrial underwater survival with pressure and machinery | screenshots + 20s clip | clone comparison | competitor-killer framing |
| Survival builders | progression, resources, base durability | base as pressure-rated machine | base/stress screenshot | too dark/too niche | cozy building |
| Horror streamers | dread, atmosphere, first reaction | sonar, black water, silhouette, system failure | dread clip | not scary enough | jump-scare game |
| Engineering/sim | systems, machines, failure logic | pumps, ballast, oxygen, pressure, readable causality | machinery clip | not enough system depth | realistic full simulation |
| Indie showcase | novelty, concise hook, Steam demo | NASA-punk deep-sea noir survival | Steam page + trailer | generic pitch | revolutionary |
| Steam demo/Next Fest | playable slice, retention, clean hook | first 20 minutes of pressure/salvage loop | demo | weak demo | early tech demo |
| Twitch first-look | fast readability, live moments | immediate descent/failure/salvage loop | stable build | slow onboarding | lore-heavy intro |
| TikTok/Shorts | instant hook, visual contrast | floodlight, sonar, pressure warning, silhouette | 9:16 clips | weak first second | cinematic slow burn |
| Press | angle, facts, assets, honesty | single-player-first industrial underwater survival | press kit | feature ambiguity | future promises |
| Russian/CIS | atmosphere, horror, survival, strong identity | mrachnoe podvodnoe vyzhivanie, davlenie, tekhnika | localized one-sheet | machine translation | kooperativ |
| German | survival systems, long-form LP, simulation | pressure/base/machinery planning | long-form demo | overhype | direct clone bait |
| Polish | indie discovery, survival/horror | industrial ocean dread and exploration | demo + one-sheet | limited localization | fake Polish translation |
| French | atmosphere, survival, curiosity | deep-sea noir and machine failure | polished screenshots | very competitive top creators | co-op tease, unsupported multiplayer scope |
| Spanish | horror/survival reactions | fear of depth, sonar, salvage | clips + demo | broad variety mismatch | competitor superiority claim |
| Portuguese/Brazil | survival, horror, strong thumbnails | black-water survival and pressure | clips + screenshots | localization quality | unverified Portuguese copy |

## Pitch Angles

### UNDERWATER_SURVIVAL

Use for adjacent underwater-survival channels.

Message:

HECTON-8 shares the broad underwater survival audience, but the fantasy is not bright alien wonder. It is pressure, industrial machinery, salvage, and black-water dread.

Required proof:

- one exterior abyss screenshot;
- one salvage/base screenshot;
- one clip that shows gameplay.

### PRESSURE_HORROR

Use for horror and atmosphere creators.

Message:

The horror is not a monster screaming at the camera. It is pressure, failing machines, limited light, unreliable instruments, and knowing the ocean has more patience than the player.

Required proof:

- sonar or silhouette clip;
- audio-forward clip;
- base failure clip.

### BASE_SYSTEMS

Use for survival builders and engineering creators.

Message:

The base is not decoration. It is a pressure-rated machine with power, seals, oxygen, flooding, and maintenance risk.

Required proof:

- interior machinery screenshot;
- base under stress clip;
- UI showing readable system state.

### HEAVY_MACHINES

Use for engineering/sandbox/vehicle creators.

Message:

Survival depends on heavy industrial devices: pumps, ballast, tools, exosuit/vehicle systems if present, and machines that feel too heavy to be toys.

Required proof:

- mechanical startup clip;
- scale shot;
- cockpit or control interaction.

### INDIE_DEMO

Use for indie showcase and Next Fest channels.

Message:

HECTON-8 is a Steam-demo-targeted candidate only after official page/demo gates pass: single-player-first underwater survival with a pressure/machinery identity.

Required proof:

- Steam page;
- trailer;
- demo date;
- press kit.

### REGIONAL_FIRST_LOOK

Use for non-English channels.

Message:

Localized one-pager, simple feature boundaries, and real screenshots. Do not machine-gun auto-translated hype.

Required proof:

- translated short pitch;
- screenshots;
- clear contact.

## Outreach Timing

| Phase | Eligible Pool After Gates | Max Batch Size | Ask |
|---|---|---:|---|
| First screenshots | 10-20 A-priority creators | 10-20 | Feedback, not coverage. |
| First 20s clip | A-priority + short-form creators | 20-40 | Would this fit future coverage? |
| Steam page live | A/B creators + press only after `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`, Official CTA Link Activation Gate V0, first human-send packet gates, and CRM/press send-log fields pass | 50-100 | Wishlist/page feedback or future demo interest. |
| Demo ready | A/B/C segmented with stable build, recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, verified route, exact access-log fields, and send log | 100-200 | Demo coverage/key request. |
| Next Fest | verified database | 200+ | Demo coverage / list inclusion. |

All rows above inherit the first human-send packet gates: matching asset metadata claim checks, creator utility 3/4+ for the recipient, open `creator_send_gate`, required factual `agency_decision_proof_gate`/`agency_decision_notes` plus AB-009/KPI decision-read fields for gameplay/pressure/route-risk sends, same-day official contact route, Promise Lint, and CRM send-log fields. Paid creator routes also require `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`. The phase name never opens a creator send or paid placement by itself.

## Personalization Formula

Every pitch must include:

1. creator name;
2. specific content fit;
3. HECTON-8 angle;
4. one real asset ID/link after asset metadata claim checks, QA, creator utility, `creator_send_gate`, and the relevant public/private route gates pass;
5. one ask;
6. opt-out/no-pressure close.

Template:

> I am reaching out because your [specific series/video/content type] audience already responds to [survival/horror/machinery/underwater exploration]. HECTON-8 is a single-player-first underwater survival game focused on [one angle], with public scope kept inside what the current build can show. HOLD_USEFUL_ASSET_LINK - insert one exact asset ID/link only after asset metadata claim checks, QA, creator utility, `creator_send_gate`, route class, and public/private route gates pass. HOLD_DEMO_PRESSKIT_ROUTE - mention demo or press kit only after the exact public CTA, private access, or presskit gate passes.

## Competitor Mention Gate

Use competitor context only to prove the creator's audience fit. Do not use it as the hook.

Allowed in internal notes:

- `recent underwater-survival coverage`;
- `direct Subnautica history`;
- dated SN2/Subnautica activity as audience-fit evidence only; never as current-send proof, competitor weakness proof, co-op bait, or public pain framing.

Allowed in final sends:

- one neutral content-fit sentence, if needed: `Your audience already responds to underwater survival and base/survival progression.`

Forbidden in final sends:

- subject lines containing `SN2`, `Subnautica 2`, `EULA`, `stutter`, `desync`, `co-op bugs`, or `performance`;
- any competitor pain, controversy, review negativity, or "we fixed what they did not" framing;
- direct comparison unless the creator explicitly asks.

Before send, check the matching asset metadata fields `public_comparison_gate`, `pain_freshness_source`, `pain_freshness_checked_at`, `creator_utility_score`, `creator_send_gate`, `agency_decision_proof_gate`, `agency_decision_notes`, `capture_handoff_packet_id`, `capture_verdict`, and `viewer_named_decision`, plus AB-009/KPI `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision` when the pitch claims gameplay/pressure/route-risk proof. If public comparison is not `PRIVATE_ONLY_NO_COMPETITOR_COPY` or stricter, if pain freshness is still `PENDING_SAME_DAY_REFRESH` for a pain-backed angle, if the creator-facing gate is not open for that recipient segment, if `capture_verdict` is held/killed/pending, or if a gameplay/pressure/route-risk pitch lacks one factual `AGENCY_PROOF_CANDIDATE` with metadata viewer decision and decision-read evidence, do not send.

SN2-derived pain or audience-fit rows require V6 or a newer same-day monitoring row in `pain_freshness_source` before they influence asset priority. They cannot justify subject lines, creator hooks, co-op teasers, performance claims, EULA commentary, or "we fixed their problem" copy.

## Rejection / Non-Response Handling

- No response after 7-10 days: one follow-up only if a new asset row passes metadata claim checks, QA, creator utility, `creator_send_gate`, and the original send-log row allows follow-up.
- Decline: mark `DECLINED`, do not argue.
- "Send key": verify identity before key.
- "Paid only": record rate, keep `paid_creator_permission_gate` blocked, and do not commit without `ALLOW_PAID_CREATOR_TEST_VERIFIED`.
- Suspicious email/domain mismatch: mark `DO_NOT_CONTACT`.
