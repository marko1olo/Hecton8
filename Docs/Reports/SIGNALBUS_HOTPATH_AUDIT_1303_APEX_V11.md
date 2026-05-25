# SHINOBU_02 Signal Bus Contract Audit CLI

Evidence Class: STATIC_SOURCE_CLASSIFIED
Scope: Full
Generated UTC: 2026-05-25T18:38:50.6223584Z

## Summary

- Files scanned: 2420 C# / 71 compute
- Signal-like definitions found: 861
- Signal definitions still in Core/GlobalSignals.cs: 0
- Pack=1 layouts: 0
- Runtime signal Pack=1 layouts: 0
- Runtime signal transitive Pack=1 field hits: 0
- Signal-like definitions without nearby StructLayout: 90
- Managed event surface hits: 0
- Local native telemetry ring hits: 45
- Registered local telemetry rings: 21
- Local native signal queue hits: 18
- Compute 1024-thread-group hits: 0
- Hot-path heuristic hits: 213
- Cold/fatal sync I/O review hits: 624
- Assembly contract boundary hits: 1
- Errors: 2
- Warnings: 529
- Infos: 664
- Confirmed/probable errors at confidence >= 90: 2
- Review-only findings below confidence 75: 1012

## Rule Breakdown

- COLD_OR_FATAL_SYNC_IO_REVIEW: total 624, errors 0, warnings 0, infos 624, avg confidence 64
- SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW: total 193, errors 0, warnings 193, infos 0, avg confidence 64
- RUNTIME_SYNC_FILE_IO_REVIEW: total 151, errors 0, warnings 151, infos 0, avg confidence 76
- SIGNAL_LAYOUT_REVIEW: total 84, errors 0, warnings 84, infos 0, avg confidence 65
- DUPLICATE_SIGNAL_LIKE_NAME_REVIEW: total 40, errors 0, warnings 40, infos 0, avg confidence 74
- ZERO_GC_HOT_PATH_ALLOCATION_REVIEW: total 19, errors 0, warnings 19, infos 0, avg confidence 66
- LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT: total 18, errors 0, warnings 18, infos 0, avg confidence 88
- EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW: total 17, errors 0, warnings 0, infos 17, avg confidence 56
- LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW: total 10, errors 0, warnings 0, infos 10, avg confidence 70
- POSSIBLE_ORPHANED_SIGNAL_QUEUE: total 8, errors 0, warnings 8, infos 0, avg confidence 82
- LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY: total 7, errors 0, warnings 7, infos 0, avg confidence 73
- EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW: total 6, errors 0, warnings 6, infos 0, avg confidence 60
- EDITOR_SIGNAL_LAYOUT_REVIEW: total 5, errors 0, warnings 0, infos 5, avg confidence 55
- LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW: total 5, errors 0, warnings 0, infos 5, avg confidence 68
- LOCAL_NATIVE_TELEMETRY_RING_ROOT_OWNER: total 3, errors 0, warnings 0, infos 3, avg confidence 91
- DUPLICATE_RUNTIME_SIGNAL_NAME: total 2, errors 2, warnings 0, infos 0, avg confidence 92
- ZERO_GC_HOT_PATH_ENUMERATION_REVIEW: total 1, errors 0, warnings 1, infos 0, avg confidence 72
- ASMDEF_SIGNAL_CONTRACT_REFERENCE_MISSING: total 1, errors 0, warnings 1, infos 0, avg confidence 85
- EDITOR_SIGNAL_NAME_SHADOWS_RUNTIME: total 1, errors 0, warnings 1, infos 0, avg confidence 68

## Classification Breakdown

- COLD_OR_FATAL_IO_BOUNDARY: 624
- HOT_PATH_HEURISTIC: 213
- IO_PRESSURE_HEURISTIC: 151
- NAME_BASED_REVIEW: 84
- STATIC_CONTRACT_REVIEW: 40
- EDITOR_ONLY_REVIEW: 29
- CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL: 18
- REGISTERED_LOCAL_QUEUE_REVIEW: 10
- PROBABLE_SIGNAL_CORRIDOR_BYPASS: 8
- STATIC_DECLARATION_REVIEW: 7
- SIGNAL_SCRATCH_REVIEW: 5
- CONFIRMED_ROOT_ALLOCATOR_TELEMETRY: 3
- CONFIRMED_RUNTIME_CONTRACT_COLLISION: 2
- COMPILE_WALL_DEPENDENCY_REVIEW: 1

## Findings

- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/BuilderTool.cs:516 | UpdateScreen
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_screenPropBlock.SetColor(PropScreenColor, screenColor);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/ConstructionManager.cs:177 | _deconstructionBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<HabitatDeconstructionTelemetryEntry> _deconstructionBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ConstructionManager.cs:1486 | DumpShinobu336BlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ConstructionManager.cs:1488 | DumpShinobu336BlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ConstructionManager.cs:1793 | DumpDeconstructionBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ConstructionManager.cs:1795 | DumpDeconstructionBlackBox
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
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/EncounterDirector.cs:290 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<EncounterDirectorBlackBoxEntry> _blackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/EncounterDirector.cs:1121 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(parent);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/EncounterDirector.cs:1123 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/FabricationAssemblerRuntime.cs:695 | TryIngestFabricationTimingsCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/FabricationAssemblerRuntime.cs:1206 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/FabricationAssemblerRuntime.cs:1208 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Fabricator.cs:2648 | FlushApplyErrorFeedback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_errorFeedbackBlock.SetColor(EmissionColorId, color);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/FaunaDirector.cs:211 | AcousticPanicCommand
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct AcousticPanicCommand`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/FlashlightTool.cs:169 | UpdatePowerIndicator
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetFloat(_ToolBatteryNormalizedID, math.saturate(batteryCharge));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/FlashlightTool.cs:172 | UpdatePowerIndicator
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetColor(_EmissionColorID, Color.black);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][66%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ALLOCATION_REVIEW | Assets/_Project/Scripts/GameTickManager.cs:815 | TickList
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_items    = new List<T>(initialCapacity);`
  Required action: Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.
- [WARN][66%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ALLOCATION_REVIEW | Assets/_Project/Scripts/GameTickManager.cs:816 | TickList
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_toAdd    = new List<T>(16);`
  Required action: Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.
- [WARN][66%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ALLOCATION_REVIEW | Assets/_Project/Scripts/GameTickManager.cs:817 | TickList
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_toRemove = new List<T>(16);`
  Required action: Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/GlobalPhysicsStateManager.cs:3299 | DumpPhysicsCullingBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/GlobalPhysicsStateManager.cs:3315 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonBoidController.cs:1368 | DispatchComputeFrustumCulling
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `boidShader.SetFloat(ShaderProps.BoidCullingRadius, Mathf.Max(0.01f, fishScale * DefaultBoidCullingRadiusScale));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonBoidController.cs:1440 | RenderBoids
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_materialProps.SetFloat(ShaderProps.BoidUseVisibleIndices, 1f);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonBoidController.cs:1441 | RenderBoids
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_materialProps.SetFloat(ShaderProps.FoveatedVatTimeScale,`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6060 | UpdateSkyboxBlend
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `blendedSkyboxMaterial.SetFloat(_ID_Blend, _currentBlend);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6076 | UpdateStarIntensity
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `blendedSkyboxMaterial.SetFloat(_ID_StarIntensity, _currentStarIntensity);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6462 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetVector(_ID_FresnelSunDir, new Vector4(toSun.x, toSun.y, toSun.z, 0));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6463 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_BacklitIntensity, backlitIntensity);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6464 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_EquatorialSpeed, equatorialRotationSpeed);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6465 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_PolarMultiplier, polarRotationMultiplier);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6466 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_PlanetPhase, _currentPhase);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6467 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_StormEmission, stormEmissionIntensity * ResolveStormEmissionMultiplier());`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6468 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_SunBacklitFactor, _currentBacklitFactor);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6469 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_GlobalRotation, _rotationPhase);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6470 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_GameTime, _gameTime);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6471 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_NightBlend, _currentBlend);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6472 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_AtmosphereTransmittanceWeight, _atmosphereTransmittanceWeight);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6473 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetFloat(_ID_AtmosphereInscatterWeight, _atmosphereInscatterWeight);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6475 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetColor(_ID_SkyColorZenith, _resolvedSkyZenith);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6476 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetColor(_ID_SkyColorHorizon, _resolvedSkyHorizon);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6477 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetColor(_ID_SkyColorNadir, _resolvedSkyNadir);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6480 | UpdateAegirMaterial
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_aegirMPB.SetVector(_ID_WindDirection, _skyMaterial.GetVector(_ID_WindDirection));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6506 | UpdateMoonMaterialOverrides
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_moonMPB.SetFloat(`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6509 | UpdateMoonMaterialOverrides
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_moonMPB.SetFloat(`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6512 | UpdateMoonMaterialOverrides
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_moonMPB.SetFloat(_ID_HectonMoonPhase01, phase01);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6513 | UpdateMoonMaterialOverrides
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_moonMPB.SetFloat(_ID_HectonMoonPhaseTextureIndex, ResolveMoonPhaseTextureIndex(phase01));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6838 | DumpCelestialBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6840 | DumpCelestialBlackBox
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
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/HectonFluidEngine.cs:1304 | _oceanSurfaceTelemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<OceanSurfaceTelemetryEntry> _oceanSurfaceTelemetry;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/HectonFluidEngine.cs:1317 | _maelstromTelemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<MaelstromTelemetryEntry> _maelstromTelemetry;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/HectonFluidEngine.cs:1457 | _fluidAdvectionTelemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<FluidAdvectionTelemetryEntry> _fluidAdvectionTelemetry;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/HectonFluidEngine.cs:1522 | _abyssalFlowTelemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<AbyssalFlowTelemetryEntry> _abyssalFlowTelemetry;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:2617 | DumpOceanSurfaceTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:2619 | DumpOceanSurfaceTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:4142 | WriteFluidAdvectionTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:4144 | WriteFluidAdvectionTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:5739 | DumpMaelstromTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:5741 | DumpMaelstromTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:6730 | DispatchAbyssalVortexImpulses
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `abyssalFlowFieldCompute.SetTexture(_gpuAbyssalVortexKernel, _AbyssalFlowTextureRWId, _gpuAbyssalFlowReadTexture);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:6731 | DispatchAbyssalVortexImpulses
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `abyssalFlowFieldCompute.SetVector(_AbyssalFlowVortexSphereId, sphere);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:6732 | DispatchAbyssalVortexImpulses
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `abyssalFlowFieldCompute.SetVector(_AbyssalFlowVortexAxisStrengthId, axisStrength);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:6877 | DumpAbyssalFlowTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:6879 | DumpAbyssalFlowTelemetryOnce
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
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:5549 | UpdateHudFogLuminanceDownsample
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `hudFogLuminanceCompute.SetTexture(_hudFogLuminanceKernel, _HectonHudFogSourceId, sourceTexture);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:5550 | UpdateHudFogLuminanceDownsample
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `hudFogLuminanceCompute.SetTexture(_hudFogLuminanceKernel, _HectonHudFogLuminanceOutputId, _hudFogLuminanceTexture);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:5551 | UpdateHudFogLuminanceDownsample
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `hudFogLuminanceCompute.SetVector(`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonVoxelEngine.cs:6804 | DumpVoxelMeshPipelineBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonVoxelEngine.cs:6806 | DumpVoxelMeshPipelineBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][66%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ALLOCATION_REVIEW | Assets/_Project/Scripts/ItemCatalog.cs:1569 | SyncAddressableWorldPrefabEntries
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `worldPrefabAddressables = new List<WorldPrefabAddressableEntry>(allItems.Count);`
  Required action: Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/LocRegistry.cs:1565 | TryApplyLocOverridesCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/PlayerInventory.cs:552 | _inventoryBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<InventoryTelemetryEntry> _inventoryBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/PlayerInventory.cs:555 | _salinityCorrosionBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<SalinityCorrosionTelemetryEntry> _salinityCorrosionBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:4943 | DumpSalinityCorrosionBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:4945 | DumpSalinityCorrosionBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:5109 | DumpInventoryBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:5111 | DumpInventoryBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/RepairTool.cs:1991 | DumpRepairBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/RepairTool.cs:1993 | DumpRepairBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/ResourceNode.cs:1147 | UpdateMeltProperties
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_propertyBlock.SetVector(_MeltCenterId, _localHitPoint);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/ResourceNode.cs:1148 | UpdateMeltProperties
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_propertyBlock.SetFloat(_MeltRadiusId, meltRadius);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/RuntimeDiagnosticsTrace.cs:86 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][66%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ALLOCATION_REVIEW | Assets/_Project/Scripts/RuntimeDiagnosticsTrace.cs:197 | FlushSuppressedDuplicates
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `List<string> channels = new List<string>(_suppressedDuplicateCountByChannel.Keys);`
  Required action: Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.
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
- [WARN][66%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ALLOCATION_REVIEW | Assets/_Project/Scripts/SaveThumbnailSystem.cs:128 | RenderRequest
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `new Dictionary<string, Texture2D>(MaxCachedTextures, StringComparer.OrdinalIgnoreCase);`
  Required action: Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/ScannerTool.cs:464 | ScannerBlackBoxEntry
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ScannerBlackBoxEntry`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/ScannerTool.cs:731 | UpdatePowerIndicator
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetColor(_EmissionColorID, Color.black);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ScannerTool.cs:1328 | DumpScannerBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ScannerTool.cs:1330 | DumpScannerBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/SeamGapDitherRenderer.cs:230 | FlushQueuedSeamDitherVisuals
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `drawMaterial.SetVector(_CameraPositionId, ResolveCameraRuntimePosition(targetCamera));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/SeamGapDitherRenderer.cs:231 | FlushQueuedSeamDitherVisuals
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `drawMaterial.SetFloat(_MaxCameraDistanceId, Mathf.Max(0.5f, maxCameraDistance));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SpatialAudioManager.cs:5154 | DumpVirtualVoiceBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SpatialAudioManager.cs:5156 | DumpVirtualVoiceBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SpatialAudioManager.cs:5212 | TryLoadAcousticLutFallbackCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `byte[] bytes = File.ReadAllBytes(path); // COLD ALLOC: byte[524288] - one-shot Sabine RT60+damping fallback read - owner: SpatialAudioManager`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SpatialAudioManager.cs:8200 | DumpAcousticPortalBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SpatialAudioManager.cs:8202 | DumpAcousticPortalBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SubmarineFluidDynamics.cs:6063 | WriteHydroBlackBoxDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SubmarineFluidDynamics.cs:6065 | WriteHydroBlackBoxDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1536 | DispatchLeakPlumeCompute
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `leakPlumeCompute.SetFloat(_LeakDeltaTimeId, safeFixedDeltaTime);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1537 | DispatchLeakPlumeCompute
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `leakPlumeCompute.SetFloat(_LeakTimeId, ResolveLeakPlumeClockSeconds());`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1538 | DispatchLeakPlumeCompute
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `leakPlumeCompute.SetVector(_LeakParamsId, new Vector4(LeakPlumeParticleCapacity, MaxActiveBreaches, 0f, 0f));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1597 | RenderLeakPlumeParticles
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_leakPlumeDrawProperties.SetFloat(_LeakUseParticleBufferId, 1f);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1598 | RenderLeakPlumeParticles
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_leakPlumeDrawProperties.SetFloat(_LeakParticleSizeId, math.max(0.01f, leakPlumeParticleSizeMeters));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1600 | RenderLeakPlumeParticles
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_leakPlumeDrawProperties.SetVector(_LeakCameraRightId, cameraRight);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1601 | RenderLeakPlumeParticles
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_leakPlumeDrawProperties.SetVector(_LeakCameraUpId, cameraUp);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1761 | DumpDamageControlTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1763 | DumpDamageControlTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/TetherManager.cs:148 | TetherManagerTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct TetherManagerTelemetryEntry`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VoxelDeltaProcessor.cs:5777 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VoxelDeltaProcessor.cs:5779 | DumpBlackBox
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
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsJobs.cs:889 | AtmosphereTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct AtmosphereTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:1193 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:1194 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:1241 | ReadCsvFileNoStringAlloc
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:864 | TryLoadLegacyWeatherFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:988 | TryLoadBeaufortProfilesCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1028 | TryLoadBeaufortProfilesCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1313 | DispatchWaveHeightReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetFloat(WaveSampleSeaLevelId, SeaLevel);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1314 | DispatchWaveHeightReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetFloat(OceanTimeId, _timeSeconds);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1315 | DispatchWaveHeightReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetFloat(OceanQualityId, _globalQualityWeight);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1317 | DispatchWaveHeightReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetVector(OceanLocalProjectionId, ToVector4(lod.CameraAupLocalXZ));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1318 | DispatchWaveHeightReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetVector(OceanWavePhaseBase0Id, ToVector4(phase.PhaseBase0));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1319 | DispatchWaveHeightReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetVector(OceanWavePhaseBase1Id, ToVector4(phase.PhaseBase1));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1320 | DispatchWaveHeightReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetVector(WaveSampleLodId, new Vector4(maxWavelength, activeWaveCount, _globalQualityWeight, _timeSeconds));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1711 | DumpTelemetryToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1713 | DumpTelemetryToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:2111 | ScanTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ScanTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:1247 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:1251 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:1353 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:10733 | DumpGranularTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:10748 | WriteGranularTelemetryDumpCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:10784 | DumpPrologueTransitionTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:10794 | WritePrologueTransitionTelemetryDumpCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:161 | VocalWarningTelemetrySnapshot
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct VocalWarningTelemetrySnapshot`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:1194 | DumpTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:1196 | DumpTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:1733 | HandleH8MemoryFatalLog
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:4958 | PrepareBackgroundDomainHandshake
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(telemetryPath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:5002 | InspectPreviousBootState
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/BaseModuleCatalogRuntime.cs:429 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/BaseModuleCatalogRuntime.cs:892 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/BaseModuleCatalogRuntime.cs:896 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Create(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Construction/BulkheadContainmentJobs.cs:481 | RecordBulkheadTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct RecordBulkheadTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime.cs:197 | TryLoadProfilesFromCsvFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime_HatchLocks.cs:827 | TryApplyHatchProfilesCsvFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:853 | s_DroneAStarTelemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `private static NativeArray<DroneAStarTelemetry> s_DroneAStarTelemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5354 | RenderRealHeadlessFleet
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `s_DroneProceduralMaterial.SetVector(s_DroneProceduralCameraOriginPropertyId, new Vector4(origin.x, origin.y, origin.z, 0f));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5355 | RenderRealHeadlessFleet
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `s_DroneProceduralMaterial.SetInt(s_UsePhantomColorsPropertyId, 0);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5394 | RenderPhantomSwarm
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `s_PhantomDronesCompute.SetVector(s_PhantomAnchorPropertyId, new Vector4(anchor.x, anchor.y, anchor.z, 0f));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5395 | RenderPhantomSwarm
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `s_PhantomDronesCompute.SetFloat(s_PhantomTimePropertyId, s_PhantomDronePhaseSeconds);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5396 | RenderPhantomSwarm
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `s_PhantomDronesCompute.SetFloat(s_PhantomBaseRadiusPropertyId, PhantomDroneOrbitRadiusMeters);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5397 | RenderPhantomSwarm
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `s_PhantomDronesCompute.SetFloat(s_PhantomVerticalAmplitudePropertyId, PhantomDroneVerticalAmplitudeMeters);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5398 | RenderPhantomSwarm
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `s_PhantomDronesCompute.SetFloat(s_PhantomScalePropertyId, PhantomDroneScaleMeters);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5411 | RenderPhantomSwarm
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `s_DroneProceduralMaterial.SetVector(s_DroneProceduralCameraOriginPropertyId, Vector4.zero);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5412 | RenderPhantomSwarm
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `s_DroneProceduralMaterial.SetInt(s_UsePhantomColorsPropertyId, 1);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5635 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5637 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5992 | TryApplyDroneSpecsCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(resolvedPath))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager_Transactions.cs:1447 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager_Transactions.cs:1449 | 
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorData.cs:667 | TryLoadProfilesFromCsvFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorData.cs:809 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorData.cs:811 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:281 | _floodBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<HabitatFloodBlackBoxEntry> _floodBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:2458 | DumpFloodBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:2460 | DumpFloodBlackBox
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs:871 | DumpHolographyTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/ShinobuSocketConstructionData.cs:873 | DumpHolographyTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs:1040 | DumpFailure
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs:1042 | DumpFailure
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
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs:101 | _snapshotBuffer
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<TelemetryEvent> _snapshotBuffer;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs:521 | TryLoadHapticProfilesFromCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs:598 | DumpHapticTelemetryIfNeeded
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs:603 | DumpHapticTelemetryIfNeeded
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs:1626 | TryReadCsvFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs:1912 | DumpScalabilityDictatorBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs:1936 | DumpScalabilityDictatorBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs:2018 | DumpScalabilityDictatorBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1255 | TryStageInputProfileCsvFromFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1571 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1573 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_inputReplayStream = new FileStream(replayPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1722 | DumpDeterministicInputBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1726 | DumpDeterministicInputBlackBox
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1598 | LoadValidationRulesCsvInternal
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/RebindingManager.cs:374 | TryLoadOverridesFromFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string json = File.ReadAllText(path);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/RebindingManager.cs:440 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/RebindingManager.cs:442 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllText(tempPath, json);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/RebindingManager.cs:445 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/RebindingManager.cs:457 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(tempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/RebindingManager.cs:475 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/RebindingManager.cs:488 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(tempPath);`
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3560 | ParseMasterExecutionPriorityCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:4564 | DumpDispatcherBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:4581 | DumpDispatcherBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:2680 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllBytes(path, bytes);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs:1861 | EcosystemTelemetryReductionJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal unsafe struct EcosystemTelemetryReductionJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs:764 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs:766 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs:812 | TryLoadBiomeSpecsCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs:1465 | InitializeNutrientTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct InitializeNutrientTelemetryJob : IJobParallelFor`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs:1841 | RecordNutrientTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct RecordNutrientTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs:982 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs:984 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime.cs:1031 | TryLoadProfilesCsvCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime_Carrion.cs:1569 | RecordCarrionTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct RecordCarrionTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime_Carrion.cs:650 | DumpCarrionTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime_Carrion.cs:652 | DumpCarrionTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime_Carrion.cs:699 | TryLoadCarrionProfilesCsvCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:2370 | LoadFaultProfilesCsvOrDefaults
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3562 | DumpSeismicDirectorTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3599 | WriteSeismicTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3629 | DumpCelestialTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3643 | WriteCelestialTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3678 | TryPollCsvProfileOverrides
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3771 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3772 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(TelemetryDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:977 | TryHydrateRigDefinitionsBinaryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1244 | TryHydrateRigConstraintsCsvCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.Read);`
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.AcousticSdf.cs:1189 | TryDumpAcousticSdfBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(_acousticSdfDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.AcousticSdf.cs:1221 | EnsureAcousticSdfDumpPathCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.AcousticSdf.cs:1444 | TryLoadAcousticHearingProfilesCsvCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:3156 | TryLoadBehaviorOverridesCsvCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string csv = File.ReadAllText(path);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:3404 | ReadMesofaunaSpeciesProfilesFileCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(path))`
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain_Steering.cs:783 | ReadLeviathanSteeringProfilesFileCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain_Steering.cs:1802 | WriteStableScannerReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain_Steering.cs:1804 | WriteStableScannerReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllText(path, sectionJson + "\n");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain_Steering.cs:1811 | UpsertSharedScannerReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain_Steering.cs:1813 | UpsertSharedScannerReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string existing = File.Exists(path) ? File.ReadAllText(path) : "{\n}\n";`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain_Steering.cs:1829 | UpsertSharedScannerReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllText(path, prefix + comma + property + "\n" + suffix + "\n");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain_Steering.cs:1923 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string text = File.ReadAllText(files[i]);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/StressDrivenSpawnDirector.cs:2212 | ReadFileIntoScratchCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/StressDrivenSpawnDirector.cs:2288 | DumpBlackBoxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(path));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/StressDrivenSpawnDirector.cs:2289 | DumpBlackBoxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Gameplay/BaseAirlock.cs:1493 | FlushStatusLight
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetColor(_emissionPropertyId != 0 ? _emissionPropertyId : _EmissionColorID, _pendingStatusLightColor);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs:1371 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ContextualPhysicalIkTelemetryEntry> _telemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1797 | PersistMmfCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1799 | PersistMmfCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1863 | DumpTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Gameplay/DeployableBeacon.cs:545 | UpdateBeaconLight
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetColor(_emissionPropertyId != 0 ? _emissionPropertyId : _EmissionColorID, lightColor);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Gameplay/EnvironmentalHazard.cs:754 | UpdateIndicator
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetColor(_emissionPropertyId != 0 ? _emissionPropertyId : _EmissionColorID, indicatorColor);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Gameplay/MessageTerminal.cs:887 | FlushStatusLight
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetColor(_emissionPropertyId != 0 ? _emissionPropertyId : _EmissionColorID, _pendingStatusLightColor);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
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
  Evidence: `private static NativeQueue<ToolDepletedSignal> _pendingToolDepletedSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs:190 | _nextFrameToolDepletedSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<ToolDepletedSignal> _nextFrameToolDepletedSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:2355 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:2357 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Gameplay/ScannableFragment.cs:514 | UpdateScanVisuals
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetFloat(_ScanProgressID, progress);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Gameplay/ScannableFragment.cs:515 | UpdateScanVisuals
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetColor(_ScanGlowColorID, scanGlowColor);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Gameplay/ScannableFragment.cs:516 | UpdateScanVisuals
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetFloat(_ScanPulseID, pulse);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1682 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1686 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Gameplay/SealedDoor.cs:591 | UpdateProgressVisuals
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetFloat(_ProgressID, progressNormalized);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Gameplay/SealedDoor.cs:592 | UpdateProgressVisuals
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_mpb.SetColor(_GlowColorID, cutGlowColor);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1709 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1775 | TryApplyCsvOverrides
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_csvOverridePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1945 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1947 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1099 | TryApplyBallastProfilesCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_ballastProfilesCsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 256, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:2563 | DumpBallastTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:2565 | DumpBallastTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:2601 | DumpBallastTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:2603 | DumpBallastTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Gameplay/SuitMeshUpdateEvents.cs:118 | _pendingSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<SuitMeshUpdateSignal> _pendingSignals;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Gameplay/SuitMeshUpdateEvents.cs:119 | _nextFrameSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<SuitMeshUpdateSignal> _nextFrameSignals;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][66%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ALLOCATION_REVIEW | Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:422 | SyncUpgradeCatalogFromFolder
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `List<SuitUpgradeData> upgrades = new List<SuitUpgradeData>(guids.Length);`
  Required action: Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.
- [WARN][72%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ENUMERATION_REVIEW | Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:435 | SyncUpgradeCatalogFromFolder
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `allUpgrades = upgrades.ToArray();`
  Required action: Review this hot-path enumeration/LINQ surface for allocations, boxing, or hidden iterator state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:1358 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:1360 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Gameplay/VehicleCommandSignals.cs:133 | _pendingCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<VehicleCommandSignal> _pendingCommands;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Gameplay/VehicleCommandSignals.cs:134 | _nextFrameCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<VehicleCommandSignal> _nextFrameCommands;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:1416 | ClearComfortTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ClearComfortTelemetryJob : IJobParallelFor`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Gameplay/VRSomaticProvider.HorizonLock.cs:533 | ClearHorizonTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ClearHorizonTelemetryJob : IJobParallelFor`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:1488 | DumpTelemetryRingH8Dump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:1490 | DumpTelemetryRingH8Dump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.CargoSync.cs:558 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.CargoSync.cs:560 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.cs:1552 | RecordSoaInventoryTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordSoaInventoryTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.cs:622 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.cs:624 | 
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/HectonLightingRuntime_DayNightRelay.cs:542 | DumpDayNightBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/HectonLightingRuntime_DayNightRelay.cs:544 | DumpDayNightBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Lighting/HectonLightingRuntime_DayNightRelay.cs:738 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `byte[] bytes = File.ReadAllBytes(fullPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:3188 | InteriorGITelemetryScanJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct InteriorGITelemetryScanJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:1692 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:1725 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:1727 | WriteTelemetryDump
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
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:415 | Queue
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeQueue<FutureCommandEnvelope> Queue;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:988 | TryReloadAllowedOpcodesCsvFromDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:1022 | TryReloadKernelTuningProfilesCsvFromDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:2365 | DumpKernelTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:2366 | DumpKernelTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(KernelDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:2404 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:2405 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(DumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/ModAssetManager.cs:193 | LoadRawTexture
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `pngBytes = File.ReadAllBytes(filePath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs:279 | _pendingCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<ModCommand> _pendingCommands;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs:280 | _pendingAupCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<ModAupCommand> _pendingAupCommands;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs:281 | _pendingRenderCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<ModRenderInstanceCommand> _pendingRenderCommands;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs:623 | ModCullTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ModCullTelemetryEntry`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs:48 | _cullTelemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ModCullTelemetryEntry> _cullTelemetry;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/ModLoader.cs:241 | TryReadManifest
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string json = File.ReadAllText(manifestPath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Narrative/HectonNarrativeDirector_PoiTriggers.cs:836 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Narrative/HectonNarrativeDirector_PoiTriggers.cs:838 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs:985 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string[] lines = File.ReadAllLines(fullSourcePath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs:1002 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllLines(fullSourcePath, lines, new UTF8Encoding(false));`
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs:1331 | TryApplyCsvOverride
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_csvProfilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs:1535 | DumpNetcodeBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs:1538 | DumpNetcodeBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:3442 | TryParseAssetCacheRulesCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:3839 | DumpHeapTelemetryToFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:3841 | DumpHeapTelemetryToFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:2106 | RecordCartographyTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordCartographyTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:812 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:889 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dir);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/PDA/CartographyGridJobs.cs:891 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:438 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, PhysicsCullingLegacyRadiiHeaderBytes, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:1465 | TickPhysicsCullingCsvOverrideMonitor
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][66%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ALLOCATION_REVIEW | Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:1465 | TickPhysicsCullingCsvOverrideMonitor
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/HabitatFluidIncursionJobs.cs:868 | FluidTelemetryRecorderJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct FluidTelemetryRecorderJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/HarpoonTensionSolver328.cs:1859 | RecordTetherTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordTetherTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/HarpoonTensionSolver328.cs:1095 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/HarpoonTensionSolver328.cs:1096 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/TetherAupVerletJobs.cs:842 | RecordTetherAupTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct RecordTetherAupTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/TetherVerletJobs.cs:438 | TetherVerletTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct TetherVerletTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/VerletCableDTOs.cs:1071 | VerletBlackBoxWriteJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct VerletBlackBoxWriteJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuMetabolismJobs.cs:979 | MetabolismTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct MetabolismTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs:552 | TryLoadBiologicalProfilesCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs:620 | TryLoadSuitThermalProfilesCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_suitCsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:1667 | DumpAutopsyReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:1669 | DumpAutopsyReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:1746 | TryLoadLegacyMetabolismTables
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:1872 | LoadCsvOverridesFromDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:1908 | LoadGasCsvOverridesFromDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_gasCsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuRadiationMutationRuntime.cs:693 | TryLoadCsvProfilesCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs:1060 | TryLoadMedicalBayCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(medicalBayPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs:1238 | TryLoadPenaltyCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuSensoryImpairmentRuntime.cs:712 | TryLoadCsvProfilesCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuSuitIntegrityRuntime.cs:788 | DumpAutopsyReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuSuitIntegrityRuntime.cs:790 | DumpAutopsyReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuSuitIntegrityRuntime.cs:824 | LoadCsvProfilesFromDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs:3059 | DumpPowerBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs:3061 | DumpPowerBlackBox
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1601 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1603 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1710 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs:2393 | ThermalGridTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private unsafe struct ThermalGridTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs:1033 | TryLoadCsvFromFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs:1686 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs:1688 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:67 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<WfcOutpostPowerBootTelemetryEntry> _blackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:723 | DumpBlackBox
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Quest/NarrativeDagInspectorWindow.cs:177 | LoadNodeNames
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string[] lines = File.ReadAllLines(NodeNamesPath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Quest/QuestDagDataLoading.cs:61 | 
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs:1261 | TryLoadCsvOverridesCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(s_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs:416 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [WARN][66%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ALLOCATION_REVIEW | Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs:2814 | Execute
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `using FileStream stream = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);`
  Required action: Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs:1918 | VoxelDeltaTelemetryRecordJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct VoxelDeltaTelemetryRecordJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs:1970 | VoxelDeltaDiskLatencyTelemetryPatchJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct VoxelDeltaDiskLatencyTelemetryPatchJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][66%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ALLOCATION_REVIEW | Assets/_Project/Scripts/SaveSystem/WalIntegrityFuzzerCore.cs:1018 | RunSectorPagingStress
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.WriteThrough | FileOptions.RandomAccess))`
  Required action: Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.
- [WARN][66%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ALLOCATION_REVIEW | Assets/_Project/Scripts/SaveSystem/WalIntegrityFuzzerCore.cs:1115 | RunContinuousWriteFuzzer
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.WriteThrough | FileOptions.RandomAccess);`
  Required action: Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs:1303 | TryDumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(path));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs:1304 | TryDumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs:1995 | LoadCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsJobs.cs:791 | ThermalTelemetryRecorderJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct ThermalTelemetryRecorderJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.cs:1174 | TryLoadProfilesCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.ReactorBridge.cs:1044 | TryLoadReactorProfilesCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.ReactorBridge.cs:1089 | TryLoadNuclearReactorProfilesCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][66%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ALLOCATION_REVIEW | Assets/_Project/Scripts/Thermodynamics/OOP_Thermal_Scanner.cs:25 | Run
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `StringBuilder findings = new StringBuilder(2048);`
  Required action: Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.
- [WARN][66%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ALLOCATION_REVIEW | Assets/_Project/Scripts/Thermodynamics/OOP_Thermal_Scanner.cs:33 | Run
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `StringBuilder json = new StringBuilder(4096);`
  Required action: Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/OOP_Thermal_Scanner.cs:66 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string text = File.ReadAllText(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/OOP_Thermal_Scanner.cs:99 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string text = File.ReadAllText(files[i]);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/OOP_Thermal_Scanner.cs:121 | WriteReports
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(reportDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/OOP_Thermal_Scanner.cs:124 | WriteReports
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllText(dedicatedPath, reportJson);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/OOP_Thermal_Scanner.cs:135 | AppendSharedReportSection
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllText(sharedPath, "{\n" + entry + "\n}\n");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/OOP_Thermal_Scanner.cs:139 | AppendSharedReportSection
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string existing = File.ReadAllText(sharedPath).Trim();`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/OOP_Thermal_Scanner.cs:142 | AppendSharedReportSection
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllText(sharedPath, "{\n" + entry + "\n}\n");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/OOP_Thermal_Scanner.cs:151 | AppendSharedReportSection
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllText(sharedPath, "{\n" + body + comma + entry + "\n}\n");`
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs:776 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(dumpPath));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs:790 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:2495 | TelemetryWriteJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct TelemetryWriteJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:1427 | LoadGlitchTableCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_glitchTableFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:1505 | TryApplyCsvOverride
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_csvFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:1962 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:1975 | DumpBlackBox
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs:694 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs:696 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/FontStreamingManager.cs:231 | ProcessSwapBatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `Material targetMaterial = _targetFont != null ? _targetFont.material : null;`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:554 | RenderWaveMesh
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_runtimeMaterial.SetFloat(TubeRadiusId, math.max(0.0005f, tubeRadius));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:560 | RenderWaveMesh
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_runtimeMaterial.SetVector(WaveScalarsId, waveScalars);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:566 | RenderWaveMesh
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_runtimeMaterial.SetVector(WaveLayoutId, waveLayout);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:572 | RenderWaveMesh
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_runtimeMaterial.SetVector(TimeErrorStageId, timeErrorStage);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:722 | DumpTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:724 | DumpTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(TelemetryDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs:611 | TryIngestLoreMetadataCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))`
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
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:869 | RenderHologramMap
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_hologramMapMaterial.SetColor(HologramTintId, edgeTint);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:870 | RenderHologramMap
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_hologramMapMaterial.SetFloat(OpacityId, pointCloudOpacity * 0.72f);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:871 | RenderHologramMap
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_hologramMapMaterial.SetFloat(HologramGlowId, tuning.VisualGlowIntensity);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:872 | RenderHologramMap
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_hologramMapMaterial.SetFloat(HologramQualityId, quality);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:873 | RenderHologramMap
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_hologramMapMaterial.SetVector(`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:939 | RenderPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_pointCloudMaterial.SetVector(AcousticPingSignalId, new Vector4(pingRadius, PointCloudPingBandWidth, _animationTime, pingActive));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:940 | RenderPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_pointCloudMaterial.SetFloat(ActiveSonarRadiusId, activeSonarRadiusMeters);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:941 | RenderPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_pointCloudMaterial.SetFloat(ActiveSonarMaxRangeId, activeSonarMaxRangeMeters);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:942 | RenderPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_pointCloudMaterial.SetFloat(PointSizeId, pointCloudPointSize);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:943 | RenderPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_pointCloudMaterial.SetFloat(OpacityId, pointCloudOpacity);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:944 | RenderPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_pointCloudMaterial.SetFloat(DepthFadeMetersId, pointCloudDepthMeters);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/PDAMapTab.cs:945 | RenderPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_pointCloudMaterial.SetFloat(HeightColorizationId, Smooth01(quality));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2235 | RenderRadarPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_radarRuntimeMaterial.SetFloat(HectonRadarProceduralId, 1f);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2236 | RenderRadarPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_radarRuntimeMaterial.SetFloat(HectonRadarGprProceduralId, 1f);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2242 | RenderRadarPointCloud
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_radarRuntimeMaterial.SetVector(`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2348 | DispatchDamageHologramCompute
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `damageHologramCompute.SetVector(DamageHologramParamsId, computeParams);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2349 | DispatchDamageHologramCompute
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `damageHologramCompute.SetVector(DamageHologramBoundsId, _damageProxyBounds);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2874 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2876 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2923 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1476 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1543 | TryReadCsvBytes
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1750 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1752 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime_PdaScreenProjector.cs:909 | TryReadPdaProfileCsvBytes
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime_PdaScreenProjector.cs:1131 | DumpPdaProjectionBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime_PdaScreenProjector.cs:1133 | DumpPdaProjectionBlackBoxOnce
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
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/CameraJuiceSystem_CameraJuiceBurst.cs:747 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:2010 | ReadSiltProfileCsvBytes
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:3662 | DispatchWakeProximityInjection
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `marineSnowCompute.SetTexture(_wakeProximityKernel, ShaderIds.CaveVoxelSdfTexId, sdfTexture);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:3664 | DispatchWakeProximityInjection
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `marineSnowCompute.SetTexture(_wakeProximityKernel, ShaderIds.TerrainHeightTextureId, heightTexture);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:3695 | DispatchParticleInitializationIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `marineSnowCompute.SetVector(ShaderIds.InitializationParamsId, new Vector4(_allocatedParticleCapacity, 0f, 0f, 0f));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:3702 | DispatchParticleInitializationIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `marineSnowCompute.SetVector(ShaderIds.InitializationParamsId, Vector4.zero);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:4671 | TryWriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:4673 | TryWriteBlackBoxDump
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:1109 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:1111 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:1175 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs:997 | TryLoadMaterialProfilesCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.OpenRead(csvPath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs:2205 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs:2207 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonBiosDiagnosticFeature.cs:139 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.IntensityId, intensity);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonBiosDiagnosticFeature.cs:145 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.LootActiveId, lootActive);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonBiosDiagnosticFeature.cs:151 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetVector(ShaderConstants.LootSphereId, state.LootSphereAup);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonBiosDiagnosticFeature.cs:157 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.DitherStrengthId, ditherStrength);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonBiosDiagnosticFeature.cs:163 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.ScanlineStrengthId, scanlineStrength);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonHolographicEdgeFeature.cs:200 | UpdateEdgeMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_material.SetColor(ShaderConstants.BaseColorId, settings.edgeColor);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonHolographicEdgeFeature.cs:201 | UpdateEdgeMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_material.SetFloat(ShaderConstants.ShellOffsetId, shellOffset);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonHolographicEdgeFeature.cs:202 | UpdateEdgeMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_material.SetFloat(ShaderConstants.FlickerSpeedId, flickerSpeed);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonHolographicEdgeFeature.cs:203 | UpdateEdgeMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_material.SetFloat(ShaderConstants.FlickerCutoffId, flickerCutoff);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonHolographicEdgeFeature.cs:204 | UpdateEdgeMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_material.SetFloat(ShaderConstants.EdgePowerId, edgePower);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonHolographicEdgeFeature.cs:205 | UpdateEdgeMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_material.SetFloat(ShaderConstants.ScanlineStrengthId, scanlineStrength);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs:158 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetVector(ShaderConstants.OriginRadiusId, originRadius);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs:164 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetVector(ShaderConstants.RightDepthId, rightDepth);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs:170 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetVector(ShaderConstants.UpAgeId, upAge);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs:176 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetVector(ShaderConstants.ForwardIntensityId, forwardIntensity);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs:182 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetColor(ShaderConstants.ColorId, settings.projectionColor);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs:188 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.GridScaleId, gridScale);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs:194 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.DitherCutoffId, ditherCutoff);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs:200 | UpdateMaterialIfNeeded
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `material.SetFloat(ShaderConstants.FlickerSpeedId, flickerSpeed);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorARStencilRendererFeature.cs:963 | LoadCsvProfilesCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorARStencilRendererFeature.cs:1137 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorARStencilRendererFeature.cs:1141 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs:1435 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs:1439 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs:1622 | TryDumpReconstructionTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs:1624 | TryDumpReconstructionTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs:1689 | TryLoadAestheticCsvCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.Noir.cs:991 | TryDumpNoirTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.Noir.cs:993 | TryDumpNoirTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.Noir.cs:1102 | TryLoadNoirColorCsvCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs:1574 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs:1580 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs:722 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/SpectrumSystem.cs:3305 | DumpActiveSonarGeoTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/SpectrumSystem.cs:3307 | DumpActiveSonarGeoTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/AbyssalThermalManager.cs:112 | ThermalTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ThermalTelemetryEntry`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/World/AbyssalThermalManager.cs:677 | _thermalTelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ThermalTelemetryEntry> _thermalTelemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs:1017 | RecordBiomeTransitionTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordBiomeTransitionTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs:3216 | ChemicalTelemetryWriteJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private unsafe struct ChemicalTelemetryWriteJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs:1125 | TryLoadEmitterProfilesFromCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs:2071 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs:2073 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/World/DestructibleOrganicManager.cs:560 | _dearLieTelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `private NativeArray<FloraDearLieTelemetryEntry> _dearLieTelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:4031 | TryIngestFloraStiffnessProfilesCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:4662 | DumpWakeBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:4664 | DumpWakeBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:4802 | DumpFloraSwayFieldBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:4804 | DumpFloraSwayFieldBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:7757 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetTexture(_wakeTrailSimulationKernel, _WakeTrailSourceId, _wakeTrailRead);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:7758 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetTexture(_wakeTrailSimulationKernel, _WakeTrailResultId, _wakeTrailWrite);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:7761 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetVector(_WakeTrailScrollUvOffsetId, new Vector4(_pendingWakeTrailScrollUv.x, _pendingWakeTrailScrollUv.y, 0f, 0f));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:7762 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetFloat(_WakeTrailFadeDeltaId, Mathf.Max(0f, fade));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:7763 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetFloat(_WakeTrailDiffusionId, _wakeTrailDiffusion);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:7764 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetFloat(_WakeTrailWaveStrengthId, _wakeTrailWaveStrength);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:7765 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetFloat(_WakeTrailDampingId, _wakeTrailWaveDamping);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:7766 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetFloat(_WakeTrailCurlStrengthId, _wakeTrailCurlStrength);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:7767 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetFloat(_WakeTrailSimulationTimeId, GetCurrentSimulationTimeSeconds());`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:7769 | ExecuteWakeTrailSimulation
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_wakeTrailSimulationCompute.SetVector(`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:1052 | TryDumpTelemetryBuffer
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:1055 | TryDumpTelemetryBuffer
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:2762 | TryLoadCsvMockProfile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(ProbeCsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:2792 | SaveCsvMockProfile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GPUScatterDirector.cs:1372 | DumpScatterBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GPUScatterDirector.cs:1374 | DumpScatterBlackBox
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs:1098 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read); // COLD ALLOC: FileStream[GPR telemetry dump] — blackbox dump file writer — owner: TERRAIN_GPR_SYSTEM`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2617 | DispatchFloraSnapFlagUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_abyssalFlowFieldCompute.SetVector(_GlobalFloatingOffsetId, globalFloatingOffset);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2618 | DispatchFloraSnapFlagUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_abyssalFlowFieldCompute.SetVector(_SubmarineWashSphereId, washSphere);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2619 | DispatchFloraSnapFlagUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_abyssalFlowFieldCompute.SetVector(_SubmarineWashVelocityId, washVelocity);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3091 | DumpFloraGrowthTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3093 | DumpFloraGrowthTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3285 | TryWriteScatterCullTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3287 | TryWriteScatterCullTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3741 | UpdateMotionVectorHistory
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `propertyBlock.SetVector(_PreviousCameraPositionId, previousCameraPosition);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/HectonVoxelStreamingBridge.cs:504 | TickChunkFade
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `state.RuntimeMaterial.SetFloat(ChunkDissolveFadeId, fade01);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:2623 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(_indexedSectorOverrideDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][66%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ALLOCATION_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:2759 | RunIndexedSectorPagingAsync
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `stagedRecords = loadedCount > 0 ? new PersistentWorldDeltaRecord[loadedCount] : Array.Empty<PersistentWorldDeltaRecord>();`
  Required action: Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:3705 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(state.EntityStateTempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:4000 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(state.EntityStateTempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:4305 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(entityStateTempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:4478 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(entityStateTempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:4637 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(entityStateTempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:1224 | _telemetryEntries
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<WreckTelemetryEntry> _telemetryEntries;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:3280 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:3282 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:1546 | ProcessQueuedMaskUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_stampCompute.SetTexture(_stampKernel, _MainTexId, _maskRead);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:1547 | ProcessQueuedMaskUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_stampCompute.SetTexture(_stampKernel, _ResultId, _maskWrite);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:1550 | ProcessQueuedMaskUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_stampCompute.SetVector(_ScrollUvOffsetId, new Vector4(_pendingScrollUv.x, _pendingScrollUv.y, 0f, 0f));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:1551 | ProcessQueuedMaskUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_stampCompute.SetFloat(_RecoveryId, _pendingRecovery);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:1552 | ProcessQueuedMaskUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_stampCompute.SetVector(`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:1731 | ProcessQueuedDamageVolumeUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_damageVolumeCompute.SetTexture(_damageVolumeKernel, _DamageVolumeSourceId, _damageVolumeRead);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:1732 | ProcessQueuedDamageVolumeUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_damageVolumeCompute.SetTexture(_damageVolumeKernel, _DamageVolumeResultId, _damageVolumeWrite);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:1735 | ProcessQueuedDamageVolumeUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_damageVolumeCompute.SetFloat(_DamageVolumeRecoveryId, Mathf.Max(0f, damageVolumeRecoveryPerSecond * Mathf.Max(0f, deltaTime)));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:1736 | ProcessQueuedDamageVolumeUpdate
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_damageVolumeCompute.SetVector(`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5935 | TryDumpFoodChainTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5937 | TryDumpFoodChainTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6100 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6103 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:8274 | DispatchOriginShiftToLiveBoidBuffersVisualSync
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `boidCompute.SetVector(_OriginShiftDeltaId, shiftVector);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs:1780 | TryOpenSectorReadStream
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `stream = new FileStream(handle, FileAccess.Read, 64 * 1024, true); // BACKGROUND_WORKER_IO_1305_STREAMING: SafeFileHandle stream only.`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs:1798 | TryOpenSectorReadStream
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `stream = new FileStream(handle, FileAccess.Read, 64 * 1024, true); // BACKGROUND_WORKER_IO_1305_STREAMING: SafeFileHandle stream only.`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs:2184 | LoadColdStreamingProfile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream( // COLD_BOOT_CONFIG_READ_1305_STREAMING: one-shot CSV ingest into native scratch.`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs:2303 | DumpTelemetryOnWorker
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpFullPath, FileMode.Create, FileAccess.Write, FileShare.Read)) // BLACKBOX_DUMP_1305_STREAMING: worker-only fault dump.`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs:2340 | PrepareDumpPathCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dir);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/VegetationDensityQueryService.cs:1073 | FlushVegetationAudioHandoffVisualSync
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `vegetationAudioMixer.SetFloat(vegetationDensityMixerParameter, density);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/VegetationDensityQueryService.cs:1076 | FlushVegetationAudioHandoffVisualSync
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `vegetationAudioMixer.SetFloat(vegetationAcousticTypeMixerParameter, (float)acousticType);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs:1355 | DumpAbyssalPathTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs:1357 | DumpAbyssalPathTelemetry
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:1933 | ReadCsvBytesCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.OpenRead(path);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:2128 | DumpBlackBoxIfFaulted
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:2130 | DumpBlackBoxIfFaulted
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Create(path);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:871 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `private NativeArray<ChunkResidencyTelemetryEntry> _telemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:5319 | DumpTelemetryToPath
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:5321 | DumpTelemetryToPath
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:1482 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:1484 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs:1031 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
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
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionAnxietyJobs.cs:178 | RecordAnxietyTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordAnxietyTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionJobs.cs:350 | RecordCognitionTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordCognitionTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs:686 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
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
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault_AnxietyDecay.cs:621 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
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
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs:366 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs:1013 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs:1015 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:4900 | CountTelemetryCountersJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct CountTelemetryCountersJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1413 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, share, math.max(1, limit), FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:2688 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:2690 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.FlockingAvoidance.cs:265 | DumpFlockingBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.FlockingAvoidance.cs:267 | DumpFlockingBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:1142 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, share, math.max(1, limit), FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:1190 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:1192 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuSpatialGridSolver.cs:1459 | TryQueueTelemetryDumpPrepared
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(ownerDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuSpatialGridSolver.cs:1462 | TryQueueTelemetryDumpPrepared
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(agentDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuSpatialGridSolver.cs:1711 | TryWriteQueuedDumpFile
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:1002 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:1004 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Animation/FaunaProcedural/ProceduralBoneBlenderJobs.cs:323 | ProceduralBoneTelemetryReduceJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct ProceduralBoneTelemetryReduceJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Animation/FaunaProcedural/ProceduralBoneBlenderTypes.cs:567 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Animation/FaunaProcedural/ProceduralBoneBlenderTypes.cs:569 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:1226 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(BlackBoxDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1266 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1270 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1310 | PollCsvRulesCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs:922 | TryLoadBankIntoVaultCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs:1073 | ReloadDialogueCsvMetadataCold
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs:1715 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs:1717 | DumpBlackBox
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
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8DataBaker.cs:120 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(outputDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8DataBaker.cs:761 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8DataBaker.cs:764 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8DataBaker.cs:774 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(backupPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8DataBaker.cs:1162 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, CsvReadBufferBytes, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:4118 | DumpDefragBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:4142 | DumpPhiVodBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:4166 | DumpShinobu202BlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:4224 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:4261 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:4263 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][91%][CONFIRMED_ROOT_ALLOCATOR_TELEMETRY] LOCAL_NATIVE_TELEMETRY_RING_ROOT_OWNER | Assets/_Project/Scripts/Core/Memory/H8Memory.cs:2229 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_H8MEMORY_SCAN
  Evidence: `private static NativeArray<H8MemoryTelemetryEntry> _blackBox;`
  Required action: This telemetry ring belongs to the H8Memory/GlobalDataVault root allocation layer. Keep dispose coverage, but do not classify the root owner itself as a downstream private non-vault breach.
- [INFO][91%][CONFIRMED_ROOT_ALLOCATOR_TELEMETRY] LOCAL_NATIVE_TELEMETRY_RING_ROOT_OWNER | Assets/_Project/Scripts/Core/Memory/H8Memory.cs:2230 | _eventBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_H8MEMORY_SCAN
  Evidence: `private static NativeArray<H8MemoryTelemetryEntry> _eventBlackBox;`
  Required action: This telemetry ring belongs to the H8Memory/GlobalDataVault root allocation layer. Keep dispose coverage, but do not classify the root owner itself as a downstream private non-vault breach.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/H8Memory.cs:3563 | WriteFatalLeakBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/H8Memory.cs:3586 | WriteFatalLeakBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/VaultLegacyBinaryArchaeology.cs:215 | TryReadHeader
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/VaultLegacyBinaryArchaeology.cs:250 | ParseCsvOverrideStream
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Origin/AupOriginShiftCoordinator.cs:1214 | TryPollCsvOverride
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, scratch.Length, FileOptions.SequentialScan))`
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Scheduling/JobSchedulingProfileCatalog.cs:77 | ParseProfileCsvFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][66%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ALLOCATION_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:75 | Run
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `Thread[] threads = new Thread[producerCount];`
  Required action: Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.
- [WARN][66%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ALLOCATION_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:76 | Run
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `ProducerState[] states = new ProducerState[producerCount];`
  Required action: Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:227 | WriteReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(path));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:246 | WriteReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllText(path, json);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:163 | TryLoadFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:321 | TryReadCsvBytesForLoad
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:869 | DumpToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:871 | DumpToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:985 | TryLoad
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:2836 | TryReadCsvBytesForLoad
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:2904 | DumpToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:2906 | DumpToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:37 | _cursor
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<SignalRingCursorState> _cursor;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:171 | _cursor
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<SignalRingCursorState> _cursor;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:294 | _cursor
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `[NativeDisableParallelForRestriction] private NativeArray<SignalRingCursorState> _cursor;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:200 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory(cacheDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:247 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:1481 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:2211 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:2230 | DumpTelemetry
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
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentRouterRuntime.cs:955 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
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
- [WARN][66%][HOT_PATH_HEURISTIC] ZERO_GC_HOT_PATH_ALLOCATION_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/ArmorPenetrationEditorFacade.cs:363 | Run
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `targetObject = new GameObject("X_008_Armor_Runtime_Proof_Target");`
  Required action: Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs:2297 | BallisticsTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct BallisticsTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs:930 | TryLoadPenetrationCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs:400 | TryLoadStatusEffectProfilesCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `byte[] bytes = File.ReadAllBytes(path);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs:1538 | TryDumpStatusEffectTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs:1540 | TryDumpStatusEffectTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs:959 | DumpArmorTelemetryIfNeeded
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDir);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs:961 | DumpArmorTelemetryIfNeeded
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs:1847 | TryLoadArmorProfilesCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `byte[] bytes = File.ReadAllBytes(path);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:1864 | DumpTelemetryBuffer
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(dumpPath));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:1865 | DumpTelemetryBuffer
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs:1516 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs:1518 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Graphics/Culling/AbyssalShadowCullingJobs.cs:477 | ReduceShadowCullTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct ReduceShadowCullTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/AbyssalShadowCullingRuntime.cs:344 | LoadProfileCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/AbyssalShadowCullingTypes.cs:509 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/AbyssalShadowCullingTypes.cs:513 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:233 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetVector(_CameraPositionId, _cameraPosition.Position);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:234 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetVector(_CameraForwardId, _cameraPosition.Forward);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:236 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetVector(_Plane0Id, _cameraFrustum.Plane0);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:237 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetVector(_Plane1Id, _cameraFrustum.Plane1);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:238 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetVector(_Plane2Id, _cameraFrustum.Plane2);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:239 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetVector(_Plane3Id, _cameraFrustum.Plane3);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:240 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetVector(_Plane4Id, _cameraFrustum.Plane4);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:241 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetVector(_Plane5Id, _cameraFrustum.Plane5);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:242 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetFloat(_BoundsRadiusId, math.max(0.001f, descriptor.BoundsRadius > 0f ? descriptor.BoundsRadius : _defaultBoundsRadius));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:243 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetFloat(_CullDistanceId, cullDistance);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:245 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetFloat(_VramUsedMbId, descriptor.VramUsedMb);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:247 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetVector(_VoxelSdfOriginId, _voxelSdfOrigin);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:248 | Dispatch
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `_activeComputeShader.SetVector(`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:635 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:637 | DumpBlackBox
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
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs:1302 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs:1566 | Dump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs:1568 | Dump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Graphics/Materials/ShinobuMaterialResponseRuntime.cs:1168 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
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
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs:1722 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:1933 | DumpBlackBoxOnceLocked
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:1975 | ResolveBlackBoxDumpPathCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:1099 | TryOpenDumpPath
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:1101 | TryOpenDumpPath
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs:2026 | RecordInventoryTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordInventoryTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs:935 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs:937 | WriteTelemetryDump
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/DynamicPointLightCulling/DynamicPointLightCullingDirector.cs:1529 | TryLoadProfilesFromCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:762 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(path));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:763 | DumpBlackbox
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs:615 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs:618 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AnalyticalGerstnerWaveJobs.cs:442 | RecordWaveMathTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct RecordWaveMathTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AnalyticalGerstnerWaveRuntime.cs:850 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementJobs.cs:1022 | ReduceBuoyancyTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct ReduceBuoyancyTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementRuntime.cs:1847 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs:976 | TryLoadOrdnanceCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(csvPath))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:1414 | ReadCsvBytes
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs:2036 | KinematicTelemetryAggregateJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct KinematicTelemetryAggregateJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs:2110 | KccEnvironmentTelemetryAggregateJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct KccEnvironmentTelemetryAggregateJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsJobs.cs:521 | ReduceSeaglideTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct ReduceSeaglideTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsRuntime.cs:1219 | TryLoadVehicleProfileCsvCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:1200 | RecordGyroTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public unsafe struct RecordGyroTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1114 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1145 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1325 | TryApplyCsvOverrides
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1406 | TryApplyHullProfilesCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_hullProfilesCsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime_Gyroscopes.cs:618 | TryApplyGyroProfilesCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_gyroProfilesCsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageRuntime.cs:851 | TryLoadCsvLayout
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Plugins/Crest/HectonCrestOceanDepthCacheRuntimeBridge.cs:89 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Plugins/Crest/HectonCrestOceanDepthCacheRuntimeBridge.cs:107 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllBytes(absolutePath, pngBytes);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/BatteryChargerLogistics/BatteryChargerLogisticsRuntime.cs:1565 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/BatteryChargerLogistics/BatteryChargerLogisticsRuntime.cs:1567 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/BatteryChargerLogistics/BatteryChargerLogisticsRuntime.cs:1652 | MonitorProfileCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs:1161 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs:1163 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs:941 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs:944 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:844 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/AbyssalCaustics/AbyssalDeferredCausticsRuntime.cs:935 | LoadFileBytesIntoScratch
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/AbyssalCaustics/AbyssalDeferredCausticsRuntime.cs:1456 | EnsureBlackBoxDumpPathCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(_blackBoxDumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/AbyssalCaustics/AbyssalDeferredCausticsRuntime.cs:1478 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerRuntime.cs:1202 | EnsureBlackBoxDumpPathCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(_blackBoxDumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerRuntime.cs:1223 | LoadFileBytesIntoScratch
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerRuntime.cs:1523 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/OceanSinglePass/OceanSinglePassContracts.cs:462 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/OceanSinglePass/OceanSinglePassContracts.cs:464 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/OceanSinglePass/OceanSinglePassRuntime.cs:578 | LoadFileBytesIntoScratch
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/OceanSinglePass/ShorelineFoamGraftContracts.cs:783 | LoadFileBytes
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/OceanSinglePass/ShorelineFoamGraftContracts.cs:997 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/OceanSinglePass/ShorelineFoamGraftContracts.cs:999 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1371 | DispatchCull
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `scatterCullCompute.SetVector(_ScatterParams0Id, constants.Params0);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1372 | DispatchCull
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `scatterCullCompute.SetVector(_ScatterParams1Id, constants.Params1);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1373 | DispatchCull
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `scatterCullCompute.SetVector(_ScatterParams2Id, constants.Params2);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1374 | DispatchCull
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `scatterCullCompute.SetVector(_ScatterParams3Id, constants.Params3);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1375 | DispatchCull
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `scatterCullCompute.SetVector(_ScatterParams4Id, constants.Params4);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1376 | DispatchCull
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `scatterCullCompute.SetVector(_ScatterFrustumPlane0Id, constants.FrustumPlane0);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1377 | DispatchCull
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `scatterCullCompute.SetVector(_ScatterFrustumPlane1Id, constants.FrustumPlane1);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1378 | DispatchCull
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `scatterCullCompute.SetVector(_ScatterFrustumPlane2Id, constants.FrustumPlane2);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1379 | DispatchCull
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `scatterCullCompute.SetVector(_ScatterFrustumPlane3Id, constants.FrustumPlane3);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1380 | DispatchCull
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `scatterCullCompute.SetVector(_ScatterFrustumPlane4Id, constants.FrustumPlane4);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1381 | DispatchCull
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `scatterCullCompute.SetVector(_ScatterFrustumPlane5Id, constants.FrustumPlane5);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1422 | Render
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `properties.SetFloat(_FloraScatterVisualPayloadEnabledId, _cachedQualityWeight01);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1425 | Render
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `properties.SetVector(_GlobalFloatingOffsetId, _aupShiftOffset);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1426 | Render
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `properties.SetVector(_HectonFloatingOriginOffsetId, _aupShiftOffset);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1427 | Render
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `properties.SetFloat(_LodNearDistanceId, SanitizePositiveFinite(lowTierCullDistanceMeters, 100f));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1428 | Render
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `properties.SetFloat(_LodFarDistanceId, SanitizePositiveFinite(_effectiveCullDistanceMeters, ResolveDesiredCullDistance()));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1429 | Render
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `properties.SetFloat(_LodTransitionRangeId, SanitizeNonNegativeFinite(lodCrossfadeRangeMeters) * _cachedQualityWeight01);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1985 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1987 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/WaterOptics/WaterOpticsRuntime.cs:864 | TryLoadProfilesCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/WaterOptics/WaterOpticsRuntime.cs:1094 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/WaterOptics/WaterOpticsRuntime.cs:1096 | DumpTelemetryOnce
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
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:2015 | DispatchDirtyScreens
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `terminalBlitCompute.SetTexture(_blitKernel, TerminalTextureArrayId, _terminalTextureArray);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:2021 | DispatchDirtyScreens
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `terminalBlitCompute.SetTexture(_blitKernel, FontSdfAtlasId, fontSdfAtlas);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:2026 | DispatchDirtyScreens
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `terminalBlitCompute.SetFloat(TimeSeedId, ownerFrame * HectonPhysicsContract.FixedDeltaTimeSeconds);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:2027 | DispatchDirtyScreens
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `terminalBlitCompute.SetFloat(HectonDiegeticGlitchQualityWeightId, _globalQualityWeight);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:2571 | TryMonitorLayoutCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_csvFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:2606 | TryMonitorDecryptionCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_decryptionCsvFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:3492 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:3494 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:3736 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:3860 | 
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs:1543 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs:1547 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(BlackBoxDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs:1785 | TryApplyMaterialColorCsvFileForEditor
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/VR/OpenXRManualOverrideLever.cs:662 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/VR/OpenXRManualOverrideLever.cs:664 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:1586 | LoadProfilesFromDiskOrDefaults
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, ProfileByteCount, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:2263 | EnsureCsvBackgroundWatcher
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:2417 | TryReadCsvOverrideIntoScratch
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, capacity, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:3305 | WriteBlackBoxDumpBytes
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:3307 | WriteBlackBoxDumpBytes
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:2060 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:2070 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs:325 | ExportCurrentTuningCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs:435 | TryReadTuningCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string[] lines = File.ReadAllLines(path);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs:536 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs:542 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs:586 | TryValidateTuningBinary
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs:624 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs:646 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(tempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamContracts.cs:578 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamContracts.cs:580 | 
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Parasites/ParasiteSwarmContracts.cs:296 | TryWriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Parasites/ParasiteSwarmContracts.cs:300 | TryWriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Parasites/ParasiteSwarmGpuRuntime.cs:289 | ReadCsvFileIntoScratch
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, math.max(1, maxBytes), FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:1639 | PlasmaBeamArgsTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct PlasmaBeamArgsTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:1057 | MonitorBeamCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:1224 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:1226 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:1687 | DumpBiolumTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:1689 | DumpBiolumTelemetry
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
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/Biomes/BiomeTransitionManagerRuntime.cs:1623 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/Biomes/BiomeTransitionManagerRuntime.cs:1625 | 
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraAmbientSway/FloraAmbientSwayRuntime.cs:861 | TryLoadBiomeProfilesFromEditorCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeCsvHotloader.cs:71 | TryReadFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeVaultRuntime.cs:526 | DumpBlackBoxIfFatal
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeVaultRuntime.cs:540 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:1751 | DumpBlackBox
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs:1435 | TryWriteDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Create(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:1262 | LoadCsvFileIntoScratch
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); // COLD ALLOC: FileStream[csv] — designer distribution CSV stream into Vault scratch — owner: ProceduralOreSpawner`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:2605 | RenderDormantOres
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `oreMaterial.SetFloat(_QualityOverkillId, visualWeight);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:2916 | TryWriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read); // COLD ALLOC: FileStream[telemetry dump] — blackbox dump file writer — owner: ProceduralOreSpawner`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs:886 | RegrowthTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct RegrowthTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs:583 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs:594 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllBytes(path, dump);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs:1433 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(path))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs:1613 | TryWriteDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Create(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs:1628 | TryWriteDumpFiles
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dir);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:522 | ReloadSynthPresetCsvCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:1574 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:1578 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1370 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1371 | DumpBlackBox
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
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/BaseStructuralWarningDispatcherTypes.cs:1015 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 256, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:2314 | CheckCsvOverrideCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:2344 | CheckMaterialStrengthCsvCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorRuntime.cs:1329 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
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
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs:566 | DispatchGpuReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetFloat(WaveSampleSeaLevelId, seaLevel);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs:567 | DispatchGpuReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetFloat(OceanTimeId, _timeSeconds);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs:568 | DispatchGpuReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetFloat(OceanQualityId, _globalQualityWeight);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs:570 | DispatchGpuReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetVector(OceanLocalProjectionId, ToVector4(new float4(cameraProjection.x, cameraProjection.y, maxWavelength, wakeWorldSize)));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs:571 | DispatchGpuReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetVector(OceanWavePhaseBase0Id, ToVector4(phaseBase0));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs:572 | DispatchGpuReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetVector(OceanWavePhaseBase1Id, ToVector4(phaseBase1));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs:573 | DispatchGpuReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetVector(WaveSampleLodId, new Vector4(maxWavelength, activeWaveCount, _globalQualityWeight, _timeSeconds));`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs:575 | DispatchGpuReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetTexture(_kernelIndex, OceanWakeDisplacementId, wakeDisplacement != null ? wakeDisplacement : Texture2D.blackTexture);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [WARN][64%][HOT_PATH_HEURISTIC] SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs:576 | DispatchGpuReadback
  Evidence kind: HOT_METHOD_REGEX
  Evidence: `waveHeightSamplerCompute.SetVector(OceanShorelineDepthParamsId, shorelineParams);`
  Required action: Review material mutation in hot path. Prefer GraphicsBuffer/CBUFFER paths that keep SRP batching intact.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs:1203 | LoadVehicleSamplingProfiles
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakePipeline.cs:516 | _telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<HadalArchBakeTelemetryEntry> _telemetry;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs:299 | _telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<HadalTrenchBakeTelemetryEntry> _telemetry;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/OfflineWreckageBlackBox.cs:18 | s_ring
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<OfflineWreckageTelemetryEntry> s_ring;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][55%][EDITOR_ONLY_REVIEW] EDITOR_SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/StaticCaveSdfBaker/Editor/StaticCaveSdfBakePipeline.cs:993 | StaticCaveSdfBakeTelemetryBuffer
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct StaticCaveSdfBakeTelemetryBuffer`
  Required action: Editor/test signal-like structs do not gate runtime, but should not shadow production contracts.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Editor/DamageBake/HabitatDamageBakePipeline.cs:688 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<HabitatDamageBakeTelemetryEntry> _telemetryRing;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [WARN][85%][COMPILE_WALL_DEPENDENCY_REVIEW] ASMDEF_SIGNAL_CONTRACT_REFERENCE_MISSING | Assets/_Project/Scripts/Plugins/Hecton8.Plugins.asmdef:4 | Hecton8.Plugins
  Evidence kind: ASMDEF_REFERENCE_SCAN
  Evidence: `Signal contract source: Assets/_Project/Scripts/Plugins/MapMagic/MapMagicRuntimeBridge.cs`
  Required action: Add a direct Hecton8.Core.Contracts asmdef reference for signal contract usage. Keep Hecton8.Core only when this assembly also consumes runtime Core APIs.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/CrashTelemetryBuffer.cs:220 | TelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct TelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][68%][EDITOR_ONLY_REVIEW] EDITOR_SIGNAL_NAME_SHADOWS_RUNTIME | Assets/_Project/Scripts/Editor/BlackBoxBinaryReader.cs:40 | TelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct TelemetryEntry`
  Required action: Editor/test structs should not shadow runtime signal names; rename smoke payloads or fully isolate them.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:82 | OceanSurfaceTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct OceanSurfaceTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereContracts.cs:88 | OceanSurfaceTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct OceanSurfaceTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:2111 | ScanTelemetryJob
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
- [ERROR][92%][CONFIRMED_RUNTIME_CONTRACT_COLLISION] DUPLICATE_RUNTIME_SIGNAL_NAME | Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs:72 | ToolDepletedSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct ToolDepletedSignal`
  Required action: Signal names must be globally unique across runtime contracts. Merge duplicate contracts or wrap mock/domain-local payloads behind explicit names.
- [ERROR][92%][CONFIRMED_RUNTIME_CONTRACT_COLLISION] DUPLICATE_RUNTIME_SIGNAL_NAME | Assets/_Project/Scripts/Tools/EquipmentThermalBatteryContracts.cs:116 | ToolDepletedSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct ToolDepletedSignal`
  Required action: Signal names must be globally unique across runtime contracts. Merge duplicate contracts or wrap mock/domain-local payloads behind explicit names.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:183 | MockAcousticSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockAcousticSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/VFX/VolumetricSiltContracts.cs:45 | MockAcousticSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockAcousticSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Physics/HarpoonTensionSolver328.cs:1859 | RecordTetherTelemetryJob
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
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/World/TerrainChunkPagerTypes.cs:207 | PagerTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct PagerTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs:2729 | TelemetryWriteJob
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct TelemetryWriteJob`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:2495 | TelemetryWriteJob
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct TelemetryWriteJob`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardTypes.cs:242 | ThermalTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct ThermalTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/World/AbyssalThermalManager.cs:112 | ThermalTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct ThermalTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:81 | MockDepthSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockDepthSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:60 | MockDepthSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockDepthSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityTypes.cs:298 | MockDepthSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockDepthSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:47 | MockPredatorProximitySignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockPredatorProximitySignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:98 | MockPredatorProximitySignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockPredatorProximitySignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:69 | MockTensionSignal
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
