# First-20 Quest/Data Spine Conflict - 2026-06-06

Status: `STATIC_SYNTHESIS / RUNTIME_ACCEPTANCE_BLOCKED`
Evidence class: `STATIC_DOC + STATIC_SOURCE`

No Unity, MCP, Play Mode, profiler, import/build, scene, prefab, material, ProjectSettings, raw YAML, deletion, restore, move, stage, checkout, copy, revert, or commit action was performed for this synthesis.

## Active Spine Decision

Use **Black Keel / claim / P-63 shallow salvage** as the product wrapper, with the current copper/resource director route as the first runtime-bindable substrate.

This is not acceptance. It is the cleanest static route because it preserves the project identity: professional salvage pressure, debt/claim motive, physical evidence before exposition, bright shallow exit, oxygen/pressure cue, safe anchor, and a return/save proof target.

Concrete order:

```text
wake/safe anchor -> bright shallow exit -> oxygen/pressure cue
-> physical salvage/resource action -> Data_Copper or equivalent route material
-> repair/build/useful improvement -> P-63/Black Keel evidence packet
-> save/load return
```

## Competing Spines

| Spine | Evidence | Static status |
|---|---|---|
| Copper route | `FirstHourDirector.cs`, `Data/Narrative/Quest_Graph.json`, `Quest_CopperSample.asset`, first-20 docs | Keep as proof substrate. Reject as whole first-20 identity if it becomes "collect copper only." |
| Titanium-to-scanner | `Quest_Graph.json`, `Quest_FirstHour_CollectTitanium.asset`, `Quest_FirstHour_CraftScanner.asset` | Demote/defer. It makes `Item_Tool_Scanner` mandatory before scanner unlock/binding proof exists. |
| Scanner-first / leviathan / radio | `Data/Narrative/First_Hour_Quests.json`, generated quest masks, DAG loading/resolver sources | Defer. Runtime event bridge is unproven. |
| Black Keel / claim / P-63 | `Docs/Lore/AppliedContent`, `FIRST_20_CONTENT_ROUTE_STATIC_SYNTHESIS_20260606.md` | Best product wrapper. Runtime lore/objective gating proof absent. |

## Hard Conflicts

- Objective order conflict: `FirstHourDirector.cs` runs orientation -> copper sample -> first breath/module discovery, while `First_Hour_Quests.json` runs wake -> scanner -> leviathan trace -> radio.
- Tool unlock conflict: scanner-first and titanium-to-scanner require `Item_Tool_Scanner` too early.
- Scanner target conflict: the DAG wants `leviathan_trace_alpha_scanned`; inspected scanner lanes emit scan/lore signals, but no proven quest bridge was found.
- Interaction target conflict: `radio_repair_completed` has no proven direct `InteractionEvents` completion lane.
- Oxygen/depth conflict: `quest_first_breath` has static threshold mismatch in `Quest_Graph.json` context (`triggerValue=150`, `completionValue=300`). Do not bind HUD/objective copy until intended threshold is resolved.
- Safe anchor conflict: docs require a damaged safe pocket/P-63 anchor; runtime object binding proof is absent.
- Lore gating conflict: P151-P155/P246-P250 content exists, but stable runtime LocIDs/unlock IDs are unproven.
- Data authority conflict: `Quest_Graph.json` identifies itself as a generated mirror; runtime authority remains `QuestData` unless promoted. `First_Hour_Quests.json` has compiled DAG/masks, but objective UI/runtime ownership is unproven.

## Integrator Disposition

- Black Keel/P-63 + copper substrate: active spine, pending runtime proof.
- Copper-only: proof substrate only.
- Titanium-to-scanner: deferred until scanner recipe, materials, pickup/craft UI, objective UI, and scan target prove in Play Mode.
- Scanner-first / leviathan / radio: deferred until scan/interaction triggers are bridged into the DAG or the graph is renamed/recompiled to current signal lanes.
- P151-P155/P246-P250: narrative source only until PDA/lore/objective bindings exist.

## Required Proof Predicates

- Runtime objective sequence fires in the chosen order.
- Safe anchor/P-63 pocket exists as a physical route object.
- Oxygen/pressure cue appears through active HUD/visor/audio/gameplay state.
- Physical salvage/resource action completes with correct item identity.
- P-63/Black Keel packet unlocks from physical action, not from text preload.
- Save/load preserves objective, item, opened/looted/scanned, safe anchor, and hazard state.

Final status: `STATIC_ROUTE_SELECTED / PENDING OWNER02_OWNER03_RUNTIME_PROOF`.
