# AUP Drift Report

Agent: ARCHITECTURAL_AUP_INTEGRITY_AUDITOR
Status: VERIFIED AUP INTEGRITY - COMPILE/ASMDEF BLOCKED BY DEPENDENCY/ARCHITECTURE

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
- Voxel terrain-hole/spawn helper signatures now consume double committed offsets; remaining voxel `Vector3` absolute-position fields are compatibility/persistence boundaries pending a dedicated voxel storage migration.
- Fauna cognition `FloatingOriginOffset` now uses `double3`; remaining `AUPMath.ToRuntimeFloat3(..., float3 offset)` hits are smoke tests or unowned presentation/job payloads.
- Verify AUP shift consumer coverage across fluid, voxel, world streaming, scatter, foveated simulation, and GPR.
- Decide whether a future batch may introduce a true `Hecton8.Core.AUP` asmdef; current file placement prevents UnityEngine isolation.
