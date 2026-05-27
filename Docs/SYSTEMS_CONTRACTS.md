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
