# Script Documentation Coverage Gap Ledger - 2026-06-05

Status: STATIC_SOURCE / STATIC_DOC COVERAGE GAPS

Evidence class: STATIC_SOURCE, STATIC_DOC.

Source: script documentation coverage scout over `2545` `.cs` files and `276` stable doc files. No Unity, dotnet, build, Play Mode, profiler, GCMonitor, scene readback, or player-build evidence was run.

## Boundary

This ledger records exact stable-doc anchor gaps. A class may appear in historical reports, archives, BibleMandateAudits, or broad domain docs and still fail this ledger if no current stable document ties the live class to owner, route, phase, signals/DataVault lanes, failure mode, and missing proof.

Do not use this ledger to claim runtime defects. Use it to assign documentation owners.

## Highest-Risk Exact Anchor Gaps

| Risk | Live class/source | Domain | Stable-doc target | Required next documentation action |
|---:|---|---|---|---|
| 1 | `Core/Signals/GlobalSignalPayloads.DomainRemainder.cs` / signal DTOs | core signal contracts | `Docs/SYSTEMS_CONTRACTS.md`, `Docs/ARCHITECTURE/GLOBAL_SIGNAL_CORRIDOR.md` | Add exact DTO family map, owner lanes, overflow/coalescing boundary, and proof gaps. |
| 2 | `SaveSystem/H8BinaryWorldPager.cs` / `H8BinaryWorldPager` | persistence/world paging | `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md`, `persistence.md` | Add pager owner, phase, IO route, native lifetime, fault mode, and save/load proof requirements. |
| 3 | `Core/Memory/VaultMemoryContracts.cs` / vault DTOs | DataVault memory sovereignty | `data.md`, `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md` | Add exact DTO/lifetime map and distinguish fault dump/cold helper paths from hot ownership. |
| 4 | `Plugins/Crest/OceanKinematics/OceanKinematicsVaultRuntime.cs` | ocean/physics vault bridge | `water.md`, Crest/ocean kinematics route card | Add Crest bridge ownership, no-material-clone rule, data route, telemetry, and visual/profiler proof gaps. |
| 5 | `Physics/Vehicles/SubmarineDynamicsContracts.cs` | vehicle physics | submarine dynamics route docs, `physics.md` | Add contract/job map, force ownership, black-box lanes, and vehicle proof blockers. |
| 6 | `Physics/Vehicles/VehicleComponentDamageJobs.cs` | vehicle damage jobs | `Docs/ARCHITECTURE/Vehicle_Component_Damage_Router_SHINOBU_152.md` | Add class/job mapping and current proof status to existing route card. |
| 7 | `Power/SubmarineOsThermalGridRuntime.cs` | power/logistics thermal grid | `logistics.md`, `Docs/ARCHITECTURE/HABITAT_LOGISTICS_GRAPH.md` | Add thermal grid owner, route, power/logistics coupling, black-box dump boundary, and runtime proof gaps. |
| 8 | `UI/Navigation/DiegeticGyroCompassRuntime.cs` | diegetic navigation UI | `sonar.md`, `UI_DIEGETIC_HUD_STANDARDS.md` | Add exact compass owner, dispatcher phase, HUD text/GC boundary, and navigation proof requirements. |
| 9 | `Visor/SpectrumSystem.cs` / `SpectrumSystem` | visor/sonar spectrum | `sonar.md`, visor route doc | Add exact spectrum owner, signal inputs, DataVault/black-box boundary, and UI/profiler proof gaps. |
| 10 | `Visor/HectonVisorFluidDistortionFeature.cs` | visor RenderGraph/distortion | `rendering.md`, visor/water optics route doc | Add RenderGraph feature ownership, visual fake boundary, black-box path, and Frame Debugger proof requirement. |
| 11 | `Graphics/Materials/VisualPressureAgingRuntime.cs` | material aging GPU/jobs | `shaders.md`, material response route doc | Add exact material-aging owner, `GlobalQualityWeight` effect, GPU upload budget, and visual/profiler proof gaps. |
| 12 | `Graphics/Materials/ShinobuMaterialResponseRuntime.cs` | material response GPU/jobs | `shaders.md`, `rendering.md` | Add exact class anchor and route to texture-set rows, Vault/GPU ownership, and proof requirements. |
| 13 | `World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs` | world anomaly runtime | `world.md`, anomaly route doc | Add exact anomaly runtime owner, route truth boundary, save/state effects, and proof blockers. |
| 14 | `Gameplay/Mining/DeployableSdfDrillRuntime.cs` | mining/SDF gameplay | `tools.md`, `voxels.md` | Add deployable drill distinction from starter handheld seafloor drill, SDF route, save/inventory effects, and proof blockers. |
| 15 | `World/SargassumCutManager.cs` | flora interaction/cutting | `world.md`, `3DMODEL_FLORA_CORAL.md` | Add flora-cut owner, GPU mask route, gameplay interaction boundary, and visual/profiler proof gaps. |
| 16 | `World/HectonAnomalyEngine.cs` | geology/SDF jobs | `terrain.md`, `voxels.md` | Add exact engine anchor, terrain/SDF boundary, ownership, and missing scene/profiler proof. |
| 17 | `Lighting/DynamicPointLightCulling/DynamicPointLightCullingDirector.cs` | lighting culling | existing lighting route doc | Add live-class tie, culling profile route, buffer ownership, and Frame Debugger/profiler proof gaps. |
| 18 | `HectonBoidController.cs` / `HectonBoidController` | AI GPU boids | `ai.md`, `creatures.md` | Add exact boid owner, GPU path, ecosystem boundary, and visual/perf proof requirements. |
| 19 | `Tools/ToolDurabilitySystem.cs` | tool durability gameplay | `tools.md`, `Docs/SYSTEMS_CONTRACTS.md` | Add exact durability owner, inventory/tool truth, save effects, and UI proof gaps. |
| 20 | `AtlasSignal/SignalBeacon.cs` / `SignalBeacon` | navigation/beacon telemetry | `sonar.md`, atlas/navigation route doc | Add exact beacon owner, route signal role, save/quest boundary, and sonar/navigation proof gaps. |

## Work Waves

1. Core truth anchors: risks 1-3.
2. Persistence and memory route anchors: risks 2-3.
3. Water, vehicle, damage, lighting anchors: risks 4-7 and 17.
4. UI, sonar, visor, beacon anchors: risks 8-10 and 20.
5. Materials, world anomaly, mining, flora, AI/tool anchors: risks 11-16, 18-19.

## Rejection Rules

- Do not copy archive/BibleMandateAudit status into active docs as current readiness.
- Do not mark any item `VERIFIED`, `READY`, `COMPLETE`, `PLAYMODE`, `PROFILER`, `0 GC`, or `VISUAL PASS` without fresh artifact paths.
- Do not broaden docs with prose that lacks owner, phase, lane, failure mode, and proof artifact class.
- Do not edit `AGENTS.md` as part of this ledger work.

## Low / Middle / High / Ultra Consequences

- Low: agents stop missing high-risk live classes during doc-driven implementation.
- Middle: route-card owners can assign proof work from exact source anchors.
- High: parallel agents can update domain docs without colliding because each wave has disjoint target docs.
- Ultra: runtime proof still requires Unity/profiler/Frame Debugger/GC/player artifacts; this ledger only improves source-to-doc traceability.
