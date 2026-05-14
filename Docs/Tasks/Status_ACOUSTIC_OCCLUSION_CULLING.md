Agent: ACOUSTIC_OCCLUSION_CULLING
Role: DSP_ACOUSTIC_LEAD
Domain: Echelon 8 Presentation & UX / DSP Acoustic Radar
Batch source: Docs/Tasks/CURRENT_BATCH.md
Status: PENDING VERIFICATION
Task count: 19
Compile status: blocked by stale/generated project graph; earlier `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly` reported 124 missing namespace/type errors across unrelated generated asmdefs before isolating this patch. Controlled rerun with `/m:1 /p:UseSharedCompilation=false` completed and reports 130 missing namespace/type errors from stale asmdef/csproj references, including missing generated references for `Hecton8.Audio.Virtualization`. Latest full build attempt timed out after 184 s and left stale MSBuild node-reuse children, which were stopped by parent PID after verification. Unity MCP validate/read_console is not reachable on `127.0.0.1:8088`. Focused Mono compile of `AcousticPortalPropagation.cs` + virtualizer contracts + `AudioVirtualizationJobs.cs` passes after replacing the full sorter with one-pass bounded top-K selection; Roslyn parse passes for `AudioVirtualizationJobs.cs` and `SpatialAudioManager.cs` after dead queue removal.

[ANALYSIS]
Target: Virtual voice stealing for fauna/boid/predator sound emissions before they hit DSP buffers.
Affected systems: Hecton8.Audio, Hecton8.Audio.Contracts, GlobalRegistry audio service slots, SystemDispatcher tick phases, foveated simulation contracts, crash telemetry.
Zero GC proof: NativeList/NativeArray only in hot voice queue, no managed List.Sort, no LINQ, no AudioSource priority reliance, no per-frame registry polling, no string formatting in tick path.
State check: status/rationale files were missing at start; CURRENT_BATCH prompt was extracted by CLI; neighboring prompts ignored.
Rule quote: AGENTS.md forbids classic singletons and hot-path GC; prompt forbids List<T>.Sort and requires 16 physical voices, 8 on Low tier.
Selected mandates:
- AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt
- AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt
- AUDIO_Hrtf_Binaural_Spatialization.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- REND_Foveated_Simulation_LOD.txt

## Loop 0 - Setup
- [x] Prompt extraction and isolation | DOD: PowerShell raw regex extracted only `<AGENT_PROMPT id="ACOUSTIC_OCCLUSION_CULLING">`; neighboring task blocks discarded. Alternative rejected: relying on chat text or MCP read. Estimate: 120 us.
- [x] Domain verification | DOD: `Docs/Actual Domains of Project.txt` maps this work to Echelon 8 DSP Acoustic Radar: zonal sound occlusion and muffling without ray checks. Alternative rejected: treating this as AI/fauna ownership. Estimate: 60 us.
- [x] Mandate selection | DOD: eight task-relevant registry mandates identified before code. Alternative rejected: broad registry bulk-read. Estimate: 90 us.
- [x] Baseline code reconnaissance | DOD: located `SpatialAudioManager` signal ingress, `GlobalRegistry` service slots, `IFoveatedSimulationDirector`, `SystemDispatcher` fast/late phases, acoustic portal blackbox pattern, and runtime `AudioSource.priority` usage. Alternative rejected: inventing a parallel voice manager. Estimate: 310 us.

## Phase 1 - Purge & Isolation
- [x] Task 1 - SINGLETON ERADICATION: Purge `VoiceManager.Instance`; register `IAudioVirtualizationService`. | DOD: no `VoiceManager` references found; `GlobalRegistry.AudioVirtualization` slot added; `SpatialAudioManager` registers/unregisters service. Alternative rejected: static singleton facade. Estimate: 45 us per lookup avoided.
- [x] Task 2 - SIGNAL MIGRATION: Intercept `SoundEmissionSignal` before DSP buffers. | DOD: `QueueSoundEmissionSignal` now builds `VirtualVoiceRequest` and bypasses immediate physical `AudioSource` dispatch. Alternative rejected: queueing then culling after source acquisition. Estimate: avoids up to 5100 physical play attempts per burst.
- [x] Task 3 - ASMDEF ISOLATION: `Hecton8.Audio.Virtualization` -> Contracts. | DOD: created `Hecton8.Audio.Virtualization.Contracts` and `Hecton8.Audio.Virtualization`; Core references explicit assemblies. Alternative rejected: dumping structs into Core contracts. Estimate: 80 us compile-domain isolation benefit during iteration.
- [x] Task 4 - DEAD CODE HUNT: Remove reliance on Unity `AudioSource.priority`. | DOD: runtime assignments removed from `MusicVoicePool` and `PlayerThrusterAudio`; remaining priority assignment is editor asset metadata. Alternative rejected: hiding priority behind constants. Estimate: 0 us runtime sort dependence.

## Phase 2 - Voice Sorting (Burst)
- [x] Task 5 - THE VIRTUAL QUEUE: Maintain `NativeList<VirtualVoice>`. | DOD: double-buffered persistent `NativeList<VirtualVoice>` write/sort queues with capacity guard and no growth in hot path. Alternative rejected: managed `Queue`/`List`. Estimate: bounded 8192 enqueue path, 0 B managed GC.
- [x] Task 6 - SORTING JOB: FastTick sort by priority over distance squared. | DOD: `VirtualVoiceSortJob` runs from `FastTick` and ranks with `Priority / (distanceSq + 1)`, but only maintains the top physical voice budget. Alternative rejected: `List<T>.Sort`, Unity priority, and full-list native quicksort when only 8/16 voices can be heard. Estimate: O(n*8) Low, O(n*16) Mid/High/Ultra, native only.
- [x] Task 7 - CULLING: Drop inaudible voice when `Volume * Attenuation < 0.01f`. | DOD: Burst compaction skips voices below `VirtualVoiceUtility.MinimumAudibleEnergy`. Alternative rejected: passing inaudible sources to Unity rolloff. Estimate: saves one DSP setup per culled voice.

## Phase 3 - Voice Stealing
- [x] Task 8 - THE CHANNELS: Take top 16 sorted voices. | DOD: selection buffer is `NativeArray<VirtualVoiceSelection>[16]`; late-frame injection maps only selected voices to physical channels. Alternative rejected: expanding pool size under load. Estimate: caps fauna/predator DSP voices at 16.
- [x] Task 9 - SMOOTH CROSSFADE: 10 ms fade-out envelope on stolen channels. | DOD: stolen channel stores pending selection and ramps old source volume over `0.01f` seconds before reuse. Alternative rejected: immediate `Stop()`/clip overwrite. Estimate: fixed 16-slot fade loop under 10 us.
- [x] Task 10 - FOVEATED MUTE: Clamp frozen entity acoustic priority to zero. | DOD: `ResolveVirtualVoiceFoveatedTier` reads cached `IFoveatedSimulationDirector`; Tier 2 requests enter with priority 0 and are culled by Burst. Alternative rejected: asking fauna emitters to self-silence. Estimate: no physical voices for frozen predators.

## Phase 4 - Safety & LOD
- [x] Task 11 - AUP SHIFT SAFETY: Rebase virtual voices synchronously. | DOD: service exposes `ApplyVirtualVoiceAupShift`; `SpatialAudioManager` listens to origin shifts and completes outstanding sort ownership before post-shift use. Alternative rejected: runtime-space cached positions in virtual queue. Estimate: 0 B GC and no stale job handle.
- [x] Task 12 - MATH LOD: Low tier restricts physical voices to 8. | DOD: `RefreshVirtualPhysicalVoiceLimit` clamps Low/MX350/fallback-memory profile to 8, otherwise 16. Alternative rejected: single balanced cap. Estimate: halves virtualized DSP channels on MX350.
- [x] Task 13 - ZERO-GC: NativeArrays/Burst only for sorting and fading. | DOD: sort buffers/statistics/blackbox are persistent `NativeList`/`NativeArray`; fades are fixed cold arrays. Alternative rejected: coroutines/tweens/managed sort. Estimate: 0 B managed GC in ranking and channel fade loops.
- [x] Task 14 - EXECUTION PHASE: Sort POST_SIMULATION, inject VISUAL_SYNC. | DOD: `FastTick` schedules sort after simulation lanes; `LateFrameTick` completes and injects before audio event drain. Alternative rejected: sorting inside audio callbacks. Estimate: main-thread ownership stays deterministic.
- [x] Task 15 - BLACKBOX DUMP: Push culled/active voice counts to telemetry. | DOD: 300-frame `NativeArray<VirtualVoiceTelemetryEntry>` stores counts/hash; telemetry publishes active and culled counts; NaN loudest weight dumps to `Docs/AgentLogs/Dump_ACOUSTIC_OCCLUSION_CULLING.bin`. Alternative rejected: chat-only diagnostics. Estimate: fixed 32 B per frame.
- [x] Task 16 - DOPPLER SYNC: Preserve Doppler pitch through handoff. | DOD: `VirtualVoiceSelection.DopplerRatio` is written into `_smoothedDopplerRatios` before `ResolveSourcePitch`. Alternative rejected: resetting pitch to 1 on every steal. Estimate: preserves acoustic continuity.
- [x] Task 17 - RECONNAISSANCE: Scan for `AudioSource.Play()` bypasses. | DOD: scan found non-central direct play sites in Atmosphere, Music, Fabricator, tools, MantaScooter, VRSomatic, PlayerThruster, and central audio renderers; `FluidFeedbackListener` splash bypass was routed through `GlobalRegistry.Audio`. Alternative rejected: broad cross-domain rewrites. Estimate: one local bypass removed.
- [x] Task 18 - OMEGA COMPILE CHECK: Verify Burst sort allocation safety. | DOD: static scan verifies no `.Sort()`/`List<T>.Sort()` in virtualizer files; Burst job uses a fixed top-K insertion selector over a `NativeArray<VirtualVoiceSelection>[16]` instead of `FixedList` stack sorting. Alternative rejected: managed comparer sort and full-list quicksort. Estimate: 0 B managed allocation in sort path.

## Recursive Re-Verification
- [x] Task 19 - Re-read prompt and verify no `List<T>.Sort()` or managed hot-path sorting remains. | DOD: current `CURRENT_BATCH.md` no longer contains the `ACOUSTIC_OCCLUSION_CULLING` tag, so verification used the previously CLI-extracted prompt preserved in this status/rationale trail; `rg` confirms no `.Sort()`/`List<T>.Sort()` in `Assets/_Project/Scripts/Audio/Virtualization`. Alternative rejected: reading a neighboring current batch prompt. Estimate: 35 us.

## Omega Polish
- [x] Polish mandate parsed only after tasks 1-18 were complete. | DOD: extracted `<POLISH_MANDATE id="OMEGA_POLISH">` by CLI after core closure. Alternative rejected: pre-reading polish before primary task loop. Estimate: 40 us.
- [x] Anti-bloat scan complete. | DOD: virtualizer hot path contains no managed sort, no interpolated strings, no `math.sqrt`, and no `math.normalize`; pre-existing `SpatialAudioManager.ResolveAcousticPortalReverbMix` still uses `math.sqrt` outside the new voice-stealing path. Alternative rejected: unrelated reverb presentation rewrite under this prompt. Estimate: 55 us.

## Continuation Upgrade - 2026-05-14
- [x] Hot dependency refresh removed from virtual enqueue/FastTick path. | DOD: foveated director and quality tier refresh now occur on init/slow cadence; enqueue consumes cached director only. Alternative rejected: per-emission registry lookup. Estimate: removes one cold registry branch from each sound emission.
- [x] Low/MX350 voice-cap hysteresis added. | DOD: tier target must persist for 25 slow ticks before switching after initialization. Alternative rejected: immediate 8/16 flip. Estimate: prevents cadence flicker under thermal/profile churn.
- [x] AUP rebase coverage widened. | DOD: explicit `ApplyVirtualVoiceAupShift` now rebases write queue, sort queue, selected voices, pending fade payloads, and listener AUP. Alternative rejected: only rebasing queued voices. Estimate: prevents stale pending replay after origin shifts.
- [x] Pending-channel duplicate steal guard added. | DOD: selection injection checks pending stable keys before taking a free channel. Alternative rejected: allowing the same pending voice to occupy two channels during the 10 ms fade. Estimate: fixed 16-slot scan.
- [x] Dropped voice stats fixed. | DOD: public `DroppedVoiceCount` reports last-sort drops plus current-frame drops. Alternative rejected: hiding drops after the sort reset. Estimate: telemetry correctness, no allocation.
- [x] Focused compiler probe fixed and passed. | DOD: Mono compile against Unity 6000.4.1f1 assemblies passes for propagation + virtualizer contracts + Burst sort job. Alternative rejected: relying only on stale `dotnet build`. Estimate: caught one real Unity.Collections API mismatch.

## Continuation Upgrade - 2026-05-14 Pass 2
- [x] Full native quicksort removed from virtual ranking. | DOD: `VirtualVoiceSortJob` now compacts audible voices, then keeps only a sorted top-K selection buffer capped by `PhysicalVoiceLimit`; no `FixedList`, no recursive stack, no full audible-list rank. Alternative rejected: sorting up to 8192 audible voices when the output can only consume 8 or 16. Estimate: Low caps candidate maintenance near 65k comparisons at 8192 audible voices; High/Ultra caps near 131k simple comparisons, trading full-list partition writes/stack churn for fixed selected-output work.
- [x] Post-top-K verification pass complete. | DOD: focused Mono compile passes; Roslyn parse probe passes after rebuild; explicit file-scoped scans over virtualizer contracts/job found no managed sort, `FixedList`, `foreach`, `math.sqrt`, `math.normalize`, string formatting, `StartCoroutine`, or `PlayOneShot`; Unity MCP transport remains offline at `127.0.0.1:8088`. Alternative rejected: trusting earlier broad `rg` output after shell regex pollution. Estimate: 0 B GC retained; build graph still pending Unity/generated project repair.

## Continuation Upgrade - 2026-05-14 Pass 3
- [x] Top-K selection fused into compaction. | DOD: each audible voice is sanitized, compacted, and inserted into the selected physical buffer in the same Burst loop; the second audible-list scan was removed. Alternative rejected: compact then rescan the same `NativeList` for selected output. Estimate: removes one linear read pass over up to 8192 audible voices per sort; Low keeps max 8 candidate slots, High/Ultra max 16.
- [x] Dead `SoundEmissionSignal` physical dispatch queue removed. | DOD: deleted unused `NativeQueue<SoundEmissionSignal>`, prewarm, drain, immediate physical dispatch fallback, sentinel registration, reset, and dispose plumbing from `SpatialAudioManager`; `QueueSoundEmissionSignal` now has only the virtual voice ingress path. Alternative rejected: keeping a stale bypass that could later reintroduce pre-cull DSP submissions. Estimate: removes one persistent native queue allocation and one dead late-frame drain call.
- [x] Post-pass verification complete. | DOD: focused Mono compile passes for propagation + virtualizer contracts/job; Roslyn parse passes for `AudioVirtualizationJobs.cs` and `SpatialAudioManager.cs`; file-scoped scans find no deleted queue identifiers and no managed sort/`FixedList`/sqrt/normalize/string/coroutine/audio-one-shot patterns in the virtualizer. Alternative rejected: starting another full build while unrelated `dotnet build`/Unity compiler processes are active. Estimate: no new managed allocation.
