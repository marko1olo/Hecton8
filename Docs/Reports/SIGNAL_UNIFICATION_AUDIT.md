# Signal Unification Audit

Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R13 Report Snapshot Boundary

This report file is a snapshot/provenance document. It is active only where it agrees with:

- `Docs/README.md`
- `Docs/Reports/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Historical `PASS`, `VERIFIED`, `current`, `latest`, counter, compile, runtime, 0-GC, frame-time, cost, and performance statements inside this report are not current proof unless the exact claim links a fresh artifact path, command/tool, timestamp, evidence class, and unresolved-error list. No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied by this file alone.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
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
  - `PlayerLookTargetSignal`
  - `PlayerStateSignal`
  - `SurvivalVitalsChangedSignal`
  - `PlayerActionProgressSignal`
  - `CameraPositionSignal`
  - `CameraFrustumSignal`
  - `HullDeformedSignal`
  - `BaseModuleCompromisedSignal`
  - `PlayerBaseEnterSignal`
  - `PlayerBaseExitSignal`
  - `AupPreShiftSignal`
  - `AupShiftSignal`
  - `RadiationDoseSignal`
  - `TemperatureChangedSignal`
  - `RadiationSourceSignal`
  - `CullingOverloadSignal`
  - `WakeGeneratedSignal`
  - `BiomeGradientSignal`
  - `MemoryPressureSignal`
  - `ResolutionChangedSignal`
  - `SystemHealthIndexSignal`
  - `CpuStarvationSignal`
  - `AcousticPingSignal`
  - `FluidIncursionSignal`
  - `SubmarineFloodStateSignal`
  - `FluidDensityChangedSignal`
  - `StreamingTurbulenceSignal`
  - `AtmosphericReentrySignal`
  - `VehicleUpgradesChangedSignal`
  - `SaveLifecycleSignal`
  - `SaveStatusSignal`
  - `LightLevelSignal`
  - `SubmarineLightsChangedSignal`
  - `PhysiologyStateSignal`
  - `PlayerStressSignal`
  - `TraumaSignal`
  - `HapticRequest`
  - `PlayerActionCancelledSignal`
  - `DropPodLandedSignal`
  - `ItemAcquiredSignal`
  - `BiomeChangedSignal`
  - `SectorResidencyHydratedSignal`
  - `SectorDehydratedSignal`
  - `ChunkDehydratedSignal`
  - `ItemDurabilityChangedSignal`
  - `BrownoutSignal`
  - `EntityDeathSignal`
  - `MovementAcousticSignal`
  - `SwarmDispersedSignal`
  - `ScannerToolActiveSignal`
  - `StorageDebtSignal`
  - `PrologueCompleteSignal`
  - `ManualOverridePulledSignal`
  - `WfcOutpostGeneratedSignal`
  - `WfcOutpostDoorPowerSignal`
- Added source-publish finite vaccination before legacy queue enqueue for:
  - `TimeDilationSignal`
  - `SimulationPauseSignal`
  - `BulletTimeVisualSignal`
  - `WeatherStrengthSignal`
- Replaced per-push guard type discovery with a per-generic guard-kind cache (`SignalPayloadFiniteGuardCache<T>.Kind`), so lane type resolution is cold and hot pushes use a byte switch.
- `SignalBus<T>.FlushPreSimulation()` now caps to current snapshot capacity instead of growing `NativeList<T>` at the pre-simulation boundary.
- Added main-thread publish sanitization before legacy `DamageSignal` and `ImpactSignal` queues receive packets.
- Rewired `Gameplay/Combat/CombatDamageRuntime.cs` to consume `SignalBus<Hecton8.Core.Signals.CombatDamageSignal>.GetFrameSnapshot()` instead of destructively draining `GlobalSignals.TryDequeueDamage`.
- Rewired `World/SoundscapeSystem.cs` to consume `SignalBus<ImpactSignal>.GetFrameSnapshot()` instead of destructively draining `GlobalSignals.TryDequeueImpact`.
- Cached soundscape audio/scalability dependencies outside impact-drain logic via GlobalRegistry hot-swap events and `ScalabilityEvents`.
- Cached combat runtime math/scalability policy outside `ResolveRuntimeMathLod()`.
- Padded `HighSpeedImpactSignal` from 88 to 96 bytes; static scan found no remaining non-16-byte `StructLayout(Size=...)` values in `GlobalSignals.cs`.
- Replaced bridge `new ...Signal` object-initializer text with `default` plus explicit field assignment in signal mirror paths.
- Replaced Core direct `SignalBus<T>.Push(new ...Signal)` producers and selected Core `new ...Signal` value initializers with `default` packets plus explicit field assignment in `SystemDispatcher.cs` and `InputDispatcher.cs`.
- Removed `FixedString64Bytes Prompt` from `PlayerLookTargetSignal`; the signal now carries `PromptHash` and reserved uint args only. Prompt text lives in bounded `PlayerLookTargetPromptCache` sidecar storage keyed by hash.

## Remaining Legacy Evidence

The mandatory scan still returns first-party legacy communication:
- 18 `HectonEventBus.Publish` producers remain across weather, construction/logistics, economy, inventory, progression, and mod-facing code.
- 59 files still contain `Action<T>`, `event Action`, or delegate patterns.
- 30 files still contain `UnityEvent`/`UnityAction`.

Status: BLOCKED BY DOMAIN BLAST RADIUS for global eradication. This pass only standardizes the confirmed damage/impact hot lanes.


## Static Zero-GC / String Poison Scan

- `SignalPayloadFiniteGuards` contains no `new` and no `string`; the former `new float3(...)` fallback is now scalar assignment.
- Focused Core/touched-path scan found no `new float3`, `FixedString64Bytes Prompt`, `signal.Prompt`, direct `SignalBus<T>.Push(new ...Signal)`, or `new ...Signal` text in `GlobalSignals.cs`, `PlayerLookTargetPromptCache.cs`, `SystemDispatcher.cs`, `InputDispatcher.cs`, `PlayerInteraction.cs`, or `DiegeticTooltipSystem.cs`.
- `GlobalSignals.cs` string hits are SignalBus cold labels or method parameters (`OwnerLabel`, `ResolveQueueLabel`, `ComputeStableSignalLaneHash`, native sentinel labels), not signal DTO payload fields.
- Focused scan found no `FixedString` in `GlobalSignals.cs`, `PlayerInteraction.cs`, or `DiegeticTooltipSystem.cs` after the look-target prompt rewrite.
- `new` hits in `GlobalSignals.cs` are cold static arrays/adapters or native collection allocation; hot bridge signal DTO construction was removed from mirror paths. Runtime GC proof remains unavailable without Unity Profiler/GCMonitor.
- Loop 15 static sweep found no remaining `SignalBus<T>`-referenced Core contract struct with explicit `float`, `float2`, `float3`, `float4`, or `AbsoluteUniversePosition` fields lacking a cached ingress guard in `GlobalSignals.cs`. Legacy-only queues still require domain-owner migration.

## Compile Evidence

Command:

```powershell
dotnet build Hecton8.Core.csproj -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false
```

Result: last known green errors-only run succeeded with 0 warnings / 0 errors after Loop 13 Core producer cleanup. Latest Loop 14 errors-only run exits 1 before source-level verification because Unity-generated package assemblies/surfaces are missing from `Library/ScriptAssemblies` and package project references. Filtered diagnostics on touched files are unresolved external Unity/TMP/InputSystem/URP types, not new guard syntax. Earlier warnings-only compile passes recorded generated-project/package duplicate-type warnings from the ignored CLI project after including IK job source beside a stale imported assembly. The repair included existing Unity-imported source paths for the prompt-cache, WFC/blueprint, and IK job source, plus restoration of a referenced private audio Burst probe job; no stub contracts were invented.

Evidence class: STATIC_SOURCE for Loop 14 guard expansion, last-known CLI_COMPILE for Loop 13, and DEPENDENCY_BLOCKED for fresh Loop 14 build. Runtime GC, Unity Console, and full global event eradication remain PENDING because Unity MCP refresh was unavailable and the mandatory legacy scan still returns 2230 non-zero hits.
