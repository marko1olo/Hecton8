# HECTON-8 Project Bibles Index

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Evidence class: STATIC_DOC
Scope: root routing index for taste, bootstrap, gameplay, survival physiology, combat/damage contact, input, camera, sonar/navigation, logistics/power networks, world, terrain/biomes, ecosystem simulation, atmosphere, celestial cycles, water, inventory/economy, drones/automation, audio, visual presentation, cinematics, lighting, VFX, shaders/material runtime, rendering, performance, GPU compute, XR, UI, settings, localization, generated assets, AI, creature behavior, vehicles, networking/rollback, authoring/data bridge, data architecture, runtime architecture, math/determinism, telemetry, modding/SDK/API, platform readiness, release readiness, testing/CI, physics, animation, streaming, persistence, voxels, public writing, and quality gates.

## Prime Rule

Before creating a major system, boot route, screen, model, texture, shader, biome, terrain surface, ecosystem rule, sonar/scanner/navigation feature, logistics/power network, drone/automation route, XR feature, cinematic moment, celestial/macro-cycle, survival route, combat/damage path, input/control route, camera behavior, atmosphere/weather field, water effect, inventory/economy feature, creature, soundscape, lighting pass, VFX pass, menu, settings option, localized text route, performance budget, GPU compute route, networking/rollback surface, authoring tool, deterministic math route, telemetry route, modding route, SDK/API surface, testing/CI gate, platform claim, release claim, public text, or gameplay loop, read the relevant bible below. Technical mandates in `.agents-skills` still apply. These files define production taste, rejection gates, and system intent so agents do not produce polished-looking emptiness.

## Product Bar Rule

A bible is useful only if it pushes work toward a playable, beautiful, optimized, believable product. It must turn taste into decisions, decisions into implementation boundaries, and implementation into proof.

Weak bible content is rejected:

- mood words with no player-visible result;
- optimization language that permits ugly visuals;
- visual ambition with no low-tier path;
- gameplay ambition with no physical operation;
- lore ambition with no surface, source, unlock, or evidence object;
- architecture ambition with no owner, route, or proof artifact;
- acceptance language that says "good enough" without screenshots, profiler data, manifests, tests, or runtime proof where applicable.

If a route bible does not force an agent to make a better object, scene, system, interaction, text, or proof packet, strengthen the bible before implementation.

## Authority Scope

Only this index, `VISION_LOCKS.md`, and files listed in `Routes` below are standing root bible authorities. Other root markdown files may be work plans, issue lists, generated playbooks, temporary reports, or historical snapshots; read them only when the task, a route bible, or the edited file directly references them.

This is the explicit documentation-governance exception for standing root route bibles. It does not allow root reports, prompts, status files, work logs, generated evidence, task-progress prose, or temporary scan counters.

Procedural asset package pipeline authority is root `PROCEDURAL_ASSET_PIPELINE.md`. `Docs/PROCEDURAL_ASSET_PIPELINE.md` is a non-binding supporting/historical duplicate unless a later source-backed governance patch promotes or removes it.

Do not bulk-read every root `.md` file as a substitute for judgment. For ordinary work, choose `PROJECT_BIBLES.md`, `TASTE.md` when player-facing taste is involved, `quality.md` when acceptance/proof is involved, and the narrow set of matching domain bibles. More documents require a concrete reason.

Batch prompts, controller prompts, task files, and old logs assign work; they do not lower project standards. If they conflict with `AGENTS.md`, `TASTE.md`, this index, or the matching route bible, the root authority wins and the stale instruction must be corrected or reported.

Planning snapshots such as `BUILD_PLAYTEST_ISSUES.md` and `MASTER_RELEASE_WORK_PLAN.md` are not standing design authority unless the user explicitly points at them for the current task.

## Routes

- Project taste and rejection language: `TASTE.md`
- User product vision locks and ambiguity resolutions: `VISION_LOCKS.md`
- Procedural asset package pipeline binding route bible: `PROCEDURAL_ASSET_PIPELINE.md`
- Generated meshes, textures, materials, LODs, and collision: `3dmodel.md`
- Hero generated models: `3DMODEL_HERO_REALISM_OVERKILL.md`
- Texture family generation: `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- UI, HUD, menus, terminals, cockpit panels: `ui.md`
- Menu/frontend screens: `UI_MENU_SCREEN_STANDARDS.md`
- Diegetic HUD/world-space panels: `UI_DIEGETIC_HUD_STANDARDS.md`
- Settings, options, quality profiles, user configuration: `settings.md`
- Localization, subtitles, font atlases, zero-GC runtime text: `localization.md`
- Core gameplay loop, survival verbs, progression, salvage, failure: `gameplay.md`
- Survival physiology, oxygen, pressure, trauma, gas, temperature, death/recovery: `survival.md`
- Combat, damage routing, hitboxes, penetration, threat contact: `combat.md`
- Input, rebinding, device abstraction, haptics, UI navigation: `input.md`
- Player feel, controls, movement, camera, vehicles, haptics: `player.md`
- Camera, view, cockpit camera, shake, capture rigs: `camera.md`
- Sonar, scanner, navigation, acoustic radar, cartography: `sonar.md`
- Submarines, suits, docking, EVA handoff, vehicle interiors, cockpit truth: `vehicles.md`
- Tools, equipment, repair, welding, cutting, scanning, interaction targets: `tools.md`
- Construction, resources, crafting, logistics, inventory, base systems: `construction.md`
- Logistics, power, oxygen, fluid/coolant/data networks, graph flow: `logistics.md`
- Drones, automation, repair/mining/scanner probes, remote systems, tether relays: `drones.md`
- Inventory, resources, crafting, storage, salvage economy: `inventory.md`
- Narrative, missions, evidence, black-box records, quest state, text taste: `narrative.md`
- In-world articles, encyclopedia entries, survivor diaries, terminal notes, technical lore prose, and multilingual AppliedContent packets: `writing.md`
- Public copy, store text, social posts, creator outreach, marketing captions: `textes.md`
- Accessibility, readability, subtitles, remapping, flashing/motion reduction: `accessibility.md`
- Bootstrap, startup, initialization, GlobalRegistry cold setup, scene transition: `bootstrap.md`
- Runtime architecture, phases, ownership, signal lanes, hot-path access: `systems.md`
- Performance, zero-GC, frame budgets, memory/VRAM, load shedding, arena allocation: `performance.md`
- GPU compute, kernels, dispatch sizing, buffers, barriers, async readback: `compute.md`
- Networking, rollback, co-op readiness, Merkle/delta sync, reconciliation: `networking.md`
- Authoring/editor tools, CSV/SO facades, h8bin baking, data bridges: `authoring.md`
- Data architecture, DTO layout, NativeArray payloads, SignalBus packets, GPU upload records: `data.md`
- Math, determinism, AUP/floating origin, RNG, hot-path math, CI math gates: `math.md`
- Telemetry, black-box rings, crash dumps, profiler markers, post-mortem evidence: `telemetry.md`
- Modding, SDK, public API, envelope-only UGC, starter kits, command envelopes: `modding.md`
- Platform and hardware proof, MX350/i3, Steam Deck/Linux, macOS, XR, consoles: `platform.md`
- XR, VR, headset comfort, foveation, stencil masking, XR input/UI proof: `xr.md`
- Release readiness, build proof, platform proof, content lock, regression triage: `release.md`
- Physics, pressure, damage, flooding, tethers, cables, collision truth: `physics.md`
- Atmosphere, weather, tides, thermodynamics, gases, vents, macro environment: `atmosphere.md`
- Celestial cycles, tides, moon/day-night relay, seismic macro timing: `celestial.md`
- Abyssal water, currents, turbidity, silt, caustics, flooding presentation: `water.md`
- Terrain, biomes, scatter masks, geology placement, traversal surface: `terrain.md`
- Animation, IK, rigs, creature/player/tool motion, VAT strategy: `animation.md`
- Streaming, Addressables, residency, HLOD, asset lifecycle: `streaming.md`
- Persistence, save/load, binary deltas, checksums, black-box records: `persistence.md`
- Voxel terrain, SDF caves, carving, seams, voxel persistence: `voxels.md`
- AI Director, cognition, navigation, flocking, encounter pacing: `ai.md`
- Ecosystem, biome simulation, biomass migration, ecology placement: `ecosystem.md`
- World composition, biomes, routes, habitats, wrecks, geology placement: `world.md`
- Audio, sonar, warnings, soundscape, mix states: `audio.md`
- Rendering, URP, RenderGraph, shaders, fog, lighting, GPU budgets: `rendering.md`
- Shader/material runtime, keywords, variants, SRP Batcher, material proof: `shaders.md`
- Lighting, motivated lights, shadows, probes, biolum, darkness readability: `lighting.md`
- VFX, particles, leaks, sparks, silt, tool effects, pooling: `vfx.md`
- Lighting/VFX/camera composition, screenshots, cinematic presentation: `presentation.md`
- Cinematics, cutscenes, directed moments, capture truth, black-box replay: `cinematics.md`
- Creature behavior, encounters, ecology, telegraphing, AI taste: `creatures.md`
- Testing, CI, verification evidence classes, regression proof: `testing.md`
- Cross-system review and proof gates: `quality.md`

## Bible Completeness Rule

A root bible is complete only if it defines:

- the prime law of the domain;
- what owns gameplay truth;
- what is presentation only;
- what is forbidden in hot paths;
- how `GlobalQualityWeight` scales from weak hardware to visual overkill;
- what concrete proof is required before acceptance;
- which screenshots, profiler captures, manifests, or validation reports prove the work;
- what gets rejected even if it technically runs.

If a document lacks these sections, improve it before using it as authority.

Depth rule: a route file may be concise, but it must still contain a production packet or equivalent implementation checklist. A file that only states taste or mood is not a bible; strengthen it before implementation starts.

Index rule: this file is not a substitute for a domain bible. It only tells agents where the binding owner, runtime boundary, `GlobalQualityWeight`, proof, and rejection rules live.

## Missing Bible Rule

If a task belongs to a major player-facing system and no route above fits, stop and create or update the relevant bible before implementation. Do not hide a missing design authority inside one task report.
