# Signal Unification Audit

Status: PENDING VERIFICATION
Owner: ARCHITECTURAL_SIGNAL_STANDARDIZER
Evidence class: STATIC_SOURCE until compile/profiler/Unity Console artifacts exist.

## Initial Static Scan

Command:

```powershell
rg "Action<|UnityEvent|EventBus\.Publish|NativeQueue<"
```

Result summary:
- Large third-party/vendor noise exists under `Packages`, package caches, node modules, and imported assets.
- First-party hits include `Assets/_Project/Scripts/Core/GlobalSignals.cs`, `Gameplay/BaseAirlockEvents.cs`, `Gameplay/BaseAirlock.cs`, `EntityChangeDetector.cs`, `Economy/ScrapManager.cs`, `Economy/ResourceRecyclerModule.cs`, `Fauna/FaunaBrain.cs`, `Fabricator.cs`, and multiple editor harnesses.
- Current authority doc `Docs/ARCHITECTURE/GLOBAL_SIGNAL_CORRIDOR.md` says `GlobalSignals` owns 33 first-party NativeQueue lanes and `Publish(in T)` is enqueue-only, not callback dispatch.

## Audit Scope

Included:
- `Assets/_Project/Scripts/**/*.cs`
- `Docs/ARCHITECTURE/GLOBAL_SIGNAL_CORRIDOR.md`
- `Docs/SYSTEMS_CONTRACTS.md`
- Task-relevant mandates in `.agents-skills`

Excluded unless source references demand it:
- Third-party packages under `Packages`, `Assets/Crest`, `Assets/MapMagic`, `Assets/VolumetricLightBeam`, `Assets/ScifiFacility`
- Editor-only test harnesses except where they enforce first-party signal policy

## Current Risk Flags

- `Docs/Tasks/CURRENT_BATCH.md` does not contain this agent ID. User-provided XML is active boundary.
- Worktree was already dirty with many modified/deleted/untracked assets and code files before this agent added these audit files.
- Broad "0 legacy events found" is not claimable yet.

## Protocol Map - First-Party Source

Command:

```powershell
rg -l "<pattern>" Assets/_Project/Scripts --glob "*.cs"
```

| Protocol | First-party files | Notes |
|---|---:|---|
| `Action<T>` / `event Action` / `delegate` | 59 | Includes cold async callbacks, input/UI surfaces, `EntityChangeDetector`, survival state, and fauna callbacks. Requires hot-path classification before removal. |
| `UnityEvent` / `UnityAction` | 30 | Mostly designer/prefab hooks in gameplay/UI. Simulation-loop use remains suspect; prefab contract risk prevents blind deletion. |
| `HectonEventBus.Publish` / legacy EventBus publish | 18 | Live producers in weather, economy, item pickup, progression, mod API, player inventory, builder, survival, random events. |
| direct `NativeQueue<T>` lanes | 108 | Mix of valid owned native queues and local ad-hoc event wrappers. Must be normalized only where cross-domain broadcast exists. |
| typed `SignalBus<T>` consumers | 31 | Existing lane model already present. Consumers include combat, kinematics, UI, world, save, graphics, prologue, determinism. |
| typed `SignalBus<T>` producers | 14 | Producer coverage is incomplete relative to consumers and legacy EventBus producers. |

## Duplicate Signal Findings

| Duplicate shape | Source evidence | Decision |
|---|---|---|
| `Hecton8.Core.Signals.DamageSignal` | `Core/GlobalSignals.cs` | Legacy 32-byte central damage packet. Still kept for compatibility/latest damage reads. |
| `Hecton8.Core.Signals.CombatDamageSignal` | `Core/GlobalSignals.cs` | Typed 64-byte lane packet. Chosen as the unified bus-facing combat damage DTO for cross-domain signaling. |
| `Hecton8.Gameplay.DamageSignal` | `Gameplay/HabitatIntegrityManager.cs` | Local receiver callback packet. Requires later wrapper/receiver migration; not removed in this pass to avoid public signature break. |
| `Hecton8.Gameplay.CombatDamageSignal` | `Gameplay/Combat/CombatDamageRuntime.cs` | Internal job packet. Kept as private runtime shape because it is SoA/job-optimized and not a cross-domain signal. |
| `ImpactSignal` | `Core/GlobalSignals.cs` plus impact producers | Sanitized legacy publishes now mirror into `SignalBus<ImpactSignal>`; `World/SoundscapeSystem.cs` consumes typed snapshots. Legacy queue kept as compatibility API only. |

## Interface Drift Findings

`rg "\bICoreAudio\b|\\bIAudioService\\b"` found `IAudioService` as the active first-party service contract and found no `ICoreAudio` symbol in `Assets/_Project/Scripts`. Current drift is not duplicate interface naming; it is repeated hot/cadence-unknown `GlobalRegistry.Audio` resolution at call sites.

## First Rewrite

- Added finite-value vaccination inside `SignalBus<T>.Push()` for known consolidated damage/impact lanes:
  - `DamageSignal`
  - `ImpactSignal`
  - `HighSpeedImpactSignal`
  - `CombatDamageSignal`
- Extended finite-value vaccination to additional typed bridge lanes:
  - `FluidImpulseSignal`
  - `SystemPauseSignal`
  - `WeatherChangedSignal`
- Added source-publish finite vaccination before legacy queue enqueue for:
  - `TimeDilationSignal`
  - `SimulationPauseSignal`
  - `BulletTimeVisualSignal`
  - `WeatherStrengthSignal`
- Replaced per-push guard type discovery with a per-generic guard-kind cache (`SignalPayloadFiniteGuardCache<T>.Kind`), so lane type resolution is cold and hot pushes use a byte switch.
- Added main-thread publish sanitization before legacy `DamageSignal` and `ImpactSignal` queues receive packets.
- Rewired `Gameplay/Combat/CombatDamageRuntime.cs` to consume `SignalBus<Hecton8.Core.Signals.CombatDamageSignal>.GetFrameSnapshot()` instead of destructively draining `GlobalSignals.TryDequeueDamage`.
- Rewired `World/SoundscapeSystem.cs` to consume `SignalBus<ImpactSignal>.GetFrameSnapshot()` instead of destructively draining `GlobalSignals.TryDequeueImpact`.
- Cached soundscape audio/scalability dependencies outside impact-drain logic via GlobalRegistry hot-swap events and `ScalabilityEvents`.
- Cached combat runtime math/scalability policy outside `ResolveRuntimeMathLod()`.
- Padded `HighSpeedImpactSignal` from 88 to 96 bytes; static scan found no remaining non-16-byte `StructLayout(Size=...)` values in `GlobalSignals.cs`.
- Replaced bridge `new ...Signal` object-initializer text with `default` plus explicit field assignment in signal mirror paths.

## Remaining Legacy Evidence

The mandatory scan still returns first-party legacy communication:
- 18 `HectonEventBus.Publish` producers remain across weather, construction/logistics, economy, inventory, progression, and mod-facing code.
- 59 files still contain `Action<T>`, `event Action`, or delegate patterns.
- 30 files still contain `UnityEvent`/`UnityAction`.

Status: BLOCKED BY DOMAIN BLAST RADIUS for global eradication. This pass only standardizes the confirmed damage/impact hot lanes.

## Static Zero-GC / String Poison Scan

- `SignalPayloadFiniteGuards` contains no `new` and no `string`.
- `GlobalSignals.cs` string hits are SignalBus cold labels or method parameters (`OwnerLabel`, `ResolveQueueLabel`, `ComputeStableSignalLaneHash`, native sentinel labels), not signal DTO payload fields.
- `new` hits in `GlobalSignals.cs` are cold static arrays/adapters or native collection allocation; hot bridge signal DTO construction was removed from mirror paths. Runtime GC proof remains unavailable without Unity Profiler/GCMonitor.

## Compile Evidence

Command:

```powershell
dotnet build Hecton8.Core.csproj
```

Result: failed with 129 errors / 47 warnings on the latest attempt. Current visible errors are missing external/neighbor assemblies and types (`Hecton8.Environment.Fluids`, `Hecton8.Audio.Virtualization`, `MacroSwarm`, `BrineLayerSample`, `SoundEmissionSignal`, `AcousticAup`, `VirtualVoice*`, etc.). A filtered build scan for `GlobalSignals.cs`, `CombatDamageRuntime.cs`, and `SoundscapeSystem.cs` returned no matches.

Evidence class: CLI_COMPILE for failure state only. Runtime GC and Unity Console remain PENDING VERIFICATION.
