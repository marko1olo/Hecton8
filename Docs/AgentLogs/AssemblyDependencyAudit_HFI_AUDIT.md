# Assembly Dependency Audit

Evidence class: STATIC_SOURCE. No Unity import, compile, player build, or runtime proof was executed.

- Schema: `hecton8.assembly_dependency_audit.v1`
- Source root: `Assets/_Project/Scripts`
- Asmdefs: `137`
- First-party asmdefs: `137`
- Runtime first-party asmdefs: `102`
- Editor first-party asmdefs: `35`
- First-party `noEngineReferences=true`: `6`
- First-party `autoReferenced=false`: `134`

## Core Compile-Wall Pressure

- Core present: `True`
- Core references: `20`
- Core first-party references: `8`
- Core concrete sibling references: `1`

| Reference | Source asmdef |
|---|---|
| `Hecton8.Input` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |

## Runtime Concrete Cross-Domain References

- Count: `77`

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
| `Hecton8.Audio.Prologue` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/Prologue/Hecton8.Audio.Prologue.asmdef` |
| `Hecton8.Audio.Synthesis` | `Hecton8.Core` | `Assets/_Project/Scripts/Audio/Synthesis/Hecton8.Audio.Synthesis.asmdef` |
| `Hecton8.Audio.Synthesis` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Audio/Synthesis/Hecton8.Audio.Synthesis.asmdef` |
| `Hecton8.Cartography` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Cartography/Hecton8.Cartography.asmdef` |
| `Hecton8.Dev.SpaceEngine098` | `Hecton8.Core` | `Assets/_Project/Scripts/Dev/SpaceEngine098/Hecton8.Dev.SpaceEngine098.asmdef` |
| `Hecton8.Dev.SpaceEngine098` | `Hecton8.SpaceEngine098Terrain` | `Assets/_Project/Scripts/Dev/SpaceEngine098/Hecton8.Dev.SpaceEngine098.asmdef` |
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
| `Hecton8.Core` | `Hecton8.Input` | `Assets/_Project/Scripts/Hecton8.Core.asmdef` |
| `Hecton8.Inventory.Routing.Runtime` | `Hecton8.Core` | `Assets/_Project/Scripts/Inventory/Routing/Hecton8.Inventory.Routing.Runtime.asmdef` |
| `Hecton8.Inventory.Routing.Runtime` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Inventory/Routing/Hecton8.Inventory.Routing.Runtime.asmdef` |
| `Hecton8.Lighting` | `Hecton8.Core` | `Assets/_Project/Scripts/Lighting/Hecton8.Lighting.asmdef` |
| `Hecton8.Lighting` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Lighting/Hecton8.Lighting.asmdef` |
| `Hecton8.Narrative.Campaign` | `Hecton8.Core` | `Assets/_Project/Scripts/Narrative/Campaign/Hecton8.Narrative.Campaign.asmdef` |
| `Hecton8.Physiology` | `Hecton8.Core` | `Assets/_Project/Scripts/Physiology/Hecton8.Physiology.asmdef` |
| `Hecton8.Physiology` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Physiology/Hecton8.Physiology.asmdef` |
| `Hecton8.Plugins` | `Hecton8.Core` | `Assets/_Project/Scripts/Plugins/Hecton8.Plugins.asmdef` |
| `Hecton8.Plugins` | `Hecton8.SpaceEngine098Terrain` | `Assets/_Project/Scripts/Plugins/Hecton8.Plugins.asmdef` |
| `Hecton8.Power.Generators` | `Hecton8.Core` | `Assets/_Project/Scripts/Power/Generators/Hecton8.Power.Generators.asmdef` |
| `Hecton8.Power.Generators` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Power/Generators/Hecton8.Power.Generators.asmdef` |
| `Hecton8.Prologue.Space` | `Hecton8.Core` | `Assets/_Project/Scripts/Prologue/Space/Hecton8.Prologue.Space.asmdef` |
| `Hecton8.Prologue.VFX` | `Hecton8.Core` | `Assets/_Project/Scripts/Prologue/VFX/Hecton8.Prologue.VFX.asmdef` |
| `Hecton8.QA.Headless` | `Hecton8.Core` | `Assets/_Project/Scripts/QA/Headless/Hecton8.QA.Headless.asmdef` |
| `Hecton8.QA.Headless` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/QA/Headless/Hecton8.QA.Headless.asmdef` |
| `Hecton8.QA` | `Hecton8.Core` | `Assets/_Project/Scripts/QA/Hecton8.QA.asmdef` |
| `Hecton8.Thermodynamics` | `Hecton8.Core` | `Assets/_Project/Scripts/Thermodynamics/Hecton8.Thermodynamics.asmdef` |
| `Hecton8.Thermodynamics` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Thermodynamics/Hecton8.Thermodynamics.asmdef` |
| `Hecton8.Tools.ToolKinematics` | `Hecton8.Core` | `Assets/_Project/Scripts/Tools/ToolKinematics/Hecton8.Tools.ToolKinematics.asmdef` |
| `Hecton8.Tools.ToolKinematics` | `Hecton8.Core.Memory` | `Assets/_Project/Scripts/Tools/ToolKinematics/Hecton8.Tools.ToolKinematics.asmdef` |

## Cycles

- First-party asmdef cycles: `0`

## Interpretation

- Core concrete sibling references are compile-wall pressure. They are not automatically removable; each one needs a contract/facade migration plan plus Unity import proof.
- Runtime concrete cross-domain references are review surfaces. Same-domain and `.Contracts` references are not counted in this bucket.
- Cycles in first-party asmdefs are hard architectural defects if Unity import confirms them. This tool only reports the serialized asmdef graph.
- This audit does not mutate asmdefs and does not claim compile health.
