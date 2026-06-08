# HECTON-8 Architecture Atlas - Dependency Graph

Generated: 2026-06-08 03:59:08
Date: 2026-06-08
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

- C# source files scanned under `Assets/` and `Packages/`: 5349
- C# line count scanned under `Assets/` and `Packages/`: 2,669,084
- First-party C# source files under `Assets/_Project/Scripts/`: 2547
- First-party C# line count under `Assets/_Project/Scripts/`: 2,055,325
- Assembly definitions scanned: 225
- First-party assembly definitions under `Assets/_Project/`: 169
- Markdown docs under `Docs/`: 17572

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

Assemblies directly depending on exact `Hecton8.Core`: 109

| Assembly | Path |
|---|---|
| `Hecton8.AI.Ambient` | `Assets/_Project/Scripts/AI/Ambient/Hecton8.AI.Ambient.asmdef` |
| `Hecton8.AI.Cognition.Editor` | `Assets/_Project/Scripts/AI/Cognition/Editor/Hecton8.AI.Cognition.Editor.asmdef` |
| `Hecton8.AI.Pathfinding` | `Assets/_Project/Scripts/AI/Pathfinding/Hecton8.AI.Pathfinding.asmdef` |
| `Hecton8.AI.Pathfinding.Editor` | `Assets/_Project/Scripts/AI/Pathfinding/Editor/Hecton8.AI.Pathfinding.Editor.asmdef` |
| `Hecton8.AbyssalGeology1606.Editor` | `Assets/_Project/Editor/Generators/Geology/Hecton8.AbyssalGeology1606.Editor.asmdef` |
| `Hecton8.AbyssalScatter1614.Editor` | `Assets/_Project/Editor/Generators/World/Hecton8.AbyssalScatter1614.Editor.asmdef` |
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
| `Hecton8.Editor.Generators.Fauna` | `Assets/_Project/Editor/Generators/Fauna/Hecton8.Editor.Generators.Fauna.asmdef` |
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
| `Hecton8.Rendering.TexturePacker.Editor` | `Assets/_Project/Scripts/Editor/TextureChannelPacker/Hecton8.Rendering.TexturePacker.Editor.asmdef` |
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
| `Hecton8.World.ProceduralCoral` | `Assets/_Project/Scripts/World/ProceduralCoral/Hecton8.World.ProceduralCoral.asmdef` |
| `Hecton8.World.ProceduralCoral.Editor` | `Assets/_Project/Scripts/World/ProceduralCoral/Editor/Hecton8.World.ProceduralCoral.Editor.asmdef` |
| `Hecton8.World.ProceduralWreckage` | `Assets/_Project/Scripts/World/ProceduralWreckage/Hecton8.World.ProceduralWreckage.asmdef` |
| `Hecton8.World.ProceduralWreckage.Editor` | `Assets/_Project/Scripts/World/ProceduralWreckage/Editor/Hecton8.World.ProceduralWreckage.Editor.asmdef` |
| `Hecton8.World.ShinobuBiomimetic` | `Assets/_Project/Scripts/World/ShinobuBiomimetic/Hecton8.World.ShinobuBiomimetic.asmdef` |
| `Hecton8.World.ShinobuBiomimetic.Editor` | `Assets/_Project/Scripts/World/ShinobuBiomimetic/Editor/Hecton8.World.ShinobuBiomimetic.Editor.asmdef` |
| `Hecton8.World.StaticCaveSdfBaker.Editor` | `Assets/_Project/Scripts/World/StaticCaveSdfBaker/Editor/Hecton8.World.StaticCaveSdfBaker.Editor.asmdef` |
| `Hecton8.World.VoxelSurfaceNets` | `Assets/_Project/Scripts/World/VoxelSurfaceNets/Hecton8.World.VoxelSurfaceNets.asmdef` |
| `Hecton8.World.VoxelSurfaceNets.Editor` | `Assets/_Project/Scripts/World/VoxelSurfaceNets/Editor/Hecton8.World.VoxelSurfaceNets.Editor.asmdef` |

Assemblies depending on any `Hecton8.Core*` assembly: 132

| Assembly | Core-family references | Path |
|---|---|---|
| `Hecton8.AI.Ambient` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/AI/Ambient/Hecton8.AI.Ambient.asmdef` |
| `Hecton8.AI.Cognition` | `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/AI/Cognition/Hecton8.AI.Cognition.asmdef` |
| `Hecton8.AI.Cognition.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/AI/Cognition/Editor/Hecton8.AI.Cognition.Editor.asmdef` |
| `Hecton8.AI.Foveated` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/AI/Foveated/Hecton8.AI.Foveated.asmdef` |
| `Hecton8.AI.Pathfinding` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/AI/Pathfinding/Hecton8.AI.Pathfinding.asmdef` |
| `Hecton8.AI.Pathfinding.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/AI/Pathfinding/Editor/Hecton8.AI.Pathfinding.Editor.asmdef` |
| `Hecton8.AbyssalGeology1606.Editor` | `Hecton8.Core` | `Assets/_Project/Editor/Generators/Geology/Hecton8.AbyssalGeology1606.Editor.asmdef` |
| `Hecton8.AbyssalScatter1614.Editor` | `Hecton8.Core` | `Assets/_Project/Editor/Generators/World/Hecton8.AbyssalScatter1614.Editor.asmdef` |
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
| `Hecton8.Core.Content.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Core/Content/Editor/Hecton8.Core.Content.Editor.asmdef` |
| `Hecton8.Core.Database` | `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Core/Database/Hecton8.Core.Database.asmdef` |
| `Hecton8.Core.Hardware` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Core/Hardware/Hecton8.Core.Hardware.asmdef` |
| `Hecton8.Core.Memory` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Core/Memory/Hecton8.Core.Memory.asmdef` |
| `Hecton8.Core.Memory.Defrag` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Core/Memory/Defrag/Hecton8.Core.Memory.Defrag.asmdef` |
| `Hecton8.Core.Memory.Editor` | `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Core/Memory/Editor/Hecton8.Core.Memory.Editor.asmdef` |
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
| `Hecton8.Editor.Generators.Fauna` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Editor/Generators/Fauna/Hecton8.Editor.Generators.Fauna.asmdef` |
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
| `Hecton8.Plugins` | `Hecton8.Core.Contracts`, `Hecton8.Core` | `Assets/_Project/Scripts/Plugins/Hecton8.Plugins.asmdef` |
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
| `Hecton8.Rendering.TexturePacker.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/Editor/TextureChannelPacker/Hecton8.Rendering.TexturePacker.Editor.asmdef` |
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
| `Hecton8.UI.TerminalOS.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/UI/TerminalOS/Editor/Hecton8.UI.TerminalOS.Editor.asmdef` |
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
| `Hecton8.World.ProceduralCoral` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/ProceduralCoral/Hecton8.World.ProceduralCoral.asmdef` |
| `Hecton8.World.ProceduralCoral.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/ProceduralCoral/Editor/Hecton8.World.ProceduralCoral.Editor.asmdef` |
| `Hecton8.World.ProceduralWreckage` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/ProceduralWreckage/Hecton8.World.ProceduralWreckage.asmdef` |
| `Hecton8.World.ProceduralWreckage.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/ProceduralWreckage/Editor/Hecton8.World.ProceduralWreckage.Editor.asmdef` |
| `Hecton8.World.ShinobuBiomimetic` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/ShinobuBiomimetic/Hecton8.World.ShinobuBiomimetic.asmdef` |
| `Hecton8.World.ShinobuBiomimetic.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/ShinobuBiomimetic/Editor/Hecton8.World.ShinobuBiomimetic.Editor.asmdef` |
| `Hecton8.World.StaticCaveSdfBaker.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/World/StaticCaveSdfBaker/Editor/Hecton8.World.StaticCaveSdfBaker.Editor.asmdef` |
| `Hecton8.World.VoxelSurfaceNets` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/VoxelSurfaceNets/Hecton8.World.VoxelSurfaceNets.asmdef` |
| `Hecton8.World.VoxelSurfaceNets.Editor` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/World/VoxelSurfaceNets/Editor/Hecton8.World.VoxelSurfaceNets.Editor.asmdef` |

### Domain Namespace Edges

Static `using Hecton8.*` edges from first-party source. This exposes compile-time namespace pressure, not runtime coupling proof.

| From domain | To domain | Using count |
|---|---|---:|
| `RootScripts` | `Core` | 361 |
| `World` | `Core` | 267 |
| `Gameplay` | `Core` | 241 |
| `UI` | `Core` | 179 |
| `Editor` | `Core` | 174 |
| `Physics` | `Core` | 138 |
| `Construction` | `Core` | 99 |
| `RootScripts` | `World` | 91 |
| `Editor` | `World` | 75 |
| `Visor` | `Core` | 67 |
| `Physiology` | `Core` | 65 |
| `RootScripts` | `Gameplay` | 63 |
| `Gameplay` | `World` | 57 |
| `AI` | `Core` | 56 |
| `Fauna` | `Core` | 54 |
| `Audio` | `Core` | 51 |
| `VFX` | `Core` | 50 |
| `Atmosphere` | `Core` | 45 |
| `RootScripts` | `Items` | 38 |
| `Power` | `Core` | 34 |
| `Tools` | `Core` | 34 |
| `RootScripts` | `Inventory` | 33 |
| `Optimization` | `Core` | 33 |
| `RootScripts` | `Interaction` | 32 |
| `Interaction` | `Core` | 31 |
| `SaveSystem` | `Core` | 31 |
| `UI` | `World` | 31 |
| `Plugins` | `Core` | 30 |
| `UI` | `Gameplay` | 30 |
| `World` | `Gameplay` | 30 |
| `RootScripts` | `Environment` | 29 |
| `Graphics` | `Core` | 29 |
| `World` | `Environment` | 27 |
| `Ecosystem` | `Core` | 25 |
| `Rendering` | `Core` | 25 |
| `RootScripts` | `SaveSystem` | 24 |
| `RootScripts` | `UI` | 24 |
| `Construction` | `World` | 24 |
| `Gameplay` | `Interaction` | 24 |
| `Construction` | `Gameplay` | 23 |
| `RootScripts` | `Bootstrap` | 22 |
| `Editor` | `Gameplay` | 22 |
| `Quest` | `Core` | 22 |
| `RootScripts` | `Tools` | 20 |
| `ModdingAPI` | `Core` | 20 |
| `Vehicles` | `Core` | 20 |
| `RootScripts` | `Construction` | 19 |
| `QA` | `Core` | 19 |
| `Animation` | `Core` | 18 |
| `RootScripts` | `Building` | 18 |
| `Lighting` | `Core` | 18 |
| `Narrative` | `Core` | 18 |
| `RootScripts` | `Caves` | 17 |
| `Gameplay` | `Audio` | 17 |
| `Inventory` | `Core` | 17 |
| `PDA` | `Core` | 17 |
| `Thermodynamics` | `Core` | 17 |
| `Bootstrap` | `Core` | 16 |
| `Fauna` | `World` | 16 |
| `Construction` | `Power` | 15 |
| `Editor` | `Construction` | 15 |
| `UI` | `Bootstrap` | 15 |
| `Editor` | `Items` | 14 |
| `RootScripts` | `Audio` | 13 |
| `Editor` | `Physics` | 13 |
| `Gameplay` | `UI` | 13 |
| `Habitat` | `Core` | 13 |
| `Physics` | `World` | 13 |
| `World` | `Caves` | 13 |
| `RootScripts` | `Atmosphere` | 12 |
| `Prologue` | `Core` | 12 |
| `Editor` | `AI` | 11 |
| `RootScripts` | `AI` | 11 |
| `Gameplay` | `Inventory` | 11 |
| `Construction` | `Items` | 10 |
| `Dev` | `Core` | 10 |
| `Ecosystem` | `World` | 10 |
| `Editor` | `Environment` | 10 |
| `Gameplay` | `SaveSystem` | 10 |
| `Gameplay` | `Items` | 10 |

## SignalBus<T> Flow Map

`ISignal` structs declared: 308. `SignalBus<T>` lanes observed in producer/consumer calls: 283. Union listed below: 315 signals.
Legacy `GlobalSignals.Publish(...)` call sites found: 0.

| Signal | Declared at | Producers (`Push/Publish/TryPush*/TryEnqueueBounded`) | Consumers (`GetFrameSnapshot*`/`TryConsumeFrame`/`TryGetLatest`) |
|---|---|---|---|
| `AcousticPingSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:887` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:1709`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:430`<br>`Assets/_Project/Scripts/Core/Signals/SignalCorridorMockSignalGenerators.cs:51`<br>`Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:2639`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:2714`<br>`Assets/_Project/Scripts/Fauna/FaunaBrain.cs:3291`<br>`Assets/_Project/Scripts/Fauna/FaunaBrain.cs:3298`<br>`Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1674`<br>`Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:2028`<br>`Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs:1263`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2917`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2929`<br>... +25 more | `Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:1429`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:4725`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1097`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1228`<br>`Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.AcousticSdf.cs:664`<br>`Assets/_Project/Scripts/FaunaDirector.cs:1045`<br>`Assets/_Project/Scripts/HectonBoidController.cs:2482`<br>`Assets/_Project/Scripts/Physiology/PlayerStressMetricsRuntime.cs:272`<br>`Assets/_Project/Scripts/SpatialAudioManager.cs:6603`<br>`Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:2486`<br>`Assets/_Project/Scripts/Visor/SpectrumSystem.cs:3455`<br>`Assets/_Project/Scripts/Visor/SpectrumSystem.cs:3492`<br>... +2 more |
| `AcousticShockwaveSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:5928` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:2705` | none found |
| `AcousticZoneChangedEvent` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:58` | `Assets/_Project/Scripts/AcousticZoneController.cs:104` | `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:2104` |
| `AnomalyProximitySignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:529` | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:156`<br>`Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:967` | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:1157` |
| `AnomalySignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:514` | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1322` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1071` |
| `ApexBrainAcousticEchoTap` | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:219` | none found | none found |
| `ApexPanicSignal` | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:519` | none found | none found |
| `ApexProximitySignal` | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:461` | none found | none found |
| `AppliedLoreTerminalPreviewSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1211` | `Assets/_Project/Scripts/Gameplay/MessageTerminal.cs:650` | `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:707` |
| `AssetLoadProgressSignal` | `Assets/_Project/Scripts/Core/Contracts/Signals/AssetLoadProgressSignal.cs:14` | `Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs:917` | none found |
| `AtmosphericReentrySignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1411` | `Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs:1122` | `Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:255`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:216`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1147`<br>`Assets/_Project/Scripts/Prologue/Space/PrologueWorldHandoffSceneLoader.cs:143`<br>`Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:523` |
| `AudioEvent` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:225` | `Assets/_Project/Scripts/Audio/ProceduralAudioEvents.cs:895`<br>`Assets/_Project/Scripts/PowerGrid.cs:1963` | `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:6874` |
| `AupPreShiftSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:69` | `Assets/_Project/Scripts/Core/Signals/SignalBridgeRoutes.cs:30` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1051`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:3012` |
| `AupShiftSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:79` | `Assets/_Project/Scripts/Core/Signals/SignalBridgeRoutes.cs:51` | `Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs:1275`<br>`Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1076`<br>`Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1524`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1053`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.RuntimeLifecycle.cs:403`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:995`<br>`Assets/_Project/Scripts/HectonFluidEngine.cs:4454`<br>`Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:776`<br>`Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:495`<br>`Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:1193`<br>`Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:2356`<br>`Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:1910`<br>... +7 more |
| `AuxiliaryFlareLightSignal` | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentContracts.cs:230` | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentJobs.cs:265` | none found |
| `AuxiliarySonarRequestSignal` | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentContracts.cs:244` | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentJobs.cs:295` | none found |
| `AuxiliaryTetherConnectionSignal` | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentContracts.cs:259` | `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentJobs.cs:345` | none found |
| `BaseIntegrityEventPayload` | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorTypes.cs:59` | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorTypes.cs:714` | none found |
| `BaseModuleCompromisedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1871` | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:1865`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1293`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorTypes.cs:746`<br>`Assets/_Project/Scripts/Thermodynamics/ReactorThermalGridJobs.cs:830` | none found |
| `BaseStructuralWarningSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:208` | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/BaseStructuralWarningDispatcherTypes.cs:577` | `Assets/_Project/Scripts/Audio/Editor/AbyssalDspTunerWindow.cs:133`<br>`Assets/_Project/Scripts/Audio/Editor/AbyssalDspTunerWindow.cs:260`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:6888` |
| `BatteryLevelSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:144` | `Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:663` | `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:1250`<br>`Assets/_Project/Scripts/UI/SubtitleManager.cs:1520` |
| `BiomeChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:230` | `Assets/_Project/Scripts/Data/Monolith/H8AppliedLoreRuntime.cs:391`<br>`Assets/_Project/Scripts/HectonNarrativeDirector.cs:525`<br>`Assets/_Project/Scripts/World/BiomeTransitionFogBlendJobs.cs:743` | `Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:1450`<br>`Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1124`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1205`<br>`Assets/_Project/Scripts/Graphics/Materials/ProceduralFloraBiomeTintBridge.cs:114`<br>`Assets/_Project/Scripts/PlayerInventory.cs:5188`<br>`Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs:2166` |
| `BiomeGradientSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:241` | `Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs:686` | `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:2086`<br>`Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs:881`<br>`Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs:1727`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:2634` |
| `BlueprintUnlockedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1234` | `Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1320`<br>`Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:648` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1127` |
| `BootstrapEventPayload` | `Assets/_Project/Scripts/Bootstrap/BootstrapEvents.cs:21` | `Assets/_Project/Scripts/Bootstrap/BootstrapEvents.cs:226` | `Assets/_Project/Scripts/Bootstrap/BootstrapEvents.cs:234` |
| `BrownoutSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:372` | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1682`<br>`Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:729`<br>`Assets/_Project/Scripts/PowerGrid.cs:1421`<br>`Assets/_Project/Scripts/PowerGridManager.cs:736` | `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:1243`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1057`<br>`Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:548`<br>`Assets/_Project/Scripts/SpatialAudioManager.cs:10463`<br>`Assets/_Project/Scripts/Visor/VisorHUDController.cs:1418` |
| `BubbleSpawnSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:130` | `Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationContracts.cs:507`<br>`Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs:1112`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:2424`<br>`Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsRuntime.cs:1067` | none found |
| `BulletTimeVisualSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:364` | `Assets/_Project/Scripts/Core/Signals/SignalBridgeRoutes.cs:126` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1173` |
| `CameraFrustumSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1713` | `Assets/_Project/Scripts/Core/SystemDispatcher.cs:6781` | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1058`<br>`Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1944` |
| `CameraJuiceImpactSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:934` | `Assets/_Project/Scripts/Core/CameraJuiceSignals.cs:96` | `Assets/_Project/Scripts/Core/CameraJuiceSignals.cs:173`<br>`Assets/_Project/Scripts/VFX/CameraJuiceSystem_CameraJuiceBurst.cs:457`<br>`Assets/_Project/Scripts/VFX/CameraJuiceSystem_CameraJuiceBurst.cs:546` |
| `CameraPositionSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1701` | `Assets/_Project/Scripts/Core/SystemDispatcher.cs:6770` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:3551`<br>`Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:1086`<br>`Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1045` |
| `CardiacPulseSignal` | `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyData.cs:752` | `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:1368` | none found |
| `CavitationAcousticSignal` | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:485` | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:1708` | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:2392` |
| `ChunkDehydratedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:975` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:5418` | `Assets/_Project/Scripts/SaveManager.cs:2431` |
| `CombatDamageSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1729` | `Assets/_Project/Scripts/Core/PlayerRuntimeContext.cs:308`<br>`Assets/_Project/Scripts/Core/Signals/SignalCorridorMockSignalGenerators.cs:91`<br>`Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs:357`<br>`Assets/_Project/Scripts/Fauna/FaunaBrain.cs:2071`<br>`Assets/_Project/Scripts/Fauna/FaunaBrain.cs:4003`<br>`Assets/_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs:2206`<br>`Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs:1075`<br>`Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs:1312`<br>`Assets/_Project/Scripts/HectonFluidEngine.cs:7251`<br>`Assets/_Project/Scripts/Interaction/PhysicalHandController.cs:2197`<br>`Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:995`<br>`Assets/_Project/Scripts/Physiology/ShinobuSuitIntegrityJobs.cs:397`<br>... +5 more | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.FlockingAvoidance.cs:67`<br>`Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1110`<br>`Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:1714`<br>`Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1029`<br>`Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs:208`<br>`Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs:413`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1222`<br>`Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.AcousticSdf.cs:711`<br>`Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:1198`<br>`Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:1007`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1157`<br>`Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs:277`<br>... +9 more |
| `CombatDamageSignalAupShiftTransformer` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1998` | none found | none found |
| `CompassCalibratedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:572` | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:133` | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:1170` |
| `ComplianceViolationSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:287` | `Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs:1069`<br>`Assets/_Project/Scripts/Core/SystemDispatcher.cs:934`<br>`Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:909` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1159` |
| `ConstructionPreviewSignal` | `Assets/_Project/Scripts/Construction/ConstructionSignals.cs:21` | `Assets/_Project/Scripts/PlayerBuilder.cs:3770` | `Assets/_Project/Scripts/Construction/HectonBlueprintPreviewBatch.cs:486` |
| `ControlSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:500` | none found | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1069`<br>`Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs:715` |
| `CoreCombatDamageSignal` | not found as local `ISignal` declaration | none found | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:1604` |
| `CoreHackedSignal` | `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs:49` | `Assets/_Project/Scripts/World/SeedShipAnomaly/Editor/SeedShipAnomalyTunerWindow.cs:72`<br>`Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:415` | `Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:1182` |
| `CoreTetherFiredSignal` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/Physics/TetherSignals.cs:86` | none found |
| `CpuStarvationSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:872` | `Assets/_Project/Scripts/Core/JobAdmissionTelemetryBridge.cs:30`<br>`Assets/_Project/Scripts/Core/JobAdmissionTelemetryBridge.cs:76` | none found |
| `CraftingCompletedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1264` | `Assets/_Project/Scripts/Core/Signals/SignalBridgeRoutes.cs:146` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1131`<br>`Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs:1425`<br>`Assets/_Project/Scripts/PDA/PDALogbookManager.cs:592` |
| `CraftingStartedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1249` | `Assets/_Project/Scripts/Fabricator.cs:3305` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1129` |
| `CrashTelemetrySignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:615` | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1332`<br>`Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs:854`<br>`Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:987` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1075`<br>`Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs:506` |
| `CrushWarningSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:710` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:10500` | `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:1242`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1087` |
| `CullingOverloadSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:252` | `Assets/_Project/Scripts/Core/InstanceCullingServiceRegistryBridge.cs:100` | none found |
| `DataReloadSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:775` | none found | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1093` |
| `DataVaultUpdateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:304` | `Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs:438`<br>`Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs:458`<br>`Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs:565`<br>`Assets/_Project/Scripts/Core/Bridge/H8InputMappingFacade.cs:283`<br>`Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistryRuntimeBinder.cs:488` | none found |
| `DebrisAvalancheSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:5901` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:2673` | none found |
| `DebrisDestroyedSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:302` | none found | none found |
| `DebrisSpawnSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:387` | `Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:2147`<br>`Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5761`<br>`Assets/_Project/Scripts/ConstructionManager.cs:1074`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1769`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:2691`<br>`Assets/_Project/Scripts/Fauna/FaunaBrain.cs:3976`<br>`Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1642`<br>`Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:1851`<br>`Assets/_Project/Scripts/Gameplay/HarvestableOutcrop.cs:454`<br>`Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:1962`<br>`Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs:1339`<br>`Assets/_Project/Scripts/Gameplay/SargassumCutResponder.cs:108`<br>... +16 more | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1059`<br>`Assets/_Project/Scripts/HectonFluidEngine.cs:4445`<br>`Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:1057` |
| `DebugSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:397` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeDebugSignal.cs:23` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:749` |
| `DeconstructRequestSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:648` | `Assets/_Project/Scripts/ConstructionManager.cs:828` | `Assets/_Project/Scripts/ConstructionManager.cs:849`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1079` |
| `DeconstructResultSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:664` | `Assets/_Project/Scripts/ConstructionManager.cs:1109`<br>`Assets/_Project/Scripts/FabricationAssemblerRuntime.cs:2228` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1081` |
| `DeferredSubmarineImpactSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:1212` | `Assets/_Project/Scripts/PhysicsApplySystem.cs:3786`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:3844` | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1198`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:3799` |
| `DeflectSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:407` | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:2447`<br>`Assets/_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs:1594` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1061` |
| `DeltaCrusherMockLaserFireSignal` | `Assets/_Project/Scripts/VFX/Debris/ShinobuDeltaCrusherJobs.cs:37` | none found | none found |
| `DesyncDetectedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:591` | `Assets/_Project/Scripts/Core/Signals/CoreDeterminismSignals.cs:109` | none found |
| `DiegeticHudSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:50` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:466`<br>`Assets/_Project/Scripts/UI/TerminalOS/HectonSubmarineOS.cs:76` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1153` |
| `DirectorAIMusicSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:103` | `Assets/_Project/Scripts/HectonDirectorAI.cs:321` | `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:2119` |
| `DockingCompleteSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:821` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5015`<br>`Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:2020` | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1264` |
| `DockingFailedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:842` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5048`<br>`Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5066`<br>`Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:2053` | none found |
| `DockingRequestSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:800` | none found | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:4743` |
| `DroneFleetInventoryTransactionSignal` | `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs:952` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5296`<br>`Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5488`<br>`Assets/_Project/Scripts/Construction/DroneFleetManager_Transactions.cs:1282` | none found |
| `DroneFleetMiningServiceSignal` | `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs:941` | none found | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:6204` |
| `DroneFleetRepairServiceSignal` | `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs:930` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5453` | none found |
| `DropPodCommandSignal` | `Assets/_Project/Scripts/Vehicles/DropPod/DropPodTransitSignals.cs:47` | `Assets/_Project/Scripts/Vehicles/DropPod/DropPodAirlockController.cs:414`<br>`Assets/_Project/Scripts/Vehicles/DropPod/DropPodDashboardToggleSwitch.cs:220`<br>`Assets/_Project/Scripts/Vehicles/DropPod/DropPodSeatController.cs:583` | none found |
| `DropPodLandedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:89` | `Assets/_Project/Scripts/Vehicles/DropPod/DropPodSeatController.cs:611` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1055`<br>`Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:2312` |
| `DropPodStatusSignal` | `Assets/_Project/Scripts/Vehicles/DropPod/DropPodTransitSignals.cs:58` | `Assets/_Project/Scripts/Vehicles/DropPod/DropPodAirlockController.cs:426`<br>`Assets/_Project/Scripts/Vehicles/DropPod/DropPodSeatController.cs:595` | `Assets/_Project/Scripts/Vehicles/DropPod/DropPodDashboardTextRenderer.cs:146`<br>`Assets/_Project/Scripts/Vehicles/DropPod/DropPodEmergencyLightingController.cs:127` |
| `DynamicMusicScalarSignal` | `Assets/_Project/Scripts/Core/Contracts/Signals/DynamicMusicScalarSignal.cs:14` | `Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1326`<br>`Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:1943` | `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:1679` |
| `EclipseGameplayEventPayload` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:5880` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3126` | none found |
| `EncumbranceSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:250` | none found | none found |
| `EncyclopediaUnlockSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:301` | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1128` | none found |
| `EntityDeathSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:434` | `Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs:718`<br>`Assets/_Project/Scripts/Fauna/FaunaBrain.cs:6677`<br>`Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:1888` | `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs:1682`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1063`<br>`Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime_Carrion.cs:392`<br>`Assets/_Project/Scripts/HectonDirectorAI.cs:988`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:7363` |
| `EntityDepletedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:315` | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1163` | none found |
| `EntitySpawnSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:450` | `Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs:503` | none found |
| `EquipItemSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:268` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:1518` | none found |
| `EquipmentOverheatSignal` | `Assets/_Project/Scripts/Tools/EquipmentThermalBatteryContracts.cs:101` | `Assets/_Project/Scripts/ModularEquipmentEngine.cs:4000` | none found |
| `ExosuitAcousticEchoTap` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsContracts.cs:84` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:1006` | none found |
| `ExtractorCapacityReachedSignal` | `Assets/_Project/Scripts/Construction/ConstructionSignals.cs:148` | `Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs:583` | none found |
| `FabricationCompletedSignal` | `Assets/_Project/Scripts/FabricationAssemblerRuntime.cs:72` | `Assets/_Project/Scripts/FabricationAssemblerRuntime.cs:2240` | none found |
| `FabricationTickSignal` | `Assets/_Project/Scripts/FabricationAssemblerRuntime.cs:88` | `Assets/_Project/Scripts/FabricationAssemblerRuntime.cs:2265` | none found |
| `FaunaStateChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1612` | `Assets/_Project/Scripts/Fauna/FaunaBrain.cs:5274`<br>`Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:4371`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:3212`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:3242`<br>`Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:1995` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:3569`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1190`<br>`Assets/_Project/Scripts/Fauna/FaunaBrain.cs:4637`<br>`Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1573` |
| `FlashlightEventPayload` | `Assets/_Project/Scripts/PlayerFlashlight.cs:59` | `Assets/_Project/Scripts/PlayerFlashlight.cs:247` | `Assets/_Project/Scripts/PlayerFlashlight.cs:160` |
| `FloraExclusionSignal` | `Assets/_Project/Scripts/Construction/ConstructionSignals.cs:73` | `Assets/_Project/Scripts/PlayerBuilder.cs:4262` | none found |
| `FloraSpawnedSignal` | `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeContracts.cs:628` | `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeVaultRuntime.cs:592` | none found |
| `FluidDensityChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1110` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:3086` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1113`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1167` |
| `FluidImpulseSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:111` | `Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:1991`<br>`Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:2061`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2972`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:2431`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:5296`<br>`Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:1762`<br>`Assets/_Project/Scripts/World/FloraInteractionManager.cs:4898`<br>`Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:1910` | none found |
| `FluidIncursionSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1069` | `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs:981`<br>`Assets/_Project/Scripts/Gameplay/HabitatIntegrityManager.cs:620`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1273`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorTypes.cs:744`<br>`Assets/_Project/Scripts/Physics/HabitatFluidIncursionJobs.cs:354`<br>`Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1874` | `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:1102`<br>`Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:1248`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1111`<br>`Assets/_Project/Scripts/Physics/HabitatFluidIncursionDirector.cs:673` |
| `FocusBrokenSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:301` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:8216` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1209` |
| `FoundationStructuralWarningSignal` | `Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorData.cs:202` | `Assets/_Project/Scripts/Construction/FoundationPylonGpuBatch.cs:843`<br>`Assets/_Project/Scripts/Construction/FoundationPylonGpuBatch.cs:864` | none found |
| `FramePacingWarningSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1975` | `Assets/_Project/Scripts/Core/SystemDispatcher.cs:5239`<br>`Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeVaultRuntime.cs:617` | none found |
| `FrameTimeSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:145` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:838` | `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs:1728`<br>`Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1537`<br>`Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:2469` |
| `GameBootstrapperEventPayload` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:68` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:1182` | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:756` |
| `GlobalPanicSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:5955` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:2655` | none found |
| `GlobalTimeSyncSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:320` | `Assets/_Project/Scripts/HectonCelestialEngine.cs:5302` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1161`<br>`Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs:2104` |
| `GlobalWorldStateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:209` | `Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs:771`<br>`Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs:892` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1203` |
| `HUDNotificationSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:35` | none found | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1141`<br>`Assets/_Project/Scripts/HUDNotification.cs:232` |
| `HabitatConstructionSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:635` | `Assets/_Project/Scripts/ConstructionManager.cs:1036` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1077` |
| `HabitatFloodAcousticMuffleSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:78` | `Assets/_Project/Scripts/AcousticZoneController.cs:117`<br>`Assets/_Project/Scripts/Physics/HabitatFluidIncursionDirector.cs:1245` | none found |
| `HapticPulseSignal` | `Assets/_Project/Scripts/Core/Contracts/Signals/HapticPulseSignal.cs:11` | `Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:1213`<br>`Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:1230`<br>`Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs:311`<br>`Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs:480`<br>`Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs:809`<br>`Assets/_Project/Scripts/Quest/QuestManager.cs:977`<br>`Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs:519`<br>`Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs:1522`<br>`Assets/_Project/Scripts/World/BioCableIK.cs:470` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:4013`<br>`Assets/_Project/Scripts/Core/InputDispatcher.cs:4105` |
| `HapticRequest` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:80` | `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:1173`<br>`Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:534`<br>`Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:556`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:453`<br>`Assets/_Project/Scripts/Fauna/FaunaBrain.cs:3305`<br>`Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1653`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2911`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2923`<br>`Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:2024`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:2417`<br>`Assets/_Project/Scripts/Interaction/PhysicalHandController.cs:935`<br>`Assets/_Project/Scripts/LaserCutter.cs:2377`<br>... +8 more | `Assets/_Project/Scripts/Core/InputDispatcher.cs:4005`<br>`Assets/_Project/Scripts/Core/InputDispatcher.cs:4098`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1169` |
| `HashDeltaUpdateSignal` | `Assets/_Project/Scripts/Core/Contracts/MemorySentinelSignals.cs:30` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:387` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1385` |
| `HectonFloraSporeEvent` | `Assets/_Project/Scripts/World/HectonIndirectVegetationContracts.cs:64` | `Assets/_Project/Scripts/World/HectonIndirectVegetationContracts.cs:242` | `Assets/_Project/Scripts/World/HectonIndirectVegetationContracts.cs:124` |
| `HighSpeedImpactSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:32` | `Assets/_Project/Scripts/Fauna/FaunaBrain.cs:3949` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.FlockingAvoidance.cs:54`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:4746`<br>`Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs:207`<br>`Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs:412`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:3075`<br>`Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:979`<br>`Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2116`<br>`Assets/_Project/Scripts/VFX/CameraJuiceSystem_CameraJuiceBurst.cs:445`<br>`Assets/_Project/Scripts/VFX/CameraJuiceSystem_CameraJuiceBurst.cs:543`<br>`Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs:2318` |
| `HullDeformedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1819` | `Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs:1702`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:2055` | `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:1723`<br>`Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:1591`<br>`Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:2099` |
| `HullRepairedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1848` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5577`<br>`Assets/_Project/Scripts/RepairTool.cs:2257` | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs:2313`<br>`Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs:2321`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1202` |
| `HypoxiaSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1003` | `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:1737`<br>`Assets/_Project/Scripts/World/BioCableIK.cs:461` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1103` |
| `ImpactSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:10` | `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs:916`<br>`Assets/_Project/Scripts/Construction/LogisticsPipeNode.cs:754`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1741`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:2723`<br>`Assets/_Project/Scripts/Fauna/FaunaBrain.cs:3958`<br>`Assets/_Project/Scripts/Fauna/FaunaBrain.cs:6404`<br>`Assets/_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs:1627`<br>`Assets/_Project/Scripts/GlobalPhysicsStateManager.cs:2557`<br>`Assets/_Project/Scripts/HectonFluidEngine.cs:5889`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:5259`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:5332`<br>`Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1661`<br>... +4 more | `Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs:206`<br>`Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs:411`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1049`<br>`Assets/_Project/Scripts/VFX/CameraJuiceSystem_CameraJuiceBurst.cs:441`<br>`Assets/_Project/Scripts/VFX/CameraJuiceSystem_CameraJuiceBurst.cs:542`<br>`Assets/_Project/Scripts/World/SoundscapeSystem.cs:796` |
| `InputSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:525` | `Assets/_Project/Scripts/Core/Signals/CoreDeterminismSignals.cs:85` | none found |
| `InputStateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:1001` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:762` | `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:961`<br>`Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:764`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1980`<br>`Assets/_Project/Scripts/UI/InteractionUI.cs:992`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:6021` |
| `InteractionUiSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1037` | `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:1067`<br>`Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:1405` | `Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime.cs:1300`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1107` |
| `InventoryChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:308` | `Assets/_Project/Scripts/Editor/CraftingFastFailXRayWindow_SHINOBU317.cs:142`<br>`Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.CargoSync.cs:869`<br>`Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.CargoSync.cs:871`<br>`Assets/_Project/Scripts/PlayerInventory.cs:4977` | `Assets/_Project/Scripts/Editor/SoaInventoryXRayWindow_SHINOBU316.cs:125`<br>`Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:930`<br>`Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:1045`<br>`Assets/_Project/Scripts/HectonPlayerMovement.cs:5090`<br>`Assets/_Project/Scripts/PDAInventoryTab.cs:633`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1146`<br>`Assets/_Project/Scripts/PlayerToolManager.cs:2531`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:3013`<br>`Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs:296`<br>`Assets/_Project/Scripts/UI/PDAConstructionTab.cs:419`<br>`Assets/_Project/Scripts/UI/PDALoadoutTab.cs:414`<br>`Assets/_Project/Scripts/UI/PDAShellChrome.cs:622`<br>... +1 more |
| `InventoryCommandSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:226` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5489`<br>`Assets/_Project/Scripts/Physiology/ShinobuRespawnJobs.cs:645`<br>`Assets/_Project/Scripts/PlayerInventory.cs:2733` | `Assets/_Project/Scripts/PlayerInventory.cs:5352` |
| `InventoryDeathLootCacheSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:262` | `Assets/_Project/Scripts/ConstructionManager.cs:1758`<br>`Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:829`<br>`Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:860`<br>`Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:1505`<br>`Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:1514`<br>`Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.CargoSync.cs:840`<br>`Assets/_Project/Scripts/PlayerInventory_SoaQuery.cs:307` | `Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:799`<br>`Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:816` |
| `InventoryRespawnDeathAupSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:242` | `Assets/_Project/Scripts/Physiology/ShinobuRespawnJobs.cs:654` | `Assets/_Project/Scripts/PlayerInventory.cs:2952` |
| `InventoryRespawnPenaltyResultSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:290` | `Assets/_Project/Scripts/PlayerInventory.cs:2995` | `Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs:1209` |
| `ItemAcquiredSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:372` | `Assets/_Project/Scripts/Construction/DroneFleetManager_Transactions.cs:1308`<br>`Assets/_Project/Scripts/ConstructionManager.cs:1669`<br>`Assets/_Project/Scripts/Fabricator.cs:3340`<br>`Assets/_Project/Scripts/Gameplay/HarvestableOutcrop.cs:714`<br>`Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:1937`<br>`Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs:1289`<br>`Assets/_Project/Scripts/HectonItem.cs:501`<br>`Assets/_Project/Scripts/Items/PickupItem.cs:594`<br>`Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs:1775`<br>`Assets/_Project/Scripts/VoxelDeltaProcessor.cs:5797`<br>`Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:2742` | `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs:1693`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1179`<br>`Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:2174`<br>`Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:2196`<br>`Assets/_Project/Scripts/PlayerInventory.cs:5202`<br>`Assets/_Project/Scripts/PlayerInventory.cs:5394`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:7374` |
| `ItemDecaySignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1523` | none found | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1177` |
| `ItemDurabilityChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:322` | `Assets/_Project/Scripts/PlayerInventory.cs:5736`<br>`Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs:1271` | `Assets/_Project/Scripts/HUDQuickBar.cs:392`<br>`Assets/_Project/Scripts/PlayerToolManager.cs:2554`<br>`Assets/_Project/Scripts/UI/PDALoadoutTab.cs:433`<br>`Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:286` |
| `ItemLifecycleSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:343` | `Assets/_Project/Scripts/Core/Signals/ItemLifecycleSignalRoute.cs:94` | `Assets/_Project/Scripts/Meta/GlobalProfileManager.cs:451`<br>`Assets/_Project/Scripts/World/EnvironmentalStrainManager.cs:356` |
| `KccVelocitySignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:649` | `Assets/_Project/Scripts/Core/Signals/CoreDeterminismSignals.cs:155`<br>`Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1999` | `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs:1473`<br>`Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs:1597` |
| `KillSwitchSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:165` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:851` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1531` |
| `LaserCutterEventPayload` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:1081` | none found | `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:6614` |
| `LaserCutterEventPayloadSignal` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/LaserCutter.cs:381`<br>`Assets/_Project/Scripts/LaserCutter.cs:396` | `Assets/_Project/Scripts/LaserCutter.cs:200` |
| `LightLevelSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1546` | `Assets/_Project/Scripts/World/HectonCaveVoxelLightingVolume.cs:1021` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1185`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1242`<br>`Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:535`<br>`Assets/_Project/Scripts/Physiology/PlayerStressMetricsRuntime.cs:596`<br>`Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs:2378` |
| `LocalizationLanguageChangedSignal` | `Assets/_Project/Scripts/LocRegistry.cs:443` | `Assets/_Project/Scripts/LocRegistry.cs:1812` | none found |
| `LockstepSnapshotSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:1019` | `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1179` | none found |
| `LogisticsTransferSignal` | `Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs:182` | `Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs:1682` | none found |
| `LoreFragmentScannedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1194` | `Assets/_Project/Scripts/Data/Monolith/H8AppliedLoreRuntime.cs:321`<br>`Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1158` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1125`<br>`Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs:882` |
| `MacroCollisionSignal` | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1316` | none found | none found |
| `MacroDatabaseSectorHydrationSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:187` | `Assets/_Project/Scripts/Core/MacroDatabaseSignalBridge.cs:24` | `Assets/_Project/Scripts/SaveManager.cs:2350`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:5390`<br>`Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:790` |
| `ManualOverridePulledSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:10` | `Assets/_Project/Scripts/UI/VR/OpenXRManualOverrideLever.cs:503` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1151` |
| `MechHapticSignalDTO` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsContracts.cs:49` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:963` | none found |
| `MemoryAddressShiftSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:804` | `Assets/_Project/Scripts/Core/SystemDispatcher.cs:4614`<br>`Assets/_Project/Scripts/Core/SystemDispatcher.cs:4661` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/VaultMemoryGizmoVisualizer.cs:42`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:2152` |
| `MemoryDesyncSignal` | `Assets/_Project/Scripts/Core/Contracts/MemorySentinelSignals.cs:6` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1584`<br>`Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1613` | none found |
| `MemoryPressureSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:790` | `Assets/_Project/Scripts/Core/SystemDispatcher.cs:4682`<br>`Assets/_Project/Scripts/Core/SystemDispatcher.cs:5708` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:992`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1095`<br>`Assets/_Project/Scripts/UI/PDAShellChrome.cs:593`<br>`Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4770` |
| `MemorySentinelRollbackSignal` | `Assets/_Project/Scripts/Core/Contracts/MemorySentinelSignals.cs:51` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1599` | none found |
| `MixerStateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:317` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:421`<br>`Assets/_Project/Scripts/HectonPlayerMovement.cs:9073` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1211` |
| `MockAupRebaseSignal` | `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs:25` | `Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:965` | none found |
| `MockCombatDamageSignal` | `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainContracts.cs:494` | none found | none found |
| `MockConsumeSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:199` | none found | none found |
| `MockCraftingRequestSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:182` | none found | none found |
| `MockDamageSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:202` | none found | none found |
| `MockFloodSignal` | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:450` | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:348` | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1188` |
| `MockHotbarSelectSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:285` | none found | none found |
| `MockHudSignal` | `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs:63` | `Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:963` | none found |
| `MockImpactSignal` | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs:468` | none found | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1213` |
| `MockInventoryTransactionSignal` | `Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:295` | `Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:2618` | none found |
| `MockItemAcquiredSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:165` | none found | none found |
| `MockLaserFireSignal` | `Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:77` | none found | none found |
| `MockNarrativeTriggerSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:5835` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:2535` | none found |
| `MockPlayerFootstepSignal` | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1237` | `Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs:345` | none found |
| `MockPlayerPositionSignal` | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:253` | `Assets/_Project/Scripts/Quest/QuestDagMockSignalJobs.cs:35` | none found |
| `MockPredatorSignal` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:6088` | none found | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:3587` |
| `MockQualityWeightSignal` | `Assets/_Project/Scripts/Core/Contracts/DrsContracts.cs:353` | none found | none found |
| `MockReconstructionInputSignal` | `Assets/_Project/Scripts/Core/Contracts/DrsContracts.cs:391` | none found | none found |
| `MockRockCollisionSignal` | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1299` | none found | none found |
| `MockStoryEventSignal` | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:238` | `Assets/_Project/Scripts/Quest/QuestDagMockSignalJobs.cs:57` | none found |
| `MockTextRequestSignal` | `Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:1678` | none found | none found |
| `MockToolUsedSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:216` | none found | none found |
| `ModAssetReferenceSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:174` | none found | none found |
| `ModFutureDevNullSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:216` | none found | none found |
| `ModHapticPulseSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:241` | none found | none found |
| `ModInteractionRejectedPayload` | `Assets/_Project/Scripts/ModdingAPI/ModSpatialContracts.cs:254` | none found | none found |
| `ModSpawnRequestSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:161` | none found | none found |
| `ModSubtitleCueSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:254` | none found | none found |
| `ModdedGameMaskSignal` | `Assets/_Project/Scripts/Core/Contracts/MemorySentinelSignals.cs:68` | `Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs:577` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:777` |
| `ModuleDeconstructSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:682` | `Assets/_Project/Scripts/ConstructionManager.cs:1060` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1083` |
| `MovementAcousticSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:918` | `Assets/_Project/Scripts/Construction/HatchLockJobs.cs:421`<br>`Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationContracts.cs:520`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2905`<br>`Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:2011`<br>`Assets/_Project/Scripts/HectonPlayerMovement.cs:7783`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineBallastBuoyancyContracts.cs:600`<br>`Assets/_Project/Scripts/Physiology/ShinobuSuitIntegrityJobs.cs:410` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.FlockingAvoidance.cs:40`<br>`Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:1392`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1099`<br>`Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.AcousticSdf.cs:618`<br>`Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs:853`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:7377` |
| `NarrativeFocusSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:272` | `Assets/_Project/Scripts/HectonNarrativeDirector.cs:560` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1207`<br>`Assets/_Project/Scripts/HectonPlayerMovement.cs:8114` |
| `NarrativeHudWaypointSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:334` | `Assets/_Project/Scripts/HectonNarrativeDirector.cs:583` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1213` |
| `NarrativePoiStateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:358` | `Assets/_Project/Scripts/HectonNarrativeDirector.cs:621`<br>`Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs:909` | `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:2050`<br>`Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1136`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1217` |
| `OxygenCriticalSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1018` | `Assets/_Project/Scripts/World/BioCableIK.cs:452` | `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:1246`<br>`Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs:810`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1105`<br>`Assets/_Project/Scripts/HectonSurvivalSystem.cs:1064` |
| `PDAEventPayload` | `Assets/_Project/Scripts/PlayerPDA.cs:75` | `Assets/_Project/Scripts/PlayerPDA.cs:653` | none found |
| `PdaExchangeStateChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:89` | `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:976` | `Assets/_Project/Scripts/UI/PDABarterTab.cs:212`<br>`Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1025` |
| `PhysicsEventPayload` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:1166` | `Assets/_Project/Scripts/AtlasSignal/SignalBeacon.cs:316`<br>`Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:2147`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:6797`<br>`Assets/_Project/Scripts/Construction/DroneFleetManager.cs:6604`<br>`Assets/_Project/Scripts/Gameplay/CelestialCataclysmSystem.cs:247`<br>`Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs:428`<br>`Assets/_Project/Scripts/Physics/Cable132/CablePhysicsSolver132.cs:1648`<br>`Assets/_Project/Scripts/Physics/HarpoonTensionSolver328.cs:839`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:1034`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:1077`<br>`Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs:5430`<br>`Assets/_Project/Scripts/UI/SubtitleManager.cs:2093`<br>... +2 more | `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:6759`<br>`Assets/_Project/Scripts/Gameplay/TraumaDispatcher.cs:845`<br>`Assets/_Project/Scripts/HectonDirectorAI.cs:1014`<br>`Assets/_Project/Scripts/Interaction/VRLeakPatchWeldTarget.cs:235`<br>`Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs:3168`<br>`Assets/_Project/Scripts/PhysicsApplySystem.cs:843`<br>`Assets/_Project/Scripts/SpatialAudioManager.cs:6593`<br>`Assets/_Project/Scripts/Tools/ToolHapticsRuntime.cs:413`<br>`Assets/_Project/Scripts/UI/AcousticEcholocationTranslator.cs:439`<br>`Assets/_Project/Scripts/Visor/SpectrumSystem.cs:2390` |
| `PhysiologyStateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1625` | `Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs:1116`<br>`Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:977`<br>`Assets/_Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:1201` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1192`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1260`<br>`Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs:670`<br>`Assets/_Project/Scripts/HectonSurvivalSystem.cs:1520`<br>`Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs:1693` |
| `PipeRuptureSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1123` | `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs:905`<br>`Assets/_Project/Scripts/Construction/LogisticsPipeNode.cs:743` | `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:1249`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1115` |
| `PlasmaBeamAcousticEchoTap` | `Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:92` | `Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs:794` | none found |
| `PlayVoiceOverSignal` | `Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs:1691` | `Assets/_Project/Scripts/Core/Data/BabelDictionaryStore.cs:1152` | none found |
| `PlayerActionCancelledSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:197` | `Assets/_Project/Scripts/Gameplay/PlayerActionController.cs:413` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:152` |
| `PlayerActionCompletedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:181` | `Assets/_Project/Scripts/Gameplay/PlayerActionController.cs:398` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:148` |
| `PlayerActionProgressSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:162` | `Assets/_Project/Scripts/Gameplay/PlayerActionController.cs:379` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:144` |
| `PlayerBaseEnterSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1901` | `Assets/_Project/Scripts/BaseModule.cs:4581` | `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:1162`<br>`Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs:2294`<br>`Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs:2405`<br>`Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:1520` |
| `PlayerBaseExitSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1916` | `Assets/_Project/Scripts/BaseModule.cs:4593` | `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:1181`<br>`Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs:2290`<br>`Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs:2401`<br>`Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:1508` |
| `PlayerExhaleSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:121` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:11293` | `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:6761`<br>`Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:1329`<br>`Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs:464` |
| `PlayerFatalPressureSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:161` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:11317`<br>`Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs:1709` | `Assets/_Project/Scripts/UI/HectonOSBootManager.cs:325`<br>`Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:1350` |
| `PlayerFootstepSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:19` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:11259` | `Assets/_Project/Scripts/PlayerFootstepAudio.cs:254` |
| `PlayerInputSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:1264` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:3962` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:288`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:685`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:752`<br>`Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs:2466`<br>`Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs:2487`<br>`Assets/_Project/Scripts/HectonFabricatorUI.cs:692`<br>`Assets/_Project/Scripts/HectonFabricatorUI.cs:718`<br>`Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:350`<br>`Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:367`<br>`Assets/_Project/Scripts/MainMenuController.cs:1554`<br>`Assets/_Project/Scripts/MainMenuController.cs:1570`<br>`Assets/_Project/Scripts/PlayerBuilder.cs:829`<br>... +12 more |
| `PlayerLookTargetSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:1283` | `Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:677`<br>`Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:700` | `Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:464` |
| `PlayerRespawnSignal` | `Assets/_Project/Scripts/Core/Contracts/Signals/PlayerRespawnSignal.cs:28` | `Assets/_Project/Scripts/Gameplay/PlayerDeathReconciliationBridge.cs:61`<br>`Assets/_Project/Scripts/Gameplay/PlayerDeathReconciliationBridge.cs:65`<br>`Assets/_Project/Scripts/Physiology/ShinobuRespawnJobs.cs:107` | `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:1270`<br>`Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs:4453`<br>`Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs:260`<br>`Assets/_Project/Scripts/PlayerInventory.cs:2929` |
| `PlayerSprintStateSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:141` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:11305` | `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs:1209` |
| `PlayerStateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:106` | `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:1224`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2784` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:3540`<br>`Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:1078`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1196`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1254`<br>`Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs:215`<br>`Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs:222`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2529`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:3100`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:3201`<br>`Assets/_Project/Scripts/Physiology/PlayerStressMetricsRuntime.cs:312`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5982` |
| `PlayerStressSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1670` | `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:1085`<br>`Assets/_Project/Scripts/Fauna/FaunaBrain.cs:5350`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2810`<br>`Assets/_Project/Scripts/Physiology/PlayerStressMetricsRuntime.cs:469`<br>`Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:1973` | `Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:1064`<br>`Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:2251`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:10678`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1194`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1248`<br>`Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs:1809`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:3118`<br>`Assets/_Project/Scripts/HectonDirectorAI.cs:2169`<br>`Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:315`<br>`Assets/_Project/Scripts/Visor/PlayerStressVFX.cs:448` |
| `PlayerTransportBailoutSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:178` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:11333` | `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs:1537` |
| `PlayerWaterSplashSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:52` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:11282` | `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:1294`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:6881`<br>`Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:1337` |
| `PowerDrainSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1347` | `Assets/_Project/Scripts/Fabricator.cs:3290`<br>`Assets/_Project/Scripts/Tools/LaserCutterDodRuntime.cs:1198` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1137` |
| `PrefabAcousticSignatureSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:328` | `Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistry.cs:437`<br>`Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistryRuntimeBinder.cs:377` | none found |
| `PrefabLoreLinkSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:352` | `Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistry.cs:448`<br>`Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistryRuntimeBinder.cs:386` | none found |
| `ProgressionEventSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:156` | `Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1311`<br>`Assets/_Project/Scripts/Narrative/HectonNarrativeDirector_PoiTriggers.cs:705`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:7497` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1201`<br>`Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs:324`<br>`Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs:498` |
| `ProgressionMetaSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:169` | `Assets/_Project/Scripts/Core/Signals/ProgressionMetaSignalRoute.cs:68` | `Assets/_Project/Scripts/Meta/DynamicDifficultyDirector.cs:131`<br>`Assets/_Project/Scripts/Meta/GlobalProfileManager.cs:357`<br>`Assets/_Project/Scripts/PDA/PDALogbookManager.cs:674`<br>`Assets/_Project/Scripts/Progression/PlayerAchievementRegistry.cs:369` |
| `PrologueCompleteSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1458` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:496`<br>`Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs:1218`<br>`Assets/_Project/Scripts/UI/VR/OpenXRManualOverrideLever.cs:534` | `Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:347`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:246`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1149`<br>`Assets/_Project/Scripts/HectonFluidEngine.cs:4100`<br>`Assets/_Project/Scripts/Prologue/Space/PrologueWorldHandoffSceneLoader.cs:161`<br>`Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:573` |
| `QuestDagMockItemAcquiredSignal` | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:269` | `Assets/_Project/Scripts/Quest/QuestDagMockSignalJobs.cs:45` | none found |
| `RadarJamSignal` | `Assets/_Project/Scripts/Core/Contracts/SeedShipAnomalySignals.cs:34` | `Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyJobs.cs:199` | none found |
| `RadiationDoseSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:385` | `Assets/_Project/Scripts/Gameplay/BioReactor.cs:1071`<br>`Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:336`<br>`Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:2425`<br>`Assets/_Project/Scripts/PlayerInventory.cs:6453`<br>`Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:1022` | `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:1245`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1181`<br>`Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:2307`<br>`Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:1475`<br>`Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs:1318`<br>`Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1008` |
| `RadiationSourceSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:39` | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:289`<br>`Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:302`<br>`Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:2301`<br>`Assets/_Project/Scripts/Thermodynamics/ReactorThermalGridJobs.cs:843`<br>`Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:1009`<br>`Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:1036` | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:2262`<br>`Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:2286` |
| `ReactorDamageSignal` | `Assets/_Project/Scripts/Core/Contracts/Signals/ReactorDamageSignal.cs:13` | `Assets/_Project/Scripts/Gameplay/BioReactor.cs:1020` | `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs:1122` |
| `RebaseSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:490` | `Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs:810`<br>`Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:844` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1067` |
| `ReconDataSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:159` | none found | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1155` |
| `ReentryAcousticStressSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1430` | `Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs:769`<br>`Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs:786` | `Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:300` |
| `ReentryVfxStateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:466` | `Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:976` | none found |
| `ResidencySectorDehydratedSignal` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:5408` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:4801`<br>`Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:5468` |
| `ResidencySectorHydratedSignal` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:5392` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:5473` |
| `ResolutionChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:829` | `Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:1981`<br>`Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs:405` | none found |
| `ResourceDepletionDeltaSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:55` | `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1184`<br>`Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs:1809`<br>`Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:2743` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1183` |
| `RespawnSignalResolvedTargetTransformer` | `Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs:2135` | none found | none found |
| `RigidbodySleepSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1151` | `Assets/_Project/Scripts/GlobalPhysicsStateManager.cs:4205` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1119` |
| `RollbackRequiredSignal` | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs:460` | `Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs:1437` | none found |
| `SandboxMockAcousticSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:188` | none found | none found |
| `SaveCompletedSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:283` | `Assets/_Project/Scripts/SaveManager.cs:3202` | none found |
| `SaveLifecycleSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:172` | `Assets/_Project/Scripts/SaveManager.cs:3187` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1157` |
| `SaveMetadataReadySignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:261` | `Assets/_Project/Scripts/SaveThumbnailSystem.cs:1072` | none found |
| `SaveStatusSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:299` | `Assets/_Project/Scripts/SaveManager.cs:3176` | none found |
| `ScalabilityChangedEvent` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:37` | `Assets/_Project/Scripts/Core/IPlatformIntegration.cs:158` | `Assets/_Project/Scripts/Core/IPlatformIntegration.cs:171` |
| `ScanCompleteSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1181` | `Assets/_Project/Scripts/Data/Monolith/H8AppliedLoreRuntime.cs:355`<br>`Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1149` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1123`<br>`Assets/_Project/Scripts/Narrative/HectonNarrativeDirector_PoiTriggers.cs:1142`<br>`Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs:855` |
| `ScanLogChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:69` | `Assets/_Project/Scripts/ScanLogSystem.cs:583` | `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:948`<br>`Assets/_Project/Scripts/PDA/PDALogbookManager.cs:614` |
| `ScannerToolActiveSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1164` | `Assets/_Project/Scripts/ScannerTool.cs:1088` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1121`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1278`<br>`Assets/_Project/Scripts/Core/Signals/SignalBridgeRoutes.cs:177`<br>`Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:532`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:7395`<br>`Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs:1055` |
| `SeaglidePropulsionRequestSignal` | `Assets/_Project/Scripts/Core/Contracts/Physics/SeaglidePropulsionContracts.cs:86` | `Assets/_Project/Scripts/Gameplay/MantaScooter.cs:836` | `Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsRuntime.cs:507` |
| `SectorDehydratedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:960` | `Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:714` | `Assets/_Project/Scripts/World/EcosystemDirector.cs:5374` |
| `SectorResidencyHydratedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:945` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:517`<br>`Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:684` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:346`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:5399` |
| `SeismicShockwaveSignal` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:5857` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:2628`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:4863` | none found |
| `SeismicSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1475` | `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:1720`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:2646`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:3541`<br>`Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs:4851`<br>`Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:1955` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1163`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1272`<br>`Assets/_Project/Scripts/VFX/CameraJuiceSystem_CameraJuiceBurst.cs:453`<br>`Assets/_Project/Scripts/VFX/CameraJuiceSystem_CameraJuiceBurst.cs:545`<br>`Assets/_Project/Scripts/World/AbyssalThermalManager.cs:5850` |
| `SessionLifecycleSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:188` | `Assets/_Project/Scripts/Core/Signals/SessionLifecycleSignalRoute.cs:50` | `Assets/_Project/Scripts/Meta/DynamicDifficultyDirector.cs:156`<br>`Assets/_Project/Scripts/Meta/GlobalProfileManager.cs:342`<br>`Assets/_Project/Scripts/Meta/RunModifierController.cs:265`<br>`Assets/_Project/Scripts/PDA/PDALogbookManager.cs:560`<br>`Assets/_Project/Scripts/Progression/PDAContextualAdvisorySystem.cs:411`<br>`Assets/_Project/Scripts/Progression/PlayerAchievementRegistry.cs:389`<br>`Assets/_Project/Scripts/UI/HectonOSBootManager.cs:264` |
| `ShinobuPlayerExertionSignal` | `Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:20` | `Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:2045` | none found |
| `SignalAudioEvent` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/Fauna/FaunaBrain.cs:3284`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:4593` | none found |
| `SignalWardenMockDamageSignal` | `Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1278` | `Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs:332` | none found |
| `SiltExplosionSignal` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsContracts.cs:73` | `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs:990` | none found |
| `SimulationBucketSyncSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1960` | `Assets/_Project/Scripts/Core/SystemDispatcher.cs:5189` | none found |
| `SimulationPauseSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:349` | `Assets/_Project/Scripts/Core/Signals/SignalBridgeRoutes.cs:98` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1167`<br>`Assets/_Project/Scripts/Core/SystemDispatcher.cs:1912` |
| `SolarFlareSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:475` | none found | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1065` |
| `SonarPingSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:990` | `Assets/_Project/Scripts/QA/QAEnduranceWatchdogBot.cs:721` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1101` |
| `SoundscapeProfileSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:347` | `Assets/_Project/Scripts/HectonNarrativeDirector.cs:542` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1215` |
| `SpectrumScanSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1136` | none found | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1117` |
| `SplashEvent` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:1110` | `Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs:1710`<br>`Assets/_Project/Scripts/HectonPlayerMovement.cs:11237`<br>`Assets/_Project/Scripts/Physics/FluidFeedbackListener.cs:198`<br>`Assets/_Project/Scripts/Physics/FluidFeedbackListener.cs:217` | `Assets/_Project/Scripts/Physics/FluidFeedbackListener.cs:166` |
| `StateChangedSignal` | `Assets/_Project/Scripts/Quest/QuestDagRuntimeTypes.cs:223` | `Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs:1633`<br>`Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs:1893` | none found |
| `StateCorrectionSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:556` | `Assets/_Project/Scripts/Core/Signals/CoreDeterminismSignals.cs:97` | none found |
| `StorageDebtSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1377` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3626` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1143` |
| `StreamingTurbulenceSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1398` | `Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs:1188`<br>`Assets/_Project/Scripts/Prologue/Space/PrologueWorldHandoffSceneLoader.cs:393`<br>`Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3639` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1145` |
| `SubmarineFloodStateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1087` | `Assets/_Project/Scripts/Physics/HabitatFluidIncursionDirector.cs:1191`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:2845` | `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:1247`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1171`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1440`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1179` |
| `SubmarineLightsChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1574` | `Assets/_Project/Scripts/Gameplay/MantaScooter.cs:2165`<br>`Assets/_Project/Scripts/Gameplay/MantaScooter.cs:2263` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:1099`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1188`<br>`Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:4094`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5222` |
| `SubtitleCueSignal` | `Assets/_Project/Scripts/Core/Contracts/Signals/SubtitleCueSignal.cs:9` | `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:1465`<br>`Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs:387` | `Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs:1028` |
| `SubtitleSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:726` | none found | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1091`<br>`Assets/_Project/Scripts/UI/SubtitleManager.cs:1088` |
| `SurvivalOverrideSignal` | `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs:229` | none found | none found |
| `SurvivalVitalsChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:146` | `Assets/_Project/Scripts/Core/Signals/SignalBridgeRoutes.cs:169` | `Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1117`<br>`Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:1251`<br>`Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs:1707`<br>`Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs:1055`<br>`Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:1177`<br>`Assets/_Project/Scripts/UI/SubtitleManager.cs:1533`<br>`Assets/_Project/Scripts/UI/SuitAdvisoryController.cs:177`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:4686`<br>`Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:996`<br>`Assets/_Project/Scripts/Visor/VisorHUDController.cs:1649` |
| `SwarmDispersedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:932` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.FlockingAvoidance.cs:281`<br>`Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:703`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:6136`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:7456` | none found |
| `SyncFenceSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:616` | `Assets/_Project/Scripts/Core/Signals/CoreDeterminismSignals.cs:124`<br>`Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs:1180` | none found |
| `SystemGlitchSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:1041` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:4445`<br>`Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:1279`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime_Gyroscopes.cs:435`<br>`Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:983` | `Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:1357` |
| `SystemHealthIndexSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:852` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:865`<br>`Assets/_Project/Scripts/QA/Headless/Shinobu38QaWatchdogRuntime.cs:1318` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:3609`<br>`Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs:1099`<br>`Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:1244`<br>`Assets/_Project/Scripts/Core/InputDispatcher.cs:4446`<br>`Assets/_Project/Scripts/Core/SystemDispatcher.cs:3302`<br>`Assets/_Project/Scripts/Core/SystemDispatcher.cs:4896`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:3171`<br>`Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:863`<br>`Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs:1471`<br>`Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:965`<br>`Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs:1820`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1155`<br>... +6 more |
| `SystemHealthSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:426` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:811` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1523`<br>`Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:955`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:958`<br>`Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:486`<br>`Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1282`<br>`Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1968`<br>`Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:1185`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:6039`<br>`Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:2445` |
| `SystemKillSwitchBitsSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:184` | `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:124` | `Assets/_Project/Scripts/HectonFluidEngine.cs:8250`<br>`Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5947` |
| `SystemPauseSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1946` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:408`<br>`Assets/_Project/Scripts/Core/SceneRuntimeService.cs:583`<br>`Assets/_Project/Scripts/Core/Signals/SignalBridgeRoutes.cs:107`<br>`Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs:1537` | none found |
| `T` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:17` | `Assets/_Project/Scripts/Core/Signals/CoreDeterminismSignals.cs:262` |
| `TelemetryAnomalySignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:596` | `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:1622`<br>`Assets/_Project/Scripts/HectonPlayerMovement.cs:3348`<br>`Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs:1229`<br>`Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:995` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1073` |
| `TemperatureChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:11` | `Assets/_Project/Scripts/Power/Generators/RadioisotopeThermalGenerator.cs:954`<br>`Assets/_Project/Scripts/World/AbyssalThermalManager.cs:1953` | none found |
| `TerminalClickSignal` | `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:219` | `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:381` | `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:2726` |
| `TerminalCommandSignal` | `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:235` | `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:620`<br>`Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:1078`<br>`Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:1416` | `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:396` |
| `TerminalUnlockedSignal` | `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:267` | `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs:1318` | `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs:402` |
| `TerrainChunkGeneratedSignal` | `Assets/_Project/Scripts/World/Contracts/TerrainChunkGeneratedSignal.cs:13` | `Assets/_Project/Scripts/TerrainChunkGeneratedEvents.cs:45` | `Assets/_Project/Scripts/TerrainChunkGeneratedEvents.cs:59` |
| `TetherFiredSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:769` | none found | none found |
| `TetherSnappedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:732` | `Assets/_Project/Scripts/Physics/HarpoonTensionSolver328.cs:893`<br>`Assets/_Project/Scripts/Physics/TetherSignals.cs:98`<br>`Assets/_Project/Scripts/World/BioCableIK.cs:484` | `Assets/_Project/Scripts/Physics/TetherSignals.cs:121` |
| `TetherTensionSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:687` | `Assets/_Project/Scripts/Physics/HarpoonTensionSolver328.cs:873`<br>`Assets/_Project/Scripts/Physics/HarpoonTensionSolver328.cs:897`<br>`Assets/_Project/Scripts/Physics/TetherSignals.cs:110` | none found |
| `ThermalSourceSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:28` | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs:4778` | `Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.cs:530`<br>`Assets/_Project/Scripts/VFX/Parasites/ParasiteSwarmGpuRuntime.cs:939` |
| `ThermalStateChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:127` | `Assets/_Project/Scripts/Core/Hardware/HardwareThermalService.cs:650`<br>`Assets/_Project/Scripts/Thermodynamics/ReactorThermalGridJobs.cs:1152` | `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:972`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:988`<br>`Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:508`<br>`Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs:1806` |
| `ThermalUpdraftSignal` | `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardTypes.cs:77` | `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs:1168` | none found |
| `ThermodynamicsMockDamageSignal` | `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardTypes.cs:96` | none found | none found |
| `TimeDilationSignal` | `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:334` | `Assets/_Project/Scripts/Core/Signals/SignalBridgeRoutes.cs:80` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1165` |
| `ToolAcousticSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1321` | `Assets/_Project/Scripts/Data/Monolith/H8AppliedLoreRuntime.cs:414`<br>`Assets/_Project/Scripts/Fabricator.cs:3269`<br>`Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1405`<br>`Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs:1114`<br>`Assets/_Project/Scripts/LaserCutter.cs:2358`<br>`Assets/_Project/Scripts/Physics/Seaglide/SeaglideHydrodynamicsRuntime.cs:1044`<br>`Assets/_Project/Scripts/PlayerInventory.cs:3242`<br>`Assets/_Project/Scripts/PlayerInventory.cs:4117`<br>`Assets/_Project/Scripts/PlayerInventory.cs:5719`<br>`Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs:573`<br>`Assets/_Project/Scripts/Tools/WfcLaserCutRuntime.cs:501`<br>`Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:752`<br>... +2 more | `Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs:209`<br>`Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs:414`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1135` |
| `ToolBrokenSignal` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:233` | `Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs:1503` | none found |
| `ToolCarveRequestSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:200` | `Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs:446` | none found |
| `ToolDepletedSignal` | `Assets/_Project/Scripts/Tools/EquipmentThermalBatteryContracts.cs:116` | `Assets/_Project/Scripts/ModularEquipmentEngine.cs:3929` | none found |
| `ToolHeatSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:220` | `Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs:433` | none found |
| `ToolLoadoutChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1298` | `Assets/_Project/Scripts/PlayerToolManager.cs:796` | `Assets/_Project/Scripts/Gameplay/PlayerTransportCoordinator.cs:395`<br>`Assets/_Project/Scripts/HUDQuickBar.cs:368`<br>`Assets/_Project/Scripts/PDAInventoryTab.cs:703`<br>`Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs:337`<br>`Assets/_Project/Scripts/UI/PDAConstructionTab.cs:460`<br>`Assets/_Project/Scripts/UI/PDALoadoutTab.cs:468`<br>`Assets/_Project/Scripts/UI/PDAShellChrome.cs:662` |
| `ToolPowerDepletedSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:237` | `Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs:503` | none found |
| `ToolStateChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1278` | `Assets/_Project/Scripts/ModularEquipmentEngine.cs:3224`<br>`Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs:554` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1133`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1284`<br>`Assets/_Project/Scripts/UI/Tools/ToolDiegeticDisplayController.cs:417`<br>`Assets/_Project/Scripts/Visor/VisorHUDController.cs:1463` |
| `ToolTriggerPullSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:186` | `Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs:426` | none found |
| `ToolTriggerSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1362` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:3458`<br>`Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs:534` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1139` |
| `ToxicBioluminescenceSignal` | `Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryTypes.cs:165` | `Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:1304` | none found |
| `ToxicityExposureSignal` | `Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryTypes.cs:144` | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs:2980`<br>`Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs:1267`<br>`Assets/_Project/Scripts/Gameplay/EnvironmentalHazard.cs:619`<br>`Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs:1316`<br>`Assets/_Project/Scripts/Gameplay/TraumaDispatcher.cs:709`<br>`Assets/_Project/Scripts/HectonSurvivalSystem.cs:3544`<br>`Assets/_Project/Scripts/HectonSurvivalSystem.cs:3579`<br>`Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs:1138`<br>`Assets/_Project/Scripts/World/FloraInteractionManager.cs:5267` | `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs:1425` |
| `TraumaSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1685` | `Assets/_Project/Scripts/Physiology/PlayerStressMetricsRuntime.cs:483` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1198` |
| `UIRescaleRequestSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1055` | `Assets/_Project/Scripts/UI/FontStreamingManager.cs:260` | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1109`<br>`Assets/_Project/Scripts/UI/DiegeticHudManualLayout.cs:113`<br>`Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs:2142`<br>`Assets/_Project/Scripts/UI/SubtitleManager.cs:828` |
| `VehicleCommandSignal` | `Assets/_Project/Scripts/Gameplay/VehicleCommandSignals.cs:26` | none found | none found |
| `VehicleHazardSignal` | `Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageContracts.cs:200` | `Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageJobs.cs:645` | none found |
| `VehicleUpgradesChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:110` | `Assets/_Project/Scripts/Gameplay/VehicleUpgradeModule.cs:198` | none found |
| `VfxSparkRequestSignal` | `Assets/_Project/Scripts/Tools/ToolKinematics/Contracts/ToolKinematicsContracts.cs:253` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:5775`<br>`Assets/_Project/Scripts/Tools/LaserCutterDodRuntime.cs:650`<br>`Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs:442` | none found |
| `VisorBreachSignal` | `Assets/_Project/Scripts/Visor/DiegeticVisorLensTypes.cs:167` | `Assets/_Project/Scripts/Visor/DiegeticVisorLensRuntime.cs:1499` | none found |
| `VisorDropletSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:499` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:11355`<br>`Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:952` | `Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs:494` |
| `VisualFlareSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:903` | `Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:752` | none found |
| `VisualScavengeSignal` | `Assets/_Project/Scripts/Core/Contracts/Signals/VisualScavengeSignal.cs:29` | `Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs:1840` | none found |
| `VitalWarningSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:695` | `Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs:565` | `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:1241`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1085` |
| `VocalCueSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:756` | `Assets/_Project/Scripts/Audio/Synthesis/Editor/DigitalVoiceForgeWindow.cs:338`<br>`Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:1453` | `Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs:930` |
| `VocalWarningSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:741` | `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:497`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:441`<br>`Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs:2096`<br>`Assets/_Project/Scripts/Narrative/Campaign/MetaCampaignService.cs:860`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:1666`<br>`Assets/_Project/Scripts/SubmarineStructuralGrid.cs:1692` | `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs:1240`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1089` |
| `VoxelCarveEvent` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:863` | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs:2128` | `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:1018` |
| `WakeGeneratedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:102` | `Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:1978`<br>`Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs:2040`<br>`Assets/_Project/Scripts/Gameplay/VehicleMotor.cs:1097`<br>`Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs:2305`<br>`Assets/_Project/Scripts/World/FloraInteractionManager.cs:3291` | `Assets/_Project/Scripts/World/FloraInteractionManager.cs:3296` |
| `WakeRequestSignal` | `Assets/_Project/Scripts/Core/Signals/PhysicsWakeSignalContracts.cs:17` | `Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs:1400`<br>`Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.WakeRequests.cs:16` | `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementRuntime.cs:491` |
| `WaterTransitionSignal` | `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs:92` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:11382` | `Assets/_Project/Scripts/Gameplay/WaterTransitionHandler.cs:68`<br>`Assets/_Project/Scripts/Visor/DynamicDecalVaultRuntime.cs:2414` |
| `WaterlineBreachSignal` | `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereContracts.cs:1599` | `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs:2001` | `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs:1732` |
| `WeatherChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1931` | none found | `Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs:289` |
| `WeatherStrengthSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1508` | none found | `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1175` |
| `WfcOutpostDoorPowerSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:238` | `Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:708` | `Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:1426` |
| `WfcOutpostGeneratedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:201` | `Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:1877` | `Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1191`<br>`Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:432`<br>`Assets/_Project/Scripts/Tools/WfcLaserCutRuntime.cs:343` |
| `WfcOutpostStateChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:222` | `Assets/_Project/Scripts/AudioLog/AudioLogPickup.cs:343`<br>`Assets/_Project/Scripts/Gameplay/MessageTerminal.cs:1032`<br>`Assets/_Project/Scripts/Gameplay/SealedDoor.cs:884` | `Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs:124`<br>`Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1232`<br>`Assets/_Project/Scripts/SaveManager.cs:1951` |

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
| `PlayerActionProgressSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:162` | `Assets/_Project/Scripts/Gameplay/PlayerActionController.cs:379` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:144` |
| `PlayerActionCompletedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:181` | `Assets/_Project/Scripts/Gameplay/PlayerActionController.cs:398` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:148` |
| `PlayerActionCancelledSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:197` | `Assets/_Project/Scripts/Gameplay/PlayerActionController.cs:413` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:152` |
| `PdaExchangeStateChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:89` | `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:976` | `Assets/_Project/Scripts/UI/PDABarterTab.cs:212`<br>`Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:1025` |
| `VehicleUpgradesChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.UiSaveWorld.cs:110` | `Assets/_Project/Scripts/Gameplay/VehicleUpgradeModule.cs:198` | none found |
| `PlayerStateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:106` | `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:1224`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2784` | `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs:3540`<br>`Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs:1078`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1196`<br>`Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs:1254`<br>`Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs:215`<br>`Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs:222`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:2529`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:3100` |
| `InventoryChangedSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.PhysicsInventory.cs:308` | `Assets/_Project/Scripts/Editor/CraftingFastFailXRayWindow_SHINOBU317.cs:142`<br>`Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.CargoSync.cs:869`<br>`Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.CargoSync.cs:871`<br>`Assets/_Project/Scripts/PlayerInventory.cs:4977` | `Assets/_Project/Scripts/Editor/SoaInventoryXRayWindow_SHINOBU316.cs:125`<br>`Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:930`<br>`Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs:1045`<br>`Assets/_Project/Scripts/HectonPlayerMovement.cs:5090`<br>`Assets/_Project/Scripts/PDAInventoryTab.cs:633`<br>`Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1146`<br>`Assets/_Project/Scripts/PlayerToolManager.cs:2531`<br>`Assets/_Project/Scripts/SubmarineFluidDynamics.cs:3013` |
| `InputStateSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:1001` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:762` | `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs:961`<br>`Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:764`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1980`<br>`Assets/_Project/Scripts/UI/InteractionUI.cs:992`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:6021` |
| `SystemHealthSignal` | `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:426` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:811` | `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs:1523`<br>`Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs:955`<br>`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs:958`<br>`Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs:486`<br>`Assets/_Project/Scripts/Power/ShinobuLogisticsRouter.cs:1282`<br>`Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs:1968`<br>`Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs:1185`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:6039` |

## Phi-Resonance Connectivity Model

The engine connectivity model is not mystical. It is a three-layer resonance model: contracts define stable frequency, typed signal lanes carry bounded pulses, and dispatcher phases control when state can move. `GlobalRegistry` is the cold authority spine for stable interfaces. `SignalBus<T>` and queue-backed event lanes are the nervous system for broadcast state. DataVault/native buffers are the mass: owned memory that simulation mutates and presentation samples.

Low/MX350 interpretation: coalesce lanes, consume bounded snapshots, keep optional visual consumers in `VISUAL_SYNC`, and shed mips/LOD before simulation truth. High/Ultra interpretation: do not increase gameplay broadcast cost; attach richer presentation consumers to the same snapshots and spend the saved budget on light, fog, HUD, audio, and material overkill.

Connectivity risk: `Hecton8.Core` currently references many domain assemblies directly. That may be intentional current architecture, but it is not a clean inward-only core. The integrator should treat this as a dependency inversion watchpoint, especially while many agents expand contracts in parallel.

## Verification Commands

- `python Tools/BuildArchitectureAtlas.py`
- `python Tools/AtlasCheck.py`
- `python -m py_compile Tools/BuildArchitectureAtlas.py Tools/AtlasCheck.py`
- C# compile verification is outside this atlas; run Unity import/Console and serial CLI builds as separate evidence.
- AtlasCheck gate: `python Tools/AtlasCheck.py` exits `0` with `ATLAS_CHECK_PASS references=5601 atlas=C:\tmp\Hecton8-codex-systems\Docs\Generated\DEPENDENCY_GRAPH.md`. This is static reference integrity only, not Unity/runtime proof.
- This generated atlas is not `VERIFIED` unless `Tools/AtlasCheck.py` exits `0` after generation.

## Residual Risk

- STATIC_SOURCE only: Unity import, runtime wiring, actual VRAM residency, profiler frame time, GC, build, and Play Mode remain PENDING VERIFICATION.
- The signal producer map only resolves explicit `SignalBus<T>` flow calls. Legacy `GlobalSignals.Publish(...)` variable publishes and wrapper methods require Roslyn-level dataflow to type-resolve fully.
- Task and agent-log folders are intentionally excluded from architecture authority sections.
