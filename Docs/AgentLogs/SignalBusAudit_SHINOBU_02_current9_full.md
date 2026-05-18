# SHINOBU_02 Signal Bus Contract Audit CLI

Evidence Class: STATIC_SOURCE_CLASSIFIED
Scope: Full
Generated UTC: 2026-05-18T20:59:21.3961948Z

## Summary

- Files scanned: 1757 C# / 61 compute
- Signal-like definitions found: 581
- Signal definitions still in Core/GlobalSignals.cs: 162
- Pack=1 layouts: 353
- Runtime signal Pack=1 layouts: 0
- Runtime signal transitive Pack=1 field hits: 1
- Signal-like definitions without nearby StructLayout: 33
- Managed event surface hits: 0
- Local native telemetry ring hits: 77
- Registered local telemetry rings: 51
- Local native signal queue hits: 37
- Compute 1024-thread-group hits: 0
- Hot-path heuristic hits: 0
- Cold/fatal sync I/O review hits: 372
- Assembly contract boundary hits: 0
- Errors: 13
- Warnings: 535
- Infos: 469
- Confirmed/probable errors at confidence >= 90: 13
- Review-only findings below confidence 75: 670

## Rule Breakdown

- COLD_OR_FATAL_SYNC_IO_REVIEW: total 372, errors 0, warnings 0, infos 372, avg confidence 64
- PACK1_RUNTIME_NATIVE_REVIEW: total 157, errors 0, warnings 157, infos 0, avg confidence 78
- PACK1_REQUIRES_OWNER_JUSTIFICATION: total 142, errors 0, warnings 142, infos 0, avg confidence 68
- RUNTIME_SYNC_FILE_IO_REVIEW: total 95, errors 0, warnings 95, infos 0, avg confidence 76
- LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT: total 51, errors 0, warnings 51, infos 0, avg confidence 88
- PACK1_FILE_FORMAT_BOUNDARY_REVIEW: total 51, errors 0, warnings 0, infos 51, avg confidence 62
- DUPLICATE_SIGNAL_LIKE_NAME_REVIEW: total 33, errors 0, warnings 33, infos 0, avg confidence 74
- SIGNAL_LAYOUT_REVIEW: total 27, errors 0, warnings 27, infos 0, avg confidence 65
- LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW: total 21, errors 0, warnings 0, infos 21, avg confidence 70
- POSSIBLE_ORPHANED_SIGNAL_QUEUE: total 16, errors 0, warnings 16, infos 0, avg confidence 82
- LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS: total 11, errors 0, warnings 0, infos 11, avg confidence 92
- DUPLICATE_RUNTIME_SIGNAL_NAME: total 8, errors 8, warnings 0, infos 0, avg confidence 92
- LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY: total 5, errors 0, warnings 5, infos 0, avg confidence 73
- EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW: total 5, errors 0, warnings 0, infos 5, avg confidence 56
- LOCAL_NATIVE_TELEMETRY_RING_UNOWNED: total 5, errors 5, warnings 0, infos 0, avg confidence 90
- LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW: total 4, errors 0, warnings 0, infos 4, avg confidence 68
- EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW: total 3, errors 0, warnings 3, infos 0, avg confidence 60
- SIGNAL_LAYOUT_UNDECLARED: total 3, errors 0, warnings 3, infos 0, avg confidence 86
- EDITOR_PACK1_REVIEW: total 3, errors 0, warnings 0, infos 3, avg confidence 50
- EDITOR_SIGNAL_LAYOUT_REVIEW: total 2, errors 0, warnings 0, infos 2, avg confidence 55
- TRANSITIVE_PACK1_FIELD_REVIEW: total 1, errors 0, warnings 1, infos 0, avg confidence 82
- MANAGED_STRING_IN_SIGNAL_LIKE_REVIEW: total 1, errors 0, warnings 1, infos 0, avg confidence 72
- EDITOR_SIGNAL_NAME_SHADOWS_RUNTIME: total 1, errors 0, warnings 1, infos 0, avg confidence 68

## Classification Breakdown

- COLD_OR_FATAL_IO_BOUNDARY: 372
- PROBABLE_RUNTIME_NATIVE_PAYLOAD: 157
- STATIC_LAYOUT_REVIEW: 143
- IO_PRESSURE_HEURISTIC: 95
- CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL: 51
- FILE_FORMAT_OR_SERIALIZATION_CANDIDATE: 51
- STATIC_CONTRACT_REVIEW: 34
- NAME_BASED_REVIEW: 27
- REGISTERED_LOCAL_QUEUE_REVIEW: 21
- PROBABLE_SIGNAL_CORRIDOR_BYPASS: 16
- EDITOR_ONLY_REVIEW: 14
- CONFIRMED_VAULT_ALIAS_REVIEW: 11
- CONFIRMED_RUNTIME_CONTRACT_COLLISION: 8
- STATIC_DECLARATION_REVIEW: 5
- PROBABLE_NATIVE_OWNERSHIP_BREACH: 5
- SIGNAL_SCRATCH_REVIEW: 4
- PROBABLE_RUNTIME_PAYLOAD: 3

## Findings

- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/ConstructionManager.cs:155 | _deconstructionBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<HabitatDeconstructionTelemetryEntry> _deconstructionBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ConstructionManager.cs:1163 | DumpDeconstructionBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ConstructionManager.cs:1165 | DumpDeconstructionBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/EncounterDirector.cs:1040 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(parent);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/EncounterDirector.cs:1042 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/FaunaDirector.cs:211 | AcousticPanicCommand
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct AcousticPanicCommand`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/GlobalPhysicsStateManager.cs:141 | PhysicsImpactSignal
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public readonly struct PhysicsImpactSignal`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/GlobalPhysicsStateManager.cs:3194 | DumpPhysicsCullingBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/GlobalPhysicsStateManager.cs:3210 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/HectonCelestialEngine.cs:88 | EclipseStartedEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6074 | DumpCelestialBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonCelestialEngine.cs:6076 | DumpCelestialBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:2372 | DumpOceanSurfaceTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:2374 | DumpOceanSurfaceTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:3749 | WriteFluidAdvectionTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:3751 | WriteFluidAdvectionTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:5147 | DumpMaelstromTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:5149 | DumpMaelstromTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:6073 | DumpAbyssalFlowTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:6075 | DumpAbyssalFlowTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonNarrativeDirector.cs:934 | DumpBlackBoxToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonNarrativeDirector.cs:936 | DumpBlackBoxToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonPlayerMovement.cs:3097 | DumpPlayerKinematicsBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonPlayerMovement.cs:3099 | DumpPlayerKinematicsBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonPlayerMovement.cs:8186 | DumpCinematicFocusBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonPlayerMovement.cs:8188 | DumpCinematicFocusBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/HectonSurvivalSystem.cs:138 | SurvivalDatabaseItemRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 20)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/HectonVoxelEngine.cs:2836 | _voxelMeshPipelineBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<VoxelMeshPipelineTelemetryEntry> _voxelMeshPipelineBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonVoxelEngine.cs:5403 | DumpVoxelMeshPipelineBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/HectonVoxelEngine.cs:5405 | DumpVoxelMeshPipelineBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/InventoryEvents.cs:67 | InventoryPhysicalDropRequestPayload
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/ITickable.cs:35 | H8TimeSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/LocalizationManager.cs:962 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/LocalizationManager.cs:1018 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/LocRegistry.cs:474 | _telemetryFrames
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `private static NativeArray<BabelTelemetryEntry> _telemetryFrames;`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/LocRegistry.cs:1460 | TryApplyLocOverridesCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/LocRegistry.cs:3005 | WriteTelemetryDumpFiles
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(docsPath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/LocRegistry.cs:3020 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:4438 | DumpSalinityCorrosionBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:4440 | DumpSalinityCorrosionBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:4604 | DumpInventoryBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PlayerInventory.cs:4606 | DumpInventoryBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/RepairTool.cs:89 | RepairToolBlackBoxEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = RepairBlackBoxEntrySizeBytes)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/RepairTool.cs:1352 | DumpRepairBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/RepairTool.cs:1354 | DumpRepairBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ScannerTool.cs:1302 | DumpScannerBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ScannerTool.cs:1304 | DumpScannerBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/SpatialAudioManager.cs:908 | _virtualVoiceBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `private NativeArray<VirtualVoiceTelemetryEntry> _virtualVoiceBlackBox; // Vault alias; GlobalDataVault owns backing memory.`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/SpatialAudioManager.cs:952 | _acousticPortalBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `private NativeArray<AcousticTelemetryEntry> _acousticPortalBlackBox; // Vault alias; GlobalDataVault owns backing memory.`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SpatialAudioManager.cs:3622 | DumpVirtualVoiceBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SpatialAudioManager.cs:3624 | DumpVirtualVoiceBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SpatialAudioManager.cs:3678 | TryLoadAcousticLutFallbackCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `byte[] bytes = File.ReadAllBytes(path); // COLD ALLOC: byte[524288] - one-shot Sabine RT60+damping fallback read - owner: SpatialAudioManager`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SpatialAudioManager.cs:6359 | DumpAcousticPortalBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SpatialAudioManager.cs:6361 | DumpAcousticPortalBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SubmarineFluidDynamics.cs:5906 | WriteHydroBlackBoxDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SubmarineFluidDynamics.cs:5908 | WriteHydroBlackBoxDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1668 | DumpDamageControlTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1670 | DumpDamageControlTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/TerrainChunkGeneratedEvents.cs:13 | _events
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<TerrainChunkGeneratedSignal> _events;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/TetherInstance.cs:246 | _verletTelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `private NativeArray<TetherVerletTelemetryEntry> _verletTelemetryRing;`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/TetherManager.cs:122 | TetherManagerTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct TetherManagerTelemetryEntry`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/TetherManager.cs:101 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `private NativeArray<TetherManagerTelemetryEntry> _telemetryRing;`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VoxelDeltaProcessor.cs:3952 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VoxelDeltaProcessor.cs:3954 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs:139 | _terrainSeamBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<TerrainSeamTelemetryEntry> _terrainSeamBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs:1643 | DumpTerrainSeamBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs:1645 | DumpTerrainSeamBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:571 | TryLoadLegacyWeatherFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:712 | TryLoadWeatherProfilesCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:903 | DumpTelemetryToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:905 | DumpTelemetryToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:1895 | ScanTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ScanTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:1037 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:1041 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:1140 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:734 | _granularTelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `private NativeArray<GranularAudioTelemetryEntry> _granularTelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:736 | _prologueTransitionTelemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `private NativeArray<PrologueAudioTransitionTelemetryEntry> _prologueTransitionTelemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:9886 | DumpGranularTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:9900 | WriteGranularTelemetryDumpCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:9935 | DumpPrologueTransitionTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:9945 | WritePrologueTransitionTelemetryDumpCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:88 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `private NativeArray<VwsTelemetryEntry> _telemetryRing; // Vault alias; GlobalDataVault owns backing memory.`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:845 | DumpTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:847 | DumpTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:1558 | HandleH8MemoryFatalLog
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:4639 | PrepareBackgroundDomainHandshake
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(telemetryPath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:4683 | InspectPreviousBootState
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/DroneFleetManager.cs:4389 | TryApplyDroneSpecsCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(resolvedPath))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs:52 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<FluidPipeTelemetryEntry> _telemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs:651 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs:653 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:262 | _floodBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<HabitatFloodBlackBoxEntry> _floodBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:2251 | DumpFloodBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:2253 | DumpFloodBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/ModularBaseConstructionValidator.cs:630 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/ModularBaseConstructionValidator.cs:632 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllBytes(absolutePath, bytes);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:48 | DockTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 128)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:1159 | DumpDockTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:1163 | DumpDockTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs:476 | DumpFailure
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs:478 | DumpFailure
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:94 | DodReplayInputEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:123 | DodReplayJobProfileRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:145 | DodReplayBurstPanicRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:169 | DodReplayAupDriftRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:195 | DodReplayEntityGhostRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:213 | DodReplayLogisticFlowRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:233 | DodReplayAtmosphereCellRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 40)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:259 | DodReplayVramAllocationRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:279 | DodReplayPhysicsSmokeRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 56)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:1656 | InitializeReplayFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_replayStream = new FileStream(_replayPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.RandomAccess);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:1796 | ReplaySourceHash
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/DodReplayRecorder.cs:1805 | AupDriftState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1175 | DumpTelemetryBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1177 | DumpTelemetryBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistry.cs:335 | ForceOverrideToken
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:175 | OrbitalDirectorSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:236 | StreamingHlodImpostorPoint
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:322 | DamagePacket
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:493 | CurrentMeta
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:520 | GerstnerWaveComponent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:557 | OceanGerstnerWaveBufferMeta
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:569 | WeatherRuntimeSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:631 | CelestialRuntimeSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:695 | GIRelayRuntimeSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:746 | SeismicRuntimeSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:1016 | AudioEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:1041 | AudioTransitionState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:1485 | VRSomaticChestSocketPose
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:1506 | VRSomaticCollisionState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:1547 | VRSomaticSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:1609 | VRSomaticHandPose
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:1778 | NarrativeSpatialTriggerAuthoring
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:1799 | PlayerRuntimePoseSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2221 | HabitatRoomWaterlineSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2633 | GasRoomSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2672 | GasBaseHibernationSnapshot
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2714 | ToxicitySignal
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2747 | GasDynamicsNativeMemoryAudit
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2850 | HectonHardwareProfile
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:3002 | RegistryEventPayload
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:3442 | EcosystemSectorPopulationSample
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:3504 | FaunaGenomeMutationRequest
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:3535 | AmbientBiotaState
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:3561 | AmbientBiotaTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:3612 | EcosystemBiomassAuditSample
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.cs:944 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.cs:946 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs:1469 | TryReadCsvFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs:1754 | DumpScalabilityDictatorBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs:1778 | DumpScalabilityDictatorBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs:1813 | DumpScalabilityDictatorBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1038 | TryStageInputProfileCsvFromFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1340 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1342 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_inputReplayStream = new FileStream(replayPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1495 | DumpDeterministicInputBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/InputDispatcher.cs:1499 | DumpDeterministicInputBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1489 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1491 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1556 | LoadValidationRulesCsvInternal
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/NativeBitmask256.cs:7 | NativeBitmask256
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/NativeMemorySentinel.cs:29 | NativeAllocationSnapshotSource
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
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
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/StackQueue.cs:10 | StackQueue
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:2629 | DumpMasterPipelineTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:2630 | DumpMasterPipelineTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:2737 | ParseMasterExecutionPriorityCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Open(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3250 | DumpDispatcherBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `System.IO.Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/SystemDispatcher.cs:3267 | DumpDispatcherBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (System.IO.FileStream stream = System.IO.File.Open(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Dev/BotController.cs:451 | FlushCsvSamplesCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:2408 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:2417 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllBytes(path, bytes);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1031 | TryLoadLegacyFaultBinaryAt
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1516 | DumpSeismicDirectorTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1517 | DumpSeismicDirectorTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(SeismicDirectorConstants.DumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1568 | TryPollCsvProfileOverrides
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1648 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1649 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(TelemetryDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][82%][STATIC_LAYOUT_REVIEW] TRANSITIVE_PACK1_FIELD_REVIEW | Assets/_Project/Scripts/Environment/WeatherEvents.cs:21 | WeatherEventPayload.CurrentMeta
  Evidence kind: STRUCT_BODY_FIELD_SCAN
  Evidence: `public CurrentMeta CurrentMeta;`
  Required action: Runtime signal/native payload embeds a Pack=1 struct. Replace it with an aligned runtime projection, or prove this field never crosses SignalBus<T>, NativeArray<T>, Burst jobs, or runtime memcpy boundaries.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1538 | DumpTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1547 | DumpTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1606 | DumpBiteTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1609 | DumpBiteTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs:1371 | DumpTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs:1373 | DumpTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:2113 | TryLoadBehaviorOverridesCsvCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string csv = File.ReadAllText(path);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:2740 | DumpRetinalBlackBoxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:2755 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:2976 | DumpAlphaLeviathanBlackBoxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:1272 | DumpTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs:1274 | DumpTelemetryBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs:169 | ContextualPhysicalIkFootData
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs:1532 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ContextualPhysicalIkTelemetryEntry> _telemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs:2498 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs:2500 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1566 | TryLoadMmfCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1658 | PersistMmfCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1660 | PersistMmfCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1724 | DumpTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:3514 | DumpFaultTelemetryIfNeeded
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:3527 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:919 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:921 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1102 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1112 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllBytes(path, payload);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1626 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1691 | TryApplyCsvOverrides
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_csvOverridePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1860 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1862 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1986 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1988 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:1176 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:1178 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/SuitUpgradeResolver.cs:27 | SuitStats
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Gameplay/ToolEffectEvents.cs:23 | ToolEffectSignal
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public readonly struct ToolEffectSignal`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Gameplay/VehicleCommandSignals.cs:52 | _pendingCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<VehicleCommandSignal> _pendingCommands;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Gameplay/VehicleCommandSignals.cs:53 | _nextFrameCommands
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
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs:2449 | VRSomaticRootSyncJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs:2523 | VRSomaticHandKinematicsJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs:2587 | BuildHeadCapsulecastCommandsJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs:2632 | ProcessHeadCapsulecastHitsJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
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
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:2220 | ShinobuEconomyTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct ShinobuEconomyTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:1238 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:1240 | DumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:1275 | DumpTelemetryRingH8Dump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:1277 | DumpTelemetryRingH8Dump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs:74 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<GIRelayTelemetryEntry> _telemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs:762 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs:764 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:1852 | InteriorGITelemetryScanJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct InteriorGITelemetryScanJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:1144 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:1174 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:1176 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:293 | Queue
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeQueue<FutureCommandEnvelope> Queue;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:766 | TryReloadAllowedOpcodesCsvFromDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `byte[] managedBytes = File.ReadAllBytes(path);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:1232 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:1233 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(DumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/ModAssetManager.cs:180 | LoadRawTexture
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `pngBytes = File.ReadAllBytes(filePath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs:266 | _pendingCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<ModCommand> _pendingCommands;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs:267 | _pendingAupCommands
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeQueue<ModAupCommand> _pendingAupCommands;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs:268 | _pendingRenderCommands
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/ModdingAPI/ModLoader.cs:229 | TryReadManifest
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string json = File.ReadAllText(manifestPath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Narrative/LoreMmfEncyclopedia.cs:48 | TryOpen
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream indexStream = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Narrative/LoreMmfEncyclopedia.cs:49 | TryOpen
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_payloadStream = new FileStream(payloadPath, FileMode.Open, FileAccess.Read, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs:431 | TryLoadLegacyLatencyProfile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_legacyProfilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs:605 | TryApplyCsvOverride
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_csvProfilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs:747 | DumpNetcodeBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs:750 | DumpNetcodeBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:1786 | TryParseAssetCacheRulesCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string csv = File.ReadAllText(absolutePath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:2018 | DumpHeapTelemetryToFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:2020 | DumpHeapTelemetryToFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs:60 | _cartographyBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<CartographyBlackBoxEntry> _cartographyBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs:61 | _pendingMapRevealSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<MapRevealSignal> _pendingMapRevealSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs:1119 | DumpCartographyBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory("Docs/AgentLogs");`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs:1120 | DumpCartographyBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(CartographyDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:147 | _physicsTargetWakeRequests
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<PhysicsCullingTargetWakeRequestSignal> _physicsTargetWakeRequests;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:380 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, PhysicsCullingLegacyRadiiHeaderBytes, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:1216 | TickPhysicsCullingCsvOverrideMonitor
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/TetherVerletJobs.cs:388 | TetherVerletTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct TetherVerletTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Physics/VerletCableDTOs.cs:808 | VerletBlackBoxWriteJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct VerletBlackBoxWriteJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:746 | DumpAutopsyReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:748 | DumpAutopsyReport
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:811 | TryLoadLegacyMetabolismTables
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:929 | MonitorCsvOverrides
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs:1405 | _powerBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<PowerGridBlackBoxEntry> _powerBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs:2764 | DumpPowerBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs:2766 | DumpPowerBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:220 | _mockStateSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<MockModuleStateSignal> _mockStateSignals;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:221 | _breachSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<HullBreachSignal> _breachSignals;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1310 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1312 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1380 | TryReloadCsvOverrides
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1761 | BreachSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `public NativeQueue<HullBreachSignal> BreachSignals;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:66 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<WfcOutpostPowerBootTelemetryEntry> _blackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:707 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:81 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<QAEnduranceBlackBoxEntry> _blackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:259 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(_csvPath));`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:791 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(_dumpPath));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:792 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read)) // COLD ALLOC: FileStream[1] — crash blackbox dump — owner: QAEnduranceWatchdogBot`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:832 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(_resultPath));`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:929 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(_path));`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:930 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_stream = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous); // COLD ALLOC: FileStream[1] — async CSV file sink — owner: QAEnduranceCsvWriter`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Quest/NarrativeDagInspectorWindow.cs:174 | LoadNodeNames
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string[] lines = File.ReadAllLines(NodeNamesPath);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Quest/QuestDagDataLoading.cs:61 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(binaryPath, FileMode.Open, FileAccess.Read, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Quest/QuestDagDataLoading.cs:427 | BuildAllDoneMask
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string csv = File.ReadAllText(path);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs:1191 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs:1193 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/Quest/QuestGraphEvaluator.cs:37 | _pendingSignals
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<QuestSignalPayload> _pendingSignals;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Quest/QuestStateManager.cs:1546 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.AppendAllText(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs:1131 | TryWriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs:1132 | TryWriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs:1180 | RefreshCsvOverrides
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(s_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/HectonUberNoirRuntimeBridge.cs:319 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Rendering/ShinobuMaterialResponseRuntime.cs:996 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/ShinobuMaterialResponseRuntime.cs:1207 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/ShinobuMaterialResponseRuntime.cs:1209 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs:2059 | TelemetryWriteJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct TelemetryWriteJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:1721 | ScanTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct ScanTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:1182 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:1184 | WriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:2155 | TelemetryWriteJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `private struct TelemetryWriteJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:1213 | LoadGlitchTableCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_glitchTableFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:1281 | TryApplyCsvOverride
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_csvFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:1675 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:1688 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_dumpFullPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:168 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<TooltipBlackBoxEntry> _blackBox;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:1371 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(dumpPath));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:1372 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs:61 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<DiegeticHudTelemetryEntry> _blackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs:556 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs:558 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:731 | DumpTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:733 | DumpTelemetryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(TelemetryDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2079 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2081 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2125 | WriteDamageHolographerMirrorDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:232 | _vitalsQueue
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<PlayerVitalsSignal> _vitalsQueue;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [INFO][70%][REGISTERED_LOCAL_QUEUE_REVIEW] LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:233 | _pdaQueue
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<PdaOpenedSignal> _pdaQueue;`
  Required action: This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1244 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `byte[] bytes = File.ReadAllBytes(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1280 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `byte[] bytes = File.ReadAllBytes(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1342 | TryReadCsvBytes
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1551 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1553 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs:41 | CameraJuiceTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = CameraJuiceTelemetryEntrySizeBytes)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs:1698 | DumpCameraJuiceTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:1487 | ReadSiltProfileCsvBytes
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:3572 | TryWriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:3574 | TryWriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/VFX/VfxComputeParticleBudgetCatalog.cs:289 | VfxComputeParticleBudget
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 28)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:698 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:700 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:764 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs:950 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs:954 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs:63 | _telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<WaterlineTelemetryEntry> _telemetry;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs:558 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/SpectrumSystem.cs:2922 | DumpActiveSonarGeoTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Visor/SpectrumSystem.cs:2924 | DumpActiveSonarGeoTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:29 | EcosystemSectorSaveRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:37 | EcosystemBiomassSaveRun
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:4509 | DumpMacroSwarmBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:4511 | DumpMacroSwarmBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:4584 | DumpFaunaMutationBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:4586 | DumpFaunaMutationBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:5310 | DumpBiomassBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/EcosystemDirector.cs:5312 | DumpBiomassBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:2727 | DumpWakeBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraInteractionManager.cs:2729 | DumpWakeBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:1050 | TryDumpTelemetryBuffer
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:1053 | TryDumpTelemetryBuffer
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:2749 | TryLoadCsvMockProfile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (var stream = new FileStream(ProbeCsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GlobalWorldSampler.cs:2779 | SaveCsvMockProfile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/World/GPUScatterDirector.cs:316 | _scatterTelemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ScatterTelemetryEntry> _scatterTelemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GPUScatterDirector.cs:1266 | DumpScatterBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GPUScatterDirector.cs:1268 | DumpScatterBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs:865 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:466 | _floraGrowthTelemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<FloraGrowthTelemetryEntry> _floraGrowthTelemetry;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:467 | _scatterCullTelemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ScatterCullTelemetryEntry> _scatterCullTelemetry;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3025 | DumpFloraGrowthTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3027 | DumpFloraGrowthTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3192 | TryWriteScatterCullTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:3194 | TryWriteScatterCullTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:36 | AbyssalPathTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:1754 | _abyssalPathTelemetry
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `private NativeArray<AbyssalPathTelemetryEntry> _abyssalPathTelemetry;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:261 | PoolSlotData
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 40)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:275 | EntityDataRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:297 | PersistentWorldItemRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 204)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:355 | PersistentWorldDeltaRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:463 | PersistentWorldCompactDeltaRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [INFO][62%][FILE_FORMAT_OR_SERIALIZATION_CANDIDATE] PACK1_FILE_FORMAT_BOUNDARY_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:480 | TombstoneDecayCollectJob
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 16)]`
  Required action: If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:2280 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(_indexedSectorOverrideDirectory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:3262 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(state.EntityStateTempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:3557 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(state.EntityStateTempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:3832 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(entityStateTempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:4005 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(entityStateTempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:4162 | 
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
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:279 | WreckModulePlacement
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:293 | WreckMergedVertex
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:302 | WreckLootRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:317 | WreckDebrisRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:336 | WreckDebrisCluster
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:351 | WreckArtifactRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:367 | WreckScorchDecalRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:382 | WreckBurialCutRecord
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:396 | WreckTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:1195 | _telemetryEntries
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<WreckTelemetryEntry> _telemetryEntries;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:3141 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs:3143 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs:176 | DebrisTimer
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5567 | TryDumpFoodChainTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5569 | TryDumpFoodChainTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5732 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5735 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/ShinobuStreamingRuntime.cs:551 | 
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs:1271 | DumpAbyssalPathTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs:1273 | DumpAbyssalPathTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:841 | VolcanicTelemetryFinalizeJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct VolcanicTelemetryFinalizeJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:1532 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 256, FileOptions.SequentialScan);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:1847 | ReadCsvBytesCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.OpenRead(path);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:2014 | DumpBlackBoxIfFaulted
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:2016 | DumpBlackBoxIfFaulted
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Create(path);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:851 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `private NativeArray<ChunkResidencyTelemetryEntry> _telemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:5034 | DumpTelemetryToPath
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:5036 | DumpTelemetryToPath
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:1280 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:1282 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:1326 | AmbientBiotaGpuInstance
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanCognitionVault.cs:407 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanCognitionVault.cs:411 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanCognitionVault.cs:640 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs:791 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs:959 | TryWriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs:962 | TryWriteDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs:1043 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(path);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs:347 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs:893 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs:895 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:2246 | CountTelemetryCountersJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct CountTelemetryCountersJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:660 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, share, math.max(1, limit), FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1222 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1224 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:1045 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, share, math.max(1, limit), FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:1093 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:1095 | 
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs:839 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs:844 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:871 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:873 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/Animation/FaunaProcedural/ProceduralBoneBlenderJobs.cs:313 | ProceduralBoneTelemetryReduceJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct ProceduralBoneTelemetryReduceJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Animation/FaunaProcedural/ProceduralBoneBlenderTypes.cs:549 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Animation/FaunaProcedural/ProceduralBoneBlenderTypes.cs:551 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Animation/IK/LeviathanTerrainIkJobs.cs:43 | LeviathanTerrainIkTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 96)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Animation/IK/LeviathanTerrainIkJobs.cs:89 | Validate
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Animation/IK/LeviathanTerrainIkJobs.cs:91 | Validate
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Animation/IK/VRPhysicalHandPresenceIkJobs.cs:309 | Validate
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Animation/IK/VRPhysicalHandPresenceIkJobs.cs:311 | Validate
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:1024 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(BlackBoxDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:1051 | PrepareBlackBoxDumpDirectoryCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(BlackBoxDumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:231 | _mockPredator
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<MockPredatorProximitySignal> _mockPredator; // Vault alias; GlobalDataVault owns backing memory.`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:232 | _mockDepth
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<MockDepthSignal> _mockDepth; // Vault alias; GlobalDataVault owns backing memory.`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:233 | _mockTension
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<MockTensionSignal> _mockTension; // Vault alias; GlobalDataVault owns backing memory.`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [INFO][92%][CONFIRMED_VAULT_ALIAS_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:234 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_VAULT_ALIAS
  Evidence: `private NativeArray<AudioStemTelemetryEntry> _telemetryRing; // Vault alias; GlobalDataVault owns backing memory.`
  Required action: This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1071 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1075 | DumpTelemetryOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1111 | PollCsvRulesCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Bridge/H8BridgeContracts.cs:96 | H8FacadeTelemetryDumpHeader
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs:410 | RequestBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs:437 | RequestBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Bucketing/ModuloSimulationBucketer.cs:736 | TryDumpBlackBoxIfRequested
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Bucketing/ModuloSimulationBucketer.cs:738 | TryDumpBlackBoxIfRequested
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(BlackBoxDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Content/ContentLoreBinaryProvider.cs:146 | Open
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_fallbackStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileStreamBufferBytes);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs:1493 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs:1495 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Data/BabelDictionaryStore.cs:133 | Open
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.RandomAccess);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/BabelDictionaryStore.cs:405 | 
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
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8DataBaker.cs:981 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, CsvReadBufferBytes, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:569 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:585 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Core/Data/InventoryCost.cs:8 | InventoryCost
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Data/StaticDataStore.cs:97 | Open
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.RandomAccess);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Data/StaticDataStore.cs:121 | Open
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileStreamBufferBytes, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:44 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<MacroDatabaseTelemetryEntry> _blackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:206 | TryOpenExistingFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_fileStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:255 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:268 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `_fileStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:825 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:829 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs:1287 | 
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
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:79 | LockstepReplayBlockHeader
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 128)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1336 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1342 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1420 | LoadGhostReplay
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, ReplayBlockBytes * 4, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:109 | ThermalTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 24)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:700 | DumpBlackBoxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:702 | DumpBlackBoxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:493 | _defragBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<MemoryDefragTelemetryEntry> _defragBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:2637 | DumpDefragBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:2659 | DumpPhiVodBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs:2674 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Core/Memory/H8Memory.cs:1241 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<H8MemoryTelemetryEntry> _blackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Core/Memory/H8Memory.cs:1242 | _eventBlackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<H8MemoryTelemetryEntry> _eventBlackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/H8Memory.cs:2428 | WriteFatalLeakBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/H8Memory.cs:2451 | WriteFatalLeakBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/VaultLegacyBinaryArchaeology.cs:156 | TryReadHeader
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Memory/VaultLegacyBinaryArchaeology.cs:184 | ParseCsvOverrideStream
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Origin/AupOriginShiftCoordinator.cs:1129 | TryPollCsvOverride
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, scratch.Length, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Origin/AupOriginShiftCoordinator.cs:1277 | WriteOriginShiftDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Origin/AupOriginShiftCoordinator.cs:1279 | WriteOriginShiftDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, DumpWriteBufferBytes, FileOptions.WriteThrough))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Scheduling/BurstTokenBucketJobAdmissionService.cs:728 | DumpAdmissionBlackboxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Scheduling/BurstTokenBucketJobAdmissionService.cs:730 | DumpAdmissionBlackboxCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(AdmissionBlackboxDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Core/Scheduling/BurstTokenBucketJobAdmissionService.cs:764 | JobAdmissionBlackboxEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = BlackboxEntrySizeBytes)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:161 | TryLoadFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:333 | DumpToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:335 | DumpToDisk
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:422 | TryLoad
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/Editor/AssemblyGuard/CompileWallXRayWindow.cs:1143 | _entries
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<CompileWallBlackBoxEntry> _entries;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][68%][SIGNAL_SCRATCH_REVIEW] LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:299 | _signalDetails
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private static NativeArray<CombatDamageSignalDetail> _signalDetails;`
  Required action: This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement.
- [WARN][73%][STATIC_DECLARATION_REVIEW] LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:320 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_WITHOUT_ALLOCATION
  Evidence: `private static NativeArray<CombatTelemetryEntry> _telemetryRing;`
  Required action: This telemetry field is declared but no persistent allocation was found in the same source file. Keep it under review, but do not count it as a live ownership breach until allocation exists.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:1208 | TryDumpCombatTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:1210 | TryDumpCombatTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:1456 | DumpTelemetryBuffer
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(dumpPath));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:1457 | DumpTelemetryBuffer
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs:1378 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs:1380 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Graphics/Caustics/AnalyticalCausticsService.cs:73 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<CausticTelemetryEntry> _blackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Caustics/AnalyticalCausticsService.cs:693 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Caustics/AnalyticalCausticsService.cs:694 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:78 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<InstanceCullingTelemetryEntry> _telemetryRing;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:545 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs:547 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs:561 | TryLoadBinaryLimitFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs:587 | TryLoadTextureBudgetFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs:1014 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs:1274 | Dump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs:1276 | Dump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:1696 | DumpBlackBoxOnceLocked
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:1697 | DumpBlackBoxOnceLocked
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(Path.Combine(logDirectory, DumpFileName), FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:142 | FoveatedRenderTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = TelemetryRecordSizeBytes)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:1061 | TryOpenDumpPath
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:1063 | TryOpenDumpPath
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:15 | LightShaftTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:604 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(path));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:605 | DumpBlackbox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftSource.cs:8 | LightShaftContribution
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/ModdingAPI/Editor/ModApiSandboxTunerWindow.cs:37 | _telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ModSandboxTelemetryEntry> _telemetry;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs:165 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<MetaCampaignBlackBoxEntry> _blackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs:879 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs:881 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs:37 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<PrologueSequenceTelemetryEntry> _blackBox;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs:583 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs:586 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:770 | ReadCsvBytes
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:995 | DumpTelemetryBuffer
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:1019 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Physics/KCC/SdfSqueezeJob.cs:16 | SdfSqueezeResult
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:74 | _mockFloodQueue
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<MockFloodSignal> _mockFloodQueue;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:75 | _mockImpactQueue
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<MockImpactSignal> _mockImpactQueue;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][82%][PROBABLE_SIGNAL_CORRIDOR_BYPASS] POSSIBLE_ORPHANED_SIGNAL_QUEUE | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:76 | _cavitationQueue
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeQueue<CavitationAcousticSignal> _cavitationQueue;`
  Required action: Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:588 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:619 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:765 | TryApplyCsvOverrides
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:959 | DumpBlackBoxIfFaulted
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logRoot);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:983 | TryWriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/Plugins/Crest/HectonCrestOceanDepthCacheRuntimeBridge.cs:75 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs:1068 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs:1070 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs:1098 | RtgTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 23)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs:87 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<OrbitalTelemetryEntry> _telemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs:823 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(folder);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs:826 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [ERROR][90%][PROBABLE_NATIVE_OWNERSHIP_BREACH] LOCAL_NATIVE_TELEMETRY_RING_UNOWNED | Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:104 | _telemetry
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ReentryVfxTelemetryEntry> _telemetry;`
  Required action: Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:820 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs:63 | _blackbox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<HeadlessTelemetryEntry> _blackbox;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][56%][EDITOR_ONLY_REVIEW] EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW | Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:113 | _blackbox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<FractureTelemetryEntry> _blackbox;`
  Required action: Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1838 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1840 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:20 | CompassBlackBoxEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 40)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:35 | CompassPresentationStateDTO
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 80)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:1317 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:1319 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:1157 | TryMonitorLayoutCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(_csvFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:1557 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:1559 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/UI/VR/OpenXRManualOverrideLever.cs:70 | _blackBox
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ManualOverrideLeverTelemetryEntry> _blackBox;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/VR/OpenXRManualOverrideLever.cs:605 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/UI/VR/OpenXRManualOverrideLever.cs:607 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:1581 | LoadProfilesFromDiskOrDefaults
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, ProfileByteCount, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:2274 | EnsureCsvBackgroundWatcher
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:2358 | LoadCsvOnWorkerThread
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, scratch.Length, FileOptions.SequentialScan))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:3120 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:3122 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:1996 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:2006 | DumpBlackBoxOnce
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs:324 | ExportCurrentTuningCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs:428 | TryReadTuningCsv
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `string[] lines = File.ReadAllLines(path);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs:478 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs:484 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs:528 | TryValidateTuningBinary
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs:566 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs:581 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.Delete(tempPath);`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:566 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(logDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:568 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [WARN][68%][STATIC_LAYOUT_REVIEW] PACK1_REQUIRES_OWNER_JUSTIFICATION | Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs:50 | BiolumPointGpuData
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]`
  Required action: Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:59 | BiolumTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:1448 | DumpBiolumTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:1450 | DumpBiolumTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs:51 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<BiomeBoundaryTelemetryEntry> _telemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs:423 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs:425 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeCsvHotloader.cs:70 | TryReadFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeVaultRuntime.cs:453 | DumpBlackBoxIfFatal
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dumpDirectory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeVaultRuntime.cs:467 | WriteBlackBoxDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/World/GPR/GroundRadarJobs.cs:25 | GroundRadarTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 36)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:88 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<OutpostTelemetryEntry> _telemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:1408 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][88%][CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL] LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT | Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:81 | _telemetryRing
  Evidence kind: FIELD_DECLARATION_PLUS_SENTINEL_SCAN
  Evidence: `private NativeArray<ProceduralOreTelemetryEntry> _telemetryRing;`
  Required action: This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:1066 | DumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs:846 | RegrowthTelemetryJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `internal struct RegrowthTelemetryJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs:543 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs:554 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `File.WriteAllBytes(path, dump);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:638 | ReadColdBytes
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:897 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(Path.GetDirectoryName(_dumpPath));`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:898 | TryDumpTelemetry
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][65%][NAME_BASED_REVIEW] SIGNAL_LAYOUT_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:2570 | PoiBlackBoxValidationJob
  Evidence kind: ANCHORED_STRUCT_DECLARATION
  Evidence: `public struct PoiBlackBoxValidationJob : IJob`
  Required action: Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:589 | TryDumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/ShinobuBiomimetic/ShinobuBiomimeticArchitectureRuntime.cs:591 | TryDumpTelemetryRing
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs:642 | TryDumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(dir);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs:702 | TryLoadLookupFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][76%][IO_PRESSURE_HEURISTIC] RUNTIME_SYNC_FILE_IO_REVIEW | Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs:791 | 
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.OpenRead(path))`
  Required action: Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs:969 | TryWriteDumpFile
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = File.Create(path))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1131 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1132 | DumpBlackBox
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Gameplay/Loot/Contracts/LootMagnetContracts.cs:79 | LootMagnetSignalEvent
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
- [WARN][78%][PROBABLE_RUNTIME_NATIVE_PAYLOAD] PACK1_RUNTIME_NATIVE_REVIEW | Assets/_Project/Scripts/Gameplay/Loot/Contracts/LootMagnetContracts.cs:91 | LootMagnetTelemetryEntry
  Evidence kind: STRUCTLAYOUT_ATTRIBUTE
  Evidence: `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 128)]`
  Required action: Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64.
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
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1193 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `Directory.CreateDirectory(directory);`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1195 | WriteTelemetryDump
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [INFO][64%][COLD_OR_FATAL_IO_BOUNDARY] COLD_OR_FATAL_SYNC_IO_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1244 | CheckCsvOverrideCold
  Evidence kind: SANITIZED_LINE_REGEX
  Evidence: `using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))`
  Required action: This synchronous file I/O is in a cold, load, dump, or fatal-reporting context by name. Keep it outside Tick/dispatch hot paths and prefer background/MMF for recurring writes.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/CrashTelemetryBuffer.cs:214 | TelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct TelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][68%][EDITOR_ONLY_REVIEW] EDITOR_SIGNAL_NAME_SHADOWS_RUNTIME | Assets/_Project/Scripts/Editor/BlackBoxBinaryReader.cs:40 | TelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct TelemetryEntry`
  Required action: Editor/test structs should not shadow runtime signal names; rename smoke payloads or fully isolate them.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/HectonFluidEngine.cs:116 | OceanSurfaceTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct OceanSurfaceTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereContracts.cs:73 | OceanSurfaceTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct OceanSurfaceTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:1895 | ScanTelemetryJob
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct ScanTelemetryJob`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:1721 | ScanTelemetryJob
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct ScanTelemetryJob`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Core/InputDeterminismDtos.cs:51 | InputTelemetryEntryDTO
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct InputTelemetryEntryDTO`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Input/Determinism/DeterministicInputContracts.cs:73 | InputTelemetryEntryDTO
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct InputTelemetryEntryDTO`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Core/InputDeterminismDtos.cs:66 | MockCollisionSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockCollisionSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Input/Determinism/DeterministicInputContracts.cs:88 | MockCollisionSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockCollisionSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Core/InputDeterminismDtos.cs:75 | MockToolEquipSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockToolEquipSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Input/Determinism/DeterministicInputContracts.cs:97 | MockToolEquipSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockToolEquipSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Core/InputDeterminismDtos.cs:84 | MockPlayerKinematicsSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockPlayerKinematicsSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Input/Determinism/DeterministicInputContracts.cs:106 | MockPlayerKinematicsSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockPlayerKinematicsSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [ERROR][92%][CONFIRMED_RUNTIME_CONTRACT_COLLISION] DUPLICATE_RUNTIME_SIGNAL_NAME | Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:2379 | GlobalPanicSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct GlobalPanicSignal`
  Required action: Signal names must be globally unique across runtime contracts. Merge duplicate contracts or wrap mock/domain-local payloads behind explicit names.
- [ERROR][92%][CONFIRMED_RUNTIME_CONTRACT_COLLISION] DUPLICATE_RUNTIME_SIGNAL_NAME | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:407 | GlobalPanicSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct GlobalPanicSignal`
  Required action: Signal names must be globally unique across runtime contracts. Merge duplicate contracts or wrap mock/domain-local payloads behind explicit names.
- [ERROR][92%][CONFIRMED_RUNTIME_CONTRACT_COLLISION] DUPLICATE_RUNTIME_SIGNAL_NAME | Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:95 | MockAcousticSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockAcousticSignal`
  Required action: Signal names must be globally unique across runtime contracts. Merge duplicate contracts or wrap mock/domain-local payloads behind explicit names.
- [ERROR][92%][CONFIRMED_RUNTIME_CONTRACT_COLLISION] DUPLICATE_RUNTIME_SIGNAL_NAME | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:179 | MockAcousticSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockAcousticSignal`
  Required action: Signal names must be globally unique across runtime contracts. Merge duplicate contracts or wrap mock/domain-local payloads behind explicit names.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/VFX/VolumetricSiltContracts.cs:45 | MockAcousticSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockAcousticSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [ERROR][92%][CONFIRMED_RUNTIME_CONTRACT_COLLISION] DUPLICATE_RUNTIME_SIGNAL_NAME | Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:193 | MockDamageSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockDamageSignal`
  Required action: Signal names must be globally unique across runtime contracts. Merge duplicate contracts or wrap mock/domain-local payloads behind explicit names.
- [ERROR][92%][CONFIRMED_RUNTIME_CONTRACT_COLLISION] DUPLICATE_RUNTIME_SIGNAL_NAME | Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardTypes.cs:84 | MockDamageSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockDamageSignal`
  Required action: Signal names must be globally unique across runtime contracts. Merge duplicate contracts or wrap mock/domain-local payloads behind explicit names.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyData.cs:152 | MockPressureSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockPressureSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/DepthStressGranularSynthesisKernel.cs:87 | MockPressureSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockPressureSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Physiology/ShinobuPhysiologyData.cs:167 | MockCombatDamageSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockCombatDamageSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:389 | MockCombatDamageSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockCombatDamageSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:94 | MockCombatDamageSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockCombatDamageSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityTypes.cs:122 | MockCombatDamageSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockCombatDamageSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs:2059 | TelemetryWriteJob
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct TelemetryWriteJob`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:2155 | TelemetryWriteJob
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct TelemetryWriteJob`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/UI/DiegeticGlitchSurgeonRuntime.cs:77 | MockDepthSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockDepthSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:52 | MockDepthSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockDepthSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityTypes.cs:142 | MockDepthSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockDepthSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/World/AbyssalThermalManager.cs:111 | ThermalTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct ThermalTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:110 | ThermalTelemetryEntry
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct ThermalTelemetryEntry`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [ERROR][92%][CONFIRMED_RUNTIME_CONTRACT_COLLISION] DUPLICATE_RUNTIME_SIGNAL_NAME | Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:194 | AcousticEchoTap
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct AcousticEchoTap`
  Required action: Signal names must be globally unique across runtime contracts. Merge duplicate contracts or wrap mock/domain-local payloads behind explicit names.
- [ERROR][92%][CONFIRMED_RUNTIME_CONTRACT_COLLISION] DUPLICATE_RUNTIME_SIGNAL_NAME | Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsContracts.cs:85 | AcousticEchoTap
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct AcousticEchoTap`
  Required action: Signal names must be globally unique across runtime contracts. Merge duplicate contracts or wrap mock/domain-local payloads behind explicit names.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:39 | MockPredatorProximitySignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockPredatorProximitySignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:81 | MockPredatorProximitySignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockPredatorProximitySignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:61 | MockTensionSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockTensionSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Audio/Synthesis/DepthStressGranularSynthesisKernel.cs:103 | MockTensionSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockTensionSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Core/Contracts/DrsContracts.cs:106 | MockQualityWeightSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockQualityWeightSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.
- [WARN][74%][STATIC_CONTRACT_REVIEW] DUPLICATE_SIGNAL_LIKE_NAME_REVIEW | Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs:53 | MockQualityWeightSignal
  Evidence kind: ANCHORED_STRUCT_GROUP
  Evidence: `struct MockQualityWeightSignal`
  Required action: Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers.

## Non-Claims

- This audit does not prove Unity import, player build, IL2CPP, runtime GC, profiler, scene wiring, or actual struct sizeof(T).
- Static confidence is not semantic proof. This CLI intentionally stays outside Unity and uses standard .NET only.
- This audit reports contract debt only. It does not modify runtime contracts.
