# DEAD_ASSET_SWEEP_REPORT
Date: 2026-05-07
Status: PENDING VERIFICATION


**Date:** 2026-04-29
**Status:** PENDING VERIFICATION
**Scope:** filesystem-only recheck of prior dead-asset accusations under `Assets/_Project`

**Mandates Followed:** `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

---

## Method

- Re-checked the specific paths named in the earlier sweep.
- Counted live filesystem entries only.
- Did not run Unity dependency graph, Addressables dependency graph, or `AssetDatabase.FindDependencies`.

---

## What Can Be Proved From This Pass

| Path or area | Current result |
|---|---|
| `Assets/_Project` | `7963` files currently present |
| `Assets/_Project/Art/Models/Rocks/Rock 4 - Ð£ÐÐ˜Ð’Ð•Ð Ð¡ÐÐ›Ð¬ÐÐ«Ð™ Ð’Ð«Ð‘ÐžÐ ` | folder exists and currently contains `41` files in this sweep |
| `Assets/_Project/Art/Models/Sandbox` | folder exists and currently contains `4` files in this sweep |
| `Assets/_Project/Art/Models/Sandbox/Coral_Albedo.png` | present |
| `Assets/_Project/Art/Models/Sandbox/Coral_Normal.png` | present |
| sandbox content at project level | present beyond the two textures, including sandbox scenes and sandbox attraction profiles |

---

## Claims The Old Report Could Not Support

- `"all materials are used"` was not proven.
- `"Rock 4-7 are unused"` was not proven.
- `"delete Sandbox textures"` was not justified by current project state.
- A filesystem scan alone cannot classify an asset as dead when sandbox scenes, authoring profiles, or late-bound references exist.

---

## Current Verdict On The Earlier Candidates

| Candidate | Current judgement |
|---|---|
| `Rock 4 - Ð£ÐÐ˜Ð’Ð•Ð Ð¡ÐÐ›Ð¬ÐÐ«Ð™ Ð’Ð«Ð‘ÐžÐ ` | `NOT PROVEN DEAD` |
| `Coral_Albedo.png` / `Coral_Normal.png` | `NOT PROVEN DEAD` |
| Sandbox-labeled assets in general | `NOT PROVEN DEAD`; sandbox content still exists as an active content bucket in the project tree |

---

## What Is Required For A Real Dead-Asset Audit

- Unity-side dependency graph readback
- prefab/material/scene referencer resolution
- Addressables group membership review
- explicit distinction between production, sandbox, editor-only, and archival content

Without that, deletion advice is not evidence-based.

---

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | None. Documentation-only rewrite. |
| GC | None. Documentation-only rewrite. |
| Memory | None. Documentation-only rewrite. |
| Cadence | None. Runtime code unchanged. |
| Correctness | Improved by removing unsupported deletion recommendations. |

---

## Verdict

The prior dead-asset sweep was selective and overconfident.
This pass only confirms presence, not liveness.
All deletion conclusions remain `PENDING VERIFICATION`.
