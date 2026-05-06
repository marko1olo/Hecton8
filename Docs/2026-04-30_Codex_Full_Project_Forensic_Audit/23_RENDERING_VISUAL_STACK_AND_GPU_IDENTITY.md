# 23 Rendering Visual Stack And GPU Identity

Date: 2026-05-07
Status: PENDING VERIFICATION

Mandates followed:
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

Purpose:
- Audit whether HECTON-8 visuals are mostly stock Unity/URP dressing or a genuinely custom rendering stack.
- Judge shader, compute, Visor, RT, and GPU-feature breadth against the project's declared hardware discipline.

## 1. Visual surface weight

Static snapshot:

| Surface | Count |
|---|---:|
| `.shader` files | 62 |
| `.compute` files | 22 |
| `.shadergraph` files | 3 |
| `.mat` files | 168 |
| `Assets/_Project/Shaders` non-meta files | 2 |
| `Assets/_Project/Materials` non-meta files | 30 |
| `Assets/_Project/Art/Materials` non-meta files | 127 |

Related code weight:

| Domain | Files | Lines |
|---|---:|---:|
| `Scripts/VFX` | 5 | 2,132 |
| `Scripts/Visor` | 19 | 8,206 |
| `Scripts/Optimization` | 22 | 4,294 |
| `Scripts/World` | 108 | 64,476 |

Interpretation:
- The visual stack is not small.
- More importantly, it is not centered in one shader folder. It is spread across runtime owners, Visor features, world simulation, and RT governance.

## 2. HECTON-8 already has a custom rendering identity

Evidence:
- `Visor` contains heavy first-party renderer features and post systems:
  - `VisorHUDController.cs` (`1,472` lines)
  - `SpectrumSystem.cs` (`975`)
  - `HectonScooterVolumetricShaftsFeature.cs` (`670`)
  - `VolumetricLightFeature.cs` (`354`)
  - `HectonAbyssalSsdoFeature.cs` (`353`)
  - `HectonBiolumSSGIFeature.cs` (`345`)
  - `CausticsProjectorManager.cs` (`359`)
- Multiple features load dedicated compute shaders from `Assets/_Project/Art/Shaders/...`.
- `World` code also owns rendering-heavy systems such as indirect vegetation, HLOD, sediment accumulation, cut-mask volumes, damping masks, and boid/VAT surfaces.

What is genuinely true:
- This is not "URP plus a few materials."
- HECTON-8 already behaves like a project with its own render platform layered on top of URP.

Verdict:
- Visual-system implementation reality: extremely high.
- Render-stack uniqueness: very high.

## 3. Visor is one of the projectâ€™s strongest and riskiest domains

Evidence:
- `HectonBiolumSSGIFeature.cs:16-18` is a real `ScriptableRendererFeature` with a dedicated compute path.
- `VolumetricLightFeature.cs:18-20` is another real feature with its own compute asset.
- `HectonScooterVolumetricShaftsFeature.cs:19` is large and parameter-dense, with a huge set of shader property IDs (`651-698`).
- `CausticsProjectorManager.cs:22` is an active runtime owner, not a dead authoring shell.

What is genuinely good:
- The project is trying to own its underwater visual language technically, not just aesthetically.
- Volumetrics, biolum, SSDO, sonar point clouds, flashlight voxel shadows, and caustics are all real named systems.

What is bad:
- This is a lot of custom visual technology for the declared target hardware.
- The more the projectâ€™s mood depends on custom render features, the more expensive every regression becomes.
- Visor is no longer a cosmetic layer. It is a major engineering surface.

Verdict:
- Visual ambition: extremely high.
- Perf/regression exposure: extremely high.

## 4. RenderTexture governance is unusually mature

Evidence:
- `RenderTextureLifecycleTracker.cs:14` is a real leak/ownership tracker with category queries and audit output.
- `RenderTexturePool.cs` owns pooled RT reuse by format and size hash.
- Dedicated RT managers exist for camera, postFX, visor, and UI memory tracking.
- `CrashTelemetryBuffer.cs:141-168`, `413-417` captures frame timing and profiler recorder data alongside a binary telemetry ring.

What is genuinely good:
- The project is not treating RT usage as invisible implementation detail.
- It has explicit lifecycle tracking, pooling, and budget awareness.
- This is stronger than most repos of comparable apparent maturity.

What is bad:
- The existence of so much RT governance is also proof that the render surface is heavy enough to require defensive infrastructure.
- Complexity is not only in effects. It is also in keeping effect memory alive without collapsing.

Verdict:
- Render-memory governance reality: very high.

## 5. World rendering is deeply GPU-facing

Evidence:
- `HectonIndirectVegetationRenderer` owns compute-driven culling, depth pyramid RTs, and indirect vegetation runtime.
- `FloraInteractionManager` owns wake-trail compute textures and vegetation fog/globals.
- `SargassumCutManager`, `SargassumCrestDampingController`, and `SedimentAccumulationManager` all own persistent RT/compute resources.
- `SargassumMicroFaunaBoids.cs` exposes a huge shader/GPU parameter surface including VAT, cut masks, density textures, threat grids, and spatial grids.

What this means:
- The world is not only CPU/Burst-heavy.
- It also has a meaningful GPU simulation and presentation layer.

Verdict:
- GPU-world integration reality: extremely high.
- Simplicity: low.

## 6. The strongest praise

HECTON-8 has a real visual engineering identity.

It is not visually generic.
It is not shadergraph-only.
It is not post-stack-only.

The project has built technical mechanisms for:
- noir fog handling
- underwater shafts
- biolum SSGI
- custom SSDO
- sonar point clouds
- vegetation GPU interaction
- compute-driven world masks
- RT lifecycle governance

That is serious work.

## 7. The hardest criticism

The same evidence also says this:

- the visual stack is expensive to maintain
- the Visor domain is a major risk concentrator
- the RT/compute surface is already large
- this projectâ€™s visual identity depends on custom systems that can easily outrun the MX350 target if discipline slips

This is not a repo with weak visuals.
It is a repo whose visuals are strong enough to become one of the main delivery risks.
