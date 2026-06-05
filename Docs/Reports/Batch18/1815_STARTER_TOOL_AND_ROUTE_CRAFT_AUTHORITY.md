# Agent 1815 - Starter Tool And Route Craft Authority Audit

Date: 2026-06-04
Agent: 1815 / STARTER_TOOL_AND_ROUTE_CRAFT_AUTHORITY_AUDITOR
Scope: No-Unity static audit of starter equipment authority, copper extraction gate, and route-relevant first craft gate.

## Evidence Boundary

This report is static only. Agent 1815 produced no Unity Editor run, Play Mode route execution, profiler capture, Frame Debugger capture, player build, screenshot, or runtime console proof.

## Verdict

Status: STATIC AUDIT COMPLETE / IMPLEMENTATION REQUIRED / UNITY SLOT PROOF PENDING.

Current first-20-minute route truth is not proven. The project has authored data for copper, copper wire, repair/build/beacon candidates, and tool loadout presets, but the only visible automated starter provisioning route is a disabled development helper. `FirstHourDirector` also marks `FirstCraft` complete for any non-null crafted item, not a route-relevant repair/build/wire/beacon state. A source fix was not applied because the missing piece is product authority: which route owner grants starter tools, which exact extractor satisfies copper's authored harvest class, and which craft/use state is the accepted first-route gate.

## Authorities Loaded

- Root bibles: `AGENTS.md`, `PROJECT_BIBLES.md`, `VISION_LOCKS.md`, `TASTE.md`, `quality.md`.
- Domain bibles: `gameplay.md`, `survival.md`, `tools.md`, `inventory.md`, `construction.md`, plus `persistence.md` and `data.md` for save/tool identity implications.
- Prior report: `Docs/Reports/Batch18/1803_FIRST20_ROUTE_BLOCKER_MATRIX.md`.
- Mandates: `QA_Evidence_Text_Filter_Audit`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `CORE_Tools_Equipment_Interaction_Raycast_Heat`, `DATA_Inventory_Resources_Items_SOA_Layout`, `DATA_Save_Persistence_Binary_Delta_Checksum`, `ARCH_Execution_Phases`, `ARCH_Global_Registry_ServiceLocator_DI_Init`, `ARCH_Signal_Lane_Segregation`.

## Files Inspected

- `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_CopperVein.asset`
- `Assets/_Project/Scripts/Scavenging/ResourceNodeTemplate.cs`
- `Assets/_Project/Scripts/Scavenging/ResourceNode.cs`
- `Assets/_Project/Scripts/ToolLoadoutProvisioner.cs`
- `Assets/_Project/Scripts/PlayerToolManager.cs`
- `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs`
- `Assets/_Project/Scripts/Fabricator.cs`
- `Assets/_Project/Scripts/CraftingEvents.cs`
- `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs`
- `Assets/_Project/Data/Tools/Presets/Preset_Loadout_FieldRecovery.asset`
- `Assets/_Project/Data/Tools/Presets/Preset_Loadout_Construction.asset`
- `Assets/_Project/Data/Tools/Presets/Preset_Loadout_Exploration.asset`
- `Assets/_Project/Data/Tools/ToolMetadata_Scanner.asset`
- `Assets/_Project/Data/Tools/ToolMetadata_SalvageSampler.asset`
- `Assets/_Project/Data/Tools/ToolMetadata_Repair.asset`
- `Assets/_Project/Data/Tools/ToolMetadata_Builder.asset`
- `Assets/_Project/Data/Tools/ToolMetadata_LaserCutter.asset`
- `Assets/_Project/Data/Tools/ToolMetadata_BeaconDeployer.asset`
- `Assets/_Project/Data/Items/Tools/Item_Tool_Scanner.asset`
- `Assets/_Project/Data/Items/Tools/Item_Tool_SalvageSampler.asset`
- `Assets/_Project/Data/Items/Tools/Item_Tool_Repair.asset`
- `Assets/_Project/Data/Items/Tools/Item_Tool_Builder.asset`
- `Assets/_Project/Data/Items/Tools/Item_Tool_LaserCutter.asset`
- `Assets/_Project/Data/Items/Tools/Item_Tool_BeaconDeployer.asset`
- `Assets/_Project/Data/Crafting/Recipes/Recipe_CopperWire.asset`
- `Assets/_Project/Data/Crafting/Recipes/Recipe_FieldBeacon.asset`
- `Assets/_Project/Data/Crafting/Recipes/Recipe_RepairTool.asset`
- `Assets/_Project/Data/Crafting/Recipes/Recipe_PressureSeal.asset`
- `Assets/_Project/Data/Crafting/Recipes/Recipe_SealantPack.asset`
- `Assets/_Project/Data/Items/Resources/Components/Comp_CopperWire.asset`
- `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset`
- `Assets/_Project/Data/Items/Data_Copper.asset`
- `Assets/_Project/Data/Lore/Quests/Quest_CopperSample.asset`

## Findings

| ID | Finding | Evidence class | Source evidence | Impact |
| --- | --- | --- | --- | --- |
| 1815-F01 | Copper is authored behind a harvest tool-class gate. | Static data/source | `ResourceNodeTemplate_CopperVein.asset:19` has `requiredToolClass: 2`; `ResourceNodeTemplate.cs:32-38` maps `2` to `Drill`; `ResourceNodeTemplate.cs:643` exports it as `RuntimeDescriptor.RequiredToolClass`. | Starter route cannot be accepted unless a product-owned route grants/proves a matching extraction tool before copper is required, or route authoring changes the copper gate with design approval. |
| 1815-F02 | Static grep did not find a product starter authority that grants the required extractor plus persistent starter inventory. | Static source/data | The found automated provisioner is `Hecton8.Dev.ToolLoadoutProvisioner`; it is in a dev namespace and menu at `ToolLoadoutProvisioner.cs:17-20`. | A preset or dev object is not first-20 product proof. New-game starter ownership remains unproven. |
| 1815-F03 | `ToolLoadoutProvisioner` is disabled by default and build-gated. | Static source | `ToolLoadoutProvisioner.cs:36-39` defaults `provisionInventoryOnStart`, `assignCoreLoadoutOnStart`, and `provisionConstructionMaterialsOnStart` to `false`; `ToolLoadoutProvisioner.cs:204-210` returns `false` outside editor/development builds. | It cannot be used as acceptance proof for release-like first-20 route truth. |
| 1815-F04 | The dev helper can bypass the actual first-resource route by granting materials. | Static source | `ToolLoadoutProvisioner.cs:122-141` provisions construction materials; earlier audit found default construction material raw copper. | Dev provisioning raw copper would fake the copper route instead of proving oxygen, extraction, inventory, and craft flow. |
| 1815-F05 | Real tool availability is inventory-owned, not slot-only. | Static source | `PlayerToolManager.cs:876-883` requires assigned prefab and `HasToolInInventory`; `PlayerToolManager.cs:2163-2185` resolves `ToolData.PersistentId`, hashes it, and calls `playerInventory.CountAvailableTotal`. | A product starter fix must grant persistent `Item_Tool_*` inventory items and quick-slot assignments together. Slot preset alone creates dead slots after ownership checks. |
| 1815-F06 | Existing loadout presets are authoring aids, not ownership proof. | Static data/source | `Preset_Loadout_FieldRecovery.asset:15-17` contains sampler/cutter/propulsion/analyzer; `Preset_Loadout_Construction.asset:15-17` contains builder/repair/scanner/cutter; `PlayerToolManager.ApplyLoadoutPreset` assigns prefabs but inventory checks still gate usability. | Presets are useful inputs to a product authority, but not acceptance artifacts. |
| 1815-F07 | The inspected starter-style tool metadata does not map cleanly to copper's `HarvestToolClass.Drill` gate. | Static data/source | Tool metadata uses `tier` and `category` (`ToolMetadata_SalvageSampler.asset:15-18`, `ToolMetadata_LaserCutter.asset:15-18`); copper uses `HarvestToolClass` in `ResourceNodeTemplate.cs:32-38`. Static search only found `RequiredToolClass` exported from the template. | Do not claim scanner/sampler/cutter satisfies copper class 2 without the missing runtime mapping or PlayMode proof. |
| 1815-F08 | `FirstHourDirector` marks `FirstCraft` complete for any craft result. | Static source | `FirstHourDirector.cs:1308-1315` checks only `resultItem != null` and `!IsMilestoneComplete(FirstCraft)`; `FirstHourDirector.cs:1370-1378` routes every `CraftCompleted` payload to that method. | A player can satisfy first craft with unrelated fabrication. This is not route-relevant copper/wire/repair/build truth. |
| 1815-F09 | The resource pickup gate is stricter than the craft gate. | Static source/data | `FirstHourDirector.cs:713-717` configures `quest_copper_sample` and `Data_Copper`; `FirstHourDirector.cs:1393-1403` requires `item.MatchesPersistentHash(_firstResourceItemHash)` before completing resource quest. | The first-craft gate is the weak link; it should follow route-specific identity, not generic completion. |
| 1815-F10 | Fabricator already emits a richer typed completion path. | Static source | `Fabricator.cs:3312-3323` queues `CraftingCompletedSignal` with `FabricatorHash`, `RecipeHash`, `ResultItemHash`, `Frame`, and `Quantity`; `CraftingCompletedSignal` fields are defined at `GlobalSignalPayloads.DomainRemainder.cs:1262-1274`. | A route craft gate can filter recipe/result hashes without new managed hot polling. |
| 1815-F11 | The managed `CraftingEvents` completion path used by `FirstHourDirector` drops recipe specificity. | Static source | `CraftingEvents.cs:505-523` stores `Recipe = null`, `Item = resultItem`, `RecipeHashId = 0`, and `ResultItemHashId = resultItemHash` for `CraftCompleted`. | If `FirstHourDirector` stays on this listener, it can only filter by result item unless `CraftingEvents` is extended. |
| 1815-F12 | Route-relevant craft candidates exist, but none are proven as the accepted first-route state. | Static data | `Recipe_CopperWire.asset:15-34` produces Copper Wire from raw copper; `Comp_CopperWire.asset:15-26` says it feeds early devices/beacons/control loops; `Recipe_FieldBeacon.asset:15-36` is a route beacon; `Recipe_RepairTool.asset:15-39` is repair; `Recipe_PressureSeal.asset:15-38` and `Recipe_SealantPack.asset:15-38` are repair/survival materials. | Gate candidates exist. Acceptance must bind one to a route state such as repaired module, build placement, beacon deployment, or explicit route unlock. |
| 1815-F13 | Copper item identity is duplicated. | Static data | `Data/Items/Resources/Raw/Data_Copper.asset:13-53` and `Data/Items/Data_Copper.asset:13-53` both use `stableId: Data_Copper`; only the raw resource asset has `isRawResource: 1` and a world prefab. `Quest_CopperSample.asset:15,30` uses `quest_copper_sample` and `completionId: Data_Copper`. | Any route gate using only `Data_Copper` must resolve which asset owns first-resource truth. Save and quest identity need owner cleanup before runtime proof. |

## Starter Tool Authority Requirement

Product starter equipment must be owned by a release-valid first-20 route authority, not `Hecton8.Dev.ToolLoadoutProvisioner`.

Minimum product contract:

- Grant persistent inventory items for the starter tools by stable item identity, not by scene-only prefabs.
- Assign matching quick-slot prefabs through `PlayerToolManager` after inventory ownership exists.
- Publish at most one loadout change through the existing `PlayerToolManager` path; no hot `GlobalRegistry` polling.
- Preserve starter core tools across save/load and ordinary death/respawn according to persistence rules.
- Prove the copper extractor. If copper remains `requiredToolClass: 2 / Drill`, the route must either grant a verified drill-class extractor before copper is required or move copper behind a different authored route with product approval.
- Do not grant raw copper as starter proof. That bypasses oxygen, traversal, harvesting, inventory pickup, and first craft.

Candidate starter kit, subject to route owner approval:

- Scanner: basic field read.
- Repair tool or build tool: route-relevant service loop.
- A verified copper extractor: exact prefab/data must be proven against `HarvestToolClass.Drill`, not inferred from tool tier.
- Beacon or beacon deployer only if the first route uses frontier/return-path marker logic.

## Route-Relevant Craft Gate Requirement

`FirstCraft` must not mean "any craft." It must mean "first route craft or route improvement completed."

Acceptable gate shapes:

- Result-filter gate: first craft completes only for explicitly configured stable IDs such as `Comp_CopperWire`, `Item_Tool_Repair`, `Item_Tool_Builder`, `Item_Tool_BeaconDeployer`, `Comp_PressureSeal`, or `Comp_SealantPack`.
- Recipe-filter gate: first craft completes only for explicitly configured recipe hashes such as `Recipe_CopperWire`, `Recipe_RepairTool`, `Recipe_FieldBeacon`, `Recipe_PressureSeal`, or `Recipe_SealantPack`.
- Stronger route-state gate: craft alone does not complete; completion waits for repair applied, build placed, beacon deployed, or route unlock signal. This is the preferred product truth if the first craft is supposed to change the route, not just inventory.

Preferred technical route:

- Consume `CraftingCompletedSignal` through the existing typed signal lane or a dispatcher-owned snapshot and filter `RecipeHash`/`ResultItemHash`.
- If `FirstHourDirector` must remain on `CraftingEvents`, extend `TryRaiseCraftCompleted` or add an overload that preserves recipe hash/recipe reference in the payload. Do not scrape UI strings or recipe names.
- Keep whitelist data serialized on the first-hour/route authority, with stable IDs prehashed during owner initialization.
- Do not query `GlobalRegistry` from the hot craft-completion check. `GlobalRegistry` can cold-bind services only.

## Decoupling And Runtime Rules

- One fact, one owner: starter tool ownership belongs to player inventory plus the product starter/route authority; tool visibility belongs to `PlayerToolManager`; route milestone ownership belongs to `FirstHourDirector` or a route progression owner.
- Read accessors must stay pure. No route gate should search scene state, mutate inventory, allocate/grow buffers, or complete jobs from a `Get*`/`TryGet*` method.
- Hot broadcast must use `SignalBus<T>` or an owner-owned snapshot. Do not add direct `GlobalRegistry` hot polling.
- The craft whitelist should be bounded and prehashed; no per-frame string comparisons.
- Quality cannot alter gameplay truth. `GlobalQualityWeight` may alter presentation, cadence, VFX/audio, and optional telemetry only.

## Scalability Consequences

- Compact: Same starter items, same copper/craft gates, same save identity. Lower VFX/audio density and UI animation cadence only.
- Middle: Same gameplay gates. Normal diegetic feedback and expected starter route readability.
- High: Same gameplay gates. Extra tool VFX, richer fabricator feedback, stronger route beacon presentation.
- Ultra: Same gameplay gates and DTO/save identity. Visual overkill only: richer sparks, better hologram/audio/particle density, no different craft truth.

## Required Unity Slot Proof Packet

This proof is pending and must be produced by a later runtime agent or Unity slot. Agent 1815 did not run it.

1. Release-like new game with `ToolLoadoutProvisioner` confirmed absent or inactive; do not use it as acceptance evidence.
2. Inventory after boot contains the product starter tool stable IDs; quick slots are assigned and usable because inventory ownership passes `PlayerToolManager.HasToolInInventory`.
3. Save/load after boot preserves starter tools and assigned slots.
4. Oxygen loop is visible and functional during copper route attempt.
5. Actual copper acquisition comes from route-valid world node or equivalent authored source, not raw-copper dev grant.
6. The active extractor satisfies copper's authored harvest gate, or route authoring proves copper does not require that tool at the point it is requested.
7. `quest_copper_sample` advances only after the correct copper item identity is acquired.
8. Unrelated craft does not complete `FirstCraft`.
9. Route-relevant craft/use state completes `FirstCraft`: copper wire tied to route unlock, repair/build applied, pressure seal used, or field beacon deployed.
10. Save/load after first craft preserves route state and does not replay completion incorrectly.
11. Ordinary death/respawn preserves starter core tools and route truth according to persistence rules.
12. Console and profiler artifacts must come from the Unity/runtime proof owner, not from static grep or editor helper output.

## Narrow Implementation Prompt

Use this only after route owner approval or if the assigned implementation agent owns first-20 progression.

```
TASK: Product starter loadout authority and route-specific first craft gate.

Hard rules:
- Do not use Hecton8.Dev.ToolLoadoutProvisioner as product proof.
- Do not grant raw copper as starter acceptance.
- Do not mark FirstCraft from any craft.
- Do not add hot GlobalRegistry polling.
- Preserve continuous GlobalQualityWeight semantics; quality never changes route truth, save identity, DTO layout, or starter ownership.

Implementation:
1. Add or wire a release-valid first-20 starter authority near the existing product bootstrap/first-hour route owner.
2. Grant starter tool inventory by stable item IDs, then assign matching quick-slot prefabs through PlayerToolManager.
3. Prove or author the copper extractor mapping for ResourceNodeTemplate.HarvestToolClass.Drill. If not proven, stop and request route-authority decision.
4. Replace FirstHourDirector's generic FirstCraft completion with a serialized route craft whitelist and/or route use-state signal.
5. Prefer CraftingCompletedSignal RecipeHash/ResultItemHash filtering. If using CraftingEvents, extend completion payload so recipe identity is not lost.
6. Add static/editor tests only for identity/whitelist hashing if cheap. Runtime proof still requires Unity slot.

Acceptance:
- Unrelated craft does not complete FirstCraft.
- Route-relevant craft/use state does complete FirstCraft.
- Starter tools are owned in inventory, assigned to slots, save/load stable, and no dev helper is involved.
```

## No Source Fix Applied

No gameplay/data source was changed by Agent 1815. Changing copper's tool requirement, enabling dev provisioning, granting raw materials, or hardcoding a speculative craft whitelist would lower route truth. The correct next step is a product-owned starter authority plus a route-specific first craft/use-state gate, followed by the Unity proof packet above.
