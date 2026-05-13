# SYSTEM INTERCONNECT MATRIX â€” AbsoluteUniversePosition (AUP)
Date: 2026-05-07
Status: PENDING VERIFICATION


**Status:** PENDING VERIFICATION
**Target Struct:** `AbsoluteUniversePosition` (`Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`, lines 19â€“109)
**Blit Struct:** `AbsoluteUniversePositionBlit128` (lines 132â€“141)
**Rule Basis:** AGENTS.md Â§ [REQ] All universe math MUST use Absolute Universe Position (AUP = int64x3 grid + float3 local). Transform.position is presentation-only.
**Mandates Followed:** AGENTS.md [RULE] MANDATE CONTEXTUAL INGESTION, [RULE] ARCHITECTURE FIRST.

---

## EXECUTIVE SUMMARY

If you change a field in `AbsoluteUniversePosition` (e.g., `CellSizeMeters`, packing, or add a new coordinate lane), **28 first-party systems will break** at compile-time or silently corrupt at runtime. This matrix lists every system, the nature of the dependency, and the failure mode.

**Structural risk:** `AbsoluteUniversePosition` is a `Pack = 1, Size = 36` struct. Changing its layout invalidates `PersistentWorldItemRecord` (pack 1, size 192) and `EntityDataRecord` (pack 1, size 64), which are blitted directly to `NativeArray` save buffers. **Save data corruption is guaranteed** if layout drifts.

---

## DEPENDENCY MAP

### TIER S â€” SAVE / SERIALIZATION (Layout-Critical)

| # | System | File | Dependency Type | Failure Mode if AUP Changes |
|---|--------|------|-----------------|---------------------------|
| 1 | **PersistentWorldRegistry** | `PersistentWorldRegistry.cs` | **DEFINES** `AbsoluteUniversePosition` and `AbsoluteUniversePositionBlit128`. Owns `EntityDataRecord`, `PersistentWorldItemRecord`, `PoolSlotData`. | **CATASTROPHIC.** Any size/pack change breaks binary save format. `ToAlignedBlit()` / `FromAlignedBlit()` assume exact byte offsets. |
| 2 | **SaveBinaryStorage** | `SaveBinaryStorage.cs` | Reads/writes `AbsoluteUniversePosition PlayerPosition` in save header. Uses `ToAup()` / `ToRuntimePosition()`. | Save header size mismatch â†’ checksum failure â†’ fallback to `.bak` on every load. |
| 3 | **PersistentWorldDeltaRecord** | `PersistentWorldRegistry.cs` | `UnpackPosition()` reconstructs AUP from packed uint. `PackLocalPosition()` quantizes AUP into 10-bit axes. | Packing math assumes `chunkSizeMeters` and AUP local range. Change = items teleport on load. |

### TIER A â€” WORLD / SPATIAL (Coordinate-Critical)

| # | System | File | Dependency Type | Failure Mode if AUP Changes |
|---|--------|------|-----------------|---------------------------|
| 4 | **HectonFloatingOrigin** | `HectonFloatingOrigin.cs` | `ToAbsoluteUniversePosition()` and `ToRuntimePosition()` are the **canonical converters**. All AUP math starts/ends here. | **CATASTROPHIC.** Every system using AUP will compute wrong coordinates. Floating-origin shifts will misalign world by `delta(offset)`. |
| 5 | **HectonWorldGenerator** | `HectonWorldGenerator.cs` | `ResolveAbsoluteChunkCoord()` calls `AbsoluteUniversePosition.FromRuntimePosition()`. | Chunk coordinates misalign â†’ terrain seams â†’ duplicate or missing chunks. |
| 6 | **HectonSpatialHash** | `HectonSpatialHash.cs` | `Register()`, `UpdateEntry()`, `CollectSphere()` take `in AbsoluteUniversePosition`. | Spatial hash cells map to wrong bins â†’ queries return distant objects or miss nearby ones. |
| 7 | **WorldSpatialHashGrid** | `WorldSpatialHashGrid.cs` | Wraps `HectonSpatialHash`; converts transforms to AUP for registration. | Same as #6; additionally, origin-shift update loop re-inserts stale positions. |
| 8 | **FaunaSpatialHashRegistry** | `FaunaSpatialHashRegistry.cs` | Fauna-specific spatial hash wrapper using AUP. | Fauna despawn/respawn logic breaks; creatures spawn inside player or at world origin. |
| 9 | **MapMagicBridge** | `MapMagicBridge.cs` | `TryGetHeightAUP()` / `SampleHeightAUP()` accept `Vector3 absoluteUniversePosition`. | Terrain height queries return wrong altitude â†’ objects float or bury themselves. |
| 10 | **ProceduralWreckGenerator** | `ProceduralWreckGenerator.cs` | `ComputeGenerationSeed()` hashes AUP grid coordinates. `GenerateForSection()` uses AUP for seed. | Same AUP produces different seed â†’ wreck layout changes across origin shifts â†’ non-deterministic. |
| 11 | **WorldProceduralScatterDirectorSpatialHelpers** | `WorldProceduralScatterDirectorSpatialHelpers.cs` | `ToAbsoluteScatterPosition()` calls `HectonFloatingOrigin.ToAbsoluteUniversePosition()`. | Scatter objects shift on origin teleport â†’ visible pop-in/pop-out. |
| 12 | **WorldGenerativeGeologyIntegrationDirector** | `WorldGenerativeGeologyIntegrationDirector.cs` | Stores `absoluteUniversePosition` in `WorldGeologySeamPlan`. Computes chunk/macro-zone from AUP. | Geology seams misalign â†’ terrain holes at macro-zone boundaries. |
| 13 | **WorldGenerativeGeologySeamPlan** | `WorldGenerativeGeologySeamPlan.cs` | `RuntimeWorldPosition` converts stored AUP back to runtime via `HectonFloatingOrigin.ToRuntimePosition()`. | Seam plans render at wrong world positions â†’ floating rocks or buried structures. |

### TIER B â€” ENTITY / GAMEPLAY (Position-Critical)

| # | System | File | Dependency Type | Failure Mode if AUP Changes |
|---|--------|------|-----------------|---------------------------|
| 14 | **FaunaDirector** | `FaunaDirector.cs` | `FromRuntimePosition()` for player/creature AUP. `DistanceSq()` for culling. `WritePoolSlotPosition()` / `ReadPoolSlotPosition()` for hydration. | Creatures hydrate at wrong positions â†’ inside geometry or 5000m away. Culling radius math breaks. |
| 15 | **HectonVoxelVolume** | `HectonVoxelVolume.cs` | `_generationAbsoluteUniversePosition` anchors voxel cave volume. | Cave generation shifts relative to terrain â†’ mismatched tunnel entrances. |
| 16 | **HectonVoxelEngine** | `HectonVoxelEngine.cs` | `worldCenter = ToRuntimePosition(volume.GenerationAbsoluteUniversePosition, committedTotalOffset)` on origin shift. | Voxel chunks drift after shift â†’ caves disconnect from surface. |
| 17 | **HectonVoxelStreamingBridge** | `HectonVoxelStreamingBridge.cs` | `BuildHoleKey()` and `BuildHoleSeed()` hash AUP. | Hole keys collide or diverge â†’ terrain holes appear/disappear on reload. |
| 18 | **VoxelDeltaProcessor** | `VoxelDeltaProcessor.cs` | `ToAbsoluteUniversePosition()` for carve/crater hit points. | Carving applies to wrong absolute coordinates â†’ holes appear far from impact. |
| 19 | **HazardZoneManager** | `HazardZoneManager.cs` | `HazardVolumeData.AbsoluteUniversePosition` for threat evaluation. | Hazard zones evaluate wrong distance â†’ player takes damage in safe areas or is immune in danger. |
| 20 | **SubmarineFluidDynamics** | `SubmarineFluidDynamics.cs` | `AbsoluteUniversePosition` field in splash struct for persistent VFX anchoring. | Splash VFX spawn at wrong position â†’ particles appear at world origin. |
| 21 | **CrashTelemetryBuffer** | `CrashTelemetryBuffer.cs` | `ToAbsoluteUniversePosition()` for player position in crash dumps. | Crash reports show wrong coordinates â†’ debugging impossible. |
| 22 | **PlayerCriticalProceduralAudioRenderer** | `PlayerCriticalProceduralAudioRenderer.cs` | `ToAbsoluteUniversePosition()` for depth calculation. | Depth-based audio filters use wrong value â†’ submarine sounds incorrect. |
| 23 | **SonarHoloCompass** | `SonarHoloCompass.cs` | `ToAbsoluteUniversePosition()` for listener and emitter positions. | Sonar blips render at wrong compass bearings â†’ navigation failure. |
| 24 | **OriginShiftEventData** | `OriginShiftEventData.cs` | `ToRuntimePosition(Vector3 absoluteUniversePosition)` converts AUP after shift. | All AUPâ†’runtime conversions post-shift are wrong â†’ every shifted object teleports. |

### TIER C â€” EDITOR / DEBUG (Non-Runtime)

| # | System | File | Dependency Type | Failure Mode if AUP Changes |
|---|--------|------|-----------------|---------------------------|
| 25 | **KinematicGhostDebugger** | `Assets/_Project/Scripts/Editor/KinematicGhostDebugger.cs` | `ToAbsoluteUniversePosition()` for ghost trail history. | Editor debug visualization shows wrong trail â†’ misleading physics debug. |
| 26 | **HectonCrestOceanDepthCacheBootstrap** | `HectonCrestOceanDepthCacheBootstrap.cs` | `ResolveAbsoluteUniversePoint()` for ocean depth sampling. | Depth cache samples wrong world points â†’ underwater fog/visibility incorrect. |
| 27 | **SargassumGlobalDragManager** | `SargassumGlobalDragManager.cs` | `FromRuntimePosition()` for external scavenger site AUP. | External sites quantize to wrong chunks â†’ POI markers drift. |

### TIER D â€” SHADER / GPU (Indirect)

| # | System | File | Dependency Type | Failure Mode if AUP Changes |
|---|--------|------|-----------------|---------------------------|
| 28 | **Vegetation Shaders** | `Hecton8/Vegetation/Indirect*.shader` | Previously used `_HectonPlayerAbsoluteUniversePosition` (now `_HectonPlayerRuntimePosition` per 2026-04-28 diff). | **NOTE:** Shader globals were decoupled from AUP in the 2026-04-28 GPU patch. If AUP is reintroduced to shaders, this reopens the dependency. |

---

## AUP SURGERY PROTOCOL

### Before Changing `AbsoluteUniversePosition`:

1. **Bump `SaveDataVersion`** in `SaveBinaryStorage.cs`.
2. **Write migration path** in `SaveDataMigration.cs` for old AUP layout â†’ new layout.
3. **Update `CellSizeMeters`** â†’ re-bake all terrain (MapMagic chunk size must match).
4. **Update `PackLocalPosition` bit masks** â†’ verify `PersistentWorldDeltaRecord` still fits in 32 bits.
5. **Re-run `PersistentWorldRegistry` unit tests** (if any exist; if not, write them first).
6. **Verify shader globals** â€” ensure no shader reads AUP directly.
7. **MCP validation:** Load save â†’ verify player position â†’ verify creature hydration â†’ verify spatial hash query accuracy.

### What Does NOT Break (Presentation-Only):

- `Transform.position` reads in camera controllers
- UI overlay positions (`SuitHUDV4CanvasOverlay`)
- Local animation rigs
- Particle system emitters (unless they use AUP for world-space anchoring)

---

## REGRESSION MODEL

| Change Type | CPU Impact | GC Impact | Memory Impact | Correctness Risk | Why Kept/Rejected |
|-------------|------------|-----------|---------------|------------------|-------------------|
| Add `float W` to AUP | +4 bytes Ã— 16k records = +64 KB | 0 B | +64 KB persistent | **HIGH** â€” breaks all blit layouts | Rejected unless save version bumped + migration written |
| Change `CellSizeMeters` to 4096 | 0 B | 0 B | 0 B | **HIGH** â€” terrain chunk misalignment | Rejected without full world re-bake |
| Replace `long` grid with `int` | âˆ’12 bytes Ã— 16k = âˆ’192 KB | 0 B | âˆ’192 KB | **HIGH** â€” overflow at 2.1B meters | Rejected â€” universe must be > 2.1B meters |
| Add `ToRuntimeFloat3(float3 explicitOffset)` overload | 0 B | 0 B | 0 B | **LOW** â€” additive API | Accepted â€” no layout change |

---

*STATUS: PENDING VERIFICATION*
*Action: AUP Surgery requires approval of SaveDataVersion bump + migration path.*
