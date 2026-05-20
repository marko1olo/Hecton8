# HECTON-8 Architecture Atlas - Dependency Graph

Generated: 2026-05-20 15:52:17
Date: 2026-05-20
Status: ATLAS GENERATED STATIC SOURCE / ATLASCHECK SEPARATE GATE REQUIRED / RUNTIME PENDING
Evidence class: STATIC_SOURCE / STATIC_DOC / FILESYSTEM / PY_TOOL. No Unity Editor, Play Mode, Memory Profiler, Frame Debugger, or player build evidence is claimed here.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R47 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

Current DOC_GLOBAL boundary (2026-05-20 R47): `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md` is the latest local static root/architecture authority-spine, runtime-wording, and counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction; R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; AtlasCheck remains red and runtime proof is absent.
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

- C# source files scanned under `Assets/` and `Packages/`: 5289
- C# line count scanned under `Assets/` and `Packages/`: 2,067,632
- First-party C# source files under `Assets/_Project/Scripts/`: 2027
- First-party C# line count under `Assets/_Project/Scripts/`: 1,405,085
- Assembly definitions scanned: 203
- First-party assembly definitions under `Assets/_Project/`: 143
- Markdown docs under `Docs/`: 2987

## Assembly Dependency Graph

Core assembly: `Assets/_Project/Scripts/Hecton8.Core.asmdef`

`Hecton8.Core` direct references currently recorded in its asmdef:
- `Hecton8.Core.Contracts`
- `Hecton8.Core.Database`
- `Hecton8.Core.Scheduling`
- `Hecton8.Core.Bucketing`
- `Hecton8.Core.Persistence.Paging`
- `Hecton8.Core.Memory`
- `Hecton8.Audio.Virtualization.Contracts`
- `Hecton8.Input`
- `Unity.InputSystem`
- `Unity.Mathematics`
- `Unity.Burst`
- `Unity.Collections`
- `Unity.Addressables`
- `Unity.ResourceManager`
- `Unity.Profiling.Core`
- `Unity.TextMeshPro`
- `UnityEngine.UI`
- `Unity.RenderPipelines.Core.Runtime`
- `Unity.RenderPipelines.Universal.Runtime`
- `GPUInstancer`

Core contracts assembly: `Assets/_Project/Scripts/Core/Contracts/Hecton8.Core.Contracts.asmdef` references `Unity.Collections`, `Unity.Mathematics`.

Assemblies directly depending on exact `Hecton8.Core`: 66

| Assembly | Path |
|---|---|
| `Hecton8.AI.Ambient` | `Assets/_Project/Scripts/AI/Ambient/Hecton8.AI.Ambient.asmdef` |
| `Hecton8.AI.Cognition.Editor` | `Assets/_Project/Scripts/AI/Cognition/Editor/Hecton8.AI.Cognition.Editor.asmdef` |
| `Hecton8.AI.Pathfinding` | `Assets/_Project/Scripts/AI/Pathfinding/Hecton8.AI.Pathfinding.asmdef` |
| `Hecton8.Animation.FaunaProcedural` | `Assets/_Project/Scripts/Animation/FaunaProcedural/Hecton8.Animation.FaunaProcedural.asmdef` |
| `Hecton8.Animation.FaunaProcedural.Editor` | `Assets/_Project/Scripts/Animation/FaunaProcedural/Editor/Hecton8.Animation.FaunaProcedural.Editor.asmdef` |
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
| `Hecton8.World.Outposts` | `Assets/_Project/Scripts/World/Outposts/Hecton8.World.Outposts.asmdef` |
| `Hecton8.World.ProceduralCoral.Editor` | `Assets/_Project/Scripts/World/ProceduralCoral/Editor/Hecton8.World.ProceduralCoral.Editor.asmdef` |
| `Hecton8.World.ProceduralWreckage.Editor` | `Assets/_Project/Scripts/World/ProceduralWreckage/Editor/Hecton8.World.ProceduralWreckage.Editor.asmdef` |
| `Hecton8.World.ShinobuBiomimetic` | `Assets/_Project/Scripts/World/ShinobuBiomimetic/Hecton8.World.ShinobuBiomimetic.asmdef` |
| `Hecton8.World.ShinobuBiomimetic.Editor` | `Assets/_Project/Scripts/World/ShinobuBiomimetic/Editor/Hecton8.World.ShinobuBiomimetic.Editor.asmdef` |
| `Hecton8.World.Streaming` | `Assets/_Project/Scripts/World/Streaming/Hecton8.World.Streaming.asmdef` |
| `Hecton8.World.VoxelSurfaceNets.Editor` | `Assets/_Project/Scripts/World/VoxelSurfaceNets/Editor/Hecton8.World.VoxelSurfaceNets.Editor.asmdef` |

Assemblies depending on any `Hecton8.Core*` assembly: 98

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
| `RootScripts` | `Core` | 258 |
| `World` | `Core` | 175 |
| `Gameplay` | `Core` | 158 |
| `UI` | `Core` | 137 |
| `RootScripts` | `World` | 87 |
| `Editor` | `Core` | 87 |
| `Editor` | `World` | 70 |
| `Physics` | `Core` | 70 |
| `RootScripts` | `Gameplay` | 62 |
| `Construction` | `Core` | 54 |
| `Visor` | `Core` | 50 |
| `Gameplay` | `World` | 43 |
| `RootScripts` | `Items` | 38 |
| `RootScripts` | `Bootstrap` | 37 |
| `AI` | `Core` | 34 |
| `Audio` | `Core` | 34 |
| `Atmosphere` | `Core` | 33 |
| `RootScripts` | `Environment` | 32 |
| `RootScripts` | `Physics` | 32 |
| `Tools` | `Core` | 32 |
| `Fauna` | `Core` | 30 |
| `RootScripts` | `Inventory` | 29 |
| `UI` | `Gameplay` | 29 |
| `UI` | `World` | 29 |
| `World` | `Environment` | 29 |
| `World` | `Gameplay` | 29 |
| `Power` | `Core` | 28 |
| `Gameplay` | `Physics` | 27 |
| `Graphics` | `Core` | 26 |
| `SaveSystem` | `Core` | 26 |
| `RootScripts` | `SaveSystem` | 25 |
| `VFX` | `Core` | 25 |
| `Physiology` | `Core` | 24 |
| `RootScripts` | `Interaction` | 22 |
| `RootScripts` | `Building` | 21 |
| `Construction` | `Gameplay` | 21 |
| `RootScripts` | `UI` | 20 |
| `Construction` | `World` | 20 |
| `RootScripts` | `Tools` | 20 |
| `RootScripts` | `Construction` | 19 |
| `RootScripts` | `Caves` | 19 |
| `Gameplay` | `Audio` | 19 |
| `Optimization` | `Core` | 19 |
| `Editor` | `Gameplay` | 18 |
| `RootScripts` | `AI` | 18 |
| `Gameplay` | `Interaction` | 18 |
| `UI` | `Bootstrap` | 18 |
| `Animation` | `Core` | 17 |
| `Interaction` | `Core` | 17 |
| `RootScripts` | `Audio` | 16 |
| `ModdingAPI` | `Core` | 16 |
| `Construction` | `Power` | 15 |
| `Lighting` | `Core` | 15 |
| `Quest` | `Core` | 15 |
| `RootScripts` | `Atmosphere` | 14 |
| `Plugins` | `Core` | 14 |
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
| `Gameplay` | `Tools` | 11 |
| `QA` | `Core` | 11 |
| `Rendering` | `Core` | 11 |
| `UI` | `Audio` | 11 |
| `World` | `AI` | 11 |
| `Editor` | `Environment` | 10 |
| `Gameplay` | `Items` | 10 |
| `UI` | `Input` | 10 |
| `Visor` | `Gameplay` | 10 |
| `World` | `Bootstrap` | 10 |
| `Construction` | `Building` | 9 |
| `Core` | `World` | 9 |

## SignalBus<T> Flow Map

`ISignal` structs declared: 279. `SignalBus<T>` lanes observed in producer/consumer calls: 219. Union listed below: 284 signals.
Legacy `GlobalSignals.Publish(...)` call sites found: 257. Many use local variables and are not type-resolved by this static pass; they remain a migration/integrator risk.

| Signal | Declared at | Producers (`SignalBus<T>.Push/Publish`) | Consumers (`GetFrameSnapshot`) |
|---|---|---|---|
| `AcousticPingSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9227` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6654`<br>`Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:2448`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1817`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1452`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1204`<br>`Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs:923`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:4375`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5409` | `Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:756`<br>`Assets/_Project/Scripts/Audio/Editor/ShinobuAcousticDspSmokeTester.cs:106`<br>`Assets/_Project/Scripts/HectonBoidController.cs:1724`<br>`Assets/_Project/Scripts/SpatialAudioManager.cs:5075`<br>`Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:2068`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:4357`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6075` |
| `AcousticShockwaveSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3960` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1808` | none found |
| `AcousticZoneChangedEvent` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:649` | `Assets/_Project/Scripts/AcousticZoneController.cs:29`<br>`Assets/_Project/Scripts/AcousticZoneController.cs:100`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:651` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:404`<br>`Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:1444` |
| `AnomalyProximitySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8906` | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:133` | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:823` |
| `AnomalySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8891` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6533` | none found |
| `ApexBrainAcousticEchoTap` | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:194` | none found | none found |
| `ApexPanicSignal` | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:407` | none found | none found |
| `ApexProximitySignal` | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:370` | none found | none found |
| `AtmosphericReentrySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9700` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6935` | `Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:219`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:196`<br>`Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:365` |
| `AudioEvent` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:639` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:633`<br>`Assets/_Project/Scripts/Audio/ProceduralAudioEvents.cs:750`<br>`Assets/_Project/Scripts/PowerGrid.cs:1392` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:249`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:5075` |
| `AupPreShiftSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8502` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6448` | `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2448` |
| `AupShiftSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8512` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6456` | `Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs:1059`<br>`Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1097`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:7456`<br>`Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1383`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:885`<br>`Assets/_Project/Scripts/HectonFluidEngine.cs:3370`<br>`Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:545`<br>`Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:484`<br>`Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:859`<br>`Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:1936`<br>`Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:1657`<br>`Assets/_Project/Scripts/Visor/SpectrumSystem.cs:2758`<br>... +5 more |
| `AuxiliaryFlareLightSignal` | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentContracts.cs:182` | none found | none found |
| `AuxiliarySonarRequestSignal` | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentContracts.cs:196` | none found | none found |
| `AuxiliaryTetherConnectionSignal` | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentContracts.cs:211` | none found | none found |
| `BaseIntegrityEventPayload` | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorTypes.cs:59` | none found | none found |
| `BaseModuleCompromisedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10355` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6421`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1244` | none found |
| `BatteryLevelSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9870` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6862`<br>`Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:631` | none found |
| `BiomeChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8619` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7227` | `Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:681`<br>`Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:824`<br>`Assets/_Project/Scripts/Graphics/Materials/ProceduralFloraBiomeTintBridge.cs:70`<br>`Assets/_Project/Scripts/PlayerInventory.cs:4366`<br>`Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs:1672` |
| `BiomeGradientSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8630` | `Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs:363` | `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:1426`<br>`Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs:789`<br>`Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:1319`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:2110` |
| `BlueprintUnlockedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9530` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6777` | none found |
| `BrownoutSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8761` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6477`<br>`Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1476`<br>`Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:498` | `Assets/_Project/Scripts/SpatialAudioManager.cs:7702` |
| `BubbleSpawnSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8563` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7206`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1525` | none found |
| `BulletTimeVisualSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:173` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7069` | none found |
| `CameraFrustumSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10224` | `Assets/_Project/Scripts/Core/SystemDispatcher.cs:6766` | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1079`<br>`Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1512` |
| `CameraJuiceImpactSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:911` | `Assets/_Project/Scripts/Core/CameraJuiceSignals.cs:44` | none found |
| `CameraPositionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10212` | `Assets/_Project/Scripts/Core/SystemDispatcher.cs:6755` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1706`<br>`Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:596`<br>`Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1066` |
| `CardiacPulseSignal` | `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyData.cs:337` | none found | none found |
| `CavitationAcousticSignal` | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:272` | none found | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1097` |
| `ChunkDehydratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9311` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4696` | none found |
| `CombatDamageSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10240` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6353`<br>`Assets/_Project/Scripts/Core/PlayerRuntimeContext.cs:272`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1866` | `Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:810`<br>`Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:956`<br>`Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1053`<br>`Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:961`<br>`Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:697`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1120`<br>`Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageRuntime.cs:494`<br>`Assets/_Project/Scripts/Vehicles/VFX/HullDentShaderController.cs:171`<br>`Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs:1303` |
| `CombatDamageSignalAupShiftTransformer` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10476` | none found | none found |
| `CompassCalibratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8944` | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:110` | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:836` |
| `ComplianceViolationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10013` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6998` | none found |
| `ConstructionPreviewSignal` | `Assets/_Project/Scripts/Construction/ConstructionSignals.cs:14` | none found | `Assets/_Project/Scripts/Construction/HectonBlueprintPreviewBatch.cs:319` |
| `ControlSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8877` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6526` | none found |
| `CoreCombatDamageSignal` | not found as local `ISignal` declaration | none found | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:1241` |
| `CoreHackedSignal` | `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs:40` | none found | `Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:931` |
| `CoreTetherFiredSignal` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/Physics/TetherSignals.cs:69` | none found |
| `CpuStarvationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9212` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6645` | none found |
| `CraftingCompletedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9560` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6797` | `Assets/_Project/Scripts/PDA/PDALogbookManager.cs:616` |
| `CraftingStartedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9545` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6784` | none found |
| `CrashTelemetrySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8983` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6547` | none found |
| `CrushWarningSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9069` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6589` | none found |
| `CullingOverloadSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:61` | `Assets/_Project/Scripts/Core/InstanceCullingServiceRegistryBridge.cs:74` | none found |
| `DataReloadSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9115` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6610` | none found |
| `DataVaultUpdateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:299` | `Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs:107`<br>`Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs:197`<br>`Assets/_Project/Scripts/Core/Bridge/H8InputMappingFacade.cs:161`<br>`Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistryRuntimeBinder.cs:165` | none found |
| `DebrisAvalancheSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3938` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1776` | none found |
| `DebrisDestroyedSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:261` | none found | none found |
| `DebrisSpawnSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8776` | `Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:1216`<br>`Assets/_Project/Scripts/Construction/DroneFleetManager.cs:3282`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:6484`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1123`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1794`<br>`Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1480`<br>`Assets/_Project/Scripts/Gameplay/HarvestableOutcrop.cs:260`<br>`Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs:1393`<br>`Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs:1203`<br>`Assets/_Project/Scripts/Gameplay/VehicleMotor.cs:1775`<br>`Assets/_Project/Scripts/RepairTool.cs:1038`<br>`Assets/_Project/Scripts/ResourceNode.cs:947`<br>... +5 more | `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:871` |
| `DebugSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:392` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeDebugSignal.cs:22` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:580` |
| `DeconstructRequestSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9012` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6561` | none found |
| `DeconstructResultSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9028` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6568` | none found |
| `DeferredSubmarineImpactSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1178` | `Assets/_Project/Scripts/PhysicsApplySystem.cs:2784`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:2842` | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:424`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:2797` |
| `DeflectSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8796` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6491` | none found |
| `DeltaCrusherMockLaserFireSignal` | `Assets/_Project/Scripts/VFX/Debris/ShinobuDeltaCrusherJobs.cs:29` | none found | none found |
| `DesyncDetectedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:573` | `Assets/_Project/Scripts/Core/Signals/CoreDeterminismSignals.cs:76` | none found |
| `DiegeticHudSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9776` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6848` | none found |
| `DirectorAIMusicSignal` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:465` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:466`<br>`Assets/_Project/Scripts/HectonDirectorAI.cs:247` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:408`<br>`Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:1459` |
| `DockingCompleteSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:803` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:2610`<br>`Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:1501` | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1060` |
| `DockingFailedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:824` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:2643`<br>`Assets/_Project/Scripts/Construction/DroneFleetManager.cs:2661`<br>`Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:1532` | none found |
| `DockingRequestSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:782` | none found | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:2266` |
| `DroneFleetInventoryTransactionSignal` | `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs:724` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:2843`<br>`Assets/_Project/Scripts/Construction/DroneFleetManager.cs:3030` | none found |
| `DroneFleetMockMiningSignal` | `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs:713` | none found | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:3590` |
| `DroneFleetMockRepairSignal` | `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs:702` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:3000` | none found |
| `DropPodLandedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8522` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6463` | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:1608` |
| `DynamicMusicScalarSignal` | `Assets/_Project/Scripts/Core/Contracts/Signals/DynamicMusicScalarSignal.cs:9` | none found | `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:938` |
| `EclipseGameplayEventPayload` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3922` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:2080` | none found |
| `EncumbranceSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:224` | none found | none found |
| `EncyclopediaUnlockSignal` | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:2643` | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:984` | none found |
| `EntityDeathSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8818` | `Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs:669`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:6498` | `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs:1604`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:5267` |
| `EntityDepletedSignal` | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:2657` | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1009` | none found |
| `EntitySpawnSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8831` | `Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:393`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:6505` | none found |
| `EquipItemSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:237` | none found | none found |
| `EquipmentOverheatSignal` | `Assets/_Project/Scripts/Tools/EquipmentThermalBatteryContracts.cs:84` | none found | none found |
| `ExosuitAcousticEchoTap` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsContracts.cs:85` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:624` | none found |
| `FabricationCompletedSignal` | `Assets/_Project/Scripts/FabricationAssemblerRuntime.cs:72` | none found | none found |
| `FabricationTickSignal` | `Assets/_Project/Scripts/FabricationAssemblerRuntime.cs:88` | none found | none found |
| `FaunaStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10141` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7150` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1724`<br>`Assets/_Project/Scripts/Fauna/FaunaBrain.cs:4180`<br>`Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1400` |
| `FloraExclusionSignal` | `Assets/_Project/Scripts/Construction/ConstructionSignals.cs:45` | none found | none found |
| `FloraSpawnedSignal` | `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeContracts.cs:641` | none found | none found |
| `FluidDensityChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9432` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6726` | none found |
| `FluidImpulseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8544` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7199`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1549`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:5051`<br>`Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:1350`<br>`Assets/_Project/Scripts/World/FloraInteractionManager.cs:4248` | none found |
| `FluidIncursionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9396` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6710`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1243` | `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:705` |
| `FocusBrokenSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8690` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7241` | none found |
| `FramePacingWarningSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10453` | `Assets/_Project/Scripts/Core/SystemDispatcher.cs:5139` | none found |
| `FrameTimeSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:15` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:851` | `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs:1650`<br>`Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1281`<br>`Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:2051` |
| `GlobalPanicSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3982` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1757` | none found |
| `GlobalTimeSyncSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:129` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7005` | none found |
| `GlobalWorldStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8598` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7220` | none found |
| `HUDNotificationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9761` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6841`<br>`Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:554`<br>`Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:2432` | none found |
| `HabitatConstructionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8999` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6554` | none found |
| `HabitatFloodAcousticMuffleSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:77` | `Assets/_Project/Scripts/AcousticZoneController.cs:107`<br>`Assets/_Project/Scripts/Physics/HabitatFluidIncursionDirector.cs:720` | none found |
| `HapticPulseSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:236` | none found | none found |
| `HapticRequest` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8222` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6379`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1468`<br>`Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:595`<br>`Assets/_Project/Scripts/Tools/WfcLaserCutRuntime.cs:409` | none found |
| `HashDeltaUpdateSignal` | `Assets/_Project/Scripts/Core/Contracts/MemorySentinelSignals.cs:30` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:401` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1244` |
| `HighSpeedImpactSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8178` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6372` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:243`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:3273`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2509`<br>`Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:609`<br>`Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:1559`<br>`Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs:1270` |
| `HullDeformedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10313` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6408`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1937`<br>`Assets/_Project/Scripts/Vehicles/VFX/HullDentShaderController.cs:594` | `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:965`<br>`Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:1228`<br>`Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:1542` |
| `HullRepairedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10338` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:3119`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:6415`<br>`Assets/_Project/Scripts/RepairTool.cs:1515` | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1162` |
| `HypoxiaSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9339` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6682` | none found |
| `ImpactSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8161` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6365`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1826`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:5017`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:5087` | `Assets/_Project/Scripts/World/SoundscapeSystem.cs:739` |
| `InputSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:507` | `Assets/_Project/Scripts/Core/Signals/CoreDeterminismSignals.cs:64` | none found |
| `InputStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:967` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:625` | `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:742`<br>`Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:598`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1665`<br>`Assets/_Project/Scripts/UI/InteractionUI.cs:908`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5842` |
| `InteractionUiSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9369` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6696` | none found |
| `InventoryChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8383` | `Assets/_Project/Scripts/PlayerInventory.cs:4216` | `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:669`<br>`Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:866`<br>`Assets/_Project/Scripts/HectonPlayerMovement.cs:4688`<br>`Assets/_Project/Scripts/PDAInventoryTab.cs:449`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:373`<br>`Assets/_Project/Scripts/PlayerToolManager.cs:1980`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:2839`<br>`Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs:279`<br>`Assets/_Project/Scripts/UI/PDAConstructionTab.cs:326`<br>`Assets/_Project/Scripts/UI/PDALoadoutTab.cs:288`<br>`Assets/_Project/Scripts/UI/PDAShellChrome.cs:537`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5820` |
| `InventoryCommandSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8367` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:3031`<br>`Assets/_Project/Scripts/PlayerInventory.cs:2090` | `Assets/_Project/Scripts/PlayerInventory.cs:2158` |
| `ItemAcquiredSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8418` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7104`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:7122`<br>`Assets/_Project/Scripts/VoxelDeltaProcessor.cs:3515` | `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs:1615`<br>`Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:622`<br>`Assets/_Project/Scripts/PlayerInventory.cs:4380`<br>`Assets/_Project/Scripts/PlayerInventory.cs:4405`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:5278` |
| `ItemDecaySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10057` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7097` | none found |
| `ItemDurabilityChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8397` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6869`<br>`Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs:952` | `Assets/_Project/Scripts/HUDQuickBar.cs:337`<br>`Assets/_Project/Scripts/PlayerToolManager.cs:2012`<br>`Assets/_Project/Scripts/UI/PDALoadoutTab.cs:307`<br>`Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:292` |
| `KccVelocitySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:631` | `Assets/_Project/Scripts/Core/Signals/CoreDeterminismSignals.cs:104`<br>`Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1515` | `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs:1217`<br>`Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs:835` |
| `KillSwitchSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:30` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:864` | none found |
| `LaserCutterEventPayload` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1047` | none found | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:247`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:4840` |
| `LaserCutterEventPayloadSignal` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/LaserCutter.cs:288`<br>`Assets/_Project/Scripts/LaserCutter.cs:300` | `Assets/_Project/Scripts/LaserCutter.cs:115` |
| `LightLevelSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10080` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7136` | none found |
| `LocalizationLanguageChangedSignal` | `Assets/_Project/Scripts/LocRegistry.cs:442` | `Assets/_Project/Scripts/LocRegistry.cs:1678` | none found |
| `LockstepSnapshotSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:985` | `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:994` | none found |
| `LogisticsTransferSignal` | `Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs:137` | none found | none found |
| `LoreFragmentScannedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9516` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6770` | `Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs:868` |
| `MacroCollisionSignal` | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1142` | none found | none found |
| `MacroDatabaseSectorHydrationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9913` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6970` | `Assets/_Project/Scripts/SaveManager.cs:1406`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:3899`<br>`Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:540` |
| `ManualOverridePulledSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9736` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6949` | none found |
| `MechHapticSignalDTO` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsContracts.cs:50` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:584` | none found |
| `MemoryAddressShiftSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9144` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6624` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/VaultMemoryGizmoVisualizer.cs:41`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:2023` |
| `MemoryDesyncSignal` | `Assets/_Project/Scripts/Core/Contracts/MemorySentinelSignals.cs:6` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1443`<br>`Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1472` | none found |
| `MemoryPressureSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9130` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6617` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:703`<br>`Assets/_Project/Scripts/UI/PDAShellChrome.cs:508`<br>`Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4085` |
| `MemorySentinelRollbackSignal` | `Assets/_Project/Scripts/Core/Contracts/MemorySentinelSignals.cs:51` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1458` | none found |
| `MixerStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8706` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7248` | none found |
| `MockAcousticSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:183` | none found | none found |
| `MockAupRebaseSignal` | `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs:16` | none found | none found |
| `MockCarveRequestSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:190` | none found | none found |
| `MockCombatDamageSignal` | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:389` | none found | none found |
| `MockConsumeSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:188` | none found | none found |
| `MockCraftingRequestSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:176` | none found | none found |
| `MockDamageSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:197` | none found | none found |
| `MockFloodSignal` | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:237` | none found | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:414` |
| `MockHotbarSelectSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:249` | none found | none found |
| `MockHudSignal` | `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs:54` | none found | none found |
| `MockImpactSignal` | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:255` | none found | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:439` |
| `MockInventoryTransactionSignal` | `Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:294` | `Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:2422` | none found |
| `MockItemAcquiredSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:164` | none found | none found |
| `MockLaserFireSignal` | `Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:77` | none found | none found |
| `MockNarrativeTriggerSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3887` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1680` | none found |
| `MockPlayerFootstepSignal` | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1071` | none found | none found |
| `MockPlayerPositionSignal` | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:116` | none found | none found |
| `MockPredatorSignal` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:3275` | none found | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1742` |
| `MockQualityWeightSignal` | `Assets/_Project/Scripts/Core/Contracts/DrsContracts.cs:123` | none found | none found |
| `MockReconstructionInputSignal` | `Assets/_Project/Scripts/Core/Contracts/DrsContracts.cs:155` | none found | none found |
| `MockRockCollisionSignal` | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1125` | none found | none found |
| `MockStoryEventSignal` | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:101` | none found | none found |
| `MockTextRequestSignal` | `Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:1687` | none found | none found |
| `MockToolUsedSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:200` | none found | none found |
| `MockTriggerPullSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:181` | none found | none found |
| `ModAssetReferenceSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:169` | none found | none found |
| `ModFutureDevNullSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:211` | none found | none found |
| `ModInteractionRejectedPayload` | `Assets/_Project/Scripts/ModdingAPI/ModSpatialContracts.cs:242` | none found | none found |
| `ModSpawnRequestSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:156` | none found | none found |
| `ModdedGameMaskSignal` | `Assets/_Project/Scripts/Core/Contracts/MemorySentinelSignals.cs:68` | none found | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:654` |
| `ModuleDeconstructSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9041` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6575` | none found |
| `MovementAcousticSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9254` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6661` | `Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:719`<br>`Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:707`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6048` |
| `NarrativeFocusSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8661` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7234` | none found |
| `NarrativeHudWaypointSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8723` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7255` | none found |
| `NarrativePoiStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8747` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7269` | `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1507`<br>`Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:836` |
| `OxygenCriticalSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9354` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6689` | none found |
| `PdaExchangeStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9815` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6904` | `Assets/_Project/Scripts/UI/PDABarterTab.cs:198`<br>`Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:875` |
| `PhysicsEventPayload` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1132` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:245`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:5020`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:923`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:957` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:122`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:244`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:4981`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:763`<br>`Assets/_Project/Scripts/SpatialAudioManager.cs:5055` |
| `PhysiologyStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10154` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7164` | none found |
| `PipeRuptureSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9445` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6733` | none found |
| `PlasmaBeamAcousticEchoTap` | `Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:92` | none found | none found |
| `PlayVoiceOverSignal` | `Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:1700` | none found | none found |
| `PlayerActionCancelledSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8339` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6890` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:141` |
| `PlayerActionCompletedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8323` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6883` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:137` |
| `PlayerActionProgressSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8304` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6876` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:133` |
| `PlayerBaseEnterSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10379` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6428` | `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:765`<br>`Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:812` |
| `PlayerBaseExitSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10394` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6435` | `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:784`<br>`Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:800` |
| `PlayerExhaleSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:94` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10535` | `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:6071`<br>`Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:705`<br>`Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs:322` |
| `PlayerFatalPressureSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:124` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10559` | `Assets/_Project/Scripts/UI/HectonOSBootManager.cs:215`<br>`Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:726` |
| `PlayerFootstepSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:7` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10501` | `Assets/_Project/Scripts/PlayerFootstepAudio.cs:224` |
| `PlayerInputSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1230` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:3202` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:567`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:584`<br>`Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs:2191`<br>`Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs:2212`<br>`Assets/_Project/Scripts/HectonFabricatorUI.cs:620`<br>`Assets/_Project/Scripts/HectonFabricatorUI.cs:646`<br>`Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:314`<br>`Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:331`<br>`Assets/_Project/Scripts/MainMenuController.cs:897`<br>`Assets/_Project/Scripts/MainMenuController.cs:913`<br>`Assets/_Project/Scripts/PlayerBuilder.cs:709`<br>`Assets/_Project/Scripts/PlayerBuilder.cs:726`<br>... +7 more |
| `PlayerLookTargetSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1249` | `Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:611`<br>`Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:631` | `Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:456` |
| `PlayerRespawnSignal` | `Assets/_Project/Scripts/Core/Contracts/Signals/PlayerRespawnSignal.cs:28` | none found | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:1030`<br>`Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs:2008`<br>`Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs:194` |
| `PlayerSprintStateSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:109` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10547` | `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs:1209` |
| `PlayerStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8248` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6388` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1695`<br>`Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:588`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2079`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2532`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2633`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5803` |
| `PlayerStressSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10181` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7178` | `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2550`<br>`Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:321` |
| `PlayerTransportBailoutSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:136` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10575` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1396` |
| `PlayerWaterSplashSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:35` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10524` | `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:997`<br>`Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:713` |
| `PowerDrainSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9636` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6827` | none found |
| `PrefabAcousticSignatureSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:323` | `Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistry.cs:345`<br>`Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistryRuntimeBinder.cs:89` | none found |
| `PrefabLoreLinkSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:347` | `Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistry.cs:356`<br>`Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistryRuntimeBinder.cs:100` | none found |
| `ProgressionEventSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8585` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7213` | none found |
| `PrologueCompleteSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9719` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6942` | `Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:259`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:226`<br>`Assets/_Project/Scripts/HectonFluidEngine.cs:3013`<br>`Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:415` |
| `QuestDagMockItemAcquiredSignal` | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:132` | none found | none found |
| `RadarJamSignal` | `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs:25` | none found | none found |
| `RadiationDoseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8431` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7111`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:7125` | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:670`<br>`Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs:1150`<br>`Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:858` |
| `RadiationSourceSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8472` | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:99`<br>`Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:112` | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:646` |
| `ReactorDamageSignal` | `Assets/_Project/Scripts/Core/Contracts/Signals/ReactorDamageSignal.cs:13` | none found | `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:725` |
| `RebaseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8867` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6519` | none found |
| `ReconDataSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9885` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6956` | none found |
| `ReentryVfxStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:456` | `Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:770` | none found |
| `ResidencySectorDehydratedSignal` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4686` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4116`<br>`Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4740` |
| `ResidencySectorHydratedSignal` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4670` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4745` |
| `ResolutionChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9169` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6631`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:1636` | none found |
| `ResourceDepletionDeltaSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8488` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7118`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:7128` | none found |
| `RespawnSignalResolvedTargetTransformer` | `Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs:1266` | none found | none found |
| `RigidbodySleepSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9473` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6747` | none found |
| `SaveCompletedSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:92` | `Assets/_Project/Scripts/SaveManager.cs:2198` | none found |
| `SaveLifecycleSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9898` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6963` | none found |
| `SaveMetadataReadySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9987` | `Assets/_Project/Scripts/SaveThumbnailSystem.cs:965` | none found |
| `SaveStatusSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:108` | `Assets/_Project/Scripts/SaveManager.cs:2172` | none found |
| `ScalabilityChangedEvent` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:36` | `Assets/_Project/Scripts/Core/IPlatformIntegration.cs:137` | `Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:870`<br>`Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:840`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:131`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:271`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:447`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:477`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:2206`<br>`Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:171`<br>`Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:286`<br>`Assets/_Project/Scripts/Core/IPlatformIntegration.cs:148`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1732`<br>`Assets/_Project/Scripts/SpatialAudioManager.cs:1466`<br>... +1 more |
| `ScanCompleteSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9503` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6763` | `Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs:856` |
| `ScanLogChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9795` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6897` | `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:687`<br>`Assets/_Project/Scripts/PDA/PDALogbookManager.cs:638` |
| `ScannerToolActiveSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9486` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6756` | `Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:444`<br>`Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs:646` |
| `SectorDehydratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9296` | `Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:566` | `Assets/_Project/Scripts/World/EcosystemDirector.cs:3883` |
| `SectorResidencyHydratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9281` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:431`<br>`Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:536` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:271`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:3908` |
| `SeismicShockwaveSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3904` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1748` | none found |
| `SeismicSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10028` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7014` | `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs:1541` |
| `ShinobuPlayerExertionSignal` | `Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:19` | `Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1561` | none found |
| `SignalWardenMockDamageSignal` | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1104` | none found | none found |
| `SiltExplosionSignal` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsContracts.cs:74` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:609` | none found |
| `SimulationBucketSyncSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10438` | `Assets/_Project/Scripts/Core/SystemDispatcher.cs:5098` | none found |
| `SimulationPauseSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:158` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7041` | none found |
| `SolarFlareSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8852` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6512` | none found |
| `SonarPingSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9326` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6675` | none found |
| `SoundscapeProfileSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8736` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7262` | none found |
| `SpectrumScanSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9458` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6740` | none found |
| `SplashEvent` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1076` | `Assets/_Project/Scripts/Physics/FluidFeedbackListener.cs:155`<br>`Assets/_Project/Scripts/Physics/FluidFeedbackListener.cs:176` | `Assets/_Project/Scripts/Physics/FluidFeedbackListener.cs:122` |
| `StateChangedSignal` | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:86` | `Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs:1149` | none found |
| `StateCorrectionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:538` | `Assets/_Project/Scripts/Core/Signals/CoreDeterminismSignals.cs:70` | none found |
| `StorageDebtSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9666` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6921` | none found |
| `StreamingTurbulenceSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9687` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6928` | none found |
| `SubmarineFloodStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9409` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6717`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:2684` | `Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:962`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:405` |
| `SubmarineLightsChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10108` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7143` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:609`<br>`Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:3614`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:4229` |
| `SubtitleCueSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:249` | none found | `Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs:515` |
| `SubtitleSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9085` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6603` | `Assets/_Project/Scripts/UI/SubtitleManager.cs:778` |
| `SurvivalOverrideSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:224` | none found | none found |
| `SurvivalVitalsChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8288` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6401` | `Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:817`<br>`Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs:1629`<br>`Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:843`<br>`Assets/_Project/Scripts/UI/SuitAdvisoryController.cs:161`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:4564`<br>`Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:846`<br>`Assets/_Project/Scripts/Visor/VisorHUDController.cs:1456` |
| `SwarmDispersedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9268` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6668`<br>`Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:555`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6127` | none found |
| `SyncFenceSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:598` | `Assets/_Project/Scripts/Core/Signals/CoreDeterminismSignals.cs:85`<br>`Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1051` | none found |
| `SystemGlitchSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1007` | `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1092` | `Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:733` |
| `SystemHealthIndexSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9192` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6638`<br>`Assets/_Project/Scripts/Core/HomeostasisBrain.cs:878`<br>`Assets/_Project/Scripts/QA/Headless/Shinobu38QaWatchdogRuntime.cs:1072` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1764`<br>`Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:799`<br>`Assets/_Project/Scripts/Core/InputDispatcher.cs:3499`<br>`Assets/_Project/Scripts/Core/SystemDispatcher.cs:3120`<br>`Assets/_Project/Scripts/Core/SystemDispatcher.cs:4789`<br>`Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1422`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2603`<br>`Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:735`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:993`<br>`Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:595`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1741`<br>`Assets/_Project/Scripts/LaserCutter.cs:1615`<br>... +7 more |
| `SystemHealthSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:421` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:824` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1273`<br>`Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:585`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:848`<br>`Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:423`<br>`Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1078`<br>`Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1536`<br>`Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:851`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5860`<br>`Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:1842` |
| `SystemPauseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10424` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7049`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:7056`<br>`Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs:1077` | none found |
| `TelemetryAnomalySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8968` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6540` | none found |
| `TemperatureChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8444` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6470` | none found |
| `TerminalClickSignal` | `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:156` | none found | none found |
| `TerminalCommandSignal` | `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:167` | none found | none found |
| `TetherFiredSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:751` | none found | none found |
| `TetherSnappedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:714` | `Assets/_Project/Scripts/Physics/TetherSignals.cs:76` | `Assets/_Project/Scripts/Physics/TetherSignals.cs:89` |
| `TetherTensionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:669` | `Assets/_Project/Scripts/Physics/TetherSignals.cs:83` | none found |
| `ThermalSourceSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8461` | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs:3211` | `Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.cs:514` |
| `ThermalStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9853` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6855`<br>`Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:618` | `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:602`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:878`<br>`Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:445`<br>`Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs:1542` |
| `ThermalUpdraftSignal` | `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardTypes.cs:77` | `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:923` | none found |
| `ThermodynamicsMockDamageSignal` | `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardTypes.cs:96` | none found | none found |
| `TimeDilationSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:143` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7028` | none found |
| `ToolAcousticSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9617` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6820`<br>`Assets/_Project/Scripts/Tools/WfcLaserCutRuntime.cs:397`<br>`Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:355` | none found |
| `ToolBrokenSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:212` | none found | none found |
| `ToolDepletedSignal` | `Assets/_Project/Scripts/Tools/EquipmentThermalBatteryContracts.cs:99` | none found | none found |
| `ToolHeatSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:205` | none found | none found |
| `ToolLoadoutChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9594` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6813` | `Assets/_Project/Scripts/Gameplay/PlayerTransportCoordinator.cs:344`<br>`Assets/_Project/Scripts/HUDQuickBar.cs:313`<br>`Assets/_Project/Scripts/PDAInventoryTab.cs:518`<br>`Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs:320`<br>`Assets/_Project/Scripts/UI/PDAConstructionTab.cs:367`<br>`Assets/_Project/Scripts/UI/PDALoadoutTab.cs:342`<br>`Assets/_Project/Scripts/UI/PDAShellChrome.cs:577` |
| `ToolStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9574` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6806` | none found |
| `ToolTriggerSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9651` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6834` | none found |
| `ToxicBioluminescenceSignal` | `Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryTypes.cs:160` | none found | none found |
| `ToxicityExposureSignal` | `Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryTypes.cs:144` | none found | none found |
| `TraumaSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10196` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7185` | none found |
| `UIRescaleRequestSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9382` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6703` | none found |
| `VehicleHazardSignal` | `Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageContracts.cs:197` | none found | none found |
| `VehicleUpgradesChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9836` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6911` | none found |
| `VfxSparkRequestSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:217` | none found | none found |
| `VisorBreachSignal` | `Assets/_Project/Scripts/Visor/DiegeticVisorLensTypes.cs:110` | none found | none found |
| `VisorDropletSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:485` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10595`<br>`Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:742` | `Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs:352` |
| `VisualFlareSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:885` | `Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:593` | none found |
| `VisualScavengeSignal` | `Assets/_Project/Scripts/Core/Contracts/Signals/VisualScavengeSignal.cs:23` | none found | none found |
| `VitalWarningSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9054` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6582` | none found |
| `VocalWarningSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9100` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6596` | none found |
| `VoxelCarveEvent` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:845` | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs:1218` | `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:832` |
| `WakeGeneratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8535` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7192`<br>`Assets/_Project/Scripts/Gameplay/VehicleMotor.cs:1287`<br>`Assets/_Project/Scripts/World/FloraInteractionManager.cs:2760` | `Assets/_Project/Scripts/World/FloraInteractionManager.cs:2765` |
| `WakeRequestSignal` | `Assets/_Project/Scripts/Core/Signals/PhysicsWakeSignalContracts.cs:12` | `Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs:931` | none found |
| `WaterTransitionSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:70` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10620` | `Assets/_Project/Scripts/Gameplay/WaterTransitionHandler.cs:60` |
| `WaterlineBreachSignal` | `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereContracts.cs:1483` | `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1457` | `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:974` |
| `WeatherChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10409` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7090` | none found |
| `WeatherStrengthSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:10042` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7081` | none found |
| `WfcOutpostDoorPowerSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9964` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6991`<br>`Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:477` | `Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:946` |
| `WfcOutpostGeneratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9927` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6977`<br>`Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:1305` | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:996`<br>`Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:235`<br>`Assets/_Project/Scripts/Tools/WfcLaserCutRuntime.cs:246` |
| `WfcOutpostStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9948` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6984` | `Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs:110`<br>`Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1028`<br>`Assets/_Project/Scripts/SaveManager.cs:1229` |

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
| `Docs/AgentLogs/Rationale_SHINOBU_155.md` | 23 | Problem: `HectonPlayerHealth.Die()` has no reload path to delete, but it also has no authoritative AUP/physiology reset and leaves death as a managed event/TODO. |
| `Docs/AgentLogs/Rationale_SHINOBU_201.md` | 283 | Rejected Alternatives: Keeping `sqrt` for readability was rejected by the i3/NEON mandate. Quake-style bit hacks were rejected by mandate. Lookup tables were rejected for this d... |

## PHI Self-Audit

Exact PHI_SYN rationale file is absent from active logs. Near-match H-Phi rationale files were scanned as supporting evidence, but this is not treated as the exact requested artifact.

Near-match active logs:

| H-Phi / UX signal | Declared at | Producers | Consumers |
|---|---|---|---|
| `PlayerActionProgressSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8304` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6876` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:133` |
| `PlayerActionCompletedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8323` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6883` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:137` |
| `PlayerActionCancelledSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8339` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6890` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:141` |
| `PdaExchangeStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9815` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6904` | `Assets/_Project/Scripts/UI/PDABarterTab.cs:198`<br>`Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:875` |
| `VehicleUpgradesChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:9836` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6911` | none found |
| `PlayerStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8248` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6388` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1695`<br>`Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:588`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2079`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2532`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2633`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5803` |
| `InventoryChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8383` | `Assets/_Project/Scripts/PlayerInventory.cs:4216` | `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:669`<br>`Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:866`<br>`Assets/_Project/Scripts/HectonPlayerMovement.cs:4688`<br>`Assets/_Project/Scripts/PDAInventoryTab.cs:449`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:373`<br>`Assets/_Project/Scripts/PlayerToolManager.cs:1980`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:2839`<br>`Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs:279` |
| `InputStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:967` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:625` | `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:742`<br>`Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:598`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1665`<br>`Assets/_Project/Scripts/UI/InteractionUI.cs:908`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5842` |
| `SystemHealthSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:421` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:824` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1273`<br>`Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:585`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:848`<br>`Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:423`<br>`Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1078`<br>`Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1536`<br>`Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:851`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5860` |

## Phi-Resonance Connectivity Model

The engine connectivity model is not mystical. It is a three-layer resonance model: contracts define stable frequency, typed signal lanes carry bounded pulses, and dispatcher phases control when state can move. `GlobalRegistry` is the cold authority spine for stable interfaces. `SignalBus<T>` and queue-backed event lanes are the nervous system for broadcast state. DataVault/native buffers are the mass: owned memory that simulation mutates and presentation samples.

Low/MX350 interpretation: coalesce lanes, consume bounded snapshots, keep optional visual consumers in `VISUAL_SYNC`, and shed mips/LOD before simulation truth. High/Ultra interpretation: do not increase gameplay broadcast cost; attach richer presentation consumers to the same snapshots and spend the saved budget on light, fog, HUD, audio, and material overkill.

Connectivity risk: `Hecton8.Core` currently references many domain assemblies directly. That may be intentional current architecture, but it is not a clean inward-only core. The integrator should treat this as a dependency inversion watchpoint, especially while many agents expand contracts in parallel.

## Verification Commands

- `python Tools/BuildArchitectureAtlas.py`
- `python Tools/AtlasCheck.py`
- `python -m py_compile Tools/BuildArchitectureAtlas.py Tools/AtlasCheck.py`
- C# compile verification is outside this atlas; run Unity import/Console and serial CLI builds as separate evidence.
- Current DOC_GLOBAL R47 blocker: `python Tools/AtlasCheck.py` still exits `1` with `ATLAS_CHECK_FAIL references=6781 missing=61`; missing refs include one Dynamic Decals vendor asset reference, RealtimeCSG vendor icon/readme image references, `Assets/_Project/Scripts/Editor/HectonMaskChannelPacker.cs`, `Assets/_Project/Scripts/Editor/HectonMaterialChannelPackValidator.cs`, and `Assets/_Project/Scripts/Habitat/Deformation/Editor/HabitatDamageBakePipeline.cs` until the references are restored or the atlas check excludes that evidence class deliberately.
- This generated atlas is not `VERIFIED` unless `Tools/AtlasCheck.py` exits `0` after generation.

## Residual Risk

- STATIC_SOURCE only: Unity import, runtime wiring, actual VRAM residency, profiler frame time, GC, build, and Play Mode remain PENDING VERIFICATION.
- The signal producer map only resolves explicit `SignalBus<T>` calls. Legacy `GlobalSignals.Publish(...)` variable publishes require Roslyn-level dataflow to type-resolve fully.
- Active logs can change while this atlas is being written because the workspace is multi-agent.
