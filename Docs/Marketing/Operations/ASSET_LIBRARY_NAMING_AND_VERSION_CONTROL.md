# HECTON-8 Marketing Asset Library, Naming, And Version Control

Status: asset ops policy
Owner lane: SHINOBU_81 / marketing operations
Runtime impact: none

## Purpose

Marketing fails when assets are scattered, mislabeled, outdated, or accidentally imply the wrong build. This document defines file organization before screenshots and clips start multiplying.

## Folder Structure

Recommended outside repo if files are large; store index/metadata in docs.

```text
MarketingAssets/
  00_Brand/
  01_Screenshots/
    Raw/
    Edited/
    Approved/
    Rejected/
  02_Video/
    CaptureRaw/
    TrailerEdits/
    Shorts/
    Broll/
  03_Steam/
    Capsules/
    Screenshots/
    Trailers/
    Announcements/
  04_Presskit/
    Screenshots/
    Logos/
    Factsheet/
    Trailer/
  05_CreatorPacks/
  06_Localized/
  07_Archive/
```

## 2026-05-19 Empty Directory Skeleton V0

The empty filesystem skeleton now exists at repo root under `MarketingAssets/` so capture can start without inventing folders during the session. This is directory custody only. It does not imply that any asset exists, any metadata row is captured, or any public proof gate has passed.

Rules:

- do not add placeholder screenshots, fake captures, or `.gitkeep` files just to make folders visible in Git;
- do not treat an empty folder as an approved asset location;
- first real files still require `Data/MARKETING_ASSET_METADATA_TEMPLATE.csv` path/build/date/source updates;
- if large media should live outside Git later, keep this folder as a local working convention and store only metadata/index rows in docs.

## Filename Format

```text
H8_[AssetType]_[Hook]_[Build]_[Date]_[Version].[ext]
```

Examples:

```text
H8_Screenshot_PressureHatch_b0142_2026-06-02_v01.png
H8_Clip_SalvageRoute_b0142_2026-06-02_v03.mp4
H8_Capsule_Main_b0142_2026-06-05_v02.psd
H8_Trailer_Steam_b0150_2026-06-20_v07.mp4
```

## Asset Status Values

| Status | Meaning |
|---|---|
| PLANNED_CAPTURE | Capture/export slot exists, but no real asset has been captured. |
| RAW | Captured, not reviewed. |
| QA_FAIL | Rejected by checklist. |
| REVISION | Needs edit/capture change. |
| APPROVED_INTERNAL | Usable internally. |
| APPROVED_PUBLIC | Can publish. |
| APPROVED_PRESS | Can send to presskit. |
| DEPRECATED | Do not use; old build/claim. |
| LEGAL_HOLD | Licensing/claim issue. |

## Metadata Table

Track every public asset:

```csv
asset_id,type,path,build_id,date,status,hook,shown_features,source_capture,qa_score,multiplayer_scope_check,performance_claim_check,feature_truth_check,localization_check,owner,rejection_code,notes,creator_rows_unlocked,creator_utility_score,creator_send_gate,pain_bucket_answered,pain_proof_score,pain_freshness_source,pain_freshness_checked_at,public_comparison_gate,agency_decision_proof_gate,agency_decision_notes,capture_handoff_packet_id,capture_verdict,viewer_named_decision,capture_next_actions
```

## 2026-05-19 Planned Capture To Metadata Workflow V0

The current metadata file already contains planned slots. When real captures arrive, do not create new IDs unless the existing slot cannot describe the asset.

| Step | Action | Metadata field changed | Rule |
|---:|---|---|---|
| 1 | Copy raw capture into the matching planned path family. | `path` | Replace `[build]` and `[date]` with real values. |
| 2 | Record build and capture date. | `build_id`, `date` | No `TBD` remains for a captured asset. |
| 3 | Change status from `PLANNED_CAPTURE` to `RAW`. | `status` | Do not jump directly to public approval. |
| 4 | Fill capture source. | `source_capture` | Use scene/area/tool, not "screenshot". |
| 5 | Score asset through QA. | `qa_score` | Social minimum 9/12, Steam screenshot minimum 10/12. |
| 6 | Score creator utility if creator-facing. | `creator_rows_unlocked`, `creator_utility_score`, `creator_send_gate` | Creator-facing use requires 3/4+ and exact CRM row mapping; public social use does not imply creator send readiness. |
| 7 | Score private pain proof if SN2/market pain is part of capture priority. | `pain_bucket_answered`, `pain_proof_score`, `pain_freshness_source`, `pain_freshness_checked_at`, `public_comparison_gate` | Minimum 4/5 for first-pack priority after same-day/current-week source proof; always `PRIVATE_ONLY_NO_COMPETITOR_COPY` or stricter. This cannot create public comparison language. |
| 8 | Score agency/decision proof. | `agency_decision_proof_gate`, `agency_decision_notes` | First packet needs one asset where a cold viewer can name the player choice without caption; mood, anomaly, threat, or machinery alone is not enough. |
| 9 | Bind first-capture handoff packet. | `capture_handoff_packet_id`, `capture_verdict`, `viewer_named_decision`, `capture_next_actions` | These fields store the packet ID, verdict, named decision, and capped follow-up actions; notes-only handoff is invalid. |
| 10 | Check claims. | `multiplayer_scope_check`, `performance_claim_check`, `feature_truth_check` | Any failed claim check blocks public use. |
| 11 | Assign final state. | `status`, `rejection_code`, `notes` | Use fixed rejection codes only. |
| 12 | Move approved export into target folder. | `path`, `status` | Approved public/press assets must not point to Raw. |

### First Real Capture Intake Packet V0

Use this when the first screenshot or clip files exist. Do not wait for a full polished pack; intake the first useful files so weak captures fail fast.

Required facts before a planned row can become `RAW`:

| Field | Required value pattern | Reject if |
|---|---|---|
| `asset_id` | Existing `PLAN-*` row unless the capture proves a new hook. | A new ID duplicates an existing planned hook. |
| `path` | Real file path with build/date/version tokens resolved. | Path still contains `[build]`, `[date]`, or points to concept/reference art. |
| `build_id` | Exact build label, branch, or captured executable identifier. | `TBD`, "latest", "current", or guessed build. |
| `date` | Capture date in ISO form. | Capture date is missing or inferred later. |
| `source_capture` | Scene/route/tool/camera/settings note. | Generic value such as "screenshot" or "clip". |
| `shown_features` | Only visible current-build features. | Mentions roadmap-only systems. |
| `multiplayer_scope_check` | `SINGLE_PLAYER_SCOPE_OK`, `UNSUPPORTED_MULTIPLAYER_IMPLIED_FAIL`, or `NOT_APPLICABLE_INTERNAL`. | Any caption, UI, filename, or metadata implies unproved multiplayer scope. |
| `performance_claim_check` | `NO_PERFORMANCE_CLAIM`, `MEASURED_PROOF_ATTACHED`, or `PERF_CLAIM_UNPROVED_FAIL`. | FPS/Deck/low-end language appears without measured proof. |
| `feature_truth_check` | `CURRENT_BUILD_PROVEN`, `ACTIVE_WORK_INTERNAL_ONLY`, or `FEATURE_CLAIM_FAIL`. | The asset needs future systems to be honest. |
| `pain_freshness_source` | Monitoring section/version or source row, for example `Monitoring SN2 Steam API/Page Refresh V5`. | `PENDING_SAME_DAY_REFRESH`, empty, or notes-only source after `pain_proof_score` rises above 0. |
| `pain_freshness_checked_at` | ISO date of the source check used for pain-proof scoring. | Missing date or older-than-current-week check for first-pack priority. |
| `public_comparison_gate` | `PRIVATE_ONLY_NO_COMPETITOR_COPY` or stricter. | Metadata creates public competitor-attack copy. |
| `agency_decision_proof_gate` | `AGENCY_PROOF_CANDIDATE`, `SUPPORTING_AGENCY_SIGNAL`, or explicit non-proof value. | Missing value or mood-only value lets Campaign 01 bypass the player-choice gate. |
| `agency_decision_notes` | One sentence naming the visible player decision or why this asset cannot prove agency. | Notes are empty or repeat a vibe without a choice. |
| `capture_handoff_packet_id` | Stable first-capture packet ID or `PENDING_CAPTURE_PACKET` before capture. | Blank value, loose notes, or a packet ID not traceable to the shotlist handoff. |
| `capture_verdict` | `KEEP_TESTING`, `REVISE_SCENE`, `HOLD_ASSET`, `KILL_ANGLE`, `AGENCY_MISSING_HOLD`, or `PENDING_CAPTURE` before capture. | Verdict lives only in prose, or `KEEP_TESTING` is used while agency proof is missing. |
| `viewer_named_decision` | Exact decision named by the cold viewer, or `PENDING_VIEWER_DECISION` before capture. | Empty value on any agency candidate, or a mood noun instead of an action choice. |
| `capture_next_actions` | Up to three concrete next actions, or `PENDING_CAPTURE_NEXT_ACTIONS` before capture. | Action list is open-ended, generic, or not tied to a reject code/verdict. |

Minimum first intake batch:

1. One identity still: `PLAN-SHOT-001` or `REVISION` with exact reject code.
2. One player-verb still: `PLAN-SHOT-003` or `REVISION` with exact reject code.
3. One machinery/base still: `PLAN-SHOT-002`, `PLAN-SHOT-004`, or `PLAN-SHOT-005`.
4. One agency/decision proof candidate: `PLAN-SHOT-006`, `PLAN-CLIP-001`, or `PLAN-CLIP-003` with `agency_decision_proof_gate = AGENCY_PROOF_CANDIDATE` after capture QA.
5. Dashboard row drafted in `KPI/MARKETING_DASHBOARD_SPEC.md` Asset Gate Table for every captured asset.

If fewer than two assets can receive factual `build_id`, `source_capture`, and `feature_truth_check`, stop marketing intake and fix capture custody before more copy work.

### Status Promotion Rules

| From | To | Allowed only if |
|---|---|---|
| `PLANNED_CAPTURE` | `RAW` | real file exists and build/date/source are filled. |
| `RAW` | `REVISION` | asset has useful hook but fails a fixable issue. |
| `RAW` | `QA_FAIL` | asset fails identity, honesty, claim, or readability gate. |
| `RAW` | `APPROVED_INTERNAL` | useful for internal review but not public. |
| `APPROVED_INTERNAL` | `APPROVED_PUBLIC` | QA pass, claim checks pass, current build truth matches. |
| `APPROVED_PUBLIC` | `APPROVED_PRESS` | presskit use case and source/caption are finalized. |
| any | `DEPRECATED` | build truth changed or stronger asset replaced it. |
| any | `LEGAL_HOLD` | license/source/claim risk appears. |

Do not keep multiple `APPROVED_PUBLIC` versions for the same hook unless the use case differs.

## Public Asset Rules

- Every public asset needs build/source.
- Every public asset needs QA status.
- Do not reuse old screenshots after UI/feature changes without checking accuracy.
- Do not send raw captures to press.
- Do not publish rejected assets because "we need content".
- Do not edit screenshots so they imply non-existent gameplay.

## Versioning

Use version increments:

- `v01`: first working export;
- `v02+`: meaningful edit/crop/color/text change;
- `final`: forbidden in filename unless paired with version, because final rarely stays final.

Acceptable:

```text
H8_Trailer_Steam_b0150_2026-06-20_v07_APPROVED.mp4
```

Forbidden:

```text
final_final_real_final.mp4
```

## Rejection Reasons

Use fixed rejection codes:

- `GENERIC_VISUAL`;
- `NO_PLAYER_VERB`;
- `TOO_DARK`;
- `UI_UNREADABLE`;
- `CONCEPT_NOT_GAMEPLAY`;
- `UNSUPPORTED_MULTIPLAYER_SCOPE`;
- `PERF_CLAIM_UNPROVED`;
- `FEATURE_NOT_PUBLIC`;
- `LICENSE_RISK`;
- `BAD_COMPOSITION`;
- `TEXT_TOO_SMALL`;
- `DERIVATIVE_COMPETITOR_READ`.

Capture handoff hold/reject codes:

- `AGENCY_MISSING_HOLD`;
- `MISSING_HANDOFF_PACKET`;
- `VIEWER_DECISION_MISSING`;
- `HANDOFF_NEXT_ACTIONS_UNCAPPED`.

## Asset Review Ritual

Before each campaign:

1. List candidate assets.
2. Check build/source.
3. Score via QA checklist.
4. Check copy/feature implications.
5. Assign campaign/UTM.
6. Move approved export to correct folder.
7. Archive rejected versions.

## Current HECTON-8 Decision

The empty `MarketingAssets/` directory skeleton exists locally. It is not asset proof. Keep this file as the naming and metadata authority until real captures arrive.
