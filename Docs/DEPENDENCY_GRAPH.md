# HECTON-8 Architecture Atlas - Dependency Graph

Generated: 2026-05-21 04:34:51
Date: 2026-05-21
Status: ATLAS GENERATED STATIC SOURCE / ATLASCHECK SEPARATE GATE REQUIRED / RUNTIME PENDING
Evidence class: STATIC_SOURCE / STATIC_DOC / FILESYSTEM / PY_TOOL. No Unity Editor, Play Mode, Memory Profiler, Frame Debugger, or player build evidence is claimed here.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/_Archive/Reports_X_012_2026-05-23/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction; R48 remains the prior date-rollover/AtlasCheck/source-counter correction; R47 remains the prior authority-spine/runtime-wording/counter-drift correction; R46 remains the prior interior-authority/route-field/proof-language correction; R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. AtlasCheck remains red and runtime proof is absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Source Of Authority
- `AGENTS.md`
- `Docs/Tasks/CURRENT_BATCH.md`
- `Docs/Actual Domains of Project.txt`
- `Docs/ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md`
- `Docs/Reports/VRAM_Budget_Audit.json`
- `Docs/Reports/VRAM_Budget_Audit_Summary.md`
- `Docs/Reports/VRAM_Remediation_Plan.md`
- `Docs/DEPENDENCY_GRAPH.json`
- `Docs/DEPENDENCY_GRAPH.cache.json`
- `Tools/BuildArchitectureAtlas.py`
- `Tools/AtlasCheck.py`

## Loaded Mandates
- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

## Repository Scale

- C# source files scanned under `Assets/` and `Packages/`: 5372
- C# line count scanned under `Assets/` and `Packages/`: 2,139,957
- First-party C# source files under `Assets/_Project/Scripts/`: 2106
- First-party C# line count under `Assets/_Project/Scripts/`: 1,476,805
- Assembly definitions scanned: 215
- First-party assembly definitions under `Assets/_Project/`: 155
- Markdown docs under `Docs/`: 3103

## Assembly Dependency Graph

Core assembly: `Assets/_Project/Scripts/Hecton8.Core.asmdef`

`Hecton8.Core` direct references currently recorded in its asmdef:
- `Hecton8.Core.Contracts`
- `Hecton8.Core.Database`
- `Hecton8.Core.Scheduling`
- `Hecton8.Core.Bucketing`
- `Hecton8.Core.Persistence.Paging`
- `Hecton8.Core.Memory`
- `Hecton8.World.Contracts`
- `Hecton8.Audio.Virtualization.Contracts`
- `Hecton8.Input`
- `Unity.InputSystem`
- `Unity.Mathematics`
- `Unity.Burst`
- `Unity.Collections`
- `Unity.Jobs`
- `Unity.Addressables`
- `Unity.ResourceManager`
- `Unity.Profiling.Core`
- `Unity.TextMeshPro`
- `UnityEngine.UI`
- `Unity.RenderPipelines.Core.Runtime`
- `Unity.RenderPipelines.Universal.Runtime`
- `GPUInstancer`

Core contracts assembly: `Assets/_Project/Scripts/Core/Contracts/Hecton8.Core.Contracts.asmdef` references `Unity.Collections`, `Unity.Mathematics`.

Assemblies directly depending on exact `Hecton8.Core`: 70

| Assembly | Path |
|---|---|
| `Hecton8.AI.Ambient` | `Assets/_Project/Scripts/AI/Ambient/Hecton8.AI.Ambient.asmdef` |
| `Hecton8.AI.Cognition.Editor` | `Assets/_Project/Scripts/AI/Cognition/Editor/Hecton8.AI.Cognition.Editor.asmdef` |
| `Hecton8.AI.Pathfinding` | `Assets/_Project/Scripts/AI/Pathfinding/Hecton8.AI.Pathfinding.asmdef` |
| `Hecton8.Animation.FaunaProcedural` | `Assets/_Project/Scripts/Animation/FaunaProcedural/Hecton8.Animation.FaunaProcedural.asmdef` |
| `Hecton8.Animation.FaunaProcedural.Editor` | `Assets/_Project/Scripts/Animation/FaunaProcedural/Editor/Hecton8.Animation.FaunaProcedural.Editor.asmdef` |
| `Hecton8.Atmosphere.StormPropagation.Editor` | `Assets/_Project/Scripts/Atmosphere/StormPropagation/Editor/Hecton8.Atmosphere.StormPropagation.Editor.asmdef` |
| `Hecton8.Atmosphere.StormPropagation.Runtime` | `Assets/_Project/Scripts/Atmosphere/StormPropagation/Hecton8.Atmosphere.StormPropagation.Runtime.asmdef` |
| `Hecton8.Audio.Prologue` | `Assets/_Project/Scripts/Audio/Prologue/Hecton8.Audio.Prologue.asmdef` |
| `Hecton8.Audio.Synthesis` | `Assets/_Project/Scripts/Audio/Synthesis/Hecton8.Audio.Synthesis.asmdef` |
| `Hecton8.Audio.Synthesis.Editor` | `Assets/_Project/Scripts/Audio/Synthesis/Editor/Hecton8.Audio.Synthesis.Editor.asmdef` |
| `Hecton8.Cartography.Editor` | `Assets/_Project/Scripts/Cartography/Editor/Hecton8.Cartography.Editor.asmdef` |
| `Hecton8.Core.Bridge.Editor` | `Assets/_Project/Scripts/Core/Bridge/Editor/Hecton8.Core.Bridge.Editor.asmdef` |
| `Hecton8.Core.Content.Editor` | `Assets/_Project/Scripts/Core/Content/Editor/Hecton8.Core.Content.Editor.asmdef` |
| `Hecton8.Core.Hardware` | `Assets/_Project/Scripts/Core/Hardware/Hecton8.Core.Hardware.asmdef` |
| `Hecton8.DataMonolith.Editor` | `Assets/_Project/Scripts/Editor/DataMonolith/Hecton8.DataMonolith.Editor.asmdef` |
| `Hecton8.Dev.SpaceEngine098` | `Assets/_Project/Scripts/Dev/SpaceEngine098/Hecton8.Dev.SpaceEngine098.asmdef` |
| `Hecton8.EditModeTests` | `Assets/_Project/Tests/Editor/Hecton8.EditModeTests.asmdef` |
| `Hecton8.Editor` | `Assets/_Project/Scripts/Editor/Hecton8.Editor.asmdef` |
| `Hecton8.Gameplay.Loot` | `Assets/_Project/Scripts/Gameplay/Loot/Hecton8.Gameplay.Loot.asmdef` |
| `Hecton8.Gameplay.Loot.Contracts` | `Assets/_Project/Scripts/Gameplay/Loot/Contracts/Hecton8.Gameplay.Loot.Contracts.asmdef` |
| `Hecton8.Gameplay.Mining` | `Assets/_Project/Scripts/Gameplay/Mining/Hecton8.Gameplay.Mining.asmdef` |
| `Hecton8.Graphics.Caustics` | `Assets/_Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef` |
| `Hecton8.Graphics.Culling` | `Assets/_Project/Scripts/Graphics/Culling/Hecton8.Graphics.Culling.asmdef` |
| `Hecton8.Graphics.Materials` | `Assets/_Project/Scripts/Graphics/Materials/Hecton8.Graphics.Materials.asmdef` |
| `Hecton8.Graphics.Scalability` | `Assets/_Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef` |
| `Hecton8.Habitat.Deformation` | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/Hecton8.Habitat.Deformation.asmdef` |
| `Hecton8.Inventory.Routing.Runtime` | `Assets/_Project/Scripts/Inventory/Routing/Hecton8.Inventory.Routing.Runtime.asmdef` |
| `Hecton8.InventoryRouting.Editor` | `Assets/_Project/Scripts/Editor/InventoryRouting/Hecton8.InventoryRouting.Editor.asmdef` |
| `Hecton8.Lighting` | `Assets/_Project/Scripts/Lighting/Hecton8.Lighting.asmdef` |
| `Hecton8.Lighting.Editor` | `Assets/_Project/Scripts/Lighting/Editor/Hecton8.Lighting.Editor.asmdef` |
| `Hecton8.Narrative.Campaign` | `Assets/_Project/Scripts/Narrative/Campaign/Hecton8.Narrative.Campaign.asmdef` |
| `Hecton8.Optimization.Editor` | `Assets/_Project/Scripts/Optimization/Editor/Hecton8.Optimization.Editor.asmdef` |
| `Hecton8.Physiology` | `Assets/_Project/Scripts/Physiology/Hecton8.Physiology.asmdef` |
| `Hecton8.Physiology.Editor` | `Assets/_Project/Scripts/Physiology/Editor/Hecton8.Physiology.Editor.asmdef` |
| `Hecton8.PlayModeTests` | `Assets/_Project/Tests/PlayMode/Hecton8.PlayModeTests.asmdef` |
| `Hecton8.Plugins` | `Assets/_Project/Scripts/Plugins/Hecton8.Plugins.asmdef` |
| `Hecton8.Power.Generators` | `Assets/_Project/Scripts/Power/Generators/Hecton8.Power.Generators.asmdef` |
| `Hecton8.Project.Editor` | `Assets/_Project/Editor/Hecton8.Project.Editor.asmdef` |
| `Hecton8.Prologue.Space` | `Assets/_Project/Scripts/Prologue/Space/Hecton8.Prologue.Space.asmdef` |
| `Hecton8.Prologue.VFX` | `Assets/_Project/Scripts/Prologue/VFX/Hecton8.Prologue.VFX.asmdef` |
| `Hecton8.QA` | `Assets/_Project/Scripts/QA/Hecton8.QA.asmdef` |
| `Hecton8.QA.Editor` | `Assets/_Project/Scripts/QA/Editor/Hecton8.QA.Editor.asmdef` |
| `Hecton8.QA.Headless` | `Assets/_Project/Scripts/QA/Headless/Hecton8.QA.Headless.asmdef` |
| `Hecton8.QA.Headless.Editor` | `Assets/_Project/Scripts/QA/Headless/Editor/Hecton8.QA.Headless.Editor.asmdef` |
| `Hecton8.Rendering.BilateralDrs` | `Assets/_Project/Scripts/Rendering/BilateralDrs/Hecton8.Rendering.BilateralDrs.asmdef` |
| `Hecton8.SeedShipAnomaly.Editor` | `Assets/_Project/Scripts/World/SeedShipAnomaly/Editor/Hecton8.SeedShipAnomaly.Editor.asmdef` |
| `Hecton8.SeedShipAnomaly.Runtime` | `Assets/_Project/Scripts/World/SeedShipAnomaly/Hecton8.SeedShipAnomaly.Runtime.asmdef` |
| `Hecton8.Thermodynamics` | `Assets/_Project/Scripts/Thermodynamics/Hecton8.Thermodynamics.asmdef` |
| `Hecton8.Tools.ToolKinematics` | `Assets/_Project/Scripts/Tools/ToolKinematics/Hecton8.Tools.ToolKinematics.asmdef` |
| `Hecton8.Tools.ToolKinematics.Editor` | `Assets/_Project/Scripts/Tools/ToolKinematics/Editor/Hecton8.Tools.ToolKinematics.Editor.asmdef` |
| `Hecton8.UI.Diegetic` | `Assets/_Project/Scripts/UI/Diegetic/Hecton8.UI.Diegetic.asmdef` |
| `Hecton8.UI.Editor` | `Assets/_Project/Scripts/UI/Editor/Hecton8.UI.Editor.asmdef` |
| `Hecton8.UI.Navigation` | `Assets/_Project/Scripts/UI/Navigation/Hecton8.UI.Navigation.asmdef` |
| `Hecton8.UI.Tools` | `Assets/_Project/Scripts/UI/Tools/Hecton8.UI.Tools.asmdef` |
| `Hecton8.UI.VR` | `Assets/_Project/Scripts/UI/VR/Hecton8.UI.VR.asmdef` |
| `Hecton8.VFX.Bioluminescence.Runtime` | `Assets/_Project/Scripts/VFX/Bioluminescence/Hecton8.VFX.Bioluminescence.Runtime.asmdef` |
| `Hecton8.VFX.Debris` | `Assets/_Project/Scripts/VFX/Debris/Hecton8.VFX.Debris.asmdef` |
| `Hecton8.VFX.Materials` | `Assets/_Project/Scripts/VFX/Materials/Hecton8.VFX.Materials.asmdef` |
| `Hecton8.VFX.PlasmaBeam.Runtime` | `Assets/_Project/Scripts/VFX/PlasmaBeam/Hecton8.VFX.PlasmaBeam.Runtime.asmdef` |
| `Hecton8.Vehicles.VFX` | `Assets/_Project/Scripts/Vehicles/VFX/Hecton8.Vehicles.VFX.asmdef` |
| `Hecton8.World.Economy` | `Assets/_Project/Scripts/World/Resources/Hecton8.World.Economy.asmdef` |
| `Hecton8.World.Economy.Editor` | `Assets/_Project/Scripts/World/Resources/Editor/Hecton8.World.Economy.Editor.asmdef` |
| `Hecton8.World.HydraulicErosionForge.Editor` | `Assets/_Project/Scripts/Editor/HydraulicErosionForge/Shinobu242/Hecton8.World.HydraulicErosionForge.Editor.asmdef` |
| `Hecton8.World.Outposts` | `Assets/_Project/Scripts/World/Outposts/Hecton8.World.Outposts.asmdef` |
| `Hecton8.World.ProceduralCoral.Editor` | `Assets/_Project/Scripts/World/ProceduralCoral/Editor/Hecton8.World.ProceduralCoral.Editor.asmdef` |
| `Hecton8.World.ProceduralWreckage.Editor` | `Assets/_Project/Scripts/World/ProceduralWreckage/Editor/Hecton8.World.ProceduralWreckage.Editor.asmdef` |
| `Hecton8.World.ShinobuBiomimetic` | `Assets/_Project/Scripts/World/ShinobuBiomimetic/Hecton8.World.ShinobuBiomimetic.asmdef` |
| `Hecton8.World.ShinobuBiomimetic.Editor` | `Assets/_Project/Scripts/World/ShinobuBiomimetic/Editor/Hecton8.World.ShinobuBiomimetic.Editor.asmdef` |
| `Hecton8.World.Streaming` | `Assets/_Project/Scripts/World/Streaming/Hecton8.World.Streaming.asmdef` |
| `Hecton8.World.VoxelSurfaceNets.Editor` | `Assets/_Project/Scripts/World/VoxelSurfaceNets/Editor/Hecton8.World.VoxelSurfaceNets.Editor.asmdef` |

Assemblies depending on any `Hecton8.Core*` assembly: 102

| Assembly | Core-family references | Path |
|---|---|---|
| `Hecton8.AI.Ambient` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/AI/Ambient/Hecton8.AI.Ambient.asmdef` |
| `Hecton8.AI.Cognition` | `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/AI/Cognition/Hecton8.AI.Cognition.asmdef` |
| `Hecton8.AI.Cognition.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/AI/Cognition/Editor/Hecton8.AI.Cognition.Editor.asmdef` |
| `Hecton8.AI.Ecology.Migration` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/AI/Ecology/Migration/Hecton8.AI.Ecology.Migration.asmdef` |
| `Hecton8.AI.Foveated` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/AI/Foveated/Hecton8.AI.Foveated.asmdef` |
| `Hecton8.AI.Pathfinding` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/AI/Pathfinding/Hecton8.AI.Pathfinding.asmdef` |
| `Hecton8.Animation.FaunaProcedural` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Animation/FaunaProcedural/Hecton8.Animation.FaunaProcedural.asmdef` |
| `Hecton8.Animation.FaunaProcedural.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Animation/FaunaProcedural/Editor/Hecton8.Animation.FaunaProcedural.Editor.asmdef` |
| `Hecton8.Animation.IK` | `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Animation/IK/Hecton8.Animation.IK.asmdef` |
| `Hecton8.Atmosphere.StormPropagation.Editor` | `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Atmosphere/StormPropagation/Editor/Hecton8.Atmosphere.StormPropagation.Editor.asmdef` |
| `Hecton8.Atmosphere.StormPropagation.Runtime` | `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Atmosphere/StormPropagation/Hecton8.Atmosphere.StormPropagation.Runtime.asmdef` |
| `Hecton8.Audio.Echolocation` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Audio/Echolocation/Hecton8.Audio.Echolocation.asmdef` |
| `Hecton8.Audio.Prologue` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Audio/Prologue/Hecton8.Audio.Prologue.asmdef` |
| `Hecton8.Audio.Propagation` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Audio/Propagation/Hecton8.Audio.Propagation.asmdef` |
| `Hecton8.Audio.Synthesis` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Audio/Synthesis/Hecton8.Audio.Synthesis.asmdef` |
| `Hecton8.Audio.Synthesis.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Audio/Synthesis/Editor/Hecton8.Audio.Synthesis.Editor.asmdef` |
| `Hecton8.Audio.Virtualization` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Audio/Virtualization/Hecton8.Audio.Virtualization.asmdef` |
| `Hecton8.Audio.Virtualization.Contracts` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Audio/Virtualization/Contracts/Hecton8.Audio.Virtualization.Contracts.asmdef` |
| `Hecton8.Cartography` | `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Cartography/Hecton8.Cartography.asmdef` |
| `Hecton8.Cartography.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/Cartography/Editor/Hecton8.Cartography.Editor.asmdef` |
| `Hecton8.Core.Bridge.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Core/Bridge/Editor/Hecton8.Core.Bridge.Editor.asmdef` |
| `Hecton8.Core.Bucketing` | `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Core/Bucketing/Hecton8.Core.Bucketing.asmdef` |
| `Hecton8.Core.Content.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/Core/Content/Editor/Hecton8.Core.Content.Editor.asmdef` |
| `Hecton8.Core.Database` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Core/Database/Hecton8.Core.Database.asmdef` |
| `Hecton8.Core.Hardware` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Core/Hardware/Hecton8.Core.Hardware.asmdef` |
| `Hecton8.Core.Memory` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Core/Memory/Hecton8.Core.Memory.asmdef` |
| `Hecton8.Core.Memory.Defrag` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Core/Memory/Defrag/Hecton8.Core.Memory.Defrag.asmdef` |
| `Hecton8.Core.Persistence` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Core/Persistence/Hecton8.Core.Persistence.asmdef` |
| `Hecton8.Core.Persistence.Paging` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Core/Persistence/Paging/Hecton8.Core.Persistence.Paging.asmdef` |
| `Hecton8.Core.Scheduling` | `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Core/Scheduling/Hecton8.Core.Scheduling.asmdef` |
| `Hecton8.DataMonolith.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/Editor/DataMonolith/Hecton8.DataMonolith.Editor.asmdef` |
| `Hecton8.Dev.SpaceEngine098` | `Hecton8.Core` | `Assets/_Project/Scripts/Dev/SpaceEngine098/Hecton8.Dev.SpaceEngine098.asmdef` |
| `Hecton8.EditModeTests` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Tests/Editor/Hecton8.EditModeTests.asmdef` |
| `Hecton8.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Editor/Hecton8.Editor.asmdef` |
| `Hecton8.Gameplay.Loot` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Gameplay/Loot/Hecton8.Gameplay.Loot.asmdef` |
| `Hecton8.Gameplay.Loot.Contracts` | `Hecton8.Core.Contracts`, `Hecton8.Core` | `Assets/_Project/Scripts/Gameplay/Loot/Contracts/Hecton8.Gameplay.Loot.Contracts.asmdef` |
| `Hecton8.Gameplay.Mining` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Gameplay/Mining/Hecton8.Gameplay.Mining.asmdef` |
| `Hecton8.Graphics.Caustics` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef` |
| `Hecton8.Graphics.Culling` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Graphics/Culling/Hecton8.Graphics.Culling.asmdef` |
| `Hecton8.Graphics.Materials` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Graphics/Materials/Hecton8.Graphics.Materials.asmdef` |
| `Hecton8.Graphics.Scalability` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef` |
| `Hecton8.Habitat.Deformation` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/Hecton8.Habitat.Deformation.asmdef` |
| `Hecton8.Input.Determinism` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Input/Determinism/Hecton8.Input.Determinism.asmdef` |
| `Hecton8.Input.Universal` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Input/Universal/Hecton8.Input.Universal.asmdef` |
| `Hecton8.Inventory.Algorithms` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Inventory/Algorithms/Hecton8.Inventory.Algorithms.asmdef` |
| `Hecton8.Inventory.Routing.Runtime` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Inventory/Routing/Hecton8.Inventory.Routing.Runtime.asmdef` |
| `Hecton8.InventoryRouting.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Editor/InventoryRouting/Hecton8.InventoryRouting.Editor.asmdef` |
| `Hecton8.Lighting` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Lighting/Hecton8.Lighting.asmdef` |
| `Hecton8.Lighting.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Lighting/Editor/Hecton8.Lighting.Editor.asmdef` |
| `Hecton8.Logistics.Grid.Contracts` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Logistics/Grid/Contracts/Hecton8.Logistics.Grid.Contracts.asmdef` |
| `Hecton8.Narrative.Camera` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Narrative/Camera/Hecton8.Narrative.Camera.asmdef` |
| `Hecton8.Narrative.Campaign` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Narrative/Campaign/Hecton8.Narrative.Campaign.asmdef` |
| `Hecton8.Narrative.Prologue` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Narrative/Prologue/Hecton8.Narrative.Prologue.asmdef` |
| `Hecton8.Optimization.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/Optimization/Editor/Hecton8.Optimization.Editor.asmdef` |
| `Hecton8.Physics.CCD` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Physics/CCD/Hecton8.Physics.CCD.asmdef` |
| `Hecton8.Physics.Determinism` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Physics/Determinism/Hecton8.Physics.Determinism.asmdef` |
| `Hecton8.Physiology` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Physiology/Hecton8.Physiology.asmdef` |
| `Hecton8.Physiology.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Physiology/Editor/Hecton8.Physiology.Editor.asmdef` |
| `Hecton8.PlayModeTests` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Tests/PlayMode/Hecton8.PlayModeTests.asmdef` |
| `Hecton8.Plugins` | `Hecton8.Core` | `Assets/_Project/Scripts/Plugins/Hecton8.Plugins.asmdef` |
| `Hecton8.Power.Generators` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Power/Generators/Hecton8.Power.Generators.asmdef` |
| `Hecton8.Project.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Editor/Hecton8.Project.Editor.asmdef` |
| `Hecton8.Prologue.Space` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Prologue/Space/Hecton8.Prologue.Space.asmdef` |
| `Hecton8.Prologue.VFX` | `Hecton8.Core.Contracts`, `Hecton8.Core` | `Assets/_Project/Scripts/Prologue/VFX/Hecton8.Prologue.VFX.asmdef` |
| `Hecton8.QA` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/QA/Hecton8.QA.asmdef` |
| `Hecton8.QA.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/QA/Editor/Hecton8.QA.Editor.asmdef` |
| `Hecton8.QA.Headless` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/QA/Headless/Hecton8.QA.Headless.asmdef` |
| `Hecton8.QA.Headless.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/QA/Headless/Editor/Hecton8.QA.Headless.Editor.asmdef` |
| `Hecton8.Rendering.BilateralDrs` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Rendering/BilateralDrs/Hecton8.Rendering.BilateralDrs.asmdef` |
| `Hecton8.SeedShipAnomaly.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/SeedShipAnomaly/Editor/Hecton8.SeedShipAnomaly.Editor.asmdef` |
| `Hecton8.SeedShipAnomaly.Runtime` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/SeedShipAnomaly/Hecton8.SeedShipAnomaly.Runtime.asmdef` |
| `Hecton8.Thermodynamics` | `Hecton8.Core`, `Hecton8.Core.Memory`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Thermodynamics/Hecton8.Thermodynamics.asmdef` |
| `Hecton8.Tools.ToolKinematics` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Tools/ToolKinematics/Hecton8.Tools.ToolKinematics.asmdef` |
| `Hecton8.Tools.ToolKinematics.Contracts` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/Hecton8.Tools.ToolKinematics.Contracts.asmdef` |
| `Hecton8.Tools.ToolKinematics.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Tools/ToolKinematics/Editor/Hecton8.Tools.ToolKinematics.Editor.asmdef` |
| `Hecton8.UI.Diegetic` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/UI/Diegetic/Hecton8.UI.Diegetic.asmdef` |
| `Hecton8.UI.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/UI/Editor/Hecton8.UI.Editor.asmdef` |
| `Hecton8.UI.Localization` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/UI/Localization/Hecton8.UI.Localization.asmdef` |
| `Hecton8.UI.Navigation` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/UI/Navigation/Hecton8.UI.Navigation.asmdef` |
| `Hecton8.UI.Tools` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/UI/Tools/Hecton8.UI.Tools.asmdef` |
| `Hecton8.UI.VR` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/UI/VR/Hecton8.UI.VR.asmdef` |
| `Hecton8.VFX.Bioluminescence.Runtime` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/VFX/Bioluminescence/Hecton8.VFX.Bioluminescence.Runtime.asmdef` |
| `Hecton8.VFX.Debris` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/VFX/Debris/Hecton8.VFX.Debris.asmdef` |
| `Hecton8.VFX.Materials` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/VFX/Materials/Hecton8.VFX.Materials.asmdef` |
| `Hecton8.VFX.PlasmaBeam.Runtime` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/VFX/PlasmaBeam/Hecton8.VFX.PlasmaBeam.Runtime.asmdef` |
| `Hecton8.VFX.Sonar` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/VFX/Sonar/Hecton8.VFX.Sonar.asmdef` |
| `Hecton8.Vehicles.Physics.Contracts` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Vehicles/Physics/Contracts/Hecton8.Vehicles.Physics.Contracts.asmdef` |
| `Hecton8.Vehicles.VFX` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Vehicles/VFX/Hecton8.Vehicles.VFX.asmdef` |
| `Hecton8.World.Economy` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/Resources/Hecton8.World.Economy.asmdef` |
| `Hecton8.World.Economy.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/Resources/Editor/Hecton8.World.Economy.Editor.asmdef` |
| `Hecton8.World.HydraulicErosionForge.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/Editor/HydraulicErosionForge/Shinobu242/Hecton8.World.HydraulicErosionForge.Editor.asmdef` |
| `Hecton8.World.OfflineWreckageBaker.Editor` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/Hecton8.World.OfflineWreckageBaker.Editor.asmdef` |
| `Hecton8.World.Outposts` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/World/Outposts/Hecton8.World.Outposts.asmdef` |
| `Hecton8.World.ProceduralCoral` | `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/ProceduralCoral/Hecton8.World.ProceduralCoral.asmdef` |
| `Hecton8.World.ProceduralCoral.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/ProceduralCoral/Editor/Hecton8.World.ProceduralCoral.Editor.asmdef` |
| `Hecton8.World.ProceduralWreckage` | `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/ProceduralWreckage/Hecton8.World.ProceduralWreckage.asmdef` |
| `Hecton8.World.ProceduralWreckage.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/ProceduralWreckage/Editor/Hecton8.World.ProceduralWreckage.Editor.asmdef` |
| `Hecton8.World.ShinobuBiomimetic` | `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/ShinobuBiomimetic/Hecton8.World.ShinobuBiomimetic.asmdef` |
| `Hecton8.World.ShinobuBiomimetic.Editor` | `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/ShinobuBiomimetic/Editor/Hecton8.World.ShinobuBiomimetic.Editor.asmdef` |
| `Hecton8.World.Streaming` | `Hecton8.Core` | `Assets/_Project/Scripts/World/Streaming/Hecton8.World.Streaming.asmdef` |
| `Hecton8.World.VoxelSurfaceNets` | `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/VoxelSurfaceNets/Hecton8.World.VoxelSurfaceNets.asmdef` |
| `Hecton8.World.VoxelSurfaceNets.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/VoxelSurfaceNets/Editor/Hecton8.World.VoxelSurfaceNets.Editor.asmdef` |

### Domain Namespace Edges

Static `using Hecton8.*` edges from first-party source. This exposes compile-time namespace pressure, not runtime coupling proof.

| From domain | To domain | Using count |
|---|---|---:|
| `RootScripts` | `Core` | 268 |
| `World` | `Core` | 182 |
| `Gameplay` | `Core` | 159 |
| `UI` | `Core` | 139 |
| `Editor` | `Core` | 95 |
| `RootScripts` | `World` | 87 |
| `Physics` | `Core` | 77 |
| `Editor` | `World` | 70 |
| `RootScripts` | `Gameplay` | 61 |
| `Visor` | `Core` | 56 |
| `Construction` | `Core` | 54 |
| `Gameplay` | `World` | 46 |
| `Atmosphere` | `Core` | 39 |
| `RootScripts` | `Items` | 38 |
| `AI` | `Core` | 34 |
| `Audio` | `Core` | 34 |
| `RootScripts` | `Bootstrap` | 33 |
| `RootScripts` | `Environment` | 32 |
| `RootScripts` | `Physics` | 32 |
| `Tools` | `Core` | 32 |
| `Fauna` | `Core` | 31 |
| `Physiology` | `Core` | 31 |
| `RootScripts` | `Inventory` | 29 |
| `Power` | `Core` | 29 |
| `UI` | `Gameplay` | 29 |
| `UI` | `World` | 29 |
| `World` | `Environment` | 29 |
| `World` | `Gameplay` | 29 |
| `Gameplay` | `Physics` | 27 |
| `VFX` | `Core` | 27 |
| `SaveSystem` | `Core` | 26 |
| `RootScripts` | `SaveSystem` | 25 |
| `Graphics` | `Core` | 25 |
| `RootScripts` | `Interaction` | 22 |
| `RootScripts` | `Building` | 21 |
| `Construction` | `Gameplay` | 21 |
| `RootScripts` | `UI` | 20 |
| `RootScripts` | `Tools` | 20 |
| `RootScripts` | `Construction` | 19 |
| `RootScripts` | `Caves` | 19 |
| `Gameplay` | `Audio` | 19 |
| `Optimization` | `Core` | 19 |
| `Construction` | `World` | 18 |
| `Editor` | `Gameplay` | 18 |
| `Gameplay` | `Interaction` | 18 |
| `UI` | `Bootstrap` | 18 |
| `Animation` | `Core` | 17 |
| `Interaction` | `Core` | 17 |
| `ModdingAPI` | `Core` | 16 |
| `Plugins` | `Core` | 16 |
| `Quest` | `Core` | 16 |
| `RootScripts` | `Audio` | 15 |
| `Construction` | `Power` | 15 |
| `Lighting` | `Core` | 15 |
| `Rendering` | `Core` | 15 |
| `RootScripts` | `Atmosphere` | 14 |
| `RootScripts` | `AI` | 14 |
| `Bootstrap` | `Core` | 13 |
| `Ecosystem` | `Core` | 13 |
| `Editor` | `Physics` | 13 |
| `Editor` | `Items` | 13 |
| `Editor` | `Construction` | 13 |
| `Fauna` | `World` | 13 |
| `Physics` | `World` | 13 |
| `RootScripts` | `Input` | 12 |
| `Gameplay` | `UI` | 12 |
| `World` | `Caves` | 12 |
| `Construction` | `Items` | 11 |
| `Gameplay` | `Inventory` | 11 |
| `QA` | `Core` | 11 |
| `UI` | `Audio` | 11 |
| `World` | `AI` | 11 |
| `Editor` | `Environment` | 10 |
| `Gameplay` | `Items` | 10 |
| `Gameplay` | `Tools` | 10 |
| `UI` | `Input` | 10 |
| `World` | `Bootstrap` | 10 |
| `Construction` | `Building` | 9 |
| `Dev` | `Core` | 9 |
| `Gameplay` | `Bootstrap` | 9 |

## SignalBus<T> Flow Map

`ISignal` structs declared: 282. `SignalBus<T>` lanes observed in producer/consumer calls: 221. Union listed below: 287 signals.
Legacy `GlobalSignals.Publish(...)` call sites found: 248. Many use local variables and are not type-resolved by this static pass; they remain a migration/integrator risk.

| Signal | Declared at | Producers (`SignalBus<T>.Push/Publish`) | Consumers (`GetFrameSnapshot`) |
|---|---|---|---|
| `AcousticPingSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9338` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6764`<br>`Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:2448`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1821`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1454`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1213`<br>`Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs:1125`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:4412`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5599` | `Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:771`<br>`Assets/_Project/Scripts/Audio/Editor/ShinobuAcousticDspSmokeTester.cs:106`<br>`Assets/_Project/Scripts/HectonBoidController.cs:1724`<br>`Assets/_Project/Scripts/SpatialAudioManager.cs:5172`<br>`Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:2186`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:4523`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6287` |
| `AcousticShockwaveSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:4000` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1812` | none found |
| `AcousticZoneChangedEvent` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:650` | `Assets/_Project/Scripts/AcousticZoneController.cs:29`<br>`Assets/_Project/Scripts/AcousticZoneController.cs:100`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:652` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:404`<br>`Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:1444` |
| `AnomalyProximitySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9017` | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:133` | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:823` |
| `AnomalySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9002` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6643` | none found |
| `ApexBrainAcousticEchoTap` | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:194` | none found | none found |
| `ApexPanicSignal` | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:407` | none found | none found |
| `ApexProximitySignal` | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:370` | none found | none found |
| `AtmosphericReentrySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9811` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7045` | `Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:219`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:196`<br>`Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:365` |
| `AudioEvent` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:640` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:634`<br>`Assets/_Project/Scripts/Audio/ProceduralAudioEvents.cs:842`<br>`Assets/_Project/Scripts/PowerGrid.cs:1392` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:249`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:5641` |
| `AupPreShiftSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8613` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6556` | `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2544` |
| `AupShiftSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8623` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6566` | `Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs:1059`<br>`Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1121`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:7566`<br>`Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1383`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:885`<br>`Assets/_Project/Scripts/HectonFluidEngine.cs:3494`<br>`Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:560`<br>`Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:484`<br>`Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:859`<br>`Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:2054`<br>`Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:1657`<br>`Assets/_Project/Scripts/Visor/SpectrumSystem.cs:2948`<br>... +5 more |
| `AuxiliaryFlareLightSignal` | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentContracts.cs:209` | none found | none found |
| `AuxiliarySonarRequestSignal` | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentContracts.cs:223` | none found | none found |
| `AuxiliaryTetherConnectionSignal` | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentContracts.cs:238` | none found | none found |
| `BaseIntegrityEventPayload` | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorTypes.cs:59` | none found | none found |
| `BaseModuleCompromisedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10478` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6530`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1258` | none found |
| `BatteryLevelSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9981` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6972`<br>`Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:631` | none found |
| `BiomeChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8730` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7337` | `Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:728`<br>`Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:864`<br>`Assets/_Project/Scripts/Graphics/Materials/ProceduralFloraBiomeTintBridge.cs:70`<br>`Assets/_Project/Scripts/PlayerInventory.cs:4384`<br>`Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs:1673` |
| `BiomeGradientSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8741` | `Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs:363` | `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:1426`<br>`Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs:786`<br>`Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:1317`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:2120` |
| `BlueprintUnlockedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9641` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6887` | none found |
| `BrownoutSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8872` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6587`<br>`Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1476`<br>`Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:513` | `Assets/_Project/Scripts/SpatialAudioManager.cs:7871` |
| `BubbleSpawnSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8674` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7316`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1531` | none found |
| `BulletTimeVisualSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:196` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7179` | none found |
| `CameraFrustumSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10335` | `Assets/_Project/Scripts/Core/SystemDispatcher.cs:6782` | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1103`<br>`Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1525` |
| `CameraJuiceImpactSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:912` | `Assets/_Project/Scripts/Core/CameraJuiceSignals.cs:44` | none found |
| `CameraPositionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10323` | `Assets/_Project/Scripts/Core/SystemDispatcher.cs:6771` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1709`<br>`Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:644`<br>`Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1090` |
| `CardiacPulseSignal` | `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyData.cs:337` | none found | none found |
| `CavitationAcousticSignal` | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:345` | none found | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1404` |
| `ChunkDehydratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9422` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4696` | none found |
| `CombatDamageSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10351` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6462`<br>`Assets/_Project/Scripts/Core/PlayerRuntimeContext.cs:273`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1870` | `Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:850`<br>`Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:944`<br>`Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1077`<br>`Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:1115`<br>`Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:697`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1124`<br>`Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageRuntime.cs:498`<br>`Assets/_Project/Scripts/Vehicles/VFX/HullDentShaderController.cs:217`<br>`Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs:1305` |
| `CombatDamageSignalAupShiftTransformer` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10599` | none found | none found |
| `CompassCalibratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9055` | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:110` | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:836` |
| `ComplianceViolationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10124` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7108` | none found |
| `ConstructionPreviewSignal` | `Assets/_Project/Scripts/Construction/ConstructionSignals.cs:14` | none found | `Assets/_Project/Scripts/Construction/HectonBlueprintPreviewBatch.cs:378` |
| `ControlSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8988` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6636` | none found |
| `CoreCombatDamageSignal` | not found as local `ISignal` declaration | none found | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:1270` |
| `CoreHackedSignal` | `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs:40` | none found | `Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:931` |
| `CoreTetherFiredSignal` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/Physics/TetherSignals.cs:66` | none found |
| `CpuStarvationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9323` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6755` | none found |
| `CraftingCompletedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9671` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6907` | `Assets/_Project/Scripts/PDA/PDALogbookManager.cs:619` |
| `CraftingStartedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9656` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6894` | none found |
| `CrashTelemetrySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9094` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6657` | none found |
| `CrushWarningSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9180` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6699` | none found |
| `CullingOverloadSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:84` | `Assets/_Project/Scripts/Core/InstanceCullingServiceRegistryBridge.cs:74` | none found |
| `DataReloadSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9226` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6720` | none found |
| `DataVaultUpdateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:300` | `Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs:107`<br>`Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs:197`<br>`Assets/_Project/Scripts/Core/Bridge/H8InputMappingFacade.cs:161`<br>`Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistryRuntimeBinder.cs:165` | none found |
| `DebrisAvalancheSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3978` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1780` | none found |
| `DebrisDestroyedSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:261` | none found | none found |
| `DebrisSpawnSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8887` | `Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:1290`<br>`Assets/_Project/Scripts/Construction/DroneFleetManager.cs:3345`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:6594`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1127`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1798`<br>`Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1504`<br>`Assets/_Project/Scripts/Gameplay/HarvestableOutcrop.cs:258`<br>`Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs:1435`<br>`Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs:1219`<br>`Assets/_Project/Scripts/Gameplay/VehicleMotor.cs:1795`<br>`Assets/_Project/Scripts/RepairTool.cs:1045`<br>`Assets/_Project/Scripts/ResourceNode.cs:947`<br>... +4 more | `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:871` |
| `DebugSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:393` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeDebugSignal.cs:22` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:634` |
| `DeconstructRequestSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9123` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6671` | none found |
| `DeconstructResultSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9139` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6678` | none found |
| `DeferredSubmarineImpactSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1179` | `Assets/_Project/Scripts/PhysicsApplySystem.cs:2963`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:3021` | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:598`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:2976` |
| `DeflectSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8907` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6601` | none found |
| `DeltaCrusherMockLaserFireSignal` | `Assets/_Project/Scripts/VFX/Debris/ShinobuDeltaCrusherJobs.cs:29` | none found | none found |
| `DesyncDetectedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:574` | `Assets/_Project/Scripts/Core/Signals/CoreDeterminismSignals.cs:76` | none found |
| `DiegeticHudSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9887` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6958` | none found |
| `DirectorAIMusicSignal` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:465` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:466`<br>`Assets/_Project/Scripts/HectonDirectorAI.cs:262` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:408`<br>`Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:1459` |
| `DockingCompleteSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:804` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:2673`<br>`Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:1588` | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1060` |
| `DockingFailedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:825` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:2706`<br>`Assets/_Project/Scripts/Construction/DroneFleetManager.cs:2724`<br>`Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:1621` | none found |
| `DockingRequestSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:783` | none found | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:2329` |
| `DroneFleetInventoryTransactionSignal` | `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs:775` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:2906`<br>`Assets/_Project/Scripts/Construction/DroneFleetManager.cs:3093` | none found |
| `DroneFleetMockMiningSignal` | `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs:764` | none found | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:3653` |
| `DroneFleetMockRepairSignal` | `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs:753` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:3063` | none found |
| `DropPodLandedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8633` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6573` | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:1980` |
| `DynamicMusicScalarSignal` | `Assets/_Project/Scripts/Core/Contracts/Signals/DynamicMusicScalarSignal.cs:9` | none found | `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:926` |
| `EclipseGameplayEventPayload` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3962` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:2104` | none found |
| `EncumbranceSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:224` | none found | none found |
| `EncyclopediaUnlockSignal` | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:2639` | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:975` | none found |
| `EntityDeathSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8929` | `Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs:666`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:6608` | `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs:1604`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:5321` |
| `EntityDepletedSignal` | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:2653` | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1001` | none found |
| `EntitySpawnSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8942` | `Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:392`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:6615` | none found |
| `EquipItemSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:237` | none found | none found |
| `EquipmentOverheatSignal` | `Assets/_Project/Scripts/Tools/EquipmentThermalBatteryContracts.cs:84` | none found | none found |
| `ExosuitAcousticEchoTap` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsContracts.cs:85` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:612` | none found |
| `FabricationCompletedSignal` | `Assets/_Project/Scripts/FabricationAssemblerRuntime.cs:72` | none found | none found |
| `FabricationTickSignal` | `Assets/_Project/Scripts/FabricationAssemblerRuntime.cs:88` | none found | none found |
| `FaunaStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10252` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7260` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1727`<br>`Assets/_Project/Scripts/Fauna/FaunaBrain.cs:4285`<br>`Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1420` |
| `FloraExclusionSignal` | `Assets/_Project/Scripts/Construction/ConstructionSignals.cs:45` | none found | none found |
| `FloraSpawnedSignal` | `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeContracts.cs:641` | none found | none found |
| `FluidDensityChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9543` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6836` | none found |
| `FluidImpulseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8655` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7309`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1555`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:5093`<br>`Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:1656`<br>`Assets/_Project/Scripts/World/FloraInteractionManager.cs:4267` | none found |
| `FluidIncursionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9507` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6820`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1239` | `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:705` |
| `FocusBrokenSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8801` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7351` | none found |
| `FramePacingWarningSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10576` | `Assets/_Project/Scripts/Core/SystemDispatcher.cs:5160` | none found |
| `FrameTimeSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:15` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:851` | `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs:1650`<br>`Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1340`<br>`Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:2169` |
| `GlobalPanicSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:4022` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1761` | none found |
| `GlobalTimeSyncSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:152` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7115` | none found |
| `GlobalWorldStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8709` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7330` | none found |
| `HUDNotificationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9872` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6951`<br>`Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:554`<br>`Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:2432` | none found |
| `HabitatConstructionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9110` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6664` | none found |
| `HabitatFloodAcousticMuffleSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:78` | `Assets/_Project/Scripts/AcousticZoneController.cs:107`<br>`Assets/_Project/Scripts/Physics/HabitatFluidIncursionDirector.cs:720` | none found |
| `HapticPulseSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:236` | none found | none found |
| `HapticRequest` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8333` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6488`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1471`<br>`Assets/_Project/Scripts/LaserCutter.cs:2037`<br>`Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:583`<br>`Assets/_Project/Scripts/Tools/WfcLaserCutRuntime.cs:461` | none found |
| `HashDeltaUpdateSignal` | `Assets/_Project/Scripts/Core/Contracts/MemorySentinelSignals.cs:30` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:401` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1244` |
| `HighSpeedImpactSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8289` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6481` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:243`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:3785`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2605`<br>`Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:609`<br>`Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:1559`<br>`Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs:1272` |
| `HullDeformedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10436` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6517`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1981`<br>`Assets/_Project/Scripts/Vehicles/VFX/HullDentShaderController.cs:724` | `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:953`<br>`Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:1257`<br>`Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:1542` |
| `HullRepairedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10461` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:3182`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:6524`<br>`Assets/_Project/Scripts/RepairTool.cs:1653` | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1169` |
| `HypoxiaSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9450` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6792` | none found |
| `ImpactSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8272` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6474`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1830`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:5057`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:5128` | `Assets/_Project/Scripts/World/SoundscapeSystem.cs:811` |
| `InputSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:508` | `Assets/_Project/Scripts/Core/Signals/CoreDeterminismSignals.cs:64` | none found |
| `InputStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:968` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:629` | `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:742`<br>`Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:598`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1753`<br>`Assets/_Project/Scripts/UI/InteractionUI.cs:908`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5875` |
| `InteractionUiSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9480` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6806` | none found |
| `InventoryChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8494` | `Assets/_Project/Scripts/PlayerInventory.cs:4234` | `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:666`<br>`Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:869`<br>`Assets/_Project/Scripts/HectonPlayerMovement.cs:4808`<br>`Assets/_Project/Scripts/PDAInventoryTab.cs:512`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:547`<br>`Assets/_Project/Scripts/PlayerToolManager.cs:1980`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:2866`<br>`Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs:279`<br>`Assets/_Project/Scripts/UI/PDAConstructionTab.cs:326`<br>`Assets/_Project/Scripts/UI/PDALoadoutTab.cs:288`<br>`Assets/_Project/Scripts/UI/PDAShellChrome.cs:563`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5853` |
| `InventoryCommandSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8478` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:3094`<br>`Assets/_Project/Scripts/PlayerInventory.cs:2090` | `Assets/_Project/Scripts/PlayerInventory.cs:2158` |
| `ItemAcquiredSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8529` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7214`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:7232`<br>`Assets/_Project/Scripts/VoxelDeltaProcessor.cs:3519` | `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs:1615`<br>`Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:628`<br>`Assets/_Project/Scripts/PlayerInventory.cs:4398`<br>`Assets/_Project/Scripts/PlayerInventory.cs:4423`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:5332` |
| `ItemDecaySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10168` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7207` | none found |
| `ItemDurabilityChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8508` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6979`<br>`Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs:959` | `Assets/_Project/Scripts/HUDQuickBar.cs:337`<br>`Assets/_Project/Scripts/PlayerToolManager.cs:2012`<br>`Assets/_Project/Scripts/UI/PDALoadoutTab.cs:307`<br>`Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:293` |
| `KccVelocitySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:632` | `Assets/_Project/Scripts/Core/Signals/CoreDeterminismSignals.cs:104`<br>`Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1515` | `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs:1222`<br>`Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs:837` |
| `KillSwitchSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:30` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:864` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1334` |
| `LaserCutterEventPayload` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1048` | none found | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:247`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:5406` |
| `LaserCutterEventPayloadSignal` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/LaserCutter.cs:379` | `Assets/_Project/Scripts/LaserCutter.cs:194` |
| `LightLevelSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10191` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7246` | none found |
| `LocalizationLanguageChangedSignal` | `Assets/_Project/Scripts/LocRegistry.cs:442` | `Assets/_Project/Scripts/LocRegistry.cs:1678` | none found |
| `LockstepSnapshotSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:986` | `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:994` | none found |
| `LogisticsTransferSignal` | `Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs:126` | none found | none found |
| `LoreFragmentScannedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9627` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6880` | `Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs:766` |
| `MacroCollisionSignal` | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1181` | none found | none found |
| `MacroDatabaseSectorHydrationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10024` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7080` | `Assets/_Project/Scripts/SaveManager.cs:1443`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:3947`<br>`Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:540` |
| `ManualOverridePulledSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9847` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7059` | none found |
| `MechHapticSignalDTO` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsContracts.cs:50` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:572` | none found |
| `MemoryAddressShiftSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9255` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6734` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/VaultMemoryGizmoVisualizer.cs:41`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:2050` |
| `MemoryDesyncSignal` | `Assets/_Project/Scripts/Core/Contracts/MemorySentinelSignals.cs:6` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1443`<br>`Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1472` | none found |
| `MemoryPressureSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9241` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6727` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:703`<br>`Assets/_Project/Scripts/UI/PDAShellChrome.cs:534`<br>`Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4085` |
| `MemorySentinelRollbackSignal` | `Assets/_Project/Scripts/Core/Contracts/MemorySentinelSignals.cs:51` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1458` | none found |
| `MixerStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8817` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7358` | none found |
| `MockAcousticSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:183` | none found | none found |
| `MockAupRebaseSignal` | `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs:16` | none found | none found |
| `MockCarveRequestSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:190` | none found | none found |
| `MockCombatDamageSignal` | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:389` | none found | none found |
| `MockConsumeSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:188` | none found | none found |
| `MockCraftingRequestSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:176` | none found | none found |
| `MockDamageSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:197` | none found | none found |
| `MockFloodSignal` | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:310` | none found | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:588` |
| `MockHotbarSelectSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:249` | none found | none found |
| `MockHudSignal` | `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs:54` | none found | none found |
| `MockImpactSignal` | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:328` | none found | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:613` |
| `MockInventoryTransactionSignal` | `Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:294` | `Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:2422` | none found |
| `MockItemAcquiredSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:164` | none found | none found |
| `MockLaserFireSignal` | `Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:77` | none found | none found |
| `MockNarrativeTriggerSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3927` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1684` | none found |
| `MockPlayerFootstepSignal` | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1110` | none found | none found |
| `MockPlayerPositionSignal` | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:200` | none found | none found |
| `MockPredatorSignal` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:3294` | none found | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1745` |
| `MockQualityWeightSignal` | `Assets/_Project/Scripts/Core/Contracts/DrsContracts.cs:282` | none found | none found |
| `MockReconstructionInputSignal` | `Assets/_Project/Scripts/Core/Contracts/DrsContracts.cs:314` | none found | none found |
| `MockRockCollisionSignal` | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1164` | none found | none found |
| `MockStoryEventSignal` | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:185` | none found | none found |
| `MockTextRequestSignal` | `Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:1734` | none found | none found |
| `MockToolUsedSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:200` | none found | none found |
| `MockTriggerPullSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:181` | none found | none found |
| `ModAssetReferenceSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:169` | none found | none found |
| `ModFutureDevNullSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:211` | none found | none found |
| `ModInteractionRejectedPayload` | `Assets/_Project/Scripts/ModdingAPI/ModSpatialContracts.cs:242` | none found | none found |
| `ModSpawnRequestSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:156` | none found | none found |
| `ModdedGameMaskSignal` | `Assets/_Project/Scripts/Core/Contracts/MemorySentinelSignals.cs:68` | none found | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:654` |
| `ModuleDeconstructSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9152` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6685` | none found |
| `MovementAcousticSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9365` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6771` | `Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:734`<br>`Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:730`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6260` |
| `NarrativeFocusSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8772` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7344` | none found |
| `NarrativeHudWaypointSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8834` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7365` | none found |
| `NarrativePoiStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8858` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7379` | `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1515`<br>`Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:876` |
| `OxygenCriticalSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9465` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6799` | none found |
| `PdaExchangeStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9926` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7014` | `Assets/_Project/Scripts/UI/PDABarterTab.cs:198`<br>`Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:875` |
| `PhysicsEventPayload` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1133` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:245`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:5586`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:915`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:949` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:122`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:244`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:5547`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:755`<br>`Assets/_Project/Scripts/SpatialAudioManager.cs:5152` |
| `PhysiologyStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10265` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7274` | none found |
| `PipeRuptureSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9556` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6843` | none found |
| `PlasmaBeamAcousticEchoTap` | `Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:92` | none found | none found |
| `PlayVoiceOverSignal` | `Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:1747` | none found | none found |
| `PlayerActionCancelledSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8450` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7000` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:141` |
| `PlayerActionCompletedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8434` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6993` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:137` |
| `PlayerActionProgressSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8415` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6986` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:133` |
| `PlayerBaseEnterSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10502` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6537` | `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:765`<br>`Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:812` |
| `PlayerBaseExitSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10517` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6544` | `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:784`<br>`Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:800` |
| `PlayerExhaleSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:94` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10729` | `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:6106`<br>`Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:705`<br>`Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs:342` |
| `PlayerFatalPressureSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:124` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10753` | `Assets/_Project/Scripts/UI/HectonOSBootManager.cs:215`<br>`Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:726` |
| `PlayerFootstepSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:7` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10695` | `Assets/_Project/Scripts/PlayerFootstepAudio.cs:224` |
| `PlayerInputSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1231` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:3194` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:567`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:584`<br>`Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs:2191`<br>`Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs:2212`<br>`Assets/_Project/Scripts/HectonFabricatorUI.cs:628`<br>`Assets/_Project/Scripts/HectonFabricatorUI.cs:654`<br>`Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:314`<br>`Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:331`<br>`Assets/_Project/Scripts/MainMenuController.cs:897`<br>`Assets/_Project/Scripts/MainMenuController.cs:913`<br>`Assets/_Project/Scripts/PlayerBuilder.cs:715`<br>`Assets/_Project/Scripts/PlayerBuilder.cs:732`<br>... +7 more |
| `PlayerLookTargetSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1250` | `Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:611`<br>`Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:633` | `Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:456` |
| `PlayerRespawnSignal` | `Assets/_Project/Scripts/Core/Contracts/Signals/PlayerRespawnSignal.cs:28` | none found | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:1184`<br>`Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs:3181`<br>`Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs:201` |
| `PlayerSprintStateSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:109` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10741` | `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs:1209` |
| `PlayerStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8359` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6497` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1698`<br>`Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:636`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2169`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2628`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2729`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5836` |
| `PlayerStressSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10292` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7288` | `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2646`<br>`Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:322` |
| `PlayerTransportBailoutSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:136` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10769` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1396` |
| `PlayerWaterSplashSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:35` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10718` | `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:999`<br>`Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:713` |
| `PowerDrainSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9747` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6937` | none found |
| `PrefabAcousticSignatureSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:324` | `Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistry.cs:345`<br>`Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistryRuntimeBinder.cs:89` | none found |
| `PrefabLoreLinkSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:348` | `Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistry.cs:356`<br>`Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistryRuntimeBinder.cs:100` | none found |
| `ProgressionEventSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8696` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7323` | none found |
| `PrologueCompleteSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9830` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7052` | `Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:259`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:226`<br>`Assets/_Project/Scripts/HectonFluidEngine.cs:3137`<br>`Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:415` |
| `QuestDagMockItemAcquiredSignal` | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:216` | none found | none found |
| `RadarJamSignal` | `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs:25` | none found | none found |
| `RadiationDoseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8542` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7221`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:7235` | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:676`<br>`Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs:1039`<br>`Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:858` |
| `RadiationSourceSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8583` | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:101`<br>`Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:114` | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:652` |
| `ReactorDamageSignal` | `Assets/_Project/Scripts/Core/Contracts/Signals/ReactorDamageSignal.cs:13` | none found | `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:725` |
| `RebaseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8978` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6629` | none found |
| `ReconDataSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9996` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7066` | none found |
| `ReentryVfxStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:457` | `Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:770` | none found |
| `ResidencySectorDehydratedSignal` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4686` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4116`<br>`Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4740` |
| `ResidencySectorHydratedSignal` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4670` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4745` |
| `ResolutionChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9280` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6741`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:1636` | none found |
| `ResourceDepletionDeltaSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8599` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7228`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:7238`<br>`Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1022` | none found |
| `RespawnSignalResolvedTargetTransformer` | `Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs:1469` | none found | none found |
| `RigidbodySleepSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9584` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6857` | none found |
| `SaveCompletedSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:115` | `Assets/_Project/Scripts/SaveManager.cs:2235` | none found |
| `SaveLifecycleSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10009` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7073` | none found |
| `SaveMetadataReadySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10098` | `Assets/_Project/Scripts/SaveThumbnailSystem.cs:965` | none found |
| `SaveStatusSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:131` | `Assets/_Project/Scripts/SaveManager.cs:2209` | none found |
| `ScalabilityChangedEvent` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:37` | `Assets/_Project/Scripts/Core/IPlatformIntegration.cs:137` | `Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:894`<br>`Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:880`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:131`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:271`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:447`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:477`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:2699`<br>`Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:171`<br>`Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:291`<br>`Assets/_Project/Scripts/Core/IPlatformIntegration.cs:148`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1746`<br>`Assets/_Project/Scripts/SpatialAudioManager.cs:1557`<br>... +1 more |
| `ScanCompleteSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9614` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6873`<br>`Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:996` | `Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs:754` |
| `ScanLogChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9906` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7007` | `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:684`<br>`Assets/_Project/Scripts/PDA/PDALogbookManager.cs:641` |
| `ScannerToolActiveSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9597` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6866`<br>`Assets/_Project/Scripts/ScannerTool.cs:1000` | `Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:472`<br>`Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs:698` |
| `SeaglidePropulsionRequestSignal` | `Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsContracts.cs:119` | none found | `Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsRuntime.cs:478` |
| `SectorDehydratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9407` | `Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:566` | `Assets/_Project/Scripts/World/EcosystemDirector.cs:3931` |
| `SectorResidencyHydratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9392` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:431`<br>`Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:536` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:271`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:3956` |
| `SeismicShockwaveSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3944` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1752` | none found |
| `SeismicSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10139` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7124` | `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs:1541` |
| `ShinobuPlayerExertionSignal` | `Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:19` | `Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1561` | none found |
| `SignalWardenMockDamageSignal` | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1143` | none found | none found |
| `SiltExplosionSignal` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsContracts.cs:74` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:597` | none found |
| `SimulationBucketSyncSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10561` | `Assets/_Project/Scripts/Core/SystemDispatcher.cs:5119` | none found |
| `SimulationPauseSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:181` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7151` | none found |
| `SolarFlareSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8963` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6622` | none found |
| `SonarPingSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9437` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6785` | none found |
| `SoundscapeProfileSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8847` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7372` | none found |
| `SpectrumScanSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9569` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6850` | none found |
| `SplashEvent` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1077` | `Assets/_Project/Scripts/Physics/FluidFeedbackListener.cs:188`<br>`Assets/_Project/Scripts/Physics/FluidFeedbackListener.cs:208` | `Assets/_Project/Scripts/Physics/FluidFeedbackListener.cs:155` |
| `StateChangedSignal` | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:170` | `Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs:1149` | none found |
| `StateCorrectionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:539` | `Assets/_Project/Scripts/Core/Signals/CoreDeterminismSignals.cs:70` | none found |
| `StorageDebtSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9777` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7031` | none found |
| `StreamingTurbulenceSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9798` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7038` | none found |
| `SubmarineFloodStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9520` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6827`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:2711` | `Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:960`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:579` |
| `SubmarineLightsChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10219` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7253` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:657`<br>`Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:3784`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:4395` |
| `SubtitleCueSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:249` | none found | `Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs:555` |
| `SubtitleSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9196` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6713` | `Assets/_Project/Scripts/UI/SubtitleManager.cs:778` |
| `SurvivalOverrideSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:224` | none found | none found |
| `SurvivalVitalsChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8399` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6510` | `Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:857`<br>`Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs:1629`<br>`Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:843`<br>`Assets/_Project/Scripts/UI/SuitAdvisoryController.cs:161`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:4597`<br>`Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:846`<br>`Assets/_Project/Scripts/Visor/VisorHUDController.cs:1456` |
| `SwarmDispersedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9379` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6778`<br>`Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:555`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6342` | none found |
| `SyncFenceSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:599` | `Assets/_Project/Scripts/Core/Signals/CoreDeterminismSignals.cs:85`<br>`Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1051` | none found |
| `SystemGlitchSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1008` | `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1092` | `Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:733` |
| `SystemHealthIndexSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9303` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6748`<br>`Assets/_Project/Scripts/Core/HomeostasisBrain.cs:878`<br>`Assets/_Project/Scripts/QA/Headless/Shinobu38QaWatchdogRuntime.cs:1072` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1767`<br>`Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:839`<br>`Assets/_Project/Scripts/Core/InputDispatcher.cs:3491`<br>`Assets/_Project/Scripts/Core/SystemDispatcher.cs:3196`<br>`Assets/_Project/Scripts/Core/SystemDispatcher.cs:4810`<br>`Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1444`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2699`<br>`Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:747`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:991`<br>`Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:595`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1755`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:556`<br>... +6 more |
| `SystemHealthSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:422` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:824` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1326`<br>`Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:585`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:848`<br>`Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:426`<br>`Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1078`<br>`Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1549`<br>`Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:851`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5893`<br>`Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:1872` |
| `SystemKillSwitchBitsSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:44` | `Assets/_Project/Scripts/Core/GlobalRegistry.cs:844` | `Assets/_Project/Scripts/HectonFluidEngine.cs:6062`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5048` |
| `SystemPauseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10547` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7159`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:7166`<br>`Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs:1088` | none found |
| `TelemetryAnomalySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9079` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6650` | none found |
| `TemperatureChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8555` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6580` | none found |
| `TerminalClickSignal` | `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:156` | none found | none found |
| `TerminalCommandSignal` | `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:167` | none found | none found |
| `TetherFiredSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:752` | none found | none found |
| `TetherSnappedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:715` | `Assets/_Project/Scripts/Physics/TetherSignals.cs:73` | `Assets/_Project/Scripts/Physics/TetherSignals.cs:86` |
| `TetherTensionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:670` | `Assets/_Project/Scripts/Physics/TetherSignals.cs:80` | none found |
| `ThermalSourceSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8572` | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs:3229` | `Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.cs:516` |
| `ThermalStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9964` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6965`<br>`Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:618` | `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:602`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:878`<br>`Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:448`<br>`Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs:1552` |
| `ThermalUpdraftSignal` | `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardTypes.cs:77` | `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:940` | none found |
| `ThermodynamicsMockDamageSignal` | `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardTypes.cs:96` | none found | none found |
| `TimeDilationSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:166` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7138` | none found |
| `ToolAcousticSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9728` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6930`<br>`Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:961`<br>`Assets/_Project/Scripts/LaserCutter.cs:2018`<br>`Assets/_Project/Scripts/Tools/WfcLaserCutRuntime.cs:449`<br>`Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:356` | none found |
| `ToolBrokenSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:212` | none found | none found |
| `ToolDepletedSignal` | `Assets/_Project/Scripts/Tools/EquipmentThermalBatteryContracts.cs:99` | none found | none found |
| `ToolHeatSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:205` | none found | none found |
| `ToolLoadoutChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9705` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6923` | `Assets/_Project/Scripts/Gameplay/PlayerTransportCoordinator.cs:344`<br>`Assets/_Project/Scripts/HUDQuickBar.cs:313`<br>`Assets/_Project/Scripts/PDAInventoryTab.cs:581`<br>`Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs:320`<br>`Assets/_Project/Scripts/UI/PDAConstructionTab.cs:367`<br>`Assets/_Project/Scripts/UI/PDALoadoutTab.cs:342`<br>`Assets/_Project/Scripts/UI/PDAShellChrome.cs:603` |
| `ToolStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9685` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6916` | none found |
| `ToolTriggerSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9762` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6944` | none found |
| `ToxicBioluminescenceSignal` | `Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryTypes.cs:160` | none found | none found |
| `ToxicityExposureSignal` | `Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryTypes.cs:144` | none found | none found |
| `TraumaSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10307` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7295` | none found |
| `UIRescaleRequestSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9493` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6813` | none found |
| `VehicleCommandSignal` | `Assets/_Project/Scripts/Gameplay/VehicleCommandSignals.cs:21` | none found | none found |
| `VehicleHazardSignal` | `Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageContracts.cs:197` | none found | none found |
| `VehicleUpgradesChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9947` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7021` | none found |
| `VfxSparkRequestSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:217` | none found | none found |
| `VisorBreachSignal` | `Assets/_Project/Scripts/Visor/DiegeticVisorLensTypes.cs:110` | none found | none found |
| `VisorDropletSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:486` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10791`<br>`Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:742` | `Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs:372` |
| `VisualFlareSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:886` | `Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:701` | none found |
| `VisualScavengeSignal` | `Assets/_Project/Scripts/Core/Contracts/Signals/VisualScavengeSignal.cs:23` | none found | none found |
| `VitalWarningSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9165` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6692` | none found |
| `VocalWarningSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9211` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6706` | none found |
| `VoxelCarveEvent` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:846` | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs:1222` | `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:832` |
| `WakeGeneratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8646` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7302`<br>`Assets/_Project/Scripts/Gameplay/VehicleMotor.cs:1305`<br>`Assets/_Project/Scripts/World/FloraInteractionManager.cs:2772` | `Assets/_Project/Scripts/World/FloraInteractionManager.cs:2777` |
| `WakeRequestSignal` | `Assets/_Project/Scripts/Core/Signals/PhysicsWakeSignalContracts.cs:12` | `Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs:1133` | none found |
| `WaterTransitionSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:70` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10818` | `Assets/_Project/Scripts/Gameplay/WaterTransitionHandler.cs:60` |
| `WaterlineBreachSignal` | `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereContracts.cs:1483` | `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1465` | `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:962` |
| `WeatherChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10532` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7200` | none found |
| `WeatherStrengthSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10153` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7191` | none found |
| `WfcOutpostDoorPowerSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10075` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7101`<br>`Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:492` | `Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:946` |
| `WfcOutpostGeneratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10038` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7087`<br>`Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:1317` | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:996`<br>`Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:250`<br>`Assets/_Project/Scripts/Tools/WfcLaserCutRuntime.cs:305` |
| `WfcOutpostStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10059` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7094`<br>`Assets/_Project/Scripts/Gameplay/SealedDoor.cs:737` | `Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs:110`<br>`Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1028`<br>`Assets/_Project/Scripts/SaveManager.cs:1266` |

## Queue-Backed Signal Lanes

Queue-backed lanes parsed from `Docs/ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md`: 56.

| Lane Owner | Backing Queue | Listener Contract | Raise Surface | Flush Owner |
|---|---|---|---|---|
| `ModRegistryEvents` | front/back `NativeQueue<ModRegistryEventPayload>` | `IModRegistryEventListener` | `NotifyRuntimeRegistryChanged`, `NotifySettingsRegistryChanged`, `NotifyRecipeRegistryChanged`, `NotifyBuildableRegistryChanged` | `SystemDispatcher.LateUpdate()` |
| `BootstrapEvents` | front/back `NativeQueue<BootstrapEventPayload>` | `IBootstrapEventListener` | `NotifyBootstrapComplete` | `SystemDispatcher.LateUpdate()` |
| `LocalizationEvents` | front/back `NativeQueue<LocalizationEventPayload>` | `ILocalizationLanguageChangedListener`, `ILocalizationCorruptionVisualStateListener` | `PublishLanguageChanged`, `PublishCorruptionVisualStateChanged` | `SystemDispatcher.LateUpdate()` |
| `NarrativeEvents` | front/back `NativeQueue<NarrativeEventPayload>` | `INarrativeEventListener` | `RaiseNarrativePOIRegistered`, `RaiseNarrativePOIDisposed`, `RaiseDiscoveryMade`, `RaiseDepthTierReached` | `SystemDispatcher.LateUpdate()` |
| `PhysicsEvents` | `GlobalPhysicsStateManager` owned `NativeQueue<PhysicsImpactEventData>` | `IPhysicsImpactEventListener` | internal `RaiseImpact` after native impact flush | `GlobalPhysicsStateManager.LateFrameTick()` during `ILateFrameTickable` pass |
| `PhysicsEventBus` | front/back `NativeQueue<PhysicsEventPayload>` | `IPressureImpulseEventListener`, `IElectromagneticPulseEventListener`, `IAcousticPingEventListener` | `NotifyPressureImpulse`, `NotifyElectromagneticPulse`, `NotifyAcousticPing` | `SystemDispatcher.LateUpdate()` |
| `InteractionEvents` | front/back `NativeQueue<InteractionEventPayload>` | `IInteractionEventListener` | `RaiseItemCollected`, `RaiseInteractionStarted`, `RaiseHoverChanged` | `SystemDispatcher.LateUpdate()` |
| `CraftingEvents` | front/back `NativeQueue<CraftingEventPayload>` | `ICraftingEventListener` | `RaiseCraftStarted`, `RaiseCraftCompleted`, `RaiseCraftCancelled` | `SystemDispatcher.LateUpdate()` |
| `ScanEvents` | front/back `NativeQueue<ScanEventPayload>` | `IScanEventListener` | `RaiseScanTriggered`, `RaiseNodeFound`, `RaiseEntryDiscovered` | `SystemDispatcher.LateUpdate()` |
| `SaveEvents` | front/back `NativeQueue<SaveEventPayload>` | `ISaveEventListener` | `RaiseSaveStarted`, `RaiseSaveCompleted`, `RaiseSaveFailed`, `RaiseLoadStarted`, `RaiseLoadCompleted`, `RaiseLoadFailed`, `RaiseEmergencyBackupRestoreRequested` | `SystemDispatcher.LateUpdate()` |
| `QuestEvents` | front/back `NativeQueue<QuestEventPayload>` | `IQuestEventListener` | `RaiseActivated`, `RaiseCompleted`, `RaiseFailed`, `RaiseRevertRequested` | `SystemDispatcher.LateUpdate()` |
| `FirstHourEvents` | front/back `NativeQueue<FirstHourEventPayload>` | `IFirstHourEventListener` | `RaiseMilestone` | `SystemDispatcher.LateUpdate()` |
| `EndingEvents` | front/back `NativeQueue<EndingEventPayload>` | `IEndingEventListener` | `RaiseConditionMet`, `RaiseChosen`, `RaiseSequenceComplete` | `SystemDispatcher.LateUpdate()` |
| `AudioLogEvents` | front/back `NativeQueue<AudioLogEventPayload>` | `IAudioLogEventListener` | `RaiseLogDiscovered`, `RaisePlaybackStarted`, `RaisePlaybackStopped`, `RaisePlaybackCompleted` | `SystemDispatcher.LateUpdate()` |
| `AtmosphereEvents` | front/back `NativeQueue<EnvironmentState>` | `IAtmosphereStateEventListener` | `RaiseStateChanged` | `SystemDispatcher.LateUpdate()` |
| `CelestialEvents` | front/back `NativeQueue<CelestialEventPayload>` | `ICelestialEventListener` | `RaiseEclipseStarted`, `RaiseEclipseEnded`, `RaiseSunAngleChanged`, `RaisePlanetPhaseChanged` | `SystemDispatcher.LateUpdate()` |
| `EclipseGameplayEvents` | front/back `NativeQueue<EclipseGameplayEventPayload>` | `IEclipseGameplayEventListener` | `RaisePhaseChanged`, `RaiseNightPredatorsRising`, `RaiseTemperatureDelta` | `SystemDispatcher.LateUpdate()` |
| `AcousticZoneEvents` | front/back `NativeQueue<AcousticZoneChangedEvent>` | `IAcousticZoneEventListener` | `Raise` | `SystemDispatcher.LateUpdate()` |
| `HighPressureEvents` | front/back `NativeQueue<HighPressureEventPayload>` | `IHighPressureEventListener` | `Notify` | `SystemDispatcher.LateUpdate()` |
| `FatalPressureImplosionEvents` | front/back `NativeQueue<FatalPressureImplosionEventPayload>` | `IFatalPressureImplosionEventListener` | `Notify` | `SystemDispatcher.LateUpdate()` |
| `FluidFeedbackEvents` | front/back `NativeQueue<SplashEvent>` | `IFluidSplashEventListener` | `PublishSplashQueued` | `SystemDispatcher.LateUpdate()` |
| `RepairDroneTorchAcousticEvents` | front/back `NativeQueue<RepairDroneTorchAcousticPayload>` | `IRepairDroneTorchAcousticListener` | `Notify` | `SystemDispatcher.LateUpdate()` |
| `ElectrolysisAcousticEvents` | front/back `NativeQueue<ElectrolysisAcousticPayload>` | `IElectrolysisAcousticEventListener` | `Notify` | `SystemDispatcher.LateUpdate()` |
| `AudioCaptionEvents` | front/back `NativeQueue<AudioCaptionPayload>` | `IAudioCaptionEventListener` | `Raise` | `SystemDispatcher.LateUpdate()` |
| `SpectrumEvents` | front/back `NativeQueue<SpectrumMode>`, `NativeQueue<float>`, `NativeQueue<SpatialSonarSnapshot>`, `NativeQueue<AcousticEchoEvent>` | `ISpectrumModeEventListener`, `ISonarPulseEventListener`, `ISonarPingEventListener`, `ISonarSnapshotEventListener`, `IAcousticEchoEventListener` | `RaiseModeChanged`, `RaiseSonarPulse`, `RaiseSonarPingSent`, `RaiseSonarSnapshotUpdated`, `RaiseAcousticEchoReturned` | `SystemDispatcher.LateUpdate()` |
| `ProceduralAudioEvents` | front/back `NativeQueue<AudioPingTriggerInfo>`, `NativeQueue<StructuralStressAudioInfo>` | `IProceduralAudioEventListener` | `RaiseAudioPingTriggered`, `RaiseStructuralStressTriggered` | `SystemDispatcher.LateUpdate()` |
| `HectonSubmarineOsEvents` | front/back `NativeQueue<SubmarineOsEventPayload>` | `ISubmarineOsEventListener` | `RaiseSnapshotUpdated`, `RaiseLogRequested` | `SystemDispatcher.LateUpdate()` |
| `LaserCutterEvents` | front/back `NativeQueue<LaserCutterEventPayload>` | `ILaserCutterEventListener` | `RaiseHeatChanged`, `RaiseBeamStateChanged` | `SystemDispatcher.LateUpdate()` |
| `FlashlightEvents` | front/back `NativeQueue<FlashlightEventPayload>` | `IFlashlightEventListener` | `RaiseToggled`, `RaiseBatteryDepleted`, `RaiseOverheat`, `RaiseFlickerStart` | `SystemDispatcher.LateUpdate()` |
| `PlayerSignalEvents` | front/back `NativeQueue<TraumaHudSignal>`, `NativeQueue<InteractionSignal>`, `NativeQueue<ToolDepletedSignal>` | `IPlayerSignalEventListener` | `RaiseTraumaHudSignal`, `RaiseInteractionSignal`, `RaiseToolDepletedSignal` | `SystemDispatcher.LateUpdate()` |
| `MapMagicBiomeEvents` | front/back `NativeQueue<int>` | `IMapMagicBiomeEventListener` | `RaiseBiomeChanged` | `SystemDispatcher.LateUpdate()` |
| `BiomeMatrixEvents` | front/back `NativeQueue<BiomeMatrixEventPayload>` | `IBiomeMatrixEventListener` | `RaiseMatrixBiomeChanged`, `RaiseDepthTierChanged` | `SystemDispatcher.LateUpdate()` |
| `DirectorAIEvents` | front/back `NativeQueue<DirectorAIEventPayload>` | `IDirectorAIEventListener` | `RaiseSpawnHordeRequested`, `RaiseEquipmentGlitchRequested`, `RaiseRareDiscoveryRequested`, `RaiseWeatherShiftRequested`, `RaiseMissionTriggerRequested`, `RaisePredatorPressureChanged` | `SystemDispatcher.LateUpdate()` |
| `HectonDroneFleetEvents` | vault-backed pending/next-frame `NativeArray<HectonDroneFleetSnapshotPayload>[64]` lanes | `IDroneFleetSnapshotEventListener` | `RaiseSnapshotUpdated` | `SystemDispatcher.LateUpdate()` |
| `WeatherEvents` | front/back `NativeQueue<WeatherEventPayload>` | `IWeatherEventListener` | `RaiseSnapshotUpdated` | `SystemDispatcher.LateUpdate()` |
| `RandomEventEvents` | front/back `NativeQueue<RandomEventStartedPayload>`, `NativeQueue<RandomEventType>`, `NativeQueue<SeismicShockwaveEvent>` | `IRandomEventListener` | `RaiseStarted`, `RaiseEnded`, `RaiseSeismicShockwave` | `SystemDispatcher.LateUpdate()` |
| `PowerGridTelemetryEvents` | front/back `NativeQueue<PowerGridTelemetrySnapshot>` | `IPowerGridTelemetryListener` | `Raise` | `SystemDispatcher.LateUpdate()` |
| `ModuleStatusEvents` | front/back `NativeQueue<ModuleStatusEventPayload>` | `IModuleStatusEventListener` | `NotifyEnter`, `NotifyExit` | `SystemDispatcher.LateUpdate()` |
| `BaseAirlockEvents` | front/back `NativeQueue<BaseAirlockEventPayload>` | `IBaseAirlockEventListener` | `RaiseCycleStarted`, `RaiseCycleCompleted`, `RaiseEnvironmentChanged`, `RaiseEmergencyLockdownChanged`, `RaiseManualOverrideBlockedChanged`, `RaiseManualOverrideCompleted` | `SystemDispatcher.LateUpdate()` |
| `DepthZoneEvents` | front/back `NativeQueue<DepthZoneEventPayload>` | `IDepthZoneEventListener` | `RaiseZoneEntered`, `RaiseZoneExited` | `SystemDispatcher.LateUpdate()` |
| `SoundscapeEvents` | front/back `NativeQueue<SoundscapeEventPayload>` | `ISoundscapeEventListener` | `RaiseTierChanged` | `SystemDispatcher.LateUpdate()` |
| `EmergencyServiceRelayEvents` | front/back `NativeQueue<RelayEventPayload>` | `IEmergencyServiceRelayEventListener` | `RaiseRelayActivated` | `SystemDispatcher.LateUpdate()` |
| `SargassumGlobalDragManager` | front/back `NativeQueue<EntanglementStrainSignal>`, `NativeQueue<MassiveDisplacementSignal>` | `ISargassumGlobalDragEventListener` | `RaiseEntanglementStrain`, `RaiseMassiveDisplacement` | `SystemDispatcher.LateUpdate()` via `FlushPendingEvents()` |
| `AtlasSignalEvents` | front/back `NativeQueue<AtlasSignalEventPayload>` | `IAtlasSignalEventListener` | `RaisePulse`, `RaiseDetected`, `RaiseStrengthChanged`, `RaiseDecoded` | `SystemDispatcher.LateUpdate()` |
| `InventoryEvents` | front/back `NativeQueue<InventoryEventPayload>` | `IInventoryEventListener` | `NotifyInventoryFull`, `NotifyInventoryChanged`, `NotifyEncumbranceChanged` | `SystemDispatcher.LateUpdate()` |
| `PlayerExpressionEvents` | front/back `NativeQueue<PlayerExpressionEventPayload>` | `IPlayerExpressionEventListener` | `RaiseProfileChanged` | `SystemDispatcher.LateUpdate()` |
| `BaseIntegrityEvents` | front/back `NativeQueue<BaseIntegrityEventPayload>` | `IBaseIntegrityEventListener` | `RaiseIntegrityWarning`, `RaiseBreached`, `RaiseEmergency`, `RaiseAirQualityWarning` | `SystemDispatcher.LateUpdate()` |
| `NotificationEvents` | front/back `NativeQueue<NotificationEventPayload>` | `INotificationEventListener` | `PushInfo`, `PushWarning`, `PushCritical` | `SystemDispatcher.LateUpdate()` |
| `PDAIntrusionEvents` | front/back `NativeQueue<PDAIntrusionEventPayload>` | `IPDAIntrusionEventListener` | `RaiseRebootCompleted` | `SystemDispatcher.LateUpdate()` |
| `PDAEvents` | front/back `NativeQueue<PDAEventPayload>` | `IPDAEventListener` | `RaiseOpened`, `RaiseClosed`, `RaiseTabChanged`, `RaiseMapChunkExplored`, `RaiseMarkerChanged`, `RaiseLogbookChanged` | `SystemDispatcher.LateUpdate()` |
| `GameBootstrapper` | front/back `NativeQueue<GameBootstrapperEventPayload>` | `IGameBootstrapperEventListener` | private `RaiseGameReadyEvent`, private `RaiseBootstrapFailedEvent` | `SystemDispatcher.LateUpdate()` via `FlushPendingEvents()` |
| `ObjectPoolDiagnostics` | front/back `NativeQueue<PoolDiagnosticsEventPayload>` | `IObjectPoolDiagnosticsListener` | `PublishDataBusDepth`, internal pool warnings | `SystemDispatcher.LateUpdate()` |
| `PerformanceEvents` | front/back `NativeQueue<PerformanceEventPayload>` | `IPerformanceEventListener` | `RaiseFrameTimeSpike`, `RaiseGCAllocExceeded`, `RaiseJobQueueBacklog` | `SystemDispatcher.LateUpdate()` |
| `Atlas6Events` | front/back `NativeQueue<Atlas6EventPayload>` | `IAtlas6EventListener` | `RaisePlayerStatusChanged`, `RaiseDirectiveConflict`, `RaiseBarterAccepted`, `RaiseScarcityDirective` | `SystemDispatcher.LateUpdate()` |
| `GlobalRegistry` service rebound events | front/back `NativeQueue<RegistryEventPayload>` plus fixed sidecar slots | `IRegistryEventListener`, `IGlobalRegistryHotSwapListener` | service `Register*` / `Unregister*` rebound queueing | `SystemDispatcher.LateUpdate()` via `FlushPendingServiceReboundEvents()` |
| `ModCommandDispatcher` | `NativeQueue<ModCommand>`, `NativeQueue<ModAupCommand>`, `NativeQueue<ModRenderInstanceCommand>` | `IModCommandKernel`, `IDispatcherRaycastReceiver`, `HectonEventBus` unmanaged result payloads | `Request`, `RequestAup`, `RequestRenderInstance` | `SystemDispatcher.LateUpdate()` before first-party event flushes |

## VRAM Map

Mandate target for MX350 from performance budget: total VRAM ceiling 1800 MiB; texture budget 900 MiB; render targets and depth 320 MiB; shadow maps 128 MiB; geometry buffers 200 MiB; compute/UAV 128 MiB; shader constant pools 64 MiB; post-process chain 96 MiB; driver reserve 164 MiB. Guard: used/total > 0.90 triggers mip downgrade.

| Metric | Value | Evidence |
|---|---:|---|
| Texture files scanned | `1652` | `Docs/Reports/VRAM_Budget_Audit.json` |
| Mesh files scanned | `302` | `Docs/Reports/VRAM_Budget_Audit.json` |
| All scanned full-mip BC7 MiB | `1298.652` | `Docs/Reports/VRAM_Budget_Audit.json` |
| Runtime-candidate full-mip BC7 MiB | `1298.652` | `Docs/Reports/VRAM_Budget_Audit.json` |
| First-party production full-mip BC7 MiB | `505.623` | `Docs/Reports/VRAM_Budget_Audit.json` |
| MX350 texture budget MiB | `900.0` | `Docs/Reports/VRAM_Budget_Audit.json` |
| Critical texture pool MiB | `1228.8` | `Docs/Reports/VRAM_Budget_Audit.json` |
| Texture VRAM crime rows | `801` | `Docs/Reports/VRAM_Budget_Audit.json` |
| Mesh redline rows | `293` | `Docs/Reports/VRAM_Budget_Audit.json` |
| First-party large streaming mips off | `50` | `Docs/Reports/VRAM_Budget_Audit.json` |
| All large streaming mips off | `156` | `Docs/Reports/VRAM_Budget_Audit.json` |
| Expected VRAM CI exit code | `2` | `Docs/Reports/VRAM_Budget_Audit.json` |

### Top Non-First-Party Runtime Payload Pressure

| Directory | Count | Full-mip BC7 MiB | VRAM crime rows |
|---|---:|---:|---:|
| `Assets/ScifiFacility/Textures` | 76 | 525.0 | 11 |
| `Assets/Screenshots` | 100 | 43.623 | 0 |
| `Assets/Feel/MMTools/Tools/MMVFX/MMBloomDirt` | 4 | 34.277 | 4 |
| `Assets/Feel/MMTools/Tools/MMPrototypeTextures/Textures/MMProtoTextures` | 24 | 32.0 | 0 |
| `Assets/MapMagic/Map_Graph/New Gen` | 1 | 21.333 | 1 |
| `Assets/TRANSFER HUB/family kelp tall` | 4 | 21.333 | 0 |
| `Assets/Feel/MMTools/Tools/MMPrototypeTextures/Textures/MMPlastic` | 3 | 16.0 | 0 |
| `Packages/com.unity.shadergraph/GraphTemplates/Cross Pipeline` | 7 | 15.234 | 2 |
| `Assets/Plugins/Sirenix/Odin Inspector/Assets/Editor` | 1 | 12.0 | 1 |
| `Assets/Feel/MMTools/Tools/MMVFX/MMNoise` | 13 | 9.333 | 0 |
| `Assets/Feel/MMTools/Demos/MMTween/Textures` | 1 | 5.333 | 1 |
| `Assets/Feel/MMTools/Tools/MMVFX/MMParticles` | 16 | 5.333 | 0 |

### Atlas Candidates

| Group | Count | Combined BC7 MiB |
|---|---:|---:|
| `Assets/_Project/Art/TEXTURES/Detali` | 7 | 7.0 |
| `Assets/_Project/Art/Sprites/ui` | 6 | 6.0 |
| `Assets/_Project/Art/TEXTURES` | 4 | 4.0 |
| `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching.v2` | 4 | 4.0 |
| `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.brittle` | 4 | 4.0 |

### Mesh Redlines

| Path | Triangles | Flags |
|---|---:|---|
| `Assets/_Project/_PROLOGUE_CONTENT/Models/ceiling_10.fbx` | 108 | `MESH_COMPRESSION_OFF_STATIC_SUSPECT` |
| `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_05.fbx` | 670 | `MESH_COMPRESSION_OFF_STATIC_SUSPECT` |
| `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_large_8x8.fbx` | 2 | `MESH_COMPRESSION_OFF_STATIC_SUSPECT` |
| `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_2x3_a.fbx` | 530 | `MESH_COMPRESSION_OFF_STATIC_SUSPECT` |
| `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx` | 742 | `MESH_COMPRESSION_OFF_STATIC_SUSPECT` |
| `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx` | 782 | `MESH_COMPRESSION_OFF_STATIC_SUSPECT` |
| `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_04_3x6_d.fbx` | 586 | `MESH_COMPRESSION_OFF_STATIC_SUSPECT` |
| `Assets/_Project/Art/Materials/Meshy_AI_Alien_barnacles_clust_0301230506_texture.fbx` | 10000 | `MESH_COMPRESSION_OFF_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/_Project/Art/Models/Rocks/Rock 5/orig/River_Rock_FBX.fbx` | 3054 | `MESH_COMPRESSION_OFF_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/_Project/Art/Models/Rocks/Rock 6/rock6/Mossy_Forest_Rock_vimrfjsaw_Mid.fbx` | 3539 | `MESH_COMPRESSION_OFF_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx` | 6519 | `MESH_COMPRESSION_OFF_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Forest_Rock_Shelf_wgpqfjl_Mid.fbx` | 4038 | `MESH_COMPRESSION_OFF_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Mossy_Forest_Rock_vimrfjsaw_Mid.fbx` | 3539 | `MESH_COMPRESSION_OFF_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Nordic_Beach_Rock_Formation_vd4iecjva_Low.fbx` | 2100 | `MESH_COMPRESSION_OFF_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Nordic_Beach_Rock_uknoehp_Mid.fbx` | 1218 | `MESH_COMPRESSION_OFF_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx` | 5000 | `MESH_COMPRESSION_OFF_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/Dynamic Decals/Resources/Decal.obj` | 12 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_COMPRESSION_OFF_STATIC_SUSPECT` |
| `Assets/Feel/MMTools/Demos/MMGhostCamera/Models/MMGhostCameraCity.fbx` | 127645 | `MESH_GEOMETRY_ESTIMATE_GT_16MIB_STATIC, MESH_GT_80K_ABSOLUTE_STATIC, MESH_REDLINE_GT_50K_NO_LOD, MESH_COMPRESSION_OFF_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/decal_01.fbx` | 63 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/decal_02.fbx` | 72 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/decal_03.fbx` | 102 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/decal_04.fbx` | 44 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/decal_05.fbx` | 8 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/decal_06.fbx` | 16 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/decal_07.fbx` | 14 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/decal_a2.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/decal_a3.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/decal_b2.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/decal_b3.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/decal_x2.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/decal_x3.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/label_01.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/label_02.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/label_03.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/label_04.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/label_05.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/label_06.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/label_07.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/label_08.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/label_09.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/label_10.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/label_11.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/stripes_01.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/stripes_02.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/stripes_03.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/stripes_04.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/stripes_05.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/stripes_06.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/stripes_07.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/stripes_08.fbx` | 4 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/stripes_icon_01.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/stripes_icon_02.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/stripes_icon_03.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/stripes_icon_04.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/stripes_icon_05.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/decals/stripes_icon_06.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/bed.fbx` | 1046 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/bed_02.fbx` | 2243 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/bed_02_base.fbx` | 970 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/bed_ladder.fbx` | 912 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/chair_01.fbx` | 2548 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/chair_02.fbx` | 2234 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/chair_03.fbx` | 440 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/couch_01.fbx` | 308 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/couch_02.fbx` | 590 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/couch_03.fbx` | 590 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/couch_connector.fbx` | 188 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/locker_01.fbx` | 342 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/locker_02.fbx` | 253 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/locker_03.fbx` | 497 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/locker_04.fbx` | 515 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/table_01.fbx` | 698 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/table_02.fbx` | 746 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/table_03.fbx` | 266 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/table_03_connector.fbx` | 78 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/table_03_corner.fbx` | 212 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/table_03_prop_01.fbx` | 86 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/furniture/table_base.fbx` | 466 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/box_01.fbx` | 750 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/controlpanels/controlpanel_01.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/controlpanels/controlpanel_02.fbx` | 98 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/controlpanels/controlpanel_03.fbx` | 290 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/controlpanels/controlpanel_04.fbx` | 226 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/controlpanels/controlpanel_05.fbx` | 34 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/controlpanels/controlpanel_06.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/controlpanels/controlpanel_07.fbx` | 106 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/controlpanels/controlpanel_08.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/controlpanels/controlpanel_09.fbx` | 498 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/controlpanels/controlpanel_10.fbx` | 80 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/controlpanels/controlpanel_11.fbx` | 258 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/controlpanels/controlpanel_12.fbx` | 54 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/controlpanels/controlpanel_13.fbx` | 1260 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/controlpanels/controlpanel_14.fbx` | 754 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/controlpanels/controlpanel_15.fbx` | 1212 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/controlpanels/controlpanel_16.fbx` | 690 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/controlpanels/controlpanel_17.fbx` | 1000 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/pipes/detail_05_e01.fbx` | 588 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/pipes/detail_05_e02.fbx` | 392 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/pipes/detail_05_e03.fbx` | 560 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/pipes/detail_05_e04.fbx` | 224 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/technical/detail_01.fbx` | 594 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/technical/detail_02.fbx` | 2688 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/technical/detail_03.fbx` | 52 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/technical/detail_03_a.fbx` | 1216 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/technical/detail_03_b.fbx` | 1964 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/technical/detail_04.fbx` | 48 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/technical/detail_04_a.fbx` | 827 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/technical/detail_04_b.fbx` | 7377 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/technical/detail_04_c.fbx` | 1788 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/technical/detail_04_d.fbx` | 1518 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/technical/detail_04_e.fbx` | 278 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/technical/detail_04_f.fbx` | 589 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/technical/detail_04_g.fbx` | 598 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/technical/detail_05_a.fbx` | 212 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/technical/detail_05_b.fbx` | 258 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/technical/detail_05_c.fbx` | 409 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/details/technical/detail_05_d.fbx` | 250 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/keyboard.fbx` | 1314 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/keyboard_b.fbx` | 1314 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/lights/light_01.fbx` | 276 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/lights/light_02.fbx` | 48 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/lights/light_03.fbx` | 34 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/lights/light_04.fbx` | 26 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/lights/light_05.fbx` | 138 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/lights/light_06.fbx` | 176 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/lights/light_07.fbx` | 252 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/lights/light_08.fbx` | 280 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/lights/light_09.fbx` | 200 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/lights/warningLight.fbx` | 238 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/monitor.fbx` | 510 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/prop_01.fbx` | 575 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/prop_02.fbx` | 510 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/prop_03.fbx` | 612 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/prop_04.fbx` | 382 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/prop_04_b.fbx` | 175 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/prop_04_c.fbx` | 161 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/prop_05.fbx` | 1582 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/prop_06.fbx` | 448 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/prop_07.fbx` | 270 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/prop_08.fbx` | 266 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/prop_09.fbx` | 264 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/prop_10.fbx` | 1858 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/prop_10_base.fbx` | 3952 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/prop_11.fbx` | 3090 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/prop_11_base.fbx` | 1920 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/prop_12_a.fbx` | 60 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/prop_12_b.fbx` | 252 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/prop_13.fbx` | 1992 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/prop_14.fbx` | 5189 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/prop_15.fbx` | 2999 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/server_racks/server_rack_01.fbx` | 684 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/server_racks/server_rack_02.fbx` | 668 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/server_racks/server_rack_03.fbx` | 684 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/tubes/tube_01.fbx` | 352 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/tubes/tube_02.fbx` | 352 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/tubes/tube_03.fbx` | 1337 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/tubes/tube_cap_01.fbx` | 180 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/props/tubes/tube_cap_02.fbx` | 180 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/ceiling/ceiling_01.fbx` | 170 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/ceiling/ceiling_02.fbx` | 136 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/ceiling/ceiling_03.fbx` | 136 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/ceiling/ceiling_04.fbx` | 126 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/ceiling/ceiling_05.fbx` | 14 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/ceiling/ceiling_06.fbx` | 98 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/ceiling/ceiling_07.fbx` | 66 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/ceiling/ceiling_08.fbx` | 90 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/ceiling/ceiling_09.fbx` | 312 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/ceiling/ceiling_09_connector.fbx` | 220 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/ceiling/ceiling_11.fbx` | 1032 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/ceiling/ceiling_12.fbx` | 22 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_01.fbx` | 78 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_01_4x4.fbx` | 6 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_02.fbx` | 348 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_02a.fbx` | 166 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_03.fbx` | 168 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_04.fbx` | 166 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_05.fbx` | 670 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_06.fbx` | 18 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_border_01.fbx` | 273 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_border_02.fbx` | 181 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_border_03.fbx` | 33 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_border_04.fbx` | 256 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_border_05.fbx` | 119 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_border_06.fbx` | 29 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_border_corner_01.fbx` | 210 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_border_corner_02.fbx` | 94 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_crosssection_01.fbx` | 94 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_large_8x8.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_part_01.fbx` | 154 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_part_02.fbx` | 186 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_part_03.fbx` | 172 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor/floor_small_4x4.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor_01.fbx` | 78 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor_01_4x4.fbx` | 6 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor_02.fbx` | 348 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor_02a.fbx` | 166 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor_03.fbx` | 168 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor_04.fbx` | 166 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor_06.fbx` | 18 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor_border_01.fbx` | 273 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor_border_02.fbx` | 181 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor_border_03.fbx` | 33 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor_border_04.fbx` | 256 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor_border_05.fbx` | 119 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor_border_06.fbx` | 29 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor_border_corner_01.fbx` | 210 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor_border_corner_02.fbx` | 94 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor_crosssection_01.fbx` | 94 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor_part_01.fbx` | 154 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor_part_02.fbx` | 186 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor_part_03.fbx` | 172 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/floor/floor_small_4x4.fbx` | 2 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/rails+scaffolds+stairs/rail_01.fbx` | 582 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/rails+scaffolds+stairs/rail_02.fbx` | 594 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/rails+scaffolds+stairs/scaffold_01.fbx` | 80 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/rails+scaffolds+stairs/scaffold_02.fbx` | 80 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/rails+scaffolds+stairs/scaffold_03.fbx` | 32 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/rails+scaffolds+stairs/scaffold_04.fbx` | 8 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/rails+scaffolds+stairs/scaffold_connector.fbx` | 320 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/rails+scaffolds+stairs/stairs_01.fbx` | 2176 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/rails+scaffolds+stairs/walk_01.fbx` | 1880 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/rails+scaffolds+stairs/walk_02.fbx` | 2228 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/rails+scaffolds+stairs/walk_03.fbx` | 1188 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/rails+scaffolds+stairs/walk_04.fbx` | 382 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/trims/trim_01.fbx` | 308 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/trims/trim_02.fbx` | 90 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/trims/trim_03.fbx` | 28 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/trims/trim_04.fbx` | 56 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/trims/trim_05.fbx` | 68 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/trims/trim_05_cap.fbx` | 28 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/trims/trim_06_a.fbx` | 178 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/trims/trim_06_b.fbx` | 182 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/trims/trim_07.fbx` | 4 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/column_01.fbx` | 1535 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/column_01_base.fbx` | 334 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/column_03.fbx` | 2000 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/column_04.fbx` | 1594 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/column_04_connector.fbx` | 180 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/column_04_connector_b.fbx` | 92 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/column_05.fbx` | 882 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/column_05_prop_01.fbx` | 282 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/hull.fbx` | 223 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/viewing_deck.fbx` | 12778 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_2x3_b.fbx` | 662 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_2x3_top_trim.fbx` | 89 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_a.fbx` | 55 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_b.fbx` | 282 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_d.fbx` | 140 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_door.fbx` | 1407 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_door_02.fbx` | 960 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_door_b.fbx` | 614 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_door_wing_left.fbx` | 304 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_door_wing_right.fbx` | 524 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_door_wings.fbx` | 878 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_e.fbx` | 481 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_f.fbx` | 422 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_g.fbx` | 92 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_h.fbx` | 5388 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_h_door.fbx` | 552 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_i.fbx` | 447 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_top_trim.fbx` | 112 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_top_trim_corner.fbx` | 364 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_window.fbx` | 706 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_6x3_door.fbx` | 3540 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_6x3_door_b.fbx` | 3468 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_connector_01.fbx` | 158 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_connector_02.fbx` | 276 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_connector_03.fbx` | 20 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_connector_04.fbx` | 110 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_connector_ceiling_07.fbx` | 42 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_connector_corner.fbx` | 19 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_corner_a.fbx` | 235 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_corner_a_ceiling.fbx` | 202 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_corner_b.fbx` | 235 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_01_corner_b_ceiling.fbx` | 276 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_02_1x1_a.fbx` | 204 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_02_1x1_b.fbx` | 235 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_02_1x1_corner.fbx` | 226 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_02_1x2_d.fbx` | 98 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_02_1x3_c.fbx` | 467 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_03_4x3_a.fbx` | 321 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_03_8x3_b.fbx` | 451 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_04_10x1_f.fbx` | 18 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_04_2x4_b.fbx` | 84 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_04_4x1_f.fbx` | 16 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_04_6x4_e.fbx` | 254 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_04_8x10_c.fbx` | 180 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_04_8x10_c_connector_a.fbx` | 20 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_04_8x10_c_connector_b.fbx` | 48 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_04_8x10_c_connector_c.fbx` | 48 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_04_8x10_c_connector_d.fbx` | 90 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/ScifiFacility/Models/structural/walls/wall_04_8x10_c_door.fbx` | 290 | `MESH_READ_WRITE_ENABLED_STATIC_SUSPECT, MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT` |
| `Assets/Shapes/Models/shapes_primitives.fbx` | 3222 | `MESH_COMPRESSION_OFF_STATIC_SUSPECT` |

## SHERST Wall Of Shame

Pattern scan: active `Docs/AgentLogs/` only; terms: `TODO`, `HACK`, `FIX LATER`. These are text hits, not proof of executable debt.

| File | Line | Text |
|---|---:|---|
| `Docs/AgentLogs/LOG_SHINOBU_242.md` | 34 | - Static proof only: `git diff --check` passed; rg found no `get; set;`, `async void`, `TODO`, `NotImplemented`, `Mathf`, `System.Random`, runtime `ParticleSystem`, or runtime `... |
| `Docs/AgentLogs/LOG_SHINOBU_242.md` | 248 | - Static gates: `git diff --check` passed for SHINOBU_242 paths; rg found no `Task`, `System.Threading.Tasks`, `async void`, `FloatMode.Deterministic`, auto-properties, `Pack=1`... |
| `Docs/AgentLogs/LOG_SHINOBU_244.md` | 130 | Verification: Static scan for persistent native ownership, scene component preview, old helpers, async writer, best-point remnants, TODO/NotImplemented returns no hits in owned ... |
| `Docs/AgentLogs/LOG_SHINOBU_251.md` | 10 | Exact Microseconds saved: Replacing Rigidbody.drag/mass tuning prevents managed scene-side force hacks and hot component writes. Diagonal minimum-budget path estimates 0.24 us/entity;... |
| `Docs/AgentLogs/LOG_SHINOBU_251.md` | 12 | Verification: Static scan found no Rigidbody.mass/drag/angularDrag hack sites under Assets&#47;_Project/Scripts/Vehicles. git diff --check passed with only existing LF/CRLF warnings... |
| `Docs/AgentLogs/LOG_SHINOBU_81.md` | 1269 | What was wrong: parked verification batches already said "do not contact", but row bodies still contained operational scratch fields: `Custom opener: TODO`, `Required asset: TOD... |
| `Docs/AgentLogs/Rationale_SHINOBU_155.md` | 23 | Problem: `HectonPlayerHealth.Die()` has no reload path to delete, but it also has no authoritative AUP/physiology reset and leaves death as a managed event/TODO. |
| `Docs/AgentLogs/Rationale_SHINOBU_201.md` | 283 | Rejected Alternatives: Keeping `sqrt` for readability was rejected by the i3/NEON mandate. Quake-style bit hacks were rejected by mandate. Lookup tables were rejected for this d... |
| `Docs/AgentLogs/Rationale_SHINOBU_251.md` | 8 | Hardware Impact: i3/MX350 avoids scene polling and Rigidbody.drag hacks; expected minimum-budget saving is dominated by no managed hot path and no per-frame component search. |
| `Docs/AgentLogs/Rationale_SHINOBU_251.md` | 36 | Hardware Impact: Additional divide/multiply is below 0.1 us/entity on low-end silicon and replaces heavier managed tuning hacks. |
| `Docs/AgentLogs/Rationale_SHINOBU_251.md` | 63 | Scalability potential: Audit/docs do not change runtime quality behavior; they prevent future regressions that would reintroduce scalar Rigidbody hacks. |
| `Docs/AgentLogs/Rationale_SHINOBU_81.md` | 1471 | ## Decision 268 - Verification Batch TODO Fields Are Scratch, Not Readiness |
| `Docs/AgentLogs/Rationale_SHINOBU_81.md` | 1473 | Problem: `AgentOps/VerificationBatches_2026-05-19/VERIFY_BATCH_*.md` files are parked behind explicit raw sprints, but each row still contains operational-looking fields such as... |

## PHI Self-Audit

Exact PHI_SYN rationale file is absent from active logs. Near-match H-Phi rationale files were scanned as supporting evidence, but this is not treated as the exact requested artifact.

Near-match active logs:

| H-Phi / UX signal | Declared at | Producers | Consumers |
|---|---|---|---|
| `PlayerActionProgressSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8415` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6986` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:133` |
| `PlayerActionCompletedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8434` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6993` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:137` |
| `PlayerActionCancelledSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8450` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7000` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:141` |
| `PdaExchangeStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9926` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7014` | `Assets/_Project/Scripts/UI/PDABarterTab.cs:198`<br>`Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:875` |
| `VehicleUpgradesChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9947` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7021` | none found |
| `PlayerStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8359` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6497` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1698`<br>`Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:636`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2169`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2628`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2729`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5836` |
| `InventoryChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8494` | `Assets/_Project/Scripts/PlayerInventory.cs:4234` | `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:666`<br>`Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:869`<br>`Assets/_Project/Scripts/HectonPlayerMovement.cs:4808`<br>`Assets/_Project/Scripts/PDAInventoryTab.cs:512`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:547`<br>`Assets/_Project/Scripts/PlayerToolManager.cs:1980`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:2866`<br>`Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs:279` |
| `InputStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:968` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:629` | `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:742`<br>`Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:598`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1753`<br>`Assets/_Project/Scripts/UI/InteractionUI.cs:908`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5875` |
| `SystemHealthSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:422` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:824` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1326`<br>`Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:585`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:848`<br>`Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:426`<br>`Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1078`<br>`Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1549`<br>`Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:851`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5893` |

## Phi-Resonance Connectivity Model

The engine connectivity model is not mystical. It is a three-layer resonance model: contracts define stable frequency, typed signal lanes carry bounded pulses, and dispatcher phases control when state can move. `GlobalRegistry` is the cold authority spine for stable interfaces. `SignalBus<T>` and queue-backed event lanes are the nervous system for broadcast state. DataVault/native buffers are the mass: owned memory that simulation mutates and presentation samples.

Low/MX350 interpretation: coalesce lanes, consume bounded snapshots, keep optional visual consumers in `VISUAL_SYNC`, and shed mips/LOD before simulation truth. High/Ultra interpretation: do not increase gameplay broadcast cost; attach richer presentation consumers to the same snapshots and spend the saved budget on light, fog, HUD, audio, and material overkill.

Connectivity risk: `Hecton8.Core` currently references many domain assemblies directly. That may be intentional current architecture, but it is not a clean inward-only core. The integrator should treat this as a dependency inversion watchpoint, especially while many agents expand contracts in parallel.

## Verification Commands

- `python Tools/BuildArchitectureAtlas.py`
- `python Tools/AtlasCheck.py`
- `python -m py_compile Tools/BuildArchitectureAtlas.py Tools/AtlasCheck.py`
- C# compile verification is outside this atlas; run Unity import/Console and serial CLI builds as separate evidence.
- Current DOC_GLOBAL R51 blocker: `python Tools/AtlasCheck.py` still exits `1` with `ATLAS_CHECK_FAIL references=6881 missing=60`; missing refs currently include `Assets/Dynamic Decals/Resources/Decal.obj`, RealtimeCSG vendor icon/readme image references (57), `Assets/_Project/Scripts/Editor/HectonMaskChannelPacker.cs`, `Assets/_Project/Scripts/Editor/HectonMaterialChannelPackValidator.cs` until the references are restored or the atlas check excludes that evidence class deliberately.
- This generated atlas is not `VERIFIED` unless `Tools/AtlasCheck.py` exits `0` after generation.

## Residual Risk

- STATIC_SOURCE only: Unity import, runtime wiring, actual VRAM residency, profiler frame time, GC, build, and Play Mode remain PENDING VERIFICATION.
- The signal producer map only resolves explicit `SignalBus<T>` calls. Legacy `GlobalSignals.Publish(...)` variable publishes require Roslyn-level dataflow to type-resolve fully.
- Active logs can change while this atlas is being written because the workspace is multi-agent.
