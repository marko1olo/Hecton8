# Manual Review Pass 9 - Visor RenderFeature Material Lifecycle And Shader Fallbacks

Status: STATIC METHOD REVIEW - NO FRAME DEBUGGER / GPU PROFILER PROOF
Date: 2026-06-02

## Reviewed Files

- `Assets/_Project/Scripts/Visor/HectonBiolumSSGIFeature.cs`
- `Assets/_Project/Scripts/Visor/HectonScooterVolumetricShaftsFeature.cs`
- `Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs`
- `Assets/_Project/Scripts/Visor/VolumetricLightFeature.cs`
- `Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs`

## Findings

### 1. RenderGraph Entry Points Exist, But They Are Not Runtime Proof

The reviewed visor features use `RecordRenderGraph`: Biolum SSGI at `HectonBiolumSSGIFeature.cs:197`, scooter volumetric shafts at `HectonScooterVolumetricShaftsFeature.cs:340`, sonar point cloud at `HectonSonarPointCloudFeature.cs:153`, volumetric light at `VolumetricLightFeature.cs:326`, and particulate fog at `HectonVolumetricParticulateFogFeature.cs:746`. This is structurally aligned with the Unity 6000 RenderGraph bible. It does not prove GPU cost, resource lifetime, render pass ordering, or compact-lane visual quality.

Classification: `YELLOW_RENDERGRAPH_STRUCTURE_PRESENT_GPU_PROOF_MISSING`. These files are not a structural RenderGraph failure from static review, but they remain blocked by Frame Debugger, RenderGraph Viewer, GPU profiler, and compact/high visual captures.

### 2. Create-Time Materials Are Cold Only If Create/HotSwap Cadence Is Proven

Biolum SSGI creates/recreates a composite material in `Create()` through `RecreateMaterial(ref _compositeMaterial, compositeShader)` at `HectonBiolumSSGIFeature.cs:577`, with `CoreUtils.CreateEngineMaterial(shader)` at `:612-625`. Scooter volumetric shafts recreate four materials at `HectonScooterVolumetricShaftsFeature.cs:1491-1494`, then creates materials inside `RecreateMaterial()` at `:1613-1626`. Sonar point cloud recreates its material at `HectonSonarPointCloudFeature.cs:592` and `:756-769`. Volumetric light recreates the proxy material at `VolumetricLightFeature.cs:785` and `:987-1000`. Particulate fog creates a Dear Lie proxy material through `CoreUtils.CreateEngineMaterial(shader)` at `HectonVolumetricParticulateFogFeature.cs:2116-2119`.

Static interpretation: `Create()` material creation can be legal if feature construction happens once per renderer/quality load, not during gameplay cadence. It is not green without lifecycle proof because several features also register hot-swap listeners.

Classification: `YELLOW_RENDER_FEATURE_MATERIAL_LIFECYCLE_PROOF_REQUIRED`.

### 3. Dev Shader.Find Fallbacks Are Guarded, But Production Shader Assignment Still Needs Proof

Biolum SSGI uses `Shader.Find("Hidden/Hecton8/BiolumSSGIComposite")` under `#if UNITY_EDITOR || DEVELOPMENT_BUILD` at `HectonBiolumSSGIFeature.cs:568-570`. Scooter shafts use `Shader.Find("Hidden/Hecton8/ScooterVolumetricShafts")` under the same guard at `HectonScooterVolumetricShaftsFeature.cs:1484-1486`. Sonar point cloud uses `Shader.Find("Hidden/Hecton8/SonarGridOverlay")` under guard at `HectonSonarPointCloudFeature.cs:584-586`. Volumetric light uses `Shader.Find("Hidden/Hecton8/VolumetricLightProxy")` under guard at `VolumetricLightFeature.cs:781-783`. Particulate fog has a Dear Lie proxy shader fallback at `HectonVolumetricParticulateFogFeature.cs:2098`.

The guarded `Shader.Find` route is acceptable as editor/development recovery only. Release builds still need assigned shader assets, variant collection proof, and no hidden runtime material fallback.

Classification: `YELLOW_SHADER_ASSIGNMENT_AND_VARIANT_PROOF_REQUIRED`.

### 4. GlobalRegistry HotSwap Is Correct Shape, But It Needs Event Count Proof

Scooter shafts, sonar point cloud, volumetric light, and particulate fog implement `IGlobalRegistryHotSwapListener` and register/unregister through `GlobalRegistry.TryRegisterHotSwapListener` or `TryUnregisterHotSwapListener`. Examples: `HectonScooterVolumetricShaftsFeature.cs:1596-1610`, `HectonSonarPointCloudFeature.cs:705-718`, `VolumetricLightFeature.cs:953-966`, and `HectonVolumetricParticulateFogFeature.cs:2797-2810`.

This is preferable to scene search and keeps dependency updates routed through global authority. The missing proof is cadence: hot-swap must be rare bootstrap/service replacement, not a normal visual quality transition that recreates materials or resources during gameplay.

Classification: `YELLOW_HOTSWAP_CADENCE_AND_RESOURCE_RECREATE_PROOF_REQUIRED`.

## Blocker Change From Pass 9

- Add `RB-125`: Visor RenderFeature material/shader/hot-swap lifecycle proof. This is a P1 proof gate, not a confirmed P0 runtime bug.

## Current Honest Verdict

The visor feature files are better than generic legacy URP code because they use `RecordRenderGraph` and guarded development shader lookup. The risk is not "bad architecture"; the risk is false green reporting. RenderFeature material creation, shader fallback, hot-swap registration, and proxy paths need proof artifacts before the rendering bible can be marked satisfied.

