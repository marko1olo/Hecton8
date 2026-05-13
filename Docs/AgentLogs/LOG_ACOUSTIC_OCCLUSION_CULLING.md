# LOG_ACOUSTIC_OCCLUSION_CULLING

## 2026-05-14 - Virtual Voice Stealing
Agent: ACOUSTIC_OCCLUSION_CULLING
Role: DSP_ACOUSTIC_LEAD
Domain: Echelon 8 Presentation & UX / DSP Acoustic Radar
Status: PENDING VERIFICATION - global/generated project graph blocks compile verification.

What was wrong:
- `SoundEmissionSignal` could reach physical `AudioSource` assignment before relevance ranking, so swarm/predator bursts could create thousands of attempted physical voices.
- Runtime code still had Unity `AudioSource.priority` writes, which delegates stealing to Unity internals instead of deterministic project policy.
- Splash playback in `FluidFeedbackListener` bypassed central audio routing.
- No `IAudioVirtualizationService` slot existed in `GlobalRegistry`; adding a direct singleton would violate project architecture.

What was done:
- Added `Hecton8.Audio.Virtualization.Contracts` with `IAudioVirtualizationService`, `VirtualVoiceRequest`, `VirtualVoice`, `VirtualVoiceSelection`, `VirtualVoiceStatistics`, `VirtualVoiceTelemetryEntry`, and `VirtualVoiceUtility`.
- Added `Hecton8.Audio.Virtualization` with `VirtualVoiceSortJob`, a Burst `IJob` that compacts audible voices and ranks by `Priority * rcp(distanceSq + 1)`.
- Added `GlobalRegistryServiceSlot.AudioVirtualization` and registry accessors/register/unregister/resolve paths.
- Integrated virtual emission queueing into `SpatialAudioManager`: `QueueSoundEmissionSignal` now creates virtual requests before DSP, FastTick schedules native sort, LateFrame injects selected voices before audio event drain.
- Capped physical virtualized voices at 16, with Low/MX350 capped at 8.
- Added 10 ms deterministic steal fade, stable virtual channel keys, and Doppler ratio handoff into existing pitch smoothing.
- Added foveated Tier 2 acoustic mute by zeroing virtual priority before sort.
- Added 300-frame virtual voice blackbox telemetry and binary dump path `Docs/AgentLogs/Dump_ACOUSTIC_OCCLUSION_CULLING.bin` for invalid weight/origin-shift faults.
- Removed runtime `AudioSource.priority` writes from `MusicVoicePool` and `PlayerThrusterAudio`; remaining priority scan hit is editor barter metadata, not runtime audio.
- Routed `FluidFeedbackListener` splash playback through `GlobalRegistry.Audio.PlayAtPoint`.

Cinematic Cheats used:
- Priority over squared distance instead of honest acoustic simulation.
- `math.rcp(distanceSq + 1)` attenuation, no sqrt in virtualizer ranking.
- Hard perceptual floor: `Volume * Attenuation < 0.01` is culled.
- Frozen/foveated Tier 2 entities are acoustically silent by priority zero.
- Low/MX350 profile halves the physical voice budget to 8.
- Fixed 10 ms steal envelope hides hard source replacement instead of preserving two full physical sources.

Exact microseconds saved:
- Measured profiler data is unavailable because Unity compile/console verification is blocked.
- Budget estimate: each culled emission avoids one physical `AudioSource` acquisition/play path; burst saving scales with dropped voices.
- Sort path is native and allocation-free; expected ranking overhead remains bounded by one `NativeList` compaction/sort and a 16-slot injection loop.
- Fade overhead estimate: under 10 us/frame for the fixed 16-slot loop; Low tier uses 8 active physical slots.
- Registry lookup avoidance estimate: cached foveated director/audio virtualization paths avoid per-emission singleton lookups, roughly 45 us per heavy burst depending on emitter count.

Verification:
- `rg` found no `VoiceManager` or `VoiceManager.Instance` in `Assets/_Project/Scripts`.
- `rg` found no `.Sort()` or `List<T>.Sort()` in `Assets/_Project/Scripts/Audio/Virtualization`.
- `rg` found no runtime `AudioSource.priority` assignments; only `Assets/_Project/Scripts/Editor/BarterBootstrapAuthoring.cs` asset priority remains.
- Direct `AudioSource.Play()` reconnaissance remains in music/tool/player loop systems and central audio renderers; broad cross-domain rewrites were rejected.
- `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly` is blocked. Earlier run reported 124 unrelated missing namespace/type errors from stale/generated asmdefs; latest rerun timed out after 124 s. Unity MCP refresh timed out and console was unavailable.

Final diff scope:
- `Assets/_Project/Scripts/Audio/Virtualization/Contracts/Hecton8.Audio.Virtualization.Contracts.asmdef`
- `Assets/_Project/Scripts/Audio/Virtualization/Contracts/AudioVirtualizationContracts.cs`
- `Assets/_Project/Scripts/Audio/Virtualization/Hecton8.Audio.Virtualization.asmdef`
- `Assets/_Project/Scripts/Audio/Virtualization/AudioVirtualizationJobs.cs`
- `Assets/_Project/Scripts/Hecton8.Core.asmdef`
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs`
- `Assets/_Project/Scripts/SpatialAudioManager.cs`
- `Assets/_Project/Scripts/Audio/MusicVoicePool.cs`
- `Assets/_Project/Scripts/PlayerThrusterAudio.cs`
- `Assets/_Project/Scripts/Physics/FluidFeedbackListener.cs`
- `Docs/Tasks/Status_ACOUSTIC_OCCLUSION_CULLING.md`
- `Docs/AgentLogs/Rationale_ACOUSTIC_OCCLUSION_CULLING.md`
