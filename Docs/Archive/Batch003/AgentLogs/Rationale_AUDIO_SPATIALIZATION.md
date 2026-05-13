# AUDIO_SPATIALIZATION Rationale

Agent: DSP_ACOUSTIC_LEAD
Prompt ID: AUDIO_SPATIALIZATION
Batch: CURRENT_BATCH prompt re-read for anti-amnesia protocol; stale 600 Hz audit text superseded by extracted XML
Status: PENDING VERIFICATION per assignment. Prior dotnet compile path is green; subsequent user directive forbids further dotnet builds, so continuation verification is static-only.

## DSP Decisions

Problem: True binaural HRTF convolution is too expensive and too opaque for the i3/MX350 target.
Solution: Use a psychoacoustic fake: 0.1-0.7 ms interaural time delay from the ear-axis dot, fractional ring-buffer sampling, and cheap far-ear coloration.
Rejected Alternatives: True HRTF convolution and engine spatializer plugins were rejected because they are harder to control and spend CPU on realism rather than useful fear cues.
Scalability potential: Low uses one fractional delay tap. Mid keeps the same math with better reverb density. High/Ultra can add notch coloration after profiler proof.
Hardware Impact: Estimated 80-300 us saved versus true HRTF-style processing under active voice load.

Problem: Acoustic occlusion needed solid-rock truth without Unity raycasts.
Solution: Prefer HectonVoxelVolume SDF tests before distance fallback and return the CURRENT_BATCH ~800 Hz low-pass shadow when the straight source-listener path intersects rock.
Rejected Alternatives: Physics.Raycast, RaycastNonAlloc, AudioLowPassFilter components, and collider-thickness truth were rejected by the prompt and frame-time budget.
Scalability potential: Low uses one bounded SDF path plus cache. High/Ultra can raise probe density only after profiler evidence.
Hardware Impact: Estimated 25-120 us saved per occlusion query on low-end CPU versus physics broadphase/collider work.

Problem: Cave response needed enclosure density without AudioReverbZone.
Solution: Six cardinal SDF probes estimate spans, volume, surface area, openness, and Sabine RT60 using reciprocal math.
Rejected Alternatives: AudioReverbZone, realtime reflection simulation, and acoustic mesh propagation were rejected as too slow and less predictable.
Scalability potential: Low/MX350 uses static/prebaked tail behavior. Mid uses native Sabine scalar. High/Ultra can spend cycles on richer FDN/convolution density.
Hardware Impact: Estimated 150-700 us saved per reverb refresh compared with zone/physics/reflection approaches.

Problem: Depth muffling must follow ambient pressure, not only transform depth.
Solution: Read GlobalRegistry player survival pressure and convert it into equivalent depth for high-frequency rolloff.
Rejected Alternatives: Shader global pressure polling was rejected because the audio layer should consume registered gameplay state.
Scalability potential: Same scalar works across Low/Mid/High/Ultra; higher tiers can add spectral detail without changing the contract.
Hardware Impact: Below 2 us/tick expected.

Problem: Leviathan roars lost threat when the global abyss low-pass filtered everything.
Solution: Drive a one-pole LFE bypass from the roar and inject the low-frequency energy after global depth EQ.
Rejected Alternatives: Full stereo/multichannel mixer rewrite was rejected as too much blast radius for the current audio path.
Scalability potential: Low keeps one pole. High/Ultra can route the same scalar to haptics or a real sub bus later.
Hardware Impact: Expected incremental cost under 10 us per active block.

Problem: Nitrogen narcosis needed an audio sickness cue without engine chorus components.
Solution: Values above NarcosisScalar 0.5 modulate the binaural delay read pointer with a slow sine wobble.
Rejected Alternatives: AudioChorusFilter was rejected because the prompt forbids it and engine filters hide control costs.
Scalability potential: Low uses read-pointer wobble only. High/Ultra can add asymmetric wet layers later.
Hardware Impact: Expected under 15 us per active DSP block.

Problem: Required blackbox fields were missing.
Solution: Report ActiveDSPVoices, SdfSampleTimeMicroseconds, and AudioBufferUnderruns into CrashTelemetryBuffer through fixed scalar fields written on the main telemetry tick.
Rejected Alternatives: Audio-thread file logging and string telemetry were rejected as allocation/race hazards.
Scalability potential: Low writes scalars only. High/Ultra can visualize the same buffer.
Hardware Impact: Expected under 5 us per decimated report window.

Problem: The continuation pass found dormant high-tier Unity RaycastCommand machinery inside the acoustic occlusion utility.
Solution: Removed the inactive RaycastCommand queues, hits, scheduling, and collider-response helpers; LateFrameTick stays as a no-op compatibility hook and occlusion remains SDF/distance-only.
Rejected Alternatives: Keeping dormant physics scaffolding was rejected because it contradicts the raw-SDF acoustic contract and can be reactivated accidentally later.
Scalability potential: Low through Ultra now share the same predictable SDF-first contract; higher tiers can increase SDF probe density instead of switching truth systems.
Hardware Impact: Removes cold native buffer ownership and future job scheduling risk; steady-state CPU is unchanged because the path was dormant.

Problem: Batch 03 asked for optional eardrum rupture tinnitus when massive impact signals exceed 0.9.
Solution: Bound-player physics impacts set a decaying scalar, and the audio mixer renders a low-gain 12 kHz sine through the existing tinnitus state.
Rejected Alternatives: Combat damage coupling and managed event fanout were rejected to stay inside audio ownership and avoid GC.
Scalability potential: Low renders one sine only while active. High/Ultra can add asymmetric ear ringing or filter modulation later.
Hardware Impact: Expected under 3 us per active block; no allocation and no new engine components.

Problem: The standalone Burst binaural/Doppler jobs accepted default NativeArrays and a caller-owned delay write index by value.
Solution: Added NativeArray creation guards, finite-value vaccination, power-of-two ring validation, delay-tap clamping, output clearing on invalid input, tail clearing for short buffers, and an optional NativeArray<int> write-index state slot for persistent scheduled-job ownership.
Rejected Alternatives: Trusting caller initialization or falling back to modulo wrapping was rejected because DSP rings must use bit masks and fail safe when invalid.
Scalability potential: Low stays one delay tap and cheap shadow. High/Ultra can schedule the same job safely across larger batches without losing ring position.
Hardware Impact: Expected 0-2 us overhead per scheduled block; avoids NaN propagation and audible delay reset defects.

Problem: The continuation audit found two Doppler helpers using the heavier physical ratio while the extracted XML prompt states `Pitch = 1.0 + RelativeVel/SpeedOfSound`.
Solution: Sonar echo and Leviathan pitch helpers now use the prompt formula with the existing finite guards, clamps, and 128-sample smoothing.
Rejected Alternatives: Keeping the two-sided Doppler ratio was rejected because the prompt's cheaper scalar is the active acceptance target for this agent.
Scalability potential: Low through Ultra share the same stable pitch cue; higher tiers can layer richer spectral motion without changing gameplay pitch authority.
Hardware Impact: Estimated 1-3 us saved per source update versus the extra denominator path.

Problem: Audio compile warning hygiene still carried two dead hull synthesis fields.
Solution: Removed unused GrainPlaybackRate and GrainLoopStartIndex declarations from HullSynthesisState.
Rejected Alternatives: Warning suppression was rejected because first-party audio warnings should not hide real drift.
Scalability potential: No behavior change; keeps Low through Ultra audio state smaller and easier to audit.
Hardware Impact: 0 runtime us.

Problem: The live audio producer could still trust scratch buffer capacity while the standalone Burst job had stronger fail-safe behavior.
Solution: Added a live CanProduceAudioBlock gate for all synthesis scratch buffers, stereo output scratch, binaural delay/shadow state, low-pass state, and grain bank state before any block synthesis runs.
Rejected Alternatives: Relying on _frameCapacity and initialization order was rejected because partial disposal or resize failure must fail silent, not write stale output.
Scalability potential: Low/MX350 avoids rare underrun clock corruption; High/Ultra can increase block sizes without weakening the same guard.
Hardware Impact: Expected 0-1 us per produced block.

Problem: The SPSC audio ring accepted short source buffers as partial writes while callers advanced produced sample count by the requested frame count.
Solution: TryWriteInterleaved now rejects any source whose exact frame count is unavailable.
Rejected Alternatives: Partial writes were rejected because they create silent producer/consumer clock drift.
Scalability potential: Same exact-frame contract works for mono, stereo, Low, and Ultra block sizes.
Hardware Impact: 0 runtime us in the normal path.

Problem: Non-finite live binaural params or samples could enter delay/shadow history even though the standalone job sanitized them.
Solution: Live binaural spatialization now sanitizes parameter scalars, mono samples, and sonar stereo deltas before delay-ring writes and final output.
Rejected Alternatives: Trusting snapshot validity was rejected because audio failure must collapse to silence/mono, not contaminate history.
Scalability potential: Keeps the cheap binaural path stable across all tiers; expensive HRTF tiers can layer on top later.
Hardware Impact: Expected 0-2 us per block.

Problem: Native audio consumer state can corrupt or expose unmasked shared read/write frame slots.
Solution: Producer-side SPSC reads mask shared frame indices before buffered-frame math and write eligibility checks.
Rejected Alternatives: Trusting native plugin index hygiene was rejected because one bad shared slot can make the producer miscompute capacity.
Scalability potential: The same ring remains stable across mono/stereo output and Low through Ultra block sizes.
Hardware Impact: 0 runtime us in normal operation; prevents rare index corruption instead of adding DSP work.

Problem: Non-finite granular samples could trigger binary file IO directly from the DSP producer path.
Solution: The producer now sets an atomic dump-request flag; `LateFrameTick` drains that request and performs the cold binary export on the main tick lane.
Rejected Alternatives: Writing the dump immediately from the producer was rejected because disk IO can stall audio generation and contradicts the blackbox rationale.
Scalability potential: Low/MX350 keeps the producer deterministic; High/Ultra can add richer diagnostics without changing the producer contract.
Hardware Impact: Prevents unbounded producer stall; normal-path CPU is unchanged.

Problem: The granular diagnostic dump used a subsystem filename instead of the required agent dump filename.
Solution: Renamed the binary export target to `Docs/AgentLogs/Dump_AUDIO_SPATIALIZATION.bin`.
Rejected Alternatives: Keeping `Dump_AUDIO_GRANULAR_SYNTH.bin` was rejected because the blackbox mandate names dumps by agent ID.
Scalability potential: No runtime behavior change; crash artifacts remain easier to collect across Low through Ultra.
Hardware Impact: 0 runtime us.

Problem: Producer-side `AudioBufferUnderruns` counted every low-buffer polling pass, so one starvation window could flood the blackbox with repeated underrun counts.
Solution: Added an atomic underrun-window latch. The producer increments the counter only on the transition into low-buffer state, clears the latch after recovery, and resets the latch during buffer reinitialization/disposal.
Rejected Alternatives: Counting every producer poll was rejected because it measures loop frequency, not incident count. Moving the counter to disk or logs was rejected as hot-path noise.
Scalability potential: Low/MX350 telemetry stays readable under sustained audio starvation; High/Ultra can still inspect incident count without changing the SPSC contract.
Hardware Impact: 0 runtime us in the normal path; prevents telemetry spam and blackbox churn during starvation.

Problem: The underrun latch still treated startup lead-fill as a starvation window after the first block was produced.
Solution: Gate underrun accounting until the producer has generated more frames than the configured target lead; cold prefill is now silent, runtime drain below one block is still counted.
Rejected Alternatives: Counting any post-first-block low buffer was rejected because it reports initialization, not an underrun. Sleeping until target lead without telemetry was rejected because real post-start starvation still needs blackbox evidence.
Scalability potential: Low/MX350 gets cleaner incident counts during startup and device switches. High/Ultra can use the same count for richer audio diagnostics without changing the producer ring.
Hardware Impact: 0 runtime us in the normal path; removes false positive blackbox churn during initial prefill.

Problem: The impact audio event queue still used raw volatile read/write slots for fixed-array access in one producer/consumer path.
Solution: Mask read/write slots before array indexing and full/empty comparisons while preserving the raw observed read slot for the CAS guard.
Rejected Alternatives: Trusting SPSC slot hygiene was rejected because one corrupted slot can become an out-of-range array access instead of a dropped event.
Scalability potential: Low through Ultra keep the same allocation-free impact cue path and drop-oldest policy.
Hardware Impact: 0 runtime us in the normal path; prevents rare queue-corruption failure without adding DSP work.

Problem: The low-tier reverb path could still spend work intended for higher tiers. SDF enclosure sampling had already been reduced, but enclosure density could wake the native interior FDN even when the selected tier was `UnityProfileOnly`.
Solution: Skip six-point SDF enclosure probes on `UnityProfileOnly` and force interior FDN send to zero unless the active reverb tier is native DSP. Low/MX350 keeps only the static biome tail/mixer profile; Mid retains Sabine FDN; High/Ultra retain richer native density.
Rejected Alternatives: Running one shared FDN path on every tier was rejected because the prompt explicitly disables Sabine FDN on MX350. Removing all listener reverb fallback was rejected because authored mixer/filter profiles provide the cheap static tail without spending native DSP blocks.
Scalability potential: Low uses static biome tail values. Mid uses Sabine scalar and FDN. High can layer native density. Ultra can spend the saved low-tier branch budget on convolution density or richer coloration after profiler proof.
Hardware Impact: Estimated 25-120 us saved per low-tier reverb refresh by skipping SDF enclosure probes and 40-180 us saved per enclosed audio block by keeping the FDN cold.

Problem: The low-tier FDN kill switch needed a cheap no-regression guard.
Solution: Added editor smoke-test source assertions in the advanced acoustics and DSP thread-safety validators for `nativeReverbActive` and `interiorFdnSend` tier gating.
Rejected Alternatives: Relying on manual review was rejected because this is a recurring performance contract, not a one-off cleanup.
Scalability potential: Low-tier protection remains visible during editor validation while Mid/High/Ultra behavior stays unchanged.
Hardware Impact: 0 runtime us; editor-only validation prevents future low-tier DSP regressions.

Problem: The SPSC producer still copied stereo blocks through a generic per-channel inner loop even though the shipped live bridge writes mono or stereo only.
Solution: Split `TryWriteInterleaved` into a stereo fast path with `<< 1` addressing and a mono fallback after exact-frame and channel validation.
Rejected Alternatives: Keeping a generic nested channel loop was rejected because it spends a branch/counter per channel for no current runtime flexibility. Adding arbitrary channel-count support was rejected because the native bridge contract is mono/stereo.
Scalability potential: Low/MX350 gets the cheapest producer copy. Mid/High/Ultra keep the same exact-frame SPSC contract and can spend saved cycles on richer DSP layers instead of copy overhead.
Hardware Impact: Estimated 1-4 us saved per producer block on MX350-class CPU, pending profiler proof.

Problem: The SPSC producer accepted invalid `sourceChannels` by clamping to mono/stereo before validating against the ring's configured channel count.
Solution: Reject `sourceChannels < 1 || sourceChannels > 2` before computing source stride, then copy only when the explicit channel count matches the configured ring.
Rejected Alternatives: Silent clamping was rejected because a bad caller can reinterpret an invalid interleaved source buffer without surfacing failure. Supporting arbitrary channel counts was rejected because this bridge is a mono/stereo native audio contract.
Scalability potential: Low through Ultra preserve the same explicit channel contract; future multichannel output requires a new bridge contract rather than hidden widening.
Hardware Impact: 0 runtime us in the normal path; failure-path safety avoids corrupting the SPSC clock with wrongly-strided input.

Problem: Editor smoke tests still contained stale acceptance text after the prompt-aligned 0.7 ms ITD and SDF hard-shadow constants were restored.
Solution: Updated AdvancedAcoustics and DSPThreadSafety assertions to the current 0.7 ms ITD cap, `SdfOcclusionTransmission01 = 0.18f`, `SdfOcclusionLowPassHertz = 800f`, and added SPSC stereo fast-path assertions.
Rejected Alternatives: Leaving stale 0.6 ms and old voxel names was rejected because it creates false failures and hides real regressions.
Scalability potential: Editor-only guards protect Low through Ultra audio contracts without adding runtime work.
Hardware Impact: 0 runtime us; prevents regression in the low-cost path.

## Integration Repairs

Problem: The prior compile wall hid behind missing types that already existed in source files.
Solution: Added existing contract files to Hecton8.Core.csproj: suit resolver, suit mesh events, platform policies, native bridge, VRAM tracker, haptics waveform, and voxel modification events.
Rejected Alternatives: Duplicating SuitStats/SuitUpgrades or stubbing missing platform classes was rejected because the real implementations were already present.
Scalability potential: Restores existing Low/High platform gates and event lanes instead of weakening them.
Hardware Impact: No direct runtime cost; it enables the correct compiled code paths.

Problem: Three non-audio call sites used ambiguous AudioEvent after both Hecton8.Audio and Hecton8.Core were in scope.
Solution: Added CoreAudioEvent aliases in PhysicalPanelButton, SoundscapeSystem, and HectonSubmarineOS so IAudioService receives the zero-GC core event payload.
Rejected Alternatives: Renaming event types or removing using directives was rejected as broad churn.
Scalability potential: Keeps UI/world/submarine audio alarms on the shared NativeQueue path.
Hardware Impact: Preserves 20-80 us/event-burst savings by avoiding point-clip fallback behavior.

Problem: PDAMapTab briefly had a dead CPU point-cloud fallback beside an existing compute append-buffer/indirect-draw path.
Solution: Removed the unused CPU payload struct, NativeArray, GraphicsBuffer, upload flags, and cleanup code while preserving the compute path.
Rejected Alternatives: Keeping parallel dead buffers was rejected because it adds memory ownership risk without improving the rendered sonar path.
Scalability potential: The existing compute path remains the single authoritative point-cloud renderer; Low tier can still be governed by shader/compute dispatch policy.
Hardware Impact: No runtime cost added; removed dead allocation risk.

Problem: WorldChunkResidencyManager passed NativeArray index expressions by explicit in/ref.
Solution: Copy the blit value to a local and call distance helpers without explicit in at the expression sites.
Rejected Alternatives: Rewriting streaming math or changing AUP layout was rejected.
Scalability potential: Predictive streaming behavior remains unchanged.
Hardware Impact: No meaningful runtime change; compile correctness restored.

Problem: The compiler reported a missing Sargassum fallback method that was present in the same class.
Solution: Shut down the dotnet build server and rebuilt; the stale error disappeared.
Rejected Alternatives: Duplicating the method was rejected because it would create real code debt.
Scalability potential: None; this was verification hygiene.
Hardware Impact: None.

Problem: A re-audit found the status/rationale and implementation drifted to 600 Hz even though the extracted `CURRENT_BATCH.md` XML requires a harsh low-pass cutoff around 800 Hz.
Solution: Set `SdfOcclusionLowPassHertz` to `800f` and corrected the audit files to match the active prompt.
Rejected Alternatives: Keeping the 600 Hz value was rejected because no visible current XML directive supports it.
Scalability potential: Same scalar works on Low through Ultra; material coloration can layer later.
Hardware Impact: No CPU delta.

Problem: Full compile exposed ScannerTool as not satisfying IDispatcherRaycastReceiver even though the raycast callback existed.
Solution: Converted the callback to a public implicit ConsumeDispatcherRaycastHit implementation while preserving scanner behavior.
Rejected Alternatives: Editing dispatcher contracts or disabling scanner raycast flow was rejected as cross-domain churn.
Scalability potential: Keeps queued dispatcher raycasts decoupled from audio and preserves existing scanner scaling.
Hardware Impact: 0 runtime us.

Problem: Full compile later exposed an external `SubmarineStructuralGrid` late-frame registration contract mismatch.
Solution: Added the already-implemented `ILateFrameTickable` interface declaration and the missing `_registeredLateFrame` flag only.
Rejected Alternatives: Broad submarine/physics refactoring was rejected because this was a compile-contract repair outside audio ownership.
Scalability potential: Restores the existing late-frame leak-plume presentation lane without changing its scheduling model.
Hardware Impact: 0 runtime us.

## Verification

Problem: The project needed an honest compile result after integration drift repairs and producer telemetry hardening.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -v:minimal`.
Rejected Alternatives: Reporting only targeted validators was rejected after cross-domain project-file drift was found.
Scalability potential: Full core assembly now includes hardware-tier/platform files needed for Low/High gates.
Hardware Impact: 0 runtime us. Latest result: PASS, 0 errors, 0 warnings.

Problem: Core green does not prove the main Unity assembly path.
Solution: Ran `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /nr:false -v:minimal`.
Rejected Alternatives: Stopping at Hecton8.Core was rejected.
Scalability potential: Confirms main assembly references can compile with the restored contracts.
Hardware Impact: 0 runtime us. Latest result: PASS, 0 errors, 12 package/vendor warnings.

Problem: Unity editor console verification could not complete cleanly.
Solution: Retried MCP console/resource verification after the local builds. Stale fauna reports contradicted current source and dotnet compile; after reconnect, `read_console` for errors returned 0 entries.
Rejected Alternatives: Claiming editor-console green from local dotnet compile alone was rejected before the successful MCP error query; editing the fauna solver was rejected because current source already had the relevant compile surface corrected.
Scalability potential: None.
Hardware Impact: None. Status stays PENDING VERIFICATION because the assignment explicitly requires pending state even with local compile and current MCP error query green.

Problem: The final continuation pass occurred after an explicit user instruction not to build or run dotnet build.
Solution: Performed static source inspection, focused reverb branch readback, forbidden DSP/SDF pattern scan, and `git diff --check` only.
Rejected Alternatives: Running another project build or Unity script refresh was rejected because it would violate the latest user instruction.
Scalability potential: None; this was verification discipline.
Hardware Impact: 0 runtime us. Latest static checks: forbidden-pattern scan PASS; `git diff --check` PASS with CRLF normalization warnings only.

Problem: The live `Docs/Tasks/CURRENT_BATCH.md` no longer contains the `AUDIO_SPATIALIZATION` XML block.
Solution: Treated this as a batch hygiene/extraction block, ignored the neighboring `AUDIO_VWS_SYSTEM` prompt, and continued from the existing on-disk status/rationale record.
Rejected Alternatives: Reading a different agent prompt was rejected because it would contaminate the active architecture decisions. Reading archived batch logs was avoided because the current AGENTS hygiene rule forbids previous-batch logs unless explicitly ordered.
Scalability potential: None; this is prompt isolation discipline.
Hardware Impact: 0 runtime us. Latest static verification: stale smoke assertion scan PASS; forbidden scan contains only editor assertion strings; `git diff --check` PASS with CRLF normalization warnings only. Unity `read_console` is blocked by MCP HTTP transport failure at `127.0.0.1:8088/mcp`.
