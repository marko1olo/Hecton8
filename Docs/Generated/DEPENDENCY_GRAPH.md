# HECTON-8 Architecture Atlas - Dependency Graph

Generated: 2026-05-26 22:44:10
Date: 2026-05-26
Status: ATLAS GENERATED STATIC SOURCE / ATLASCHECK SEPARATE GATE REQUIRED / RUNTIME PENDING
Evidence class: STATIC_SOURCE / STATIC_DOC / FILESYSTEM / PY_TOOL. No Unity Editor, Play Mode, Memory Profiler, Frame Debugger, or player build evidence is claimed here.

## Authority Boundary

Generated dependency detail is static source evidence only. `Docs/PROJECT_BASELINE.md`, active architecture contracts, current source, and fresh proof artifacts override dated generated claims.

No Unity import, Console, Play Mode, profiler, GC/memory, render, player-build, save/load, platform, or visual proof is implied by this generated graph.

## Source Of Authority
- `AGENTS.md`
- `Docs/PROJECT_BASELINE.md`
- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/PROJECT_ATLAS.md`
- `Docs/DEPENDENCY_GRAPH.md`
- `Docs/Actual Domains of Project.txt`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- `Docs/ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md`
- `Docs/Generated/DEPENDENCY_GRAPH.md`
- `Docs/Generated/DEPENDENCY_GRAPH.json`
- `Docs/Generated/DEPENDENCY_GRAPH.cache.json`
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

- C# source files scanned under `Assets/` and `Packages/`: 5547
- C# line count scanned under `Assets/` and `Packages/`: 2,437,622
- First-party C# source files under `Assets/_Project/Scripts/`: 2439
- First-party C# line count under `Assets/_Project/Scripts/`: 1,804,498
- Assembly definitions scanned: 220
- First-party assembly definitions under `Assets/_Project/`: 167
- Markdown docs under `Docs/`: 4220

## Assembly Dependency Graph

Core assembly: `Assets/_Project/Scripts/Hecton8.Core.asmdef`

`Hecton8.Core` direct references currently recorded in its asmdef:
- `Hecton8.Core.Contracts`
- `Hecton8.Core.Database`
- `Hecton8.Core.Scheduling`
- `Hecton8.Core.Bucketing`
- `Hecton8.Core.Persistence.Paging`
- `Hecton8.Core.Memory`
- `Hecton8.Bootstrap.Contracts`
- `Hecton8.Audio.Virtualization.Contracts`
- `Hecton8.Logistics.Grid.Contracts`
- `Hecton8.Inventory.Corrosion.Contracts`
- `Hecton8.Tools.ToolKinematics.Contracts`
- `Hecton8.UI.Diegetic.Contracts`
- `Hecton8.World.Contracts`
- `Hecton8.Habitat.Deformation.Contracts`
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

Core contracts assembly: `Assets/_Project/Scripts/Core/Contracts/Hecton8.Core.Contracts.asmdef` references `Unity.Collections`, `Unity.Jobs`, `Unity.Mathematics`.

Assemblies directly depending on exact `Hecton8.Core`: 103

| Assembly | Path |
|---|---|
| `Hecton8.AI.Ambient` | `Assets/_Project/Scripts/AI/Ambient/Hecton8.AI.Ambient.asmdef` |
| `Hecton8.AI.Cognition.Editor` | `Assets/_Project/Scripts/AI/Cognition/Editor/Hecton8.AI.Cognition.Editor.asmdef` |
| `Hecton8.AI.Pathfinding` | `Assets/_Project/Scripts/AI/Pathfinding/Hecton8.AI.Pathfinding.asmdef` |
| `Hecton8.AI.Pathfinding.Editor` | `Assets/_Project/Scripts/AI/Pathfinding/Editor/Hecton8.AI.Pathfinding.Editor.asmdef` |
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
| `Hecton8.Crest.Bridge` | `Assets/_Project/Scripts/Plugins/Crest/Hecton8.Crest.Bridge.asmdef` |
| `Hecton8.Crest.Bridge.Editor` | `Assets/_Project/Scripts/Plugins/Crest/Editor/Hecton8.Crest.Bridge.Editor.asmdef` |
| `Hecton8.DataMonolith.Editor` | `Assets/_Project/Scripts/Editor/DataMonolith/Hecton8.DataMonolith.Editor.asmdef` |
| `Hecton8.Dev.SpaceEngine098` | `Assets/_Project/Scripts/Dev/SpaceEngine098/Hecton8.Dev.SpaceEngine098.asmdef` |
| `Hecton8.Ecosystem.NutrientDrift.Editor` | `Assets/_Project/Scripts/Editor/NutrientDrift/Hecton8.Ecosystem.NutrientDrift.Editor.asmdef` |
| `Hecton8.EditModeTests` | `Assets/_Project/Tests/Editor/Hecton8.EditModeTests.asmdef` |
| `Hecton8.Editor` | `Assets/_Project/Scripts/Editor/Hecton8.Editor.asmdef` |
| `Hecton8.Editor.ProceduralGen` | `Assets/_Project/Scripts/Editor/ProceduralGen/Hecton8.Editor.ProceduralGen.asmdef` |
| `Hecton8.Gameplay.Loot` | `Assets/_Project/Scripts/Gameplay/Loot/Hecton8.Gameplay.Loot.asmdef` |
| `Hecton8.Gameplay.Loot.Contracts` | `Assets/_Project/Scripts/Gameplay/Loot/Contracts/Hecton8.Gameplay.Loot.Contracts.asmdef` |
| `Hecton8.Gameplay.Mining` | `Assets/_Project/Scripts/Gameplay/Mining/Hecton8.Gameplay.Mining.asmdef` |
| `Hecton8.Graphics.Culling` | `Assets/_Project/Scripts/Graphics/Culling/Hecton8.Graphics.Culling.asmdef` |
| `Hecton8.Graphics.Materials` | `Assets/_Project/Scripts/Graphics/Materials/Hecton8.Graphics.Materials.asmdef` |
| `Hecton8.Graphics.Scalability` | `Assets/_Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef` |
| `Hecton8.Habitat.Deformation` | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/Hecton8.Habitat.Deformation.asmdef` |
| `Hecton8.Habitat.Deformation.DamageBake.Editor` | `Assets/_Project/Scripts/Habitat/Deformation/Editor/DamageBake/Hecton8.Habitat.Deformation.DamageBake.Editor.asmdef` |
| `Hecton8.HabitatInteriorClutterForge.Editor` | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Hecton8.HabitatInteriorClutterForge.Editor.asmdef` |
| `Hecton8.InventoryRouting.Editor` | `Assets/_Project/Scripts/Editor/InventoryRouting/Hecton8.InventoryRouting.Editor.asmdef` |
| `Hecton8.Lighting` | `Assets/_Project/Scripts/Lighting/Hecton8.Lighting.asmdef` |
| `Hecton8.Lighting.Editor` | `Assets/_Project/Scripts/Lighting/Editor/Hecton8.Lighting.Editor.asmdef` |
| `Hecton8.Narrative.Campaign` | `Assets/_Project/Scripts/Narrative/Campaign/Hecton8.Narrative.Campaign.asmdef` |
| `Hecton8.Narrative.Prologue` | `Assets/_Project/Scripts/Narrative/Prologue/Hecton8.Narrative.Prologue.asmdef` |
| `Hecton8.Optimization.Editor` | `Assets/_Project/Scripts/Optimization/Editor/Hecton8.Optimization.Editor.asmdef` |
| `Hecton8.Physics.Buoyancy.Editor` | `Assets/_Project/Scripts/Physics/Buoyancy/Editor/Hecton8.Physics.Buoyancy.Editor.asmdef` |
| `Hecton8.Physics.Buoyancy.Runtime` | `Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/Hecton8.Physics.Buoyancy.Runtime.asmdef` |
| `Hecton8.Physics.Cable132` | `Assets/_Project/Scripts/Physics/Cable132/Hecton8.Physics.Cable132.asmdef` |
| `Hecton8.Physics.KCC.Editor` | `Assets/_Project/Scripts/Physics/KCC/Editor/Hecton8.Physics.KCC.Editor.asmdef` |
| `Hecton8.Physics.Vehicles.Editor` | `Assets/_Project/Scripts/Physics/Vehicles/Editor/Hecton8.Physics.Vehicles.Editor.asmdef` |
| `Hecton8.Physiology` | `Assets/_Project/Scripts/Physiology/Hecton8.Physiology.asmdef` |
| `Hecton8.Physiology.Editor` | `Assets/_Project/Scripts/Physiology/Editor/Hecton8.Physiology.Editor.asmdef` |
| `Hecton8.PlayModeTests` | `Assets/_Project/Tests/PlayMode/Hecton8.PlayModeTests.asmdef` |
| `Hecton8.Plugins` | `Assets/_Project/Scripts/Plugins/Hecton8.Plugins.asmdef` |
| `Hecton8.Power.BatteryChargerLogistics.Editor` | `Assets/_Project/Scripts/Power/Editor/Hecton8.Power.BatteryChargerLogistics.Editor.asmdef` |
| `Hecton8.Power.BatteryChargerLogistics.Runtime` | `Assets/_Project/Scripts/Power/BatteryChargerLogistics/Hecton8.Power.BatteryChargerLogistics.Runtime.asmdef` |
| `Hecton8.Power.Generators` | `Assets/_Project/Scripts/Power/Generators/Hecton8.Power.Generators.asmdef` |
| `Hecton8.Project.Editor` | `Assets/_Project/Editor/Hecton8.Project.Editor.asmdef` |
| `Hecton8.Prologue.Space` | `Assets/_Project/Scripts/Prologue/Space/Hecton8.Prologue.Space.asmdef` |
| `Hecton8.Prologue.VFX` | `Assets/_Project/Scripts/Prologue/VFX/Hecton8.Prologue.VFX.asmdef` |
| `Hecton8.QA` | `Assets/_Project/Scripts/QA/Hecton8.QA.asmdef` |
| `Hecton8.QA.Editor` | `Assets/_Project/Scripts/QA/Editor/Hecton8.QA.Editor.asmdef` |
| `Hecton8.QA.Headless` | `Assets/_Project/Scripts/QA/Headless/Hecton8.QA.Headless.asmdef` |
| `Hecton8.QA.Headless.Editor` | `Assets/_Project/Scripts/QA/Headless/Editor/Hecton8.QA.Headless.Editor.asmdef` |
| `Hecton8.Rendering.BilateralDrs` | `Assets/_Project/Scripts/Rendering/BilateralDrs/Hecton8.Rendering.BilateralDrs.asmdef` |
| `Hecton8.Rendering.OceanSinglePass` | `Assets/_Project/Scripts/Rendering/OceanSinglePass/Hecton8.Rendering.OceanSinglePass.asmdef` |
| `Hecton8.Rendering.WaterOptics` | `Assets/_Project/Scripts/Rendering/WaterOptics/Hecton8.Rendering.WaterOptics.asmdef` |
| `Hecton8.Rendering.WaterOptics.Editor` | `Assets/_Project/Scripts/Rendering/WaterOptics/Editor/Hecton8.Rendering.WaterOptics.Editor.asmdef` |
| `Hecton8.SaveSystem.EditModeTests` | `Assets/_Project/Tests/Editor/SaveSystem/Hecton8.SaveSystem.EditModeTests.asmdef` |
| `Hecton8.SaveSystem.Editor` | `Assets/_Project/Scripts/Editor/SaveSystem/Hecton8.SaveSystem.Editor.asmdef` |
| `Hecton8.SeedShipAnomaly.Editor` | `Assets/_Project/Scripts/World/SeedShipAnomaly/Editor/Hecton8.SeedShipAnomaly.Editor.asmdef` |
| `Hecton8.SeedShipAnomaly.Runtime` | `Assets/_Project/Scripts/World/SeedShipAnomaly/Hecton8.SeedShipAnomaly.Runtime.asmdef` |
| `Hecton8.Thermodynamics` | `Assets/_Project/Scripts/Thermodynamics/Hecton8.Thermodynamics.asmdef` |
| `Hecton8.Tools.ToolKinematics` | `Assets/_Project/Scripts/Tools/ToolKinematics/Hecton8.Tools.ToolKinematics.asmdef` |
| `Hecton8.Tools.ToolKinematics.Editor` | `Assets/_Project/Scripts/Tools/ToolKinematics/Editor/Hecton8.Tools.ToolKinematics.Editor.asmdef` |
| `Hecton8.UI.Editor` | `Assets/_Project/Scripts/UI/Editor/Hecton8.UI.Editor.asmdef` |
| `Hecton8.UI.Navigation` | `Assets/_Project/Scripts/UI/Navigation/Hecton8.UI.Navigation.asmdef` |
| `Hecton8.UI.TerminalOS.Editor` | `Assets/_Project/Scripts/UI/TerminalOS/Editor/Hecton8.UI.TerminalOS.Editor.asmdef` |
| `Hecton8.UI.Tools` | `Assets/_Project/Scripts/UI/Tools/Hecton8.UI.Tools.asmdef` |
| `Hecton8.UI.VR` | `Assets/_Project/Scripts/UI/VR/Hecton8.UI.VR.asmdef` |
| `Hecton8.VFX.Bioluminescence.Editor` | `Assets/_Project/Scripts/VFX/Bioluminescence/Editor/Hecton8.VFX.Bioluminescence.Editor.asmdef` |
| `Hecton8.VFX.Bioluminescence.Runtime` | `Assets/_Project/Scripts/VFX/Bioluminescence/Hecton8.VFX.Bioluminescence.Runtime.asmdef` |
| `Hecton8.VFX.Debris` | `Assets/_Project/Scripts/VFX/Debris/Hecton8.VFX.Debris.asmdef` |
| `Hecton8.VFX.JacobianFoam.Editor` | `Assets/_Project/Scripts/VFX/JacobianFoam/Editor/Hecton8.VFX.JacobianFoam.Editor.asmdef` |
| `Hecton8.VFX.JacobianFoam.Runtime` | `Assets/_Project/Scripts/VFX/JacobianFoam/Hecton8.VFX.JacobianFoam.Runtime.asmdef` |
| `Hecton8.VFX.Materials` | `Assets/_Project/Scripts/VFX/Materials/Hecton8.VFX.Materials.asmdef` |
| `Hecton8.VFX.Parasites.Editor` | `Assets/_Project/Scripts/VFX/Parasites/Editor/Hecton8.VFX.Parasites.Editor.asmdef` |
| `Hecton8.VFX.Parasites.Runtime` | `Assets/_Project/Scripts/VFX/Parasites/Hecton8.VFX.Parasites.Runtime.asmdef` |
| `Hecton8.VFX.PlasmaBeam.Runtime` | `Assets/_Project/Scripts/VFX/PlasmaBeam/Hecton8.VFX.PlasmaBeam.Runtime.asmdef` |
| `Hecton8.Vehicles.VFX` | `Assets/_Project/Scripts/Vehicles/VFX/Hecton8.Vehicles.VFX.asmdef` |
| `Hecton8.World.BiomeWeightMapBaker.Editor` | `Assets/_Project/Scripts/World/BiomeWeightMapBaker/Editor/Hecton8.World.BiomeWeightMapBaker.Editor.asmdef` |
| `Hecton8.World.BiotaDensityMapBaker.Editor` | `Assets/_Project/Scripts/World/BiotaDensityMapBaker/Editor/Hecton8.World.BiotaDensityMapBaker.Editor.asmdef` |
| `Hecton8.World.Economy` | `Assets/_Project/Scripts/World/Resources/Hecton8.World.Economy.asmdef` |
| `Hecton8.World.Economy.Editor` | `Assets/_Project/Scripts/World/Resources/Editor/Hecton8.World.Economy.Editor.asmdef` |
| `Hecton8.World.FloraAmbientSway` | `Assets/_Project/Scripts/World/FloraAmbientSway/Hecton8.World.FloraAmbientSway.asmdef` |
| `Hecton8.World.FloraAmbientSway.Editor` | `Assets/_Project/Scripts/Editor/FloraAmbientSway/Hecton8.World.FloraAmbientSway.Editor.asmdef` |
| `Hecton8.World.GeographySanity.Editor` | `Assets/_Project/Scripts/Editor/GeographySanity/Hecton8.World.GeographySanity.Editor.asmdef` |
| `Hecton8.World.HydraulicErosionForge.Editor` | `Assets/_Project/Scripts/Editor/HydraulicErosionForge/Shinobu242/Hecton8.World.HydraulicErosionForge.Editor.asmdef` |
| `Hecton8.World.OfflineGeology.Editor` | `Assets/_Project/Scripts/Editor/GeologyForge/Hecton8.World.OfflineGeology.Editor.asmdef` |
| `Hecton8.World.OfflineGeometry.Editor` | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/Hecton8.World.OfflineGeometry.Editor.asmdef` |
| `Hecton8.World.OfflineHadalTrenchBaker.Editor` | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/Hecton8.World.OfflineHadalTrenchBaker.Editor.asmdef` |
| `Hecton8.World.OfflineWreckageBaker.Editor` | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/Hecton8.World.OfflineWreckageBaker.Editor.asmdef` |
| `Hecton8.World.Outposts` | `Assets/_Project/Scripts/World/Outposts/Hecton8.World.Outposts.asmdef` |
| `Hecton8.World.ProceduralCoral.Editor` | `Assets/_Project/Scripts/World/ProceduralCoral/Editor/Hecton8.World.ProceduralCoral.Editor.asmdef` |
| `Hecton8.World.ProceduralWreckage` | `Assets/_Project/Scripts/World/ProceduralWreckage/Hecton8.World.ProceduralWreckage.asmdef` |
| `Hecton8.World.ProceduralWreckage.Editor` | `Assets/_Project/Scripts/World/ProceduralWreckage/Editor/Hecton8.World.ProceduralWreckage.Editor.asmdef` |
| `Hecton8.World.ShinobuBiomimetic` | `Assets/_Project/Scripts/World/ShinobuBiomimetic/Hecton8.World.ShinobuBiomimetic.asmdef` |
| `Hecton8.World.ShinobuBiomimetic.Editor` | `Assets/_Project/Scripts/World/ShinobuBiomimetic/Editor/Hecton8.World.ShinobuBiomimetic.Editor.asmdef` |
| `Hecton8.World.StaticCaveSdfBaker.Editor` | `Assets/_Project/Scripts/World/StaticCaveSdfBaker/Editor/Hecton8.World.StaticCaveSdfBaker.Editor.asmdef` |
| `Hecton8.World.VoxelSurfaceNets.Editor` | `Assets/_Project/Scripts/World/VoxelSurfaceNets/Editor/Hecton8.World.VoxelSurfaceNets.Editor.asmdef` |

Assemblies depending on any `Hecton8.Core*` assembly: 127

| Assembly | Core-family references | Path |
|---|---|---|
| `Hecton8.AI.Ambient` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/AI/Ambient/Hecton8.AI.Ambient.asmdef` |
| `Hecton8.AI.Cognition` | `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/AI/Cognition/Hecton8.AI.Cognition.asmdef` |
| `Hecton8.AI.Cognition.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/AI/Cognition/Editor/Hecton8.AI.Cognition.Editor.asmdef` |
| `Hecton8.AI.Foveated` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/AI/Foveated/Hecton8.AI.Foveated.asmdef` |
| `Hecton8.AI.Pathfinding` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/AI/Pathfinding/Hecton8.AI.Pathfinding.asmdef` |
| `Hecton8.AI.Pathfinding.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/AI/Pathfinding/Editor/Hecton8.AI.Pathfinding.Editor.asmdef` |
| `Hecton8.Animation.FaunaProcedural` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Animation/FaunaProcedural/Hecton8.Animation.FaunaProcedural.asmdef` |
| `Hecton8.Animation.FaunaProcedural.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Animation/FaunaProcedural/Editor/Hecton8.Animation.FaunaProcedural.Editor.asmdef` |
| `Hecton8.Atmosphere.StormPropagation.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Atmosphere/StormPropagation/Editor/Hecton8.Atmosphere.StormPropagation.Editor.asmdef` |
| `Hecton8.Atmosphere.StormPropagation.Runtime` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Atmosphere/StormPropagation/Hecton8.Atmosphere.StormPropagation.Runtime.asmdef` |
| `Hecton8.Audio.Echolocation` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Audio/Echolocation/Hecton8.Audio.Echolocation.asmdef` |
| `Hecton8.Audio.Prologue` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Audio/Prologue/Hecton8.Audio.Prologue.asmdef` |
| `Hecton8.Audio.Synthesis` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Audio/Synthesis/Hecton8.Audio.Synthesis.asmdef` |
| `Hecton8.Audio.Synthesis.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Audio/Synthesis/Editor/Hecton8.Audio.Synthesis.Editor.asmdef` |
| `Hecton8.Audio.Virtualization.Contracts` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Audio/Virtualization/Contracts/Hecton8.Audio.Virtualization.Contracts.asmdef` |
| `Hecton8.Cartography.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/Cartography/Editor/Hecton8.Cartography.Editor.asmdef` |
| `Hecton8.Core.Bridge.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Core/Bridge/Editor/Hecton8.Core.Bridge.Editor.asmdef` |
| `Hecton8.Core.Bucketing` | `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Core/Bucketing/Hecton8.Core.Bucketing.asmdef` |
| `Hecton8.Core.Content.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/Core/Content/Editor/Hecton8.Core.Content.Editor.asmdef` |
| `Hecton8.Core.Database` | `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Core/Database/Hecton8.Core.Database.asmdef` |
| `Hecton8.Core.Hardware` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Core/Hardware/Hecton8.Core.Hardware.asmdef` |
| `Hecton8.Core.Memory` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Core/Memory/Hecton8.Core.Memory.asmdef` |
| `Hecton8.Core.Memory.Defrag` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Core/Memory/Defrag/Hecton8.Core.Memory.Defrag.asmdef` |
| `Hecton8.Core.Persistence` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Core/Persistence/Hecton8.Core.Persistence.asmdef` |
| `Hecton8.Core.Persistence.Paging` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Core/Persistence/Paging/Hecton8.Core.Persistence.Paging.asmdef` |
| `Hecton8.Core.Scheduling` | `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Core/Scheduling/Hecton8.Core.Scheduling.asmdef` |
| `Hecton8.Crest.Bridge` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Plugins/Crest/Hecton8.Crest.Bridge.asmdef` |
| `Hecton8.Crest.Bridge.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Plugins/Crest/Editor/Hecton8.Crest.Bridge.Editor.asmdef` |
| `Hecton8.DataMonolith.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Editor/DataMonolith/Hecton8.DataMonolith.Editor.asmdef` |
| `Hecton8.Dev.SpaceEngine098` | `Hecton8.Core` | `Assets/_Project/Scripts/Dev/SpaceEngine098/Hecton8.Dev.SpaceEngine098.asmdef` |
| `Hecton8.Ecosystem.NutrientDrift.Editor` | `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Editor/NutrientDrift/Hecton8.Ecosystem.NutrientDrift.Editor.asmdef` |
| `Hecton8.EditModeTests` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Tests/Editor/Hecton8.EditModeTests.asmdef` |
| `Hecton8.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Scheduling`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Editor/Hecton8.Editor.asmdef` |
| `Hecton8.Editor.ProceduralGen` | `Hecton8.Core` | `Assets/_Project/Scripts/Editor/ProceduralGen/Hecton8.Editor.ProceduralGen.asmdef` |
| `Hecton8.Environment.Fluids` | `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Environment/Fluids/Hecton8.Environment.Fluids.asmdef` |
| `Hecton8.Gameplay.Loot` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Gameplay/Loot/Hecton8.Gameplay.Loot.asmdef` |
| `Hecton8.Gameplay.Loot.Contracts` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Gameplay/Loot/Contracts/Hecton8.Gameplay.Loot.Contracts.asmdef` |
| `Hecton8.Gameplay.Mining` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Gameplay/Mining/Hecton8.Gameplay.Mining.asmdef` |
| `Hecton8.Graphics.Culling` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Graphics/Culling/Hecton8.Graphics.Culling.asmdef` |
| `Hecton8.Graphics.Materials` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Graphics/Materials/Hecton8.Graphics.Materials.asmdef` |
| `Hecton8.Graphics.Scalability` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef` |
| `Hecton8.Habitat.Deformation` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/Hecton8.Habitat.Deformation.asmdef` |
| `Hecton8.Habitat.Deformation.DamageBake.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/Habitat/Deformation/Editor/DamageBake/Hecton8.Habitat.Deformation.DamageBake.Editor.asmdef` |
| `Hecton8.HabitatInteriorClutterForge.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Hecton8.HabitatInteriorClutterForge.Editor.asmdef` |
| `Hecton8.Input.Determinism` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Input/Determinism/Hecton8.Input.Determinism.asmdef` |
| `Hecton8.InventoryRouting.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Editor/InventoryRouting/Hecton8.InventoryRouting.Editor.asmdef` |
| `Hecton8.Lighting` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Lighting/Hecton8.Lighting.asmdef` |
| `Hecton8.Lighting.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Lighting/Editor/Hecton8.Lighting.Editor.asmdef` |
| `Hecton8.Logistics.Grid.Contracts` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Logistics/Grid/Contracts/Hecton8.Logistics.Grid.Contracts.asmdef` |
| `Hecton8.Narrative.Camera` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Narrative/Camera/Hecton8.Narrative.Camera.asmdef` |
| `Hecton8.Narrative.Campaign` | `Hecton8.Core`, `Hecton8.Core.Memory`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Narrative/Campaign/Hecton8.Narrative.Campaign.asmdef` |
| `Hecton8.Narrative.Prologue` | `Hecton8.Core`, `Hecton8.Core.Memory`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Narrative/Prologue/Hecton8.Narrative.Prologue.asmdef` |
| `Hecton8.Optimization.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Optimization/Editor/Hecton8.Optimization.Editor.asmdef` |
| `Hecton8.Physics.Buoyancy.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Physics/Buoyancy/Editor/Hecton8.Physics.Buoyancy.Editor.asmdef` |
| `Hecton8.Physics.Buoyancy.Runtime` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/Hecton8.Physics.Buoyancy.Runtime.asmdef` |
| `Hecton8.Physics.CCD` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Physics/CCD/Hecton8.Physics.CCD.asmdef` |
| `Hecton8.Physics.Cable132` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Physics/Cable132/Hecton8.Physics.Cable132.asmdef` |
| `Hecton8.Physics.Determinism` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Physics/Determinism/Hecton8.Physics.Determinism.asmdef` |
| `Hecton8.Physics.KCC.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Physics/KCC/Editor/Hecton8.Physics.KCC.Editor.asmdef` |
| `Hecton8.Physics.Vehicles.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Physics/Vehicles/Editor/Hecton8.Physics.Vehicles.Editor.asmdef` |
| `Hecton8.Physiology` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Physiology/Hecton8.Physiology.asmdef` |
| `Hecton8.Physiology.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Physiology/Editor/Hecton8.Physiology.Editor.asmdef` |
| `Hecton8.PlayModeTests` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Tests/PlayMode/Hecton8.PlayModeTests.asmdef` |
| `Hecton8.Plugins` | `Hecton8.Core` | `Assets/_Project/Scripts/Plugins/Hecton8.Plugins.asmdef` |
| `Hecton8.Power.BatteryChargerLogistics.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/Power/Editor/Hecton8.Power.BatteryChargerLogistics.Editor.asmdef` |
| `Hecton8.Power.BatteryChargerLogistics.Runtime` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Power/BatteryChargerLogistics/Hecton8.Power.BatteryChargerLogistics.Runtime.asmdef` |
| `Hecton8.Power.Generators` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Power/Generators/Hecton8.Power.Generators.asmdef` |
| `Hecton8.Project.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Editor/Hecton8.Project.Editor.asmdef` |
| `Hecton8.Prologue.Space` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Prologue/Space/Hecton8.Prologue.Space.asmdef` |
| `Hecton8.Prologue.VFX` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Prologue/VFX/Hecton8.Prologue.VFX.asmdef` |
| `Hecton8.QA` | `Hecton8.Core`, `Hecton8.Core.Memory`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/QA/Hecton8.QA.asmdef` |
| `Hecton8.QA.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/QA/Editor/Hecton8.QA.Editor.asmdef` |
| `Hecton8.QA.Headless` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/QA/Headless/Hecton8.QA.Headless.asmdef` |
| `Hecton8.QA.Headless.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/QA/Headless/Editor/Hecton8.QA.Headless.Editor.asmdef` |
| `Hecton8.Rendering.BilateralDrs` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Rendering/BilateralDrs/Hecton8.Rendering.BilateralDrs.asmdef` |
| `Hecton8.Rendering.OceanSinglePass` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Rendering/OceanSinglePass/Hecton8.Rendering.OceanSinglePass.asmdef` |
| `Hecton8.Rendering.WaterOptics` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Rendering/WaterOptics/Hecton8.Rendering.WaterOptics.asmdef` |
| `Hecton8.Rendering.WaterOptics.Editor` | `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Rendering/WaterOptics/Editor/Hecton8.Rendering.WaterOptics.Editor.asmdef` |
| `Hecton8.SaveSystem.EditModeTests` | `Hecton8.Core` | `Assets/_Project/Tests/Editor/SaveSystem/Hecton8.SaveSystem.EditModeTests.asmdef` |
| `Hecton8.SaveSystem.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/Editor/SaveSystem/Hecton8.SaveSystem.Editor.asmdef` |
| `Hecton8.SeedShipAnomaly.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/SeedShipAnomaly/Editor/Hecton8.SeedShipAnomaly.Editor.asmdef` |
| `Hecton8.SeedShipAnomaly.Runtime` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/SeedShipAnomaly/Hecton8.SeedShipAnomaly.Runtime.asmdef` |
| `Hecton8.Thermodynamics` | `Hecton8.Core`, `Hecton8.Core.Memory`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Thermodynamics/Hecton8.Thermodynamics.asmdef` |
| `Hecton8.Tools.ToolKinematics` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Tools/ToolKinematics/Hecton8.Tools.ToolKinematics.asmdef` |
| `Hecton8.Tools.ToolKinematics.Contracts` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/Hecton8.Tools.ToolKinematics.Contracts.asmdef` |
| `Hecton8.Tools.ToolKinematics.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Tools/ToolKinematics/Editor/Hecton8.Tools.ToolKinematics.Editor.asmdef` |
| `Hecton8.UI.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/UI/Editor/Hecton8.UI.Editor.asmdef` |
| `Hecton8.UI.Localization` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/UI/Localization/Hecton8.UI.Localization.asmdef` |
| `Hecton8.UI.Navigation` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/UI/Navigation/Hecton8.UI.Navigation.asmdef` |
| `Hecton8.UI.TerminalOS.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/UI/TerminalOS/Editor/Hecton8.UI.TerminalOS.Editor.asmdef` |
| `Hecton8.UI.Tools` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/UI/Tools/Hecton8.UI.Tools.asmdef` |
| `Hecton8.UI.VR` | `Hecton8.Core`, `Hecton8.Core.Memory`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/UI/VR/Hecton8.UI.VR.asmdef` |
| `Hecton8.VFX.Bioluminescence.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/VFX/Bioluminescence/Editor/Hecton8.VFX.Bioluminescence.Editor.asmdef` |
| `Hecton8.VFX.Bioluminescence.Runtime` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/VFX/Bioluminescence/Hecton8.VFX.Bioluminescence.Runtime.asmdef` |
| `Hecton8.VFX.Debris` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/VFX/Debris/Hecton8.VFX.Debris.asmdef` |
| `Hecton8.VFX.JacobianFoam.Editor` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/VFX/JacobianFoam/Editor/Hecton8.VFX.JacobianFoam.Editor.asmdef` |
| `Hecton8.VFX.JacobianFoam.Runtime` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/VFX/JacobianFoam/Hecton8.VFX.JacobianFoam.Runtime.asmdef` |
| `Hecton8.VFX.Materials` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/VFX/Materials/Hecton8.VFX.Materials.asmdef` |
| `Hecton8.VFX.Parasites.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/VFX/Parasites/Editor/Hecton8.VFX.Parasites.Editor.asmdef` |
| `Hecton8.VFX.Parasites.Runtime` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/VFX/Parasites/Hecton8.VFX.Parasites.Runtime.asmdef` |
| `Hecton8.VFX.PlasmaBeam.Runtime` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/VFX/PlasmaBeam/Hecton8.VFX.PlasmaBeam.Runtime.asmdef` |
| `Hecton8.VFX.Sonar` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/VFX/Sonar/Hecton8.VFX.Sonar.asmdef` |
| `Hecton8.Vehicles.Physics.Contracts` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Vehicles/Physics/Contracts/Hecton8.Vehicles.Physics.Contracts.asmdef` |
| `Hecton8.Vehicles.VFX` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Vehicles/VFX/Hecton8.Vehicles.VFX.asmdef` |
| `Hecton8.World.BiomeWeightMapBaker.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/World/BiomeWeightMapBaker/Editor/Hecton8.World.BiomeWeightMapBaker.Editor.asmdef` |
| `Hecton8.World.BiotaDensityMapBaker.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/World/BiotaDensityMapBaker/Editor/Hecton8.World.BiotaDensityMapBaker.Editor.asmdef` |
| `Hecton8.World.Contracts` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/World/Contracts/Hecton8.World.Contracts.asmdef` |
| `Hecton8.World.Economy` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/Resources/Hecton8.World.Economy.asmdef` |
| `Hecton8.World.Economy.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/Resources/Editor/Hecton8.World.Economy.Editor.asmdef` |
| `Hecton8.World.FloraAmbientSway` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/FloraAmbientSway/Hecton8.World.FloraAmbientSway.asmdef` |
| `Hecton8.World.FloraAmbientSway.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/Editor/FloraAmbientSway/Hecton8.World.FloraAmbientSway.Editor.asmdef` |
| `Hecton8.World.GeographySanity.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/Editor/GeographySanity/Hecton8.World.GeographySanity.Editor.asmdef` |
| `Hecton8.World.HydraulicErosionForge.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/Editor/HydraulicErosionForge/Shinobu242/Hecton8.World.HydraulicErosionForge.Editor.asmdef` |
| `Hecton8.World.OfflineGeology.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/Editor/GeologyForge/Hecton8.World.OfflineGeology.Editor.asmdef` |
| `Hecton8.World.OfflineGeometry.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/Editor/OfflineGeometryBaker/Shinobu213/Hecton8.World.OfflineGeometry.Editor.asmdef` |
| `Hecton8.World.OfflineHadalTrenchBaker.Editor` | `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/Hecton8.World.OfflineHadalTrenchBaker.Editor.asmdef` |
| `Hecton8.World.OfflineWreckageBaker.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/World/OfflineWreckageBaker/Editor/Hecton8.World.OfflineWreckageBaker.Editor.asmdef` |
| `Hecton8.World.Outposts` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/Outposts/Hecton8.World.Outposts.asmdef` |
| `Hecton8.World.ProceduralCoral` | `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/ProceduralCoral/Hecton8.World.ProceduralCoral.asmdef` |
| `Hecton8.World.ProceduralCoral.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/ProceduralCoral/Editor/Hecton8.World.ProceduralCoral.Editor.asmdef` |
| `Hecton8.World.ProceduralWreckage` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/ProceduralWreckage/Hecton8.World.ProceduralWreckage.asmdef` |
| `Hecton8.World.ProceduralWreckage.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/ProceduralWreckage/Editor/Hecton8.World.ProceduralWreckage.Editor.asmdef` |
| `Hecton8.World.ShinobuBiomimetic` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/ShinobuBiomimetic/Hecton8.World.ShinobuBiomimetic.asmdef` |
| `Hecton8.World.ShinobuBiomimetic.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/ShinobuBiomimetic/Editor/Hecton8.World.ShinobuBiomimetic.Editor.asmdef` |
| `Hecton8.World.StaticCaveSdfBaker.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/World/StaticCaveSdfBaker/Editor/Hecton8.World.StaticCaveSdfBaker.Editor.asmdef` |
| `Hecton8.World.VoxelSurfaceNets` | `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/VoxelSurfaceNets/Hecton8.World.VoxelSurfaceNets.asmdef` |
| `Hecton8.World.VoxelSurfaceNets.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/VoxelSurfaceNets/Editor/Hecton8.World.VoxelSurfaceNets.Editor.asmdef` |

### Domain Namespace Edges

Static `using Hecton8.*` edges from first-party source. This exposes compile-time namespace pressure, not runtime coupling proof.

| From domain | To domain | Using count |
|---|---|---:|
| `RootScripts` | `Core` | 342 |
| `World` | `Core` | 243 |
| `Gameplay` | `Core` | 228 |
| `UI` | `Core` | 169 |
| `Editor` | `Core` | 152 |
| `Physics` | `Core` | 132 |
| `RootScripts` | `World` | 91 |
| `Construction` | `Core` | 89 |
| `Editor` | `World` | 75 |
| `Visor` | `Core` | 67 |
| `RootScripts` | `Gameplay` | 62 |
| `Physiology` | `Core` | 61 |
| `AI` | `Core` | 56 |
| `Gameplay` | `World` | 54 |
| `Fauna` | `Core` | 53 |
| `Audio` | `Core` | 51 |
| `VFX` | `Core` | 50 |
| `Atmosphere` | `Core` | 45 |
| `RootScripts` | `Items` | 38 |
| `Power` | `Core` | 34 |
| `Tools` | `Core` | 33 |
| `RootScripts` | `Bootstrap` | 31 |
| `RootScripts` | `Inventory` | 31 |
| `Optimization` | `Core` | 31 |
| `SaveSystem` | `Core` | 31 |
| `UI` | `World` | 31 |
| `World` | `Gameplay` | 30 |
| `RootScripts` | `Environment` | 29 |
| `UI` | `Gameplay` | 29 |
| `Plugins` | `Core` | 28 |
| `Graphics` | `Core` | 27 |
| `World` | `Environment` | 27 |
| `Interaction` | `Core` | 26 |
| `Ecosystem` | `Core` | 25 |
| `RootScripts` | `SaveSystem` | 24 |
| `RootScripts` | `Interaction` | 23 |
| `Construction` | `World` | 23 |
| `Rendering` | `Core` | 23 |
| `RootScripts` | `UI` | 22 |
| `Construction` | `Gameplay` | 22 |
| `Editor` | `Gameplay` | 21 |
| `RootScripts` | `Tools` | 20 |
| `Gameplay` | `Interaction` | 20 |
| `RootScripts` | `Construction` | 18 |
| `RootScripts` | `Building` | 18 |
| `Lighting` | `Core` | 18 |
| `ModdingAPI` | `Core` | 18 |
| `Quest` | `Core` | 18 |
| `Thermodynamics` | `Core` | 18 |
| `Animation` | `Core` | 17 |
| `RootScripts` | `Caves` | 17 |
| `Gameplay` | `Audio` | 17 |
| `UI` | `Bootstrap` | 17 |
| `Fauna` | `World` | 16 |
| `Narrative` | `Core` | 16 |
| `QA` | `Core` | 16 |
| `Construction` | `Power` | 15 |
| `Editor` | `Construction` | 15 |
| `Inventory` | `Core` | 15 |
| `PDA` | `Core` | 15 |
| `Bootstrap` | `Core` | 14 |
| `Physics` | `World` | 14 |
| `RootScripts` | `Audio` | 13 |
| `Editor` | `Physics` | 13 |
| `Editor` | `Items` | 13 |
| `Habitat` | `Core` | 13 |
| `RootScripts` | `Atmosphere` | 12 |
| `Gameplay` | `UI` | 12 |
| `World` | `Caves` | 12 |
| `Core` | `World` | 11 |
| `Editor` | `AI` | 11 |
| `Gameplay` | `Inventory` | 11 |
| `Construction` | `Items` | 10 |
| `Dev` | `Core` | 10 |
| `Ecosystem` | `World` | 10 |
| `Editor` | `Environment` | 10 |
| `RootScripts` | `AI` | 10 |
| `Gameplay` | `Items` | 10 |
| `Gameplay` | `Tools` | 10 |
| `World` | `Bootstrap` | 10 |

## SignalBus<T> Flow Map

`ISignal` structs declared: 299. `SignalBus<T>` lanes observed in producer/consumer calls: 125. Union listed below: 303 signals.
Legacy `GlobalSignals.Publish(...)` call sites found: 1. Many use local variables and are not type-resolved by this static pass; they remain a migration/integrator risk.

| Signal | Declared at | Producers (`SignalBus<T>.Push/Publish`) | Consumers (`GetFrameSnapshot`) |
|---|---|---|---|
| `AcousticPingSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:875` | none found | `Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:770`<br>`Assets/_Project/Scripts/Audio/Editor/ShinobuAcousticDspSmokeTester.cs:111`<br>`Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.AcousticSdf.cs:649`<br>`Assets/_Project/Scripts/FaunaDirector.cs:1001`<br>`Assets/_Project/Scripts/HectonBoidController.cs:1839`<br>`Assets/_Project/Scripts/Physiology/PlayerStressMetricsRuntime.cs:249`<br>`Assets/_Project/Scripts/SpatialAudioManager.cs:6175`<br>`Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:1895`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:4686`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6470` |
| `AcousticShockwaveSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:5715` | none found | none found |
| `AcousticZoneChangedEvent` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:734` | none found | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:481`<br>`Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:1527` |
| `AnomalyProximitySignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:525` | none found | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:911` |
| `AnomalySignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:510` | none found | none found |
| `ApexBrainAcousticEchoTap` | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:217` | none found | none found |
| `ApexPanicSignal` | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:515` | none found | none found |
| `ApexProximitySignal` | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:457` | none found | none found |
| `AtmosphericReentrySignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1366` | none found | `Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:195`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:199`<br>`Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:368` |
| `AudioEvent` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:724` | none found | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:265`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:6457` |
| `AupPreShiftSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:69` | none found | `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2768` |
| `AupShiftSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:79` | none found | `Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs:1100`<br>`Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1030`<br>`Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1371`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.RuntimeLifecycle.cs:376`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:920`<br>`Assets/_Project/Scripts/HectonFluidEngine.cs:4124`<br>`Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:726`<br>`Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:494`<br>`Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:947`<br>`Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:1765`<br>`Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:1789`<br>`Assets/_Project/Scripts/VFX/Parasites/ParasiteSwarmGpuRuntime.cs:1048`<br>... +6 more |
| `AuxiliaryFlareLightSignal` | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentContracts.cs:231` | none found | none found |
| `AuxiliarySonarRequestSignal` | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentContracts.cs:245` | none found | none found |
| `AuxiliaryTetherConnectionSignal` | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentContracts.cs:260` | none found | none found |
| `BaseIntegrityEventPayload` | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorTypes.cs:59` | none found | none found |
| `BaseModuleCompromisedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1796` | none found | none found |
| `BaseStructuralWarningSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:207` | none found | `Assets/_Project/Scripts/Audio/Editor/AbyssalDspTunerWindow.cs:131`<br>`Assets/_Project/Scripts/Audio/Editor/AbyssalDspTunerWindow.cs:258`<br>`Assets/_Project/Scripts/Audio/Editor/Shinobu351HullStressDspSmokeTester.cs:58`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:6464` |
| `BatteryLevelSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:144` | none found | none found |
| `BiomeChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:230` | none found | `Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:830`<br>`Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1121`<br>`Assets/_Project/Scripts/Graphics/Materials/ProceduralFloraBiomeTintBridge.cs:111`<br>`Assets/_Project/Scripts/PlayerInventory.cs:4924`<br>`Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs:1691` |
| `BiomeGradientSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:241` | none found | `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:1509`<br>`Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs:852`<br>`Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:1518`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:2182` |
| `BlueprintUnlockedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1192` | none found | none found |
| `BootstrapEventPayload` | `Assets/_Project/Scripts/Bootstrap/BootstrapEvents.cs:21` | none found | `Assets/_Project/Scripts/Bootstrap/BootstrapEvents.cs:234` |
| `BrownoutSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:372` | none found | `Assets/_Project/Scripts/SpatialAudioManager.cs:9726` |
| `BubbleSpawnSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:130` | none found | none found |
| `BulletTimeVisualSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:359` | none found | none found |
| `CameraFrustumSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1640` | none found | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1012`<br>`Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1648` |
| `CameraJuiceImpactSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:934` | none found | none found |
| `CameraPositionSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1628` | none found | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:2220`<br>`Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:665`<br>`Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:999` |
| `CardiacPulseSignal` | `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyData.cs:752` | none found | none found |
| `CavitationAcousticSignal` | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:486` | none found | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1969` |
| `ChunkDehydratedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:963` | none found | none found |
| `CombatDamageSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1656` | none found | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.FlockingAvoidance.cs:82`<br>`Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1107`<br>`Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:1260`<br>`Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:983`<br>`Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.AcousticSdf.cs:696`<br>`Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:1105`<br>`Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:893`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1126`<br>`Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageRuntime.cs:652`<br>`Assets/_Project/Scripts/Physiology/PlayerStressMetricsRuntime.cs:263`<br>`Assets/_Project/Scripts/Vehicles/VFX/HullDentShaderController.cs:239`<br>`Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs:1804`<br>... +3 more |
| `CombatDamageSignalAupShiftTransformer` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1921` | none found | none found |
| `CompassCalibratedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:568` | none found | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:924` |
| `ComplianceViolationSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:287` | none found | none found |
| `ConstructionPreviewSignal` | `Assets/_Project/Scripts/Construction/ConstructionSignals.cs:20` | none found | `Assets/_Project/Scripts/Construction/FoundationPylonGpuBatch.cs:548`<br>`Assets/_Project/Scripts/Construction/HectonBlueprintPreviewBatch.cs:427` |
| `ControlSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:496` | none found | none found |
| `CoreCombatDamageSignal` | not found as local `ISignal` declaration | none found | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:1520` |
| `CoreHackedSignal` | `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs:49` | none found | `Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:1009` |
| `CpuStarvationSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:860` | none found | none found |
| `CraftingCompletedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1222` | none found | `Assets/_Project/Scripts/PDA/PDALogbookManager.cs:589` |
| `CraftingStartedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1207` | none found | none found |
| `CrashTelemetrySignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:607` | none found | none found |
| `CrushWarningSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:698` | none found | none found |
| `CullingOverloadSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:247` | none found | none found |
| `DataReloadSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:763` | none found | none found |
| `DataVaultUpdateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:304` | none found | none found |
| `DebrisAvalancheSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:5688` | none found | none found |
| `DebrisDestroyedSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:301` | none found | none found |
| `DebrisSpawnSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:387` | none found | `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:936` |
| `DebugSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:397` | none found | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:749` |
| `DeconstructRequestSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:636` | none found | none found |
| `DeconstructResultSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:652` | none found | none found |
| `DeferredSubmarineImpactSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:1201` | none found | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:907`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:3595` |
| `DeflectSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:407` | none found | none found |
| `DeltaCrusherMockLaserFireSignal` | `Assets/_Project/Scripts/VFX/Debris/ShinobuDeltaCrusherJobs.cs:37` | none found | none found |
| `DesyncDetectedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:591` | none found | none found |
| `DiegeticHudSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:50` | none found | none found |
| `DirectorAIMusicSignal` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:546` | none found | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:485`<br>`Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:1542` |
| `DockingCompleteSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:821` | none found | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1233` |
| `DockingFailedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:842` | none found | none found |
| `DockingRequestSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:800` | none found | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:4025` |
| `DroneFleetInventoryTransactionSignal` | `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs:1009` | none found | none found |
| `DroneFleetMockMiningSignal` | `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs:998` | none found | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5469` |
| `DroneFleetMockRepairSignal` | `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs:987` | none found | none found |
| `DropPodLandedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:89` | none found | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:2015` |
| `DynamicMusicScalarSignal` | `Assets/_Project/Scripts/Core/Contracts/Signals/DynamicMusicScalarSignal.cs:14` | none found | `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:1242` |
| `EclipseGameplayEventPayload` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:5667` | none found | none found |
| `EncumbranceSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:249` | none found | none found |
| `EncyclopediaUnlockSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:301` | none found | none found |
| `EntityDeathSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:434` | none found | `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs:1663`<br>`Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime_Carrion.cs:309`<br>`Assets/_Project/Scripts/HectonDirectorAI.cs:933`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:6084` |
| `EntityDepletedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:315` | none found | none found |
| `EntitySpawnSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:450` | none found | none found |
| `EquipItemSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:267` | none found | none found |
| `EquipmentOverheatSignal` | `Assets/_Project/Scripts/Tools/EquipmentThermalBatteryContracts.cs:101` | none found | none found |
| `ExosuitAcousticEchoTap` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsContracts.cs:84` | none found | none found |
| `FabricationCompletedSignal` | `Assets/_Project/Scripts/FabricationAssemblerRuntime.cs:72` | none found | none found |
| `FabricationTickSignal` | `Assets/_Project/Scripts/FabricationAssemblerRuntime.cs:88` | none found | none found |
| `FaunaStateChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1539` | none found | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:2238`<br>`Assets/_Project/Scripts/Fauna/FaunaBrain.cs:4550`<br>`Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1490` |
| `FlashlightEventPayload` | `Assets/_Project/Scripts/PlayerFlashlight.cs:58` | none found | `Assets/_Project/Scripts/PlayerFlashlight.cs:159` |
| `FloraExclusionSignal` | `Assets/_Project/Scripts/Construction/ConstructionSignals.cs:54` | none found | none found |
| `FloraSpawnedSignal` | `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeContracts.cs:639` | none found | none found |
| `FluidDensityChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1094` | none found | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:876` |
| `FluidImpulseSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:111` | none found | none found |
| `FluidIncursionSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1053` | none found | `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:802`<br>`Assets/_Project/Scripts/Physics/HabitatFluidIncursionDirector.cs:597` |
| `FocusBrokenSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:301` | none found | none found |
| `FoundationStructuralWarningSignal` | `Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorData.cs:170` | none found | none found |
| `FramePacingWarningSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1898` | none found | none found |
| `FrameTimeSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:145` | none found | `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs:1709`<br>`Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1513`<br>`Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:1878` |
| `GameBootstrapperEventPayload` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:68` | none found | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:716` |
| `GlobalPanicSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:5742` | none found | none found |
| `GlobalTimeSyncSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:315` | none found | `Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs:2068` |
| `GlobalWorldStateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:209` | none found | none found |
| `HUDNotificationSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:35` | none found | none found |
| `HabitatConstructionSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:623` | none found | none found |
| `HabitatFloodAcousticMuffleSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:78` | none found | none found |
| `HapticPulseSignal` | `Assets/_Project/Scripts/Core/Contracts/Signals/HapticPulseSignal.cs:10` | none found | none found |
| `HapticRequest` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:80` | none found | none found |
| `HashDeltaUpdateSignal` | `Assets/_Project/Scripts/Core/Contracts/MemorySentinelSignals.cs:30` | none found | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1232` |
| `HighSpeedImpactSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:32` | none found | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.FlockingAvoidance.cs:69`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:259`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:4471`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2831`<br>`Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:725`<br>`Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2106`<br>`Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs:1768` |
| `HullDeformedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1746` | none found | `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:1269`<br>`Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:1507`<br>`Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2089` |
| `HullRepairedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1775` | none found | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1171` |
| `HypoxiaSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:991` | none found | none found |
| `ImpactSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:10` | none found | `Assets/_Project/Scripts/World/SoundscapeSystem.cs:781` |
| `InputSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:525` | none found | none found |
| `InputStateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:990` | none found | `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:802`<br>`Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:766`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1860`<br>`Assets/_Project/Scripts/UI/InteractionUI.cs:896`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:6093` |
| `InteractionUiSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1021` | none found | none found |
| `InventoryChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:308` | none found | `Assets/_Project/Scripts/Editor/SoaInventoryXRayWindow_SHINOBU316.cs:125`<br>`Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:734`<br>`Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:1030`<br>`Assets/_Project/Scripts/HectonPlayerMovement.cs:4713`<br>`Assets/_Project/Scripts/PDAInventoryTab.cs:552`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:855`<br>`Assets/_Project/Scripts/PlayerToolManager.cs:2234`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:2911`<br>`Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs:300`<br>`Assets/_Project/Scripts/UI/PDAConstructionTab.cs:424`<br>`Assets/_Project/Scripts/UI/PDALoadoutTab.cs:389`<br>`Assets/_Project/Scripts/UI/PDAShellChrome.cs:597`<br>... +1 more |
| `InventoryCommandSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:226` | none found | `Assets/_Project/Scripts/PlayerInventory.cs:2554` |
| `InventoryDeathLootCacheSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:262` | none found | `Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:706`<br>`Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:723` |
| `InventoryRespawnDeathAupSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:242` | none found | `Assets/_Project/Scripts/PlayerInventory.cs:2688` |
| `InventoryRespawnPenaltyResultSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:290` | none found | `Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs:1144` |
| `ItemAcquiredSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:372` | none found | `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs:1674`<br>`Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:1930`<br>`Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:1952`<br>`Assets/_Project/Scripts/PlayerInventory.cs:4938`<br>`Assets/_Project/Scripts/PlayerInventory.cs:4964`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:6095` |
| `ItemDecaySignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1450` | none found | none found |
| `ItemDurabilityChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:322` | none found | `Assets/_Project/Scripts/HUDQuickBar.cs:392`<br>`Assets/_Project/Scripts/PlayerToolManager.cs:2266`<br>`Assets/_Project/Scripts/UI/PDALoadoutTab.cs:408`<br>`Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:281` |
| `ItemLifecycleSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:343` | none found | `Assets/_Project/Scripts/Meta/GlobalProfileManager.cs:429`<br>`Assets/_Project/Scripts/World/EnvironmentalStrainManager.cs:272` |
| `KccVelocitySignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:649` | none found | `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs:1386`<br>`Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs:1401` |
| `KillSwitchSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:165` | none found | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1507` |
| `LaserCutterEventPayload` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:1070` | none found | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:263`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:6197` |
| `LaserCutterEventPayloadSignal` | not found as local `ISignal` declaration | none found | `Assets/_Project/Scripts/LaserCutter.cs:199` |
| `LightLevelSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1473` | none found | `Assets/_Project/Scripts/Physiology/PlayerStressMetricsRuntime.cs:573` |
| `LocalizationLanguageChangedSignal` | `Assets/_Project/Scripts/LocRegistry.cs:443` | none found | none found |
| `LockstepSnapshotSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:1008` | none found | none found |
| `LogisticsTransferSignal` | `Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs:182` | none found | none found |
| `LoreFragmentScannedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1178` | none found | `Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs:771` |
| `MacroCollisionSignal` | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1295` | none found | none found |
| `MacroDatabaseSectorHydrationSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:187` | none found | `Assets/_Project/Scripts/SaveManager.cs:1481`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:4360`<br>`Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:649` |
| `ManualOverridePulledSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:10` | none found | none found |
| `MechHapticSignalDTO` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsContracts.cs:49` | none found | none found |
| `MemoryAddressShiftSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:792` | none found | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/VaultMemoryGizmoVisualizer.cs:42`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:2116` |
| `MemoryDesyncSignal` | `Assets/_Project/Scripts/Core/Contracts/MemorySentinelSignals.cs:6` | none found | none found |
| `MemoryPressureSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:778` | none found | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:714`<br>`Assets/_Project/Scripts/UI/PDAShellChrome.cs:568`<br>`Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4693` |
| `MemorySentinelRollbackSignal` | `Assets/_Project/Scripts/Core/Contracts/MemorySentinelSignals.cs:51` | none found | none found |
| `MixerStateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:317` | none found | none found |
| `MockAcousticSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:183` | none found | none found |
| `MockAupRebaseSignal` | `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs:25` | none found | none found |
| `MockCarveRequestSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:195` | none found | none found |
| `MockCombatDamageSignal` | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:490` | none found | none found |
| `MockConsumeSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:198` | none found | none found |
| `MockCraftingRequestSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:181` | none found | none found |
| `MockDamageSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:197` | none found | none found |
| `MockFloodSignal` | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:451` | none found | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:897` |
| `MockHotbarSelectSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:284` | none found | none found |
| `MockHudSignal` | `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs:63` | none found | none found |
| `MockImpactSignal` | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:469` | none found | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:922` |
| `MockInventoryTransactionSignal` | `Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:294` | none found | none found |
| `MockItemAcquiredSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:164` | none found | none found |
| `MockLaserFireSignal` | `Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:77` | none found | none found |
| `MockNarrativeTriggerSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:5622` | none found | none found |
| `MockPlayerFootstepSignal` | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1216` | none found | none found |
| `MockPlayerPositionSignal` | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:213` | none found | none found |
| `MockPredatorSignal` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:4083` | none found | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:2256` |
| `MockQualityWeightSignal` | `Assets/_Project/Scripts/Core/Contracts/DrsContracts.cs:298` | none found | none found |
| `MockReconstructionInputSignal` | `Assets/_Project/Scripts/Core/Contracts/DrsContracts.cs:330` | none found | none found |
| `MockRockCollisionSignal` | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1278` | none found | none found |
| `MockStoryEventSignal` | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:198` | none found | none found |
| `MockTextRequestSignal` | `Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:1773` | none found | none found |
| `MockToolUsedSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:215` | none found | none found |
| `MockTriggerPullSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:181` | none found | none found |
| `ModAssetReferenceSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:169` | none found | none found |
| `ModFutureDevNullSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:211` | none found | none found |
| `ModHapticPulseSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:236` | none found | none found |
| `ModInteractionRejectedPayload` | `Assets/_Project/Scripts/ModdingAPI/ModSpatialContracts.cs:254` | none found | none found |
| `ModSpawnRequestSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:156` | none found | none found |
| `ModSubtitleCueSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:249` | none found | none found |
| `ModdedGameMaskSignal` | `Assets/_Project/Scripts/Core/Contracts/MemorySentinelSignals.cs:68` | none found | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:642` |
| `ModuleDeconstructSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:670` | none found | none found |
| `MovementAcousticSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:906` | none found | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.FlockingAvoidance.cs:55`<br>`Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:733`<br>`Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.AcousticSdf.cs:603`<br>`Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:844`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:6443` |
| `NarrativeFocusSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:272` | none found | none found |
| `NarrativeHudWaypointSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:334` | none found | none found |
| `NarrativePoiStateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:358` | none found | `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:1679`<br>`Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1133` |
| `OxygenCriticalSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1006` | none found | none found |
| `PdaExchangeStateChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:89` | none found | `Assets/_Project/Scripts/UI/PDABarterTab.cs:212`<br>`Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:945` |
| `PhysicsEventPayload` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:1155` | none found | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:134`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:260`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:6342`<br>`Assets/_Project/Scripts/Gameplay/TraumaDispatcher.cs:804`<br>`Assets/_Project/Scripts/HectonDirectorAI.cs:960`<br>`Assets/_Project/Scripts/Interaction/VRLeakPatchWeldTarget.cs:219`<br>`Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs:3089`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:843`<br>`Assets/_Project/Scripts/SpatialAudioManager.cs:6165`<br>`Assets/_Project/Scripts/Tools/ToolHapticsRuntime.cs:411`<br>`Assets/_Project/Scripts/UI/AcousticEcholocationTranslator.cs:472`<br>`Assets/_Project/Scripts/Visor/SpectrumSystem.cs:2297` |
| `PhysiologyStateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1552` | none found | `Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs:650`<br>`Assets/_Project/Scripts/HectonSurvivalSystem.cs:1367`<br>`Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs:1446` |
| `PipeRuptureSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1107` | none found | none found |
| `PlasmaBeamAcousticEchoTap` | `Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:92` | none found | none found |
| `PlayVoiceOverSignal` | `Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:1786` | none found | none found |
| `PlayerActionCancelledSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:197` | none found | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:152` |
| `PlayerActionCompletedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:181` | none found | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:148` |
| `PlayerActionProgressSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:162` | none found | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:144` |
| `PlayerBaseEnterSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1824` | none found | `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:862`<br>`Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:1407` |
| `PlayerBaseExitSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1839` | none found | `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:881`<br>`Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:1395` |
| `PlayerExhaleSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:121` | none found | `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:6227`<br>`Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:1095`<br>`Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs:391` |
| `PlayerFatalPressureSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:161` | none found | `Assets/_Project/Scripts/UI/HectonOSBootManager.cs:323`<br>`Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:1116` |
| `PlayerFootstepSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:19` | none found | `Assets/_Project/Scripts/PlayerFootstepAudio.cs:274` |
| `PlayerInputSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:1253` | none found | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:578`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:595`<br>`Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs:2283`<br>`Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs:2304`<br>`Assets/_Project/Scripts/HectonFabricatorUI.cs:722`<br>`Assets/_Project/Scripts/HectonFabricatorUI.cs:748`<br>`Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:313`<br>`Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:330`<br>`Assets/_Project/Scripts/MainMenuController.cs:931`<br>`Assets/_Project/Scripts/MainMenuController.cs:947`<br>`Assets/_Project/Scripts/PlayerBuilder.cs:801`<br>`Assets/_Project/Scripts/PlayerBuilder.cs:818`<br>... +7 more |
| `PlayerLookTargetSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:1272` | none found | `Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:463` |
| `PlayerRespawnSignal` | `Assets/_Project/Scripts/Core/Contracts/Signals/PlayerRespawnSignal.cs:28` | none found | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:1177`<br>`Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs:3884`<br>`Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs:214`<br>`Assets/_Project/Scripts/PlayerInventory.cs:2665` |
| `PlayerSprintStateSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:141` | none found | `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs:1179` |
| `PlayerStateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:106` | none found | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:2209`<br>`Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:657`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2385`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2856`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2957`<br>`Assets/_Project/Scripts/Physiology/PlayerStressMetricsRuntime.cs:289`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:6054` |
| `PlayerStressSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1597` | none found | `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2874`<br>`Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:310` |
| `PlayerTransportBailoutSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:178` | none found | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1384` |
| `PlayerWaterSplashSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:52` | none found | `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:1098`<br>`Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:1103` |
| `PowerDrainSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1302` | none found | none found |
| `PrefabAcousticSignatureSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:328` | none found | none found |
| `PrefabLoreLinkSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:352` | none found | none found |
| `ProgressionEventSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:156` | none found | none found |
| `ProgressionMetaSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:169` | none found | `Assets/_Project/Scripts/Meta/DynamicDifficultyDirector.cs:128`<br>`Assets/_Project/Scripts/Meta/GlobalProfileManager.cs:335`<br>`Assets/_Project/Scripts/PDA/PDALogbookManager.cs:676`<br>`Assets/_Project/Scripts/Progression/PlayerAchievementRegistry.cs:369` |
| `PrologueCompleteSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1385` | none found | `Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:235`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:229`<br>`Assets/_Project/Scripts/HectonFluidEngine.cs:3779`<br>`Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:418` |
| `QuestDagMockItemAcquiredSignal` | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:229` | none found | none found |
| `RadarJamSignal` | `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs:34` | none found | none found |
| `RadiationDoseSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:385` | none found | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:2063`<br>`Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:1362`<br>`Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs:1099`<br>`Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:928` |
| `RadiationSourceSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:39` | none found | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:2018`<br>`Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:2042` |
| `ReactorDamageSignal` | `Assets/_Project/Scripts/Core/Contracts/Signals/ReactorDamageSignal.cs:13` | none found | `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:822` |
| `RebaseSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:486` | none found | none found |
| `ReconDataSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:159` | none found | none found |
| `ReentryVfxStateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:466` | none found | none found |
| `ResidencySectorDehydratedSignal` | not found as local `ISignal` declaration | none found | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4724`<br>`Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:5355` |
| `ResidencySectorHydratedSignal` | not found as local `ISignal` declaration | none found | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:5360` |
| `ResolutionChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:817` | none found | none found |
| `ResourceDepletionDeltaSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:55` | none found | none found |
| `RespawnSignalResolvedTargetTransformer` | `Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs:1879` | none found | none found |
| `RigidbodySleepSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1135` | none found | none found |
| `RollbackRequiredSignal` | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs:460` | none found | none found |
| `SaveCompletedSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:278` | none found | none found |
| `SaveLifecycleSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:172` | none found | none found |
| `SaveMetadataReadySignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:261` | none found | none found |
| `SaveStatusSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:294` | none found | none found |
| `ScalabilityChangedEvent` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:37` | none found | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:145`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:289`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:351`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:368`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:527`<br>`Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:560`<br>`Assets/_Project/Scripts/Core/IPlatformIntegration.cs:150` |
| `ScanCompleteSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1165` | none found | `Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs:759` |
| `ScanLogChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:69` | none found | `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:752`<br>`Assets/_Project/Scripts/PDA/PDALogbookManager.cs:611` |
| `ScannerToolActiveSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1148` | none found | `Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:443`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:6116`<br>`Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs:744` |
| `SeaglidePropulsionRequestSignal` | `Assets/_Project/Scripts/Core/Contracts/Physics/SeaglidePropulsionContracts.cs:86` | none found | `Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsRuntime.cs:527` |
| `SectorDehydratedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:948` | none found | `Assets/_Project/Scripts/World/EcosystemDirector.cs:4344` |
| `SectorResidencyHydratedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:933` | none found | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:274`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:4369` |
| `SeismicShockwaveSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:5644` | none found | none found |
| `SeismicSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1402` | none found | none found |
| `SessionLifecycleSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:188` | none found | `Assets/_Project/Scripts/Meta/DynamicDifficultyDirector.cs:153`<br>`Assets/_Project/Scripts/Meta/GlobalProfileManager.cs:320`<br>`Assets/_Project/Scripts/Meta/RunModifierController.cs:227`<br>`Assets/_Project/Scripts/PDA/PDALogbookManager.cs:557`<br>`Assets/_Project/Scripts/Progression/PDAContextualAdvisorySystem.cs:410`<br>`Assets/_Project/Scripts/Progression/PlayerAchievementRegistry.cs:389`<br>`Assets/_Project/Scripts/UI/HectonOSBootManager.cs:261` |
| `ShinobuPlayerExertionSignal` | `Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:20` | none found | none found |
| `SignalWardenMockDamageSignal` | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1257` | none found | none found |
| `SiltExplosionSignal` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsContracts.cs:73` | none found | none found |
| `SimulationBucketSyncSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1883` | none found | none found |
| `SimulationPauseSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:344` | none found | none found |
| `SolarFlareSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:471` | none found | none found |
| `SonarPingSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:978` | none found | none found |
| `SoundscapeProfileSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:347` | none found | none found |
| `SpectrumScanSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1120` | none found | none found |
| `SplashEvent` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:1099` | none found | `Assets/_Project/Scripts/Physics/FluidFeedbackListener.cs:164` |
| `StateChangedSignal` | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:183` | none found | none found |
| `StateCorrectionSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:556` | none found | none found |
| `StorageDebtSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1332` | none found | none found |
| `StreamingTurbulenceSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1353` | none found | none found |
| `SubmarineFloodStateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1071` | none found | `Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1169`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:888` |
| `SubmarineLightsChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1501` | none found | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:678`<br>`Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:3884`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:4558` |
| `SubtitleCueSignal` | `Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs:22` | none found | `Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs:576` |
| `SubtitleSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:714` | none found | `Assets/_Project/Scripts/UI/SubtitleManager.cs:750` |
| `SurvivalOverrideSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:224` | none found | none found |
| `SurvivalVitalsChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:146` | none found | `Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1114`<br>`Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs:1688`<br>`Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:931`<br>`Assets/_Project/Scripts/UI/SuitAdvisoryController.cs:177`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:4804`<br>`Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:916`<br>`Assets/_Project/Scripts/Visor/VisorHUDController.cs:1589` |
| `SwarmDispersedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:920` | none found | none found |
| `SyncFenceSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:616` | none found | none found |
| `SystemGlitchSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:1030` | none found | `Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:1123` |
| `SystemHealthIndexSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:840` | none found | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:2278`<br>`Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1096`<br>`Assets/_Project/Scripts/Core/InputDispatcher.cs:3612`<br>`Assets/_Project/Scripts/Core/SystemDispatcher.cs:3215`<br>`Assets/_Project/Scripts/Core/SystemDispatcher.cs:4926`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2927`<br>`Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:770`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1200`<br>`Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:711`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1789`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:864`<br>`Assets/_Project/Scripts/QA/Headless/Shinobu38QaWatchdogRuntime.cs:1801`<br>... +5 more |
| `SystemHealthSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:426` | none found | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1499`<br>`Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:701`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:883`<br>`Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:419`<br>`Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1251`<br>`Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1672`<br>`Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:939`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:6111`<br>`Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:1959` |
| `SystemKillSwitchBitsSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:184` | none found | `Assets/_Project/Scripts/HectonFluidEngine.cs:7067`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5217` |
| `SystemPauseSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1869` | none found | none found |
| `TelemetryAnomalySignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:592` | none found | none found |
| `TemperatureChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:11` | none found | none found |
| `TerminalClickSignal` | `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:219` | none found | none found |
| `TerminalCommandSignal` | `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:235` | none found | none found |
| `TerminalUnlockedSignal` | `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:296` | none found | none found |
| `TerrainChunkGeneratedSignal` | `Assets/_Project/Scripts/World/Contracts/TerrainChunkGeneratedSignal.cs:13` | none found | none found |
| `TetherFiredSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:769` | none found | none found |
| `TetherSnappedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:732` | none found | `Assets/_Project/Scripts/Physics/TetherSignals.cs:116` |
| `TetherTensionSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:687` | none found | none found |
| `ThermalSourceSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:28` | none found | `Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.cs:545`<br>`Assets/_Project/Scripts/VFX/Parasites/ParasiteSwarmGpuRuntime.cs:683` |
| `ThermalStateChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:127` | none found | `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:718`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:913`<br>`Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:441`<br>`Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs:1759` |
| `ThermalUpdraftSignal` | `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardTypes.cs:77` | none found | none found |
| `ThermodynamicsMockDamageSignal` | `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardTypes.cs:96` | none found | none found |
| `TimeDilationSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:329` | none found | none found |
| `ToolAcousticSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1279` | none found | none found |
| `ToolBrokenSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:232` | none found | none found |
| `ToolDepletedSignal` | `Assets/_Project/Scripts/Tools/EquipmentThermalBatteryContracts.cs:116` | none found | none found |
| `ToolHeatSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:215` | none found | none found |
| `ToolLoadoutChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1256` | none found | `Assets/_Project/Scripts/Gameplay/PlayerTransportCoordinator.cs:384`<br>`Assets/_Project/Scripts/HUDQuickBar.cs:368`<br>`Assets/_Project/Scripts/PDAInventoryTab.cs:621`<br>`Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs:341`<br>`Assets/_Project/Scripts/UI/PDAConstructionTab.cs:465`<br>`Assets/_Project/Scripts/UI/PDALoadoutTab.cs:443`<br>`Assets/_Project/Scripts/UI/PDAShellChrome.cs:637` |
| `ToolStateChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1236` | none found | none found |
| `ToolTriggerSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1317` | none found | none found |
| `ToxicBioluminescenceSignal` | `Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryTypes.cs:165` | none found | none found |
| `ToxicityExposureSignal` | `Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryTypes.cs:144` | none found | `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:1312` |
| `TraumaSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1612` | none found | none found |
| `UIRescaleRequestSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1039` | none found | none found |
| `VehicleCommandSignal` | `Assets/_Project/Scripts/Gameplay/VehicleCommandSignals.cs:26` | none found | none found |
| `VehicleHazardSignal` | `Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageContracts.cs:202` | none found | none found |
| `VehicleUpgradesChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:110` | none found | none found |
| `VfxSparkRequestSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:232` | none found | none found |
| `VisorBreachSignal` | `Assets/_Project/Scripts/Visor/DiegeticVisorLensTypes.cs:125` | none found | none found |
| `VisorDropletSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:499` | none found | `Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs:421` |
| `VisualFlareSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:903` | none found | none found |
| `VisualScavengeSignal` | `Assets/_Project/Scripts/Core/Contracts/Signals/VisualScavengeSignal.cs:29` | none found | none found |
| `VitalWarningSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:683` | none found | none found |
| `VocalCueSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:744` | none found | `Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs:671` |
| `VocalWarningSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:729` | none found | none found |
| `VoxelCarveEvent` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:863` | none found | `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:897` |
| `WakeGeneratedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:102` | none found | `Assets/_Project/Scripts/World/FloraInteractionManager.cs:3147` |
| `WakeRequestSignal` | `Assets/_Project/Scripts/Core/Signals/PhysicsWakeSignalContracts.cs:17` | none found | none found |
| `WaterTransitionSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:92` | none found | `Assets/_Project/Scripts/Gameplay/WaterTransitionHandler.cs:60` |
| `WaterlineBreachSignal` | `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereContracts.cs:1496` | none found | `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:1278` |
| `WeatherChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1854` | none found | none found |
| `WeatherStrengthSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1435` | none found | none found |
| `WfcOutpostDoorPowerSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:238` | none found | `Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:1234` |
| `WfcOutpostGeneratedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:201` | none found | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1160`<br>`Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:382`<br>`Assets/_Project/Scripts/Tools/WfcLaserCutRuntime.cs:310` |
| `WfcOutpostStateChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:222` | none found | `Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs:106`<br>`Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1201`<br>`Assets/_Project/Scripts/SaveManager.cs:1304` |

## Queue-Backed Signal Lanes

Queue-backed lanes parsed from `Docs/ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md`: 0.

| Lane Owner | Backing Queue | Listener Contract | Raise Surface | Flush Owner |
|---|---|---|---|---|

## VRAM Map

Mandate target for MX350 from performance budget: total VRAM ceiling 1800 MiB; texture budget 900 MiB; render targets and depth 320 MiB; shadow maps 128 MiB; geometry buffers 200 MiB; compute/UAV 128 MiB; shader constant pools 64 MiB; post-process chain 96 MiB; driver reserve 164 MiB. Guard: used/total > 0.90 triggers mip downgrade.

No VRAM budget audit JSON was found under reports.

## Selected Signal Route Snapshot

Static source view for high-value gameplay/UX signal lanes. Task and agent-log folders are intentionally excluded: batch prompts and agent logs are process evidence, not architecture authority.

| Signal | Declared at | Producers | Consumers |
|---|---|---|---|
| `PlayerActionProgressSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:162` | none found | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:144` |
| `PlayerActionCompletedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:181` | none found | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:148` |
| `PlayerActionCancelledSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:197` | none found | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:152` |
| `PdaExchangeStateChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:89` | none found | `Assets/_Project/Scripts/UI/PDABarterTab.cs:212`<br>`Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:945` |
| `VehicleUpgradesChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:110` | none found | none found |
| `PlayerStateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:106` | none found | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:2209`<br>`Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:657`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2385`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2856`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2957`<br>`Assets/_Project/Scripts/Physiology/PlayerStressMetricsRuntime.cs:289`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:6054` |
| `InventoryChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:308` | none found | `Assets/_Project/Scripts/Editor/SoaInventoryXRayWindow_SHINOBU316.cs:125`<br>`Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:734`<br>`Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:1030`<br>`Assets/_Project/Scripts/HectonPlayerMovement.cs:4713`<br>`Assets/_Project/Scripts/PDAInventoryTab.cs:552`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:855`<br>`Assets/_Project/Scripts/PlayerToolManager.cs:2234`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:2911` |
| `InputStateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:990` | none found | `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:802`<br>`Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:766`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1860`<br>`Assets/_Project/Scripts/UI/InteractionUI.cs:896`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:6093` |
| `SystemHealthSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:426` | none found | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1499`<br>`Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:701`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:883`<br>`Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:419`<br>`Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1251`<br>`Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1672`<br>`Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:939`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:6111` |

## Phi-Resonance Connectivity Model

The engine connectivity model is not mystical. It is a three-layer resonance model: contracts define stable frequency, typed signal lanes carry bounded pulses, and dispatcher phases control when state can move. `GlobalRegistry` is the cold authority spine for stable interfaces. `SignalBus<T>` and queue-backed event lanes are the nervous system for broadcast state. DataVault/native buffers are the mass: owned memory that simulation mutates and presentation samples.

Low/MX350 interpretation: coalesce lanes, consume bounded snapshots, keep optional visual consumers in `VISUAL_SYNC`, and shed mips/LOD before simulation truth. High/Ultra interpretation: do not increase gameplay broadcast cost; attach richer presentation consumers to the same snapshots and spend the saved budget on light, fog, HUD, audio, and material overkill.

Connectivity risk: `Hecton8.Core` currently references many domain assemblies directly. That may be intentional current architecture, but it is not a clean inward-only core. The integrator should treat this as a dependency inversion watchpoint, especially while many agents expand contracts in parallel.

## Verification Commands

- `python Tools/BuildArchitectureAtlas.py`
- `python Tools/AtlasCheck.py`
- `python -m py_compile Tools/BuildArchitectureAtlas.py Tools/AtlasCheck.py`
- C# compile verification is outside this atlas; run Unity import/Console and serial CLI builds as separate evidence.
- AtlasCheck gate: `python Tools/AtlasCheck.py` exits `0` with `ATLAS_CHECK_PASS references=5795 atlas=C:\hades\Hecton8\Docs\Generated\DEPENDENCY_GRAPH.md`. This is static reference integrity only, not Unity/runtime proof.
- This generated atlas is not `VERIFIED` unless `Tools/AtlasCheck.py` exits `0` after generation.

## Residual Risk

- STATIC_SOURCE only: Unity import, runtime wiring, actual VRAM residency, profiler frame time, GC, build, and Play Mode remain PENDING VERIFICATION.
- The signal producer map only resolves explicit `SignalBus<T>` calls. Legacy `GlobalSignals.Publish(...)` variable publishes require Roslyn-level dataflow to type-resolve fully.
- Task and agent-log folders are intentionally excluded from architecture authority sections.
