# Marketing KPI Dashboard Spec

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This document is active only as static documentation/source/platform-orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current official platform rules, current verification artifacts, and the latest DOC_GLOBAL reports.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, public Steam page, public demo, wishlist performance, creator outreach readiness, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters, platform rules, dates, creator availability, contact routes, and marketing claims inside this file are subordinate to fresh official sources and current project proof.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Status: tracking design / no live data yet

## Purpose

This dashboard prevents marketing by vibes.

No metric means no claim. No source means no decision.

R19 benchmark boundary: every target in this document is provisional until replaced by Steam/UTM/outreach/demo telemetry. Default source is `INTERNAL_ASSUMPTION`; do not treat target bands as market forecast, proof, or public copy.

## Core Tables

### Steam Daily

| Field | Type | Notes |
|---|---|---|
| date | date | Local date. |
| page_visits | int | Steam traffic. |
| wishlists_total | int | Current total. |
| wishlists_delta | int | Daily delta. |
| visit_to_wishlist_rate | float | wishlists_delta / page_visits. |
| top_source | text | UTM/source if available. |
| announcement_posted | bool | Did Steam event/news post happen? |
| demo_available | bool | Demo state. |
| notes | text | Context. |

Targets:

- source: `INTERNAL_ASSUMPTION` until Steam page telemetry exists;
- pre-demo page: 5-12% visit-to-wishlist;
- after weak page iteration: under 5% means fix page;
- Next Fest: >15% demo-download-to-wishlist if demo is strong.

### Short-Form Clip

| Field | Type | Notes |
|---|---|---|
| clip_id | text | Match capture naming. |
| platform | enum | TikTok, YouTube Shorts, Reels, X, Bluesky. |
| hook_type | enum | pressure, sonar, salvage, machine, Seed Ship. |
| views | int | Platform. |
| three_second_hold | float | Target >65%. |
| completion_rate | float | Target >40% for 20s. |
| click_rate | float | Wishlist clicks / views. |
| comments_clone_ratio | float | Clone comments / total meaningful comments. |
| comments_unique_feature_ratio | float | Comments naming pressure/machine/salvage/etc. |

Kill criteria:

- completion under 25% across 10 clips;
- clone ratio dominates;
- no one names a HECTON-8-specific feature.

### Creator Outreach

| Field | Type | Notes |
|---|---|---|
| lead_id | text | Stable ID. |
| segment | enum | survival, horror, Subnautica, sim, indie, regional. |
| priority | enum | A/B/C/RAW. |
| status | enum | RAW, VERIFY, APPROVED, CONTACTED, REPLIED, COVERED, DNC. |
| contact_date | date | If contacted. |
| reply | enum | none, positive, negative, needs_build, covered. |
| coverage_url | url | If covered. |
| pitch_angle | enum | one primary angle. |
| notes | text | Manual. |

Targets:

- source: `INTERNAL_ASSUMPTION` until UTM/outreach telemetry exists;
- targeted creator reply rate: >5%;
- broad batch under 2% means poor fit/copy;
- no mass outreach until assets exist.

### Community Feedback

| Field | Type | Notes |
|---|---|---|
| post_id | text | Stable. |
| platform | text | Reddit/Steam/etc. |
| asset | text | Screenshot/clip ID. |
| question | text | One feedback question. |
| useful_comments | int | Non-meme feedback. |
| positive_signal | int | Useful positive comments. |
| negative_signal | int | Useful negative comments. |
| clone_comments | int | "Subnautica clone" signal. |
| confusion_comments | int | Did not understand asset. |
| action_taken | text | Recapture/rewrite/ignore. |

Targets:

- useful comments > low-effort reactions;
- confusion below 20% of useful comments;
- clone comments declining over asset iterations.

## 2026-05-19 Proof-Gate Dashboard V0

Use this before Steam telemetry exists. It measures whether the project is allowed to move from G0 prep to G1 screenshot drop, then from G1 to Steam page launch.

### Asset Gate Table

| Field | Type | Notes |
|---|---|---|
| asset_id | text | Must match `PLAN-SHOT-*`, `PLAN-CLIP-*`, or `PLAN-CAPSULE-*`. |
| build_id | text | Required after capture. |
| status | enum | `PLANNED_CAPTURE`, `RAW`, `REVISION`, `QA_FAIL`, `APPROVED_INTERNAL`, `APPROVED_PUBLIC`, `DEPRECATED`, `LEGAL_HOLD`. |
| qa_score | int | 0-12 for screenshots, use clip checklist for clips. |
| cold_read_genre_correct | int | Count of viewers who identify underwater survival. |
| cold_read_player_verb | int | Count of viewers who name player action/problem. |
| clone_comments | int | Count of clone/derivative comments. |
| unreadable_comments | int | Count of darkness/clarity failures. |
| ai_or_concept_comments | int | Count of fake/AI/concept-art suspicion. |
| decision | enum | `KEEP`, `REVISE`, `KILL`, `HOLD`. |
| next_action | text | Capture again, recut, approve, or block. |

Minimum to advance Campaign 01:

- at least 6 real assets are no longer `PLANNED_CAPTURE`;
- at least 4 screenshots score 10/12 or higher;
- identity hero or salvage shot passes cold-read genre at 70%;
- no asset selected for lead use has unresolved co-op/performance/AI-looking risk;
- final decision is `KEEP`, not `REVISE`.

### First Public Beat Table

| Field | Type | Notes |
|---|---|---|
| beat_id | text | Example: `screenshot_drop_01_x_post_001`. |
| asset_id | text | Exact asset used. |
| platform | text | X, Bluesky, Reddit, Steam, YouTube. |
| campaign | text | `screenshot_drop_01`, `steam_page_launch`, etc. |
| post_url | url | Blank until public. |
| useful_comments | int | Non-meme feedback. |
| intended_nouns | int | Comments naming pressure, machine, salvage, base, black water, Seed Ship. |
| confusion_comments | int | Comments asking what the game/action is. |
| clone_comments | int | Direct derivative comparison. |
| co_op_comments | int | Assumes or asks for multiplayer. |
| decision | enum | `ADVANCE`, `HOLD`, `REVISE`, `KILL`. |

Decision rule:

- `ADVANCE` only if intended nouns outnumber confusion + clone + co-op comments.
- `REVISE` if interest exists but confusion repeats.
- `KILL` if lead asset causes clone, AI-looking, or false-feature damage.
- raw likes do not affect the decision.

### Weekly Current-State Summary

```text
Week:
Current gate: G0/G1/G2/G3/G4
Assets captured:
Assets approved public:
Campaign 01 decision:
Steam page status:
Creator Wave A status:
Spend status:
Top blocker:
Next action:
```

## Weekly Report Format

Title:

`Marketing Weekly YYYY-MM-DD`

Sections:

1. Steam movement.
2. Best asset.
3. Worst asset.
4. Creator pipeline movement.
5. Community feedback themes.
6. Competitor signal.
7. Decisions.
8. Kill criteria triggered.
9. Next week work.

## Dashboard Rules

- Do not average across different asset types without segmenting.
- Do not compare paid and organic traffic directly.
- Do not count bot/spam comments as signal.
- Do not treat wishlist total as proof of game quality.
- Do not celebrate impressions if no wishlist or creator action follows.
- Do not hide negative "clone" signal; it is the main differentiation warning.
