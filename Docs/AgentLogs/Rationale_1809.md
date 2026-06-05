# Rationale 1809

## Decisions

- Selected mandates: `REND_Instanced_Flora_Physics`, `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits`, `TOOL_Designer_Facades_CSV_Binary_Bridge`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory`, `REND_VFX_Fluid_Aesthetics_Compute_Particles`, `REND_URP_Graphics_HotPath_Optimization_HLOD`, `QA_Evidence_Text_Filter_Audit`.
  Reason: the deliverable is a static flora/fauna placement manifest with density scaling, visual-fake discipline, CSV bridge shape, asset residency/proof boundaries, and evidence-label constraints.

- Evidence boundary is STATIC only.
  Reason: task forbids Unity control, scene edits, runtime/profiler claims, and live captures.

- Depth bands are fixed to `0-5 m`, `5-20 m`, `20-45 m`, `45-80 m`, and `80-100 m`.
  Reason: this covers waterline, first descent, core starter reef, deeper photic wall/service route, and lower photic threshold without flattening the whole shallows into one generic band.

- `GEN_` baked flora prefabs are allowed as static candidates only.
  Reason: the baked flora README identifies them as starter fallbacks/source-of-truth routing assets, not proof that the live route already meets the visual floor.

- `family_coral_brittle` and `family_kelp_abyssal` are excluded from the 0-100 m manifest.
  Reason: brittle coral is ruled for 900 m+ and abyssal kelp is a deep family; using either as ordinary photic flora would violate the 0-100 m route lock.

- `H8_WORLD_BIOLUM_FIELD_1428` is treated as pending candidate evidence, not active route proof.
  Reason: scene inspection found it inactive and inspected flora proxy materials do not provide assigned emission maps for navigation biolum.

- Fauna shadow rows are written as static silhouette/hazard-language candidates only.
  Reason: scene names and meshes do not prove AI, perception, swimming, damage, or route behavior.

- Density columns are authoring targets sampled from a continuous `GlobalQualityWeight` curve.
  Reason: the task requires compact/middle/high/ultra consequences, while root rules forbid binary quality switches and fake runtime counts.

- Industrial service scar/debris support is included only as overgrown route context, not final hero proof.
  Reason: 1802 marks debris candidates as weak/layout evidence and rejects primitive/proxy close-up hero use.
