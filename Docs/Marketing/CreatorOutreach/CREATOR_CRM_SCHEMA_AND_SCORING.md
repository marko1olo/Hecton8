# Creator CRM Schema And Scoring

Status: operational specification
Purpose: turn raw leads into outreach ready rows without lying

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026 05 17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

  `Docs/README.md`
  `Docs/DOC_GOVERNANCE.md`
  current public creator/contact evidence
  fresh CRM/outreach logs

No lead score, contact readiness, subscriber/reach state, platform policy compliance, runtime build, Unity import, profiler, player build, or campaign performance proof is implied unless this document links a fresh evidence artifact. Scoring rules are an internal triage model, not external market telemetry.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Table Schema

Use the live CSV schema in `Docs/Marketing/Data/CREATOR_VERIFICATION_TEMPLATE.csv`. Do not add renamed duplicate columns when a field already exists here.

| Field | Required | Values / Format | Notes |
|---|---|---|---|
| `lead_id` | yes | stable slug | Example: `yt_igp_001`. |
| `source_file` | yes | file slug | Where the row came from inside the marketing docs/data pass. |
| `source_url` | yes | URL/file | Public or internal source used for the row. |
| `creator_name` | yes | text | Public name only. |
| `channel_url` | yes | URL | Public page. |
| `platform` | yes | YouTube, Twitch, Web, Newsletter, Steam Curator, TikTok, Other |  |
| `language` | yes | English, Russian, German, etc, UNKNOWN | Apparent only. |
| `country_or_region` | optional | text | Do not infer if unclear. |
| `recent_activity_status` | yes | VERIFIED_ACTIVE, OLD_OR_INACTIVE, UNKNOWN, note | Must be checked before contact. |
| `last_relevant_video_url` | optional | URL/UNKNOWN | Use only current public video/source evidence. |
| `audience_fit_score` | yes | 0 5 | See scoring below. |
| `segment` | yes | SUBNAUTICA, SURVIVAL, HORROR, ENGINEERING, INDIE, PRESS, REGIONAL, SHORTS, TWITCH | One primary segment. |
| `contact_route` | yes | UNKNOWN, PUBLIC_EMAIL, CONTACT_FORM, YOUTUBE_ABOUT, TWITCH_BUSINESS, STEAM_CURATOR_CONNECT, SITE_FORM | Do not invent. |
| `contact_url` | optional | exact public route | Only official public contact. |
| `email_publicly_listed` | yes | yes/no/UNKNOWN | Do not reveal private or guessed emails. |
| `sponsorship_policy` | optional | text/UNKNOWN | Use only stated policy. |
| `denylist_status` | yes | CLEAR, HOLD, DO_NOT_CONTACT | Required before send. |
| `risk_notes` | yes | text | Unsupported multiplayer-scope expectation, huge creator, comedy mismatch, scam risk, or source caveat. |
| `pitch_angle` | yes | UNDERWATER_SURVIVAL, PRESSURE_HORROR, BASE_SYSTEMS, HEAVY_MACHINES, ENGINEERING_FAILURE, INDIE_DEMO, SHORT_FORM_DREAD, REGIONAL_FIRST_LOOK, PRESS_PREVIEW, STEAM_NEXT_FEST | One primary angle. |
| `personalized_opener` | yes | sentence | Must mention creator specific fit, but is not send approval. |
| `status` | yes | VERIFY_BEFORE_CONTACT, NEEDS_ASSET, LOW_PRIORITY_VERIFY_LATER, DO_NOT_CONTACT, CONTACTED, REPLIED, COVERED, DECLINED | Never skip verification; current CRM has 0 raw rows. `CONTACTED`, `REPLIED`, and `COVERED` require the structured send/reply/coverage fields below, not notes-only evidence. |
| `next_action` | yes | text | Operational next step, not final send copy. |
| `verified_by` | yes | agent/person/batch | Do not use as a public proof claim. |
| `verified_at` | yes | YYYY MM DD | Required before outreach. |
| `outreach_batch` | optional | batch id | Example: `screenshots_A_001`. |
| `sent_date` | optional | YYYY MM DD | Blank until actual send. |
| `contact_route_verified_for_send` | optional | yes/no/date or route note | Required before any send; do not infer from platform presence. |
| `asset_ids_sent` | optional | comma separated asset IDs | Must match approved asset metadata rows; first-public or gameplay-proof sends need at least one factual `AGENCY_PROOF_CANDIDATE` asset plus AB-009/KPI decision-read fields when the message references pressure decisions, threat, route risk, salvage failure, or demo readiness. |
| `creator_utility_score` | optional | 0/4 to 4/4 | Required for creator facing asset sends; 3/4 minimum. |
| `paid_creator_permission_gate` | optional | BLOCKED_NO_PAID_CREATOR_PROOF, BLOCKED_NO_DEMO_OR_STEAM_BASELINE, BLOCKED_NO_RATE_CARD_RESPONSE, BLOCKED_DISCLOSURE_OR_ROUTE_GAP, ALLOW_PAID_CREATOR_TEST_VERIFIED, DO_NOT_PAY_CREATOR | Machine-readable paid creator spend gate. Current CRM rows stay blocked; only `ALLOW_PAID_CREATOR_TEST_VERIFIED` can permit a paid creator test. |
| `utm_content` | optional | approved UTM content slug | Required only when Official CTA Link Activation Gate V0 passes for a public link. |
| `reply_deadline` | optional | YYYY MM DD | Used to prevent endless follow up drift. |
| `followup_allowed` | optional | yes/no | One follow up only and only with new asset/demo value. |
| `reply_status_after_send` | optional | NONE, POSITIVE, NEGATIVE, NEEDS_BUILD, RATE_CARD, COVERED | Blank before send. |
| `send_route_class` | optional | NO_LINK_CREATOR_FEEDBACK, PUBLIC_CTA_CREATOR, PRIVATE_ACCESS_CREATOR | Required before `CONTACTED`; tells analytics whether the send was feedback-only, public CTA, or controlled access. |
| `reply_consent_provenance` | optional | CREATOR_REPLY, EXPLICIT_PLAYTEST_OPT_IN, EXPLICIT_NEWSLETTER_OPT_IN, EXPLICIT_PRESS_PERMISSION, NONE | Blank before reply. A creator reply is not newsletter, playtest, or press consent unless explicit opt-in exists. |
| `coverage_url` | optional | URL | Required before `COVERED` when public coverage exists. |

Schema aliases rejected: do not create `contact_route_verified`, `reply_status`, `last_contacted`, `contact_value`, `personalization_note`, `country_region`, `audience_fit`, `route_class`, `consent_provenance`, or `reply_consent` in the live CSV. Use the exact current field names above.

## Scoring

Total score = `fit + recency + reach_proxy + asset_match + risk_modifier`

No subscriber counts needed for first pass. Use reach proxy only if publicly obvious.

### Fit Score

| Score | Meaning |
|---:|---|
| 5 | Direct audience: Subnautica, underwater survival, indie horror survival, survival crafting. |
| 4 | Strong adjacent: Barotrauma, The Long Dark, Pacific Drive, Dredge, The Forest, engineering survival. |
| 3 | Useful adjacent: factory/base building, sci fi, broad indie discovery. |
| 2 | Weak adjacent: general variety, broad gaming, press with no survival focus. |
| 1 | Mostly off topic but possible special angle. |
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
| 2 | Mid tier or strong niche authority. |
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
|  1 | Broad audience, comedy mismatch, or high inbox noise. |
|  2 | Unsupported multiplayer-scope expectation, controversy risk, or likely paid only. |
|  3 | Bad fit until major proof exists. |

## Priority Mapping

| Total | Priority |
|---:|---|
| 12+ | A |
| 9-11 | B |
| 6-8 | C |
| <6 | D/archive |

## Outreach Readiness Gate

A lead is outreach ready only if:

  `status = VERIFY_BEFORE_CONTACT` at minimum, followed by same day send review;
  `NEEDS_ASSET` remains blocked until the matching asset exists, passes QA, and the row is rechecked;
  recent activity checked;
  contact route verified;
  pitch angle assigned;
  one asset exists for their format;
  official project inbox custody exists before any creator facing send;
  `contact_route_verified_for_send` is filled from the official route used that day;
  `send_route_class` is filled as feedback-only, public CTA, or private access before `CONTACTED`;
  creator facing asset utility score is 3/4+ when the outreach asks for feedback, preview interest, or future coverage;
  `creator_send_gate` is open on the matching asset metadata row;
  paid creator spend remains blocked unless the row has `paid_creator_permission_gate = ALLOW_PAID_CREATOR_TEST_VERIFIED`;
  `agency_decision_proof_gate` and `agency_decision_notes` are filled on every referenced asset, and any gameplay/pressure/route-risk send includes one factual `AGENCY_PROOF_CANDIDATE` with AB-009/KPI `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`;
  no unsupported multiplayer-scope language;
  no fake performance claim;
  no generic pitch.

Current pre-send HOLD state:

- `Docs/Marketing/Data/CREATOR_VERIFICATION_TEMPLATE.csv` should have 0 non-empty values in `outreach_batch`, `sent_date`, `contact_route_verified_for_send`, `asset_ids_sent`, `creator_utility_score`, `send_route_class`, `reply_consent_provenance`, and `reply_status_after_send` until a real human send occurs.
- The same CRM should keep all current `paid_creator_permission_gate` rows blocked until demo/Steam baseline, asset proof, route/disclosure custody, rate-card response, owner budget approval, and stop rules exist.
- `Docs/Marketing/Data/MARKETING_ASSET_METADATA_TEMPLATE.csv` should keep all 13 planned rows at `creator_send_gate = BLOCKED_PLANNED_CAPTURE` and `creator_utility_score = 0` until captured assets pass QA and per-recipient utility review.
- The same asset metadata file should keep agency proof as structured data: exactly three planned rows are `AGENCY_PROOF_CANDIDATE` before capture QA, and planned status is not send proof. Creator sends that claim gameplay/pressure/route-risk proof also require the matching AB-009/KPI decision-read field before `CONTACTED`.
- `reply_consent_provenance` stays blank until a reply exists; default creator replies are `CREATOR_REPLY` only, not newsletter/playtest/press consent.
- A filled CRM send-log field without matching asset metadata proof is a data integrity failure. Move the row to HOLD review and inspect the send route before any follow-up.

## CRM Copy Field Boundary

`risk_notes`, `recent_activity_status`, and source fields may contain direct competitor titles because they are internal evidence. `pitch_angle`, `personalized_opener`, `next_action`, and any future send log text must be safe to paste into a human draft after normal editing.

Rules:

  do not put `Subnautica 2`, `SN2`, EULA/privacy complaints, desync, stutter, or competitor pain into `personalized_opener`;
  use `recent underwater survival coverage`, `current survival coverage`, or a segment specific neutral phrase instead;
  keep direct competitor title evidence in `risk_notes` or `recent_activity_status`;
  if a human needs to reference a title directly, it must be approved during final send review and pass `public_comparison_gate`.

## Batch Naming

Use:

`YYYYMMDD_PHASE_SEGMENT_BATCH`

Examples:

  `20260601_SCREENSHOTS_SUBNAUTICA_A`
  `20260615_CLIP_HORROR_A`
  `20260710_DEMO_INDIE_B`
  `20260801_NEXTFEST_PRESS_A`

## Follow Up Rules

  One follow up only.
  Follow up only with a new asset or demo date.
  No guilt language.
  No "just checking in" spam.
  If declined, mark `DECLINED`.
  If no answer after one follow up, stop.

## Fraud / Impersonation Checks

Before sending keys:

  compare email domain against official site/channel;
  verify social link chain from creator page;
  use Steam Curator Connect where possible;
  never trust "I represent [creator]" without proof;
  send tiny key batches only after `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, official inbox custody, exact access-log fields, and disclosure pass for the recipient or batch;
  revoke unused campaign keys where platform tooling allows;
  log every key.
