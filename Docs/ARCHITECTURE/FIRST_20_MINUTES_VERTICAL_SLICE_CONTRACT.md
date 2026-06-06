# First 20 Minutes Vertical Slice Contract

Date: 2026-05-19

Status: PENDING VERIFICATION

Owner domain: product route / vertical slice contract

Evidence class: PRODUCT_CONTRACT / STATIC_DOC. This file is not runtime proof.

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not the playable vertical slice, save/load roundtrip, profiler, GC, or player-build proof.

- `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs`

- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

- `Assets/_Project/Scripts/SaveBinaryStorage.cs`

## Purpose

This is the product gate above architecture breadth.

Until this slice is proven in Unity, HECTON-8 is not judged by subsystem count,

H-Phi movement, docs volume, or technical ambition. It is judged by one playable

route:

```text

boot -> world load -> semi-open beautiful shallow exit -> swim -> find resource -> tool interaction -> craft/repair/build

-> hazard response -> save -> load -> return to same state

```

Selected V0 route: `FIRST_20_MINUTES_ROUTE_BRIEF.md` defines the current

spectacular semi-open shallow route. Copper Wire is only one candidate resource chain inside that route. Scanner and Repair Tool are route extensions until their

scan gates have proven scene/prefab/data unlock paths.

## Rule

Every new batch, system, global route, content pass, marketing asset, and polish

task must answer:

```text

Which First 20 Minutes moment does this make playable, visible, faster, safer, or more testable?

```

If the answer is `none`, the work is parked unless it removes a current

integration blocker.

User vision lock 2026-06-03:

The first route must be product-facing and visually compelling, not a narrow Copper Wire-only proof demo.
The project may proceed broadly while this route is being proven, but broad work must not reduce first-route quality or fake readiness.

## Required Route

| Moment | Minimum acceptance |

|---|---|

| Boot | Current first-20 proof uses `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`. `01_ORBIT` remains an enabled standalone/YELLOW prologue route, but it is not mandatory first-20 acceptance until its route card is GREEN and the root scene-flow authority is updated. Load-game resumes may still enter `02_HECTON_WORLD` directly from `01_MAIN_MENU`. |

| World load | One selected semi-open shallow/surface-adjacent route loads with terrain, water, lighting, fog, HUD, audio, spawn safety, and visual spectacle. |

| First exit | Player exits into bright, beautiful, readable photic water with alien biota, wet terrain, sky/Aegir/moon context where visible, and technogenic traces. |

| Swim | Player can navigate, surface/dive, read oxygen/pressure/depth, and return to a known point. |

| Resource | One readable resource chain exists from world object to inventory item. |

| Tool | One tool interaction is useful on that route: scan, cut, repair, drill, or harvest. |

| Craft/repair/build | One recipe, repair action, or base-support action consumes the resource and changes player capability or route safety. |

| Hazard | One fair hazard creates a decision: oxygen neglect, pressure, leak, thermal, toxic, fauna, route risk, or cave/interior/event darkness. In 0-100 m water, darkness is not the default hazard. |

| Save/load | Save, quit/reload, and return preserve position, inventory, route state, opened/looted/scanned flags, and relevant hazard state. |

| Proof | Console, Play Mode/player run, profiler, GC, memory, screenshot/clip, and save directory diff are captured. |

## Broad Work While Route Proof Is Pending

These are not deleted or globally parked. HECTON-8 proceeds broadly, with proof discipline:

- net-new biomes outside the selected route may proceed if they do not lower first-route quality;

- broad DOTS/ECS expansion remains constrained unless it supports route, platform, tooling, or future-proof foundations with proof;

- co-op runtime claims remain unproven; cautious foundation work may continue;

- extra fauna/ecology breadth may continue, but route-visible fauna has priority;

- new global authority surface not needed by the route;

- marketing outreach may prepare structure but cannot claim readiness without proof;

- visual overkill is allowed when it improves route-relevant assets, surface/shallows/mid-depth hero views, or proof captures.

## Allowed Work Outside Route

Always allowed when it directly unblocks the route, and also allowed when it builds approved broad foundations without weakening the route:

- compile/import blockers;

- save/load blockers;

- scene/asset wiring blockers;

- DataMonolith/Addressables payload blockers;

- global authority collisions or ownership defects that can corrupt route state;

- profiler/GC/memory blockers on route-critical systems.
- lore/content/localization/site packaging that uses current canon and honest status labels;
- platform/modding/XR/SDK groundwork without public readiness claims;
- visual asset, water, terrain, sky, fauna, vehicle, or UI work that raises the route-facing quality floor.

## Proof Standard

The slice is not accepted from static docs or code review.

Minimum proof package:

- Unity import and Console with no route-blocking errors.

- Play Mode or player run through the full route.

- 60 second profiler capture on the selected route.

- GC hot-path evidence.

- Memory/VRAM snapshot on route load and after save/load.

- Save/load directory diff and restored-state notes.

- One readable screenshot and one short clip from real route gameplay.

## Agent Contract

Every agent status/log/rationale entry for product work must include:

```text

First 20 Minutes moment:

Route impact:

Proof required:

Parked work rejected:

```

Global authority route cards must include the First 20 Minutes moment they serve.

H-Phi increases without route impact are architecture hygiene only.
