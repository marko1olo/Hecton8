# SYSTEM INTERCONNECT MATRIX — AbsoluteUniversePosition (AUP)

**Status:** PENDING VERIFICATION  
**Target Struct:** `AbsoluteUniversePosition` (`Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`, lines 19–109)  
**Blit Struct:** `AbsoluteUniversePositionBlit128` (lines 132–141)  
**Rule Basis:** AGENTS.md § [REQ] All universe math MUST use Absolute Universe Position (AUP = int64x3 grid + float3 local). Transform.position is presentation-only.  
**Mandates Followed:** AGENTS.md [RULE] MANDATE CONTEXTUAL INGESTION, [RULE] ARCHITECTURE FIRST.

---

## EXECUTIVE SUMMARY

If you change a field in `AbsoluteUniversePosition` (e.g., `CellSizeMeters`, packing, or add a new coordinate lane), **28 first-party systems will break** at compile-time or silently corrupt at runtime. This matrix lists every system, the nature of the dependency, and the failure mode.

**Structural risk:** `AbsoluteUniversePosition` is a `Pack = 1, Size = 36` struct. Changing its layout invalidates `PersistentWorldItemRecord` (pack 1, size 192) and `EntityDataRecord` (pack 1, size 64), which are blitted directly to `NativeArray` save buffers. **Save data corruption is guaranteed** if layout drifts.

---

## DEPENDENCY MAP

### TIER S — SAVE / SERIALIZATION (Layout-Critical)

| # | System | File | Dependency Type | Failure Mode if AUP Changes |
|---|--------|------|-----------------|---------------------------|
| 1 | **PersistentWorldRegistry** | `PersistentWorldRegistry.cs` | **DEFINES** `AbsoluteUniversePosition` and `AbsoluteUniversePositionBlit128`. Owns `EntityDataRecord`, `PersistentWorldItemRecord`, `PoolSlotData`. | **CATASTROPHIC.** Any size/pack change breaks binary save format. `ToAlignedBlit()` / `FromAlignedBlit()` assume exact byte offsets. |
| 2 | **SaveBinaryStorage** | `SaveBinaryStorage.cs` | Reads/writes `AbsoluteUniversePosition PlayerPosition` in save header. Uses `ToAup()` / `ToRuntimePosition()`. | Save header size mismatch → checksum failure → fallback to `.bak` on every load. |
| 3 | **PersistentWorldDeltaRecord** | `PersistentWorldRegistry.cs` | `UnpackPosition()` reconstructs AUP from packed uint. `PackLocalPosition()` quantizes AUP into 10-bit axes. | Packing math assumes `chunkSizeMeters` and AUP local range. Change = items teleport on load. |

### TIER A — WORLD / SPATIAL (Coordinate-Critical)

| # | System | File | Dependency Type | Failure Mode if AUP Changes |
|---|--------|------|-----------------|---------------------------|
| 4 | **HectonFloatingOrigin** | `HectonFloatingOrigin.cs` | `ToAbsoluteUniversePosition()` and `ToRuntimePosition()` are the **canonical converters**. All AUP math starts/ends here. | **CATASTROPHIC.** Every system using AUP will compute wrong coordinates. Floating-origin shifts will misalign world by `delta(offset)`. |
| 5 | **HectonWorldGenerator** | `HectonWorldGenerator.cs` | `ResolveAbsoluteChunkCoord()` calls `AbsoluteUniversePosition.FromRuntimePosition()`. | Chunk coordinates misalign → terrain seams → duplicate or missing chunks. |
| 6 | **HectonSpatialHash** | `HectonSpatialHash.cs` | `Register()`, `UpdateEntry()`, `CollectSphere()` take `in AbsoluteUniversePosition`. | Spatial hash cells map to wrong bins → queries return distant objects or miss nearby ones. |
| 7 | **WorldSpatialHashGrid** | `WorldSpatialHashGrid.cs` | Wraps `HectonSpatialHash`; converts transforms to AUP for registration. | Same as #6; additionally, origin-shift update loop re-inserts stale positions. |
| 8 | **FaunaSpatialHashRegistry** | `FaunaSpatialHashRegistry.cs` | Fauna-specific spatial hash wrapper using AUP. | Fauna despawn/respawn logic breaks; creatures spawn inside player or at world origin. |
| 9 | **MapMagicBridge** | `MapMagicBridge.cs` | `TryGetHeightAUP()` / `SampleHeightAUP()` accept `Vector3 absoluteUniversePosition`. | Terrain height queries return wrong altitude → objects float or bury themselves. |
| 10 | **ProceduralWreckGenerator** | `ProceduralWreckGenerator.cs` | `ComputeGenerationSeed()` hashes AUP grid coordinates. `GenerateForSection()` uses AUP for seed. | Same AUP produces different seed → wreck layout changes across origin shifts → non-deterministic. |
| 11 | **WorldProceduralScatterDirectorSpatialHelpers** | `WorldProceduralScatterDirectorSpatialHelpers.cs` | `ToAbsoluteScatterPosition()` calls `HectonFloatingOrigin.ToAbsoluteUniversePosition()`. | Scatter objects shift on origin teleport → visible pop-in/pop-out. |
| 12 | **WorldGenerativeGeologyIntegrationDirector** | `WorldGenerativeGeologyIntegrationDirector.cs` | Stores `absoluteUniversePosition` in `WorldGeologySeamPlan`. Computes chunk/macro-zone from AUP. | Geology seams misalign → terrain holes at macro-zone boundaries. |
| 13 | **WorldGenerativeGeologySeamPlan** | `WorldGenerativeGeologySeamPlan.cs` | `RuntimeWorldPosition` converts stored AUP back to runtime via `HectonFloatingOrigin.ToRuntimePosition()`. | Seam plans render at wrong world positions → floating rocks or buried structures. |

### TIER B — ENTITY / GAMEPLAY (Position-Critical)

| # | System | File | Dependency Type | Failure Mode if AUP Changes |
|---|--------|------|-----------------|---------------------------|
| 14 | **FaunaDirector** | `FaunaDirector.cs` | `FromRuntimePosition()` for player/creature AUP. `DistanceSq()` for culling. `WritePoolSlotPosition()` / `ReadPoolSlotPosition()` for hydration. | Creatures hydrate at wrong positions → inside geometry or 5000m away. Culling radius math breaks. |
| 15 | **HectonVoxelVolume** | `HectonVoxelVolume.cs` | `_generationAbsoluteUniversePosition` anchors voxel cave volume. | Cave generation shifts relative to terrain → mismatched tunnel entrances. |
| 16 | **HectonVoxelEngine** | `HectonVoxelEngine.cs` | `worldCenter = ToRuntimePosition(volume.GenerationAbsoluteUniversePosition, committedTotalOffset)` on origin shift. | Voxel chunks drift after shift → caves disconnect from surface. |
| 17 | **HectonVoxelStreamingBridge** | `HectonVoxelStreamingBridge.cs` | `BuildHoleKey()` and `BuildHoleSeed()` hash AUP. | Hole keys collide or diverge → terrain holes appear/disappear on reload. |
| 18 | **VoxelDeltaProcessor** | `VoxelDeltaProcessor.cs` | `ToAbsoluteUniversePosition()` for carve/crater hit points. | Carving applies to wrong absolute coordinates → holes appear far from impact. |
| 19 | **HazardZoneManager** | `HazardZoneManager.cs` | `HazardVolumeData.AbsoluteUniversePosition` for threat evaluation. | Hazard zones evaluate wrong distance → player takes damage in safe areas or is immune in danger. |
| 20 | **SubmarineFluidDynamics** | `SubmarineFluidDynamics.cs` | `AbsoluteUniversePosition` field in splash struct for persistent VFX anchoring. | Splash VFX spawn at wrong position → particles appear at world origin. |
| 21 | **CrashTelemetryBuffer** | `CrashTelemetryBuffer.cs` | `ToAbsoluteUniversePosition()` for player position in crash dumps. | Crash reports show wrong coordinates → debugging impossible. |
| 22 | **PlayerCriticalProceduralAudioRenderer** | `PlayerCriticalProceduralAudioRenderer.cs` | `ToAbsoluteUniversePosition()` for depth calculation. | Depth-based audio filters use wrong value → submarine sounds incorrect. |
| 23 | **SonarHoloCompass** | `SonarHoloCompass.cs` | `ToAbsoluteUniversePosition()` for listener and emitter positions. | Sonar blips render at wrong compass bearings → navigation failure. |
| 24 | **OriginShiftEventData** | `OriginShiftEventData.cs` | `ToRuntimePosition(Vector3 absoluteUniversePosition)` converts AUP after shift. | All AUP→runtime conversions post-shift are wrong → every shifted object teleports. |

### TIER C — EDITOR / DEBUG (Non-Runtime)

| # | System | File | Dependency Type | Failure Mode if AUP Changes |
|---|--------|------|-----------------|---------------------------|
| 25 | **KinematicGhostDebugger** | `Editor/KinematicGhostDebugger.cs` | `ToAbsoluteUniversePosition()` for ghost trail history. | Editor debug visualization shows wrong trail → misleading physics debug. |
| 26 | **HectonCrestOceanDepthCacheBootstrap** | `HectonCrestOceanDepthCacheBootstrap.cs` | `ResolveAbsoluteUniversePoint()` for ocean depth sampling. | Depth cache samples wrong world points → underwater fog/visibility incorrect. |
| 27 | **SargassumGlobalDragManager** | `SargassumGlobalDragManager.cs` | `FromRuntimePosition()` for external scavenger site AUP. | External sites quantize to wrong chunks → POI markers drift. |

### TIER D — SHADER / GPU (Indirect)

| # | System | File | Dependency Type | Failure Mode if AUP Changes |
|---|--------|------|-----------------|---------------------------|
| 28 | **Vegetation Shaders** | `Hecton8/Vegetation/Indirect*.shader` | Previously used `_HectonPlayerAbsoluteUniversePosition` (now `_HectonPlayerRuntimePosition` per 2026-04-28 diff). | **NOTE:** Shader globals were decoupled from AUP in the 2026-04-28 GPU patch. If AUP is reintroduced to shaders, this reopens the dependency. |

---

## AUP SURGERY PROTOCOL

### Before Changing `AbsoluteUniversePosition`:

1. **Bump `SaveDataVersion`** in `SaveBinaryStorage.cs`.
2. **Write migration path** in `SaveDataMigration.cs` for old AUP layout → new layout.
3. **Update `CellSizeMeters`** → re-bake all terrain (MapMagic chunk size must match).
4. **Update `PackLocalPosition` bit masks** → verify `PersistentWorldDeltaRecord` still fits in 32 bits.
5. **Re-run `PersistentWorldRegistry` unit tests** (if any exist; if not, write them first).
6. **Verify shader globals** — ensure no shader reads AUP directly.
7. **MCP validation:** Load save → verify player position → verify creature hydration → verify spatial hash query accuracy.

### What Does NOT Break (Presentation-Only):

- `Transform.position` reads in camera controllers
- UI overlay positions (`SuitHUDV4CanvasOverlay`)
- Local animation rigs
- Particle system emitters (unless they use AUP for world-space anchoring)

---

## REGRESSION MODEL

| Change Type | CPU Impact | GC Impact | Memory Impact | Correctness Risk | Why Kept/Rejected |
|-------------|------------|-----------|---------------|------------------|-------------------|
| Add `float W` to AUP | +4 bytes × 16k records = +64 KB | 0 B | +64 KB persistent | **HIGH** — breaks all blit layouts | Rejected unless save version bumped + migration written |
| Change `CellSizeMeters` to 4096 | 0 B | 0 B | 0 B | **HIGH** — terrain chunk misalignment | Rejected without full world re-bake |
| Replace `long` grid with `int` | −12 bytes × 16k = −192 KB | 0 B | −192 KB | **HIGH** — overflow at 2.1B meters | Rejected — universe must be > 2.1B meters |
| Add `ToRuntimeFloat3(float3 explicitOffset)` overload | 0 B | 0 B | 0 B | **LOW** — additive API | Accepted — no layout change |

---

*STATUS: PENDING VERIFICATION*  
*Action: AUP Surgery requires approval of SaveDataVersion bump + migration path.*
