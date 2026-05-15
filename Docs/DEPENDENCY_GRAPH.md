# HECTON-8 Architecture Atlas - Dependency Graph

Generated: 2026-05-15 04:24:52
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
- C# line count scanned under `Assets/` and `Packages/`: 1,694,411
- First-party C# source files under `Assets/_Project/Scripts/`: 1505
- First-party C# line count under `Assets/_Project/Scripts/`: 945,750
- Assembly definitions scanned: 152
- First-party assembly definitions under `Assets/_Project/`: 91
- Markdown docs under `Docs/`: 1415

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
- `Hecton8.World.GPR`
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
- `Hecton8.SpaceEngine098Terrain`
- `Hecton8.Input`
- `Hecton8.Input.Generated`
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
| `Hecton8.Gameplay.Loot` | `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Gameplay/Loot/Hecton8.Gameplay.Loot.asmdef` |
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
| `Hecton8.Prologue.Space` | `Hecton8.Core` | `Assets/_Project/Scripts/Prologue/Space/Hecton8.Prologue.Space.asmdef` |
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
| `Hecton8.UI.VR` | `Hecton8.Core` | `Assets/_Project/Scripts/UI/VR/Hecton8.UI.VR.asmdef` |
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
| `RootScripts` | `Core` | 205 |
| `World` | `Core` | 110 |
| `Gameplay` | `Core` | 105 |
| `UI` | `Core` | 92 |
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
| `Fauna` | `Core` | 24 |
| `Gameplay` | `Audio` | 24 |
| `UI` | `World` | 24 |
| `RootScripts` | `Interaction` | 23 |
| `Editor` | `Core` | 23 |
| `RootScripts` | `Building` | 21 |
| `Construction` | `Gameplay` | 21 |
| `RootScripts` | `UI` | 20 |
| `RootScripts` | `Tools` | 20 |
| `UI` | `Bootstrap` | 20 |
| `RootScripts` | `Construction` | 19 |
| `RootScripts` | `Caves` | 18 |
| `Core` | `World` | 18 |
| `Gameplay` | `Interaction` | 18 |
| `RootScripts` | `Audio` | 17 |
| `Construction` | `World` | 17 |
| `RootScripts` | `AI` | 17 |
| `Interaction` | `Core` | 16 |
| `Construction` | `Power` | 15 |
| `Editor` | `Gameplay` | 15 |
| `Optimization` | `Core` | 15 |
| `RootScripts` | `Atmosphere` | 14 |
| `Audio` | `Core` | 14 |
| `RootScripts` | `Input` | 14 |
| `Fauna` | `World` | 14 |
| `Plugins` | `Core` | 14 |
| `Visor` | `Gameplay` | 14 |
| `Bootstrap` | `Core` | 13 |
| `Editor` | `Items` | 13 |
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
| `Narrative` | `Core` | 8 |
| `Plugins` | `World` | 8 |
| `QA` | `Core` | 8 |
| `UI` | `Tools` | 8 |

## SignalBus<T> Flow Map

`ISignal` structs declared: 139. `SignalBus<T>` lanes observed in producer/consumer calls: 79. Union listed below: 142 signals.
Legacy `GlobalSignals.Publish(...)` call sites found: 223. Many use local variables and are not type-resolved by this static pass; they remain a migration/integrator risk.

| Signal | Declared at | Producers (`SignalBus<T>.Push/Publish`) | Consumers (`GetFrameSnapshot`) |
|---|---|---|---|
| `AcousticPingSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4134` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2172` | `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5228` |
| `AnomalySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3906` | none found | none found |
| `AtmosphericReentrySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4533` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2433` | `Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:156`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:161`<br>`Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:269` |
| `AupPreShiftSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3604` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1969` | none found |
| `AupShiftSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3614` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1978` | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1039`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:2932`<br>`Assets/_Project/Scripts/HectonFluidEngine.cs:3207`<br>`Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:468`<br>`Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:364`<br>`Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:739`<br>`Assets/_Project/Scripts/Visor/SpectrumSystem.cs:2757`<br>`Assets/_Project/Scripts/World/AbyssalThermalManager.cs:2263`<br>`Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs:443`<br>`Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:438`<br>`Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:817`<br>`Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3258` |
| `BaseModuleCompromisedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5108` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1960` | none found |
| `BatteryLevelSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4672` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2367` | none found |
| `BiomeChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3703` | none found | `Assets/_Project/Scripts/Graphics/Materials/ProceduralFloraBiomeTintBridge.cs:70`<br>`Assets/_Project/Scripts/PlayerInventory.cs:4141` |
| `BiomeGradientSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3714` | `Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs:370` | `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs:1058`<br>`Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs:722`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:1911` |
| `BlueprintUnlockedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4406` | none found | none found |
| `BrownoutSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3821` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2000` | none found |
| `BulletTimeVisualSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4895` | none found | none found |
| `CameraFrustumSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5051` | none found | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1021` |
| `CameraPositionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5041` | none found | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:1008` |
| `ChunkDehydratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4214` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3670` | none found |
| `CombatDamageSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5064` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1904`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:1911`<br>`Assets/_Project/Scripts/Core/PlayerRuntimeContext.cs:264` | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:995`<br>`Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:682`<br>`Assets/_Project/Scripts/Vehicles/VFX/HullDentShaderController.cs:165` |
| `CombatDamageSignalAupShiftTransformer` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5154` | none found | none found |
| `ComplianceViolationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4834` | none found | none found |
| `ControlSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3893` | none found | none found |
| `CoreCombatDamageSignal` | not found as local `ISignal` declaration | none found | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:1086` |
| `CpuStarvationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4121` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2162` | none found |
| `CraftingCompletedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4430` | none found | none found |
| `CraftingStartedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4418` | none found | none found |
| `CrashTelemetrySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3930` | none found | none found |
| `CrushWarningSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4010` | none found | none found |
| `CullingOverloadSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3634` | `Assets/_Project/Scripts/Core/InstanceCullingServiceRegistryBridge.cs:75` | none found |
| `DamageSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3332` | none found | none found |
| `DataReloadSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4048` | none found | none found |
| `DebrisSpawnSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3834` | none found | none found |
| `DeconstructRequestSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3956` | none found | none found |
| `DeconstructResultSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3971` | none found | none found |
| `DeflectSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3847` | none found | none found |
| `DiegeticHudSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4604` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2353` | none found |
| `DockingCompleteSignal` | `Assets/_Project/Scripts/Vehicles/Automation/DroneDockingSignals.cs:31` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:1767` | none found |
| `DockingFailedSignal` | `Assets/_Project/Scripts/Vehicles/Automation/DroneDockingSignals.cs:45` | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:1794`<br>`Assets/_Project/Scripts/Construction/DroneFleetManager.cs:1811` | none found |
| `DockingRequestSignal` | `Assets/_Project/Scripts/Vehicles/Automation/DroneDockingSignals.cs:17` | none found | `Assets/_Project/Scripts/Construction/DroneFleetManager.cs:1471` |
| `DropPodLandedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3624` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1985` | `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs:517` |
| `EntityDeathSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3861` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2022` | `Assets/_Project/Scripts/World/EcosystemDirector.cs:4838` |
| `FaunaStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4991` | none found | none found |
| `FluidDensityChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4319` | none found | none found |
| `FluidImpulseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3657` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2688` | `Assets/_Project/Scripts/HectonFluidEngine.cs:3232` |
| `FluidIncursionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4287` | none found | none found |
| `FocusBrokenSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3759` | none found | none found |
| `FrameTimeSignal` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:32` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:829` | `Assets/_Project/Scripts/Graphics/DRS/ThermalDynamicResolutionAdapter.cs:302` |
| `GlobalTimeSyncSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4846` | none found | none found |
| `GlobalWorldStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3682` | none found | none found |
| `HUDNotificationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4592` | none found | none found |
| `HabitatConstructionSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3944` | none found | none found |
| `HapticRequest` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3401` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1938` | none found |
| `HighSpeedImpactSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3363` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1931` | `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs:159`<br>`Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:2945`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1613`<br>`Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:1400` |
| `HullDeformedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5085` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1954` | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:1073`<br>`Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:1383` |
| `HypoxiaSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4240` | none found | none found |
| `ImpactSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3346` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1924` | `Assets/_Project/Scripts/World/SoundscapeSystem.cs:738` |
| `InputStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:80` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:524` | `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:452`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1312`<br>`Assets/_Project/Scripts/UI/InteractionUI.cs:889`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5646` |
| `InteractionUiSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4264` | none found | none found |
| `InventoryChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3506` | `Assets/_Project/Scripts/PlayerInventory.cs:3993` | `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5624` |
| `InventoryCommandSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3495` | `Assets/_Project/Scripts/PlayerInventory.cs:2016` | `Assets/_Project/Scripts/PlayerInventory.cs:2084` |
| `ItemAcquiredSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3535` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2603`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:2621` | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:622`<br>`Assets/_Project/Scripts/PlayerInventory.cs:4155`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:4849` |
| `ItemDecaySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4918` | none found | none found |
| `ItemDurabilityChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3516` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2374` | `Assets/_Project/Scripts/VFX/Materials/MaterialDecayRuntime.cs:164` |
| `KillSwitchSignal` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:46` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:844` | none found |
| `LightLevelSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4940` | none found | none found |
| `LoreFragmentScannedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4396` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2288` | none found |
| `ManualOverridePulledSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4569` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2447` | none found |
| `MemoryAddressShiftSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4071` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2141` | none found |
| `MemoryPressureSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4059` | none found | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3395` |
| `MixerStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3772` | none found | none found |
| `ModuleDeconstructSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3984` | none found | none found |
| `MovementAcousticSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4158` | none found | `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:5202` |
| `NarrativeFocusSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3738` | none found | none found |
| `NarrativeHudWaypointSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3786` | none found | none found |
| `NarrativePoiStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3809` | none found | none found |
| `OxygenCriticalSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4252` | none found | none found |
| `PdaExchangeStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4620` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2402` | `Assets/_Project/Scripts/UI/PDABarterTab.cs:191` |
| `PhysiologyStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5004` | none found | none found |
| `PipeRuptureSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4332` | none found | none found |
| `PlayerActionCancelledSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3474` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2395` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:141` |
| `PlayerActionCompletedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3458` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2388` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:137` |
| `PlayerActionProgressSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3439` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2381` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:133` |
| `PlayerInputSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:97` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:2168` | none found |
| `PlayerLookTargetSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:116` | `Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:601`<br>`Assets/_Project/Scripts/Interaction/PlayerInteraction.cs:619` | `Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:336` |
| `PlayerStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3420` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1947` | `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1636`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5607` |
| `PlayerStressSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5016` | none found | none found |
| `PowerDrainSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4476` | none found | none found |
| `ProgressionEventSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3670` | none found | none found |
| `PrologueCompleteSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4552` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2440` | `Assets/_Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs:196`<br>`Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:188`<br>`Assets/_Project/Scripts/HectonFluidEngine.cs:2857`<br>`Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:303` |
| `RadiationDoseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3548` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2624` | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:670` |
| `RadiationSourceSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3576` | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:99`<br>`Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:112` | `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs:646` |
| `RebaseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3883` | none found | none found |
| `ReconDataSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4685` | none found | none found |
| `ReentryVfxStateSignal` | `Assets/_Project/Scripts/Core/Signals/PrologueReentrySignals.cs:10` | `Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:618` | none found |
| `ResidencySectorDehydratedSignal` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3660` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3414`<br>`Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3714` |
| `ResidencySectorHydratedSignal` | not found as local `ISignal` declaration | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3644` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3719` |
| `ResolutionChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4087` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2148` | none found |
| `ResourceDepletionDeltaSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3591` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2627` | none found |
| `RigidbodySleepSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4357` | none found | none found |
| `SaveCompletedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4779` | `Assets/_Project/Scripts/SaveManager.cs:1503` | none found |
| `SaveLifecycleSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4696` | none found | none found |
| `SaveMetadataReadySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4810` | `Assets/_Project/Scripts/SaveThumbnailSystem.cs:957` | none found |
| `SaveRequestSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4766` | `Assets/_Project/Scripts/SaveManager.cs:1028` | none found |
| `SaveStatusSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4792` | `Assets/_Project/Scripts/SaveManager.cs:1477` | none found |
| `ScanCompleteSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4384` | none found | none found |
| `ScannerToolActiveSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4368` | none found | `Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs:420` |
| `SectorDehydratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4199` | `Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:461` | `Assets/_Project/Scripts/World/EcosystemDirector.cs:3587` |
| `SectorHydratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4708` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2468` | `Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:327`<br>`Assets/_Project/Scripts/SaveManager.cs:1118`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:3607`<br>`Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:424` |
| `SectorResidencyHydratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4184` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:385`<br>`Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs:432` | `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs:230`<br>`Assets/_Project/Scripts/World/EcosystemDirector.cs:3603` |
| `SeismicSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4857` | none found | none found |
| `SimulationPauseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4883` | none found | none found |
| `SolarFlareSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3872` | none found | none found |
| `SonarPingSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4229` | none found | none found |
| `SoundscapeProfileSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3798` | none found | none found |
| `SpectrumScanSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4345` | none found | none found |
| `StorageDebtSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4502` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2419` | none found |
| `StreamingTurbulenceSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4521` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2426` | none found |
| `SubmarineFloodStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4298` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2235` | none found |
| `SubmarineLightsChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4965` | none found | none found |
| `SubtitleSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4024` | none found | none found |
| `SwarmDispersedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4171` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2186` | none found |
| `SystemHealthIndexSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4104` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2155` | `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs:3403` |
| `SystemHealthSignal` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:16` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:805` | `Assets/_Project/Scripts/Graphics/DRS/ThermalDynamicResolutionAdapter.cs:317`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5664` |
| `SystemPauseSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5143` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2547`<br>`Assets/_Project/Scripts/Core/GlobalSignals.cs:2554` | none found |
| `TelemetryAnomalySignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3918` | none found | none found |
| `TemperatureChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3560` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1992` | none found |
| `ThermalStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4656` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2360` | `Assets/_Project/Scripts/Graphics/DRS/ThermalDynamicResolutionAdapter.cs:343` |
| `TimeDilationSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4871` | none found | none found |
| `ToolAcousticSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4462` | none found | none found |
| `ToolStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4442` | none found | none found |
| `ToolTriggerSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4489` | none found | none found |
| `TraumaSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5028` | none found | none found |
| `UIRescaleRequestSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4275` | none found | none found |
| `VehicleUpgradesChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4639` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2409` | none found |
| `VisorDropletSignal` | `Assets/_Project/Scripts/Core/Signals/PrologueReentrySignals.cs:32` | `Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs:592` | `Assets/_Project/Scripts/Visor/InternalFloodWaterlineRuntime.cs:378` |
| `VisualFlareSignal` | `Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:18` | `Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs:440` | none found |
| `VitalWarningSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3997` | none found | none found |
| `VocalWarningSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4036` | none found | none found |
| `VoxelCarveEvent` | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs:59` | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs:1072` | `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs:377` |
| `WakeGeneratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3648` | none found | none found |
| `WeatherChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:5130` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2588` | none found |
| `WeatherStrengthSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4907` | none found | none found |
| `WfcOutpostDoorPowerSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4750` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2489` | `Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs:678` |
| `WfcOutpostGeneratedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4720` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2475` | `Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs:227` |
| `WfcOutpostStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4737` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2482` | `Assets/_Project/Scripts/SaveManager.cs:1092` |

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
| Texture files scanned | `1644` | `Docs/Reports/VRAM_Budget_Audit.json` |
| Mesh files scanned | `301` | `Docs/Reports/VRAM_Budget_Audit.json` |
| All scanned full-mip BC7 MiB | `1281.372` | `Docs/Reports/VRAM_Budget_Audit.json` |
| Runtime-candidate full-mip BC7 MiB | `1250.143` | `Docs/Reports/VRAM_Budget_Audit.json` |
| First-party production full-mip BC7 MiB | `503.521` | `Docs/Reports/VRAM_Budget_Audit.json` |
| MX350 texture budget MiB | `900.0` | `Docs/Reports/VRAM_Budget_Audit.json` |
| Critical texture pool MiB | `1228.8` | `Docs/Reports/VRAM_Budget_Audit.json` |
| Texture VRAM crime rows | `800` | `Docs/Reports/VRAM_Budget_Audit.json` |
| Mesh redline rows | `1` | `Docs/Reports/VRAM_Budget_Audit.json` |
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
| `Assets/Feel/MMTools/Demos/MMGhostCamera/Models/MMGhostCameraCity.fbx` | 127645 | `MESH_GT_80K_ABSOLUTE_STATIC, MESH_REDLINE_GT_50K_NO_LOD` |

## SHERST Wall Of Shame

Pattern scan: active `Docs/AgentLogs/` only; terms: `TODO`, `HACK`, `FIX LATER`. These are text hits, not proof of executable debt.

| File | Line | Text |
|---|---:|---|
| `Docs/AgentLogs/LOG_FAUNA_BEHAVIOR_SIMULATOR.md` | 70 | - Source self-review for `TODO`, Dotnet, subprocess, `os.system`, `eval`, `exec`, `random.` -> no matches in `Tools/AI_Sim/FaunaBalanceSim.py`. |
| `Docs/AgentLogs/LOG_NARRATIVE_LORE_WEAVER.md` | 73 | - Sentinel scan returned no generated-hash, todo, or placeholder-token hits in the handoff files. |
| `Docs/AgentLogs/LOG_QUEST_STATE_GRAPH_VALIDATOR.md` | 73 | - Anti-bloat pass still ran on `Tools/QuestStressTest.py`: no TODO/FIXME markers, no Unity runtime writes, no quest runtime source edits, and the only optional dependency is laz... |
| `Docs/AgentLogs/LOG_SOMATIC_COMFORT_ANALYST.md` | 349 | - Executable-source hygiene scan: one `validate_runtime_integration()` definition remains; no `TODO`, `FIXME`, `pass`, `eval`, `exec`, `subprocess`, `shell=True`, `pickle`, or `... |
| `Docs/AgentLogs/LOG_SOMATIC_COMFORT_ANALYST.md` | 433 | - Executable-source hygiene scan: one `validate_runtime_integration()` definition remains; no `TODO`, `FIXME`, `pass`, `eval`, `exec`, `subprocess`, `shell=True`, `pickle`, or `... |
| `Docs/AgentLogs/LOG_TECH_RESEARCHER.md` | 10 | - Active `Docs/AgentLogs` contained six `TODO` / `HACK` / `FIX LATER` text hits. They are text evidence only; most are log statements about scans, not confirmed executable debt. |
| `Docs/AgentLogs/LOG_VAULT_MEMORY_RELOCATOR.md` | 20 | Checked for duplicate compaction helpers, stale `FatalMemoryException.ThrowStaleVaultHandle()` use, live memmove presence, relocation signal bridge, and TODO/HACK/FIXME markers.... |
| `Docs/AgentLogs/Rationale_VISUAL_EXTINCTION_LUT_BAKER.md` | 52 | Hardware Impact: Prevents shader-side red compensation and color-grade hacks that would cost runtime and break deep-sea noir. |

## PHI Self-Audit

Exact PHI_SYN rationale file is absent from active logs. Near-match H-Phi rationale files were scanned as supporting evidence, but this is not treated as the exact requested artifact.

Near-match active logs:
- `Docs/AgentLogs/Build_HPHI_SYNAPTIC_FORGER_AssemblyCSharp_latest.txt`
- `Docs/AgentLogs/Build_HPHI_SYNAPTIC_FORGER_latest.txt`
- `Docs/AgentLogs/HphiUiUpdateDeletion_UX_HPHI_SIGNAL_WIRING.md`
- `Docs/AgentLogs/LOG_AUDIO_MATERIAL_SYNTHESIZER.md`
- `Docs/AgentLogs/LOG_HPHI_SYNAPTIC_FORGER.md`
- `Docs/AgentLogs/LOG_MACRO_WFC_PERSISTENCE_SYNC.md`
- `Docs/AgentLogs/LOG_UX_HPHI_SIGNAL_WIRING.md`
- `Docs/AgentLogs/Rationale_AUDIO_MATERIAL_SYNTHESIZER.md`
- `Docs/AgentLogs/Rationale_HPHI_SYNAPTIC_FORGER.md`
- `Docs/AgentLogs/Rationale_MACRO_WFC_PERSISTENCE_SYNC.md`
- `Docs/AgentLogs/Rationale_NET_SYNC_MERKLE_ARCHITECT.md`
- `Docs/AgentLogs/Rationale_UX_HPHI_SIGNAL_WIRING.md`

| H-Phi / UX signal | Declared at | Producers | Consumers |
|---|---|---|---|
| `PlayerActionProgressSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3439` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2381` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:133` |
| `PlayerActionCompletedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3458` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2388` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:137` |
| `PlayerActionCancelledSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3474` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2395` | `Assets/_Project/Scripts/UI/ActionProgressHUD.cs:141` |
| `PdaExchangeStateChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4620` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2402` | `Assets/_Project/Scripts/UI/PDABarterTab.cs:191` |
| `VehicleUpgradesChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:4639` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:2409` | none found |
| `PlayerStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3420` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:1947` | `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1636`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5607` |
| `InventoryChangedSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:3506` | `Assets/_Project/Scripts/PlayerInventory.cs:3993` | `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5624` |
| `InputStateSignal` | `Assets/_Project/Scripts/Core/GlobalSignals.cs:80` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:524` | `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:452`<br>`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs:1312`<br>`Assets/_Project/Scripts/UI/InteractionUI.cs:889`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5646` |
| `SystemHealthSignal` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:16` | `Assets/_Project/Scripts/Core/HomeostasisBrain.cs:805` | `Assets/_Project/Scripts/Graphics/DRS/ThermalDynamicResolutionAdapter.cs:317`<br>`Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:5664` |

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
