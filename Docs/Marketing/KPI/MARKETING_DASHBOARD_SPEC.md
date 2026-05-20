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
| status | enum | Use live CRM values only: `VERIFY_BEFORE_CONTACT`, `NEEDS_ASSET`, `LOW_PRIORITY_VERIFY_LATER`, `DO_NOT_CONTACT`, `CONTACTED`, `REPLIED`, `COVERED`, or `DECLINED`. |
| contact_date | date | If contacted. |
| asset_ids_sent | text | Required before a row is counted as asset-backed outreach. |
| creator_utility_score | int | Required for creator-facing asset sends; 3/4+ for Wave A use. |
| creator_send_gate | enum | Asset-side send gate from metadata; blocked values are not reportable outreach. |
| send_route_class | enum | Official route class used for the send; blank or `unknown` blocks reporting. |
| reply_consent_provenance | enum | Required before a reply is reused outside the original route. |
| send_gate_source | text | CRM/send/access gate or tracker field that allowed the send. |
| reply | enum | none, positive, negative, needs_build, covered. |
| coverage_url | url | If covered. |
| pitch_angle | enum | one primary angle. |
| notes | text | Manual. |

Targets:

- source: `INTERNAL_ASSUMPTION` until UTM/outreach telemetry exists;
- targeted creator reply rate: >5%;
- broad batch under 2% means poor fit/copy;
- no mass outreach until assets exist.
- do not count creator outreach in reply-rate or coverage-rate metrics unless `asset_ids_sent`, `creator_utility_score`, `creator_send_gate`, `send_route_class`, `reply_consent_provenance`, and `send_gate_source` are populated where applicable.

### Community Feedback

| Field | Type | Notes |
|---|---|---|
| post_id | text | Stable. |
| platform | text | Reddit/Steam/etc. |
| route_class | enum | `public_cta`, `no_link_feedback`, `support_route`, `private_playtest`, `creator_reply`, `press_reply`, or `unknown`. |
| consent_provenance | enum | `public_comment`, `invited_feedback`, `support_report`, `playtest_consent`, `creator_reply`, `press_reply`, or `unknown`. |
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

`unknown` route or consent values are import quarantine values only. They cannot be counted in weekly reports, agency-proof reads, creator/press reply rates, public CTA performance, support trends, or owned-audience metrics until the source row is corrected to a specific route and provenance.

## 2026-05-19 Proof-Gate Dashboard V0

Use this before Steam telemetry exists. It measures whether the project is allowed to move from G0 prep to G1 screenshot drop, then from G1 to Steam page launch.

### Asset Gate Table

| Field | Type | Notes |
|---|---|---|
| asset_id | text | Must match `PLAN-SHOT-*`, `PLAN-CLIP-*`, or `PLAN-CAPSULE-*`. |
| build_id | text | Required after capture. |
| status | enum | `PLANNED_CAPTURE`, `RAW`, `REVISION`, `QA_FAIL`, `APPROVED_INTERNAL`, `APPROVED_PUBLIC`, `DEPRECATED`, `LEGAL_HOLD`. |
| qa_score | int | 0-12 for screenshots, use clip checklist for clips. |
| pain_bucket_answered | text | Private proof bucket from asset metadata; not public comparison copy. |
| pain_proof_score | int | 0-5, from the private pain-proof gate after source/date freshness check. |
| pain_freshness_source | text | Monitoring refresh/source row used for the private pain proof score. |
| pain_freshness_checked_at | date | Date when the pain source was checked for the asset score. |
| public_comparison_gate | enum | `PRIVATE_ONLY_NO_COMPETITOR_COPY`, `INTERNAL_ONLY_NO_PUBLIC_PERFORMANCE_COMPARISON`, or stricter. |
| agency_decision_proof_gate | enum | Metadata value; first-packet advance needs one `AGENCY_PROOF_CANDIDATE`. |
| agency_decision_notes | text | One sentence naming the readable player choice or why the asset is not agency proof. |
| capture_handoff_packet_id | text | Stable first-capture packet ID from the shotlist/metadata handoff. |
| capture_verdict | enum | `KEEP_TESTING`, `REVISE_SCENE`, `HOLD_ASSET`, `KILL_ANGLE`, `AGENCY_MISSING_HOLD`, or `PENDING_CAPTURE`. |
| viewer_named_decision | text | The actual decision a cold viewer named without prompt; required for agency candidates. |
| capture_next_actions | text | Up to three concrete follow-up actions from the handoff packet. |
| cold_read_genre_correct | int | Count of viewers who identify underwater survival. |
| cold_read_player_verb | int | Count of viewers who name player action/problem. |
| cold_read_agency_decision | int | Count of valid blind readers who name the next pressure decision without prompt. |
| clone_comments | int | Count of clone/derivative comments. |
| unreadable_comments | int | Count of darkness/clarity failures. |
| ai_or_concept_comments | int | Count of fake/AI/concept-art suspicion. |
| decision | enum | `KEEP`, `REVISE`, `KILL`, `HOLD`. |
| next_action | text | Capture again, recut, approve, or block. |

Minimum to advance Campaign 01:

- at least 6 real assets are no longer `PLANNED_CAPTURE`;
- at least 4 screenshots score 10/12 or higher;
- each priority asset has `pain_proof_score` 4/5+ with `pain_freshness_source` and `pain_freshness_checked_at` filled, and no public comparison gate violation;
- at least one first-pack asset has `agency_decision_proof_gate = AGENCY_PROOF_CANDIDATE` and 60%+ valid blind readers can name the decision without prompt;
- identity hero or salvage shot passes cold-read genre at 70%;
- no asset selected for lead use has unresolved multiplayer-scope, performance, or AI-looking risk;
- final decision is `KEEP`, not `REVISE`.

### Capture Intake Join

The dashboard does not replace asset metadata or the first-capture handoff packet. For first captures, copy only the following facts from `Data/MARKETING_ASSET_METADATA_TEMPLATE.csv` and `Content/SCREENSHOT_AND_CLIP_SHOTLIST.md` after the metadata row and handoff packet are updated:

| Dashboard field | Metadata source | Gate |
|---|---|---|
| `asset_id` | `asset_id` | Must already exist or be justified by a new non-duplicate hook. |
| `file_path` | `path` / first-capture handoff packet | Cannot be blank, guessed, or point to a planned placeholder. |
| `build_id` | `build_id` | Cannot be `TBD`, `latest`, or guessed. |
| `status` | `status` | Use `RAW`, `REVISION`, `QA_FAIL`, `APPROVED_INTERNAL`, or `APPROVED_PUBLIC`; do not dashboard stale `PLANNED_CAPTURE` rows as proof. |
| `qa_score` | `qa_score` | Must come from `QA/MARKETING_ASSET_QA_CHECKLIST.md`. |
| `rejection_code` | `rejection_code` / first-capture handoff packet | Required for failed attempts; blank failure rows are not reportable proof. |
| `creator_rows_unlocked` | `creator_rows_unlocked` | Required before asset-backed creator outreach is counted. |
| `creator_utility_score` | `creator_utility_score` | Required for creator-facing use; 3/4+ for Wave A. |
| `creator_send_gate` | `creator_send_gate` | Blocked values prevent creator reporting even if QA score is high. |
| `pain_bucket_answered` | `pain_bucket_answered` | Private priority only; never public copy. |
| `pain_proof_score` | `pain_proof_score` | 0 until QA assigns it; first-pack priority requires 4/5 after source/date freshness check. |
| `pain_freshness_source` | `pain_freshness_source` | Must name the monitoring refresh/source row used for nonzero pain proof. |
| `pain_freshness_checked_at` | `pain_freshness_checked_at` | Must be same-day/current-week for first-pack priority. |
| `public_comparison_gate` | `public_comparison_gate` | Must stay `PRIVATE_ONLY_NO_COMPETITOR_COPY` or stricter for first-pack use. |
| `agency_decision_proof_gate` | `agency_decision_proof_gate` | Must be present; first-pack advance needs one `AGENCY_PROOF_CANDIDATE`. |
| `agency_decision_notes` | `agency_decision_notes` | Must name the readable choice or explain non-proof status; blank notes force `decision=HOLD`. |
| `capture_handoff_packet_id` | `capture_handoff_packet_id` | Must point to the first-capture packet; planned rows stay `PENDING_CAPTURE_PACKET`. |
| `capture_verdict` | `capture_verdict` | `AGENCY_MISSING_HOLD`, `REVISE_SCENE`, `HOLD_ASSET`, or `KILL_ANGLE` blocks Campaign 01 and agency-proof reporting. |
| `viewer_named_decision` | `viewer_named_decision` / AB-009 row | Required before an agency candidate can drive Campaign 01, Steam, creator, press, or weekly agency-proof reporting. |
| `capture_next_actions` | `capture_next_actions` | Required for every failed or held packet; action list must be capped at three. |

If a dashboard row contains a value not present in metadata, QA, or the first-capture handoff packet, mark `decision=HOLD` and fix the source row first. If the handoff packet records `AGENCY_MISSING_HOLD`, Campaign 01 and agency-proof reporting remain held.

### Cold-Read Response Table

Use with `Experiments/A_B_TESTING_AND_CREATIVE_EXPERIMENTS.md` Cold-Read Score Sheet V0.

| Field | Type | Notes |
|---|---|---|
| response_id | text | Stable anonymous id. |
| experiment_id | text | `AB-001`, `AB-002`, `AB-004`, `AB-006`, `AB-007`, or `AB-009`. |
| asset_id | text | Exact `PLAN-*` asset or copy variant. |
| reader_type | enum | internal, player, creator, press, unknown. |
| context_exposure | enum | `NONE`, `CONTEXT_EXPOSED`, `PROMPT_ECHO`, `UNKNOWN`. |
| valid_blind_read | bool | True only when `context_exposure=NONE`. |
| genre_correct | bool | True only if the reader names survival/underwater/exploration/base-adjacent genre without prompt. |
| player_verb_correct | bool | True only if the reader names action/problem, not just mood. |
| what_decision_next | text | Raw answer to the agency prompt; empty answers count as no. |
| agency_decision_read | bool | True only if the reader names repair, retreat, reroute, scan, operate, abort, recover, or an equivalent pressure decision without prompt. |
| identity_nouns | text | Pressure, machinery, salvage, base, black water, Seed Ship, etc. |
| mode_assumption | enum | single-player, unsupported_multiplayer_assumption, multiplayer_question, unknown. |
| proof_belief | enum | gameplay, concept, AI-looking, unsure. |
| readability_issue | enum | none, too_dark, too_busy, ui_unclear, unknown. |
| click_interest | int | 0-4. |
| kill_reason | text | Verbatim where possible. |
| decision_impact | enum | keep, revise, kill, ignore_noise. |

Do not merge cold-read response rows into public engagement metrics. Cold-read answers decide whether an asset is allowed to face public traffic at all. Contaminated reads can create fix notes but cannot count toward pass percentages.

### First Public Beat Table

| Field | Type | Notes |
|---|---|---|
| beat_id | text | Example: `screenshot_drop_01_x_post_001`. |
| asset_id | text | Exact asset used. |
| platform | text | X, Bluesky, Reddit, Steam, YouTube. |
| campaign | text | `screenshot_drop_01`, `steam_page_launch`, etc. |
| route_class | enum | `no_link_feedback`, `public_cta`, `support_route`, `private_access`, or `unknown`. |
| cta_packet_id | text | Required when `route_class=public_cta`; blank for no-link feedback. |
| access_route_class | enum | Required when the beat came from key, private preview, Steam Playtest, tester recruitment, or demo outreach. |
| reply_consent_provenance | enum | Required before private-access or tester feedback is reused outside its original route; use the same field name as creator, press, and curator trackers. |
| agency_decision_field_source | text | Required when access/demo/playtest copy claims gameplay/pressure/route-risk proof. |
| post_url | url | Blank until public. |
| useful_comments | int | Non-meme feedback. |
| intended_nouns | int | Comments naming pressure, machine, salvage, base, black water, Seed Ship. |
| agency_decision_read_comments | int | Comments naming a concrete player choice, not just danger or mood. |
| confusion_comments | int | Comments asking what the game/action is. |
| clone_comments | int | Direct derivative comparison. |
| multiplayer_scope_comments | int | Assumes, asks for, or reads unsupported multiplayer scope into the asset/copy. |
| decision | enum | `ADVANCE`, `HOLD`, `REVISE`, `KILL`. |

Decision rule:

- `ADVANCE` only if intended nouns outnumber confusion + clone + multiplayer-scope comments.
- `REVISE` if interest exists but confusion repeats.
- `KILL` if lead asset causes clone, AI-looking, or false-feature damage.
- raw likes do not affect the decision.

`unknown` route class blocks `ADVANCE`. If the beat used a public link, `cta_packet_id` must point to a destination whose `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`; if the beat came from private access, `access_route_class`, `reply_consent_provenance`, and any required `agency_decision_field_source` must be non-empty before the row is reportable.

### Weekly Current-State Summary

```text
Week:
Current gate: G0/G1/G2/G3/G4
Assets captured:
Assets approved public:
Campaign 01 decision:
Steam page status:
Creator Wave A status:
Agency proof status:
Spend status:
Route/consent gaps:
Rows excluded for route/permission/provenance gaps:
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
6. Agency-decision read rate.
7. Competitor signal.
8. Decisions.
9. Kill criteria triggered.
10. Next week work.

## Dashboard Rules

- Do not average across different asset types without segmenting.
- Do not compare paid and organic traffic directly.
- Do not count bot/spam comments as signal.
- Do not treat wishlist total as proof of game quality.
- Do not celebrate impressions if no wishlist or creator action follows.
- Do not hide negative "clone" signal; it is the main differentiation warning.
- Do not merge feedback, form, or support rows without `route_class` and `consent_provenance`; creator/press/curator rows use `send_route_class` and `reply_consent_provenance`; private access rows use `access_route_class` and `reply_consent_provenance`.
- Do not count support reports, playtest forms, creator replies, or press replies as newsletter, CRM, or public marketing consent.
- Do not count a gameplay/pressure/route-risk asset as agency proof unless `agency_decision_read`, `agency_decision_read_comments`, or `cold_read_agency_decision` records the decision readers named.
- Do not count key/access/playtest/demo outreach replies or claims unless `access_route_class`, `reply_consent_provenance`, and `agency_decision_field_source` are present where relevant.
- Do not count creator, press, curator, support, public CTA, private access, or owned-audience rows whose permission gate/source is blank or whose route/provenance field is `unknown`.
