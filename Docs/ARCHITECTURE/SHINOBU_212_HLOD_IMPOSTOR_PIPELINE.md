# SHINOBU_212 HLOD Impostor Pipeline <!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R47 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Current root/architecture boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md` as STATIC_DOC/STATIC_SOURCE/FILESYSTEM/PY_TOOL evidence. R46 remains the prior interior-authority/route-field/proof-language correction; R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; runtime proof remains absent.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-20 R47): `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md` is the latest local static root/architecture authority-spine, runtime-wording, and counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->  Domain: offline Editor-only HLOD impostor generation for giant far-horizon structures.  Runtime contract: - Runtime does not capture objects into RenderTextures. - Runtime consumes baked albedo-depth atlas, normal-XY atlas, quad mesh, material, and `HectonOctahedralImpostorData`. - Missing baked mesh/material fails closed; the renderer no longer creates runtime fallback mesh/material assets. - Visual LOD choice remains presentation-owned and excluded from rollback gameplay truth. - AUP-safe CPU conversion is available through `OctahedralImpostorInstance.CreateCameraRelative`. - Floating-origin offset comes from `HectonFloatingOrigin.CurrentTotalOffset`, not a terrain/MapMagic bridge. - Renderer owns no persistent private `NativeArray`; upload uses caller-provided `NativeArray` or direct `GraphicsBuffer.LockBufferForWrite`. - Renderer time comes from dispatcher delta accumulation, not Unity `Time.*`. - Active and legacy impostor shaders keep renderer-written scalar/vector material fields inside `UnityPerMaterial`; static atlas material metadata refresh is dirty-gated.  Bake path: - `CalculateCaptureAnglesJob` builds 8/16/32+ capture views with raw unmanaged fields, `[NoAlias]`, synchronous Burst fast math, and pointer iteration. - Editor camera captures albedo and normal/depth to RenderTextures; `RenderWithShader` remains editor-only and runtime scanners flag it if it appears in gameplay render directories. - `PackImpostorAtlas.compute` packs albedo RGB plus depth alpha and normal XY into atlas tiles. - `DilateImpostorEdges.compute` expands valid depth pixels into empty borders to protect mips. - `AsyncGPUReadback` and `ImageConversion.EncodeNativeArrayToPNG` serialize atlases without `ReadPixels` or managed byte arrays. - Import settings force mipmaps and Standalone BC7 compression.  Quality scaling: - GlobalQualityWeight continuously drives swap distance, mock detail, shader interpolation, and culling adapter tier. - Below q=0.22 the active and legacy shaders sample only one atlas view; q=0.22..0.55 restores interpolation through smoothstep; higher tiers regain two-view parallax. - Low: fewer views, 2048 atlas, earlier swap distance. - Middle: 16 views, 4096 atlas. - High: 16 to 32 views, longer real-geometry residency. - Ultra: 32 views and 8192 atlas when Tech Art accepts VRAM warnings.  Audit: `Docs/Reports/SHINOBU_212_SELF_AUDIT.xml` is the current SHINOBU forensic artifact; Unity import/profiler proof remains pending CPU gate.

## 2026-05-20 Loop 10 Renderer Rebind Boundary

- Runtime draw fails closed when `HectonOctahedralImpostorData`, albedo-depth atlas, or normal-depth atlas is missing; SHINOBU does not draw with stale material payload.
- `_argsMesh` and `_lastArgsInstanceCount` are invalidated whenever the indirect args buffer is created or released, so a fresh `GraphicsBuffer` cannot skip its first args write.
- Indirect args writes are lock/unlock guarded with `finally`; resource release resets counters, visible-stream state, bounds override, and static payload validity.
- `HlodImpostorStaticValidators.ScanBillboardAssets` uses a distinct `paths` array so the `StringBuilder files` output route is not shadowed.

## 2026-05-20 Loop 11 NaN / Payload Boundary

- DTO creation, capture angle jobs, mock capture generation, compute atlas packing, compute dilation, shared HLSL vertex helpers, and both impostor shaders now replace NaN/Infinity with finite local defaults before matrices, atlas pixels, lighting, fog, or `SV_Depth` are produced.
- Invalid centers collapse to local zero, invalid sizes collapse to at least 0.5m, invalid quality resolves to the minimum-survival scalar, invalid atlas depth resolves to empty occupancy, and invalid normals resolve to an up-vector fallback.
- The low-quality shader sample collapse remains intact: q below 0.22 keeps one atlas view, q 0.22..0.55 restores interpolation continuously, and higher quality keeps the richer two-view Dear Lie.

## 2026-05-20 Loop 12 Reversed-Z Depth Bias Boundary

- `Hecton_HLOD_Impostor.shader` and `Hecton_OctahedralImpostor.shader` now add depth bias under `UNITY_REVERSED_Z`; the previous subtract path contradicted the render mandate.
- The impostor still writes finite `SV_Depth` from captured atlas alpha, preserving the Dear Lie depth interaction with fog, DoF, and occlusion without re-rendering source geometry.
- Static proof is limited to source scan; Unity import, Frame Debugger, and profiler proof remain pending the project CPU gate.

## 2026-05-20 Loop 13 Binary Tier Residue Boundary

- `HectonChunkImpostorResidency` no longer exposes unused tier-based helper APIs. The retained flag resolver consumes a continuous `globalQualityWeight` float.
- `FlagLowTierSnap` was renamed to `FlagSurvivalSnap`; `WorldChunkResidencyManager` was adjusted only at the SHINOBU HLOD payload flag write site.
- This is API-surface hardening, not a streaming-system rewrite. Runtime proof remains pending CPU-gated Unity import/profiler work.

## 2026-05-20 Loop 14 Branchless Quality Curve Boundary

- `ResolveContinuousEnterDistanceMeters` now evaluates survival-to-middle and middle-to-overkill distances and blends them with `math.smoothstep(0.45, 0.55, q)`.
- The helper no longer branches on `q < 0.5`, preserving the continuous quality law mechanically inside the HLOD swap-distance math.
- Unity import/profiler proof remains pending the project CPU gate.
