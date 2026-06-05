# Generated Assets Support Corpus Audit - 2026-06-05

Status: `STATIC_DOC_AUDIT / PATCH_QUEUE_READY`.
Evidence class: `FILESYSTEM` / `STATIC_DOC` / `STATIC_SOURCE`.
Current front: documentation completeness and asset-support actuality.
First-20 route impact: removes ambiguity around surface/photic texture source candidates, Aegir/cloud sources, foam/contact masks, shoreline materials, and future import handoff proof.

This report does not prove Unity import, Addressables residency, material binding, visual acceptance, runtime texture memory, frame time, GC, or platform readiness.

## Mandates Followed

- `QA_Evidence_Text_Filter_Audit`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory`
- `TOOL_Procedural_Wreckage_Generator`

## Commands

```powershell
rg --files 'Docs/GeneratedAssets' | Measure-Object
rg --files 'Docs/GeneratedAssets' | Group-Object { [IO.Path]::GetExtension($_).ToLowerInvariant() }
rg --files 'Docs/GeneratedAssets' -g 'README*.md' -g '*MANIFEST*.md' -g '*INDEX*.md'
Test-Path 'Docs/GeneratedAssets/README.md'
Test-Path 'Docs/GeneratedAssets/AssetSystem_20260605/README.md'
Test-Path 'Docs/GeneratedAssets/Gemini/README.md'
Test-Path 'Docs/GeneratedAssets/Batch31_LocalPBR/README.md'
rg -n "GeneratedAssets" Docs/README.md Docs/ROOT_DOCS_REFERENCE.md Docs/DOC_GOVERNANCE.md PROJECT_BIBLES.md PROCEDURAL_ASSET_PIPELINE.md 3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md 3DMODEL_TEXTURES_MATERIALS.md rendering.md
```

## Static Inventory

- Total files under `Docs/GeneratedAssets`: `122`.
- Extension split: `.png` `68`, `.md` `37`, `.csv` `8`, `.json` `5`, `.py` `2`, `.jpg` `1`, `.txt` `1`.
- Local index/manifest surfaces found: `15`.
- Top folders: `AssetSystem_20260605`, `Batch31_LocalPBR`, `Gemini`.
- Missing local entry points:
  - `Docs/GeneratedAssets/README.md`
  - `Docs/GeneratedAssets/AssetSystem_20260605/README.md`
  - `Docs/GeneratedAssets/Gemini/README.md`
  - `Docs/GeneratedAssets/Batch31_LocalPBR/README.md`
- Targeted stable-doc search found no `GeneratedAssets` route in the checked authority/index files.

## What Is Already Correct

- `Docs/GeneratedAssets/AssetSystem_20260605/TEXTURE_AUTHORING_MANIFEST_3212_20260605.md` correctly states `STATIC_SOURCE` / `STATIC_IMAGE_QA` only and denies Unity import, material-slot readback, route screenshot, VRAM, and runtime residency proof.
- `Docs/GeneratedAssets/Batch31_LocalPBR/Batch31_LocalPBR_INDEX.md` correctly states `LOCAL_SOURCE_BAKE_STATIC_ONLY`, no Unity import, no runtime proof, and no visual acceptance.
- `Docs/GeneratedAssets/Gemini/Prompts/1907/README.md` correctly states prompt/source request only, not import manifest, Unity material manifest, texture acceptance report, or visual proof.
- `Docs/GeneratedAssets/Gemini/README_GENERATION_QUEUE_20260604.md` correctly tells operators not to save generated images into `Assets/**` and routes audit through `Tools/GeminiTextureIntakeAudit.py`.

## Gaps

| Priority | Path | Gap | Risk | Required direction |
|---|---|---|---|---|
| P0 | `Docs/GeneratedAssets/README.md` | Missing corpus-level boundary. | Agents can mistake source PNGs/manifests for imported production assets or visual proof. | Add local README: static source/prototype corpus only; no `Assets/**` promotion, Unity import, material binding, Addressables, visual acceptance, runtime, memory, or platform proof. |
| P0 | `Docs/GeneratedAssets/Gemini/README.md` | Missing Gemini subcorpus index. | Prompt/output/QA/refined folders are easy to quote out of context. | Add local README routing prompts, outputs, QA, refined images, and derived candidates to texture-generation/material authority files and proof gates. |
| P1 | `Docs/GeneratedAssets/AssetSystem_20260605/README.md` | Missing local summary over Aegir/cloud, foam/contact, cleanup, and texture authoring manifests. | Operators must read multiple manifests to know source-only state. | Add manifest index and no-import/no-runtime boundary. |
| P1 | `Docs/GeneratedAssets/Batch31_LocalPBR/README.md` | Missing folder README even though an index exists. | `Batch31_LocalPBR_INDEX.md` is good but not the default local entry point. | Add README or rename/index-link boundary that points to the index and states static source-bake only. |
| P1 | `Docs/GeneratedAssets/Gemini/README_GENERATION_QUEUE_20260604.md` | Uses `Evidence class: STATIC VERIFIED`. | `VERIFIED` wording can be overread as proof beyond static queue text. | Downgrade to `STATIC_DOC / OPERATOR_QUEUE`; keep its no-browser/no-Assets boundary. |
| P2 | `Docs/GeneratedAssets/Gemini/README_GENERATION_QUEUE_20260604.md` | Target output `Docs/GeneratedAssets/Gemini/Outputs/Batch22/` is absent by `Test-Path`. | Future operators can write into an unstated new folder or assume the queue ran. | State that Batch22 output folder is expected/future until created by an explicit generation task. |

## Required Stable Routes

Generated asset support docs should route to:

- `PROCEDURAL_ASSET_PIPELINE.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `rendering.md`
- `water.md`
- `terrain.md`
- `Docs/ARCHITECTURE/ARM_TEXTURE_PACKING_PIPELINE.md`
- `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md`

## Rejected Claims

- Runtime texture readiness from `Docs/GeneratedAssets` presence.
- Unity import readiness from PNG, manifest, or prompt existence.
- Material/shader binding from source-bake or Gemini QA docs.
- Addressables residency from generated asset path names.
- Surface/photic visual acceptance from contact sheets or static image QA alone.
- VRAM, frame time, GC, or platform readiness from static image/source files.

## Scalability Consequences

- Low: corpus boundary must prevent compact-lane import of oversized or uncompressed source images without streaming/import proof.
- Middle: source candidates must flow through material/texture packing authority before any route material claim.
- High: higher-resolution or richer source candidates remain authoring surplus until memory and route screenshot proof exists.
- Ultra: visual-overkill source generation is allowed only as source inventory; no direct promotion without import, material, route, and memory proof.

## Regression Model

- CPU: no runtime code changed. Static audit only.
- GC: no runtime code changed. No `0 B/frame` claim.
- Memory: no asset import or residency state changed.
- Cadence: no runtime cadence changed.
- Correctness: documentation gap identified; no source image, material, shader, or Unity scene state changed.

Final status: `PATCH_QUEUE_READY / PENDING_VERIFICATION`.
