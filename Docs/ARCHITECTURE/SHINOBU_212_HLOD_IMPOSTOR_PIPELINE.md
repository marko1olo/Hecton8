- # SHINOBU_212 HLOD Impostor Pipeline
Status: `STATIC_ROUTE_DOC / RUNTIME_PROOF_PENDING`.
Evidence class: `STATIC_DOC` / `STATIC_SOURCE`.
Owner domain: offline Editor-only HLOD impostor generation.
Review disposition: `YELLOW / STATIC_DOC_ONLY` until compile/import/runtime/profiler/player proof exists.

Domain: offline Editor-only HLOD impostor generation for giant far-horizon structures.  Runtime contract:
- Runtime does not capture objects into RenderTextures.
- Runtime consumes baked albedo-depth atlas, normal-XY atlas, quad mesh, material, and `HectonOctahedralImpostorData`.
- Missing baked mesh/material fails closed; the renderer no longer creates runtime fallback mesh/material assets.
- Visual LOD choice remains presentation-owned and excluded from rollback gameplay truth.
- AUP-safe CPU conversion is available through `OctahedralImpostorInstance.CreateCameraRelative`.
- Floating-origin offset comes from `HectonFloatingOrigin.CurrentTotalOffset`, not a terrain/MapMagic bridge.
- Renderer owns no persistent private `NativeArray`; upload uses caller-provided `NativeArray` or direct `GraphicsBuffer.LockBufferForWrite`.
- Renderer time comes from dispatcher delta accumulation, not Unity `Time.*`.
- Active and legacy impostor shaders keep renderer-written scalar/vector material fields inside `UnityPerMaterial`; static atlas material metadata refresh is dirty-gated.  Bake path:
- `CalculateCaptureAnglesJob` builds 8/16/32+ capture views with raw unmanaged fields, `[NoAlias]`, synchronous Burst fast math, and pointer iteration.
- Editor camera captures albedo and normal/depth to RenderTextures; `RenderWithShader` remains editor-only and runtime scanners flag it if it appears in gameplay render directories.
- `PackImpostorAtlas.compute` packs albedo RGB plus depth alpha and normal XY into atlas tiles.
- `DilateImpostorEdges.compute` expands valid depth pixels into empty borders to protect mips.
- `AsyncGPUReadback` and `ImageConversion.EncodeNativeArrayToPNG` serialize atlases without `ReadPixels` or managed byte arrays.
- Import settings force mipmaps and Standalone BC7 compression.
Quality scaling:
- GlobalQualityWeight continuously drives swap distance, mock detail, shader interpolation, and culling adapter output.
- q < 0.22 samples one atlas view.
- q=0.22..0.55 restores interpolation through smoothstep.
- q > 0.55 regains two-view parallax.
- Capture view count, atlas size, and real-geometry residency interpolate by authored budget curve; endpoint labels are authoring presets, not runtime hardware branches.
Audit: `Docs/Reports/SHINOBU_212_SELF_AUDIT.xml` is the current SHINOBU forensic artifact; Unity import/profiler proof remains pending CPU gate.

## 2026-05-20 Loop 10 Renderer Rebind Boundary

- Runtime draw fails closed when `HectonOctahedralImpostorData`, albedo-depth atlas, or normal-depth atlas is missing; SHINOBU does not draw with stale material payload.

- `_argsMesh` and `_lastArgsInstanceCount` are invalidated whenever the indirect args buffer is created or released, so a fresh `GraphicsBuffer` cannot skip its first args write.

- Indirect args writes are lock/unlock guarded with `finally`; resource release resets counters, visible-stream state, bounds override, and static payload validity.

- `HlodImpostorStaticValidators.ScanBillboardAssets` uses a distinct `paths` array so the `StringBuilder files` output route is not shadowed.

## 2026-05-20 Loop 11 NaN / Payload Boundary

- DTO creation, capture angle jobs, mock capture, atlas packing, dilation, HLSL helpers, and impostor shaders sanitize NaN/Infinity.
- Replacement happens before matrices, atlas pixels, lighting, fog, or `SV_Depth`.

- Invalid centers collapse to local zero.
- Invalid sizes collapse to at least `0.5m`.
- Invalid quality resolves to minimum-survival scalar.
- Invalid atlas depth resolves to empty occupancy.
- Invalid normals resolve to up-vector fallback.

- The low-quality shader sample collapse remains intact: q below 0.22 keeps one atlas view, q 0.22..0.55 restores interpolation continuously, and higher quality keeps the two-view Dear Lie.

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
