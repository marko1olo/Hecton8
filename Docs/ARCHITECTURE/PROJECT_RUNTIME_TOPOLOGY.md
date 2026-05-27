# Project Runtime Topology

Date: 2026-05-28
Status: STATIC_SOURCE_SNAPSHOT / RUNTIME PENDING
Owner: DOC_ROOT_ARCH_AUDIT
Evidence class: STATIC_SOURCE / STATIC_FILESYSTEM

Purpose: current source-backed project wiring map for agents before they edit code.

This is not compile, Unity import, Play Mode, profiler, GC, save/load, player-build, shader, platform, or visual proof.

## Authority Boundary

- `AGENTS.md`, `.agents-skills/`, `Docs/PROJECT_BASELINE.md`, and active files in `Docs/ARCHITECTURE` remain doctrine.
- Current source under `Assets/_Project` wins over dated reports and archived prompts.
- This file records static topology: paths, packages, scenes, source owners, and proof gaps.
- Runtime readiness still requires the proof ladder in `PLATFORM_PORTABILITY_PROOF_LADDER.md` and gates in `Docs/QUALITY_GATES.md`.

## Current Project Envelope

| Fact | Current static value | Source |
|---|---|---|
| Unity editor | `6000.4.1f1` | `ProjectSettings/ProjectVersion.txt` |
| Render pipeline package | `com.unity.render-pipelines.universal` `17.4.0` | `Packages/manifest.json` |
| Addressables package | `com.unity.addressables` `2.7.6` | `Packages/manifest.json` |
| Input package | `com.unity.inputsystem` `1.19.0` | `Packages/manifest.json` |
| Memory Profiler package | `com.unity.memoryprofiler` `1.1.12` | `Packages/manifest.json` |
| XR packages | OpenXR `1.17.0`, Meta OpenXR `2.5.0`, XR Management `4.6.0` | `Packages/manifest.json` |
| First-party asmdefs | `167` under `Assets/_Project` | static filesystem count |
| Data Monolith payload | `1,064,384` bytes | `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` |

Package presence is not platform readiness. XR/package fields do not prove provider setup, device launch, comfort, thermal, or frame pacing.

## Active Scene Spine

Enabled scenes in `ProjectSettings/EditorBuildSettings.asset`:

1. `Assets/_Project/Scenes/00_BOOTSTRAP.unity`
2. `Assets/_Project/Scenes/01_MAIN_MENU.unity`
3. `Assets/_Project/Scenes/01_ORBIT.unity`
4. `Assets/_Project/Scenes/02_HECTON_WORLD.unity`

Current new-game route is the static route already recorded by the first-20-minutes contracts:

```text
00_BOOTSTRAP -> 01_MAIN_MENU -> 01_ORBIT -> 02_HECTON_WORLD
```

Load-game resume may still enter `02_HECTON_WORLD` directly from `01_MAIN_MENU`. Sandbox scenes exist under `Assets/_Project/Scenes`, but they are not enabled build-spine proof.

Authority drift note:

- `AGENTS.md` still contains older no-orbit scene-flow wording.
- Current BuildSettings, first-20 route docs, and `PROLOGUE_ORBIT_HANDOFF_ROUTE_CARD_13PRO.md` include `01_ORBIT`.
- Treat this as unresolved until owner/integrator updates `AGENTS.md` or removes `01_ORBIT`.
- Do not claim Play Mode route proof from this static state.

## Core Source Spine

| Runtime area | Source anchor | Route rule |
|---|---|---|
| Bootstrap | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | cold owner setup, Kahn order, no scene-search dependency loops |
| Registry/DI | `Assets/_Project/Scripts/Core/GlobalRegistry.cs` | cold identity and dependency injection only |
| Hot first-party signals | `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs` | typed bounded payloads, owner/phase/capacity required |
| Legacy signal bridge | `Assets/_Project/Scripts/Core/GlobalSignals.cs` | documented bridge lanes only |
| Native ownership | `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` and `H8Memory.cs` | cross-domain persistent/job-visible buffers use generation-checked handles |
| Dispatcher | `Assets/_Project/Scripts/Core/SystemDispatcher.cs` and `SystemDispatcherContracts.cs` | `PRE_SIMULATION`, `SIMULATION`, `POST_SIMULATION`, `VISUAL_SYNC` owner windows |
| Scalability | `Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs` | continuous `GlobalQualityWeight`, no binary quality switch |
| Save | `Assets/_Project/Scripts/SaveBinaryStorage.cs` and `SaveManager.cs` | writer `0x000B`, header `56` bytes, proof needs route save/load |
| Scene service | `Assets/_Project/Scripts/Core/SceneRuntimeService.cs` | scene activation gate, cached service route |
| Player context | `Assets/_Project/Scripts/Core/PlayerRuntimeContextService.cs` | owner publishes player runtime truth |
| Environment context | `Assets/_Project/Scripts/Core/EnvironmentRuntimeContextService.cs` | owner publishes environment runtime truth |
| Input | `Assets/_Project/Scripts/Core/InputDispatcher.cs` | service route; no hot singleton polling |
| Physics application | `Assets/_Project/Scripts/PhysicsApplySystem.cs` | dispatcher-owned fixed/post-fixed packet windows |
| Audio | `Assets/_Project/Scripts/SpatialAudioManager.cs` | presentation/audio consumes snapshots and owned signal lanes |
| World/scatter | `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs` | one world/scatter owner path until profiler proof says otherwise |
| Encounter pacing | `Assets/_Project/Scripts/HectonDirectorAI.cs` | director route must not become a second owner for world truth |
| HUD/UI | `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` | presentation reads snapshots; UI does not own simulation truth |

## Runtime Route Map

```text
Bootstrap
  -> owner-local setup
  -> GlobalRegistry cold service identity
  -> SystemDispatcher phase ownership
  -> GlobalDataVault native snapshots and handles
  -> SignalBus<T> hot unmanaged packets
  -> presentation/audio/UI visual sync
```

Route rules:

- One gameplay fact has one owner, one route, and one proof artifact.
- Runtime owners publish once from their owner phase.
- Consumers read immutable snapshots, cached service interfaces, generation-checked handles, or typed signal payloads.
- `Get*`, `TryGet*`, `Resolve*`, and `Read*` accessors are pure.
- Read accessors must not allocate, grow buffers, publish, search scenes, sync transforms, complete jobs, or mutate global state.
- `GlobalRegistry` is not a hot polling bus.
- `HectonEventBus` is mod/API/cold managed isolation, not first-party gameplay flow.

## Data And Persistence

- Data Monolith target: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- Current static payload exists and is `1,064,384` bytes.
- Scoped Python validator recheck on 2026-05-28 passed for current StreamingAssets `.h8bin` payloads with narrowed Data Monolith source/runtime roots: `Docs/Reports/DOC_ROOT_ARCH_AUDIT_h8bin_validator_narrow_20260528.json`.
- Readiness is still `PENDING VERIFICATION` without import, bake, boot, checksum, player, save/load, and memory proof.
- Save writer version: `0x000B`.
- Current save header size: `56` bytes.
- AUP/blit layout: `48` bytes.

## First Route To Prove

The product spine is not "all systems compile". It is the Copper Wire V0 route:

```text
boot -> world load -> safe exit -> swim -> oxygen/depth/pressure
-> find copper -> collect Data_Copper -> quest_copper_sample
-> craft Recipe_CopperWire -> save -> load -> return to same state
```

Read before product/runtime work:

- `FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`
- `FIRST_20_MINUTES_ROUTE_BRIEF.md`
- `BOOT_SEQUENCE_TOPOLOGY.md`
- `DISPATCH_PIPELINE.md`
- `PLATFORM_PORTABILITY_PROOF_LADDER.md`

## Verification Gaps

Current static topology does not prove:

- full-solution compile health;
- Unity import or clean Console;
- Play Mode or player launch;
- route completion;
- profiler, GC, Memory Profiler, or VRAM budget;
- Data Monolith runtime load/checksum;
- save/load roundtrip;
- shader/import/render correctness;
- XR, Steam Deck, Linux, macOS, Quest, PICO, or console readiness.

Use `HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` for latest proof snapshots and cite fresh artifacts before changing status.
