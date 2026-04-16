# Construction Bootstrap Plan

## Current State

- `BuilderTool` exists and is part of the live tool loadout.
- `PlayerBuilder` is now present on `Player`.
- `ConstructionManager` is now present on a dedicated root object: `ConstructionManager_Root`.
- `BuilderStatusOverlay` is attached under `Suit_HUD_Canvas`.
- A deterministic editor authoring path now exists:
  - `Hecton/Authoring/Rebuild Starter Construction Kit`
- Starter authored content now exists:
  - `ModuleCatalog_Starter`
  - `Build_Foundation_Platform`
  - `Build_Corridor_Straight`
  - `Build_Utility_Pylon`
- Starter ghost/final prefabs exist and are assigned into the authored buildables.
- `Sockets` layer is authored and `PlayerBuilder.socketLayerMask` is populated.
- Starter build costs now use the real material pool currently present in project:
  - `Data_Copper`

## Verified Remaining Gaps

- Starter modules are still placeholder habitat pieces:
  - no authored art beyond primitive placeholders
  - no deeper module families/content taxonomy yet
- Starter costs currently rely on only one real construction resource in project:
  - `Data_Copper`
- Full manual deploy/snap/deconstruct gameplay still needs deeper live verification.
- `BaseModule` authoring is now started, but downstream habitat gameplay systems still need to consume it.

## Next Construction Targets

- Add construction-aware summaries into PDA / Data Log so build readiness is visible outside direct tool usage.
- Add richer authored construction resource pool beyond `Data_Copper`.
- Verify builder deploy / blocked / snap / missing-cost behavior against live inventory state.
- Author deconstruct-return path and world-item recovery loop.
- Replace primitive placeholder construction prefabs with first production geometry pass when art is available.
