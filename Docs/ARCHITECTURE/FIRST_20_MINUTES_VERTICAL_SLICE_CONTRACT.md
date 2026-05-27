# First 20 Minutes Vertical Slice Contract

Date: 2026-05-19

Status: PENDING VERIFICATION

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

boot -> world load -> swim -> find resource -> tool interaction -> craft/repair/build

-> hazard response -> save -> load -> return to same state

```

Selected V0 route: `FIRST_20_MINUTES_ROUTE_BRIEF.md` defines the current

Copper Wire route. Scanner and Repair Tool are P1 route extensions until their

scan gates have proven scene/prefab/data unlock paths.

## Rule

Every new batch, system, global route, content pass, marketing asset, and polish

task must answer:

```text

Which First 20 Minutes moment does this make playable, visible, faster, safer, or more testable?

```

If the answer is `none`, the work is parked unless it removes a current

integration blocker.

## Required Route

| Moment | Minimum acceptance |

|---|---|

| Boot | New Game uses `00_BOOTSTRAP -> 01_MAIN_MENU -> 01_ORBIT -> 02_HECTON_WORLD`; load-game resumes may still enter `02_HECTON_WORLD` directly from `01_MAIN_MENU`. |

| World load | One selected biome/route loads with terrain, water, lighting, fog, HUD, audio, and spawn safety. |

| Swim | Player can navigate, surface/dive, read oxygen/pressure/depth, and return to a known point. |

| Resource | One readable resource chain exists from world object to inventory item. |

| Tool | One tool interaction is useful on that route: scan, cut, repair, drill, or harvest. |

| Craft/repair/build | One recipe, repair action, or base-support action consumes the resource and changes player capability or route safety. |

| Hazard | One fair hazard creates a decision: pressure, leak, thermal, toxic, fauna, darkness, or route risk. |

| Save/load | Save, quit/reload, and return preserve position, inventory, route state, opened/looted/scanned flags, and relevant hazard state. |

| Proof | Console, Play Mode/player run, profiler, GC, memory, screenshot/clip, and save directory diff are captured. |

## Parked Until Route Proof

These are not deleted. They are deprioritized until the route is live:

- net-new biomes outside the selected route;

- broad DOTS/ECS expansion;

- co-op runtime claims beyond local state-contract preparation;

- extra fauna/ecology breadth not visible on the route;

- new global authority surface not needed by the route;

- marketing outreach beyond verification/asset planning;

- visual overkill that is not captured in the route.

## Allowed Work Outside Route

Allowed only when it directly unblocks the route:

- compile/import blockers;

- save/load blockers;

- scene/asset wiring blockers;

- DataMonolith/Addressables payload blockers;

- global authority collisions or ownership defects that can corrupt route state;

- profiler/GC/memory blockers on route-critical systems.

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
