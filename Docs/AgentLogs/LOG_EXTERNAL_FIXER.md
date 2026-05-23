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
