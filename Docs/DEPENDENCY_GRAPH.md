# HECTON-8 Architecture Atlas - Dependency Graph

Generated: 2026-05-19 03:18:54
Date: 2026-05-19
Status: ATLAS GENERATED STATIC SOURCE / ATLASCHECK SEPARATE GATE REQUIRED / RUNTIME PENDING
Evidence class: STATIC_SOURCE / STATIC_DOC / FILESYSTEM / PY_TOOL. No Unity Editor, Play Mode, Memory Profiler, Frame Debugger, or player build evidence is claimed here.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
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

- C# source files scanned under `Assets/` and `Packages/`: 5083
- C# line count scanned under `Assets/` and `Packages/`: 1,859,300
- First-party C# source files under `Assets/_Project/Scripts/`: 1761
- First-party C# line count under `Assets/_Project/Scripts/`: 1,186,328
- Assembly definitions scanned: 183
- First-party assembly definitions under `Assets/_Project/`: 123
- Markdown docs under `Docs/`: 2380

## Assembly Dependency Graph

Core assembly: `Assets/_Project/Scripts/Hecton8.Core.asmdef`

`Hecton8.Core` direct references currently recorded in its asmdef:
- `Hecton8.Core.Contracts`
- `Hecton8.Animation.IK`
- `Hecton8.Core.Database`
- `Hecton8.Inventory.Algorithms`
- `Hecton8.Inventory.Corrosion`
- `Hecton8.Inventory.Corrosion.Contracts`
- `Hecton8.Core.Scheduling`
- `Hecton8.Core.Bucketing`
- `Hecton8.Core.Persistence.Paging`
- `Hecton8.Core.Memory`
- `Hecton8.UI.Diegetic.Contracts`
- `Hecton8.Bootstrap.Contracts`
- `Hecton8.Environment.Fluids`
- `Hecton8.Environment.Fluids.Contracts`
- `Hecton8.World.Contracts`
- `Hecton8.World.Terrain`
- `Hecton8.AI.Cognition`
- `Hecton8.AI.Ecology.Migration`
- `Hecton8.Physics.Determinism`
- `Hecton8.Physics.CCD`
- `Hecton8.Physics.Tethers.Contracts`
- `Hecton8.Vehicles.Physics.Contracts`
- `Hecton8.Audio.Propagation`
- `Hecton8.Audio.Virtualization.Contracts`
- `Hecton8.Audio.Virtualization`
- `Hecton8.Audio.Echolocation`
- `Hecton8.Logistics`
- `Hecton8.Logistics.Grid.Contracts`
- `Hecton8.Logistics.Grid`
- `Hecton8.Cartography`
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

Assemblies directly depending on exact `Hecton8.Core`: 57

| Assembly | Path |
|---|---|
| `Hecton8.AI.Ambient` | `Assets/_Project/Scripts/AI/Ambient/Hecton8.AI.Ambient.asmdef` |
| `Hecton8.AI.Cognition.Editor` | `Assets/_Project/Scripts/AI/Cognition/Editor/Hecton8.AI.Cognition.Editor.asmdef` |
| `Hecton8.AI.Pathfinding` | `Assets/_Project/Scripts/AI/Pathfinding/Hecton8.AI.Pathfinding.asmdef` |
| `Hecton8.Animation.FaunaProcedural` | `Assets/_Project/Scripts/Animation/FaunaProcedural/Hecton8.Animation.FaunaProcedural.asmdef` |
| `Hecton8.Animation.FaunaProcedural.Editor` | `Assets/_Project/Scripts/Animation/FaunaProcedural/Editor/Hecton8.Animation.FaunaProcedural.Editor.asmdef` |
| `Hecton8.Audio.Prologue` | `Assets/_Project/Scripts/Audio/Prologue/Hecton8.Audio.Prologue.asmdef` |
| `Hecton8.Core.Bridge.Editor` | `Assets/_Project/Scripts/Core/Bridge/Editor/Hecton8.Core.Bridge.Editor.asmdef` |
| `Hecton8.Core.Content.Editor` | `Assets/_Project/Scripts/Core/Content/Editor/Hecton8.Core.Content.Editor.asmdef` |
| `Hecton8.Core.Hardware` | `Assets/_Project/Scripts/Core/Hardware/Hecton8.Core.Hardware.asmdef` |
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
| `Hecton8.World.Outposts` | `Assets/_Project/Scripts/World/Outposts/Hecton8.World.Outposts.asmdef` |
| `Hecton8.World.ShinobuBiomimetic` | `Assets/_Project/Scripts/World/ShinobuBiomimetic/Hecton8.World.ShinobuBiomimetic.asmdef` |
| `Hecton8.World.ShinobuBiomimetic.Editor` | `Assets/_Project/Scripts/World/ShinobuBiomimetic/Editor/Hecton8.World.ShinobuBiomimetic.Editor.asmdef` |
| `Hecton8.World.Streaming` | `Assets/_Project/Scripts/World/Streaming/Hecton8.World.Streaming.asmdef` |
| `Hecton8.World.VoxelSurfaceNets.Editor` | `Assets/_Project/Scripts/World/VoxelSurfaceNets/Editor/Hecton8.World.VoxelSurfaceNets.Editor.asmdef` |

Assemblies depending on any `Hecton8.Core*` assembly: 87

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
| `Hecton8.Audio.Synthesis` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Audio/Synthesis/Hecton8.Audio.Synthesis.asmdef` |
| `Hecton8.Audio.Virtualization` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Audio/Virtualization/Hecton8.Audio.Virtualization.asmdef` |
| `Hecton8.Audio.Virtualization.Contracts` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Audio/Virtualization/Contracts/Hecton8.Audio.Virtualization.Contracts.asmdef` |
| `Hecton8.Cartography` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Cartography/Hecton8.Cartography.asmdef` |
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
| `Hecton8.World.Outposts` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/World/Outposts/Hecton8.World.Outposts.asmdef` |
| `Hecton8.World.ShinobuBiomimetic` | `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/ShinobuBiomimetic/Hecton8.World.ShinobuBiomimetic.asmdef` |
| `Hecton8.World.ShinobuBiomimetic.Editor` | `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/ShinobuBiomimetic/Editor/Hecton8.World.ShinobuBiomimetic.Editor.asmdef` |
| `Hecton8.World.Streaming` | `Hecton8.Core` | `Assets/_Project/Scripts/World/Streaming/Hecton8.World.Streaming.asmdef` |
| `Hecton8.World.VoxelSurfaceNets` | `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/VoxelSurfaceNets/Hecton8.World.VoxelSurfaceNets.asmdef` |
| `Hecton8.World.VoxelSurfaceNets.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/VoxelSurfaceNets/Editor/Hecton8.World.VoxelSurfaceNets.Editor.asmdef` |

### Domain Namespace Edges

Static `using Hecton8.*` edges from first-party source. This exposes compile-time namespace pressure, not runtime coupling proof.

| From domain | To domain | Using count |
|---|---|---:|
| `RootScripts` | `Core` | 252 |
| `World` | `Core` | 149 |
| `Gameplay` | `Core` | 141 |
| `UI` | `Core` | 121 |
| `RootScripts` | `World` | 88 |
| `Editor` | `World` | 63 |
| `RootScripts` | `Gameplay` | 62 |
| `Editor` | `Core` | 54 |
| `Gameplay` | `World` | 42 |
| `Construction` | `Core` | 41 |
| `Visor` | `Core` | 41 |
| `RootScripts` | `Items` | 38 |
| `RootScripts` | `Bootstrap` | 37 |
| `RootScripts` | `Physics` | 33 |
| `AI` | `Core` | 33 |
| `RootScripts` | `Environment` | 32 |
| `World` | `Gameplay` | 31 |
| `RootScripts` | `Inventory` | 30 |
| `Fauna` | `Core` | 29 |
| `UI` | `Gameplay` | 29 |
| `World` | `Environment` | 29 |
| `Gameplay` | `Physics` | 28 |
| `Atmosphere` | `Core` | 27 |
| `Audio` | `Core` | 27 |
| `RootScripts` | `SaveSystem` | 25 |
| `Physics` | `Core` | 25 |
| `UI` | `World` | 25 |
| `VFX` | `Core` | 24 |
| `RootScripts` | `Interaction` | 23 |
| `Gameplay` | `Audio` | 23 |
| `RootScripts` | `Building` | 21 |
| `Construction` | `Gameplay` | 21 |
| `Core` | `World` | 21 |
| `RootScripts` | `UI` | 20 |
| `RootScripts` | `Tools` | 20 |
| `Tools` | `Core` | 20 |
| `RootScripts` | `Construction` | 19 |
| `RootScripts` | `Caves` | 19 |
| `Construction` | `World` | 18 |
| `RootScripts` | `AI` | 18 |
| `Gameplay` | `Interaction` | 18 |
| `Graphics` | `Core` | 18 |
| `UI` | `Bootstrap` | 18 |
| `RootScripts` | `Audio` | 17 |
| `Editor` | `Gameplay` | 17 |
| `Interaction` | `Core` | 17 |
| `Optimization` | `Core` | 17 |
| `Construction` | `Power` | 15 |
| `Power` | `Core` | 15 |
| `Quest` | `Core` | 15 |
| `RootScripts` | `Atmosphere` | 14 |
| `ModdingAPI` | `Core` | 14 |
| `Plugins` | `Core` | 14 |
| `Bootstrap` | `Core` | 13 |
| `Editor` | `Items` | 13 |
| `Fauna` | `World` | 13 |
| `RootScripts` | `Input` | 12 |
| `Gameplay` | `Inventory` | 12 |
| `Gameplay` | `UI` | 12 |
| `SaveSystem` | `Core` | 12 |
| `World` | `Caves` | 12 |
| `World` | `AI` | 12 |
| `Animation` | `Core` | 11 |
| `Construction` | `Items` | 11 |
| `QA` | `Core` | 11 |
| `UI` | `Audio` | 11 |
| `Core` | `Gameplay` | 10 |
| `Core` | `Audio` | 10 |
| `Editor` | `Environment` | 10 |
| `Gameplay` | `Items` | 10 |
| `Gameplay` | `Bootstrap` | 10 |
| `Interaction` | `World` | 10 |
| `UI` | `Input` | 10 |
| `Visor` | `Gameplay` | 10 |
| `World` | `Bootstrap` | 10 |
| `Dev` | `Core` | 9 |
| `Gameplay` | `Tools` | 9 |
| `RootScripts` | `Optimization` | 9 |
| `Construction` | `Building` | 8 |
| `Construction` | `SaveSystem` | 8 |

## SignalBus<T> Flow Map

`ISignal` structs declared: 249. `SignalBus<T>` lanes observed in producer/consumer calls: 161. Union listed below: 254 signals.
Legacy `GlobalSignals.Publish(...)` call sites found: 255. Many use local variables and are not type-resolved by this static pass; they remain a migration/integrator risk.

| Signal | Declared at | Producers (`SignalBus<T>.Push/Publish`) | Consumers (`GetFrameSnapshot`) |
|---|---|---|---|
| `AcousticEchoTap` | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:194` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:618` | none found |
| `AcousticPingSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7507` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5184`<br>`Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:2440`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1285`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1446`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:703`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:4365`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5401` | `Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:659`<br>`Assets/_Project/Scripts/HectonBoidController.cs:1655`<br>`Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:1882`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:4349`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6067` |
| `AcousticShockwaveSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:2364` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1276` | none found |
| `AcousticZoneChangedEvent` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:649` | `Assets/_Project/Scripts/AcousticZoneController.cs:29`<br>`Assets/_Project/Scripts/AcousticZoneController.cs:93`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:651` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:404`<br>`Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:1339` |
| `AnomalyProximitySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7265` | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:104` | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:698` |
| `AnomalySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7253` | none found | none found |
| `ApexProximitySignal` | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:370` | none found | none found |
| `AtmosphericReentrySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7933` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5468` | `Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:219`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:193`<br>`Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:360` |
| `AudioEvent` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:639` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:633`<br>`Assets/_Project/Scripts/Audio/ProceduralAudioEvents.cs:589` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:249`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:5057` |
| `AupPreShiftSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6921` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4972` | `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2399` |
| `AupShiftSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6931` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4981` | `Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs:1001`<br>`Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1039`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:5993`<br>`Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1325`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:820`<br>`Assets/_Project/Scripts/HectonFluidEngine.cs:3235`<br>`Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:545`<br>`Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:489`<br>`Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:734`<br>`Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:1750`<br>`Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:1655`<br>`Assets/_Project/Scripts/Visor/SpectrumSystem.cs:2758`<br>... +5 more |
| `BaseModuleCompromisedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8457` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4944`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:743` | none found |
| `BatteryLevelSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8094` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5395`<br>`Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:631` | none found |
| `BiomeChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7022` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5764` | `Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:667`<br>`Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:798`<br>`Assets/_Project/Scripts/Graphics/Materials/ProceduralFloraBiomeTintBridge.cs:70`<br>`Assets/_Project/Scripts/PlayerInventory.cs:4145` |
| `BiomeGradientSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7033` | `Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs:370` | `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:1321`<br>`Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs:722`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:2108` |
| `BlueprintUnlockedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7781` | none found | none found |
| `BrownoutSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7140` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5003`<br>`Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1292`<br>`Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:498` | `Assets/_Project/Scripts/SpatialAudioManager.cs:7029` |
| `BubbleSpawnSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6973` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5742`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1519` | none found |
| `BulletTimeVisualSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:173` | none found | none found |
| `CameraFrustumSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8383` | `Assets/_Project/Scripts/Core/SystemDispatcher.cs:5540` | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1021`<br>`Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1512` |
| `CameraJuiceImpactSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:472` | `Assets/_Project/Scripts/Core/CameraJuiceSignals.cs:45` | none found |
| `CameraPositionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8373` | `Assets/_Project/Scripts/Core/SystemDispatcher.cs:5529` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1075`<br>`Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:595`<br>`Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1008` |
| `CardiacPulseSignal` | `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyData.cs:287` | none found | none found |
| `ChunkDehydratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7589` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4597` | none found |
| `CombatDamageSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8396` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4875`<br>`Assets/_Project/Scripts/Core/PlayerRuntimeContext.cs:267`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1334`<br>`Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:998` | `Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:784`<br>`Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:995`<br>`Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:682`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:620`<br>`Assets/_Project/Scripts/Vehicles/VFX/HullDentShaderController.cs:171` |
| `CombatDamageSignalAupShiftTransformer` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8567` | none found | none found |
| `CompassCalibratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7277` | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:81` | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:711` |
| `ComplianceViolationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8212` | none found | none found |
| `ConstructionPreviewSignal` | `Assets/_Project/Scripts/Construction/ConstructionSignals.cs:14` | none found | `Assets/_Project/Scripts/Construction/HectonBlueprintPreviewBatch.cs:337` |
| `ControlSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7240` | none found | none found |
| `CoreCombatDamageSignal` | not found as local `ISignal` declaration | none found | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:1240` |
| `CoreHackedSignal` | `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs:40` | none found | `Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:913` |
| `CoreTetherFiredSignal` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/Physics/TetherSignals.cs:69` | none found |
| `CpuStarvationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7494` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5174` | none found |
| `CraftingCompletedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7805` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5330` | `Assets/_Project/Scripts/PDA/PDALogbookManager.cs:616` |
| `CraftingStartedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7793` | none found | none found |
| `CrashTelemetrySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7299` | none found | none found |
| `CrushWarningSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7379` | none found | none found |
| `CullingOverloadSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:61` | `Assets/_Project/Scripts/Core/InstanceCullingServiceRegistryBridge.cs:75` | none found |
| `DataReloadSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7417` | none found | none found |
| `DataVaultUpdateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:144` | `Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs:105`<br>`Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs:192`<br>`Assets/_Project/Scripts/Core/Bridge/H8InputMappingFacade.cs:162`<br>`Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistryRuntimeBinder.cs:159` | none found |
| `DebrisAvalancheSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:2349` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1244` | none found |
| `DebrisDestroyedSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:213` | none found | none found |
| `DebrisSpawnSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7153` | `Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:1179`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:5011`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:805`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1262`<br>`Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:969`<br>`Assets/_Project/Scripts/Gameplay/HarvestableOutcrop.cs:250`<br>`Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs:1392`<br>`Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs:1188`<br>`Assets/_Project/Scripts/Gameplay/VehicleMotor.cs:1590`<br>`Assets/_Project/Scripts/RepairTool.cs:1031`<br>`Assets/_Project/Scripts/Tools/WfcLaserCutRuntime.cs:346`<br>`Assets/_Project/Scripts/VoxelDeltaProcessor.cs:3504`<br>... +3 more | `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:870` |
| `DebugSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:207` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeDebugSignal.cs:22` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:544` |
| `DeconstructRequestSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7325` | none found | none found |
| `DeconstructResultSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7340` | none found | none found |
| `DeferredSubmarineImpactSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:685` | `Assets/_Project/Scripts/PhysicsApplySystem.cs:2573`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:2631` | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:427`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:2586` |
| `DeflectSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7173` | none found | none found |
| `DesyncDetectedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:303` | `Assets/_Project/Scripts/Physics/PhysicsDeterminismSignals.cs:78` | none found |
| `DiegeticHudSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8004` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5381` | none found |
| `DirectorAIMusicSignal` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:465` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:466`<br>`Assets/_Project/Scripts/HectonDirectorAI.cs:247` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:408`<br>`Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:1354` |
| `DockingCompleteSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:406` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:2217`<br>`Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:1500` | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:824` |
| `DockingFailedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:421` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:2245`<br>`Assets/_Project/Scripts/Construction/DroneFleetManager.cs:2263`<br>`Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:1531` | none found |
| `DockingRequestSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:391` | none found | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:1920` |
| `DroneFleetInventoryTransactionSignal` | `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs:136` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:2437`<br>`Assets/_Project/Scripts/Construction/DroneFleetManager.cs:2615` | none found |
| `DroneFleetMockMiningSignal` | `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs:125` | none found | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:3053` |
| `DroneFleetMockRepairSignal` | `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs:114` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:2582` | none found |
| `DropPodLandedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6941` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4988` | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:527` |
| `EncumbranceSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:176` | none found | none found |
| `EncyclopediaUnlockSignal` | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1678` | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:640` | none found |
| `EntityDeathSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7187` | `Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs:654`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:5026` | `Assets/_Project/Scripts/World/EcosystemDirector.cs:5145` |
| `EntityDepletedSignal` | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1692` | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:665` | none found |
| `EntitySpawnSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7198` | `Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:383`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:5033` | none found |
| `EquipItemSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:189` | none found | none found |
| `FaunaStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8323` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5686` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1093`<br>`Assets/_Project/Scripts/Fauna/FaunaBrain.cs:4181`<br>`Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:890` |
| `FloraExclusionSignal` | `Assets/_Project/Scripts/Construction/ConstructionSignals.cs:37` | none found | none found |
| `FloraSpawnedSignal` | `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeContracts.cs:638` | none found | none found |
| `FluidDensityChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7694` | none found | none found |
| `FluidImpulseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6960` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5735`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1543`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:5032`<br>`Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:1349`<br>`Assets/_Project/Scripts/World/FloraInteractionManager.cs:2655` | none found |
| `FluidIncursionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7662` | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:742` | none found |
| `FocusBrokenSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7078` | none found | none found |
| `FramePacingWarningSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8547` | `Assets/_Project/Scripts/Core/SystemDispatcher.cs:3910` | none found |
| `FrameTimeSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:15` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:865` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1237`<br>`Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:1865` |
| `GlobalPanicSignal` | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:407` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1225` | none found |
| `GlobalTimeSyncSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:129` | none found | none found |
| `GlobalWorldStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7001` | none found | none found |
| `HUDNotificationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7992` | `Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:554`<br>`Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:2424` | none found |
| `HabitatConstructionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7313` | none found | none found |
| `HapticRequest` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6676` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4902`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1462`<br>`Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:589`<br>`Assets/_Project/Scripts/Tools/WfcLaserCutRuntime.cs:371` | none found |
| `HashDeltaUpdateSignal` | `Assets/_Project/Scripts/Core/Contracts/MemorySentinelSignals.cs:30` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:398` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1189` |
| `HighSpeedImpactSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6637` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4895` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:243`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:3263`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2460`<br>`Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:1400` |
| `HullDeformedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8417` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4931`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1106`<br>`Assets/_Project/Scripts/Vehicles/VFX/HullDentShaderController.cs:599` | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:1227`<br>`Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:1383` |
| `HullRepairedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8440` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4938`<br>`Assets/_Project/Scripts/RepairTool.cs:1504` | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:661` |
| `HypoxiaSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7615` | none found | none found |
| `ImpactSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6620` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4888`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1294`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:4998`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:5068` | `Assets/_Project/Scripts/World/SoundscapeSystem.cs:739` |
| `InputSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:275` | `Assets/_Project/Scripts/Physics/PhysicsDeterminismSignals.cs:66` | none found |
| `InputStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:522` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:616` | `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:589`<br>`Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:588`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1620`<br>`Assets/_Project/Scripts/UI/InteractionUI.cs:889`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5795` |
| `InteractionUiSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7639` | none found | none found |
| `InventoryChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6819` | `Assets/_Project/Scripts/PlayerInventory.cs:3995` | `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:669`<br>`Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:874`<br>`Assets/_Project/Scripts/HectonPlayerMovement.cs:4683`<br>`Assets/_Project/Scripts/PDAInventoryTab.cs:449`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:378`<br>`Assets/_Project/Scripts/PlayerToolManager.cs:1922`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:2829`<br>`Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs:205`<br>`Assets/_Project/Scripts/UI/PDAConstructionTab.cs:326`<br>`Assets/_Project/Scripts/UI/PDALoadoutTab.cs:288`<br>`Assets/_Project/Scripts/UI/PDAShellChrome.cs:509`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5773` |
| `InventoryCommandSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6808` | `Assets/_Project/Scripts/PlayerInventory.cs:2015` | `Assets/_Project/Scripts/PlayerInventory.cs:2083` |
| `ItemAcquiredSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6852` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5639`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:5657`<br>`Assets/_Project/Scripts/VoxelDeltaProcessor.cs:3523` | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:622`<br>`Assets/_Project/Scripts/PlayerInventory.cs:4159`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:5156` |
| `ItemDecaySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8249` | none found | none found |
| `ItemDurabilityChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6832` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5402`<br>`Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs:864` | `Assets/_Project/Scripts/HUDQuickBar.cs:337`<br>`Assets/_Project/Scripts/PlayerToolManager.cs:1954`<br>`Assets/_Project/Scripts/UI/PDALoadoutTab.cs:307`<br>`Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:292` |
| `KccVelocitySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:328` | `Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1515`<br>`Assets/_Project/Scripts/Physics/PhysicsDeterminismSignals.cs:113` | none found |
| `KillSwitchSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:30` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:878` | none found |
| `LaserCutterEventPayload` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:580` | none found | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:247`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:4822` |
| `LaserCutterEventPayloadSignal` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/LaserCutter.cs:288`<br>`Assets/_Project/Scripts/LaserCutter.cs:300` | `Assets/_Project/Scripts/LaserCutter.cs:115` |
| `LightLevelSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8271` | none found | none found |
| `LocalizationLanguageChangedSignal` | `Assets/_Project/Scripts/LocRegistry.cs:408` | `Assets/_Project/Scripts/LocRegistry.cs:1586` | none found |
| `LockstepSnapshotSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:535` | `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:984` | none found |
| `LoreFragmentScannedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7771` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5302` | none found |
| `MacroCollisionSignal` | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:616` | none found | none found |
| `MacroDatabaseSectorHydrationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8130` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5503` | `Assets/_Project/Scripts/SaveManager.cs:1412`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:3777`<br>`Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:543` |
| `ManualOverridePulledSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7969` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5482` | none found |
| `MechHapticSignalDTO` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsContracts.cs:50` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:578` | none found |
| `MemoryAddressShiftSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7440` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5153` | `Assets/_Project/Scripts/SubmarineFluidDynamics.cs:2013` |
| `MemoryDesyncSignal` | `Assets/_Project/Scripts/Core/Contracts/MemorySentinelSignals.cs:6` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1385`<br>`Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1414` | none found |
| `MemoryPressureSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7428` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5146` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:700`<br>`Assets/_Project/Scripts/UI/PDAShellChrome.cs:480`<br>`Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3981` |
| `MemorySentinelRollbackSignal` | `Assets/_Project/Scripts/Core/Contracts/MemorySentinelSignals.cs:51` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1400` | none found |
| `MixerStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7091` | none found | none found |
| `MockAcousticSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:180` | none found | none found |
| `MockAupRebaseSignal` | `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs:16` | none found | none found |
| `MockCarveRequestSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:190` | none found | none found |
| `MockCombatDamageSignal` | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:389` | none found | none found |
| `MockConsumeSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:140` | none found | none found |
| `MockCraftingRequestSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:128` | none found | none found |
| `MockDamageSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:194` | `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:974` | none found |
| `MockHotbarSelectSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:201` | none found | none found |
| `MockHudSignal` | `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs:54` | none found | none found |
| `MockInventoryTransactionSignal` | `Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:290` | `Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:2414` | none found |
| `MockItemAcquiredSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:116` | none found | none found |
| `MockLaserFireSignal` | `Assets/_Project/Scripts/VFX/Debris/ShinobuDeltaCrusherJobs.cs:29` | none found | none found |
| `MockModuleStateSignal` | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:72` | none found | none found |
| `MockNarrativeTriggerSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:2332` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1160` | none found |
| `MockPlayerFootstepSignal` | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:548` | none found | none found |
| `MockPlayerPositionSignal` | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:116` | none found | none found |
| `MockPredatorSignal` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1736` | none found | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1111` |
| `MockQualityWeightSignal` | `Assets/_Project/Scripts/Core/Contracts/DrsContracts.cs:106` | none found | none found |
| `MockRockCollisionSignal` | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:594` | none found | none found |
| `MockStoryEventSignal` | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:101` | none found | none found |
| `MockTextRequestSignal` | `Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:218` | none found | none found |
| `MockToolUsedSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:152` | none found | none found |
| `MockTriggerPullSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:181` | none found | none found |
| `ModAssetReferenceSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:166` | none found | none found |
| `ModFutureDevNullSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:208` | none found | none found |
| `ModSpawnRequestSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:153` | none found | none found |
| `ModdedGameMaskSignal` | `Assets/_Project/Scripts/Core/Contracts/MemorySentinelSignals.cs:68` | none found | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:613` |
| `ModuleDeconstructSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7353` | none found | none found |
| `MovementAcousticSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7533` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5192` | `Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:622`<br>`Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:707`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6040` |
| `NarrativeFocusSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7057` | none found | none found |
| `NarrativeHudWaypointSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7105` | none found | none found |
| `NarrativePoiStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7128` | none found | `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:856`<br>`Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:810` |
| `OxygenCriticalSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7627` | none found | none found |
| `PdaExchangeStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8040` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5437` | `Assets/_Project/Scripts/UI/PDABarterTab.cs:198`<br>`Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:793` |
| `PhysicsEventPayload` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:664` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:245`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:5002`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:745`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:779` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:122`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:244`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:4963`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:606`<br>`Assets/_Project/Scripts/SpatialAudioManager.cs:4517` |
| `PhysiologyStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8336` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5700` | none found |
| `PipeRuptureSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7707` | none found | none found |
| `PlayVoiceOverSignal` | `Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:231` | none found | none found |
| `PlayerActionCancelledSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6787` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5423` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:141` |
| `PlayerActionCompletedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6771` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5416` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:137` |
| `PlayerActionProgressSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6752` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5409` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:133` |
| `PlayerBaseEnterSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8479` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4951` | none found |
| `PlayerBaseExitSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8494` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4958` | none found |
| `PlayerExhaleSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:78` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10528` | `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:6071`<br>`Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:687`<br>`Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs:318` |
| `PlayerFatalPressureSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:108` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10552` | `Assets/_Project/Scripts/UI/HectonOSBootManager.cs:215`<br>`Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:708` |
| `PlayerFootstepSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:8` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10494` | `Assets/_Project/Scripts/PlayerFootstepAudio.cs:224` |
| `PlayerInputSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:721` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:3114` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:564`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:581`<br>`Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs:2144`<br>`Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs:2165`<br>`Assets/_Project/Scripts/HectonFabricatorUI.cs:625`<br>`Assets/_Project/Scripts/HectonFabricatorUI.cs:651`<br>`Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:314`<br>`Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:331`<br>`Assets/_Project/Scripts/MainMenuController.cs:897`<br>`Assets/_Project/Scripts/MainMenuController.cs:913`<br>`Assets/_Project/Scripts/PlayerBuilder.cs:670`<br>`Assets/_Project/Scripts/PlayerBuilder.cs:687`<br>... +7 more |
| `PlayerLookTargetSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:740` | `Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:611`<br>`Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:631` | `Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:461` |
| `PlayerSprintStateSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:93` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10540` | `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs:1209` |
| `PlayerStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6699` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4911` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1064`<br>`Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:587`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2030`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2483`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2584`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5756` |
| `PlayerStressSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8348` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5714` | `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2501`<br>`Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:321` |
| `PlayerTransportBailoutSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:120` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10568` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1338` |
| `PlayerWaterSplashSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:36` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10517` | `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:1024`<br>`Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:695` |
| `PowerDrainSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7876` | none found | none found |
| `PrefabAcousticSignatureSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:158` | `Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistry.cs:345`<br>`Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistryRuntimeBinder.cs:85` | none found |
| `PrefabLoreLinkSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:172` | `Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistry.cs:356`<br>`Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistryRuntimeBinder.cs:96` | none found |
| `ProgressionEventSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6989` | none found | none found |
| `PrologueCompleteSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7952` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5475` | `Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:259`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:223`<br>`Assets/_Project/Scripts/HectonFluidEngine.cs:2880`<br>`Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:410` |
| `QuestDagMockItemAcquiredSignal` | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:129` | none found | none found |
| `RadarJamSignal` | `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs:25` | none found | none found |
| `RadiationDoseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6865` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5660` | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:670`<br>`Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs:992`<br>`Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:776` |
| `RadiationSourceSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6893` | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:99`<br>`Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:112` | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:646` |
| `RebaseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7230` | none found | none found |
| `ReconDataSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8107` | none found | none found |
| `ReentryVfxStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:240` | `Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:769` | none found |
| `ResidencySectorDehydratedSignal` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4587` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4012`<br>`Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4641` |
| `ResidencySectorHydratedSignal` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4571` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4646` |
| `ResolutionChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7456` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5160`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:1498` | none found |
| `ResourceDepletionDeltaSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6908` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5663` | none found |
| `RigidbodySleepSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7732` | none found | none found |
| `SaveCompletedSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:92` | `Assets/_Project/Scripts/SaveManager.cs:2204` | none found |
| `SaveLifecycleSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8118` | none found | none found |
| `SaveMetadataReadySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8188` | `Assets/_Project/Scripts/SaveThumbnailSystem.cs:965` | none found |
| `SaveRequestSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:75` | `Assets/_Project/Scripts/SaveManager.cs:1160` | none found |
| `SaveStatusSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:108` | `Assets/_Project/Scripts/SaveManager.cs:2178` | none found |
| `ScalabilityChangedEvent` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:36` | `Assets/_Project/Scripts/Core/IPlatformIntegration.cs:138` | `Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:814`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:131`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:271`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:447`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:477`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:2196`<br>`Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:171`<br>`Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:292`<br>`Assets/_Project/Scripts/Core/IPlatformIntegration.cs:149`<br>`Assets/_Project/Scripts/Core/SystemDispatcher.cs:3087`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:980`<br>`Assets/_Project/Scripts/SpatialAudioManager.cs:1416`<br>... +1 more |
| `ScanCompleteSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7759` | none found | none found |
| `ScanLogChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8020` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5430` | `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:687`<br>`Assets/_Project/Scripts/PDA/PDALogbookManager.cs:638` |
| `ScannerToolActiveSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7743` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5288` | `Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:427`<br>`Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs:644` |
| `SectorDehydratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7574` | `Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:566` | `Assets/_Project/Scripts/World/EcosystemDirector.cs:3761` |
| `SectorResidencyHydratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7559` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:428`<br>`Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:536` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:268`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:3786` |
| `SeismicSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8224` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5548` | `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs:1541` |
| `ShinobuPlayerExertionSignal` | `Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:19` | `Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1561` | none found |
| `SignalWardenMockDamageSignal` | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:578` | none found | none found |
| `SiltExplosionSignal` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsContracts.cs:74` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:603` | none found |
| `SimulationBucketSyncSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8534` | `Assets/_Project/Scripts/Core/SystemDispatcher.cs:3869` | none found |
| `SimulationPauseSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:158` | none found | none found |
| `SolarFlareSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7219` | none found | none found |
| `SonarPingSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7604` | none found | none found |
| `SoundscapeProfileSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7117` | none found | none found |
| `SpectrumScanSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7720` | none found | none found |
| `SplashEvent` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:609` | `Assets/_Project/Scripts/Physics/FluidFeedbackListener.cs:155`<br>`Assets/_Project/Scripts/Physics/FluidFeedbackListener.cs:176` | `Assets/_Project/Scripts/Physics/FluidFeedbackListener.cs:122` |
| `StateChangedSignal` | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:86` | `Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs:1147` | none found |
| `StateCorrectionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:288` | `Assets/_Project/Scripts/Physics/PhysicsDeterminismSignals.cs:72` | none found |
| `StorageDebtSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7902` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5454` | none found |
| `StreamingTurbulenceSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7921` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5461` | none found |
| `SubmarineFloodStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7673` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5248`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:2674` | `Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:956`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:410` |
| `SubmarineLightsChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8296` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5678` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:608`<br>`Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:2431`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:4221` |
| `SubtitleSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7393` | none found | `Assets/_Project/Scripts/UI/SubtitleManager.cs:740` |
| `SurvivalVitalsChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6738` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4924` | `Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:791`<br>`Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:718`<br>`Assets/_Project/Scripts/UI/SuitAdvisoryController.cs:161`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:4517`<br>`Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:764`<br>`Assets/_Project/Scripts/Visor/VisorHUDController.cs:1322` |
| `SwarmDispersedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7546` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5199`<br>`Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:555`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6119` | none found |
| `SyncFenceSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:314` | `Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1051`<br>`Assets/_Project/Scripts/Physics/PhysicsDeterminismSignals.cs:87` | none found |
| `SystemGlitchSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:550` | `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1082` | `Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:715` |
| `SystemHealthIndexSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7477` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5167`<br>`Assets/_Project/Scripts/Core/HomeostasisBrain.cs:892`<br>`Assets/_Project/Scripts/QA/Headless/Shinobu38QaWatchdogRuntime.cs:1073` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1133`<br>`Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:773`<br>`Assets/_Project/Scripts/Core/InputDispatcher.cs:3406`<br>`Assets/_Project/Scripts/Core/SystemDispatcher.cs:2571`<br>`Assets/_Project/Scripts/Core/SystemDispatcher.cs:3563`<br>`Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:912`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2554`<br>`Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:491`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:987`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:989`<br>`Assets/_Project/Scripts/LaserCutter.cs:1615`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:387`<br>... +6 more |
| `SystemHealthSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:224` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:838` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1229`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:783`<br>`Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:403`<br>`Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:842`<br>`Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1536`<br>`Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:726`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5813`<br>`Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:1832` |
| `SystemPauseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8522` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5583`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:5590`<br>`Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs:734` | none found |
| `TelemetryAnomalySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7287` | none found | none found |
| `TemperatureChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6877` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4995` | none found |
| `TerminalClickSignal` | `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:81` | none found | none found |
| `TerminalCommandSignal` | `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:89` | none found | none found |
| `TetherFiredSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:375` | none found | none found |
| `TetherSnappedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:360` | `Assets/_Project/Scripts/Physics/TetherSignals.cs:76` | `Assets/_Project/Scripts/Physics/TetherSignals.cs:89` |
| `TetherTensionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:343` | `Assets/_Project/Scripts/Physics/TetherSignals.cs:83` | none found |
| `ThermalStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8078` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5388`<br>`Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:618` | `Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:813`<br>`Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:425` |
| `ThermalUpdraftSignal` | `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardTypes.cs:70` | `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:966` | none found |
| `TimeDilationSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:143` | none found | none found |
| `ToolAcousticSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7859` | `Assets/_Project/Scripts/Tools/WfcLaserCutRuntime.cs:359`<br>`Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:355` | none found |
| `ToolBrokenSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:164` | none found | none found |
| `ToolHeatSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:203` | none found | none found |
| `ToolLoadoutChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7838` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5346` | `Assets/_Project/Scripts/Gameplay/PlayerTransportCoordinator.cs:344`<br>`Assets/_Project/Scripts/HUDQuickBar.cs:313`<br>`Assets/_Project/Scripts/PDAInventoryTab.cs:518`<br>`Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs:246`<br>`Assets/_Project/Scripts/UI/PDAConstructionTab.cs:367`<br>`Assets/_Project/Scripts/UI/PDALoadoutTab.cs:342`<br>`Assets/_Project/Scripts/UI/PDAShellChrome.cs:549` |
| `ToolStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7818` | none found | none found |
| `ToolTriggerSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7889` | none found | none found |
| `ToxicBioluminescenceSignal` | `Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryTypes.cs:107` | none found | none found |
| `ToxicityExposureSignal` | `Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryTypes.cs:91` | none found | none found |
| `TraumaSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8360` | none found | none found |
| `UIRescaleRequestSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7650` | none found | none found |
| `VehicleUpgradesChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8061` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5444` | none found |
| `VfxSparkRequestSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:214` | none found | none found |
| `VisorBreachSignal` | `Assets/_Project/Scripts/Visor/DiegeticVisorLensTypes.cs:106` | none found | none found |
| `VisorDropletSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:260` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10588`<br>`Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:741` | `Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs:348` |
| `VisualFlareSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:457` | `Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:551` | none found |
| `VitalWarningSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7366` | none found | none found |
| `VocalWarningSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:7405` | none found | none found |
| `VoxelCarveEvent` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:436` | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs:1227` | `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:831` |
| `WakeGeneratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6951` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5728`<br>`Assets/_Project/Scripts/Gameplay/VehicleMotor.cs:1239`<br>`Assets/_Project/Scripts/World/FloraInteractionManager.cs:2255` | `Assets/_Project/Scripts/World/FloraInteractionManager.cs:2260` |
| `WakeRequestSignal` | `Assets/_Project/Scripts/Core/Signals/PhysicsWakeSignalContracts.cs:12` | none found | none found |
| `WaterTransitionSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:58` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10612` | `Assets/_Project/Scripts/Gameplay/WaterTransitionHandler.cs:60` |
| `WaterlineBreachSignal` | `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereContracts.cs:875` | `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:813` | none found |
| `WeatherChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8509` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5624` | none found |
| `WeatherStrengthSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8238` | none found | none found |
| `WfcOutpostDoorPowerSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8172` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5524`<br>`Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:477` | `Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:949` |
| `WfcOutpostGeneratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8142` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5510`<br>`Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:1308` | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:739`<br>`Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:235`<br>`Assets/_Project/Scripts/Tools/WfcLaserCutRuntime.cs:200` |
| `WfcOutpostStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8159` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5517` | `Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs:107`<br>`Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:771`<br>`Assets/_Project/Scripts/SaveManager.cs:1235` |

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
| `HectonDroneFleetEvents` | front/back `NativeQueue<HectonDroneFleetSnapshotPayload>` | `IDroneFleetSnapshotEventListener` | `RaiseSnapshotUpdated` | `SystemDispatcher.LateUpdate()` |
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
| `SceneBootstrap` | front/back `NativeQueue<SceneBootstrapEventPayload>` | `ISceneBootstrapEventListener` | private `RaiseGameReadyEvent`, private `RaiseBootstrapFailedEvent` | `SystemDispatcher.LateUpdate()` via `FlushPendingEvents()` |
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
| none | 0 | no active matches |

## PHI Self-Audit

Exact PHI_SYN rationale file is absent from active logs. Near-match H-Phi rationale files were scanned as supporting evidence, but this is not treated as the exact requested artifact.

Near-match active logs:

| H-Phi / UX signal | Declared at | Producers | Consumers |
|---|---|---|---|
| `PlayerActionProgressSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6752` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5409` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:133` |
| `PlayerActionCompletedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6771` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5416` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:137` |
| `PlayerActionCancelledSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6787` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5423` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:141` |
| `PdaExchangeStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8040` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5437` | `Assets/_Project/Scripts/UI/PDABarterTab.cs:198`<br>`Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:793` |
| `VehicleUpgradesChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:8061` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5444` | none found |
| `PlayerStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6699` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4911` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:1064`<br>`Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:587`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2030`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2483`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2584`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5756` |
| `InventoryChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:6819` | `Assets/_Project/Scripts/PlayerInventory.cs:3995` | `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:669`<br>`Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:874`<br>`Assets/_Project/Scripts/HectonPlayerMovement.cs:4683`<br>`Assets/_Project/Scripts/PDAInventoryTab.cs:449`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:378`<br>`Assets/_Project/Scripts/PlayerToolManager.cs:1922`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:2829`<br>`Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs:205` |
| `InputStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:522` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:616` | `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:589`<br>`Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:588`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1620`<br>`Assets/_Project/Scripts/UI/InteractionUI.cs:889`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5795` |
| `SystemHealthSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:224` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:838` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1229`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:783`<br>`Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:403`<br>`Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:842`<br>`Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1536`<br>`Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:726`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5813`<br>`Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:1832` |

## Phi-Resonance Connectivity Model

The engine connectivity model is not mystical. It is a three-layer resonance model: contracts define stable frequency, typed signal lanes carry bounded pulses, and dispatcher phases control when state can move. `GlobalRegistry` is the cold authority spine for stable interfaces. `SignalBus<T>` and queue-backed event lanes are the nervous system for broadcast state. DataVault/native buffers are the mass: owned memory that simulation mutates and presentation samples.

Low/MX350 interpretation: coalesce lanes, consume bounded snapshots, keep optional visual consumers in `VISUAL_SYNC`, and shed mips/LOD before simulation truth. High/Ultra interpretation: do not increase gameplay broadcast cost; attach richer presentation consumers to the same snapshots and spend the saved budget on light, fog, HUD, audio, and material overkill.

Connectivity risk: `Hecton8.Core` currently references many domain assemblies directly. That may be intentional current architecture, but it is not a clean inward-only core. The integrator should treat this as a dependency inversion watchpoint, especially while many agents expand contracts in parallel.

## Verification Commands

- `python Tools/BuildArchitectureAtlas.py`
- `python Tools/AtlasCheck.py`
- `python -m py_compile Tools/BuildArchitectureAtlas.py Tools/AtlasCheck.py`
- C# compile verification is outside this atlas; run Unity import/Console and serial CLI builds as separate evidence.
- Current DOC_GLOBAL R27 blocker: `python Tools/AtlasCheck.py` still exits `1` on `57` RealtimeCSG vendor icon/readme image references until the references are restored or the atlas check excludes that vendor evidence class deliberately.
- This generated atlas is not `VERIFIED` unless `Tools/AtlasCheck.py` exits `0` after generation.

## Residual Risk

- STATIC_SOURCE only: Unity import, runtime wiring, actual VRAM residency, profiler frame time, GC, build, and Play Mode remain PENDING VERIFICATION.
- The signal producer map only resolves explicit `SignalBus<T>` calls. Legacy `GlobalSignals.Publish(...)` variable publishes require Roslyn-level dataflow to type-resolve fully.
- Active logs can change while this atlas is being written because the workspace is multi-agent.
