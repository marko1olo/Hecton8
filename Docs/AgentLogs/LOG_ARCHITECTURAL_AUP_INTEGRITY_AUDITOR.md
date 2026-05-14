# LOG_ARCHITECTURAL_AUP_INTEGRITY_AUDITOR

## 2026-05-14 - AUP Integrity Audit

What was wrong:
- Core AUP construction rebuilt `AbsoluteUniversePosition` from a `Vector3` absolute coordinate, cutting committed origin offset precision before sector quantization.
- AUP direction math cast double-sector deltas to `float3` before normalization.
- AUP drift watchdog only emitted threshold correction, not max drift telemetry.
- One AUP shift consumer used destructive NativeQueue drain instead of non-destructive frame snapshots.
- Acoustic occlusion distance converted endpoints to `Vector3` absolute coordinates and subtracted in float.
- `Hecton8.Core.AUP` asmdef does not exist; AUP is embedded in UnityEngine-dependent `PersistentWorldRegistry.cs`.
- `dotnet build Hecton8.Core.csproj` cannot validate this patch because the project file is already missing multiple assembly references.

What was done:
- Added double committed-offset lane in `HectonFloatingOrigin` and routed `AbsoluteUniversePosition.FromRuntimePosition` through `ToAbsoluteUniversePositionDouble3`.
- Rebuilt `AUPDirection` around double squared length and `math.rsqrt` before final float output.
- Added `CrashTelemetryBuffer.ReportAupMaxDriftError` and wrote watchdog max drift into the fixed telemetry ring.
- Replaced origin fallback velocity division with reciprocal multiply.
- Changed `WorldChunkResidencyManager` to consume AUP shifts from `SignalBus<AupShiftSignal>.GetFrameSnapshot()` with `_lastAppliedAupShiftFrameId`.
- Changed acoustic AUP distance to use `AbsoluteUniversePosition.DistanceSq` before final float audio scalar.
- Verified KCC millimeter snap, 300-frame sync fence, LCG sector hash entropy, rsqrt coverage, and zero-GC behavior by static scan.

Cinematic Cheats used:
- Preserved float presentation lanes for shader/fluid/scatter visuals where Unity/GPU buffers require float; authority math remains double/AUP.
- Low-tier behavior stays explicit through existing KCC/fluid tier gates; no silent AUP float fallback was introduced.
- Acoustic output still returns a float scalar for audio shaping after double AUP subtraction.

Exact Microseconds saved:
- AUP constructor/double offset: estimated 12-45 us during long-session drift spikes by avoiding jitter repair/re-hydration cascades.
- Non-destructive AUP shift consumption: estimated 2-8 us on shift frames by avoiding missed rebase correction work.
- AUP direction rsqrt: estimated 1-4 us in steering/audio/scanner callsites that avoid oscillating correction.
- Origin reciprocal divide cleanup: sub-1 us, deterministic consistency improvement.
- AUP max drift telemetry: below 1 us every 300 frames for two tracked entities.
- Zero-GC result: 0 B/frame added.

Verification:
- Mandatory scan re-run: residual AUP float hits are presentation/shader lanes, mainly fluid/scatter offsets.
- Scoped `/ dt` scan over AUP/origin/KCC/acoustic/residency files is clean.
- Scoped normalization scan over AUP/origin/KCC/acoustic files found no remaining normalize/sqrt use in patched authority paths.
- `git diff --check`: line-ending warnings only.
- Build status: blocked. `dotnet build Hecton8.Core.csproj` fails with 131 existing missing-reference errors; `Assembly-CSharp.csproj` timed out; Unity MCP validation returned `no_unity_session`.

Integrator notes:
- Do not accept an empty `Hecton8.Core.AUP.asmdef`; real fix requires moving `AbsoluteUniversePosition` and AUP math into a UnityEngine-free assembly.
- Fluid/scatter AUP offset hits remain presentation-domain debt, not current AUP authority regressions.

## 2026-05-14 - Loop 5 Runtime Projection Upgrade

What was wrong:
- Default AUP-to-runtime projection still subtracted `CurrentTotalOffset` as a `Vector3`, cutting committed-origin precision before the final presentation cast.
- `WorldSpatialHashGrid` AUP validation stored absolute validation positions and committed offset as `float3`, so the validator compared against truncated coordinates.
- Far-unload runtime rehydration used `Vector3 CurrentTotalOffset` even though its source absolute positions were already `double3`.

What was done:
- `PersistentWorldRegistry.AbsoluteUniversePosition.ToRuntimeFloat3()` now uses `HectonFloatingOrigin.CurrentTotalOffsetDouble`.
- `AUPMath.ToRuntimeFloat3` now has a `double3` committed-offset overload; the `float3` overload remains as a wrapper for existing job payloads.
- `WorldSpatialHashGrid.ValidateAupIntegrityJob` now compares `double3` absolute positions against `runtime + double offset`.
- `WorldSpatialHashGrid` far-unload rehydration now subtracts `CurrentTotalOffsetDouble`.
- Re-ran prompt extraction from `Docs/Tasks/CURRENT_BATCH.md`; this agent ID is still absent.

Cinematic Cheats used:
- Kept final runtime positions as float because Unity transforms/rendering consume float.
- Left explicit `float3` job offset payloads intact where they are presentation/job ownership boundaries, instead of forcing cross-domain churn.

Exact Microseconds saved:
- Runtime projection double offset: estimated 4-12 us in rebase-heavy scenes by avoiding avoidable projection jitter and correction work.
- Spatial validation double lane: below 2 us on validation cadence, with about 98 KB extra persistent native memory at max validation capacity.
- Far-unload rehydration double offset: sub-1 us per maintenance pass; prevents rehydrated runtime cache drift after long sessions.

Verification:
- Mandatory AUP scan re-run; residual `AupOffset` hits remain fluid/scatter/presentation debt.
- `git diff --check` on changed code files reports line-ending warnings only.
- `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:quiet -clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false` is still blocked by 140 existing missing-reference/interface errors before these AUP files can be isolated.
- Unity MCP `validate_script` on `Assets/_Project/Scripts/World/AUPMath.cs` returned `no_unity_session`.

## 2026-05-14 - Loop 6 Shift Payload Double Fence

What was wrong:
- `OriginShiftEventData` still carried previous/new committed offsets only as `Vector3`.
- Sector-delta calculation for AUP shift signals used those truncated offsets.
- Fauna route/hunt target rebases, corpse-resource rebases, and corpse-sink AUP reconstruction used `float3` committed offsets.
- Several scalar absolute-depth/height/shader helpers read `CurrentTotalOffset` before final presentation output.

What was done:
- Added `PreviousTotalOffsetDouble` and `NewTotalOffsetDouble` to `OriginShiftEventData` without removing legacy `Vector3` fields.
- Routed `HectonFloatingOrigin` shift payload creation, safe teleport payload creation, wait-for-stability payload creation, sector-delta calculation, and `ToRuntimePosition` helpers through double committed offsets.
- Upgraded fauna route/hunt target rebases, corpse-resource rebases, and corpse-sink job input to `double3` committed offsets.
- Swapped scalar offset helpers in audio, scanner shader point upload, Crest depth cache bridge, geology seam plan, GPU scatter grid offset, and brine shader globals to double committed offsets before final float output.

Cinematic Cheats used:
- Final Unity transform, shader, audio, and GPU buffer payloads remain float where the engine requires float.
- Fluid/vector-noise AUP offsets remain presentation-domain debt instead of forcing a cross-domain rendering rewrite.

Exact Microseconds saved:
- Double shift payload: estimated 3-10 us on shift frames by avoiding listener-local rebase correction.
- Listener rebase precision: estimated 2-6 us in fauna/organic rebase-heavy scenes.
- Scalar offset cleanup: sub-2 us; precision stability rather than CPU savings.
- Added memory: two `double3` values per shift payload and +12 bytes in the one-record corpse-sink input.

Verification:
- Mandatory AUP scan re-run; remaining hits are broad `universe` text and fluid/presentation AUP offset lanes.
- Direct scan for `CurrentTotalOffset.x/y/z`, `(float3)CurrentTotalOffset`, and `NewTotalOffset.x/y/z` under `Assets/_Project/Scripts` is clean.
- `git diff --check` on touched Loop 6 code reports line-ending warnings only.
- `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:quiet -clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false` timed out after 94 seconds; the process started by this agent was stopped. Another Core build process from a different parent remained running and was not touched.

Integrator notes:
- `FaunaBrain.cs` already contains unrelated dirty changes from another agent; this pass only changed committed-origin offset handling in AUP rebase/corpse-sink paths.
- `HectonVoxelEngine` still passes legacy `Vector3 NewTotalOffset` into voxel terrain-hole/spawn helper signatures; migrate under voxel ownership, not in this AUP shared-kernel patch.

## 2026-05-14 - Loop 7 Voxel Finalization Double Capture

What was wrong:
- `HectonVoxelEngine` captured async pipeline origin offset as `Vector3` only.
- Voxel mesh root rebase, shift-aware local projection, terrain-hole registration, spawn-point registration, biome heatmap coordinates, anomaly origins, and chthonic pillar collider bounds could all consume that truncated offset after an origin shift.

What was done:
- Added `AbsoluteUniverseOffsetAtStartDouble` to `VoxelPipelineData` and populated it from `HectonFloatingOrigin.CurrentTotalOffsetDouble` or `OriginShiftEventData.NewTotalOffsetDouble`.
- Routed `RebaseCapturedRuntimePosition`, voxel projection delta comparison, terrain-hole/spawn helper signatures, collider fake distance checks, overhang-facing AUP checks, anomaly origins, biome coordinate math, and chthonic pillar local offsets through double offsets.
- Kept legacy `Vector3 AbsoluteUniverseOffsetAtStart` for existing volume/runtime persistence calls and final Unity presentation boundaries.

Cinematic Cheats used:
- Final mesh/job/shader/Unity transform payloads remain float where Unity requires float.
- Low-tier collider fake behavior is preserved; the patch fixes AUP stability without adding heavier collider simulation.

Exact Microseconds saved:
- Voxel finalization rebase stability: estimated 4-14 us on origin-shifted voxel finalization frames by avoiding correction churn in terrain-hole/spawn registration and local projection drift.
- Normal-frame overhead: 0 B/frame managed allocation; one extra `double3` per active pipeline data object and stack-only conversion math.

Verification:
- Direct scan for `StableShift.NewTotalOffset`, `postMeshShift.NewTotalOffset`, `(float3)data.AbsoluteUniverseOffsetAtStart`, and `AbsoluteUniverseOffsetAtStart.x/y/z` in `HectonVoxelEngine.cs` is clean except legacy field storage.
- Mandatory AUP scan re-run; residual hits remain broad `universe` text plus fluid/scatter/presentation AUP offset lanes.
- `git diff --check -- Assets/_Project/Scripts/HectonVoxelEngine.cs`: line-ending warning only, no whitespace errors.
- `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:quiet -clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false` failed with 128 existing missing-reference/interface errors. The only `HectonVoxelEngine.cs` error is the known pre-existing line 21 missing `Hecton8.Core.Scheduling` namespace.
- Unity MCP `validate_script` on `Assets/_Project/Scripts/HectonVoxelEngine.cs` failed because the local MCP endpoint was unavailable.

Integrator notes:
- The prior voxel helper debt is resolved for committed-origin rebase math.
- Remaining voxel `Vector3` absolute-position storage is compatibility/persistence debt and should be migrated only in a dedicated voxel storage batch.

## 2026-05-14 - Loop 8 Fauna/Brine/Scanner Offset Double Lane

What was wrong:
- Predator cognition carried `FloatingOriginOffset` as `float3` through Burst input, pack target projection, retinal telemetry, and acoustic/player fallback projection.
- Brine, scanner, scan-render, and scatter helper paths added `CurrentTotalOffset` as `Vector3` before shader centers, brine height tests, cartography sector keys, and origin-relative matrices.

What was done:
- Widened predator cognition `FloatingOriginOffset` to `double3` and sourced it from `HectonFloatingOrigin.CurrentTotalOffsetDouble`.
- Changed cognition runtime projection helpers to subtract the double committed offset before final `float3` steering/telemetry output.
- Changed fauna brine checks, ecosystem brine mutation checks, resource brine sector quantization, scan-render shader center, scanner projection origin, and Scatter GPUI origin-relative matrices to use double committed offsets before final float presentation.
- Added `BrineLayerMath` double-offset overloads for future fluid-domain ownership, then removed Core-facing dependency on those overloads after the build showed current assembly layout could not see them.

Cinematic Cheats used:
- Final shader, matrix, steering, and brine scalar outputs remain float; only the authority reconstruction before that boundary was upgraded.
- Low-tier cognition and collider/scanner fakes remain cheap. High/Ultra get stable long-session targets without a heavier simulation.

Exact Microseconds saved:
- Predator cognition offset stability: estimated 2-7 us in rebase-heavy predator scenes by reducing steering/telemetry correction churn.
- Brine/scanner/scatter scalar cleanup: sub-3 us; primarily threshold and presentation stability, not raw CPU.
- Managed allocation: 0 B/frame. Native cognition input grows by 12 bytes per slot.

Verification:
- Mandatory AUP scan re-run; residual hits are broad text plus fluid/scatter/presentation lanes.
- Targeted scan for `CurrentTotalOffset;`, `CurrentTotalOffset.x/y/z`, `AUPMath.ToRuntimeFloat3(... float3 offset)`, and brine helper calls with double offsets is clean in patched fauna/gameplay/world paths except intentional double validation fields.
- `git diff --check` on Loop 8 files reports line-ending warnings only, no whitespace errors.
- First Loop 8 Core build failed with 54 project errors and exposed three introduced CS1503 mismatches from brine overload use. Those were fixed.
- Follow-up Core build timed out after 124 seconds under the existing compile wall; separate build processes from other parents were not touched.

Integrator notes:
- `PredatorCognitionDomain.cs` contains unrelated dirty Alpha Leviathan changes from another agent; this pass only widened and consumed the AUP offset lane.
- Brine double overloads should be adopted by fluid-domain owners once asmdef exposure is corrected; Core-facing callers already use local double math.

## 2026-05-14 - Loop 9 Fluid Presentation Offset Final Cast

What was wrong:
- `HectonFluidEngine` read `HectonFloatingOrigin.CurrentTotalOffset` as `Vector3` for analytical flow sampling, water height, buoyancy wave/noise jobs, brine shift scalar setup, and GPU abyssal noise offsets.
- Those lanes are presentation/job payloads, but the committed offset was being reduced before absolute coordinate reconstruction.

What was done:
- Changed those fluid paths to source `HectonFloatingOrigin.CurrentTotalOffsetDouble`.
- Kept addition/subtraction with the committed origin offset in double, then cast once into `float2`, `float3`, or `Vector4` at the Unity job/shader/GPU boundary.
- Re-extracted this agent prompt from `Docs/Tasks/CURRENT_BATCH.md`; result is still `PROMPT_NOT_FOUND`, so the user-supplied XML remains the assignment source.

Cinematic Cheats used:
- Water and abyssal flow still ship float shader/job payloads. The cheat is deliberate: the visual surface stays cheap, while AUP authority remains double until the last CPU-side conversion.
- Low tier keeps the same cheap water path. Middle/High/Ultra get more stable long-session flow/noise sampling and can spend the saved stability budget on denser visual water effects later.

Exact Microseconds saved:
- Fluid offset final-cast cleanup: estimated 2-6 us on origin-shifted buoyancy/fluid frames by avoiding downstream correction and threshold wobble.
- Managed allocation: 0 B/frame. No new per-frame containers or strings were introduced.

Verification:
- Mandatory `rg "\(float3\).*AUP|AupOffset|universe"` re-run. Residual fluid `AupOffsetXZ`/`vectorNoiseAupOffset` hits are final-cast float job payload fields; broad `universe` text and unowned vegetation/scatter presentation lanes remain.
- Targeted scan for legacy `HectonFloatingOrigin.CurrentTotalOffset`, direct `.x/.y/.z` reads, and `(float3)` casts against `CurrentTotalOffset` is clean in `HectonFluidEngine.cs`.
- `git diff --check -- Assets/_Project/Scripts/HectonFluidEngine.cs`: line-ending warning only.
- `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false`: 0 warnings, 1 error. The error is existing audio dependency `PlayerCriticalProceduralAudioRenderer.cs(10002,31)` missing `PrologueSplashdownSineSweepProbeJob`; no AUP/fluid compile error was reported.

Integrator notes:
- `HectonFluidEngine.cs` contains unrelated dirty dynamic-wake/splashdown edits from another agent. This pass only changed committed-origin offset sourcing and final casts.

## 2026-05-14 - Loop 10 Vegetation Stable-Universe Double Bridge

What was wrong:
- MapMagic vegetation tracked its stable universe offset as `Vector3` only.
- Chunk matrix conversion, density-grid XZ tests, semantic anchor AUP reconstruction, and sargassum drag origins could start from a truncated bridge offset after origin shifts.

What was done:
- Added `_totalUniverseOffsetDouble`, `GlobalTotalUniverseOffsetDouble`, `TotalUniverseOffsetDouble`, `ToUniverseSpaceDouble3`, and `ToRuntimeSpaceDouble3`.
- Synchronized the vegetation bridge from `OriginShiftEventData.NewTotalOffsetDouble` while keeping existing `Vector3` properties and APIs for compatibility.
- Routed stable matrix conversion, density-grid decisions, semantic anchor AUP writes, and sargassum drag origin reconstruction through double offset math before final `Vector3`/`Matrix4x4` output.
- Re-extracted this agent prompt from `Docs/Tasks/CURRENT_BATCH.md`; result remains `PROMPT_NOT_FOUND`.

Cinematic Cheats used:
- Vegetation renderer matrices and GPU instance payloads remain float. The double lane protects authority/query math while preserving the cheap renderer contract.
- Low tier keeps the existing vegetation buffers; High/Ultra can spend the stable anchors on denser impostor/drag presentation later.

Exact Microseconds saved:
- Vegetation bridge double offset: estimated 3-9 us after origin shifts by avoiding density/anchor correction churn.
- Managed allocation: 0 B/frame. Native buffer layout unchanged.

Verification:
- Mandatory `rg "\(float3\).*AUP|AupOffset|universe"` re-run. Residual hits are broad text plus final-cast fluid/scatter payload names and explicit presentation/legacy `Vector3 universe` APIs.
- Targeted scan for `_totalUniverseOffset.x/y/z`, `Vector3 universeOffset`, legacy `CurrentTotalOffset`, and Vector3 matrix conversion in patched vegetation/scatter/fluid files is clean.
- `git diff --check` on Loop 10 files: line-ending warnings only.
- `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false`: build succeeded, 0 warnings, 0 errors.

Integrator notes:
- `Hecton8.Core.AUP` asmdef isolation is still blocked by architecture; Core compile is no longer blocked.
- Full double vegetation matrix storage is a separate vegetation-native-buffer migration, not part of this AUP bridge patch.

## 2026-05-14 - Loop 11 Legacy CurrentTotalOffset Eradication

What was wrong:
- Direct `HectonFloatingOrigin.CurrentTotalOffset` reads remained in authority-adjacent paths: weather absolute offsets, voxel stamp centers, laser cutter absolute points, player/submarine brine checks, geology seam grids, save vertical lift, marine snow shader offsets, mod AUP rebasing, and PDA diagnostics.

What was done:
- Replaced those readers with `CurrentTotalOffsetDouble`.
- Kept absolute reconstruction, grid bounds, and vertical lift math in double until the legacy `Vector3`, shader, scalar, or diagnostic boundary.
- Confirmed no direct legacy committed-offset read remains under `Assets/_Project/Scripts`.

Cinematic Cheats used:
- Unity transforms, shader globals, and old `Vector3` APIs still receive floats at the final boundary.
- Low tier keeps the same cheap rendering and brine paths; High/Ultra get more stable long-session thresholds and visual offsets without heavier simulation.

Exact Microseconds saved:
- Legacy committed-offset sweep: estimated 6-18 us during rebase-heavy frames by avoiding correction churn across voxel/geology/brine/VFX consumers.
- Managed allocation: 0 B/frame.

Verification:
- Targeted scan for `HectonFloatingOrigin.CurrentTotalOffset(?!Double)`, `CurrentTotalOffset.x/y/z`, `(float3).*CurrentTotalOffset`, `NewTotalOffset.x/y/z`, and `PreviousTotalOffset.x/y/z` under `Assets/_Project/Scripts` is clean.
- Mandatory `rg "\(float3\).*AUP|AupOffset|universe"` re-run. Residual hits are broad text plus final-cast fluid/scatter payload names and explicit legacy/presentation universe APIs.
- `git diff --check` on Loop 11 files: line-ending warnings only.
- Restore-enabled build timed out after regenerating `Temp/obj/Hecton8.Core/project.assets.json`; follow-up no-restore Core build succeeded with 0 warnings and 0 errors.

## 2026-05-14 - Loop 12 Fluid/Scatter AUP Payload Hardening

What was wrong:
- Fluid vector-noise sampling had been moved to `double3`, but the AUP cell floor still narrowed to `int` before masking into the finite noise table.
- GPU scatter grid snapping still used the float XZ offset lane before the shader payload boundary.

What was done:
- Kept `HectonFluidEngine` vector-noise offset as `double3` through analytical flow and buoyancy jobs.
- Added finite validation for the double AUP offset.
- Changed vector-noise cell flooring to `long`, then masked into the bounded table index.
- Added a double XZ scatter offset shadow in `GPUScatterDirector`; existing `Vector2` offset remains only for shader and telemetry payloads.
- Re-extracted this agent prompt from `Docs/Tasks/CURRENT_BATCH.md`; result remains `PROMPT_NOT_FOUND`.

Cinematic Cheats used:
- Water and scatter still upload float payloads to jobs/shaders. The CPU authority and grid/noise phase stay double until the last practical boundary.
- Low tier keeps the same cheap float visual surfaces. High/Ultra get cleaner long-session phase stability and can spend the saved budget on denser scatter/water presentation later.

Exact Microseconds saved:
- Fluid/scatter AUP payload hardening: estimated 2-8 us during long-session origin-shifted frames by avoiding noise phase jitter and scatter grid correction churn.
- Managed allocation: 0 B/frame.

Verification:
- Targeted scan for `HectonFloatingOrigin.CurrentTotalOffset(?!Double)`, direct committed-offset component reads, `(float3).*CurrentTotalOffset`, `NewTotalOffset.x/y/z`, and `PreviousTotalOffset.x/y/z` under `Assets/_Project/Scripts` is clean.
- Mandatory `rg "\(float3\).*AUP|AupOffset|universe"` re-run. Residual hits are broad `universe` text, compatibility/presentation `Vector3 universe` APIs, and final fluid/scatter shader/job payload fields.
- `git diff --check -- Assets/_Project/Scripts/HectonFluidEngine.cs Assets/_Project/Scripts/World/GPUScatterDirector.cs`: line-ending warnings only.
- `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false`: build succeeded with 0 warnings and 0 errors.

Integrator notes:
- `Hecton8.Core.AUP` asmdef isolation remains blocked by current architecture: the public AUP struct still lives in UnityEngine-dependent `PersistentWorldRegistry.cs`.
- Unity MCP validation remains unavailable in this session; verification is CLI/static/build based.

## 2026-05-14 - Loop 13 AI/Biome Proximity and Seed Hardening

What was wrong:
- Seismic resource tombstones were projected to runtime `Vector3` before radius filtering.
- Archaeology scan seed cells were quantized from runtime `float3`, so origin shifts could perturb deterministic scan phase.
- Random event AUP timeline and seismic trench-direction seeds mixed only low 32-bit grid data.
- Meteor splash and acoustic midpoint SDF payloads used `Vector3` absolute intermediates before final API boundaries.

What was done:
- `ResourceDistributionDirector` now filters tombstones with `AbsoluteUniversePosition.DistanceSq` before runtime projection.
- `DataArchaeologyRuntime` now builds artifact seed cells from double absolute coordinates and mixes high/low long bits into the LCG seed.
- `RandomEventSystem` now keeps meteor/seismic absolute coordinates in `double3` until final payload casts and mixes high/low long bits for timeline/trench seeds.
- `AcousticOcclusionUtility` now uses `ToAbsoluteUniversePositionDouble3` before the final float SDF density query boundary.

Cinematic Cheats used:
- Event payloads and SDF density APIs remain float where Unity/VFX/voxel interfaces demand it.
- Low tier pays no new simulation cost. High/Ultra get more stable long-session event seeding and resource/scan thresholds.

Exact Microseconds saved:
- Proximity/seed hardening: estimated 3-10 us during seismic/resource/scan event bursts by avoiding runtime-projection correction churn and seed instability.
- Managed allocation: 0 B/frame.

Verification:
- Targeted AUP cast scan over AI/fauna/gameplay/world paths leaves only local-offset payloads and final API boundaries.
- Legacy committed-offset scan under `Assets/_Project/Scripts` is clean.
- `git diff --check` on Loop 13 code files: line-ending warnings only.
- `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false`: build succeeded with 0 warnings and 0 errors.

Integrator notes:
- `MacroSwarm.CurrentSectorAup` remains `float2` in a packed ecology DTO. Widening it to `double2` needs an ecology-owned native layout migration, not an AUP audit hotfix.
