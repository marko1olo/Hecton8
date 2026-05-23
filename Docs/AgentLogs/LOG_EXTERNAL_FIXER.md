# LOG_EXTERNAL_FIXER

## 2026-05-23 Autonomous External Fix Pass

What was wrong:

- In progress. Initial state is a dirty multi-agent workspace; no runtime/compiler claim yet.

What was done:

- In progress. Rule intake and candidate source scans started.

Cinematic Cheats used:

- None yet.

Exact microseconds saved:

- 0 us measured so far. No runtime code changed yet.

## 2026-05-23 Hot-Path Registry Tranche

What was wrong:

- `ScavengePopulator.ProcessSpawnQueue` and `DespawnChunk` pulled `ObjectPoolManager` / `WorldStateManager` from `GlobalRegistry` in runtime spawn/despawn paths.
- `VoxelDeltaProcessor.EmitCaveInDustDecal` pulled `AbyssalFluidDecals` from `GlobalRegistry` during carve commit side effects.
- `Atlas6DirectiveSystem` and `AtlasSignalDecoder` pulled `AtlasSignal` / `FirstHour` from `GlobalRegistry` in slow-tick, pulse, and narrative decision paths.
- Workspace is dirty from other agents. `VoxelDeltaProcessor.cs` already contained unrelated `GetGenerationHandle -> EnsureGenerationHandle` edits before this report; not owned by EXTERNAL_FIXER.

What was done:

- Added cached service fields plus `IGlobalRegistryHotSwapListener` handling to `ScavengePopulator`, `VoxelDeltaProcessor`, `Atlas6DirectiveSystem`, and `AtlasSignalDecoder`.
- Moved runtime decisions to cached references while keeping cold lifecycle/service-registration reads intact.
- Preserved public APIs, save DTOs, ownership routes, and existing SignalBus/EventBus behavior.

Cinematic Cheats used:

- None. This tranche is service-route hardening, not visual simulation replacement.

Exact microseconds saved:

- No profiler claim. STATIC_SOURCE estimate only: removed 2 registry reads per active `ScavengePopulator` spawn-queue slow tick, 1 registry read per chunk despawn, 1 registry read per voxel cave-in dust emission, 1 registry read per `Atlas6DirectiveSystem` slow tick, and 1 registry read per `AtlasSignalDecoder` slow tick/pulse sync.

Verification:

- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: succeeded, 0 warnings, 0 errors.
- `git diff --check` on touched code files: no whitespace errors; Git reported LF->CRLF warnings only.

## 2026-05-23 RT Budget Registry Cache Tranche

What was wrong:

- `CameraRTManager`, `PostFXRTManager`, `UIRTManager`, and `VisorRTManager` each resolved `GlobalRegistry.RenderTextureLifecycle` twice in `SlowTick()` memory measurement.
- That violates the cold DI/hot-path mandate: RT budget accounting is a recurring runtime cadence, not bootstrap wiring.

What was done:

- Added cached `RenderTextureLifecycleTracker` fields to all four RT budget managers.
- Added `IGlobalRegistryHotSwapListener` handling for `GlobalRegistryServiceSlot.RenderTextureLifecycleRuntime`.
- Moved measurement calls to the cached tracker while preserving existing preallocated `List<RenderTextureAllocationRecord>` buffers and budget behavior.

Cinematic Cheats used:

- None. This tranche is service-route hardening, not physical or visual simulation.

Exact microseconds saved:

- No profiler claim. STATIC_SOURCE estimate only: removed 2 registry reads per RT manager slow tick, 8 registry reads per full Camera/PostFX/UI/Visor budget sweep.

Verification:

- `git diff --check` on touched files: no whitespace errors; Git reported LF->CRLF warnings only.
- Guarded build: CPU/process gate waited until CPU was 5 percent and no dotnet/csc/VBCSCompiler process was present.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: succeeded, 0 warnings, 0 errors.

## 2026-05-23 VRAM Monitor Fallback Cache Tranche

What was wrong:

- `VRAMMonitor.ReadRenderTextureMemoryBytes()` used `GlobalRegistry.RenderTextureLifecycle` from the slow-tick measurement path when the profiler RT counter returned zero.
- `TetherManager` also has slow-tick registry polling, but the file is already dirty with unrelated HarpoonTension/Vault changes, so it was not touched in this commit.

What was done:

- Added a cached `RenderTextureLifecycleTracker` to `VRAMMonitor`.
- Added `IGlobalRegistryHotSwapListener` handling for `GlobalRegistryServiceSlot.RenderTextureLifecycleRuntime`.
- Kept profiler counters as first source of truth and used the cached lifecycle tracker only for fallback RT bytes.

Cinematic Cheats used:

- None. This tranche is service-route hardening, not simulation or visual approximation.

Exact microseconds saved:

- No profiler claim. STATIC_SOURCE estimate only: removes 1 registry read per VRAM slow tick when profiler RT memory counter is unavailable.

Verification:

- `git diff --check` on touched files: no whitespace errors; Git reported LF->CRLF warnings only.
- Guarded build waited while CPU was 100 percent with active `dotnet`, `csc`, and `VBCSCompiler`, then ran after the compiler window cleared.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: succeeded, 0 warnings, 0 errors.

## 2026-05-23 Sky Follow Atmosphere Cache Tranche

What was wrong:

- `SkySystemFollowCamera.Tick()` can call `ResolveSeaLevelY()`, which could call `ResolveAtmosphereManager()` and use `GlobalRegistry.Atmosphere` as fallback from the per-frame follow path.

What was done:

- Added a cached fallback `HectonAtmosphereManager`.
- Added `IGlobalRegistryHotSwapListener` handling for `GlobalRegistryServiceSlot.AtmosphereRuntime`.
- Preserved explicit inspector `atmosphereManager` ownership ahead of the cached fallback.

Cinematic Cheats used:

- None. This tranche is route hardening for a sky-follow helper.

Exact microseconds saved:

- No profiler claim. STATIC_SOURCE estimate only: removes 1 registry read from the sky follow tick path when sea-level lock needs atmosphere fallback.

Verification:

- `git diff --check` on touched files: no whitespace errors; Git reported LF->CRLF warnings only.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: succeeded, 0 warnings, 0 errors.

## 2026-05-23 Cave AO Player Cache Tranche

What was wrong:

- `HectonCaveVoxelAmbientOcclusionController.SlowTick()` can call `TryResolveViewerReferences()`, which used `GlobalRegistry.Player` when `viewerCamera` was unresolved.
- That made cave ambient-occlusion cadence depend on hot service-locator polling during viewer fallback.

What was done:

- Added cached `IPlayerRuntimeContext` storage.
- Added `IGlobalRegistryHotSwapListener` handling for `GlobalRegistryServiceSlot.Player`.
- Kept explicit `viewerCamera` ownership first and changed fallback resolution to cached player context only.

Cinematic Cheats used:

- None. This tranche is route hardening for cave AO viewer binding, not AO math or visual approximation.

Exact microseconds saved:

- No profiler claim. STATIC_SOURCE estimate only: removes 1 registry read per cave AO slow tick while viewer camera resolution is unresolved.

Verification:

- `git diff --check` on touched code file: no whitespace errors; Git reported LF->CRLF warning only.
- Guarded build ran at CPU 48 percent with no active dotnet/csc/VBCSCompiler process.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: succeeded, 0 warnings, 0 errors.

## 2026-05-23 Audio Log Runtime Cache Tranche

What was wrong:

- `AudioLogSystem.PlayLogByHash()` used `GlobalRegistry.Audio` during playback.
- `AudioLogSystem.PlayEncryptedPartialPreview()` used `GlobalRegistry.Audio` twice during preview route selection.
- `AudioLogSystem.ResolveNarrativeRadioInterference01()` used `GlobalRegistry.Player` while calculating depth/radiation interference for playback.

What was done:

- Added cached `IAudioService`, cached `SpatialAudioManager`, and cached `IPlayerRuntimeContext`.
- Added `IGlobalRegistryHotSwapListener` handling for `GlobalRegistryServiceSlot.Audio` and `GlobalRegistryServiceSlot.Player`.
- Changed log playback, encrypted preview playback, and interference calculation to use cached services only.

Cinematic Cheats used:

- None. This tranche is route hardening for narrative audio playback, not DSP/math simulation.

Exact microseconds saved:

- No profiler claim. STATIC_SOURCE estimate only: removes up to 3 registry reads per encrypted preview playback and up to 2 registry reads per full log playback with interference.

Verification:

- `git diff --check` on touched code file: no whitespace errors; Git reported LF->CRLF warning only.
- Guarded build waited while CPU was 100 percent with active dotnet/csc/VBCSCompiler processes, then ran after the compiler window cleared.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: succeeded, 0 warnings, 0 errors.

## 2026-05-23 Emergency Relay Runtime Cache Tranche

What was wrong:

- `EmergencyServiceRelay.IsDiscovered` pulled `GlobalRegistry.NarrativeDirector` from a read property.
- `EmergencyServiceRelay.Interact()` checked `GlobalRegistry.AudioLogs` twice before linked-log playback.
- `TryResolveInventory()` pulled `GlobalRegistry.Player` while granting relay rewards.
- `ResolveLocalized()` pulled `GlobalRegistry.Localization` from a read-looking helper used by relay labels, descriptors, interaction text, and reward warnings.

What was done:

- Added cached `HectonNarrativeDirector`, `AudioLogSystem`, `IPlayerRuntimeContext`, and `LocalizationManager` fields.
- Added `IGlobalRegistryHotSwapListener` handling for `NarrativeDirectorRuntime`, `AudioLogRuntime`, `Player`, and `LocalizationRuntime`.
- Converted relay discovery, linked-log playback, reward inventory fallback, and localization fallback to cached service references.

Cinematic Cheats used:

- None. This tranche is global-authority route hardening for interaction/read helpers, not physical simulation or presentation math.

Exact microseconds saved:

- No profiler claim. STATIC_SOURCE estimate only: removes up to 1 registry read per discovery read, 2 registry reads per linked-log relay activation, 1 registry read per reward inventory fallback, and 1 registry read per localized fallback call.

Verification:

- `git diff --check` on touched code file: no whitespace errors; Git reported LF->CRLF warning only.
- Guarded build ran at CPU 3 percent with no active dotnet/csc/VBCSCompiler process.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: succeeded, 0 warnings, 0 errors.

## 2026-05-23 Emergency Relay Director Cache Tranche

What was wrong:

- `EmergencyServiceRelayDirector.HandleRelayActivated()` pulled `GlobalRegistry.FirstHour` when registering a relay-route contact.
- `ShouldDriveBreadcrumbs()` pulled `GlobalRegistry.FirstHour` and `GlobalRegistry.AtlasSignal` during route gating.
- `ResolveLocalized()` pulled `GlobalRegistry.Localization` from a fallback helper used by contextual guidance.

What was done:

- Added cached `FirstHourDirector`, `AtlasSignalSystem`, and `LocalizationManager` fields.
- Added `IGlobalRegistryHotSwapListener` handling for `FirstHourRuntime`, `AtlasSignalRuntime`, and `LocalizationRuntime`.
- Changed route-contact registration, breadcrumb gating, and fallback localization to use cached services.

Cinematic Cheats used:

- None. This tranche is route hardening for first-hour relay guidance, not simulation or presentation math.

Exact microseconds saved:

- No profiler claim. STATIC_SOURCE estimate only: removes 1 registry read per route contact registration, up to 2 registry reads per breadcrumb gate check, and 1 registry read per fallback localization.

Verification:

- `git diff --check` on touched code file: no whitespace errors; Git reported LF->CRLF warning only.
- Guarded build ran at CPU 19.5 percent with no active dotnet/csc/VBCSCompiler process.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: succeeded, 0 warnings, 0 errors.
