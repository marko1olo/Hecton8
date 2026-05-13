Agent: ACOUSTIC_OCCLUSION_CULLING
Role: DSP_ACOUSTIC_LEAD
Status: PENDING VERIFICATION

## Decision 0 - Prompt, domain, and mandate gate
Problem: The user supplied both an agent identity and a prompt ID; the batch file lives under `Docs/Tasks/CURRENT_BATCH.md`, not project root. Wrong ID selection would contaminate this task with neighboring agent work.
Solution: Extracted only `<AGENT_PROMPT id="ACOUSTIC_OCCLUSION_CULLING">` via CLI regex. Treated `ACOUSTIC_OCCLUSION_CULLING` as the disk/log ID and `DSP_ACOUSTIC_LEAD` as role. Domain is Echelon 8 DSP Acoustic Radar from the domain authority file.
Rejected Alternatives: Did not use root `CURRENT_BATCH.md` assumption because the file is absent. Did not use the role string as status filename because the XML explicitly says `Status_ACOUSTIC_OCCLUSION_CULLING.md`.
Scalability potential: Low tier must reduce physical voices to 8 and convert the rest into perceptual virtual state; Middle/High/Ultra can spend saved cycles on richer filters, spatial spread, and selected high-value Doppler cues.
Hardware Impact: Expected gain on i3/MX350 comes from preventing 5000 boid plus 100 predator emitters from entering physical DSP. Baseline cost is unmeasured; target is bounded audio submission with 8 physical voices on Low and 16 on higher tiers.

## Decision 1 - Mandate set
Problem: Virtual voice stealing touches audio DSP, native memory, registry ownership, foveated simulation state, and crash telemetry; a narrow audio-only reading would miss lifecycle and memory hazards.
Solution: Selected eight mandates: AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC, AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation, AUDIO_Hrtf_Binaural_Spatialization, OPT_Zero_GC_Policy_AllocFree_Mandate, OPT_Native_Memory_Collections_JobSystem_Protocol, ARCH_Global_Registry_ServiceLocator_DI_Init, DBG_Telemetry_Crash_Reporting_PostMortem, REND_Foveated_Simulation_LOD.
Rejected Alternatives: Did not bulk-load all mandate files because the registry orders 2-8 task-relevant files. Did not select physics force mandates because this task is acoustic ranking, not collision truth.
Scalability potential: Low uses amplitude and low-pass fakes; Middle keeps 16 virtual-priority candidates; High/Ultra can add more expensive binaural or convolution paths only after profiler proof.
Hardware Impact: Mandates force no managed audio callbacks, no blocking audio thread, native queueing, and foveated mute. On low-end silicon the expected benefit is stable audio thread time rather than marginal frame CPU savings.

## Decision 2 - Registry-owned virtualizer instead of a new manager singleton
Problem: The prompt bans `VoiceManager.Instance`; a new audio singleton would create another boot-order dependency and collide with 20+ parallel agents.
Solution: Added `IAudioVirtualizationService` as `GlobalRegistryServiceSlot.AudioVirtualization` and made the existing `SpatialAudioManager` implement it. Signal producers still hit the audio contract; physical DSP assignment is hidden behind the registry service.
Rejected Alternatives: Did not add `VoiceManager`, `AudioVirtualizationManager.Instance`, or a scene-wide static queue. Did not make fauna depend on this implementation assembly.
Scalability potential: Low/MX350 clamps to 8 physical voices; Mid/High/Ultra keeps 16 physical voices and can spend reclaimed audio-thread budget on richer filter presentation.
Hardware Impact: i3/MX350 avoids thousands of physical `AudioSource` start attempts during swarm bursts; expected saving is audio-thread stability, not GPU frame time.

## Decision 3 - Double-buffered NativeList ranking
Problem: `SoundEmissionSignal` could previously reach `AudioSource` acquisition before relevance ranking, causing Unity DSP voice pressure.
Solution: `QueueSoundEmissionSignal` now converts to `VirtualVoiceRequest`; a Burst `VirtualVoiceSortJob` compacts audible voices, computes `Priority / (distanceSq + 1)`, and outputs the selected physical candidates.
Rejected Alternatives: Rejected `List<T>.Sort`, LINQ, managed priority queues, and Unity `AudioSource.priority`; they violate the prompt and allocate or delegate ranking to Unity internals.
Scalability potential: Low uses the same native sort but injects only 8 voices. High/Ultra can use the top-16 set as a future feed for overkill binaural/convolution detail without changing emitters.
Hardware Impact: Queue growth is capped at 8192 and guarded before `Add`; hot path target remains 0 B managed GC on low-end silicon.

## Decision 4 - Steal fade as a deterministic envelope
Problem: Replacing a physical channel instantly produces a perceptible click and violates the 10 ms steal-fade requirement.
Solution: Each virtual channel owns a pending selection and a 10 ms fade countdown. Old source volume ramps to zero before the new selection reuses the channel; Doppler ratio is copied into `_smoothedDopplerRatios` before playback.
Rejected Alternatives: Did not use coroutines, tweens, async tasks, or per-steal managed closures. Did not trust Unity voice priority to steal smoothly.
Scalability potential: Low keeps fewer fades active by limiting physical channels to 8; High/Ultra retains 16 and can later add richer transition filtering.
Hardware Impact: Fade work is a fixed 16-slot loop, negligible against i3/MX350 budgets; expected cost is below 10 us per frame.

## Decision 5 - AUP safety and outside-domain edits
Problem: Virtual voices carry acoustic positions across frame phases while floating-origin shifts can occur in the same frame. Runtime-space cached positions would become stale.
Solution: Virtual ranking stores `AbsoluteUniversePosition` and completes outstanding sort ownership during origin-shift notification. `ApplyVirtualVoiceAupShift` exists for explicit grid rebases; normal Unity floating-origin shifts do not mutate absolute AUP values. Outside-domain edits were limited to task-required audio ingress: removing runtime `AudioSource.priority` from `MusicVoicePool` and `PlayerThrusterAudio`, plus routing `FluidFeedbackListener` splash playback through `GlobalRegistry.Audio`.
Rejected Alternatives: Did not cache only `Vector3` runtime positions. Did not rewrite unrelated tool, music, breathing, or motor audio loops because the prompt domain is `SoundEmissionSignal` acoustic emission virtualization.
Scalability potential: Low keeps absolute acoustic coordinates but injects only 8 voices; Middle/High/Ultra can keep richer Doppler and filter continuity for the selected voices.
Hardware Impact: AUP ownership avoids per-shift managed repair work and keeps the MX350 path bounded to native list mutation and 8 physical channels.

## OMEGA POLISH CHANGES
Problem: The first Burst sorter used `FixedList512Bytes<int>.Capacity`, which is not worth trusting across Unity.Collections versions. The final audit also required checking for honest math, managed sorting, managed string churn, and domain creep.
Solution: Replaced the capacity property check with `FixedSortStackSafeLimit = 120`, a conservative fixed-stack guard. The sort path uses `math.lengthsq`, `math.rcp`, and multiply; no unconditional `math.sqrt` or `math.normalize` exists in `Assets/_Project/Scripts/Audio/Virtualization`. `rg` confirms no `.Sort()` or `List<T>.Sort()` in the virtualizer. No `$"..."`, `string.Format`, or `.ToString()` were added to the virtualizer hot path.
Rejected Alternatives: Did not replace the pre-existing `SpatialAudioManager.ResolveAcousticPortalReverbMix` square-root curve because it is outside the new virtual voice stealing path and controls an existing reverb presentation curve. Did not paste a managed comparer behind Burst.
Cinematic Cheats Used: Priority over squared distance instead of expensive acoustic truth; hard inaudible cutoff at `Volume * Attenuation < 0.01`; foveated Tier 2 priority zero; 8-channel Low/MX350 cap; 10 ms fixed steal envelope instead of honest simultaneous source continuity; blackbox counts/hashes instead of verbose per-voice logs.
Scalability potential: Low = 8 physical voices and aggressive perceptual silence. Middle = 16 physical voices with native ranking. High = same selected set can buy richer HRTF/filter detail. Ultra = spend reclaimed DSP budget on overkill binaural/convolution only for the top selected voices.
Hardware Impact: Expected MX350/i3 gain is bounded DSP submission and zero managed ranking GC; exact profiler number is unavailable because Unity compile/console verification is blocked.
Final Git Diff: added `Assets/_Project/Scripts/Audio/Virtualization/Contracts`, added `Assets/_Project/Scripts/Audio/Virtualization`, added registry service slot and registration in `GlobalRegistry`, integrated virtual queue/sort/channel/fade/telemetry into `SpatialAudioManager`, removed runtime `AudioSource.priority` writes in `MusicVoicePool` and `PlayerThrusterAudio`, routed `FluidFeedbackListener` splash through `GlobalRegistry.Audio`, and updated status/rationale logs.
Build Verification: `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly` is not a clean verifier in the current workspace. Earlier run reported 124 unrelated missing namespace/type errors from generated asmdefs; latest rerun after Omega patch timed out after 124 s. Unity MCP refresh timed out and console reads returned unavailable. Status remains PENDING VERIFICATION, not VERIFIED MASTER GRADE.
Batch Constraint: Current `Docs/Tasks/CURRENT_BATCH.md` no longer contains `<AGENT_PROMPT id="ACOUSTIC_OCCLUSION_CULLING">`; task 19 verification used the prompt extracted earlier and preserved in disk status/rationale.
