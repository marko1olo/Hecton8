# Assembly Dependency Audit

Evidence class: STATIC_SOURCE. No Unity import, compile, player build, or runtime proof was executed.

- Schema: `hecton8.assembly_dependency_audit.v2`
- Source root: `Assets/_Project`
- Asmdefs: `179`
- First-party asmdefs: `179`
- Runtime first-party asmdefs: `117`
- Editor first-party asmdefs: `62`
- First-party `noEngineReferences=true`: `6`
- First-party `autoReferenced=false`: `179`

## DAG

- Nodes: `179`
- Edges: `422`
- Acyclic: `True`
- First-party asmdef cycles: `0`
- Unresolved first-party/GUID refs: `0`
- Duplicate assembly names: `0`

## Core Contracts Boundary

- Required cross-domain route: `Hecton8.Core.Contracts`
- Violations: `147`

| Assembly | Reference | Path |
|---|---|---|
| `Hecton8.AI.Ambient` | `Hecton8.Core` | `Assets/_Project/Scripts/AI/Ambient/Hecton8.AI.Ambient.asmdef` |
| `Hecton8.AI.Ambient` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/AI/Ambient/Hecton8.AI.Ambient.asmdef` |
| `Hecton8.AI.Cognition` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/AI/Cognition/Hecton8.AI.Cognition.asmdef` |
| `Hecton8.AI.Ecology.Migration` | `Hecton8.World.Contracts` | `Assets/_Project/Scripts/AI/Ecology/Migration/Hecton8.AI.Ecology.Migration.asmdef` |
| `Hecton8.AI.Pathfinding` | `Hecton8.Core` | `Assets/_Project/Scripts/AI/Pathfinding/Hecton8.AI.Pathfinding.asmdef` |
| `Hecton8.AI.Pathfinding` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/AI/Pathfinding/Hecton8.AI.Pathfinding.asmdef` |
| `Hecton8.Animation.FaunaProcedural` | `Hecton8.Core` | `Assets/_Project/Scripts/Animation/FaunaProcedural/Hecton8.Animation.FaunaProcedural.asmdef` |
| `Hecton8.Animation.FaunaProcedural` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Animation/FaunaProcedural/Hecton8.Animation.FaunaProcedural.asmdef` |
| `Hecton8.Animation.IK` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Animation/IK/Hecton8.Animation.IK.asmdef` |
| `Hecton8.Atmosphere.StormPropagation.Runtime` | `Hecton8.Core` | `Assets/_Project/Scripts/Atmosphere/StormPropagation/Hecton8.Atmosphere.StormPropagation.Runtime.asmdef` |
| `Hecton8.Atmosphere.StormPropagation.Runtime` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Atmosphere/StormPropagation/Hecton8.Atmosphere.StormPropagation.Runtime.asmdef` |
| `Hecton8.Audio.Prologue` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/Prologue/Hecton8.Audio.Prologue.asmdef` |
| `Hecton8.Audio.Synthesis` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/Synthesis/Hecton8.Audio.Synthesis.asmdef` |
| `Hecton8.Audio.Synthesis` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Audio/Synthesis/Hecton8.Audio.Synthesis.asmdef` |
| `Hecton8.Cartography` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Cartography/Hecton8.Cartography.asmdef` |
| `Hecton8.Cartography` | `Hecton8.Bootstrap.Contracts` | `Assets/_Project/Scripts/Cartography/Hecton8.Cartography.asmdef` |
| `Hecton8.Cartography` | `Hecton8.World.Contracts` | `Assets/_Project/Scripts/Cartography/Hecton8.Cartography.asmdef` |
| `Hecton8.Core.Hardware` | `Hecton8.Bootstrap.Contracts` | `Assets/_Project/Scripts/Core/Hardware/Hecton8.Core.Hardware.asmdef` |
| `Hecton8.Core.Time` | `Hecton8.Bootstrap.Contracts` | `Assets/_Project/Scripts/Core/Time/Hecton8.Core.Time.asmdef` |
| `Hecton8.Dev.SpaceEngine098` | `Hecton8.Core` | `Assets/_Project/Scripts/Dev/SpaceEngine098/Hecton8.Dev.SpaceEngine098.asmdef` |
| `Hecton8.Dev.SpaceEngine098` | `Hecton8.SpaceEngine098Terrain` | `Assets/_Project/Scripts/Dev/SpaceEngine098/Hecton8.Dev.SpaceEngine098.asmdef` |
| `Hecton8.Environment.Fluids` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Environment/Fluids/Hecton8.Environment.Fluids.asmdef` |
| `Hecton8.Gameplay.Loot.Contracts` | `Hecton8.Core` | `Assets/_Project/Scripts/Gameplay/Loot/Contracts/Hecton8.Gameplay.Loot.Contracts.asmdef` |
| `Hecton8.Gameplay.Loot` | `Hecton8.Core` | `Assets/_Project/Scripts/Gameplay/Loot/Hecton8.Gameplay.Loot.asmdef` |
| `Hecton8.Gameplay.Loot` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Gameplay/Loot/Hecton8.Gameplay.Loot.asmdef` |
| `Hecton8.Gameplay.Mining` | `Hecton8.Core` | `Assets/_Project/Scripts/Gameplay/Mining/Hecton8.Gameplay.Mining.asmdef` |
| `Hecton8.Gameplay.Mining` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Gameplay/Mining/Hecton8.Gameplay.Mining.asmdef` |
| `Hecton8.MockDomain.Authoring` | `Hecton8.Global.Contracts` | `Assets/_Project/Scripts/Global/MockDomain/Authoring/Hecton8.MockDomain.Authoring.asmdef` |
| `Hecton8.MockDomain.Contracts` | `Hecton8.Global.Contracts` | `Assets/_Project/Scripts/Global/MockDomain/Contracts/Hecton8.MockDomain.Contracts.asmdef` |
| `Hecton8.MockDomain.Runtime` | `Hecton8.Global.Contracts` | `Assets/_Project/Scripts/Global/MockDomain/Runtime/Hecton8.MockDomain.Runtime.asmdef` |
| `Hecton8.Graphics.Caustics` | `Hecton8.Bootstrap.Contracts` | `Assets/_Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef` |
| `Hecton8.Graphics.Caustics` | `Hecton8.Core` | `Assets/_Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef` |
| `Hecton8.Graphics.Caustics` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef` |
| `Hecton8.Graphics.Culling` | `Hecton8.Core` | `Assets/_Project/Scripts/Graphics/Culling/Hecton8.Graphics.Culling.asmdef` |
| `Hecton8.Graphics.Culling` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Graphics/Culling/Hecton8.Graphics.Culling.asmdef` |
| `Hecton8.Graphics.Culling` | `Hecton8.World.Contracts` | `Assets/_Project/Scripts/Graphics/Culling/Hecton8.Graphics.Culling.asmdef` |
| `Hecton8.Graphics.Materials` | `Hecton8.Core` | `Assets/_Project/Scripts/Graphics/Materials/Hecton8.Graphics.Materials.asmdef` |
| `Hecton8.Graphics.Materials` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Graphics/Materials/Hecton8.Graphics.Materials.asmdef` |
| `Hecton8.Graphics.Materials` | `Hecton8.Habitat.Deformation.Contracts` | `Assets/_Project/Scripts/Graphics/Materials/Hecton8.Graphics.Materials.asmdef` |
| `Hecton8.Graphics.Scalability` | `Hecton8.Core` | `Assets/_Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef` |
| `Hecton8.Graphics.Scalability` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef` |
| `Hecton8.Graphics.Scalability` | `Hecton8.Bootstrap.Contracts` | `Assets/_Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef` |
| `Hecton8.Habitat.Deformation` | `Hecton8.Bootstrap.Contracts` | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/Hecton8.Habitat.Deformation.asmdef` |
| `Hecton8.Habitat.Deformation` | `Hecton8.Core` | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/Hecton8.Habitat.Deformation.asmdef` |
| `Hecton8.Habitat.Deformation` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/Hecton8.Habitat.Deformation.asmdef` |
| `Hecton8.Core` | `Hecton8.Bootstrap.Contracts` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.World.Terrain` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Audio.Propagation` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Audio.Echolocation` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Audio.Virtualization.Contracts` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Audio.Virtualization` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Animation.IK` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.AI.Ecology.Migration` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Cartography` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Environment.Fluids` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Logistics` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Logistics.Grid.Contracts` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Logistics.Grid` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Physics.CCD` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Physics.Determinism` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Inventory.Algorithms` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Inventory.Corrosion.Contracts` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Inventory.Corrosion` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Tools.ToolKinematics.Contracts` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.UI.Diegetic.Contracts` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Input` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Input` | `Hecton8.Bootstrap.Contracts` | `Assets/_Project/Scripts/Input/Hecton8.Input.asmdef` |
| `Hecton8.Lighting` | `Hecton8.Core` | `Assets/_Project/Scripts/Lighting/Hecton8.Lighting.asmdef` |
| `Hecton8.Lighting` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Lighting/Hecton8.Lighting.asmdef` |
| `Hecton8.Narrative.Campaign` | `Hecton8.Core` | `Assets/_Project/Scripts/Narrative/Campaign/Hecton8.Narrative.Campaign.asmdef` |
| `Hecton8.Narrative.Campaign` | `Hecton8.Bootstrap.Contracts` | `Assets/_Project/Scripts/Narrative/Campaign/Hecton8.Narrative.Campaign.asmdef` |
| `Hecton8.Narrative.Prologue` | `Hecton8.Core` | `Assets/_Project/Scripts/Narrative/Prologue/Hecton8.Narrative.Prologue.asmdef` |
| `Hecton8.Physics.Buoyancy.Runtime` | `Hecton8.Core` | `Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/Hecton8.Physics.Buoyancy.Runtime.asmdef` |
| `Hecton8.Physics.Buoyancy.Runtime` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/Hecton8.Physics.Buoyancy.Runtime.asmdef` |
| `Hecton8.Physics.Cable132` | `Hecton8.Core` | `Assets/_Project/Scripts/Physics/Cable132/Hecton8.Physics.Cable132.asmdef` |
| `Hecton8.Physics.Cable132` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Physics/Cable132/Hecton8.Physics.Cable132.asmdef` |
| `Hecton8.Physiology` | `Hecton8.Core` | `Assets/_Project/Scripts/Physiology/Hecton8.Physiology.asmdef` |
| `Hecton8.Physiology` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Physiology/Hecton8.Physiology.asmdef` |
| `Hecton8.Crest.Bridge` | `Hecton8.Bootstrap.Contracts` | `Assets/_Project/Scripts/Plugins/Crest/Hecton8.Crest.Bridge.asmdef` |
| `Hecton8.Crest.Bridge` | `Hecton8.Core` | `Assets/_Project/Scripts/Plugins/Crest/Hecton8.Crest.Bridge.asmdef` |

## Core Compile-Wall Pressure

- Core present: `True`
- Core references: `40`
- Core first-party references: `27`
- Core concrete sibling references: `15`

| Reference | Source asmdef |
|---|---|
| `Hecton8.World.Terrain` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Audio.Propagation` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Audio.Echolocation` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Audio.Virtualization` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Animation.IK` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.AI.Ecology.Migration` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Cartography` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Environment.Fluids` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Logistics` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Logistics.Grid` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Physics.CCD` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Physics.Determinism` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Inventory.Algorithms` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Inventory.Corrosion` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Input` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |

## Runtime Concrete Cross-Domain References

- Count: `116`

| Assembly | Reference | Path |
|---|---|---|
| `Hecton8.AI.Ambient` | `Hecton8.Core` | `Assets/_Project/Scripts/AI/Ambient/Hecton8.AI.Ambient.asmdef` |
| `Hecton8.AI.Ambient` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/AI/Ambient/Hecton8.AI.Ambient.asmdef` |
| `Hecton8.AI.Cognition` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/AI/Cognition/Hecton8.AI.Cognition.asmdef` |
| `Hecton8.AI.Pathfinding` | `Hecton8.Core` | `Assets/_Project/Scripts/AI/Pathfinding/Hecton8.AI.Pathfinding.asmdef` |
| `Hecton8.AI.Pathfinding` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/AI/Pathfinding/Hecton8.AI.Pathfinding.asmdef` |
| `Hecton8.Animation.FaunaProcedural` | `Hecton8.Core` | `Assets/_Project/Scripts/Animation/FaunaProcedural/Hecton8.Animation.FaunaProcedural.asmdef` |
| `Hecton8.Animation.FaunaProcedural` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Animation/FaunaProcedural/Hecton8.Animation.FaunaProcedural.asmdef` |
| `Hecton8.Animation.IK` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Animation/IK/Hecton8.Animation.IK.asmdef` |
| `Hecton8.Atmosphere.StormPropagation.Runtime` | `Hecton8.Core` | `Assets/_Project/Scripts/Atmosphere/StormPropagation/Hecton8.Atmosphere.StormPropagation.Runtime.asmdef` |
| `Hecton8.Atmosphere.StormPropagation.Runtime` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Atmosphere/StormPropagation/Hecton8.Atmosphere.StormPropagation.Runtime.asmdef` |
| `Hecton8.Audio.Prologue` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/Prologue/Hecton8.Audio.Prologue.asmdef` |
| `Hecton8.Audio.Synthesis` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/Synthesis/Hecton8.Audio.Synthesis.asmdef` |
| `Hecton8.Audio.Synthesis` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Audio/Synthesis/Hecton8.Audio.Synthesis.asmdef` |
| `Hecton8.Cartography` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Cartography/Hecton8.Cartography.asmdef` |
| `Hecton8.Dev.SpaceEngine098` | `Hecton8.Core` | `Assets/_Project/Scripts/Dev/SpaceEngine098/Hecton8.Dev.SpaceEngine098.asmdef` |
| `Hecton8.Dev.SpaceEngine098` | `Hecton8.SpaceEngine098Terrain` | `Assets/_Project/Scripts/Dev/SpaceEngine098/Hecton8.Dev.SpaceEngine098.asmdef` |
| `Hecton8.Environment.Fluids` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Environment/Fluids/Hecton8.Environment.Fluids.asmdef` |
| `Hecton8.Gameplay.Loot.Contracts` | `Hecton8.Core` | `Assets/_Project/Scripts/Gameplay/Loot/Contracts/Hecton8.Gameplay.Loot.Contracts.asmdef` |
| `Hecton8.Gameplay.Loot` | `Hecton8.Core` | `Assets/_Project/Scripts/Gameplay/Loot/Hecton8.Gameplay.Loot.asmdef` |
| `Hecton8.Gameplay.Loot` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Gameplay/Loot/Hecton8.Gameplay.Loot.asmdef` |
| `Hecton8.Gameplay.Mining` | `Hecton8.Core` | `Assets/_Project/Scripts/Gameplay/Mining/Hecton8.Gameplay.Mining.asmdef` |
| `Hecton8.Gameplay.Mining` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Gameplay/Mining/Hecton8.Gameplay.Mining.asmdef` |
| `Hecton8.Graphics.Caustics` | `Hecton8.Core` | `Assets/_Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef` |
| `Hecton8.Graphics.Caustics` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef` |
| `Hecton8.Graphics.Culling` | `Hecton8.Core` | `Assets/_Project/Scripts/Graphics/Culling/Hecton8.Graphics.Culling.asmdef` |
| `Hecton8.Graphics.Culling` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Graphics/Culling/Hecton8.Graphics.Culling.asmdef` |
| `Hecton8.Graphics.Materials` | `Hecton8.Core` | `Assets/_Project/Scripts/Graphics/Materials/Hecton8.Graphics.Materials.asmdef` |
| `Hecton8.Graphics.Materials` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Graphics/Materials/Hecton8.Graphics.Materials.asmdef` |
| `Hecton8.Graphics.Scalability` | `Hecton8.Core` | `Assets/_Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef` |
| `Hecton8.Graphics.Scalability` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Graphics/Scalability/Hecton8.Graphics.Scalability.asmdef` |
| `Hecton8.Habitat.Deformation` | `Hecton8.Core` | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/Hecton8.Habitat.Deformation.asmdef` |
| `Hecton8.Habitat.Deformation` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Habitat/Deformation/Runtime/Hecton8.Habitat.Deformation.asmdef` |
| `Hecton8.Core` | `Hecton8.World.Terrain` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Audio.Propagation` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Audio.Echolocation` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Audio.Virtualization` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Animation.IK` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.AI.Ecology.Migration` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Cartography` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Environment.Fluids` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Logistics` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Logistics.Grid` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Physics.CCD` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Physics.Determinism` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Inventory.Algorithms` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Inventory.Corrosion` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Core` | `Hecton8.Input` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Lighting` | `Hecton8.Core` | `Assets/_Project/Scripts/Lighting/Hecton8.Lighting.asmdef` |
| `Hecton8.Lighting` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Lighting/Hecton8.Lighting.asmdef` |
| `Hecton8.Narrative.Campaign` | `Hecton8.Core` | `Assets/_Project/Scripts/Narrative/Campaign/Hecton8.Narrative.Campaign.asmdef` |
| `Hecton8.Narrative.Prologue` | `Hecton8.Core` | `Assets/_Project/Scripts/Narrative/Prologue/Hecton8.Narrative.Prologue.asmdef` |
| `Hecton8.Physics.Buoyancy.Runtime` | `Hecton8.Core` | `Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/Hecton8.Physics.Buoyancy.Runtime.asmdef` |
| `Hecton8.Physics.Buoyancy.Runtime` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/Hecton8.Physics.Buoyancy.Runtime.asmdef` |
| `Hecton8.Physics.Cable132` | `Hecton8.Core` | `Assets/_Project/Scripts/Physics/Cable132/Hecton8.Physics.Cable132.asmdef` |
| `Hecton8.Physics.Cable132` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Physics/Cable132/Hecton8.Physics.Cable132.asmdef` |
| `Hecton8.Physiology` | `Hecton8.Core` | `Assets/_Project/Scripts/Physiology/Hecton8.Physiology.asmdef` |
| `Hecton8.Physiology` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Physiology/Hecton8.Physiology.asmdef` |
| `Hecton8.Crest.Bridge` | `Hecton8.Core` | `Assets/_Project/Scripts/Plugins/Crest/Hecton8.Crest.Bridge.asmdef` |
| `Hecton8.Crest.Bridge` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Plugins/Crest/Hecton8.Crest.Bridge.asmdef` |
| `Hecton8.Plugins` | `Hecton8.Core` | `Assets/_Project/Scripts/Plugins/Hecton8.Plugins.asmdef` |
| `Hecton8.Plugins` | `Hecton8.SpaceEngine098Terrain` | `Assets/_Project/Scripts/Plugins/Hecton8.Plugins.asmdef` |
| `Hecton8.Power.BatteryChargerLogistics.Runtime` | `Hecton8.Core` | `Assets/_Project/Scripts/Power/BatteryChargerLogistics/Hecton8.Power.BatteryChargerLogistics.Runtime.asmdef` |
| `Hecton8.Power.BatteryChargerLogistics.Runtime` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Power/BatteryChargerLogistics/Hecton8.Power.BatteryChargerLogistics.Runtime.asmdef` |
| `Hecton8.Power.Generators` | `Hecton8.Core` | `Assets/_Project/Scripts/Power/Generators/Hecton8.Power.Generators.asmdef` |
| `Hecton8.Power.Generators` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Power/Generators/Hecton8.Power.Generators.asmdef` |
| `Hecton8.Prologue.Space` | `Hecton8.Core` | `Assets/_Project/Scripts/Prologue/Space/Hecton8.Prologue.Space.asmdef` |
| `Hecton8.Prologue.Space` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Prologue/Space/Hecton8.Prologue.Space.asmdef` |
| `Hecton8.Prologue.VFX` | `Hecton8.Core` | `Assets/_Project/Scripts/Prologue/VFX/Hecton8.Prologue.VFX.asmdef` |
| `Hecton8.QA.Headless` | `Hecton8.Core` | `Assets/_Project/Scripts/QA/Headless/Hecton8.QA.Headless.asmdef` |
| `Hecton8.QA.Headless` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/QA/Headless/Hecton8.QA.Headless.asmdef` |
| `Hecton8.QA` | `Hecton8.Core` | `Assets/_Project/Scripts/QA/Hecton8.QA.asmdef` |
| `Hecton8.Rendering.BilateralDrs` | `Hecton8.Core` | `Assets/_Project/Scripts/Rendering/BilateralDrs/Hecton8.Rendering.BilateralDrs.asmdef` |
| `Hecton8.Rendering.BilateralDrs` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Rendering/BilateralDrs/Hecton8.Rendering.BilateralDrs.asmdef` |
| `Hecton8.Rendering.OceanSinglePass` | `Hecton8.Core` | `Assets/_Project/Scripts/Rendering/OceanSinglePass/Hecton8.Rendering.OceanSinglePass.asmdef` |
| `Hecton8.Rendering.OceanSinglePass` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Rendering/OceanSinglePass/Hecton8.Rendering.OceanSinglePass.asmdef` |
| `Hecton8.Rendering.WaterOptics` | `Hecton8.Core` | `Assets/_Project/Scripts/Rendering/WaterOptics/Hecton8.Rendering.WaterOptics.asmdef` |
| `Hecton8.Rendering.WaterOptics` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Rendering/WaterOptics/Hecton8.Rendering.WaterOptics.asmdef` |
| `Hecton8.Thermodynamics` | `Hecton8.Core` | `Assets/_Project/Scripts/Thermodynamics/Hecton8.Thermodynamics.asmdef` |
| `Hecton8.Thermodynamics` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Thermodynamics/Hecton8.Thermodynamics.asmdef` |
| `Hecton8.Tools.ToolKinematics` | `Hecton8.Core` | `Assets/_Project/Scripts/Tools/ToolKinematics/Hecton8.Tools.ToolKinematics.asmdef` |

## Cycles

- First-party asmdef cycles: `0`

## Interpretation

- The serialized first-party asmdef graph is treated as a DAG. Any cycle is a hard compile-wall defect under the strict flag.
- Cross-domain runtime references that do not route through `Hecton8.Core.Contracts` are strict boundary violations under `--fail-on-core-contract-boundary`.
- Core concrete sibling references are compile-wall pressure. They are not automatically removable; each one needs a contract/facade migration plan plus Unity import proof.
- Runtime concrete cross-domain references are review surfaces. Same-domain and `.Contracts` references are reported separately from the strict Core.Contracts boundary.
- This audit does not mutate asmdefs and does not claim compile health.
