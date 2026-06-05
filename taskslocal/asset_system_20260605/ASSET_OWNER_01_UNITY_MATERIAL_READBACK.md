# Asset Owner 01 - Unity Material Readback

Mission: prove or reject active sky, Aegir, water, terrain, and photic material bindings in Unity without raw YAML edits.

Read first:

- `Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.md`
- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`
- `Docs/AssetAudit/TEXTURE_MATERIAL_USAGE_REVIEW_20260605.md`
- `rendering.md`
- `water.md`
- `terrain.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `Docs/Reports/AssetSystem_20260605/MATERIAL_READBACK_PREFLIGHT_STATIC_BLOCKERS_3215_20260605.md`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

Required checks:

- P0 first: `foam.png` active world/ocean reachability despite visual rejection.
- P0 first: four `WorldProceduralProxy` flora/coral/kelp materials serialized in `02_HECTON_WORLD.unity`.
- Read active `Mat_HectonSky.mat` shader slots and scene skybox refs.
- Use the exact scene/material target list from `MATERIAL_READBACK_PREFLIGHT_STATIC_BLOCKERS_3215_20260605.md`; do not rediscover it from scratch unless the file is stale.
- Confirm `_MainCloudTex`, `_HighCloudTex`, `_MainCloudAtlas` actual effective bindings.
- Confirm active Aegir material textures and shader properties.
- Confirm Crest ocean material slots and foam contribution without material clones or wrapper edits.
- Confirm active terrain material route and stale/broken `terrain.mat` / `Mat_TriplanarRock.mat` status.
- Read candidate geology/flora material refs for proxy/placeholder contamination.
- Confirm why visually rejected `foam.png` is still serialized-reachable through active world/ocean users.
- Confirm and plan replacement for the four `WorldProceduralProxy` flora/coral/kelp materials serialized in `02_HECTON_WORLD.unity`.

Proof output:

- Material readback report under `Docs/Reports/Batch31/` or a new dated asset report.
- Screenshot proof packet under `Docs/Screenshots/HectonProofPackets/`.
- Unity Console state.
- Stats or Frame Debugger state.
- Explicit `PENDING`, `REJECTED`, or `CANDIDATE_AFTER_READBACK` disposition per material family.

Hard rejects:

- No raw YAML edits.
- No Crest wrapper/material clone.
- No visual acceptance from Editor inspector only.
- No darkness/fog cover-up for weak surface/shallow art.
