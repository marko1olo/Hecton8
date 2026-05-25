# Platform Portability Proof Audit

Evidence class: STATIC_SOURCE / PACKAGE_LOCK / FILESYSTEM. No Unity import, player build, install, launch, profiler, GC, memory, shader, headset, Deck, macOS, Linux, or console proof was executed.

- Schema: `hecton8.platform_portability_proof_audit.v11`
- Root: `.`

## Package/XR Surface

- Required XR packages in manifest: `yes`
- Required XR packages in lock: `yes`
- Addressables package in manifest: `yes`
- Addressables package in lock: `yes`
- PICO package candidates: `0`

| Package | Manifest | Lock | Manifest Version | Lock Version |
|---|---|---|---|---|
| `com.unity.xr.management` | `yes` | `yes` | `4.6.0` | `4.6.0` |
| `com.unity.xr.openxr` | `yes` | `yes` | `1.17.0` | `1.17.0` |
| `com.unity.xr.meta-openxr` | `yes` | `yes` | `2.5.0` | `2.5.0` |

## Android/XR Settings

- Android application id: `com.danatgames.hecton8`
- Android target SDK: `35`
- Android min SDK: `25`
- Android ARM64-only serialized value: `2` / `yes`
- Android IL2CPP serialized value: `1` / `yes`
- Android sustained performance: `1` / `yes`
- Android graphics API raw: `15000000`, automatic: `0`, Vulkan-only: `yes`
- `m_BuildTargetVRSettings` empty: `yes`
- XR provider serialized proof: `no`
- XR readiness validator present: `yes`
- XR provider route validator present: `yes`
- XR provider route fixer present: `yes`
- Android Quest/XR route repairer present: `yes`

## Quality / Quest URP Wiring

- Quest URP asset: `Assets/_Project/Data/URP_Quest_VR.asset`, present: `yes`, guid: `d9c4cd6a763fec04a913c6a149663003`
- Quality settings count: `3`
- Android default quality index: `1`
- Android default quality render pipeline guid: `0a1617ac2a1aa74409dd0f7176dffe42`
- Quest URP referenced in QualitySettings: `no`
- Quest URP referenced in GraphicsSettings: `no`
- Android default quality uses Quest URP: `no`
- Quest configurator present: `yes`
- Quest configurator reports quality route: `yes`
- Quest configurator can wire Android route: `yes`

## Shader / Compute Static Risk

- Preloaded shader entries: `2`
- ShaderVariantCollection files: `6`
- Bootstrap shader collection field present: `yes`
- Bootstrap explicit `ShaderVariantCollection.WarmUp()` calls: `1`
- Bootstrap `isWarmedUp` reads: `1`
- `Shader.WarmupAllShaders()` call sites in bootstrap: `0`
- Shader source files: `608`
- `shader_feature`/`multi_compile` pragmas: `887`
- `#pragma target >= 4.5`: `108`
- `#pragma target >= 5.0`: `5`
- Compute files: `70`
- Compute reference files scanned: `9308`
- Compute reference files skipped over `2000000` bytes: `39` / bytes `346161212`
- `numthreads` declarations: `151`
- Risky numeric thread groups > `64`: `5`
- Risky numeric thread groups by execution surface: `{'Editor': 2, 'Runtime': 3}`
- Risky numeric thread groups by runtime reachability: `{'EditorOrTestOnly': 4, 'UnreferencedAsset': 1}`
- Runtime asset risky numeric thread groups > `64`: `3`
- Runtime-referenced risky numeric thread groups > `64`: `0`
- Compute target 5.0 files: `5`
- C# compute dispatch calls: `121`; runtime: `117`
- C# compute dispatch caller files: `49`; runtime: `47`
- Dispatch calls without file-level `GetKernelThreadGroupSizes`: `72`; runtime: `68`
- Dispatch caller files without file-level `GetKernelThreadGroupSizes`: `26`; runtime: `24`
- Dispatch calls without thread-group query by execution surface: `{'Editor': 4, 'Runtime': 68}`

## Payload / Build Artifacts

- Addressables data path: `Assets/AddressableAssetsData`, files: `0`
- Addressables settings folder exists: `yes`
- ContentAuthority validator: `Assets/_Project/Scripts/Core/Content/Editor/ContentAuthorityBuildValidators.cs`, present: `yes`
- ContentAuthority prebuild gate: `yes`
- Addressables tier group gate: `yes`
- Content hash map route: `Assets/_Project/Scripts/Core/Content/ContentAssetHashMap.cs`, present: `yes`
- Bootstrap dependency prewarm route: `yes`
- AssetLifecycleGovernor async load route: `yes`
- AssetLifecycleGovernor blind-frame release route: `yes`
- Addressables telemetry dump route: `yes`
- Texture tier Addressables authoring route: `yes`
- Data Monolith path: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`, exists: `no`, bytes: `0`
- Data Monolith compiler: `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs`, present: `yes`
- Data Monolith command-line bake route: `yes`
- Data Monolith prebuild bake/validation gate: `yes`
- Data Monolith output validation route: `yes`
- Data Monolith atomic temp-write/validate route: `yes`
- Data Monolith little-endian guard: `yes`
- Data Monolith production coverage gate: `yes`
- External `.h8bin` validator: `Tools/h8bin_validator.py`, present: `yes`
- Data Monolith source folder: `Assets/_SourceData/DataMonolith`, exists: `no`, files: `0`
- Data Monolith balance folder: `Data/Balance`, exists: `yes`, files: `47`
- Builds path: `Builds`, exists: `no`, files: `0`
- Build result logs: `0`

## Native Plugin Surface

- Plugin files: `24`
- By extension: `{'.dll': 24}`
- By class: `{'editorOrManagedDll': 8, 'managedOrUnknownDll': 14, 'windowsNativeOrManagedDll': 2}`

First-party/runtime-critical candidates:

- `Assets/_Project/Plugins/Windows/x86_64/liblz4.dll`
- `Assets/Plugins/x86_64/HectonAudioKernel.dll`

Risky numeric compute thread groups:

- `Assets/_Project/Art/Shaders/Hecton_SonarMap.compute:59` `8, 8, 8` => `512` threads (`Runtime`, `UnreferencedAsset`, runtime refs `0`)
- `Assets/_Project/Art/Shaders/Hecton_SonarRaymarch.compute:113` `128, 1, 1` => `128` threads (`Runtime`, `EditorOrTestOnly`, runtime refs `0`)
- `Assets/_Project/Art/Shaders/Hecton_SonarRaymarch.compute:186` `128, 1, 1` => `128` threads (`Runtime`, `EditorOrTestOnly`, runtime refs `0`)
- `Assets/Editor/x64/Bakery/shaderSrc/ftCullFarSphere.compute:20` `256, 1, 1` => `256` threads (`Editor`, `EditorOrTestOnly`, runtime refs `0`)
- `Assets/Editor/x64/Bakery/shaderSrc/ftTransformFarSphere.compute:30` `16, 16, 1` => `256` threads (`Editor`, `EditorOrTestOnly`, runtime refs `0`)

C# compute dispatch callers without file-level thread-group query:

- `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:2915` (`Runtime`) `compute.Dispatch(cullingParams.ClearArgsKernel, 1, 1, 1);`
- `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:2937` (`Runtime`) `compute.Dispatch(cullingParams.CullKernel, math.max(1, groups), 1, 1);`
- `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:4594` (`Runtime`) `s_PhantomDronesCompute.Dispatch(`
- `Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs:508` (`Editor`) `cmd.DispatchCompute(compute, kernel, Mathf.CeilToInt(tileWidth / 8f), Mathf.CeilToInt(tileHeight / 8f), 1);`
- `Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs:534` (`Editor`) `cmd.DispatchCompute(compute, kernel, Mathf.CeilToInt(atlasSize / 8f), Mathf.CeilToInt(atlasSize / 8f), 1);`
- `Assets/_Project/Scripts/HectonCelestialEngine.cs:2985` (`Runtime`) `firmamentBakeCompute.Dispatch(`
- `Assets/_Project/Scripts/HectonCelestialEngine.cs:2990` (`Runtime`) `firmamentBakeCompute.Dispatch(`
- `Assets/_Project/Scripts/HectonCelestialEngine.cs:3007` (`Runtime`) `firmamentBakeCompute.Dispatch(`
- `Assets/_Project/Scripts/HectonFluidEngine.cs:6312` (`Runtime`) `abyssalFlowFieldCompute.Dispatch(_gpuAbyssalUpdateKernel, groupCount, 1, 1);`
- `Assets/_Project/Scripts/HectonFluidEngine.cs:6319` (`Runtime`) `abyssalFlowFieldCompute.Dispatch(_gpuAbyssalTextureUpdateKernel, textureGroupCount, textureGroupCount, textureGroupCount);`
- `Assets/_Project/Scripts/HectonFluidEngine.cs:6329` (`Runtime`) `abyssalFlowFieldCompute.Dispatch(_gpuAbyssalWakeKernel, textureGroupCount, textureGroupCount, textureGroupCount);`
- `Assets/_Project/Scripts/HectonFluidEngine.cs:6453` (`Runtime`) `abyssalFlowFieldCompute.Dispatch(_gpuAbyssalVortexKernel, textureGroupCount, textureGroupCount, textureGroupCount);`
- `Assets/_Project/Scripts/HectonFluidEngine.cs:6858` (`Runtime`) `gpuBuoyancyCompute.Dispatch(_gpuBuoyancyKernel, groupCount, 1, 1);`
- `Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1370` (`Runtime`) `leakPlumeCompute.Dispatch(_leakPlumeKernelIndex, 1, 1, 1);`
- `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:1477` (`Runtime`) `radarCompute.Dispatch(_radarKernel, (visualPointCount + 63) >> 6, 1, 1);`
- `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:1807` (`Runtime`) `damageHologramCompute.Dispatch(_damageHologramKernel, (_damageProxyVertexCount + 63) >> 6, 1, 1);`
- `Assets/_Project/Scripts/VFX/JacobianFoam/HectonJacobianFoamRenderFeature.cs:119` (`Runtime`) `context.cmd.DispatchCompute(payloadData.Compute, payloadData.ClearKernel, payloadData.DispatchGroupsX, payloadData.DispatchGroupsY, 1);`
- `Assets/_Project/Scripts/VFX/JacobianFoam/HectonJacobianFoamRenderFeature.cs:123` (`Runtime`) `context.cmd.DispatchCompute(payloadData.Compute, payloadData.CalculateKernel, payloadData.DispatchGroupsX, payloadData.DispatchGroupsY, 1);`
- `Assets/_Project/Scripts/VFX/JacobianFoam/HectonJacobianFoamRenderFeature.cs:147` (`Runtime`) `context.cmd.DispatchCompute(payloadData.Compute, payloadData.AdvectKernel, payloadData.DispatchGroupsX, payloadData.DispatchGroupsY, 1);`
- `Assets/_Project/Scripts/Visor/HectonFluidAdvectionRenderFeature.cs:85` (`Runtime`) `context.cmd.DispatchCompute(`

## Readiness Flags

| Flag | Value |
|---|---|
| `addressablesContentPresent` | `no` |
| `addressablesContentRoutePresent` | `yes` |
| `addressablesPackagePresent` | `yes` |
| `addressablesRuntimeLifecycleRoutePresent` | `yes` |
| `androidQuestScaffold` | `yes` |
| `androidQuestXrRouteRepairerPresent` | `yes` |
| `androidSustainedPerformanceEnabled` | `yes` |
| `androidVulkanOnlySerialized` | `yes` |
| `bootstrapExplicitShaderWarmup` | `yes` |
| `buildArtifactPresent` | `no` |
| `dataMonolithBakeRoutePresent` | `yes` |
| `dataMonolithPresent` | `no` |
| `dataMonolithValidationRoutePresent` | `yes` |
| `noHighRiskComputeThreadGroups` | `yes` |
| `noRuntimeAssetHighRiskComputeThreadGroups` | `no` |
| `noRuntimeComputeDispatchWithoutThreadGroupQuery` | `no` |
| `noRuntimeHighRiskComputeThreadGroups` | `yes` |
| `noRuntimeReferencedHighRiskComputeThreadGroups` | `yes` |
| `picoPackagePresent` | `no` |
| `questUrpAssetPresent` | `yes` |
| `questUrpWiredToAndroidQuality` | `no` |
| `shaderVariantCollectionsPresent` | `yes` |
| `shaderWarmupPreloaded` | `yes` |
| `xrProviderRouteFixerPresent` | `yes` |
| `xrProviderRouteValidatorPresent` | `yes` |
| `xrProviderSerializedProof` | `no` |

## Interpretation

- Quest/Android scaffold exists only if XR packages, ARM64, IL2CPP, and target SDK settings are present. That is not headset readiness.
- Android sustained-performance mode, Vulkan serialization, Quest URP wiring, shader warmup, and compute thread-group risk are static readiness gates, not runtime proof.
- Empty `m_BuildTargetVRSettings`, missing Addressables data, missing Data Monolith, and missing build artifacts block any GREEN platform claim.
- Native plugin parity is unresolved until Windows, Linux/Deck, macOS, Android/Quest, and PCVR player builds prove load behavior on target hardware.
- This audit is a no-claim gate. It prevents package/settings text from being inflated into runtime proof.
