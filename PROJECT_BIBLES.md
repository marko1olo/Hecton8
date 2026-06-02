# HECTON-8 Project Bibles Index

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Scope: root routing index for taste, gameplay, world, audio, visual presentation, UI, generated assets, creature behavior, and quality gates.

## Prime Rule

Before creating a major system, screen, model, texture, biome, creature, soundscape, VFX pass, menu, or gameplay loop, read the relevant bible below. Technical mandates in `.agents-skills` still apply. These files define production taste, rejection gates, and system intent so agents do not produce polished-looking emptiness.

## Routes

- Project taste and rejection language: `taste.md`
- Generated meshes, textures, materials, LODs, and collision: `3dmodel.md`
- Hero generated models: `3DMODEL_HERO_REALISM_OVERKILL.md`
- Texture family generation: `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- UI, HUD, menus, terminals, cockpit panels: `ui.md`
- Menu/frontend screens: `UI_MENU_SCREEN_STANDARDS.md`
- Diegetic HUD/world-space panels: `UI_DIEGETIC_HUD_STANDARDS.md`
- Core gameplay loop, survival verbs, progression, salvage, failure: `gameplay.md`
- World composition, biomes, routes, habitats, wrecks, geology placement: `world.md`
- Audio, sonar, warnings, soundscape, mix states: `audio.md`
- Lighting, VFX, camera, screenshots, render presentation: `presentation.md`
- Creature behavior, encounters, ecology, telegraphing, AI taste: `creatures.md`
- Cross-system review and proof gates: `quality.md`

## Missing Bible Rule

If a task belongs to a major player-facing system and no route above fits, stop and create or update the relevant bible before implementation. Do not hide a missing design authority inside one task report.
