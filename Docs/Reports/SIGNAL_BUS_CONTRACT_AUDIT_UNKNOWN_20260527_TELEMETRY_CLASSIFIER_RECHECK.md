# SHINOBU_02 Signal Bus Contract Audit CLI

Evidence Class: STATIC_SOURCE_CLASSIFIED
Scope: Full
Generated UTC: 2026-05-27T06:15:05.9330503Z

## Summary

- Files scanned: 2440 C# / 71 compute
- Signal-like definitions found: 876
- Signal definitions still in Core/GlobalSignals.cs: 0
- Pack=1 layouts: 0
- Runtime signal Pack=1 layouts: 0
- Runtime signal transitive Pack=1 field hits: 0
- Signal-like definitions without nearby StructLayout: 88
- Managed event surface hits: 0
- Local native telemetry ring hits: 30
- Registered local telemetry rings: 10
- Local native signal queue hits: 18
- Compute 1024-thread-group hits: 0
- Hot-path heuristic hits: 102
- Cold/fatal sync I/O review hits: 444
- Assembly contract boundary hits: 0
- Cache-line-critical stride debt hits: 3
- Errors: 0
- Warnings: 365
- Infos: 553
- Confirmed/probable errors at confidence >= 90: 0
- Review-only findings below confidence 75: 733

## Rule Breakdown

- COLD_OR_FATAL_SYNC_IO_REVIEW: total 444, errors 0, warnings 0, infos 444, avg confidence 64
- RUNTIME_SYNC_FILE_IO_REVIEW: total 172, errors 0, warnings 172, infos 0, avg confidence 76
- SIGNAL_LAYOUT_REVIEW: total 83, errors 0, warnings 83, infos 0, avg confidence 65
- DUPLICATE_SIGNAL_LIKE_NAME_REVIEW: total 48, errors 0, warnings 48, infos 0, avg confidence 74
- SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW: total 42, errors 0, warnings 42, infos 0, avg confidence 64
- MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW: total 37, errors 0, warnings 0, infos 37, avg confidence 52
- GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW: total 20, errors 0, warnings 0, infos 20, avg confidence 54
- LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW: total 17, errors 0, warnings 0, infos 17, avg confidence 70
- EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW: total 15, errors 0, warnings 0, infos 15, avg confidence 56
- EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW: total 6, errors 0, warnings 6, infos 0, avg confidence 60
- LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW: total 6, errors 0, warnings 0, infos 6, avg confidence 68
- LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY: total 5, errors 0, warnings 5, infos 0, avg confidence 73
- LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT: total 5, errors 0, warnings 5, infos 0, avg confidence 88
- EDITOR_SIGNAL_LAYOUT_REVIEW: total 5, errors 0, warnings 0, infos 5, avg confidence 55
- ZERO_GC_HOT_PATH_ALLOCATION_REVIEW: total 3, errors 0, warnings 3, infos 0, avg confidence 66
- CACHELINE_CRITICAL_SIGNAL_STRIDE_DEBT: total 3, errors 0, warnings 0, infos 3, avg confidence 88
- LOCAL_NATIVE_TELEMETRY_RING_ROOT_OWNER: total 3, errors 0, warnings 0, infos 3, avg confidence 91
- LOCAL_NATIVE_TELEMETRY_RING_OWNER_LOCAL: total 2, errors 0, warnings 0, infos 2, avg confidence 80
- LOCAL_SIGNAL_QUEUE_DECLARED_ONLY_REVIEW: total 1, errors 0, warnings 0, infos 1, avg confidence 61
- EDITOR_SIGNAL_NAME_SHADOWS_RUNTIME: total 1, errors 0, warnings 1, infos 0, avg confidence 68

## Classification Breakdown

- COLD_OR_FATAL_IO_BOUNDARY: 444
- IO_PRESSURE_HEURISTIC: 172
- HOT_PATH_HEURISTIC: 102
- NAME_BASED_REVIEW: 83
- STATIC_CONTRACT_REVIEW: 48
- EDITOR_ONLY_REVIEW: 27
- REGISTERED_LOCAL_QUEUE_REVIEW: 17
- STATIC_DECLARATION_REVIEW: 6
- SIGNAL_SCRATCH_REVIEW: 6
- CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL: 5
- CACHELINE_CRITICAL_TELEMETRY_DEBT: 3
- CONFIRMED_ROOT_ALLOCATOR_TELEMETRY: 3
- CONFIRMED_OWNER_LOCAL_TELEMETRY: 2

## Findings

- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ConstructionManager.cs:1573 | DumpShinobu336BlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ConstructionManager.cs:1575 | DumpShinobu336BlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ConstructionManager.cs:1990 | DumpDeconstructionBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ConstructionManager.cs:1992 | DumpDeconstructionBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/CraftingSystem.FastFail.cs:336 | TryDumpTelemetryToFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/CraftingSystem.FastFail.cs:340 | TryDumpTelemetryToFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][80%][CONFIRMED_OWNER_LOCAL_TELEMETRY] LOCAL_NATIVE_TELEMETRY_RING_OWNER_LOCAL | Assets/_Project/Scripts/EncounterDirector.cs:290 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<EncounterDirectorBlackBoxEntry> _blackBox;`
  Required action: This telemetry/blackbox ring has sentinel ownership, bounded lifetime, and owner-local dump usage. Do not migrate it to GlobalDataVault unless another domain consumes the buffer or the state becomes persistent authority.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/EncounterDirector.cs:1121 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(parent);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/EncounterDirector.cs:1123 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/FabricationAssemblerRuntime.cs:1206 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/FabricationAssemblerRuntime.cs:1208 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fabricator.cs:1917 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fabricator.cs:1919 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/Fabricator.cs:3169 | FlushApplyErrorFeedback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_errorFeedbackBlock.SetColor(EmissionColorId, color);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/FaunaDirector.cs:211 | AcousticPanicCommand
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct AcousticPanicCommand`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/FlashlightTool.cs:169 | UpdatePowerIndicator
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetFloat(_ToolBatteryNormalizedID, math.saturate(batteryCharge));`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/FlashlightTool.cs:172 | UpdatePowerIndicator
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetColor(_EmissionColorID, Color.black);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonBoidController.cs:1539 | RenderBoids
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_runtimeFishMaterial.SetFloat(ShaderProps.BoidUseVisibleIndices, 1f);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonBoidController.cs:1540 | RenderBoids
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_runtimeFishMaterial.SetFloat(ShaderProps.FishScale, fishScale);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonBoidController.cs:1541 | RenderBoids
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_runtimeFishMaterial.SetFloat(ShaderProps.FoveatedVatTimeScale, ResolveFoveatedVatTimeScale());`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6139 | UpdateStarIntensity
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `blendedSkyboxMaterial.SetFloat(_ID_StarIntensity, _currentStarIntensity);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6525 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetVector(_ID_FresnelSunDir, new Vector4(toSun.x, toSun.y, toSun.z, 0));`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6526 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_BacklitIntensity, backlitIntensity);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6527 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_EquatorialSpeed, equatorialRotationSpeed);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6528 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_PolarMultiplier, polarRotationMultiplier);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6529 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_PlanetPhase, _currentPhase);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6530 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_StormEmission, stormEmissionIntensity * ResolveStormEmissionMultiplier());`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6531 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_SunBacklitFactor, _currentBacklitFactor);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6532 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_GlobalRotation, _rotationPhase);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6533 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_GameTime, _gameTime);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6534 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_NightBlend, _currentBlend);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6535 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_AtmosphereTransmittanceWeight, _atmosphereTransmittanceWeight);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6536 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_AtmosphereInscatterWeight, _atmosphereInscatterWeight);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6538 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetColor(_ID_SkyColorZenith, _resolvedSkyZenith);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6539 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetColor(_ID_SkyColorHorizon, _resolvedSkyHorizon);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6540 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetColor(_ID_SkyColorNadir, _resolvedSkyNadir);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6543 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetVector(_ID_WindDirection, _skyMaterial.GetVector(_ID_WindDirection));`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6569 | UpdateMoonMaterialOverrides
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_moonMPB.SetFloat(`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6572 | UpdateMoonMaterialOverrides
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_moonMPB.SetFloat(`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6575 | UpdateMoonMaterialOverrides
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_moonMPB.SetFloat(_ID_HectonMoonPhase01, phase01);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6576 | UpdateMoonMaterialOverrides
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_moonMPB.SetFloat(_ID_HectonMoonPhaseTextureIndex, ResolveMoonPhaseTextureIndex(phase01));`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6901 | DumpCelestialBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6903 | DumpCelestialBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonFabricatorUI.cs:1111 | UpdateHologramMaterialState
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_runtimeHologramMaterial.SetFloat(CraftProgressId, progress);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonFabricatorUI.cs:1114 | UpdateHologramMaterialState
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_runtimeHologramMaterial.SetFloat(ScanProgressId, progress);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonFabricatorUI.cs:1124 | UpdateHologramMaterialState
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_runtimeHologramMaterial.SetFloat(GlitchAmountId, glitch);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:3023 | DumpOceanSurfaceTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:3025 | DumpOceanSurfaceTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:3237 | DumpFluidSovereigntyTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:3239 | DumpFluidSovereigntyTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:4763 | WriteFluidAdvectionTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:4765 | WriteFluidAdvectionTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:6368 | DumpMaelstromTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:6370 | DumpMaelstromTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:7610 | DumpAbyssalFlowTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:7612 | DumpAbyssalFlowTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonPlayerMovement.cs:3319 | DumpPlayerKinematicsBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonPlayerMovement.cs:3321 | DumpPlayerKinematicsBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonPlayerMovement.cs:8454 | DumpCinematicFocusBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonPlayerMovement.cs:8456 | DumpCinematicFocusBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/HectonVoxelEngine.cs:7216 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/HectonVoxelEngine.cs:7218 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/LocalizationManager.cs:1019 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/LocalizationManager.cs:1075 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/LocRegistry.cs:513 | _telemetryFrames
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `private static NativeArray<BabelTelemetryEntry> _telemetryFrames;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/LocRegistry.cs:3085 | WriteTelemetryDumpFiles
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(docsPath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/LocRegistry.cs:3102 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/MathLodApproximation.cs:824 | TryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/MathLodApproximation.cs:826 | TryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModularEquipmentEngine.cs:2504 | DumpEquipmentTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModularEquipmentEngine.cs:2506 | DumpEquipmentTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:5269 | DumpSalinityCorrosionBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:5271 | DumpSalinityCorrosionBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:5439 | DumpInventoryBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:5441 | DumpInventoryBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/RepairTool.cs:2024 | DumpRepairBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/RepairTool.cs:2026 | DumpRepairBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/ResourceNode.cs:1153 | UpdateMeltProperties
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_propertyBlock.SetVector(_MeltCenterId, _localHitPoint);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/ResourceNode.cs:1154 | UpdateMeltProperties
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_propertyBlock.SetFloat(_MeltRadiusId, meltRadius);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/RuntimeDiagnosticsTrace.cs:88 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][66%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ALLOCATION_REVIEW | Assets/_Project/Scripts/SaveBinaryStorage.cs:404 | FlushPath
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/SaveManager.cs:202 | _saveTelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<AsyncPersistenceTelemetryEntry> _saveTelemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/SaveManager.cs:203 | _wfcOutpostTelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<WfcOutpostTelemetryEntry> _wfcOutpostTelemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/SaveManager.cs:204 | _wfcOutpostEventTelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<WfcOutpostTelemetryEntry> _wfcOutpostEventTelemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/ScannerTool.cs:464 | ScannerBlackBoxEntry
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ScannerBlackBoxEntry`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/ScannerTool.cs:731 | UpdatePowerIndicator
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetColor(_EmissionColorID, Color.black);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ScannerTool.cs:1328 | DumpScannerBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ScannerTool.cs:1330 | DumpScannerBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/SeamGapDitherRenderer.cs:233 | FlushQueuedSeamDitherVisuals
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_drawPropertyBlock.SetVector(_CameraPositionId, ResolveCameraRuntimePosition(targetCamera));`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/SeamGapDitherRenderer.cs:234 | FlushQueuedSeamDitherVisuals
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_drawPropertyBlock.SetFloat(_MaxCameraDistanceId, Mathf.Max(0.5f, maxCameraDistance));`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs:5988 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SubmarineFluidDynamics.cs:6063 | WriteHydroBlackBoxDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SubmarineFluidDynamics.cs:6065 | WriteHydroBlackBoxDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1795 | RenderLeakPlumeParticles
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_leakPlumeDrawProperties.SetFloat(_LeakUseParticleBufferId, 1f);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1796 | RenderLeakPlumeParticles
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_leakPlumeDrawProperties.SetFloat(_LeakParticleSizeId, math.max(0.01f, leakPlumeParticleSizeMeters));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1798 | RenderLeakPlumeParticles
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_leakPlumeDrawProperties.SetVector(_LeakCameraRightId, cameraRight);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1799 | RenderLeakPlumeParticles
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_leakPlumeDrawProperties.SetVector(_LeakCameraUpId, cameraUp);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:2102 | WriteDamageControlTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:2104 | WriteDamageControlTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/TetherManager.cs:157 | TetherManagerTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct TetherManagerTelemetryEntry`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VoxelDeltaProcessor.cs:6392 | WriteBlackBoxDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VoxelDeltaProcessor.cs:6394 | WriteBlackBoxDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs:2073 | DumpTerrainSeamBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs:2075 | DumpTerrainSeamBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Animation/LeviathanTerrainIkJobs.cs:164 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Animation/LeviathanTerrainIkJobs.cs:166 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Animation/VRPhysicalHandPresenceIkJobs.cs:337 | Validate
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Animation/VRPhysicalHandPresenceIkJobs.cs:339 | Validate
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/AtlasSignal/AtlasSignalEvents.cs:144 | _pendingEvents
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<AtlasSignalEventPayload> _pendingEvents;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/AtlasSignal/AtlasSignalEvents.cs:145 | _nextFrameEvents
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<AtlasSignalEventPayload> _nextFrameEvents;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsJobs.cs:891 | AtmosphereTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct AtmosphereTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:1224 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:1225 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1750 | DumpTelemetryToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1752 | DumpTelemetryToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:2188 | ScanTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ScanTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:1296 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:1300 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:12023 | DumpGranularTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:12041 | WriteGranularTelemetryDumpCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:12079 | DumpPrologueTransitionTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:12092 | WritePrologueTransitionTelemetryDumpCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:12135 | DumpAudioSynthesisTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:12148 | WriteAudioSynthesisTelemetryDumpCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:223 | VocalWarningTelemetrySnapshot
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct VocalWarningTelemetrySnapshot`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:1451 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:1453 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:1933 | HandleH8MemoryFatalLog
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:6338 | TryPrepareBackgroundDomainHandshake
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(telemetryPath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:6425 | InspectPreviousBootState
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/BaseModuleCatalogRuntime.cs:448 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/BaseModuleCatalogRuntime.cs:911 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/BaseModuleCatalogRuntime.cs:915 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Create(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Construction/BulkheadContainmentJobs.cs:481 | RecordBulkheadTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct RecordBulkheadTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime.cs:1706 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime.cs:1707 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime_HatchLocks.cs:756 | TryDumpHatchBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime_HatchLocks.cs:757 | TryDumpHatchBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_hatchDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:6705 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:6707 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager_Transactions.cs:1507 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager_Transactions.cs:1509 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs:806 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs:808 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorData.cs:809 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorData.cs:811 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:2853 | DumpFloodBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:2855 | DumpFloodBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Construction/HatchLockJobs.cs:522 | RecordHatchTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct RecordHatchTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/ModularBaseConstructionValidator.cs:809 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/ModularBaseConstructionValidator.cs:811 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs:871 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs:873 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Construction/ShinobuSocketConstructionJobs.cs:1035 | RecordConstructionSocketTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordConstructionSocketTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Construction/SumpPumpPipeGridJobs.cs:765 | DrainageTelemetryRecorderJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct DrainageTelemetryRecorderJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/SumpPumpPipeGridRuntime.cs:1481 | InitializeDumpWriterCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/SumpPumpPipeGridRuntime.cs:1556 | DumpWriterLoop
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:1229 | DumpDockTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:1233 | DumpDockTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs:1063 | DumpFailure
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs:1065 | DumpFailure
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:1705 | InitializeReplayFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_replayStream = new FileStream(_replayPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.RandomAccess);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:261 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `private NativeArray<FoveatedSimulationTelemetryEntry> _telemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1174 | DumpTelemetryBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1176 | DumpTelemetryBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][66%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ALLOCATION_REVIEW | Assets/_Project/Scripts/Core/GlobalTelemetryBus.Blackbox.cs:1612 | FlushMmfScratchToDisk
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))`
  Required action: Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs:100 | _snapshotBuffer
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<TelemetryEvent> _snapshotBuffer;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs:608 | DumpHapticTelemetryIfNeeded
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs:613 | DumpHapticTelemetryIfNeeded
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/HectonPersistentPathPolicy.cs:32 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.cs:931 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.cs:933 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs:1912 | DumpScalabilityDictatorBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs:1936 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs:2018 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1574 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1576 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_inputReplayStream = new FileStream(replayPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1725 | DumpDeterministicInputBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1729 | DumpDeterministicInputBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1532 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1534 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/RebindingManager.cs:595 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3390 | DumpMasterFenceTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3391 | DumpMasterFenceTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3443 | DumpMasterPipelineTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3444 | DumpMasterPipelineTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:4564 | DumpDispatcherBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:4581 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs:223 | _pendingCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<EntityCommand> _pendingCommands;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Dev/BotController.cs:514 | FlushCsvSamplesCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:2671 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:2680 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllBytes(path, bytes);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs:1934 | EcosystemTelemetryReductionJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal unsafe struct EcosystemTelemetryReductionJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs:805 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs:807 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs:1551 | InitializeNutrientTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct InitializeNutrientTelemetryJob : IJobParallelFor`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs:1927 | RecordNutrientTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct RecordNutrientTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs:1032 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs:1034 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime_Carrion.cs:1610 | RecordCarrionTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct RecordCarrionTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime_Carrion.cs:655 | DumpCarrionTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime_Carrion.cs:657 | DumpCarrionTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/ChroniclerDiagnosticHeatmapWindow.cs:45 | _signalLanes
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<SignalLaneTelemetry> _signalLanes;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/ChroniclerDiagnosticHeatmapWindow.cs:46 | _signalFrames
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<SignalTelemetryFrame> _signalFrames;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/OOP_Trigger_Scanner.cs:170 | _telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<AupNarrativeTriggerTelemetryEntry> _telemetry;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][55%][EDITOR_ONLY_REVIEW] EDITOR_SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Editor/SaveSystemTelemetry.cs:30 | SectorTelemetryRow
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct SectorTelemetryRow`
  Required action: Editor/test signal-like structs do not gate runtime, but should not shadow production contracts.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs:19 | _telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<SignalLaneTelemetry> _telemetry;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs:20 | _frames
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<SignalTelemetryFrame> _frames;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][55%][EDITOR_ONLY_REVIEW] EDITOR_SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Editor/SystemDiagnosticsBoard.cs:35 | TelemetrySnapshotRow
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct TelemetrySnapshotRow`
  Required action: Editor/test signal-like structs do not gate runtime, but should not shadow production contracts.
- [WARN][60%][EDITOR_ONLY_REVIEW] EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW | Assets/_Project/Scripts/Editor/SystemDiagnosticsBoard.cs:41 | TelemetrySnapshotRow
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `public string Systems;`
  Required action: Editor/test signal-like structs can use managed strings, but must not become runtime payload contracts.
- [WARN][60%][EDITOR_ONLY_REVIEW] EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW | Assets/_Project/Scripts/Editor/SystemDiagnosticsBoard.cs:59 | TelemetrySnapshotRow
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `public string ErrorFlags;`
  Required action: Editor/test signal-like structs can use managed strings, but must not become runtime payload contracts.
- [WARN][60%][EDITOR_ONLY_REVIEW] EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW | Assets/_Project/Scripts/Editor/SystemDiagnosticsBoard.cs:62 | TelemetrySnapshotRow
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `public string ExportReason;`
  Required action: Editor/test signal-like structs can use managed strings, but must not become runtime payload contracts.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:2213 | TryLoadLegacyFaultBinaryAt
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3562 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3599 | WriteSeismicTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3629 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3643 | WriteCelestialTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3771 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3772 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(TelemetryDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:977 | TryHydrateRigDefinitionsBinaryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:2331 | DumpTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:2340 | DumpTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:2401 | DumpBiteTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:2404 | DumpBiteTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs:1585 | DumpTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs:1587 | DumpTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.AcousticSdf.cs:179 | AcousticSensoryTelemetrySnapshot
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct AcousticSensoryTelemetrySnapshot`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.AcousticSdf.cs:2122 | RecordAcousticTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct RecordAcousticTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.AcousticSdf.cs:1189 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(_acousticSdfDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.AcousticSdf.cs:1221 | EnsureAcousticSdfDumpPathCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:4193 | DumpRetinalBlackBoxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:4208 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:4429 | DumpAlphaLeviathanBlackBoxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:4444 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:4605 | DumpMesofaunaBlackBoxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:4620 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain_Steering.cs:1340 | RecordSteeringTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private unsafe struct RecordSteeringTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain_Steering.cs:580 | DumpLeviathanSteeringBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(SteeringDumpDirectoryRelativePath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain_Steering.cs:582 | DumpLeviathanSteeringBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(SteeringDumpRelativePath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:1263 | DumpTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:1265 | DumpTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Fauna/StressDrivenSpawnDirector.cs:3022 | RecordDirectorTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal unsafe struct RecordDirectorTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/StressDrivenSpawnDirector.cs:2288 | DumpBlackBoxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(path));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/StressDrivenSpawnDirector.cs:2289 | DumpBlackBoxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/Gameplay/BaseAirlock.cs:1493 | FlushStatusLight
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetColor(_emissionPropertyId != 0 ? _emissionPropertyId : _EmissionColorID, _pendingStatusLightColor);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][80%][CONFIRMED_OWNER_LOCAL_TELEMETRY] LOCAL_NATIVE_TELEMETRY_RING_OWNER_LOCAL | Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs:1371 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ContextualPhysicalIkTelemetryEntry> _telemetryRing;`
  Required action: This telemetry/blackbox ring has sentinel ownership, bounded lifetime, and owner-local dump usage. Do not migrate it to GlobalDataVault unless another domain consumes the buffer or the state becomes persistent authority.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs:2552 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs:2554 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1705 | TryLoadMmfCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1797 | .ctor
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1799 | .ctor
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/Gameplay/DeployableBeacon.cs:545 | UpdateBeaconLight
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetColor(_emissionPropertyId != 0 ? _emissionPropertyId : _EmissionColorID, lightColor);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/Gameplay/EnvironmentalHazard.cs:754 | UpdateIndicator
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetColor(_emissionPropertyId != 0 ? _emissionPropertyId : _EmissionColorID, indicatorColor);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/Gameplay/MessageTerminal.cs:887 | FlushStatusLight
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetColor(_emissionPropertyId != 0 ? _emissionPropertyId : _EmissionColorID, _pendingStatusLightColor);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:3868 | DumpFaultTelemetryIfNeeded
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:3881 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime_HandIK.cs:625 | DumpHandIkTelemetryFaultOnly
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime_HandIK.cs:629 | DumpHandIkTelemetryFaultOnly
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs:185 | _pendingTraumaHudSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<TraumaHudSignal> _pendingTraumaHudSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs:186 | _nextFrameTraumaHudSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<TraumaHudSignal> _nextFrameTraumaHudSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs:187 | _pendingInteractionSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<PlayerInteractionStressSignal> _pendingInteractionSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs:188 | _nextFrameInteractionSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<PlayerInteractionStressSignal> _nextFrameInteractionSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs:189 | _pendingToolDepletedSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<PlayerToolDepletedSignal> _pendingToolDepletedSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs:190 | _nextFrameToolDepletedSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<PlayerToolDepletedSignal> _nextFrameToolDepletedSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:2355 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:2357 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/Gameplay/ScannableFragment.cs:514 | UpdateScanVisuals
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetFloat(_ScanProgressID, progress);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/Gameplay/ScannableFragment.cs:515 | UpdateScanVisuals
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetColor(_ScanGlowColorID, scanGlowColor);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/Gameplay/ScannableFragment.cs:516 | UpdateScanVisuals
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetFloat(_ScanPulseID, pulse);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1682 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1686 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/Gameplay/SealedDoor.cs:591 | UpdateProgressVisuals
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetFloat(_ProgressID, progressNormalized);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/Gameplay/SealedDoor.cs:592 | UpdateProgressVisuals
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetColor(_GlowColorID, cutGlowColor);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1741 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:2087 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:2089 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:2563 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:2565 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:2601 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:2603 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Gameplay/SuitMeshUpdateEvents.cs:118 | _pendingSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<SuitMeshUpdateSignal> _pendingSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Gameplay/SuitMeshUpdateEvents.cs:119 | _nextFrameSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<SuitMeshUpdateSignal> _nextFrameSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:1361 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:1363 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Gameplay/VehicleCommandSignals.cs:133 | _pendingCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<VehicleCommandSignal> _pendingCommands;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Gameplay/VehicleCommandSignals.cs:134 | _nextFrameCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<VehicleCommandSignal> _nextFrameCommands;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:1525 | ClearComfortTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ClearComfortTelemetryJob : IJobParallelFor`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Gameplay/VRSomaticProvider.HorizonLock.cs:576 | ClearHorizonTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ClearHorizonTelemetryJob : IJobParallelFor`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Input/ControlRemapper.cs:137 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Input/ControlRemapper.cs:140 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, FileStreamBufferBytes, FileOptions.WriteThrough))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Input/ControlRemapper.cs:549 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferBytes, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Input/ControlRemapper.cs:587 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Input/ControlRemapper.cs:590 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, FileStreamBufferBytes, FileOptions.WriteThrough))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Input/ControlRemapper.cs:642 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(tempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Interaction/VRInteractionKinematicBridge.cs:1177 | RecordVRInteractionTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordVRInteractionTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Interaction/VRInteractionKinematicBridge.cs:319 | DumpTelemetryFaultOnly
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Interaction/VRInteractionKinematicBridge.cs:323 | DumpTelemetryFaultOnly
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(resolvedPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:2433 | ShinobuEconomyTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct ShinobuEconomyTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:1451 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:1453 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:1488 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:1490 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.CargoSync.cs:775 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.CargoSync.cs:777 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.cs:1641 | RecordSoaInventoryTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordSoaInventoryTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.cs:700 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.cs:702 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs:894 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs:896 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Lighting/HectonLightingRuntime_DayNightRelay.cs:542 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Lighting/HectonLightingRuntime_DayNightRelay.cs:544 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:3222 | InteriorGITelemetryScanJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct InteriorGITelemetryScanJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:1759 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:1761 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Meta/GlobalProfileManager.cs:1019 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Meta/GlobalProfileManager.cs:1024 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllText(tempPath, json);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Meta/GlobalProfileManager.cs:1027 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Meta/GlobalProfileManager.cs:1040 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(tempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Meta/GlobalProfileManager.cs:1059 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string json = File.ReadAllText(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][61%][STATIC_DECLARATION_REVIEW] LOCAL_SIGNAL_QUEUE_DECLARED_ONLY_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:417 | Queue
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeQueue<FutureCommandEnvelope> Queue;`
  Required action: This NativeQueue field has no allocation in the same source file. Keep the external owner visible, but do not classify it as a live orphaned lane until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:2354 | DumpKernelTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:2355 | DumpKernelTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(KernelDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:2393 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:2394 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(DumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/ModAssetManager.cs:193 | LoadRawTexture
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `pngBytes = File.ReadAllBytes(filePath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs:279 | _pendingCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<ModCommand> _pendingCommands;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs:280 | _pendingAupCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<ModAupCommand> _pendingAupCommands;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs:281 | _pendingRenderCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<ModRenderInstanceCommand> _pendingRenderCommands;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs:623 | ModCullTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ModCullTelemetryEntry`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs:48 | _cullTelemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ModCullTelemetryEntry> _cullTelemetry;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/ModLoader.cs:251 | TryReadManifest
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string json = File.ReadAllText(manifestPath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][88%][CACHELINE_CRITICAL_TELEMETRY_DEBT] CACHELINE_CRITICAL_SIGNAL_STRIDE_DEBT | Assets/_Project/Scripts/Narrative/HectonNarrativeDirector_PoiTriggers.cs:1120 | ProgressionEventSignal
  Evidence kind: CONFIGURE_CACHELINE_CRITICAL_CALL
  Evidence: `SignalBus<ProgressionEventSignal>.ConfigureCacheLineCritical( expectedCapacity: 256, maxFrameSignals: 512, lowTierFrameSignals: 64, laneHash: AupNarrativePoiRuntimeConstants.SignalLaneHash);`
  Required action: This cache-line-critical lane currently has a payload stride outside 64/128 bytes. Keep telemetry flag bit 32 active and migrate to a 64/128-byte payload or split gameplay truth from visual sidecar before raising cadence.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Narrative/HectonNarrativeDirector_PoiTriggers.cs:836 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Narrative/HectonNarrativeDirector_PoiTriggers.cs:838 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Narrative/LoreMmfEncyclopedia.cs:48 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream indexStream = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Narrative/LoreMmfEncyclopedia.cs:49 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_payloadStream = new FileStream(payloadPath, FileMode.Open, FileAccess.Read, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs:1011 | TryLoadLegacyLatencyProfile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_legacyProfilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs:1535 | DumpNetcodeBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs:1538 | DumpNetcodeBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:3839 | DumpHeapTelemetryToFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:3841 | DumpHeapTelemetryToFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:2347 | RecordCartographyTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordCartographyTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:1096 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dir);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:1127 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(telemetryDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:2410 | TryDumpPhysicsCullingBlackBoxToFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:2414 | TryDumpPhysicsCullingBlackBoxToFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/HabitatFluidIncursionJobs.cs:868 | FluidTelemetryRecorderJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct FluidTelemetryRecorderJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/HarpoonTensionSolver328.cs:1877 | RecordTetherTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordTetherTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/TetherAupVerletJobs.cs:842 | RecordTetherAupTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct RecordTetherAupTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/TetherVerletJobs.cs:438 | TetherVerletTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct TetherVerletTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/VerletCableDTOs.cs:1081 | VerletBlackBoxWriteJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct VerletBlackBoxWriteJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuMetabolismJobs.cs:979 | MetabolismTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct MetabolismTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs:2314 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs:2359 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs:2394 | ReplaceBlackBoxDumpByBackupMove
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(backupPath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs:2426 | TryDeleteBlackBoxDumpPath
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(targetPath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:1666 | DumpAutopsyReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:1668 | DumpAutopsyReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuRadiationMutationJobs.cs:422 | PatchRadiationMutationTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct PatchRadiationMutationTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuRadiationMutationRuntime.cs:631 | DumpAutopsyReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuRadiationMutationRuntime.cs:633 | DumpAutopsyReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs:1339 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs:1340 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuSensoryImpairmentJobs.cs:398 | PatchSensoryTelemetryGasJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct PatchSensoryTelemetryGasJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuSensoryImpairmentRuntime.cs:650 | DumpAutopsyReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuSensoryImpairmentRuntime.cs:652 | DumpAutopsyReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuSuitIntegrityRuntime.cs:788 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuSuitIntegrityRuntime.cs:790 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs:3533 | DumpPowerBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs:3535 | DumpPowerBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Power/PowerGridJacobiContracts.cs:735 | RecordPowerTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordPowerTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Power/PowerGridSolarContracts.cs:761 | RecordSolarTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordSolarTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/PowerGridSolarContracts.cs:1715 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/PowerGridSolarContracts.cs:1726 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1731 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1733 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs:2398 | ThermalGridTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private unsafe struct ThermalGridTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs:1691 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs:1693 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:100 | _blackBox
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `private NativeArray<WfcOutpostPowerBootTelemetryEntry> _blackBox => ResolveBuffer(in _blackBoxHandle);`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:908 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:910 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:297 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(_csvPath));`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:991 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(_dumpPath));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:992 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read)) // COLD ALLOC: FileStream[1] — crash blackbox dump — owner: QAEnduranceWatchdogBot`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:1033 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(_resultPath));`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:1135 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(_path));`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:1136 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_stream = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous); // COLD ALLOC: FileStream[1] — async CSV file sink — owner: QAEnduranceCsvWriter`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Quest/QuestDagDataLoading.cs:61 | .ctor
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(binaryPath, FileMode.Open, FileAccess.Read, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs:1278 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs:1280 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Quest/QuestGraphEvaluator.cs:38 | _pendingSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<QuestSignalPayload> _pendingSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Quest/QuestStateManager.cs:1540 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.AppendAllText(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs:1216 | TryWriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs:1217 | TryWriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs:416 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs:438 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs:457 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs:484 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/LutArrayResolver.cs:219 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(cacheDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/LutArrayResolver.cs:298 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/LutArrayResolver.cs:347 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/LutArrayResolver.cs:440 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs:3466 | EntityDeltaTelemetryRecordJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct EntityDeltaTelemetryRecordJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs:3515 | EntityDeltaDiskLatencyTelemetryPatchJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct EntityDeltaDiskLatencyTelemetryPatchJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs:2729 | TelemetryWriteJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct TelemetryWriteJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs:1918 | VoxelDeltaTelemetryRecordJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct VoxelDeltaTelemetryRecordJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs:1970 | VoxelDeltaDiskLatencyTelemetryPatchJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct VoxelDeltaDiskLatencyTelemetryPatchJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs:1303 | TryDumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(path));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs:1304 | TryDumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsJobs.cs:791 | ThermalTelemetryRecorderJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct ThermalTelemetryRecorderJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.cs:1245 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.cs:1255 | WriteDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.ReactorBridge.cs:140 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.ReactorBridge.cs:892 | WriteReactorDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Thermodynamics/ReactorThermalGridJobs.cs:1164 | NuclearReactorTelemetryRecorderJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct NuclearReactorTelemetryRecorderJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Thermodynamics/ReactorThermalGridJobs.cs:1287 | ReactorTelemetryRecorderJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct ReactorTelemetryRecorderJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:1758 | ScanTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ScanTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:1346 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:1348 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.FileWorker.cs:326 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, ConfigFileStreamBufferBytes, FileOptions.SequentialScan);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.FileWorker.cs:352 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, ConfigFileStreamBufferBytes, FileOptions.SequentialScan);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Tools/UpgradeMatrixCompiler.cs:1258 | RecordUpgradeTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordUpgradeTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs:872 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs:874 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:2646 | TelemetryWriteJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct TelemetryWriteJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:1452 | LoadGlitchTableCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_glitchTableFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:2049 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:2062 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpFullPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:1458 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(dumpPath));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:1459 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs:712 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs:714 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:741 | DumpTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:743 | DumpTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(TelemetryDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs:2216 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs:2230 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/PdaH8lrLoreStore.cs:218 | TryOpenMemoryMapped
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2274 | RenderRadarPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_radarRuntimeMaterial.SetFloat(HectonRadarProceduralId, 1f);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2275 | RenderRadarPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_radarRuntimeMaterial.SetFloat(HectonRadarGprProceduralId, 1f);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2281 | RenderRadarPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_radarRuntimeMaterial.SetVector(`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2916 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2918 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2965 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1805 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1807 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime_PdaScreenProjector.cs:1172 | DumpPdaProjectionBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime_PdaScreenProjector.cs:1174 | DumpPdaProjectionBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs:1534 | DumpCameraJuiceTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs:1536 | DumpCameraJuiceTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/VFX/CameraJuiceSystem_CameraJuiceBurst.cs:934 | InitializeCameraJuiceTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct InitializeCameraJuiceTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:3728 | DispatchWakeProximityInjection
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `marineSnowCompute.SetTexture(_wakeProximityKernel, ShaderIds.CaveVoxelSdfTexId, sdfTexture);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:3730 | DispatchWakeProximityInjection
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `marineSnowCompute.SetTexture(_wakeProximityKernel, ShaderIds.TerrainHeightTextureId, heightTexture);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:3761 | DispatchParticleInitializationIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `marineSnowCompute.SetVector(ShaderIds.InitializationParamsId, new Vector4(_allocatedParticleCapacity, 0f, 0f, 0f));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:4770 | TryWriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:4772 | TryWriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/PropwashGpuContracts.cs:865 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/PropwashGpuContracts.cs:867 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:1383 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:1385 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:1567 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs:2233 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs:2235 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonBiosDiagnosticFeature.cs:162 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.IntensityId, intensity);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonBiosDiagnosticFeature.cs:168 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.LootActiveId, lootActive);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonBiosDiagnosticFeature.cs:174 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetVector(ShaderConstants.LootSphereId, state.LootSphereAup);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonBiosDiagnosticFeature.cs:180 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.DitherStrengthId, ditherStrength);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonBiosDiagnosticFeature.cs:186 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.ScanlineStrengthId, scanlineStrength);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs:181 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetVector(ShaderConstants.OriginRadiusId, originRadius);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs:187 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetVector(ShaderConstants.RightDepthId, rightDepth);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs:193 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetVector(ShaderConstants.UpAgeId, upAge);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs:199 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetVector(ShaderConstants.ForwardIntensityId, forwardIntensity);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs:205 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetColor(ShaderConstants.ColorId, settings.projectionColor);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs:211 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.GridScaleId, gridScale);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs:217 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.DitherCutoffId, ditherCutoff);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs:223 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.FlickerSpeedId, flickerSpeed);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs:445 | UpdateMaterialParameters
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.PersistenceSecondsId, math.max(0.05f, settings.persistenceSeconds));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs:446 | UpdateMaterialParameters
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.PointDensityId, math.max(0.05f, settings.pointDensity));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs:447 | UpdateMaterialParameters
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.PointBoostId, math.max(0f, settings.pointBoost));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs:448 | UpdateMaterialParameters
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.HasHistoryId, historyValid ? 1f : 0f);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs:449 | UpdateMaterialParameters
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.WorldPersistenceSecondsId, math.max(0.05f, settings.worldPersistenceSeconds));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs:450 | UpdateMaterialParameters
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.WorldPointRadiusId, math.max(0.05f, settings.worldPointRadius));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs:451 | UpdateMaterialParameters
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.HasWorldHistoryId, worldHistoryValid ? 1f : 0f);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs:452 | UpdateMaterialParameters
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetVector(ShaderConstants.WorldMemoryRectId, worldMemoryRect);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs:453 | UpdateMaterialParameters
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetVector(ShaderConstants.WorldScrollUvOffsetId, new Vector4(worldScrollUvOffset.x, worldScrollUvOffset.y, 0f, 0f));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs:454 | UpdateMaterialParameters
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetVector(ShaderConstants.WorldOriginOffsetId, new Vector4(floatingOriginOffset.x, floatingOriginOffset.y, floatingOriginOffset.z, 0f));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorARStencilRendererFeature.cs:1286 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorARStencilRendererFeature.cs:1288 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs:1563 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs:1565 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs:1679 | TryDumpReconstructionTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs:1681 | TryDumpReconstructionTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.Noir.cs:1063 | TryDumpNoirTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.Noir.cs:1065 | TryDumpNoirTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs:1662 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs:1666 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs:728 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/SpectrumSystem.cs:3375 | DumpActiveSonarGeoTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/SpectrumSystem.cs:3377 | DumpActiveSonarGeoTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs:1017 | RecordBiomeTransitionTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordBiomeTransitionTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs:3216 | ChemicalTelemetryWriteJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private unsafe struct ChemicalTelemetryWriteJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs:2071 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs:2073 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:3783 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:5133 | DumpMacroSwarmBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:5135 | DumpMacroSwarmBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:5326 | DumpFaunaGeneticsTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:5328 | DumpFaunaGeneticsTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:5370 | DumpFaunaMutationBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:5372 | DumpFaunaMutationBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:6266 | DumpBiomassBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:6268 | DumpBiomassBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:4801 | DumpWakeBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:4803 | DumpWakeBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:4952 | DumpFloraSwayFieldBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:4954 | DumpFloraSwayFieldBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:8265 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetTexture(_wakeTrailSimulationKernel, _WakeTrailSourceId, _wakeTrailRead);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:8266 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetTexture(_wakeTrailSimulationKernel, _WakeTrailResultId, _wakeTrailWrite);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:8269 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetVector(_WakeTrailScrollUvOffsetId, new Vector4(_pendingWakeTrailScrollUv.x, _pendingWakeTrailScrollUv.y, 0f, 0f));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:8270 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetFloat(_WakeTrailFadeDeltaId, Mathf.Max(0f, fade));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:8271 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetFloat(_WakeTrailDiffusionId, _wakeTrailDiffusion);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:8272 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetFloat(_WakeTrailWaveStrengthId, _wakeTrailWaveStrength);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:8273 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetFloat(_WakeTrailDampingId, _wakeTrailWaveDamping);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:8274 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetFloat(_WakeTrailCurlStrengthId, _wakeTrailCurlStrength);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:8275 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetFloat(_WakeTrailSimulationTimeId, GetCurrentSimulationTimeSeconds());`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:8277 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetVector(`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:9529 | DumpFloraMemoryTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:9564 | DumpFloraMemoryTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllBytes(fullPath, work.Payload);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:9584 | WriteFloraMemoryTelemetryDumpQueued
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllBytes(work.Path, work.Payload);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:1241 | TryDumpTelemetryBuffer
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:1244 | TryDumpTelemetryBuffer
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GPUScatterDirector.cs:1388 | DumpScatterBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GPUScatterDirector.cs:1390 | DumpScatterBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs:270 | Render
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(GroundRadarPulseId, _pulsePhaseSeconds);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs:271 | Render
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(GroundRadarScaleId, math.max(0.1f, ringScaleMeters));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs:1101 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read); // COLD ALLOC: FileStream[GPR telemetry dump] — blackbox dump file writer — owner: TERRAIN_GPR_SYSTEM`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2685 | DispatchFloraSnapFlagUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_abyssalFlowFieldCompute.SetVector(_GlobalFloatingOffsetId, globalFloatingOffset);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2686 | DispatchFloraSnapFlagUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_abyssalFlowFieldCompute.SetVector(_SubmarineWashSphereId, washSphere);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2687 | DispatchFloraSnapFlagUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_abyssalFlowFieldCompute.SetVector(_SubmarineWashVelocityId, washVelocity);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3245 | DumpFloraGrowthTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3247 | DumpFloraGrowthTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3439 | TryWriteScatterCullTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3441 | TryWriteScatterCullTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:4506 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(_indexedSectorOverrideDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][66%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ALLOCATION_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:4644 | RunIndexedSectorPagingAsync
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `stagedRecords = loadedCount > 0 ? new PersistentWorldDeltaRecord[loadedCount] : Array.Empty<PersistentWorldDeltaRecord>();`
  Required action: Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:5770 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(state.EntityStateTempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:6086 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(state.EntityStateTempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:6396 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(entityStateTempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:6576 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(entityStateTempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:6757 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(entityStateTempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:8376 | DumpWorldTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:8378 | DumpWorldTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(WorldTelemetryDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:4514 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:4516 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6072 | TryDumpFoodChainTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6074 | TryDumpFoodChainTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6237 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6240 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/SpawnZoneSdfValidation.cs:586 | SpawnValidationTelemetryReduceJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct SpawnValidationTelemetryReduceJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/SpawnZoneSdfValidation.cs:1022 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/SpawnZoneSdfValidation.cs:1024 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/SpawnZoneSdfValidation.cs:1326 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs:1707 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `stream = new FileStream(handle, FileAccess.Read, 64 * 1024, true); // BACKGROUND_WORKER_IO_1305_STREAMING: SafeFileHandle stream only.`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs:1725 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `stream = new FileStream(handle, FileAccess.Read, 64 * 1024, true); // BACKGROUND_WORKER_IO_1305_STREAMING: SafeFileHandle stream only.`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs:2230 | DumpTelemetryOnWorker
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpFullPath, FileMode.Create, FileAccess.Write, FileShare.Read)) // BLACKBOX_DUMP_1305_STREAMING: worker-only fault dump.`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs:2267 | PrepareDumpPathCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dir);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/VegetationDensityQueryService.cs:1484 | FlushVegetationAudioHandoffVisualSync
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `vegetationAudioMixer.SetFloat(vegetationDensityMixerParameter, density);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/VegetationDensityQueryService.cs:1487 | FlushVegetationAudioHandoffVisualSync
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `vegetationAudioMixer.SetFloat(vegetationAcousticTypeMixerParameter, (float)acousticType);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VegetationMemorySovereigntyRuntime.cs:508 | DumpVegetationMemoryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VegetationMemorySovereigntyRuntime.cs:510 | DumpVegetationMemoryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs:2027 | DumpAbyssalPathTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs:2029 | DumpAbyssalPathTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:864 | VolcanicTelemetryFinalizeJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct VolcanicTelemetryFinalizeJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:1623 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 256, FileOptions.SequentialScan);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:2128 | DumpBlackBoxIfFaulted
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:2130 | DumpBlackBoxIfFaulted
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Create(path);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:5819 | DumpTelemetryToPath
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:5821 | DumpTelemetryToPath
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:1713 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:1715 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanCognitionVault.cs:372 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanCognitionVault.cs:376 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanCognitionVault.cs:673 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs:1200 | TryWriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs:1203 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs:1284 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionAnxietyJobs.cs:178 | RecordAnxietyTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordAnxietyTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionJobs.cs:350 | RecordCognitionTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordCognitionTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs:939 | TryWriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs:942 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs:996 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault_AnxietyDecay.cs:784 | TryWriteAnxietyDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault_AnxietyDecay.cs:787 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault_AnxietyDecay.cs:841 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs:1085 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs:1087 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:5024 | CountTelemetryCountersJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct CountTelemetryCountersJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1445 | LoadFileIntoNativeScratch
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, share, math.max(1, limit), FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:2719 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:2721 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.FlockingAvoidance.cs:265 | DumpFlockingBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.FlockingAvoidance.cs:267 | DumpFlockingBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuSpatialGridSolver.cs:1459 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(ownerDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuSpatialGridSolver.cs:1462 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(agentDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuSpatialGridSolver.cs:1712 | TryWriteQueuedDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs:1016 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs:1021 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime_VoxelAStar.cs:810 | TryDumpVoxelAStarBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime_VoxelAStar.cs:815 | TryDumpVoxelAStarBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:1003 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:1005 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Animation/FaunaProcedural/ProceduralBoneBlenderJobs.cs:323 | ProceduralBoneTelemetryReduceJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct ProceduralBoneTelemetryReduceJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorJobs.cs:764 | KineticAnimationTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct KineticAnimationTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorTypes.cs:866 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorTypes.cs:868 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:1226 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(BlackBoxDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:1253 | PrepareBlackBoxDumpDirectoryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(BlackBoxDumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationRuntime.cs:1029 | TryDumpTelemetryToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationRuntime.cs:1034 | TryDumpTelemetryToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationRuntime.cs:1041 | TryDumpTelemetryToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(tempPath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationRuntime.cs:1178 | CopyFileIntoScratchCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1430 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1432 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][88%][CACHELINE_CRITICAL_TELEMETRY_DEBT] CACHELINE_CRITICAL_SIGNAL_STRIDE_DEBT | Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs:237 | VocalCueSignal
  Evidence kind: CONFIGURE_CACHELINE_CRITICAL_CALL
  Evidence: `SignalBus<VocalCueSignal>.ConfigureCacheLineCritical(64, 64, 16, VocalCueLaneHash);`
  Required action: This cache-line-critical lane currently has a payload stride outside 64/128 bytes. Keep telemetry flag bit 32 active and migrate to a 64/128-byte payload or split gameplay truth from visual sidecar before raising cadence.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs:922 | TryLoadBankIntoVaultCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs:1304 | DumpBlackboxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs:1306 | DumpBlackboxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs:417 | RequestBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs:439 | RequestBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Bucketing/ModuloSimulationBucketer.cs:815 | TryDumpBlackBoxIfRequested
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Bucketing/ModuloSimulationBucketer.cs:817 | TryDumpBlackBoxIfRequested
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(BlackBoxDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Content/ContentLoreBinaryProvider.cs:146 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_fallbackStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferBytes);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs:1715 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs:1717 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/BabelDictionaryStore.cs:158 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.RandomAccess);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Data/BabelDictionaryStore.cs:512 | LoadFileIntoPaddedBufferCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:1394 | FlushBTreeTelemetryPostSimulationJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct FlushBTreeTelemetryPostSimulationJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:2403 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:2419 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:2444 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:2460 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/StaticDataStore.cs:110 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.RandomAccess);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/StaticDataStore.cs:134 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:244 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_fileStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:293 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:306 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_fileStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:915 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:919 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:1442 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(tempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1515 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1521 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1599 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, ReplayBlockBytes * 4, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1919 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_replayStream = new FileStream(replayPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:701 | DumpBlackBoxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:703 | DumpBlackBoxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][91%][CONFIRMED_ROOT_ALLOCATOR_TELEMETRY] LOCAL_NATIVE_TELEMETRY_RING_ROOT_OWNER | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:479 | _defragBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_H8MEMORY_SCAN
  Evidence: `private NativeArray<MemoryDefragTelemetryEntry> _defragBlackBox;`
  Required action: This telemetry ring belongs to the H8Memory/GlobalDataVault root allocation layer. Keep dispose coverage, but do not classify the root owner itself as a downstream private non-vault breach.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:4127 | DumpDefragBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:4151 | DumpPhiVodBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:4175 | DumpShinobu202BlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:4233 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:4270 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:4272 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][91%][CONFIRMED_ROOT_ALLOCATOR_TELEMETRY] LOCAL_NATIVE_TELEMETRY_RING_ROOT_OWNER | Assets/_Project/Scripts/Core/Memory/H8Memory.cs:2344 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_H8MEMORY_SCAN
  Evidence: `private static NativeArray<H8MemoryTelemetryEntry> _blackBox;`
  Required action: This telemetry ring belongs to the H8Memory/GlobalDataVault root allocation layer. Keep dispose coverage, but do not classify the root owner itself as a downstream private non-vault breach.
- [INFO][91%][CONFIRMED_ROOT_ALLOCATOR_TELEMETRY] LOCAL_NATIVE_TELEMETRY_RING_ROOT_OWNER | Assets/_Project/Scripts/Core/Memory/H8Memory.cs:2345 | _eventBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_H8MEMORY_SCAN
  Evidence: `private static NativeArray<H8MemoryTelemetryEntry> _eventBlackBox;`
  Required action: This telemetry ring belongs to the H8Memory/GlobalDataVault root allocation layer. Keep dispose coverage, but do not classify the root owner itself as a downstream private non-vault breach.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/H8Memory.cs:3677 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/H8Memory.cs:3700 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/VaultLegacyBinaryArchaeology.cs:215 | TryReadHeader
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/VaultMemoryContracts.cs:304 | TryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/VaultMemoryContracts.cs:306 | TryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Origin/AupOriginShiftCoordinator.cs:1363 | WriteOriginShiftDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Origin/AupOriginShiftCoordinator.cs:1365 | WriteOriginShiftDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, DumpWriteBufferBytes, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Core/Origin/AupPrecisionJobs.cs:580 | AupPrecisionTelemetryFoldJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct AupPrecisionTelemetryFoldJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Origin/AupPrecisionJobs.cs:388 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Origin/AupPrecisionJobs.cs:390 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Scheduling/BurstTokenBucketJobAdmissionService.cs:829 | DumpAdmissionBlackboxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Scheduling/BurstTokenBucketJobAdmissionService.cs:831 | DumpAdmissionBlackboxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(AdmissionBlackboxDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][88%][CACHELINE_CRITICAL_TELEMETRY_DEBT] CACHELINE_CRITICAL_SIGNAL_STRIDE_DEBT | Assets/_Project/Scripts/Core/Signals/GlobalSignals.RuntimeLifecycle.cs:850 | TetherTensionSignal
  Evidence kind: CONFIGURE_CACHELINE_CRITICAL_CALL
  Evidence: `SignalBus<TetherTensionSignal>.ConfigureCacheLineCritical(128, laneHash: ComputeStableSignalLaneHash(nameof(TetherTensionSignal)));`
  Required action: This cache-line-critical lane currently has a payload stride outside 64/128 bytes. Keep telemetry flag bit 32 active and migrate to a 64/128-byte payload or split gameplay truth from visual sidecar before raising cadence.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:43 | _laneDispatch
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<SignalLaneDispatch> _laneDispatch;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:164 | TryLoadFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:900 | DumpToDiskAtPath
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:902 | DumpToDiskAtPath
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1070 | TryLoad
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:2989 | DumpToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:2991 | DumpToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:37 | _cursor
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<SignalRingCursorState> _cursor;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:174 | _cursor
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<SignalRingCursorState> _cursor;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:296 | _cursor
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[NativeDisableParallelForRestriction] private NativeArray<SignalRingCursorState> _cursor;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:309 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory(cacheDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:400 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:1848 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:2637 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory(folder);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:2672 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/AssemblyGuard/CompileWallXRayWindow.cs:1614 | _entries
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<CompileWallBlackBoxEntry> _entries;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][55%][EDITOR_ONLY_REVIEW] EDITOR_SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeSupport.cs:17 | InteriorClutterBlackBoxSession
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct InteriorClutterBlackBoxSession : IDisposable`
  Required action: Editor/test signal-like structs do not gate runtime, but should not shadow production contracts.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeSupport.cs:19 | _ring
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<InteriorClutterTelemetryEntry> _ring;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [WARN][60%][EDITOR_ONLY_REVIEW] EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW | Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeSupport.cs:63 | InteriorClutterBlackBoxSession
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `internal void RecordFailure(string sourcePath, InteriorClutterWarningFlags flags)`
  Required action: Editor/test signal-like structs can use managed strings, but must not become runtime payload contracts.
- [WARN][60%][EDITOR_ONLY_REVIEW] EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW | Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeSupport.cs:71 | InteriorClutterBlackBoxSession
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `internal unsafe void Dump(string reason)`
  Required action: Editor/test signal-like structs can use managed strings, but must not become runtime payload contracts.
- [WARN][60%][EDITOR_ONLY_REVIEW] EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW | Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeSupport.cs:76 | InteriorClutterBlackBoxSession
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `string path = "Docs/AgentLogs/Dump_SHINOBU_211.bin";`
  Required action: Editor/test signal-like structs can use managed strings, but must not become runtime payload contracts.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/TextureChannelPacker/HectonArmTextureChannelPacker.cs:1301 | _ring
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<TexturePackerTelemetryEntry> _ring;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentJobs.cs:434 | RecordAuxiliaryTelemetryPass
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordAuxiliaryTelemetryPass`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentRouterRuntime.cs:1403 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentRouterRuntime.cs:1407 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationContracts.cs:441 | TryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationContracts.cs:445 | TryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationJobs.cs:669 | RecordAirlockTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordAirlockTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs:2297 | BallisticsTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct BallisticsTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs:1258 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:316 | _signalDetails
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<CombatDamageSignalDetail> _signalDetails;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:337 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `private static NativeArray<CombatTelemetryEntry> _telemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:1469 | TryDumpCombatTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:1471 | TryDumpCombatTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs:60 | _statusEffectTelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `private static NativeArray<CombatStatusEffectTelemetryEntry> _statusEffectTelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs:64 | _statusEffectDamageSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<CombatDamageSignal> _statusEffectDamageSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs:1538 | TryDumpStatusEffectTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs:1540 | TryDumpStatusEffectTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:1864 | DumpTelemetryBuffer
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(dumpPath));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:1865 | DumpTelemetryBuffer
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs:1516 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs:1518 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Graphics/Culling/AbyssalShadowCullingJobs.cs:477 | ReduceShadowCullTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct ReduceShadowCullTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/AbyssalShadowCullingTypes.cs:509 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/AbyssalShadowCullingTypes.cs:513 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:627 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:629 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs:781 | TryLoadBinaryLimitFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs:807 | TryLoadTextureBudgetFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs:1618 | Dump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs:1620 | Dump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Materials/ShinobuMaterialResponseRuntime.cs:1380 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Materials/ShinobuMaterialResponseRuntime.cs:1382 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs:2425 | RecordVisualAgingTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct RecordVisualAgingTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs:1560 | OpenDumpStreamCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs:1563 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `return new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, TelemetryDumpSnapshotBytes, FileOptions.WriteThrough);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:1943 | DumpBlackBoxOnceLocked
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:1985 | ResolveBlackBoxDumpPathCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:1183 | TryOpenDumpPath
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:1185 | TryOpenDumpPath
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs:2168 | RecordInventoryTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordInventoryTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs:1076 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs:1078 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/DynamicPointLightCulling/DynamicPointLightCullingDirector.cs:552 | DumpBlackBoxNow
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/DynamicPointLightCulling/DynamicPointLightCullingDirector.cs:554 | DumpBlackBoxNow
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:773 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(path));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:774 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs:1191 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs:1193 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs:614 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs:617 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AnalyticalGerstnerWaveJobs.cs:442 | RecordWaveMathTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordWaveMathTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementJobs.cs:1022 | ReduceBuoyancyTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct ReduceBuoyancyTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/BuoyancySimdVectorization.cs:1166 | RecordSimdTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordSimdTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/Cable132/CablePhysicsSolver132.cs:1593 | RecordTetherTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal unsafe struct RecordTetherTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/Cable132/CablePhysicsSolver132.cs:520 | TryDumpLatestVault
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/Cable132/CablePhysicsSolver132.cs:679 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs:2221 | RecordShockwaveTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct RecordShockwaveTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs:2036 | KinematicTelemetryAggregateJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct KinematicTelemetryAggregateJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs:2110 | KccEnvironmentTelemetryAggregateJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct KccEnvironmentTelemetryAggregateJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsJobs.cs:504 | ReduceSeaglideTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct ReduceSeaglideTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:1200 | RecordGyroTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct RecordGyroTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Plugins/Crest/HectonCrestOceanDepthCacheRuntimeBridge.cs:92 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Plugins/Crest/HectonCrestOceanDepthCacheRuntimeBridge.cs:115 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read, 65536, FileOptions.SequentialScan);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/BatteryChargerLogistics/BatteryChargerLogisticsRuntime.cs:1565 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/BatteryChargerLogistics/BatteryChargerLogisticsRuntime.cs:1567 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs:1161 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs:1163 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs:1066 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs:1069 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:909 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/AbyssalCaustics/AbyssalDeferredCausticsRuntime.cs:1459 | EnsureBlackBoxDumpPathCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(_blackBoxDumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/AbyssalCaustics/AbyssalDeferredCausticsRuntime.cs:1481 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerRuntime.cs:1202 | EnsureBlackBoxDumpPathCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(_blackBoxDumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerRuntime.cs:1523 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/OceanSinglePass/OceanSinglePassContracts.cs:462 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/OceanSinglePass/OceanSinglePassContracts.cs:464 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/OceanSinglePass/ShorelineFoamGraftContracts.cs:997 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/OceanSinglePass/ShorelineFoamGraftContracts.cs:999 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1990 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1992 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/WaterOptics/WaterOpticsRuntime.cs:1097 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/WaterOptics/WaterOpticsRuntime.cs:1099 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:1578 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:1580 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:2084 | DispatchDirtyScreens
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `terminalBlitCompute.SetTexture(_blitKernel, TerminalTextureArrayId, _terminalTextureArray);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:2090 | DispatchDirtyScreens
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `terminalBlitCompute.SetTexture(_blitKernel, FontSdfAtlasId, fontSdfAtlas);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:2095 | DispatchDirtyScreens
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `terminalBlitCompute.SetFloat(TimeSeedId, ownerFrame * HectonPhysicsContract.FixedDeltaTimeSeconds);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:2096 | DispatchDirtyScreens
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `terminalBlitCompute.SetFloat(HectonDiegeticGlitchQualityWeightId, _globalQualityWeight);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:3561 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:3563 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:3805 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:3929 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime_TerminalProjection.cs:516 | WriteTerminalInputBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime_TerminalProjection.cs:519 | WriteTerminalInputBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs:1569 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs:1573 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(BlackBoxDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/VR/OpenXRManualOverrideLever.cs:663 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/VR/OpenXRManualOverrideLever.cs:665 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:1586 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, ProfileByteCount, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:3305 | WriteBlackBoxDumpBytes
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:3307 | WriteBlackBoxDumpBytes
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:2086 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:2096 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamContracts.cs:581 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamContracts.cs:583 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:598 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:600 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:1644 | PlasmaBeamArgsTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct PlasmaBeamArgsTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:1229 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:1231 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:1394 | DumpBiolumTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:1396 | DumpBiolumTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs:731 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs:733 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/Biomes/BiomeTransitionManagerRuntime.cs:1229 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/Biomes/BiomeTransitionManagerRuntime.cs:1631 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/Biomes/BiomeTransitionManagerRuntime.cs:1633 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/Contracts/InstanceCullingContracts.cs:35 | InstanceCullingCameraPositionSignal
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct InstanceCullingCameraPositionSignal`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/Contracts/InstanceCullingContracts.cs:47 | InstanceCullingCameraFrustumSignal
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct InstanceCullingCameraFrustumSignal`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/Contracts/InstanceCullingContracts.cs:103 | InstanceCullingTelemetry
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct InstanceCullingTelemetry`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraAmbientSway/FloraAmbientSwayRuntime.cs:964 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraAmbientSway/FloraAmbientSwayRuntime.cs:966 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeContracts.cs:595 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeVaultRuntime.cs:517 | DumpBlackBoxIfFatal
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeVaultRuntime.cs:531 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:1783 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs:667 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dir);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs:1226 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(path))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs:1435 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Create(path))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs:748 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dir);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs:934 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(path))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs:1148 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Create(path))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:2624 | RenderDormantOres
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `oreMaterial.SetFloat(_QualityOverkillId, visualWeight);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:2935 | TryWriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read); // COLD ALLOC: FileStream[telemetry dump] — blackbox dump file writer — owner: ProceduralOreSpawner`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs:898 | RegrowthTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct RegrowthTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs:583 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs:594 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllBytes(path, dump);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:686 | ReadColdBytes
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:993 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(_dumpPath));`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:994 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:2668 | PoiBlackBoxValidationJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct PoiBlackBoxValidationJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:686 | TryDumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:688 | TryDumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs:1343 | TryLoadLookupFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs:1613 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Create(path))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs:1628 | TryWriteDumpFiles
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dir);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:1574 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:1578 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1371 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1372 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureBakeBlackBox.cs:16 | _ring
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<AITextureBakeTelemetryEntry> _ring;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBakeBlackBox.cs:23 | _ring
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<OfflineGeometryBakeTelemetryEntry> _ring;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/BaseStructuralWarningDispatcherTypes.cs:758 | WriteStructuralWarningTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct WriteStructuralWarningTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/BaseStructuralWarningDispatcherTypes.cs:1390 | DumpBaseStructuralWarningTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/BaseStructuralWarningDispatcherTypes.cs:1405 | DumpBaseStructuralWarningTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Create(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:2230 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:2232 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:2259 | WriteDeformationTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:2261 | WriteDeformationTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorRuntime.cs:1543 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorRuntime.cs:1558 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Create(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorTypes.cs:848 | StructuralTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct StructuralTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Physics/KCC/Editor/Shinobu355KccSmokeEditorFacade.cs:525 | _telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<HydrodynamicKccRuntime.KccSmokeTelemetryEntry> _telemetry;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][55%][EDITOR_ONLY_REVIEW] EDITOR_SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/Automation/SubmarineAutopilotSdfNavigator.cs:1299 | RecordAutopilotTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct RecordAutopilotTelemetryJob : IJob`
  Required action: Editor/test signal-like structs do not gate runtime, but should not shadow production contracts.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Plugins/Crest/OceanKinematics/OceanKinematicsVaultRuntime.cs:208 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Plugins/Crest/OceanKinematics/OceanKinematicsVaultRuntime.cs:213 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs:562 | _telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<PowerJacobiStressFrameTelemetry> _telemetry;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs:569 | _fuzzTelemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<JacobiFuzzTelemetryEntry> _fuzzTelemetry;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs:254 | _telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<HadalTrenchBakeTelemetryEntry> _telemetry;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][55%][EDITOR_ONLY_REVIEW] EDITOR_SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/StaticCaveSdfBaker/Editor/StaticCaveSdfBakePipeline.cs:993 | StaticCaveSdfBakeTelemetryBuffer
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal ref struct StaticCaveSdfBakeTelemetryBuffer`
  Required action: Editor/test signal-like structs do not gate runtime, but should not shadow production contracts.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Editor/DamageBake/HabitatDamageBakePipeline.cs:688 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<HabitatDamageBakeTelemetryEntry> _telemetryRing;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/CrashTelemetryBuffer.cs:220 | TelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct TelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][68%][EDITOR_ONLY_REVIEW] EDITOR_SIGNAL_NAME_SHADOWS_RUNTIME | Assets/_Project/Scripts/Editor/BlackBoxBinaryReader.cs:40 | TelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct TelemetryEntry`
  Required action: Editor/test structs should not shadow runtime signal names; rename smoke payloads or fully isolate them.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:84 | OceanSurfaceTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct OceanSurfaceTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereContracts.cs:88 | OceanSurfaceTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct OceanSurfaceTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:401 | StructuralTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct StructuralTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorTypes.cs:16 | StructuralTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct StructuralTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsTypes.cs:130 | AtmosphereTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct AtmosphereTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs:3378 | AtmosphereTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct AtmosphereTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:2188 | ScanTelemetryJob
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct ScanTelemetryJob`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:1758 | ScanTelemetryJob
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct ScanTelemetryJob`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Audio/AcousticPortalPropagation.cs:278 | AcousticTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct AcousticTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Audio/Virtualization/Contracts/AudioVirtualizationContracts.cs:427 | AcousticTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct AcousticTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Core/InputDeterminismDtos.cs:315 | InputTelemetryEntryDTO
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct InputTelemetryEntryDTO`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Input/Determinism/DeterministicInputContracts.cs:89 | InputTelemetryEntryDTO
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct InputTelemetryEntryDTO`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Core/InputDeterminismDtos.cs:333 | MockCollisionSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockCollisionSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Input/Determinism/DeterministicInputContracts.cs:107 | MockCollisionSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockCollisionSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Core/InputDeterminismDtos.cs:342 | MockToolEquipSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockToolEquipSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Input/Determinism/DeterministicInputContracts.cs:116 | MockToolEquipSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockToolEquipSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Core/InputDeterminismDtos.cs:351 | MockPlayerKinematicsSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockPlayerKinematicsSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Input/Determinism/DeterministicInputContracts.cs:125 | MockPlayerKinematicsSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockPlayerKinematicsSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:185 | MockAcousticSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockAcousticSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/VFX/VolumetricSiltContracts.cs:45 | MockAcousticSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockAcousticSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Physics/HabitatFluidIncursionContracts.cs:129 | FluidIncursionTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct FluidIncursionTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Core/Contracts/Physics/HabitatFluidIncursionContracts.cs:129 | FluidIncursionTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct FluidIncursionTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Physics/HabitatFluidIncursionContracts.cs:150 | FluidCompartmentTelemetryDTO
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct FluidCompartmentTelemetryDTO`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Core/Contracts/Physics/HabitatFluidIncursionContracts.cs:150 | FluidCompartmentTelemetryDTO
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct FluidCompartmentTelemetryDTO`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Physics/HarpoonTensionSolver328.cs:1877 | RecordTetherTelemetryJob
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct RecordTetherTelemetryJob`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Physics/Cable132/CablePhysicsSolver132.cs:1593 | RecordTetherTelemetryJob
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct RecordTetherTelemetryJob`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyData.cs:572 | MockPressureSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockPressureSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/DepthStressGranularSynthesisKernel.cs:99 | MockPressureSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockPressureSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyData.cs:608 | MockCombatDamageSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockCombatDamageSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:490 | MockCombatDamageSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockCombatDamageSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:114 | MockCombatDamageSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockCombatDamageSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityTypes.cs:278 | MockCombatDamageSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockCombatDamageSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs:3090 | PagerTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct PagerTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/World/TerrainChunkPagerTypes.cs:210 | PagerTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct PagerTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs:2729 | TelemetryWriteJob
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct TelemetryWriteJob`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:2646 | TelemetryWriteJob
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct TelemetryWriteJob`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardTypes.cs:242 | ThermalTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct ThermalTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/World/AbyssalThermalManager.cs:114 | ThermalTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct ThermalTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:81 | MockDepthSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockDepthSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:65 | MockDepthSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockDepthSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityTypes.cs:298 | MockDepthSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockDepthSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:49 | MockPredatorProximitySignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockPredatorProximitySignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:98 | MockPredatorProximitySignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockPredatorProximitySignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:74 | MockTensionSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockTensionSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/DepthStressGranularSynthesisKernel.cs:119 | MockTensionSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockTensionSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Core/Contracts/DrsContracts.cs:298 | MockQualityWeightSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockQualityWeightSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs:60 | MockQualityWeightSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockQualityWeightSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.

## Non-Claims

- This audit does not prove Unity import, player build, IL2CPP, runtime GC, profiler, scene wiring, or actual struct sizeof(T).
- Static confidence is not semantic proof. This CLI intentionally stays outside Unity and uses standard .NET only.
- This audit reports contract debt only. It does not modify runtime contracts.
