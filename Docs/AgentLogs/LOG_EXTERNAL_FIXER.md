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
