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
