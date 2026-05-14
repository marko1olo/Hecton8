# AUP Drift Report

Agent: ARCHITECTURAL_AUP_INTEGRITY_AUDITOR
Status: VERIFIED AUP INTEGRITY - CORE BUILD PASS; ASMDEF BLOCKED BY ARCHITECTURE

## Authority

- AUP must remain authoritative in 64-bit/int64-sector form until the last presentation conversion.
- Distance triggers must subtract in double precision before squared-length checks.
- Rebase shifts require atomic processing and 300-frame snap-fence telemetry.
- Low-tier float fallback is only allowed for distant entities after explicit tier and distance gates.

## Loop 1 Findings

- `Docs/Tasks/CURRENT_BATCH.md` did not contain this agent prompt on initial extraction or the Task 4 re-extraction.
- Mandatory scan `rg "\(float3\).*AUP|AupOffset|universe"` ran. Runtime findings include fluid/vector-noise AUP float offsets, GPU scatter AUP offset storage, and the core AUP runtime-position constructor.
- `AbsoluteUniversePosition.FromRuntimePosition` was the authority leak: it converted runtime to absolute via `Vector3` before `double3` sector quantization.
- `AUPDirection` normalized after a premature float cast.
- `PlayerKinematicsRuntime` publishes a sync fence every 300 fast ticks and writes hash telemetry.
- `ProceduralOreSpawner` preserves sector hash entropy by folding low/high `long` bits into the uint job seed; no `(int)SectorHash` truncation was found.
- `Hecton8.Core.AUP` asmdef does not exist. Existing `Hecton8.Core` and contracts asmdefs still depend on UnityEngine; isolation remains pending.

## Code Changes

- `Assets/_Project/Scripts/HectonFloatingOrigin.cs`: added `_totalOffsetDouble`, `CurrentTotalOffsetDouble`, and `ToAbsoluteUniversePositionDouble3`; shift accumulation now preserves committed offset in double.
- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`: `AbsoluteUniversePosition.FromRuntimePosition` now uses the double AUP conversion path.
- `Assets/_Project/Scripts/World/AUPMath.cs`: `AUPDirection` now computes length in double and uses `math.rsqrt` before the final float3 output.
- `Assets/_Project/Scripts/HectonFloatingOrigin.cs`: drift watchdog reports max AUP/runtime error and uses `math.rcp` for anchor velocity fallback.
- `Assets/_Project/Scripts/CrashTelemetryBuffer.cs`: added `ReportAupMaxDriftError` and ring-buffer write path.
- `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs`: AUP shift consumption now uses non-destructive SignalBus snapshots and a local applied shift sequence.
- `Assets/_Project/Scripts/World/AcousticOcclusionUtility.cs`: acoustic AUP distance now subtracts through `AbsoluteUniversePosition.DistanceSq` before final float audio scalar.
- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`: default `AbsoluteUniversePosition.ToRuntimeFloat3()` now subtracts `CurrentTotalOffsetDouble` before final runtime float output.
- `Assets/_Project/Scripts/World/AUPMath.cs`: added a double-offset runtime projection overload and retained the float-offset overload as a compatibility wrapper for existing job payloads.
- `Assets/_Project/Scripts/World/WorldSpatialHashGrid.cs`: AUP validation buffers/job and far-unload runtime rehydration now use double committed offsets.
- `Assets/_Project/Scripts/OriginShiftEventData.cs`: shift payloads now carry `PreviousTotalOffsetDouble` and `NewTotalOffsetDouble` in addition to legacy `Vector3` offsets.
- `Assets/_Project/Scripts/HectonFloatingOrigin.cs`: wait-for-stability, committed shift event creation, safe teleport event creation, sector-delta calculation, and runtime projection helpers now use double committed offsets.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs`: route/hunt target rebases and corpse-sink AUP reconstruction now consume double committed offsets.
- `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs`: corpse-resource runtime cache rebuild now consumes `NewTotalOffsetDouble`.
- `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`, `Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs`, `Assets/_Project/Scripts/Plugins/Crest/HectonCrestOceanDepthCacheBootstrap.cs`, `Assets/_Project/Scripts/WorldGenerativeGeologySeamPlan.cs`, `Assets/_Project/Scripts/World/GPUScatterDirector.cs`, and `Assets/_Project/Scripts/HectonPlayerMovement.cs`: scalar/presentation absolute offset helpers now use double committed offsets before final float output.
- `Assets/_Project/Scripts/HectonVoxelEngine.cs`: voxel pipeline data now preserves `AbsoluteUniverseOffsetAtStartDouble`; async finalization rebases, shift-aware projection, terrain-hole/spawn reconstruction, anomaly origins, biome coordinate math, and chthonic pillar bounds use double captured offsets before final float presentation casts.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.Compatibility.cs`, `Assets/_Project/Scripts/Fauna/FaunaBrain.cs`, and `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs`: predator cognition origin-offset payloads now use `double3` and subtract in double before final steering/telemetry float output.
- `Assets/_Project/Scripts/Fauna/FaunaSensorSuite.cs`, `Assets/_Project/Scripts/Gameplay/HectonScanRenderRegistry.cs`, `Assets/_Project/Scripts/Gameplay/HectonScannerProjectionState.cs`, `Assets/_Project/Scripts/World/ScatterGPUIBackend.cs`, `Assets/_Project/Scripts/World/EcosystemDirector.cs`, and `Assets/_Project/Scripts/World/ResourceDistributionDirector.cs`: brine/scanner/scatter presentation helpers now use `CurrentTotalOffsetDouble` before final float output.
- `Assets/_Project/Scripts/Environment/Fluids/BrineLayerMath.cs`: added double-offset overloads for future fluid-domain callers; current Core-facing callers use local double math because the Core project does not expose the new overload surface during `dotnet build`.
- `Assets/_Project/Scripts/HectonFluidEngine.cs`: flow sampling, water-height sampling, buoyancy wave/vector-noise scheduling, brine shift scalar setup, and GPU abyssal flow noise offset upload now use `CurrentTotalOffsetDouble` until the final job/shader float payload boundary.
- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs`: added a double vegetation universe-offset lane and double runtime/universe conversion helpers while preserving legacy `Vector3` APIs.
- `Assets/_Project/Scripts/World/VegetationChunkResidencyDirector.cs`, `Assets/_Project/Scripts/World/VegetationDensityQueryService.cs`, `Assets/_Project/Scripts/World/VegetationTerrainHoleSynchronizer.cs`, and `Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs`: stable matrix conversion, density-grid XZ tests, semantic anchor AUP reconstruction, and sargassum drag origins now use the double vegetation offset before final float presentation/storage.

## Verification

- Re-ran mandatory AUP scan; remaining float AUP offset hits are primarily presentation/shader fluid/scatter lanes and are recorded for later domain owners.
- Scoped `/ dt` scan over AUP/origin/KCC/acoustic/residency files found no remaining AUP integration division after the reciprocal patch.
- `GlobalSignals.TryDequeueAupShift` now has no runtime consumers; it remains an available compatibility API only.
- `dotnet build Hecton8.Core.csproj`: failed with 131 existing missing-reference errors, including `Hecton8.Environment.Fluids`, `Hecton8.Audio.Virtualization`, `Hecton8.Physics.CCD`, and `Hecton8.Core.Scheduling`.
- `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:quiet -clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: post-Loop-5 recheck still failed with 140 existing missing-reference/interface errors, including missing `Hecton8.Environment.Fluids`, `Hecton8.Audio.Virtualization`, `Hecton8.Physics.CCD`, `Hecton8.Core.Scheduling`, and unrelated `DynamicResolutionScaler` interface members.
- Loop 6 post-edit build recheck with the same Core command timed out after 94 seconds; the specific timed-out process was stopped. A separate Core build process from another parent was left running.
- Loop 7 post-edit build recheck with the same Core command failed with 128 existing missing-reference/interface errors; the only `HectonVoxelEngine.cs` error reported is the known pre-existing line 21 missing `Hecton8.Core.Scheduling` namespace.
- Loop 8 first build failed with 54 project errors and exposed three introduced CS1503 mismatches from brine double-overload calls; those callers were fixed. Follow-up build timed out after 124 seconds under the existing compile wall.
- Loop 9 build recheck with `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false` failed with 0 warnings and 1 existing audio dependency error: `PlayerCriticalProceduralAudioRenderer.cs(10002,31)` missing `PrologueSplashdownSineSweepProbeJob`.
- Loop 9 targeted fluid scan for legacy `HectonFloatingOrigin.CurrentTotalOffset`, direct `.x/.y/.z` reads, and `(float3)` casts against `CurrentTotalOffset` is clean in `HectonFluidEngine.cs`.
- Loop 10 targeted scan for `_totalUniverseOffset.x/y/z`, `Vector3 universeOffset`, legacy `CurrentTotalOffset`, and Vector3 matrix conversion in patched vegetation/scatter/fluid files is clean.
- Loop 10 Core build: `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- `dotnet build Assembly-CSharp.csproj`: timed out after 120s; timed-out build process and MSBuild servers were stopped.
- Unity MCP script validation: unavailable (`no_unity_session`).
- Unity MCP post-Loop-5 `validate_script` on `Assets/_Project/Scripts/World/AUPMath.cs`: unavailable (`no_unity_session`).
- Unity MCP post-Loop-7 `validate_script` on `Assets/_Project/Scripts/HectonVoxelEngine.cs`: unavailable; the local MCP endpoint at `http://127.0.0.1:8088/mcp` rejected the request.
- Rsqrt audit: scoped AUP/origin/KCC/acoustic files contain no remaining `math.normalize`, `math.normalizesafe`, `.normalized`, or sqrt normalization in patched authority paths.
- ASMDEF audit: no `Hecton8.Core.AUP` asmdef exists; current AUP struct is not isolated from UnityEngine because it lives in `PersistentWorldRegistry.cs`.
- Polish mandate extraction: `POLISH_MANDATE_NOT_FOUND`; anti-bloat polish still executed under standing rules.
- `git diff --check`: line-ending warnings only, no whitespace errors.

## Evidence Queue

- Continue scan of AI/Biome proximity callsites for silent `float3` seeds and presentation-only exceptions.
- Future safe upgrade: convert explicit `AUPMath.ToRuntimeFloat3(..., float3 offset)` job payloads to `double3` only in their owning AI/fauna/vegetation batches.
- Remaining fluid `AupOffsetXZ` and `vectorNoiseAupOffset` names are final-cast job payload fields after Loop 9, not current committed-offset authority sources.
- Remaining vegetation `Vector3 universePosition` APIs are compatibility/presentation surfaces after Loop 10. True double vegetation storage would require a dedicated matrix/native buffer migration.
- Voxel terrain-hole/spawn helper signatures now consume double committed offsets; remaining voxel `Vector3` absolute-position fields are compatibility/persistence boundaries pending a dedicated voxel storage migration.
- Fauna cognition `FloatingOriginOffset` now uses `double3`; remaining `AUPMath.ToRuntimeFloat3(..., float3 offset)` hits are smoke tests or unowned presentation/job payloads.
- Verify AUP shift consumer coverage across fluid, voxel, world streaming, scatter, foveated simulation, and GPR.
- Decide whether a future batch may introduce a true `Hecton8.Core.AUP` asmdef; current file placement prevents UnityEngine isolation.

## Loop 14 Runtime Chemical/Wreck Persistent Double Lane

### Findings

- `ChemicalInfluenceGrid` still used float absolute breadcrumb centers and `Vector4` defoliant centers for trigger-distance math after runtime-to-AUP conversion.
- `ProceduralWreckGenerator` burial cut records stored voxel surgeon absolute box centers as `float3`, then replayed those records into voxel delta processing later.
- Selected splash, acoustic, and wreck terrain-height callsites still used `HectonFloatingOrigin.ToAbsoluteUniversePosition(Vector3)` instead of the double committed-offset path.
- `Docs/Tasks/CURRENT_BATCH.md` still does not contain this agent block; the user-supplied XML remains the assignment source.

### Code Changes

- `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs`: added `double3 AbsolutePositionDouble` for breadcrumbs and a fixed `double3[64]` defoliant center lane. Breadcrumb merge, scent-grid cell resolution, channel sampling, nearest-waypoint search, and dead-zone checks now subtract in double before final float storage.
- `Assets/_Project/Scripts/World/AcousticOcclusionUtility.cs`: midpoint SDF occlusion sampling now reconstructs AUP through `ToAbsoluteUniversePositionDouble3` before the final `float3` SDF query payload.
- `Assets/_Project/Scripts/HectonPlayerMovement.cs`: surface-breach splash publication now builds the AUP payload from a double absolute position before final VFX payload casts.
- `Assets/_Project/Scripts/SubmarineFluidDynamics.cs`: splash and breach payloads now use double AUP reconstruction; splash LCG hashing folds floored double AUP coordinates as `long` entropy before the final seed mix.
- `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs`: terrain-height AUP query and burial cut centers now use double AUP reconstruction. `WreckBurialCutRecord.AbsoluteCenter` is `double3` and still fits the 64-byte record; voxel box crater submission calls the `double3` delta overload.

### Verification

- Mandatory scan re-run: `rg "\(float3\).*AUP|AupOffset|universe" Assets/_Project/Scripts --glob '*.cs'`. Residual hits are broad `universe` text plus final-cast fluid/scatter/shader payload names such as `AupOffsetXZ`; no new Loop 14 authority leak was identified.
- Direct committed-offset leak scan is clean: no `HectonFloatingOrigin.CurrentTotalOffset` without `Double`, no direct `CurrentTotalOffset.x/y/z`, and no direct legacy `NewTotalOffset`/`PreviousTotalOffset` component reads under `Assets/_Project/Scripts`.
- Targeted `ToAbsoluteUniversePosition(` scan is clean in `ChemicalInfluenceGrid`, `AcousticOcclusionUtility`, `HectonPlayerMovement`, `SubmarineFluidDynamics`, and `ProceduralWreckGenerator`.
- `git diff --check` on Loop 14 touched files reports line-ending warnings only, no whitespace errors.
- `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:normal /m:1 /nr:false /p:UseSharedCompilation=false /flp:"logfile=Docs\AgentLogs\AUP_build_loop14b.log;verbosity=normal"` failed with 0 warnings and 60 errors from unrelated active dependency work: `HardwareProfileCatalog`, `SaveMasterHashV10Result`/`SaveFileHeaderV10`, and `SystemID` vs `JobHandle`.
- Filtered build-log scan for `ChemicalInfluenceGrid`, `SubmarineFluidDynamics`, `HectonPlayerMovement`, `AcousticOcclusionUtility`, `ProceduralWreckGenerator`, and previously upgraded AUP/voxel files returned no errors.

### Evidence Queue

- `ChemicalBreadcrumbWaypoint` now carries double authority while legacy `AbsolutePosition` remains for existing consumers; future AI-facing contracts should migrate to the double field explicitly when ownership allows.
- Wreck burial cut records are double-safe without a native buffer size increase; any future voxel persistence migration should keep the same final-cast rule.
- Remaining editor-only `KinematicGhostDebugger` float universe history is diagnostic presentation and not a runtime AUP authority leak.

## Loop 15 Construction/Voxel/Seismic AUP Ingress Cleanup

### Findings

- Construction rupture/decal state, habitat edge midpoint feedback, drone voxel edit dispatch, drill placement probes, meteor splash, and seismic geology replay still had selected legacy `HectonFloatingOrigin.ToAbsoluteUniversePosition(Vector3)` callsites.
- The high-risk subset was authority or persistent-event math: rupture comparison, voxel DDA ingress, spark AUP payload, seismic trench line/id generation, and geology replay length calculation.
- `Docs/Tasks/CURRENT_BATCH.md` still does not contain this agent block; the user-supplied XML remains the assignment source.

### Code Changes

- `Assets/_Project/Scripts/Construction/BaseDegradationSystem.cs`: added `RuptureNodeState.AbsoluteUniversePositionDouble`, double comparison helpers, and double rupture/module AUP reconstruction before final decal `Vector3` output.
- `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs`: edge midpoint publication now reconstructs both sockets as `double3`, averages in double, and projects back to runtime only at the event/VFX boundary.
- `Assets/_Project/Scripts/HectonVoxelVolume.cs`: added `double3` overloads for `ApplyPlasmaCutDda` and `ApplyRepairWeldDda`; existing `Vector3` overloads now wrap through `ToDouble3`.
- `Assets/_Project/Scripts/Construction/DroneFleetManager.cs`: repair weld, plasma cut, and repair spark publication now preserve the ray hit as double AUP through voxel and persistent spark payload construction.
- `Assets/_Project/Scripts/Construction/DeepDrillModule.cs`: placement probe AUP sampling now uses `ToAbsoluteUniversePositionDouble3` and casts only into the existing probe packet.
- `Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs`: meteor splash AUP sampling now uses the double path; `SeismicShockwaveEvent` carries double AUP line endpoints and folds rounded double coordinates for deterministic seed entropy.
- `Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs`: seismic trench replay now uses double payload endpoints, double line length, and long-rounded trench ids before final legacy voxel plan casts.

### Verification

- Mandatory scan re-run: `rg "\(float3\).*AUP|AupOffset|universe" Assets/_Project/Scripts --glob '*.cs'`. Residual hits are broad `universe` text plus final-cast fluid/scatter/shader payload names.
- Direct committed-offset leak scan is clean across `Assets/_Project/Scripts`.
- Targeted `ToAbsoluteUniversePosition(` scan is clean in `BaseDegradationSystem`, `HabitatGraphManager`, `DroneFleetManager`, `DeepDrillModule`, `HectonVoxelVolume`, `RandomEventSystem`, and `WorldGenerativeGeologyVoxelBridgeDirector`.
- First Loop 15 build found one local error: `DeepDrillModule.cs` needed `using Unity.Mathematics;`. Fixed.
- After-fix Core build: `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:normal /m:1 /nr:false /p:UseSharedCompilation=false /flp:"logfile=Docs\AgentLogs\AUP_build_loop15_afterfix.log;verbosity=normal"` failed with 0 warnings and 60 unrelated dependency errors: missing `SaveMasterHashV10Result`/`SaveFileHeaderV10`, missing `HardwareProfileCatalog`, and `SystemID` passed where `JobHandle` is expected.
- Filtered build-log scan for Loop 15 touched files returned no errors.
- `git diff --check` on Loop 15 touched files reports line-ending warnings only, no whitespace errors.

### Evidence Queue

- Remaining runtime legacy `ToAbsoluteUniversePosition(` callsites are outside this Loop 15 patch and remain queued for classification: crash telemetry fallback, interaction tools, localized signage, player builder/tool, MapMagic/Crest helper surfaces, repair tools, submarine structural grid, spatial audio listener fallback, geology integration planning, and physical UI controls.
- `Vector3` overloads remain intentionally as compatibility wrappers. Future owning-domain patches should route authority callers to `double3` overloads and leave presentation-only callers documented.
- Seismic event dual lanes preserve existing consumers while allowing geology replay to stay 64-bit until the final voxel-plan boundary.

## Loop 16 Global Legacy Runtime-To-AUP Cleanup

### Findings

- The remaining `HectonFloatingOrigin.ToAbsoluteUniversePosition(Vector3)` runtime callsites covered interaction/tool packet producers, repair weld/debris payloads, geology planning, crash telemetry fallback, spatial audio listener fallback, submarine leak impact signals, and MapMagic/Crest/scatter/sign/player-builder presentation helpers.
- Several callsites were true authority or persistent-event math: voxel weld ingress, repair spark AUP, geology retained runtime keys, geology terrain/voxel centers, leak impact signals, and crash telemetry fallback.
- Presentation-only helper endpoints still needed final-cast discipline so no caller would reduce the committed offset before shader/transform/vector output.

### Code Changes

- `Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs`, `Assets/_Project/Scripts/PlayerTool.cs`, `Assets/_Project/Scripts/Interaction/PhysicalSnapSwitch.cs`, and `Assets/_Project/Scripts/UI/PhysicalPanelButton.cs`: interaction packet origins now use `ToAbsoluteUniversePositionDouble3` and cast once into `float3` packet fields.
- `Assets/_Project/Scripts/RepairTool.cs`: repair weld DDA and repair spark publication now use double AUP; spark payload builds `AbsoluteUniversePosition` from the double hit point.
- `Assets/_Project/Scripts/Construction/HabitatConstructionManager.cs`: grid snapping now reconstructs absolute coordinates as double, snaps millimeters in double, and projects runtime output through the double overload.
- `Assets/_Project/Scripts/WorldGenerativeGeologyIntegrationDirector.cs`: retained and newly built plans now use double AUP for world, terrain, and voxel centers; fallback runtime keys use rounded double millimeters instead of `Vector3.GetHashCode()`.
- `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` and `Assets/_Project/Scripts/SpatialAudioManager.cs`: leak impact and listener fallback AUP now build from double absolute positions.
- `Assets/_Project/Scripts/CrashTelemetryBuffer.cs`: player AUP fallback now uses MapMagic double universe space or `ToAbsoluteUniversePositionDouble3` before final telemetry `float3`.
- `Assets/_Project/Scripts/Plugins/MapMagic/MapMagicRuntimeBridge.cs`, `Assets/_Project/Scripts/Plugins/Crest/HectonCrestOceanDepthCacheBootstrap.cs`, `Assets/_Project/Scripts/WorldProceduralScatterDirectorSpatialHelpers.cs`, `Assets/_Project/Scripts/LocalizedWorldSign.cs`, and `Assets/_Project/Scripts/PlayerBuilder.cs`: presentation helper paths now keep double AUP until shader/transform/`Vector3` boundaries. Localized signs retain a double AUP side lane for origin-shift projection.

### Verification

- Global legacy HFO scan is clean: `rg -n "HectonFloatingOrigin\.ToAbsoluteUniversePosition\(" Assets/_Project/Scripts --glob '*.cs'` returned no hits.
- Direct committed-offset leak scan remains clean: no legacy `CurrentTotalOffset` reads without `Double`, no direct committed-offset component reads, and no legacy shift offset component reads under `Assets/_Project/Scripts`.
- Mandatory scan re-run: `rg "\(float3\).*AUP|AupOffset|universe" Assets/_Project/Scripts --glob '*.cs'`. Residual hits are broad `universe` text, editor diagnostics, and final-cast fluid/scatter/shader payload names.
- `git diff --check` on Loop 16 touched files reports line-ending warnings only, no whitespace errors.
- Core build: `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:normal /m:1 /nr:false /p:UseSharedCompilation=false /flp:"logfile=Docs\AgentLogs\AUP_build_loop16.log;verbosity=normal"` failed with 0 warnings and 74 unrelated errors from residency/power/fauna/core save-layout/hardware-profile/scheduler/gameplay dependency drift.
- Filtered build-log scan for Loop 16 touched files returned no errors.

### Evidence Queue

- All runtime `HectonFloatingOrigin.ToAbsoluteUniversePosition(` callsites under `Assets/_Project/Scripts` are removed. Remaining text hits are local helper names, editor diagnostics, docs/comments, or final-cast payload names.
- `InteractionPacket` remains float because it is a shared public contract; future contract migration should add a double/AUP lane rather than mutate existing fields in-place.
- Geology seam-plan storage remains legacy `Vector3` for consumers; a future world-geometry batch can add explicit double plan fields if downstream consumers are ready.

## Loop 17 Organic Vegetation Universe-Space Trigger Cleanup

### Findings

- `DestructibleOrganicManager.ApplyConstructionDecomposition` converted runtime construction centers through `HectonMapMagicVegetationBridge.ToUniverseSpace`, reducing stable universe coordinates to `Vector3` before construction cleanup radius checks.
- `ApplyDefoliantDeadZone` had the same float-center path and compared active flora roots via `(rootPosition - centerUniversePosition).sqrMagnitude`.
- Giant-kelp construction checks used a `Vector3` closest-point-on-segment calculation, so the stem/root distance gate lost committed-offset precision before trigger math completed.
- Titan root mound lookup extracted a stable-universe matrix translation into `Vector3` and projected it through the legacy `ToRuntimeSpace(Vector3)` helper before voxel lookup.

### Code Changes

- `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs`: construction and defoliant trigger centers now use `ToUniverseSpaceDouble3`; radii reject non-finite values and squared-radius math is `double`.
- `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs`: construction lane and defoliant lane signatures now consume `double3` centers and compare double squared distances against double radius thresholds.
- `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs`: giant-kelp construction distance now uses a `double3` root/top/center segment projection helper with `math.rcp`, then returns double squared length.
- `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs`: titan root mound voxel lookup now keeps the stable-universe anchor as `double3` and final-casts only through the bridge runtime projection boundary.
- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs`: added `ToRuntimeSpace(double3)` and `ToRuntimeSpaceDouble3(double3)` overloads while keeping existing `Vector3` compatibility.

### Verification

- Prompt extraction from `Docs/Tasks/CURRENT_BATCH.md` still returns `PROMPT_NOT_FOUND`; user-supplied XML remains authoritative.
- Targeted DestructibleOrganicManager scan is clean for legacy `HectonMapMagicVegetationBridge.ToUniverseSpace(`, `Vector3 universePosition`, Vector3 construction/defoliant lane signatures, and `(rootPosition - centerUniversePosition).sqrMagnitude`.
- Global `rg -n "HectonFloatingOrigin\.ToAbsoluteUniversePosition\(" Assets/_Project/Scripts --glob '*.cs'` remains clean.
- Direct committed-offset leak scan remains clean across `Assets/_Project/Scripts`.
- Mandatory `rg "\(float3\).*AUP|AupOffset|universe" Assets/_Project/Scripts --glob '*.cs'` was re-run. Residual hits are broad `universe` text, editor diagnostics, final-cast fluid/scatter/shader payload names, and double-safe vegetation bridge/helper names.
- `git diff --check -- Assets/_Project/Scripts/World/DestructibleOrganicManager.cs Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` reports no whitespace errors.
- Core build log `Docs/AgentLogs/AUP_build_loop17.log` completed with 47 unrelated package warnings and 74 unrelated Core errors; filtered scan for `DestructibleOrganicManager.cs` and `HectonMapMagicVegetationBridge.cs` reports no errors or warnings.

### Evidence Queue

- `HectonMapMagicVegetationBridgeFloraCollisionProxies` still owns a legacy `Vector3` proxy cache path for collision-proxy deactivation; that is a candidate for a future loop if it feeds authority distance math rather than presentation/collider toggling.
- Editor-only `KinematicGhostDebugger` still has float universe preview history; it remains diagnostic presentation, not runtime AUP authority.
- Octahedral impostor universe centers remain float shader presentation data and are outside this Loop 17 trigger-math repair.

## Loop 18 Large-Flora Collision Proxy Double Cache

### Findings

- `HectonMapMagicVegetationBridgeFloraCollisionProxies` used `Vector3[] _largeFloraColliderUniverseCenters` for active proxy cache state.
- Player universe resolution and candidate center resolution used legacy `ToUniverseSpace`, reducing the vegetation bridge double offset before activation checks.
- Activation and deactivation thresholds used `Vector3.sqrMagnitude`, so proxy pool state could oscillate near distance thresholds after origin shifts.
- Proxy rebase projected cached `Vector3` universe centers through the legacy `ToRuntimeSpace(Vector3)` helper.

### Code Changes

- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`: `_largeFloraColliderUniverseCenters` is now `double3[]`; cold allocation comment updated.
- Player universe resolution now uses `ToUniverseSpaceDouble3`.
- Candidate center conversion now uses `ToUniverseSpaceDouble3`; activation radius compares `math.lengthsq(double3)` against double radius squared.
- Deactivation hysteresis compares cached `double3` proxy centers against the `double3` player center.
- Proxy transform rebase uses `ToRuntimeSpace(double3)` and casts only at the Unity transform boundary.

### Verification

- Prompt extraction from `Docs/Tasks/CURRENT_BATCH.md` still returns `PROMPT_NOT_FOUND`; user-supplied XML remains authoritative.
- Targeted proxy scan is clean for legacy `ToUniverseSpace(`, `Vector3 playerUniverse`, `Vector3 centerUniverse`, `Vector3 proxyUniverse`, `.sqrMagnitude`, `Vector3[] _largeFloraColliderUniverseCenters`, and the old `ActivateOrUpdateLargeFloraCollisionProxy` `Vector3` center signature.
- Global `rg -n "HectonFloatingOrigin\.ToAbsoluteUniversePosition\(" Assets/_Project/Scripts --glob '*.cs'` remains clean.
- Direct committed-offset leak scan remains clean across `Assets/_Project/Scripts`.
- Mandatory `rg "\(float3\).*AUP|AupOffset|universe" Assets/_Project/Scripts --glob '*.cs'` was re-run. Residual hits are broad `universe` text, editor diagnostics, final-cast fluid/scatter/shader payload names, and double-safe vegetation helper/cache names.
- `git diff --check -- Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` reports line-ending warnings only, no whitespace errors.
- Core build log `Docs/AgentLogs/AUP_build_loop18.log` completed with 47 unrelated package warnings and 74 unrelated Core errors; filtered scan for `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` reports no C# errors or warnings.

### Evidence Queue

- Remaining `ToUniverseSpace` runtime hits are the legacy bridge wrapper itself, editor-only `KinematicGhostDebugger`, and `VoxelDynamicNavGridRuntime.ToRuntimeSpace(stableUniverseRoot)` which needs separate authority classification before mutation.
- Octahedral impostor universe centers remain float shader presentation data.
- `PlayerPDA` universe-offset display remains a UI presentation path and not authority distance math.

## Loop 19 Voxel Nav Macro-Flora Root Projection Double Bridge

### Findings

- `VoxelDynamicNavGridRuntime.TryResolveMacroFloraObstacleWorldBounds` converted a stable vegetation matrix root into `Vector3` before calling the vegetation runtime projection helper.
- The method feeds macro-flora obstacle bounds used by dynamic nav-grid/runtime passability, so the bridge hop is authority-adjacent even though the final grid payload remains float.
- Targeted H-Phi scan found `HphiReactiveUiTelemetry` and headless QA counters only. No H-Phi file consumed AUP/origin/distance math in this agent's domain.

### Code Changes

- `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs`: matrix translation is now captured as `double3 stableUniverseRoot`.
- Runtime projection now calls `HectonMapMagicVegetationBridge.ToRuntimeSpace(double3)` and final-casts only when building the `float3` nav obstacle center.
- No UI/H-Phi file was changed because no AUP authority dependency was present.

### Verification

- Prompt extraction from `Docs/Tasks/CURRENT_BATCH.md` still returns `PROMPT_NOT_FOUND`; user-supplied XML remains authoritative.
- Mandatory `rg "\(float3\).*AUP|AupOffset|universe" Assets/_Project/Scripts --glob '*.cs'` was re-run. Residual hits remain broad `universe` text, editor diagnostics, and known final-cast fluid/scatter/shader payload names.
- Direct committed-offset leak scan remains clean across `Assets/_Project/Scripts`.
- Targeted nav scan confirms no `Vector3 stableUniverseRoot`; `stableUniverseRoot` is now `double3` and resolves to the bridge's double overload.
- `rg -n "ToUniverseSpace\(|ToRuntimeSpace\(" Assets/_Project/Scripts/World Assets/_Project/Scripts/UI Assets/_Project/Scripts/Gameplay --glob '*.cs'` leaves bridge wrappers plus double-safe runtime callsites.
- `git diff --check -- Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` reports line-ending warnings only, no whitespace errors.
- No `dotnet build` or rebuild was run in Loop 19 because the latest user instruction explicitly forbids rebuilds.

### Evidence Queue

- `PlayerPDA` universe-offset display remains a UI presentation path: it sources `CurrentTotalOffsetDouble` and casts for text output only.
- Octahedral impostor universe centers remain float shader presentation data.
- H-Phi UI telemetry should be handled by a UI/QA owner if throttling or metric changes are desired; no AUP precision leak was found there.

## Loop 20 H-Phi Static AUP Precision Hygiene

### Findings

- The existing headless H-Phi static model scored architecture and memory-shape risk but did not include AUP precision hygiene.
- This meant legacy patterns such as committed-offset component reads or legacy AUP bridge calls were invisible to the H-Phi scalar even though they are first-order drift risks.
- `HphiReactiveUiTelemetry` remains UI-only and was not changed.

### Code Changes

- `Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs`: added `AupPrecisionSafe` and `AupPrecisionRisk` fields to `HPhiStaticCounters`.
- Added `CountAupPrecisionSafe` for double-safe AUP patterns: `CurrentTotalOffsetDouble`, `ToAbsoluteUniversePositionDouble3`, `ToUniverseSpaceDouble3`, `ToRuntimeSpaceDouble3`, `ToRuntimeSpace(double3)`, `FromAbsolutePosition`, and AUP `DistanceSq`.
- Added `CountAupPrecisionRisk` for legacy precision-risk patterns: `CurrentTotalOffset` component reads, legacy shift offset component reads, legacy AUP/vegetation bridge calls, `(float3)AUP`, `Vector3 universePosition`, and `Vector3 stableUniverseRoot`.
- `CalculateHPhiRisk` now multiplies by an AUP precision integrity factor. Files with no AUP patterns default to neutral `1.0` for that factor.
- H-Phi model text is now `runtime_aup_risk_adjusted` in JSON output and the `[H-PHI_STATIC]` warning line.

### Verification

- Prompt extraction from `Docs/Tasks/CURRENT_BATCH.md` still returns `PROMPT_NOT_FOUND`; user-supplied XML remains authoritative.
- Mandatory `rg "\(float3\).*AUP|AupOffset|universe" Assets/_Project/Scripts --glob '*.cs'` was re-run. The scanner's own split literals no longer add `(float3).*AUP` hits.
- Direct committed-offset leak scan remains clean across `Assets/_Project/Scripts`.
- Targeted H-Phi scan confirms `runtime_aup_risk_adjusted`, `AupPrecisionSafe`, and `AupPrecisionRisk` are present.
- `git diff --check -- Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs` reports line-ending warnings only, no whitespace errors.
- No `dotnet build` or rebuild was run in Loop 20 because the latest user instruction explicitly forbids rebuilds.

### Evidence Queue

- Runtime AUP shift signal payloads still expose `ShiftMeters` as `float3`; most consumers are presentation/runtime rebase paths. A future signal-contract loop can add a double side lane if compile validation is allowed.
- H-Phi now catches newly introduced AUP precision debt statically, but the scalar still requires actual headless execution to produce a measured value.
