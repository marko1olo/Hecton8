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
- Quality settings count: `4`
- Android default quality index: `3`
- Android default quality render pipeline guid: `d9c4cd6a763fec04a913c6a149663003`
- Quest URP referenced in QualitySettings: `yes`
- Quest URP referenced in GraphicsSettings: `no`
- Android default quality uses Quest URP: `yes`
- Quest configurator present: `yes`
- Quest configurator reports quality route: `yes`
- Quest configurator can wire Android route: `yes`

## Shader / Compute Static Risk

- Preloaded shader entries: `0`
- ShaderVariantCollection files: `10`
- Bootstrap shader collection field present: `yes`
- Bootstrap legacy `ShaderVariantCollection.WarmUp()` calls: `0`
- Bootstrap `ShaderWarmup.WarmupShaderFromCollection()` calls: `1`
- Bootstrap `WarmUpProgressively()` calls: `1`
- Bootstrap `isWarmedUp` reads: `3`
- `Shader.WarmupAllShaders()` call sites in bootstrap: `0`
- Shader source files: `613`
- `shader_feature`/`multi_compile` pragmas: `886`
- `#pragma target >= 4.5`: `107`
- `#pragma target >= 5.0`: `5`
- Compute files: `71`
- Compute reference files scanned: `9558`
- Compute reference files skipped over `2000000` bytes: `39` / bytes `346161212`
- `numthreads` declarations: `156`
- Risky numeric thread groups > `64`: `4`
- Risky numeric thread groups by execution surface: `{'Editor': 2, 'Runtime': 2}`
- Risky numeric thread groups by runtime reachability: `{'EditorOrTestOnly': 4}`
- Runtime asset risky numeric thread groups > `64`: `2`
- Runtime-referenced risky numeric thread groups > `64`: `0`
- Compute target 5.0 files: `5`
- C# compute dispatch calls: `127`; runtime: `123`
- C# compute dispatch caller files: `50`; runtime: `48`
- Dispatch calls without file-level `GetKernelThreadGroupSizes`: `40`; runtime: `38`
- Dispatch caller files without file-level `GetKernelThreadGroupSizes`: `14`; runtime: `13`
- Dispatch calls without thread-group query by execution surface: `{'Editor': 2, 'Runtime': 38}`

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
- Data Monolith path: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`, exists: `yes`, bytes: `1064384`
- Data Monolith compiler: `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs`, present: `yes`
- Data Monolith command-line bake route: `yes`
- Data Monolith prebuild bake/validation gate: `yes`
- Data Monolith output validation route: `yes`
- Data Monolith atomic temp-write/validate route: `yes`
- Data Monolith little-endian guard: `yes`
- Data Monolith production coverage gate: `yes`
- External `.h8bin` validator: `Tools/h8bin_validator.py`, present: `yes`
- Data Monolith source folder: `Assets/_SourceData/DataMonolith`, exists: `yes`, files: `0`
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

- `Assets/_Project/Art/Shaders/Hecton_SonarRaymarch.compute:113` `128, 1, 1` => `128` threads (`Runtime`, `EditorOrTestOnly`, runtime refs `0`)
- `Assets/_Project/Art/Shaders/Hecton_SonarRaymarch.compute:186` `128, 1, 1` => `128` threads (`Runtime`, `EditorOrTestOnly`, runtime refs `0`)
- `Assets/Editor/x64/Bakery/shaderSrc/ftCullFarSphere.compute:20` `256, 1, 1` => `256` threads (`Editor`, `EditorOrTestOnly`, runtime refs `0`)
- `Assets/Editor/x64/Bakery/shaderSrc/ftTransformFarSphere.compute:30` `16, 16, 1` => `256` threads (`Editor`, `EditorOrTestOnly`, runtime refs `0`)

C# compute dispatch callers without file-level thread-group query:

- `Assets/_Project/Scripts/VFX/JacobianFoam/HectonJacobianFoamRenderFeature.cs:125` (`Runtime`) `context.cmd.DispatchCompute(payloadData.Compute, payloadData.ClearKernel, payloadData.ClearDispatchGroupsX, payloadData.ClearDispatchGroupsY, 1);`
- `Assets/_Project/Scripts/VFX/JacobianFoam/HectonJacobianFoamRenderFeature.cs:129` (`Runtime`) `context.cmd.DispatchCompute(payloadData.Compute, payloadData.CalculateKernel, payloadData.CalculateDispatchGroupsX, payloadData.CalculateDispatchGroupsY, 1);`
- `Assets/_Project/Scripts/VFX/JacobianFoam/HectonJacobianFoamRenderFeature.cs:153` (`Runtime`) `context.cmd.DispatchCompute(payloadData.Compute, payloadData.AdvectKernel, payloadData.AdvectDispatchGroupsX, payloadData.AdvectDispatchGroupsY, 1);`
- `Assets/_Project/Scripts/Visor/HectonFluidAdvectionRenderFeature.cs:91` (`Runtime`) `context.cmd.DispatchCompute(`
- `Assets/Crest/Crest/Scripts/Collision/QueryBase.cs:495` (`Runtime`) `_shaderProcessQueries.Dispatch(_kernelHandle, numGroups, 1, 1);`
- `Assets/Crest/Crest/Scripts/Helpers/TextureArrayHelpers.cs:57` (`Runtime`) `s_clearToBlackShader.Dispatch(`
- `Assets/Crest/Crest/Scripts/LodData/LodDataMgrAnimWaves.cs:411` (`Runtime`) `buf.DispatchCompute(_combineShader, selectedShaderKernel,`
- `Assets/Crest/Crest/Scripts/LodData/LodDataMgrPersistent.cs:175` (`Runtime`) `buf.DispatchCompute(_shader, krnl_ShaderSim,`
- `Assets/Crest/Crest/Scripts/Shapes/FFT/FFTBaker.cs:109` (`Runtime`) `buf.DispatchCompute(waveCombineShader, kernel, bakedWaves.width / 8, bakedWaves.height / 8, 1);`
- `Assets/Crest/Crest/Scripts/Shapes/FFT/FFTCompute.cs:408` (`Runtime`) `buf.DispatchCompute(_shaderSpectrum, _kernelSpectrumInit, _resolution / 8, _resolution / 8, CASCADE_COUNT);`
- `Assets/Crest/Crest/Scripts/Shapes/FFT/FFTCompute.cs:426` (`Runtime`) `buf.DispatchCompute(_shaderSpectrum, _kernelSpectrumUpdate, _resolution / 8, _resolution / 8, CASCADE_COUNT);`
- `Assets/Crest/Crest/Scripts/Shapes/FFT/FFTCompute.cs:443` (`Runtime`) `buf.DispatchCompute(_shaderFFT, kernelOffset, 1, _resolution, CASCADE_COUNT);`
- `Assets/Crest/Crest/Scripts/Shapes/FFT/FFTCompute.cs:450` (`Runtime`) `buf.DispatchCompute(_shaderFFT, kernelOffset + 1, _resolution, 1, CASCADE_COUNT);`
- `Assets/Crest/Crest/Scripts/Shapes/ShapeGerstner.cs:364` (`Runtime`) `buf.DispatchCompute(_shaderGerstner, _krnlGerstner, _waveBuffers.width / LodDataMgr.THREAD_GROUP_SIZE_X, _waveBuffers.height / LodDataMgr.THREAD_GROUP_SIZE_Y, _`
- `Assets/Editor/x64/Bakery/scripts/ftBuildGraphics.cs:3003` (`Editor`) `farSphereCSTransform.Dispatch(0, dispatchWidth, dispatchWidth, 1);`
- `Assets/Editor/x64/Bakery/scripts/ftBuildGraphics.cs:3017` (`Editor`) `farSphereCSCull.Dispatch(0, dispatchIndexGroups, 1, 1);`
- `Assets/GPUInstancer/Scripts/Core/Contract/GPUInstancerManager.cs:637` (`Runtime`) `_argsBufferComputeShader.Dispatch(_argsBufferDoubleInstanceCountComputeKernelID, Mathf.CeilToInt(count / GPUInstancerConstants.COMPUTE_SHADER_THREAD_COUNT), 1, `
- `Assets/GPUInstancer/Scripts/Core/Static/GPUInstancerUtility.cs:573` (`Runtime`) `cameraComputeShader.Dispatch(instanceVisibilityComputeKernelId,`
- `Assets/GPUInstancer/Scripts/Core/Static/GPUInstancerUtility.cs:611` (`Runtime`) `visibilityComputeShader.Dispatch(instanceVisibilityComputeKernelId,`
- `Assets/GPUInstancer/Scripts/Core/Static/GPUInstancerUtility.cs:706` (`Runtime`) `bufferToTextureComputeShader.Dispatch(bufferToTextureComputeKernelID, Mathf.CeilToInt(runtimeData.bufferSize / GPUInstancerConstants.COMPUTE_SHADER_THREAD_COUNT`

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
| `dataMonolithPresent` | `yes` |
| `dataMonolithValidationRoutePresent` | `yes` |
| `noHighRiskComputeThreadGroups` | `yes` |
| `noRuntimeAssetHighRiskComputeThreadGroups` | `no` |
| `noRuntimeComputeDispatchWithoutThreadGroupQuery` | `no` |
| `noRuntimeHighRiskComputeThreadGroups` | `yes` |
| `noRuntimeReferencedHighRiskComputeThreadGroups` | `yes` |
| `picoPackagePresent` | `no` |
| `questUrpAssetPresent` | `yes` |
| `questUrpWiredToAndroidQuality` | `yes` |
| `shaderVariantCollectionsPresent` | `yes` |
| `shaderWarmupPreloaded` | `no` |
| `xrProviderRouteFixerPresent` | `yes` |
| `xrProviderRouteValidatorPresent` | `yes` |
| `xrProviderSerializedProof` | `no` |

## Interpretation

- Quest/Android scaffold exists only if XR packages, ARM64, IL2CPP, and target SDK settings are present. That is not headset readiness.
- Android sustained-performance mode, Vulkan serialization, Quest URP wiring, shader warmup, and compute thread-group risk are static readiness gates, not runtime proof.
- Empty `m_BuildTargetVRSettings`, missing Addressables data, missing Data Monolith, and missing build artifacts block any GREEN platform claim.
- Native plugin parity is unresolved until Windows, Linux/Deck, macOS, Android/Quest, and PCVR player builds prove load behavior on target hardware.
- This audit is a no-claim gate. It prevents package/settings text from being inflated into runtime proof.
