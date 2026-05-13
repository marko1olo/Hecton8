# Status_ACOUSTIC_REFLECTION_MAPPER

Prompt: `ACOUSTIC_REFLECTION_MAPPER`
Role: `DSP_ACOUSTIC_LEAD`
Domain: Echelon 8 / DSP Acoustic Radar / Echolocation
Status: PENDING VERIFICATION

Mandates loaded before coding:
- `AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt`
- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`

## Loop 1: Tasks 1-5
- [x] Task 1. Singleton eradication / extend `IAudioService` path | DOD: no new singleton/manager; used existing `PlayerCriticalProceduralAudioRenderer` DSP owner and GlobalRegistry-exposed runtime path. | Rejected: spawning a new echolocation manager or classic singleton. | Estimate: 0 us/frame extra singleton overhead; PENDING VERIFICATION.
- [x] Task 2. Consume `AcousticPingSignal` | DOD: `Tick` now consumes latest active-sonar `AcousticPingSignal` by sequence and debounces Spectrum-originated pings. | Rejected: new event ID/string RPC; direct poll by source systems. | Estimate: 1-3 us on frames with a new active ping, 0 B; PENDING VERIFICATION.
- [x] Task 3. ASMDEF isolation `Hecton8.Audio.Echolocation` | DOD: new child asmdef contains pure Burst raymarch math and Core references it. | Rejected: dumping raymarch code into Core or adding third-party DSP package. | Estimate: 0 us/frame when idle; compile-scope isolation only; PENDING VERIFICATION.
- [x] Task 4. Dead code hunt: `AudioReverbZone` | DOD: `rg` scan under `Assets` for `.cs/.prefab/.unity` returned no `AudioReverbZone` hits. | Rejected: blind YAML edits/deletes. | Estimate: 0 us/frame, no change required.
- [x] Task 5. Burst ping ray fan | DOD: `AcousticEcholocationRaymarchJob : IJobParallelFor` schedules up to 32 spherical SDF rays from runtime AUP ping origin. | Rejected: AudioSource echo spawning and synchronous 32-ray main-thread default path. | Estimate: 20-60 us per active ping on MX350-class CPU; PENDING VERIFICATION.

## Loop 2: Tasks 6-10
- [x] Task 6. SDF sampling from existing voxel density authority | DOD: job samples published sonar SDF NativeArray + material IDs from nearest active `HectonVoxelVolume`. | Rejected: managed SDF copies and Unity Physics raycasts. | Estimate: 0 B, 32 ray marches only per ping; PENDING VERIFICATION.
- [x] Task 7. Virtual source hit-point return distance | DOD: every hit records hit point, ray distance, return distance to listener, and state hash. | Rejected: one-way delay only. | Estimate: <5 us per 32-hit publish pass; PENDING VERIFICATION.
- [x] Task 8. Delay math `(RayDistance + ReturnDistance) / 1480` | DOD: Burst job and synchronous fallback both use round-trip distance with `SoundSpeedWaterMetersPerSecondInv`. | Rejected: old one-way SDF delay. | Estimate: no DSP cost; precalculated off audio inner loop.
- [x] Task 9. Amplitude math with reciprocal falloff | DOD: job computes gain from `math.rcp(TotalTime * TotalTime)` with epsilon guard, reflectivity, absorption, and material multiplier. | Rejected: division operator and per-sample gain math. | Estimate: <3 us per ping ray batch; PENDING VERIFICATION.
- [x] Task 10. Echo tap upload bridge | DOD: completed ray hits enqueue `SonarEchoTap` records into persistent `NativeQueue<SonarEchoTap>` then drain into double-buffered DSP tap snapshot. | Rejected: managed list and direct audio-thread queue mutation. | Estimate: <8 us per 32 taps, 0 B; PENDING VERIFICATION.

## Loop 3: Tasks 11-14
- [x] Task 11. DSP delay-line consumption | DOD: existing `RenderSonarBlock` consumes prebuilt `SonarEchoTap` snapshots with delay-line read cursors; new taps flow through that same worker path. | Rejected: AudioSource voices or direct queue drain inside audio sample loop. | Estimate: existing O(taps*frames) path now capped at 32 taps; PENDING VERIFICATION.
- [x] Task 12. Distance low-pass filtering | DOD: Burst job resolves distance/material cutoff and `BuildSonarEchoTap` precomputes biquad coefficients before DSP consumption. | Rejected: per-sample cutoff calculation. | Estimate: no new division/filter setup in DSP inner loop; PENDING VERIFICATION.
- [x] Task 13. AUP shift safety | DOD: scheduled jobs store `HectonFloatingOrigin.CurrentShiftSequence`; late-frame completion reschedules instead of publishing stale runtime-space hits after a shift. | Rejected: trusting stale hit points across rebase. | Estimate: 0 us unless a shift occurs; one extra job on shift; PENDING VERIFICATION.
- [x] Task 14. Math LOD rays Low 8 / default 32 | DOD: `ResolveSonarSdfProbeCount` returns 8 for Low/MX350/Unknown and 32 for normal/high/ultra. | Rejected: fixed 32 on MX350 and flickering per-frame LOD. | Estimate: Low saves roughly 24 ray marches per ping; PENDING VERIFICATION.

## Loop 4: Tasks 15-18
- [x] Task 15. Zero-GC hot path audit | DOD: hot path uses persistent NativeArray/NativeQueue, struct jobs, no managed collections/AudioSource/AudioReverbZone. | Rejected: managed queues/lists and per-ping object spawning. | Estimate: 0 B/frame target; measured proof absent/PENDING VERIFICATION.
- [x] Task 16. Predator flesh echo tap | DOD: aggressive leviathan bioform query injects `SonarAudioMaterialIdBiological` tap with meat/flesh low-pass/pitch profile. | Rejected: generic rock/default material for predators. | Estimate: one bounded spatial query on ping only; PENDING VERIFICATION.
- [x] Task 17. Blackbox echo telemetry | DOD: `GranularAudioTelemetryEntry` now writes `ActiveEchoTaps` and dump mirror `Dump_ACOUSTIC_REFLECTION_MAPPER.bin`. | Rejected: chat-only telemetry claim. | Estimate: one int write per sampled blackbox frame; PENDING VERIFICATION.
- [ ] Task 18. Omega compile/DSP stall check [BLOCKED BY DEPENDENCY] | DOD attempted: Unity refresh/compile requested; `validate_script` returned 0 diagnostics for changed acoustic scripts after the continuation pass. | Blocker: Unity Console currently reports unrelated `WorldChunkResidencyManager.cs(400,142)` missing `IStreamingBackpressureService.IsChunkImpostorAudioMuted(long)`, so runtime/profiler proof is still blocked outside this prompt. | Estimate: our DSP job schedules/late-frame completes through `DispatcherJobSwap`; profiler proof blocked.

## Iterative Self-Review
- [x] Pass 1. Prompt re-read after task 5 | Re-extracted XML with PowerShell regex and confirmed 18 tasks.
- [x] Pass 2. Code scan after task 10 | `validate_script` reported 0 diagnostics for echolocation job, renderer, and voxel volume.
- [x] Pass 3. Code scan after task 14 | Scanned DSP block and echolocation handoff; new math is pre-DSP and tap fields are precalculated.
- [x] Pass 4. Compile/error scan after task 18 | Unity Console errors are outside touched files; no diagnostics from validator on touched scripts.
- [x] Pass 5. Polish mandate after all tasks checked or blocked | Omega polish replaced sqrt/trig ray fan with bitmask weighted rays and documented final diff evidence.

## Continuation Pass 6: AAA Recheck / 2026-05-13
- [x] Duplicate active-ping suppression | DOD: recorded direct `SonarPingSent` origin/intensity/frame and rejected the matching delayed `AcousticPingSignal` within a two-frame/one-meter hysteresis window. | Rejected: relying on Spectrum publish ordering; it raises the event before publishing the signal. | Estimate: removes one duplicate ping job/tap publish per active ping from the event+signal path; exact us PENDING VERIFICATION.
- [x] In-flight SDF job guard | DOD: a new ping arriving while an SDF echo job is pending now publishes the dry ping only instead of falling into synchronous SDF raymarch. | Rejected: blocking or running 8/32 main-thread rays while the previous Burst job is still pending. | Estimate: avoids worst-case sync fallback work under ping spam; exact us PENDING VERIFICATION.
- [x] Dispatcher-owned job finalization | DOD: non-forced SDF echo completion now uses `DispatcherJobSwap.TryComplete` from late-frame/teardown paths. | Rejected: direct `JobHandle.Complete()` from the sonar trigger path. | Estimate: no measured frame delta; reduces illegal completion/stall risk.
- [x] Finite-origin and reciprocal math pass | DOD: active acoustic signal origins are checked for finite values before trigger/job writes, and Burst SDF sample conversion uses `math.rcp(safeCell)` instead of vector division. | Rejected: letting non-finite AUP data enter DSP state or Burst payloads. | Estimate: 0 B, tiny ALU cleanup; exact us PENDING VERIFICATION.
- [ ] Runtime/profiler proof [BLOCKED BY DEPENDENCY] | DOD attempted: Unity validation passed for `PlayerCriticalProceduralAudioRenderer.cs` and `AcousticEcholocationRaymarch.cs`; `git diff --check` passed with CRLF warnings only. | Blocker: live Console reports unrelated `WorldChunkResidencyManager.cs(400,142)` interface implementation error, not acoustic C# errors. | Estimate: measured proof absent.

## Continuation Pass 7: Stale Publish Recheck / 2026-05-13
- [x] Stale SDF completion guard | DOD: completed SDF jobs now compare their scheduled sonar sequence against the latest pending sonar sequence before publishing taps. Older jobs are dropped instead of overwriting a newer dry ping state. | Rejected: blocking for old jobs, replaying stale echo taps, or adding a multi-job queue without profiler proof. | Estimate: one integer compare per completed SDF job; prevents one stale tap publish under overlapping pings; exact us PENDING VERIFICATION.
- [x] Targeted acoustic code scan | DOD: confirmed no direct `_sonarEcholocationJobHandle.Complete()` remains, non-forced completion uses `DispatcherJobSwap.TryComplete`, and Burst SDF sample conversion uses reciprocal multiply. | Rejected: broad unrelated refactor while other agents own dirty files. | Estimate: no runtime change; evidence pass only.
- [ ] Latest Unity session verification [BLOCKED BY TOOL/EDITOR STATE] | DOD attempted: `read_console` through Unity MCP returned `no_unity_session`; local `git diff --check` still passes for touched acoustic files with CRLF warnings only. | Blocker: Unity session unavailable after the final stale-publish guard patch, so latest runtime/profiler proof remains unavailable. | Estimate: measured proof absent.

## Continuation Pass 8: Pending Buffer Race Recheck / 2026-05-13
- [x] Async echo revision double-buffer safety | DOD: completed SDF echo jobs now drain taps into the currently inactive pending buffer, then publish revision 2 from that buffer. The dry ping buffer remains read-only for the audio producer until the revision swap. | Rejected: writing echo taps into the same buffer made readable by the dry ping, because the audio producer can copy it concurrently. | Estimate: no extra allocation; one read-index load per completed SDF job; prevents pending tap buffer tearing under async completion.
- [x] Removed obsolete scheduled buffer field | DOD: deleted `_sonarEcholocationScheduledBufferIndex` and the unused `inactiveIndex` parameter from `TryScheduleSdfSonarEchoJob`. | Rejected: leaving dead scheduling state that implied the old unsafe publish model still existed. | Estimate: no measurable runtime delta; reduces state surface.
- [ ] Compile/profiler proof [BLOCKED BY EXTERNAL COMPILE ERRORS] | DOD attempted: Unity `validate_script` passed 0 diagnostics for `AcousticEcholocationRaymarch.cs` and `HectonVoxelVolume.cs`; renderer validator still times out on the large file; live Console blockers are unrelated Core/Input/GlobalDataVault missing symbols, not acoustic files. | Estimate: measured acoustic DSP stall proof still absent.

## Continuation Pass 9: Revision & Signal Handoff Recheck / 2026-05-13
- [x] Async ghost fallback revision fix | DOD: SDF job completion now keeps `EchoRevision = 2` even when no SDF hits produce fallback ghost taps, so the audio producer consumes the fallback revision after the dry ping. | Rejected: publishing fallback taps as revision 1, because the dry ping already consumed revision 1 for the same sequence. | Estimate: no extra allocation; restores audible fallback taps under empty-SDF async pings.
- [x] Stale shift-reschedule gate | DOD: completed SDF jobs now check scheduled sequence against the latest pending sonar sequence before AUP shift rescheduling. | Rejected: rescheduling a stale job only to drop it later. | Estimate: avoids one wasted 8/32-ray job after overlapping ping plus floating-origin shift; exact us PENDING VERIFICATION.
- [x] Narrowed direct event signal consumption | DOD: `HandleSonarPingSent` no longer marks the latest arbitrary active `AcousticPingSignal` as consumed. The duplicate path relies on origin/intensity hysteresis instead. | Rejected: broad latest-signal consumption that could suppress an unrelated active sonar signal from another source. | Estimate: no frame cost; reduces handoff false negatives.
- [ ] Latest compile/profiler proof [BLOCKED BY EXTERNAL CORE MEMORY ERRORS] | DOD attempted: Unity `validate_script` passed 0 diagnostics for `AcousticEcholocationRaymarch.cs` and `HectonVoxelVolume.cs`; live Console now reports unrelated `GlobalDataVault.cs` Core.Memory missing references. | Estimate: measured acoustic DSP stall proof still absent.

## Continuation Pass 10: SDF Origin Self-Hit Recheck / 2026-05-13
- [x] Zero-distance echo guard | DOD: the Burst SDF raymarch origin sample can seed previous-density state but cannot satisfy `thresholdHit` or `initialSolidHit`; first audible return must come from a positive ray distance. | Rejected: allowing a density hit at `distance = 0`, because it can publish a near-zero delay tap and blow up perceived gain in malformed/inside-solid SDF starts. | Estimate: one boolean gate per sample; prevents one pathological full-gain tap, exact us PENDING VERIFICATION.
- [x] Targeted validation after self-hit guard | DOD: Unity `validate_script` returned 0 diagnostics for `AcousticEcholocationRaymarch.cs`; tracked acoustic `git diff --check` passed with CRLF warnings only. | Rejected: running broad compile-fix edits in unrelated Ecosystem code. | Estimate: no runtime change beyond the guard.
- [ ] Latest compile/profiler proof [BLOCKED BY EXTERNAL ECOSYSTEM ERRORS] | DOD attempted: live Unity Console read succeeded, but current errors are unrelated `FaunaBrain.Ecosystem.cs` missing `_ecosystemPropertyBlock` references. | Estimate: measured acoustic DSP stall proof still absent.
