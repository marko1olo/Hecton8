# First 20 Minutes Route Brief

Date: 2026-05-19
Status: PENDING VERIFICATION

Evidence class: STATIC_SOURCE / STATIC_DOC. This is the selected product route,
not Unity runtime proof.

## Source Anchors

- `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
- `Assets/_Project/Scripts/SaveBinaryStorage.cs`
- `Assets/_Project/Data/Crafting/Recipes/Recipe_CopperWire.asset`
- `Assets/_Project/Data/Lore/Quests/Quest_CopperSample.asset`
- `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_CopperVein.asset`
- `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset`
- `Assets/_Project/Data/Items/Resources/Components/Comp_CopperWire.asset`

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Verdict

Use the Copper Wire route as V0.

Do not use Scanner or Repair Tool as the first accepted route yet. Their recipes
are valuable, but current static evidence still leaves `scan.expedition_contact`
and `scan.structure_relay` without a proven production scene/prefab/data unlock
route. Making either one the V0 gate would move the proof target behind an
unproven scan chain.

## Selected V0 Route

```text
boot -> world load -> safe exit -> swim -> oxygen/depth pressure
-> find copper -> harvest/collect Data_Copper -> quest_copper_sample
-> craft Recipe_CopperWire -> save -> load -> return to same state
```

## Why This Route

- `Recipe_CopperWire.asset` is not scan-locked.
- `Quest_CopperSample.asset` completes on `Data_Copper`.
- `ResourceNodeTemplate_CopperVein.asset` and the current catalog path point at
  cataloged raw `Data_Copper`.
- Existing static project reports already identify copper -> item collected ->
  inventory -> quest -> Copper Wire -> save/load as the bounded gameplay proof.
- This route tests the product spine without making late systems the blocker:
  boot, world load, swim, resource read, tool interaction, inventory, quest,
  fabrication, hazard pressure, persistence, and return.

## Route Steps

| Step | Minimum proof |
|---|---|
| Boot | Production order reaches `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`. |
| Safe exit | Player exits the start/lifepod state without hidden dev grants deciding the route. |
| Swim | Player can surface/dive, read oxygen/depth/pressure, and return to a known point. |
| Hazard | Oxygen, depth, darkness, pressure, or route distance creates a fair return decision. |
| Resource | Player finds the selected copper source in the route, not through a console grant. |
| Tool | Actual starter interaction can acquire the resource. If copper requires Drill and the player lacks a real starter Drill route, this is a blocker, not a pass. |
| Inventory | `InteractionEvents.ItemCollected` or equivalent route event is observed and inventory contains cataloged `Data_Copper`. |
| Quest | `quest_copper_sample` activates/completes through the route or its activation seam is logged as a blocker. |
| Craft | Fabricator crafts `Recipe_CopperWire` into `Comp_CopperWire` without scan-gate dependency. |
| Save/load | Reload preserves position, inventory, quest state, crafted result, opened/looted flags, and hazard-relevant state. |
| Capture | Console, Play Mode/player run, 60s profiler, GC, memory/VRAM, save diff, screenshot, and clip are captured. |

## Park Until V0 Passes

- Scanner crafting as a product gate, until `scan.expedition_contact` has a
  proven route.
- Repair Tool crafting as a product gate, until `scan.structure_relay` has a
  proven route.
- Extra fauna/ecology breadth outside the selected route.
- Net-new biomes outside the selected route.
- Broad DOTS/ECS expansion not needed to prove the route.
- Co-op runtime claims beyond local state-contract preparation.
- Visual overkill that is not captured in the route proof.
- Marketing send-ready status without real assets from this route.

## Current Route Blockers To Prove Or Fix

- Starting tool truth: prove the player can acquire copper with real authored
  equipment, or pick a starter resource interaction that is already reachable.
- Quest activation truth: prove `quest_copper_sample` activates before/when the
  player collects copper in the real route.
- Fabricator truth: prove the route has a reachable powered fabricator and
  enough inventory capacity to craft Copper Wire.
- Item identity truth: legacy root `Data_Copper` must not contaminate the route;
  the cataloged raw asset is the authority.
- Persistence truth: save/load must preserve the route state, not only serialize
  a happy-path file.

## Agent Rule

Every new task must state one of these:

```text
First 20 route step:
Route impact:
Proof artifact:
Parked work rejected:
```

If the task does not improve this route or remove a blocker, it is parked.
