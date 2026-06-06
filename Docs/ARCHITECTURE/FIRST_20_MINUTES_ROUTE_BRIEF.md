# First 20 Minutes Route Brief

Date: 2026-05-19

Status: PENDING VERIFICATION

Owner domain: product route / first 20 minutes

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

## Verdict

Use the spectacular semi-open shallow route as product-facing V0.

The old Copper Wire chain remains a useful verified resource/crafting spine inside V0.
It is not sufficient as the identity of the first 20 minutes because it does not sell HECTON-8.

Do not block the first accepted route on Scanner or Repair Tool unless their scene/prefab/data unlock routes are proven. Their recipes

are valuable, but current static evidence still leaves `scan.expedition_contact`

and `scan.structure_relay` without a proven production scene/prefab/data unlock

route. Making either one mandatory without proof would move the proof target behind an

unproven scan chain.

V0 must include bright/beautiful surface-adjacent or photic shallows, alien biota, technogenic colony/industrial traces, oxygen/depth pressure, a fair death-capable hazard, one resource/tool chain, one craft/repair/build result, and save/load preservation.

## Selected V0 Route

```text

boot -> world load -> damaged safe anchor -> semi-open beautiful shallow exit

-> swim -> oxygen/depth pressure -> local unease or avoidable danger

-> find copper -> harvest/collect Data_Copper -> quest_copper_sample

-> craft/repair/build route improvement -> save -> load -> return to same state

```

## Why This Route

- User vision rejects a boring proof-only first route.

- The first playable route must sell HECTON-8: beautiful alien shallows, industrial traces, pressure, oxygen planning, tool use, and threat.

- `Recipe_CopperWire.asset` is not scan-locked.

- `Quest_CopperSample.asset` completes on `Data_Copper`.

- `ResourceNodeTemplate_CopperVein.asset` and the current catalog path point at

  cataloged raw `Data_Copper`.

- Existing static project reports already identify copper -> item collected ->

  inventory -> quest -> Copper Wire -> save/load as the bounded gameplay proof.

- Copper is useful proof material, not the whole V0 fantasy.

- This route tests the product spine without making late systems the blocker:

  boot, world load, swim, resource read, tool interaction, inventory, quest,

  fabrication, hazard pressure, persistence, and return.

## Route Steps

| Step | Minimum proof |

|---|---|

| Boot | Current first-20 proof reaches `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`. `01_ORBIT` remains an enabled standalone/YELLOW prologue route, but it is not mandatory first-20 acceptance until its route card is GREEN and the root scene-flow authority is updated. Load-game resumes may still enter `02_HECTON_WORLD` directly from `01_MAIN_MENU`. |

| Safe anchor | Player starts from a damaged but usable safe anchor such as Shallow Annex P-63 or equivalent, without hidden dev grants deciding the route. |

| First exit | Player exits into bright, beautiful, readable photic water or surface-adjacent shallows with alien biota and technogenic colony/industrial traces. |

| Swim | Player can surface/dive, read oxygen/depth/pressure, and return to a known point. |

| Hazard | Oxygen neglect, depth, pressure, route distance, or avoidable aggressive creature contact creates a fair return/death decision. In 0-100 m water, darkness is not the default hazard outside caves, interiors, storms, eclipse windows, or route events. |

| Resource | Player finds the selected starter resource in the route, not through a console grant. Copper is allowed if the route proves it. |

| Tool | Actual starter interaction can acquire or use the resource. If the chosen resource requires an unavailable tool, this is a blocker, not a pass. |

| Inventory | `InteractionEvents.ItemCollected` or equivalent route event is observed and inventory contains the cataloged starter item. |

| Quest/Need | `quest_copper_sample` or another real route need activates/completes through the route, or its activation seam is logged as a blocker. |

| Craft/repair/build | Fabricator, repair action, or base-support action consumes the resource and changes capability or route safety. Copper Wire is acceptable but not mandatory if a stronger verified chain exists. |

| Save/load | Reload preserves position, inventory, quest state, crafted result, opened/looted flags, and hazard-relevant state. |

| Capture | Console, Play Mode/player run, 60s profiler, GC, memory/VRAM, save diff, screenshot, and clip are captured. |

## Parallel Work While V0 Is Pending

The project is not restricted to a narrow single-resource proof lane.

Allowed parallel work:

- surface, shallow, and medium-depth visual quality;
- lore, AppliedContent, website/wiki, and localization packaging;
- terrain, water, celestial visuals, assets, creatures, vehicles, UI, tools, platform, XR, modding, and SDK foundations when they follow root bibles;
- Scanner and Repair Tool route proof as upgrades to the spectacular V0;
- DataMonolith, Addressables, import/export foundations;
- visual overkill work when it improves route-relevant scenes, assets, or captures.

Still constrained:

- public readiness claims without proof;
- co-op runtime claims beyond cautious foundation work;
- unrelated breadth that lowers the first route quality;
- placeholders below the visual floor in production route scenes.

## Current Route Blockers To Prove Or Fix

- Starting tool truth: prove the player can acquire or use the selected starter resource with real authored

  equipment, or pick a starter resource interaction that is already reachable.

- Spectacle truth: prove the first exit has bright, beautiful, readable water, terrain, sky/celestial context where visible, alien biota, and technogenic traces in Unity.

- Hazard truth: prove immediate death is possible only through fair player error such as oxygen neglect or avoidable aggressive creature contact.

- Quest activation truth: prove `quest_copper_sample` or the selected route need activates before/when the

  player collects/uses the starter resource in the real route.

- Fabricator/repair/build truth: prove the route has a reachable powered fabricator, repair action, or base-support action and

  enough inventory capacity to complete the chain.

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
