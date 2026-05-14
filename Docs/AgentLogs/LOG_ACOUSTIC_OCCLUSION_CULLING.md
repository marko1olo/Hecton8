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

## 2026-05-14 - Continuation Upgrade
What was wrong:
- The virtualizer still refreshed foveated/scalability dependencies too close to FastTick/enqueue.
- Explicit AUP rebase covered queued voices but not selected and pending fade payloads.
- During a 10 ms steal fade, a still-pending stable key could be treated as absent and assigned to another channel.
- Public dropped-count telemetry could hide drops after the sort pass reset the live counter.
- Focused compile had not isolated the Burst job from the broken generated project graph.

What was done:
- Moved virtualizer dependency/tier refresh to initialization and SlowTick.
- Added 25 slow ticks of hysteresis before post-init 8/16 physical voice cap changes.
- Expanded explicit `ApplyVirtualVoiceAupShift` to selected voices and pending channel payloads.
- Added pending stable-key lookup before free-channel selection.
- Changed `DroppedVoiceCount` to report last-sort drops plus current-frame drops.
- Fixed `FixedList512Bytes<int>.Add` calls to use `ref int`, matching this Unity.Collections version.

Cinematic Cheats used:
- Still uses squared-distance priority and hard audible floor, not honest acoustic ray truth.
- Low/MX350 remains 8 physical voices; High/Ultra spend saved budget on richer selected-voice presentation, not wider chaos.
- Hysteresis prevents visible/audible tier twitching during thermal/profile churn.

Exact microseconds saved:
- Measured profiler data is still unavailable.
- The upgrade removes per-emission registry fallback work and prevents duplicate pending voice assignment during fades.
- Channel guard remains fixed 16-slot work; expected cost stays under the previous 10 us fade-loop estimate.

Verification:
- Focused Mono compile against Unity 6000.4.1f1 assemblies passes for `AcousticPortalPropagation.cs`, virtualizer contracts, and `AudioVirtualizationJobs.cs`.
- Roslyn syntax parse passes for the changed C# files.
- Static scans still show no managed sort, `math.sqrt`, `math.normalize`, `foreach`, string formatting, `GlobalRegistry.Get<T>`, `VoiceManager`, or `PlayOneShot` inside `Assets/_Project/Scripts/Audio/Virtualization`.
- Full `dotnet build Hecton8.Core.csproj` remains blocked by 130 stale/generated project reference errors. Unity MCP remains unavailable.

## 2026-05-14 - Continuation Upgrade Pass 2
What was wrong:
- The Burst job was still doing full audible-list quicksort even though only the top 8/16 voices are ever consumed by physical channel injection.
- The fixed-stack sorter carried extra branch and stack fallback code for an output shape that does not need full ordering.
- One anti-bloat scan used a shell regex that polluted the result set; it was replaced with explicit file-scoped checks.

What was done:
- Replaced native quicksort with bounded top-K insertion selection in `AudioVirtualizationJobs.cs`.
- Kept compacted `NativeList<VirtualVoice>` for stats/ownership, but made `NativeArray<VirtualVoiceSelection>[16]` the only ranked output.
- Cleared all stale selection slots after active selections, protecting Low/MX350 8-voice cap transitions.
- Rechecked `SpatialAudioManager` injection: it reads only `_virtualVoiceSelections` and `_lastVirtualVoiceStatistics`, so unsorted compacted voices are not a behavioral dependency.
- Stopped stale MSBuild node-reuse child processes left by my timed-out broad build attempt; left a different active `dotnet build --disable-build-servers /nr:false` process alone as unrelated concurrent work.

Cinematic Cheats used:
- Same perceptual fake stack: squared-distance ranking, reciprocal attenuation, hard audible floor, foveated Tier 2 silence, 8-channel Low cap, 10 ms steal fade.
- New cheat: do not sort reality. Keep only the voices the player can physically hear and discard ordering information nobody consumes.

Exact microseconds saved:
- Profiler data remains unavailable because Unity MCP/console is not connected and the generated project graph blocks full verification.
- Mechanical estimate at 8192 audible candidates: Low cap maintains 8 sorted candidates; Mid/High/Ultra maintains 16. This removes full-list partitioning and sorter stack work while preserving 0 B managed allocation.
- Expected low-end gain is lower CPU variance under swarm bursts, not a guaranteed measured frame-time number.

Verification:
- Focused Mono compile passes against Unity 6000.4.1f1 assemblies for `AcousticPortalPropagation.cs`, `AudioVirtualizationContracts.cs`, and `AudioVirtualizationJobs.cs`.
- Roslyn parse probe passes after rebuilding the temp probe.
- File-scoped scans over `AudioVirtualizationJobs.cs` and `AudioVirtualizationContracts.cs` report no `.Sort(`, `List<T>.Sort`, `FixedList`, `foreach`, `math.sqrt`, `math.normalize`, `StartCoroutine`, `PlayOneShot`, `string.Format`, `$"..."`, or `.ToString(`.
- `git diff --check` reports no whitespace errors, only CRLF conversion warnings for touched files.
- Unity MCP validate/read_console failed with HTTP transport failure to `127.0.0.1:8088`; editor-side proof remains unavailable.
- Full `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly /m:1 /p:UseSharedCompilation=false` timed out after 184 s; status remains PENDING VERIFICATION.

## 2026-05-14 - Continuation Upgrade Pass 3
What was wrong:
- Top-K ranking still required a second linear scan over the compacted audible `NativeList`.
- `SpatialAudioManager` still allocated and drained an obsolete `NativeQueue<SoundEmissionSignal>` that public ingress no longer used.
- That dead queue retained an immediate physical dispatch fallback, which was architectural debt against the "before DSP" culling rule.

What was done:
- Fused candidate insertion into `VirtualVoiceSortJob.Execute()` compaction: sanitize, compact, and top-K rank in one loop.
- Deleted `MaxQueuedSoundEmissionSignals`, `_soundEmissionSignals`, sound-emission queue counters, prewarm, drain, direct dispatch fallback, sentinel registration, reset, and dispose code.
- Rechecked public `QueueSoundEmissionSignal`: it now routes only through `VirtualVoiceRequest` / `EnqueueVirtualVoice`.

Cinematic Cheats used:
- Same perceptual model: squared-distance reciprocal ranking, hard audible floor, foveated silence, Low/MX350 8-channel cap, fixed 10 ms handoff fade.
- New cleanup cheat: no legacy physical fallback. If the virtualizer rejects a voice, the system drops it instead of spending DSP budget to prove inaudible noise existed.

Exact microseconds saved:
- Profiler data remains unavailable.
- Mechanical saving: one persistent native queue allocation removed, one late-frame dead drain removed, and one linear read pass over up to 8192 audible voices removed from the Burst ranking job.
- Expected low-end result is lower variance during swarm bursts and fewer stale bypass surfaces, not a measured frame-time claim.

Verification:
- Focused Mono compile passes for `AcousticPortalPropagation.cs`, `AudioVirtualizationContracts.cs`, and `AudioVirtualizationJobs.cs`.
- Roslyn parse probe passes for `AudioVirtualizationJobs.cs` and `SpatialAudioManager.cs`.
- File-scoped scans report no stale sound-emission queue identifiers in `SpatialAudioManager.cs`.
- File-scoped scans over virtualizer contracts/job report no `.Sort(`, `List<T>.Sort`, `FixedList`, `foreach`, `math.sqrt`, `math.normalize`, `StartCoroutine`, `PlayOneShot`, `string.Format`, `$"..."`, or `.ToString(`.
- Direct `AudioSource.Play()` scan still reports legacy base/tool/player/music/central audio loops outside the `SoundEmissionSignal` virtualization domain.
- Full project build was not restarted because unrelated `dotnet build` / Unity compiler processes are active; Unity MCP remains offline.

## 2026-05-14 - Continuation Upgrade Pass 4
What was wrong:
- Stable virtual channel identity ignored `StationaryCacheKey`, wasting a known stationary acoustic identity.
- A local stable-key helper had appeared in `SpatialAudioManager`, duplicating the contracts utility and creating drift risk.
- Internal `QueueSoundEmissionSignal` validated an audio event, then called public `EnqueueVirtualVoice`, which validated the same event again.

What was done:
- `VirtualVoiceUtility.ComputeStableKey` now hashes `StationaryCacheKey`.
- Removed the local stable-key helper and routed `SpatialAudioManager` through the contracts utility.
- Split `AppendVirtualVoice` from public `EnqueueVirtualVoice`; internal signal ingress validates once, external callers remain guarded.

Cinematic Cheats used:
- Same deterministic perceptual stack: identity hash plus selected top-K, not a managed source registry; stationary cache identity is used as cheap continuity glue.

Exact microseconds saved:
- Profiler data unavailable.
- Mechanical saving: one `TryResolveAudioEventClip` table lookup removed per accepted internal `SoundEmissionSignal`.
- Expected qualitative saving: fewer unnecessary 10 ms steals for stationary emitters because channel keys now include stationary acoustic identity.

Verification:
- Focused Mono compile passes for `AcousticPortalPropagation.cs`, `AudioVirtualizationContracts.cs`, and `AudioVirtualizationJobs.cs`.
- Roslyn parse passes for `AudioVirtualizationContracts.cs`, `AudioVirtualizationJobs.cs`, and `SpatialAudioManager.cs`.
- Scan confirms only one `ComputeStableKey` implementation remains and `SpatialAudioManager` calls `VirtualVoiceUtility.ComputeStableKey`.
- Virtualizer anti-bloat scans remain clean.
