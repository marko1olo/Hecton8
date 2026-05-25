Batch012 reentry LOGS summary v3. Files=151. Bytes=4887828. Raw files archived intact. Policy: selected md/txt/log lines are not truncated; json/bin listed intact; markdown decoration removed; no article stripping.

FILE AssemblyDependencyAudit_HFI_AUDIT.json bytes=102541 raw=json archived-intact

FILE AssemblyDependencyAudit_HFI_AUDIT.md bytes=22736 errors=1 warnings=0 successMarkers=4 selected=18
- Evidence class: STATICSOURCE. No Unity import, compile, player build, or runtime proof was executed.
- Required cross-domain route: Hecton8.Core.Contracts
- | Hecton8.Core.Hardware | Hecton8.Bootstrap.Contracts | Assets/Project/Scripts/Core/Hardware/Hecton8.Core.Hardware.asmdef |
- | Hecton8.MockDomain.Authoring | Hecton8.Global.Contracts | Assets/Project/Scripts/Global/MockDomain/Authoring/Hecton8.MockDomain.Authoring.asmdef |
- | Hecton8.MockDomain.Contracts | Hecton8.Global.Contracts | Assets/Project/Scripts/Global/MockDomain/Contracts/Hecton8.MockDomain.Contracts.asmdef |
- | Hecton8.MockDomain.Runtime | Hecton8.Global.Contracts | Assets/Project/Scripts/Global/MockDomain/Runtime/Hecton8.MockDomain.Runtime.asmdef |
- | Hecton8.Graphics.Scalability | Hecton8.Core | Assets/Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef |
- | Hecton8.Graphics.Scalability | Hecton8.Core.Memory | Assets/Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef |
- | Hecton8.Graphics.Scalability | Hecton8.Bootstrap.Contracts | Assets/Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef |
- | Hecton8.Rendering.OceanSinglePass | Hecton8.Core | Assets/Project/Scripts/Rendering/OceanSinglePass/Hecton8.Rendering.OceanSinglePass.asmdef |
- | Hecton8.Rendering.OceanSinglePass | Hecton8.Core.Memory | Assets/Project/Scripts/Rendering/OceanSinglePass/Hecton8.Rendering.OceanSinglePass.asmdef |
- Core Compile-Wall Pressure
- Runtime Concrete Cross-Domain References
- The serialized first-party asmdef graph is treated as a DAG. Any cycle is a hard compile-wall defect under the strict flag.
- Cross-domain runtime references that do not route through Hecton8.Core.Contracts are strict boundary violations under --fail-on-core-contract-boundary.
- Core concrete sibling references are compile-wall pressure. They are not automatically removable; each one needs a contract/facade migration plan plus Unity import proof.
- Runtime concrete cross-domain references are review surfaces. Same-domain and .Contracts references are reported separately from the strict Core.Contracts boundary.
- This audit does not mutate asmdefs and does not claim compile health.

FILE AssemblyDependencyAudit_X_003.json bytes=102541 raw=json archived-intact

FILE AssemblyDependencyAudit_X_003.md bytes=22736 errors=1 warnings=0 successMarkers=4 selected=18
- Evidence class: STATICSOURCE. No Unity import, compile, player build, or runtime proof was executed.
- Required cross-domain route: Hecton8.Core.Contracts
- | Hecton8.Core.Hardware | Hecton8.Bootstrap.Contracts | Assets/Project/Scripts/Core/Hardware/Hecton8.Core.Hardware.asmdef |
- | Hecton8.MockDomain.Authoring | Hecton8.Global.Contracts | Assets/Project/Scripts/Global/MockDomain/Authoring/Hecton8.MockDomain.Authoring.asmdef |
- | Hecton8.MockDomain.Contracts | Hecton8.Global.Contracts | Assets/Project/Scripts/Global/MockDomain/Contracts/Hecton8.MockDomain.Contracts.asmdef |
- | Hecton8.MockDomain.Runtime | Hecton8.Global.Contracts | Assets/Project/Scripts/Global/MockDomain/Runtime/Hecton8.MockDomain.Runtime.asmdef |
- | Hecton8.Graphics.Scalability | Hecton8.Core | Assets/Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef |
- | Hecton8.Graphics.Scalability | Hecton8.Core.Memory | Assets/Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef |
- | Hecton8.Graphics.Scalability | Hecton8.Bootstrap.Contracts | Assets/Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef |
- | Hecton8.Rendering.OceanSinglePass | Hecton8.Core | Assets/Project/Scripts/Rendering/OceanSinglePass/Hecton8.Rendering.OceanSinglePass.asmdef |
- | Hecton8.Rendering.OceanSinglePass | Hecton8.Core.Memory | Assets/Project/Scripts/Rendering/OceanSinglePass/Hecton8.Rendering.OceanSinglePass.asmdef |
- Core Compile-Wall Pressure
- Runtime Concrete Cross-Domain References
- The serialized first-party asmdef graph is treated as a DAG. Any cycle is a hard compile-wall defect under the strict flag.
- Cross-domain runtime references that do not route through Hecton8.Core.Contracts are strict boundary violations under --fail-on-core-contract-boundary.
- Core concrete sibling references are compile-wall pressure. They are not automatically removable; each one needs a contract/facade migration plan plus Unity import proof.
- Runtime concrete cross-domain references are review surfaces. Same-domain and .Contracts references are reported separately from the strict Core.Contracts boundary.
- This audit does not mutate asmdefs and does not claim compile health.

FILE AssemblyDependencyAudit_X_003_cycles.json bytes=113847 raw=json archived-intact

FILE AssemblyDependencyAudit_X_003_cycles.md bytes=23181 errors=1 warnings=0 successMarkers=2 selected=23
- Evidence class: STATICSOURCE. No Unity import, compile, player build, or runtime proof was executed.
- Required cross-domain route: Hecton8.Core.Contracts
- | Hecton8.Core.Hardware | Hecton8.Bootstrap.Contracts | Assets/Project/Scripts/Core/Hardware/Hecton8.Core.Hardware.asmdef |
- | Hecton8.MockDomain.Authoring | Hecton8.Global.Contracts | Assets/Project/Scripts/Global/MockDomain/Authoring/Hecton8.MockDomain.Authoring.asmdef |
- | Hecton8.MockDomain.Contracts | Hecton8.Global.Contracts | Assets/Project/Scripts/Global/MockDomain/Contracts/Hecton8.MockDomain.Contracts.asmdef |
- | Hecton8.MockDomain.Runtime | Hecton8.Global.Contracts | Assets/Project/Scripts/Global/MockDomain/Runtime/Hecton8.MockDomain.Runtime.asmdef |
- | Hecton8.Graphics.Caustics | Hecton8.Bootstrap.Contracts | Assets/Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef |
- | Hecton8.Graphics.Caustics | Hecton8.Core | Assets/Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef |
- | Hecton8.Graphics.Caustics | Hecton8.Core.Memory | Assets/Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef |
- | Hecton8.Graphics.Scalability | Hecton8.Core | Assets/Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef |
- | Hecton8.Graphics.Scalability | Hecton8.Core.Memory | Assets/Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef |
- | Hecton8.Graphics.Scalability | Hecton8.Bootstrap.Contracts | Assets/Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef |
- | Hecton8.Core | Hecton8.Inventory.Algorithms | Assets/Project/Scripts/Hecton8.Core.asmdef |
- Core Compile-Wall Pressure
- | Hecton8.Inventory.Algorithms | Assets/Project/Scripts/Hecton8.Core.asmdef |
- Runtime Concrete Cross-Domain References
- | Hecton8.Rendering.OceanSinglePass | Hecton8.Core | Assets/Project/Scripts/Rendering/OceanSinglePass/Hecton8.Rendering.OceanSinglePass.asmdef |
- | Hecton8.Rendering.OceanSinglePass | Hecton8.Core.Memory | Assets/Project/Scripts/Rendering/OceanSinglePass/Hecton8.Rendering.OceanSinglePass.asmdef |
- The serialized first-party asmdef graph is treated as a DAG. Any cycle is a hard compile-wall defect under the strict flag.
- Cross-domain runtime references that do not route through Hecton8.Core.Contracts are strict boundary violations under --fail-on-core-contract-boundary.
- Core concrete sibling references are compile-wall pressure. They are not automatically removable; each one needs a contract/facade migration plan plus Unity import proof.
- Runtime concrete cross-domain references are review surfaces. Same-domain and .Contracts references are reported separately from the strict Core.Contracts boundary.
- This audit does not mutate asmdefs and does not claim compile health.

FILE AssemblyDependencyAudit_X_003_FULL.md bytes=22947 errors=1 warnings=0 successMarkers=5 selected=22
- Evidence class: STATICSOURCE. No Unity import, compile, player build, or runtime proof was executed.
- Required cross-domain route: Hecton8.Core.Contracts
- | Hecton8.Core.Hardware | Hecton8.Bootstrap.Contracts | Assets/Project/Scripts/Core/Hardware/Hecton8.Core.Hardware.asmdef |
- | Hecton8.MockDomain.Authoring | Hecton8.Global.Contracts | Assets/Project/Scripts/Global/MockDomain/Authoring/Hecton8.MockDomain.Authoring.asmdef |
- | Hecton8.MockDomain.Contracts | Hecton8.Global.Contracts | Assets/Project/Scripts/Global/MockDomain/Contracts/Hecton8.MockDomain.Contracts.asmdef |
- | Hecton8.MockDomain.Runtime | Hecton8.Global.Contracts | Assets/Project/Scripts/Global/MockDomain/Runtime/Hecton8.MockDomain.Runtime.asmdef |
- | Hecton8.Graphics.Caustics | Hecton8.Bootstrap.Contracts | Assets/Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef |
- | Hecton8.Graphics.Caustics | Hecton8.Core | Assets/Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef |
- | Hecton8.Graphics.Caustics | Hecton8.Core.Memory | Assets/Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef |
- | Hecton8.Graphics.Scalability | Hecton8.Core | Assets/Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef |
- | Hecton8.Graphics.Scalability | Hecton8.Core.Memory | Assets/Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef |
- | Hecton8.Graphics.Scalability | Hecton8.Bootstrap.Contracts | Assets/Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef |
- | Hecton8.Rendering.OceanSinglePass | Hecton8.Core | Assets/Project/Scripts/Rendering/OceanSinglePass/Hecton8.Rendering.OceanSinglePass.asmdef |
- | Hecton8.Rendering.OceanSinglePass | Hecton8.Core.Memory | Assets/Project/Scripts/Rendering/OceanSinglePass/Hecton8.Rendering.OceanSinglePass.asmdef |
- | Hecton8.Rendering.OceanSinglePass | Hecton8.Habitat.Deformation.Contracts | Assets/Project/Scripts/Rendering/OceanSinglePass/Hecton8.Rendering.OceanSinglePass.asmdef |
- Core Compile-Wall Pressure
- Runtime Concrete Cross-Domain References
- The serialized first-party asmdef graph is treated as a DAG. Any cycle is a hard compile-wall defect under the strict flag.
- Cross-domain runtime references that do not route through Hecton8.Core.Contracts are strict boundary violations under --fail-on-core-contract-boundary.
- Core concrete sibling references are compile-wall pressure. They are not automatically removable; each one needs a contract/facade migration plan plus Unity import proof.
- Runtime concrete cross-domain references are review surfaces. Same-domain and .Contracts references are reported separately from the strict Core.Contracts boundary.
- This audit does not mutate asmdefs and does not claim compile health.

FILE AssemblyDependencyAudit_X_003_runtime_refs.json bytes=113847 raw=json archived-intact

FILE AssemblyDependencyAudit_X_003_runtime_refs.md bytes=23181 errors=1 warnings=0 successMarkers=2 selected=23
- Evidence class: STATICSOURCE. No Unity import, compile, player build, or runtime proof was executed.
- Required cross-domain route: Hecton8.Core.Contracts
- | Hecton8.Core.Hardware | Hecton8.Bootstrap.Contracts | Assets/Project/Scripts/Core/Hardware/Hecton8.Core.Hardware.asmdef |
- | Hecton8.MockDomain.Authoring | Hecton8.Global.Contracts | Assets/Project/Scripts/Global/MockDomain/Authoring/Hecton8.MockDomain.Authoring.asmdef |
- | Hecton8.MockDomain.Contracts | Hecton8.Global.Contracts | Assets/Project/Scripts/Global/MockDomain/Contracts/Hecton8.MockDomain.Contracts.asmdef |
- | Hecton8.MockDomain.Runtime | Hecton8.Global.Contracts | Assets/Project/Scripts/Global/MockDomain/Runtime/Hecton8.MockDomain.Runtime.asmdef |
- | Hecton8.Graphics.Caustics | Hecton8.Bootstrap.Contracts | Assets/Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef |
- | Hecton8.Graphics.Caustics | Hecton8.Core | Assets/Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef |
- | Hecton8.Graphics.Caustics | Hecton8.Core.Memory | Assets/Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef |
- | Hecton8.Graphics.Scalability | Hecton8.Core | Assets/Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef |
- | Hecton8.Graphics.Scalability | Hecton8.Core.Memory | Assets/Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef |
- | Hecton8.Graphics.Scalability | Hecton8.Bootstrap.Contracts | Assets/Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef |
- | Hecton8.Core | Hecton8.Inventory.Algorithms | Assets/Project/Scripts/Hecton8.Core.asmdef |
- Core Compile-Wall Pressure
- | Hecton8.Inventory.Algorithms | Assets/Project/Scripts/Hecton8.Core.asmdef |
- Runtime Concrete Cross-Domain References
- | Hecton8.Rendering.OceanSinglePass | Hecton8.Core | Assets/Project/Scripts/Rendering/OceanSinglePass/Hecton8.Rendering.OceanSinglePass.asmdef |
- | Hecton8.Rendering.OceanSinglePass | Hecton8.Core.Memory | Assets/Project/Scripts/Rendering/OceanSinglePass/Hecton8.Rendering.OceanSinglePass.asmdef |
- The serialized first-party asmdef graph is treated as a DAG. Any cycle is a hard compile-wall defect under the strict flag.
- Cross-domain runtime references that do not route through Hecton8.Core.Contracts are strict boundary violations under --fail-on-core-contract-boundary.
- Core concrete sibling references are compile-wall pressure. They are not automatically removable; each one needs a contract/facade migration plan plus Unity import proof.
- Runtime concrete cross-domain references are review surfaces. Same-domain and .Contracts references are reported separately from the strict Core.Contracts boundary.
- This audit does not mutate asmdefs and does not claim compile health.

FILE AssemblyDependencyMatrix_X_003.md bytes=23129 errors=1 warnings=0 successMarkers=2 selected=23
- Evidence class: STATICSOURCE. No Unity import, compile, player build, or runtime proof was executed.
- Required cross-domain route: Hecton8.Core.Contracts
- | Hecton8.Core.Hardware | Hecton8.Bootstrap.Contracts | Assets/Project/Scripts/Core/Hardware/Hecton8.Core.Hardware.asmdef |
- | Hecton8.MockDomain.Authoring | Hecton8.Global.Contracts | Assets/Project/Scripts/Global/MockDomain/Authoring/Hecton8.MockDomain.Authoring.asmdef |
- | Hecton8.MockDomain.Contracts | Hecton8.Global.Contracts | Assets/Project/Scripts/Global/MockDomain/Contracts/Hecton8.MockDomain.Contracts.asmdef |
- | Hecton8.MockDomain.Runtime | Hecton8.Global.Contracts | Assets/Project/Scripts/Global/MockDomain/Runtime/Hecton8.MockDomain.Runtime.asmdef |
- | Hecton8.Graphics.Caustics | Hecton8.Bootstrap.Contracts | Assets/Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef |
- | Hecton8.Graphics.Caustics | Hecton8.Core | Assets/Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef |
- | Hecton8.Graphics.Caustics | Hecton8.Core.Memory | Assets/Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef |
- | Hecton8.Graphics.Scalability | Hecton8.Core | Assets/Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef |
- | Hecton8.Graphics.Scalability | Hecton8.Core.Memory | Assets/Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef |
- | Hecton8.Graphics.Scalability | Hecton8.Bootstrap.Contracts | Assets/Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef |
- | Hecton8.Core | Hecton8.Inventory.Algorithms | Assets/Project/Scripts/Hecton8.Core.asmdef |
- Core Compile-Wall Pressure
- | Hecton8.Inventory.Algorithms | Assets/Project/Scripts/Hecton8.Core.asmdef |
- Runtime Concrete Cross-Domain References
- | Hecton8.Rendering.OceanSinglePass | Hecton8.Core | Assets/Project/Scripts/Rendering/OceanSinglePass/Hecton8.Rendering.OceanSinglePass.asmdef |
- | Hecton8.Rendering.OceanSinglePass | Hecton8.Core.Memory | Assets/Project/Scripts/Rendering/OceanSinglePass/Hecton8.Rendering.OceanSinglePass.asmdef |
- The serialized first-party asmdef graph is treated as a DAG. Any cycle is a hard compile-wall defect under the strict flag.
- Cross-domain runtime references that do not route through Hecton8.Core.Contracts are strict boundary violations under --fail-on-core-contract-boundary.
- Core concrete sibling references are compile-wall pressure. They are not automatically removable; each one needs a contract/facade migration plan plus Unity import proof.
- Runtime concrete cross-domain references are review surfaces. Same-domain and .Contracts references are reported separately from the strict Core.Contracts boundary.
- This audit does not mutate asmdefs and does not claim compile health.

FILE AssemblyDependencyUsingAudit_X_003.json bytes=458260 raw=json archived-intact

FILE AssemblyDependencyUsingAudit_X_003.md bytes=458179 errors=3 warnings=3 successMarkers=2 selected=34
- "cachePath": "Docs/AgentLogs/AssemblyDependencyUsingAuditX003.json",
- "evidenceClass": "STATICSOURCETOKENSCANNOTROSLYN",
- "requiredRoute": "Hecton8.Core.Contracts",
- "residualRisk": "Namespace import token scanner; not Roslyn semantic binding. Intended to fail obvious cross-domain using leaks before Unity compile.",
- "schema": "hecton8.usingboundaryaudit.v1",
- "elapsedMs": 5622.605,
- "topAssemblies": [
- "assembly": "Hecton8.Rendering.OceanSinglePass",
- "assembly": "Hecton8.Graphics.Scalability",
- "kind": "CROSSDOMAINUSINGBOUNDARY",
- "message": "Runtime C# source imports a sibling Hecton8 domain instead of the Core.Contracts route.",
- "path": "Assets/Project/Scripts/AcousticZoneController.cs",
- "sourceDomain": "Hecton8.Core",
- "targetDomain": "Hecton8.Atmosphere",
- "using": "Hecton8.Atmosphere"
- "targetDomain": "Hecton8.Bootstrap",
- "using": "Hecton8.Bootstrap"
- "targetDomain": "Hecton8.Environment",
- "using": "Hecton8.Environment"
- "targetDomain": "Hecton8.Gameplay",
- "using": "Hecton8.Gameplay"
- "targetDomain": "Hecton8.Physics",
- "using": "Hecton8.Physics"
- "targetDomain": "Hecton8.Visor",
- "using": "Hecton8.Visor"
- "targetDomain": "Hecton8.World",
- "using": "Hecton8.World"
- "sourceDomain": "Hecton8.AI",
- "targetDomain": "Hecton8.Core",
- "using": "Hecton8.Core"
- "using": "Hecton8.Core.Memory"
- "path": "Assets/Project/Scripts/AI/Cognition/AlphaLeviathanCognitionVault.cs",
- "path": "Assets/Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs",
- "path": "Assets/Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs",

FILE audio_resweep3_minimal.patch bytes=13655 errors=0 warnings=0 successMarkers=0 selected=9
- diff --git a/Assets/Project/Scripts/AcousticZoneController.cs b/Assets/Project/Scripts/AcousticZoneController.cs
- a/Assets/Project/Scripts/AcousticZoneController.cs
- +++ b/Assets/Project/Scripts/AcousticZoneController.cs
- diff --git a/Assets/Project/Scripts/Audio/HectonMusicDirector.cs b/Assets/Project/Scripts/Audio/HectonMusicDirector.cs
- a/Assets/Project/Scripts/Audio/HectonMusicDirector.cs
- +++ b/Assets/Project/Scripts/Audio/HectonMusicDirector.cs
- diff --git a/Assets/Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs b/Assets/Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs
- a/Assets/Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs
- +++ b/Assets/Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs

FILE audio_resweep4_minimal.patch bytes=13655 errors=0 warnings=0 successMarkers=0 selected=9
- diff --git a/Assets/Project/Scripts/AcousticZoneController.cs b/Assets/Project/Scripts/AcousticZoneController.cs
- a/Assets/Project/Scripts/AcousticZoneController.cs
- +++ b/Assets/Project/Scripts/AcousticZoneController.cs
- diff --git a/Assets/Project/Scripts/Audio/HectonMusicDirector.cs b/Assets/Project/Scripts/Audio/HectonMusicDirector.cs
- a/Assets/Project/Scripts/Audio/HectonMusicDirector.cs
- +++ b/Assets/Project/Scripts/Audio/HectonMusicDirector.cs
- diff --git a/Assets/Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs b/Assets/Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs
- a/Assets/Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs
- +++ b/Assets/Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs

FILE audio_resweep6_all_minimal.patch bytes=27465 errors=0 warnings=0 successMarkers=3 selected=24
- diff --git a/Assets/Project/Scripts/AcousticZoneController.cs b/Assets/Project/Scripts/AcousticZoneController.cs
- a/Assets/Project/Scripts/AcousticZoneController.cs
- +++ b/Assets/Project/Scripts/AcousticZoneController.cs
- diff --git a/Assets/Project/Scripts/Audio/HectonMusicDirector.cs b/Assets/Project/Scripts/Audio/HectonMusicDirector.cs
- a/Assets/Project/Scripts/Audio/HectonMusicDirector.cs
- +++ b/Assets/Project/Scripts/Audio/HectonMusicDirector.cs
- diff --git a/Assets/Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs b/Assets/Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs
- a/Assets/Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs
- +++ b/Assets/Project/Scripts/Audio/Prologue/PrologueAcousticOrchestrator.cs
- diff --git a/Assets/Project/Scripts/PlayerBuilder.cs b/Assets/Project/Scripts/PlayerBuilder.cs
- a/Assets/Project/Scripts/PlayerBuilder.cs
- +++ b/Assets/Project/Scripts/PlayerBuilder.cs
- diff --git a/Assets/Project/Scripts/PlayerThrusterAudio.cs b/Assets/Project/Scripts/PlayerThrusterAudio.cs
- a/Assets/Project/Scripts/PlayerThrusterAudio.cs
- +++ b/Assets/Project/Scripts/PlayerThrusterAudio.cs
- diff --git a/Assets/Project/Scripts/UI/AcousticRadarSphereRenderer.cs b/Assets/Project/Scripts/UI/AcousticRadarSphereRenderer.cs
- a/Assets/Project/Scripts/UI/AcousticRadarSphereRenderer.cs
- +++ b/Assets/Project/Scripts/UI/AcousticRadarSphereRenderer.cs
- diff --git a/Assets/Project/Scripts/UI/SonarHoloCompass.cs b/Assets/Project/Scripts/UI/SonarHoloCompass.cs
- a/Assets/Project/Scripts/UI/SonarHoloCompass.cs
- +++ b/Assets/Project/Scripts/UI/SonarHoloCompass.cs
- diff --git a/Assets/Project/Scripts/Visor/SpectrumSystem.cs b/Assets/Project/Scripts/Visor/SpectrumSystem.cs
- a/Assets/Project/Scripts/Visor/SpectrumSystem.cs
- +++ b/Assets/Project/Scripts/Visor/SpectrumSystem.cs

FILE Build_AUTOFIX9_Assembly-CSharp-Editor.log bytes=2029 errors=0 warnings=0 successMarkers=0 selected=1
- COMMAND: dotnet build Assembly-CSharp-Editor.csproj --no-restore -m:2 /nr:false /p:UseSharedCompilation=false

FILE Build_EXTERNAL_CODEX_hotpath_cleanup106_save_owner_tail.log bytes=7040 errors=7 warnings=11 successMarkers=0 selected=12
- CSC : warning CS2002: Source file 'C:\hades\Hecton8\Assets\Project\Scripts\Input\InputManager.cs' specified multiple times [C:\hades\Hecton8\Hecton8.Core.csproj]
- CSC : warning CS2002: Source file 'C:\hades\Hecton8\Assets\Project\Scripts\Input\UserOptionsPersistence.cs' specified multiple times [C:\hades\Hecton8\Hecton8.Core.csproj]
- CSC : warning CS2002: Source file 'C:\hades\Hecton8\Assets\Project\Scripts\Core\Contracts\Signals\UniversalInputStateSignal.cs' specified multiple times [C:\hades\Hecton8\Hecton8.Core.csproj]
- CSC : warning CS2002: Source file 'C:\hades\Hecton8\Assets\Project\Scripts\Audio\AcousticPortalPropagation.cs' specified multiple times [C:\hades\Hecton8\Hecton8.Core.csproj]
- CSC : warning CS2002: Source file 'C:\hades\Hecton8\Assets\Project\Scripts\Audio\AudioVirtualizationJobs.cs' specified multiple times [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(1069,21): error CS0103: The name 'PhysicsDeterminismSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(1509,21): error CS0103: The name 'PhysicsDeterminismSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(1512,38): error CS0165: Use of unassigned local variable 'kccVelocity' [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll
- Build FAILED.
- 5 Warning(s)
- 3 Error(s)

FILE Build_EXTERNAL_CODEX_hotpath_cleanup122_dispatcher_rebind_tail.log bytes=4338 errors=0 warnings=9 successMarkers=1 selected=8
- CSC : warning CS2002: Source file 'C:\hades\Hecton8\Assets\Project\Scripts\Input\InputManager.cs' specified multiple times [C:\hades\Hecton8\Hecton8.Core.csproj]
- CSC : warning CS2002: Source file 'C:\hades\Hecton8\Assets\Project\Scripts\Input\UserOptionsPersistence.cs' specified multiple times [C:\hades\Hecton8\Hecton8.Core.csproj]
- CSC : warning CS2002: Source file 'C:\hades\Hecton8\Assets\Project\Scripts\Core\Contracts\Signals\UniversalInputStateSignal.cs' specified multiple times [C:\hades\Hecton8\Hecton8.Core.csproj]
- CSC : warning CS2002: Source file 'C:\hades\Hecton8\Assets\Project\Scripts\Audio\AcousticPortalPropagation.cs' specified multiple times [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll
- Build succeeded.
- 4 Warning(s)
- 0 Error(s)

FILE Build_EXTERNAL_CODEX_hotpath_cleanup122_tick_registration.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup129_buildserver_shutdown.log bytes=322 errors=0 warnings=0 successMarkers=0 selected=4
- Shutting down MSBuild server...
- Shutting down VB/C# compiler server...
- MSBuild server shut down successfully.
- VB/C# compiler server shut down successfully.

FILE Build_EXTERNAL_CODEX_hotpath_cleanup129_registration_probe_multilane.log bytes=23214 errors=45 warnings=3 successMarkers=0 selected=27
- CSC : warning CS2002: Source file 'C:\hades\Hecton8\Assets\Project\Scripts\Core\Contracts\Fluids\FluidAnalyticalContracts.cs' specified multiple times [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\HectonUnderwaterVisuals.cs(6212,17): error CS0103: The name 'PhysicsDeterminismSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\HectonUnderwaterVisuals.cs(6261,35): error CS0103: The name 'PhysicsDeterminismSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\SubmarineAutoLevelBallastController.cs(924,13): error CS0246: The type or namespace name 'SubmarineFluidDynamics' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\SubmarineAutoLevelBallastController.cs(933,13): error CS0246: The type or namespace name 'SubmarineFluidDynamics' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\SubmarineAutoLevelBallastController.cs(1550,17): error CS0103: The name 'PhysicsForceRouter' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\SubmarineAutoLevelBallastController.cs(1570,13): error CS0246: The type or namespace name 'SubmarineFluidDynamics' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\SubmarineAutoLevelBallastController.cs(1746,13): error CS0246: The type or namespace name 'SubmarineFluidDynamics' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\SubmarineAutoLevelBallastController.cs(1856,13): error CS0246: The type or namespace name 'SubmarineFluidDynamics' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\SubmarineAutoLevelBallastController.cs(1889,13): error CS0246: The type or namespace name 'SubmarineFluidDynamics' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\SubmarineAutoLevelBallastController.cs(2243,17): error CS0103: The name 'PhysicsForceRouter' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\SubmarineAutoLevelBallastController.cs(2247,17): error CS0103: The name 'PhysicsForceRouter' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\SubmarineAutoLevelBallastController.cs(2262,13): error CS0246: The type or namespace name 'SubmarineFluidDynamics' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\SubmarineAutoLevelBallastController.cs(2290,13): error CS0246: The type or namespace name 'SubmarineFluidDynamics' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs(2585,13): error CS0103: The name 'PhysicsDeterminismSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs(3409,18): error CS0103: The name 'PhysicsDeterminismSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs(3689,22): error CS0103: The name 'PhysicsDeterminismSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs(3712,41): error CS0103: The name 'PhysicsDeterminismSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs(3753,13): error CS0103: The name 'PhysicsDeterminismSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs(3800,13): error CS0103: The name 'PhysicsDeterminismSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs(4030,37): error CS0103: The name 'PhysicsDeterminismSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs(4052,37): error CS0103: The name 'PhysicsDeterminismSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\PlayerKinematicsRuntime.cs(4063,37): error CS0103: The name 'PhysicsDeterminismSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll
- Build FAILED.
- 1 Warning(s)
- 22 Error(s)

FILE Build_EXTERNAL_CODEX_hotpath_cleanup129_registration_probe_zero.log bytes=4904 errors=0 warnings=5 successMarkers=0 selected=17
- C:\Program Files\dotnet\sdk\10.0.202\Microsoft.Common.CurrentVersion.targets(3743,5): warning MSB3491: Could not write lines to file "C:\hades\Hecton8\Temp\obj\Hecton8.Core\.NETStandard,Version=v2.1.AssemblyAttributes.cs". Access to the path is denied. [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\Program Files\dotnet\sdk\10.0.202\Microsoft.Common.CurrentVersion.targets(3881,5): error MSB3491: Could not write lines to file "C:\hades\Hecton8\Temp\obj\Hecton8.Core\Hecton8.Core.csproj.CoreCompileInputs.cache". Access to the path is denied. [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\Program Files\dotnet\sdk\10.0.202\Microsoft.Common.CurrentVersion.targets(6002,5): error MSB3491: Could not write lines to file "C:\hades\Hecton8\Temp\obj\Hecton8.Core\Hecton8.Core.csproj.FileListAbsolute.txt". Access to the path is denied. [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll
- C:\Program Files\dotnet\sdk\10.0.202\Microsoft.Common.CurrentVersion.targets(3743,5): warning MSB3491: Could not write lines to file "C:\hades\Hecton8\Temp\obj\GPUInstancer\.NETStandard,Version=v2.1.AssemblyAttributes.cs". Access to the path is denied. [C:\hades\Hecton8\GPUInstancer.csproj]
- C:\Program Files\dotnet\sdk\10.0.202\Microsoft.Common.CurrentVersion.targets(3881,5): error MSB3491: Could not write lines to file "C:\hades\Hecton8\Temp\obj\GPUInstancer\GPUInstancer.csproj.CoreCompileInputs.cache". Access to the path is denied. [C:\hades\Hecton8\GPUInstancer.csproj]
- C:\Program Files\dotnet\sdk\10.0.202\Microsoft.Common.CurrentVersion.targets(6002,5): error MSB3491: Could not write lines to file "C:\hades\Hecton8\Temp\obj\GPUInstancer\GPUInstancer.csproj.FileListAbsolute.txt". Access to the path is denied. [C:\hades\Hecton8\GPUInstancer.csproj]
- C:\Program Files\dotnet\sdk\10.0.202\Microsoft.Common.CurrentVersion.targets(3743,5): warning MSB3491: Could not write lines to file "C:\hades\Hecton8\Temp\obj\MapMagic\.NETStandard,Version=v2.1.AssemblyAttributes.cs". Access to the path is denied. [C:\hades\Hecton8\MapMagic.csproj]
- C:\Program Files\dotnet\sdk\10.0.202\Microsoft.Common.CurrentVersion.targets(3881,5): error MSB3491: Could not write lines to file "C:\hades\Hecton8\Temp\obj\MapMagic\MapMagic.csproj.CoreCompileInputs.cache". Access to the path is denied. [C:\hades\Hecton8\MapMagic.csproj]
- C:\Program Files\dotnet\sdk\10.0.202\Microsoft.Common.CurrentVersion.targets(6002,5): error MSB3491: Could not write lines to file "C:\hades\Hecton8\Temp\obj\MapMagic\MapMagic.csproj.FileListAbsolute.txt". Access to the path is denied. [C:\hades\Hecton8\MapMagic.csproj]
- C:\Program Files\dotnet\sdk\10.0.202\Microsoft.Common.CurrentVersion.targets(3743,5): warning MSB3491: Could not write lines to file "C:\hades\Hecton8\Temp\obj\ShapesRuntime\.NETStandard,Version=v2.1.AssemblyAttributes.cs". Access to the path is denied. [C:\hades\Hecton8\ShapesRuntime.csproj]
- C:\Program Files\dotnet\sdk\10.0.202\Microsoft.Common.CurrentVersion.targets(3881,5): error MSB3491: Could not write lines to file "C:\hades\Hecton8\Temp\obj\ShapesRuntime\ShapesRuntime.csproj.CoreCompileInputs.cache". Access to the path is denied. [C:\hades\Hecton8\ShapesRuntime.csproj]
- C:\Program Files\dotnet\sdk\10.0.202\Microsoft.Common.CurrentVersion.targets(6002,5): error MSB3491: Could not write lines to file "C:\hades\Hecton8\Temp\obj\ShapesRuntime\ShapesRuntime.csproj.FileListAbsolute.txt". Access to the path is denied. [C:\hades\Hecton8\ShapesRuntime.csproj]
- C:\Program Files\dotnet\sdk\10.0.202\Microsoft.Common.CurrentVersion.targets(3743,5): warning MSB3491: Could not write lines to file "C:\hades\Hecton8\Temp\obj\VolumetricLightBeam\.NETStandard,Version=v2.1.AssemblyAttributes.cs". Access to the path is denied. [C:\hades\Hecton8\VolumetricLightBeam.csproj]
- C:\Program Files\dotnet\sdk\10.0.202\Microsoft.Common.CurrentVersion.targets(3881,5): error MSB3491: Could not write lines to file "C:\hades\Hecton8\Temp\obj\VolumetricLightBeam\VolumetricLightBeam.csproj.CoreCompileInputs.cache". Access to the path is denied. [C:\hades\Hecton8\VolumetricLightBeam.csproj]
- C:\Program Files\dotnet\sdk\10.0.202\Microsoft.Common.CurrentVersion.targets(6002,5): error MSB3491: Could not write lines to file "C:\hades\Hecton8\Temp\obj\VolumetricLightBeam\VolumetricLightBeam.csproj.FileListAbsolute.txt". Access to the path is denied. [C:\hades\Hecton8\VolumetricLightBeam.csproj]
- C:\Program Files\dotnet\sdk\10.0.202\Microsoft.Common.CurrentVersion.targets(6002,5): error MSB3491: Could not write lines to file "C:\hades\Hecton8\Temp\obj\Hecton8.Editor\Hecton8.Editor.csproj.FileListAbsolute.txt". Access to the path is denied. [C:\hades\Hecton8\Hecton8.Editor.csproj]

FILE Build_EXTERNAL_CODEX_hotpath_cleanup131_target_dedupe.log bytes=1030 errors=0 warnings=1 successMarkers=0 selected=2
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll
- C:\Program Files\dotnet\sdk\10.0.202\Microsoft.Common.CurrentVersion.targets(2451,5): warning MSB3101: Could not write state file "Temp\obj\Hecton8.Editor\Hecton8.Editor.csproj.AssemblyReference.cache". Access to the path 'C:\hades\Hecton8\Temp\obj\Hecton8.Editor\Hecton8.Editor.csproj.AssemblyReference.cache' is denied. [C:\hades\Hecton8\Hecton8.Editor.csproj]

FILE Build_EXTERNAL_CODEX_hotpath_cleanup139_context_purity.log bytes=610 errors=0 warnings=0 successMarkers=0 selected=2
- C:\Program Files\dotnet\sdk\10.0.202\Sdks\Microsoft.NET.Sdk\targets\Microsoft.PackageDependencyResolution.targets(266,5): error NETSDK1004: Assets file 'C:\hades\Hecton8\Temp\obj\Hecton8.Editor\project.assets.json' not found. Run a NuGet package restore to generate this file. [C:\hades\Hecton8\Hecton8.Editor.csproj]
- C:\Program Files\dotnet\sdk\10.0.202\Microsoft.Common.CurrentVersion.targets(6002,5): error MSB3491: Could not write lines to file "C:\hades\Hecton8\Temp\obj\Hecton8.Editor\Hecton8.Editor.csproj.FileListAbsolute.txt". Access to the path is denied. [C:\hades\Hecton8\Hecton8.Editor.csproj]

FILE Build_EXTERNAL_CODEX_hotpath_cleanup144_rebind_batch.log bytes=610 errors=0 warnings=0 successMarkers=0 selected=2
- C:\Program Files\dotnet\sdk\10.0.202\Sdks\Microsoft.NET.Sdk\targets\Microsoft.PackageDependencyResolution.targets(266,5): error NETSDK1004: Assets file 'C:\hades\Hecton8\Temp\obj\Hecton8.Editor\project.assets.json' not found. Run a NuGet package restore to generate this file. [C:\hades\Hecton8\Hecton8.Editor.csproj]
- C:\Program Files\dotnet\sdk\10.0.202\Microsoft.Common.CurrentVersion.targets(6002,5): error MSB3491: Could not write lines to file "C:\hades\Hecton8\Temp\obj\Hecton8.Editor\Hecton8.Editor.csproj.FileListAbsolute.txt". Access to the path is denied. [C:\hades\Hecton8\Hecton8.Editor.csproj]

FILE Build_EXTERNAL_CODEX_hotpath_cleanup147_gi_despawn.log bytes=610 errors=0 warnings=0 successMarkers=0 selected=2
- C:\Program Files\dotnet\sdk\10.0.202\Sdks\Microsoft.NET.Sdk\targets\Microsoft.PackageDependencyResolution.targets(266,5): error NETSDK1004: Assets file 'C:\hades\Hecton8\Temp\obj\Hecton8.Editor\project.assets.json' not found. Run a NuGet package restore to generate this file. [C:\hades\Hecton8\Hecton8.Editor.csproj]
- C:\Program Files\dotnet\sdk\10.0.202\Microsoft.Common.CurrentVersion.targets(6002,5): error MSB3491: Could not write lines to file "C:\hades\Hecton8\Temp\obj\Hecton8.Editor\Hecton8.Editor.csproj.FileListAbsolute.txt". Access to the path is denied. [C:\hades\Hecton8\Hecton8.Editor.csproj]

FILE Build_EXTERNAL_CODEX_hotpath_cleanup158_world_dispatcher_rebind.log bytes=1440 errors=1 warnings=1 successMarkers=0 selected=4
- C:\Program Files\dotnet\sdk\10.0.202\Sdks\Microsoft.NET.Sdk\targets\Microsoft.PackageDependencyResolution.targets(266,5): error NETSDK1004: Assets file 'C:\hades\Hecton8\Temp\obj\Hecton8.Editor\project.assets.json' not found. Run a NuGet package restore to generate this file. [C:\hades\Hecton8\Hecton8.Editor.csproj]
- Build FAILED.
- 0 Warning(s)
- 1 Error(s)

FILE Build_EXTERNAL_CODEX_hotpath_cleanup16.log bytes=755 errors=1 warnings=0 successMarkers=0 selected=2
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\PlayerKinematicsRuntimeHandIK.cs(34,92): error CS0117: 'PlayerHandIkContract' does not contain a definition for 'PublishedStatesBufferId' [C:\hades\Hecton8\Hecton8.Core.csproj]

FILE Build_EXTERNAL_CODEX_hotpath_cleanup16_retry1.log bytes=808 errors=1 warnings=0 successMarkers=0 selected=2
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\PlayerKinematicsRuntimeHandIK.cs(510,58): error CS0246: The type or namespace name 'AbsoluteUniversePosition' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]

FILE Build_EXTERNAL_CODEX_hotpath_cleanup16_retry2.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup17.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup18.log bytes=828 errors=1 warnings=0 successMarkers=0 selected=2
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll
- C:\hades\Hecton8\Assets\Project\Scripts\WorldProceduralScatterDirector.cs(11167,17): error CS0120: An object reference is required for the non-static field, method, or property 'WorldProceduralScatterDirector.DestroyProxyInstance(WorldProceduralProxyInstance)' [C:\hades\Hecton8\Hecton8.Core.csproj]

FILE Build_EXTERNAL_CODEX_hotpath_cleanup18_retry1.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup19.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup19_retry1.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup20.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup21.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup21_beacon_pause.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup22_audio_log.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup22_flora_genome.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup23_outpost_quality.log bytes=4705 errors=20 warnings=0 successMarkers=0 selected=21
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(963,17): error CS0103: The name 'RecordVaultTelemetry' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(971,17): error CS0103: The name 'RecordVaultTelemetry' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(991,17): error CS0103: The name 'RecordVaultTelemetry' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1000,17): error CS0103: The name 'RecordVaultTelemetry' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1046,13): error CS0103: The name 'EnsureEncryptedFragmentState' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1049,17): error CS0103: The name 'encryptedFragmentLogHashes' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1050,17): error CS0103: The name 'encryptedFragmentRecoveredBits' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1059,35): error CS0103: The name 'encryptedFragmentLogHashes' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1059,77): error CS0103: The name 'encryptedFragmentRecoveredBits' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1064,21): error CS0103: The name 'encryptedFragmentLogHashes' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1067,33): error CS0103: The name 'encryptedFragmentRecoveredBits' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1079,13): error CS0103: The name 'EnsureEncryptedFragmentState' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1082,21): error CS0103: The name 'encryptedFragmentLogHashes' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1085,17): error CS0103: The name 'encryptedFragmentRecoveredBits' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1099,13): error CS0103: The name 'encryptedFragmentLogHashes' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1100,13): error CS0103: The name 'encryptedFragmentRecoveredBits' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1275,32): error CS0103: The name 'encryptedFragmentLogHashes' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1275,72): error CS0103: The name 'encryptedFragmentLogHashes' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1276,38): error CS0103: The name 'encryptedFragmentRecoveredBits' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1277,23): error CS0103: The name 'encryptedFragmentRecoveredBits' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]

FILE Build_EXTERNAL_CODEX_hotpath_cleanup23_outpost_quality_retry1.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup23_outpost_quality_retry2.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup23_resource_scarcity.log bytes=1370 errors=4 warnings=0 successMarkers=0 selected=5
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1352,32): error CS0103: The name 'encryptedFragmentLogHashes' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1352,72): error CS0103: The name 'encryptedFragmentLogHashes' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1353,38): error CS0103: The name 'encryptedFragmentRecoveredBits' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\AudioLog\AudioLogSystem.cs(1354,23): error CS0103: The name 'encryptedFragmentRecoveredBits' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]

FILE Build_EXTERNAL_CODEX_hotpath_cleanup24_soundscape_quality.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup25_save_thumbnail_quality.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup26_contextual_ik_quality.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup27_acoustic_service_rebind.log bytes=966 errors=2 warnings=0 successMarkers=0 selected=3
- C:\hades\Hecton8\Assets\Project\Scripts\World\ProceduralWreckGenerator.cs(1555,13): error CS0103: The name 'TryUnregisterScalabilityListener' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\World\ProceduralWreckGenerator.cs(1829,13): error CS0103: The name 'TryRegisterScalabilityListener' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup27_acoustic_service_rebind_retry1.log bytes=744 errors=1 warnings=0 successMarkers=0 selected=2
- C:\hades\Hecton8\Assets\Project\Scripts\World\SargassumGlobalDragManager.cs(1589,13): error CS0103: The name 'ApplyDynamicTexturesIfDirty' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup27_wreck_quality.log bytes=1618 errors=4 warnings=1 successMarkers=0 selected=5
- C:\hades\Hecton8\Assets\Project\Scripts\Core\SystemDispatcher.cs(931,57): error CS1503: Argument 1: cannot convert from 'in Hecton8.Core.Contracts.Signals.ComplianceViolationSignal' to 'in Hecton8.Core.Contracts.Signals.HUDNotificationSignal' [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\HectonPlayerMovement.cs(9998,54): error CS1503: Argument 1: cannot convert from 'in Hecton8.Core.Contracts.Signals.CrushWarningSignal' to 'in Hecton8.Core.Contracts.Signals.VocalWarningSignal' [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\SaveManager.cs(2262,53): error CS1503: Argument 1: cannot convert from 'in Hecton8.Core.Contracts.Signals.SimulationPauseSignal' to 'in Hecton8.Core.Contracts.Signals.SystemPauseSignal' [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\SaveManager.cs(2276,53): error CS1503: Argument 1: cannot convert from 'in Hecton8.Core.Contracts.Signals.SimulationPauseSignal' to 'in Hecton8.Core.Contracts.Signals.SystemPauseSignal' [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup27_wreck_quality_retry1.log bytes=946 errors=2 warnings=0 successMarkers=0 selected=3
- C:\hades\Hecton8\Assets\Project\Scripts\PowerGrid.cs(1008,23): error CS0246: The type or namespace name 'BrownoutSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\PowerGrid.cs(1008,13): error CS0103: The name 'SignalBus' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup27_wreck_quality_retry2.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup28_ui_quality.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup29_battery_charger.log bytes=781 errors=1 warnings=0 successMarkers=0 selected=2
- C:\hades\Hecton8\Assets\Project\Scripts\GlobalPhysicsStateManager.cs(3962,42): error CS0246: The type or namespace name 'FaunaBrain' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup29_battery_charger_retry1.log bytes=725 errors=1 warnings=0 successMarkers=0 selected=2
- C:\hades\Hecton8\Assets\Project\Scripts\HectonVoxelEngine.cs(2804,73): error CS0117: 'BufferID' does not contain a definition for 'VoxelMeshPipelineBlackBox' [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup29_smoke_guard.log bytes=2817 errors=9 warnings=0 successMarkers=0 selected=10
- C:\hades\Hecton8\Assets\Project\Scripts\FaunaDirector.cs(68,114): error CS0246: The type or namespace name 'IAcousticPingEventListener' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\FaunaDirector.cs(902,39): error CS0246: The type or namespace name 'AcousticPingEvent' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Data\Monolith\H8StaticDataArena.cs(1501,10): error CS0246: The type or namespace name 'DllImportAttribute' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Data\Monolith\H8StaticDataArena.cs(1501,10): error CS0246: The type or namespace name 'DllImport' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Data\Monolith\H8StaticDataArena.cs(1501,67): error CS0103: The name 'CharSet' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Data\Monolith\H8StaticDataArena.cs(1511,10): error CS0246: The type or namespace name 'DllImportAttribute' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Data\Monolith\H8StaticDataArena.cs(1511,10): error CS0246: The type or namespace name 'DllImport' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Data\Monolith\H8StaticDataArena.cs(1519,10): error CS0246: The type or namespace name 'DllImportAttribute' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Data\Monolith\H8StaticDataArena.cs(1519,10): error CS0246: The type or namespace name 'DllImport' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup29_smoke_guard_retry1.log bytes=748 errors=1 warnings=0 successMarkers=0 selected=2
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\MessageTerminal.cs(72,96): error CS0535: 'MessageTerminal' does not implement interface member 'ILateFrameTickable.LateFrameTick()' [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup29_smoke_guard_retry2.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup30_loot_quality.log bytes=2858 errors=11 warnings=0 successMarkers=0 selected=12
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3484,18): error CS0103: The name 'KinematicCcdMath' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3505,30): error CS0103: The name 'KinematicCcdMath' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3512,34): error CS0103: The name 'KinematicCcdMath' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3707,49): error CS0246: The type or namespace name 'IPhysicsImpactMaterialProvider' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3710,13): error CS0246: The type or namespace name 'IPhysicsImpactMaterialProvider' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3710,94): error CS0246: The type or namespace name 'IPhysicsImpactMaterialProvider' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3731,39): error CS0103: The name 'KinematicCcdMath' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3786,65): error CS0103: The name 'KinematicCcdMath' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3804,13): error CS0103: The name 'GlobalPhysicsStateManager' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(5192,38): error CS0103: The name 'CurrentVolume' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(7250,40): error CS0103: The name 'HectonContactJob' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup30_loot_quality_retry1.log bytes=767 errors=1 warnings=0 successMarkers=0 selected=2
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\MountablePlayerTransport.cs(28,121): error CS0535: 'MountablePlayerTransport' does not implement interface member 'ILateFrameTickable.LateFrameTick()' [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup30_loot_quality_retry2.log bytes=2858 errors=11 warnings=0 successMarkers=0 selected=12
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3484,18): error CS0103: The name 'KinematicCcdMath' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3505,30): error CS0103: The name 'KinematicCcdMath' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3512,34): error CS0103: The name 'KinematicCcdMath' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3707,49): error CS0246: The type or namespace name 'IPhysicsImpactMaterialProvider' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3710,13): error CS0246: The type or namespace name 'IPhysicsImpactMaterialProvider' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3710,94): error CS0246: The type or namespace name 'IPhysicsImpactMaterialProvider' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3731,39): error CS0103: The name 'KinematicCcdMath' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3786,65): error CS0103: The name 'KinematicCcdMath' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3804,13): error CS0103: The name 'GlobalPhysicsStateManager' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(5192,38): error CS0103: The name 'CurrentVolume' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(7250,40): error CS0103: The name 'HectonContactJob' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup30_loot_quality_retry3.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup31_airlock_flora_quality.log bytes=3680 errors=11 warnings=12 successMarkers=0 selected=13
- C:\hades\Hecton8\Assets\Project\Scripts\GlobalPhysicsStateManager.cs(60,14): warning CS0108: 'IPhysicsImpactMaterialProvider.ImpactAudioMaterialId' hides inherited member 'IImpactMaterialProvider.ImpactAudioMaterialId'. Use the new keyword if hiding was intended. [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Audio\VocalWarningSystem.cs(2039,43): error CS0246: The type or namespace name 'NativeMinHeap<' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Audio\VocalWarningSystem.cs(2094,41): error CS0246: The type or namespace name 'NativeMinHeap<' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Audio\VocalWarningSystem.cs(2110,40): error CS0246: The type or namespace name 'NativeMinHeap<' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Audio\VocalWarningSystem.cs(2134,51): error CS0246: The type or namespace name 'NativeMinHeap<' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Audio\VocalWarningSystem.cs(2168,47): error CS0246: The type or namespace name 'NativeMinHeap<' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Audio\VocalWarningSystem.cs(2178,60): error CS0246: The type or namespace name 'NativeMinHeap<' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Audio\VocalWarningSystem.cs(2198,44): error CS0246: The type or namespace name 'NativeMinHeap<' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Audio\VocalWarningSystem.cs(2215,46): error CS0246: The type or namespace name 'NativeMinHeap<' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Audio\VocalWarningSystem.cs(2244,60): error CS0246: The type or namespace name 'NativeMinHeap<' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Audio\VocalWarningSystem.cs(2254,71): error CS0246: The type or namespace name 'NativeMinHeap<' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Audio\VocalWarningSystem.cs(2264,47): error CS0246: The type or namespace name 'NativeMinHeap<' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup31_airlock_flora_quality_retry1.log bytes=1348 errors=4 warnings=0 successMarkers=0 selected=5
- C:\hades\Hecton8\Assets\Project\Scripts\Power\LogisticsNetworkGraph.cs(1355,62): error CS0103: The name 'JacobiPowerGridSolverJob' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\LogisticsNetworkGraph.cs(1356,62): error CS0103: The name 'JacobiPowerGridSolverJob' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\PowerGrid.cs(51,80): error CS0117: 'LogisticsNetworkGraph' does not contain a definition for 'JacobiPowerGridSolverJob' [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\PowerGrid.cs(52,91): error CS0117: 'LogisticsNetworkGraph' does not contain a definition for 'JacobiPowerGridSolverJob' [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup31_airlock_flora_quality_retry2.log bytes=732 errors=1 warnings=1 successMarkers=0 selected=2
- C:\hades\Hecton8\Assets\Project\Scripts\Audio\VocalWarningSystem.cs(1975,39): error CS0103: The name 'ResolvePriorityBitIndex' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup31_airlock_flora_quality_retry3.log bytes=7619 errors=15 warnings=11 successMarkers=2 selected=27
- C:\hades\Hecton8\Assets\Project\Scripts\UI\PDADecryptionSpectrogramPanel.cs(859,13): error CS0103: The name 'materialBufferBound' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(422,35): error CS0246: The type or namespace name 'LogisticsFlowDeltaPassJob' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(457,17): error CS0117: 'ShinobuLogisticsRouter.LogisticsFlowFinalizeJob' does not contain a definition for 'DeltaPassCount' [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(1123,13): error CS0103: The name 'jacobiIterations' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(1315,17): error CS0117: 'LogisticsTuningDTO' does not contain a definition for 'JacobiSmoothingFactor' [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(1315,41): error CS0103: The name 'DefaultJacobiSmoothing' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(1616,44): error CS1061: 'LogisticsGraphTelemetryEntry' does not contain a definition for 'JacobiIterations' and no accessible extension method 'JacobiIterations' accepting a first argument of type 'LogisticsGraphTelemetryEntry' could be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(1762,28): error CS1061: 'LogisticsTuningDTO' does not contain a definition for 'JacobiSmoothingFactor' and no accessible extension method 'JacobiSmoothingFactor' accepting a first argument of type 'LogisticsTuningDTO' could be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(1963,17): error CS0117: 'LogisticsTuningDTO' does not contain a definition for 'JacobiSmoothingFactor' [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(1963,68): error CS1061: 'LogisticsTuningDTO' does not contain a definition for 'JacobiSmoothingFactor' and no accessible extension method 'JacobiSmoothingFactor' accepting a first argument of type 'LogisticsTuningDTO' could be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(1963,91): error CS0103: The name 'DefaultJacobiSmoothing' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(2787,26): error CS0103: The name 'CounterJacobiIterations' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(2805,21): error CS0117: 'LogisticsGraphTelemetryEntry' does not contain a definition for 'JacobiIterations' [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(2593,62): error CS1061: 'LogisticsTuningDTO' does not contain a definition for 'JacobiSmoothingFactor' and no accessible extension method 'JacobiSmoothingFactor' accepting a first argument of type 'LogisticsTuningDTO' could be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(2593,85): error CS0103: The name 'DefaultJacobiSmoothing' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(2563,24): warning CS0649: Field 'ShinobuLogisticsRouter.LogisticsFlowSolverJob.EdgeOffsetsBaseIndex' is never assigned to, and will always have its default value 0 [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\UI\PDADecryptionSpectrogramPanel.cs(1079,26): warning CS0649: Field 'PDADecryptionSpectrogramPanel.FrequencyWaveGenerateJob.LocalWidth' is never assigned to, and will always have its default value 0 [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(2697,24): warning CS0649: Field 'ShinobuLogisticsRouter.LogisticsFlowFinalizeJob.JacobiIterations' is never assigned to, and will always have its default value 0 [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(2561,24): warning CS0649: Field 'ShinobuLogisticsRouter.LogisticsFlowSolverJob.NodeCount' is never assigned to, and will always have its default value 0 [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(2560,84): warning CS0649: Field 'ShinobuLogisticsRouter.LogisticsFlowSolverJob.NodesPtr' is never assigned to, and will always have its default value [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(2565,24): warning CS0649: Field 'ShinobuLogisticsRouter.LogisticsFlowSolverJob.AdjacencyEntryCount' is never assigned to, and will always have its default value 0 [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\UI\PDADecryptionSpectrogramPanel.cs(1080,26): warning CS0649: Field 'PDADecryptionSpectrogramPanel.FrequencyWaveGenerateJob.LocalHeight' is never assigned to, and will always have its default value 0 [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(2564,24): warning CS0649: Field 'ShinobuLogisticsRouter.LogisticsFlowSolverJob.EdgeDestinationsBaseIndex' is never assigned to, and will always have its default value 0 [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(2562,26): warning CS0649: Field 'ShinobuLogisticsRouter.LogisticsFlowSolverJob.GlobalQualityWeight' is never assigned to, and will always have its default value 0 [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\UI\PDADecryptionSpectrogramPanel.cs(1074,24): warning CS0649: Field 'PDADecryptionSpectrogramPanel.FrequencyWaveGenerateJob.SegmentCount' is never assigned to, and will always have its default value 0 [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\UI\PDADecryptionSpectrogramPanel.cs(1081,24): warning CS0649: Field 'PDADecryptionSpectrogramPanel.FrequencyWaveGenerateJob.StageIndex' is never assigned to, and will always have its default value 0 [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup31_airlock_flora_quality_retry4.log bytes=4112 errors=16 warnings=0 successMarkers=0 selected=17
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(812,41): error CS0103: The name 'ArmorGridRows' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(814,45): error CS0103: The name 'ArmorGridColumns' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(816,44): error CS0103: The name 'ArmorGridColumns' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(925,47): error CS0103: The name 'ArmorGridColumns' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(925,90): error CS0103: The name 'ArmorGridRows' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(928,38): error CS0103: The name 'ArmorGridRows' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(1009,60): error CS0103: The name 'ArmorGridColumns' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(1009,82): error CS0103: The name 'ArmorGridColumns' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(1010,57): error CS0103: The name 'ArmorGridRows' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(1010,76): error CS0103: The name 'ArmorGridRows' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(1011,35): error CS0103: The name 'ArmorGridColumns' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\UI\PDADecryptionSpectrogramPanel.cs(694,17): error CS0103: The name 'ToolHapticsRuntime' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(1485,38): error CS0103: The name 'ArmorGridColumns' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(1485,72): error CS0103: The name 'ArmorGridColumns' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(1486,38): error CS0103: The name 'ArmorGridRows' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(1486,69): error CS0103: The name 'ArmorGridRows' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup31_airlock_flora_quality_retry5.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup32_scatter_quality.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup33_atmo_thermo_quality.log bytes=945 errors=2 warnings=0 successMarkers=0 selected=3
- C:\hades\Hecton8\Assets\Project\Scripts\Interaction\EquipmentInteractionHandler.cs(926,44): error CS0103: The name 'TryResolveKinematicRaycastHit' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\VRSomaticProvider.cs(1938,18): error CS0103: The name 'IsFinite' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup33_atmo_thermo_quality_retry1.log bytes=11893 errors=43 warnings=1 successMarkers=0 selected=34
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(23,58): error CS0234: The type or namespace name 'DiegeticHudSignal' does not exist in the namespace 'Hecton8.Core.Contracts.Signals' (are you missing an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(30,61): error CS0234: The type or namespace name 'ScanLogChangedSignal' does not exist in the namespace 'Hecton8.Core.Contracts.Signals' (are you missing an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(1177,72): error CS0246: The type or namespace name 'WfcOutpostGeneratedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(4680,69): error CS0246: The type or namespace name 'VehicleUpgradesChangedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(4692,60): error CS0246: The type or namespace name 'SaveLifecycleSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(4900,67): error CS0246: The type or namespace name 'ManualOverridePulledSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(4918,66): error CS0246: The type or namespace name 'WfcOutpostGeneratedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(4930,66): error CS0246: The type or namespace name 'WfcOutpostDoorPowerSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\WfcOutpostPowerBootRuntime.cs(272,48): error CS0246: The type or namespace name 'WfcOutpostGeneratedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\WfcOutpostPowerBootRuntime.cs(302,46): error CS0246: The type or namespace name 'WfcOutpostGeneratedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\WfcOutpostPowerBootRuntime.cs(307,50): error CS0246: The type or namespace name 'WfcOutpostGeneratedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\WfcOutpostPowerBootRuntime.cs(312,53): error CS0246: The type or namespace name 'WfcOutpostGeneratedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Scavenging\ScavengingLootOracle.cs(690,28): error CS0246: The type or namespace name 'HUDNotificationSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(6192,35): error CS0246: The type or namespace name 'HUDNotificationSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7213,39): error CS0246: The type or namespace name 'HUDNotificationSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7227,39): error CS0246: The type or namespace name 'ThermalStateChangedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7234,39): error CS0246: The type or namespace name 'BatteryLevelSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7276,39): error CS0246: The type or namespace name 'PdaExchangeStateChangedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7283,39): error CS0246: The type or namespace name 'VehicleUpgradesChangedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7321,39): error CS0246: The type or namespace name 'ManualOverridePulledSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7328,39): error CS0246: The type or namespace name 'ReconDataSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7335,39): error CS0246: The type or namespace name 'SaveLifecycleSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7342,39): error CS0246: The type or namespace name 'MacroDatabaseSectorHydrationSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7349,39): error CS0246: The type or namespace name 'WfcOutpostGeneratedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7356,39): error CS0246: The type or namespace name 'WfcOutpostStateChangedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7363,39): error CS0246: The type or namespace name 'WfcOutpostDoorPowerSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7370,39): error CS0246: The type or namespace name 'ComplianceViolationSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7668,58): error CS0246: The type or namespace name 'HUDNotificationSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7673,63): error CS0246: The type or namespace name 'ManualOverridePulledSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7675,52): error CS0246: The type or namespace name 'ReconDataSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7676,56): error CS0246: The type or namespace name 'SaveLifecycleSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7677,62): error CS0246: The type or namespace name 'ComplianceViolationSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(5908,36): error CS0246: The type or namespace name 'HUDNotificationSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(5909,36): error CS0246: The type or namespace name 'ReconDataSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]

FILE Build_EXTERNAL_CODEX_hotpath_cleanup33_atmo_thermo_quality_retry2.log bytes=854 errors=0 warnings=1 successMarkers=0 selected=2
- CSC : warning CS2002: Source file 'C:\hades\Hecton8\Assets\Project\Scripts\Core\Contracts\CoreContractsAssemblyMarker.cs' specified multiple times [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup33_atmo_thermo_quality_retry3.log bytes=3682 errors=15 warnings=0 successMarkers=0 selected=16
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(141,24): error CS0103: The name 'ResolveShapeArchetype' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(149,24): error CS0103: The name 'ResolveTerrainSeamMode' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(157,24): error CS0103: The name 'ResolveCaveBlendMode' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(893,38): error CS0103: The name 'ArchetypeArchLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(895,38): error CS0103: The name 'ArchetypeCanopyLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(897,38): error CS0103: The name 'ArchetypeArchClusterLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(899,38): error CS0103: The name 'ArchetypeReefPackLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(901,38): error CS0103: The name 'ArchetypeCaveBridgeLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(909,38): error CS0103: The name 'TerrainSeamHeightBlendLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(911,38): error CS0103: The name 'TerrainSeamSdfBlendLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(913,38): error CS0103: The name 'TerrainSeamDebrisBridgeLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(915,38): error CS0103: The name 'TerrainSeamCarveAndDebrisLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(923,38): error CS0103: The name 'CaveBlendProbeOnlyLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(925,38): error CS0103: The name 'CaveBlendSdfBlendLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(927,38): error CS0103: The name 'CaveBlendCarvePortalLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup33_atmo_thermo_quality_retry4.log bytes=1591 errors=5 warnings=0 successMarkers=0 selected=6
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(2182,51): error CS0103: The name 'ResolveCachedEcosystemDirectorConcrete' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(2413,51): error CS0103: The name 'ResolveCachedEcosystemDirectorConcrete' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(6260,51): error CS0103: The name 'ResolveCachedEcosystemDirectorConcrete' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(6748,51): error CS0103: The name 'ResolveCachedEcosystemDirectorConcrete' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(6892,51): error CS0103: The name 'ResolveCachedEcosystemDirectorConcrete' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup33_atmo_thermo_quality_retry5.log bytes=1092 errors=2 warnings=0 successMarkers=0 selected=3
- C:\hades\Hecton8\Assets\Project\Scripts\Interaction\PlayerInteraction.cs(535,53): error CS0246: The type or namespace name 'QueryResult' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Interaction\PlayerInteraction.cs(495,14): error CS0540: 'PlayerInteraction.IDispatcherRaycastReceiver.ConsumeDispatcherRaycastHit(int, in RaycastHit)': containing type does not implement interface 'IDispatcherRaycastReceiver' [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup44_battery_physics_retry2.log bytes=4184 errors=17 warnings=0 successMarkers=0 selected=18
- C:\hades\Hecton8\Assets\Project\Scripts\Physics\Cavitation\AbyssalCavitationRuntime.cs(961,21): error CS0214: Pointers and fixed size buffers may only be used in an unsafe context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Physics\Cavitation\AbyssalCavitationRuntime.cs(961,47): error CS0214: Pointers and fixed size buffers may only be used in an unsafe context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Physics\Cavitation\AbyssalCavitationRuntime.cs(961,41): error CS0214: Pointers and fixed size buffers may only be used in an unsafe context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Physics\Cavitation\AbyssalCavitationRuntime.cs(961,40): error CS0214: Pointers and fixed size buffers may only be used in an unsafe context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Physics\Cavitation\AbyssalCavitationRuntime.cs(962,61): error CS0214: Pointers and fixed size buffers may only be used in an unsafe context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Physics\Cavitation\AbyssalCavitationRuntime.cs(962,46): error CS0214: Pointers and fixed size buffers may only be used in an unsafe context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3484,18): error CS0103: The name 'KinematicCcdMath' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3505,30): error CS0103: The name 'KinematicCcdMath' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3512,34): error CS0103: The name 'KinematicCcdMath' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3707,49): error CS0246: The type or namespace name 'IPhysicsImpactMaterialProvider' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3710,13): error CS0246: The type or namespace name 'IPhysicsImpactMaterialProvider' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3710,94): error CS0246: The type or namespace name 'IPhysicsImpactMaterialProvider' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3731,39): error CS0103: The name 'KinematicCcdMath' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3786,65): error CS0103: The name 'KinematicCcdMath' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(3804,13): error CS0103: The name 'GlobalPhysicsStateManager' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(5192,38): error CS0103: The name 'CurrentVolume' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Fauna\FaunaBrain.cs(7250,40): error CS0103: The name 'HectonContactJob' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup49_service_rebind.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup52_cultivation_inventory_rebind.log bytes=11893 errors=43 warnings=1 successMarkers=0 selected=34
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(30,61): error CS0234: The type or namespace name 'ScanLogChangedSignal' does not exist in the namespace 'Hecton8.Core.Contracts.Signals' (are you missing an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(23,58): error CS0234: The type or namespace name 'DiegeticHudSignal' does not exist in the namespace 'Hecton8.Core.Contracts.Signals' (are you missing an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\ShinobuLogisticsRouter.cs(1177,72): error CS0246: The type or namespace name 'WfcOutpostGeneratedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\WfcOutpostPowerBootRuntime.cs(272,48): error CS0246: The type or namespace name 'WfcOutpostGeneratedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\WfcOutpostPowerBootRuntime.cs(302,46): error CS0246: The type or namespace name 'WfcOutpostGeneratedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\WfcOutpostPowerBootRuntime.cs(307,50): error CS0246: The type or namespace name 'WfcOutpostGeneratedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Power\WfcOutpostPowerBootRuntime.cs(312,53): error CS0246: The type or namespace name 'WfcOutpostGeneratedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Scavenging\ScavengingLootOracle.cs(690,28): error CS0246: The type or namespace name 'HUDNotificationSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\PDA\PDALogbookManager.cs(692,56): error CS0246: The type or namespace name 'ScanLogChangedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(4680,69): error CS0246: The type or namespace name 'VehicleUpgradesChangedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(4692,60): error CS0246: The type or namespace name 'SaveLifecycleSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(4900,67): error CS0246: The type or namespace name 'ManualOverridePulledSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(4918,66): error CS0246: The type or namespace name 'WfcOutpostGeneratedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(4930,66): error CS0246: The type or namespace name 'WfcOutpostDoorPowerSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Audio\VocalWarningSystem.cs(1690,52): error CS0246: The type or namespace name 'BatteryLevelSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\SaveManager.cs(1336,75): error CS0246: The type or namespace name 'WfcOutpostStateChangedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\SaveManager.cs(1393,65): error CS0246: The type or namespace name 'WfcOutpostStateChangedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\SaveManager.cs(1407,67): error CS0246: The type or namespace name 'WfcOutpostStateChangedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\SaveManager.cs(1426,26): error CS0246: The type or namespace name 'WfcOutpostStateChangedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(6192,35): error CS0246: The type or namespace name 'HUDNotificationSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\SaveManager.cs(1659,47): error CS0234: The type or namespace name 'MacroDatabaseSectorHydrationSignal' does not exist in the namespace 'Hecton8.Core.Contracts.Signals' (are you missing an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7213,39): error CS0246: The type or namespace name 'HUDNotificationSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7227,39): error CS0246: The type or namespace name 'ThermalStateChangedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7234,39): error CS0246: The type or namespace name 'BatteryLevelSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7276,39): error CS0246: The type or namespace name 'PdaExchangeStateChangedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7283,39): error CS0246: The type or namespace name 'VehicleUpgradesChangedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7321,39): error CS0246: The type or namespace name 'ManualOverridePulledSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7328,39): error CS0246: The type or namespace name 'ReconDataSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7335,39): error CS0246: The type or namespace name 'SaveLifecycleSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7342,39): error CS0246: The type or namespace name 'MacroDatabaseSectorHydrationSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7349,39): error CS0246: The type or namespace name 'WfcOutpostGeneratedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7356,39): error CS0246: The type or namespace name 'WfcOutpostStateChangedSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7363,39): error CS0246: The type or namespace name 'WfcOutpostDoorPowerSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalSignals.cs(7370,39): error CS0246: The type or namespace name 'ComplianceViolationSignal' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]

FILE Build_EXTERNAL_CODEX_hotpath_cleanup52_cultivation_inventory_rebind_retry1.log bytes=783 errors=1 warnings=0 successMarkers=0 selected=2
- C:\hades\Hecton8\Assets\Project\Scripts\FaunaDirector.cs(534,17): error CS0246: The type or namespace name 'IDynamicResolutionRuntime' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup52_cultivation_inventory_rebind_retry2.log bytes=854 errors=0 warnings=1 successMarkers=0 selected=2
- CSC : warning CS2002: Source file 'C:\hades\Hecton8\Assets\Project\Scripts\Core\Contracts\CoreContractsAssemblyMarker.cs' specified multiple times [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup52_cultivation_inventory_rebind_retry3.log bytes=3682 errors=15 warnings=0 successMarkers=0 selected=16
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(141,24): error CS0103: The name 'ResolveShapeArchetype' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(149,24): error CS0103: The name 'ResolveTerrainSeamMode' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(157,24): error CS0103: The name 'ResolveCaveBlendMode' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(893,38): error CS0103: The name 'ArchetypeArchLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(895,38): error CS0103: The name 'ArchetypeCanopyLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(897,38): error CS0103: The name 'ArchetypeArchClusterLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(899,38): error CS0103: The name 'ArchetypeReefPackLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(901,38): error CS0103: The name 'ArchetypeCaveBridgeLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(909,38): error CS0103: The name 'TerrainSeamHeightBlendLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(911,38): error CS0103: The name 'TerrainSeamSdfBlendLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(913,38): error CS0103: The name 'TerrainSeamDebrisBridgeLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(915,38): error CS0103: The name 'TerrainSeamCarveAndDebrisLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(923,38): error CS0103: The name 'CaveBlendProbeOnlyLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(925,38): error CS0103: The name 'CaveBlendSdfBlendLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\WorldGenerativeGeologyService.cs(927,38): error CS0103: The name 'CaveBlendCarvePortalLabel' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup52_cultivation_inventory_rebind_retry4.log bytes=666 errors=0 warnings=0 successMarkers=0 selected=1
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup53_atmo_thermo_interaction.log bytes=1847 errors=0 warnings=4 successMarkers=0 selected=5
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(1836,46): warning CS0649: Field 'CombatDamageRuntime.EvaluateArmorPenetrationJob.ArmorTuning' is never assigned to, and will always have its default value [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(1834,69): warning CS0649: Field 'CombatDamageRuntime.EvaluateArmorPenetrationJob.TargetArmorProfiles' is never assigned to, and will always have its default value [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(1824,24): warning CS0649: Field 'CombatDamageRuntime.EvaluateArmorPenetrationJob.TargetCount' is never assigned to, and will always have its default value 0 [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(1835,59): warning CS0649: Field 'CombatDamageRuntime.EvaluateArmorPenetrationJob.DamageArmorLut' is never assigned to, and will always have its default value [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup53_atmo_thermo_interaction_retry1.log bytes=728 errors=1 warnings=0 successMarkers=0 selected=2
- C:\hades\Hecton8\Assets\Project\Scripts\AcousticZoneController.cs(3143,20): error CS0103: The name 'HasAnySnapshotBinding' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup53_atmo_thermo_interaction_retry2.log bytes=1855 errors=5 warnings=0 successMarkers=0 selected=6
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalRegistryContracts.cs(3479,32): error CS0246: The type or namespace name 'IDataVault' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalRegistryContracts.cs(3487,32): error CS0246: The type or namespace name 'IDataVault' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalRegistryContracts.cs(3504,13): error CS0246: The type or namespace name 'IDataVault' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\GlobalRegistryContracts.cs(3520,33): error CS0246: The type or namespace name 'IDataVault' could not be found (are you missing a using directive or an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\InputDispatcher.cs(3288,14): error CS0540: 'InputDispatcher.IDispatcherRaycastReceiver.ConsumeDispatcherRaycastHit(int, in RaycastHit)': containing type does not implement interface 'IDispatcherRaycastReceiver' [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup53_atmo_thermo_interaction_retry3.log bytes=779 errors=1 warnings=0 successMarkers=0 selected=2
- C:\hades\Hecton8\Assets\Project\Scripts\ModalWindow.cs(16,92): error CS0535: 'ModalWindow' does not implement interface member 'IModalWindowService.ShowModal(string, char[], int, Action, Action, string, string)' [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup53_basemodule_service_rebind.log bytes=96556 errors=439 warnings=14 successMarkers=8 selected=34
- C:\hades\Hecton8\Assets\Project\Scripts\UI\SettingsManager.cs(1527,37): warning CS0168: The variable 'ex' is declared but never used [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(37,13): error CS0103: The name 'CreateQueue' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(37,29): error CS0103: The name 'impactSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(37,74): error CS0103: The name 'impactSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(38,13): error CS0103: The name 'CreateQueue' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(38,29): error CS0103: The name 'aupPreShiftSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(38,84): error CS0103: The name 'aupPreShiftSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(39,13): error CS0103: The name 'CreateQueue' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(39,29): error CS0103: The name 'aupShiftSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(39,78): error CS0103: The name 'aupShiftSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(40,13): error CS0103: The name 'CreateQueue' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(40,29): error CS0103: The name 'brownoutSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(40,78): error CS0103: The name 'brownoutSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(41,13): error CS0103: The name 'CreateQueue' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(41,29): error CS0103: The name 'debrisSpawnSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(41,84): error CS0103: The name 'debrisSpawnSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(42,13): error CS0103: The name 'CreateQueue' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(42,29): error CS0103: The name 'deflectSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(42,76): error CS0103: The name 'deflectSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(43,13): error CS0103: The name 'CreateQueue' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(43,29): error CS0103: The name 'entityDeathSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(43,84): error CS0103: The name 'entityDeathSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(44,13): error CS0103: The name 'CreateQueue' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(44,29): error CS0103: The name 'solarFlareSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(44,82): error CS0103: The name 'solarFlareSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(45,13): error CS0103: The name 'CreateQueue' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(45,29): error CS0103: The name 'rebaseSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(45,74): error CS0103: The name 'rebaseSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(46,13): error CS0103: The name 'CreateQueue' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(46,29): error CS0103: The name 'controlSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(46,76): error CS0103: The name 'controlSignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(47,13): error CS0103: The name 'CreateQueue' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(47,29): error CS0103: The name 'anomalySignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\Signals\GlobalSignals.RuntimeLifecycle.cs(47,76): error CS0103: The name 'anomalySignals' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]

FILE Build_EXTERNAL_CODEX_hotpath_cleanup54_somatic_quality_retry1.log bytes=9788 errors=44 warnings=0 successMarkers=0 selected=34
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\SomaticKinematicsRuntime.cs(589,52): error CS0103: The name 'SmoothQuality01' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Core\BootstrapContracts\InputBindingServiceContracts.cs(43,26): error CS0234: The type or namespace name 'TickCount' does not exist in the namespace 'Hecton8.Environment' (are you missing an assembly reference?) [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Bootstrap\GameBootstrapper.cs(2046,66): error CS1503: Argument 1: cannot convert from 'Hecton8.Input.InputManager' to 'Hecton8.Core.INativeInputManagerRuntime' [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Bootstrap\GameBootstrapper.cs(3285,61): error CS1503: Argument 1: cannot convert from 'Hecton8.Input.InputManager' to 'Hecton8.Core.INativeInputManagerRuntime' [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Bootstrap\GameBootstrapper.cs(3296,47): error CS1503: Argument 1: cannot convert from 'Hecton8.Input.InputManager' to 'Hecton8.Core.INativeInputManagerRuntime' [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Bootstrap\GameBootstrapper.cs(3311,66): error CS1503: Argument 1: cannot convert from 'Hecton8.Input.InputManager' to 'Hecton8.Core.INativeInputManagerRuntime' [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(655,22): error CS0103: The name 'TryQueueDeferredDeconstructionProbe' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(935,35): error CS0103: The name 'deconstructionRaycastScheduled' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(938,18): error CS0103: The name 'deconstructionRaycastCommands' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(938,63): error CS0103: The name 'deconstructionRaycastHits' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(941,18): error CS0103: The name 'deconstructionRaycastCommands' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(942,18): error CS0103: The name 'deconstructionRaycastHits' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(948,13): error CS0103: The name 'deconstructionRaycastCommands' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(949,13): error CS0103: The name 'deconstructionRaycastHits' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(952,13): error CS0103: The name 'deconstructionRaycastHandle' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(953,17): error CS0103: The name 'deconstructionRaycastCommands' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(954,17): error CS0103: The name 'deconstructionRaycastHits' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(957,13): error CS0103: The name 'deconstructionRaycastScheduled' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(948,49): error CS0165: Use of unassigned local variable 'command' [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(994,18): error CS0103: The name 'deconstructionRaycastScheduled' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(995,61): error CS0103: The name 'deconstructionRaycastHandle' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(1000,13): error CS0103: The name 'deconstructionRaycastScheduled' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(1023,18): error CS0103: The name 'deconstructionRaycastHits' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(1023,58): error CS0103: The name 'deconstructionRaycastHits' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(1026,36): error CS0103: The name 'deconstructionRaycastHits' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(1602,18): error CS0103: The name 'deconstructionRaycastCommands' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(1604,17): error CS0103: The name 'deconstructionRaycastCommands' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(1608,58): error CS0103: The name 'deconstructionRaycastCommands' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(1608,116): error CS0103: The name 'deconstructionRaycastCommands' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(1611,18): error CS0103: The name 'deconstructionRaycastHits' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(1613,17): error CS0103: The name 'deconstructionRaycastHits' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(1617,58): error CS0103: The name 'deconstructionRaycastHits' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(1617,112): error CS0103: The name 'deconstructionRaycastHits' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\ConstructionManager.cs(1735,17): error CS0103: The name 'deconstructionRaycastCommands' does not exist in the current context [C:\hades\Hecton8\Hecton8.Core.csproj]

FILE Build_EXTERNAL_CODEX_hotpath_cleanup59_runtime_binary_tail.log bytes=0 errors=0 warnings=0 successMarkers=0 selected=0

FILE Build_EXTERNAL_CODEX_hotpath_cleanup65_owner_cache.log bytes=1233 errors=3 warnings=3 successMarkers=0 selected=4
- C:\hades\Hecton8\Assets\Project\Scripts\Bootstrap\GameBootstrapper.cs(3436,30): warning CS0168: The variable 'exception' is declared but never used [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Bootstrap\GameBootstrapper.cs(4315,30): warning CS0168: The variable 'exception' is declared but never used [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Bootstrap\GameBootstrapper.cs(4829,30): warning CS0168: The variable 'exception' is declared but never used [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup66_warning_fix.log bytes=2398 errors=0 warnings=6 successMarkers=0 selected=7
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(1912,24): warning CS0649: Field 'CombatDamageRuntime.CombatDamageTortureJob.Count' is never assigned to, and will always have its default value 0 [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(1922,59): warning CS0649: Field 'CombatDamageRuntime.CombatDamageTortureJob.DamageArmorLut' is never assigned to, and will always have its default value [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(1914,25): warning CS0649: Field 'CombatDamageRuntime.CombatDamageTortureJob.SourceHash' is never assigned to, and will always have its default value 0 [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(1913,24): warning CS0649: Field 'CombatDamageRuntime.CombatDamageTortureJob.TargetCount' is never assigned to, and will always have its default value 0 [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(1923,46): warning CS0649: Field 'CombatDamageRuntime.CombatDamageTortureJob.ArmorTuning' is never assigned to, and will always have its default value [C:\hades\Hecton8\Hecton8.Core.csproj]
- C:\hades\Hecton8\Assets\Project\Scripts\Gameplay\Combat\HectonCombatRuntimeArmorPenetration.cs(1921,69): warning CS0649: Field 'CombatDamageRuntime.CombatDamageTortureJob.TargetArmorProfiles' is never assigned to, and will always have its default value [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE Build_EXTERNAL_CODEX_hotpath_cleanup86_player_kinematics_rebind.log bytes=729 errors=1 warnings=0 successMarkers=0 selected=2
- C:\hades\Hecton8\Assets\Project\Scripts\RepairTool.cs(52,64): error CS0535: 'RepairTool' does not implement interface member 'ILateFrameTickable.LateFrameTick()' [C:\hades\Hecton8\Hecton8.Core.csproj]
- Unity.RenderPipelines.Universal.Runtime - C:\hades\Hecton8\Temp\bin\Debug\Unity.RenderPipelines.Universal.Runtime.dll

FILE CompileWall_X_003_Archaeology.json bytes=801500 raw=json archived-intact

FILE CompileWall_X_003_Archaeology.md bytes=16970 errors=8 warnings=0 successMarkers=0 selected=34
- Compile Wall X003 Static Archaeology
- Evidence class: STATICSOURCE. No Unity import, C# compile, runtime wiring, GC, profiler, or player build proof.
- | Assembly | Blast Radius | Direct Inbound | Outbound | First-Party Outbound |
- | Type | Kind | Assembly | External Assemblies | External Domains | Path |
- | BufferID | enum | Hecton8.Core.Memory | 71 | 50 | Assets/Project/Scripts/Core/Memory/H8Memory.cs:89 |
- | IDataVault | interface | Hecton8.Core.Memory | 74 | 49 | Assets/Project/Scripts/Core/Memory/GlobalDataVault.cs:29 |
- | VaultGenerationHandle | struct | Hecton8.Core.Memory | 70 | 49 | Assets/Project/Scripts/Core/Memory/GlobalDataVault.cs:224 |
- | Result | struct | Hecton8.Core | 13 | 10 | Assets/Project/Scripts/World/PlanetaryCanvasSmokeTester.cs:14 |
- | CombatDamageSignal | struct | Hecton8.Core | 11 | 10 | Assets/Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1656 |
- | AcousticPingSignal | struct | Hecton8.Core | 10 | 8 | Assets/Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:875 |
- | MockWorldSampler | struct | Hecton8.VFX.Debris | 4 | 8 | Assets/Project/Scripts/VFX/Debris/ShinobuDeltaCrusherJobs.cs:77 |
- | DebrisSpawnSignal | struct | Hecton8.Core | 9 | 7 | Assets/Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:387 |
- | Domain | Count |
- | Hecton8.Systems | 19 |
- | GetComponent | AcousticZoneController | Hecton8.Core | Assets/Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:2573 |
- | explicit | IntPtr | Hecton8.Core | Assets/Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:258 |
- | explicit | IntPtr | Hecton8.Core | Assets/Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:259 |
- | explicit | IntPtr | Hecton8.Core | Assets/Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:260 |
- | explicit | IntPtr | Hecton8.Core | Assets/Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:261 |
- | GetComponent | T | Hecton8.Core | Assets/Project/Scripts/Bootstrap/HectonLoreSystemsRoot.cs:251 |
- | as | ResourceNode | Hecton8.Core | Assets/Project/Scripts/Construction/AutonomousExtractorSystem.cs:1281 |
- | GetComponent | ResourceNode | Hecton8.Core | Assets/Project/Scripts/Construction/AutonomousExtractorSystem.cs:1383 |
- | is | UnauthorizedAccessException | Hecton8.Core | Assets/Project/Scripts/Construction/BulkheadContainmentRuntime.cs:1734 |
- | is | ArgumentException | Hecton8.Core | Assets/Project/Scripts/Construction/BulkheadContainmentRuntime.cs:1735 |
- | is | NotSupportedException | Hecton8.Core | Assets/Project/Scripts/Construction/BulkheadContainmentRuntime.cs:1736 |
- | explicit | IntegrityFailureReasonCode | Hecton8.Core | Assets/Project/Scripts/Construction/HabitatConstructionManager.cs:392 |
- | explicit | LogisticsModuleStatusBits | Hecton8.Core | Assets/Project/Scripts/Construction/HabitatGraphManager.cs:4643 |
- | is | UnauthorizedAccessException | Hecton8.Core | Assets/Project/Scripts/Core/Content/ContentLoreBinaryProvider.cs:151 |
- | is | NotSupportedException | Hecton8.Core | Assets/Project/Scripts/Core/Content/ContentLoreBinaryProvider.cs:152 |
- | is | ArgumentException | Hecton8.Core | Assets/Project/Scripts/Core/Content/ContentLoreBinaryProvider.cs:153 |
- | is | UnauthorizedAccessException | Hecton8.Core | Assets/Project/Scripts/Core/Content/ContentLoreBinaryProvider.cs:369 |
- Source Using Domain Audit
- Cross-domain using edges: 538
- Cross-domain using directives: 3600

FILE CompileWallX003Audit.md bytes=16993 errors=8 warnings=0 successMarkers=0 selected=34
- Compile Wall X003 Static Archaeology
- Evidence class: STATICSOURCE. No Unity import, C# compile, runtime wiring, GC, profiler, or player build proof.
- | Assembly | Blast Radius | Direct Inbound | Outbound | First-Party Outbound |
- | Type | Kind | Assembly | External Assemblies | External Domains | Path |
- | BufferID | enum | Hecton8.Core.Memory | 71 | 50 | Assets/Project/Scripts/Core/Memory/H8Memory.cs:89 |
- | IDataVault | interface | Hecton8.Core.Memory | 74 | 49 | Assets/Project/Scripts/Core/Memory/GlobalDataVault.cs:29 |
- | VaultGenerationHandle | struct | Hecton8.Core.Memory | 70 | 49 | Assets/Project/Scripts/Core/Memory/GlobalDataVault.cs:224 |
- | Result | struct | Hecton8.Core | 13 | 10 | Assets/Project/Scripts/World/PlanetaryCanvasSmokeTester.cs:14 |
- | CombatDamageSignal | struct | Hecton8.Core | 11 | 10 | Assets/Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:1656 |
- | AcousticPingSignal | struct | Hecton8.Core | 10 | 8 | Assets/Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:875 |
- | MockWorldSampler | struct | Hecton8.VFX.Debris | 4 | 8 | Assets/Project/Scripts/VFX/Debris/ShinobuDeltaCrusherJobs.cs:77 |
- | DebrisSpawnSignal | struct | Hecton8.Core | 9 | 7 | Assets/Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs:387 |
- | Domain | Count |
- | Hecton8.Systems | 19 |
- | GetComponent | AcousticZoneController | Hecton8.Core | Assets/Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:2558 |
- | explicit | IntPtr | Hecton8.Core | Assets/Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:258 |
- | explicit | IntPtr | Hecton8.Core | Assets/Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:259 |
- | explicit | IntPtr | Hecton8.Core | Assets/Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:260 |
- | explicit | IntPtr | Hecton8.Core | Assets/Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:261 |
- | GetComponent | T | Hecton8.Core | Assets/Project/Scripts/Bootstrap/HectonLoreSystemsRoot.cs:251 |
- | as | ResourceNode | Hecton8.Core | Assets/Project/Scripts/Construction/AutonomousExtractorSystem.cs:1262 |
- | GetComponent | ResourceNode | Hecton8.Core | Assets/Project/Scripts/Construction/AutonomousExtractorSystem.cs:1364 |
- | is | UnauthorizedAccessException | Hecton8.Core | Assets/Project/Scripts/Construction/BulkheadContainmentRuntime.cs:1735 |
- | is | ArgumentException | Hecton8.Core | Assets/Project/Scripts/Construction/BulkheadContainmentRuntime.cs:1736 |
- | is | NotSupportedException | Hecton8.Core | Assets/Project/Scripts/Construction/BulkheadContainmentRuntime.cs:1737 |
- | explicit | IntegrityFailureReasonCode | Hecton8.Core | Assets/Project/Scripts/Construction/HabitatConstructionManager.cs:392 |
- | explicit | LogisticsModuleStatusBits | Hecton8.Core | Assets/Project/Scripts/Construction/HabitatGraphManager.cs:4629 |
- | is | UnauthorizedAccessException | Hecton8.Core | Assets/Project/Scripts/Core/Content/ContentLoreBinaryProvider.cs:151 |
- | is | NotSupportedException | Hecton8.Core | Assets/Project/Scripts/Core/Content/ContentLoreBinaryProvider.cs:152 |
- | is | ArgumentException | Hecton8.Core | Assets/Project/Scripts/Core/Content/ContentLoreBinaryProvider.cs:153 |
- | is | UnauthorizedAccessException | Hecton8.Core | Assets/Project/Scripts/Core/Content/ContentLoreBinaryProvider.cs:369 |
- Source Using Domain Audit
- Cross-domain using edges: 564
- Cross-domain using directives: 3637

FILE DataVaultSovereigntyAudit_X_000_pre_roslyn_blocked.md bytes=29314 errors=4 warnings=1 successMarkers=0 selected=34
- DataVault Sovereignty Audit - VAULTSOVEREIGNTYENFORCER
- Schema: hecton8.datavaultsovereigntyaudit.v3
- Status: BLOCKEDBASELINEMISSING
- Pattern: \bnew\s+NativeArray\s<
- Baseline: Docs/AgentLogs/DataVaultSovereigntyBaselineVAULTSOVEREIGNTYENFORCER.json
- | Total direct new NativeArray<T constructors | 1276 |
- | Total field-like NativeArray<T declarations | 6577 |
- | Allowed DataVault/H8Memory declarations | 5263 |
- | Persistent owner native collection declarations | 1050 |
- | Job input native collection declarations | 4651 |
- | Burst job input native collection declarations | 4651 |
- | Native view/payload/kernel struct declarations | 569 |
- | Unknown struct native collection declarations | 281 |
- | 6 | Assets/Project/Scripts/Construction/AutonomousExtractorSystem.cs | 488, 489, 490, 491, 492, 493 |
- | 25 | Assets/Project/Scripts/Power/ShinobuLogisticsRouter.cs | 211, 212, 213, 214, 215, 216, 217, 218, ... |
- | 14 | Assets/Project/Scripts/Core/DodReplayRecorder.cs | 381, 382, 383, 384, 385, 386, 387, 388, ... |
- | 7 | Assets/Project/Scripts/Construction/LogisticsRouteScratchMemory.cs | 18, 19, 20, 21, 22, 23, 24 |
- | 7 | Assets/Project/Scripts/Gameplay/Combat/CombatDamageRuntimeStatusEffects.cs | 55, 56, 57, 58, 59, 60, 61 |
- | 2 | Assets/Project/Scripts/Editor/WorldProceduralProxySceneBuilder.cs | 273, 274 |
- | 1 | Assets/Project/Scripts/Editor/HydraulicErosionForge/Shinobu242/HydraulicErosionWeatheringCsv.cs | 39 |
- Allowed DataVault/H8Memory Declaration Sites
- | 37 | Assets/Project/Scripts/Fauna/PredatorCognitionDomain.cs | 5410, 5411, 5412, 5413, 5414, 5415, 5416, 5417, ... |
- | 21 | Assets/Project/Scripts/Fauna/PredatorCognitionDomain.cs | 5069, 5071, 5072, 5073, 5074, 5075, 5076, 5077, ... |
- | 20 | Assets/Project/Scripts/World/ProceduralCoral/ProceduralCoralVault.cs | 66, 67, 68, 69, 70, 71, 72, 73, ... |
- | 19 | Assets/Project/Scripts/Construction/ShinobuSocketConstructionData.cs | 251, 252, 253, 254, 255, 256, 257, 258, ... |
- | 18 | Assets/Project/Scripts/World/ProceduralWreckage/ProceduralWreckageVault.cs | 62, 63, 64, 65, 66, 67, 68, 69, ... |
- | 18 | Assets/Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs | 62, 63, 64, 65, 66, 67, 68, 69, ... |
- | 17 | Assets/Project/Scripts/Gameplay/Combat/CombatDamageRuntimeStatusEffects.cs | 1511, 1512, 1513, 1514, 1515, 1516, 1517, 1518, ... |
- | 17 | Assets/Project/Scripts/Inventory/Algorithms/InventoryDefragJob.cs | 30, 31, 32, 33, 34, 35, 36, 37, ... |
- | 17 | Assets/Project/Scripts/Power/ShinobuLogisticsRouter.cs | 2700, 2701, 2705, 2706, 2707, 2708, 2709, 2710, ... |
- | 16 | Assets/Project/Scripts/Power/ShinobuLogisticsRouter.cs | 2092, 2093, 2094, 2095, 2096, 2097, 2098, 2099, ... |
- | 15 | Assets/Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs | 85, 86, 87, 88, 89, 90, 91, 92, ... |
- | 15 | Assets/Project/Scripts/Construction/FoundationSnappingCalculatorData.cs | 187, 188, 189, 190, 191, 192, 193, 194, ... |
- | 15 | Assets/Project/Scripts/Gameplay/ScannerDataMiningRouter.cs | 456, 457, 458, 459, 460, 461, 462, 463, ... |

FILE Dump_DATA_MONOLITH.bin bytes=19220 raw=bin archived-intact

FILE Dump_SHINOBU_103.bin bytes=19220 raw=bin archived-intact

FILE Dump_SHINOBU_202.bin bytes=152 raw=bin archived-intact

FILE Dump_X_002.bin bytes=19220 raw=bin archived-intact

FILE localization_recover1.patch bytes=906 errors=0 warnings=0 successMarkers=0 selected=8
- diff --git a/Assets/Project/Scripts/LocalizedTextReference.cs b/Assets/Project/Scripts/LocalizedTextReference.cs
- a/Assets/Project/Scripts/LocalizedTextReference.cs
- +++ b/Assets/Project/Scripts/LocalizedTextReference.cs
- @@ -88,1 +88,1 @@
- LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
- + LocalizationManager manager = Hecton.Localization.LocalizationManager.ActiveRuntimeInstance;
- diff --git a/Assets/Project/Scripts/Narrative/CorporateOrderSystem.cs b/Assets/Project/Scripts/Narrative/CorporateOrderSystem.cs
- a/Assets/Project/Scripts/Narrative/CorporateOrderSystem.cs

FILE LOG_ARCHIVE_12_REENTRY.md bytes=550 errors=0 warnings=0 successMarkers=0 selected=4
- What was wrong: active hygiene folders held stale task/log/rationale/build artifacts; existing Batch012 archive could not be overwritten safely.
- What was done: process scan performed; no Hecton8 build/Unity compiler processes found; created Batch012Reentry20260525143323; generated TASKS/LOGS/RATIONALE summaries; moved active evidence intact except CURRENTBATCH.md.
- Cinematic Cheats used: none; filesystem hygiene only.
- Exact Microseconds saved: 0 runtime; context/file-read volume reduced by summary artifacts.

FILE LOG_AUTOFIX.md bytes=2325 errors=2 warnings=1 successMarkers=1 selected=19
- Status: STATIC PASS COMPLETE / COMPILE BLOCKED BY CPU GUARD / PENDING UNITY VERIFICATION
- Runtime and development diagnostics were inconsistent across multiple systems.
- Selected runtime/cold-failure paths used naked Debug.Log / Debug.LogException.
- Some diagnostics built interpolated or concatenated strings before the Unity logger call.
- Touched exactly 40 source files, inside the user's requested 20-40 file window.
- Did not edit scenes, prefabs, assets, project settings, public interfaces, DTO layout, save identity, gameplay truth, or global authority routes.
- Created/updated Docs/Tasks/StatusAUTOFIX.md and Docs/AgentLogs/RationaleAUTOFIX.md.
- Cinematic Cheats used:
- Exact microseconds saved:
- Measured: PENDING VERIFICATION. No Unity profiler/player artifact.
- Static estimate: 2-12 us per emitted diagnostic path, plus avoided release string construction where arguments are interpolated/concatenated.
- Hot-path GC proof: static only. H8Debug conditional calls are omitted from non-editor/non-development builds, so call arguments are not evaluated in release.
- Verification:
- git diff --check on the touched files produced no whitespace errors; Git emitted LF/CRLF warnings only.
- Build not run: CPU average 77.1%, exceeding the AGENTS 50% build guard. No active dotnet/csc process was found.
- CPU: release diagnostic paths get cheaper; dev builds keep diagnostics.
- GC: release avoids diagnostic string argument construction at converted sites.
- Correctness: logs are not gameplay truth; black-box/telemetry routes remain untouched.
- Risk: if a release player relied on Unity console logs for user-visible failure handling, that path was already wrong; it still needs UI/signal handling in a separate task.

FILE LOG_AUTOFIX2.md bytes=2201 errors=1 warnings=2 successMarkers=0 selected=13
- 28 runtime/dev smoke, UI fallback, visual validation, and scatter diagnostic files still called UnityEngine.Debug.Log directly.
- These were diagnostic surfaces, not gameplay truth routes, and should use the project-owned compile-stripped facade.
- Converted direct Debug.LogWarning, Debug.LogError, and Debug.LogException to Hecton8.Core.H8Debug in:
- Dev/CelestialCataclysmSmokeTester.cs, Dev/ShellVerificationRuntimeSmokeTester.cs,
- ToolTrialRangeRuntimeSmokeTester.cs, UI/PauseControlsPanel.cs, UI/RelayHUDRuntimeBootstrap.cs,
- Cinematic cheats used:
- No simulation added. Diagnostic visibility stays in editor/development builds through H8Debug; release-player log noise is stripped instead of simulated/observed at runtime.
- Proof artifacts:
- git diff --check exit 0; only LF/CRLF normalization warnings reported.
- Build gate: no dotnet/csc process listed; CPU average was 52.1%, so AGENTS.md forbids launching build.
- Exact microseconds saved:
- Measured: PENDING VERIFICATION because build/profiler run was blocked by CPU gate.
- Static estimate: 0 us steady-frame in normal gameplay; savings are avoided release-player diagnostic call/string surface on the converted cold/dev/fallback paths.

FILE LOG_AUTOFIX3.md bytes=1114 errors=2 warnings=1 successMarkers=0 selected=12
- Event/smoke/UI/visual/fallback diagnostics still used direct UnityEngine.Debug.Log in another 34 source files.
- H8Debug could not preserve Unity object context for exception logs.
- Added Hecton8.Core.H8Debug.LogException(Exception, UnityEngine.Object).
- Cinematic cheats used:
- No simulation or new runtime effect added. Diagnostics stay editor/development-only; release-player builds do not pay for these selected diagnostic paths.
- Proof artifacts:
- Scoped identifier-bound rg over converted files returned no direct Unity Debug.Log matches.
- git diff --check exit 0; only LF/CRLF normalization warnings.
- Build gate: CPU average 70.8%; AGENTS.md forbids launching build above 50%. No compiler process was listed in the gate output.
- Exact microseconds saved:
- Measured: PENDING VERIFICATION because build/profiler run was blocked by CPU gate.
- Static estimate: 0 us steady-frame gameplay; release-player diagnostic path is stripped by conditional facade calls.

FILE LOG_AUTOFIX4.md bytes=1550 errors=4 warnings=3 successMarkers=1 selected=15
- Domain: Cross-domain diagnostic/runtime hygiene
- Status: DONE - STATIC VERIFIED / BUILD GATED
- Runtime and validation files still used direct Unity Debug. calls, bypassing the project-owned diagnostic facade.
- Several paths were black-box dump failures, DTO layout validators, profiler warnings, and visual/quality fallback diagnostics. The messages were useful; the route was the problem.
- Converted direct Debug.LogWarning, Debug.LogError, Debug.LogException, and UnityEngine.Debug. calls to Hecton8.Core.H8Debug in 32 source files.
- Preserved message text, context objects, exception routes, compile-time guards, and gameplay behavior.
- Updated Docs/Tasks/StatusAUTOFIX4.md and Docs/AgentLogs/RationaleAUTOFIX4.md.
- Cinematic Cheats used:
- Existing fake-first diagnostics around meteor splash, render targets, black-box dump failure, visual pressure aging, and bilateral DRS remained evidence-only diagnostics.
- Exact Microseconds saved:
- Accepted runtime savings: 0 us. No profiler proof was produced.
- Static estimate only: tens to low hundreds of microseconds during clustered diagnostic/fallback storms on i3/MX350, pending profiler/GC verification.
- Verification:
- git diff --check on touched files: exit 0, line-ending warnings only.
- Build: not launched. CPU sample was 64.18%, above the 50% AGENTS gate. No dotnet/csc process was active.

FILE LOG_AUTOFIX5.md bytes=3538 errors=3 warnings=2 successMarkers=1 selected=21
- Domain: Cross-domain diagnostic/runtime hygiene
- Status: DONE - STATIC VERIFIED / BUILD GATED
- 32 existing source files still emitted direct Unity Debug. diagnostics in runtime, smoke, validation, audio, world, input, memory, construction, and fallback paths.
- These calls bypassed the project-owned diagnostic facade and kept route ownership fragmented.
- Converted direct Debug.LogWarning, Debug.LogError, Debug.LogException, and UnityEngine.Debug. calls to Hecton8.Core.H8Debug.
- Preserved message text, exception visibility, context objects, compile guards, fallback branches, DTO validation behavior, and lifecycle behavior.
- Updated Docs/Tasks/StatusAUTOFIX5.md and Docs/AgentLogs/RationaleAUTOFIX5.md.
- Files changed:
- Assets/Project/Scripts/AcousticZoneController.cs
- Assets/Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationContracts.cs
- Assets/Project/Scripts/Audio/HectonMusicDirector.cs
- Assets/Project/Scripts/BuilderRuntimeSmokeTester.cs
- Assets/Project/Scripts/Core/NativeMemorySentinel.cs
- Cinematic Cheats used:
- Existing fake-first and fallback diagnostics remain diagnostic-only: world scatter fallback, cave graph validation, audio reverb fallback, seam dither material failure, and smoke-test failures still report through the same visible messages.
- Exact Microseconds saved:
- Accepted runtime savings: 0 us. No profiler proof was produced.
- Static estimate only: up to several hundred microseconds during clustered development/fallback diagnostic storms on i3/MX350, pending profiler/GC proof.
- Verification:
- git diff --check on touched files: exit 0, LF/CRLF warnings only.
- Build: not launched. CPU sample was 91.50%, above the 50% AGENTS gate. No dotnet/csc process was active.

FILE LOG_AUTOFIX6.md bytes=3782 errors=3 warnings=3 successMarkers=1 selected=16
- Direct Unity diagnostics remained in first-party runtime files across flow-field, fauna, physics, scene runtime, signal lanes, voxel, underwater visuals, survival, pooling, narrative, interaction, seam, and save-event code. These calls bypass the central H8Debug policy and make release-path diagnostic allocation risk harder to prove.
- Replaced direct Debug.LogWarning, Debug.LogError, and Debug.LogException / UnityEngine.Debug.LogException call sites with Hecton8.Core.H8Debug in 32 C# files. Preserved messages, context objects, exception payloads, and control flow. No public API, YAML, prefab, scene, asset, package, project setting, simulation, save identity, DTO, or authority route changes.
- Files changed:
- Assets/Project/Scripts/Core/Signals/SignalBusRuntime.cs
- Assets/Project/Scripts/Core/BootstrapContracts/BootstrapStatus.cs
- Cinematic cheats used:
- None. This was runtime hygiene, not presentation/simulation work. The cheap path was to centralize diagnostics instead of changing systems.
- Exact microseconds saved:
- Static-only estimate: 0us in normal non-fault gameplay frames. Fault/debug paths now route through conditional H8Debug; any actual saved CPU/GC requires Unity Profiler/GCMonitor proof. Status: PENDING VERIFICATION.
- Verification:
- Scoped direct-debug call-site scan: clean. No matches for ^\s(UnityEngine\.)?Debug\.(LogWarning|LogError|LogException) across the 32 edited files.
- H8Debug routed call count: 74.
- git diff --check: exit 0. Git reported LF-CRLF working-copy warnings only.
- Build: not run. Gate blocked by CPU=74 and active dotnet process 64580.
- Unity runtime/profiler: not run. PENDING external Unity artifact.
- CPU: neutral outside diagnostic fault paths. GC: lower policy risk due conditional facade, but measured proof absent. Memory/VRAM: no change. Cadence: no dispatcher/tick route changed. Correctness: diagnostic messages and contexts preserved. Failure mode: if H8Debug facade signature changes, compile fails; current signature supports message/context/exception overloads.

FILE LOG_AUTOFIX7.md bytes=3793 errors=4 warnings=3 successMarkers=1 selected=17
- 2026-05-25 Runtime Diagnostic Route Cleanup
- Another first-party runtime slice still used direct Unity Debug.Log, Debug.LogWarning, Debug.LogError, and Debug.LogException calls. That bypasses the central H8Debug facade and leaves diagnostic stripping/allocation policy inconsistent across content, render-target, input, player, save, physics, and world systems.
- Routed direct diagnostics through Hecton8.Core.H8Debug in 32 C# files. Preserved original messages, context objects, exception text, and control flow. No public signatures, DTOs, save identity, signal lanes, DataVault ownership, dispatcher phases, prefabs, scenes, YAML, project settings, packages, or third-party assets were changed.
- Files changed:
- Assets/Project/Scripts/Input/UserOptionsPersistence.cs
- Assets/Project/Scripts/PlayerBuilder.cs
- Cinematic Cheats used:
- None. This was not simulation/presentation work. The cheap route was source-only diagnostic centralization instead of runtime system redesign.
- Exact Microseconds saved:
- Static-only estimate: 0us in normal non-fault gameplay frames. Fault/development paths now route through the conditional facade. Actual CPU/GC savings require Unity Profiler/GCMonitor proof. Status: PENDING VERIFICATION.
- Verification:
- Scoped direct-debug scan: clean. No matches for ^\s(UnityEngine\.)?Debug\.(Log|LogWarning|LogError|LogException) across the 32 edited C# files.
- H8Debug routed call count: 123.
- git diff --check: exit 0. Git emitted LF-CRLF working-copy warnings only.
- Build: not run. Gate blocked by CPU=93 plus active csc process 56240 and dotnet process 50252.
- Unity runtime/profiler: not run. PENDING external Unity artifact.
- CPU: neutral outside diagnostic fault/development paths. GC: lower release-policy risk due central conditional facade, but measured proof absent. Memory/VRAM: no change. Cadence: no dispatcher/tick route changed. Correctness: diagnostic messages and contexts preserved. Failure mode: if H8Debug facade changes, compile will catch routed call mismatches.

FILE LOG_AUTOFIX8.md bytes=4178 errors=4 warnings=2 successMarkers=7 selected=22
- Domain: Cross-domain runtime hygiene
- 32 first-party runtime source files still emitted direct Debug.Log or Debug.LogException.
- Direct Unity diagnostics bypass the project diagnostic facade, weakening release stripping policy and making future allocation/log-spam audits harder.
- Replaced direct Debug.Log, Debug.LogWarning, Debug.LogError, Debug.LogException, and UnityEngine.Debug. calls with Hecton8.Core.H8Debug..
- Preserved original message strings, context objects, exception payloads, branch structure, public signatures, serialized fields, YAML, prefabs, scenes, packages, save identities, dispatcher phases, and gameplay truth ownership.
- No new simulation, jobs, registries, scene searches, events, allocations, quality switches, or visual systems were added.
- Files changed:
- Assets/Project/Scripts/Visor/DynamicDecalVaultRuntime.cs
- Assets/Project/Scripts/Core/BootstrapContracts/BootstrapStatus.cs
- Cinematic Cheats used:
- Exact microseconds saved:
- Normal gameplay frame: 0us claimed. This was a diagnostic-route hardening pass, not a measured frame-time optimization.
- Verification:
- Scoped direct-debug scan over the 32 AUTOFIX8 source files: PASS. No Debug.Log or Debug.LogException remained in that set.
- Routed diagnostic count in the same set: PASS. 133 Hecton8.Core.H8Debug. calls found.
- git diff --check over tracked AUTOFIX8 source/docs paths: PASS, exit code 0. Git reported LF to CRLF normalization warnings only.
- New AUTOFIX8 docs are untracked, so they were checked separately with trailing-whitespace scan: PASS.
- Build/compile: NOT RUN. Gate result was CPU 84%, dotnet/csc process count 0. AGENTS.md forbids build above 50% CPU.
- Unity runtime/profiler: PENDING external Unity artifact.
- Status:
- Static source verification complete.
- Compile and runtime validation remain pending by local build-gate law, not by choice.

FILE LOG_AUTOFIX9.md bytes=5686 errors=5 warnings=2 successMarkers=6 selected=34
- Domain: Cross-domain editor/diagnostic hygiene
- 32 first-party editor/diagnostic source files still emitted direct Debug.Log or Debug.LogException.
- Replaced actual line-start direct Debug.Log, Debug.LogWarning, Debug.LogError, Debug.LogException, and UnityEngine.Debug. calls with Hecton8.Core.H8Debug..
- Preserved scanner pattern strings, report content, context objects, exception payloads, public signatures, serialized data, YAML, prefabs, scenes, packages, and runtime authority.
- No gameplay logic, dispatcher phase, SignalBus lane, DataVault ownership, quality tier, visual system, or simulation route was changed.
- Files changed:
- Assets/Project/Scripts/Audio/Editor/VocalWarningStormTortureX011.cs
- Assets/Project/Scripts/Audio/Editor/ShinobuAcousticDspSmokeTester.cs
- Assets/Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs
- Assets/Project/Scripts/Editor/AssemblyGuard/CompileWallXRayWindow.cs
- Assets/Project/Scripts/Construction/Editor/FoundationSnappingCalculatorEditor.cs
- Cinematic Cheats used:
- Exact microseconds saved:
- Normal gameplay frame: 0us claimed. This was editor/diagnostic route hardening, not a measured frame-time optimization.
- Editor/static-gate benefit: reduced direct Unity diagnostic surface by 88 actual call-site lines in this slice.
- Verification:
- Scoped direct-debug scan over the 32 AUTOFIX9 source files: PASS. No line-start Debug.Log, Debug.LogException, or UnityEngine.Debug. calls remain in that set.
- Routed diagnostic count in the same set: PASS. 89 Hecton8.Core.H8Debug. calls found after the rewrite.
- git diff --check over tracked selected source/docs paths: PASS, exit code 0.
- New AUTOFIX9 docs passed separate trailing-whitespace scan: PASS.
- CPU/process build gate before compile attempt: PASS, CPU 18%, dotnet/csc process count 0.
- Compile attempt: dotnet build Assembly-CSharp-Editor.csproj --no-restore -m:2 /nr:false /p:UseSharedCompilation=false failed before C# diagnostics with NETSDK1004 missing Temp/obj/Assembly-CSharp-Editor/project.assets.json.
- Build log: Docs/AgentLogs/BuildAUTOFIX9Assembly-CSharp-Editor.log.
- Generated-project coverage gap: current generated .csproj files do not list the selected editor scripts. Unity/project regeneration is required before this slice can receive generated-project compile coverage.
- Unity runtime/profiler: PENDING external Unity artifact.
- CPU: no runtime CPU path changed.
- GC: no gameplay allocation path added; editor diagnostics remain editor-only.
- Memory: no runtime memory ownership changed.
- Cadence: no dispatcher phase, tick cadence, coroutine, or job route changed.
- Correctness: scanner report strings and diagnostic payloads were preserved; only the console emission route changed.
- Status:
- Static source verification complete.
- Compile proof blocked by generated-project/dependency state, not by a C# diagnostic from this change.
- Runtime readiness remains PENDING VERIFICATION.

FILE LOG_DataVaultNativeOwnershipAuditor.md bytes=1934 errors=0 warnings=0 successMarkers=0 selected=17
- LOGDataVaultNativeOwnershipAuditor
- 2026-05-25 DataVault/native ownership read-only audit.
- HazardZoneManager dirty migration removed local Persistent NativeArray/NativeList ownership, but scheduled Burst jobs over GlobalDataVault-resolved NativeArray views without using GlobalDataVault.TryLockBuffer/TryUnlockBuffer.
- GlobalDataVault TryAcquireWriteLock is not an external job pointer pin and does not set BlockFlagLocked/Reserved1 or ActiveBurstLockMask.
- HazardZoneManager public read model still resolves mutable vault views through HazardVaultArray indexers/properties in GetHazardIntensity/TrySampleHazardAvoidance paths.
- GlobalDataVault TryAcquireWriteLock does not enforce caller SystemID equals handle/meta owner outside collections checks.
- ReleaseBuffer rejects stale generations; HazardVaultArray.ReleaseBuffer clears descriptors without checking release success.
- Reviewed AGENTS.md, domain boundary, and mandates: GlobalRegistry DI, execution phases, signal lane segregation, native memory/jobs, zero GC, crash telemetry.
- Ran git status, git log since 2026-05-22, and targeted rg gates for TryGetLatestCreated, TryReadHandle, TryAcquireWriteLock, ReleaseBuffer, Allocator.Persistent.
- Verified findings against line-numbered source in HazardZoneManager.cs and GlobalDataVault.cs.
- Cinematic cheats used:
- Exact microseconds saved:
- 0 us direct runtime change.
- Estimated potential if fixed: hazard point-sample read path can avoid repeated vault handle metadata resolution in O(N) loops; unmeasured microsecond-scale on i3/MX350.
- Estimated potential if fixed: DataVault relocation against live job pointers avoids catastrophic hitch/crash path; no legitimate average-frame number without profiler proof.
- Build/verification:
- No build run. Prompt was read-only audit; no compile proof claimed.

FILE LOG_DENTE.md bytes=111918 errors=14 warnings=66 successMarkers=268 selected=34
- Patient forms request omitted medicalinterventionrefusal from quick UI/document task actions.
- Telegram control UI showed weak source states: raw link-code statuses, no visual-card thumbnail preview, and hidden outbox warnings/block reasons.
- Added refusal form support to patient forms quick workflow labels.
- Added Russian fallback labels for Telegram statuses, feature-disabled messages, blocked reasons, and link-code states.
- Added visual-card image previews and outbox warning/block reason rendering.
- Added first-open appointment draft guard so saved schedule defaults seed until the user edits the draft.
- Cinematic Cheats used:
- Bounded visual-card preview: browser renders compact image thumbnail instead of building a separate rich preview surface.
- Exact Microseconds saved:
- 0 us claimed. No profiler artifact. This was product correctness and UX hardening, not measured performance work.
- Evidence:
- npm run build
- npm run smoke:russian-fallback-source
- Screenshot capture mode in smoke-mobile-overflow.mjs hung in headless Edge; overflow smoke without screenshot passed.
- Vite still warns that assets/index-.js is above 500 kB after minification.
- Tax application payload could use all eligible fiscal payments instead of the checkboxes explicitly selected by the operator.
- Tax selection useEffect silently selected every eligible payment again after year/payer/form changes.
- Tax payer INN comparison used raw trimmed strings, so formatted operator input could fail against unformatted ledger data.
- Changed tax payment selection refresh to prune invalid ids only; explicit Ð’ÑÐµ remains as a visible button action.
- Updated architecture/UX docs with explicit fiscal selection, payer-fact ownership, and same-day schedule hint rules.
- 0 us claimed. No profiler artifact. Runtime impact is bounded list filtering over visible payments and adjacent schedule rows.
- Vite still warns that main app chunk is above 500 kB after minification.
- GET /api/telegram/outbox returned one local slice and computed ready/due/blocked counts from that slice, not from the real queue.
- Settings filtered Telegram outbox items in the browser and hid everything after a short UI slice, which would feel wrong for real reminder batches.
- Bulk due-send selected from the same unfiltered page instead of asking the queue for due items.
- The Telegram smoke had a time-dependent false positive because a generated appointment minute could contain 36, the same string used as a tooth-number leak sentinel.
- Added outbox query options for status, templateKind, limit, and cursor.
- Moved outbox status/template filtering and count-before-page calculation into the API.
- Cursor paging is a cheap queue viewport instead of building a heavy real-time queue UI.
- Onboarding reuses the schedule readiness DTO instead of duplicating schedule math in React.
- 0 us claimed. No profiler artifact. This is correctness/scalability work over bounded Node arrays and React state.
- Link-code/chat-link lists still use fixed small latest lists; they need the same status/subject/cursor contract next.
- Multi-clinic Telegram runtime still uses one active settings object; full per-organization settings/runtime resolver remains open.
- Settings could not page through real clinic connection history or see reliable filtered totals for pending/used/expired/revoked codes and active/revoked chat links.

FILE LOG_EXTERNAL_CODEX.md bytes=175607 errors=28 warnings=126 successMarkers=170 selected=34
- 2026-05-23 External Integration Pass
- Generated project reference pruner removed missing Library/ScriptAssemblies references. This erased valid local asmdef dependencies before Unity produced DLLs and caused Hecton8.Habitat.Deformation.Contracts to disappear from Hecton8.Core.csproj.
- ShaderCompassRibbon cached GlobalRegistry.InertialNavigation only during OnEnable/Start. If the navigation runtime registered after UI boot, the compass stayed hidden until component restart.
- Added Assets/Project/Tests/Editor/HectonGeneratedProjectReferencePrunerEditTests.cs to lock the pruner behavior: keep local Hecton8.Habitat.Deformation.Contracts script assembly reference, remove stale Unity.Entities package-cache reference.
- Updated Assets/Project/Scripts/UI/ShaderCompassRibbon.cs to implement IGlobalRegistryHotSwapListener, refresh its cached navigation service on InertialNavigationRuntime replacement, and retry dispatcher registration on dispatcher replacement.
- Patched ignored local Hecton8.Core.csproj generated artifact only to move verification past the stale Habitat contract wall before Unity regenerates project files.
- Cinematic Cheats used:
- Exact Microseconds saved:
- Pruner fix: runtime 0 us; editor compile triage avoids repeated failed Habitat namespace compiles, estimated 10,000,000 us per failed dotnet build attempt on this machine.
- Compass hot-swap fix: runtime LateFrame remains 0 allocation and no GlobalRegistry polling; avoids a per-frame global read fallback. Estimated saved hot-path cost 0.1-0.3 us/frame when navigation service is absent or late-bound.
- Build-server discipline: shut down compiler servers after verification; avoids parallel compiler contention, estimated 500,000+ us avoided on low-end i3/MX350-class hardware during subsequent build attempts.
- Verification:
- git diff --check passed for touched tracked files; only CRLF normalization warnings were reported.
- dotnet build Hecton8.Editor.csproj --no-restore attempt 1 failed on missing Habitat namespace from stale generated Hecton8.Core.csproj.
- After local ignored generated graph repair, build attempt 2 passed the Habitat wall and failed later with 290 unrelated errors in other-agent partials: WristHologramHudRuntime, VRSomaticProvider.Comfort, HectonNarrativeDirectorPoiTriggers, AirlockPressurization, BulkheadContainmentRuntimeHatchLocks, TetherManager.
- Historical first-pass compile status then: BLOCKED BY DEPENDENCY outside EXTERNALCODEX changes. Superseded by later zero-warning verifier entries below.
- 2026-05-23 External Integration Compile Closure
- After the generated project graph was forced past the Habitat contract wall, the project had real compile defects in source: illegal NativeArray<T.ReadOnly.AsReadOnly() usage, in parameters used where buffers were mutated, unsafe field addresses taken directly, missing narrative imports, wrong metabolism fatigue constant name, inaccessible nested mock seismic job, ambiguous Burst math overload, unassigned camera-juice arrays, variable shadowing, and an editor Environment namespace collision.
- Several files existed on disk but were absent from the ignored generated Hecton8.Core.csproj, so local dotnet verification could not see partial definitions and generated contracts until the generated graph was repaired for this checkout.
- Fixed source compile faults with narrow edits in the owning files: SubmarineAutoLevelBallastController, SignalWardenRuntime, PlayerCriticalProceduralAudioRenderer, HydrodynamicKccRuntime, AirlockPressurizationRuntime, AirlockPressurizationJobs, HectonNarrativeDirectorPoiTriggers, AlignmentTelemetryContracts, SystemDispatcher, HectonSeismicTideDirector, CombatDamageRuntimeStatusEffects, CameraJuiceSystemCameraJuiceBurst, BulkheadContainmentRuntime, and CraftingFastFailXRayWindowSHINOBU317.
- Added a mutable SystemDispatcher.TryResolveDispatcherVaultBuffer<T overload while keeping read-only accessors read-only.
- Patched ignored local Hecton8.Core.csproj only as a verification bridge for existing on-disk partial/generated sources; durable source fix remains the tracked project-reference pruner and its regression test.
- Ran guarded iterative builds. Error-wall progression: Habitat graph wall - 580 error lines - 194 errors - 7 errors - 6 errors - 1 error - 0 errors.
- Shut down MSBuild and C# compiler servers after final verification.
- None. This pass repaired compile/integration defects. No physical simulation or visual system was replaced with a fake.
- Runtime intended delta: 0 us for namespace/import/visibility/definite-assignment repairs.
- ShaderCompassRibbon hot-swap route avoids per-frame GlobalRegistry polling fallback; estimated hot-path saving 0.1-0.3 us/frame in late-bound navigation scenarios.
- Airlock unsafe pointer repair preserves native atomic routes instead of managed proxy work; estimated avoided allocation/dispatch pressure 0.5-2.0 us per contested flush on i3/MX350-class machines.
- Build graph/pruner repair avoids repeated failed local compile passes; measured category is build-time, estimated 10,000,000+ us per avoided failed dotnet attempt on this machine.
- Build-server shutdown avoids subsequent compiler contention; estimated 500,000+ us on low-end editor hardware.
- Final guarded command: dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/BuildEXTERNALCODEXafterpatch5.log;verbosity=minimal".
- Final build output produced Temp/bin/Debug/Hecton8.Core.dll and Temp/bin/Debug/Hecton8.Editor.dll; log has 0 : error entries.
- Remaining warnings: two CS0618 warnings in Assets/Project/Scripts/Editor/SubmarineDynoTunerWindow.cs for obsolete SubmarineKinematicConfig.BallastLiftN.
- git diff --check passed for touched files; only Git LF-to-CRLF normalization warnings were reported.

FILE LOG_EXTERNAL_FIXER.md bytes=72596 errors=3 warnings=63 successMarkers=42 selected=34
- 2026-05-23 Autonomous External Fix Pass
- In progress. Initial state is a dirty multi-agent workspace; no runtime/compiler claim yet.
- Cinematic Cheats used:
- Exact microseconds saved:
- 0 us measured so far. No runtime code changed yet.
- VoxelDeltaProcessor.EmitCaveInDustDecal pulled AbyssalFluidDecals from GlobalRegistry during carve commit side effects.
- Added cached service fields plus IGlobalRegistryHotSwapListener handling to ScavengePopulator, VoxelDeltaProcessor, Atlas6DirectiveSystem, and AtlasSignalDecoder.
- Preserved public APIs, save DTOs, ownership routes, and existing SignalBus/EventBus behavior.
- None. This tranche is service-route hardening, not visual simulation replacement.
- No profiler claim. STATICSOURCE estimate only: removed 2 registry reads per active ScavengePopulator spawn-queue slow tick, 1 registry read per chunk despawn, 1 registry read per voxel cave-in dust emission, 1 registry read per Atlas6DirectiveSystem slow tick, and 1 registry read per AtlasSignalDecoder slow tick/pulse sync.
- Verification:
- dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal: succeeded, 0 warnings, 0 errors.
- git diff --check on touched code files: no whitespace errors; Git reported LF-CRLF warnings only.
- None. This tranche is service-route hardening, not physical or visual simulation.
- No profiler claim. STATICSOURCE estimate only: removed 2 registry reads per RT manager slow tick, 8 registry reads per full Camera/PostFX/UI/Visor budget sweep.
- git diff --check on touched files: no whitespace errors; Git reported LF-CRLF warnings only.
- Guarded build: CPU/process gate waited until CPU was 5 percent and no dotnet/csc/VBCSCompiler process was present.
- VRAMMonitor.ReadRenderTextureMemoryBytes() used GlobalRegistry.RenderTextureLifecycle from the slow-tick measurement path when the profiler RT counter returned zero.
- TetherManager also has slow-tick registry polling, but the file is already dirty with unrelated HarpoonTension/Vault changes, so it was not touched in this commit.
- Kept profiler counters as first source of truth and used the cached lifecycle tracker only for fallback RT bytes.
- None. This tranche is service-route hardening, not simulation or visual approximation.
- No profiler claim. STATICSOURCE estimate only: removes 1 registry read per VRAM slow tick when profiler RT memory counter is unavailable.
- Guarded build waited while CPU was 100 percent with active dotnet, csc, and VBCSCompiler, then ran after the compiler window cleared.
- SkySystemFollowCamera.Tick() can call ResolveSeaLevelY(), which could call ResolveAtmosphereManager() and use GlobalRegistry.Atmosphere as fallback from the per-frame follow path.
- Preserved explicit inspector atmosphereManager ownership ahead of the cached fallback.
- None. This tranche is route hardening for a sky-follow helper.
- No profiler claim. STATICSOURCE estimate only: removes 1 registry read from the sky follow tick path when sea-level lock needs atmosphere fallback.
- HectonCaveVoxelAmbientOcclusionController.SlowTick() can call TryResolveViewerReferences(), which used GlobalRegistry.Player when viewerCamera was unresolved.
- That made cave ambient-occlusion cadence depend on hot service-locator polling during viewer fallback.
- Kept explicit viewerCamera ownership first and changed fallback resolution to cached player context only.
- None. This tranche is route hardening for cave AO viewer binding, not AO math or visual approximation.
- No profiler claim. STATICSOURCE estimate only: removes 1 registry read per cave AO slow tick while viewer camera resolution is unresolved.
- git diff --check on touched code file: no whitespace errors; Git reported LF-CRLF warning only.
- Guarded build ran at CPU 48 percent with no active dotnet/csc/VBCSCompiler process.

FILE LOG_SHINOBU_315.md bytes=33566 errors=4 warnings=8 successMarkers=59 selected=34
- Existing ProceduralFabrikArmJobs.cs is Burst but not sufficient: no 64B IkHandStateDTO, no double3 AUP root subtraction, no SHINOBU315 Vault lanes, no rollback fence proof, no telemetry dump path.
- First-party source scan found no active FinalIK, FastIKFabric, RootMotion.FinalIK, OnAnimatorIK, or Animator.SetIKPosition users. No source deletion was justified.
- Added Vault-backed visual-only lanes: 315730..315735.
- Added Burst jobs: BuildHandIkTargetsFromBridgeJob, GenerateMockIkTargetsJob, EvaluateHandIkJob, and BuildHandBoneMatricesJob.
- Added pole projection after FABRIK forward/backward passes.
- Added 300-frame IkHandTelemetryEntry ring and cold dump path Docs/AgentLogs/DumpSHINOBU315.bin.
- Cinematic cheats used:
- Low quality does not swap algorithms. It continuously lowers FABRIK iterations to one pass through math.lerp(1,max,GlobalQualityWeight).
- Pole correction projects the elbow onto a stable local plane instead of solving anatomical muscle dynamics.
- Exact microseconds saved:
- Not claimed. A build/profiler gate was not run because active dotnet was present and CPU stayed at 100%, which the batch explicitly forbids.
- Target estimates recorded in status: 40 us/2 hands for FABRIK solve, 8 us/2 hands for pole correction, 2 us/2 hands for Dear Lie, 128B/frame telemetry write.
- Compile and validation:
- git diff --check passed for modified files except repository LF-CRLF warnings on existing files.
- No dotnet build was launched: active dotnet PID 3056 and CPU=100%.
- Runtime hot file scan found no new NativeArray, LateUpdate, Mathf.Lerp, Transform[], GetComponent, FindObjectsOfType, or hot GlobalRegistry use in PlayerKinematicsRuntimeHandIK.cs.
- <TASK id="01" status="PASS"Source archaeology completed; actual domains identified.</TASK
- <TASK id="02" status="PASS"Partial integration through PlayerKinematicsRuntime.</TASK
- <TASK id="03" status="PASS"Existing signals and DataVault lanes checked; no new signal lane.</TASK
- <TASK id="04" status="PASS"Managed IK users absent; no deletion candidate.</TASK
- <TASK id="05" status="PASS"Animator IK users absent in first-party runtime.</TASK
- <TASK id="06" status="PASS"GenerateMockIkTargetsJob implemented.</TASK
- <TASK id="07" status="PASS"EvaluateHandIkJob implemented.</TASK
- <TASK id="08" status="PASS"Pole vector projection implemented.</TASK
- <TASK id="09" status="PASS"Dear Lie release blend implemented.</TASK
- <TASK id="10" status="PASS"Double GraphicsBuffer upload implemented.</TASK
- <TASK id="11" status="PASS"Continuous iteration scaling implemented.</TASK
- <TASK id="12" status="PASS"AUP double subtraction before float cast implemented.</TASK
- <TASK id="13" status="PASS"Visual-only BufferIDs excluded from sync/Merkle route.</TASK
- <TASK id="14" status="PASS"UninitializedMemory requested for overwritten runtime buffers.</TASK
- <TASK id="15" status="PASS"300-frame telemetry ring and dump path implemented; completion time is fence elapsed, not fabricated Burst-only timing.</TASK
- <TASK id="16" status="PASS"VR Kinematics Tuner implemented.</TASK
- <TASK id="17" status="PASS"Span CSV parser implemented.</TASK
- <TASK id="18" status="PASS"SceneView bone/pole gizmo implemented.</TASK

FILE LOG_SHINOBU_361.md bytes=74278 errors=16 warnings=7 successMarkers=84 selected=34
- What was done: Created durable status, rationale, and log files. Extracted SHINOBU361 prompt block from CURRENTBATCH.md and counted 20 tasks.
- Cinematic Cheats used: Adopted baked texture detail and ORM packing as the default surface-complexity path instead of geometry/runtime simulation.
- Exact Microseconds saved: PENDING PROFILER. No runtime profiler capture exists for this static setup step.
- Audit result: 972 target files scanned; 4,568 audited slot/reference rows; 333 factual remediation prompts; 0 production 1x1/checkerboard stubs; 0 .tga/.psd source-format blockers; 17 import-setting issue textures; 563.889 MiB estimated replacement residency versus 900 MiB texture budget, status PASS. OOP texture scanner found 173 dynamic-material/material-access static debt rows, so project eradication state is PENDINGREMEDIATION, not green.
- Cinematic Cheats used: Prompt and bake plans push rivets, panel seams, scratches, salt crystals, basalt pores, flora membranes, glass fracture edges, and weld scars into albedo, BC5 normal, and packed ORM masks. Geometry-heavy surface detail and separate AO/roughness/metallic samplers were rejected.
- Exact Microseconds saved: PENDING PROFILER. Static audit cannot truthfully claim frame-time savings. Expected runtime benefit is fewer missing-material fallbacks, fewer separate mask samplers after ORM packing, and reduced geometry pressure from baked detail; profiler proof is absent.
- <TASK id="01" status="PASS" evidence="Docs/Reports/TextureAuditSHINOBU361.json targetfilecounts"/
- <TASK id="02" status="PASS" evidence="Docs/Reports/productiontexturemanifest.csv referenceguid and resolvedtexturepath columns"/
- <TASK id="03" status="PASS" evidence="Docs/Reports/TextureAuditSHINOBU361.json stubtexturecount=0"/
- <TASK id="04" status="PASS" evidence="Exact category set enforced in manifest"/
- <TASK id="05" status="PASS" evidence="priority column in productiontexturemanifest.csv"/
- <TASK id="06" status="PASS" evidence="estimatedmissingtexturevrammib=563.889"/
- <TASK id="07" status="PASS" evidence="importissuetexturecount=17 and forbiddenformattexturecount=0"/
- <TASK id="08" status="PASS" evidence="333 natural-English prompt entries"/
- <TASK id="09" status="PASS" evidence="normalplan field on every prompt entry"/
- <TASK id="10" status="PASS" evidence="ormplan field on every prompt entry"/
- <TASK id="11" status="PASS" evidence="32 GEOLOGYTRIPLANAR remediation prompts"/
- <TASK id="12" status="PASS" evidence="cockpit template exists; no factual cockpit defect prompt emitted"/
- <TASK id="13" status="PASS" evidence="258 HABITATINTERIORS remediation prompts"/
- <TASK id="14" status="PASS" evidence="43 FLORAEPIDERMIS remediation prompts"/
- <TASK id="15" status="PASS" evidence="decal template exists; no factual decal defect prompt emitted"/
- <TASK id="16" status="PASS" evidence="Tools/BatchImportTextures.py and dry-run CSV artifact"/
- <TASK id="17" status="PASS" evidence="Docs/Reports/productiontexturemanifest.csv rows=4568"/
- <TASK id="18" status="PASS" evidence="TextureMigrationDebugGizmo.cs editor-only manifest overlay"/
- <TASK id="19" status="PASSASSCANNERPENDINGASPROJECT" evidence="RENDERINGOPTIMIZATIONREPORT.json findingCount=173"/
- <TASK id="20" status="PASS" evidence="SELFAUDITSTATICPASS command output"/
- <ARM64CHECKNo runtime DTO, NativeArray element, SignalBus payload, telemetry struct, save struct, Burst job struct, GPU upload struct, or FieldOffset layout was introduced. Editor-only RendererIssue is a reference class and does not cross runtime/native boundaries. Runtime byte layout proof is NOTAPPLICABLE.</ARM64CHECK
- <ZEROGCCHECKNo gameplay Tick, Update, FixedUpdate, LateUpdate, coroutine, Resources.Load, or new Material path was introduced. Editor SceneView gizmo allocates only in editor cache/refresh surfaces and does not enter player builds due to #if UNITYEDITOR.</ZEROGCCHECK
- <PROMPTCHECKPASS: 333 prompts contain required flat diffuse lighting, zero directional shadows, top-down orthographic view, seamless tileability, and no banned --, ::, [, or ] syntax.</PROMPTCHECK
- <MANIFESTRLECHECKCSV bytes 2470486; RLE run count 975; estimated RLE index bytes 31200; runtime CSV parser not introduced.</MANIFESTRLECHECK
- <VAULTBUFFERIDSNone. No GlobalDataVault route or runtime buffer was added.</VAULTBUFFERIDS
- <COMPILECHECKPython pycompile PASS. C# compile PENDINGVERIFICATION because CPU preflight was 99.4178073412672 percent and build launch was forbidden.</COMPILECHECK
- 2026-05-23 Continuation R5 - Current Evidence Snapshot
- What was wrong: Earlier log entries preserve historical intermediate counts. The current disk truth after the prefab false-positive filter is lower and must be the bottom-most evidence for the CTO.

FILE LOG_TASTE.md bytes=1556 errors=0 warnings=0 successMarkers=0 selected=11
- Created Docs/Tasks/StatusTASTE.md.
- Distilled active authority from AGENTS.md, .agents-skills, Docs/README.md, domain map, flooded geography, scalability matrix, cinematic cheats, visual identity, lore, brand, PBR surface doctrine, and Subnautica counterposition docs.
- Cinematic Cheats used:
- The document explicitly encodes fake-first taste: depth fog, shader waterlines, scalar pressure, flow masks, projected caustics, authored scars, audio/haptic/UI consequences.
- It rejects simulation for invisible causes and requires high-end overkill to remain presentation-only.
- Exact Microseconds saved:
- Verified runtime savings: 0us. Markdown-only change.
- Estimated runtime impact: 0us. No C# source, prefab, asset, shader, scene, or project setting was edited.
- Verification:
- Build not run because no code changed and compiler work is not needed for a Markdown-only edit.
- Runtime proof remains not implied.

FILE LOG_TOKEN_USAGE_AUDIT.md bytes=4972 errors=1 warnings=0 successMarkers=1 selected=30
- LOGTOKENUSAGEAUDIT
- 2026-05-23 Token Usage, Code Lines, Commit Count
- Prior token ledger stopped at 2026-05-18 and did not include the May 21 cleanup backup plus current May 21-23 sessions.
- Counted 87,322,244,824 total Codex tokens from 2,497 sessions with token usage.
- Counted broader first-party source under Assets/Project plus Tools, excluding JSON: 3,015 files and 1,859,225 physical lines.
- Added Docs/TOKENUSAGELEDGER.md and Docs/Reports/2026-05-23TOKENUSAGECODEBASEANDCOMMITCOUNTERS.md.
- Cinematic Cheats used:
- Exact microseconds saved:
- 0 us measured. No runtime code changed.
- Verification:
- JSON parse/read errors: 0.
- Unity compile/import/profiler not run because only documentation changed.
- 2026-05-23 Token Usage Refresh 16:11 Europe/Samara
- The 15:05 token ledger no longer included current 2026-05-23 Codex session growth and six pushed runtime-fix commits.
- Counted 97,306,917,423 total Codex tokens from 2,599 sessions with token usage.
- Counted broader first-party source under Assets/Project plus Tools, excluding JSON: 3,047 files and 1,866,086 physical lines.
- Updated Docs/TOKENUSAGELEDGER.md, Docs/Reports/2026-05-23TOKENUSAGECODEBASEANDCOMMITCOUNTERS.md, Docs/DOCGOVERNANCE.md, and Docs/HECTON8GLOBALARCHITECTUREMAP.md.
- sqlite3 was unavailable in the shell; JSONL final per-session telemetry remained the accounting source.
- 2026-05-25 TOKENUSAGEAUDIT process/token refresh
- What was wrong - VS Code was responsive, but Unity batch compiles left orphan VBCSCompiler dotnet processes after terminal compile failures. First compile wall was FaunaSensorSuite.maxRayLength after a rename; second wall was HazardZoneManager wrapper compatibility.
- What was done - Stopped only orphan compiler servers after parent death/log completion; current Fauna source now routes the legacy serialized value to maxProbeLength, HazardVaultArray exposes the missing wrapper surfaces, and token ledger was refreshed from 2,741 JSONL files across current and backup roots.
- Cinematic Cheats used - None; audit/process hygiene only.
- Exact Microseconds saved - 0 us game runtime. Workstation contention reduced by terminating orphan compiler servers; no profiler sample, so no runtime timing claim.
- Token report - Docs/Reports/TOKENUSAGEAUDIT2026-05-25.md and .json. Total tokens 95,707,766,654; gpt-5.3-codex standard API-equivalent $27,238.18.
- Evidence - STATICLOCALCODEXJSONLANDFILESYSTEM plus Unity compile log tails from Temp/X002UnityDataMonolithProbeRerunFINAL.log. Runtime/Unity PlayMode proof absent.
- Final process pass - Unity/dotnet/csc/MSBuild/VBCSCompiler count 0. VS Code process tree responsive. CPU still 96 percent from live VS Code/node workload; active dental-crm node dev servers were not stopped because they were not orphaned. Guarded build skipped by project rule: CPU 50 percent.
- 2026-05-25 TOKENUSAGEAUDIT model-price/statistics refresh
- Exact Microseconds saved - 0 us game runtime. Static telemetry and docs only.
- Token report - Docs/Reports/TOKENUSAGEAUDIT2026-05-25.md and .json. Total tokens 95,853,026,051; all-as-gpt-5.3-codex standard API-equivalent $27,275.85; model-bound known+unpriced-as-gpt-5.5 standard $69,925.03.
- Evidence - STATICLOCALCODEXJSONLANDFILESYSTEM plus official OpenAI pricing pages. Runtime/Unity PlayMode proof absent.

FILE LOG_UNKNOWN.md bytes=44874 errors=6 warnings=20 successMarkers=33 selected=34
- Evidence class: static source audit plus read-only subagent audits. No build run. Green build, log noise, and warning noise were intentionally excluded from the verdict.
- The project is not "doing nothing". The last-three-day direction contains real architecture work: scene search removal, concrete dependency reduction, SignalBus typed payload migration, GlobalDataVault handle ownership, and dispatcher frame ownership. However, the implementation is currently RED for architecture compliance because several fixes replace old coupling with new global authority surfaces.
- GlobalRegistry.TryGet usage is currently zero.
- GlobalSignals.Publish usage is currently zero; HectonEventBus.Publish remains isolated to ModdingAPI.
- GlobalDataVault introduces generation handles, write locks, and black-box telemetry surfaces.
- SystemDispatcher.CurrentFrameId and TimeSliceScheduler frame ownership exist.
- GlobalRegistry surface is still too large and has grown into command transport. Current GlobalRegistry contains 176 service slots and hundreds of public accessors. New routes such as PersistentDroppedItems, AtlasSignalDecodeSink, EndingRuntimeService, Atlas6DirectiveCommandSink, EnvironmentalStrainIndustrialSink, ResourceScarcityReadModel, FaunaWorldSeed, and LoreDatabaseReadModel lack route-card proof.
- Registry is used for gameplay commands. PDAExchangeSystem pulls IAtlas6DirectiveCommandSink from GlobalRegistry during barter execution. AutonomousExtractorSystem calls persistent dropped item registration through a registry route.
- Hot registry polling remains in fauna selection. FaunaDirector calls MigrationDirector.ResolveSelectionMultiplier; MigrationDirector reads GlobalRegistry.Migration and GlobalRegistry.FaunaWorldSeed without a cached fauna seed route.
- DataVault job views are not proven pinned. HazardZoneManager schedules jobs on vault views acquired through TryAcquireWriteLock, while GlobalDataVault live defrag can move unlocked blocks via MemMove. TryAcquireWriteLock is not equivalent to relocation pinning.
- HazardZoneManager public read accessors resolve mutable vault views through indexers. This violates the pure read accessor doctrine and turns read-model access into hot DataVault resolution.
- DataVault write-lock authority is weak in player builds: owner check is tied to collection checks in at least one resolve path, and TryAcquireWriteLock accepts caller systemID as active writer after generation checks.
- Continuous GlobalQualityWeight exists, but runtime still collapses quality into tiers and lets quality affect authority-adjacent data. Examples include cartography discovery shell width, foundation snapping solve budgets, HabitatGraph flood node budgets, and AI cognition cadence without proof of truth invariance.
- Frame authority is partial. SystemDispatcher frame ID exists, but raw Time.frameCount is still written into runtime payload-style fields in Core, Input, Construction, Animation, and World systems.
- Jobs completion audit is incomplete. Several .Complete() calls are annotated cold/QA, but runtime dispatcher/fence/voxel paths still require explicit completion-window proof.
- Cinematic cheats / scalability:
- Positive: continuous GlobalQualityWeight and weighted math helpers exist and can buy visual overkill without binary quality switches.
- Failure: tier bridges, High/Ultra gates, and quality-dependent gameplay/data writes remain. Toaster, middle, high, and ultra tiers must be different cost paths for presentation/cadence/capacity only; they must not change save-visible truth or authority routes.
- Freeze new GlobalRegistry slots until each route is classified as read model, command sink, or cold DI identity. Command sinks must move to owner-local cached interfaces or typed SignalBus command lanes unless there is a documented owner route.
- Remove Unity concrete leaks from Core contracts where possible: GameObject, BaseModule, BuildableData, mutable char[] buffers, and broad IReadOnlyList<GameObject surfaces.
- Make SignalBus lane initialization a bootstrap invariant. Publish path must not allocate or lazily initialize persistent native storage.
- Add a real DataVault job pin or prohibit live defrag while any scheduled job holds vault views. TryAcquireWriteLock must not be treated as a relocation pin unless it sets the same lock bits used by defrag.
- Convert HazardZone read accessors to immutable snapshots or cached read-only views. Do not resolve mutable DataVault handles inside public Get/TryGet read methods.
- Replace binary quality/tier branches in gameplay and save-visible paths with continuous GlobalQualityWeight only where it affects fidelity/cadence, not authority.
- Split Time.frameCount hits into allowed visual/diagnostic use and forbidden gameplay/signal-frame use; migrate forbidden cases to SystemDispatcher.CurrentFrameId or TimeSliceScheduler.CurrentFrameId.
- Microsecond estimate:
- No credible saved-microsecond claim was made. Static architecture audit cannot prove frame-time savings. The only defensible estimate is that removing scene search and TryGet polling reduces unpredictable spikes, but the current DataVault and GlobalRegistry risks can reintroduce worse costs or correctness failures.
- 2026-05-25 - Deep Architecture Audit, Proof-Backed Append
- User requested a last-three-day architecture audit, not green-build/noise review.
- Baseline commit: 3d9c1023e413eb96c32fbddb4fe99c95738c9a87, 2026-05-21T23:56:38+04:00, chore: checkpoint project audit status tail.
- Evidence class: STATICGIT, STATICSOURCE, STATICDOC, STATICFILESYSTEM, and read-only subagent audits. No compile, Unity import, PlayMode, profiler, or player build proof was produced in this turn.
- ARCHGlobalRegistryServiceLocatorDIInit: GlobalRegistry must be cold identity/DI. Hot polling and gameplay command transport are not acceptable without route proof.
- ARCHSignalLaneSegregation: SignalBus<T is hot typed broadcast; GlobalSignals direct publish is legacy bridge; HectonEventBus is mod/API/cold isolation.
- OPTNativeMemoryCollectionsJobSystemProtocol: scheduled jobs must own stable native views and completion windows; hidden same-frame completion and relocation hazards require proof.

FILE LOG_X_000.md bytes=94935 errors=52 warnings=43 successMarkers=44 selected=34
- 2026-05-23 - Scoped Vault Exorcism: AudioLogSystem
- AudioLogSystem owned one persistent NativeQueue<uint and two persistent NativeArray<uint fields inside a MonoBehaviour.
- These aliases blocked DataVault relocation/defragmentation safety because they preserved direct native collection views across dispatcher phases.
- Initial full Roslyn audit found 2270 forbidden persistent native alias candidates across Assets/Project/Scripts.
- Added Tools/VaultNativeAliasRoslynAudit and generated a machine-readable Roslyn AST ledger.
- Added AudioLog vault BufferIDs:
- AudioLogTelemetryRing = 70675
- AudioLogTelemetryCursor = 70676
- Replaced AudioLogSystem persistent native aliases with VaultGenerationHandle<T descriptors.
- Rewrote playback queue enqueue/dequeue/clear paths to resolve DataVault views only inside method scope.
- Rewrote encrypted fragment save/load/read/write paths to use transient DataVault read-only views and bounded writer locks.
- Added AudioLogVaultTelemetryEntry, explicit layout, 64 bytes, 300-row ring.
- Corrected telemetry writes to acquire/release DataVault writer fences for ring and cursor.
- Wrote proof artifacts:
- Docs/Reports/VAULTNATIVEALIASLEDGERX000.json
- Docs/Reports/VAULTNATIVEALIASLEDGERX000AudioLogafter.json
- Docs/Reports/VAULTEXORCISMREPORTX000.json
- Cinematic cheats used:
- Telemetry records failure counters and generations only. No managed stack traces, no string formatting, no hot-path exception payload.
- Exact microseconds saved:
- Runtime GC saved: unmeasured in profiler, structurally 0 managed allocations added on migrated hot paths.
- Persistent native alias count saved in AudioLogSystem: 3 fields removed.
- Expected normal-frame telemetry overhead: 0 us because telemetry recording is only on fallback/error paths.
- Error-path telemetry cost: two bounded DataVault writer-fence attempts plus one 64-byte row write; no profiler microsecond sample available in this shell session.
- Verification:
- dotnet build Hecton8.Editor.csproj --no-restore: succeeded, 0 warnings, 0 errors, 00:01:26.34.
- Full Roslyn audit after migration: 2373 files, 0 parse failures, 2267 forbidden persistent candidates, hash a0e80f2152a4712f729c3d6e867c21a0b199b26bff7764e2d31b4fb808ef04a7.
- Scoped AudioLog audit after migration: 5 files, 0 parse failures, 2 remaining static queue findings in AudioLogEvents.cs, 0 MonoBehaviour candidates, hash c4af968a29c0bf6f24172fbd986896e5234a00f1ebc4e2007d01c5d80bde7474.
- Project-wide purge is not complete. Full audit still reports 2267 forbidden persistent native alias candidates.
- AudioLogEvents.cs still owns pendingEvents and nextFrameEvents static NativeQueue<AudioLogEventPayload lanes. They require a SignalBus route decision before removal.
- 2026-05-23 - Scoped Vault Exorcism: AwaitableDropSequenceDirector
- AwaitableDropSequenceDirector owned NativeArray<PrologueSequenceTelemetryEntry blackBox as a persistent MonoBehaviour field.
- That ring is exactly the class of stale alias that breaks DataVault relocation guarantees.
- Added SystemID.PrologueSequence = 350.

FILE LOG_X_001.md bytes=89587 errors=17 warnings=36 successMarkers=44 selected=34
- GlobalSignals.cs is still a central bridge surface: 74 NativeQueue<T fields, 141 direct flush invocations, 73 CreateQueue sites, and 523 GlobalSignals call sites.
- Two hard DTO violations remain: ToolEffectSignal carries Transform; PendingDurabilityCommand carries string.
- The old SignalBus<T ABI fence allowed only 16/32/64/128/192 byte payloads, rejecting valid 8-byte-aligned DTO sizes such as 24, 40, and 48 bytes.
- Reclassified legacy root scripts as Legacy Root / Requires Owner Route Card instead of laundering them into Core ownership.
- Patched SignalBus<T.HasValidPayloadStride() to accept positive 8-byte-aligned payloads up to 192 bytes.
- Updated Docs/Tasks/StatusX001.md and Docs/AgentLogs/RationaleX001.md with blocked route-card decisions.
- Cinematic Cheats used:
- Static storm model used cheap burst math: capacity + 1 when capacity is known, otherwise 257 against default LaneCapacity=256.
- Exact Microseconds saved:
- Verified runtime savings: 0us. No Unity profiler or GCMonitor run was executed.
- Verification:
- dotnet run --project Tools/SignalArchitectureOptimizationAuditX001/SignalArchitectureOptimizationAuditX001.csproj -- --repo C:\hades\Hecton8 passed.
- Report summary: 2372 files scanned, 0 parse failures, 523 GlobalSignals call sites, 403 payloads, 2 hard payload violations, canonical hash 973c29f508747223dad454d34fd0be26c3c20a2017143d373af8ff4dbcc503c2.
- Unity compile, runtime profiler, and GCMonitor proof were not run.
- Blocked:
- Full contract extraction, producer/consumer rewiring, and dispatcher flush migration remain blocked until owner route cards exist for the affected lanes and the two hard payload violations are removed.
- PendingDurabilityCommand stored string ToolId inside the queued command struct.
- These two findings blocked any honest claim that signal/command payload DTOs were clean.
- ToolEffectSignal is now [StructLayout(LayoutKind.Explicit, Size = 40)] and stores primitive ids plus positions.
- PendingDurabilityCommand is now [StructLayout(LayoutKind.Explicit, Size = 24)].
- Tool string identity was moved into queuedDurabilityCommandToolIds, an owner-managed sidecar outside the command payload.
- Used identity ids instead of object references to preserve gameplay matching with less payload weight.
- Verified runtime savings: 0us. No profiler proof was executed.
- AST report rerun passed: 2373 files scanned, 0 parse failures, 0 hard payload violations, 7 layout warnings, canonical hash 18f6a27bd840c835cae400e4fed5f169ebc1025f24dd424b7273ca8e9e2fbe02.
- 2026-05-23 - APEX Hidden Route And Capacity Audit
- 181 signal lanes still carry centralization debt through direct flush, legacy queue creation, legacy publish, or legacy consume paths.
- Reactor damage has a typed ReactorDamageSignal lane, but related reactor/thermal/outgassing paths still publish legacy signals in RadioisotopeThermalGenerator and ToxicOutgassingChemistryRuntime.
- Hull deformation has typed HullDeformedSignal usage, but legacy hull/damage publishes remain in adjacent fauna/construction/environment paths.
- Recorded capacity tokens, low-tier frame caps, lane hashes, legacy publish counts, typed publish counts, overflow policy text, coalescing policy text, 5000-burst verdicts, and static zero-GC claim text per lane.
- Confirmed hard DTO managed-reference payload violations remain at 0 after the previous ToolEffectSignal and PendingDurabilityCommand fixes.
- Replaced runtime storm execution with deterministic source-ledger math because Unity profiler/GCMonitor was not run in this pass.
- Coalescing proof is limited to lanes with explicit native merge semantics, including CombatDamageSignal and acoustic energy lanes; non-coalesced lanes are reported as bounded native drop/clear behavior.
- Verified runtime savings: 0us. This pass produced static source proof and documentation, not a Unity frame capture.
- Report summary: 2373 files scanned, 0 parse failures, 403 payload definitions, 0 hard payload violations, 231 legacy publish sites, 0 unknown legacy publish payloads, 287 signal lanes in ledger, 181 centralization-debt lanes, canonical hash 480dd1942320675a360d5b141064dc2899816249440c4e842ed7e3f0f202ce76.

FILE LOG_X_002.md bytes=87079 errors=62 warnings=53 successMarkers=98 selected=34
- What was wrong: Data Monolith assignment started with no local status/rationale/log files for X002.
- What was done: Created disk-backed status, rationale, and final log files. Extracted X002 prompt from Docs/Tasks/CURRENTBATCH.md and identified 10 tasks.
- Cinematic Cheats used: None; this is static data infrastructure, not simulation or visual load.
- Exact Microseconds saved: PENDING VERIFICATION. No runtime code changed yet.
- 2026-05-23 - Data Monolith Architecture Pass
- Existing header proof was too thin: 16 bytes did not carry enough schema/range identity for fail-fast rejection.
- H8StaticDataArena used local numeric BufferID casts and could refresh through GlobalRegistry.DataVault, weakening the one-owner route.
- No executable corruption proof existed for bad magic, checksum drift, truncation, or section table offset damage.
- Runtime/static parser risks existed outside Data Monolith ownership and needed a disk report, not chat-only claims.
- Updated compiler and validator to write and verify the 64-byte header, checksum [64..blobLength), little-endian flag, schema hash, and directory identity.
- Routed runtime initialization through explicit globalDataVault injection from GameBootstrapper; moved monolith BufferIDs into H8Memory.cs.
- Added Unity .meta files for new DataMonolith editor scripts and Roslyn precompiled references to Hecton8.DataMonolith.Editor.asmdef.
- Ran corruption fuzzer through the CLI path. Result: PASS 4/4 (badmagic, badchecksum, truncatedblob, badsectionoffset).
- Updated Docs/ARCHITECTURE/DATAMONOLITHRUNTIMEINTEGRATION.md, Docs/Reports/DATAPIPELINEOPTIMIZATIONREPORTX002.json, Docs/Reports/DATAMONOLITHCORRUPTIONFUZZERX002.json, Docs/Tasks/StatusX002.md, and Docs/AgentLogs/RationaleX002.md.
- Cinematic Cheats used:
- Rejected tiny jobs and same-frame schedule/readback loops; all bake/fuzz/scanner work is editor/tool-only.
- Used fail-fast header/range/checksum checks instead of expensive late traversal on corrupt payloads.
- Exact Microseconds saved:
- Player-frame cost claimed: 0 us; this pass adds no per-frame system.
- Header corrupt-data early reject avoids section traversal in bad-data cases; expected gain is tens of microseconds, exact measured gain pending profiler.
- Verification:
- dotnet build Tools/DataMonolithBakeCli/DataMonolithBakeCli.csproj --no-restore -v:minimal: PASS with warnings only.
- dotnet run --no-build --project Tools/DataMonolithBakeCli/DataMonolithBakeCli.csproj -- .: PASS, bake plus fuzzer.
- dotnet build Hecton8.Core.csproj --no-restore: BLOCKED by unrelated Assets/Project/Scripts/AudioLog/AudioLogSystem.cs missing symbol errors before Data Monolith assembly verification. No X002 out-of-domain edits.
- Current batch again contains <AGENTPROMPT id="X002"; the earlier status could be misread as every CSV on disk being baked.
- Actual proof only supports the Data/Balance monolith lane plus parser-risk reporting, not full migration of every cross-domain CSV.
- Inventory result: 215 CSV files total, 125 data/asset/root CSVs, 18 active Data/Balance tables baked, 22 Data/Balance schema templates, 2 allowed external Data/Balance CSVs, 3 StreamingAssets CSV risks, 8 repo-root CSV risks, 70 cross-domain authoring sources, 68 docs/archive/report CSVs.
- Updated StatusX002.md and RationaleX002.md so the state is factual: core monolith baked, cross-domain migration pending owner routes.
- No new profiler claim. Current Data/Balance monolith player-frame cost remains 0 us; unresolved CSV owners still require route-specific measurement.
- No new dotnet run launched because external compiler processes were active.
- 2026-05-23 - T.A.R.S. Corruption Stress Pass
- Previous fuzzer proof was too narrow: 4 cases did not cover directory identity, data-start drift, record-size drift, unaligned offsets, section ranges into void, or localization-directory corruption.
- A global release parser-purity claim would be false: static scan found broad non-Editor file/text/parser hits outside the Data Monolith owner boundary.
- Changed Data Monolith section starts to 64-byte alignment while retaining 16-byte fixed-record alignment.

FILE LOG_X_003.md bytes=170163 errors=11 warnings=100 successMarkers=131 selected=34
- 2026-05-23 COMPILEWALLSMASHERANDDOMAINDECOUPLER
- Status: PARTIAL PASS - CORE COMPILE VERIFIED / ARCHITECTURAL BLOCKERS REMAIN
- Evidence class: STATICSOURCE / CLISTATICTOOL / CHANGEDASSEMBLYCOMPILE. No Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, or generated-project regeneration proof.
- Source using-boundary audit found 2,207 runtime cross-domain using violations under Assets/Project.
- Core-owned gameplay files remain trapped in the root assembly. Selected blast radius is 98 assemblies for:
- Top contract-extraction candidates are real but unsafe for blind movement: IDataVault, BufferID, VaultGenerationHandle, IGlobalRegistryHotSwapListener, ILateFrameTickable, IUpdatable, ISlowTickable, IPlayerRuntimeContext.
- Added Tools/CompileWallX003Audit.py.
- Corrected X003 audit scope from Assets/Project/Scripts to Assets/Project so generated input/domain editor asmdefs are included.
- Docs/AgentLogs/CompileWallX003Archaeology.json
- Docs/AgentLogs/CompileWallX003Archaeology.md
- AssemblyDependencyAudit.py --fail-on-cycles: PASS, 0 cycles.
- AssemblyDependencyAudit.py --fail-on-runtime-concrete-sibling-refs: FAIL as required, currently 116 runtime concrete sibling refs under full-project scope.
- Docs/Tasks/StatusX003.md
- Cinematic Cheats used:
- None. This task touched compile topology, static audit tooling, and one registry-cache hot-path cleanup. No water/light/deformation simulation was introduced.
- Exact microseconds saved:
- Measured runtime microseconds: 0 claimed. No profiler/GCMonitor proof was allowed or run.
- Compile-wall microseconds saved: 0 claimed. No runtime asmdef edge was severed because current source coupling would break compile.
- Static compile-wall baseline preserved for future proof: 98 affected assemblies for selected Core-owned gameplay files.
- Verification:
- python -m pycompile Tools/CompileWallX003Audit.py: PASS.
- python Tools/CompileWallX003Audit.py: PASS, report generated.
- python Tools/AssemblyDependencyAudit.py --fail-on-cycles: PASS.
- python Tools/AssemblyDependencyAudit.py --fail-on-runtime-concrete-sibling-refs: FAIL, expected and documented.
- Initial dotnet build: delayed by AGENTS.md rule because CPU was 65%, then 100%, and active dotnet/csc.exe processes existed.
- Later dotnet build Hecton8.Core.csproj --no-restore: PASS, 0 warnings, 0 errors, 00:02:03.32.
- EndingSystem.cs compiles inside Hecton8.Core.csproj; Unity import and Play Mode remain unverified.
- Contract extraction remains blocked until pure wrappers are designed for broad public APIs and concrete Unity/player facades.
- 2026-05-23 Compile Verification Addendum
- What was wrong: X003 status still carried compile as pending after the first guard blocked execution.
- What was done: Rechecked CPU/process guard, then ran the minimal changed-assembly compile: dotnet build Hecton8.Core.csproj --no-restore.
- Cinematic Cheats used: none. This is assembly verification, not simulation or presentation code.
- Exact Microseconds saved: runtime 0. Compile proof cost was 00:02:03.32 wall time. Assembly debt remains 116 runtime concrete sibling refs under full-project scope; no false compile-wall saving claimed.
- Result: Hecton8.Core compiled successfully with 0 warnings and 0 errors.

FILE LOG_X_004.md bytes=116671 errors=30 warnings=35 successMarkers=61 selected=34
- What was done - Created task status, rationale, and log files for file-backed memory.
- Cinematic Cheats used - None yet; scanner and mapping phase pending.
- Exact Microseconds saved - 0 us measured; no runtime code changed.
- 2026-05-23 Presentation Decoupling Pass
- What was wrong - Presentation APIs were present in pre-visual or simulation-adjacent lanes, and no reproducible X004 proof artifact existed.
- What was done - Added Tools/PresentationDecouplingAudit and generated Docs/Reports/PRESENTATIONDECOUPLINGOPTIMIZATIONREPORTX004.json. Last completed run scanned 2373 files, 843 runtime files, 615 simulation files, 229 presentation files, with 0 parser failures. Last completed report hash: 3e0b88b501559b0c883073b544abf1439811d551e89d0f7995b0ef7fe74a3153.
- Cinematic Cheats used - Static Dear Lie mapping: shader/constant-buffer visual fakes, SPSC audio route, zero-GC UI buffer route, and VISUALSYNC ownership route per finding.
- Exact Microseconds saved - 0 us measured; static proof only. Estimated review time saved: 20-45 us per finding cluster.
- What was done - Replaced direct shader write with pendingFloodScalar plus floodScalarDirty; Render now commits H8GlobalFloodScalar.
- Cinematic Cheats used - Flood/muffle presentation remains a shader-side scalar lie; simulation keeps only fluid summary truth.
- Exact Microseconds saved - 0 us measured. Estimate: 2-5 us on dirty flood frames on i3/MX350 class hardware.
- What was done - Added ILateFrameTickable, queued pendingShaderTier, registered/unregistered the late-frame route, and moved the shader write to LateFrameTick.
- Cinematic Cheats used - Depth-tier visual coloration is a presentation snapshot; audio/gameplay tier truth remains separate.
- Exact Microseconds saved - 0 us measured. Estimate: 1-3 us per dirty tier change on low-end hardware.
- What was done - Added a fixed pending visual snapshot and moved prop-wash, interaction buffer, and interaction count shader writes to LateFrameTick.
- Cinematic Cheats used - Flora bending/pushback presentation is a GPU interaction-buffer lie; gameplay density math remains the producer.
- Exact Microseconds saved - 0 us measured. Estimate: 6-8 us per active flora interaction frame on i3/MX350 class hardware.
- What was wrong - Final analyzer rerun and full compile were unsafe under active external C# workload.
- What was done - Checked process/CPU state: dotnet.exe and csc.exe were active, CPU probe returned 100.0%. Later CPU was 24.1%, but multiple dotnet.exe nodes still remained. Compile and analyzer rerun after final patches were not launched.
- Cinematic Cheats used - None.
- Exact Microseconds saved - 0 us measured; no false compile claim recorded.
- 2026-05-23 X004 Proof Closure
- What was wrong - Five presentation-owned renderer classes still pushed shader/material state from Tick, leaving visual work in the wrong phase even after simulation-owned hot paths were clean.
- What was done - Added ILateFrameTickable and late-frame registration to HectonBiolumDiffusionVolume, GPUScatterDirector, HectonDistantLandmarkRenderer, HectonHLODRenderer, and HectonOctahedralImpostorRenderer; their Tick methods are now no-op compatibility stubs and GPU writes execute from LateFrameTick.
- Cinematic Cheats used - Biolum volume, scatter, landmark silhouettes, HLOD matrices, and impostor animation remain GPU-side lies fed by immutable buffers/scalars after pre-visual phases finish.
- Exact Microseconds saved - 0 us measured. Estimate: 18 us pre-visual contention removed on i3/MX350.
- What was wrong - PRESENTATIONMUTABLETRUTHACCESS proof was noisy because generic TryResolveHandle read consumers were counted as mutable writers.
- What was done - Tightened the Roslyn scanner to exact mutable/write member names. Latest report: fatalHotPath=0, mutablePresentation=0, uiStringGcRisks=0, parseFailures=0, hash b8b9e08b96aa4c9e5530cbc737b88a765a54d6f195f1777bbf400aed9ab6a10c.
- Cinematic Cheats used - None; proof-only fix.
- Exact Microseconds saved - 0 us runtime; 262 false-positive review entries removed.
- What was wrong - Compile proof was pending because external compiler workloads were active earlier.
- What was done - Waited until no dotnet.exe/csc.exe were present and CPU was under threshold. Ran dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false.
- Exact Microseconds saved - 0 us measured. Build result: 0 warnings, 0 errors, 00:01:06.72 elapsed. Unity runtime/profiler proof still not run.
- 2026-05-23 APEX Re-Audit Pass

FILE LOG_X_004_A.md bytes=3386 errors=0 warnings=0 successMarkers=0 selected=13
- Broad scan produced noisy overlaps because many valid presentation owners contain shader/GPU APIs. Manual route inspection rejected those as findings when writes occurred from LateFrameTick, Render, or VisualSyncTick, not from the requested simulation entrypoints.
- Read local authority and mandates: AGENTS.md, Actual Domains, ARCHEXECUTIONPHASES, ARCHSIGNALLANESEGREGATION, PHYSFluidIncursionInterior, COREDamageSystemHullIntegrityVFXFeedback, VOXVoxelWorldLogicCarvingPersistence, OPTZeroGCPolicy, RENDGPUSovereignty, OPTCinematicCheat.
- Scanned runtime C# under Assets/Project/Scripts, excluding Editor and Tests, for domain keywords plus simulation entrypoints plus requested presentation sinks.
- Manually inspected routes in:
- Cinematic cheats used:
- Existing accepted route pattern observed: simulation writes compact state/dirty flags; presentation fakes flush shader/GPU/audio/particle work later.
- Exact microseconds saved:
- 0 us measured. Read-only audit made no code change.
- Proof commands:
- PowerShell intersection scan: domain keywords hull|flood|compartment|structural|integrity|fluid|water|voxel|deform|deformation|ecology|ecosystem|biolum|flora|fauna|vegetation|coral|silt|snow|slosh|breach, entry regex \b(Update|FixedUpdate|Tick|FixedTick|PostFixedTick|PreSimulationTick|ScheduleSimulation|Execute)\b, sink regex Material\.|Shader\.|SetGlobal|Renderer|Light\b|ParticleSystem|AudioSource|IAudioService|\.Play\(|TMP|\.text\s=|SetCharArray|GraphicsBuffer|LockBufferForWrite|Dispatch\(|AsyncGPUReadback|ObjectPool|Spawn\(|Despawn\(|SetActive\(.
- Targeted rg -n route scans on the files listed above.
- Verification status:
- No Unity import, Play Mode, Frame Debugger, profiler, GCMonitor, or build was run.

FILE LOG_X_004_SUB_A.md bytes=15900 errors=1 warnings=0 successMarkers=2 selected=34
- What was done: No source files edited. Current source line scans and helper-chain reads were performed for FloraInteractionManager, SargassumCrestDampingController, SargassumGlobalDragManager, AbyssalThermalManager, SargassumCutManager, SargassumMicroFaunaBoids, HectonCaveVoxelLightingVolume, and EcosystemDirector. Mandates read: OPTZeroGC, ARCHExecutionPhases, ARCHGlobalRegistryDI, ARCHSignalLaneSegregation, RENDInstancedFloraPhysics, RENDURPGraphicsHotPath, RENDVFXFluidAesthetics, OPTCinematicCheat.
- Cinematic Cheats used: report-only patch plan recommends DTO/scalar staging and VISUALSYNC shader/VFX fakes. No runtime implementation performed.
- Exact microseconds saved: 0 us verified. Static inspection only. Expected savings are pending Unity profiler/GCMonitor proof after source patches.
- SargassumCrestDampingController: real residual leaks. Tick/SlowTick call PublishGlobals and DisableLegacyInputs, which mutate shader globals, Renderer.enabled, and Transform.localScale.
- AbyssalThermalManager: mixed. Old Tick thermal map upload findings are stale because UploadThermalMapTextureIfDirty is now LateFrameTick. Real leaks remain in FixedTick local thermal Shader.SetGlobal, Tick smoke compute dispatch/MPB upload/Graphics.RenderPrimitives, and thermal bubble globals if reached from Tick helper chain.
- SargassumCutManager: old Shader.SetGlobal fatal lines are mostly stale because Tick/SlowTick now queue globals and LateFrameTick publishes. Real residual leaks remain in Tick/RegisterExternalCut through debris particle emit, ComputeShader.Dispatch, and Graphics.SetRenderTarget damage-volume clear paths.
- SargassumMicroFaunaBoids: real residual leaks. Tick dispatches boid compute kernels and calls RenderCurrentBuffer, which mutates MPB and submits Graphics.RenderMeshIndirect. Slow/cold fallback texture creation is a first-use presentation hazard.
- HectonCaveVoxelLightingVolume: old fatal findings are stale in current source. Tick queues; LateFrameTick uploads Texture3D and shader globals. Remaining source risk is not fatal under this mission unless LateFrame registration fails.
- EcosystemDirector: real residual leaks in SlowTick predator AUP visual globals and buffer upload, plus external hot route FaunaBrain.Tick - PublishBiolumFlashBang - Shader.SetGlobalVector. Biomass overgrowth SetGlobalFloat is currently reached from LateFrame/cold routes and is not classified as fatal from inspected evidence.
- Verification: Unity import, Play Mode, profiler, GCMonitor, Frame Debugger, and compile were not run. No source edits were made, so compile was intentionally skipped.
- LateFrameTick:1920 - FlushInteractionVisualSync:2130 is already VISUALSYNC style and allowed in principle, though the same class still leaks direct Tick paths.
- Minimal zero-GC VISUALSYNC patch:
- Replace direct PublishGlobals calls in Tick/SlowTick with fixed pending structs: wash, damage reaction, flow field metadata, wake globals, parasite anchors, player runtime, reset flags.
- Replace ParticleSystem sediment burst with a SignalBus<EnvironmentSignal or owner-local fixed queue consumed by a VFX renderer in VISUALSYNC.
- Move wake compute dispatch and RenderTexture clears to LateFrameTick or render owner; Tick only appends wake DTO commands.
- SargassumCrestDampingController.cs
- Add/keep ILateFrameTickable; Tick/SlowTick only detect density/drift/cut-mask changes and set pending facade state.
- OnEnable/Awake calls to render resource ensure/publish are cold, not fatal by phase, but they prove mixed ownership.
- LateFrame/render owner performs Texture2D.Apply, Shader.SetGlobal, BRG registration/bounds/buffer sync, and RenderMeshInstanced.
- Collapse chunks: if visual-only, LateFrame VFX queue; if gameplay collision truth, publish a bounded PhysicsSignal/owner command, not transform fallback in ecology slow tick.
- Tick:1102 - BindSmokeUniforms:3755 - MPB SetBuffer/Vector/Float/Color:3781-3788.
- Tick/Slow helper route - PublishThermalBubbleCommands:3472 - Shader.SetGlobalInt/VectorArray:3484/3491 if still reached from AdvanceThermalGpuRefresh in Tick.
- Scanner labels for smoke uniform writes as Material.Set are imprecise; current source uses MaterialPropertyBlock.Set, still real because it is reached from Tick.
- FixedTick writes local thermal presentation DTO only; LateFrame flushes local heat/temperature/condensation globals.
- Tick updates thermal simulation scalars and smoke particle state intent only; smoke compute dispatch, MPB mutation, render submission, and bubble command globals move to LateFrameTick/render pass.
- Old Shader.SetGlobal report entries are stale: Tick:492 and SlowTick:555 now call QueueGlobalPublish:1373; LateFrameTick:568 - PublishGlobals:1379 performs Shader.SetGlobal.
- If cut mask is gameplay truth, separate authoritative cut state from visual mask texture; gameplay reads DTO/native mask, not RenderTexture.
- Additional dispatch helpers: clear/stat/spatial/PBD/origin shift compute dispatches at 6628, 6672, 6680, 7951, 7954 must be phase-owned if reached from Tick/fallback render paths.
- Split boid simulation ownership from boid rendering. Tick may write simulation command DTOs or schedule owner-approved jobs; render dispatch/property upload/draw submission moves to VISUALSYNC/render owner.
- Convert MPB use to constant/GraphicsBuffer where possible; MPB is not the preferred standard-geometry path.
- LateFrameTick:216 - Texture3D.SetPixelData/Apply:222-223 - FlushGlobals:590 - Shader.SetGlobal:592-619 is phase-correct.
- PublishInactiveGlobals:685 is still called from cold lifecycle paths; not a hot fatal unless registration/lifecycle misuse calls it from a dispatcher lane.
- Verify TryRegister includes LateFrame lane registration and add telemetry if LateFrameTick is not registered.
- SlowTick:2108 - PublishFloraPredatorAupBuffer:5563 - GraphicsBufferUploadUtility.UploadNativeArray:5593 - PublishFloraPredatorAupGlobals:5613 - Shader.SetGlobalBuffer/Vector/Int:5618/5619/5627.

FILE LOG_X_005.md bytes=183355 errors=12 warnings=81 successMarkers=154 selected=34
- HydrodynamicKccRuntime is not pure SDF collision. It still schedules CapsulecastCommand.ScheduleBatch and extracts RaycastHit into native DTOs.
- The KCC float SDF route ShinobuKccEnvironmentSdf is currently mock-marked. The real world byte SDF route is separate.
- Read project authority docs, domain boundaries, and the relevant physics/AUP/layout/zero-GC/job/telemetry mandates.
- Created Docs/Tasks/StatusX005.md.
- Mapped player/KCC/vehicle/VR collision routes, SDF routes, input routes, and deterministic velocity output routes.
- Cinematic cheats used:
- Exact microseconds saved:
- Runtime saved by Phase 0: 0 us. No runtime code changed.
- Planned removal opportunity: 120-380 us/frame on i3/MX350 class hardware by retiring movement PhysX command bridges and Rigidbody callback churn. This is an engineering estimate, not profiler proof.
- Planned SDF adapter budget: below 35 us/frame low-tier by generation/cadence gating. This is an engineering target, not profiler proof.
- Verification:
- Static source scan completed.
- No compile launched. Reason: Phase 0 was doc-only; no C# runtime source was modified, and project instructions forbid unnecessary build launches.
- Runtime/profiler proof remains pending.
- 2026-05-23 - Phase 0 Re-Entry Verification
- The same Phase 0 directive was received again. Without a disk-backed check, this could cause duplicate archaeology or false task progress.
- Re-read Docs/Tasks/StatusX005.md.
- Verified task count remains 10.
- Verified Phase 0 artifacts still exist: Docs/Reports/KINEMATICCOLLISIONLEDGERX005.md and Docs/AgentLogs/LOGX005.md.
- Runtime saved: 0 us.
- Phase 0 remains static-scan complete for Tasks 01-03.
- Tasks 04-10 remain pending.
- No compile launched because no C# runtime source changed.
- HydrodynamicKccRuntime was native/Burst-heavy but still used CapsulecastCommand.ScheduleBatch and RaycastHit extraction, so calling it pure SDF was false.
- Vehicle/VR/Contextual IK command bridges still exist and must not be hidden behind a fake "all clean" report.
- Replaced the Hydro KCC command/extract stage with BuildSdfCollisionHitsJob, sampling ShinobuKccEnvironmentSdf and writing speculative SDF contact hits into the existing native resolution path.
- Updated Docs/Reports/KINEMATICCOLLISIONLEDGERX005.md, Docs/Tasks/StatusX005.md, and Docs/AgentLogs/RationaleX005.md.
- Continuous GlobalQualityWeight scales SDF sample count from low-cost survival to richer high-tier contact sampling without changing DTO layout.
- Profiler proof not available in Loop 2 because build/profiling was blocked by CPU policy.
- Expected removed cost in Hydro-active player route: one Hydro CapsulecastCommand.ScheduleBatch plus one RaycastHit extraction pass, target 120-380 us/frame on i3/MX350 class hardware.
- Expected removed player fallback cost when Hydro authority is live: player motor sweep batch and kinematic repair ray batch are no longer scheduled; exact value pending profiler.
- Runtime cost of scanner/report updates: 0 us.
- git diff --check passed for touched files; only CRLF normalization warnings.
- Tools/OOPKccScannerX005.py result: Hydro KCC forbidden command hits = 0; residual scoped findings = 1 collision callback symbol, 6 command schedules, 38 command type references, 6 linearVelocity writes.

FILE LOG_X_006.md bytes=174126 errors=222 warnings=98 successMarkers=222 selected=34
- What was wrong: No X006 status/rationale files existed in active task/log folders at session start. The active prompt had to be extracted from CURRENTBATCH before any architecture decision.
- What was done: Extracted X006 from Docs/Tasks/CURRENTBATCH.md with CLI regex, confirmed 10 tasks, checked Echelon 2 domain boundary, and loaded 8 task-relevant mandates.
- Cinematic Cheats used: None implemented yet. Dear Lie shader dissolve remains Phase 0 design target.
- Exact Microseconds saved: 0 us measured. Static archaeology only; runtime proof absent.
- Verification: PENDING VERIFICATION. Compile not run; no code mutation yet.
- What was wrong: The active voxel deformation path is only partially asynchronous. HectonVoxelEngine schedules jobs and yields, but still allocates build-sized Persistent buffers during rebuild and performs MeshData upload on the main thread. VoxelDeltaProcessor schedules CarveSdfJob, but commits authoritative writes through main-thread managed chunk dictionaries. Renderer damage volume/cut mask infrastructure exists, but AbyssalVoxelRock uses it for scar/fresh-cut shading rather than Dear Lie carve clipping.
- What was done: Audited HectonVoxelEngine, VoxelDeltaProcessor, VoxelSurfaceNets, H8BinaryWorldPager, WorldChunkResidencyManager, SargassumCutManager, AbyssalVoxelRock, VoxelBakeGhost, and TerrainMaster. Wrote the Phase 0 target list to Docs/Reports/VOXELPHASE0TARGETLISTX006.json. Updated StatusX006 and RationaleX006 with closed tasks 01-03 and decisions 004-006.
- Cinematic Cheats used: No runtime cheat implemented in Phase 0. Required cheat path is identified: reuse existing damage volume/cut mask payloads for immediate GPU clip/depth/shadow parity, while mesh rebuild and authoritative chunk delta commit trail asynchronously.
- Exact Microseconds saved: 0 us measured in Phase 0. Target savings after implementation: remove rebuild/allocation/upload spikes greater than 1000 us on weak hardware, keep per-frame carve commit/upload slices under 100 us unless profiler proof allows more, and route immediate visual response through GPU stamps rather than main-thread geometry.
- Verification: STATIC COMPLETE. Compile not run because runtime source was not changed. Prompt re-extracted from CURRENTBATCH via CLI and task count remained 10.
- What was wrong: The same Phase 0 order was reissued. A first revalidation regex checked only the exact opening tag and failed because the real tag contains additional attributes.
- What was done: Re-ran CLI extraction against Docs/Tasks/CURRENTBATCH.md with an attribute-tolerant AGENTPROMPT regex, confirmed X006 prompt length 11973 chars and task count 10. Rechecked Phase 0 artifact state: target list has 11 targets, closed tasks are 1,2,3, compilerun=false.
- Cinematic Cheats used: None newly implemented. Dear Lie remains identified as Phase 1 shader/GPU clip work using existing damage volume/cut mask substrate.
- Exact Microseconds saved: 0 us measured. No runtime source changed.
- Verification: REVALIDATED. Phase 0 remains complete; no duplicate archaeology pass needed.
- What was wrong: The codebase could not honestly prove the voxel stack was monolithic or Zero-GC. Existing evidence showed managed chunk dictionaries in VoxelDeltaProcessor, runtime NativeArray allocation sites in HectonVoxelEngine/VoxelDeltaProcessor, main-thread Mesh.ApplyAndDisposeWritableMeshData calls, and a synchronous MeshCollider.sharedMesh fallback when deferred collider registration failed. Shader damage data existed, but stale voxel/terrain geometry was not clipped consistently in forward, shadow, and depth passes.
- What was done: Added Dear Lie damage-volume clip parity to HectonAbyssalVoxelRock forward/shadow/depth, TerrainMaster forward/shadow/depth/depth-normals, and HectonVoxelBakeGhost forward. Removed the immediate PhysX sharedMesh fallback from failed deferred collider registration in HectonVoxelEngine. Added VoxelCarvingTortureJob for deterministic 60 Hz synthetic carve pressure. Updated VoxelDeltaProcessor black-box dump path for X006. Added Tools/OOPVoxelScanner.py and generated Docs/Reports/VOXELOPTIMIZATIONREPORTX006.json with hard pass/fail gates.
- Cinematic Cheats used: Existing damage-volume GPU route is now used as the immediate visual authority for carved holes while the authoritative SDF/mesh/persistence paths trail behind. No new per-cut GraphicsBuffer was added; the existing bounded stamp route remains the single visual payload route.
- Exact Microseconds saved: 0 us measured. Static proof only. Expected visual-path saving is avoiding visible wait on mesh regeneration for a 60 Hz laser. Stress math: 7200 frames over 120 seconds, one laser stamp per frame, bounded by 16 damage-volume stamps per frame; worst-case 32^3 chunk RLE packet is 40 + 32768 8 = 262184 bytes; H8 pager write queue remains bounded by existing write slots/queue capacity.
- Verification: OOP scanner result is FAILSTATICREMAININGHOTPATHS. Passed gates: Dear Lie shader clip present, Graphics stamp route bounded, pager write queue bounded, RLE packet aligned, sync PhysX registration fallback removed, UnsafeUtility.Malloc absent in active voxel runtime scan, torture job present, X006 dump path present. Failed gates: managedchunktrackingabsent, hotnativeallocationsabsent, meshuploadmainthreadabsent. git diff --check passed on touched files with line-ending warnings only. dotnet/Unity compile was not run because CPU load was 100%, and project rule forbids launching dotnet build above 50% CPU load.
- What was wrong: The previous scanner proved bounded routes too coarsely and did not expose the exact stress ceiling requested by the CTO. It also did not separate SurfaceNets DataVault scratch allocation from active dirty-chunk SDF recycling, which can create a false "pool exists therefore chunk recycling is solved" conclusion.
- What was done: Expanded Tools/OOPVoxelScanner.py and regenerated Docs/Reports/VOXELOPTIMIZATIONREPORTX006.json. The report now contains: exact 60Hz/120s laser stamp math, damage-volume bandwidth, WorldPager write arena limits, SurfaceNets Vault byte ledger, VoxelDeltaProcessor dirty-chunk byte ledger, RLE packet offsets, and explicit residual PhysX collider assignment sites.
- Cinematic Cheats used: Dear Lie remains bounded through the existing damage-volume stamp route. One laser at 60Hz consumes 1 of 16 same-frame damage stamp slots; the damage-stamp GraphicsBuffer ceiling is 16 32 B = 512 B. Default damage-volume ping-pong traffic is 2097152 B/dispatch, 125829120 B/s at 60Hz. Max configured damage volume is 25165824 B/dispatch, 1509949440 B/s at 60Hz, so Ultra only.
- Exact Microseconds saved: 0 us measured. This was a proof/audit pass, not a profiler run. Bounded memory facts: H8BinaryWorldPager write arena is 32 262080 B = 8386560 B; SurfaceNets Vault preallocates 3335708 B. Dirty chunk state remains 135168 B per dirty chunk with no hard cap proven.
- Verification: OOP scanner result remains FAILSTATICREMAININGHOTPATHS. Failed gates: managedchunktrackingabsent, hotnativeallocationsabsent, meshuploadmainthreadabsent, deformationcollidermainthreadassignmentabsent, rleworstcasefitssinglepagersector, globaldatavaultdirtychunkrecyclerproven. RLE native snapshot worst-case 32^3 one-cell-run packet is 262184 B, exceeding sector payload 262080 B by 104 B. Compile not run because CPU load was 100% and dotnet/csc processes were active.
- What was done: Updated VoxelDeltaProcessor native snapshot measurement/writing to select dense delta snapshot when sparse RLE is larger than dense. Added aligned dense delta writers for dirty and compacted chunks using the existing 40 B NativeSnapshotChunkHeaderDeltaRle with payload hash. Updated OOPVoxelScanner.py and regenerated VOXELOPTIMIZATIONREPORTX006.json.
- Cinematic Cheats used: None. This is persistence correctness, not visual fakery.
- Exact Microseconds saved: 0 us measured. Memory result: effective worst-case chunk payload is now 135208 B, leaving 126872 B inside the 262080 B sector payload. Queue remains bounded at 8386560 B write arena.
- Verification: OOP scanner still returns FAILSTATICREMAININGHOTPATHS, but rleworstcasefitssinglepagersector is now passing. Remaining failed gates: managedchunktrackingabsent, hotnativeallocationsabsent, meshuploadmainthreadabsent, deformationcollidermainthreadassignmentabsent, globaldatavaultdirtychunkrecyclerproven. git diff --check passed for touched voxel files with line-ending warnings only.
- What was wrong: VoxelDeltaProcessor dirty chunks still allocated ChunkDeltaState NativeArrays on first touch. A sustained 60 Hz drill or scooter traversal could hit new dirty chunks under frame pressure, causing allocation spikes before persistence compaction could drain them.
- What was done: Added a fixed dirty chunk state lease pool in VoxelDeltaProcessor. Capacity is 256 slots. Per slot native storage is 135168 B: DirtyMaskWords 4096 B, SdfValueBits 65536 B, MaterialIds 32768 B, CellFlags 32768 B. Total prewarmed native storage is 34603008 B. Load/carve paths now use TryGetOrCreateChunkState and fail closed on pool exhaustion; compaction returns dirty states to the pool. IsPooled now requires created native buffers, so a default struct cannot be mistaken for slot 0.
- Cinematic Cheats used: None. This is authoritative deformation state ownership. Dear Lie remains the visual latency mask.
- Exact Microseconds saved: 0 us measured. Expected saving is removal of first-touch dirty-chunk NativeArray allocation from the live carve path. Memory ceiling for this local pool is now explicit at 34603008 B.
- Verification: OOP scanner records fixeddirtychunkpoolpresent=true, localpoolhardcapacityproven=true, fixeddirtychunkpoolcapacity=256, fixeddirtychunkpoolnativebytes=34603008. Verdict remains FAILSTATICREMAININGHOTPATHS because managed dictionaries, compaction NativeArray allocations, main-thread mesh upload, late MeshCollider.sharedMesh assignment, and lack of a GlobalDataVault dirty-chunk recycler remain. git diff --check passed on touched files with line-ending warnings only. dotnet/Unity compile was not run because CPU load was 70-99% during checks and dotnet processes were active.

FILE LOG_X_007.md bytes=98315 errors=64 warnings=31 successMarkers=80 selected=34
- Heavy mathematical solver risk was undocumented for X007. Prompt-named systems crossed physiology, gas dynamics, power, thermodynamics, boids, and celestial routes. Direct edits would have risked changing gameplay authority without residual proof.
- Parsed Docs/Tasks/CURRENTBATCH.md for <AGENTPROMPT id="X007". Created Docs/Tasks/StatusX007.md and Docs/AgentLogs/RationaleX007.md. Loaded eight mandates before source work. Generated Docs/Reports/MATHLODCOMPLEXITYLEDGERX007.json from a static scan of Assets/Project/Scripts. Wrote Docs/Reports/MATHLODPHASE0REPORTX007.md with priority files, residual bounds, and the GlobalQualityWeight route decision.
- Cinematic Cheats used:
- Exact Microseconds saved:
- 0 verified microseconds. No runtime code was changed. Potential savings are PENDING VERIFICATION and must be measured after Phase 1 patches.
- Proof artifacts:
- Compile:
- Not run. Phase 0 wrote documentation/report artifacts only; no C# runtime code was modified.
- The Phase 0 report gave residual direction but did not prove the patched decompression exponent against float residuals, did not remove quality-dependent decompression tissue state changes, and did not expose the requested continuous 2..50 Jacobi range in power/logistics and thermal solvers.
- Added ShinobuPhysiologyJobMath.ApproxExpNegPade33Reduced(float4) and replaced the decompression math.exp(-effectiveK dt) hot path. Forced decompression authority to the runtime 3-lane tissue count for every quality weight so a GlobalQualityWeight drop from 1.0 to 0.1 cannot alter tissue state directly. Added continuous Jacobi curves for power/logistics and abyssal thermodynamics: iterations(q)=round(lerp(2,50,qq(3-2q))), omega 0.55..0.92, and tolerance from survival-loose to overkill-strict.
- Rejected for decompression authority. The cheat is allowed only in telemetry/visual lanes later. For Jacobi, low quality uses fewer damped relaxation passes; it does not claim convergence.
- 0 verified microseconds. Build/profiler run was blocked because csc.exe was already running and CPU sampled at 100%. Theoretical saving: one SFU exp removed per 4 decompression tissues; low-quality Jacobi avoids up to 48 scheduled passes versus q=1.0.
- Numerical proof:
- PadÃ© [3/3] range-reduced float scan: max abs error [0,1] = 4.152223150E-007; max abs error [0,4] = 7.629343334E-007. Physiological bounded worst-case x=0.147871399: exact 0.862542032, approx 0.862542093, abs error 6.080794979E-008.
- Branch proof:
- Approximation core has no if; it uses math.select, min, max, rcp, and saturate. Full jobs are not branchless: static audit found 360 if ( occurrences across the audited physiology, power/logistics, power Jacobi, and thermal files. Those are topology, bounds, and fault-isolation branches.
- Docs/Reports/MATHLODRESIDUALPROOFX007.md
- Not run by rule. Existing csc.exe process and CPU 100% prohibit launching another build.
- The user repeated the Phase 0 bootstrap directive. Proceeding from memory would violate the batch prompt protocol and could hide the fact that Phase 0 had already completed and Phase 1 Tasks 04-05 were partially patched.
- Verified there is no root currentbatch.md or CURRENTBATCH.md. Re-extracted <AGENTPROMPT id="X007" from Docs/Tasks/CURRENTBATCH.md, lines 1089..1131, task count 10. Re-scanned Assets/Project/Scripts for direct transcendental calls and wrote Docs/Reports/MATHLODPHASE0REVALIDATIONX007.md.
- 0 verified microseconds. This was a static revalidation pass.
- Not run. No new C# runtime patch was made in this revalidation pass.
- 2026-05-23 APEX Proof Correction
- SubmarineOsThermalGridRuntime.SelfAuditArchitecture() still expected constant tolerance, omega, and residual-mask values after the solver was changed to continuous Math LOD curves. That made the proof layer inconsistent with the runtime curve.
- Updated the self-audit to validate monotonic low/mid/high behavior: iterations 2 - 26 - 50, omega 0.55 - 0.735 - 0.92, tolerance 0.032 - 0.01625 - 0.0005, and residual sampling mask trending down from low to high quality. Re-ran numeric residual checks and appended the new finite-extreme table to Docs/Reports/MATHLODRESIDUALPROOFX007.md.
- None. This was a proof and self-audit correction, not a visual fake.
- 0 verified microseconds. Build/profiler still pending. CPU sampled at 70.9242930873432%, above the 50% build gate.
- Proof:
- PadÃ© decompression worst physiological bounded error remains 6.080794979E-008. Historical note: this pass still treated all non-finite exponent inputs as the x=0 fallback. Loop 29 later corrected +Infinity so positive overflow clamps to the maximum finite decay side 0.01831487938761711; NaN and -Infinity still resolve to the safe fallback side 1.0.
- Not run by rule; CPU was above the allowed threshold.
- The repeated APEX challenge required checking current source anchors because the working tree is dirty and line numbers moved.
- Revalidated current code locations: ApproxExpNegPade33Reduced is in Assets/Project/Scripts/Physiology/ShinobuPhysiologyJobs.cs:101; decompression uses it at :789; power Jacobi iteration curve is in Assets/Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs:52; runtime iteration usage is at :667; continuous-curve self-audit is at :1196.
- 0 verified microseconds. CPU sampled at 100%, so no build or profiler run was allowed.
- Not run by rule. CPU was above the allowed threshold.

FILE LOG_X_008.md bytes=173637 errors=39 warnings=73 successMarkers=189 selected=34
- The previous armor path already had a 64B ArmorProfileDTO, CAS health mutation, and deferred DeflectSignal/ImpactSignal routes, but the 48-byte LUT was spatial: local hit UV - row/column - row 8 + col.
- Read required mandates: damage/VFX, ARM64 DTO, Zero-GC, native jobs, signal lanes, execution phases, AUP determinism, black-box telemetry.
- Generated Docs/Reports/COMBATDAMAGEPIPELINETARGETLISTX008.json with 2375 C# files scanned, 488 relevant files, target file list, call-site findings, and explicit Roslyn host-binding failure status. No fake AST success claimed.
- Added Docs/ARCHITECTURE/X008COMBATARMORLUTROUTECARD.md.
- Cinematic cheats used:
- Used dot-product angle quantization instead of trig angle-of-attack.
- Exact microseconds saved:
- PENDING VERIFICATION. No profiler or compile artifact exists.
- Verification:
- Compile not run. Build guard sampled 100% CPU and active dotnet/csc processes from another session.
- Task 05 is not fully done because the production route is still ProcessDamageQueueJob : IJob; the requested EvaluateArmorPenetrationJob : IJobParallelFor transaction split remains pending.
- Task 08 10,000-hit harness pending.
- Task 09 dump filename/black-box expansion pending.
- Task 10 final metric validator pending.
- User repeated the Phase 0 decree after Loop 1-2 artifacts already existed.
- Re-read Docs/Tasks/StatusX008.md.
- Re-extracted Docs/Tasks/CURRENTBATCH.md <AGENTPROMPT id="X008" by CLI regex. Prompt found, 10 tasks, mandatory constraints present.
- Preserved current state instead of restarting and overwriting evidence.
- 0 us. No runtime change.
- Disk state confirms Tasks 01-04 done, Task 05 pending full IJobParallelFor transaction split, compile still pending build guard clearance.
- 2026-05-23 Proof-Debt Pass / Angle and CAS Challenge
- Previous wording was too broad. The LUT index core is branchless at source level, but the whole ProcessDamageQueueJob : IJob is not branchless because queue drain, target resolution, shield/status/death handling, feedback gates, and CAS success/failure are conditional.
- ResolveArmorSurfaceNormal still used nested source-level ?: selection before the LUT index. That was not trig, but it was branch-shaped code in the lookup preparation.
- The CAS proof was incomplete. Eight retry attempts are atomic per successful write, but do not mathematically guarantee 100 concurrent same-target pellet writers all commit in a true parallel apply phase.
- Gameplay/Combat forbidden trig result: 0.
- Project-wide acos/asin inventory result: 11, all outside X008 armor route (IK, editor bake, celestial, player movement).
- Not claimed. Static proof only. Unity import, Burst disassembly, profiler, GCMonitor, and pellet torture harness have not run.
- Build was not launched because 7 active dotnet processes existed. Project rule forbids starting another build under that condition.
- Current proof:
- CAS invariant: successful CompareExchange is linearizable and health is monotonic non-increasing. Debt remains for true 100-writer same-slot parallel apply; solution must be per-target aggregation or dispatcher-owned retry, not an 8-try claim.
- 2026-05-23 CAS Closure Pass / 100-Pellet Same-Slot Proof
- The old CAS helper used 8 attempts. That is not enough to prove correctness when 100 pellets race against one target health float in a future parallel apply phase.
- Changed TryAtomicSubtractHealth in HectonCombatRuntimeArmorPenetration.cs to loop to AtomicHealthCasRetryLimit instead of 8.
- Regenerated Docs/Reports/COMBATOPTIMIZATIONREPORTX008.json with CAS source evidence.

FILE LOG_X_009.md bytes=76396 errors=7 warnings=61 successMarkers=33 selected=34
- Status authority is fragmented through uint StatusFlags and uint ActiveTraumaMask; the requested ulong StatusEffectMask does not exist in the hot physiology contract.
- SlowTick and ColdTick already exist in SystemDispatcher at 0.1s and 1.0s. Physiology is not using the 10 Hz lane yet.
- Some runtime publication still uses GlobalSignals.Publish; typed SignalBus lanes exist and are already used for selected physiology/damage/hypoxia outputs.
- Read selected mandates for survival pressure/O2 logic, ARM64 DTO layout, zero-GC, native jobs, dispatcher phases, registry/signal doctrine, blackbox telemetry, and AUP.
- Built Docs/Reports/PHYSIOLOGYOPTIMIZATIONREPORTX009.json with file/line targets, replacement route, DTO byte layout, status bit allocation, and cadence/signal map.
- Updated Docs/Tasks/StatusX009.md with Task 01 blocked for AST execution, Task 02 complete, Task 03 complete.
- Updated Docs/AgentLogs/RationaleX009.md with non-fluff decision notes and rejected alternatives.
- Cinematic Cheats used:
- 3 tissue lanes replace medical fidelity: fast blood/lung, medium muscle/organ, slow bone/fat. This is enough for warning/damage timing if threshold multipliers are tuned and stress-tested.
- Presentation smoothing is moved to VISUALSYNC/UI. Truth stays 10 Hz; visual alarms can interpolate without mutating gameplay state.
- Quality scaling affects telemetry/presentation density only. It must not change status bit authority, DTO layout, save identity, or decompression damage route.
- Exact microseconds saved:
- Phase 0 changed no runtime code, so measured saved time is 0 us.
- Estimated low-end gain after implementation: 35-80 us per active player-scale solve on i3/MX350-class CPU, pending profiler proof.
- Verification:
- Compile not run. CPU guard reported 100 and no dotnet/csc process was launched.
- AST not completed. Direct Roslyn load failed with Roslyn.Utilities.StringTable initializer exception; dotnet fallback was blocked by CPU rule.
- 2026-05-23 Phase 0 - Call Graph Pass
- The previous Phase 0 report had target files and DTO/cadence design, but the physiology core call graph was not explicit enough for surgical Phase 1 work.
- CPU guard still blocks compiler-backed AST work: latest CPU sample was 99.
- Re-read StatusX009.md, RationaleX009.md, and the X009 prompt from Docs/Tasks/CURRENTBATCH.md.
- Captured current chained path: Tick - MockEnvironmentDropJob - GenerateMockBreathingGasJob - CalculatePartialPressuresJob - PhysiologySignalIngestJob - IntegrateBloodGasTensionsJob - CalculateCnsToxicityJob - OxygenConsumptionJob - LateFrameTick finalize - telemetry/publish/dump.
- Captured replacement path: ISlowTickable.SlowTick - 3-lane physiology job - ulong status job - no-wait ILateFrameTickable finalize - typed SignalBus publication.
- Keep 10 Hz truth and move warning smoothness to presentation. This buys readable UI without simulating extra decompression compartments.
- Runtime saved time remains 0 us because no gameplay code was modified.
- 2026-05-23 Phase 0 - Revalidation Under CPU Guard
- The Phase 0 directive was repeated while Task 01 remains blocked specifically on compiler-backed AST proof.
- CPU guard returned 100. Project law forbids launching dotnet/csc when CPU is above 50.
- Re-read Docs/Tasks/StatusX009.md.
- None. This pass was verification and state protection only.
- 0 us measured. No runtime code changed.
- Implementation target remains unchanged: 3 tissue lanes, ulong StatusEffectMask, 10 Hz SlowTick truth.
- 2026-05-23 Phase 0 - Active Compiler Guard
- The Phase 0 directive was repeated while the machine was already compiling or compiler-adjacent work was active.

FILE LOG_X_010.md bytes=5950 errors=4 warnings=3 successMarkers=14 selected=34
- Power/logistics/drainage authority paths still carried Jacobi-style multi-pass relaxation or naming around hot CSR solves.
- There was no X010 scanner proving two-pass propagation, open-circuit zeroing, and the 2000-node/6000-edge stress shape.
- Replaced hot solve loops in LogisticsNetworkGraph, ShinobuLogisticsRouter, and SumpPumpPipeGridRuntime with fixed two-pass delta propagation.
- Reduced PipeEdgeDTO to explicit 32-byte layout and kept layout validators for ARM64 offset proof.
- Added LogisticsGridTortureJob: unmanaged Burst IJob, 2000 nodes, 6000 edges, fixed two delta passes, 64-byte result summary.
- Added Tools/OOPFluidScannerX010.py; Tools/OOPFluidScanner.py routes to it. Latest report: PASS, failureCount 0, hotWarningCount 0, targetCount 76, hotPathCount 7.
- Wrote status/rationale artifacts: Docs/Tasks/StatusX010.md, Docs/AgentLogs/RationaleX010.md, Docs/Reports/LOGISTICSOPTIMIZATIONREPORTX010.json.
- Cinematic cheats used:
- Replaced convergence realism with deterministic two-pass delta approximation.
- Exact microseconds saved:
- Two-pass replacement versus ten-pass relaxation on 2000-node/6000-edge stress shape: 320 us static estimate on i3/MX350.
- 32-byte PipeEdgeDTO versus 64-byte edge payload: 12 us static estimate per 6000-edge sweep and 192 KB less hot edge footprint.
- Open/no-power fast path: 48 us static estimate per dead-grid tick.
- Active compartment cap versus whole-map traversal: 80 us static estimate when inactive map nodes exceed 4096.
- Managed traversal removal from hot proof scope: 35 us static estimate per hot solve.
- Scanner/status/rationale/report work: 0 runtime us.
- Verification:
- Static scanner PASS: python Tools/OOPFluidScanner.py.
- Stale target-symbol grep PASS for edited hot files.
- Compile not verified. One legal dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false failed before X010 files with 17 core route-symbol errors (CraftingSignalRoute, SimulationSignalRoute, SurvivalSignalRoute, AupSignalRoute). Retry blocked by active dotnet.exe/csc.exe and CPU 50%.
- Reran python Tools/OOPFluidScanner.py: PASS, failureCount 0, hotWarningCount 0, targetCount 76, hotPathCount 7.
- Rechecked build gate: CPU 65.20%, 92.02%, 94.02%; active dotnet.exe/csc.exe.
- Result:
- Compile retry remains blocked by explicit project rule.
- LOGX010 - Revalidation - 2026-05-23 - Pass 2
- Re-extracted <AGENTPROMPT id="X010" from Docs/Tasks/CURRENTBATCH.md; task count remains 10.
- Reran python Tools/OOPFluidScanner.py: PASS, failureCount 0, hotWarningCount 0, targetCount 76, hotPathCount 7, scannedFileCount 2379.
- Rechecked build gate after delay: CPU 94.60%, 95.37%, 95.18%; two active dotnet.exe processes.
- Compile retry remains forbidden by the explicit CPU/process gate.
- LOGX010 - T.A.R.S. Stress Proof - 2026-05-23
- Previous proof did not execute a moving short-circuit storm.
- Repeated blackout/open-circuit frames still risked rewriting zero buffers, which is not sub-microsecond on weak hardware.
- A project-wide zero-Jacobi release claim would be false because thermal/legacy release files still contain Jacobi/relaxation symbols outside X010.
- Added latched zero-state fast path to LogisticsNetworkGraph: first transition commits zero state; unchanged idle unpowered/open frames return with 0 node writes and 0 edge visits.

FILE LOG_X_011.md bytes=132087 errors=68 warnings=98 successMarkers=41 selected=34
- LOG X011 - VOCALWARNINGANDSUBTITLESTREAMLINER
- 2026-05-23 - Phase 0 Through Static Proof Pass
- VocalWarningSystem retained a heap-style priority route for a five-alarm domain. The route had more state mutation than needed and made low-count scheduling look like a general OS priority queue problem.
- SubtitleManager retained a managed string lane: SubtitleRequest, stringQueue, currentMessage, lastEnqueuedMessage, ShowImmediate(string), and string-based display corruption. Public string callers could feed the route into retained managed strings.
- Existing proof state was prose-heavy. No X011 scanner, target JSON, or deterministic 50-trigger storm report existed.
- Replaced the VWS queue state with VocalWarningPriorityState.VwsPriorityWord plus one VocalWarningDTO slot per bit index. Canonical warnings map to bits 63..59. Highest priority resolves by high-bit scan, not heap sift.
- Rewired VWS dispatch to publish SignalBus<VocalCueSignal and SignalBus<SubtitleCueSignal from the resolved priority-word candidate. Rejection marks the priority-state fault flags and can trigger telemetry dump.
- Removed SubtitleManager's legacy managed string queue/current-message path. Public DisplaySubtitle(string) and notification strings now copy immediately to the pooled ReadOnlySpan<char / BufferedSubtitleRequest route. Rendering remains ApplySubtitleBuffer - TMP SetCharArray.
- Added OOPVoiceScannerX011 and report Docs/Reports/UXOPTIMIZATIONREPORTX011.json. Static forbidden hot-route findings: none for NativeMinHeap, VocalWarningHeapOps, managed subtitle string queue, direct .text writes, new string, or string.Format in the VWS/subtitle files.
- Added VocalWarningStormTortureX011 and report Docs/Reports/UXVWSSTORMTORTUREX011.json. Static deterministic storm: 50 triggers collapse to 5 active bits, priorityWordHex 0xF800000000000000, highestBit 63.
- Added/updated Docs/Tasks/StatusX011.md and Docs/AgentLogs/RationaleX011.md with DOD, rejected alternatives, scalability, and build-gate status.
- Restored Assembly-CSharp project assets under guard and verified dotnet build Assembly-CSharp.csproj with 0 warnings and 0 errors.
- Cinematic Cheats used:
- Subtitle synchronization uses audio-frame timestamps and fixed char buffers. Text polish scales by quality tier; text ownership and DTO identity do not change.
- Exact Microseconds saved:
- Measured microseconds saved: 0 us claimed. Unity profiler/GCMonitor/player proof not run.
- Build gate: first build attempt failed before compile with NETSDK1004 because Temp/obj/Assembly-CSharp/project.assets.json was missing. Guarded restore generated it. dotnet build Assembly-CSharp.csproj then passed with 0 warnings and 0 errors.
- Static operation delta: heap sift/sort path removed from VWS hot route; priority fetch is one 64-bit scan over VwsPriorityWord. Exact frame-time value remains PENDING PROFILER.
- Verification artifacts:
- Static scan command found no forbidden hot-route tokens in VocalWarningSystem.cs, SubtitleManager.cs, and BabelSubtitleSyncRuntime.cs.
- git diff --check on touched files returned no whitespace errors; line-ending warnings only.
- dotnet build Assembly-CSharp.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false: PASS, 0 warnings, 0 errors.
- No Unity editor Play Mode, player build, profiler, or GCMonitor evidence exists from this session.
- New Editor scanner/harness files may require Unity project-file regeneration before they appear in generated csproj compile coverage.
- The first UX optimization JSON was accurate for owned hot-route files, but too narrow for the literal Phase 0 order to parse Assets/Project/Scripts and build a source-backed audio/subtitle route graph.
- Full-tree searches produce noisy findings: heap keywords still exist in AI, construction, economy, world, and editor harness files. Those are not VWS/subtitle runtime ownership, but leaving them unclassified would make the report ambiguous.
- Added owned hot-route files and source-backed route map: VocalWarningSystem signal snapshots - VwsPriorityWord - SignalBus<VocalCueSignal and SignalBus<SubtitleCueSignal - BabelSubtitleSyncRuntime/SubtitleManager.
- Reconfirmed: owned VWS/subtitle hot-route forbidden findings remain empty.
- No new runtime cheat was introduced in this delta. The existing cheat remains the single 64-bit priority word for five alarms, with pooled subtitle buffers and audio-frame token sync.
- Measured microseconds saved: 0 us claimed.
- This delta was documentation/reporting only; no runtime code changed.
- Runtime build proof remains the prior guarded pass: dotnet build Assembly-CSharp.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false, 0 warnings, 0 errors.
- Full-tree string-risk count is intentionally broad and includes unrelated UI/editor .text, .ToString(), and string creation patterns. It is not a hot-route GC proof.
- Unity profiler, GCMonitor, Play Mode, and player-build proof remain pending.

FILE LOG_X_012.md bytes=58493 errors=11 warnings=2 successMarkers=81 selected=34
- Domain: root/Docs documentation cleanup, actualization, archive routing
- Evidence: STATICDOC / STATICSOURCE / STATICFILESYSTEM / OFFLINEVALIDATOR
- Build launched: no
- Root anchors MASTERRELEASEWORKPLAN.md and BUILDPLAYTESTISSUES.md were active mega-ledgers: 154853 and 148692 bytes before compression.
- Active reports mixed current evidence with historical markdown snapshots; active docs had to scan stale report prose before current contracts.
- Active docs contained stale source facts: prompt/report SignalBusRegistry=256, H8DM header 16, Data Monolith payload absence.
- BUILDPLAYTESTISSUES.md - Docs/DEPRECATED/RootBloatX0122026-05-23/BUILDPLAYTESTISSUES.md
- Added proof scripts:
- SignalBusRegistry.LaneCapacity = 512
- Proof
- finalPass=true
- source sync pass: true
- pass=true
- Cinematic Cheats Used
- Documentation cheat: historical report internals were not rewritten. They were marked [ARCHIVE] and removed from active validation scope, preserving evidence while cutting active context load.
- Exact Microseconds Saved
- Runtime frame time: 0 us; no C# or frame-path code changed.
- Low-end i3/MX350 runtime gain: 0 us.
- Middle/high/ultra runtime gain: 0 us.
- Documentation context retired: 389278 words from active corpus. This is search/read overhead reduction, not frame-time performance.
- <signalbusregistrylanecapacity512</signalbusregistrylanecapacity
- <oopdocscanner finalPass="true" sourceSyncPass="true" activeStaleParameterFiles="0" /
- <verifydocstructure pass="true" duplicateHeaderFiles="0" brokenLinkFiles="0" fenceIssueFiles="0" staleParameterFiles="0" encodingWithoutUtf8Sig="0" /
- The prompt example SignalBus 256 remained stale against source; current source is SignalBusRuntime.LaneCapacity = 512.
- Scripts were used only for discovery, validation, and proof JSON.
- SignalBusRuntime.LaneCapacity = 512.
- Documentation cheat: keep historical proof in JSON/archive, keep active specs as short route facts.
- Runtime frame time: 0 us.
- Documentation-context reduction: 54.908927175155206% active text reduction by scanner proof.
- The prompt example SignalBus 256 remains stale against source; current source is SignalBusRuntime.LaneCapacity = 512.
- Documentation cheat: convert long prose/list facts into compact bullets/tables and retain proof as JSON.
- Documentation-context reduction: 54.90493032456174% active text reduction by scanner proof.
- Active architecture specs still had 319 paragraphs/list/table blocks at =35 words after the 40-word pass.
- Tools/OOPDocScanner.py still enforced older architecture density limits instead of the new 35-word proof target.

FILE player_recover1.patch bytes=13341 errors=0 warnings=0 successMarkers=0 selected=5
- diff --git a/Assets/Project/Scripts/Audio/AcousticReverbPresetTrigger.cs b/Assets/Project/Scripts/Audio/AcousticReverbPresetTrigger.cs
- a/Assets/Project/Scripts/Audio/AcousticReverbPresetTrigger.cs
- +++ b/Assets/Project/Scripts/Audio/AcousticReverbPresetTrigger.cs
- playerRuntime = playerContext ?? (useRegistryFallback ? GlobalRegistry.Player : null);
- + playerRuntime = playerContext ?? (useRegistryFallback ? Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext : null);

FILE player_recover2.patch bytes=14637 errors=0 warnings=0 successMarkers=0 selected=15
- diff --git a/Assets/Project/Scripts/Gameplay/ScannerDataMiningRouter.cs b/Assets/Project/Scripts/Gameplay/ScannerDataMiningRouter.cs
- a/Assets/Project/Scripts/Gameplay/ScannerDataMiningRouter.cs
- +++ b/Assets/Project/Scripts/Gameplay/ScannerDataMiningRouter.cs
- diff --git a/Assets/Project/Scripts/Physiology/ShinobuSensoryImpairmentRuntime.cs b/Assets/Project/Scripts/Physiology/ShinobuSensoryImpairmentRuntime.cs
- a/Assets/Project/Scripts/Physiology/ShinobuSensoryImpairmentRuntime.cs
- +++ b/Assets/Project/Scripts/Physiology/ShinobuSensoryImpairmentRuntime.cs
- diff --git a/Assets/Project/Scripts/Physiology/ShinobuSuitIntegrityRuntime.cs b/Assets/Project/Scripts/Physiology/ShinobuSuitIntegrityRuntime.cs
- a/Assets/Project/Scripts/Physiology/ShinobuSuitIntegrityRuntime.cs
- +++ b/Assets/Project/Scripts/Physiology/ShinobuSuitIntegrityRuntime.cs
- diff --git a/Assets/Project/Scripts/PlayerBuilder.cs b/Assets/Project/Scripts/PlayerBuilder.cs
- a/Assets/Project/Scripts/PlayerBuilder.cs
- +++ b/Assets/Project/Scripts/PlayerBuilder.cs
- diff --git a/Assets/Project/Scripts/UI/AcousticEcholocationTranslator.cs b/Assets/Project/Scripts/UI/AcousticEcholocationTranslator.cs
- a/Assets/Project/Scripts/UI/AcousticEcholocationTranslator.cs
- +++ b/Assets/Project/Scripts/UI/AcousticEcholocationTranslator.cs

FILE player_recover3.patch bytes=12971 errors=0 warnings=0 successMarkers=9 selected=24
- diff --git a/Assets/Project/Scripts/UI/BuilderStatusOverlay.cs b/Assets/Project/Scripts/UI/BuilderStatusOverlay.cs
- a/Assets/Project/Scripts/UI/BuilderStatusOverlay.cs
- +++ b/Assets/Project/Scripts/UI/BuilderStatusOverlay.cs
- diff --git a/Assets/Project/Scripts/UI/DiegeticPdaFocusDistanceController.cs b/Assets/Project/Scripts/UI/DiegeticPdaFocusDistanceController.cs
- a/Assets/Project/Scripts/UI/DiegeticPdaFocusDistanceController.cs
- +++ b/Assets/Project/Scripts/UI/DiegeticPdaFocusDistanceController.cs
- diff --git a/Assets/Project/Scripts/UI/Navigation/DiegeticGyroCompassPhysicalBinding.cs b/Assets/Project/Scripts/UI/Navigation/DiegeticGyroCompassPhysicalBinding.cs
- a/Assets/Project/Scripts/UI/Navigation/DiegeticGyroCompassPhysicalBinding.cs
- +++ b/Assets/Project/Scripts/UI/Navigation/DiegeticGyroCompassPhysicalBinding.cs
- diff --git a/Assets/Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs b/Assets/Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs
- a/Assets/Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs
- +++ b/Assets/Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs
- diff --git a/Assets/Project/Scripts/UI/PDADeathMemoryDump.cs b/Assets/Project/Scripts/UI/PDADeathMemoryDump.cs
- a/Assets/Project/Scripts/UI/PDADeathMemoryDump.cs
- +++ b/Assets/Project/Scripts/UI/PDADeathMemoryDump.cs
- diff --git a/Assets/Project/Scripts/UI/PauseMenuController.cs b/Assets/Project/Scripts/UI/PauseMenuController.cs
- a/Assets/Project/Scripts/UI/PauseMenuController.cs
- +++ b/Assets/Project/Scripts/UI/PauseMenuController.cs
- diff --git a/Assets/Project/Scripts/UI/SonarHoloCompass.cs b/Assets/Project/Scripts/UI/SonarHoloCompass.cs
- a/Assets/Project/Scripts/UI/SonarHoloCompass.cs
- +++ b/Assets/Project/Scripts/UI/SonarHoloCompass.cs
- diff --git a/Assets/Project/Scripts/Visor/DynamicDecalVaultRuntime.cs b/Assets/Project/Scripts/Visor/DynamicDecalVaultRuntime.cs
- a/Assets/Project/Scripts/Visor/DynamicDecalVaultRuntime.cs
- +++ b/Assets/Project/Scripts/Visor/DynamicDecalVaultRuntime.cs

FILE player_recover4_final.patch bytes=8953 errors=0 warnings=0 successMarkers=0 selected=5
- CacheColdServices(GlobalRegistry.Player, GlobalRegistry.DataVault);
- + CacheColdServices(Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext, GlobalRegistry.DataVault);
- diff --git a/Assets/Project/Scripts/World/HectonCaveVoxelAmbientOcclusionController.cs b/Assets/Project/Scripts/World/HectonCaveVoxelAmbientOcclusionController.cs
- a/Assets/Project/Scripts/World/HectonCaveVoxelAmbientOcclusionController.cs
- +++ b/Assets/Project/Scripts/World/HectonCaveVoxelAmbientOcclusionController.cs

FILE Prompt_X_003_EXTRACTED_CURRENT.md bytes=12598 errors=2 warnings=1 successMarkers=3 selected=90
- <AGENTPROMPT id="X003" role="COMPILEWALLSMASHERANDDOMAINDECOUPLER"
- COMPILEWALLSMASHERANDDOMAINDECOUPLER, an Echelon 9 Integration and
- "Compile Wall." Because disparate systems (like Habitat, Physics, and Combat)
- line of code in the UI can trigger a 3-minute rebuild of the entire physics
- identify every concrete cross-domain dependency, rip out the direct class
- Contracts assembly, and rewire the communication to use these clean, abstract
- boundaries. You will stop systems from knowing how other systems work, forcing
- them to only know what data they produce and consume via the GlobalDataVault and
- SignalBus<T. You will be the enforcer of modularity, the champion of fast
- authority over the .asmdef files, the using statements, and the structural
- placement of DTOs and Interfaces. You must operate with relentless momentum,
- analyzing the compiler errors and dependency matrices with 100% honesty. You
- will not hide a bad dependency behind a #pragma warning disable; you will
- decoupled without a rewrite, you will explicitly document this failure and
- self-audit your work continuously by running simulated assembly compilations in
- your mind, verifying that changes in leaf nodes do not trigger rebuilds of the
- cyclic dependencies and zero unauthorized cross-domain concrete references.
- </AUTONOMYANDFREEWILLDIRECTIVE <MANDATORYCONSTRAINTS
- Config) or Interface that must be accessed by more than one distinct domain
- (e.g., read by both UI and Physics) MUST be physically moved into the
- assembly). These contract files must contain ONLY raw data definitions and
- logic, MonoBehaviours, or dependencies on heavy Unity packages.
- SIBLING DOMAIN ISOLATION: A runtime domain assembly (e.g.,
- another sibling runtime domain assembly (e.g., Hecton8.Vehicles.Physics).
- They may only communicate by resolving shared BufferIDs from the
- GlobalDataVault, pushing typed signals to the SignalBus<T, or looking up
- cached interfaces defined in the Contracts assembly. You must relentlessly
- ERADICATION OF CONCRETE CASTS: You must hunt down and destroy any code that
- different domain (e.g.,
- it must read a Vault buffer; if it needs to trigger action, it must send a
- ASMDEF HYGIENE AND OPTIMIZATION: You must meticulously audit every .asmdef
- file in the project. You must ensure autoReferenced is set to false for all
- domain assemblies to prevent them from silently polluting the global
- namespace. You must verify that overrideReferences and precompiled
- references are used correctly for external plugins (like Roslyn scanners) so
- they do not leak into the player runtime builds.
- THE COMPILE-WALL METRIC: You must track and prove the reduction of the
- compile wall. You must document the "Blast Radius" of key systems before and
- after your intervention. If modifying the PlayerHealth script previously
- caused 80 files to recompile, and after your changes it only causes 3 files
- to recompile, you must clearly document this victory in the architectural
- NO BASTARDIZATION OF THE VAULT: While pushing systems to use the
- GlobalDataVault for decoupling, you must not allow the Vault to become a
- dumping ground for managed objects or poorly defined arrays. Every DTO moved
- to the Contracts assembly must remain a strictly aligned, 16/32/64-byte
- enforced by other agents. </MANDATORYCONSTRAINTS
- <PHASE0ARCHITECTURALARCHAEOLOGY Task 01: COMPILATIONDEPENDENCYINQUISITION.
- You must initiate your mission by running a comprehensive static analysis of the
- the using directives within the C# files to map the actual, physical compilation
- assemblies that have accumulated too many inbound dependencies, causing the
- compile wall. You will generate a detailed JSON matrix exposing these illicit
- Task 02: DTOANDINTERFACECENSUS. You must scan the codebase for public
- assemblies but are widely accessed by external systems. Look for types like
- Task 03: HOTPATHREGISTRYPOLLINGDETECTION. Decoupling often leads lazy
- loops to find the systems they are no longer directly referenced to. You must
- architectural debts that must be replaced with cold, initialization-phase
- dependency caching or pure DataVault handle resolution.
- <PHASE1THEGREATDECOUPLING Task 04: CONTRACTASSEMBLYPOPULATION. You will
- DTOs, signal payloads, and interfaces identified in Task 02 into the
- will strip these files of any using directives that point back to the runtime
- Task 05: SIBLINGREFERENCEAMPUTATION. You will systematically open the .asmdef
- files of the major gameplay domains (e.g., Combat, AI, Vehicles, Environment)
- and mercilessly delete the references to their sibling domains. You will fix the
- resulting compiler errors not by restoring the reference, but by altering the C#
- code to rely on the newly extracted Contracts, SignalBus<T, or GlobalDataVault
- Task 06: COLDCACHEDEPENDENCYINJECTION. You will repair the hot-path registry
- polling identified in Task 03. You will rewrite the offending systems to
- Burst jobs operate exclusively on cached references or resolved Vault handles,
- Task 07: GENERATEDPROJECTFILEHYGIENE. Because Unity's internal generation of
- .csproj files can lag behind physical file movements, causing false-positive
- compile errors for external agents, you will meticulously verify that your file
- Directory.Build.targets bridges or explicit .meta file handling to ensure the CI
- pipeline and other agents can compile the project seamlessly.
- <PHASE2STRESSTESTINGANDFORENSICPROOF Task 08: DEPENDENCYCYCLEFUZZER.
- You must mathematically prove that your decoupling did not introduce circular
- script to perform a rigorous topological sort of the .asmdef graph. If a single
- fuzzer must exit with a fatal error code.
- Task 09: COMPILEWALLBLASTRADIUSMETRICS. You will document the precise impact
- of your work. You will select three previously highly-coupled files (e.g.,
- Radius"â€”the number of assemblies that would be forced to recompile if a single
- comment was changed in those files. You will provide the "Before" and "After"
- Task 10: AUTOMATEDMETRICVALIDATOR. You will finalize your work by generating a
- definitive proof artifact. You will ensure that the
- </PHASE2STRESSTESTINGANDFORENSICPROOF
- <POLISHMANDATE LISTEN TO ME. The Compile Wall is the silent killer of AAA
- single using Hecton8.Physics; inside the AI assembly. I do not want to see the
- UI assembly waiting for the Fluid Dynamics assembly to compile. You must be
- you will isolate it, mock its inputs, and leave it to fail closed, rather than
- allowing it to drag down the entire dependency graph. You will meticulously
- architectural philosophy behind every DTO you move. Your output must be a

FILE Restore_EXTERNAL_CODEX_loop140_editor.log bytes=38 errors=0 warnings=0 successMarkers=0 selected=1
- Determining projects to restore...

FILE save_manager_active_runtime.patch bytes=583 errors=0 warnings=0 successMarkers=0 selected=8
- diff --git a/Assets/Project/Scripts/SaveManager.cs b/Assets/Project/Scripts/SaveManager.cs
- a/Assets/Project/Scripts/SaveManager.cs
- +++ b/Assets/Project/Scripts/SaveManager.cs
- @@ -43,0 +44,1 @@
- + public static SaveManager ActiveRuntimeInstance { get; private set; }
- @@ -570,0 +572,3 @@
- +
- + if (ReferenceEquals(ActiveRuntimeInstance, this))

FILE save_manager_active_runtime_context.patch bytes=2073 errors=0 warnings=0 successMarkers=0 selected=3
- private static int signalPushDropCount;
- private const long MainThreadSnapshotBudgetMs = 5L;
- isBusy = false;

FILE save_manager_active_runtime_context2.patch bytes=2160 errors=0 warnings=0 successMarkers=0 selected=3
- private static int signalPushDropCount;
- private const long MainThreadSnapshotBudgetMs = 5L;
- isBusy = false;

FILE save_manager_active_runtime_minimal.patch bytes=516 errors=0 warnings=0 successMarkers=0 selected=8
- diff --git a/Assets/Project/Scripts/SaveManager.cs b/Assets/Project/Scripts/SaveManager.cs
- a/Assets/Project/Scripts/SaveManager.cs
- +++ b/Assets/Project/Scripts/SaveManager.cs
- @@ -43,0 +44,1 @@
- + public static SaveManager ActiveRuntimeInstance { get; private set; }
- @@ -570,0 +572,3 @@
- +
- + if (ReferenceEquals(ActiveRuntimeInstance, this))

FILE save_manager_active_runtime_zero2.patch bytes=583 errors=0 warnings=0 successMarkers=0 selected=8
- diff --git a/Assets/Project/Scripts/SaveManager.cs b/Assets/Project/Scripts/SaveManager.cs
- a/Assets/Project/Scripts/SaveManager.cs
- +++ b/Assets/Project/Scripts/SaveManager.cs
- @@ -43,0 +44,1 @@
- + public static SaveManager ActiveRuntimeInstance { get; private set; }
- @@ -570,0 +572,3 @@
- +
- + if (ReferenceEquals(ActiveRuntimeInstance, this))

FILE save_recover1_consumers.patch bytes=15481 errors=0 warnings=2 successMarkers=0 selected=7
- diff --git a/Assets/Project/Scripts/CrashTelemetryBuffer.cs b/Assets/Project/Scripts/CrashTelemetryBuffer.cs
- a/Assets/Project/Scripts/CrashTelemetryBuffer.cs
- +++ b/Assets/Project/Scripts/CrashTelemetryBuffer.cs
- Debug.LogWarning("[MainMenuController] Hecton8.Core.GlobalRegistry.Save is null. Save/Load features unavailable.");
- + Debug.LogWarning("[MainMenuController] Hecton8.SaveSystem.SaveManager.ActiveRuntimeInstance is null. Save/Load features unavailable.");
- Debug.LogError("[MainMenuController] Hecton8.Core.GlobalRegistry.Save is null. Cannot validate save file.");
- + Debug.LogError("[MainMenuController] Hecton8.SaveSystem.SaveManager.ActiveRuntimeInstance is null. Cannot validate save file.");

FILE save_recover2_remaining.patch bytes=12435 errors=0 warnings=0 successMarkers=0 selected=3
- diff --git a/Assets/Project/Scripts/UI/PauseMenuController.cs b/Assets/Project/Scripts/UI/PauseMenuController.cs
- a/Assets/Project/Scripts/UI/PauseMenuController.cs
- +++ b/Assets/Project/Scripts/UI/PauseMenuController.cs

FILE SIGNAL_ARCHITECTURE_OPTIMIZATION_REPORT_X_001.md bytes=24516 errors=2 warnings=4 successMarkers=4 selected=34
- Evidence class: STATICSOURCEROSLYNAST
- Runtime proof: False
- Parse failures: 0
- GlobalSignals NativeQueue fields: 74
- FlushDirectSignalLane invocations: 141
- Assets/Project/Scripts/Core/Signals/SignalBridgeRoutes.cs | calls=13 publish=0 consume=0 read=1
- Assets/Project/Scripts/Visor/DynamicDecalVaultRuntime.cs | calls=5 publish=0 consume=0 read=3
- WARN SIGNALLAYOUTUNDECLARED Assets/Project/Scripts/Core/Signals/SignalWardenRuntime.cs:1101 EntityAliveMaskSignalFilter | No StructLayout attribute found.
- INFO SIGNALLAYOUTUNDECLARED Assets/Project/Scripts/FaunaDirector.cs:211 AcousticPanicCommand | No StructLayout attribute found.
- WARN SIGNALLAYOUTUNDECLARED Assets/Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs:1861 RespawnSignalResolvedTargetTransformer | No StructLayout attribute found.
- INFO SIGNALLAYOUTUNDECLARED Assets/Project/Scripts/World/Contracts/InstanceCullingContracts.cs:34 InstanceCullingCameraPositionSignal | No StructLayout attribute found.
- INFO SIGNALLAYOUTUNDECLARED Assets/Project/Scripts/World/Contracts/InstanceCullingContracts.cs:45 InstanceCullingCameraFrustumSignal | No StructLayout attribute found.
- AcousticPingSignal | configure=2 | maxFrame=128,64 | lowTier=16,8 | legacyPublish=0 | typedPublish=36 | coalescing=Coalesces by channel and AUP meter cell; acoustic energy is merged in native snapshot memory.
- AcousticZoneChangedEvent | configure=1 | maxFrame=8 | lowTier=AcousticZoneChangedSignalCapacity | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- AnomalyProximitySignal | configure=2 | maxFrame=16 | lowTier=4 | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- AtmosphericReentrySignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- AudioEvent | configure=1 | maxFrame=16 | lowTier=16 | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- AupPreShiftSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- AupShiftSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- BaseModuleCompromisedSignal | configure=2 | maxFrame=DefaultMaxFrameSignals,64 | lowTier=DefaultSurvivalFrameSignals,16 | legacyPublish=0 | typedPublish=3 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- BaseStructuralWarningSignal | configure=2 | maxFrame=64,BaseStructuralWarningConstants.MaxFrameSignals | lowTier=8,BaseStructuralWarningConstants.LowTierFrameSignals | legacyPublish=0 | typedPublish=0 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- BatteryLevelSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- BiomeChangedSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=2 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- BiomeGradientSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- BrownoutSignal | configure=1 | maxFrame=BrownoutSignalCapacity | lowTier=16 | legacyPublish=0 | typedPublish=5 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- BubbleSpawnSignal | configure=2 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=5 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- CameraFrustumSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- CameraJuiceImpactSignal | configure=2 | maxFrame=ImpactSignalCapacity,128 | lowTier=LowTierImpactSignalCapacity,32 | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- CameraPositionSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- ChunkDehydratedSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- CombatDamageSignal | configure=1 | maxFrame=128 | lowTier=16 | legacyPublish=0 | typedPublish=17 | coalescing=Coalesces by TargetHash + DamageType + Channel inside the native frame snapshot; magnitude and integrity delta accumulate, flags OR, first nonzero source is retained.
- CompassCalibratedSignal | configure=2 | maxFrame=8 | lowTier=2 | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- CpuStarvationSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=3 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.
- CraftingCompletedSignal | configure=1 | maxFrame=DefaultMaxFrameSignals | lowTier=DefaultSurvivalFrameSignals | legacyPublish=0 | typedPublish=1 | coalescing=No semantic coalescing detected for this lane; deterministic policy is drop-oldest overflow plus snapshot cap.

FILE Unity_X_008_ArmorRuntimeProof.log bytes=4377 errors=3 warnings=0 successMarkers=0 selected=14
- Built from '6000.4/staging' branch; Version is '6000.4.1f1 (8535861f39e1) revision 8729990'; Using compiler version '194234433'; Build Type 'Release'
- BatchMode: 1, IsHumanControllingUs: 0, StartBugReporterOnCrash: 0, Is64bit: 1
- [Licensing::Module] Successfully launched the LicensingClient (PId: 21308)
- Assertion failed on expression: 'SUCCEEDED(hr)'
- Assertion failed on expression: 'ERRORSUCCESS == status'
- C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe
- Hecton8.Gameplay.ArmorPenetrationBatchProofRunner.Run
- C:\hades\Hecton8\Docs\AgentLogs\UnityX008ArmorRuntimeProof.log
- Successfully changed project path to: C:\hades\Hecton8
- [UnityMemory] Configuration Parameters - Can be set up in boot.config
- "memorysetup-job-temp-allocator-reduction-small-platforms=262144"
- Player connection [37508] Host joined alternative multi-casting on [225.0.0.222:34997]...
- Input System module state changed to: Initialized.
- [Package Manager] Could not establish a connection with the Unity Package Manager local server process.

FILE Unity_X_008_ArmorRuntimeProof_escalated.log bytes=5791 errors=7 warnings=0 successMarkers=0 selected=16
- Built from '6000.4/staging' branch; Version is '6000.4.1f1 (8535861f39e1) revision 8729990'; Using compiler version '194234433'; Build Type 'Release'
- BatchMode: 1, IsHumanControllingUs: 0, StartBugReporterOnCrash: 0, Is64bit: 1
- C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe
- Hecton8.Gameplay.ArmorPenetrationBatchProofRunner.Run
- C:\hades\Hecton8\Docs\AgentLogs\UnityX008ArmorRuntimeProofescalated.log
- Successfully changed project path to: C:\hades\Hecton8
- [UnityMemory] Configuration Parameters - Can be set up in boot.config
- "memorysetup-job-temp-allocator-reduction-small-platforms=262144"
- Player connection [33820] Host joined alternative multi-casting on [225.0.0.222:34997]...
- Input System module state changed to: Initialized.
- [Licensing::Client] Handshaking with LicensingClient:
- [Licensing::Module] Successfully connected to LicensingClient on channel: "LicenseClient-danat" (connect: 0.00s, validation: 0.08s, handshake: 2.54s)
- [Licensing::Module] Error: Access token is unavailable; failed to update
- [Licensing::Client] Error: Code 404 while processing request (status: Found 0 entitlement groups and 0 free entitlements matching requested entitlement ids)
- [Licensing::Module] Error: 'com.unity.editor.headless' was not found.
- No valid Unity Editor license found. Please activate your license.
