# Rationale_ACOUSTIC_REFLECTION_MAPPER

Status: PENDING VERIFICATION

## Decision 1: Use Existing Player-Critical DSP Owner
Problem: Echolocation already intersects player helmet DSP, active sonar UI, cave reverb, and existing sonar echo tap buffers. A second runtime owner would duplicate hot audio paths and risk extra scheduling, thread wakeups, and concrete cross-domain coupling.
Solution: Extend `PlayerCriticalProceduralAudioRenderer` and isolate reusable Burst raymarch math in a new `Hecton8.Audio.Echolocation` assembly. Keep the DSP delay ring as the single output authority.
Rejected Alternatives: Spawning `AudioSource` echoes was rejected by prompt and would allocate/playback-manage many Unity objects. A new MonoBehaviour manager was rejected because it would compete with the existing audio producer thread and require extra registry wiring. Direct world-system dependency inside audio was rejected; the new job receives plain buffers/structs.
Scalability potential: Low uses 8 rays and existing psychoacoustic fake. Middle/High use more ray directions. Ultra can spend the saved object-spawn cost on denser virtual echo taps and richer filter material profiles.
Hardware Impact: Low-end i3/MX350 avoids hundreds of Unity audio voices; expected gain is bounded main-thread work with no new managed hot allocations. Exact microseconds remain PENDING VERIFICATION until Unity profiling.

## Decision 2: Treat Echolocation as Deterministic Acoustic Fake
Problem: Physically correct underwater acoustic reflection would require scene-wide propagation and many material/geometry interactions. That violates the 0.1ms suspicion threshold without profiler proof.
Solution: Use a capped ray fan over the existing SDF authority to generate virtual echo taps. The DSP delay line sells cave shape through delay, gain, pan, and low-pass instead of simulating full acoustics.
Rejected Alternatives: Full wave propagation, per-wall reflection bounces, and per-source `AudioReverbZone` edits were rejected as expensive and architecturally wrong for active ping feedback.
Scalability potential: Low = coarse cardinal/diagonal ray fan. Middle = 32 rays. High/Ultra = higher tap budget, flesh/material coloring, and richer low-pass profiles if budgets allow.
Hardware Impact: Converts object churn and Unity audio scheduling into tight numeric loops. Expected low-end benefit is lower CPU variance and 0 B GC hot path; measured data PENDING VERIFICATION.

## Decision 3: Isolate Raymarch Math From World Ownership
Problem: Audio needs SDF samples, but owning voxel data or querying concrete cave objects from Burst would create cross-domain coupling and unsafe managed references.
Solution: Add `Hecton8.Audio.Echolocation` with a pure blittable `IJobParallelFor`. `HectonVoxelVolume` exposes only closest published SDF NativeArray payloads and material IDs; the audio owner schedules the job and drains results into existing sonar tap buffers.
Rejected Alternatives: Referencing `HectonVoxelEngine` in Tick, copying SDF to managed arrays, or using Unity Physics raycasts. Those either violate GlobalRegistry hot-path discipline, allocate, or test mesh colliders instead of the authored sonar SDF.
Scalability potential: Low = 8 spherical octant/cardinal-ish rays. Middle/High = 32 Fibonacci-sphere rays. Ultra can reuse the isolated job for denser tap budgets after profiler proof.
Hardware Impact: Expected cost is bounded to one scheduled job per active ping. Main thread avoids 32 synchronous SDF marches in the default path; measured microseconds PENDING VERIFICATION.

## Decision 4: Queue Upload, Existing DSP Snapshot Consumption
Problem: Prompt demands an SPSC-style echo tap upload, but the current audio worker already consumes a double-buffered `NativeArray<SonarEchoTap>` snapshot and does not own a safe `NativeQueue` drain point on the audio thread.
Solution: Use a persistent `NativeQueue<SonarEchoTap>` as the late-frame upload bridge from completed Burst hits, then drain it into the renderer's existing double-buffer before signaling the audio worker. The DSP kernel remains the existing sample-accurate multi-tap delay line.
Rejected Alternatives: Draining `NativeQueue` directly inside the DSP inner loop was rejected because it would mutate queue state on the audio producer thread and add branchy synchronization risk. Managed `Queue<T>` was rejected for GC.
Scalability potential: Low drains up to 8 taps; Middle/High drains 32. Ultra can raise the queue/tap cap later as a single constant if profiler budget exists.
Hardware Impact: Queue drain is bounded by `SonarEchoTapCapacity`; expected low-end cost under 8 us per ping publish and 0 B GC. Measured data PENDING VERIFICATION.

## Decision 5: Rebase Guard Over Stale Echo Realism
Problem: Runtime-space hit points are invalid if the floating origin shifts while the SDF ray job is running.
Solution: Store the origin shift sequence with the scheduled job. If late-frame completion sees a shift or active shift-in-progress, discard the hit publish and reschedule from the current runtime origin with the same sonar sequence/start frame.
Rejected Alternatives: Applying an approximate offset to all virtual sources was rejected because it assumes stable SDF payload origin and invites phase errors. Blocking completion mid-frame was rejected as an audio stall risk.
Scalability potential: Low/Middle/High/Ultra all use the same safety gate. Stronger machines may reschedule with richer rays after shift; weak devices keep 8 rays.
Hardware Impact: Normal path cost is one integer compare. Shift path costs one extra scheduled ping job; exact microseconds PENDING VERIFICATION.

## Decision 6: Biological Echo as Material Profile, Not New Sound Object
Problem: Predator detection needs a distinct flesh echo without creating new sources or a parallel predator audio subsystem.
Solution: Add `SonarAudioMaterialIdBiological` and build one bounded predator tap from the existing aggressive bioform spatial query. The meat/flesh profile uses lower pitch and harsh low-pass, then feeds the same DSP delay line.
Rejected Alternatives: A predator-only AudioSource, new event ID, or unbounded scan of fauna lists. These add allocation/voice management or cross-domain concrete dependencies.
Scalability potential: Low keeps one biological tap only. Middle/High/Ultra can spend saved source-management cost on richer material filters or additional biological sub-taps after measured budget proof.
Hardware Impact: Cost is one existing spatial hash query per ping; no per-frame tax. Measured microseconds PENDING VERIFICATION.

## Decision 7: Compile Wall Is External
Problem: Unity compile cannot reach a clean project state, but current console errors point at files and assemblies not touched by this task.
Solution: Validated the changed scripts individually through Unity MCP (`AcousticEcholocationRaymarch.cs`, `PlayerCriticalProceduralAudioRenderer.cs`, `HectonVoxelVolume.cs`) with 0 diagnostics. Marked the Omega compile task blocked by dependency instead of editing unrelated Bootstrap/GlobalSignals/UI assembly failures.
Rejected Alternatives: Fixing `GameBootstrapper`, `GlobalSignals`, or missing `Hecton8.UI.Tools` from this acoustic task was rejected as cross-domain sabotage under active multi-agent work.
Scalability potential: No runtime scalability effect until the external compile wall is removed.
Hardware Impact: No measurement possible while project compile is externally blocked; PENDING VERIFICATION.

## OMEGA POLISH CHANGES
Problem: The first Burst ray fan used visually honest Fibonacci distribution with `math.sqrt`/`math.sincos`, and return distance used exact square root magnitude. That is unnecessary for a psychoacoustic cave-shape fake.
Solution: Replaced the high-count direction fan with a 32-lane bitmask/weighted-axis direction set normalized by `math.rsqrt`. Replaced job return-distance `sqrt` with max/mid/min approximate magnitude. Replaced fallback exact `Vector3.Distance` calls with existing approximate distance math.
Rejected Alternatives: Keeping Fibonacci directions for elegance was rejected because the player hears tap timing/material color, not exact spherical sample uniformity. A lookup table was rejected because it would require another static data surface and the bitmask fan is cheaper and deterministic.
Scalability potential: Low still uses 8 rays; Middle/High/Ultra use the 32-lane fake. Strong hardware buys more tap coloration, not more physical correctness.
Hardware Impact: Removes trig/sqrt from active ping ray direction generation and one return-distance sqrt per hit. Exact microseconds saved are PENDING VERIFICATION; expected save is small but deterministic on i3/MX350.

## Decision 8: Suppress Event + Signal Double Fire
Problem: `SpectrumSystem.EmitSonarPulse` raises `SonarPingSent` before publishing the matching `AcousticPingSignal`. The acoustic renderer consumed both paths, producing duplicate dry pings, duplicate SDF jobs, and duplicate ping-return taps for one player input.
Solution: Record the direct event ping origin, intensity, and frame. Reject the matching delayed active `AcousticPingSignal` within a two-frame, one-meter, 0.02 intensity hysteresis window. This keeps external active acoustic signals valid while killing the known duplicate handoff.
Rejected Alternatives: Moving Spectrum publish order was rejected because it is outside the acoustic domain and likely affects visor geo ping consumers. SourceId matching was rejected because the acoustic renderer should not depend on Spectrum-owned constants.
Scalability potential: Low avoids duplicate 8-ray jobs; Middle/High/Ultra avoid duplicate 32-ray jobs and duplicate tap coloring. The saved budget buys cleaner echo density instead of louder repeated pings.
Hardware Impact: On i3/MX350, this removes one redundant job schedule and one duplicate tap publication for the normal active sonar path. Exact microseconds remain PENDING VERIFICATION.

## Decision 9: No Synchronous SDF Fallback While Job Busy
Problem: A second ping arriving while an SDF echo job was still scheduled could fall through to the synchronous SDF raymarch fallback. That violates the scheduling intent and risks a main-thread spike under ping spam.
Solution: If an SDF job is pending, publish only the dry ping state and predator ping-back signal, then let the existing late-frame completion path finish the in-flight echo job. Non-forced job completion now uses `DispatcherJobSwap.TryComplete`.
Rejected Alternatives: Blocking on the pending job, completing it directly in the trigger path, or running another synchronous 8/32 ray fallback. These approaches spend frame time to preserve an edge-case echo that the player cannot resolve under rapid ping spam.
Scalability potential: Low/Middle/High/Ultra all preserve deterministic dry ping feedback under spam. Strong hardware can later receive a small pending-request queue after profiler proof; no queue was added now because it would expand state surface.
Hardware Impact: Prevents worst-case synchronous raymarch on the main thread during overlapping pings. Exact microseconds remain PENDING VERIFICATION.

## Decision 10: Finite-Origin Guard and Reciprocal SDF Cell Math
Problem: Acoustic signals can carry AUP-derived runtime coordinates; non-finite values must not enter DSP state, ping telemetry, or Burst payloads. The SDF sample transform also used direct vector division.
Solution: Reject non-finite active sonar origins before trigger/job writes. Replace SDF sample division with component-wise reciprocal multiply.
Rejected Alternatives: Trusting upstream signal authors was rejected by the Black Box rule. Keeping division was rejected because reciprocal multiply is the local math style and easier for Burst to optimize.
Scalability potential: All tiers get identical safety. Ultra spends performance on richer material color, not avoidable arithmetic.
Hardware Impact: No measured frame delta. Reduces invalid-state risk and removes one vector division from each SDF sample.

## Decision 11: Do Not Patch Moving External Compile Wall From Acoustic Domain
Problem: Unity Console blockers changed during recheck from stale SaveSystem/thread errors to shader errors and then to `WorldChunkResidencyManager.cs(400,142)` missing `IStreamingBackpressureService.IsChunkImpostorAudioMuted(long)`. These are active non-acoustic domains.
Solution: Keep acoustic edits bounded; validate acoustic scripts directly and record the external blocker for the integrator instead of editing streaming/world ownership code from this prompt.
Rejected Alternatives: Implementing a streaming backpressure interface method in world residency from the DSP acoustic prompt was rejected as cross-domain ownership drift. Patching shader or SaveSystem blockers was also rejected because they changed under active multi-agent work.
Scalability potential: No acoustic scalability change. Keeping domain boundaries intact prevents coupling acoustic verification to unrelated streaming policy.
Hardware Impact: No runtime measurement possible until external compile wall clears. PENDING VERIFICATION.

## Decision 12: Drop Stale SDF Echo Job Results
Problem: If ping B arrived while ping A's SDF ray job was still pending, ping B correctly published immediate dry feedback, but ping A could later complete and overwrite the newer pending sonar state with stale echo taps.
Solution: Before publishing completed SDF ray hits, compare `_sonarEcholocationScheduledSequence` with the latest `_pendingSonarSequence`. If they differ, drop the completed job result. The player keeps the newest dry ping state and stale acoustic geometry never rewrites the DSP tap snapshot.
Rejected Alternatives: Blocking ping B until ping A completes was rejected as a main-thread stall. Publishing ping A anyway was rejected because it violates temporal control. Adding a multi-ping SDF job queue was rejected because it expands state and memory without profiler proof.
Scalability potential: Low/Middle/High/Ultra all use the same sequence guard. Low avoids stale 8-tap publishes; higher tiers avoid stale 32-tap publishes while preserving a simple one-job acoustic mapper.
Hardware Impact: Cost is one integer compare per completed SDF job. Expected gain is avoiding stale queue drain and DSP snapshot writes during overlapping pings; exact microseconds remain PENDING VERIFICATION.

## Decision 13: Publish Async Echo Revisions Through the Inactive Buffer
Problem: The scheduled SDF job originally stored the pending buffer index used for the immediate dry ping. That dry ping publish flips the read index, so when the later echo revision wrote taps into the same buffer, the audio producer could copy a buffer while the main thread was mutating it.
Solution: On SDF completion, resolve the currently inactive pending buffer with `1 - Volatile.Read(ref _pendingSonarStateReadIndex)`, drain echo taps there, then publish revision 2 from that buffer. The obsolete scheduled buffer field and method parameter were removed.
Rejected Alternatives: Locking around pending tap buffers was rejected because the audio producer path must stay non-blocking. Writing into the dry ping buffer was rejected because it breaks the double-buffer contract. Allocating a fresh tap array per completion was rejected by the zero-GC mandate.
Scalability potential: Low writes up to 8 taps through the safe buffer; Middle/High/Ultra write up to 32. Future higher tap counts still preserve the same buffer swap model.
Hardware Impact: Adds one volatile read per completed SDF job and removes stale scheduling state. Expected gain is correctness: no torn tap copy, no lock, no GC. Exact microseconds remain PENDING VERIFICATION.

## Decision 14: Preserve Monotonic Echo Revisions
Problem: The async SDF completion path could generate ghost fallback taps but publish them as `EchoRevision = 1`, the same revision already used by the immediate dry ping for that sequence. The audio producer only consumes a same-sequence update when the revision changes, so those fallback taps could be ignored.
Solution: Keep async completion revisions at `2` even when the taps are fallback ghosts. Revision 1 remains reserved for the immediate dry ping and initial synchronous fallback path.
Rejected Alternatives: Resetting the sonar sequence was rejected because it restarts the chirp timing. Forcing the audio producer to copy same-revision states was rejected because it weakens the existing handoff contract. Allocating a separate fallback event was rejected by the zero-GC rule.
Scalability potential: Low gets audible 8-ray/ghost fallback when no SDF hits resolve. Middle/High/Ultra get consistent revision semantics for richer tap sets.
Hardware Impact: No extra frame cost. It restores existing fallback work instead of silently dropping it; exact acoustic benefit is behavioral, not measured in microseconds.

## Decision 15: Gate Stale Jobs Before Shift Reschedule and Narrow Signal Consumption
Problem: A stale completed SDF job could skip publish but still reschedule itself during an AUP shift, wasting another raymarch. Separately, the direct sonar event handler consumed whatever latest active acoustic signal existed, which could suppress an unrelated active ping.
Solution: Check the scheduled sonar sequence against `_pendingSonarSequence` before any shift reschedule. Remove broad latest-signal consumption from `HandleSonarPingSent`; duplicate suppression now relies on the recorded direct ping's frame, origin, and intensity.
Rejected Alternatives: Rescheduling stale jobs and dropping them later was rejected as wasted work. Consuming latest signals by sequence from the direct event path was rejected because it lacks source identity and can eat another producer's signal.
Scalability potential: Low avoids a wasted 8-ray reschedule; higher tiers avoid wasted 32-ray reschedules. All tiers keep valid non-Spectrum active sonar handoffs alive.
Hardware Impact: Adds one integer compare before reschedule and removes one GlobalSignals poll from the direct event path. Expected low-end gain is tiny but deterministic under ping overlap plus AUP shift; exact microseconds remain PENDING VERIFICATION.

## Decision 16: Reject Zero-Distance SDF Echo Returns
Problem: The raymarch loop samples `distance = 0` to seed density interpolation. If that origin sample is solid or over the density threshold, it can become a virtual source at the listener with near-zero delay and saturated gain.
Solution: Keep the origin sample for previous-density state, but require `distance > 0` before `thresholdHit` or `initialSolidHit` can publish an echo. Surface-crossing behavior remains available after the first positive-distance sample.
Rejected Alternatives: Clamping the final delay was rejected because it hides the invalid tap while still coloring the mix with a fake origin hit. Starting the ray at `step` was rejected because the default SDF step can be coarse and would lose near-wall interpolation context.
Scalability potential: Low, Middle, High, and Ultra all use the same guard. Low avoids one catastrophic 8-ray loudness artifact; higher tiers avoid multiplying that artifact across 32 taps.
Hardware Impact: Adds one boolean distance gate in the ray sample loop. Expected cost is effectively noise; expected gain is eliminating a pathological full-gain self echo on i3/MX350 and high-end machines alike. Exact microseconds remain PENDING VERIFICATION.

Cinematic Cheats Used:
- SDF ray fan hears cave silhouette through virtual taps instead of simulating acoustic propagation.
- Material color is a filter/pitch profile, not physical reflection.
- Biological predator return is one bounded flesh tap, not per-body sonar scattering.
- Rebase safety reschedules rather than preserving physically exact wavefront continuity.

Cross-Domain Justification:
- `HectonVoxelVolume` edit is a narrow SDF payload accessor for existing published NativeArray data. No voxel ownership moved into audio.
- `Hecton8.Core.asmdef` edit is required so Core can consume the isolated `Hecton8.Audio.Echolocation` Burst math assembly.

Final Git Diff Evidence:
- Modified: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`
- Modified: `Assets/_Project/Scripts/Hecton8.Core.asmdef`
- Modified: `Assets/_Project/Scripts/HectonVoxelVolume.cs`
- Added: `Assets/_Project/Scripts/Audio/Echolocation/Hecton8.Audio.Echolocation.asmdef`
- Added: `Assets/_Project/Scripts/Audio/Echolocation/AcousticEcholocationRaymarch.cs`
- Modified: `Docs/Tasks/Status_ACOUSTIC_REFLECTION_MAPPER.md`
- Modified: `Docs/AgentLogs/Rationale_ACOUSTIC_REFLECTION_MAPPER.md`
- `git diff --check`: passed for touched files.
- Unity `validate_script`: 0 diagnostics for `AcousticEcholocationRaymarch.cs`; 0 diagnostics for `PlayerCriticalProceduralAudioRenderer.cs` on standard validation before the final initializer tweak and 0 diagnostics on basic validation after it.
- Latest targeted scan: no direct `_sonarEcholocationJobHandle.Complete()` remains; non-forced SDF completion uses `DispatcherJobSwap.TryComplete`; completed SDF jobs now drop stale sonar sequences.
- Latest `git diff --check`: passed for touched acoustic files with CRLF warnings only.
- Latest async buffer audit: completed SDF echo revisions now publish through the inactive pending tap buffer; obsolete scheduled buffer state removed.
- Unity `validate_script`: 0 diagnostics for `AcousticEcholocationRaymarch.cs` on standard validation; 0 diagnostics for `HectonVoxelVolume.cs` on basic validation after the async buffer fix.
- Latest revision/signal audit: async ghost fallback uses revision 2, stale jobs do not reschedule after a newer ping, and direct sonar events no longer consume arbitrary latest active acoustic signals.
- Unity `validate_script`: 0 diagnostics for `AcousticEcholocationRaymarch.cs` on standard validation; 0 diagnostics for `HectonVoxelVolume.cs` on basic validation after the revision/signal pass.
- Latest self-hit audit: `AcousticEcholocationRaymarchJob` no longer allows the `distance = 0` origin sample to publish an echo tap.
- Unity `validate_script`: 0 diagnostics for `AcousticEcholocationRaymarch.cs` on standard validation after the self-hit guard.
- Unity compile/runtime proof: PENDING/BLOCKED. Current live Console blockers are external `FaunaBrain.Ecosystem.cs` missing `_ecosystemPropertyBlock` references; renderer validator still times out on the large file regex engine.
