# Campaign 01 - First Screenshot Drop

Status: future / depends on real in-game screenshots
Public stance: single-player-first / no co-op promise
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
| Subnautica veterans | "Does this feel distinct from bright ocean survival?" |
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

I am not asking for wishlists. I need blunt cold-read feedback:

- what do you think the player does?
- does the machinery look functional?
- does the image feel too close to existing underwater survival games?
- is the darkness readable or just muddy?
- which screenshot should be first on Steam?

### Steam Announcement

Title:

New Screenshots From Below The Light

Body:

This update shows HECTON-8's current visual direction: pressure-rated machinery, black-water exploration, salvage routes, and habitats that behave like survival infrastructure.

HECTON-8 remains single-player-first. We are not promising co-op and not making performance claims without measured proof.

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

Not asking for coverage yet. I need to know if this visual direction would read clearly to your audience or if it looks too generic.

Screens: [link]

One question: which image would you lead with, and which should be killed?

## Metrics

| Metric | Useful threshold |
|---|---:|
| Cold viewers can identify genre | 70%+ |
| Cold viewers identify player verb | 50%+ |
| "Looks like Subnautica clone" comments | under 20% |
| "Too dark / cannot read" comments | under 25% |
| Creator replies from 20 critique sends | 3+ |
| Screenshot selected as Steam lead by majority | one clear winner |

## Kill Criteria

Revise before Steam page if:

- no screenshot wins clearly;
- viewers cannot identify what the player does;
- the base reads as decorative;
- the first reaction is mostly "Subnautica but darker";
- the darkness destroys readability.

## 2026-05-19 Execution Checklist V0

Status: pre-capture / do not run.

This checklist ties Campaign 01 to the current asset, experiment, UTM, social, FAQ, creator, and Steam prep docs.

### Required Inputs

| Gate | Required file/source | Pass condition |
|---|---|---|
| Asset metadata | `Data/MARKETING_ASSET_METADATA_TEMPLATE.csv` | `PLAN-SHOT-001`, `PLAN-SHOT-003`, and at least one of `PLAN-SHOT-002/004/005` have actual file path, build ID, and QA score. |
| Asset QA | `QA/MARKETING_ASSET_QA_CHECKLIST.md` | Steam-use shots score 10/12; critique-only shots score at least 9/12. |
| Creator utility | `QA/MARKETING_ASSET_QA_CHECKLIST.md`, `Data/MARKETING_ASSET_METADATA_TEMPLATE.csv`, `CreatorOutreach/MASS_LEAD_VERIFICATION_AND_PITCH_WORKFLOW.md` | Any asset used for creator micro-feedback scores at least 3/4 on creator utility and maps to named CRM rows; utility cannot override visual QA thresholds. |
| Steam assembly | `Steam/STORE_PAGE_COPY_MATRIX.md` | Candidate A/B/C chosen by cold read; no co-op/perf/large-world promises. |
| Steam asset ticket | `Steam/STEAM_PAGE_ASSET_REQUIREMENTS_CHECKLIST.md` | Reject codes filled for killed captures; no weak frame survives by taste. |
| Experiment plan | `Experiments/A_B_TESTING_AND_CREATIVE_EXPERIMENTS.md` | AB-001, AB-002, and AB-004 are ready to log. |
| UTM registry | `Analytics/MEASUREMENT_AND_UTM_PLAN.md` | `utm_content` uses `plan-shot-*` or `ab-*` format only if an official link exists. |
| FAQ/replies | `Community/PUBLIC_FAQ_AND_OBJECTION_HANDLING.md` | First screenshot response matrix is ready for co-op/clone/dark/AI/perf questions. |
| Creator wave | `CreatorOutreach/MASS_LEAD_VERIFICATION_AND_PITCH_WORKFLOW.md` | Wave A remains held unless contact route and asset gate are verified. |
| Social custody | `Social/SOCIAL_ACCOUNT_SETUP_AND_PLATFORM_PLAYBOOK.md` | Official handles are owner-controlled before posting from them. |

### 72-Hour Sequence

| Window | Action | Asset | Measurement | Stop/revise trigger |
|---|---|---|---|---|
| T-24h | Internal cold-read pass | `PLAN-SHOT-001`, `PLAN-SHOT-003`, `PLAN-CAPSULE-001` | AB-001/AB-002 notes | Under 70% genre clarity or capsule unreadable. |
| T+0h | First public critique post, no wishlist CTA | Best screenshot | Useful comments per view, clone/dark/AI confusion | 2+ repeated confusion patterns in first response set. |
| T+6h | Reply pass | FAQ matrix | Repeated objections logged as asset/copy issues | Do not argue; revise source if repeated. |
| T+24h | Second post only if first did not fail | Alternate screenshot or clip | AB-001/AB-003 comparison | Hold if first post exposed clarity failure. |
| T+48h | Wave A creator micro-feedback, max 10 | Best asset pack with creator utility 3/4+ | Reply quality, exact CRM row, exact contact route | Stop if contact routes are not verified, assets mismatch creator, or utility score is below 3/4. |
| T+72h | Campaign note | All signals | Keep/revise/kill decision | Do not move to Steam page launch without a clear winner. |

### Campaign Result Must End In One Decision

Choose exactly one:

- `KEEP`: one lead screenshot and one short-description angle are clear enough for Steam page assembly.
- `REVISE`: assets/copy need another capture/edit pass before Steam.
- `KILL`: visual direction is too generic/dark/unclear; stop public push and fix product/art read.
