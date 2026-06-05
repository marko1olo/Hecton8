# SYSTEMS_CONTRACTS.md

Date: 2026-05-26
Status: PENDING VERIFICATION
Evidence class: STATIC_DOC / STATIC_SOURCE

Purpose: non-asset runtime systems contracts. This file defines target behavior and source-authority expectations; it is not runtime proof.

## Authority Boundary

- Read `Docs/PROJECT_BASELINE.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/README.md`, and current source before using this file.
- Current proof snapshots live in `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Dated reports and archives are evidence only.
- File names listed here are contract labels unless current source confirms concrete implementation.

## Universal Runtime Contracts

- Hot paths allocate `0 B/frame`.
- Cross-domain calls use cached owner interfaces, immutable snapshots, typed signal packets, or DataVault handles.
- First-party hot broadcasts use `SignalBus<T>`.
- `HectonEventBus` is managed mod/API/cold isolation.
- `GlobalRegistry` is cold DI/identity only.
- Persistent native/job-visible/global state uses `GlobalDataVault` or an owner-local proof contract.
- Replay/black-box state uses fixed-size circular buffers, not unbounded frame-lane logs.
- Feature cost must scale through continuous `GlobalQualityWeight` where fidelity can vary.
- Visual fake first for audio, lighting, fluid, pressure, ambience, particles, and distant motion.

## Source Reality Notes

Current source must be checked before implementing against old target names.

| Contract label | Source reality / expected owner |
|---|---|
| `SaveVersioning.cs`, `SaveMigrator.cs` | target labels; current save source includes `SaveManager.cs`, `SaveBinaryStorage.cs`, `SaveDataMigration.cs`, and AUP migration code |
| `SteamManager.cs`, `CloudSaveSync.cs` | `SteamManager.cs` exists under plugins; cloud conflict handling is source-owned elsewhere |
| `UnderwaterAudioProcessor.cs` | target residue; concrete audio authority starts with `SpatialAudioManager` plus procedural audio owners |
| `CrashTelemetry.cs`, `DebugConsole.cs` | target labels; telemetry source includes crash/global telemetry buffers |
| `BenchmarkRunner.cs` | target label; current tooling may be budget controller / verification probe code |
| `ControlRemapper.cs`, `AccessibilitySettings.cs` | target labels; current rebind UI source owns concrete route |

## 2026-06-05 Exact Core Anchors

Evidence class: STATIC_SOURCE / STATIC_DOC. These anchors prove source visibility and contract intent only. Compile, Unity import, Play Mode, profiler, GC, save/load, player-build, and platform behavior remain PENDING VERIFICATION.

| Source file | Owner | Static route / phase boundary | Hot-path prohibitions | Fault / black-box boundary | Missing proof |
|---|---|---|---|---|---|
| `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs` | `Hecton8.Core.Contracts.Signals` DTO family owned by Core signal contracts | 105 explicit-layout `ISignal` payload structs plus one `ISignalSnapshotTransformer<CombatDamageSignal>`; configured through `SignalBus<T>`/retained `GlobalSignals` surfaces outside the DTO file; producer and consumer phases remain owner-specific, not declared in this file | Payloads are unmanaged records only: no managed strings, arrays, delegates, or Unity object references are accepted for hot signals | Includes telemetry/fault-facing payloads such as `CrashTelemetrySignal`, `TelemetryAnomalySignal`, `MemoryPressureSignal`, and `FramePacingWarningSignal`; payload existence is not dump implementation proof | duplicate-lane source scan, layout validator artifact, compile/import result, runtime lane stress, overflow telemetry, GC/profiler capture |
| `Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs` | `Hecton8.Core.Persistence.Paging.H8BinaryWorldPager`; vault owner `SystemID.SavePersistence` | world page route: `world_data.h8bin` plus `h8_delta.wal`; cold `GlobalRegistry.DataVault` lookup, `IGlobalRegistryHotSwapListener`, background worker loop; no dispatcher phase is statically declared for caller-side enqueue/copy APIs | Save/page callers must not use this as a per-frame blocking read/write path; native staging must remain bounded and owner-owned | 300-entry `SaveWorldPagerTelemetryRing` is declared; dump names are declared, but static source shows `WriteBlackBoxDumps` and `WriteBlackBoxDump` bodies empty | page read/write roundtrip, WAL replay/corruption, dump artifact, DataVault ownership review, GC/profiler, player save/load |
| `Assets/_Project/Scripts/Core/Memory/VaultMemoryContracts.cs` | Core DataVault memory sovereignty contracts, `SystemID.CoreDataVault` | explicit layout records, `VaultBufferContract`, `VaultSovereigntyTelemetry`, and `VaultSovereigntyMaintenance`; maintenance source states Core `PRE_SIMULATION` FrostTick work | Consumers resolve generation-checked handles/slices; no private persistent native ownership, scene search, or hot registry polling is accepted by contract | 300-entry `VaultSovereigntyTelemetryRing`; planned dump target `Docs/AgentLogs/Dump_SHINOBU_100.bin`, absent until a fault export writes it; stale-handle/generation/fault data belongs here | ABI/layout report, compile/import, mutation-guard stress, telemetry dump artifact, GC/profiler, weak-hardware cadence proof |

## Save Versioning and Migration

Required:

- Save files include version, engine version, checksum, and migration route.
- Older save versions migrate through explicit deltas before load.
- Corruption recovery promotes verified backups only.
- Delta saves serialize only player-authored or player-modified state, not regenerated world truth.
- Save/load readiness needs write/read, migration, checksum-failure, locked-file, and player-path artifacts.

Forbidden:

- Direct JSON overwrite without backup.
- `.tmp` to final rename without crash-safe protocol.
- Saving full procedural world state when seed plus delta is sufficient.

## Steam and Cloud

Required:

- Achievements, cloud save, and telemetry hooks are explicit owner routes.
- Cloud sync is lifecycle-driven, not frame-driven.
- Conflict resolution is deterministic and timestamp/version aware.

Forbidden:

- Sync in `FixedUpdate`.
- Blocking Steam callbacks on the main thread.
- Unhandled achievement unlock failures.

## Underwater Audio

Required:

- Concrete owner starts from `SpatialAudioManager` and procedural audio contracts.
- 3D audio routes through underwater occlusion/mixer policy.
- Depth and material filtering use deterministic curves.
- Manual underwater Doppler/pitch paths need profiler/audio proof before acceptance.
- No real-time convolution on MX350-class target unless profiler proof says it fits.

Forbidden:

- Dry raw underwater audio.
- Underwater `AudioSource.spatialBlend = 0` for world sounds.
- Presentation audio becoming gameplay authority.

## Crash Telemetry and Debug Console

Required:

- Critical systems keep fixed-size black-box rings.
- Fault dumps are bounded and generated on fault/shutdown path, not hot path.
- Crash telemetry records scene, frame, state hash, flags, and stack context where available.

Forbidden:

- Unbounded log writes in frame lane.
- Telemetry upload without user consent.
- In-game debug console in public EA build unless explicitly gated.

## Performance CI

Required:

- Benchmarks record frame time, main thread, VRAM, SetPass, batches, GC, and native persistent memory.
- MX350/i3-class capture is the minimum proof lane.
- Shader/rendering changes require render proof, not only compile proof.
- Regression reports include baseline, changed slice, and artifact paths.

Forbidden:

- Manual profiler claims without captured output.
- Ignoring more than `10%` regression without owner-approved load-shed proof.
- Treating editor-only run as player/platform proof.

## Runtime Degradation

If frame time exceeds budget for sustained frames, owners lower quality through continuous scalars:

1. Flora animation amplitude and cadence.
2. GPU boid count and update cadence.
3. Parallax/height blend weight.
4. Post-processing taps/radius/sample count.
5. Volumetric fog resolution and march count.
6. Noncritical rigidbody/physics solver scope.

Forbidden:

- Static binary quality tiers as runtime behavior.
- Hard crashes on performance spikes.
- Quality changes that alter gameplay truth ownership, DTO layout, save identity, or authority route.

## Lighting and Probe Grid

Required:

- Probe density follows biome/runtime need.
- Static base/ruin lighting stays baked where possible.
- Runtime outdoor lighting prefers cheap deterministic presentation.

Forbidden:

- Realtime GI as default outdoor solution.
- Excessive shadow cascades on MX350-class target.
- Unbaked cave probes when vertex AO or baked proxy is sufficient.

## Accessibility and Controls

Required:

- Full keyboard/gamepad rebinding through input action maps.
- Rebind state persists per profile.
- Critical actions have visible/current bindings.
- UI scaling and color modes are explicit settings.

Forbidden:

- Hardcoded `KeyCode` in gameplay authority.
- Unremapped critical actions.
- UI settings that break HUD readability.

## Endgame Retention

Required:

- Late-game events must use deterministic owner routes.
- Rewards and procedural caches must be save-safe.
- Difficulty scales through continuous systems, not hidden binary branches.

Forbidden:

- Static world after story completion.
- Dead zones without content beyond the accepted exploration budget.
- Endgame spawns that bypass save/streaming authority.

## AI Code Integration

Required before merge:

1. Isolated branch or owned change slice.
2. Static authority review.
3. Compile proof for changed source slice.
4. Runtime/profiler/GC proof if behavior changed.
5. Manual code review for architecture, allocation, and hardcoded values.

Forbidden:

- Direct push to main.
- Unprofiled runtime merge.
- Treating "works in editor" as production ready.
