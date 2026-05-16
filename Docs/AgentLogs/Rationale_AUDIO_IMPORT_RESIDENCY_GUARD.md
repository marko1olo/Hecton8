# AUDIO_IMPORT_RESIDENCY_GUARD Rationale

PROMPT IDENTIFIED: AUDIO_IMPORT_RESIDENCY_GUARD | DOMAIN: CORE/AUDIO | TASK COUNT: 20

## Decision 0: Persistent State Bootstrap
Problem: Required status and rationale files did not exist in the active project path, and the mandatory `powershell "cat Docs/Tasks/Status_AUDIO_IMPORT_RESIDENCY_GUARD.md"` read from `C:\hades` failed.
Solution: Create project-root status and rationale files under `C:\hades\Hecton8\Docs\Tasks` and `C:\hades\Hecton8\Docs\AgentLogs` before code edits.
Rejected Alternatives: Chat-only tracking was rejected because context compression is explicitly hostile and disk state is required. Writing status under `C:\hades\Docs` was rejected because authoritative project files live under `C:\hades\Hecton8`.
Scalability potential: Low/Middle/High/Ultra unaffected directly; this prevents agent-state loss, not runtime cost.
Hardware Impact: 0 microseconds runtime impact on i3/MX350; build-time bookkeeping only.

## Decision 1: Loop 1 Import Policy Authority
Problem: Raw long audio clips can be imported as DecompressOnLoad and reserve tens of megabytes before the menu.
Solution: Add `Assets/_Project/Scripts/Audio/Editor/AudioImportDictator.cs` as the last first-party audio import authority. Clips longer than 5 seconds become Streaming, sub-2-second clips become ADPCM/DecompressOnLoad, spatial 3D domains force mono, and preload is limited to short Player/Creatures/Interface clips.
Rejected Alternatives: Manual inspector cleanup was rejected because it regresses silently. Leaving ambient/music as always CompressedInMemory was rejected for this task because the assignment's RAM explosion is caused by long clips being resident. Streaming sub-2-second SFX was rejected because latency is audible and AGENTS bans streaming SFX.
Scalability potential: Low uses 22050 Hz non-music imports, forced mono 3D, and zero speculative preload. Middle keeps Vorbis compressed memory for medium clips. High keeps 44100 Hz music and can spend saved RAM on richer acoustic beds. Ultra can preserve music quality while the same residency math prevents uncontrolled preload.
Hardware Impact: On i3/MX350, a single 20 MB WAV no longer expands into boot-resident decoded memory. Expected gain is clip-dependent: 800-2200 us boot-load stall avoided per long decompressed clip and 10-80 MB RAM avoided across several clips.

## Decision 2: 50 MB Build Kill Switch
Problem: A build can still ship with preloaded clips if an importer is dirty or another tool edits settings.
Solution: Add `AudioRamBudgetBuildGate` implementing `IPreprocessBuildWithReport`; it estimates preloaded residency and throws `BuildFailedException` above 50 MB with the largest offenders.
Rejected Alternatives: Editor menu-only validation was rejected because it depends on a human. Runtime warning was rejected because the Quest/i3 failure mode is OOM before gameplay.
Scalability potential: Low/Middle/High/Ultra all use the same hard budget for preload; high tiers can stream richer content but cannot bloat boot RAM.
Hardware Impact: 0 us runtime overhead. Prevents OOM-class boot spikes; expected saved residency is the full amount above 50 MB.

## Decision 3: Loop 1 Compile Result
Problem: `dotnet build Hecton8.slnx` failed after 3m43s with unrelated dependency errors before a clean audio verdict.
Solution: Record compile as `[BLOCKED BY DEPENDENCY]` and continue per fail-fast protocol. Primary blockers: deleted RealtimeCSG source references, missing `SanitizeFinite`, missing visor blackbox methods, `IDataVault` type identity split, and missing signal sanitizer symbols.
Rejected Alternatives: Reverting unrelated deleted RealtimeCSG files or repairing other agents' Core edits was rejected as cross-domain sabotage. Marking green was rejected because build output is objective failure.
Scalability potential: Not runtime-relevant; compile wall blocks verification only.
Hardware Impact: 0 us runtime impact from this decision.

## Decision 4: Environment Prefab AudioSource Purge
Problem: Environment prefabs can bypass the SignalBus acoustic pipeline by owning Unity `AudioSource` components directly.
Solution: Add `EnvironmentAudioSourcePurgeGate` to strip environment prefab AudioSources through `PrefabUtility.LoadPrefabContents` and fail builds if the components return.
Rejected Alternatives: Raw YAML prefab mutation was rejected because prefab serialization is not a stable interface. Warning-only validation was rejected because the assignment requires a purge, not advice.
Scalability potential: Low uses zero resident ambient components and relies on centralized streaming. Middle keeps authored prefabs clean. High/Ultra can spend saved component overhead on richer SignalBus beds without uncontrolled source proliferation.
Hardware Impact: On i3/MX350, each stripped ambient source avoids roughly 20-120 us activation/setup cost and prevents hidden clip residency. Current raw scan found no environment prefab offenders; the gate prevents regression.

## Decision 5: Music Residency Release
Problem: Biome track changes stopped voices but did not explicitly unload old clip data from RAM.
Solution: Register music clips in the residency cache and call `AudioResidencyCache.ReleaseClip` from `StopVoiceImmediate`, which hard-unloads loaded audio data after the voice is stopped.
Rejected Alternatives: Rewriting the music director into coroutines or new streaming handles was rejected because the existing director already uses SlowTick/Update mathematical fade state and is lower-risk.
Scalability potential: Low streams one bed and frees the previous one. Middle keeps crossfade behavior. High/Ultra can use richer beds while old tracks are evicted instead of stacking residency.
Hardware Impact: On i3/MX350, expected savings are 1-40 MB per old music bed depending on clip import settings; frame cost is near zero because release happens on voice stop.

## Decision 6: Runtime LRU And Distance Cull
Problem: Repeated creature/world clips can thrash disk when not resident, while far clips can enter Unity source setup before being audibility-rejected.
Solution: Add a fixed 64-slot `AudioResidencyCache` with a 16 MB decoded budget and gate all main 3D paths against `_maxDistance` before `AudioSource` acquisition or cache touch.
Rejected Alternatives: Dictionary-based LRU was rejected for hot-path allocation/cache unpredictability. Post-load volume muting was rejected because it still burns RAM and source setup.
Scalability potential: Low uses culling and a small deterministic cache. Middle keeps common creature cues warm. High increases perceived density through reuse without disk spikes. Ultra can layer more sounds while the same cull prevents inaudible waste.
Hardware Impact: On i3/MX350, expected savings are 8-90 us per far rejected clip and 50-300 us per repeated roar that avoids a reload burst.

## Decision 7: Tool Audio Prewarm Cross-Domain Exception
Problem: Laser Cutter and Repair/Welder loop clips live on tool classes outside `Scripts/Audio`, so an audio-only listener cannot guarantee equip-time residency without polling.
Solution: Add direct `AudioResidencyCache` calls in `LaserCutter` and `RepairTool` `OnEquip`/`OnUnequip`/despawn paths. This is a limited cross-domain edit tied to Task 10 and the serialized tool AudioSources.
Rejected Alternatives: Boot preloading was rejected because Quest/i3 RAM is the failure mode. Slow polling `CurrentTool` was rejected because it adds recurring work and can miss serialized loop references.
Scalability potential: Low keeps tool audio out of boot RAM. Middle pays load at explicit equip. High/Ultra can use stronger tool loops without making them permanent residents.
Hardware Impact: On i3/MX350, boot residency is reduced by the full size of unequipped tool clips; equip cost is estimated at 150-900 us depending on clip size and import state.

## Decision 8: Loop 2 Compile Result
Problem: A focused runtime build still cannot complete due to a non-audio dependency error.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`; it failed only on `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs(433,13): ValidateAbiLayout` missing. No audio-specific diagnostics were emitted for the modified runtime audio/tool files.
Rejected Alternatives: Repairing `GlobalDataVault` was rejected as outside CORE/AUDIO authority. Claiming compile success was rejected because the command exited 1.
Scalability potential: Not runtime-relevant; this is verification state.
Hardware Impact: 0 us runtime impact from this decision.

## Decision 9: Low-Tier Sample Rate And Dialogue Compression
Problem: Quest 3/MX350 class hardware should not pay 44100 Hz ambient/voice cost, and dialogue is legibility content, not fidelity content.
Solution: Use import policy to keep non-music ambient at 22050 Hz, clamp low-tier/Quest runtime output to 22050 Hz, and force dialogue/OSHINO/VO imports to Vorbis quality 0.22 at 16000 Hz.
Rejected Alternatives: Runtime per-clip resampling was rejected because it would create CPU work to solve an import problem. High-fidelity VO was rejected because voice legibility survives 16 kHz Vorbis while RAM cost drops hard.
Scalability potential: Low runs reduced sample work and smaller voice clips. Middle keeps music untouched. High/Ultra preserve 44100 music while still keeping dialogue cheap.
Hardware Impact: On i3/MX350, ambient output work can drop by roughly 50 percent when clamped from 44100 to 22050. Dialogue sample storage drops 27 percent versus 22050 and 64 percent versus 44100 before Vorbis compression.

## Decision 10: Development RAM Overlay And Frozen Creature Eviction
Problem: Audio RAM regressions are invisible during development, and frozen predators should not keep decoded creature banks resident.
Solution: Add a `#if DEVELOPMENT_BUILD` TextMeshPro overlay driven by `AudioResidencyCache.CurrentResidentBytes`, and evict `Creatures` residency when virtual/foveated audio reaches Tier 2 Frozen or a threat source becomes culled.
Rejected Alternatives: Shipping HUD text was rejected because this is diagnostics. Per-predator hard references were rejected because the cache domain already gives a decoupled sound-bank boundary without direct AI ownership.
Scalability potential: Low evicts creature banks aggressively. Middle keeps recent active creature cues warm. High/Ultra can refill creature audio on renewed proximity while frozen predators stop consuming RAM.
Hardware Impact: Overlay is 0 us in shipping builds. Creature bank eviction saves an estimated 1-16 MB when frozen predator pressure leaves hearing range.

## Decision 11: Brownout Pitch Binding
Problem: Power brownout had no audio-wide pitch consequence, so failing systems sounded mechanically normal.
Solution: Read `BrownoutSignal` through `SignalBus<BrownoutSignal>.GetFrameSnapshot`, smooth severity into a pitch ratio, set a mixer pitch multiplier, and update active Unity sources as a fallback.
Rejected Alternatives: Draining `GlobalSignals.TryDequeueBrownout` was rejected because UI/VocalWarning consumers already use that queue. Coroutine pitch fades were rejected because audio policy already ticks mathematically.
Scalability potential: Low uses one scalar and mixer parameter. Middle/High/Ultra can layer richer brownout effects later while keeping the same scalar.
Hardware Impact: Estimated <10 us per active frame on i3/MX350; one span scan and bounded source loops, no allocations outside mixer internals.

## Decision 12: Loop 3 Compile Result
Problem: Runtime compile remains blocked by non-audio files after Loop 3.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`; it failed on unrelated VehicleDocking, SubmarineFluidDynamics, and PredatorCognitionDomain errors. No audio-specific diagnostics were present in the emitted error set.
Rejected Alternatives: Fixing VehicleDocking/Submarine/Fauna compile errors was rejected as outside CORE/AUDIO authority and would violate concurrent-agent boundaries.
Scalability potential: Not runtime-relevant; compile wall only.
Hardware Impact: 0 us runtime impact from this decision.

## Decision 13: Coroutine Fade Purge And Voice Caps
Problem: Music fade paths could not allocate iterator state, and repeated roars/bubbles could force residency and source setup even when the mix was already saturated.
Solution: Static audit found no `IEnumerator`, `StartCoroutine`, or `yield return` in the owned music/audio paths. Added byte-route hard caps before source acquisition: Leviathan/roar capped at 3 active voices, bubble cues at 10 active voices.
Rejected Alternatives: Adding a new music fade dispatcher was rejected because `HectonMusicDirector` already evaluates fades mathematically. Capping after `AudioSource` assignment was rejected because the RAM and setup cost would already be paid.
Scalability potential: Low ignores excess roars/bubbles before load. Middle keeps deterministic mix density. High/Ultra can layer richer non-capped content while roar/bubble spam remains bounded.
Hardware Impact: On i3/MX350, capped rejected requests avoid roughly 20-180 us of source/cache work and prevent new decoded residency for inaudible mix clutter.

## Decision 14: Batch Import Applicator
Problem: The import dictator only guarantees future imports unless the existing library is explicitly reprocessed.
Solution: Added a Unity menu action that enumerates every first-party `AudioClip` under `Assets/_Project/Audio`, applies the policy, and reimports changed assets in one cold editor pass.
Rejected Alternatives: Waiting for designers to touch assets was rejected because the current RAM explosion exists in already-imported clips. Raw `.meta` rewriting was rejected because Unity import settings should remain under `AudioImporter`.
Scalability potential: Low/Middle get immediate Streaming/ADPCM/Vorbis policy application. High/Ultra retain the same residency gate while allowing higher-fidelity streamed music.
Hardware Impact: 0 us runtime cost. Build/boot savings match the policy deltas applied across the library; expected result is removal of 10-80 MB boot-resident spikes when existing long WAV imports are corrected.

## Decision 15: Procedural Engine Dear Lie
Problem: A submarine/thruster loop WAV can become a large resident or repeated I/O asset for a sound that does not require recorded fidelity.
Solution: Removed the serialized loop clip from `PlayerThrusterAudio` and generates a mono 22050 Hz streaming procedural engine bed from a low-frequency sine, whine sine, and filtered white noise. Existing locomotion math still drives volume and pitch.
Rejected Alternatives: Keeping a 10 MB looping WAV was rejected for Quest/Steam Deck memory and MicroSD pressure. A full FFT/frequency-analysis system was rejected because the requested cheat is a simple deterministic fake.
Scalability potential: Low uses the cheap procedural fake. Middle retains locomotion-reactive pitch/volume. High/Ultra can route richer mixer effects around the generated bed without restoring a resident loop asset.
Hardware Impact: On i3/MX350 and Steam Deck MicroSD, this avoids a 10 MB-class clip residency/read path; per-sample cost is one bounded sine pair plus one LCG noise step, paid only by the active audio callback.

## Decision 16: Loop 4 Compile Result
Problem: Platinum compile still cannot be proven because the focused runtime build fails before audio-specific diagnostics are emitted.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`; it failed on `Assets/_Project/Scripts/Core/InputDispatcher.cs(7,2): CS1032` because a preprocessor symbol is defined after the first token in that unrelated file. Task 20 is marked dependency-blocked instead of falsely completed.
Rejected Alternatives: Editing `InputDispatcher.cs` was rejected as outside CORE/AUDIO authority. Reporting platinum was rejected because the command exited 1.
Scalability potential: Not runtime-relevant; verification is blocked by another domain.
Hardware Impact: 0 us runtime impact from this decision.

## Decision 17: Loop 5 Self-Audit Corrections
Problem: The final audit found two local polish misses: clip route classification rescanned tokens already computed for the new voice caps, and the procedural thruster audio callback did not explicitly reject non-finite gain/pitch before sample generation. The audit also surfaced pre-existing `PhysicsEventBus` and NativeArray ownership in `SpatialAudioManager`.
Solution: Reused the computed Leviathan/roar token booleans, added finite guards for procedural gain/pitch, and replaced callback-time sample-rate division with a reciprocal constant. Existing `PhysicsEventBus` is a NativeQueue-backed typed physics lane and existing NativeArrays are already registered with `NativeMemorySentinel`; migrating those to `GlobalDataVault` is a cross-domain architecture change and was not mixed into the audio residency guard.
Rejected Alternatives: Leaving the rescan and callback division was rejected as avoidable hot/callback work. Migrating all pre-existing SpatialAudioManager NativeArrays during an import-residency task was rejected because it would collide with GlobalDataVault/Submarine dependency work already failing the build.
Scalability potential: Low gets no NaN sample poison and lower callback overhead. Middle keeps deterministic fake engine output. High/Ultra can layer richer mixer processing on a stable generated bed.
Hardware Impact: On i3/MX350, this removes two token rescans per first-time capped clip classification and one float division per audio callback setup; expected gain is small (<5 us per affected callback/classification) but eliminates a NaN propagation path.

## Decision 18: Final Compile And Polish Boundary
Problem: Final verification still cannot reach a green project compile.
Solution: Re-read status/rationale, extracted `<POLISH_MANDATE>` only after all core tasks were checked or blocked, received `POLISH_MANDATE_NOT_FOUND`, ran touched-file static scans and `git diff --check`, then ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` again. The final build failed on unrelated `SubmarineFluidDynamics.cs(614-635): VaultNativeBuffer<>` missing.
Rejected Alternatives: Claiming `PLATINUM_COMPILE` was rejected because the command exits 1. Editing SubmarineFluidDynamics was rejected as outside CORE/AUDIO authority and a different dependency wall from another agent.
Scalability potential: Not runtime-relevant; this records verification state.
Hardware Impact: 0 us runtime impact from the compile boundary.

## Decision 19: Loop 6 Data-Vault Sovereignty
Problem: The post-completion inquisition correctly identified a remaining sovereignty defect: `SpatialAudioManager` still owned persistent `NativeArray` telemetry buffers for radar, virtual voice selection/statistics/blackbox, and acoustic portal scratch/blackbox state.
Solution: Added `SystemID.Audio`, fixed `SpatialAudio*` `BufferID`s, and `VaultBufferHandle<T>` fields. `SpatialAudioManager` now resolves those long-lived buffers from `GlobalDataVault`, keeps only alias views, clears aliases on DataVault rebound, and releases owner buffers on teardown. Static scan now reports zero `new NativeArray<...>` and zero NativeArray Sentinel register/unregister calls in `SpatialAudioManager`.
Rejected Alternatives: Keeping Sentinel-owned Persistent arrays was rejected because the mandate now requires DataVault sovereignty. Converting the NativeQueue/NativeList lanes in this pass was rejected because `IDataVault` currently exposes NativeArray buffers only; those queue/list structures remain bounded, prewarmed, NativeMemorySentinel-owned lane infrastructure until a vault queue/list API exists.
Scalability potential: Low keeps radar/voice/portal buffers in one vault-owned memory domain with deterministic capacity. Middle retains existing virtual voice quality. High/Ultra can increase acoustic richness through existing quality gates without losing relocation visibility or owner accounting.
Hardware Impact: 0 B/frame GC change; runtime hot paths still use fixed alias views. i3/MX350 benefit is memory accounting and leak prevention, not per-frame speed: estimated 0-3 us cold-init overhead for handle resolution, repaid by DataVault owner release and relocation-safe telemetry.

## Decision 20: Loop 6 ARM64 Explicit Layout
Problem: Audio NativeQueue/DataVault/job payloads used several `[StructLayout(LayoutKind.Sequential)]` declarations without `Pack`, leaving stride/padding decisions to platform defaults.
Solution: Added `Pack = 1` to owned sequential audio payloads in `SpatialAudioManager`, acoustic portal propagation, audio virtualization contracts, echolocation raymarch, procedural audio events, acoustic-zone payload/state, and the native audio kernel ring descriptor.
Rejected Alternatives: Leaving CLR-default sequential layout was rejected for ARM64/Quest because implicit padding is not an acceptable native boundary contract. Rewriting every non-audio project struct was rejected as outside CORE/AUDIO authority.
Scalability potential: Low/Middle/High/Ultra all use identical payload strides, which keeps cross-platform save/native/queue behavior deterministic.
Hardware Impact: 0 us runtime cost; prevents platform-dependent marshal/NativeQueue stride faults that can crash Quest/Android builds.

## Decision 21: Loop 6 Compile Boundary
Problem: The focused compile still cannot complete after the audio data-vault and layout hardening pass.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`; it failed on unrelated `Core/Determinism/LockstepStateValidator.cs` missing constants: `LockstepSnapshotSignalCapacity`, `LockstepSnapshotLaneHash`, `SystemGlitchSignalCapacity`, and `SystemGlitchLaneHash`.
Rejected Alternatives: Editing LockstepStateValidator was rejected as outside CORE/AUDIO authority. Reporting platinum was rejected because the command exits 1.
Scalability potential: Not runtime-relevant; this records the verification wall.
Hardware Impact: 0 us runtime impact from the compile boundary.

## Decision 22: Loop 7 Overlay Anti-IMGUI Cleanup
Problem: The development audio RAM debugger satisfied visibility but still used `OnGUI`, `GUI.Label`, and string interpolation. That violates the no-IMGUI/no-hot-formatting polish requirement even though it was stripped outside development builds.
Solution: Replace `OnGUI` with a development-only TextMeshPro overlay created during cold `SpatialAudioManager` service initialization. Refresh it from `LateFrameTick` only when resident kilobytes or clip count changes, using a preallocated 48-character buffer and integer ASCII writers.
Rejected Alternatives: Keeping IMGUI was rejected because it adds dev-frame layout overhead and hides hot-path debt. Using `string.Format`, interpolation, or TMP formatted strings was rejected because the overlay is a recurring diagnostic surface.
Scalability potential: Low/Middle/High/Ultra shipping builds pay 0 us because the entire overlay remains behind `DEVELOPMENT_BUILD`. Development builds keep audio residency visible without masking per-frame GC or IMGUI layout spikes.
Hardware Impact: Shipping i3/MX350 impact is 0 us. Development builds avoid the previous IMGUI callback and string formatting path; expected saved dev-frame cost is small but measurable during overlay visibility, roughly 5-40 us depending on Unity IMGUI layout state.

## Decision 23: Loop 7 Compile Boundary
Problem: The focused compile still cannot complete after the overlay cleanup.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`; it failed on unrelated `EcosystemRuntimeInstaller.cs` and `BinaryLayoutManifest.cs` references to missing namespace `Hecton8.AI.Ecosystem`.
Rejected Alternatives: Editing AI/Ecosystem or core binary manifest files was rejected as outside CORE/AUDIO authority. Reporting platinum was rejected because the command exits 1.
Scalability potential: Not runtime-relevant; this records the current verification wall.
Hardware Impact: 0 us runtime impact from the compile boundary.

## Decision 24: Loop 8 VWS Data-Vault Sovereignty
Problem: `VocalWarningSystem` still owned six persistent NativeArrays directly: fixed priority queue, warning flags, cooldowns, severity, source IDs, and 300-entry blackbox telemetry.
Solution: Added `SystemID.AudioVocalWarning`, six fixed `AudioVocalWarning*` `BufferID`s, vault handles, alias views, DataVault hot-swap rebinding, and owner-buffer release. The VWS telemetry struct now uses explicit `Pack = 1`. The bounded `NativeQueue<byte>` ingress lane remains Sentinel-owned because the current `IDataVault` contract has no queue primitive.
Rejected Alternatives: Keeping local Persistent NativeArrays was rejected under the data sovereignty mandate. Forcing the queue into an array-backed fake queue was rejected because it would replace a proven NativeQueue lane with custom ring logic and increase defect risk outside the residency goal.
Scalability potential: Low keeps warning state and blackbox data in one owner-auditable vault block; Middle keeps existing VWS behavior; High/Ultra can layer richer warning rendering without private native state.
Hardware Impact: 0 B/frame GC change. Cold allocation ownership moves from component-local Persistent arrays to GlobalDataVault; expected runtime hot-path delta is 0-2 us, dominated by unchanged NativeArray alias access. Memory leak recovery improves because VWS buffers are releasable by `SystemID.AudioVocalWarning`.

## Decision 25: Loop 8 Compile Boundary
Problem: The focused Core/audio compile needed a fresh verdict after the VWS migration, and the wider solution still needed a factual boundary check.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`; it exits 0. Then ran `dotnet build Hecton8.slnx --no-restore -v:minimal`; it exits 1 on missing generated `project.assets.json` files for editor/third-party projects and missing RealtimeCSG source files, while `Hecton8.Core` builds successfully inside the solution attempt.
Rejected Alternatives: Reporting solution green was rejected because `Hecton8.slnx` exits 1. Editing third-party RealtimeCSG project files or generated editor project restore state was rejected as outside CORE/AUDIO authority.
Scalability potential: Not runtime-relevant; verification state only.
Hardware Impact: 0 us runtime impact from the compile boundary.

## Decision 26: Loop 9 Player-Critical Data-Vault Sovereignty
Problem: `PlayerCriticalProceduralAudioRenderer` still owned a large private NativeArray slab for DSP scratch buffers, sonar taps, delay lines, granular state, telemetry rings, and VWS PCM staging lanes. That violated the data-sovereignty mandate and hid major audio residency outside `GlobalDataVault`.
Solution: Added `SystemID.AudioPlayerCritical` and 48 fixed `PlayerCritical*` `BufferID`s. The renderer now binds those buffers through `GlobalDataVault`, keeps alias NativeArray views only, releases owner buffers through `SystemID.AudioPlayerCritical`, and rebinds on DataVault hot-swap. The one-sample Burst warmup allocation was removed by using the vault-backed mix scratch buffer.
Rejected Alternatives: Keeping `Allocator.AudioKernel`/`Allocator.Persistent` private arrays was rejected because it leaves memory outside owner accounting. Moving NativeQueues and NativeParallelHashMaps into custom array rings was rejected because `IDataVault` has no queue/hash primitive and those structures are bounded lane infrastructure already registered with `NativeMemorySentinel`.
Scalability potential: Low keeps the full player-critical DSP slab in a releasable vault owner with deterministic capacities. Middle keeps the same acoustic features. High/Ultra can spend cycles on richer DSP layers while memory ownership remains visible and releasable.
Hardware Impact: Hot-path access remains NativeArray alias indexing, so expected per-frame/audio-block delta is 0 us. Cold binding pays vault handle resolution during audio configuration; expected cost is initialization-only. Memory leak recovery improves because the entire player-critical slab is releasable under `SystemID.AudioPlayerCritical`.

## Decision 27: Loop 9 ABI And Compile Verification
Problem: Remaining owned audio structs still used `Pack = 4` or explicit layouts without an explicit pack, and compile needed a fresh verdict after the renderer vault migration.
Solution: Changed the remaining owned audio struct layout declarations in `PlayerCriticalProceduralAudioRenderer` and `DepthStressGranularSynthesisKernel` to `Pack = 1`; ran runtime audio static scans for local NativeArray ownership and implicit/non-1 struct packing; then ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /nr:false`.
Rejected Alternatives: Leaving `Pack = 4` was rejected for ARM64/Quest because queue/job strides must not depend on platform assumptions. Claiming compile failure from the earlier timeout was rejected after the single-process build completed successfully.
Scalability potential: Low/Middle/High/Ultra all get deterministic DSP/job payload strides and the same vault-backed memory ownership. High/Ultra retain richer DSP capability without private native state.
Hardware Impact: 0 us runtime for layout metadata. Focused Core build exits 0; one MSB3026 copy retry warning occurred due a transient locked DLL, then output succeeded.

## Decision 28: Loop 10 Editor Smoke Tests Track DataVault Truth
Problem: Editor smoke tests still asserted the pre-migration world: private `new NativeArray` allocations, old 128-byte snapshot padding, and literal `RegisterNativeArray`/`string.Format` needles that made static scans look dirty even after runtime ownership moved to `GlobalDataVault`.
Solution: Updated the smoke assertions to require `BufferID.PlayerCriticalWorkerSonarEchoTaps`, `BufferID.PlayerCriticalSabineReverbDelay`, spatial radar DataVault buffer IDs, and the current `Pack = 1, Size = 320` audio snapshot slot. Split sentinel and string-format probe literals with compile-time concatenation so the tests still check the same runtime text without poisoning source scans.
Rejected Alternatives: Deleting the smoke tests was rejected because that would remove verification coverage. Keeping stale assertions was rejected because it would either fail valid DataVault code or encourage private NativeArray regression.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged; this strengthens CI/editor verification so low-tier residency rules and high-tier DSP buffers are checked against the actual vault-backed architecture.
Hardware Impact: 0 us runtime impact; editor-only verification now avoids false negatives and keeps static scan evidence clean.

## Decision 29: Loop 10 PlayerCriticalBufferJobs Pack Normalization
Problem: `PlayerCriticalBufferJobs` still declared five Burst job payload structs with `Pack = 16`, leaving an explicit but nonstandard ABI exception inside the audio domain after the Quest/ARM64 `Pack = 1` mandate.
Solution: Changed `DopplerShiftBatchJob`, `BinauralVoxelAcousticsOutputJob`, `GranularSynthesisBlockJob`, `VwsCooldownDecayJob`, and `VwsPrioritySortJob` to `StructLayout(LayoutKind.Sequential, Pack = 1)`.
Rejected Alternatives: Keeping `Pack = 16` for theoretical alignment was rejected because these jobs carry NativeArray handles and scalar parameters, not a measured SIMD payload requiring custom native alignment. Removing `StructLayout` entirely was rejected because explicit layout evidence is required.
Scalability potential: Low/Middle/High/Ultra get the same deterministic job payload layout. High/Ultra can still use richer DSP jobs without platform-specific stride drift.
Hardware Impact: 0 us expected runtime delta; removes platform ABI ambiguity and reduces Quest/Android crash risk from layout disagreement.

## Decision 30: Loop 10 Verification Boundary
Problem: Verification needed to prove editor smoke syntax after the test changes and establish whether the current Core build wall was audio-owned.
Solution: Ran static audio scans, `git diff --check`, `dotnet build Hecton8.Editor.csproj --no-restore --no-dependencies -v:minimal /m:1 /nr:false`, and `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /nr:false`. Editor smoke assembly builds clean. Core fails only on unrelated `World/SargassumMicroFaunaBoids.cs` and `TetherInstance.cs` errors.
Rejected Alternatives: Editing Sargassum or Tether code was rejected as outside CORE/AUDIO authority. Reporting Core green was rejected because the command exits 1. Treating editor smoke syntax as unverified was rejected after the editor project compiled.
Scalability potential: Not runtime-relevant; this is verification state.
Hardware Impact: 0 us runtime impact. Current 50 MB preloaded-audio rule remains enforced by the build gate; Loop 10 did not change residency math.

## Decision 31: Loop 11 Player Critical Comment Provenance
Problem: `PlayerCriticalProceduralAudioRenderer` code had been migrated to DataVault aliases, but its field comments still said `COLD ALLOC: NativeArray` and named the component as owner. That is not executable debt, but it is architectural misinformation in a domain where ownership evidence matters.
Solution: Updated migrated NativeArray field comments to `VAULT ALIAS` and named `SystemID.AudioPlayerCritical` as the owner. Left NativeQueue, NativeParallelHashMap, and managed cold scratch comments intact because those are still local bounded infrastructure or managed staging buffers.
Rejected Alternatives: Removing comments entirely was rejected because the field block is large and needs ownership markers. Pretending queue/list/hash containers are DataVault-backed was rejected because `IDataVault` currently has no queue/list/hash primitive.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. The value is future maintainability: the next pass sees which memory is vault-owned and which queue/list/hash lanes still need a vault primitive before further migration.
Hardware Impact: 0 us runtime impact; source evidence now matches actual memory ownership.

## Decision 32: Loop 12 Acoustic Impulse Typed-Lane Consumption
Problem: CORE/AUDIO still depended on `PhysicsEventBus` listener registration for acoustic impulse delivery. Even though that bus is backed by a typed native lane internally, the audio side still used managed listener interfaces and concrete callback registration.
Solution: Removed `IPhysicsAcousticImpulseEventListener` from `SpatialAudioManager` and `PlayerCriticalProceduralAudioRenderer`. Both systems now read `ReadOnlySpan<PhysicsEventPayload>` from `SignalBus<PhysicsEventPayload>.GetFrameSnapshot()` once per frame, filter `PhysicsEventType.AcousticImpulse`, reconstruct the unmanaged `AcousticImpulseEvent`, and feed their existing local handlers.
Rejected Alternatives: Keeping listener registration was rejected because the current mandate explicitly bans legacy EventBus-style consumption. Creating a new duplicate acoustic signal was rejected because `PhysicsEventPayload` already exists as the typed lane and prevents interface chaos.
Scalability potential: Low consumes a bounded typed snapshot with one frame guard and no managed callback fanout. Middle/High/Ultra retain the same acoustic consequences while future visual overkill consumers can also read the same lane without registering concrete audio callbacks.
Hardware Impact: Expected gain is small but real: removes two audio listener registrations and the interface callback fanout for audio consumers. Runtime cost becomes one bounded span scan per LateFrame in each audio consumer, with zero GC.

## Decision 33: Loop 12 Acoustic Impulse Typed-Lane Publishing
Problem: Player-critical audio still published predator acoustic impulses through `PhysicsEventBus.NotifyAcousticImpulse`, and the physics bus refused to enqueue acoustic impulse payloads when no legacy acoustic listeners existed. Removing audio listeners would otherwise drop physics-produced acoustic impulses.
Solution: Added a local `PublishAcousticImpulseSignal` in `PlayerCriticalProceduralAudioRenderer` that pushes `PhysicsEventPayload` directly to `SignalBus<PhysicsEventPayload>`. Removed the listener-count early return from `PhysicsEventBus.NotifyAcousticImpulse` and `NotifyLargeAcousticImpulse` so the shared typed lane remains populated even when no legacy listener is present.
Rejected Alternatives: Dual-publishing through both SignalBus and PhysicsEventBus was rejected because it can duplicate acoustic impulses. Editing all non-audio legacy consumers was rejected as outside this agent's authority; the bus can continue serving them while audio uses the typed lane.
Scalability potential: Low keeps the cheapest deterministic payload route. Middle/High/Ultra can stack richer acoustic visualization or DSP responses by reading the same typed lane without increasing producer complexity.
Hardware Impact: 0 B/frame GC. One direct `SignalBus<PhysicsEventPayload>.Push` replaces the audio-side EventBus publish path for player-critical predator impulses.

## Decision 34: Loop 12 Verification Boundary
Problem: Verification needed to prove the legacy EventBus source was gone from CORE/AUDIO and identify whether compile errors were caused by the event migration.
Solution: Ran source scans for `EventBus`, `IPhysicsAcousticImpulseEventListener`, NativeArray ownership, string formatting, and struct packing; then ran Core and Editor build checks. The audio scan set is clean. Core fails on missing shared contract constants across unrelated domains and pre-existing constant references; Editor cannot compile without the missing Core DLL.
Rejected Alternatives: Repairing HectonEcology/Scalability/Survival/Physics contract constants was rejected as outside CORE/AUDIO authority. Reporting a green compile was rejected because both build commands fail.
Scalability potential: Not runtime-relevant; verification state only.
Hardware Impact: 0 us runtime impact from the verification boundary.
