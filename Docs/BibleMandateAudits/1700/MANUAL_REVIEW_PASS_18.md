# Manual Review Pass 18 - Audio, Narrative, Quest, Soundscape, And Presentation Truth

Status: STATIC METHOD REVIEW - NO UNITY, DSP, PROFILER, GPU, PLAYER, OR DEVICE PROOF
Date: 2026-06-02

## Mandates Compared

- `.agents-skills/AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `.agents-skills/AUDIO_Hrtf_Binaural_Spatialization.txt`
- `.agents-skills/PROG_Quest_State_Graph_Logic.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

## Files Method-Read

- `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs`
- `Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs`
- `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs`
- `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`
- `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs`
- `Assets/_Project/Scripts/Audio/AtmosphericAudioRuntimeInstaller.cs`
- `Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs`
- `Assets/_Project/Scripts/PDA/PDARuntimeInstaller.cs`
- `Assets/_Project/Scripts/PDA/PDAMarkerHUDElement.cs`
- `Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs`
- `Assets/_Project/Scripts/Quest/QuestStateManager.cs`
- `Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs`
- `Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs`
- `Assets/_Project/Scripts/Narrative/LoreDatabaseManager.cs`
- `Assets/_Project/Scripts/World/SoundscapeSystem.cs`

## Critical Finding - Managed Audio Callback Policy Mismatch

`DynamicMusicGranularSynthesizer` and `VocalBankPlaybackRuntime` both contain player-runtime `OnAudioFilterRead(float[] data, int channels)` callbacks. This is the strongest pass 18 finding because the audio synthesis mandate explicitly forbids managed audio callbacks as the production DSP route.

`DynamicMusicGranularSynthesizer.OnAudioFilterRead(...)` does not appear to allocate a managed buffer itself; Unity supplies the `float[]`, and the method copies from a DataVault-backed `NativeArray<float>` into that buffer through a fixed pointer. That still keeps a managed Unity audio callback in the release path, acquires a mutation guard, reads volatile state, can increment underrun counters, and depends on the Unity managed callback cadence. Classification: `YELLOW_MANAGED_AUDIO_CALLBACK_TRANSFER_BRIDGE_RELEASE_BLOCKED`.

`VocalBankPlaybackRuntime.OnAudioFilterRead(...)` is stricter: it acquires callback views, reads bank length, invokes `VocalDecodeKernel.DecodeIntoAudioBuffer(...)`, updates telemetry/counters, and measures elapsed time with `Stopwatch.GetTimestamp()` from inside the callback. Even if it is allocation-free in the narrow C# sense, it violates the mandate intent more directly because decoding and state writes happen on the managed audio callback route. Classification: `P0_MANAGED_AUDIO_CALLBACK_DECODE_PATH`.

Required closure is not prose. Either exclude these callback components from release player builds, move output to DSPGraph/native audio kernel/SPSC ring handoff, or provide a written waiver plus compact-device DSP profiler proof showing no underruns, no GC, no blocking, no DataVault lock contention, and no audio-thread budget breach. The mandate-preferred fix is removal or replacement of the managed callback route.

## Audio Runtime Shape

`NativeAudioFrameRingBuffer` remains the best-shaped audio path in this pass. It allocates fixed raw bridge buffers through `H8Memory.AllocateRaw(...)`, validates descriptor layout, owns telemetry storage, and exposes a native bridge descriptor. Classification remains `GREEN_STATIC_RING_BUFFER_SHAPE_WITH_NATIVE_PLUGIN_PROOF_REQUIRED`. It is not release-closed until `HectonAudioKernel` availability, descriptor validity, underrun count, bridge failure count, and callback absence are proven in a player capture.

`PlayerCriticalProceduralAudioRenderer.RefreshNativeOutputBridge()` uses `NativeAudioKernelRingBufferDescriptor` and `HectonSensoryKernelNativeBridge.TryRegisterWithRetryGate(...)`. That is aligned with the mandate route. Open proof remains native plugin availability, output registration success, descriptor rejection counters, bridge clear boundaries, underrun telemetry, and compact/high mixer capture. Classification: `GREENISH_NATIVE_BRIDGE_ROUTE_PROOF_REQUIRED`.

`PlayerCriticalProceduralAudioRenderer.ResolveListenerReverbFilterCold()` and `EnsureReverbMixerBindings()` are cold/recovery shaped. Missing `AudioReverbFilter` or mixer exposed parameters logs only in editor/development guards, but production still needs authored listener reverb and exposed mixer parameter proof. Classification: `YELLOW_AUTHORED_MIXER_BINDING_PROOF_REQUIRED`.

`DynamicMusicGranularSynthesizer.GenerateEmergencyMockAudioProfiles()` and `GenerateDefaultGrainBankCold()` write procedural default music profiles/grain banks into vault storage. This is useful for development recovery, but production music must be authored through scene configs, banks, and mix rules rather than silent emergency profiles. Classification: `YELLOW_MOCK_AUDIO_PROFILE_RELEASE_GATE`.

`VocalBankPlaybackRuntime.OpenOrGenerateBankCold()` loads the bank from `Application.streamingAssetsPath`, but if the file is missing and `_useMockBankWhenFileMissing` is enabled it ensures mock-bank storage and generates a mock bank. That is a release content gate: voice/suit warnings cannot ship as generated mock vocal data unless explicitly labelled as diagnostic. Classification: `YELLOW_MOCK_VOCAL_BANK_RELEASE_GATE`.

`AtmosphericAudioRuntimeInstaller.EnsurePlayerSystems(...)` adds `DeepPsychosisController`, `PlayerStressVFX`, `PlayerCriticalProceduralAudioRenderer`, and `VocalWarningSystem` at runtime if the player/listener lacks them. That is acceptable as bootstrap recovery only. It is not acceptable as the normal release composition route because the player rig should be authored with these components and prewarmed buffers. Classification: `YELLOW_RUNTIME_AUDIO_COMPONENT_REPAIR_PROOF_REQUIRED`.

`HectonMusicDirector` allocates small fixed arrays in `Awake()`, aggregates work into `LateFrameTick()`, and uses `SlowTick()` for context refresh. That shape is generally aligned. The runtime director prefab path can warm and spawn through object pool at active scene config resolution; this needs scene config, runtime director prefab, and pool prewarm proof. Classification: `GREENISH_MUSIC_DIRECTOR_SHAPE_WITH_PREFAB_POOL_PROOF_REQUIRED`.

## Narrative, Quest, PDA, And Text Routes

`QuestStateManager.Initialize(...)` performs cold compilation of quest data into native arrays, dictionaries, char buffers, descriptor arrays, and NativeLists. This is boot/initialization shaped, not a per-frame quest evaluator defect. It still uses local persistent containers and managed dictionaries outside a pure DataVault-only model, so acceptance needs boot-only proof and no reinitialize during gameplay. Classification: `GREENISH_COLD_QUEST_COMPILE_WITH_BOOT_PROOF_REQUIRED`.

`QuestDagResolverService` constructs a persistent `NativeParallelMultiHashMap<int,int>` spatial hash for trigger occupancy and configures `SignalBus<StateChangedSignal>`. Method context confirms this is constructor/session storage, not a hot accessor. Closure needs ownership and disposal proof, plus evaluation stress against the 0.3 ms compact quest budget. Classification: `GREENISH_QUEST_RESOLVER_OWNER_STORAGE_WITH_STRESS_PROOF_REQUIRED`.

`MetaCampaignService.Tick(...)` consumes one `ProgressionEventSignal`, evaluates rules, and completes/broadcasts in `LateFrameTick()`. `DumpBlackBox()` uses a temporary byte payload and `NativeFaultDumpWriter.TryWriteAll(...)` only on fault/NaN paths. Classification: `GREENISH_META_CAMPAIGN_OWNER_PHASE_WITH_FAULT_DUMP_PROOF_REQUIRED`.

`AwaitableDropSequenceDirector.Tick(...)` is an explicit prologue/cinematic sequence state machine and writes a black-box dump with a temporary payload only on fault. This is a permitted cinematic/cold fault route, but release claims need interruption/control-loss rules, capture-truth labels, and the dump artifact under injected failure. Classification: `GREENISH_CINEMATIC_SEQUENCE_WITH_CAPTURE_AND_FAULT_PROOF_REQUIRED`.

`LoreDatabaseManager.LoreSeed` computes stable lore hashes and copies fallback text into power-of-two buffers during construction. Editor/development hash mismatch logs are guarded. Classification: `GREENISH_LORE_BOOT_BUFFER_ROUTE_WITH_LOCALIZATION_PROOF_REQUIRED`.

`AudioLogSystem` uses `SlowTick()` playback completion polling at a coarse cadence and guards log calls with `[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]`. It still needs audio-log save/load, subtitle/caption, discovery queue, and playback completion proof. Classification: `GREENISH_AUDIO_LOG_SLOWTICK_WITH_SUBTITLE_SAVE_PROOF_REQUIRED`.

`PDARuntimeInstaller.EnsurePlayerSystems(...)` dynamically adds PDA components to the player if missing. This is an authoring fail-safe, not proof that the production player rig is correct. Classification: `YELLOW_PDA_COMPONENT_REPAIR_PROOF_REQUIRED`.

`PDAMarkerHUDElement.DisableGraphicRaycasts(...)` uses a static scratch list and `GetComponentsInChildren(...)` to disable raycasts on marker display roots. Method context is setup/marker-root shaping, not a steady-state marker tick. It needs marker-prefab proof and no repeated marker-root rebuilds under gameplay. Classification: `YELLOW_PDA_MARKER_SETUP_SCAN_PROOF_REQUIRED`.

`SoundscapeSystem` uses fixed listener/event rings, `SlowTick()` depth-tier changes, and `LateFrameTick()` global shader updates only when the tier is dirty. This is aligned with low-cadence audio/presentation fake-first rules. It still lacks mix capture, tier transition proof, and listener overflow telemetry. Classification: `GREENISH_SOUNDSCAPE_SLOWTICK_WITH_MIX_PROOF_REQUIRED`.

## Document Correction Applied

`audio.md` was missing the specific managed audio callback boundary even though the `.agents-skills` audio mandates contain it. This pass adds a root-bible section requiring native/DSPGraph or native audio-kernel routes, blocking release acceptance for `OnAudioFilterRead` synthesis/decode paths unless they are excluded, strictly transfer-only with proof, or explicitly waived.

## Required New Release Gates

- Remove/replace/restrict `DynamicMusicGranularSynthesizer.OnAudioFilterRead(...)` and `VocalBankPlaybackRuntime.OnAudioFilterRead(...)`.
- Prove `HectonAudioKernel` native bridge registration, descriptor validity, underrun counters, and no bridge fallback under release player boot.
- Prove authored dynamic music profiles, vocal banks, mixer bindings, runtime director prefab, and player audio components exist in production scenes before gameplay begins.
- Run compact-device audio/DSP capture covering alarm, music, voice, ambience, sonar, audio logs, and UI feedback.
- Run subtitle/caption/accessibility proof for vocal warnings, audio logs, and critical spoken cues.
- Run quest/narrative/prologue proof for evidence-before-exposition, save/load state, black-box dump under fault, and capture-truth labels for public/presentation material.

Release blocker routing:

- `RB-017`: managed audio callback synthesis/decode and mock audio content.
- Existing presentation, subtitle, quest, and narrative proof rows remain open until runtime captures and save/fault artifacts exist.

## Release Interpretation

This pass does not claim an audio runtime failure was observed. No player build, DSP graph, profiler, mixer, or hardware capture was run. It does prove a static mandate mismatch: release-grade audio cannot be honestly claimed while managed audio callback synthesis/decode paths remain active or unproven.
