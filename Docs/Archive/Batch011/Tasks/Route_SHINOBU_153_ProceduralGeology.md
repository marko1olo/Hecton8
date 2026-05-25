# Route SHINOBU_153 Procedural Geology

Date: 2026-05-20
Owner: SHINOBU_153
Status: PENDING VERIFICATION

## Route

Producer phase: `SlowTick` schedules deterministic generation when AUP sector changes.
Mutation phase: `ProceduralOreSpawner` keeps only generation-checked `VaultBufferHandle<T>` fields persistently; routine `EnsureNativeState()` checks handle metadata only, transient Vault views are resolved at mutation/job boundaries, then Burst jobs write buffers `71530..71550`. Frame helpers resolve only the individual handle they consume. MapMagic payload lookup uses runtime coordinates derived from double AUP through `HectonFloatingOrigin`; the resulting `QuantizedHeightmapPayload` is a local refresh-to-schedule handoff, not retained as manager state. The Burst sampler uses a double absolute terrain origin for heightmap UVs.
Visual phase: `LateFrameTick` retires completed jobs, validates every uploaded matrix row, resolves draw bounds from active `ResourceMatrices` rows including visual-only crystals with shader-matched local extents, uploads matrices/args through `GraphicsBuffer.LockBufferForWrite`, and submits `Graphics.DrawProceduralIndirect`.
Occlusion phase: optional HZB readback producer fills `71549` `GeologyHzbTileDTO[4096]` and `71550` `GeologyHzbMetaDTO[1]`. The geology job reads these buffers without a renderer sibling dependency and drops hidden visual-only matrices before GPU upload.

## Ownership

Owner-local truth is `ResourceNodeDTO`, candidate-slot masks, depletion cache rows, telemetry, tuning, procedural draw args, and optional HZB readback mirrors in the World Resource Spawner lane.
External consumers read through `IWorldResourceSpawnerReadModel` or command through `IWorldResourceSpawnerCommandModel`.
No manager-owned `NativeArray<T>` or `QuantizedHeightmapPayload` aliases are retained between calls; Vault handles are the persistent route.

## Failure Mode

Invalid layout, missing Vault, non-finite ore positions, non-finite uploaded matrices, or generation fault disables unsafe output and writes the 300-frame telemetry ring to `Docs/AgentLogs/Dump_SHINOBU_153.bin` and XML compatibility alias `Docs/AgentLogs/Dump_GEOLOGY_ARCHITECT.bin`.

## Proof Boundary

Static source wiring exists. Unity import, Burst compile, Frame Debugger, profiler, GCMonitor, Play Mode, and player-build proof remain pending.
