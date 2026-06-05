# Rationale 2024

Task: FIRST20 survival/resource route implementation order.

## Decisions

1. Scanner route must be resolved before resource/quest patches.
Reason: static evidence shows `Player.prefab` assigns scanner as a tool prefab while `Quest_FirstHour_CraftScanner.asset` makes scanner a crafted completion. Patching resources first would not answer whether scanner is a starter tool or route endpoint.

2. `CopperVein` remains Drill-gated.
Reason: `ResourceNodeTemplate_CopperVein.asset` is Drill-gated, `ContentSanityValidator.cs` protects that gate, and no starter drill item/prefab exists. Early copper must be a different shallow source.

3. Recipe authority is a blocker, not an implementation detail.
Reason: `RecipeData` assets and `Data/Economy/Crafting_Costs.csv` disagree on route-critical costs. Fabricator code references `RecipeData`, while DataMonolith/economy files claim binary authority. Cost edits are unsafe until the owner declares active runtime truth.

4. `QuestData` assets remain the quest authoring truth for this spec.
Reason: `Quest_Graph.json` states it is a generated mirror and runtime authority remains `QuestData` unless leadership promotes JSON as source of truth.

5. Oxygen support is a fairness layer, not a difficulty removal.
Reason: project vision permits oxygen death and aggressive-contact death. Existing oxygen drain/refill/death/save mechanics are present; first-route work should place readable support without removing lethal consequences.

6. Save/load proof must be route-specific.
Reason: `SaveData`, codec, quest, inventory, and smoke testers exist, but static serializer presence does not prove the first-20 route roundtrips without duplication, lost critical items, or stale quest state.

## Rejected

- Weakening CopperVein to Any/Knife/Scanner/Salvage.
- Treating ToolLoadoutProvisioner dev grants as shipped route proof.
- Patching only `Quest_Graph.json`.
- Claiming oxygen hose gameplay from suit visual hose geometry.
- Running Unity, build, profiler, import, or tests in this planning task.
