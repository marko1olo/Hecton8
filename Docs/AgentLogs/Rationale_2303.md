# Rationale 2303

Status: STATIC DESIGN DECISIONS - PENDING UNITY/PROFILER VERIFICATION

## Decisions

1. Do not activate all disabled foam helpers.
   Reason: Batch22 and 1473 screenshots show disabled authored routes include visible failures. `h8_1473_rt_foam_organic_only.png` reads as a hard rectangular wedge/grid sheet. `h8_1473_rt_foam_lace_only.png` reads as a flat pixel-grid water sheet. That violates shoreline foam taste.

2. Treat Crest material/sim as the first safe foam proof path, not accepted visual proof.
   Evidence: `MAT_H8_SurfaceCrestOcean_1428.mat` has `_FOAM_ON`, `_Foam: 1`, `_FoamTexture`, `_FoamScale: 0.019`, and foam colors populated. Scene has `H8_CREST_FOAM_INPUT_PASS_1464` active. `h8_1473_mainrt_crest_foam_shoreline.png` still shows weak/absent contact foam at the shoreline, so runtime Crest pass/debug proof is required before acceptance.

3. Keep authored mesh foam as one-at-a-time rollback tests only.
   Reason: scene YAML has many disabled helpers (`H8_VisibleWaveFoam_1438`, `H8_SurfaceFoamLace_1453`, `H8_VisibleFoamUnlit_1436`, `H8_VisibleBrokenFoam_1435`, `SURFACE_FOAM_RIBBON_1428_2`, `H8_SurfaceFoamTopOnly_1458_*`). The screenshots prove some render as primitive slabs. Unity owner must test one route, capture, then rollback if rectangular/pixel-grid artifacts appear.

4. Use `H8_FloorCausticSoft_1443` as first caustic receiver check.
   Evidence: scene YAML shows `H8_FloorCausticSoft_1443` active. Material exists and uses `H8_FloorCausticSoft_1443.shader` with `_ScaleA: 0.62`, `_ScaleB: 0.98`, `_Sharpness: 5.8`. This is the least invasive caustic path because it is already serialized active.

5. Treat deferred caustics as active feature but unproven runtime owner.
   Evidence: `HectonDeferredCausticsFeature` is active in `PC_Renderer`, `PC_High_Renderer`, `Mobile_Renderer`, and `Quest_VR_Renderer`, but the feature early-outs unless `AbyssalDeferredCausticsRuntime.TryGetActiveConstantBuffer` succeeds. Batch22 found no serialized runtime GUID hit in searched scene/prefab/data assets. Static feature presence is not runtime light.

6. Legacy caustic services remain reject/deferred.
   Evidence: Batch22 found `CausticsProjectorManager` and `AnalyticalCausticsService` disabled by code. Re-enabling legacy projector/analytical routes risks duplicate authority and cheap decal spam.

7. Low/Middle/High/Ultra behavior is continuous presentation scaling, not binary switches.
   Consequence: minimum/low keeps subtle visible contact foam and sparse caustic receiver cues; middle adds Crest/deferred proof; high/ultra increase breakup, receiver richness, and light-lace density without changing gameplay truth.

## Risk

WARNING: Regression risk in visual activation is high if a Unity owner edits scene YAML blindly. Static evidence proves object/material existence, not camera visibility or taste acceptance.
