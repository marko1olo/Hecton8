# AUDIO_VWS_SYSTEM Rationale

Status: PENDING VERIFICATION

## Initial Decision Set

Problem: Existing warning audio may overlap and may rely on direct Unity audio calls.
Solution: Audit audio contracts and runtime before implementation; use `GlobalRegistry` + fixed native queue if ownership gap exists.
Rejected Alternatives: Direct `AudioSource.PlayClipAtPoint`, singleton access, string event names, managed list queues.
Scalability potential: Low uses priority clips without radio degradation; Middle keeps queue and ducking; High adds radio degradation; Ultra can spend saved CPU on richer speaker failure filtering once measured.
Hardware Impact: Expected low-end i3/MX350 gain is reduced GC and fewer overlapping clips; exact microseconds pending measurement.

Problem: VWS queue must remain deterministic under simultaneous warnings.
Solution: Fixed 16-slot queue with byte warning IDs and Burst-compatible priority sort.
Rejected Alternatives: `List<AudioClip>`, `Dictionary<string, AudioClip>`, coroutine-based cooldowns.
Scalability potential: Low uses flat priority and cooldowns; Middle/High/Ultra can enable richer DSP degradation while preserving the same queue state.
Hardware Impact: Fixed native memory avoids managed allocations and bounds queue scan cost; exact microseconds pending profiling.

## Loop 1-2 Decisions

Problem: Warning producers were crossing directly into audio playback, so priority and cooldown policy could not be enforced.
Solution: Converted submarine VWS, player vital, crush-depth, hull-breach, and brownout paths into native `GlobalSignals` lanes consumed by `VocalWarningSystem`.
Rejected Alternatives: Leaving `IAudioService.QueueAudioEvent` in `HectonSubmarineOS`; keeping `PlayStatic2D` for crush-depth groans; adding a scene singleton.
Scalability potential: Low/MX350 pays only byte enqueue and fixed sort; Middle keeps strict priorities; High/Ultra can enable speaker degradation without changing producers.
Hardware Impact: Estimated gain on i3/MX350 is removal of overlapping managed audio requests and bounded 16-slot queue, roughly 2-8 us saved on warning bursts plus zero GC.

Problem: Hard ASMDEF isolation cannot be completed without severing existing cross-domain audio dependencies.
Solution: Bound VWS through `IVocalWarningSystem` in `GlobalRegistry` and marked ASMDEF isolation blocked by dependency until audio files are physically split by module owners.
Rejected Alternatives: Creating a decorative `Hecton8.Audio.asmdef` that compiles no meaningful audio runtime or duplicating contracts.
Scalability potential: The contract lets a later pure audio assembly depend only on Contracts once `PlayerCriticalProceduralAudioRenderer` dependencies are extracted.
Hardware Impact: No runtime cost; avoids compile churn and dependency breakage on low-end devices.

Problem: Preemption needed to interrupt speech without allocations or Unity audio source churn.
Solution: Added scalar `VwsPlaybackState` in the procedural audio renderer; a higher-priority pending byte ID starts a 50 ms fade and swaps the PCM buffer after fade completion.
Rejected Alternatives: Coroutine fade, new `AudioSource`, managed clip queue, or per-warning class objects.
Scalability potential: Low uses the same scalar envelope; High/Ultra can spend cycles on degradation while preserving deterministic queue state.
Hardware Impact: Estimated fade cost is about 0.03 us/sample during the 50 ms window; idle cost is a pending integer read.

## Loop 3-4 Decisions

Problem: Vocal warnings need to cut through ambient current without muting every critical audio layer.
Solution: Ducked only the procedural ambient-current component by 0.5 while VWS is active, then mixed the warning sample outside heartbeat ducking.
Rejected Alternatives: Mixer snapshot, global master duck, or disabling sonar/hull stress layers.
Scalability potential: Low keeps the same branch; High/Ultra still preserve environmental drama while speech stays intelligible.
Hardware Impact: Estimated i3/MX350 cost is one branch and multiply per sample; saved cost versus mixer automation is no managed state and no Unity mixer transition work.

Problem: Damaged habitat radio effect must read as failing speakers without a heavy simulation.
Solution: Used a cheap one-pole low-pass plus 5-sample bit-crush hold inside `ApplyVwsRadioDegradation`.
Rejected Alternatives: Convolution, dynamic speaker cone model, FM radio noise bed, or simulating electronics.
Scalability potential: Low/MX350 disables the branch; Middle uses the cheap fake; High/Ultra can later layer noise without changing queue contracts.
Hardware Impact: On low-end silicon the branch is skipped; on high tiers cost is roughly 0.04 us/sample only during active VWS.

Problem: Localization and subtitles must not resurrect string-keyed audio lookups.
Solution: Language changes swap the active flat `AudioClip[]`; playback emits hash-based `SubtitleSignal` and uses `SubtitleManager.DisplaySubtitle` span path.
Rejected Alternatives: `Dictionary<string, AudioClip>`, producer-side caption event, or per-warning localized ScriptableObject lookup at playback time.
Scalability potential: Low/Middle/High/Ultra share identical O(1) clip indexing; high tiers spend saved CPU on DSP polish, not lookup.
Hardware Impact: Runtime warning playback uses one array read; language switch is cold-path only.

Problem: AUP origin shifts could accidentally reset world-bound audio if warnings were spatialized.
Solution: VWS playback is internal to `PlayerCriticalProceduralAudioRenderer`; no AUP subscription, no world transform, no delay pointer reset.
Rejected Alternatives: 3D warning AudioSource attached to submarine/player world coordinates.
Scalability potential: Same behavior on toaster and high-end hardware; determinism is preserved through scalar sample cursors.
Hardware Impact: Zero work on AUP shift; avoids reallocating or retargeting audio sources.

## Compile Wall

Problem: Compile verification cannot reach VWS/Burst code because the generated Core project stops on unrelated missing domains and an already-open Unity editor prevents a clean batch compile owner.
Solution: Per 3-strikes protocol, Task 19 is marked `[BLOCKED BY DEPENDENCY]` after Unity batch launch, Core csproj build, solution build, and no-project-reference Core build all failed before VWS-specific diagnostics.
Rejected Alternatives: Killing other Unity editor sessions, editing generated csproj dependency graphs, or reverting unrelated domain changes.
Scalability potential: No runtime impact; once Core dependencies are repaired, VWS has fixed low/mid/high/ultra behavior already gated by `GlobalRegistry.ScalabilityTier`.
Hardware Impact: No hardware impact from the compile wall; VWS low-tier branch skips radio degradation and preserves bounded queue cost.

## Omega Polish Decisions

Problem: The staging `NativeQueue<byte>` could grow past the mandated 16 queued warning IDs during a burst before the next `SlowTick`.
Solution: Capped ingress by checking `_pendingNativeCount` against `QueueCapacity`; overflow promotes directly into the fixed `_vwsQueue` through the same priority replacement path.
Rejected Alternatives: Letting `NativeQueue` resize, dropping every overflow event, or adding a managed spill list.
Scalability potential: Low/MX350 keeps a hard 16-entry ceiling; Middle preserves deterministic replacement; High/Ultra spend no extra queue memory and can still preempt with stronger DSP polish.
Hardware Impact: Estimated i3/MX350 gain is bounded native queue memory and no burst-time managed fallback; hot warning burst cost stays at <=1.0 us fixed scan instead of allocator-dependent growth.

Problem: Language switching used `Enum.IsDefined`, a cold but unnecessary reflection/boxing path.
Solution: Replaced it with contiguous integer bounds using `GameLanguage.English` through `GameLanguage.Arabic`.
Rejected Alternatives: `Enum.IsDefined`, dictionary lookup, or per-language string lookup.
Scalability potential: Low/Middle/High/Ultra all use the same flat bundle swap; top-tier visual/audio overkill remains in DSP, not localization lookup.
Hardware Impact: Cold-path only, but removes avoidable allocation/reflection risk on cheap devices.

Problem: Cross-domain files had to publish warnings without violating ownership boundaries.
Solution: Gameplay, movement, structural, and fluid systems now emit `GlobalSignals` only; `VocalWarningSystem` owns audio policy, cooldown, priority, subtitles, and DSP submission.
Rejected Alternatives: Direct audio playback from player/submarine scripts, singleton calls, or domain-specific dependencies on audio runtime objects.
Scalability potential: Low uses cheap byte signals; Middle keeps strict cooldown and ducking; High adds radio fake; Ultra can enhance degradation without touching producers.
Hardware Impact: Estimated burst gain is 2-8 us on i3/MX350 by removing overlapping managed audio requests and centralizing fixed queue admission.

Problem: Final verification was required after `OMEGA_POLISH`.
Solution: Re-ran no-project-reference Core build and purge scans. Build remains blocked before VWS diagnostics by missing Core/Cartography/Determinism/DataVault dependencies; purge scans are clean for VWS singleton, managed clip queues, string clip dictionaries, and coroutine cooldowns in the warning path.
Rejected Alternatives: Killing active Unity editor processes, repairing unrelated missing domains, or editing generated project references.
Scalability potential: No runtime change; verified architecture still has low/middle/high/ultra gates through `GlobalRegistry.ScalabilityTier`.
Hardware Impact: No runtime impact from the compile wall; VWS low tier remains cheapest approximation and high tiers reserve saved cycles for audible degradation.

## 2026-05-13 Continuation Decisions

Problem: VWS was reading `GlobalRegistry.PlayerCriticalAudio` from `Tick()` and renderer/subtitle/scalability registry slots from helper paths reachable during `SlowTick`.
Solution: Cached `PlayerCriticalProceduralAudioRenderer`, `SubtitleManager`, `LocalizationManager`, and the current quality tier during lifecycle setup; VWS now listens to `ScalabilityEvents` for tier changes.
Rejected Alternatives: Leaving registry reads in hot helpers, adding per-frame service refresh, or wiring concrete cross-domain dependencies into producers.
Scalability potential: Low/MX350 keeps cheapest branch with no radio fake; Middle/High/Ultra can keep stronger degradation without registry polling.
Hardware Impact: Estimated i3/MX350 gain is small but deterministic: removes 2-4 registry property reads per active VWS tick/SlowTick path and prevents hidden service-bus use as live config.

Problem: `NativeQueue<byte>` admission was logically capped but not prewarmed, so the first warning burst could allocate native backing storage during gameplay.
Solution: Prewarmed the staging queue to 16 entries inside `EnsureNativeStorage()` and drained it before runtime use.
Rejected Alternatives: Trusting NativeQueue lazy allocation, replacing it with a managed queue, or increasing fixed capacity without proof.
Scalability potential: All tiers now pay the native queue storage cost cold; Ultra still uses the same deterministic queue and spends saved time in DSP polish.
Hardware Impact: Removes first-burst native allocation risk on i3/MX350; steady-state queue cost remains <=1.0 us bounded scan/sort.

Problem: `CancelCurrentWarning()` cleared VWS bookkeeping but did not actually stop the renderer's active PCM playback.
Solution: Added an audio-thread-safe cancellation request flag in `PlayerCriticalProceduralAudioRenderer`; the DSP producer consumes the flag, clears active/pending VWS state, and returns silence without main-thread struct races.
Rejected Alternatives: Writing `_vwsPlaybackState` directly from main thread, spawning a fader object, or waiting for clip exhaustion.
Scalability potential: Low/Middle/High/Ultra share the same scalar cancel path; top tiers do not pay extra allocations for emergency warning suppression.
Hardware Impact: One volatile branch in the VWS sample path while VWS is active; zero idle audio cost outside the existing warning render call.

Problem: Non-finite authoring or clip sample data could leak NaN into native warning severity or the procedural mix.
Solution: Added finite guards for queued severity, fallback cooldown, submitted gain, and mixed PCM samples before DSP output.
Rejected Alternatives: Assuming inspector values and AudioClip PCM are always clean, or logging every bad sample.
Scalability potential: All tiers get deterministic silence fallback for corrupt values; High/Ultra degradation cannot amplify NaN into the full mix.
Hardware Impact: Negligible branch cost during warning admission/playback; avoids catastrophic NaN propagation that would cost far more than the guard.

Problem: Final recheck was requested without `dotnet build`.
Solution: Ran static purge scans and `git diff --check` only. No build command was launched. `CURRENT_BATCH.md` prompt extraction required an attribute-aware regex because the tag includes `role` and `chat_name`.
Rejected Alternatives: Running `dotnet build`, relying on chat memory, or modifying unrelated dirty files owned by other agents.
Scalability potential: No runtime change from verification method.
Hardware Impact: No runtime impact; status remains `PENDING VERIFICATION` because Unity/runtime/profiler evidence is still absent.

Problem: Caching registry services removed hot polling but created a lifecycle fault when VWS enabled before `PlayerCriticalProceduralAudioRenderer`, `SubtitleManager`, or `LocalizationManager`.
Solution: Registered VWS as an `IGlobalRegistryHotSwapRefListener` and rebound cached renderer/subtitle/localization pointers through the existing deferred service-rebound queue; localization rebound also reselects the flat clip bundle.
Rejected Alternatives: Reintroducing per-frame registry polling, scene-order dependence, serialized concrete references, or producer-side audio calls.
Scalability potential: Low/MX350 keeps zero registry reads in the VWS tick path; Middle/High/Ultra can hot-replace services for richer audio/subtitle stacks without changing signal producers.
Hardware Impact: Prevents silent VWS failure from load order while preserving the prior 2-4 registry-read savings per active VWS tick/SlowTick path on i3/MX350.

Problem: External callers could invoke `CancelCurrentWarning()` after native teardown, and queue clearing assumed native storage was still alive.
Solution: Added a native-allocation guard before clearing from public cancel and made `ClearQueuedWarnings()` check `NativeQueue`/`NativeArray` creation before draining or zeroing.
Rejected Alternatives: Assuming Unity lifecycle order, throwing on late cancel, or leaving stale warnings in the renderer.
Scalability potential: All tiers get deterministic no-op behavior after teardown; Ultra does not need separate cancellation machinery.
Hardware Impact: No measurable hot-path cost; avoids rare teardown exceptions that would block runtime recovery on cheap devices.

Problem: Disabling VWS or replacing the player critical audio renderer could leave a previously submitted warning PCM lane playing without an owning queue.
Solution: VWS now calls the renderer cancellation lane before unregistering, and the registry service-replaced hook cancels the previous renderer instance when `PlayerCriticalAudioRuntime` is swapped.
Rejected Alternatives: Waiting for the clip to expire, relying on Unity component destruction order, or clearing only VWS bookkeeping.
Scalability potential: Low/Middle/High/Ultra keep one authoritative cancellation path; hot-swapped premium renderers cannot inherit stale low-tier warning state.
Hardware Impact: Cold lifecycle only; prevents orphan playback without adding per-sample or per-frame work.
