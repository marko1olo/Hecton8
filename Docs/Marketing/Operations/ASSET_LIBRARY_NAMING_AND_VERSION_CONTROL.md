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
asset_id,type,path,build_id,date,status,hook,shown_features,source_capture,qa_score,co_op_check,performance_claim_check,feature_truth_check,localization_check,owner,rejection_code,creator_rows_unlocked,creator_utility_score,creator_send_gate,notes
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
| 7 | Check claims. | `co_op_check`, `performance_claim_check`, `feature_truth_check` | Any failed claim check blocks public use. |
| 8 | Assign final state. | `status`, `rejection_code`, `notes` | Use fixed rejection codes only. |
| 9 | Move approved export into target folder. | `path`, `status` | Approved public/press assets must not point to Raw. |

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
- `IMPLIES_COOP`;
- `PERF_CLAIM_UNPROVED`;
- `FEATURE_NOT_PUBLIC`;
- `LICENSE_RISK`;
- `BAD_COMPOSITION`;
- `TEXT_TOO_SMALL`;
- `DERIVATIVE_COMPETITOR_READ`.

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

Create asset library structure when first real screenshots exist. For now, keep this as the naming and metadata authority.
