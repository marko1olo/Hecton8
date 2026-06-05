# Rationale_3104

Date: 2026-06-05
Evidence state: STATIC VERIFIED / STATIC_IMAGE_QA / no Unity acceptance

## Decisions

1. Preserve full useful texture sources.
   - Reason: user correction and `GEMINI_TEXTURE_VISUAL_REVIEW_AND_LOCAL_PBR_20260605.md` state the Gemini mark is final-cleanup debt, not prototype blocker.
   - Consequence: `TX_B31_WetBasaltShoreline_1429_FullSourcePrototype` remains valid for prototype material work outside `Assets`.

2. Do not promote any Gemini or Batch31 LocalPBR source to production.
   - Reason: sources still have watermark/seam/repeat/baked-light/PBR separation debt.
   - Consequence: classification is `SOURCE/PROTOTYPE ONLY` until PBR roles, import settings, and route screenshots exist.

3. Reject `terrain.mat` and `Mat_TriplanarRock.mat` as active recovery routes.
   - Reason: static YAML contains missing shader/texture GUIDs and empty PBR slots.
   - Consequence: do not raw-patch GUIDs. Unity owner must replace scene/material assignment with valid first-party route materials.

4. Classify `Mat_Terrain.mat` + `TerrainMaster.shader` as valid static terrain route candidates, not acceptance proof.
   - Reason: shader exists, uses SRP-batcher CBUFFER, texture arrays/control blending, and continuous `_HectonMathLodWeight`.
   - Missing: Unity readback, scene binding, texture role proof, compact/high screenshots.

5. Classify `H8_PhoticTerrainLit_1453.shader`, `MAT_H8_HeroWetBasaltRock_1453.mat`, and `MAT_H8_AuthoredWetBasaltBreakup_1465.mat` as valid prototype/first-party material candidates with incomplete PBR stack.
   - Reason: assets exist and use first-party route naming; some bind wet basalt albedo/normal.
   - Missing: packed MRAO/wetness/salt/contact masks, detail maps, material-channel manifest, Unity visual proof.

6. Texture-only recovery is rejected.
   - Reason: screenshot metadata identifies black primitive foreground boulders and rejected photic rock garden objects; `terrain.md` and `3DMODEL_GEOLOGY_ROCKS.md` require route-readable geology, LOD, and collision proxy proof.
   - Consequence: replacement requirements include silhouette, strata, waterline erosion, sediment shelves, LOD0/1/2 or HLOD, and separate collider proxy.

## Regression Model

CPU/GC: no runtime code changed; no GC claim.

Memory/VRAM: no import changed; texture residency unchanged. Future import must respect 900 MB compact texture budget, BC7 albedo/MRAO, BC5 normals, mips, and streaming.

Cadence: no runtime cadence changed. Future upload/import work must obey async upload tier budget and avoid per-frame setting changes.

Correctness: no gameplay truth changed. Future material quality must not change terrain ownership, collision identity, save identity, or route truth.

Visual risk: highest current risk is leaving black primitive geometry while swapping textures. That fails route taste even if material sources improve.

## Scalability Consequences

Compact: preserve bright readable shoreline, wet basalt identity, shell/sand scale, contact foam, route silhouettes, packed masks, shared materials, HLOD/proxy geometry.

Middle: add stronger roughness/AO/wetness variation, local shell/calcite breakup, controlled decals, same material semantics.

High: add richer normals, denser near-field geology, stronger foam/contact breakup, longer LOD residency.

Ultra: add hero-only 2048/4096 source bakes, dense decal layers, sharper close-waterline material witnesses, reflections/caustic support after compact proof.
