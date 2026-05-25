# HECTON-8 Global Authority Burn-Down Plan

Date: 2026-05-23
Owner lane: HFI_AUDIT

Status: PENDING VERIFICATION

Evidence class: STATIC_SOURCE / STATIC_DOC / CLI_COMPILE where artifact cited

This plan orders the current global-authority burn-down work. It is not compile,

runtime, profiler, GC, memory, player-build, headset, Deck, macOS, Linux, or

console proof.

## Current Static Pressure

Source artifact:

- `Docs/AgentLogs/ArchitectureRiskHotlist_HFI_AUDIT.md`
- `Docs/AgentLogs/ArchitectureRiskHotlist_HFI_AUDIT.json`

Latest cleanup slice:

- 2026-05-23 EXTERNAL_CODEX cleanup:
  - Replaced selected registry/scene-search fallbacks with cached owner interfaces plus registry hot-swap refresh.
  - Covered UI/vegetation gates, battery/physics owner decoupling, loot-magnet quality routing, airlock/PDA-shell rebinding.
  - Covered flora/organic/trade/active-sonar/seismic/GPU scatter continuous quality cleanup and compile-wall repairs.
  - Artifact: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup32_scatter_quality.log`.
  - Boundary: CLI_COMPILE only; 0 `: warning ` and 0 `: error ` text matches.
  - Missing proof: runtime/profiler/GC.

Current linked HFI_AUDIT artifact reports an R27 candidate scan; recapture the tools below before treating these scores as current under later documentation churn:

| Domain | Score | Scored files | First review files |

|---|---:|---:|---|

| Root | 12899 | 180 | `PlayerInventory.cs`, `HectonFluidEngine.cs`, `SpatialAudioManager.cs` |

| World | 8228 | 102 | `WorldChunkResidencyManager.cs`, `DestructibleOrganicManager.cs` |

| Core | 4728 | 78 | `GlobalSignals.cs`, `FoveatedSimulationManager.cs`, `SystemDispatcher.cs` |

| Gameplay | 3452 | 88 | `CombatDamageRuntime.cs`, `ContextualPhysicalIkRig.cs` |

| Editor | 2435 | 52 | editor harness/bake tooling; review separately from runtime |

| Construction | 2237 | 26 | `DroneFleetManager.cs`, `HabitatGraphManager.cs`, `FluidPipeGraphRuntime.cs` |

| UI | 2156 | 86 | terminal/cockpit/VR UI runtime surfaces |

| Audio | 1595 | 16 | `PlayerCriticalProceduralAudioRenderer.cs`, synthesis/adaptive stem lanes |

| Atmosphere | 1362 | 8 | `GasDynamicsSolver.cs`, toxic outgassing/base atmosphere types |

| Power | 1304 | 9 | `LogisticsNetworkGraph.cs`, thermal/power solver contracts |

Interpretation: `Root` is the first problem because too much real domain logic

still lives directly under `Assets/_Project/Scripts`. That is not automatically

wrong C#, but it is bad ownership shape for global authority and platform

portability. The fix is classification and owner-route migration, not blind file

moves.

## Burn-Down Order

1. Root monolith classification.

   Review `PlayerInventory`, `HectonFluidEngine`, `SpatialAudioManager`,

   `SaveManager`, `Fabricator`, and nearby root-level managers. Each reviewed

   file must get an owner decision: keep local, move behind owner interface,

   route through typed SignalBus, or migrate cross-domain native state into

   DataVault/H8Memory.

2. World and streaming/residency.

   Review `WorldChunkResidencyManager`, destructible organic state, vegetation,

   voxel/terrain bridges, and offline baker runtime boundaries. Quest, Deck,

   and weak-PC portability depend on this slice because Addressables and content

   residency proof are still absent.

3. Core signal corridor.

   `Core/GlobalSignals.cs` scoring high is expected for a central lane owner.

   The review target is not deletion. The target is owner, capacity, overflow,

   retention, telemetry, layout, and bridge-lane documentation for every

   retained direct queue.

4. Gameplay and inventory truth.

   Inventory/combat/interaction state must not split into shadow local state,

   signal state, save state, and UI state. One fact needs one owner and one

   proof artifact.

5. Construction, power, atmosphere, and audio.

   These domains are next because they mix native buffers, solver state,

   signal fan-out, and presentation/runtime outputs. Migrate by route card and

   proof slice; do not merge them into one global "simulation state".

6. Platform proof ladder.

   After Windows/Copper Wire runtime proof, platform work proceeds in order:

   Steam Deck/low PC, PCVR, Quest 2/3, PICO, macOS, then console exploration.

   No platform readiness claim is allowed from package/settings text alone.

## Slice Rules

Every burn-down slice must record before/after evidence:

- `python Tools/ArchitectureRiskHotlistAudit.py`

- `python Tools/GlobalAuthorityGate.py`

- `python Tools/BufferIDSovereigntyAudit.py --fail-on-duplicates`

- `python Tools/DataVaultSovereigntyAudit.py --baseline Docs/AgentLogs/DataVaultSovereigntyBaselineCandidate_HFI_AUDIT.json --report Docs/AgentLogs/DataVaultSovereigntyAudit_HFI_AUDIT_candidate.md --audit-json Docs/AgentLogs/DataVaultSovereigntyAudit_HFI_AUDIT_candidate.json --fail-on-regression`

- `python Tools/AssemblyDependencyAudit.py` when asmdefs or cross-domain refs are touched

- `python Tools/PlatformPortabilityProofAudit.py` when platform readiness is discussed

Every changed global route must have:

- owner and route card;

- phase/cadence;

- max capacity and overflow policy;

- generation/shutdown behavior for Vault data;

- 300-frame telemetry or equivalent proof stream for critical state;

- review disposition from `GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`.

## Red Lines

- Do not turn `GlobalDataVault` into a mutable global heap. Local scratch stays

  local unless it crosses domain, job-owner, scene, save, replay, crash-dump, or

  relocation boundaries.

- Do not add new catch-all signal payloads, string event names, or broad

  `GameplaySignal` enum switches.

- Do not add binary low/high hardware switches. Any quality scaling must consume

  a continuous scalar such as `GlobalQualityWeight`.

- Do not remove asmdef references without source call-site classification,

  contract/facade replacement, and Unity import proof.

- Do not normalize the HFI candidate DataVault baseline as approval. It is a

  counter package for integrator review.

- Current R27 candidate no-regression check fails and the HFI candidate

  baseline remains unapproved. Forbidden constructors are `1149`; forbidden

  field-like `NativeArray<T>` declarations are `5132`. Regression domains are

  Physics `+10`, Construction `+5`, Editor `+5`, Power `+4`, World `+3`,

  Core `+2`, and Habitat `+1`. Use

  `Docs/AgentLogs/DataVaultSovereigntyAudit_HFI_AUDIT_candidate.md/json` for

  exact file deltas.

- Current R28 DataVault drilldown splits regression by execution surface.

  Runtime file-level gross growth is `+38`; Editor/offline-baker growth is

  `+12`. Runtime burn-down starts with `Tools/LaserCutterDodJobs.cs`,

  `Physics/Buoyancy/BuoyancySimdVectorization.cs`,

  `Power/PowerGridJacobiContracts.cs`, Construction pipe/socket files,

  `Gameplay/ScannerDataMiningRouter.cs`, `Core/Data/H8StaticDataContracts.cs`,

  and `World/Resources/ProceduralOreSpawner.cs`. Editor/offline-baker growth

  stays red but is second-order for frame-time portability.

## Platform Meaning

The current direction is structurally correct: registry for cold discovery,

typed signals for fan-out, Vault/H8Memory for owned native state, and static

gates for no-claim discipline. It is still not platform-ready.

Quest 2/3 and PICO readiness require, at minimum:

- XR provider serialized proof;

- Android ARM64 IL2CPP player build artifact;

- install and launch proof on device;

- input, storage, permission, shader/API, and native plugin parity proof;

- profiler/GC/memory/thermal capture;

- Addressables/content streaming and Data Monolith payload proof.

Until those artifacts exist, the platform status is scaffolded, not ready.
