# LOG_ACOUSTIC_REFLECTION_MAPPER

## 2026-05-13 - DSP_ACOUSTIC_LEAD - ACOUSTIC_REFLECTION_MAPPER
Status: PENDING VERIFICATION / COMPILE BLOCKED BY EXTERNAL DEPENDENCY

What was wrong:
- Active sonar pings had no dedicated echolocation reflection map feeding the DSP tap buffer.
- Existing SDF sonar fallback used limited probe counts and one-way delay.
- Predator returns used generic/default acoustic material.
- Blackbox telemetry did not carry `ActiveEchoTaps` for postmortem inspection.

What was done:
- Added isolated `Hecton8.Audio.Echolocation` asmdef and `AcousticEcholocationRaymarchJob`.
- Scheduled up to 32 Burst SDF rays per active ping; Low/MX350 uses 8 rays.
- Consumed active `AcousticPingSignal` by latest sequence and debounced Spectrum-originated duplicate pings.
- Added closest published SDF NativeArray accessor on `HectonVoxelVolume` for encoded density + material IDs.
- Converted SDF hits into virtual sources with round-trip delay `(RayDistance + ReturnDistance) / 1480`.
- Computed gain with guarded reciprocal time-squared falloff and material reflectivity.
- Added persistent `NativeQueue<SonarEchoTap>` upload bridge, drained into existing double-buffered DSP tap snapshot.
- Added AUP shift sequence guard; stale runtime-space job results reschedule instead of publishing.
- Added biological/flesh sonar material profile and predator echo tap.
- Added `ActiveEchoTaps` to granular blackbox telemetry and mirrored dump to `Dump_ACOUSTIC_REFLECTION_MAPPER.bin`.

Cinematic Cheats used:
- Virtual tap reflection instead of real acoustic propagation.
- Bitmask weighted 32-ray fan instead of Fibonacci/trig sphere after Omega polish.
- Approximate max/mid/min distance instead of exact hit-return sqrt in the Burst job.
- Material identity is filter/pitch/gain coloration, not physical acoustic impedance.
- Predator flesh return is one bounded spatial-hash tap, not body-surface scattering.

Exact Microseconds saved:
- Measured exact: unavailable. Unity compile is blocked by unrelated `GlobalSignals.cs` / `EcosystemDirector.cs` errors.
- Estimated removal from Omega polish: eliminates trig/sqrt direction generation and one return-distance sqrt per hit; expected low single-digit microseconds per ping on i3/MX350.
- Estimated architectural save: avoids hundreds of `AudioSource` voices; exact saved time PENDING PROFILER VERIFICATION.

Verification:
- `git diff --check` passed for touched files.
- `rg AudioReverbZone` under `Assets` for `.cs/.prefab/.unity` returned no hits.
- Unity `validate_script` returned 0 diagnostics for `AcousticEcholocationRaymarch.cs`, `PlayerCriticalProceduralAudioRenderer.cs`, and `HectonVoxelVolume.cs` before Omega polish; after polish, job script still reports 0 diagnostics and the renderer validator times out on the large file regex engine.
- Unity compile remains blocked by external errors outside touched files:
  - `Assets/_Project/Scripts/Core/GlobalSignals.cs(23,53): GlobalWorldStateSignal namespace alias failure`
  - `Assets/_Project/Scripts/World/EcosystemDirector.cs(79,111): missing IEcosystemDirectorService.ApplyCampaignToxicityPressure signature`

## 2026-05-13 - Continuation Pass 6 - AAA Recheck
Status: PENDING VERIFICATION / RUNTIME PROFILER BLOCKED BY EXTERNAL COMPILE ERROR

What was wrong:
- The active sonar handoff could double-fire because `SpectrumSystem` raises `SonarPingSent` before publishing the matching `AcousticPingSignal`.
- A new ping while the SDF ray job was still scheduled could fall through to the synchronous SDF raymarch fallback.
- Non-finite active acoustic origins were not rejected before trigger/job state writes.
- Burst SDF sample coordinate conversion still used direct vector division.

What was done:
- Added direct-ping hysteresis: frame, origin, and intensity are recorded on `SonarPingSent`; the matching delayed active `AcousticPingSignal` is consumed but not retriggered.
- Added in-flight SDF job guard: overlapping pings publish dry feedback instead of running synchronous SDF rays while the previous Burst job is pending.
- Routed non-forced SDF job completion through `DispatcherJobSwap.TryComplete`.
- Added finite-origin rejection before active sonar trigger/job writes.
- Replaced SDF sample division with `math.rcp(safeCell)` multiply.

Cinematic Cheats used:
- Under ping spam, dry ping feedback is preserved and overlapping echo mapping is dropped instead of blocking or running sync raymarch.
- Duplicate event/signal pings are collapsed by a two-frame perceptual hysteresis window.

Exact Microseconds saved:
- Measured exact: unavailable. Runtime profiler proof blocked by unrelated shader import errors.
- Expected save: one redundant ping job/tap publish removed from normal active sonar path.
- Expected spike avoidance: prevents sync fallback raymarch when a previous SDF job is in flight.

Verification:
- Unity `validate_script` returned 0 diagnostics for `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`.
- Unity `validate_script` returned 0 diagnostics for `Assets/_Project/Scripts/Audio/Echolocation/AcousticEcholocationRaymarch.cs`.
- `git diff --check` passed for touched acoustic files with CRLF warnings only.
- Live Unity Console no longer reports the first stale C# blockers; current blocker is unrelated `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs(400,142)` missing `IStreamingBackpressureService.IsChunkImpostorAudioMuted(long)`.

## 2026-05-13 - Continuation Pass 7 - Stale Publish Recheck
Status: PENDING VERIFICATION / UNITY SESSION UNAVAILABLE

What was wrong:
- An older SDF ray job could complete after a newer ping published dry feedback, then overwrite the newer sonar state with stale echo taps.
- The final validation path lost Unity MCP availability after the stale-publish patch, so runtime/profiler proof cannot be claimed.

What was done:
- Added a sequence guard in `PublishCompletedSdfSonarEchoJob`: completed SDF results publish only when the scheduled sequence still matches the latest pending sonar sequence.
- Rechecked acoustic handoff code: non-forced SDF job completion uses `DispatcherJobSwap.TryComplete`; direct `_sonarEcholocationJobHandle.Complete()` is not present.
- Rechecked Burst raymarch math: SDF cell conversion uses reciprocal multiply and the active ray fan remains trig-free.
- Re-ran `git diff --check` for touched acoustic files.

Cinematic Cheats used:
- Overlapping pings keep immediate dry feedback and drop stale geometry instead of blocking or preserving physically late wavefront continuity.
- One active acoustic reflection map remains authoritative; no multi-job echo backlog was introduced without profiler proof.

Exact Microseconds saved:
- Measured exact: unavailable. Unity MCP `read_console` returned `no_unity_session`.
- Expected save: avoids one stale queue drain and DSP tap snapshot publish when overlapping pings invalidate an older SDF job.
- Cost added: one integer sequence compare per completed SDF job.

Verification:
- `git diff --check` passed for touched acoustic files with CRLF warnings only.
- `Select-String` scan confirmed no direct `_sonarEcholocationJobHandle.Complete()` usage remains.
- Unity Console read failed with `Unity session not available; reason=no_unity_session`, so latest compile/runtime proof remains PENDING VERIFICATION.

## 2026-05-13 - Continuation Pass 8 - Pending Buffer Race Recheck
Status: PENDING VERIFICATION / RUNTIME PROFILER BLOCKED BY EXTERNAL COMPILE ERRORS

What was wrong:
- The scheduled SDF job remembered the same pending buffer used for the immediate dry ping.
- That dry ping made the buffer readable by the audio producer, so the later echo revision could mutate a buffer while the producer was copying it.
- The scheduled buffer field also made the stale publish model look intentional.

What was done:
- SDF echo completion now resolves `1 - _pendingSonarStateReadIndex` at publish time and drains taps into the currently inactive pending buffer.
- Revision 2 is published only after the inactive buffer is fully written.
- Removed `_sonarEcholocationScheduledBufferIndex` and the unused `inactiveIndex` parameter from `TryScheduleSdfSonarEchoJob`.

Cinematic Cheats used:
- The dry ping remains immediate; detailed cave-shape taps arrive as a later revision without blocking the audio producer.
- No multi-job echo backlog or per-ping tap allocation was introduced.

Exact Microseconds saved:
- Measured exact: unavailable. Project compile is currently blocked by unrelated Core/Input/GlobalDataVault code.
- Cost added: one volatile read per completed SDF echo job.
- Expected save: avoids locks, allocations, and torn pending-buffer recovery logic.

Verification:
- `git diff --check` passed for modified tracked acoustic/log files with CRLF warnings only.
- `git diff --check --no-index` on new echolocation files reported CRLF warnings only.
- Unity `validate_script` returned 0 diagnostics for `Assets/_Project/Scripts/Audio/Echolocation/AcousticEcholocationRaymarch.cs`.
- Unity `validate_script` returned 0 diagnostics for `Assets/_Project/Scripts/HectonVoxelVolume.cs`.
- Unity renderer validation still times out in MCP's regex engine because `PlayerCriticalProceduralAudioRenderer.cs` is large.
- Live Unity Console blockers are unrelated Core/Input/GlobalDataVault missing-symbol errors, including `H8MacroDatabaseService.cs(95,42)`, `InputManager.cs(647,66)`, and `GlobalDataVault.cs(312,21)`.

## 2026-05-13 - Continuation Pass 9 - Revision & Signal Handoff Recheck
Status: PENDING VERIFICATION / RUNTIME PROFILER BLOCKED BY EXTERNAL CORE MEMORY COMPILE ERRORS

What was wrong:
- Async SDF completion ghost fallback taps were published as revision 1, which the audio producer could ignore after consuming the immediate dry ping revision 1.
- Stale completed SDF jobs could still reschedule during an AUP shift before being dropped later.
- `HandleSonarPingSent` consumed the latest active `AcousticPingSignal` broadly, which could suppress an unrelated producer's signal.

What was done:
- Kept async SDF completion at `EchoRevision = 2` for fallback ghost taps.
- Added a stale-sequence gate before AUP shift rescheduling in `TryCompleteSdfSonarEchoJob`.
- Removed broad latest-signal consumption from the direct sonar event handler; duplicate suppression now uses frame/origin/intensity hysteresis only.

Cinematic Cheats used:
- Dry ping remains immediate; fallback cave ghosts arrive as a second perceptual revision when real SDF geometry gives no taps.
- Stale shifted wavefronts are dropped instead of preserved or resimulated.

Exact Microseconds saved:
- Measured exact: unavailable. Project compile is currently blocked by unrelated Core.Memory code.
- Expected save: avoids one wasted 8/32-ray reschedule after overlapping ping plus AUP shift.
- Cost removed: one direct-event `GlobalSignals.TryGetLatestAcousticPingSignal` poll.

Verification:
- `git diff --check` passed for modified tracked acoustic files with CRLF warnings only.
- Unity `validate_script` returned 0 diagnostics for `Assets/_Project/Scripts/Audio/Echolocation/AcousticEcholocationRaymarch.cs`.
- Unity `validate_script` returned 0 diagnostics for `Assets/_Project/Scripts/HectonVoxelVolume.cs`.
- Renderer validator still times out in MCP's regex engine because `PlayerCriticalProceduralAudioRenderer.cs` is large.
- Live Unity Console blocker is unrelated Core.Memory: `GlobalDataVault.cs` cannot resolve `NativeMemorySentinel`, `NativeAllocationLifetime`, and `GlobalRegistry`.

## 2026-05-13 - Continuation Pass 10 - SDF Origin Self-Hit Recheck
Status: PENDING VERIFICATION / RUNTIME PROFILER BLOCKED BY EXTERNAL ECOSYSTEM COMPILE ERRORS

What was wrong:
- `AcousticEcholocationRaymarchJob` sampled `distance = 0` and allowed that sample to satisfy the density threshold.
- If the player origin was inside or touching a solid SDF voxel, the job could publish a near-zero delay echo with saturated gain.
- That is not cave shape. It is an invalid self echo.

What was done:
- Added a positive-distance guard before `thresholdHit` and `initialSolidHit`.
- Kept the origin sample as previous-density state so later surface/threshold hits still work.
- Revalidated the isolated Burst raymarch script through Unity MCP.

Cinematic Cheats used:
- Origin solidity is treated as invalid setup state, not as a physical reflector.
- The first perceptible echo must come from positive-distance geometry; no fake listener-local virtual source is allowed.

Exact Microseconds saved:
- Measured exact: unavailable. Project compile is currently blocked by unrelated Ecosystem code.
- Cost added: one boolean gate per SDF sample.
- Expected save: prevents one pathological full-gain zero-delay tap and avoids wasting DSP energy on an inaudible/incorrect self echo.

Verification:
- Unity `validate_script` returned 0 diagnostics for `Assets/_Project/Scripts/Audio/Echolocation/AcousticEcholocationRaymarch.cs`.
- `git diff --check` passed for modified tracked acoustic files with CRLF warnings only.
- `git diff --check --no-index` on the new raymarch file reported CRLF warnings only, no whitespace errors.
- Live Unity Console blocker moved again and is unrelated: `Assets\_Project\Scripts\Ecosystem\FaunaBrain.Ecosystem.cs` cannot resolve `_ecosystemPropertyBlock`.
