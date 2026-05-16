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
- [ ] Task 11 SAMPLE_RATE_DOWNGRADE | Pending.
- [ ] Task 12 DIALOGUE_COMPRESSION | Pending.
- [ ] Task 13 VISUAL_AUDIO_DEBUGGER | Pending.
- [ ] Task 14 TIER_2_UNLOAD | Pending.
- [ ] Task 15 BROWNOUT_PITCH_SHIFT | Pending.
- [ ] Loop 3 compile gate | Pending.

## Loop 4: Tasks 16-20
- [ ] Task 16 COROUTINE_FADE_PURGE | Pending.
- [ ] Task 17 CATEGORY_VOICE_LIMITS | Pending.
- [ ] Task 18 BATCH_ASSET_APPLIER | Pending.
- [ ] Task 19 FREQUENCY_ANALYSIS_FAKE | Pending.
- [ ] Task 20 PLATINUM_COMPILE | Pending.
- [ ] Loop 4 compile gate | Pending.

## Loop 5: Strict Self-Audit
- [ ] Re-read own code and identify misses.
- [ ] Run final compile after fixes.
- [ ] Read `<POLISH_MANDATE>` only after all core tasks are done or blocked.
- [ ] Append final report to `Docs/AgentLogs/LOG_AUDIO_IMPORT_RESIDENCY_GUARD.md`.
