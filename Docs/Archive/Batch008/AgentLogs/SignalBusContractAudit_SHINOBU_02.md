# SHINOBU_02 Signal Bus Contract Audit

Evidence Class: STATIC_SOURCE_CLASSIFIED
Scope: Full
Generated UTC: 2026-05-17T23:03:46.6193971Z

## Summary

- Files scanned: 1686 C# / 61 compute
- Signal-like definitions found: 512
- Signal definitions still in Core/GlobalSignals.cs: 173
- Pack=1 layouts: 571
- Runtime signal Pack=1 layouts: 176
- Signal-like definitions without nearby StructLayout: 27
- Managed event surface hits: 0
- Local native telemetry ring hits: 77
- Registered local telemetry rings: 47
- Hot-path heuristic hits: 0
- Compute 1024-thread-group hits: 0
- Errors: 194
- Warnings: 802
- Infos: 137
- Confirmed/probable errors at confidence >= 90: 194
- Review-only findings below confidence 75: 330

## Rule Breakdown

- RUNTIME_SYNC_FILE_IO_REVIEW: total 391, errors 0, warnings 391, infos 0, avg confidence 76
- RUNTIME_SIGNAL_PACK1_FORBIDDEN: total 176, errors 176, warnings 0, infos 0, avg confidence 95.9
- PACK1_REQUIRES_OWNER_JUSTIFICATION: total 159, errors 0, warnings 159, infos 0, avg confidence 68
- PACK1_RUNTIME_NATIVE_REVIEW: total 143, errors 0, warnings 143, infos 0, avg confidence 78
- PACK1_FILE_FORMAT_BOUNDARY_REVIEW: total 90, errors 0, warnings 0, infos 90, avg confidence 62
- LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT: total 47, errors 0, warnings 47, infos 0, avg confidence 88
- LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW: total 23, errors 0, warnings 0, infos 23, avg confidence 70
- SIGNAL_LAYOUT_REVIEW: total 21, errors 0, warnings 21, infos 0, avg confidence 65
- DUPLICATE_SIGNAL_LIKE_NAME_REVIEW: total 18, errors 0, warnings 18, infos 0, avg confidence 74
- LOCAL_NATIVE_TELEMETRY_RING_UNOWNED: total 16, errors 16, warnings 0, infos 0, avg confidence 90
- POSSIBLE_ORPHANED_SIGNAL_QUEUE: total 15, errors 0, warnings 15, infos 0, avg confidence 82
- LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS: total 10, errors 0, warnings 0, infos 10, avg confidence 92
- LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW: total 5, errors 0, warnings 0, infos 5, avg confidence 68
- EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW: total 4, errors 0, warnings 0, infos 4, avg confidence 56
- EDITOR_PACK1_REVIEW: total 3, errors 0, warnings 0, infos 3, avg confidence 50
- EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW: total 3, errors 0, warnings 3, infos 0, avg confidence 60
- SIGNAL_LAYOUT_UNDECLARED: total 3, errors 0, warnings 3, infos 0, avg confidence 86
- DUPLICATE_RUNTIME_SIGNAL_NAME: total 2, errors 2, warnings 0, infos 0, avg confidence 92
- EDITOR_SIGNAL_LAYOUT_REVIEW: total 2, errors 0, warnings 0, infos 2, avg confidence 55
- MANAGED_STRING_IN_SIGNAL_LIKE_REVIEW: total 1, errors 0, warnings 1, infos 0, avg confidence 72
- EDITOR_SIGNAL_NAME_SHADOWS_RUNTIME: total 1, errors 0, warnings 1, infos 0, avg confidence 68

## Classification Breakdown

- IO_PRESSURE_HEURISTIC: 391
- CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL: 176
- STATIC_LAYOUT_REVIEW: 159
- PROBABLE_RUNTIME_NATIVE_PAYLOAD: 143
- FILE_FORMAT_OR_SERIALIZATION_CANDIDATE: 90
- CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL: 47
- REGISTERED_LOCAL_QUEUE_REVIEW: 23
- NAME_BASED_REVIEW: 21
- STATIC_CONTRACT_REVIEW: 19
- PROBABLE_NATIVE_OWNERSHIP_BREACH: 16
- PROBABLE_SIGNAL_CORRIDOR_BYPASS: 15
- EDITOR_ONLY_REVIEW: 13
- CONFIRMED_VAULT_ALIAS_REVIEW: 10
- SIGNAL_SCRATCH_REVIEW: 5
- PROBABLE_RUNTIME_PAYLOAD: 3
- CONFIRMED_RUNTIME_CONTRACT_COLLISION: 2

## Findings

- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/ConstructionManager.cs:155 | _deconstructionBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<HabitatDeconstructionTelemetryEntry> _deconstructionBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/ConstructionManager.cs:1163 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/ConstructionManager.cs:1165 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/CrashTelemetryBuffer.cs:263 | _ringBuffer
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<TelemetryEntry> _ringBuffer;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/CrashTelemetryBuffer.cs:264 | _exportSnapshot
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<TelemetryEntry> _exportSnapshot;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/EncounterDirector.cs:240 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<EncounterDirectorBlackBoxEntry> _blackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/EncounterDirector.cs:1040 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(parent);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/EncounterDirector.cs:1042 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/FaunaDirector.cs:211 | AcousticPanicCommand
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct AcousticPanicCommand`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/GlobalPhysicsStateManager.cs:141 | PhysicsImpactSignal
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public readonly struct PhysicsImpactSignal`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/GlobalPhysicsStateManager.cs:3185 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/GlobalPhysicsStateManager.cs:3187 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/HectonCelestialEngine.cs:88 | EclipseStartedEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/HectonCelestialEngine.cs:121 | CelestialEventPayload
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:540 | CelestialOrbitJobOutput
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:547 | CelestialBlackBoxEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/HectonCelestialEngine.cs:1079 | _celestialBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<CelestialBlackBoxEntry> _celestialBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6074 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6076 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:67 | ActiveThrusterFlow
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:80 | WhirlpoolFlow
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:95 | FluidViscosityRegion
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/HectonFluidEngine.cs:106 | FluidImpactEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:115 | OceanSurfaceTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:131 | FluidAdvectionTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:145 | InteriorFloodNode
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:158 | InteriorFloodEdge
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:167 | InteriorFloodBfsResult
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:289 | GpuBuoyancyObjectData
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:302 | GpuHeatSourceData
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:311 | AbyssalFlowTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:326 | MaelstromTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:600 | AdvectedSilt
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:609 | AdvectedBubble
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:618 | AdvectedDebris
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/HectonFluidEngine.cs:1264 | _oceanSurfaceTelemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<OceanSurfaceTelemetryEntry> _oceanSurfaceTelemetry;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/HectonFluidEngine.cs:1277 | _maelstromTelemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<MaelstromTelemetryEntry> _maelstromTelemetry;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/HectonFluidEngine.cs:1377 | _fluidAdvectionTelemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<FluidAdvectionTelemetryEntry> _fluidAdvectionTelemetry;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/HectonFluidEngine.cs:1421 | _abyssalFlowTelemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<AbyssalFlowTelemetryEntry> _abyssalFlowTelemetry;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:2372 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:2374 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:3749 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:3751 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:5147 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:5149 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:6073 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:6075 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:6541 | BuoyancyParams
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 96)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/HectonFluidEngine.cs:6605 | WaveQueryJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/HectonFluidEngine.cs:6731 | BuoyancyJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/HectonNarrativeDirector.cs:180 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<NarrativeTriggerTelemetryEntry> _blackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/HectonNarrativeDirector.cs:934 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/HectonNarrativeDirector.cs:936 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/HectonPlayerMovement.cs:59 | CinematicFocusTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/HectonPlayerMovement.cs:1589 | RenderInterpolationState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/HectonPlayerMovement.cs:1909 | QueuedCollisionEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/HectonPlayerMovement.cs:1921 | ColliderCallbackMetadata
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/HectonPlayerMovement.cs:3093 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/HectonPlayerMovement.cs:3095 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/HectonPlayerMovement.cs:8182 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/HectonPlayerMovement.cs:8184 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/HectonSurvivalSystem.cs:138 | SurvivalDatabaseItemRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 20)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/HectonVoxelEngine.cs:2836 | _voxelMeshPipelineBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<VoxelMeshPipelineTelemetryEntry> _voxelMeshPipelineBlackBox;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/HectonVoxelEngine.cs:5403 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/HectonVoxelEngine.cs:5405 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/InventoryEvents.cs:51 | InventoryEventPayload
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 24)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/InventoryEvents.cs:67 | InventoryPhysicalDropRequestPayload
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/ITickable.cs:35 | H8TimeSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/LocRegistry.cs:450 | _telemetryFrames
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<BabelTelemetryEntry> _telemetryFrames;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/LocRegistry.cs:1669 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(docsPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/LocRegistry.cs:1683 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/NoiseSystem.cs:28 | PlayerNoiseSignal
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public readonly struct PlayerNoiseSignal`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/PhysicsApplySystem.cs:57 | ForcePacket
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/PhysicsApplySystem.cs:88 | PressureImpulseEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 80)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/PhysicsApplySystem.cs:151 | ElectromagneticPulseEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/PhysicsApplySystem.cs:181 | AcousticPingEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/PhysicsApplySystem.cs:243 | AcousticImpulseEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/PhysicsApplySystem.cs:303 | LargeAcousticImpulseEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/PhysicsApplySystem.cs:367 | RemovedPhysicsEventPayload
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 80)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/PhysicsApplySystem.cs:1024 | RemovedDeferredSubmarineImpactSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:137 | InventoryTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = InventoryBlackBoxEntrySizeBytes)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:158 | SalinityCorrosionTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = SalinityCorrosionBlackBoxEntrySizeBytes)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/PlayerInventory.cs:433 | CraftReservation
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 12)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/PlayerInventory.cs:538 | _inventoryBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<InventoryTelemetryEntry> _inventoryBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/PlayerInventory.cs:541 | _salinityCorrosionBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<SalinityCorrosionTelemetryEntry> _salinityCorrosionBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:4438 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:4440 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:4604 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:4606 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/RepairTool.cs:89 | RepairToolBlackBoxEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = RepairBlackBoxEntrySizeBytes)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/RepairTool.cs:1352 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/RepairTool.cs:1354 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/RuntimeDiagnosticsTrace.cs:82 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/SaveManager.cs:201 | _saveTelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<AsyncPersistenceTelemetryEntry> _saveTelemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/SaveManager.cs:202 | _wfcOutpostTelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<WfcOutpostTelemetryEntry> _wfcOutpostTelemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/SaveManager.cs:203 | _wfcOutpostEventTelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<WfcOutpostTelemetryEntry> _wfcOutpostEventTelemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/ScannerTool.cs:478 | ScannerBlackBoxEntry
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ScannerBlackBoxEntry`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/ScannerTool.cs:646 | _scannerBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ScannerBlackBoxEntry> _scannerBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/ScannerTool.cs:1302 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/ScannerTool.cs:1304 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/SpatialAudioManager.cs:908 | _virtualVoiceBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `private NativeArray<VirtualVoiceTelemetryEntry> _virtualVoiceBlackBox; // Vault alias; GlobalDataVault owns backing memory.`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/SpatialAudioManager.cs:952 | _acousticPortalBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `private NativeArray<AcousticTelemetryEntry> _acousticPortalBlackBox; // Vault alias; GlobalDataVault owns backing memory.`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/SpatialAudioManager.cs:3622 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/SpatialAudioManager.cs:3624 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/SpatialAudioManager.cs:3678 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `byte[] bytes = File.ReadAllBytes(path); // COLD ALLOC: byte[524288] - one-shot Sabine RT60+damping fallback read - owner: SpatialAudioManager`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/SpatialAudioManager.cs:6359 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/SpatialAudioManager.cs:6361 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs:879 | AtmosphereStepJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/SubmarineFluidDynamics.cs:5907 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/SubmarineFluidDynamics.cs:5909 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:132 | HullDamageDiffusionJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:245 | HullCompartmentMappingJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:294 | HullFatigueCompartmentJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:353 | BreachRepairJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1668 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1670 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/TerrainChunkGeneratedEvents.cs:13 | _events
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<TerrainChunkGeneratedSignal> _events;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/TetherInstance.cs:244 | _verletTelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `private NativeArray<TetherVerletTelemetryEntry> _verletTelemetryRing;`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/TetherInstance.cs:1992 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/TetherInstance.cs:1994 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Append, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/TetherManager.cs:119 | TetherManagerTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct TetherManagerTelemetryEntry`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/TetherManager.cs:98 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `private NativeArray<TetherManagerTelemetryEntry> _telemetryRing;`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/TetherManager.cs:872 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/TetherManager.cs:874 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Append, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VoxelDeltaProcessor.cs:3947 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VoxelDeltaProcessor.cs:3949 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs:129 | _terrainSeamBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<TerrainSeamTelemetryEntry> _terrainSeamBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs:1504 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs:1506 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:1280 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:1282 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:1326 | AmbientBiotaGpuInstance
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanCognitionContracts.cs:60 | AlphaLeviathanTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanCognitionVault.cs:13 | AlphaLeviathanVaultBuffers
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanCognitionVault.cs:36 | AlphaLeviathanVaultHandles
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 120)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanCognitionVault.cs:406 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanCognitionVault.cs:410 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanCognitionVault.cs:639 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanStalkContracts.cs:78 | AlphaLeviathanAup
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 48)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanStalkContracts.cs:105 | AlphaLeviathanCognitionState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 144)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanStalkContracts.cs:125 | AlphaLeviathanSensoryStimulus
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 176)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanStalkContracts.cs:150 | AlphaLeviathanSteeringOutput
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 88)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/AI/Cognition/LeviathanStalkJob.cs:15 | LeviathanStalkJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs:347 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs:893 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs:895 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:2119 | CountTelemetryCountersJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct CountTelemetryCountersJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:653 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, share, math.max(1, limit), FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1175 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1177 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/AI/Pathfinding/PathFunnelContracts.cs:39 | NavPortal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Size = 36, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/AI/Pathfinding/PathFunnelContracts.cs:90 | PathFunnelResult
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Size = 32, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/AI/Pathfinding/PathFunnelContracts.cs:108 | PathFunnelActivePath
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Size = 32, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/AI/Pathfinding/PathFunnelContracts.cs:133 | PathFunnelInvalidation
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Size = 32, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/AI/Pathfinding/PathFunnelContracts.cs:151 | PathFunnelTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Size = 48, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/AI/Pathfinding/PathFunnelContracts.cs:181 | PathFunnelRuntimeState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Size = 64, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs:839 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs:844 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:871 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:873 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Animation/Fauna/ProceduralBiteIkJobs.cs:46 | JawIkTarget
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 128)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Animation/Fauna/ProceduralBiteIkJobs.cs:65 | CurrentJawPos
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 128)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Animation/Fauna/ProceduralBiteIkJobs.cs:89 | BiteIkSolveEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 128)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Animation/IK/LeviathanTerrainIkJobs.cs:43 | LeviathanTerrainIkTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 96)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Animation/IK/LeviathanTerrainIkJobs.cs:89 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Animation/IK/LeviathanTerrainIkJobs.cs:91 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Animation/IK/LowerBodyPresenceIkJobs.cs:50 | FootIKData
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Animation/IK/VRPhysicalHandPresenceIkJobs.cs:90 | VRHandAupPose
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Animation/IK/VRPhysicalHandPresenceIkJobs.cs:107 | VRHandGrabState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Animation/IK/VRPhysicalHandPresenceIkJobs.cs:127 | VRHandPresenceInput
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Animation/IK/VRPhysicalHandPresenceIkJobs.cs:161 | VRHandPresenceOutput
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Animation/IK/VRPhysicalHandPresenceIkJobs.cs:181 | VRHandIkTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Animation/IK/VRPhysicalHandPresenceIkJobs.cs:309 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Animation/IK/VRPhysicalHandPresenceIkJobs.cs:311 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Animation/Locomotion/LadderClimbIkJobs.cs:34 | LadderClimbIkInput
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Animation/Locomotion/LadderClimbIkJobs.cs:55 | LadderClimbIkOutput
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Animation/Locomotion/LadderClimbIkJobs.cs:70 | LadderClimbTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Animation/Locomotion/LadderClimbIkJobs.cs:87 | LadderClimbIkVaultViews
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Animation/Locomotion/LadderClimbIkJobs.cs:113 | LadderClimbIkSolveJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:1024 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(BlackBoxDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:1051 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(BlackBoxDumpDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/AtlasSignal/AtlasSignalEvents.cs:56 | _pendingEvents
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<AtlasSignalEventPayload> _pendingEvents;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/AtlasSignal/AtlasSignalEvents.cs:57 | _nextFrameEvents
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<AtlasSignalEventPayload> _nextFrameEvents;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Atmosphere/BaseAtmosphereMath.cs:408 | BaseAtmosphereColdTickJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs:1977 | PendingBaseTransitionSignal
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct PendingBaseTransitionSignal`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs:133 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<GasDynamicsTelemetryEntry> _telemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs:134 | _toxicitySignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<ToxicitySignal> _toxicitySignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:734 | _granularTelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<GranularAudioTelemetryEntry> _granularTelemetryRing;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:736 | _prologueTransitionTelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<PrologueAudioTransitionTelemetryEntry> _prologueTransitionTelemetryRing;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:9886 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:9900 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:9935 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:9945 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:88 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `private NativeArray<VwsTelemetryEntry> _telemetryRing; // Vault alias; GlobalDataVault owns backing memory.`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:845 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:847 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:1558 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:4639 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(telemetryPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:4683 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:575 | s_DroneBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `private static NativeArray<DroneFleetBlackBoxEntry> s_DroneBlackBox;`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:585 | s_DroneAStarTelemetry
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `private static NativeArray<DroneAStarTelemetry> s_DroneAStarTelemetry;`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:639 | s_DroneServiceCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<DroneServiceCommand> s_DroneServiceCommands;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:4163 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:4165 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:4389 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(resolvedPath))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs:52 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<FluidPipeTelemetryEntry> _telemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs:651 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs:653 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:262 | _floodBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<HabitatFloodBlackBoxEntry> _floodBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:2251 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:2253 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Construction/HectonBlueprintPreviewBatch.cs:27 | BuildPreviewMatricesJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:48 | DockTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 128)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:1159 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:1163 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs:449 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs:451 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/BurstCallback.cs:13 | BurstCallback
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/BurstCallback.cs:35 | BurstCallbackQueue
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/BurstCallback.cs:207 | ParallelEventWriter
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/ConnectionSplineBatchRenderer.cs:44 | FlexiblePipeInstanceGpuData
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:18 | DodReplaySnapshotHeader
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 128)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:65 | DodReplaySegmentHeader
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:94 | DodReplayInputEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:123 | DodReplayJobProfileRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:145 | DodReplayBurstPanicRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:169 | DodReplayAupDriftRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:195 | DodReplayEntityGhostRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:213 | DodReplayLogisticFlowRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:233 | DodReplayAtmosphereCellRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 40)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:259 | DodReplayVramAllocationRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:279 | DodReplayPhysicsSmokeRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 56)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:1656 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_replayStream = new FileStream(_replayPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.RandomAccess);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:1796 | ReplaySourceHash
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:1805 | AupDriftState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:75 | ImportanceScoringJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:127 | VisualInterpolationJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:145 | FoveatedSimulationTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:267 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<FoveatedSimulationTelemetryEntry> _telemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1175 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1177 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistry.cs:335 | ForceOverrideToken
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:174 | OrbitalDirectorSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:235 | StreamingHlodImpostorPoint
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:321 | DamagePacket
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:492 | CurrentMeta
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:519 | GerstnerWaveComponent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:556 | OceanGerstnerWaveBufferMeta
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:568 | WeatherRuntimeSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:630 | CelestialRuntimeSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:694 | GIRelayRuntimeSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:745 | SeismicRuntimeSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:1015 | AudioEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:1040 | AudioTransitionState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:1484 | VRSomaticChestSocketPose
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:1505 | VRSomaticCollisionState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:1546 | VRSomaticSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:1608 | VRSomaticHandPose
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:1777 | NarrativeSpatialTriggerAuthoring
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:1798 | PlayerRuntimePoseSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2220 | HabitatRoomWaterlineSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2632 | GasRoomSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2671 | GasBaseHibernationSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2713 | ToxicitySignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2746 | GasDynamicsNativeMemoryAudit
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2849 | HectonHardwareProfile
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:3001 | RegistryEventPayload
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:3441 | EcosystemSectorPopulationSample
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:3503 | FaunaGenomeMutationRequest
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:3533 | AmbientBiotaState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:3558 | AmbientBiotaTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:3609 | EcosystemBiomassAuditSample
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:42 | ScalabilityChangedEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:123 | AudioEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 144)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:150 | DataVaultUpdateSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:164 | PrefabAcousticSignatureSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:178 | PrefabLoreLinkSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:213 | DebugSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:230 | SystemHealthSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:246 | FrameTimeSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:260 | KillSwitchSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:272 | ReentryVfxStateSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:292 | VisorDropletSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:307 | InputSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:320 | StateCorrectionSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 128)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:335 | DesyncDetectedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:346 | SyncFenceSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 128)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:375 | TetherTensionSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 144)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:392 | TetherSnappedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:407 | TetherFiredSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:423 | DockingRequestSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:438 | DockingCompleteSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:453 | DockingFailedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:468 | VoxelCarveEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 128)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:489 | VisualFlareSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:504 | CameraJuiceImpactSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][90%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:554 | SignalLaneTelemetry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:571 | InputStateSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:584 | LockstepSnapshotSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:599 | SystemGlitchSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:629 | LaserCutterEventPayload
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:658 | SplashEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:713 | PhysicsEventPayload
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:734 | DeferredSubmarineImpactSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:770 | PlayerInputSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:789 | PlayerLookTargetSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 160)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][90%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6474 | SpscSignalRingBuffer
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6576 | ImpactSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6593 | HighSpeedImpactSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 96)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6655 | PlayerStateSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6694 | SurvivalVitalsChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6764 | InventoryCommandSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6775 | InventoryChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6788 | ItemDurabilityChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6808 | ItemAcquiredSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6821 | RadiationDoseSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6833 | TemperatureChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6849 | RadiationSourceSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6864 | ResourceDepletionDeltaSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6897 | DropPodLandedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6921 | WakeGeneratedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6930 | FluidImpulseSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6943 | BubbleSpawnSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6959 | ProgressionEventSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6971 | GlobalWorldStateSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:6992 | BiomeChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7003 | BiomeGradientSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7027 | NarrativeFocusSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7075 | NarrativeHudWaypointSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7087 | SoundscapeProfileSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7098 | NarrativePoiStateSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7110 | BrownoutSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7123 | DebrisSpawnSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7143 | DeflectSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7157 | EntityDeathSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7168 | EntitySpawnSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7189 | SolarFlareSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7200 | RebaseSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7210 | ControlSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7223 | AnomalySignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7235 | AnomalyProximitySignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7247 | CompassCalibratedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7257 | TelemetryAnomalySignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7269 | CrashTelemetrySignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7283 | HabitatConstructionSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7295 | DeconstructRequestSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 128)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7310 | DeconstructResultSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7323 | ModuleDeconstructSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7336 | VitalWarningSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7349 | CrushWarningSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7363 | SubtitleSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7375 | VocalWarningSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7387 | DataReloadSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7398 | MemoryPressureSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7426 | ResolutionChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7447 | SystemHealthIndexSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7464 | CpuStarvationSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7477 | AcousticPingSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7516 | SwarmDispersedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7529 | SectorResidencyHydratedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7544 | SectorDehydratedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7559 | ChunkDehydratedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7574 | SonarPingSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7585 | HypoxiaSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7597 | OxygenCriticalSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7609 | InteractionUiSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7620 | UIRescaleRequestSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7632 | FluidIncursionSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7643 | SubmarineFloodStateSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7664 | FluidDensityChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7677 | PipeRuptureSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7690 | SpectrumScanSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7702 | RigidbodySleepSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7713 | ScannerToolActiveSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7729 | ScanCompleteSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7741 | LoreFragmentScannedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7751 | BlueprintUnlockedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7763 | CraftingStartedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7775 | CraftingCompletedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7788 | ToolStateChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7808 | ToolLoadoutChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7829 | ToolAcousticSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7846 | PowerDrainSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7859 | ToolTriggerSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7872 | StorageDebtSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7891 | StreamingTurbulenceSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7903 | AtmosphericReentrySignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7922 | PrologueCompleteSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7939 | ManualOverridePulledSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7962 | HUDNotificationSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7974 | DiegeticHudSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:7990 | ScanLogChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8010 | PdaExchangeStateChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8031 | VehicleUpgradesChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8048 | ThermalStateChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8064 | BatteryLevelSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8077 | ReconDataSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8088 | SaveLifecycleSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8100 | MacroDatabaseSectorHydrationSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8112 | WfcOutpostGeneratedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 128)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8129 | WfcOutpostStateChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8142 | WfcOutpostDoorPowerSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 96)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8158 | SaveRequestSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8171 | SaveCompletedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8184 | SaveStatusSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8202 | SaveMetadataReadySignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8226 | ComplianceViolationSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8238 | GlobalTimeSyncSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8249 | SeismicSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8263 | TimeDilationSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8275 | SimulationPauseSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8310 | ItemDecaySignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8332 | LightLevelSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8357 | SubmarineLightsChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8384 | FaunaStateChangedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8397 | PhysiologyStateSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8409 | PlayerStressSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8421 | TraumaSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8434 | CameraPositionSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8444 | CameraFrustumSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8457 | CombatDamageSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8478 | HullDeformedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8501 | HullRepairedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8518 | BaseModuleCompromisedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8540 | PlayerBaseEnterSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8555 | PlayerBaseExitSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8583 | SystemPauseSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8608 | FramePacingWarningSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][90%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/GlobalSignals.cs:8628 | CombatDamageSignalAupShiftTransformer
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/Core/GlobalTelemetryBus.Blackbox.cs:254 | _blackboxEvents
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `private static NativeArray<TelemetryEventDTO> _blackboxEvents;`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/Core/GlobalTelemetryBus.Blackbox.cs:256 | _blackboxLoggingMasks
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `private static NativeArray<TelemetryLoggingMaskDTO> _blackboxLoggingMasks;`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs:97 | _snapshotBuffer
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<TelemetryEvent> _snapshotBuffer;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/HectonPersistentPathPolicy.cs:32 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/HectonSpatialIntrinsics.cs:8 | HectonAabb
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/HectonSpatialIntrinsics.cs:16 | HectonSphere
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.cs:75 | HomeostasisBlackBoxEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.cs:951 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.cs:953 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs:939 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs:1214 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs:1216 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:935 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1187 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1189 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_inputReplayStream = new FileStream(replayPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1342 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1346 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/JobFenceManager.cs:12 | JobFenceManager
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/LogisticsPipeBuilder.cs:22 | SplineDescriptor
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/MacroDatabaseSignalBridge.cs:11 | MacroDatabaseSignalBridge
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/NativeBitmask256.cs:7 | NativeBitmask256
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/NativeMemorySentinel.cs:29 | NativeAllocationSnapshotSource
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/NativeMemorySentinel.cs:87 | NativeAllocationRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/NativeMemorySentinel.cs:104 | PersistentReallocationRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/NativeQuery.cs:14 | NativeQuery
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/NativeQuery.cs:40 | NativeSelectQuery
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/NativeQuery.cs:140 | NativeFilterJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/NativeQuery.cs:162 | NativeSelectJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/NativeRingBuffer.cs:13 | NativeRingBuffer
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/PlayerRuntimeContext.cs:35 | PlayerMovementRuntimeState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/PlayerRuntimeContext.cs:53 | PlayerLookState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/PlayerRuntimeContext.cs:61 | PlayerSurvivalRuntimeState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/PlayerRuntimeContext.cs:87 | PlayerInteractionRuntimeState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/PowerGridRuntimeService.cs:9 | BatteryRuntimeSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/RebindingManager.cs:374 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string json = File.ReadAllText(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
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
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/StackQueue.cs:10 | StackQueue
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:2629 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:2630 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:2737 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3250 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3267 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs:31 | EntityCommand
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs:184 | StorageReservationCommitResolvedPayload
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs:215 | _pendingCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<EntityCommand> _pendingCommands;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/UIStateStore.cs:33 | UIStateData
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/UIStateStore.cs:80 | UIValueSlot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/UnsafeArenaAllocator.cs:10 | ArenaBlock
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Bridge/H8BridgeContracts.cs:34 | H8PrefabMappingEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Bridge/H8BridgeContracts.cs:51 | H8PrefabLoreLinkEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Bridge/H8BridgeContracts.cs:65 | H8DesignValueEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Bridge/H8BridgeContracts.cs:80 | H8FacadeTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 40)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Bridge/H8BridgeContracts.cs:96 | H8FacadeTelemetryDumpHeader
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Bridge/H8BridgeContracts.cs:110 | H8InputFacadeBindingEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Bridge/H8BridgeContracts.cs:125 | H8FacadeMacroHeader
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Bridge/H8BridgeContracts.cs:226 | FloatUInt32Union
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 4)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs:410 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs:437 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Bucketing/ModuloSimulationBucketer.cs:736 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Bucketing/ModuloSimulationBucketer.cs:738 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(BlackBoxDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Bucketing/ModuloSimulationBucketer.cs:860 | SimulationBucketRebalanceResult
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 20)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Bucketing/ModuloSimulationBucketer.cs:870 | SimulationBucketBlackBoxEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = BlackBoxEntrySizeBytes)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Content/ContentAssetHashMap.cs:31 | ContentAssetBinaryRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Content/ContentLoreBinaryProvider.cs:12 | ContentLoreBlockIndex
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Content/ContentLoreBinaryProvider.cs:146 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_fallbackStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferBytes);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs:18 | ContentBundleRefState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 24)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs:31 | ContentAuthorityTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs:51 | ContentPendingLoadState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs:60 | ContentVisualFeatureBudget
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs:1493 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs:1495 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Content/ObjectBatchBase.cs:9 | ObjectBatchInstance
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 80)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Content/ObjectBatchBase.cs:20 | ObjectBatchChunk
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 40)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Contracts/BrineLayerSample.cs:9 | BrineLayerSample
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Contracts/CoreContractsAssemblyMarker.cs:10 | CoreContractsAssemblyMarker
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Contracts/CoreContractsAssemblyMarker.cs:29 | HardwareThermalSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 20)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Contracts/CoreContractsAssemblyMarker.cs:70 | DynamicResolutionRuntimeSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 24)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Contracts/CoreContractsAssemblyMarker.cs:96 | ResolutionScaleState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Contracts/InertialNavigationContracts.cs:9 | CompassStateDTO
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 176)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Contracts/InertialNavigationContracts.cs:55 | InertialNavigationSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 120)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Contracts/MacroDatabaseContracts.cs:13 | MacroDatabaseAup
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 48)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Contracts/MacroDatabaseContracts.cs:96 | MacroDatabaseConfig
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Contracts/MacroDatabaseContracts.cs:134 | MacroDatabasePayloadHandle
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Contracts/MacroDatabaseContracts.cs:147 | MacroDatabaseNativeCacheStats
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Contracts/MacroDatabaseContracts.cs:156 | MacroDatabaseStats
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Contracts/MacroDatabaseContracts.cs:178 | MacroDatabaseCompactionSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Contracts/MacroDatabaseContracts.cs:194 | SectorHydratedSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Contracts/MacroDatabaseContracts.cs:206 | MacroDatabaseTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Contracts/MacroSwarm.cs:9 | MacroSwarm
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 48)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Contracts/MacroSwarm.cs:26 | MacroSwarmArrival
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Contracts/PrologueSequenceContracts.cs:59 | PrologueOrbitalSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Contracts/PrologueSequenceContracts.cs:89 | PrologueAtmosphericReentrySnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Contracts/PrologueSequenceContracts.cs:116 | PrologueCompleteSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Contracts/SimulationBucketingContracts.cs:90 | SimulationBucketFrameState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/BabelDictionaryStore.cs:101 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.RandomAccess);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/BabelDictionaryStore.cs:125 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8DataBaker.cs:119 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(outputDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8DataBaker.cs:627 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8DataBaker.cs:630 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8DataBaker.cs:640 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(backupPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8DataBaker.cs:970 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, CsvReadBufferBytes, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:125 | H8StaticDataHeader
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:151 | H8StaticDataLookupEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:163 | H8BabelDictionaryHeader
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:180 | H8BabelDictionaryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:192 | H8ItemStaticRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:213 | H8EconomyStaticRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:233 | H8PhysicsStaticRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:253 | H8FaunaStaticRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:273 | H8StaticDataTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:295 | H8StaticDataDumpHeader
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:499 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:515 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Data/InventoryCost.cs:8 | InventoryCost
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/StaticDataStore.cs:97 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.RandomAccess);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/StaticDataStore.cs:121 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:44 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<MacroDatabaseTelemetryEntry> _blackBox;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:70 | SectorCoord64
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:85 | HydrationCandidate
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:206 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_fileStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:255 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:268 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_fileStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:825 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:829 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:1288 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(tempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:38 | LockstepPlayerKinematicState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 96)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:61 | LockstepReplayInputFrame
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:79 | LockstepReplayBlockHeader
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 128)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:109 | LockstepArrayHash
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:122 | LockstepTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:143 | LockstepMasterHashHistoryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1336 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1342 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1420 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, ReplayBlockBytes * 4, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1678 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_replayStream = new FileStream(replayPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1888 | HashFloat3ArrayJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1909 | HashDouble3ArrayJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1930 | HashFloatArrayJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1951 | HashPlayerKinematicArrayJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1986 | CombineElementHashesJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:2024 | MasterStateHashJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:28 | ArchitectEyeQuadInstance
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 80)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:39 | ArchitectEyeBlackBoxEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:60 | ArchitectEyeRuntimeState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1131 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1132 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:109 | ThermalTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 24)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:700 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:702 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:493 | _defragBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<MemoryDefragTelemetryEntry> _defragBlackBox;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:2637 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:2659 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:2674 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/Core/Memory/H8Memory.cs:1090 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<H8MemoryTelemetryEntry> _blackBox;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/Core/Memory/H8Memory.cs:1091 | _eventBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<H8MemoryTelemetryEntry> _eventBlackBox;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/H8Memory.cs:2277 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/H8Memory.cs:2300 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/VaultLegacyBinaryArchaeology.cs:156 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/VaultLegacyBinaryArchaeology.cs:184 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Origin/AupOriginShiftCoordinator.cs:871 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, scratch.Length, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Origin/AupOriginShiftCoordinator.cs:1017 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Origin/AupOriginShiftCoordinator.cs:1019 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, _dumpScratch.Length, FileOptions.WriteThrough))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Scheduling/BurstTokenBucketJobAdmissionService.cs:728 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Scheduling/BurstTokenBucketJobAdmissionService.cs:730 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(AdmissionBlackboxDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Scheduling/BurstTokenBucketJobAdmissionService.cs:764 | JobAdmissionBlackboxEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = BlackboxEntrySizeBytes)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:7 | PlayerFootstepSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:16 | PlayerWaterSplashSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:28 | WaterTransitionSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 96)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:44 | PlayerExhaleSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:52 | PlayerSprintStateSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:61 | PlayerFatalPressureSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:70 | PlayerTransportBailoutSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:161 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:333 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:335 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:422 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:101 | H8DataBlobHeader
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = H8DataLayoutConstants.HeaderSizeBytes)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:117 | H8DataBlobDirectory
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = H8DataLayoutConstants.DirectorySizeBytes)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:142 | H8DataSectionEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:154 | H8ItemRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = H8DataLayoutConstants.ItemRecordSize)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:177 | H8CreatureGenomeTraitBlock
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = H8DataLayoutConstants.CreatureGenomeTraitBlockSize)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:193 | H8CreatureTraitRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = H8DataLayoutConstants.CreatureTraitRecordSize)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:210 | H8BiomeRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = H8DataLayoutConstants.BiomeRecordSize)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:231 | H8RecipeRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:250 | H8BiomeHeatmapCellRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:260 | H8QuestNodeRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:274 | H8QuestEdgeRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:283 | H8LootCdfRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:292 | H8VoxelMaterialRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:305 | H8AudioClipRegistryRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:314 | H8VfxScalarRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:327 | H8DepthPressureSampleRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:336 | H8ToolHeatCapacityRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:345 | H8SubmarineHullConstantRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:358 | H8NarrativeTriggerRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:368 | H8PhysicsMaterialRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:377 | H8GhostModuleRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:398 | H8RadiationIntensityCellRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:407 | H8SpawnCreditCostRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:416 | H8LightAttenuationSampleRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:429 | H8SopErrorRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:438 | H8HudLayoutRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs:459 | H8SectorPageRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:1028 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `byte[] source = File.ReadAllBytes(absolutePath); // COLD ALLOC: byte[file bytes] - boot-only single I/O staging before native blit - owner: H8StaticDataArena`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Dev/BotController.cs:451 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs:128 | MigrationGridCell
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs:143 | MigrationBloodCloudPoi
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs:162 | MigrationSwarmState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 40)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [INFO][55%][EDITOR_ONLY_REVIEW] EDITOR_SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Editor/SaveSystemTelemetry.cs:29 | SectorTelemetryRow
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct SectorTelemetryRow`
  Required action: Editor/test signal-like structs do not gate runtime, but should not shadow production contracts.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs:18 | _telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<SignalLaneTelemetry> _telemetry;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][55%][EDITOR_ONLY_REVIEW] EDITOR_SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Editor/SystemDiagnosticsBoard.cs:34 | TelemetrySnapshotRow
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct TelemetrySnapshotRow`
  Required action: Editor/test signal-like structs do not gate runtime, but should not shadow production contracts.
- [WARN][60%][EDITOR_ONLY_REVIEW] EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW | Assets/_Project/Scripts/Editor/SystemDiagnosticsBoard.cs:40 | TelemetrySnapshotRow
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `public string Systems;`
  Required action: Editor/test signal-like structs can use managed strings, but must not become runtime payload contracts.
- [WARN][60%][EDITOR_ONLY_REVIEW] EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW | Assets/_Project/Scripts/Editor/SystemDiagnosticsBoard.cs:58 | TelemetrySnapshotRow
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `public string ErrorFlags;`
  Required action: Editor/test signal-like structs can use managed strings, but must not become runtime payload contracts.
- [WARN][60%][EDITOR_ONLY_REVIEW] EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW | Assets/_Project/Scripts/Editor/SystemDiagnosticsBoard.cs:61 | TelemetrySnapshotRow
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `public string ExportReason;`
  Required action: Editor/test signal-like structs can use managed strings, but must not become runtime payload contracts.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/AssemblyGuard/CompileWallXRayWindow.cs:455 | _entries
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<CompileWallBlackBoxEntry> _entries;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1031 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1516 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1517 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(SeismicDirectorConstants.DumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1568 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1648 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1649 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(TelemetryDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Fauna/FaunaBrain.cs:60 | PackCoordinator
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 68)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Fauna/FaunaBrain.cs:105 | CorpseSinkKinematicInput
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 88)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Fauna/FaunaBrain.cs:116 | CorpseSinkKinematicOutput
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1538 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1547 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1606 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1609 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Fauna/FaunaTentacleConstrainedIk.cs:11 | FaunaTentacleConstrainedIkChain
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Fauna/FaunaTentacleConstrainedIk.cs:26 | FaunaTentacleJointPose
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Fauna/FaunaTier1LodProxyRegistry.cs:7 | FaunaTier1LodProxyEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs:62 | LeviathanTentacleTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs:1371 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs:1373 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:2113 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string csv = File.ReadAllText(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:2740 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:2755 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:2976 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:2991 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:16 | ProceduralCrabLegEntityState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:50 | ProceduralCrabLegStepState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:64 | ProceduralCrabBodyPose
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:72 | ProceduralCrabSolvedJointMatrices
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:80 | ProceduralCrabIkTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:94 | ProceduralCrabGroundRaycastBuildJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:157 | ProceduralCrabGroundTargetResolveJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:212 | ProceduralCrabStepSchedulerJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:374 | ProceduralCrabLegAupRebaseJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:395 | ProceduralCrabEntityAupRebaseJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:422 | ProceduralCrabBodyTiltJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:465 | ProceduralCrabAnalyticalTwoBoneIkJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:1272 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:1274 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs:169 | ContextualPhysicalIkFootData
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs:1532 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ContextualPhysicalIkTelemetryEntry> _telemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs:2498 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs:2500 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:114 | DataArchaeologyFrequencyInput
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 28)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:129 | DataArchaeologyFrequencyResult
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:147 | DataArchaeologyNotification
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 8)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:156 | DataArchaeologyTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1566 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1658 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1660 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1724 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs:14 | HazardVolumeData
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 56)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs:30 | HazardExposureJobResult
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 68)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/HectonCameraState.cs:9 | HectonCameraState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs:30 | ScheduledSweepState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/HectonPlayerState.cs:18 | HectonPlayerState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/HectonPlayerState.cs:68 | PlayerKinematicsHandTarget
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Gameplay/HectonPlayerState.cs:83 | PlayerKinematicsTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/HectonPlayerState.cs:99 | PlayerKinematicsLinearDragJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/HectonPlayerState.cs:128 | PlayerKinematicsNativeState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/HectonPlayerState.cs:325 | HectonPlayerMotorNativeState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:24 | PlayerKinematicsRuntimeTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:43 | PlayerKinematicsSyncState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:57 | PlayerKinematicsAccumulatorState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:3514 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:3527 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][86%][PROBABLE_RUNTIME_PAYLOAD] SIGNAL_LAYOUT_UNDECLARED | Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs:10 | TraumaHudSignal
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public readonly struct TraumaHudSignal`
  Required action: Add explicit StructLayout or document unmanaged field order before this payload crosses Burst/native/binary boundaries.
- [WARN][86%][PROBABLE_RUNTIME_PAYLOAD] SIGNAL_LAYOUT_UNDECLARED | Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs:36 | PlayerInteractionStressSignal
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public readonly struct PlayerInteractionStressSignal`
  Required action: Add explicit StructLayout or document unmanaged field order before this payload crosses Burst/native/binary boundaries.
- [WARN][86%][PROBABLE_RUNTIME_PAYLOAD] SIGNAL_LAYOUT_UNDECLARED | Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs:59 | ToolDepletedSignal
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public readonly struct ToolDepletedSignal`
  Required action: Add explicit StructLayout or document unmanaged field order before this payload crosses Burst/native/binary boundaries.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs:99 | _pendingTraumaHudSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<TraumaHudSignal> _pendingTraumaHudSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs:100 | _nextFrameTraumaHudSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<TraumaHudSignal> _nextFrameTraumaHudSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs:101 | _pendingInteractionSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<PlayerInteractionStressSignal> _pendingInteractionSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs:102 | _nextFrameInteractionSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<PlayerInteractionStressSignal> _nextFrameInteractionSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs:103 | _pendingToolDepletedSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<ToolDepletedSignal> _pendingToolDepletedSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs:104 | _nextFrameToolDepletedSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<ToolDepletedSignal> _nextFrameToolDepletedSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:58 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<RadiationTelemetryEntry> _telemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:919 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:921 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1102 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1112 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllBytes(path, payload);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1576 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1641 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_csvOverridePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1811 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1813 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1987 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1989 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Gameplay/SubmarineCoreDirector.cs:26 | SubmarinePhysicsBindingState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Gameplay/SuitMeshUpdateEvents.cs:42 | _pendingSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<SuitMeshUpdateSignal> _pendingSignals;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Gameplay/SuitMeshUpdateEvents.cs:43 | _nextFrameSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<SuitMeshUpdateSignal> _nextFrameSignals;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:113 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<SuitUpgradeTelemetryEntry> _telemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:127 | SuitUpgradeTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = TelemetryEntrySizeBytes)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:1176 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:1178 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/SuitUpgradeResolver.cs:27 | SuitStats
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Gameplay/ToolEffectEvents.cs:23 | ToolEffectSignal
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public readonly struct ToolEffectSignal`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Gameplay/VehicleCommandSignals.cs:49 | _pendingCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<VehicleCommandSignal> _pendingCommands;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Gameplay/VehicleCommandSignals.cs:50 | _nextFrameCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<VehicleCommandSignal> _nextFrameCommands;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Gameplay/VehicleMotor.cs:24 | SubmarineState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/VehicleMotor.cs:34 | ScheduledSweepState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs:206 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<VRSomaticBlackBoxEntry> _blackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs:2352 | VRSomaticRootSyncJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs:2426 | VRSomaticHandKinematicsJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs:2490 | BuildHeadCapsulecastCommandsJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs:2535 | ProcessHeadCapsulecastHitsJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:299 | _signalDetails
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<CombatDamageSignalDetail> _signalDetails;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:320 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<CombatTelemetryEntry> _telemetryRing;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:1208 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:1210 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:1456 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(dumpPath));`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:1457 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Gameplay/Loot/Contracts/LootMagnetContracts.cs:79 | LootMagnetSignalEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Gameplay/Loot/Contracts/LootMagnetContracts.cs:91 | LootMagnetTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 128)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs:1378 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs:1380 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/Mining/Contracts/DeployableSdfDrillContracts.cs:42 | DeployableSdfDrillExtractionInput
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 68)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/Mining/Contracts/DeployableSdfDrillContracts.cs:66 | DeployableSdfDrillExtractionResult
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 60)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Gameplay/Mining/Contracts/DeployableSdfDrillContracts.cs:93 | DeployableSdfDrillMacroRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 110)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Gameplay/Mining/Contracts/DeployableSdfDrillContracts.cs:129 | DeployableSdfDrillTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 56)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Graphics/Caustics/AnalyticalCausticsService.cs:73 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<CausticTelemetryEntry> _blackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Graphics/Caustics/AnalyticalCausticsService.cs:693 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Graphics/Caustics/AnalyticalCausticsService.cs:694 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:78 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<InstanceCullingTelemetryEntry> _telemetryRing;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:545 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:547 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:165 | DrsTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 48)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:1249 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:1250 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(Path.Combine(logDirectory, DumpFileName), FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:142 | FoveatedRenderTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = TelemetryRecordSizeBytes)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:1061 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:1063 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1193 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1195 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1244 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Interaction/EquipmentInteractionContracts.cs:98 | InteractionPacket
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 48)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Interaction/EquipmentInteractionContracts.cs:146 | InteractionSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 88)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Inventory/ItemTemplateRegistry.cs:56 | ItemTemplate
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 44)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:2130 | ShinobuEconomyTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct ShinobuEconomyTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:1224 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:1226 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs:74 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<GIRelayTelemetryEntry> _telemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs:762 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs:764 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:15 | LightShaftTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:604 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(path));`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:605 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftSource.cs:8 | LightShaftContribution
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Meta/GlobalProfileManager.cs:892 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Meta/GlobalProfileManager.cs:897 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllText(tempPath, json);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Meta/GlobalProfileManager.cs:900 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Meta/GlobalProfileManager.cs:913 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(tempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Meta/GlobalProfileManager.cs:932 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string json = File.ReadAllText(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/ModAssetManager.cs:180 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `pngBytes = File.ReadAllBytes(filePath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs:259 | _pendingCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<ModCommand> _pendingCommands;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs:260 | _pendingAupCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<ModAupCommand> _pendingAupCommands;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs:261 | _pendingRenderCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<ModRenderInstanceCommand> _pendingRenderCommands;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs:535 | ModCullTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ModCullTelemetryEntry`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs:43 | _cullTelemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ModCullTelemetryEntry> _cullTelemetry;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/ModLoader.cs:229 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string json = File.ReadAllText(manifestPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/ModLocalizationBridge.cs:109 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string json = File.ReadAllText(pending.FilePath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs:860 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string[] lines = File.ReadAllLines(fullSourcePath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs:877 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllLines(fullSourcePath, lines, new UTF8Encoding(false));`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Narrative/LoreMmfEncyclopedia.cs:48 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream indexStream = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Narrative/LoreMmfEncyclopedia.cs:49 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_payloadStream = new FileStream(payloadPath, FileMode.Open, FileAccess.Read, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs:165 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<MetaCampaignBlackBoxEntry> _blackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs:879 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs:881 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs:37 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<PrologueSequenceTelemetryEntry> _blackBox;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs:583 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs:586 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs:60 | _cartographyBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<CartographyBlackBoxEntry> _cartographyBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs:61 | _pendingMapRevealSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<MapRevealSignal> _pendingMapRevealSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs:1119 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs:1120 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(CartographyDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:141 | _physicsTargetWakeRequests
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<PhysicsCullingTargetWakeRequestSignal> _physicsTargetWakeRequests;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:1022 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/TetherVerletJobs.cs:388 | TetherVerletTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct TetherVerletTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/VerletCableDTOs.cs:798 | VerletBlackBoxWriteJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct VerletBlackBoxWriteJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Physics/KCC/SdfSqueezeJob.cs:16 | SdfSqueezeResult
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:73 | _mockFloodQueue
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<MockFloodSignal> _mockFloodQueue;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:74 | _mockImpactQueue
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<MockImpactSignal> _mockImpactQueue;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:75 | _cavitationQueue
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<CavitationAcousticSignal> _cavitationQueue;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:535 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:566 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:686 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:862 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logRoot);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:864 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:746 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:748 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:811 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:929 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Plugins/Crest/HectonCrestOceanDepthCacheRuntimeBridge.cs:75 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs:1405 | _powerBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<PowerGridBlackBoxEntry> _powerBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs:2764 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs:2766 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:220 | _mockStateSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<MockModuleStateSignal> _mockStateSignals;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:221 | _breachSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<HullBreachSignal> _breachSignals;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1310 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1312 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1380 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1748 | BreachSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeQueue<HullBreachSignal> BreachSignals;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:66 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<WfcOutpostPowerBootTelemetryEntry> _blackBox;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:707 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs:1068 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs:1070 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs:1098 | RtgTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 23)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs:87 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<OrbitalTelemetryEntry> _telemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs:823 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs:826 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:104 | _telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ReentryVfxTelemetryEntry> _telemetry;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:820 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:81 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<QAEnduranceBlackBoxEntry> _blackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:260 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(_csvPath));`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:792 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(_dumpPath));`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:793 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read)) // COLD ALLOC: FileStream[1] — crash blackbox dump — owner: QAEnduranceWatchdogBot`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:833 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(_resultPath));`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:930 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(_path));`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:931 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_stream = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous); // COLD ALLOC: FileStream[1] — async CSV file sink — owner: QAEnduranceCsvWriter`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs:63 | _blackbox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<HeadlessTelemetryEntry> _blackbox;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:113 | _blackbox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<FractureTelemetryEntry> _blackbox;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Quest/NarrativeDagInspectorWindow.cs:174 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string[] lines = File.ReadAllLines(NodeNamesPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Quest/QuestDagDataLoading.cs:61 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(binaryPath, FileMode.Open, FileAccess.Read, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Quest/QuestDagDataLoading.cs:420 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string csv = File.ReadAllText(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs:1073 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs:1082 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllBytes(fullPath, managed);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Quest/QuestGraphEvaluator.cs:37 | _pendingSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<QuestSignalPayload> _pendingSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Quest/QuestRuntimeTypes.cs:139 | QuestSaveHeader
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Quest/QuestStateManager.cs:1546 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.AppendAllText(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs:1131 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs:1132 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs:1180 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(s_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs:319 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs:341 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs:360 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs:387 | 
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
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1838 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1840 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs:61 | _writeQueue
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<PageWriteCommand> _writeQueue;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs:62 | _readQueue
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<PageReadCommand> _readQueue;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs:1857 | TelemetryWriteJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct TelemetryWriteJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:1721 | ScanTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ScanTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:1182 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:1184 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.FileWorker.cs:233 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, CsvBufferBytes, FileOptions.SequentialScan);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.FileWorker.cs:259 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, CsvBufferBytes, FileOptions.SequentialScan);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Tools/PerformanceBudgetController.cs:578 | SystemBudget
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Tools/PerformanceBudgetController.cs:596 | SystemBudgetInfo
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Tools/PerformanceMonitor.cs:348 | PerformanceSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs:176 | PendingDurabilityCommand
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][72%][STATIC_CONTRACT_REVIEW] MANAGED_STRING_IN_SIGNAL_LIKE_REVIEW | Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs:179 | PendingDurabilityCommand
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `public string ToolId;`
  Required action: This signal-like private/native-adjacent struct carries a managed string. Confirm it never crosses SignalBus<T>, NativeQueue<T>, Burst, or NativeArray boundaries; otherwise replace with FixedString or a stable uint hash.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs:188 | ItemState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Tools/ToolUpgradeSystem.cs:44 | ToolState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Tools/ToolUpgradeSystem.cs:61 | ToolRuntimeProfile
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Tools/ToolUpgradeSystem.cs:84 | ToolRuntimeStats
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Tools/WfcLaserCutRuntime.cs:507 | WfcLaserCutTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 96)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:168 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<TooltipBlackBoxEntry> _blackBox;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:1371 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(dumpPath));`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:1372 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs:61 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<DiegeticHudTelemetryEntry> _blackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs:556 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs:558 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:731 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:733 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(TelemetryDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:1035 | FrequencyTuningStageTarget
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 8)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:1042 | FrequencyTuningWaveGpuSegment
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:1050 | FrequencyTuningTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/UI/SonarHoloCompass.cs:43 | AcousticRadarBlipInput
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/UI/SonarHoloCompass.cs:50 | AcousticRadarBlipOutput
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:240 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<CockpitTelemetryEntry> _telemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2079 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2081 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2125 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:233 | _vitalsQueue
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<PlayerVitalsSignal> _vitalsQueue;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:234 | _pdaQueue
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<PdaOpenedSignal> _pdaQueue;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1242 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `byte[] bytes = File.ReadAllBytes(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1278 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `byte[] bytes = File.ReadAllBytes(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1340 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1549 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1551 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:20 | CompassBlackBoxEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 40)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:35 | CompassPresentationStateDTO
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 80)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:1317 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:1319 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:86 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `private NativeArray<TerminalTelemetryEntry> _telemetryRing;`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:87 | _mockPowerSignal
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<MockPowerStateSignal> _mockPowerSignal;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:88 | _mockDamageSignal
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<MockDamageScalarSignal> _mockDamageSignal;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:89 | _mockPowerStatusSignal
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<MockPowerStatusSignal> _mockPowerStatusSignal;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:92 | _clickScratch
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<TerminalClickSignal> _clickScratch;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:995 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_csvFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:1382 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:1384 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/UI/VR/OpenXRManualOverrideLever.cs:70 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ManualOverrideLeverTelemetryEntry> _blackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/VR/OpenXRManualOverrideLever.cs:605 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/VR/OpenXRManualOverrideLever.cs:607 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs:41 | CameraJuiceTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = CameraJuiceTelemetryEntrySizeBytes)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs:1698 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:1487 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:3572 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:3574 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/VFX/VfxComputeParticleBudgetCatalog.cs:289 | VfxComputeParticleBudget
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 28)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:1422 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, ProfileByteCount, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:1980 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:2064 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, scratch.Length, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:2809 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:2811 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:1996 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:2006 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs:312 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs:405 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string[] lines = File.ReadAllLines(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs:454 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs:456 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:566 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:568 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:624 | MaterialDecayState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = MaterialDecayStateSizeBytes)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/VFX/Wakes/WakeDisplacementData.cs:10 | WakeSource
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 128)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/VFX/Wakes/WakeDisplacementData.cs:27 | WakeTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs:155 | VisorRefractionTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = BlackBoxEntrySizeBytes)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs:897 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs:901 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs:41 | WaterlineTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs:64 | _telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<WaterlineTelemetryEntry> _telemetry;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs:589 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Visor/SpectrumSystem.cs:302 | _pendingPingReturnSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<PingReturnSignal> _pendingPingReturnSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Visor/SpectrumSystem.cs:303 | _nextFramePingReturnSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<PingReturnSignal> _nextFramePingReturnSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Visor/SpectrumSystem.cs:1623 | _activeSonarGeoTelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ActiveSonarGeoTelemetryEntry> _activeSonarGeoTelemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Visor/SpectrumSystem.cs:2922 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Visor/SpectrumSystem.cs:2924 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/World/AbsoluteUniversePositionBlit.cs:8 | AbsoluteUniversePositionBlit
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 48)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/AbyssalThermalManager.cs:111 | ThermalTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ThermalTelemetryEntry`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/World/AbyssalThermalManager.cs:669 | _thermalTelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ThermalTelemetryEntry> _thermalTelemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs:12 | BiomeTransitionSample
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs:31 | BiomeTransitionFogSource
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs:53 | BiomeTransitionFogResult
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:29 | EcosystemSectorSaveRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:37 | EcosystemBiomassSaveRun
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:48 | EcosystemIndexEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:56 | MacroSwarmTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:69 | FaunaMutationTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 48)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:244 | SectorPopulationState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/World/EcosystemDirector.cs:434 | BiomassImpactEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:444 | BiomassTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:457 | ApexTerritorySample
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:467 | ApexTerritoryOverlapResult
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:4509 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:4511 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:4584 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:4586 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:5310 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:5312 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/World/FloraInteractionManager.cs:41 | FloraInteractionPointGpuData
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:48 | WakeTrailStampCommand
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:70 | ParasiteNode
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:109 | FloraCascadeEventPayload
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/World/FloraInteractionManager.cs:120 | DefensiveSporeBurstState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:2727 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:2729 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read));`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:568 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:571 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:1624 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `lines = File.ReadAllLines(ProbeCsvPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:1677 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/World/GPUScatterDirector.cs:316 | _scatterTelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ScatterTelemetryEntry> _scatterTelemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/GPUScatterDirector.cs:1266 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/GPUScatterDirector.cs:1268 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs:865 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:466 | _floraGrowthTelemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<FloraGrowthTelemetryEntry> _floraGrowthTelemetry;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:467 | _scatterCullTelemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ScatterCullTelemetryEntry> _scatterCullTelemetry;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3025 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3027 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3192 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3194 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:36 | AbyssalPathTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:1754 | _abyssalPathTelemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<AbyssalPathTelemetryEntry> _abyssalPathTelemetry;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:27 | AbsoluteUniversePosition
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 48)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:204 | AbsoluteUniversePositionBlit128
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 48)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:259 | PoolSlotData
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 40)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:273 | EntityDataRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16, Size = 64)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:294 | PersistentWorldItemRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 204)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:352 | PersistentWorldDeltaRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:460 | PersistentWorldCompactDeltaRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:477 | TombstoneDecayCollectJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:2277 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(_indexedSectorOverrideDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:3259 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(state.EntityStateTempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:3554 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(state.EntityStateTempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:3829 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(entityStateTempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:4002 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(entityStateTempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:4159 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(entityStateTempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:245 | WreckGridCell
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:256 | WreckModuleRuntimeDefinition
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:279 | WreckModulePlacement
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:293 | WreckMergedVertex
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:302 | WreckLootRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:317 | WreckDebrisRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:336 | WreckDebrisCluster
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:351 | WreckArtifactRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:367 | WreckScorchDecalRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:382 | WreckBurialCutRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:396 | WreckTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:1195 | _telemetryEntries
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<WreckTelemetryEntry> _telemetryEntries;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:3141 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:3143 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:34 | StampCommand
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct StampCommand`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/SargassumCutManager.cs:56 | DamageVolumeStampCommand
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct DamageVolumeStampCommand`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:74 | SargassumFieldSample
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:87 | EntanglementStrainSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:98 | MassiveDisplacementSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:107 | CellData
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:115 | NestedAttachmentState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:135 | DisruptionZoneState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:150 | DisruptionSample
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:157 | ScavengerHostState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:167 | ExternalScavengerSiteState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [ERROR][96%][CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL] RUNTIME_SIGNAL_PACK1_FORBIDDEN | Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:176 | DebrisTimer
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:183 | DensitySourceData
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:190 | DensityContributionData
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 12)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:276 | _pendingEntanglementStrain
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<EntanglementStrainSignal> _pendingEntanglementStrain;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:277 | _nextFrameEntanglementStrain
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<EntanglementStrainSignal> _nextFrameEntanglementStrain;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:278 | _pendingMassiveDisplacement
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<MassiveDisplacementSignal> _pendingMassiveDisplacement;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:279 | _nextFrameMassiveDisplacement
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<MassiveDisplacementSignal> _nextFrameMassiveDisplacement;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:84 | BoidKillSignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:95 | FoodChainTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:112 | BoidSensoryBlackBoxEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:124 | PopulationDensityPoint
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 8)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:279 | FoveatedSimulationInput
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:292 | FoveatedSimulationDecision
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:407 | StaticObstacleData
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 28)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5567 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5569 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5732 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5735 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/ShinobuStreamingRuntime.cs:497 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string text = File.ReadAllText(file);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][50%][EDITOR_ONLY_REVIEW] EDITOR_PACK1_REVIEW | Assets/_Project/Scripts/World/TOOL_Procedural_Wreckage_Generator.cs:16 | GridCell
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: Editor/test Pack=1 does not gate runtime memory, but avoid copying it into player DTOs.
- [INFO][50%][EDITOR_ONLY_REVIEW] EDITOR_PACK1_REVIEW | Assets/_Project/Scripts/World/TOOL_Procedural_Wreckage_Generator.cs:27 | MeshDataSlice
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: Editor/test Pack=1 does not gate runtime memory, but avoid copying it into player DTOs.
- [INFO][50%][EDITOR_ONLY_REVIEW] EDITOR_PACK1_REVIEW | Assets/_Project/Scripts/World/TOOL_Procedural_Wreckage_Generator.cs:36 | ModuleDefinition
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Editor/test Pack=1 does not gate runtime memory, but avoid copying it into player DTOs.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs:3253 | NavPortal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs:1271 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs:1273 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:822 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ChunkResidencyTelemetryEntry> _telemetryRing;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4892 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4894 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs:50 | BiolumPointGpuData
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:59 | BiolumTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:1448 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:1450 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs:51 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<BiomeBoundaryTelemetryEntry> _telemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs:423 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs:425 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/Contracts/InstanceCullingContracts.cs:34 | InstanceCullingCameraPositionSignal
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct InstanceCullingCameraPositionSignal`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/Contracts/InstanceCullingContracts.cs:45 | InstanceCullingCameraFrustumSignal
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct InstanceCullingCameraFrustumSignal`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/Contracts/InstanceCullingContracts.cs:96 | InstanceCullingTelemetry
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct InstanceCullingTelemetry`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeContracts.cs:594 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeCsvHotloader.cs:70 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeVaultRuntime.cs:453 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeVaultRuntime.cs:467 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/GPR/GroundRadarJobs.cs:25 | GroundRadarTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 36)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:88 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<OutpostTelemetryEntry> _telemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:1408 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:81 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ProceduralOreTelemetryEntry> _telemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:1066 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs:846 | RegrowthTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct RegrowthTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs:543 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs:554 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllBytes(path, dump);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/CrashTelemetryBuffer.cs:214 | TelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct TelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][68%][EDITOR_ONLY_REVIEW] EDITOR_SIGNAL_NAME_SHADOWS_RUNTIME | Assets/_Project/Scripts/Editor/BlackBoxBinaryReader.cs:40 | TelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct TelemetryEntry`
  Required action: Editor/test structs should not shadow runtime signal names; rename smoke payloads or fully isolate them.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/DepthStressGranularSynthesisKernel.cs:87 | MockPressureSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockPressureSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyData.cs:152 | MockPressureSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockPressureSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Core/InputDeterminismDtos.cs:51 | InputTelemetryEntryDTO
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct InputTelemetryEntryDTO`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Input/Determinism/DeterministicInputContracts.cs:73 | InputTelemetryEntryDTO
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct InputTelemetryEntryDTO`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Core/InputDeterminismDtos.cs:65 | MockCollisionSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockCollisionSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Input/Determinism/DeterministicInputContracts.cs:87 | MockCollisionSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockCollisionSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Core/InputDeterminismDtos.cs:74 | MockToolEquipSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockToolEquipSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Input/Determinism/DeterministicInputContracts.cs:96 | MockToolEquipSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockToolEquipSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Core/InputDeterminismDtos.cs:83 | MockPlayerKinematicsSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockPlayerKinematicsSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Input/Determinism/DeterministicInputContracts.cs:105 | MockPlayerKinematicsSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockPlayerKinematicsSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:110 | ThermalTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct ThermalTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/World/AbyssalThermalManager.cs:111 | ThermalTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct ThermalTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:95 | MockAcousticSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockAcousticSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/VFX/VolumetricSiltContracts.cs:45 | MockAcousticSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockAcousticSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityTypes.cs:122 | MockCombatDamageSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockCombatDamageSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyData.cs:167 | MockCombatDamageSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockCombatDamageSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:84 | MockCombatDamageSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockCombatDamageSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [ERROR][92%][CONFIRMED_RUNTIME_CONTRACT_COLLISION] DUPLICATE_RUNTIME_SIGNAL_NAME | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:116 | MockItemAcquiredSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockItemAcquiredSignal`
  Required action: Signal names must be globally unique across runtime contracts. Merge duplicate contracts or wrap mock/domain-local payloads behind explicit names.
- [ERROR][92%][CONFIRMED_RUNTIME_CONTRACT_COLLISION] DUPLICATE_RUNTIME_SIGNAL_NAME | Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:126 | MockItemAcquiredSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockItemAcquiredSignal`
  Required action: Signal names must be globally unique across runtime contracts. Merge duplicate contracts or wrap mock/domain-local payloads behind explicit names.

## Non-Claims

- This audit does not prove Unity import, player build, IL2CPP, runtime GC, profiler, scene wiring, or actual struct sizeof(T).
- Static confidence is not semantic proof. The next precision step is an out-of-band Roslyn runner using Assets/Plugins/Roslyn without wiring analyzers into Unity projects.
- This audit intentionally reports legacy/shared ownership debt instead of silently modifying cross-domain contracts.

