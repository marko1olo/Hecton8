# SHINOBU_02 Signal Bus Contract Audit CLI

Evidence Class: STATIC_SOURCE_CLASSIFIED
Scope: Full
Generated UTC: 2026-05-27T16:37:46.3828309Z

## Summary

- Files scanned: 2441 C# / 71 compute
- Signal-like definitions found: 804
- Signal definitions still in Core/GlobalSignals.cs: 0
- Pack=1 layouts: 0
- Runtime signal Pack=1 layouts: 0
- Runtime signal transitive Pack=1 field hits: 0
- Signal-like definitions without nearby StructLayout: 4
- Managed event surface hits: 0
- Local native telemetry ring hits: 128
- Registered local telemetry rings: 14
- Local native signal queue hits: 18
- Compute 1024-thread-group hits: 0
- Hot-path heuristic hits: 225
- Cold/fatal sync I/O review hits: 553
- Assembly contract boundary hits: 0
- Cache-line-critical stride debt hits: 1
- Errors: 0
- Warnings: 245
- Infos: 1082
- Confirmed/probable errors at confidence >= 90: 0
- Review-only findings below confidence 75: 1243

## Rule Breakdown

- COLD_OR_FATAL_SYNC_IO_REVIEW: total 553, errors 0, warnings 0, infos 553, avg confidence 64
- LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW: total 180, errors 0, warnings 0, infos 180, avg confidence 54
- GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW: total 114, errors 0, warnings 0, infos 114, avg confidence 54
- LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY: total 90, errors 0, warnings 90, infos 0, avg confidence 73
- SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW: total 69, errors 0, warnings 69, infos 0, avg confidence 64
- JOB_STRUCT_LAYOUT_REVIEW: total 69, errors 0, warnings 0, infos 69, avg confidence 54
- RUNTIME_SYNC_FILE_IO_REVIEW: total 65, errors 0, warnings 65, infos 0, avg confidence 76
- LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW: total 64, errors 0, warnings 0, infos 64, avg confidence 68
- MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW: total 38, errors 0, warnings 0, infos 38, avg confidence 52
- EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW: total 20, errors 0, warnings 0, infos 20, avg confidence 56
- LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW: total 17, errors 0, warnings 0, infos 17, avg confidence 70
- DUPLICATE_SIGNAL_LIKE_NAME_REVIEW: total 14, errors 0, warnings 14, infos 0, avg confidence 74
- LOCAL_NATIVE_TELEMETRY_RING_OWNER_LOCAL: total 7, errors 0, warnings 0, infos 7, avg confidence 80
- EDITOR_SIGNAL_LAYOUT_REVIEW: total 5, errors 0, warnings 0, infos 5, avg confidence 55
- LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS: total 4, errors 0, warnings 0, infos 4, avg confidence 92
- COLD_OR_ASYNC_ALLOCATION_REVIEW: total 3, errors 0, warnings 0, infos 3, avg confidence 58
- EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW: total 3, errors 0, warnings 3, infos 0, avg confidence 60
- LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT: total 3, errors 0, warnings 3, infos 0, avg confidence 88
- LOCAL_NATIVE_TELEMETRY_RING_ROOT_OWNER: total 3, errors 0, warnings 0, infos 3, avg confidence 91
- EXECUTABLE_STRUCT_LAYOUT_REVIEW: total 2, errors 0, warnings 0, infos 2, avg confidence 54
- LOCAL_NATIVE_TELEMETRY_STAGING_BUFFER_OWNER_LOCAL: total 1, errors 0, warnings 0, infos 1, avg confidence 82
- LOCAL_SIGNAL_QUEUE_DECLARED_ONLY_REVIEW: total 1, errors 0, warnings 0, infos 1, avg confidence 61
- ZERO_GC_HOT_PATH_ENUMERATION_REVIEW: total 1, errors 0, warnings 1, infos 0, avg confidence 72
- CACHELINE_CRITICAL_SIGNAL_STRIDE_DEBT: total 1, errors 0, warnings 0, infos 1, avg confidence 88

## Classification Breakdown

- COLD_OR_FATAL_IO_BOUNDARY: 553
- HOT_PATH_HEURISTIC: 222
- BORROWED_JOB_VIEW_REVIEW: 180
- STATIC_DECLARATION_REVIEW: 91
- BURST_JOB_STRUCT_REVIEW: 69
- IO_PRESSURE_HEURISTIC: 65
- SIGNAL_SCRATCH_REVIEW: 64
- EDITOR_ONLY_REVIEW: 28
- REGISTERED_LOCAL_QUEUE_REVIEW: 17
- STATIC_CONTRACT_REVIEW: 14
- CONFIRMED_OWNER_LOCAL_TELEMETRY: 7
- CONFIRMED_VAULT_ALIAS_REVIEW: 4
- COLD_OR_ASYNC_ALLOCATION_BOUNDARY: 3
- CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL: 3
- CONFIRMED_ROOT_ALLOCATOR_TELEMETRY: 3
- EXECUTABLE_CARRIER_STRUCT_REVIEW: 2
- CONFIRMED_OWNER_LOCAL_TELEMETRY_STAGING: 1
- CACHELINE_CRITICAL_TELEMETRY_DEBT: 1

## Findings

- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/BuilderTool.cs:568 | UpdateScreen
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_screenPropBlock.SetColor(PropScreenColor, screenColor);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ConstructionManager.cs:1644 | DumpShinobu336BlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ConstructionManager.cs:1646 | DumpShinobu336BlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ConstructionManager.cs:2061 | DumpDeconstructionBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ConstructionManager.cs:2063 | DumpDeconstructionBlackBox
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
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/CraftingSystem.FastFail.cs:1091 | Telemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[NoAlias, NativeDisableParallelForRestriction] public NativeArray<CraftingFastFailTelemetryEntry> Telemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fabricator.cs:2018 | WriteFabricatorMemoryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fabricator.cs:2020 | WriteFabricatorMemoryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/Fabricator.cs:3348 | FlushApplyErrorFeedback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_errorFeedbackBlock.SetColor(EmissionColorId, color);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/FlashlightTool.cs:171 | UpdatePowerIndicator
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetFloat(_ToolBatteryNormalizedID, math.saturate(batteryCharge));`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/FlashlightTool.cs:174 | UpdatePowerIndicator
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetColor(_EmissionColorID, Color.black);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonBoidController.cs:1535 | DispatchComputeFrustumCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `boidShader.SetFloat(ShaderProps.BoidCullingRadius, math.max(0.01f, safeFishScale * DefaultBoidCullingRadiusScale));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonBoidController.cs:1987 | DumpBoidBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonBoidController.cs:1989 | DumpBoidBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6141 | UpdateSkyboxBlend
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `blendedSkyboxMaterial.SetFloat(_ID_Blend, _currentBlend);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6157 | UpdateStarIntensity
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `blendedSkyboxMaterial.SetFloat(_ID_StarIntensity, _currentStarIntensity);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6543 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetVector(_ID_FresnelSunDir, new Vector4(toSun.x, toSun.y, toSun.z, 0));`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6544 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_BacklitIntensity, backlitIntensity);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6545 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_EquatorialSpeed, equatorialRotationSpeed);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6546 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_PolarMultiplier, polarRotationMultiplier);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6547 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_PlanetPhase, _currentPhase);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6548 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_StormEmission, stormEmissionIntensity * ResolveStormEmissionMultiplier());`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6549 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_SunBacklitFactor, _currentBacklitFactor);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6550 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_GlobalRotation, _rotationPhase);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6551 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_GameTime, _gameTime);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6552 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_NightBlend, _currentBlend);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6553 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_AtmosphereTransmittanceWeight, _atmosphereTransmittanceWeight);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6554 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_AtmosphereInscatterWeight, _atmosphereInscatterWeight);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6556 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetColor(_ID_SkyColorZenith, _resolvedSkyZenith);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6557 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetColor(_ID_SkyColorHorizon, _resolvedSkyHorizon);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6558 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetColor(_ID_SkyColorNadir, _resolvedSkyNadir);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6561 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetVector(_ID_WindDirection, _skyMaterial.GetVector(_ID_WindDirection));`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6587 | UpdateMoonMaterialOverrides
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_moonMPB.SetFloat(`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6590 | UpdateMoonMaterialOverrides
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_moonMPB.SetFloat(`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6593 | UpdateMoonMaterialOverrides
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_moonMPB.SetFloat(_ID_HectonMoonPhase01, phase01);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6594 | UpdateMoonMaterialOverrides
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_moonMPB.SetFloat(_ID_HectonMoonPhaseTextureIndex, ResolveMoonPhaseTextureIndex(phase01));`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6919 | DumpCelestialBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6921 | DumpCelestialBlackBox
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:3043 | DumpOceanSurfaceTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:3045 | DumpOceanSurfaceTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:3257 | DumpFluidSovereigntyTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:3259 | DumpFluidSovereigntyTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:4889 | WriteFluidAdvectionTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:4891 | WriteFluidAdvectionTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:6674 | DumpMaelstromTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:6676 | DumpMaelstromTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:7831 | DispatchAbyssalVortexImpulses
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `abyssalFlowFieldCompute.SetTexture(_gpuAbyssalVortexKernel, _AbyssalFlowTextureRWId, _gpuAbyssalFlowReadTexture);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:7832 | DispatchAbyssalVortexImpulses
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `abyssalFlowFieldCompute.SetVector(_AbyssalFlowVortexSphereId, sphere);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:7833 | DispatchAbyssalVortexImpulses
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `abyssalFlowFieldCompute.SetVector(_AbyssalFlowVortexAxisStrengthId, axisStrength);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:7977 | DumpAbyssalFlowTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:7979 | DumpAbyssalFlowTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonPlayerMovement.cs:3331 | DumpPlayerKinematicsBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonPlayerMovement.cs:3333 | DumpPlayerKinematicsBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonPlayerMovement.cs:8466 | DumpCinematicFocusBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonPlayerMovement.cs:8468 | DumpCinematicFocusBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:5838 | UpdateHudFogLuminanceDownsample
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `hudFogLuminanceCompute.SetTexture(_hudFogLuminanceKernel, _HectonHudFogSourceId, sourceTexture);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:5839 | UpdateHudFogLuminanceDownsample
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `hudFogLuminanceCompute.SetTexture(_hudFogLuminanceKernel, _HectonHudFogLuminanceOutputId, _hudFogLuminanceTexture);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:5840 | UpdateHudFogLuminanceDownsample
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `hudFogLuminanceCompute.SetVector(`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonVoxelEngine.cs:7734 | WriteVoxelMeshPipelineBlackBoxFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonVoxelEngine.cs:7736 | WriteVoxelMeshPipelineBlackBoxFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/LocalizationManager.cs:1019 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/LocalizationManager.cs:1075 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/LocRegistry.cs:513 | _telemetryFrames
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `private static NativeArray<BabelTelemetryEntry> _telemetryFrames;`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/LocRegistry.cs:3085 | WriteTelemetryDumpFiles
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(docsPath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/LocRegistry.cs:3102 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/LocRegistry.cs:3464 | OutputSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[WriteOnly, NoAlias] public NativeArray<MockTranslationRequestSignal> OutputSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/MathLodApproximation.cs:634 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<MathLodTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/MathLodApproximation.cs:824 | TryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/MathLodApproximation.cs:826 | TryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/ModularEquipmentEngine.cs:172 | EquipmentTelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<EquipmentTelemetryEntry> EquipmentTelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/ModularEquipmentEngine.cs:174 | FlashlightTelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<FlashlightTelemetryEntry> FlashlightTelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/ModularEquipmentEngine.cs:185 | UpgradeTelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<UpgradeTelemetryEntry> UpgradeTelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModularEquipmentEngine.cs:2504 | DumpEquipmentTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModularEquipmentEngine.cs:2506 | DumpEquipmentTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:5140 | DumpSalinityCorrosionBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:5142 | DumpSalinityCorrosionBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:5310 | DumpInventoryBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:5312 | DumpInventoryBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/RepairTool.cs:1999 | DumpRepairBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/RepairTool.cs:2001 | DumpRepairBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/ResourceNode.cs:1158 | UpdateMeltProperties
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_propertyBlock.SetVector(_MeltCenterId, _localHitPoint);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/ResourceNode.cs:1159 | UpdateMeltProperties
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_propertyBlock.SetFloat(_MeltRadiusId, meltRadius);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/RuntimeDiagnosticsTrace.cs:88 | EnsureSession
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][58%][COLD_OR_ASYNC_ALLOCATION_BOUNDARY] COLD_OR_ASYNC_ALLOCATION_REVIEW | Assets/_Project/Scripts/SaveBinaryStorage.cs:428 | FlushPath
  Evidence kind: HOT_METHOD_NAME_WITH_COLD_CONTEXT
  Evidence: `using FileStream stream = new FileStream(`
  Required action: This allocation is inside a cold/save/telemetry/async context despite a hot-looking method name. Keep it outside frame cadence; do not count it as a proven zero-GC hot-path breach without call-cadence proof.
- [INFO][80%][CONFIRMED_OWNER_LOCAL_TELEMETRY] LOCAL_NATIVE_TELEMETRY_RING_OWNER_LOCAL | Assets/_Project/Scripts/SaveManager.cs:208 | SaveTelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<AsyncPersistenceTelemetryEntry> SaveTelemetryRing;`
  Required action: This telemetry/blackbox ring has sentinel ownership, bounded lifetime, and owner-local dump usage. Do not migrate it to GlobalDataVault unless another domain consumes the buffer or the state becomes persistent authority.
- [INFO][80%][CONFIRMED_OWNER_LOCAL_TELEMETRY] LOCAL_NATIVE_TELEMETRY_RING_OWNER_LOCAL | Assets/_Project/Scripts/SaveManager.cs:209 | WfcOutpostTelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<WfcOutpostTelemetryEntry> WfcOutpostTelemetryRing;`
  Required action: This telemetry/blackbox ring has sentinel ownership, bounded lifetime, and owner-local dump usage. Do not migrate it to GlobalDataVault unless another domain consumes the buffer or the state becomes persistent authority.
- [INFO][80%][CONFIRMED_OWNER_LOCAL_TELEMETRY] LOCAL_NATIVE_TELEMETRY_RING_OWNER_LOCAL | Assets/_Project/Scripts/SaveManager.cs:210 | WfcOutpostEventTelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<WfcOutpostTelemetryEntry> WfcOutpostEventTelemetryRing;`
  Required action: This telemetry/blackbox ring has sentinel ownership, bounded lifetime, and owner-local dump usage. Do not migrate it to GlobalDataVault unless another domain consumes the buffer or the state becomes persistent authority.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs:5988 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SubmarineFluidDynamics.cs:6048 | WriteHydroBlackBoxDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SubmarineFluidDynamics.cs:6050 | WriteHydroBlackBoxDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1734 | DispatchLeakPlumeCompute
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `leakPlumeCompute.SetFloat(_LeakDeltaTimeId, safeFixedDeltaTime);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1735 | DispatchLeakPlumeCompute
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `leakPlumeCompute.SetFloat(_LeakTimeId, ResolveLeakPlumeClockSeconds());`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1736 | DispatchLeakPlumeCompute
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `leakPlumeCompute.SetVector(_LeakParamsId, new Vector4(LeakPlumeParticleCapacity, MaxActiveBreaches, 0f, 0f));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1799 | RenderLeakPlumeParticles
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_leakPlumeDrawProperties.SetFloat(_LeakUseParticleBufferId, 1f);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1800 | RenderLeakPlumeParticles
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_leakPlumeDrawProperties.SetFloat(_LeakParticleSizeId, math.max(0.01f, leakPlumeParticleSizeMeters));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1802 | RenderLeakPlumeParticles
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_leakPlumeDrawProperties.SetVector(_LeakCameraRightId, cameraRight);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1803 | RenderLeakPlumeParticles
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_leakPlumeDrawProperties.SetVector(_LeakCameraUpId, cameraUp);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:2106 | WriteDamageControlTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:2108 | WriteDamageControlTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VoxelDeltaProcessor.cs:6531 | WriteBlackBoxDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VoxelDeltaProcessor.cs:6533 | WriteBlackBoxDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs:2090 | DumpTerrainSeamBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs:2092 | DumpTerrainSeamBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs:2231 | DumpVoxelBridgeBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs:2233 | DumpVoxelBridgeBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Animation/LeviathanTerrainIkJobs.cs:164 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Animation/LeviathanTerrainIkJobs.cs:166 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Animation/LeviathanTerrainIkJobs.cs:382 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<LeviathanTerrainIkTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Animation/VRPhysicalHandPresenceIkJobs.cs:337 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Animation/VRPhysicalHandPresenceIkJobs.cs:339 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Animation/VRPhysicalHandPresenceIkJobs.cs:467 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<VRHandIkTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/AtlasSignal/AtlasSignalEvents.cs:144 | _pendingEvents
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<AtlasSignalEventPayload> _pendingEvents;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/AtlasSignal/AtlasSignalEvents.cs:145 | _nextFrameEvents
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<AtlasSignalEventPayload> _nextFrameEvents;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsJobs.cs:891 | AtmosphereTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct AtmosphereTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsJobs.cs:894 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<AtmosphereTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:1224 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:1225 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs:3130 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[WriteOnly, NoAlias] public NativeArray<AtmosphereTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1375 | DispatchWaveHeightReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetFloat(WaveSampleSeaLevelId, SeaLevel);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1376 | DispatchWaveHeightReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetFloat(OceanTimeId, _timeSeconds);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1377 | DispatchWaveHeightReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetFloat(OceanQualityId, _globalQualityWeight);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1379 | DispatchWaveHeightReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetVector(OceanLocalProjectionId, ToVector4(lod.CameraAupLocalXZ));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1380 | DispatchWaveHeightReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetVector(OceanWavePhaseBase0Id, ToVector4(phase.PhaseBase0));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1381 | DispatchWaveHeightReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetVector(OceanWavePhaseBase1Id, ToVector4(phase.PhaseBase1));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1382 | DispatchWaveHeightReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetVector(WaveSampleLodId, new Vector4(maxWavelength, activeWaveCount, _globalQualityWeight, _timeSeconds));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1831 | DumpTelemetryToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1833 | DumpTelemetryToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:2188 | ScanTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ScanTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:1296 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:1300 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:2013 | ExposureSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<ToxicityExposureSignal> ExposureSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:2014 | StatusSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<ToxicityStatusSignal> StatusSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:2134 | BiolumSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<ToxicBioluminescenceSignal> BiolumSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:2192 | Scratch
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<ToxicityGridTelemetryEntry> Scratch;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:37 | Telemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<AudioBridgeTelemetryEntry> Telemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:12047 | DumpGranularTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:12065 | WriteGranularTelemetryDumpCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:12103 | DumpPrologueTransitionTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:12116 | WritePrologueTransitionTelemetryDumpCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:12159 | DumpAudioSynthesisTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:12172 | WriteAudioSynthesisTelemetryDumpCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:263 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<VwsTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:1463 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:1465 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:1940 | HandleH8MemoryFatalLog
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:6345 | TryPrepareBackgroundDomainHandshake
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(telemetryPath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:6432 | InspectPreviousBootState
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Construction/BaseModuleCatalogRuntime.cs:139 | Telemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<ModuleCatalogTelemetryEntry> Telemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/BaseModuleCatalogRuntime.cs:551 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/BaseModuleCatalogRuntime.cs:1043 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/BaseModuleCatalogRuntime.cs:1047 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Create(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime.cs:2030 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime.cs:2031 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime_HatchLocks.cs:814 | TryDumpHatchBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime_HatchLocks.cs:815 | TryDumpHatchBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_hatchDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:6629 | RenderRealHeadlessFleet
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `s_DroneProceduralMaterial.SetVector(s_DroneProceduralCameraOriginPropertyId, new Vector4(origin.x, origin.y, origin.z, 0f));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:6630 | RenderRealHeadlessFleet
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `s_DroneProceduralMaterial.SetInt(s_UsePhantomColorsPropertyId, 0);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:6685 | RenderPhantomSwarm
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `s_PhantomDronesCompute.SetVector(s_PhantomAnchorPropertyId, new Vector4(anchor.x, anchor.y, anchor.z, 0f));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:6686 | RenderPhantomSwarm
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `s_PhantomDronesCompute.SetFloat(s_PhantomTimePropertyId, s_PhantomDronePhaseSeconds);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:6687 | RenderPhantomSwarm
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `s_PhantomDronesCompute.SetFloat(s_PhantomBaseRadiusPropertyId, PhantomDroneOrbitRadiusMeters);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:6688 | RenderPhantomSwarm
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `s_PhantomDronesCompute.SetFloat(s_PhantomVerticalAmplitudePropertyId, PhantomDroneVerticalAmplitudeMeters);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:6689 | RenderPhantomSwarm
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `s_PhantomDronesCompute.SetFloat(s_PhantomScalePropertyId, PhantomDroneScaleMeters);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:6702 | RenderPhantomSwarm
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `s_DroneProceduralMaterial.SetVector(s_DroneProceduralCameraOriginPropertyId, Vector4.zero);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:6703 | RenderPhantomSwarm
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `s_DroneProceduralMaterial.SetInt(s_UsePhantomColorsPropertyId, 1);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:6996 | TryWriteDroneBlackBoxFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:6998 | TryWriteDroneBlackBoxFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager_Transactions.cs:1535 | TryWriteDroneTransactionBlackBoxFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager_Transactions.cs:1537 | TryWriteDroneTransactionBlackBoxFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs:1600 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<DroneAStarTelemetry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs:904 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs:906 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Construction/FluidPipePressureJobs.cs:37 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[WriteOnly, NoAlias] public NativeArray<FluidPipeTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorData.cs:238 | Telemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<FoundationTelemetryEntry> Telemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorData.cs:923 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorData.cs:925 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(resolvedPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Construction/HabitatDeconstructionTransactionKernel.cs:285 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[NativeDisableParallelForRestriction, NoAlias] public NativeArray<TeardownTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:2898 | DumpFloodBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:2900 | DumpFloodBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Construction/HatchLockJobs.cs:528 | RecordHatchTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct RecordHatchTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/ModularBaseConstructionValidator.cs:1008 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/ModularBaseConstructionValidator.cs:1010 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(resolvedPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs:322 | Telemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<ConstructionSocketTelemetryEntry> Telemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs:330 | HolographyTelemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<HolographyTelemetryEntry> HolographyTelemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs:1054 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs:1056 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(resolvedPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Construction/ShinobuSocketConstructionJobs.cs:1040 | RecordConstructionSocketTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordConstructionSocketTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Construction/ShinobuSocketConstructionJobs.cs:1042 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[NoAlias, NativeDisableParallelForRestriction] public NativeArray<ConstructionSocketTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Construction/SumpPumpPipeGridJobs.cs:765 | DrainageTelemetryRecorderJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct DrainageTelemetryRecorderJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Construction/SumpPumpPipeGridJobs.cs:772 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<DrainageTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Construction/SumpPumpPipeGridJobs.cs:773 | FrameSummary
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<DrainageTelemetryEntry> FrameSummary;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/SumpPumpPipeGridRuntime.cs:1552 | InitializeDumpWriterCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/SumpPumpPipeGridRuntime.cs:1627 | DumpWriterLoop
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:1265 | DumpDockTelemetryLocked
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:1269 | DumpDockTelemetryLocked
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:1706 | InitializeReplayFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_replayStream = new FileStream(_replayPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.RandomAccess);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:261 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `private NativeArray<FoveatedSimulationTelemetryEntry> _telemetryRing;`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1174 | DumpTelemetryBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1176 | DumpTelemetryBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][58%][COLD_OR_ASYNC_ALLOCATION_BOUNDARY] COLD_OR_ASYNC_ALLOCATION_REVIEW | Assets/_Project/Scripts/Core/GlobalTelemetryBus.Blackbox.cs:1612 | FlushMmfScratchToDisk
  Evidence kind: HOT_METHOD_NAME_WITH_COLD_CONTEXT
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))`
  Required action: This allocation is inside a cold/save/telemetry/async context despite a hot-looking method name. Keep it outside frame cadence; do not count it as a proven zero-GC hot-path breach without call-cadence proof.
- [INFO][82%][CONFIRMED_OWNER_LOCAL_TELEMETRY_STAGING] LOCAL_NATIVE_TELEMETRY_STAGING_BUFFER_OWNER_LOCAL | Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs:100 | _snapshotBuffer
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<TelemetryEvent> _snapshotBuffer;`
  Required action: This NativeArray is an owner-local telemetry export staging buffer, not persistent blackbox authority. Keep sentinel registration and do not migrate it unless another domain consumes the buffer directly.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Core/HapticSynthesisContracts.cs:361 | Pulses
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<HapticPulseSignal> Pulses;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Core/HapticSynthesisContracts.cs:362 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<HapticTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Core/HapticSynthesisContracts.cs:590 | Pulses
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly, NoAlias] public NativeArray<HapticPulseSignal> Pulses;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Core/HapticSynthesisContracts.cs:592 | FinalPulse
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<HapticPulseSignal> FinalPulse;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Core/HapticSynthesisContracts.cs:593 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<HapticTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Core/HapticSynthesisContracts.cs:684 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<HapticTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs:1936 | WriteScalabilityTelemetryFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs:2018 | WriteScalabilityDictatorBlackBoxFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1565 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1567 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_inputReplayStream = new FileStream(replayPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1716 | DumpDeterministicInputBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1720 | DumpDeterministicInputBlackBox
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
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/RebindingManager.cs:632 |
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:4581 | WriteDispatcherBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcherContracts.cs:248 | MockTimeDilationSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `internal NativeArray<MockTimeDilationSignal> MockTimeDilationSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcherContracts.cs:332 | Signals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MockTimeDilationSignal> Signals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs:223 | _pendingCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<EntityCommand> _pendingCommands;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Dev/BotController.cs:514 | FlushCsvSamplesCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:687 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MarauderTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:992 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MarauderTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:1458 | TransactionSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MockInventoryTransactionSignal> TransactionSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:2671 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:2680 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllBytes(path, bytes);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs:1934 | EcosystemTelemetryReductionJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal unsafe struct EcosystemTelemetryReductionJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs:805 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs:807 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs:1543 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MacroEcosystemTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs:1939 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MacroEcosystemTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs:1551 | InitializeNutrientTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct InitializeNutrientTelemetryJob : IJobParallelFor`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs:1927 | RecordNutrientTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct RecordNutrientTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs:1032 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs:1034 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime_Carrion.cs:1620 | RecordCarrionTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct RecordCarrionTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:2231 | TryLoadLegacyFaultBinaryAt
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3580 | DumpSeismicDirectorTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3617 | WriteSeismicTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3647 | DumpCelestialTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3661 | WriteCelestialTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3789 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3790 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(TelemetryDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1019 | TryHydrateRigDefinitionBinaryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:2398 | DumpTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:2407 | DumpTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:2468 | DumpBiteTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:2471 | DumpBiteTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs:568 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<LeviathanTentacleTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs:1585 | DumpTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs:1587 | DumpTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Fauna/MesofaunaBehavioralStateMachine.cs:274 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[NativeDisableParallelForRestriction, NoAlias] public NativeArray<MesofaunaTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.AcousticSdf.cs:2137 | RecordAcousticTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct RecordAcousticTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.AcousticSdf.cs:1204 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(_acousticSdfDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.AcousticSdf.cs:1236 | EnsureAcousticSdfDumpPathCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.AcousticSdf.cs:2142 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<SensoryTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:4261 | DumpRetinalBlackBoxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:4275 | WriteRetinalBlackBoxFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:4496 | DumpAlphaLeviathanBlackBoxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:4510 | WriteAlphaLeviathanBlackBoxFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:4671 | DumpMesofaunaBlackBoxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:4685 | WriteMesofaunaBlackBoxFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain_Steering.cs:1344 | RecordSteeringTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private unsafe struct RecordSteeringTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain_Steering.cs:584 | DumpLeviathanSteeringBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain_Steering.cs:586 | DumpLeviathanSteeringBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:643 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<ProceduralCrabIkTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:1263 | DumpTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:1265 | DumpTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Fauna/StressDrivenSpawnDirector.cs:3031 | RecordDirectorTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal unsafe struct RecordDirectorTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
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
- [INFO][80%][CONFIRMED_OWNER_LOCAL_TELEMETRY] LOCAL_NATIVE_TELEMETRY_RING_OWNER_LOCAL | Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs:1753 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<ContextualPhysicalIkTelemetryEntry> TelemetryRing;`
  Required action: This telemetry/blackbox ring has sentinel ownership, bounded lifetime, and owner-local dump usage. Do not migrate it to GlobalDataVault unless another domain consumes the buffer or the state becomes persistent authority.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs:2569 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs:2571 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1705 | TryLoadMmfCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1797 | PersistMmfCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1799 | PersistMmfCold
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
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/Gameplay/HectonPlayerState.cs:176 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `public NativeArray<PlayerKinematicsTelemetryEntry> TelemetryRing;`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/Gameplay/MessageTerminal.cs:887 | FlushStatusLight
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetColor(_emissionPropertyId != 0 ? _emissionPropertyId : _EmissionColorID, _pendingStatusLightColor);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:82 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PlayerKinematicsRuntimeTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:3917 | DumpFaultTelemetryIfNeeded
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:3930 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime_HandIK.cs:153 | Telemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public readonly NativeArray<IkHandTelemetryEntry> Telemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime_HandIK.cs:625 | DumpHandIkTelemetryFaultOnly
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime_HandIK.cs:629 | DumpHandIkTelemetryFaultOnly
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime_HandIK.cs:832 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<IkHandTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:2395 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:2397 | DumpBlackBox
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
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:340 | ToolSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<MockToolTransformSignal> ToolSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:469 | Telemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<ScannerTelemetryEntry> Telemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1681 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1685 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:2404 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<ScannerTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/Gameplay/SealedDoor.cs:594 | UpdateProgressVisuals
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetFloat(_ProgressID, progressNormalized);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][52%][HOT_PATH_HEURISTIC] MATERIAL_PROPERTY_BLOCK_HOT_PATH_REVIEW | Assets/_Project/Scripts/Gameplay/SealedDoor.cs:595 | UpdateProgressVisuals
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetColor(_GlowColorID, cutGlowColor);`
  Required action: This hot path updates a cached MaterialPropertyBlock-like receiver. Keep the block cached, avoid per-frame property-block allocation, and escalate only if profiler/SRP evidence shows a batching cost.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:320 | SignalScratch
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<SomaticKinematicSignalScratch> SignalScratch;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:321 | BlackBox
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<SomaticKinematicBlackBoxEntry> BlackBox;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:2546 | WriteTelemetryDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:2548 | WriteTelemetryDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:2584 | WriteBallastTelemetryDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:2586 | WriteBallastTelemetryDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Gameplay/SuitMeshUpdateEvents.cs:118 | _pendingSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<SuitMeshUpdateSignal> _pendingSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Gameplay/SuitMeshUpdateEvents.cs:119 | _nextFrameSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<SuitMeshUpdateSignal> _nextFrameSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:1373 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:1375 | DumpTelemetry
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
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:1810 | ClearComfortTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ClearComfortTelemetryJob : IJobParallelFor`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:1812 | Telemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[WriteOnly, NoAlias] public NativeArray<ComfortTelemetryEntry> Telemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs:2884 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs:2886 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Gameplay/VRSomaticProvider.HorizonLock.cs:690 | ClearHorizonTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ClearHorizonTelemetryJob : IJobParallelFor`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Gameplay/VRSomaticProvider.HorizonLock.cs:692 | Telemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[WriteOnly, NoAlias] public NativeArray<SomaticTelemetryEntry> Telemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Input/ControlRemapper.cs:138 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Input/ControlRemapper.cs:141 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, FileStreamBufferBytes, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Input/ControlRemapper.cs:594 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferBytes, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Input/ControlRemapper.cs:632 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Input/ControlRemapper.cs:635 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, FileStreamBufferBytes, FileOptions.WriteThrough))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Input/ControlRemapper.cs:687 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(tempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Interaction/VRInteractionKinematicBridge.cs:1177 | RecordVRInteractionTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordVRInteractionTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Interaction/VRInteractionKinematicBridge.cs:131 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<VRInteractionTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Interaction/VRInteractionKinematicBridge.cs:319 | DumpTelemetryFaultOnly
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Interaction/VRInteractionKinematicBridge.cs:323 | DumpTelemetryFaultOnly
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(resolvedPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Interaction/VRInteractionKinematicBridge.cs:1180 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<VRInteractionTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][EXECUTABLE_CARRIER_STRUCT_REVIEW] EXECUTABLE_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Inventory/InventoryDefragJob.cs:27 | InventoryDefragCommand
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct InventoryDefragCommand`
  Required action: This signal-like name belongs to an executable carrier with Execute(), not a binary payload contract. Keep NativeArray handles owner-owned; add StructLayout only if the carrier itself is serialized or copied as raw bytes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:2433 | ShinobuEconomyTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct ShinobuEconomyTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:1451 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:1453 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:1488 | DumpTelemetryRingOrdered
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:1490 | DumpTelemetryRingOrdered
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:2237 | Signals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<MockConsumeSignal> Signals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:2435 | Telemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<EconomyTelemetryEntry> Telemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:2456 | BrokenSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<ToolBrokenSignal> BrokenSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:2550 | Result
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[WriteOnly, NoAlias] public NativeArray<EncumbranceSignal> Result;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:2639 | DestroyedSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<DebrisDestroyedSignal> DestroyedSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:2728 | EquipSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[WriteOnly, NoAlias, NativeDisableParallelForRestriction] public NativeArray<EquipItemSignal> EquipSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:2755 | DebrisSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly, NoAlias] public NativeArray<DebrisDestroyedSignal> DebrisSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.CargoSync.cs:332 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<CargoTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.CargoSync.cs:775 | TryDumpCargoTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.CargoSync.cs:777 | TryDumpCargoTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.CargoSync.cs:1029 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[NoAlias, NativeDisableParallelForRestriction] public NativeArray<CargoTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.cs:1641 | RecordSoaInventoryTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordSoaInventoryTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.cs:138 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<InventorySoaTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.cs:700 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.cs:702 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.cs:1643 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[NativeDisableParallelForRestriction, NoAlias] public NativeArray<InventorySoaTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs:894 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs:896 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/HectonLightingRuntime_DayNightRelay.cs:542 | DumpDayNightBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/HectonLightingRuntime_DayNightRelay.cs:544 | DumpDayNightBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:3222 | InteriorGITelemetryScanJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct InteriorGITelemetryScanJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:1759 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:1761 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:2688 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<InteriorGITelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:2689 | TelemetryScratch
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<InteriorGITelemetryEntry> TelemetryScratch;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:2739 | TelemetryScratch
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<InteriorGITelemetryEntry> TelemetryScratch;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:3226 | Scratch
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<InteriorGITelemetryEntry> Scratch;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Meta/GlobalProfileManager.cs:1059 | LoadProfileFromDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string json = File.ReadAllText(path);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][61%][STATIC_DECLARATION_REVIEW] LOCAL_SIGNAL_QUEUE_DECLARED_ONLY_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:417 | _queue
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `private NativeQueue<FutureCommandEnvelope> _queue;`
  Required action: This NativeQueue field has no allocation in the same source file. Keep the external owner visible, but do not classify it as a live orphaned lane until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:2351 | DumpKernelTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:2352 | DumpKernelTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(KernelDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:2390 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:2391 | DumpBlackbox
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
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs:48 | _cullTelemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ModCullTelemetryEntry> _cullTelemetry;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/ModLoader.cs:251 | TryReadManifest
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string json = File.ReadAllText(manifestPath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Narrative/HectonNarrativeDirector_PoiTriggers.cs:268 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<AupNarrativeTriggerTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Narrative/HectonNarrativeDirector_PoiTriggers.cs:603 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<AupNarrativeTriggerTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Narrative/HectonNarrativeDirector_PoiTriggers.cs:836 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Narrative/HectonNarrativeDirector_PoiTriggers.cs:838 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Narrative/LoreMmfEncyclopedia.cs:48 | TryOpen
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream indexStream = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Narrative/LoreMmfEncyclopedia.cs:49 | TryOpen
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_payloadStream = new FileStream(payloadPath, FileMode.Open, FileAccess.Read, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs:1713 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<NetTelemetryEntry64> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs:1714 | InputPredictionTelemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<InputPredictionTelemetryEntry> InputPredictionTelemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:3839 | DumpHeapTelemetryToFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:3841 | DumpHeapTelemetryToFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:2347 | RecordCartographyTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordCartographyTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:508 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<CartographyTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:513 | MockPings
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<MapRevealSignal> MockPings;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:514 | PendingPings
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<MapRevealSignal> PendingPings;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:1096 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dir);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:1127 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(telemetryDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:1632 | Buffer
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<MapRevealSignal> Buffer;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:1648 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<CartographyTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:2010 | PendingSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<MapRevealSignal> PendingSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:2350 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<CartographyTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:2406 | TryDumpPhysicsCullingBlackBoxToFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:2410 | TryDumpPhysicsCullingBlackBoxToFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/HabitatFluidIncursionJobs.cs:867 | FluidTelemetryRecorderJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct FluidTelemetryRecorderJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/HabitatFluidIncursionJobs.cs:716 | CompartmentTelemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<FluidCompartmentTelemetryDTO> CompartmentTelemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/HabitatFluidIncursionJobs.cs:870 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<FluidIncursionTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/HarpoonTensionSolver328.cs:1877 | RecordHarpoonTetherTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordHarpoonTetherTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/HarpoonTensionSolver328.cs:1322 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<TetherTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/HarpoonTensionSolver328.cs:1883 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<TetherTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/TetherAupVerletJobs.cs:842 | RecordTetherAupTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct RecordTetherAupTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/TetherAupVerletJobs.cs:846 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<TetherAupTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/TetherVerletJobs.cs:438 | TetherVerletTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct TetherVerletTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/TetherVerletJobs.cs:440 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<TetherVerletTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/VerletCableDTOs.cs:1081 | VerletBlackBoxWriteJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct VerletBlackBoxWriteJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Physics/VerletCableDTOs.cs:747 | SnapSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[WriteOnly, NoAlias] public NativeArray<CableSnappedSignal> SnapSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/VerletCableDTOs.cs:925 | WinchSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly, NoAlias] public NativeArray<MockWinchSignal> WinchSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/VerletCableDTOs.cs:1085 | Ring
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<VerletCableBlackBoxEntry> Ring;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuMetabolismJobs.cs:979 | MetabolismTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct MetabolismTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs:2314 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs:2359 | TryEnsureBlackBoxDirectory
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs:2394 | ReplaceBlackBoxDumpByBackupMove
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(backupPath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs:2426 | TryDeleteBlackBoxDumpPath
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(targetPath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:551 | Environment
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MockEnvironmentVitalsSignal> Environment;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:552 | PressureSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MockPressureSignal> PressureSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:609 | Environment
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly, NoAlias] public NativeArray<MockEnvironmentVitalsSignal> Environment;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:652 | Environment
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly, NoAlias] public NativeArray<MockEnvironmentVitalsSignal> Environment;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:699 | CombatSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MockCombatDamageSignal> CombatSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:700 | PredatorSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MockPredatorAggroSignal> PredatorSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:701 | ToxemiaSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MockToxemiaSignal> ToxemiaSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:702 | MedicalSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MockMedicalItemUsedSignal> MedicalSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:820 | Environment
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly, NoAlias] public NativeArray<MockEnvironmentVitalsSignal> Environment;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:1084 | Environment
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly, NoAlias] public NativeArray<MockEnvironmentVitalsSignal> Environment;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:1219 | Environment
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly, NoAlias] public NativeArray<MockEnvironmentVitalsSignal> Environment;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:1222 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PhysiologyTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:1224 | DecompressionTelemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<DecompressionTelemetryEntry> DecompressionTelemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:1666 | DumpAutopsyReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:1668 | DumpAutopsyReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuRadiationMutationJobs.cs:422 | PatchRadiationMutationTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct PatchRadiationMutationTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
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
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuSensoryImpairmentJobs.cs:398 | PatchSensoryTelemetryGasJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct PatchSensoryTelemetryGasJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Physiology/ShinobuSensoryImpairmentJobs.cs:14 | Telemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[WriteOnly, NoAlias] public NativeArray<SensoryImpairmentTelemetryEntry> Telemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuSensoryImpairmentJobs.cs:35 | Environment
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MockEnvironmentVitalsSignal> Environment;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Physiology/ShinobuSensoryImpairmentJobs.cs:170 | Telemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[NativeDisableParallelForRestriction, NoAlias] public NativeArray<SensoryImpairmentTelemetryEntry> Telemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuSensoryImpairmentJobs.cs:401 | Environment
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly, NoAlias] public NativeArray<MockEnvironmentVitalsSignal> Environment;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuSensoryImpairmentJobs.cs:402 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<SensoryImpairmentTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuSensoryImpairmentRuntime.cs:650 | DumpAutopsyReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuSensoryImpairmentRuntime.cs:652 | DumpAutopsyReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Physiology/ShinobuSuitIntegrityJobs.cs:267 | Telemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[NoAlias, NativeDisableParallelForRestriction] public NativeArray<SuitIntegrityTelemetryEntry> Telemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuSuitIntegrityRuntime.cs:788 | DumpAutopsyReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuSuitIntegrityRuntime.cs:790 | DumpAutopsyReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs:3533 | DumpPowerBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs:3535 | DumpPowerBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Power/PowerGridJacobiContracts.cs:735 | RecordPowerTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordPowerTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Power/PowerGridJacobiContracts.cs:739 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PowerTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Power/PowerGridSolarContracts.cs:761 | RecordSolarTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordSolarTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Power/PowerGridSolarContracts.cs:765 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<SolarTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
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
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:2861 | BlackBox
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<LogisticsGraphTelemetryEntry> BlackBox;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs:2398 | ThermalGridTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private unsafe struct ThermalGridTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs:343 | Telemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<ThermalPowerGridTelemetrySnapshot> Telemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs:1691 | WriteBlackBoxFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs:1693 | WriteBlackBoxFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Quest/QuestDagDataLoading.cs:61 | TryLoadOshinoBinary
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(binaryPath, FileMode.Open, FileAccess.Read, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs:726 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<QuestDagTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs:1278 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs:1280 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:330 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<QuestDagTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Quest/QuestGraphEvaluator.cs:38 | _pendingSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<QuestSignalPayload> _pendingSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Quest/QuestStateManager.cs:1540 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.AppendAllText(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs:1250 | TryWriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs:1251 | TryWriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs:375 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs:414 | WriteEmptyBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs:450 | WriteBlackBoxFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs:466 | WriteEmptyBlackBoxFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/LutArrayResolver.cs:219 | TryStageStreamingUriToCache
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(cacheDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs:3466 | EntityDeltaTelemetryRecordJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct EntityDeltaTelemetryRecordJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs:3515 | EntityDeltaDiskLatencyTelemetryPatchJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct EntityDeltaDiskLatencyTelemetryPatchJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs:206 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<EntityCompressionTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs:3470 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<EntityCompressionTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs:3517 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<EntityCompressionTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs:3553 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly, NoAlias] public NativeArray<EntityCompressionTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs:2729 | SaveMerkleTelemetryWriteJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct SaveMerkleTelemetryWriteJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs:200 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<SaveMerkleTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs:2732 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<SaveMerkleTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs:1918 | VoxelDeltaTelemetryRecordJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct VoxelDeltaTelemetryRecordJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs:1970 | VoxelDeltaDiskLatencyTelemetryPatchJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct VoxelDeltaDiskLatencyTelemetryPatchJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs:189 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<VoxelDeltaCompressionTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs:1922 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<VoxelDeltaCompressionTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs:1972 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<VoxelDeltaCompressionTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/SaveSystem/WalIntegrityFuzzerCore_SHINOBU357.cs:1050 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<WalFuzzTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs:187 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<ScavengingTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs:306 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<ScavengingTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs:1303 | TryDumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(path));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs:1304 | TryDumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsJobs.cs:791 | ThermalTelemetryRecorderJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct ThermalTelemetryRecorderJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
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
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Thermodynamics/ReactorThermalGridJobs.cs:1164 | NuclearReactorTelemetryRecorderJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct NuclearReactorTelemetryRecorderJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Thermodynamics/ReactorThermalGridJobs.cs:1287 | ReactorTelemetryRecorderJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct ReactorTelemetryRecorderJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:1758 | ScanTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ScanTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:1346 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:1348 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.FileWorker.cs:326 | ReadConfigFileMapped
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, ConfigFileStreamBufferBytes, FileOptions.SequentialScan);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.FileWorker.cs:352 | ReadConfigFileStreamed
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, ConfigFileStreamBufferBytes, FileOptions.SequentialScan);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Tools/LaserCutterDodJobs.cs:352 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[NativeDisableParallelForRestriction, NoAlias] public NativeArray<LaserCutTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Tools/UpgradeMatrixCompiler.cs:1258 | RecordUpgradeTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordUpgradeTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Tools/UpgradeMatrixCompiler.cs:82 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<UpgradeTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Tools/UpgradeMatrixCompiler.cs:1262 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<UpgradeTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs:872 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs:874 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:2646 | TelemetryWriteJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct TelemetryWriteJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:1460 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(dumpPath));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:1461 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs:724 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs:726 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:661 | RenderWaveMesh
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_runtimeMaterial.SetFloat(TubeRadiusId, math.max(0.0005f, tubeRadius));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:667 | RenderWaveMesh
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_runtimeMaterial.SetVector(WaveScalarsId, waveScalars);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:673 | RenderWaveMesh
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_runtimeMaterial.SetVector(WaveLayoutId, waveLayout);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:679 | RenderWaveMesh
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_runtimeMaterial.SetVector(TimeErrorStageId, timeErrorStage);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:838 | DumpTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:840 | DumpTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(TelemetryDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs:2214 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs:2240 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/PdaH8lrLoreStore.cs:221 | TryOpenVaultMirror
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:909 | RenderHologramMap
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_hologramMapMaterial.SetColor(HologramTintId, edgeTint);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:910 | RenderHologramMap
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_hologramMapMaterial.SetFloat(OpacityId, pointCloudOpacity * 0.72f);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:911 | RenderHologramMap
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_hologramMapMaterial.SetFloat(HologramGlowId, tuning.VisualGlowIntensity);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:912 | RenderHologramMap
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_hologramMapMaterial.SetFloat(HologramQualityId, quality);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:913 | RenderHologramMap
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_hologramMapMaterial.SetVector(`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:916 | RenderHologramMap
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_hologramMapMaterial.SetVector(`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:979 | RenderPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_pointCloudMaterial.SetVector(AcousticPingSignalId, new Vector4(pingRadius, PointCloudPingBandWidth, _animationTime, pingActive));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:980 | RenderPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_pointCloudMaterial.SetFloat(ActiveSonarRadiusId, activeSonarRadiusMeters);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:981 | RenderPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_pointCloudMaterial.SetFloat(ActiveSonarMaxRangeId, activeSonarMaxRangeMeters);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:982 | RenderPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_pointCloudMaterial.SetFloat(PointSizeId, pointCloudPointSize);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:983 | RenderPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_pointCloudMaterial.SetFloat(OpacityId, pointCloudOpacity);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:984 | RenderPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_pointCloudMaterial.SetFloat(DepthFadeMetersId, pointCloudDepthMeters);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:985 | RenderPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_pointCloudMaterial.SetFloat(HeightColorizationId, Smooth01(quality));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2267 | RenderRadarPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_radarRuntimeMaterial.SetFloat(HectonRadarProceduralId, 1f);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2268 | RenderRadarPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_radarRuntimeMaterial.SetFloat(HectonRadarGprProceduralId, 1f);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2274 | RenderRadarPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_radarRuntimeMaterial.SetVector(`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2389 | DispatchDamageHologramCompute
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `damageHologramCompute.SetVector(DamageHologramParamsId, computeParams);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2390 | DispatchDamageHologramCompute
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `damageHologramCompute.SetVector(DamageHologramBoundsId, _damageProxyBounds);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2909 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2911 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2958 | WriteDamageHolographerMirrorDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1805 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1807 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:2162 | Telemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<WristHudTelemetryEntry> Telemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime_PdaScreenProjector.cs:1172 | DumpPdaProjectionBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime_PdaScreenProjector.cs:1174 | DumpPdaProjectionBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime_PdaScreenProjector.cs:1500 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PdaProjectionTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs:1534 | DumpCameraJuiceTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs:1536 | DumpCameraJuiceTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/VFX/CameraJuiceSystem_CameraJuiceBurst.cs:934 | InitializeCameraJuiceTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct InitializeCameraJuiceTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/VFX/CameraJuiceSystem_CameraJuiceBurst.cs:936 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<CameraJuiceTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/VFX/CameraJuiceSystem_CameraJuiceBurst.cs:953 | MockSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<CameraJuiceMockSignalDTO> MockSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/VFX/CameraJuiceSystem_CameraJuiceBurst.cs:976 | MockSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<CameraJuiceMockSignalDTO> MockSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:3727 | DispatchWakeProximityInjection
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `marineSnowCompute.SetTexture(_wakeProximityKernel, ShaderIds.CaveVoxelSdfTexId, sdfTexture);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:3729 | DispatchWakeProximityInjection
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `marineSnowCompute.SetTexture(_wakeProximityKernel, ShaderIds.TerrainHeightTextureId, heightTexture);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:3760 | DispatchParticleInitializationIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `marineSnowCompute.SetVector(ShaderIds.InitializationParamsId, new Vector4(_allocatedParticleCapacity, 0f, 0f, 0f));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:3767 | DispatchParticleInitializationIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `marineSnowCompute.SetVector(ShaderIds.InitializationParamsId, Vector4.zero);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:4769 | TryWriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:4771 | TryWriteBlackBoxDump
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:1549 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:1551 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:1732 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:2059 | Physiology
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<MockPhysiologySignal> Physiology;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:2060 | Environment
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<MockVisorEnvironmentSignal> Environment;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs:2353 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs:2355 | DumpBlackBox
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorARStencilRendererFeature.cs:1322 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorARStencilRendererFeature.cs:1324 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs:1584 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs:1586 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs:1688 | TryDumpReconstructionTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs:1690 | TryDumpReconstructionTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.Noir.cs:1032 | TryDumpNoirTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.Noir.cs:1034 | TryDumpNoirTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs:1768 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs:1772 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs:720 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/SpectrumSystem.cs:3387 | DumpActiveSonarGeoTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/SpectrumSystem.cs:3389 | DumpActiveSonarGeoTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs:1017 | RecordBiomeTransitionTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordBiomeTransitionTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs:1021 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[WriteOnly, NoAlias] public NativeArray<BiomeTransitionTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs:3246 | ChemicalTelemetryWriteJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private unsafe struct ChemicalTelemetryWriteJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs:2101 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs:2103 | DumpTelemetryRing
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:4806 | DumpWakeBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:4808 | DumpWakeBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:4957 | DumpFloraSwayFieldBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:4959 | DumpFloraSwayFieldBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:8268 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetTexture(_wakeTrailSimulationKernel, _WakeTrailSourceId, _wakeTrailRead);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:8269 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetTexture(_wakeTrailSimulationKernel, _WakeTrailResultId, _wakeTrailWrite);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:8272 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetVector(_WakeTrailScrollUvOffsetId, new Vector4(_pendingWakeTrailScrollUv.x, _pendingWakeTrailScrollUv.y, 0f, 0f));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:8273 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetFloat(_WakeTrailFadeDeltaId, Mathf.Max(0f, fade));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:8274 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetFloat(_WakeTrailDiffusionId, _wakeTrailDiffusion);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:8275 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetFloat(_WakeTrailWaveStrengthId, _wakeTrailWaveStrength);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:8276 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetFloat(_WakeTrailDampingId, _wakeTrailWaveDamping);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:8277 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetFloat(_WakeTrailCurlStrengthId, _wakeTrailCurlStrength);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:8278 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetFloat(_WakeTrailSimulationTimeId, GetCurrentSimulationTimeSeconds());`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:8280 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetVector(`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:9532 | DumpFloraMemoryTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:9567 | DumpFloraMemoryTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllBytes(fullPath, work.Payload);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:9587 | WriteFloraMemoryTelemetryDumpQueued
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllBytes(work.Path, work.Payload);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:242 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[NativeDisableParallelForRestriction] public NativeArray<GlobalWorldSamplerTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:1241 | TryDumpTelemetryBuffer
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:1244 | TryDumpTelemetryBuffer
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:2337 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[NoAlias, NativeDisableParallelForRestriction] public NativeArray<GlobalWorldSamplerTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:2423 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[NoAlias, NativeDisableParallelForRestriction] public NativeArray<GlobalWorldSamplerTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:2546 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[NoAlias, NativeDisableParallelForRestriction] public NativeArray<GlobalWorldSamplerTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:2548 | Signals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[NoAlias, ReadOnly] public NativeArray<MockTerrainQuerySignal> Signals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:2648 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[NoAlias, NativeDisableParallelForRestriction] public NativeArray<GlobalWorldSamplerTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:2733 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[NoAlias, NativeDisableParallelForRestriction] public NativeArray<GlobalWorldSamplerTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GPUScatterDirector.cs:1425 | DumpScatterBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GPUScatterDirector.cs:1427 | DumpScatterBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs:280 | Render
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(GroundRadarPulseId, _pulsePhaseSeconds);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs:281 | Render
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(GroundRadarScaleId, math.max(0.1f, ringScaleMeters));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs:1172 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read); // COLD ALLOC: FileStream[GPR telemetry dump] — blackbox dump file writer — owner: TERRAIN_GPR_SYSTEM`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2557 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetFloat(_DensityKeepProbabilityId, densityKeepProbability01);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2562 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetVector(_CameraPositionId, cameraPosition);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2563 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetVector(_CameraForwardId, cameraForward);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2564 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetVector(_GlobalFloatingOffsetId, globalFloatingOffset);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2565 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetFloat(_LodNearDistanceId, brgNearLodDistance);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2566 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetFloat(_LodFarDistanceId, brgFarLodDistance);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2567 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetFloat(_LodTransitionRangeId, brgLodTransitionRange);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2571 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetFloat(_PeripheralCullDotId, peripheralCullDot);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2572 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetFloat(_PeripheralCullDistanceSqId, peripheralCullDistanceSq);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2573 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetFloat(_OcclusionDepthBiasId, _occlusionDepthBias);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2575 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetVector(_OcclusionZBufferParamsId, Shader.GetGlobalVector(_GlobalZBufferParamsId));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2577 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetFloat(_DarknessBiolumThresholdId, _darknessBiolumThreshold);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2580 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetTexture(_cullFloraKernel, _DepthPyramidTextureId, _depthPyramidTexture);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2582 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetVector(_DepthPyramidTexelSizeId, new Vector4(`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2590 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetFloat(_FloorBiolumStrengthId, Shader.GetGlobalFloat(_FloorBiolumStrengthId));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2591 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetFloat(_OceanBiolumStrengthId, Shader.GetGlobalFloat(_OceanBiolumStrengthId));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2592 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetFloat(_BiolumIntensityVectorId, ResolveBiolumIntensityScalar());`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2606 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetFloat(_DensityKeepProbabilityId, densityKeepProbability01);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2611 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetVector(_CameraPositionId, cameraPosition);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2612 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetVector(_CameraForwardId, cameraForward);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2613 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetVector(_GlobalFloatingOffsetId, globalFloatingOffset);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2614 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetFloat(_LodNearDistanceId, brgNearLodDistance);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2615 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetFloat(_LodFarDistanceId, brgFarLodDistance);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2616 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetFloat(_LodTransitionRangeId, brgLodTransitionRange);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2617 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetFloat(_PeripheralCullDotId, peripheralCullDot);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2618 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetFloat(_PeripheralCullDistanceSqId, peripheralCullDistanceSq);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2620 | DispatchGpuCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_cullingCompute.SetFloat(_DarknessBiolumThresholdId, _darknessBiolumThreshold);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2681 | DispatchFloraSnapFlagUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_abyssalFlowFieldCompute.SetVector(_GlobalFloatingOffsetId, globalFloatingOffset);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2682 | DispatchFloraSnapFlagUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_abyssalFlowFieldCompute.SetVector(_SubmarineWashSphereId, washSphere);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2683 | DispatchFloraSnapFlagUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_abyssalFlowFieldCompute.SetVector(_SubmarineWashVelocityId, washVelocity);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3240 | DumpFloraGrowthTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3242 | DumpFloraGrowthTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3434 | TryWriteScatterCullTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3436 | TryWriteScatterCullTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/HectonVoxelStreamingBridge.cs:548 | TickChunkFade
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `state.RuntimeMaterial.SetFloat(ChunkDissolveFadeId, fade01);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:5030 | RestoreFromIndexedSave
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(_indexedSectorOverrideDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][58%][COLD_OR_ASYNC_ALLOCATION_BOUNDARY] COLD_OR_ASYNC_ALLOCATION_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:5195 | RunIndexedSectorPagingAsync
  Evidence kind: HOT_METHOD_NAME_WITH_COLD_CONTEXT
  Evidence: `long[] quarantineResetScratch = new long[SaveBinaryStorage.IndexedSectorQuarantineHashCapacity];`
  Required action: This allocation is inside a cold/save/telemetry/async context despite a hot-looking method name. Keep it outside frame cadence; do not count it as a proven zero-GC hot-path breach without call-cadence proof.
- [WARN][72%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ENUMERATION_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:6990 | RunSectorOverrideCommitAsync
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `commitWork = _dueSectorOverrideCommitWork.ToArray();`
  Required action: Review this hot-path enumeration/LINQ surface for allocations, boxing, or hidden iterator state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:7086 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:9345 | DumpWorldTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:9347 | DumpWorldTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(WorldTelemetryDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:4628 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:4630 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/SargassumCrestDampingController.cs:387 | DispatchFacadeBake
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_facadeBakeCompute.SetTexture(_facadeBakeKernel, _DensityTexId, densityTexture);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/SargassumCrestDampingController.cs:388 | DispatchFacadeBake
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_facadeBakeCompute.SetTexture(_facadeBakeKernel, _CutMaskTexId, cutMaskActive && cutMaskTexture != null ? cutMaskTexture : Texture2D.blackTexture);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/SargassumCrestDampingController.cs:389 | DispatchFacadeBake
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_facadeBakeCompute.SetTexture(_facadeBakeKernel, _WaveDampingMaskResultId, _waveDampingMask);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/SargassumCrestDampingController.cs:390 | DispatchFacadeBake
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_facadeBakeCompute.SetTexture(_facadeBakeKernel, _OilFilmMaskResultId, _oilFilmMask);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/SargassumCrestDampingController.cs:391 | DispatchFacadeBake
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_facadeBakeCompute.SetVector(_DensityWorldRectId, densityWorldRect);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/SargassumCrestDampingController.cs:392 | DispatchFacadeBake
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_facadeBakeCompute.SetVector(_CutMaskWorldRectId, cutMaskWorldRect);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/SargassumCrestDampingController.cs:394 | DispatchFacadeBake
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_facadeBakeCompute.SetVector(_GlobalDriftOffsetId, _activeDriftOffset);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/SargassumCrestDampingController.cs:395 | DispatchFacadeBake
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_facadeBakeCompute.SetVector(_WaveFacadeParamsId, new Vector4(waveDampingDensityPower, waveDampingCutRelief, 1f, 0f));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/SargassumCrestDampingController.cs:396 | DispatchFacadeBake
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_facadeBakeCompute.SetVector(_OilFacadeParamsId, new Vector4(oilFilmDensityPower, oilFilmCutRelief, oilFilmAlphaScale, 0f));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:1640 | ProcessQueuedMaskUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_stampCompute.SetTexture(_stampKernel, _MainTexId, _maskRead);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:1641 | ProcessQueuedMaskUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_stampCompute.SetTexture(_stampKernel, _ResultId, _maskWrite);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:1644 | ProcessQueuedMaskUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_stampCompute.SetVector(_ScrollUvOffsetId, new Vector4(_pendingScrollUv.x, _pendingScrollUv.y, 0f, 0f));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:1645 | ProcessQueuedMaskUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_stampCompute.SetFloat(_RecoveryId, _pendingRecovery);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:1646 | ProcessQueuedMaskUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_stampCompute.SetVector(`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:1832 | ProcessQueuedDamageVolumeUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_damageVolumeCompute.SetTexture(_damageVolumeKernel, _DamageVolumeSourceId, _damageVolumeRead);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:1833 | ProcessQueuedDamageVolumeUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_damageVolumeCompute.SetTexture(_damageVolumeKernel, _DamageVolumeResultId, _damageVolumeWrite);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:1836 | ProcessQueuedDamageVolumeUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_damageVolumeCompute.SetFloat(_DamageVolumeRecoveryId, Mathf.Max(0f, damageVolumeRecoveryPerSecond * Mathf.Max(0f, deltaTime)));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:1837 | ProcessQueuedDamageVolumeUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_damageVolumeCompute.SetVector(`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:1840 | ProcessQueuedDamageVolumeUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_damageVolumeCompute.SetVector(`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:235 | KillSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<BoidKillSignal> KillSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6226 | TryDumpFoodChainTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6228 | TryDumpFoodChainTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6407 | TryDumpBoidSensoryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6410 | TryDumpBoidSensoryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:8736 | DispatchOriginShiftToLiveBoidBuffersVisualSync
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `boidCompute.SetVector(_OriginShiftDeltaId, shiftVector);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/ShinobuStreamingRuntime.cs:241 | Signal
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MockAupShiftSignal> Signal;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/ShinobuStreamingRuntime.cs:268 | Signal
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly, NoAlias] public NativeArray<MockAupShiftSignal> Signal;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/World/SpawnZoneSdfValidation.cs:586 | SpawnValidationTelemetryReduceJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct SpawnValidationTelemetryReduceJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/SpawnZoneSdfValidation.cs:589 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<SpawnValidationTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
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
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/VegetationDensityQueryService.cs:1491 | FlushVegetationAudioHandoffVisualSync
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `vegetationAudioMixer.SetFloat(vegetationDensityMixerParameter, density);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/VegetationDensityQueryService.cs:1494 | FlushVegetationAudioHandoffVisualSync
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs:2093 | DumpAbyssalPathTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs:2095 | DumpAbyssalPathTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:864 | VolcanicTelemetryFinalizeJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct VolcanicTelemetryFinalizeJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:660 | HeatSignal
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<VolcanicPlayerHeatSignalDTO> HeatSignal;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:731 | FloatSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<VolcanicFloatStateSignal> FloatSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:868 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<VolcanicUpdraftTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:1709 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:1711 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanCognitionVault.cs:18 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<AlphaLeviathanTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanCognitionVault.cs:372 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanCognitionVault.cs:376 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanCognitionVault.cs:673 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/AI/Cognition/LeviathanStalkJob.cs:37 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<AlphaLeviathanTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainJobs.cs:88 | ProximitySignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<ApexProximitySignal> ProximitySignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainJobs.cs:89 | CombatDamageSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MockCombatDamageSignal> CombatDamageSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainJobs.cs:90 | PanicSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<ApexPanicSignal> PanicSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainJobs.cs:107 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[NativeDisableParallelForRestriction, NoAlias] public NativeArray<ApexTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs:99 | ProximitySignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<ApexProximitySignal> ProximitySignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs:100 | CombatDamageSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<MockCombatDamageSignal> CombatDamageSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs:101 | PanicSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<ApexPanicSignal> PanicSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs:104 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<ApexTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs:1200 | TryWriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs:1203 | TryWriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs:1284 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionAnxietyJobs.cs:178 | RecordAnxietyTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordAnxietyTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionAnxietyJobs.cs:182 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<AnxietyTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionJobs.cs:350 | RecordCognitionTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordCognitionTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionJobs.cs:355 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<CognitionTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs:78 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<CognitionTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs:939 | TryWriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs:942 | TryWriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs:996 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault_AnxietyDecay.cs:66 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<AnxietyTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault_AnxietyDecay.cs:784 | TryWriteAnxietyDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault_AnxietyDecay.cs:787 | TryWriteAnxietyDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault_AnxietyDecay.cs:841 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs:1089 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs:1091 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs:1165 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<EcosystemPopulationTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:5028 | CountTelemetryCountersJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct CountTelemetryCountersJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1449 | LoadFileIntoNativeScratch
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, share, math.max(1, limit), FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:2723 | WriteBlackBoxFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:2725 | WriteBlackBoxFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.FlockingAvoidance.cs:265 | DumpFlockingBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.FlockingAvoidance.cs:267 | DumpFlockingBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuSpatialGridSolver.cs:893 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<SpatialGridTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
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
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/AI/Pathfinding/VoxelAStarJobs.cs:354 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PathfindingTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/AI/Pathfinding/VoxelAStarJobs.cs:1135 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PathfindingTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/AI/Perception/RetinalAdaptationVault.cs:13 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<RetinalTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:1040 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:1042 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Animation/FaunaProcedural/ProceduralBoneBlenderJobs.cs:323 | ProceduralBoneTelemetryReduceJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct ProceduralBoneTelemetryReduceJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Animation/FaunaProcedural/ProceduralBoneBlenderJobs.cs:13 | Signals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MockAiVelocitySignal> Signals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Animation/FaunaProcedural/ProceduralBoneBlenderJobs.cs:68 | MockSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly, NoAlias] public NativeArray<MockAiVelocitySignal> MockSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Animation/FaunaProcedural/ProceduralBoneBlenderJobs.cs:326 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<ProceduralBoneTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorJobs.cs:764 | KineticAnimationTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct KineticAnimationTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorJobs.cs:768 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<KineticAnimationTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorTypes.cs:866 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Animation/KineticCharacter/KineticCharacterAnimatorTypes.cs:868 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Animation/Locomotion/LadderClimbIkJobs.cs:105 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<LadderClimbTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Animation/Locomotion/LadderClimbIkJobs.cs:131 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<LadderClimbTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:1226 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(BlackBoxDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:1253 | PrepareBlackBoxDumpDirectoryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(BlackBoxDumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationJobs.cs:53 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<StormPropagationTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationRuntime.cs:1032 | TryDumpTelemetryToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationRuntime.cs:1037 | TryDumpTelemetryToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationRuntime.cs:1044 | TryDumpTelemetryToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(tempPath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationRuntime.cs:1181 | CopyFileIntoScratchCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:259 | MockPredator
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<MockPredatorProximitySignal> MockPredator;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:260 | MockDepth
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<MockDepthSignal> MockDepth;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:261 | MockTension
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<MockTensionSignal> MockTension;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:262 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<AudioStemTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1430 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1432 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/DepthStressGranularSynthesisKernel.cs:142 | Output
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MockHullStressSignal> Output;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/DepthStressGranularSynthesisKernel.cs:144 | PressureOutput
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<DepthStressMockPressureSignal> PressureOutput;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/DepthStressGranularSynthesisKernel.cs:146 | TensionOutput
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<DepthStressMockTensionSignal> TensionOutput;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/HullStressGranularDspKernel.cs:518 | OutputSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<BaseStructuralWarningSignal> OutputSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/HullStressGranularDspKernel.cs:587 | Signals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly, NoAlias] public NativeArray<BaseStructuralWarningSignal> Signals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/HullStressGranularDspKernel.cs:716 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<AudioDspTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/VocalBankContracts.cs:498 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<VocalTelemetryEntryDTO> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs:22 | Telemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<VocalTelemetryEntryDTO> Telemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Content/ContentLoreBinaryProvider.cs:146 | Open
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_fallbackStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferBytes);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs:1715 | TryWriteBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs:1717 | TryWriteBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Data/BabelDictionaryStore.cs:158 | Open
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.RandomAccess);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Data/BabelDictionaryStore.cs:512 | LoadFileIntoPaddedBufferCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:1394 | FlushBTreeTelemetryPostSimulationJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct FlushBTreeTelemetryPostSimulationJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:1396 | Ring
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<BTreeTelemetryEntry> Ring;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:1398 | Accumulator
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<BTreeTelemetryAccumulatorDTO> Accumulator;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:2403 | Write
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:2419 | Write
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:2444 | Write
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:2460 | Write
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Data/StaticDataStore.cs:110 | Open
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.RandomAccess);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Data/StaticDataStore.cs:134 | Open
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:244 | TryOpenExistingFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_fileStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1599 | LoadGhostReplay
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, ReplayBlockBytes * 4, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:4233 | WriteDefragBlackBoxFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:4270 | WriteDefragBlackBoxFileRaw
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:4272 | WriteDefragBlackBoxFileRaw
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][91%][CONFIRMED_ROOT_ALLOCATOR_TELEMETRY] LOCAL_NATIVE_TELEMETRY_RING_ROOT_OWNER | Assets/_Project/Scripts/Core/Memory/H8Memory.cs:2361 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_H8MEMORY_SCAN
  Evidence: `private static NativeArray<H8MemoryTelemetryEntry> _blackBox;`
  Required action: This telemetry ring belongs to the H8Memory/GlobalDataVault root allocation layer. Keep dispose coverage, but do not classify the root owner itself as a downstream private non-vault breach.
- [INFO][91%][CONFIRMED_ROOT_ALLOCATOR_TELEMETRY] LOCAL_NATIVE_TELEMETRY_RING_ROOT_OWNER | Assets/_Project/Scripts/Core/Memory/H8Memory.cs:2362 | _eventBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_H8MEMORY_SCAN
  Evidence: `private static NativeArray<H8MemoryTelemetryEntry> _eventBlackBox;`
  Required action: This telemetry ring belongs to the H8Memory/GlobalDataVault root allocation layer. Keep dispose coverage, but do not classify the root owner itself as a downstream private non-vault breach.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/H8Memory.cs:3694 | WriteFatalLeakBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/H8Memory.cs:3717 | WriteFatalLeakBlackBoxFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Core/Origin/AupOriginShiftCoordinator.cs:62 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `internal NativeArray<AupOriginShiftTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Origin/AupOriginShiftCoordinator.cs:1363 | WriteOriginShiftDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Origin/AupOriginShiftCoordinator.cs:1365 | WriteOriginShiftDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, DumpWriteBufferBytes, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Core/Origin/AupPrecisionJobs.cs:580 | AupPrecisionTelemetryFoldJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct AupPrecisionTelemetryFoldJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Core/Origin/AupPrecisionJobs.cs:334 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `internal NativeArray<AupPrecisionTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Origin/AupPrecisionJobs.cs:388 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Origin/AupPrecisionJobs.cs:390 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Core/Origin/AupPrecisionJobs.cs:586 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<AupPrecisionTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
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
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1316 | Input
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly, NoAlias] public NativeArray<MockRockCollisionSignal> Input;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1317 | Output
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MacroCollisionSignal> Output;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1619 | Headers
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[NativeDisableParallelForRestriction, NoAlias] public NativeArray<SignalThreadLocalHeader64> Headers;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1778 | Headers
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[NativeDisableParallelForRestriction, NoAlias] public NativeArray<SignalThreadLocalHeader64> Headers;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1788 | OverflowSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[NativeDisableParallelForRestriction, NoAlias] public NativeArray<SignalWardenMockDamageSignal> OverflowSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1798 | OverflowHeader
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[NativeDisableParallelForRestriction, NoAlias] public NativeArray<SignalThreadOverflowHeader64> OverflowHeader;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:2004 | ReadHeaders
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<SignalThreadLocalHeader64> ReadHeaders;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:2005 | Output
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<SignalWardenMockDamageSignal> Output;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:2008 | OverflowSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<SignalWardenMockDamageSignal> OverflowSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:2009 | OverflowHeader
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<SignalThreadOverflowHeader64> OverflowHeader;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:2010 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<SignalThreadContentionTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:2328 | Headers
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<SignalThreadLocalHeader64> Headers;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:309 | TryStageStreamingAssetsUriToCacheAsync
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory(cacheDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:400 | TryDeleteFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:1848 | TryReadWholeFileIntoArena
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:2637 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:2672 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/TextureChannelPacker/HectonArmTextureChannelPacker.cs:1301 | _ring
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<TexturePackerTelemetryEntry> _ring;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][54%][EXECUTABLE_CARRIER_STRUCT_REVIEW] EXECUTABLE_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentJobs.cs:434 | RecordAuxiliaryTelemetryPass
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordAuxiliaryTelemetryPass`
  Required action: This signal-like name belongs to an executable carrier with Execute(), not a binary payload contract. Keep NativeArray handles owner-owned; add StructLayout only if the carrier itself is serialized or copied as raw bytes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentJobs.cs:438 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<AuxiliaryTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentRouterRuntime.cs:1417 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentRouterRuntime.cs:1421 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentRouterRuntime.cs:1445 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<AuxiliaryTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationContracts.cs:448 | TryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationContracts.cs:452 | TryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationJobs.cs:670 | RecordAirlockTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordAirlockTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationJobs.cs:119 | VfxSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<BubbleSpawnSignal> VfxSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationJobs.cs:120 | AcousticSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MovementAcousticSignal> AcousticSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationJobs.cs:674 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<AirlockTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationRuntime.cs:76 | VfxSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<BubbleSpawnSignal> VfxSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationRuntime.cs:77 | AcousticSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<MovementAcousticSignal> AcousticSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationRuntime.cs:78 | Telemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<AirlockTelemetryEntry> Telemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs:2297 | BallisticsTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct BallisticsTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs:1258 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs:2300 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<BallisticsTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:316 | _signalDetails
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<CombatDamageSignalDetail> _signalDetails;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][80%][CONFIRMED_OWNER_LOCAL_TELEMETRY] LOCAL_NATIVE_TELEMETRY_RING_OWNER_LOCAL | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:337 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<CombatTelemetryEntry> _telemetryRing;`
  Required action: This telemetry/blackbox ring has sentinel ownership, bounded lifetime, and owner-local dump usage. Do not migrate it to GlobalDataVault unless another domain consumes the buffer or the state becomes persistent authority.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:1469 | TryDumpCombatTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:1471 | TryDumpCombatTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:2026 | SignalDetails
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly, NoAlias] public NativeArray<CombatDamageSignalDetail> SignalDetails;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:2058 | ArmorTelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<ArmorPenetrationTelemetryEntry> ArmorTelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs:60 | _statusEffectTelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `private static NativeArray<CombatStatusEffectTelemetryEntry> _statusEffectTelemetryRing;`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
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
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs:1732 | DamageSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[NativeDisableParallelForRestriction, NoAlias] public NativeArray<CombatDamageSignal> DamageSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs:172 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<ArmorPenetrationTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs:176 | MockDetails
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<CombatDamageSignalDetail> MockDetails;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs:180 | TortureDetails
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<CombatDamageSignalDetail> TortureDetails;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs:2190 | Details
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly, NoAlias] public NativeArray<CombatDamageSignalDetail> Details;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs:2233 | Details
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[WriteOnly, NoAlias] public NativeArray<CombatDamageSignalDetail> Details;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs:2293 | Details
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[WriteOnly, NoAlias] public NativeArray<CombatDamageSignalDetail> Details;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:1864 | DumpTelemetryBuffer
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(dumpPath));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:1865 | DumpTelemetryBuffer
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs:1522 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs:1524 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/AbyssalShadowCullingTypes.cs:509 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/AbyssalShadowCullingTypes.cs:513 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:258 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetVector(_CameraPositionId, _cameraPosition.Position);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:259 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetVector(_CameraForwardId, _cameraPosition.Forward);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:261 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetVector(_Plane0Id, _cameraFrustum.Plane0);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:262 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetVector(_Plane1Id, _cameraFrustum.Plane1);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:263 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetVector(_Plane2Id, _cameraFrustum.Plane2);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:264 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetVector(_Plane3Id, _cameraFrustum.Plane3);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:265 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetVector(_Plane4Id, _cameraFrustum.Plane4);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:266 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetVector(_Plane5Id, _cameraFrustum.Plane5);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:267 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetFloat(_BoundsRadiusId, math.max(0.001f, descriptor.BoundsRadius > 0f ? descriptor.BoundsRadius : _defaultBoundsRadius));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:268 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetFloat(_CullDistanceId, cullDistance);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:270 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetFloat(_VramUsedMbId, descriptor.VramUsedMb);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:272 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetVector(_VoxelSdfOriginId, _voxelSdfOrigin);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:273 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetVector(`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:277 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetTexture(_kernel, _VoxelSdfTextureId, _voxelSdfTexture);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:627 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:629 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonJobs.cs:13 | QualitySignal
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MockQualityWeightSignal> QualitySignal;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonJobs.cs:215 | QualitySignal
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<MockQualityWeightSignal> QualitySignal;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonRuntime.cs:78 | MockQualitySignal
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<MockQualityWeightSignal> MockQualitySignal;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs:392 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<TBDRPipelineTelemetryEntry> TelemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs:781 | TryLoadBinaryLimitFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs:807 | TryLoadTextureBudgetFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs:1545 | Ring
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<TBDRPipelineTelemetryEntry> Ring;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
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
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Graphics/Materials/ShinobuMaterialResponseRuntime.cs:1587 | Signals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MockBiomassDensitySignal> Signals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs:2425 | RecordVisualAgingTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct RecordVisualAgingTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs:1560 | OpenDumpStreamCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs:1563 | OpenDumpStreamCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `return new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, TelemetryDumpSnapshotBytes, FileOptions.WriteThrough);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs:2430 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<VisualAgingTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs:2432 | DegradationTelemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<DegradationTelemetryEntry> DegradationTelemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
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
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs:2168 | RecordInventoryTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordInventoryTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs:267 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<InventoryRoutingTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs:1076 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs:1078 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs:2171 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<InventoryRoutingTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs:788 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs:791 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Physics/Buoyancy/AnalyticalGerstnerWaveJobs.cs:445 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[WriteOnly, NoAlias] public NativeArray<WaveMathTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementJobs.cs:1022 | ReduceBuoyancyTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct ReduceBuoyancyTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementJobs.cs:104 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[WriteOnly, NoAlias] public NativeArray<BuoyancyTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementJobs.cs:106 | SleepTelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[WriteOnly, NoAlias] public NativeArray<SleepStateTelemetryEntry> SleepTelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementJobs.cs:1026 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[WriteOnly, NoAlias] public NativeArray<BuoyancyTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementJobs.cs:1028 | SleepTelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[WriteOnly, NoAlias] public NativeArray<SleepStateTelemetryEntry> SleepTelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/BuoyancySimdVectorization.cs:1151 | RecordSimdTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordSimdTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Physics/Buoyancy/BuoyancySimdVectorization.cs:1153 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[WriteOnly, NoAlias] public NativeArray<SimdTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/Cable132/CablePhysicsSolver132.cs:1593 | RecordCableTetherTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal unsafe struct RecordCableTetherTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/Cable132/CablePhysicsSolver132.cs:520 | TryDumpLatestVault
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/Cable132/CablePhysicsSolver132.cs:679 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/Cable132/CablePhysicsSolver132.cs:963 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<TetherTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/Cable132/CablePhysicsSolver132.cs:1598 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<TetherTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs:2219 | RecordShockwaveTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct RecordShockwaveTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs:1493 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<ShockwaveTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs:2224 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<ShockwaveTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsJobs.cs:180 | CrushDepth
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly, NoAlias] public NativeArray<MockCrushDepthSignal> CrushDepth;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsJobs.cs:183 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<ExosuitTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsJobs.cs:186 | HapticSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MechHapticSignalDTO> HapticSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsJobs.cs:187 | SiltSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<SiltExplosionSignal> SiltSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Physics/KCC/HectonKccRuntime_SmokeTest.cs:311 | Telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[NativeDisableParallelForRestriction, NoAlias] public NativeArray<KccSmokeTelemetryEntry> Telemetry;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Physics/KCC/HectonKccRuntime_SmokeTest.cs:313 | MockDesyncSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[NativeDisableParallelForRestriction, NoAlias] public NativeArray<DesyncDetectedSignal> MockDesyncSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs:2036 | KinematicTelemetryAggregateJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct KinematicTelemetryAggregateJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs:2110 | KccEnvironmentTelemetryAggregateJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct KccEnvironmentTelemetryAggregateJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs:2041 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<KinematicTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs:2115 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<KccEnvironmentTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsJobs.cs:30 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[WriteOnly, NoAlias] public NativeArray<SeaglideTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsJobs.cs:35 | AudioSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[WriteOnly, NoAlias] public NativeArray<SeaglideAudioSignalDTO> AudioSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsJobs.cs:36 | CavitationSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[WriteOnly, NoAlias] public NativeArray<SeaglideCavitationVfxSignalDTO> CavitationSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsJobs.cs:171 | CavitationSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[WriteOnly, NoAlias] public NativeArray<SeaglideCavitationVfxSignalDTO> CavitationSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsJobs.cs:463 | AudioSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[WriteOnly, NoAlias] public NativeArray<SeaglideAudioSignalDTO> AudioSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsJobs.cs:509 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<SeaglideTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Physics/Vehicles/SubmarineBallastBuoyancyContracts.cs:561 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[NoAlias, NativeDisableParallelForRestriction] public NativeArray<SubmarineBallastTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:1200 | RecordGyroTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct RecordGyroTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:1296 | HydrodynamicsTelemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<SubmarineHydrodynamicsTelemetry> HydrodynamicsTelemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:1576 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<SubmarineKinematicTelemetry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageJobs.cs:450 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<VehicleDamageTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Plugins/Crest/HectonCrestOceanDepthCacheRuntimeBridge.cs:92 | HectonSaveDepthCacheTexturePng
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Plugins/Crest/HectonCrestOceanDepthCacheRuntimeBridge.cs:115 | HectonSaveDepthCacheTexturePng
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read, 65536, FileOptions.SequentialScan);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:956 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/QA/Headless/Shinobu38QaWatchdogRuntime.cs:2267 | Snapshot
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<TelemetrySnapshotDTO> Snapshot;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/QA/Headless/Shinobu38QaWatchdogRuntime.cs:2277 | RebaseSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<MockRebaseSignal> RebaseSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/AbyssalCaustics/AbyssalDeferredCausticsRuntime.cs:1480 | EnsureBlackBoxDumpPathCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(_blackBoxDumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/AbyssalCaustics/AbyssalDeferredCausticsRuntime.cs:1502 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Rendering/BilateralDrs/BilateralDrsUpscalerContracts.cs:133 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NativeDisableContainerSafetyRestriction] [WriteOnly] [NoAlias] public NativeArray<UpscalerTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerRuntime.cs:1575 | EnsureBlackBoxDumpPathCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(_blackBoxDumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerRuntime.cs:1937 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/OceanSinglePass/OceanSinglePassContracts.cs:462 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/OceanSinglePass/OceanSinglePassContracts.cs:464 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/OceanSinglePass/ShorelineFoamGraftContracts.cs:1036 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/OceanSinglePass/ShorelineFoamGraftContracts.cs:1038 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1426 | Render
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `properties.SetFloat(_FloraScatterVisualPayloadEnabledId, _cachedQualityWeight01);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1429 | Render
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `properties.SetVector(_GlobalFloatingOffsetId, _aupShiftOffset);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1430 | Render
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `properties.SetVector(_HectonFloatingOriginOffsetId, _aupShiftOffset);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1431 | Render
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `properties.SetFloat(_LodNearDistanceId, SanitizePositiveFinite(lowTierCullDistanceMeters, 100f));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1432 | Render
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `properties.SetFloat(_LodFarDistanceId, SanitizePositiveFinite(_effectiveCullDistanceMeters, ResolveDesiredCullDistance()));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1433 | Render
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `properties.SetFloat(_LodTransitionRangeId, SanitizeNonNegativeFinite(lodCrossfadeRangeMeters) * _cachedQualityWeight01);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1989 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1991 | DumpBlackBox
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
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs:1503 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<ToolKinematicsTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs:1504 | MockTriggerSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<MockTriggerPullSignal> MockTriggerSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs:1505 | CarveRequests
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<MockCarveRequestSignal> CarveRequests;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs:1506 | HeatSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<ToolHeatSignal> HeatSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs:1507 | SparkRequests
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<VfxSparkRequestSignal> SparkRequests;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:1597 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:1599 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:2113 | DispatchDirtyScreens
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `terminalBlitCompute.SetTexture(_blitKernel, TerminalTextureArrayId, _terminalTextureArray);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:2119 | DispatchDirtyScreens
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `terminalBlitCompute.SetTexture(_blitKernel, FontSdfAtlasId, fontSdfAtlas);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:2124 | DispatchDirtyScreens
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `terminalBlitCompute.SetFloat(TimeSeedId, ownerFrame * HectonPhysicsContract.FixedDeltaTimeSeconds);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:2125 | DispatchDirtyScreens
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `terminalBlitCompute.SetFloat(HectonDiegeticGlitchQualityWeightId, _globalQualityWeight);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:3645 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:3647 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:3893 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:4017 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime_TerminalProjection.cs:570 | WriteTerminalInputBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime_TerminalProjection.cs:573 | WriteTerminalInputBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:558 | PowerSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly] [NoAlias] public NativeArray<MockPowerStateSignal> PowerSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:559 | DamageSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly] [NoAlias] public NativeArray<MockDamageScalarSignal> DamageSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:560 | PowerStatusSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly] [NoAlias] public NativeArray<MockPowerStatusSignal> PowerStatusSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs:1786 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs:1790 | DumpBlackBox
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:1586 | LoadProfilesFromDiskOrDefaults
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, ProfileByteCount, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:3305 | WriteBlackBoxDumpBytes
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:3307 | WriteBlackBoxDumpBytes
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:3481 | WeatherSignal
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<MockWeatherSignal> WeatherSignal;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:3483 | PredatorSignal
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<BiolumMockPredatorProximitySignal> PredatorSignal;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:3530 | WeatherSignal
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<MockWeatherSignal> WeatherSignal;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:3532 | PredatorSignal
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<BiolumMockPredatorProximitySignal> PredatorSignal;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:2089 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:2099 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:1644 | PlasmaBeamArgsTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct PlasmaBeamArgsTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:1229 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:1231 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:1377 | MockSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MockLaserFireSignal> MockSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:1649 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PlasmaBeamTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:1395 | DumpBiolumTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:1397 | DumpBiolumTelemetry
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Biomes/BiomeTransitionManagerRuntime.cs:1631 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Biomes/BiomeTransitionManagerRuntime.cs:1633 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeJobs.cs:472 | BlackBox
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<FloraGenomeBlackBoxEntry> BlackBox;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
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
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralJobs.cs:289 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<CoralGenerationTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralJobs.cs:940 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<CoralGenerationTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs:77 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<CoralGenerationTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs:667 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dir);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs:1226 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(path))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs:1435 | TryWriteDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Create(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageJobs.cs:340 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<WreckageGenerationTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs:71 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<WreckageGenerationTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs:748 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dir);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs:934 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(path))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs:1148 | TryWriteDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Create(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:176 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<GeologyGenerationTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:2627 | RenderDormantOres
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `oreMaterial.SetFloat(_QualityOverkillId, visualWeight);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:2938 | TryWriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read); // COLD ALLOC: FileStream[telemetry dump] — blackbox dump file writer — owner: ProceduralOreSpawner`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs:898 | RegrowthTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct RegrowthTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][80%][CONFIRMED_OWNER_LOCAL_TELEMETRY] LOCAL_NATIVE_TELEMETRY_RING_OWNER_LOCAL | Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs:184 | BlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<WorldRegrowthTelemetryEntry> BlackBox;`
  Required action: This telemetry/blackbox ring has sentinel ownership, bounded lifetime, and owner-local dump usage. Do not migrate it to GlobalDataVault unless another domain consumes the buffer or the state becomes persistent authority.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs:583 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs:594 |
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllBytes(path, dump);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs:903 | BlackBox
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<WorldRegrowthTelemetryEntry> BlackBox;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyJobs.cs:23 | RebaseSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[WriteOnly, NoAlias] public NativeArray<MockAupRebaseSignal> RebaseSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyJobs.cs:63 | HudSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[WriteOnly, NoAlias] public NativeArray<MockHudSignal> HudSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyJobs.cs:65 | RebaseSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly, NoAlias] public NativeArray<MockAupRebaseSignal> RebaseSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyJobs.cs:66 | Telemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[WriteOnly, NoAlias] public NativeArray<AnomalyTelemetryEntry> Telemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:686 | ReadColdBytes
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:993 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(_dumpPath));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:994 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:2668 | PoiBlackBoxValidationJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct PoiBlackBoxValidationJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:686 | TryDumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:688 | TryDumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:936 | Signals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<MockGeologySignal> Signals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:969 | Signals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[NoAlias, ReadOnly] public NativeArray<MockGeologySignal> Signals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:971 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:1158 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:1364 | Signals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[NoAlias, ReadOnly] public NativeArray<MockGeologySignal> Signals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:1370 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:1605 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:1727 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:1868 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:1961 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:2048 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:2091 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:2260 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:2369 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:2458 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:2590 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:2671 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs:80 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<VoxelMeshingTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs:675 | ModifiedSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<VoxelSurfaceModifiedSignal> ModifiedSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs:1343 | TryLoadLookupFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs:1613 | TryWriteDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Create(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs:1628 | TryWriteDumpFiles
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dir);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:276 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<AudioDSPTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:1574 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:1578 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/Editor/AudioSynthesisMemorySovereigntyValidator.cs:806 | Telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<VocalTelemetryEntryDTO> Telemetry;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
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
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/HydraulicErosionForge/Shinobu242/HydraulicErosionForgeJobs.cs:180 | Telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[NativeDisableContainerSafetyRestriction, NoAlias] public NativeArray<ErosionBakeTelemetryEntry> Telemetry;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/OfflineGeometryBakeBlackBox.cs:23 | _ring
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<OfflineGeometryBakeTelemetryEntry> _ring;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Gameplay/Loot/Contracts/LootMagnetContracts.cs:146 | SignalEvents
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<LootMagnetSignalEvent> SignalEvents;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Gameplay/Loot/Contracts/LootMagnetContracts.cs:147 | Telemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<LootMagnetTelemetryEntry> Telemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Gameplay/Loot/Contracts/LootMagnetPullJob.cs:24 | SignalEvents
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<LootMagnetSignalEvent> SignalEvents;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/BaseStructuralWarningDispatcherTypes.cs:758 | WriteStructuralWarningTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct WriteStructuralWarningTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/BaseStructuralWarningDispatcherTypes.cs:221 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[WriteOnly] [NoAlias] public NativeArray<BaseStructuralWarningTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/BaseStructuralWarningDispatcherTypes.cs:761 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<BaseStructuralWarningTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
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
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityTypes.cs:457 | DepthSignal
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<HullMockDepthSignal> DepthSignal;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityTypes.cs:489 | DamageSignals
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly] [NoAlias] public NativeArray<HullMockCombatDamageSignal> DamageSignals;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityTypes.cs:647 | DepthSignal
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[ReadOnly] [NoAlias] public NativeArray<HullMockDepthSignal> DepthSignal;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorRuntime.cs:1543 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorRuntime.cs:1558 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Create(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][54%][BURST_JOB_STRUCT_REVIEW] JOB_STRUCT_LAYOUT_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorTypes.cs:848 | StructuralTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct StructuralTelemetryJob : IJob`
  Required action: This signal-like name belongs to a Burst/job carrier, not a binary payload contract. Keep fields unmanaged and owner-owned; do not add StructLayout unless this job struct is serialized or copied as raw bytes.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorTypes.cs:214 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[WriteOnly] [NoAlias] public NativeArray<StructuralTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorTypes.cs:853 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<StructuralTelemetryEntry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs:565 | DispatchGpuReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetFloat(WaveSampleSeaLevelId, seaLevel);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs:566 | DispatchGpuReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetFloat(OceanTimeId, _timeSeconds);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs:567 | DispatchGpuReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetFloat(OceanQualityId, AsyncBuoyancyReadbackConstants.AuthoritativeQualityWeight);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs:569 | DispatchGpuReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetVector(OceanLocalProjectionId, ToVector4(new float4(cameraProjection.x, cameraProjection.y, maxWavelength, wakeWorldSize)));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs:570 | DispatchGpuReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetVector(OceanWavePhaseBase0Id, ToVector4(phaseBase0));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs:571 | DispatchGpuReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetVector(OceanWavePhaseBase1Id, ToVector4(phaseBase1));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs:572 | DispatchGpuReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetVector(WaveSampleLodId, new Vector4(maxWavelength, activeWaveCount, AsyncBuoyancyReadbackConstants.AuthoritativeQualityWeight, _timeSeconds));`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs:574 | DispatchGpuReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetTexture(_kernelIndex, OceanWakeDisplacementId, wakeDisplacement != null ? wakeDisplacement : Texture2D.blackTexture);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][54%][HOT_PATH_HEURISTIC] GPU_DISPATCH_PARAMETER_HOT_PATH_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs:575 | DispatchGpuReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetVector(OceanShorelineDepthParamsId, shorelineParams);`
  Required action: This hot path updates ComputeShader dispatch parameters. Keep IDs cached and cadence-gate expensive dispatches; do not classify this as an SRP material mutation without render/profiler evidence.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Physics/KCC/Editor/Shinobu355KccSmokeEditorFacade.cs:525 | _telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<HydrodynamicKccRuntime.KccSmokeTelemetryEntry> _telemetry;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][55%][EDITOR_ONLY_REVIEW] EDITOR_SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/Automation/SubmarineAutopilotSdfNavigator.cs:1300 | RecordAutopilotTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct RecordAutopilotTelemetryJob : IJob`
  Required action: Editor/test signal-like structs do not gate runtime, but should not shadow production contracts.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Plugins/Crest/OceanKinematics/OceanKinematicsVaultRuntime.cs:40 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `public NativeArray<OceanKinematicsTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
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
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs:1424 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PowerJacobiStressFrameTelemetry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs:1509 | StressTelemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PowerJacobiStressFrameTelemetry> StressTelemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs:1510 | FuzzTelemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<JacobiFuzzTelemetryEntry> FuzzTelemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs:2065 | FuzzTelemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<JacobiFuzzTelemetryEntry> FuzzTelemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][54%][BORROWED_JOB_VIEW_REVIEW] LOCAL_NATIVE_TELEMETRY_JOB_VIEW_REVIEW | Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs:2129 | Telemetry
  Evidence kind: FIELD_DECLARATION_JOB_VIEW
  Evidence: `[NoAlias] public NativeArray<PowerJacobiStressFrameTelemetry> Telemetry;`
  Required action: This NativeArray telemetry field is a borrowed job/native view, not persistent ownership. Verify the enclosing owner allocates/disposes the backing buffer.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:504 | HeatSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[WriteOnly, NoAlias] public NativeArray<ToolHeatSignal> HeatSignals;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:505 | SparkRequests
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[WriteOnly, NoAlias] public NativeArray<VfxSparkRequestSignal> SparkRequests;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:506 | TelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `[WriteOnly, NoAlias] public NativeArray<ToolKinematicsTelemetryEntry> TelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:764 | CarveRequests
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[WriteOnly, NoAlias] public NativeArray<MockCarveRequestSignal> CarveRequests;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs:254 | _telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<HadalTrenchBakeTelemetryEntry> _telemetry;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][55%][EDITOR_ONLY_REVIEW] EDITOR_SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/StaticCaveSdfBaker/Editor/StaticCaveSdfBakePipeline.cs:993 | StaticCaveSdfBakeTelemetryBuffer
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal ref struct StaticCaveSdfBakeTelemetryBuffer`
  Required action: Editor/test signal-like structs do not gate runtime, but should not shadow production contracts.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/World/StaticCaveSdfBaker/Editor/StaticCaveSdfBakePipeline.cs:995 | Ring
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeArray<StaticCaveSdfTelemetryEntry> Ring;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Editor/DamageBake/HabitatDamageBakePipeline.cs:688 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<HabitatDamageBakeTelemetryEntry> _telemetryRing;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:84 | OceanSurfaceTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct OceanSurfaceTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereContracts.cs:89 | OceanSurfaceTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct OceanSurfaceTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:402 | StructuralTelemetryEntry
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
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:185 | MockAcousticSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockAcousticSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/VFX/VolumetricSiltContracts.cs:45 | MockAcousticSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockAcousticSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardTypes.cs:242 | ThermalTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct ThermalTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/World/AbyssalThermalManager.cs:124 | ThermalTelemetryEntry
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
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Core/Contracts/DrsContracts.cs:352 | MockQualityWeightSignal
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
