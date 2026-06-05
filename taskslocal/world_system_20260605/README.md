# World System Task Index - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_DOC`.
Scope: world placement/orchestration task packets generated during the 2026-06-05 run.

This folder does not prove Unity placement, scene state, visual quality, profiler, GC, memory, Addressables, or route acceptance.

## Current Packets

- `WORLD_OWNER_01_ROCK_FLORA_CORAL_PLACEMENT_STAGING_PACKET.md` - future placement staging packet for rocks, flora, coral, and debris after base proof passes for water, sky/Aegir/moons, terrain, player/HUD, lighting, route materials, and h8_1475 screenshots.

## Hard Boundaries

- Placement is deferred until base proof exists.
- Placement must improve route decisions, not fill space.
- `WorldProceduralProxy`, `WorldRuntime/ProceduralPlaceholders`, primitive visible meshes, proxy/default/null materials, and source-only generated images are rejected for visible route placement.
- Do not use darkness, fog, bloom, particles, caustics, or clutter to hide weak water, sky, Aegir, terrain, shoreline, or material failures.
- Do not mutate scenes, prefabs, materials, importers, Addressables, or project settings from this packet alone.

Final status remains `PENDING VERIFICATION`.
