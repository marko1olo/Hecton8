# Rationale_ARCHITECTURAL_AUP_INTEGRITY_AUDITOR

## Decision 0 - Authority Bootstrap

Problem: The batch protocol requires extracting `<AGENT_PROMPT id="ARCHITECTURAL_AUP_INTEGRITY_AUDITOR">` from `Docs/Tasks/CURRENT_BATCH.md`, but the file did not contain this ID during initial CLI extraction.
Solution: Treat the user-supplied XML block as the current assignment while recording the mismatch in status and reports. Continue mandatory scans because the user supplied the exact prompt inline.
Rejected Alternatives: Stop for a wipe or invent a neighboring batch prompt. Both violate evidence-based execution.
Scalability potential: Low/Middle/High/Ultra unchanged; this is process integrity, not runtime math.
Hardware Impact: 0 us runtime gain; prevents wrong-domain edits that could cost compile and integration time on low-end i3/MX350 workflows.

## Decision 1 - Mandate Set

Problem: AUP precision audit touches math authority, physics integration, deterministic hashing, job/native telemetry, and zero-GC constraints.
Solution: Selected eight mandates: AUP floating origin, CI math violations, deterministic RNG, rsqrt, physics integrity, zero-GC, native memory/jobs, and post-mortem telemetry.
Rejected Alternatives: Reading the whole registry. It wastes context and increases risk of mandate bleed from unrelated domains.
Scalability potential: Low uses coarse snap/fence hashes; Middle adds AUP samples; High adds render matrix samples; Ultra can emit full per-frame AUP telemetry.
Hardware Impact: Expected low-end benefit is avoiding float drift correction work and jitter compensation; exact microseconds pending static and profiler evidence.

## Decision 2 - Domain Boundary

Problem: The prompt requires cross-domain audit across Physics, Voxel, Kinematics, AI, Biomes, and rendering shift logic, but ownership rules forbid concrete cross-domain coupling.
Solution: Limit code changes to AUP/math kernels and interface/event telemetry boundaries. Any domain fix must preserve existing public APIs unless a legacy wrapper is added.
Rejected Alternatives: Directly wiring concrete systems together or introducing per-system dependencies on AUP manager internals. That breaks parallel agent isolation.
Scalability potential: Low relies on explicit tier checks before float fallback; High/Ultra spends saved cycles on tighter drift probes and visual continuity.
Hardware Impact: Avoids cache-inefficient coupling and registry polling in hot paths; exact i3/MX350 gain pending scans.

## Decision 3 - Double Offset Lane

Problem: `AbsoluteUniversePosition.FromRuntimePosition` reconstructed AUP from `HectonFloatingOrigin.ToAbsoluteUniversePosition(Vector3)`, forcing the committed origin offset through `Vector3` before sector quantization.
Solution: Added `HectonFloatingOrigin.CurrentTotalOffsetDouble` and `ToAbsoluteUniversePositionDouble3`, accumulated `_totalOffsetDouble` on every shift, and routed AUP construction through the double lane.
Rejected Alternatives: Replacing every legacy `Vector3 CurrentTotalOffset` consumer in one pass. That would cross multiple active domains and destabilize presentation systems.
Scalability potential: Low keeps float offsets for shaders and visual fakes; Middle/High/Ultra keep AUP authority in double and spend saved correction budget on denser telemetry or richer visual continuity.
Hardware Impact: Expected i3/MX350 gain is 12-45 us during long-session drift spikes by avoiding repeated jitter correction and rehydration misses after sector quantization.

## Decision 4 - Direction Kernel Rsqrt

Problem: `AUPDirection` cast the double AUP delta to `float3` before normalization, losing sector precision before distance math finished.
Solution: Calculate double squared length, use `math.rsqrt(lengthSq)`, normalize in double, then cast once for the render/steering payload.
Rejected Alternatives: `math.normalizesafe(float3)` and `Vector3.normalized`; both hide the precision cut before the final presentation boundary.
Scalability potential: Low may still consume final float direction; High/Ultra retain stable far-field direction vectors for AI, audio, and scanner presentation.
Hardware Impact: Expected i3/MX350 gain is 1-4 us in callsites that avoid oscillating steering corrections; no managed allocation added.

## Decision 5 - AUP Drift Telemetry

Problem: The watchdog only flagged threshold violation; it did not push `AupMaxDriftError` into the black-box stream for non-crashing analysis.
Solution: Compute max drift from NativeArray-backed watchdog buffers after job completion and write a non-faulting crash telemetry sample using existing ring-buffer fields.
Rejected Alternatives: Adding managed logs or allocating a new telemetry stream. Both violate zero-GC and black-box fixed-buffer rules.
Scalability potential: Low records one scalar every 300 frames; Middle/High can correlate with shift sequence; Ultra can later add more lanes without changing hot AUP authority.
Hardware Impact: Expected i3/MX350 cost is below 1 us every 300 frames for two tracked entities; gain is post-mortem visibility without live debug UI.

## Decision 6 - Compile Wall Classification

Problem: `dotnet build Hecton8.Core.csproj` fails on existing missing references to separate assemblies before validating the AUP edits.
Solution: Record the failure as a project-reference dependency wall after three verification attempts: Core csproj build, Assembly-CSharp build timeout, and Unity MCP validation unavailable.
Rejected Alternatives: Editing asmdefs/project references across unrelated domains or reverting unrelated existing changes in touched files.
Scalability potential: Runtime unaffected; process risk is contained for Low/Middle/High/Ultra because no fake green report is generated.
Hardware Impact: 0 us runtime gain; prevents high-cost integration churn on low-end development machines.

## Decision 7 - Non-Destructive AUP Shift Consumption

Problem: `WorldChunkResidencyManager` consumed `AupShiftSignal` through `GlobalSignals.TryDequeueAupShift`, a destructive queue path inconsistent with the frame-snapshot consumers used by fluid, ore, GPR, foveated simulation, and thermal systems.
Solution: Switched residency to `SignalBus<AupShiftSignal>.GetFrameSnapshot()` and added `_lastAppliedAupShiftFrameId` so repeated snapshots do not double-apply a shift.
Rejected Alternatives: Keeping the direct queue drain because it currently had no other queue consumers. That assumption fails under the stated 20+ agent parallel execution model.
Scalability potential: Low/Middle/High/Ultra all receive the same atomic shift view; high tiers can add more listeners without starving older consumers.
Hardware Impact: Expected i3/MX350 benefit is 2-8 us on shift frames by avoiding missed residency re-evaluation and later correction churn; normal frames pay no extra work.

## Decision 8 - Acoustic AUP Distance

Problem: Acoustic occlusion distance converted both endpoints to `Vector3` absolute positions and subtracted in float before distance estimation.
Solution: Convert endpoints to `AbsoluteUniversePosition`, use `AbsoluteUniversePosition.DistanceSq`, compute distance from double squared length with `math.rsqrt`, and only then return float for audio shaping.
Rejected Alternatives: Keeping `ApproximateMagnitude3D(float3(listenerAup - sourceAup))`; it is cheaper but not authoritative for long-session coordinates.
Scalability potential: Low still receives one float acoustic scalar; High/Ultra keep precise far-field occlusion and can spend saved stability budget on richer echo fakes.
Hardware Impact: Expected i3/MX350 cost is sub-1 us per acoustic distance query; benefit is removing jittery occlusion thresholds after origin drift.

## Decision 9 - Task 6-10 Scope Control

Problem: The scans exposed many float world/presentation paths, but only some are AUP authority. Patching every `Vector3 universe` lane would cross graphics, vegetation, voxel, and UI ownership.
Solution: Fix authority/shared math paths now; log presentation lanes as residual findings. KCC snap and `/ dt` scans are complete for AUP/KCC files; Math LOD is accepted only where explicit tier gates already exist.
Rejected Alternatives: Global replacement of presentation offsets or shader-space data with double/AUP structs. Unity rendering and GPU buffers consume floats by design.
Scalability potential: Low uses cheap presentation lanes with explicit tier gates; Middle/High/Ultra keep double authority and can increase visual overkill without corrupting AUP state.
Hardware Impact: Expected i3/MX350 gain is indirect: fewer rebase misses and less acoustic/AI threshold flicker; no GC introduced.

## Decision 10 - ASMDEF Isolation Block

Problem: The task requires `Hecton8.Core.AUP` with zero UnityEngine dependency, but the project contains no such asmdef or namespace, and `AbsoluteUniversePosition` lives inside `PersistentWorldRegistry.cs` alongside UnityEngine-dependent registry code.
Solution: Mark the task blocked by architecture and document the required migration instead of creating an empty compliance shell.
Rejected Alternatives: Empty `Hecton8.Core.AUP.asmdef` with no AUP code, or moving the public struct and all dependent files during an audit patch.
Scalability potential: Future Low/Middle/High/Ultra builds should benefit from a platform-neutral AUP package, but this batch cannot safely split it.
Hardware Impact: 0 us immediate runtime gain; prevents a high-risk compile break across every system that imports `Hecton8.World.AbsoluteUniversePosition`.

## Decision 11 - Zero-GC Verification

Problem: AUP fixes must not add managed hot-path churn.
Solution: Use a double field, stack `double3`, `ReadOnlySpan` snapshots, and existing NativeArray/ring-buffer storage only. No new per-frame List/Dictionary/string allocation was added by this patch set.
Rejected Alternatives: Managed audit reports, per-frame collections, or string telemetry.
Scalability potential: Low keeps memory pressure flat; High/Ultra can spend CPU/GPU budget on visual overkill without GC spikes.
Hardware Impact: Expected i3/MX350 gain is 0 B/frame and stable frame pacing; normal-frame CPU cost is sub-1 us.

## Decision 12 - Runtime Projection Offset Precision

Problem: `AbsoluteUniversePosition.ToRuntimeFloat3()` still pulled `HectonFloatingOrigin.CurrentTotalOffset` as `Vector3`, losing committed-origin precision before subtracting from the double absolute coordinate.
Solution: Route the default AUP-to-runtime projection through `CurrentTotalOffsetDouble` and a new `AUPMath.ToRuntimeFloat3(double3 committedOffset)` overload. Keep the `float3` overload as a compatibility wrapper for existing Burst/job payloads that intentionally store presentation offsets.
Rejected Alternatives: Replacing every direct `AUPMath.ToRuntimeFloat3(..., float3)` job input immediately. That would cross AI/fauna/vegetation ownership and risk destabilizing active agents.
Scalability potential: Low still receives final float runtime positions; Middle/High/Ultra preserve double offset authority until the last presentation cast, improving long-session visual stability without extra allocations.
Hardware Impact: Expected i3/MX350 benefit is 4-12 us during rebase-heavy scenes by avoiding avoidable runtime projection jitter and downstream correction churn.

## Decision 13 - Spatial Validation Double Integrity

Problem: `WorldSpatialHashGrid.ValidateAupIntegrityJob` stored validation absolute positions and the committed total offset as `float3`, so the validator could approve or reject spatial coherence against already-truncated AUP data.
Solution: Upgrade validation absolute positions and the job's committed offset to `double3`, reconstruct runtime+offset in double, and use `CurrentTotalOffsetDouble` for far-unload runtime rehydration.
Rejected Alternatives: Leaving the validator float-only because it is maintenance/debug logic. A precision validator that validates against truncated precision is not useful for AUP drift diagnosis.
Scalability potential: Low pays only persistent native buffer size increase in a maintenance lane; Middle/High/Ultra gain stronger black-box evidence for spatial drift without per-frame managed work.
Hardware Impact: Expected i3/MX350 cost is below 2 us on validation cadence and about 98 KB extra persistent native memory at max validation capacity; gain is fewer false rebase investigations and cleaner drift evidence.

## Decision 14 - Post-Upgrade Compile Recheck

Problem: A second compile was required after the Loop 5 AUP projection and spatial validation edits.
Solution: Ran `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:quiet -clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`; it still fails before AUP isolation on existing missing assembly/interface debt.
Rejected Alternatives: Rewiring unrelated assembly references or reporting a fake compile pass.
Scalability potential: Runtime unchanged; integration risk remains explicitly surfaced for all tiers.
Hardware Impact: 0 us runtime gain; prevents low-end developer machines from burning time chasing this patch as the source of unrelated missing-type failures.

## Decision 15 - Double Origin Shift Payload

Problem: `OriginShiftEventData` carried previous/new committed offsets only as `Vector3`, so every shift listener saw a truncated origin even though `HectonFloatingOrigin` now keeps `_totalOffsetDouble`.
Solution: Added `PreviousTotalOffsetDouble` and `NewTotalOffsetDouble`, routed origin shift creation and safe teleport creation through the double fields, and kept legacy `Vector3` properties for existing listeners.
Rejected Alternatives: Changing all listener signatures or deleting the `Vector3` properties. That would break broad domain ownership during an audit pass.
Scalability potential: Low still gets float transform presentation; Middle/High/Ultra keep stable double offset metadata for AUP rebases, black-box tags, and future listener upgrades.
Hardware Impact: Expected i3/MX350 benefit is 3-10 us on shift frames by avoiding listener-local rebase drift and later correction churn; runtime memory increase is two `double3` values per shift payload.

## Decision 16 - Listener Rebase Precision

Problem: Fauna route/hunt target rebases, corpse-resource rebases, and corpse-sink AUP reconstruction consumed committed offsets as `float3`.
Solution: Use `shiftData.NewTotalOffsetDouble` for listener rebases and store the corpse-sink job's `FloatingOriginOffset` as `double3`, casting only after AUP-to-runtime projection.
Rejected Alternatives: Leaving AI/organic rebases as float because the final transforms are float. The distance and reconstruction math still needs the committed offset in double before the last presentation cast.
Scalability potential: Low avoids target jitter on long sessions; High/Ultra can layer richer predator and corpse presentation without unstable target anchors.
Hardware Impact: Expected i3/MX350 benefit is 2-6 us in rebase-heavy fauna/organic scenes; corpse-sink input grows by 12 bytes for one persistent record.

## Decision 17 - Scalar Offset Cleanup

Problem: Several absolute-depth/height/shader helpers added runtime values to `CurrentTotalOffset` as `Vector3` before final float presentation output.
Solution: Swapped those paths to `CurrentTotalOffsetDouble` or a double sum, then cast at the final shader/audio/geology presentation boundary.
Rejected Alternatives: Rewriting shader/fluid/scatter architecture to double. Unity shader globals and GPU scatter buffers remain float presentation lanes by design.
Scalability potential: Low keeps cheap shader payloads; Middle/High/Ultra get more stable absolute-depth and grid-offset inputs after long play sessions.
Hardware Impact: Expected i3/MX350 benefit is sub-2 us; value is precision stability, not raw CPU savings.

## Decision 18 - Loop 6 Verification Wall

Problem: A fresh build check was required after Loop 6, but repo-wide concurrent build work is active.
Solution: Ran the Core build command with a 90 second cap; it timed out after 94 seconds and the specific process started by this agent was stopped. A separate Core build process with a different parent remained running and was not touched.
Rejected Alternatives: Killing all `dotnet`/MSBuild processes or pretending the timeout is a compile pass.
Scalability potential: Runtime unchanged; process containment protects parallel agents.
Hardware Impact: 0 us runtime gain; avoids wasting low-end workstation time on runaway duplicate build processes.

## Decision 19 - Voxel Finalization Double Capture

Problem: `HectonVoxelEngine` async finalization captured the origin offset as `Vector3` and later rebased mesh roots, terrain holes, spawn points, local projection buffers, biome coordinates, and anomaly bounds from that truncated value.
Solution: Preserve `AbsoluteUniverseOffsetAtStartDouble` in `VoxelPipelineData` while keeping the legacy `Vector3` field for volume/runtime API compatibility. Use the double lane for rebase comparisons, `OriginShiftEventData` rebases, terrain-hole/spawn runtime reconstruction, AUP distance checks, anomaly origins, biome coordinate subtraction, and chthonic pillar bounds; cast only at Unity transform/job/shader boundaries.
Rejected Alternatives: Replacing the public voxel volume absolute-position API with `double3` in this pass. That crosses persistence, delta, and vegetation ownership and risks a compile wall beyond the AUP audit boundary.
Scalability potential: Low keeps final float mesh/terrain/spawn payloads and cheap collider fakes; Middle/High keep stable async AUP reconstruction; Ultra can spend the stability budget on denser anomaly and seam detail without rebase jitter.
Hardware Impact: Expected i3/MX350 benefit is 4-14 us on origin-shifted voxel finalization frames by avoiding terrain-hole/spawn re-registration correction and mesh-local projection drift; normal frames add one `double3` per pipeline data object and stack-only conversion math.

## Decision 20 - Loop 7 Verification Wall

Problem: The voxel patch required compile verification, but the Core project still fails on missing cross-assembly namespaces and interfaces before a clean compile can be reached.
Solution: Re-ran the constrained Core build with node reuse/shared compilation disabled. It failed with 128 existing errors; the only `HectonVoxelEngine.cs` error is the known line 21 missing `Hecton8.Core.Scheduling` namespace seen in earlier logs. Unity MCP script validation was attempted and failed because `http://127.0.0.1:8088/mcp` was unavailable.
Rejected Alternatives: Fixing unrelated assembly ownership or killing orphaned build nodes from other processes. Both violate the domain boundary and parallel-agent rules.
Scalability potential: Runtime unchanged; verification evidence is honest for Low/Middle/High/Ultra rather than masking project dependency debt.
Hardware Impact: 0 us runtime gain; prevents low-end developer machines from chasing this voxel precision patch as the source of existing compile-wall errors.

## Decision 21 - Fauna Cognition Double Offset Lane

Problem: Predator cognition and compatibility paths still carried `FloatingOriginOffset` as `float3`, so AUP-backed pack targets, retinal light telemetry, acoustic ping runtime projection, and player-target fallbacks could lose committed-origin precision before Burst scoring.
Solution: Widen `CognitionInput.FloatingOriginOffset` to `double3`, source it from `HectonFloatingOrigin.CurrentTotalOffsetDouble`, and subtract that double offset in cognition runtime projection helpers before final `float3` steering/telemetry output.
Rejected Alternatives: Keeping cognition float-only because its final steering positions are float. The AUP-to-runtime projection is still distance/decision input, not just rendering.
Scalability potential: Low keeps float steering outputs and existing low-tier fallback flags; Middle/High/Ultra get stable long-session pack and retinal target projection without introducing managed allocations.
Hardware Impact: Expected i3/MX350 benefit is 2-7 us in rebase-heavy predator scenes by reducing steering/telemetry correction churn; native input memory increases by 12 bytes per cognition slot.

## Decision 22 - Brine/Scanner Presentation Boundary Cleanup

Problem: Several scanner, scatter, brine, and ecosystem helpers added `CurrentTotalOffset` as `Vector3` before shader centers, brine height tests, cartography sector quantization, and origin-relative matrices.
Solution: Use `CurrentTotalOffsetDouble` for the absolute reconstruction and cast only at shader, matrix, and float scalar output. Core-facing brine callers perform local double subtraction because current project assembly layout did not expose the new `BrineLayerMath` overloads to the Core build.
Rejected Alternatives: Forcing a cross-assembly brine API migration or switching GPU/shader payloads to doubles. Unity shader and matrix paths are float presentation surfaces.
Scalability potential: Low preserves cheap brine/scanner presentation; Middle/High/Ultra avoid long-session sector/height wobble and can spend stable presentation budget on denser scan effects.
Hardware Impact: Expected i3/MX350 benefit is sub-3 us; the primary gain is stable thresholds and scan centers after long sessions, with 0 B/frame managed allocation.

## Decision 23 - Loop 8 Verification Wall

Problem: First Loop 8 Core build exposed three type mismatches caused by relying on new `BrineLayerMath` double overloads that the Core project could not see through current assembly layout.
Solution: Fixed the callers to do local double reconstruction without depending on those overloads, re-ran targeted scans, and re-ran the constrained Core build. The follow-up build timed out after 124 seconds under the existing compile wall; a separate build process from another parent was left untouched.
Rejected Alternatives: Rewiring asmdefs or killing unrelated build processes. Both are outside AUP audit ownership.
Scalability potential: Runtime unchanged beyond the Loop 8 precision fixes; verification remains dependency-blocked for all tiers.
Hardware Impact: 0 us runtime gain from the verification step; prevents an introduced CS1503 mismatch from surviving while still documenting the unresolved project wall.

## Decision 24 - Fluid AUP Offset Final Cast Boundary

Problem: `HectonFluidEngine` still sourced fluid flow, water height, buoyancy wave/noise, brine shift, and GPU abyssal noise offsets from `HectonFloatingOrigin.CurrentTotalOffset` as `Vector3`, then fed those values into distance/noise coordinates that survive across origin shifts.
Solution: Source those offsets from `CurrentTotalOffsetDouble`, perform runtime + committed-origin sums in double, and cast once into `float2`/`float3`/`Vector4` where Unity jobs, shader globals, or GPU buffers require float payloads.
Rejected Alternatives: Converting the analytical flow jobs, shader uniforms, and GPU structured buffers to double. Unity GPU/shader surfaces are float presentation boundaries, and changing them would be expensive without improving AUP authority.
Scalability potential: Low keeps cheap float shader/job payloads after explicit final casts; Middle/High/Ultra get stable long-session wave/noise/flow sampling without heavier water simulation.
Hardware Impact: Expected i3/MX350 benefit is 2-6 us in origin-shifted buoyancy/fluid frames by avoiding correction churn and threshold wobble; managed allocation remains 0 B/frame.

## Decision 25 - Loop 9 Verification

Problem: The fluid patch needed proof that it did not introduce C# errors and that residual `AupOffset` names are no longer sourced from a legacy float committed offset.
Solution: Ran targeted legacy-offset scans, the mandatory AUP regex scan, `git diff --check`, and a constrained Core build. The build reached a single existing audio dependency error with 0 warnings and no AUP/fluid compile errors reported.
Rejected Alternatives: Reporting the build as green or chasing `PrologueSplashdownSineSweepProbeJob` from the audio domain inside an AUP audit loop.
Scalability potential: Runtime unchanged beyond the Loop 9 precision fixes; build risk is isolated for Low/Middle/High/Ultra because the remaining error is a missing audio job type.
Hardware Impact: 0 us runtime gain from verification itself; prevents low-end developer time loss by separating this patch from an unrelated compile-wall error.

## Decision 26 - Vegetation Stable-Universe Double Bridge

Problem: MapMagic vegetation used `_totalUniverseOffset` and `GlobalTotalUniverseOffset` as `Vector3` for stable universe conversion, chunk matrix conversion, density grids, semantic anchor AUP reconstruction, and sargassum drag origins.
Solution: Add `_totalUniverseOffsetDouble`, `GlobalTotalUniverseOffsetDouble`, `TotalUniverseOffsetDouble`, and double conversion helpers. Sync the bridge from `OriginShiftEventData.NewTotalOffsetDouble`, use double offset math for stable matrix conversion and query decisions, then cast only at `Vector3`, `Matrix4x4`, or renderer payload boundaries.
Rejected Alternatives: Replacing all vegetation matrix storage with double. Unity matrices, GPU instance data, and renderer payloads are float surfaces; changing them would be a broad vegetation storage migration outside the AUP bridge fix.
Scalability potential: Low keeps cheap float instance buffers; Middle/High/Ultra get stable long-session vegetation anchors, density grids, and drag fields without increasing renderer payload cost.
Hardware Impact: Expected i3/MX350 benefit is 3-9 us after origin shifts by reducing vegetation density/anchor correction churn; managed allocation remains 0 B/frame and native buffer layout stays unchanged.

## Decision 27 - Loop 10 Core Build Pass

Problem: Earlier Core builds were blocked by missing references and then by an audio job dependency, preventing a clean compile verdict for the cumulative AUP patch set.
Solution: Re-ran the constrained Core build after Loop 10. `Hecton8.Core.csproj` built successfully with 0 warnings and 0 errors.
Rejected Alternatives: Continuing to label compile as blocked after the local evidence changed, or expanding the verification scope into Unity Editor MCP while the endpoint remains unavailable.
Scalability potential: Runtime unchanged; the integration gate is now factual for Low/Middle/High/Ultra Core code.
Hardware Impact: 0 us runtime gain from verification; saves low-end developer iteration time by clearing the AUP patch set from compile suspicion.

## Decision 28 - Chemical Influence Double Breadcrumbs

Problem: `ChemicalInfluenceGrid` mixed runtime positions into absolute chemical breadcrumbs and defoliant dead zones, then stored the authoritative centers as `float3`/`Vector4` before distance checks and scent-grid cell resolution.
Solution: Add `double3 AbsolutePositionDouble` to `ChemicalBreadcrumbWaypoint`, preserve double defoliant centers in a fixed `double3[64]` side lane, and route merge, sample, nearest-waypoint, dead-zone, and grid-cell math through double subtraction before final legacy presentation/storage casts.
Rejected Alternatives: Replacing every existing AI-facing breadcrumb API with a new AUP struct. That would break consumers during a precision audit and add cross-domain churn. Keeping float breadcrumbs was rejected because chemical proximity is trigger math, not presentation.
Scalability potential: Low keeps the same 64 breadcrumb cap and byte scent grid; Middle/High/Ultra get stable long-session chemical trails and defoliant gates without heavier simulation.
Hardware Impact: Expected i3/MX350 cost is below 3 us on chemical query frames and 0 B/frame managed allocation. Expected gain is fewer false scent/defoliant threshold flips after origin shifts.

## Decision 29 - Splash, Acoustic, And Terrain Query AUP Reconstruction

Problem: Splash anchors, acoustic SDF midpoint sampling, and wreck terrain-height queries still used legacy `ToAbsoluteUniversePosition(Vector3)` in selected callsites, reducing the committed offset before seeds, persistent payloads, or absolute queries were built.
Solution: Convert those callsites to `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3`, keep seed/position calculations in double, and cast only into existing float payloads for shader, VFX, audio, and MapMagic API boundaries.
Rejected Alternatives: Converting shader/VFX/MapMagic payload contracts to doubles. Those surfaces are presentation or third-party API boundaries; the correct repair is keeping CPU authority in double until the final required cast.
Scalability potential: Low keeps cheap splash and acoustic payloads; Middle/High/Ultra gain stable persistent anchors and deterministic splash seeds that can support denser visual effects without AUP wobble.
Hardware Impact: Expected i3/MX350 benefit is 1-5 us in splash/acoustic bursts by avoiding seed churn and post-shift correction. Managed allocation remains 0 B/frame.

## Decision 30 - Wreck Burial Voxel Cut Double Center

Problem: Procedural wreck burial cuts queued voxel surgeon box centers as `float3 AbsoluteCenter`, so an origin-shifted wreck could feed truncated AUP centers into voxel crater cuts.
Solution: Replace the record center with `double3 AbsoluteCenter` while preserving the 64-byte record size, populate it from `ToAbsoluteUniversePositionDouble3`, and submit directly to the voxel delta processor's `double3` box-crater overload.
Rejected Alternatives: Adding a second native side buffer or increasing the record size. The existing record had enough reserved space to preserve double precision without extra buffers, allocations, or cache-thrashing metadata.
Scalability potential: Low keeps the existing placement cap and voxel cut count; High/Ultra get stable buried-wreck excavation cuts and can spend saved correction budget on richer debris presentation.
Hardware Impact: Expected i3/MX350 benefit is 2-6 us on buried-wreck voxel cut frames by avoiding misaligned crater retries. Memory footprint remains 64 bytes per record and 0 B/frame managed allocation.

## Decision 31 - Loop 14 Verification Wall

Problem: Loop 14 required compile verification after the AUP repairs, but the current Core build is blocked by active unrelated dependency changes outside the touched files.
Solution: Run mandatory and targeted scans, `git diff --check`, and a constrained Core build with a binary log file. Then filter the build log for every Loop 14 touched file before classifying the failure.
Rejected Alternatives: Reporting a fake compile pass, chasing `HardwareProfileCatalog`, save-header, or `SystemID`/`JobHandle` errors outside this agent's AUP boundary, or reverting other agents' dirty files.
Scalability potential: Runtime unchanged; evidence isolation protects Low/Middle/High/Ultra integration by proving the AUP patch is not the current compile blocker.
Hardware Impact: 0 us runtime gain from verification. Developer-time gain on low-end machines is avoiding false attribution of 60 unrelated Core errors to this AUP patch.

## Decision 32 - Construction Rupture Double AUP State

Problem: Construction rupture/decal logic reconstructed module and rupture positions through legacy `Vector3` absolute AUP before comparing state and building outward decal vectors.
Solution: Preserve `RuptureNodeState.AbsoluteUniversePositionDouble`, compare prior/current rupture anchors in double, and calculate rupture-vs-module outward vectors from `ToAbsoluteUniversePositionDouble3` before the final `Vector3` decal matrix boundary.
Rejected Alternatives: Replacing all construction rupture public payloads with `double3`. Existing consumers and serialized/debug surfaces expect `Vector3`; a wrapper double lane keeps compatibility while removing authority drift.
Scalability potential: Low keeps the same decal/VFX payload and cheap visual fracture fake. Middle/High/Ultra get stable long-session rupture anchors and can spend the stability budget on denser crack VFX without changing gameplay truth.
Hardware Impact: Expected i3/MX350 benefit is 1-4 us on rupture/decal update frames by avoiding repeated state churn from float AUP comparison jitter. Managed allocation remains 0 B/frame.

## Decision 33 - Drone Voxel Ingress Double Boundary

Problem: Drone repair and plasma cut dispatch converted ray hits to legacy `Vector3` AUP before feeding voxel DDA and spark events, truncating the committed origin offset before voxel authority work.
Solution: Add `double3` overloads for `HectonVoxelVolume.ApplyPlasmaCutDda` and `ApplyRepairWeldDda`, route `DroneFleetManager` through those overloads, and convert spark payloads to `AbsoluteUniversePosition` from the double hit point.
Rejected Alternatives: Removing the `Vector3` overloads or changing all tool callers in one pass. Legacy wrappers are required to avoid broad public API churn across active tool/interaction agents.
Scalability potential: Low keeps the same DDA cost and VFX payload. High/Ultra can layer denser weld/cut particles because the hit anchor no longer drifts after origin shifts.
Hardware Impact: Expected i3/MX350 benefit is 2-6 us on drone voxel edit bursts by avoiding misaligned DDA retries and spark correction. No per-frame managed allocation or native buffer growth.

## Decision 34 - Seismic Event Double Line Payload

Problem: Meteor splash and seismic shockwave events produced persistent splash/trench AUP anchors from `Vector3` absolute positions; geology replay then computed trench length, direction, and ids after precision loss.
Solution: Add `AupStartDouble` and `AupEndDouble` to `SeismicShockwaveEvent`, generate splash/trench anchors with `ToAbsoluteUniversePositionDouble3`, fold rounded double coordinates into deterministic uint seeds, and consume the double line in `WorldGenerativeGeologyVoxelBridgeDirector` before final legacy voxel plan casts.
Rejected Alternatives: Making all random/geology event payload fields double-only. Existing event consumers still expect legacy `Vector3` fields; dual lanes preserve compatibility and keep the authority path 64-bit.
Scalability potential: Low keeps the same cheap seismic trench fake and voxel plan fields. Middle/High/Ultra gain deterministic long-session trench placement and can spend saved stability on richer debris/dust presentation.
Hardware Impact: Expected i3/MX350 benefit is 2-8 us on seismic event execution by avoiding trench-id churn and line-length correction after origin shifts. Event struct grows by two `double3` lanes but remains stack/blittable usage; managed allocation remains 0 B/frame.

## Decision 35 - Loop 15 Verification Wall

Problem: Loop 15 compile verification initially exposed one local import error, then returned to the active unrelated Core dependency wall.
Solution: Fix the local `DeepDrillModule.cs` missing `Unity.Mathematics` import, re-run the constrained Core build, and filter the build log for every Loop 15 touched file. The after-fix build has 0 warnings and no errors in touched files; remaining 60 errors are save-layout, hardware-profile, and scheduler-handle dependencies.
Rejected Alternatives: Leaving the local CS0246 error in place, reporting a fake green build, or chasing unrelated save/core-memory ownership from the AUP auditor domain.
Scalability potential: Runtime unchanged beyond the Loop 15 fixes; verification evidence keeps Low/Middle/High/Ultra integration risk scoped to actual blockers.
Hardware Impact: 0 us runtime gain from verification. Developer-time gain on low-end machines is preventing a local AUP import error from being buried under unrelated dependency failures.

## Decision 36 - Interaction And Tool Packet Final-Cast Boundary

Problem: Player tools, physical switches, panel buttons, equipment interaction packets, and repair welds converted runtime hit origins to legacy `Vector3` AUP before packet, voxel, or debris-spark publication.
Solution: Route those producers through `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3`, validate double finiteness where needed, and cast only into existing `float3` packet/presentation fields or `AbsoluteUniversePosition.FromAbsolutePosition`.
Rejected Alternatives: Changing `InteractionPacket` and all interaction service contracts to double in this audit pass. That would mutate public interfaces across active interaction/tool agents. The safer DOD pattern is a double authority lane with legacy final-cast wrappers.
Scalability potential: Low keeps cheap float interaction packets and haptics. Middle/High/Ultra get stable long-session hit anchors and can spend saved stability on denser panel/spark feedback without changing the event contract.
Hardware Impact: Expected i3/MX350 benefit is 2-7 us on interaction/repair bursts by avoiding hit-anchor correction and voxel weld retry churn. Managed allocation remains 0 B/frame.

## Decision 37 - Geology Plan Double Runtime Keys

Problem: `WorldGenerativeGeologyIntegrationDirector` built retained plan centers, terrain heights, voxel centers, and fallback runtime keys after legacy `Vector3` AUP reconstruction.
Solution: Reconstruct plan world/terrain/voxel centers as `double3`, create AUP structs from absolute double positions, and build fallback runtime keys from rounded double millimeters with an FNV-style long hash before final legacy `Vector3` plan fields.
Rejected Alternatives: Converting `WorldGenerativeGeologySeamPlan` storage wholesale to double. The plan struct is consumed by voxel, terrain, gizmo, and compatibility code; widening it is a separate world-geometry contract migration.
Scalability potential: Low keeps the existing cheap seam-plan payload. High/Ultra get stable deterministic plan retention and can spend saved churn on richer seam/debris presentation.
Hardware Impact: Expected i3/MX350 benefit is 3-9 us on plan refreshes by avoiding key churn and repeated plan rebuilds after origin shifts. No managed allocation was added.

## Decision 38 - Presentation Helper Double Bridge

Problem: MapMagic shader origins, Crest depth helper points, scatter helper positions, localized sign rebases, player build ghost projection, crash telemetry fallback, submarine leak impact signals, and spatial audio listener fallback still used legacy AUP reconstruction.
Solution: Keep double AUP until the required shader `Vector4`, Unity `Vector3`, telemetry `float3`, or `AbsoluteUniversePosition` boundary. Localized signs now retain a double AUP side lane for origin-shift projection while preserving the legacy `Vector3` field.
Rejected Alternatives: Converting shader globals, Unity transforms, and third-party helper surfaces to doubles. Those endpoints are float presentation surfaces; the correct fix is final-cast discipline, not GPU/API contract mutation.
Scalability potential: Low keeps cheap shader/transform/telemetry payloads. Middle/High/Ultra get stable origins for richer terrain fades, depth effects, signage, scatter, audio, and leak feedback.
Hardware Impact: Expected i3/MX350 benefit is 1-5 us across shift-heavy frames by avoiding presentation correction churn. Crash telemetry remains fixed-buffer and zero-GC.

## Decision 39 - Loop 16 Verification Wall

Problem: After the global legacy HFO AUP cleanup, the Core build still fails under active unrelated dependency changes.
Solution: Re-run the global `HectonFloatingOrigin.ToAbsoluteUniversePosition(` scan, mandatory AUP regex, direct committed-offset scan, `git diff --check`, and constrained Core build. Then filter the build log for every Loop 16 touched file before classifying the result.
Rejected Alternatives: Chasing residency/power/fauna/core save-layout/hardware-profile/scheduler errors outside the AUP auditor domain, or reporting a fake green build.
Scalability potential: Runtime unchanged beyond Loop 16 fixes; verification evidence proves Low/Middle/High/Ultra AUP edits are not the current compile blocker.
Hardware Impact: 0 us runtime gain from verification. Developer-time gain on low-end machines is avoiding false attribution of 74 unrelated Core errors to this AUP patch.

## Decision 40 - Organic Vegetation Trigger Double Space

Problem: `DestructibleOrganicManager` construction decomposition and defoliant dead-zone checks converted runtime centers into stable vegetation universe coordinates as `Vector3`, then did radius math against matrix roots and giant-kelp segments in float.
Solution: Route trigger centers through `HectonMapMagicVegetationBridge.ToUniverseSpaceDouble3`, compare construction/defoliant distance in double, add a double closest-point segment helper using `math.rcp`, and project titan root mound anchors through a new `ToRuntimeSpace(double3)` bridge overload before the final voxel lookup cast.
Rejected Alternatives: Widening all vegetation matrix storage and public renderer contracts to double. Unity matrices, GPU instance payloads, and renderer contracts are float presentation surfaces; the authority leak was the trigger math and bridge hop, not the storage surface itself.
Scalability potential: Low keeps existing matrix/renderer payloads and cheap trigger loops. Middle/High/Ultra get stable long-session construction cleanup, chemical dead-zone tombstoning, and titan root mound placement without increasing GPU payload size; saved stability can buy denser decomposition, wilt, and mound VFX.
Hardware Impact: Expected i3/MX350 benefit is 2-7 us on construction/defoliant bursts by avoiding float-distance threshold churn and reprocessing near boundaries. Managed allocation remains 0 B/frame; changes use stack `double3`, existing NativeArrays, and existing compatibility wrappers.

## Decision 41 - Loop 17 Verification Wall

Problem: Loop 17 needed compile verification, but the current Core build still fails under unrelated dependency work and package warnings outside the touched vegetation/AUP files.
Solution: Run mandatory AUP scan, targeted vegetation scans, global legacy HFO scan, direct committed-offset scan, `git diff --check`, and a constrained Core build log, then filter the build log for `DestructibleOrganicManager.cs` and `HectonMapMagicVegetationBridge.cs`.
Rejected Alternatives: Reporting the build as green because touched files have no local errors, or chasing save-layout, scheduler-handle, hardware-profile, power, fauna, and package warning debt from the AUP auditor domain.
Scalability potential: Runtime unchanged beyond the Loop 17 precision repair; verification keeps Low/Middle/High/Ultra risk scoped to actual blockers instead of burying precision fixes under unrelated compile noise.
Hardware Impact: 0 us runtime gain from verification itself. Developer-time gain on low-end machines is avoiding false attribution of 74 unrelated Core errors and 47 package warnings to the organic vegetation AUP patch.

## Decision 42 - Large-Flora Collision Proxy Double Cache

Problem: Large-flora collision proxies cached universe centers as `Vector3` and compared player/proxy/candidate distances with `.sqrMagnitude`, so collider activation and deactivation could drift around threshold boundaries after long sessions.
Solution: Store proxy universe centers as `double3`, resolve player and candidate centers through `ToUniverseSpaceDouble3`, use `math.lengthsq(double3)` for activation/deactivation tests, and rebase proxy transforms through `ToRuntimeSpace(double3)` only at the final transform boundary.
Rejected Alternatives: Replacing proxy transforms, BoxColliders, or pooled GameObject contracts with double-aware physics objects. Unity physics and transforms are float runtime surfaces; the correct repair is keeping the proxy decision cache in double and casting only for the existing collider transform.
Scalability potential: Low keeps the same 24 default proxy pool and cheap scan budget. Middle/High/Ultra get stable long-session proxy activation and can spend the saved churn on denser collision/interaction feedback near large coral without expanding the renderer payload.
Hardware Impact: Expected i3/MX350 benefit is 1-4 us on proxy scan/deactivation frames by avoiding threshold churn and repeated pool despawn/spawn near boundaries. Memory increases by 12 bytes per proxy slot at the default 24-slot pool; managed allocation remains cold-init only and 0 B/frame.

## Decision 43 - Loop 18 Verification Wall

Problem: Loop 18 needed compile verification, but the Core build still fails under unrelated save-layout, scheduler, hardware-profile, fauna, power, and package warning debt.
Solution: Run prompt extraction, mandatory AUP scan, targeted proxy scan, global HFO scan, direct committed-offset scan, `git diff --check`, and a constrained Core build log; then filter the build log specifically for `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`.
Rejected Alternatives: Chasing unrelated Core/package errors from the AUP auditor domain, or stopping at the timed-out shell call while the build process continued to completion in the log.
Scalability potential: Runtime unchanged beyond the Loop 18 precision repair; verification keeps Low/Middle/High/Ultra risk tied to the proxy cache changes rather than unrelated active dependency work.
Hardware Impact: 0 us runtime gain from verification itself. Developer-time gain on low-end machines is preventing one proxy-cache patch from being blamed for the current 74 unrelated Core errors and 47 unrelated package warnings.

## Decision 44 - Voxel Nav Macro-Flora Root Double Projection

Problem: `VoxelDynamicNavGridRuntime.TryResolveMacroFloraObstacleWorldBounds` extracted a stable vegetation matrix translation into `Vector3` and then called the vegetation bridge runtime projection. That reduced the stable universe root before the macro-flora obstacle center was emitted to nav-grid runtime bounds.
Solution: Capture the matrix translation as `double3` and call `HectonMapMagicVegetationBridge.ToRuntimeSpace(double3)`, preserving the bridge offset in 64-bit until the final `Vector3`/`float3` nav-bound output.
Rejected Alternatives: Widening the nav-grid obstacle record and passability payload to double. Those surfaces are runtime grid contracts and Burst float payloads; the precision leak was the bridge hop, not the final nav voxel representation.
Scalability potential: Low keeps the cheap macro-flora obstacle fake and existing grid payloads. Middle/High/Ultra get steadier long-session obstacle placement around kelp, coral, and sargassum without heavier nav-grid memory.
Hardware Impact: Expected i3/MX350 benefit is sub-2 us on macro-flora obstacle resolution frames by reducing obstacle-bound churn after origin shifts. Managed allocation remains 0 B/frame; only stack `double3` was added.

## Decision 45 - H-Phi Scope Classification

Problem: The user requested H-Phi improvement, but the only direct H-Phi runtime file found by targeted scan is `HphiReactiveUiTelemetry`, a UI performance warning publisher with no AUP, origin offset, distance trigger, or universe-space math.
Solution: Do not mutate UI telemetry from the AUP auditor domain. Record the classification and continue repairing confirmed AUP authority leaks.
Rejected Alternatives: Editing `HphiReactiveUiTelemetry` to throttle or reshape UI warnings. That would be a UI/telemetry-domain change with no evidence of AUP precision drift.
Scalability potential: Low/Middle/High/Ultra unchanged for AUP. H-Phi remains a separate UI/QA metric unless a future scan connects it to AUP authority.
Hardware Impact: 0 us runtime gain from this classification; it avoids cross-domain churn and prevents this AUP loop from creating UI telemetry regressions.

## Decision 46 - H-Phi Static AUP Precision Factor

Problem: The headless H-Phi static score measured integration risk, tick purity, data-vault usage, and struct layout, but it did not penalize AUP precision leaks. That allowed a run to report the same H-Phi value whether code used double-safe AUP bridges or legacy float-origin patterns.
Solution: Add `AupPrecisionSafe` and `AupPrecisionRisk` counters to `HeadlessStressFractureBot`, multiply the existing H-Phi score by `aupPrecisionIntegrity`, and rename the model to `runtime_aup_risk_adjusted`.
Rejected Alternatives: Mutating `HphiReactiveUiTelemetry` or adding runtime UI counters. UI update cadence is outside AUP authority and would not catch precision drift. Adding allocations for detailed source reports was rejected; the static score remains scalar and bounded.
Scalability potential: Low gets a cheap static gate that catches AUP regression before runtime tests. Middle/High/Ultra get cleaner precision hygiene data and can spend stable AUP anchors on richer visual feedback without hidden drift debt.
Hardware Impact: 0 us gameplay-frame cost. Headless startup/source-scan cost increases by simple ordinal string scans only; expected low-end impact is negligible compared with the existing all-script `File.ReadAllText` pass.

## Decision 47 - H-Phi AUP Precision Counter Export

Problem: Loop 20 made the static H-Phi scalar AUP-risk-adjusted, but the result JSON still exposed only the scalar and model name. That hid whether a run changed because double-safe AUP patterns increased, legacy precision-risk patterns increased, or both.
Solution: Carry the `HPhiStaticCounters` result out of `ComputeStaticHPhiMetric`, cache `staticHPhiAupPrecisionIntegrity`, `staticHPhiAupPrecisionSafe`, and `staticHPhiAupPrecisionRisk`, and write those values to both JSON output and the one-time `[H-PHI_STATIC]` startup log.
Rejected Alternatives: Emit a per-file source report or mutate runtime `HphiReactiveUiTelemetry`. Per-file reports would allocate and bloat headless output; UI telemetry is not AUP authority and does not belong to this domain.
Scalability potential: Low gets a compact static QA counter that can be parsed without opening source files. Middle/High/Ultra get better drift-hygiene attribution before spending saved AUP stability on denser visual feedback.
Hardware Impact: 0 us gameplay-frame cost and 0 B/frame. Headless startup cost is only three primitive fields plus existing `StreamWriter` writes in the result-report path.

## Decision 48 - Qualified H-Phi AUP Risk Patterns

Problem: The first AUP H-Phi risk scan counted broad method names such as `ToAbsoluteUniversePosition(` and `ToUniverseSpace(`. That caught actual legacy calls, but it also caught private helper names that already route through double-backed AUP construction, creating noisy H-Phi debt.
Solution: Restrict legacy bridge counting to fully qualified `HectonFloatingOrigin.ToAbsoluteUniversePosition(` and `HectonMapMagicVegetationBridge.ToUniverseSpace(` patterns while retaining explicit component-read and `Vector3 universe` risk patterns.
Rejected Alternatives: Rename private helpers across CrashTelemetry/Fauna or keep broad string matches. Renaming safe helpers would be metric-chasing churn; broad matches punish correct local wrappers and make the H-Phi signal less useful.
Scalability potential: Low gets less noisy static QA output. Middle/High/Ultra get clearer attribution when real legacy AUP bridge calls reappear, which protects drift-sensitive visual overkill work from false-positive audit debt.
Hardware Impact: 0 us gameplay-frame cost and 0 B/frame. Headless scan cost is unchanged: the same ordinal count operations with longer, more specific literals.
