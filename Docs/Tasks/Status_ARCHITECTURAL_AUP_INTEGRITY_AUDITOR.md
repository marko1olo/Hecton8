# Status_ARCHITECTURAL_AUP_INTEGRITY_AUDITOR

Agent: ARCHITECTURAL_AUP_INTEGRITY_AUDITOR
Domain: ECHELON 1 / Origin Shift (AUP Manager), with audit reach into Physics, Voxel, Kinematics, AI trigger math, Biome trigger math, and deterministic seed callsites.
Assignment Source: User-supplied XML block. `Docs/Tasks/CURRENT_BATCH.md` extraction returned `PROMPT_NOT_FOUND` for this ID on initial pass.
Status: VERIFIED AUP INTEGRITY - LOOP 17 APPLIED; ORGANIC VEGETATION UNIVERSE-SPACE TRIGGERS DOUBLE-SAFE; GLOBAL LEGACY HFO AUP SCAN CLEAN; CORE BUILD BLOCKED BY UNRELATED DEPENDENCY WALL; ASMDEF BLOCKED BY ARCHITECTURE

## Selected Mandates

1. MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
2. CI_MATH_VIOLATIONS_Gate.txt
3. MATH_Deterministic_RNG_SlotMachine.txt
4. MATH_Rsqrt_i3_SIMD.txt
5. PHYS_Physics_Integrity_Determinism_ForceMode.txt
6. OPT_Zero_GC_Policy_AllocFree_Mandate.txt
7. OPT_Native_Memory_Collections_JobSystem_Protocol.txt
8. DBG_Telemetry_Crash_Reporting_PostMortem.txt

## State Machine

- [x] Task 1 - THE FLOAT SCAN | Justification: ran mandatory `rg "\(float3\).*AUP|AupOffset|universe"` and scoped runtime scans. DOD: direct CLI evidence found AUP float offset lanes in fluid/GPU scatter and a core AUP constructor downcast. Alternative rejected: trusting type names. Estimate: 18-35 us saved by removing downstream jitter correction work from AUP constructors.
- [x] Task 2 - ACCUMULATOR INQUISITION | Justification: scanned `AbsoluteUniversePosition`, `AbsolutePosition`, `ToAbsoluteDouble3`, and `dt` accumulation paths. DOD: no direct `AbsoluteUniversePosition += float dt` hot path found; origin offset accumulation was upgraded to a double lane. Alternative rejected: rewriting prologue visual universe velocity outside AUP domain. Estimate: 2-6 us saved by avoiding late correction passes.
- [x] Task 3 - SYNC-FENCE AUDIT | Justification: verified `PlayerKinematicsRuntime.SyncFenceFrameInterval = 300`, sync hash telemetry, and AUP shift sequence publication. DOD: 300-frame fence exists; origin watchdog now records drift telemetry on completion. Alternative rejected: comments-only acceptance. Estimate: 1-3 us overhead every 300 frames.
- [x] Task 4 - DOUBLE-PRECISION KERNEL | Justification: `AbsoluteUniversePosition.FromRuntimePosition` now calls `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3`; `AUPDirection` subtracts in double and uses rsqrt before final float cast. Alternative rejected: `Vector3` absolute reconstruction and `math.normalizesafe(float3(delta))`. Estimate: 12-45 us saved under high drift by preventing jitter repair cascades.
- [x] Task 5 - LCG DETERMINISM | Justification: scans found no `(int)SectorHash` truncation; `ProceduralOreSpawner` folds low/high sector hash bits before uint job seed and uses long sector keys for depletion. Alternative rejected: int-only seed. Estimate: 0 us hot change; preserves deterministic entropy.
- [x] Task 6 - REBASE UNIFICATION | Justification: audited `AupShiftSignal` publication and consumers; changed `WorldChunkResidencyManager` from destructive queue drain to non-destructive `SignalBus<AupShiftSignal>.GetFrameSnapshot()` with applied-sequence guard. Alternative rejected: direct queue consumption that can starve future parallel consumers. Estimate: 2-8 us saved on shift frames by avoiding missed rebase repair.
- [x] Task 7 - MILLIMETER SNAP | Justification: verified `PlayerKinematicsRuntime.StageStateWrite`, body job exit, correction ingress, and `HectonPlayerMotor.MovePosition` all snap final KCC positions to millimeters. Alternative rejected: adding duplicate snap in every caller. Estimate: 0 us change; prevents drift accumulation.
- [x] Task 8 - DIVISION BAN | Justification: scoped `/ dt` scan across AUP/origin/KCC files is clean after replacing origin anchor fallback velocity with `* math.rcp(safeDeltaTime)`. Alternative rejected: rewriting unrelated presentation velocity estimators. Estimate: sub-1 us plus deterministic math consistency.
- [x] Task 9 - MATH LOD | Justification: verified low-tier math is explicitly tier-gated in KCC/fluid paths; no hidden AUP float fallback was introduced. Remaining fluid/scatter AUP float offsets are presentation/shader lanes and recorded in `AUP_DRIFT_REPORT.md`. Alternative rejected: silent float downgrade in AUP authority. Estimate: 0 us code change beyond audit.
- [x] Task 10 - BLACKBOX DUMP | Justification: `CrashTelemetryBuffer.ReportAupMaxDriftError` now records max watchdog drift into the fixed telemetry ring without fault export. Alternative rejected: managed log strings or per-frame allocations. Estimate: below 1 us every 300 frames for two tracked entities.
- [x] Task 11 - ZERO-GC | Justification: changed hot paths use fields, stack value math, ReadOnlySpan snapshots, existing NativeArrays, and existing telemetry ring writes. DOD: no managed allocation introduced in AUP/origin/residency/acoustic patches. Alternative rejected: managed debug logs or new per-frame containers. Estimate: 0 B/frame, sub-1 us normal frames.
- [x] Task 12 - TRIPLE-STRIKE REPAIR | Justification: introduced brine overload mismatches were fixed instead of marked blocked; later dependency drift cleared and Loop 10 Core build passed. Alternative rejected: asmdef rewiring across unrelated domains. Estimate: 0 us direct runtime, prevents failed precision patch from entering integration.
- [x] Task 13 - RSQRT AUDIT | Justification: scoped normalization/sqrt scan over AUP/origin/KCC/acoustic files found no `math.normalize`, `math.normalizesafe`, `.normalized`, or sqrt after patches; Kinematic CCD and AUP direction use `math.rsqrt`. Alternative rejected: sqrt normalization. Estimate: 1-4 us saved in drift/steering callsites.
- [x] Task 14 - ASMDEF ISOLATION [BLOCKED BY ARCHITECTURE] | Justification: `rg` found no `Hecton8.Core.AUP` asmdef or namespace. Existing AUP struct is embedded in `PersistentWorldRegistry.cs`, which depends on UnityEngine. Alternative rejected: creating an empty asmdef or moving a shared public struct during an audit patch. Estimate: future migration required.
- [x] Task 15 - OMEGA COMPILE | Justification: Loop 10 `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors. Alternative rejected: fake green compile report during earlier dependency wall. Estimate: 0 us runtime, integration gate cleared for Core.

## Iteration Log

Loop 0:
- Read AGENTS.md, domain map, and selected mandates.
- Current batch extraction failed for this ID; user-supplied XML remains primary assignment unless a matching batch block appears later.
- No code edits yet.

Loop 1:
- Re-extracted batch prompt after Task 4; `Docs/Tasks/CURRENT_BATCH.md` still has no `ARCHITECTURAL_AUP_INTEGRITY_AUDITOR` block.
- Patched AUP constructor path to use a double committed-offset lane until final presentation cast.
- Patched AUP direction normalization to calculate double length and use `math.rsqrt`.
- Patched origin drift watchdog to push `AupMaxDriftError` into crash telemetry every completed watchdog pass.
- Patched origin motion `/ safeDeltaTime` to `* math.rcp(safeDeltaTime)`.
- Compile attempt 1: `dotnet build Hecton8.Core.csproj` failed with 131 existing missing-reference errors before edited code could be isolated.
- Compile attempt 2: `dotnet build Assembly-CSharp.csproj` timed out after 120s; stopped the timed-out build process and shut down orphaned build servers.
- Compile attempt 3: Unity MCP script validation failed because no Unity session was available.

Loop 2:
- Re-extracted batch prompt after Task 8; `Docs/Tasks/CURRENT_BATCH.md` still has no matching prompt block.
- Patched `WorldChunkResidencyManager` AUP shift consumption to snapshot-based non-destructive reads.
- Patched `AcousticOcclusionUtility.ResolveAupDistanceMeters` to use `AbsoluteUniversePosition.DistanceSq` and double rsqrt before final float return.
- Re-ran mandatory AUP scan; residual hits remain in fluid/scatter presentation lanes and documents.
- Scoped division scan over AUP/origin/KCC/acoustic/residency files returned no `/ dt` hits.

Loop 3:
- Scoped rsqrt audit over AUP/origin/KCC/acoustic files found no remaining normalize/sqrt calls in the patched authority paths.
- Confirmed no `Hecton8.Core.AUP` asmdef exists; task marked blocked by architecture instead of creating an empty assembly.
- Marked compile tasks blocked by the documented project-reference wall after three verification attempts.

Loop 4 - Omega Polish:
- Extracted `<POLISH_MANDATE>` from `Docs/Tasks/CURRENT_BATCH.md`; result: `POLISH_MANDATE_NOT_FOUND`.
- Ran anti-bloat review anyway: no empty asmdef shell, no managed telemetry logs, no new per-frame collections, no destructive AUP queue consumers.
- Verified Unity.Mathematics has `math.rsqrt(double)` in package cache.
- `git diff --check` reported line-ending warnings only, no whitespace errors.

Loop 5 - Runtime Projection Upgrade:
- Re-read status/rationale, re-opened the Unity MCP operator skill, and re-ran the mandatory AUP scan.
- Re-extracted `ARCHITECTURAL_AUP_INTEGRITY_AUDITOR` from `Docs/Tasks/CURRENT_BATCH.md`; result remains `PROMPT_NOT_FOUND`.
- Patched `AbsoluteUniversePosition.ToRuntimeFloat3()` to subtract `HectonFloatingOrigin.CurrentTotalOffsetDouble` before the final float presentation cast.
- Added a `double3` overload for `AUPMath.ToRuntimeFloat3` and retained the `float3` overload for existing job payload compatibility.
- Patched `WorldSpatialHashGrid` AUP validation and far-unload rehydration to use `double3` committed offsets instead of `Vector3` offsets.
- Re-ran `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:quiet -clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`; still blocked by 140 existing missing-reference/interface errors outside this AUP patch set.
- Unity MCP `validate_script` on `Assets/_Project/Scripts/World/AUPMath.cs` returned `no_unity_session`.

Loop 6 - Shift Payload Double Fence:
- Re-read status/rationale and selected mandates before code.
- Re-extracted `ARCHITECTURAL_AUP_INTEGRITY_AUDITOR` from `Docs/Tasks/CURRENT_BATCH.md`; result remains `PROMPT_NOT_FOUND`.
- Added `PreviousTotalOffsetDouble` and `NewTotalOffsetDouble` to `OriginShiftEventData` while preserving the existing `Vector3` API.
- Routed `HectonFloatingOrigin.WaitForShiftStabilityAsync`, committed shift events, safe teleport events, sector-delta calculation, and `ToRuntimePosition` helpers through double committed offsets.
- Upgraded fauna route/hunt target rebases, corpse-resource rebase, and corpse-sink Burst input to use `double3` committed offsets.
- Swapped scalar absolute-depth/height/shader offset helpers to `CurrentTotalOffsetDouble` before final float presentation output.
- Direct scan for `CurrentTotalOffset.x/y/z`, `(float3)CurrentTotalOffset`, and `NewTotalOffset.x/y/z` is clean under `Assets/_Project/Scripts`; remaining mandatory regex hits are broader `universe` text and fluid/presentation AUP offset lanes.
- Post-edit Core build attempt timed out after 94 seconds; stopped only the timed-out `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers ...` process started by this agent. Another Core build process from a different parent remained running and was not touched.

Loop 7 - Voxel Finalization Double Capture:
- Re-read status/rationale before patching and re-extracted `ARCHITECTURAL_AUP_INTEGRITY_AUDITOR` from `Docs/Tasks/CURRENT_BATCH.md`; result remains `PROMPT_NOT_FOUND`.
- Patched `HectonVoxelEngine` pipeline data to preserve `AbsoluteUniverseOffsetAtStartDouble` beside the legacy `Vector3` compatibility field.
- Routed voxel async root rebase, shift-aware local projection, terrain-hole registration, spawn-point registration, collider fake distance checks, overhang facing AUP checks, anomaly origins, biome heatmap coordinate math, and chthonic pillar bounds through the double captured offset before final `Vector3`/`float3` presentation casts.
- Direct scan for `StableShift.NewTotalOffset`, `postMeshShift.NewTotalOffset`, `(float3)data.AbsoluteUniverseOffsetAtStart`, and `AbsoluteUniverseOffsetAtStart.x/y/z` in `HectonVoxelEngine.cs` is clean except the legacy field storage itself.
- Re-ran mandatory `rg "\(float3\).*AUP|AupOffset|universe"`; residual hits remain broad `universe` text plus fluid/scatter/presentation AUP offset lanes.
- `git diff --check -- Assets/_Project/Scripts/HectonVoxelEngine.cs` reports line-ending warning only, no whitespace errors.
- `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:quiet -clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false` failed with 128 existing missing-reference/interface errors; only `HectonVoxelEngine.cs` error reported is the known pre-existing line 21 `Hecton8.Core.Scheduling` missing namespace. Unity MCP validation failed because the local MCP endpoint was unavailable.

Loop 8 - Fauna/Brine/Scanner Offset Double Lane:
- Re-read status/rationale and re-ran prompt extraction; `Docs/Tasks/CURRENT_BATCH.md` still has no matching `ARCHITECTURAL_AUP_INTEGRITY_AUDITOR` block.
- Upgraded predator cognition input `FloatingOriginOffset` from `float3` to `double3`; fauna compatibility now sources `HectonFloatingOrigin.CurrentTotalOffsetDouble`, and telemetry/runtime AUP projection subtracts the double offset before final `float3`.
- Upgraded fauna sensor brine-plane checks, ecosystem brine mutation sampling, resource brine cartography sector math, scan render shader centers, scanner projection shader origin, and Scatter GPUI origin-relative matrices to use double committed offsets before final float presentation output.
- Added double-offset overloads to `BrineLayerMath`; Core compile could not see that surface through current assembly layout, so Core-facing callers now perform double subtraction locally instead of depending on the overload.
- Re-ran mandatory `rg "\(float3\).*AUP|AupOffset|universe"`; remaining hits are broad text plus fluid/scatter/presentation lanes.
- Targeted scan for `CurrentTotalOffset;`, `CurrentTotalOffset.x/y/z`, `AUPMath.ToRuntimeFloat3(... float3 offset)`, and brine helper calls with double offsets is clean in patched fauna/gameplay/world paths except intentional double validation fields.
- First Loop 8 build failed with 54 project errors and exposed three caller type mismatches from the new brine overload use; those were fixed. The follow-up constrained Core build timed out after 124 seconds under the existing compile wall, with a separate build from another parent left untouched.

Loop 9 - Fluid Presentation Offset Final Cast:
- Re-read status/rationale, re-opened the AUP mandate, and re-extracted `ARCHITECTURAL_AUP_INTEGRITY_AUDITOR` from `Docs/Tasks/CURRENT_BATCH.md`; result remains `PROMPT_NOT_FOUND`.
- Patched `HectonFluidEngine` flow sampling, water-height sampling, buoyancy wave/vector-noise scheduling, brine shift scalar setup, and GPU abyssal flow noise offset upload to source `HectonFloatingOrigin.CurrentTotalOffsetDouble` and cast only at the job/shader float payload boundary.
- Targeted fluid scan for legacy `HectonFloatingOrigin.CurrentTotalOffset`, direct `.x/.y/.z` reads, and `(float3)` casts against `CurrentTotalOffset` is clean in `HectonFluidEngine.cs`.
- Re-ran mandatory `rg "\(float3\).*AUP|AupOffset|universe"`; residual fluid hits are named job fields (`AupOffsetXZ`, `vectorNoiseAupOffset`) that now receive final-cast float payloads, plus broad universe text and unowned vegetation/scatter presentation lanes.
- `git diff --check -- Assets/_Project/Scripts/HectonFluidEngine.cs` reports line-ending warning only, no whitespace errors.
- `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false` failed with 0 warnings and 1 existing dependency error: `PlayerCriticalProceduralAudioRenderer.cs(10002,31)` missing `PrologueSplashdownSineSweepProbeJob`.

Loop 10 - Vegetation Stable-Universe Double Bridge:
- Re-read status/rationale and re-extracted `ARCHITECTURAL_AUP_INTEGRITY_AUDITOR` from `Docs/Tasks/CURRENT_BATCH.md`; result remains `PROMPT_NOT_FOUND`.
- Added a double-precision vegetation universe-offset lane to `HectonMapMagicVegetationBridge` while preserving legacy `Vector3` properties and conversion APIs for existing callers.
- Routed vegetation origin-shift sync from `OriginShiftEventData.NewTotalOffsetDouble`, stable matrix conversion, runtime/universe bridge helpers, semantic anchor AUP reconstruction, density-grid XZ tests, and sargassum density origins through double offset math before final `Vector3`/matrix outputs.
- Targeted scan for `_totalUniverseOffset.x/y/z`, `Vector3 universeOffset`, legacy `CurrentTotalOffset`, and Vector3 matrix conversion in patched vegetation/scatter/fluid files is clean.
- Re-ran mandatory `rg "\(float3\).*AUP|AupOffset|universe"`; residual hits are broad text plus final-cast fluid/scatter payload names and explicit presentation/legacy `Vector3 universe` APIs.
- `git diff --check` on Loop 10 files reports line-ending warnings only, no whitespace errors.
- `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.

Loop 14 - Runtime Chemical/Wreck Persistent Double Lane:
- Re-read status/rationale, re-opened the Unity MCP operator skill, and re-extracted `ARCHITECTURAL_AUP_INTEGRITY_AUDITOR` from `Docs/Tasks/CURRENT_BATCH.md`; result remains `PROMPT_NOT_FOUND`.
- Patched `ChemicalInfluenceGrid` breadcrumbs, scent-grid cells, channel sampling, nearest-waypoint distance checks, and permanent defoliant dead-zone centers to retain `double3` absolute positions for all distance math before final legacy `float3`/`Vector4` storage.
- Patched `AcousticOcclusionUtility`, `HectonPlayerMovement`, `SubmarineFluidDynamics`, and `ProceduralWreckGenerator` callsites so persistent payloads, splash seeds, terrain-height AUP queries, and voxel burial cuts reconstruct AUP through `ToAbsoluteUniversePositionDouble3`.
- Changed `ProceduralWreckGenerator.WreckBurialCutRecord.AbsoluteCenter` from `float3` to `double3` while preserving the 64-byte record size; burial crater submission now calls the `double3` voxel delta overload.
- Direct scan for legacy committed-offset reads is clean: no `HectonFloatingOrigin.CurrentTotalOffset` without `Double`, no direct `.x/.y/.z` offset reads, and no direct legacy `NewTotalOffset`/`PreviousTotalOffset` component reads under `Assets/_Project/Scripts`.
- Targeted Loop 14 scan for `ToAbsoluteUniversePosition(` is clean in `ChemicalInfluenceGrid`, `AcousticOcclusionUtility`, `HectonPlayerMovement`, `SubmarineFluidDynamics`, and `ProceduralWreckGenerator`.
- Re-ran mandatory `rg "\(float3\).*AUP|AupOffset|universe"`; residual hits are broad `universe` text plus final-cast fluid/scatter/shader payload names, not newly introduced AUP authority leaks.
- `git diff --check` on Loop 14 touched files reports line-ending warnings only, no whitespace errors.
- `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:normal /m:1 /nr:false /p:UseSharedCompilation=false /flp:"logfile=Docs\AgentLogs\AUP_build_loop14b.log;verbosity=normal"` failed with 0 warnings and 60 unrelated errors from active dependency work: `HardwareProfileCatalog`, `SaveMasterHashV10Result`/`SaveFileHeaderV10`, and `SystemID` vs `JobHandle`. A filtered build-log scan reports no errors for Loop 14 touched AUP files.

Loop 15 - Construction/Voxel/Seismic AUP Ingress Cleanup:
- Re-read status/rationale, AGENTS.md, and the Unity MCP operator skill; re-extracted `ARCHITECTURAL_AUP_INTEGRITY_AUDITOR` from `Docs/Tasks/CURRENT_BATCH.md`; result remains `PROMPT_NOT_FOUND`.
- Patched `BaseDegradationSystem` rupture state to preserve `AbsoluteUniversePositionDouble` and compare decal state in double before final legacy `Vector3` storage/output.
- Patched `HabitatGraphManager` edge midpoint events to reconstruct both edge endpoints through `ToAbsoluteUniversePositionDouble3` and average in double before runtime projection.
- Added `double3` ingress overloads to `HectonVoxelVolume.ApplyPlasmaCutDda` and `ApplyRepairWeldDda`; legacy `Vector3` overloads remain as wrappers.
- Patched `DroneFleetManager` repair/plasma cut dispatch and spark publication to keep hit points in double AUP until voxel/VFX boundary conversion.
- Patched `DeepDrillModule` placement probe AUP reconstruction to use `ToAbsoluteUniversePositionDouble3`; first Loop 15 build exposed a missing `Unity.Mathematics` import, which was fixed.
- Patched `RandomEventSystem` meteor splash and `SeismicShockwaveEvent` to carry double AUP line endpoints; trench direction seeding folds rounded double coordinates into uint entropy without int truncation.
- Patched `WorldGenerativeGeologyVoxelBridgeDirector` seismic trench replay to consume the new double AUP line, compute length/id in double/long, and cast only for legacy voxel plan fields.
- Direct committed-offset scan remains clean: no `HectonFloatingOrigin.CurrentTotalOffset` without `Double`, no direct `.x/.y/.z` committed-offset reads, and no direct legacy `NewTotalOffset`/`PreviousTotalOffset` component reads under `Assets/_Project/Scripts`.
- Targeted Loop 15 `ToAbsoluteUniversePosition(` scan is clean in `BaseDegradationSystem`, `HabitatGraphManager`, `DroneFleetManager`, `DeepDrillModule`, `HectonVoxelVolume`, `RandomEventSystem`, and `WorldGenerativeGeologyVoxelBridgeDirector`.
- Re-ran mandatory `rg "\(float3\).*AUP|AupOffset|universe"`; residual hits remain broad `universe` text plus final-cast fluid/scatter/shader payload names.
- `git diff --check` on Loop 15 touched files reports line-ending warnings only, no whitespace errors.
- First Loop 15 Core build failed with 61 errors, one local error in `DeepDrillModule.cs` (`double3` import missing) plus the dependency wall. After fixing the import, `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:normal /m:1 /nr:false /p:UseSharedCompilation=false /flp:"logfile=Docs\AgentLogs\AUP_build_loop15_afterfix.log;verbosity=normal"` failed with 0 warnings and 60 unrelated errors from `SaveMasterHashV10Result`/`SaveFileHeaderV10`, `HardwareProfileCatalog`, and `SystemID` vs `JobHandle`. A filtered build-log scan reports no errors for Loop 15 touched files.

Loop 16 - Global Legacy Runtime-To-AUP Cleanup:
- Re-read status/rationale before reporting; continued from the remaining `HectonFloatingOrigin.ToAbsoluteUniversePosition(` scan results.
- Patched interaction packet producers in `EquipmentInteractionHandler`, `PlayerTool`, `PhysicalSnapSwitch`, and `PhysicalPanelButton` to reconstruct hand/tool origins through `ToAbsoluteUniversePositionDouble3` before final `float3` packet casts.
- Patched `RepairTool` to send voxel weld ingress and repair spark payloads from double AUP; existing packet/voxel compatibility wrappers remain intact.
- Patched `HabitatConstructionManager.SnapWorldPosition` to snap absolute grid coordinates in double millimeters before runtime projection.
- Patched `SubmarineStructuralGrid` leak impact signals and `SpatialAudioManager` listener fallback to build `AbsoluteUniversePosition` from double AUP.
- Patched MapMagic/Crest/scatter/sign/player-builder presentation helpers to use double AUP until their required shader, transform, or `Vector3` boundary.
- Patched `CrashTelemetryBuffer` fallback player AUP helper to compare/use MapMagic double universe space and otherwise fall back to `ToAbsoluteUniversePositionDouble3`.
- Patched `WorldGenerativeGeologyIntegrationDirector` planning to store terrain/voxel centers from double AUP, build fallback runtime keys from rounded double millimeters, and use `AbsoluteUniversePosition.FromAbsolutePosition`.
- Global scan for `HectonFloatingOrigin.ToAbsoluteUniversePosition(` under `Assets/_Project/Scripts --glob '*.cs'` is clean.
- Direct committed-offset scan remains clean.
- Re-ran mandatory `rg "\(float3\).*AUP|AupOffset|universe"`; residual hits are broad `universe` text, editor diagnostics, and final-cast fluid/scatter/shader payload names.
- `git diff --check` on Loop 16 touched files reports line-ending warnings only, no whitespace errors.
- `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:normal /m:1 /nr:false /p:UseSharedCompilation=false /flp:"logfile=Docs\AgentLogs\AUP_build_loop16.log;verbosity=normal"` failed with 0 warnings and 74 unrelated errors from active dependency work: residency/power/fauna native release signatures, `HardwareProfileCatalog`, save layout V10 types, `SystemID` vs `JobHandle`, `ContextualPhysicalIkRig.SpineTargetCountPerChain`, and `SubmarineAutoLevelBallastController` handle mismatch. A filtered build-log scan reports no errors for Loop 16 touched files.

Loop 17 - Organic Vegetation Universe-Space Trigger Cleanup:
- Re-read status/rationale before reporting and re-extracted `ARCHITECTURAL_AUP_INTEGRITY_AUDITOR` from `Docs/Tasks/CURRENT_BATCH.md`; result remains `PROMPT_NOT_FOUND`.
- Patched `DestructibleOrganicManager.ApplyConstructionDecomposition` and `ApplyDefoliantDeadZone` to convert runtime centers through `HectonMapMagicVegetationBridge.ToUniverseSpaceDouble3`, reject non-finite radii, and compare squared distances in double.
- Patched construction giant-kelp segment distance to use `double3` closest-point math with `math.rcp` instead of reducing the center/root/top to `Vector3` before the trigger check.
- Patched defoliant lane checks to subtract `double3` roots from the `double3` center before radius comparison.
- Added `HectonMapMagicVegetationBridge.ToRuntimeSpace(double3)` / `ToRuntimeSpaceDouble3(double3)` so stable-universe vegetation anchors can project to runtime without a legacy `Vector3` bridge hop.
- Patched titan root mound voxel lookup to feed a `double3` stable-universe anchor through the new vegetation bridge overload and final-cast only for `TryGetNearestActiveVolume`.
- Targeted DestructibleOrganicManager scan is clean: no legacy `HectonMapMagicVegetationBridge.ToUniverseSpace(` call, no `Vector3 universePosition`, no Vector3 lane signatures for construction/defoliant, and no `(rootPosition - centerUniversePosition).sqrMagnitude`.
- Global legacy `HectonFloatingOrigin.ToAbsoluteUniversePosition(` scan remains clean under `Assets/_Project/Scripts --glob '*.cs'`.
- Direct committed-offset scan remains clean.
- Re-ran mandatory `rg "\(float3\).*AUP|AupOffset|universe"`; residual hits are broad `universe` text, editor diagnostics, final-cast fluid/scatter/shader payload names, and the new double-safe vegetation bridge/helper names.
- `git diff --check` on Loop 17 touched files reports no whitespace errors.
- `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:normal /m:1 /nr:false /p:UseSharedCompilation=false /flp:"logfile=Docs\AgentLogs\AUP_build_loop17.log;verbosity=normal"` completed in the log with 47 unrelated package warnings and 74 unrelated Core errors. Filtered build-log scan reports no errors or warnings for `DestructibleOrganicManager.cs` or `HectonMapMagicVegetationBridge.cs`.
