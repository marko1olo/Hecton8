# Creator CRM Schema And Scoring

Status: operational specification
Purpose: turn raw leads into outreach-ready rows without lying

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- current public creator/contact evidence
- fresh CRM/outreach logs

No lead score, contact readiness, subscriber/reach state, platform policy compliance, runtime build, Unity import, profiler, player-build, or campaign-performance proof is implied unless this document links a fresh evidence artifact. Scoring rules are an internal triage model, not external market telemetry.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Table Schema

Use this schema in a future CSV/Sheet/Notion/database.

| Field | Required | Values / Format | Notes |
|---|---|---|---|
| `lead_id` | yes | stable slug | Example: `yt_igp_001`. |
| `status` | yes | RAW_SEED, VERIFY_BEFORE_CONTACT, READY_FOR_HUMAN_REVIEW, APPROVED_TO_CONTACT, CONTACTED, REPLIED, COVERED, DECLINED, DO_NOT_CONTACT | Never skip verification. |
| `priority` | yes | A, B, C, D | A = strong fit; D = archive. |
| `segment` | yes | SUBNAUTICA, SURVIVAL, HORROR, ENGINEERING, INDIE, PRESS, REGIONAL, SHORTS, TWITCH | One primary segment. |
| `name` | yes | text | Public name only. |
| `platform` | yes | YouTube, Twitch, Web, Newsletter, Steam Curator, TikTok, Other |  |
| `url` | yes | URL | Public page. |
| `language` | yes | English, Russian, German, etc, UNKNOWN | Apparent only. |
| `country_region` | optional | text | Do not infer if unclear. |
| `recent_activity` | yes | VERIFIED_ACTIVE, OLD_OR_INACTIVE, UNKNOWN | Must be checked before contact. |
| `audience_fit` | yes | 0-5 | See scoring below. |
| `asset_fit` | yes | SCREENSHOT, CLIP, DEMO, PRESS_KIT, STEAM_PAGE | What they need before contact. |
| `contact_route` | yes | UNKNOWN, PUBLIC_EMAIL, CONTACT_FORM, YOUTUBE_ABOUT, TWITCH_BUSINESS, STEAM_CURATOR_CONNECT, SITE_FORM | Do not invent. |
| `contact_value` | optional | exact public route | Only official public contact. |
| `pitch_angle` | yes | UNDERWATER_SURVIVAL, PRESSURE_HORROR, BASE_SYSTEMS, HEAVY_MACHINES, ENGINEERING_FAILURE, INDIE_DEMO, SHORT_FORM_DREAD, REGIONAL_FIRST_LOOK, PRESS_PREVIEW, STEAM_NEXT_FEST | One primary angle. |
| `personalization_note` | yes | sentence | Must mention creator-specific fit. |
| `risk_notes` | yes | text | Co-op expectation, huge creator, comedy mismatch, etc. |
| `last_verified` | yes | YYYY-MM-DD | Required before outreach. |
| `source` | yes | URL/file | Where row came from. |
| `outreach_batch` | optional | batch id | Example: `screenshots_A_001`. |
| `contact_route_verified` | optional | yes/no/date or route note | Required before any send; do not infer from platform presence. |
| `asset_ids_sent` | optional | comma-separated asset IDs | Must match approved asset metadata rows. |
| `creator_utility_score` | optional | 0/4 to 4/4 | Required for creator-facing asset sends; 3/4 minimum. |
| `utm_content` | optional | approved UTM content slug | Required only when an official public link exists. |
| `reply_deadline` | optional | YYYY-MM-DD | Used to prevent endless follow-up drift. |
| `followup_allowed` | optional | yes/no | One follow-up only and only with new asset/demo value. |
| `last_contacted` | optional | date |  |
| `reply_status` | optional | NONE, POSITIVE, NEGATIVE, NEEDS_BUILD, RATE_CARD, COVERED |  |
| `coverage_url` | optional | URL |  |
| `do_not_contact_reason` | optional | text | Required if DNC. |

## Scoring

Total score = `fit + recency + reach_proxy + asset_match + risk_modifier`

No subscriber counts needed for first pass. Use reach proxy only if publicly obvious.

### Fit Score

| Score | Meaning |
|---:|---|
| 5 | Direct audience: Subnautica, underwater survival, indie horror survival, survival crafting. |
| 4 | Strong adjacent: Barotrauma, The Long Dark, Pacific Drive, Dredge, The Forest, engineering survival. |
| 3 | Useful adjacent: factory/base-building, sci-fi, broad indie discovery. |
| 2 | Weak adjacent: general variety, broad gaming, press with no survival focus. |
| 1 | Mostly off-topic but possible special angle. |
| 0 | Do not contact. |

### Recency Score

| Score | Meaning |
|---:|---|
| 3 | Active in last 30 days. |
| 2 | Active in last 90 days. |
| 1 | Active in last year. |
| 0 | Inactive/unclear. |

### Reach Proxy

Use only public obvious tier, not fake numbers:

| Score | Meaning |
|---:|---|
| 3 | Large/highly visible creator/outlet. |
| 2 | Mid-tier or strong niche authority. |
| 1 | Small but relevant. |
| 0 | Unknown. |

### Asset Match

| Score | Meaning |
|---:|---|
| 3 | Current asset fits their format exactly. |
| 2 | Asset probably fits. |
| 1 | Needs better asset. |
| 0 | Do not contact yet. |

### Risk Modifier

| Score | Meaning |
|---:|---|
| 0 | Normal. |
| -1 | Broad audience, comedy mismatch, or high inbox noise. |
| -2 | Co-op expectation, controversy risk, or likely paid-only. |
| -3 | Bad fit until major proof exists. |

## Priority Mapping

| Total | Priority |
|---:|---|
| 12+ | A |
| 9-11 | B |
| 6-8 | C |
| <6 | D/archive |

## Outreach Readiness Gate

A lead is outreach-ready only if:

- `status = READY_FOR_HUMAN_REVIEW` or higher;
- recent activity checked;
- contact route verified;
- pitch angle assigned;
- one asset exists for their format;
- creator-facing asset utility score is 3/4+ when the outreach asks for feedback, preview interest, or future coverage;
- no co-op language;
- no fake performance claim;
- no generic pitch.

## Batch Naming

Use:

`YYYYMMDD_PHASE_SEGMENT_BATCH`

Examples:

- `20260601_SCREENSHOTS_SUBNAUTICA_A`
- `20260615_CLIP_HORROR_A`
- `20260710_DEMO_INDIE_B`
- `20260801_NEXTFEST_PRESS_A`

## Follow-Up Rules

- One follow-up only.
- Follow-up only with a new asset or demo date.
- No guilt language.
- No "just checking in" spam.
- If declined, mark `DECLINED`.
- If no answer after one follow-up, stop.

## Fraud / Impersonation Checks

Before sending keys:

- compare email domain against official site/channel;
- verify social link chain from creator page;
- use Steam Curator Connect where possible;
- never trust "I represent [creator]" without proof;
- send tiny key batches;
- revoke unused campaign keys where platform tooling allows;
- log every key.
