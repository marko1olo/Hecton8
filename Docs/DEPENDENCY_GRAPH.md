# HECTON-8 Architecture Atlas - Dependency Graph

Generated: 2026-05-15 14:03:07
Status: ATLAS VERIFIED PENDING RUNTIME VERIFICATION
Evidence class: STATIC_SOURCE / STATIC_DOC / FILESYSTEM / PY_TOOL. No Unity Editor, Play Mode, Memory Profiler, Frame Debugger, or player build evidence is claimed here.

## Source Of Authority
- `AGENTS.md`
- `Docs/Tasks/CURRENT_BATCH.md`
- `Docs/Actual Domains of Project.txt`
- `Docs/ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md`
- `Docs/Reports/VRAM_Budget_Audit.json`
- `Docs/Reports/VRAM_Budget_Audit_Summary.md`
- `Docs/Reports/VRAM_Remediation_Plan.md`
- `Docs/AgentLogs/LOG_VRAM_ASSET_SCOUT.md`
- `Docs/AgentLogs/Rationale_VRAM_ASSET_SCOUT.md`
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

- C# source files scanned under `Assets/` and `Packages/`: 5034
- C# line count scanned under `Assets/` and `Packages/`: 1,709,155
- First-party C# source files under `Assets/_Project/Scripts/`: 1505
- First-party C# line count under `Assets/_Project/Scripts/`: 960,494
- Assembly definitions scanned: 152
- First-party assembly definitions under `Assets/_Project/`: 91
- Markdown docs under `Docs/`: 1451

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

Assemblies directly depending on exact `Hecton8.Core`: 35

| Assembly | Path |
|---|---|
| `Hecton8.Audio.Prologue` | `Assets/_Project/Scripts/Audio/Prologue/Hecton8.Audio.Prologue.asmdef` |
| `Hecton8.Core.Determinism` | `Assets/_Project/Scripts/Core/Determinism/Hecton8.Core.Determinism.asmdef` |
| `Hecton8.Core.Hardware` | `Assets/_Project/Scripts/Core/Hardware/Hecton8.Core.Hardware.asmdef` |
| `Hecton8.EditModeTests` | `Assets/_Project/Tests/Editor/Hecton8.EditModeTests.asmdef` |
| `Hecton8.Editor` | `Assets/_Project/Scripts/Editor/Hecton8.Editor.asmdef` |
| `Hecton8.Gameplay.Loot` | `Assets/_Project/Scripts/Gameplay/Loot/Hecton8.Gameplay.Loot.asmdef` |
| `Hecton8.Gameplay.Loot.Contracts` | `Assets/_Project/Scripts/Gameplay/Loot/Contracts/Hecton8.Gameplay.Loot.Contracts.asmdef` |
| `Hecton8.Gameplay.Mining` | `Assets/_Project/Scripts/Gameplay/Mining/Hecton8.Gameplay.Mining.asmdef` |
| `Hecton8.Graphics.Caustics` | `Assets/_Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef` |
| `Hecton8.Graphics.DRS` | `Assets/_Project/Scripts/Graphics/DRS/Hecton8.Graphics.DRS.asmdef` |
| `Hecton8.Graphics.Materials` | `Assets/_Project/Scripts/Graphics/Materials/Hecton8.Graphics.Materials.asmdef` |
| `Hecton8.Lighting` | `Assets/_Project/Scripts/Lighting/Hecton8.Lighting.asmdef` |
| `Hecton8.Lighting.Shafts` | `Assets/_Project/Scripts/Lighting/Shafts/Hecton8.Lighting.Shafts.asmdef` |
| `Hecton8.Narrative.Campaign` | `Assets/_Project/Scripts/Narrative/Campaign/Hecton8.Narrative.Campaign.asmdef` |
| `Hecton8.Optimization.Editor` | `Assets/_Project/Scripts/Optimization/Editor/Hecton8.Optimization.Editor.asmdef` |
| `Hecton8.Physiology` | `Assets/_Project/Scripts/Physiology/Hecton8.Physiology.asmdef` |
| `Hecton8.PlayModeTests` | `Assets/_Project/Tests/PlayMode/Hecton8.PlayModeTests.asmdef` |
| `Hecton8.Plugins` | `Assets/_Project/Scripts/Plugins/Hecton8.Plugins.asmdef` |
| `Hecton8.Power.Generators` | `Assets/_Project/Scripts/Power/Generators/Hecton8.Power.Generators.asmdef` |
| `Hecton8.Prologue.Space` | `Assets/_Project/Scripts/Prologue/Space/Hecton8.Prologue.Space.asmdef` |
| `Hecton8.Prologue.VFX` | `Assets/_Project/Scripts/Prologue/VFX/Hecton8.Prologue.VFX.asmdef` |
| `Hecton8.QA` | `Assets/_Project/Scripts/QA/Hecton8.QA.asmdef` |
| `Hecton8.QA.Editor` | `Assets/_Project/Scripts/QA/Editor/Hecton8.QA.Editor.asmdef` |
| `Hecton8.QA.Headless` | `Assets/_Project/Scripts/QA/Headless/Hecton8.QA.Headless.asmdef` |
| `Hecton8.QA.Headless.Editor` | `Assets/_Project/Scripts/QA/Headless/Editor/Hecton8.QA.Headless.Editor.asmdef` |
| `Hecton8.UI.Diegetic` | `Assets/_Project/Scripts/UI/Diegetic/Hecton8.UI.Diegetic.asmdef` |
| `Hecton8.UI.Editor` | `Assets/_Project/Scripts/UI/Editor/Hecton8.UI.Editor.asmdef` |
| `Hecton8.UI.Tools` | `Assets/_Project/Scripts/UI/Tools/Hecton8.UI.Tools.asmdef` |
| `Hecton8.UI.VR` | `Assets/_Project/Scripts/UI/VR/Hecton8.UI.VR.asmdef` |
| `Hecton8.VFX.Debris` | `Assets/_Project/Scripts/VFX/Debris/Hecton8.VFX.Debris.asmdef` |
| `Hecton8.VFX.Materials` | `Assets/_Project/Scripts/VFX/Materials/Hecton8.VFX.Materials.asmdef` |
| `Hecton8.Vehicles.VFX` | `Assets/_Project/Scripts/Vehicles/VFX/Hecton8.Vehicles.VFX.asmdef` |
| `Hecton8.World.Economy` | `Assets/_Project/Scripts/World/Resources/Hecton8.World.Economy.asmdef` |
| `Hecton8.World.Outposts` | `Assets/_Project/Scripts/World/Outposts/Hecton8.World.Outposts.asmdef` |
| `Hecton8.World.Streaming` | `Assets/_Project/Scripts/World/Streaming/Hecton8.World.Streaming.asmdef` |

Assemblies depending on any `Hecton8.Core*` assembly: 60

| Assembly | Core-family references | Path |
|---|---|---|
| `Hecton8.AI.Cognition` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/AI/Cognition/Hecton8.AI.Cognition.asmdef` |
| `Hecton8.AI.Ecology.Migration` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/AI/Ecology/Migration/Hecton8.AI.Ecology.Migration.asmdef` |
| `Hecton8.AI.Foveated` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/AI/Foveated/Hecton8.AI.Foveated.asmdef` |
| `Hecton8.Animation.IK` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Animation/IK/Hecton8.Animation.IK.asmdef` |
| `Hecton8.Audio.Echolocation` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Audio/Echolocation/Hecton8.Audio.Echolocation.asmdef` |
| `Hecton8.Audio.Prologue` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Audio/Prologue/Hecton8.Audio.Prologue.asmdef` |
| `Hecton8.Audio.Synthesis` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Audio/Synthesis/Hecton8.Audio.Synthesis.asmdef` |
| `Hecton8.Core.Bucketing` | `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Core/Bucketing/Hecton8.Core.Bucketing.asmdef` |
| `Hecton8.Core.Database` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Core/Database/Hecton8.Core.Database.asmdef` |
| `Hecton8.Core.Determinism` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Core/Determinism/Hecton8.Core.Determinism.asmdef` |
| `Hecton8.Core.Hardware` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Core/Hardware/Hecton8.Core.Hardware.asmdef` |
| `Hecton8.Core.Memory` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Core/Memory/Hecton8.Core.Memory.asmdef` |
| `Hecton8.Core.Memory.Defrag` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Core/Memory/Defrag/Hecton8.Core.Memory.Defrag.asmdef` |
| `Hecton8.Core.Persistence` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Core/Persistence/Hecton8.Core.Persistence.asmdef` |
| `Hecton8.Core.Persistence.Paging` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Core/Persistence/Paging/Hecton8.Core.Persistence.Paging.asmdef` |
| `Hecton8.Core.Scheduling` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Core/Scheduling/Hecton8.Core.Scheduling.asmdef` |
| `Hecton8.EditModeTests` | `Hecton8.Core` | `Assets/_Project/Tests/Editor/Hecton8.EditModeTests.asmdef` |
| `Hecton8.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/Editor/Hecton8.Editor.asmdef` |
| `Hecton8.Gameplay.Loot` | `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Gameplay/Loot/Hecton8.Gameplay.Loot.asmdef` |
| `Hecton8.Gameplay.Loot.Contracts` | `Hecton8.Core` | `Assets/_Project/Scripts/Gameplay/Loot/Contracts/Hecton8.Gameplay.Loot.Contracts.asmdef` |
| `Hecton8.Gameplay.Mining` | `Hecton8.Core` | `Assets/_Project/Scripts/Gameplay/Mining/Hecton8.Gameplay.Mining.asmdef` |
| `Hecton8.Graphics.Caustics` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef` |
| `Hecton8.Graphics.DRS` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Graphics/DRS/Hecton8.Graphics.DRS.asmdef` |
| `Hecton8.Graphics.Materials` | `Hecton8.Core.Contracts`, `Hecton8.Core` | `Assets/_Project/Scripts/Graphics/Materials/Hecton8.Graphics.Materials.asmdef` |
| `Hecton8.Input.Determinism` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Input/Determinism/Hecton8.Input.Determinism.asmdef` |
| `Hecton8.Input.Universal` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Input/Universal/Hecton8.Input.Universal.asmdef` |
| `Hecton8.Inventory.Algorithms` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Inventory/Algorithms/Hecton8.Inventory.Algorithms.asmdef` |
| `Hecton8.Lighting` | `Hecton8.Core` | `Assets/_Project/Scripts/Lighting/Hecton8.Lighting.asmdef` |
| `Hecton8.Lighting.Shafts` | `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Lighting/Shafts/Hecton8.Lighting.Shafts.asmdef` |
| `Hecton8.Logistics.Grid.Contracts` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Logistics/Grid/Contracts/Hecton8.Logistics.Grid.Contracts.asmdef` |
| `Hecton8.Narrative.Camera` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Narrative/Camera/Hecton8.Narrative.Camera.asmdef` |
| `Hecton8.Narrative.Campaign` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Narrative/Campaign/Hecton8.Narrative.Campaign.asmdef` |
| `Hecton8.Narrative.Prologue` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Narrative/Prologue/Hecton8.Narrative.Prologue.asmdef` |
| `Hecton8.Optimization.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/Optimization/Editor/Hecton8.Optimization.Editor.asmdef` |
| `Hecton8.Physics.CCD` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Physics/CCD/Hecton8.Physics.CCD.asmdef` |
| `Hecton8.Physics.Determinism` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Physics/Determinism/Hecton8.Physics.Determinism.asmdef` |
| `Hecton8.Physiology` | `Hecton8.Core` | `Assets/_Project/Scripts/Physiology/Hecton8.Physiology.asmdef` |
| `Hecton8.PlayModeTests` | `Hecton8.Core` | `Assets/_Project/Tests/PlayMode/Hecton8.PlayModeTests.asmdef` |
| `Hecton8.Plugins` | `Hecton8.Core` | `Assets/_Project/Scripts/Plugins/Hecton8.Plugins.asmdef` |
| `Hecton8.Power.Generators` | `Hecton8.Core` | `Assets/_Project/Scripts/Power/Generators/Hecton8.Power.Generators.asmdef` |
| `Hecton8.Prologue.Space` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Prologue/Space/Hecton8.Prologue.Space.asmdef` |
| `Hecton8.Prologue.VFX` | `Hecton8.Core.Contracts`, `Hecton8.Core` | `Assets/_Project/Scripts/Prologue/VFX/Hecton8.Prologue.VFX.asmdef` |
| `Hecton8.QA` | `Hecton8.Core` | `Assets/_Project/Scripts/QA/Hecton8.QA.asmdef` |
| `Hecton8.QA.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/QA/Editor/Hecton8.QA.Editor.asmdef` |
| `Hecton8.QA.Headless` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/QA/Headless/Hecton8.QA.Headless.asmdef` |
| `Hecton8.QA.Headless.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/QA/Headless/Editor/Hecton8.QA.Headless.Editor.asmdef` |
| `Hecton8.Thermodynamics` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/Thermodynamics/Hecton8.Thermodynamics.asmdef` |
| `Hecton8.UI.Diegetic` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/UI/Diegetic/Hecton8.UI.Diegetic.asmdef` |
| `Hecton8.UI.Editor` | `Hecton8.Core` | `Assets/_Project/Scripts/UI/Editor/Hecton8.UI.Editor.asmdef` |
| `Hecton8.UI.Localization` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/UI/Localization/Hecton8.UI.Localization.asmdef` |
| `Hecton8.UI.Navigation` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/UI/Navigation/Hecton8.UI.Navigation.asmdef` |
| `Hecton8.UI.Tools` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/UI/Tools/Hecton8.UI.Tools.asmdef` |
| `Hecton8.UI.VR` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/UI/VR/Hecton8.UI.VR.asmdef` |
| `Hecton8.VFX.Debris` | `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/VFX/Debris/Hecton8.VFX.Debris.asmdef` |
| `Hecton8.VFX.Materials` | `Hecton8.Core.Contracts`, `Hecton8.Core` | `Assets/_Project/Scripts/VFX/Materials/Hecton8.VFX.Materials.asmdef` |
| `Hecton8.VFX.Sonar` | `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/VFX/Sonar/Hecton8.VFX.Sonar.asmdef` |
| `Hecton8.Vehicles.VFX` | `Hecton8.Core.Contracts`, `Hecton8.Core` | `Assets/_Project/Scripts/Vehicles/VFX/Hecton8.Vehicles.VFX.asmdef` |
| `Hecton8.World.Economy` | `Hecton8.Core` | `Assets/_Project/Scripts/World/Resources/Hecton8.World.Economy.asmdef` |
| `Hecton8.World.Outposts` | `Hecton8.Core`, `Hecton8.Core.Contracts` | `Assets/_Project/Scripts/World/Outposts/Hecton8.World.Outposts.asmdef` |
| `Hecton8.World.Streaming` | `Hecton8.Core` | `Assets/_Project/Scripts/World/Streaming/Hecton8.World.Streaming.asmdef` |

### Domain Namespace Edges

Static `using Hecton8.*` edges from first-party source. This exposes compile-time namespace pressure, not runtime coupling proof.

| From domain | To domain | Using count |
|---|---|---:|
| `RootScripts` | `Core` | 211 |
| `Gameplay` | `Core` | 110 |
| `World` | `Core` | 110 |
| `UI` | `Core` | 98 |
| `RootScripts` | `World` | 87 |
| `RootScripts` | `Gameplay` | 66 |
| `Editor` | `World` | 57 |
| `Gameplay` | `World` | 41 |
| `RootScripts` | `Items` | 39 |
| `RootScripts` | `Bootstrap` | 37 |
| `RootScripts` | `Physics` | 33 |
| `RootScripts` | `Environment` | 32 |
| `RootScripts` | `Inventory` | 31 |
| `Construction` | `Core` | 31 |
| `Visor` | `Core` | 30 |
| `World` | `Gameplay` | 30 |
| `UI` | `Gameplay` | 29 |
| `World` | `Environment` | 29 |
| `Gameplay` | `Physics` | 27 |
| `RootScripts` | `SaveSystem` | 25 |
| `Gameplay` | `Audio` | 24 |
| `UI` | `World` | 24 |
| `RootScripts` | `Interaction` | 23 |
| `Editor` | `Core` | 23 |
| `Fauna` | `Core` | 23 |
| `RootScripts` | `Building` | 21 |
| `Construction` | `Gameplay` | 21 |
| `RootScripts` | `UI` | 20 |
| `RootScripts` | `Tools` | 20 |
| `RootScripts` | `Construction` | 19 |
| `RootScripts` | `Caves` | 18 |
| `Core` | `World` | 18 |
| `Gameplay` | `Interaction` | 18 |
| `UI` | `Bootstrap` | 18 |
| `RootScripts` | `Audio` | 17 |
| `Construction` | `World` | 17 |
| `RootScripts` | `AI` | 17 |
| `Interaction` | `Core` | 16 |
| `Audio` | `Core` | 15 |
| `Construction` | `Power` | 15 |
| `Editor` | `Gameplay` | 15 |
| `Optimization` | `Core` | 15 |
| `RootScripts` | `Atmosphere` | 14 |
| `RootScripts` | `Input` | 14 |
| `Plugins` | `Core` | 14 |
| `Visor` | `Gameplay` | 14 |
| `Bootstrap` | `Core` | 13 |
| `Editor` | `Items` | 13 |
| `Fauna` | `World` | 13 |
| `Gameplay` | `Inventory` | 12 |
| `Gameplay` | `UI` | 12 |
| `World` | `Caves` | 12 |
| `Construction` | `Items` | 11 |
| `UI` | `Audio` | 11 |
| `World` | `AI` | 11 |
| `Core` | `Gameplay` | 10 |
| `Core` | `Audio` | 10 |
| `Editor` | `Environment` | 10 |
| `Gameplay` | `Items` | 10 |
| `Gameplay` | `Bootstrap` | 10 |
| `Interaction` | `World` | 10 |
| `UI` | `Input` | 10 |
| `VFX` | `Core` | 10 |
| `World` | `Bootstrap` | 10 |
| `Atmosphere` | `Core` | 9 |
| `Dev` | `Core` | 9 |
| `RootScripts` | `Modding` | 9 |
| `Gameplay` | `Tools` | 9 |
| `RootScripts` | `Optimization` | 9 |
| `ModdingAPI` | `Core` | 9 |
| `Power` | `Core` | 9 |
| `Construction` | `Building` | 8 |
| `Construction` | `SaveSystem` | 8 |
| `Construction` | `Inventory` | 8 |
| `Core` | `Physics` | 8 |
| `Editor` | `Dev` | 8 |
| `Gameplay` | `SaveSystem` | 8 |
| `RootScripts` | `Narrative` | 8 |
| `Narrative` | `Core` | 8 |
| `Plugins` | `World` | 8 |

## SignalBus<T> Flow Map

`ISignal` structs declared: 144. `SignalBus<T>` lanes observed in producer/consumer calls: 85. Union listed below: 147 signals.
Legacy `GlobalSignals.Publish(...)` call sites found: 228. Many use local variables and are not type-resolved by this static pass; they remain a migration/integrator risk.

| Signal | Declared at | Producers (`SignalBus<T>.Push/Publish`) | Consumers (`GetFrameSnapshot`) |
|---|---|---|---|
| `AcousticPingSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4294` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2250` | `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5334` |
| `AnomalySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4066` | none found | none found |
| `AtmosphericReentrySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4715` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2532` | `Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:203`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:184`<br>`Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:296` |
| `AupPreShiftSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3764` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2047` | none found |
| `AupShiftSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3774` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2056` | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1039`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:3038`<br>`Assets/_Project/Scripts/HectonFluidEngine.cs:3266`<br>`Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:508`<br>`Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:467`<br>`Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:1223`<br>`Assets/_Project/Scripts/Visor/SpectrumSystem.cs:2757`<br>`Assets/_Project/Scripts/World/AbyssalThermalManager.cs:2263`<br>`Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs:446`<br>`Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:555`<br>`Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:851`<br>`Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3258` |
| `BaseModuleCompromisedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5312` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2024` | none found |
| `BatteryLevelSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4876` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2459` | none found |
| `BiomeChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3863` | none found | `Assets/_Project/Scripts/Graphics/Materials/ProceduralFloraBiomeTintBridge.cs:70`<br>`Assets/_Project/Scripts/PlayerInventory.cs:4144` |
| `BiomeGradientSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3874` | `Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs:370` | `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:1177`<br>`Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs:722`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:1911` |
| `BlueprintUnlockedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4566` | none found | none found |
| `BrownoutSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3981` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2078`<br>`Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:470` | none found |
| `BulletTimeVisualSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5099` | none found | none found |
| `CameraFrustumSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5255` | none found | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1021` |
| `CameraPositionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5245` | none found | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1008` |
| `ChunkDehydratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4374` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3670` | none found |
| `CombatDamageSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5268` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1955`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:1962`<br>`Assets/_Project/Scripts/Core/PlayerRuntimeContext.cs:264` | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:995`<br>`Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:682`<br>`Assets/_Project/Scripts/Vehicles/VFX/HullDentShaderController.cs:165` |
| `CombatDamageSignalAupShiftTransformer` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5387` | none found | none found |
| `ComplianceViolationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5038` | none found | none found |
| `ControlSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4053` | none found | none found |
| `CoreCombatDamageSignal` | not found as local `ISignal` declaration | none found | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:1239` |
| `CpuStarvationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4281` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2240` | none found |
| `CraftingCompletedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4590` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2394` | `Assets/_Project/Scripts/PDA/PDALogbookManager.cs:616` |
| `CraftingStartedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4578` | none found | none found |
| `CrashTelemetrySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4090` | none found | none found |
| `CrushWarningSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4170` | none found | none found |
| `CullingOverloadSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3794` | `Assets/_Project/Scripts/Core/InstanceCullingServiceRegistryBridge.cs:75` | none found |
| `DamageSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3464` | none found | none found |
| `DataReloadSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4208` | none found | none found |
| `DebrisSpawnSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3994` | none found | none found |
| `DeconstructRequestSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4116` | none found | none found |
| `DeconstructResultSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4131` | none found | none found |
| `DeflectSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4007` | none found | none found |
| `DiegeticHudSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4786` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2445` | none found |
| `DockingCompleteSignal` | `Assets/_Project/Scripts/Vehicles/Automation/DroneDockingSignals.cs:31` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:1767` | none found |
| `DockingFailedSignal` | `Assets/_Project/Scripts/Vehicles/Automation/DroneDockingSignals.cs:45` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:1794`<br>`Assets/_Project/Scripts/Construction/DroneFleetManager.cs:1811` | none found |
| `DockingRequestSignal` | `Assets/_Project/Scripts/Vehicles/Automation/DroneDockingSignals.cs:17` | none found | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:1471` |
| `DropPodLandedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3784` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2063` | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:527` |
| `EntityDeathSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4021` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2100` | `Assets/_Project/Scripts/World/EcosystemDirector.cs:4838` |
| `FaunaStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5195` | none found | none found |
| `FluidDensityChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4479` | none found | none found |
| `FluidImpulseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3817` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2787` | `Assets/_Project/Scripts/HectonFluidEngine.cs:3291` |
| `FluidIncursionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4447` | none found | none found |
| `FocusBrokenSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3919` | none found | none found |
| `FrameTimeSignal` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:32` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:829` | `Assets/_Project/Scripts/Graphics/DRS/ThermalDynamicResolutionAdapter.cs:307` |
| `GlobalTimeSyncSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5050` | none found | none found |
| `GlobalWorldStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3842` | none found | none found |
| `HUDNotificationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4774` | none found | none found |
| `HabitatConstructionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4104` | none found | none found |
| `HapticRequest` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3533` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1989` | none found |
| `HighSpeedImpactSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3495` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1982` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:232`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:3027`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1737`<br>`Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:1400` |
| `HullDeformedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5289` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2018` | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:1226`<br>`Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:1383` |
| `HypoxiaSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4400` | none found | none found |
| `ImpactSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3478` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1975` | `Assets/_Project/Scripts/World/SoundscapeSystem.cs:738` |
| `InputStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:82` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:524` | `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:452`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1381`<br>`Assets/_Project/Scripts/UI/InteractionUI.cs:889`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5678` |
| `InteractionUiSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4424` | none found | none found |
| `InventoryChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3666` | `Assets/_Project/Scripts/PlayerInventory.cs:3996` | `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:669`<br>`Assets/_Project/Scripts/PDAInventoryTab.cs:449`<br>`Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs:205`<br>`Assets/_Project/Scripts/UI/PDAConstructionTab.cs:319`<br>`Assets/_Project/Scripts/UI/PDALoadoutTab.cs:310`<br>`Assets/_Project/Scripts/UI/PDAShellChrome.cs:366`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5656` |
| `InventoryCommandSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3655` | `Assets/_Project/Scripts/PlayerInventory.cs:2016` | `Assets/_Project/Scripts/PlayerInventory.cs:2084` |
| `ItemAcquiredSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3695` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2702`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:2720` | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:622`<br>`Assets/_Project/Scripts/PlayerInventory.cs:4158`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:4849` |
| `ItemDecaySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5122` | none found | none found |
| `ItemDurabilityChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3676` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2466` | `Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:187` |
| `KillSwitchSignal` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:46` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:844` | none found |
| `LightLevelSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5144` | none found | none found |
| `LoreFragmentScannedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4556` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2366` | none found |
| `MacroDatabaseSectorHydrationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4912` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2567` | `Assets/_Project/Scripts/SaveManager.cs:1272`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:3607`<br>`Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:540` |
| `ManualOverridePulledSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4751` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2546` | none found |
| `MemoryAddressShiftSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4231` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2219` | none found |
| `MemoryPressureSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4219` | none found | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:666`<br>`Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3395` |
| `MixerStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3932` | none found | none found |
| `ModuleDeconstructSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4144` | none found | none found |
| `MovementAcousticSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4318` | none found | `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5308` |
| `NarrativeFocusSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3898` | none found | none found |
| `NarrativeHudWaypointSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3946` | none found | none found |
| `NarrativePoiStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3969` | none found | none found |
| `OxygenCriticalSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4412` | none found | none found |
| `PdaExchangeStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4822` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2501` | `Assets/_Project/Scripts/UI/PDABarterTab.cs:191` |
| `PhysiologyStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5208` | none found | none found |
| `PipeRuptureSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4492` | none found | none found |
| `PlayerActionCancelledSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3634` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2487` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:141` |
| `PlayerActionCompletedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3618` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2480` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:137` |
| `PlayerActionProgressSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3599` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2473` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:133` |
| `PlayerBaseEnterSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5334` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2031` | none found |
| `PlayerBaseExitSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5348` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2038` | none found |
| `PlayerInputSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:99` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:2168` | none found |
| `PlayerLookTargetSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:118` | `Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:601`<br>`Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:619` | `Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:439` |
| `PlayerStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3552` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1998` | `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1760`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5639` |
| `PlayerStressSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5220` | none found | none found |
| `PowerDrainSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4658` | none found | none found |
| `ProgressionEventSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3830` | none found | none found |
| `PrologueCompleteSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4734` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2539` | `Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:243`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:214`<br>`Assets/_Project/Scripts/HectonFluidEngine.cs:2910`<br>`Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:346` |
| `RadiationDoseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3708` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2723` | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:670` |
| `RadiationSourceSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3736` | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:99`<br>`Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:112` | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:646` |
| `RebaseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4043` | none found | none found |
| `ReconDataSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4889` | none found | none found |
| `ReentryVfxStateSignal` | `Assets/_Project/Scripts/Core/Signals/PrologueReentrySignals.cs:10` | `Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:690` | none found |
| `ResidencySectorDehydratedSignal` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3660` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3414`<br>`Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3714` |
| `ResidencySectorHydratedSignal` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3644` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3719` |
| `ResolutionChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4247` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2226` | none found |
| `ResourceDepletionDeltaSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3751` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2726` | none found |
| `RigidbodySleepSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4517` | none found | none found |
| `SaveCompletedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4983` | `Assets/_Project/Scripts/SaveManager.cs:1708` | none found |
| `SaveLifecycleSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4900` | none found | none found |
| `SaveMetadataReadySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5014` | `Assets/_Project/Scripts/SaveThumbnailSystem.cs:957` | none found |
| `SaveRequestSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4970` | `Assets/_Project/Scripts/SaveManager.cs:1114` | none found |
| `SaveStatusSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4996` | `Assets/_Project/Scripts/SaveManager.cs:1682` | none found |
| `ScanCompleteSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4544` | none found | none found |
| `ScanLogChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4802` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2494` | `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:687`<br>`Assets/_Project/Scripts/PDA/PDALogbookManager.cs:638` |
| `ScannerToolActiveSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4528` | none found | `Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs:423` |
| `SectorDehydratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4359` | `Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:566` | `Assets/_Project/Scripts/World/EcosystemDirector.cs:3587` |
| `SectorResidencyHydratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4344` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:419`<br>`Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:536` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:259`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:3603` |
| `SeismicSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5061` | none found | none found |
| `SimulationPauseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5087` | none found | none found |
| `SolarFlareSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4032` | none found | none found |
| `SonarPingSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4389` | none found | none found |
| `SoundscapeProfileSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3958` | none found | none found |
| `SpectrumScanSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4505` | none found | none found |
| `StorageDebtSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4684` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2518` | none found |
| `StreamingTurbulenceSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4703` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2525` | none found |
| `SubmarineFloodStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4458` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2313` | none found |
| `SubmarineLightsChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5169` | none found | none found |
| `SubtitleSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4184` | none found | none found |
| `SurvivalVitalsChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3585` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2011` | `Assets/_Project/Scripts/UI/SuitAdvisoryController.cs:161`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:4400`<br>`Assets/_Project/Scripts/Visor/VisorHUDController.cs:1322` |
| `SwarmDispersedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4331` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2264`<br>`Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:555` | none found |
| `SystemHealthIndexSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4264` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2233` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3403` |
| `SystemHealthSignal` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:16` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:805` | `Assets/_Project/Scripts/Graphics/DRS/ThermalDynamicResolutionAdapter.cs:322`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5696` |
| `SystemPauseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5375` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2646`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:2653` | none found |
| `TelemetryAnomalySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4078` | none found | none found |
| `TemperatureChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3720` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2070` | none found |
| `ThermalStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4860` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2452` | `Assets/_Project/Scripts/Graphics/DRS/ThermalDynamicResolutionAdapter.cs:348` |
| `TimeDilationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5075` | none found | none found |
| `ToolAcousticSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4644` | none found | none found |
| `ToolLoadoutChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4623` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2410` | `Assets/_Project/Scripts/HUDQuickBar.cs:345`<br>`Assets/_Project/Scripts/PDAInventoryTab.cs:518`<br>`Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs:246`<br>`Assets/_Project/Scripts/UI/PDAConstructionTab.cs:360`<br>`Assets/_Project/Scripts/UI/PDALoadoutTab.cs:351`<br>`Assets/_Project/Scripts/UI/PDAShellChrome.cs:406` |
| `ToolStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4603` | none found | none found |
| `ToolTriggerSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4671` | none found | none found |
| `TraumaSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5232` | none found | none found |
| `UIRescaleRequestSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4435` | none found | none found |
| `VehicleUpgradesChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4843` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2508` | none found |
| `VisorDropletSignal` | `Assets/_Project/Scripts/Core/Signals/PrologueReentrySignals.cs:33` | `Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:662` | `Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs:374` |
| `VisualFlareSignal` | `Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:18` | `Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:440` | none found |
| `VitalWarningSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4157` | none found | none found |
| `VocalWarningSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4196` | none found | none found |
| `VoxelCarveEvent` | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs:60` | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs:1201` | `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:650` |
| `WakeGeneratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3808` | none found | none found |
| `WeatherChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5362` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2687` | none found |
| `WeatherStrengthSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5111` | none found | none found |
| `WfcOutpostDoorPowerSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4954` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2588` | `Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:946` |
| `WfcOutpostGeneratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4924` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2574` | `Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:234` |
| `WfcOutpostStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4941` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2581` | `Assets/_Project/Scripts/SaveManager.cs:1187` |

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
| Texture files scanned | `1645` | `Docs/Reports/VRAM_Budget_Audit.json` |
| Mesh files scanned | `301` | `Docs/Reports/VRAM_Budget_Audit.json` |
| All scanned full-mip BC7 MiB | `1282.47` | `Docs/Reports/VRAM_Budget_Audit.json` |
| Runtime-candidate full-mip BC7 MiB | `1251.242` | `Docs/Reports/VRAM_Budget_Audit.json` |
| First-party production full-mip BC7 MiB | `504.619` | `Docs/Reports/VRAM_Budget_Audit.json` |
| MX350 texture budget MiB | `900.0` | `Docs/Reports/VRAM_Budget_Audit.json` |
| Critical texture pool MiB | `1228.8` | `Docs/Reports/VRAM_Budget_Audit.json` |
| Texture VRAM crime rows | `800` | `Docs/Reports/VRAM_Budget_Audit.json` |
| Mesh redline rows | `293` | `Docs/Reports/VRAM_Budget_Audit.json` |
| First-party large streaming mips off | `50` | `Docs/Reports/VRAM_Budget_Audit.json` |
| All large streaming mips off | `148` | `Docs/Reports/VRAM_Budget_Audit.json` |
| Expected VRAM CI exit code | `2` | `Docs/Reports/VRAM_Budget_Audit.json` |

### Top Non-First-Party Runtime Payload Pressure

| Directory | Count | Full-mip BC7 MiB | VRAM crime rows |
|---|---:|---:|---:|
| `Assets/ScifiFacility/Textures` | 67 | 483.667 | 11 |
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
| `Docs/AgentLogs/AUP_DRIFT_REPORT.md` | 126 | - `Assets/_Project/Scripts/HectonVoxelVolume.cs`: added `double3` overloads for `ApplyPlasmaCutDda` and `ApplyRepairWeldDda`; existing `Vector3` overloads now wrap through `ToDo... |
| `Docs/AgentLogs/LOG_FAUNA_BEHAVIOR_SIMULATOR.md` | 70 | - Source self-review for `TODO`, Dotnet, subprocess, `os.system`, `eval`, `exec`, `random.` -> no matches in `Tools/AI_Sim/FaunaBalanceSim.py`. |
| `Docs/AgentLogs/LOG_NARRATIVE_LORE_WEAVER.md` | 73 | - Sentinel scan returned no generated-hash, todo, or placeholder-token hits in the handoff files. |
| `Docs/AgentLogs/LOG_QUEST_STATE_GRAPH_VALIDATOR.md` | 73 | - Anti-bloat pass still ran on `Tools/QuestStressTest.py`: no TODO/FIXME markers, no Unity runtime writes, no quest runtime source edits, and the only optional dependency is laz... |
| `Docs/AgentLogs/LOG_SOMATIC_COMFORT_ANALYST.md` | 349 | - Executable-source hygiene scan: one `validate_runtime_integration()` definition remains; no `TODO`, `FIXME`, `pass`, `eval`, `exec`, `subprocess`, `shell=True`, `pickle`, or `... |
| `Docs/AgentLogs/LOG_SOMATIC_COMFORT_ANALYST.md` | 433 | - Executable-source hygiene scan: one `validate_runtime_integration()` definition remains; no `TODO`, `FIXME`, `pass`, `eval`, `exec`, `subprocess`, `shell=True`, `pickle`, or `... |
| `Docs/AgentLogs/LOG_TECH_RESEARCHER.md` | 10 | - Active `Docs/AgentLogs` contained six `TODO` / `HACK` / `FIX LATER` text hits. They are text evidence only; most are log statements about scans, not confirmed executable debt. |
| `Docs/AgentLogs/LOG_VAULT_MEMORY_RELOCATOR.md` | 20 | Checked for duplicate compaction helpers, stale `FatalMemoryException.ThrowStaleVaultHandle()` use, live memmove presence, relocation signal bridge, and TODO/HACK/FIXME markers.... |
| `Docs/AgentLogs/Rationale_MACRO_WFC_PERSISTENCE_SYNC.md` | 312 | Rejected Alternatives: Only trusting the old status log; changing `ApplyWfcOutpostFlagsToDoor()` without proving current behavior; publishing a reset mutation. Stale logs are no... |
| `Docs/AgentLogs/Rationale_VISUAL_EXTINCTION_LUT_BAKER.md` | 52 | Hardware Impact: Prevents shader-side red compensation and color-grade hacks that would cost runtime and break deep-sea noir. |

## PHI Self-Audit

Exact PHI_SYN rationale file is absent from active logs. Near-match H-Phi rationale files were scanned as supporting evidence, but this is not treated as the exact requested artifact.

Near-match active logs:
- `Docs/AgentLogs/Build_HPHI_SYNAPTIC_FORGER_AssemblyCSharp_latest.txt`
- `Docs/AgentLogs/Build_HPHI_SYNAPTIC_FORGER_latest.txt`
- `Docs/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_DuplicateZero.json`
- `Docs/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_FinalBudgetPass.json`
- `Docs/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_FinalBudgetPass2.json`
- `Docs/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_PostPatchBudgetPass.json`
- `Docs/AgentLogs/HphiUiUpdateDeletion_UX_HPHI_SIGNAL_WIRING.md`
- `Docs/AgentLogs/LOG_AUDIO_MATERIAL_SYNTHESIZER.md`
- `Docs/AgentLogs/LOG_GIT_SYNC.md`
- `Docs/AgentLogs/LOG_HECTON_PHI_MONITOR.md`
- `Docs/AgentLogs/LOG_HPHI_SYNAPTIC_FORGER.md`
- `Docs/AgentLogs/LOG_MACRO_WFC_PERSISTENCE_SYNC.md`
- `Docs/AgentLogs/LOG_UX_HPHI_SIGNAL_WIRING.md`
- `Docs/AgentLogs/Rationale_AUDIO_MATERIAL_SYNTHESIZER.md`
- `Docs/AgentLogs/Rationale_GIT_SYNC.md`
- `Docs/AgentLogs/Rationale_HECTON_PHI_MONITOR.md`
- `Docs/AgentLogs/Rationale_HPHI_SYNAPTIC_FORGER.md`
- `Docs/AgentLogs/Rationale_MACRO_WFC_PERSISTENCE_SYNC.md`
- `Docs/AgentLogs/Rationale_NET_SYNC_MERKLE_ARCHITECT.md`
- `Docs/AgentLogs/Rationale_UX_HPHI_SIGNAL_WIRING.md`

| H-Phi / UX signal | Declared at | Producers | Consumers |
|---|---|---|---|
| `PlayerActionProgressSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3599` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2473` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:133` |
| `PlayerActionCompletedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3618` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2480` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:137` |
| `PlayerActionCancelledSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3634` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2487` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:141` |
| `PdaExchangeStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4822` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2501` | `Assets/_Project/Scripts/UI/PDABarterTab.cs:191` |
| `VehicleUpgradesChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4843` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2508` | none found |
| `PlayerStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3552` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1998` | `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1760`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5639` |
| `InventoryChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3666` | `Assets/_Project/Scripts/PlayerInventory.cs:3996` | `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:669`<br>`Assets/_Project/Scripts/PDAInventoryTab.cs:449`<br>`Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs:205`<br>`Assets/_Project/Scripts/UI/PDAConstructionTab.cs:319`<br>`Assets/_Project/Scripts/UI/PDALoadoutTab.cs:310`<br>`Assets/_Project/Scripts/UI/PDAShellChrome.cs:366`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5656` |
| `InputStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:82` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:524` | `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:452`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1381`<br>`Assets/_Project/Scripts/UI/InteractionUI.cs:889`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5678` |
| `SystemHealthSignal` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:16` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:805` | `Assets/_Project/Scripts/Graphics/DRS/ThermalDynamicResolutionAdapter.cs:322`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5696` |

## Phi-Resonance Connectivity Model

The engine connectivity model is not mystical. It is a three-layer resonance model: contracts define stable frequency, typed signal lanes carry bounded pulses, and dispatcher phases control when state can move. `GlobalRegistry` is the cold authority spine for stable interfaces. `SignalBus<T>` and queue-backed event lanes are the nervous system for broadcast state. DataVault/native buffers are the mass: owned memory that simulation mutates and presentation samples.

Low/MX350 interpretation: coalesce lanes, consume bounded snapshots, keep optional visual consumers in `VISUAL_SYNC`, and shed mips/LOD before simulation truth. High/Ultra interpretation: do not increase gameplay broadcast cost; attach richer presentation consumers to the same snapshots and spend the saved budget on light, fog, HUD, audio, and material overkill.

Connectivity risk: `Hecton8.Core` currently references many domain assemblies directly. That may be intentional current architecture, but it is not a clean inward-only core. The integrator should treat this as a dependency inversion watchpoint, especially while many agents expand contracts in parallel.

## Verification Commands

- `python Tools/BuildArchitectureAtlas.py`
- `python Tools/AtlasCheck.py`
- `python -m py_compile Tools/BuildArchitectureAtlas.py Tools/AtlasCheck.py`
- `rg --files | rg "\.sln$|\.csproj$"` returned no project files during this pass, so C# compile verification is not available from current root state.

## Residual Risk

- STATIC_SOURCE only: Unity import, runtime wiring, actual VRAM residency, profiler frame time, GC, build, and Play Mode remain PENDING VERIFICATION.
- The signal producer map only resolves explicit `SignalBus<T>` calls. Legacy `GlobalSignals.Publish(...)` variable publishes require Roslyn-level dataflow to type-resolve fully.
- Active logs can change while this atlas is being written because the workspace is multi-agent.
