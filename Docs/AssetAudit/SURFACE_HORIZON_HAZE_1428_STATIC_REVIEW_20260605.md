# Surface Horizon Haze 1428 Static Review - 2026-06-05

Status: `STATIC_REVIEW_ONLY / REJECTED_AS_PROOF`
Evidence class: `STATIC_SOURCE_READBACK + DIAGNOSTIC_SCREENSHOT_REVIEW`

No Unity run, Play Mode, import, shader compile, material save, scene save, prefab save, `.meta` edit, or `Assets` mutation was performed.

## Scope

Reviewed:

- `Assets/_Project/Art/Shaders/H8_SurfaceHorizonHaze_1428.shader`
- `Assets/_Project/Art/Shaders/H8_UnderwaterHorizonHaze_1437.shader`
- `Assets/_Project/Art/Materials/World/MAT_SurfaceHorizonSaltHaze_1428.mat`
- `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_UnderwaterHorizonHaze_1437.mat`
- `Assets/_Project/Art/Meshes/Generated/MESH_SurfaceHorizonHaze_1428.asset`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- Latest diagnostic surface screenshots under `Docs/Reports/McpScreenshots/`

## Mandates Followed

- `AGENTS.md`
- `TASTE.md`
- `shaders.md`
- `rendering.md`
- `water.md`
- `terrain.md`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## Findings

`H8_SurfaceHorizonHaze_1428.shader` and `H8_SurfaceHorizonHaze_1428.shader.meta` are both untracked. The meta GUID is `3b69f27b08093544e9470eef7d7106ef`, and targeted text search found no serialized material, scene, prefab, or asset references to that GUID. It is not a stable route asset and cannot be treated as route proof.

The shader is transparent, unlit, `Queue=Transparent+40`, `ZWrite Off`, and `ZTest Always`. This can visually cover a horizon seam regardless of depth. It is acceptable only as a proven presentation approximation; it is not evidence that ocean, coastline, terrain, or sky material truth was fixed.

`SURFACE_HORIZON_SALT_HAZE_1428` in `02_HECTON_WORLD.unity` is serialized with `m_IsActive: 0`. Its renderer is enabled, but the GameObject is inactive. Static scene data therefore says this is candidate/reachable data, not active proof.

`MAT_SurfaceHorizonSaltHaze_1428.mat` uses transparent URP material settings, has empty `_BaseMap` and `_MainTex`, and was already flagged by older primitive/null/default validators. It cannot be promoted as product-face or surface visual proof without Unity material readback and route screenshots.

`H8_UnderwaterHorizonHaze_1437` route is also static-only here: the scene object `H8_UnderwaterHorizonHaze_1437` is inactive and its renderer disabled in static YAML.

The newest diagnostic screenshot `UnityMcp_MainCamera_20260605_surface_ocean_no_clip_ab3.png` improves surface ripple texture slightly, but the dark horizon band, over-turquoise slab water, primitive black/yellow shoreline blobs, and weak Aegir integration remain. It is rejection evidence, not acceptance proof.

Additional post-review diagnostic screenshots inspected:

- `Docs/Reports/McpScreenshots/UnityMcp_MainCamera_20260605_surface_underwater_renderer_off_probe.png`
- `Docs/Reports/McpScreenshots/UnityMcp_MainCamera_20260605_surface_ocean_only_no_albedo_transparency_probe.png`
- `Docs/Reports/McpScreenshots/UnityMcp_MainCamera_20260605_surface_haze_ocean_ab5.png`
- `Docs/Reports/McpScreenshots/UnityMcp_MainCamera_20260605_surface_horizon_shader_ab4.png`

Verdict remains rejected:

- `surface_underwater_renderer_off_probe` and `surface_ocean_only_no_albedo_transparency_probe` still show a black horizon strip, acid/mint water plane, detached island blobs, and no believable waterline contact.
- `surface_haze_ocean_ab5` demonstrates the risk of `ZTest Always` haze: it softens the seam by turning the shoreline into green milk. That is camouflage, not premium approximation.
- `surface_horizon_shader_ab4` adds some water contrast, but the horizon seam, shoreline material failure, weak contact logic, and shallow water-volume failure remain.
- None of these screenshots can support h8_1475, surface route acceptance, Crest/ocean proof, terrain proof, or product-face promotion.

## Required Owner Decision

If a horizon haze approximation remains:

- keep it as a presentation overlay only;
- give it a stable imported shader asset and tracked `.meta`;
- bind it through an owned material route, not an ad hoc untracked shader;
- prove it does not hide geometry/terrain/water failures;
- prove SRP Batcher, transparent overdraw cost, render queue, and Frame Debugger placement;
- capture compact and high screenshots against the mandatory visual references.

Do not use `ZTest Always` haze as a cover for a broken horizon plane, flat water, primitive shoreline, weak terrain, or smeared Aegir.

## Low / Middle / High / Ultra Consequences

Low/compact:

- Horizon haze may be cheap, but compact still needs readable water volume, terrain silhouette, and sky integration. A transparent seam cover is not enough.

Middle:

- Haze must sit behind proven Crest/ocean, terrain, and sky ownership. It cannot be the only thing making the surface route look coherent.

High:

- Extra budget should buy real shoreline contact, better water response, and stronger cloud/Aegir material detail before more overlay haze.

Ultra:

- Visual overkill may layer atmospheric horizon effects only after base water/terrain/sky proof passes. It cannot create a second visual truth route.

## Regression Model

- CPU: static review only. No runtime cost claim.
- GPU: transparent `ZTest Always` overlay is suspicious until Frame Debugger/profiler proof exists.
- GC: no runtime code changed; no `0 B/frame` claim.
- Memory: untracked shader has no import/residency proof.
- Correctness: static route remains `PENDING_VERIFICATION`; raw screenshots remain diagnostic rejection evidence only.

Final status: `STATIC_REVIEW_ONLY / REJECTED_AS_PROOF`.
