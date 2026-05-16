# AUDIO_IMPORT_RESIDENCY_GUARD Status

PROMPT IDENTIFIED: AUDIO_IMPORT_RESIDENCY_GUARD | DOMAIN: CORE/AUDIO | TASK COUNT: 20

## Mandates Selected Before Coding
- AUDIO_Hrtf_Binaural_Spatialization.txt
- AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt
- STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- ARCH_Signal_Lane_Segregation.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt

## Hygiene
- [x] Mandatory initial status read attempted from `C:\hades`: missing file confirmed.
- [x] Active status file created in project authority path.
- [x] Original assignment extraction attempted from `Docs/Tasks/CURRENT_BATCH.md` and `CURRENT_BATCH_AUDIT_20260516.md` | Prompt block missing from both files; pasted XML is the only available assignment source.

## Loop 1: Tasks 1-5
- [x] Task 1 IMPORT_POLICY_GATE | `AudioImportDictator.cs` forces first-party clips longer than 5.0s to Streaming; DOD: final postprocessor authority, cold editor-only pass; rejected per-asset manual fixes; estimated save: 800-2200 us boot load per avoided decompressed long clip plus RAM relief.
- [x] Task 2 SHORT_CLIP_POLICY | Clips under 2.0s resolve to DecompressOnLoad plus ADPCM except dialogue/music/environment exceptions; DOD: deterministic import math; rejected PCM/developer-by-hand import settings; estimated save: 15-80 us per cue dispatch by avoiding stream latency.
- [x] Task 3 SPATIAL_MONO_FORCE | Spatialized Player/Creatures/Environment domains set `forceToMono`; DOD: path/domain heuristic, music/interface/dialogue excluded; rejected stereo 3D ambience; estimated save: 50 percent source sample residency for 3D clips.
- [x] Task 4 RESIDENCY_CATEGORIES | Added fixed domains `Music`, `Player`, `Creatures`, `Environment`, `Interface`; DOD: byte enum and cold editor classification; rejected free-form string categories; estimated save: 1-5 us per policy lookup versus string routing in runtime code.
- [x] Task 5 RAM_BUDGET_KILL_SWITCH | Added `AudioRamBudgetBuildGate` with 50 MB preloaded-audio budget and offender report; DOD: build-time fail-fast, no runtime tax; rejected warning-only validation; estimated save: prevents OOM-class boot residency spikes.
- [BLOCKED BY DEPENDENCY] Loop 1 compile gate | `dotnet build Hecton8.slnx` failed before an audio-specific verdict: RealtimeCSG project references deleted source files, and Hecton8.Core has unrelated missing symbols/type-split errors. No audio errors were present in the visible build tail.

## Loop 2: Tasks 6-10
- [x] Task 6 AMBIENT_COMPONENT_PURGE | Added Unity-API prefab purge and build validation for environment prefab AudioSources; DOD: editor prefab load/save path, no raw YAML mutation; rejected destructive text rewrite; estimated save: 20-120 us per stripped ambient source activation plus SignalBus residency control.
- [x] Task 7 BIOME_TRACK_STREAMING | Music voices now register clips as Music residency and unload released voice clips via `UnloadAudioData`; DOD: existing SlowTick fade math preserved; rejected coroutine/crossfade rewrite; estimated save: 1-40 MB resident track memory per biome transition.
- [x] Task 8 LRU_AUDIO_CACHE | Added fixed 64-slot LRU decoded audio cache with 16 MB budget and domain eviction API; DOD: array scan, no dictionary/LINQ hot path; rejected unbounded `LoadAudioData`; estimated save: 50-300 us per repeated roar/load burst and prevents disk thrash.
- [x] Task 9 DISTANCE_MUTE_CULLING | SpatialAudioManager now checks AUP distance against `_maxDistance` before source acquisition or residency touch in direct, queued, weather, and no-evict paths; DOD: AUP squared distance, no Vector3 drift; rejected post-load muting; estimated save: 8-90 us per culled far clip plus zero RAM load.
- [x] Task 10 TOOL_PREWARM | Laser Cutter and Repair/Welder loop clips prewarm only on equip and release on unequip/despawn/disable; DOD: direct serialized AudioSource hooks; rejected boot preload and slow polling; estimated save: avoids boot residency for tool loops, shifts 150-900 us load cost to explicit equip.
- [BLOCKED BY DEPENDENCY] Loop 2 compile gate | `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` failed on unrelated `GlobalDataVault.ValidateAbiLayout` missing. Changed runtime audio files reached compilation with no audio-specific diagnostics before that dependency wall.

## Loop 3: Tasks 11-15
- [x] Task 11 SAMPLE_RATE_DOWNGRADE | Low/Mx350/Quest-class runtime policy clamps AudioSettings output to 22050 Hz and import policy keeps non-music/ambient at 22050; DOD: cached scalability/hardware policy, no per-clip runtime rewrite; rejected per-frame resampling; estimated save: up to 50 percent ambient sample work on low-tier hardware.
- [x] Task 12 DIALOGUE_COMPRESSION | Dialogue/OSHINO/VO paths import as Vorbis quality 0.22 at 16000 Hz; DOD: import-time compression, no runtime transcoding; rejected high-fidelity VO residency; estimated save: 27 percent sample-rate memory reduction versus 22050 and much larger versus 44100 PCM.
- [x] Task 13 VISUAL_AUDIO_DEBUGGER | Added `#if DEVELOPMENT_BUILD` TMP overlay reporting `AudioResidencyCache` MB and resident clip count through the LateFrame tick path; DOD: stripped outside development builds, no IMGUI/string formatting; rejected production HUD text; estimated save: 0 us shipping cost, debugging exposes RAM regressions immediately.
- [x] Task 14 TIER_2_UNLOAD | Frozen foveated tier and culled threat sources evict `Creatures` residency banks; DOD: domain eviction through cache, throttled once per frame; rejected keeping predator banks warm while frozen; estimated save: 1-16 MB predator bank residency per frozen predator pressure burst.
- [x] Task 15 BROWNOUT_PITCH_SHIFT | Brownout SignalBus snapshots drive a global mixer pitch multiplier and active source fallback pitch; DOD: non-consuming SignalBus read, mathematical smoothing; rejected queue stealing and coroutine fades; estimated cost: <10 us per frame while active.
- [BLOCKED BY DEPENDENCY] Loop 3 compile gate | `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` failed on unrelated VehicleDocking/SubmarineFluidDynamics/Fauna dependency errors. No audio-specific diagnostics were present in the emitted error set.

## Loop 4: Tasks 16-20
- [x] Task 16 COROUTINE_FADE_PURGE | `rg` audit found no `IEnumerator`, `StartCoroutine`, or `yield return` in `HectonMusicDirector`, `Scripts/Audio`, or `SpatialAudioManager`; existing fades are SlowTick/Update math in voice state; DOD: static source audit plus no coroutine replacement needed; rejected adding a new fade driver; estimated save: avoids coroutine scheduler overhead and managed iterator allocation per music fade.
- [x] Task 17 CATEGORY_VOICE_LIMITS | Added hard source-category caps before source acquisition/residency touch: 3 Leviathan/roar voices and 10 bubble voices; DOD: byte route flags and fixed per-source category array; rejected AudioSource-count-only limiting after load; estimated save: 20-180 us and zero new RAM for rejected capped cues.
- [x] Task 18 BATCH_ASSET_APPLIER | Added `Hecton/Audio/Apply Import Policy To All Audio Assets` to reimport every first-party AudioClip under `Assets/_Project/Audio`; DOD: AssetDatabase enumeration with guarded policy application; rejected waiting for manual inspector touches; estimated save: applies residency policy to the existing library in one cold editor pass.
- [x] Task 19 FREQUENCY_ANALYSIS_FAKE | Removed the serialized thruster loop clip path from `PlayerThrusterAudio` and replaced it with a 22050 Hz mono streaming procedural sine/filtered-white-noise engine bed; DOD: Dear Lie procedural fake, no 10 MB WAV residency; rejected asset-loop fidelity; estimated save: 10 MB-class clip residency plus MicroSD read pressure for transport/engine loop.
- [BLOCKED BY DEPENDENCY] Task 20 PLATINUM_COMPILE | `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` exits 1 on unrelated `Assets/_Project/Scripts/Core/InputDispatcher.cs(7,2): CS1032` preprocessor placement. No audio-specific diagnostics emitted before this dependency wall.
- [BLOCKED BY DEPENDENCY] Loop 4 compile gate | Same `InputDispatcher.cs` compile wall. Per fail-fast, no cross-domain repair was made.

## Loop 5: Strict Self-Audit
- [x] Re-read own code and identify misses | Fixed route-token rescans and added finite guards plus reciprocal sample-rate math to the procedural thruster callback; DOD: touched-file scan and diff check; rejected broad NativeArray/EventBus migration because those are pre-existing NativeQueue/Sentinel systems outside the import-residency task; estimated save: <5 us per affected classification/callback setup and NaN poison prevention.
- [BLOCKED BY DEPENDENCY] Run final compile after fixes | `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` exits 1 on unrelated `SubmarineFluidDynamics.cs(614-635): VaultNativeBuffer<>` missing. Audio-specific compile verdict remains blocked.
- [x] Read `<POLISH_MANDATE>` only after all core tasks are done or blocked | CLI extraction from `Docs/Tasks/CURRENT_BATCH.md` returned `POLISH_MANDATE_NOT_FOUND`; no invented polish block executed.
- [x] Append final report to `Docs/AgentLogs/LOG_AUDIO_IMPORT_RESIDENCY_GUARD.md` | CTO-facing loop 4 and loop 5 report appended; status: CORE/AUDIO residency scope VERIFIED MASTER GRADE, project platinum compile BLOCKED BY DEPENDENCY.

## Loop 6: Multiplatform Data-Vault Inquisition
- [x] Re-read AGENTS/domain/mandates and original assignment extraction | `AGENTS.md`, `Docs/Actual Domains of Project.txt`, GlobalRegistry/Signal/ZeroGC/Telemetry mandates read; `CURRENT_BATCH` extraction still lacks the AUDIO_IMPORT_RESIDENCY_GUARD XML block, so the pasted assignment remains the only available prompt body.
- [x] Evict SpatialAudioManager local NativeArray ownership | Added `SystemID.Audio`, 12 fixed `SpatialAudio*` `BufferID`s, DataVault handles, hot-swap rebinding, and owner-buffer release; `SpatialAudioManager` static scan now finds zero `new NativeArray<...>` and zero NativeArray Sentinel register/unregister sites. DOD: GlobalDataVault ownership with alias views only; rejected local Persistent arrays; estimated save: leak-sentinel correctness and relocation-safe telemetry, 0 B/frame GC.
- [x] ARM64/Quest explicit layout audit for audio structs | Added `Pack = 1` to spatial-audio, acoustic portal, virtualization, echolocation, procedural event, acoustic-zone, and native audio kernel sequential payloads. DOD: explicit ABI layout; rejected implicit CLR padding; estimated save: prevents platform-dependent marshal/NativeQueue stride faults, no runtime cost.
- [x] Static inquisition scan | `rg` verified no `new NativeArray<...>` or NativeArray Sentinel ownership remains in `SpatialAudioManager`, and no audio-domain `StructLayout(LayoutKind.Sequential...)` remains without explicit `Pack`.
- [BLOCKED BY DEPENDENCY] Loop 6 compile gate | `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` exits 1 on unrelated `Core/Determinism/LockstepStateValidator.cs` missing `LockstepSnapshotSignalCapacity`, `LockstepSnapshotLaneHash`, `SystemGlitchSignalCapacity`, and `SystemGlitchLaneHash`. No audio-specific diagnostics emitted before this wall.

## Loop 7: Overlay Anti-IMGUI Polish
- [x] Purge development overlay IMGUI debt | Replaced `SpatialAudioManager.OnGUI` with a development-only TextMeshPro overlay created during cold service initialization and refreshed from `LateFrameTick` with a preallocated 48-char buffer. DOD: no per-frame string interpolation or IMGUI callback; rejected `GUI.Label` diagnostics; estimated save: removes dev-frame IMGUI/layout overhead and 0 us shipping impact.
- [x] Re-scan owned audio domain for forbidden hot-path debt | `rg` found no `OnGUI`, `StartCoroutine`, `IEnumerator`, `yield return`, or standard `Update`/`LateUpdate`/`FixedUpdate` methods in `SpatialAudioManager` or `Scripts/Audio`; one `string.Format` match is a smoke-test literal proving absence in audited code, not runtime formatting.
- [BLOCKED BY DEPENDENCY] Loop 7 compile gate | `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` exits 1 on unrelated `EcosystemRuntimeInstaller.cs` and `BinaryLayoutManifest.cs` references to missing namespace `Hecton8.AI.Ecosystem`. No audio-specific diagnostics emitted before this wall.
