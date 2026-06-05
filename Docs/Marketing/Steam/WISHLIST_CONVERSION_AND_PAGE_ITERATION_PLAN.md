# HECTON-8 Wishlist Conversion And Steam Page Iteration Plan

Status: pre-page conversion plan
Owner lane: Marketing / Steam conversion
Runtime impact: none

## Authority Boundary

Static Steam conversion plan only. Public voice routes through root `textes.md`.
Any quality, release, platform, Steam, wishlist, demo, performance, press, review-key, embargo, showcase, curator, asset-QA, schedule, SEO, pricing, discount, publication, website, or presskit claim requires `quality.md`, `release.md`, `platform.md`, current proof artifacts, and the matching local permission gate.
Page hypotheses, wishlist rows, iteration plans, checklists, schedules, and strategy rows do not approve Steam page, wishlist, pricing, discount, review-key, presskit, showcase, publication, platform, or release claims.

## Steam Page Publish Gate

Current machine gate: `steam_page_publish_permission_gate = HOLD_NO_STEAM_PAGE_PUBLICATION`.

Future allow value: `ALLOW_STEAM_PAGE_PUBLISH_VERIFIED`.

No wishlist conversion work can report page signal until the public page was published through this gate and the exact external link destination also has `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`.

## Purpose

The Steam page is not a poster. It is a conversion surface. The page must convert the right cold viewer into a wishlist/follow while filtering out players who expect unsupported multiplayer scope, low-pressure scenic exploration, or unsupported features.

## Page Hypothesis

HECTON-8 will convert when the page communicates:

1. single-player deep-sea survival;
2. pressure and machinery as identity;
3. base/salvage loop;
4. one readable player decision under pressure with non-pending metadata `viewer_named_decision`, valid non-held `capture_verdict`, and AB-009/KPI decision-read evidence;
5. Seed Ship anomaly/mystery;
6. real in-game visuals;
7. no false multiplayer-scope or performance promise.

## Page Section Order

Recommended first draft:

1. Capsule: dark industrial pressure identity, readable logo.
2. Trailer: first 10 seconds show player action.
3. Short description: direct genre and hook.
4. First four screenshots: route, machinery, base risk, and agency/decision proof with metadata handoff plus AB-009/KPI decision-read fields.
5. Feature bullets: verbs, not adjectives.
6. Long description: current/future boundaries.
7. Tags: survival/exploration/base/sci-fi/singleplayer.

## Screenshot Order

Steam first screenshot candidates:

1. Bright surface/photic route beauty with player route cue, if the current build proves it.
2. Player facing pressure/machinery problem.
3. Salvage route in black water.
4. Base/module with visible consequence.
5. Agency/decision proof: threat, leak, route cost, sonar pressure, or salvage failure, backed by metadata `viewer_named_decision`/`capture_verdict` plus `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`.
6. Seed Ship/anomaly signal.
7. Heavy vehicle/tool if real.
8. Biome/scale shot.
9. UI/inventory only if polished.

Never lead with:

- empty ocean;
- logo/title card;
- concept art;
- UI wall;
- blue aquarium vista;
- unclear corridor.

## Feature Bullet Formula

Bad:

- "Explore a massive world."
- "Craft many items."
- "Experience atmospheric gameplay."

Good:

- "Repair pressure-stressed machinery before leaks become route failures."
- "Scavenge black-water wrecks for parts that keep your base alive."
- "Track Seed Ship interference through instruments, corrupted routes, and hostile ecology."

## Conversion Experiments

| Test | Variant A | Variant B | Metric |
|---|---|---|---|
| Short description | direct genre | atmosphere-first | wishlist conversion |
| First screenshot | machinery problem | Seed Ship silhouette | click/wishlist conversion |
| Trailer opening | player action | threat reveal | watch-through/wishlist |
| Tags | survival-first | atmosphere-first | traffic quality |
| Capsule | no text | 2-word text | click-through |

Only change one variable at a time if data volume permits.

## Steam Page Weekly Review

After page launch, review weekly:

```text
Week:
Traffic by source:
Wishlist delta:
Visits -> wishlist:
Top UTM source:
Worst UTM source:
Top confusion comment:
Top positive comment:
Screenshot changes proposed:
Copy changes proposed:
Tag changes proposed:
Decision:
```

## Page Kill Signals

Revise page if:

- users ask "what do you do?";
- users assume unsupported multiplayer scope;
- users compare it only as "Subnautica clone";
- high visits but weak wishlist conversion;
- creators say "send gameplay when you have it";
- comments praise art but ignore systems;
- first screenshot is called concept art/AI-looking;
- viewers praise atmosphere but cannot name a player decision.

## Page Strong Signals

Keep direction if players independently say:

- "industrial";
- "pressure";
- "heavy";
- "salvage";
- "base failure";
- "I had to choose";
- "dark Subnautica but not a clone" only as their wording, not ours;
- "I want to hear/feel this."

## Store Copy Refresh Rules

Update copy only when:

- new asset proves a stronger hook;
- recurring confusion appears;
- a feature is removed/changed;
- a demo/public build changes current truth;
- Steam tags drift.

Do not rewrite every week from anxiety.

## Current HECTON-8 Decision

No page iteration until the Steam page exists through `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED` and Official CTA Link Activation Gate V0 passes. Prepare page versioning and conversion log now.
