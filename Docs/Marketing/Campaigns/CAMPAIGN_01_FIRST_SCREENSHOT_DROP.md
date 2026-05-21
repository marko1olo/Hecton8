# Campaign 01 - First Screenshot Drop

Status: future / depends on real in-game screenshots
Public stance: single-player-first scope / proof-first campaign copy
Runtime impact: none

## Objective

Use the first screenshot pack to test whether HECTON-8 has a distinct market read before spending money or sending demo keys.

The goal is not likes. The goal is cold-read clarity.

## Asset Gate

Do not run this campaign until the project has:

- 6-10 real in-game screenshots;
- no placeholder UI unless intentionally labeled;
- consistent NASA-punk / deep-sea noir identity;
- at least one player verb visible;
- at least one base/machinery shot;
- at least one agency/decision proof shot or clip from `PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003`;
- at least one black-water/exploration shot;
- no public performance claim.

## Screenshot Pack Order

| Slot | Purpose | Question |
|---:|---|---|
| 1 | Identity | Does this read as HECTON-8 in one glance? |
| 2 | Player verb | What is the player doing? |
| 3 | Base survival | Does the base look functional or decorative? |
| 4 | Salvage route | Would the player risk going there? |
| 5 | Threat | Is danger readable without a caption? |
| 6 | Seed Ship | Does anomaly/corruption read as systemic, not random VFX? |
| 7 | Interior | Does it look pressure-rated and lived-in? |
| 8 | Scale | Does depth feel expensive? |

## Target Audiences

| Audience | Post angle |
|---|---|
| Adjacent underwater-survival players | "Does this feel distinct from bright ocean survival?" |
| Survival builders | "Does this base look like a machine that keeps you alive?" |
| Horror players | "Is the dread readable without jump scares?" |
| Engineering players | "Does the machinery look functional?" |
| Indie discovery | "Does the image explain the game without text?" |

## Community Post Templates

### Reddit Critique

Title:

Does this read as industrial underwater survival, or just generic sci-fi?

Body:

We are testing the first HECTON-8 screenshot pack. The target is single-player underwater survival with pressure, machinery, salvage, and deep-sea noir isolation.

This post is for blunt cold-read feedback only:

- what do you think the player does?
- what decision would the player make next, if any?
- does the machinery look functional?
- does the image feel too close to existing underwater survival games?
- is the darkness readable or just muddy?
- which screenshot should be first on Steam?

### Steam Announcement

Title:

New Screenshots From Below The Light

Body:

This update shows HECTON-8's current visual direction: pressure-rated machinery, black-water exploration, salvage routes, and habitats that behave like survival infrastructure.

HECTON-8 remains single-player-first and proof-first. Public scope and performance claims stay inside measured build evidence.

Useful feedback:

- does the world read as hostile?
- does the base look functional?
- does the tone feel distinct?
- which image would make you click the Steam page?

### X / Bluesky

HECTON-8 is underwater survival where the base is a machine, not a house.

Looking for blunt visual read: does this sell pressure, machinery, and black water, or does it look too generic?

## Creator Micro-Outreach

Send only to verified `NEEDS_ASSET` leads who tolerate WIP.

Subject:

HECTON-8 screenshot read - does this fit your survival audience?

Message:

Hi [Name],

You cover [specific verified pattern]. We are testing the first screenshot pack for HECTON-8, a single-player-first underwater survival game about pressure, machinery, salvage, and black-water exploration.

This is feedback-only; coverage waits until a real preview route exists. I need to know if this visual direction would read clearly to your audience or if it looks too generic.

Screens: HOLD_SCREEN_LINK - use exact approved screenshot packet link only after metadata claim checks, QA, first-capture handoff fields, and private/public route gates pass. Do not use Steam/demo/presskit links here unless their destination-specific `public_cta_permission_gate` or private access gate is allowed.

One question: which image would you lead with, and which should be killed?

If the packet claims gameplay/pressure/route-risk proof, also ask which player decision the audience can name without a caption.

## Metrics

| Metric | Useful threshold |
|---|---:|
| Cold viewers can identify genre | 70%+ |
| Cold viewers identify player verb | 50%+ |
| Cold viewers name agency decision | 60%+ on AB-009 candidate |
| "Looks like Subnautica clone" comments | under 20% |
| "Too dark / cannot read" comments | under 25% |
| Valid blind reads | 15 uncontaminated human/player reads before public post |
| Creator replies from 20 critique sends | 3+ |
| Screenshot selected as Steam lead by majority | one clear winner |

## Kill Criteria

Revise before Steam page if:

- no screenshot wins clearly;
- viewers cannot identify what the player does;
- viewers cannot name a pressure decision from the agency-proof candidate;
- cold-read answers mostly repeat prompt words instead of naming what the asset shows;
- the base reads as decorative;
- the first reaction is mostly "Subnautica but darker";
- the darkness destroys readability.

## 2026-05-19 Execution Checklist V0

Status: pre-capture / do not run.

This checklist ties Campaign 01 to the current asset, experiment, UTM, social, FAQ, creator, and Steam prep docs.

### Required Inputs

| Gate | Required file/source | Pass condition |
|---|---|---|
| First capture session | `Content/SCREENSHOT_AND_CLIP_SHOTLIST.md`, `QA/MARKETING_ASSET_QA_CHECKLIST.md` | First session verdict is `KEEP_TESTING` and the shotlist handoff packet includes file paths, build ID, QA score, creator utility, pain freshness, public comparison gate, agency proof gate, viewer-named decision, reject codes, and next actions; if the packet records `AGENCY_MISSING_HOLD`, Campaign 01 stays held and the scene/angle gets revised before public testing. |
| Asset metadata | `Data/MARKETING_ASSET_METADATA_TEMPLATE.csv` | `PLAN-SHOT-001`, `PLAN-SHOT-003`, at least one base/machinery asset from `PLAN-SHOT-002/004/005`, and at least one agency/decision proof asset from `PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003` have actual file path, build ID, QA score, `multiplayer_scope_check`, `performance_claim_check`, `feature_truth_check`, `pain_bucket_answered`, `pain_proof_score`, `pain_freshness_source`, `pain_freshness_checked_at`, `public_comparison_gate`, `agency_decision_proof_gate`, `agency_decision_notes`, `capture_handoff_packet_id`, `capture_verdict`, `viewer_named_decision`, and `capture_next_actions`. |
| Asset QA | `QA/MARKETING_ASSET_QA_CHECKLIST.md` | Steam-use shots score 10/12; critique-only shots score at least 9/12. |
| Agency/decision proof gate | `QA/MARKETING_ASSET_QA_CHECKLIST.md`, `Data/MARKETING_ASSET_METADATA_TEMPLATE.csv`, `Experiments/A_B_TESTING_AND_CREATIVE_EXPERIMENTS.md`, `KPI/MARKETING_DASHBOARD_SPEC.md` | At least one first-pack asset is `agency_decision_proof_gate = AGENCY_PROOF_CANDIDATE`; supporting or anomaly-only assets cannot satisfy this gate; AB-009/KPI rows store `what_decision_next` and `agency_decision_read` or Campaign 01 remains `HOLD`. |
| Pain-proof gate | `QA/MARKETING_ASSET_QA_CHECKLIST.md`, `Monitoring/COMPETITOR_AND_SENTIMENT_MONITORING_QUERIES.md`, `Data/MARKETING_ASSET_METADATA_TEMPLATE.csv` | Any first-pack priority asset scores at least 4/5 on private pain proof only after a same-day monitoring row, `pain_freshness_source`, `pain_freshness_checked_at`, non-pending `viewer_named_decision`, and valid non-held `capture_verdict` are filled; `public_comparison_gate` stays `PRIVATE_ONLY_NO_COMPETITOR_COPY` or stricter. 2026-05-21 V6 keeps SN2 classified as strong, so pain proof cannot become public weakness copy. |
| Blind cold-read protocol | `Experiments/A_B_TESTING_AND_CREATIVE_EXPERIMENTS.md`, `KPI/MARKETING_DASHBOARD_SPEC.md` | Pass percentages use only valid blind reads; `CONTEXT_EXPOSED` and `PROMPT_ECHO` responses are fix notes, not proof. |
| Creator utility | `QA/MARKETING_ASSET_QA_CHECKLIST.md`, `Data/MARKETING_ASSET_METADATA_TEMPLATE.csv`, `CreatorOutreach/MASS_LEAD_VERIFICATION_AND_PITCH_WORKFLOW.md` | Any asset used for creator micro-feedback scores at least 3/4 on creator utility and maps to named CRM rows; utility cannot override visual QA thresholds. |
| Steam assembly | `Steam/STORE_PAGE_COPY_MATRIX.md` | Candidate A/B/C chosen by cold read; no unsupported multiplayer-scope, performance, or large-world promises. |
| Steam asset ticket | `Steam/STEAM_PAGE_ASSET_REQUIREMENTS_CHECKLIST.md` | Reject codes filled for killed captures; no weak frame survives by taste. |
| Experiment plan | `Experiments/A_B_TESTING_AND_CREATIVE_EXPERIMENTS.md` | AB-001, AB-002, AB-004, and AB-009 use the Cold-Read Score Sheet V0; raw responses are logged before any public post. |
| UTM registry | `Analytics/MEASUREMENT_AND_UTM_PLAN.md` | `utm_content` uses `plan-shot-*` or `ab-*` format only after `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` passes for that destination. |
| FAQ/replies | `Community/PUBLIC_FAQ_AND_OBJECTION_HANDLING.md` | First screenshot response matrix is ready for multiplayer-scope/clone/dark/AI/perf questions. |
| Creator wave | `CreatorOutreach/MASS_LEAD_VERIFICATION_AND_PITCH_WORKFLOW.md` | Wave A remains held unless contact route and asset gate are verified. |
| Public post custody | `Social/SOCIAL_ACCOUNT_SETUP_AND_PLATFORM_PLAYBOOK.md` | Official handles are owner-controlled before posting from them; `account_registration_permission_gate` and `public_post_permission_gate` must both be allow values for the exact post. |

### 72-Hour Sequence

| Window | Action | Asset | Measurement | Stop/revise trigger |
|---|---|---|---|---|
| T-24h | Blind cold-read pass | `PLAN-SHOT-001`, `PLAN-SHOT-003`, `PLAN-CAPSULE-001`, and one agency candidate from `PLAN-SHOT-006` / `PLAN-CLIP-001` / `PLAN-CLIP-003` | AB-001/AB-002/AB-009 valid blind notes plus `what_decision_next` / `agency_decision_read` fields | Under 70% genre clarity, under 60% agency-decision read, capsule unreadable, fewer than 15 valid blind human/player reads, or prompt echo dominates. |
| T+0h | First public critique post, feedback-only/no external CTA | Best screenshot | Useful comments per view, clone/dark/AI confusion | 2+ repeated confusion patterns in first response set. |
| T+6h | Reply pass | FAQ matrix | Repeated objections logged as asset/copy issues | Do not argue; revise source if repeated. |
| T+24h | Second post only if first did not fail | Alternate screenshot or clip | AB-001/AB-003 comparison | Hold if first post exposed clarity failure. |
| T+48h | Wave A creator micro-feedback, max 10 | Best asset pack with creator utility 3/4+ | Reply quality, exact CRM row, exact contact route | Stop if contact routes are not verified, assets mismatch creator, or utility score is below 3/4. |
| T+72h | Campaign note | All signals plus first-capture handoff packet | Keep/revise/kill decision with agency-decision field | Do not move to Steam page launch without a clear winner, one stored viewer-named decision, `capture_verdict = KEEP_TESTING` or stronger campaign `KEEP`, and metadata rows updated from facts rather than wishes. |

### Campaign Result Must End In One Decision

Choose exactly one:

- `KEEP`: one lead screenshot, one short-description angle, and one AB-009 agency-proof row are clear enough for Steam page assembly.
- `REVISE`: assets/copy need another capture/edit pass before Steam.
- `KILL`: visual direction is too generic/dark/unclear; stop public push and fix product/art read.
