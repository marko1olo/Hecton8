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
