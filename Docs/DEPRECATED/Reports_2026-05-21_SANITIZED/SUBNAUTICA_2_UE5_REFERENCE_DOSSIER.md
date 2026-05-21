# SUBNAUTICA 2 UE5 REFERENCE DOSSIER

Date: 2026-05-17

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R13 Report Snapshot Boundary

This report file is a snapshot/provenance document. It is active only where it agrees with:

- `Docs/README.md`
- `Docs/Reports/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Historical `PASS`, `VERIFIED`, `current`, `latest`, counter, compile, runtime, 0-GC, frame-time, cost, and performance statements inside this report are not current proof unless the exact claim links a fresh artifact path, command/tool, timestamp, evidence class, and unresolved-error list. No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied by this file alone.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
Agent: SUBNAUTICA_RESEARCHER
Mode: clean-room public research only. No Subnautica 2 files, assets, binaries, or Unreal project internals inspected.

## Executive Verdict

Subnautica 2 is public, but current state is Early Access / Xbox Game Preview, not final 1.0. It launched on 2026-05-14.

The screenshot surface is catchable. The harder threat is the production machine around that surface: co-op, base building, content cadence, platform presets, player feedback loop, and creature reactivity. HECTON-8 should not chase a bright coral fantasy clone. The winning angle is deep-sea noir: pressure, metal, acoustic danger, atmosphere failures, hull deformation, silt, salt, black-box telemetry, and persistent wreck systems.

## Verified Product Facts

- Title: Subnautica 2.
- Developer / publisher: Unknown Worlds Entertainment.
- Engine: Unreal Engine 5 is confirmed in KRAFTON press material for creature AI / behavior work.
- Release state: Early Access on Steam and Game Preview on Xbox ecosystem.
- Early Access start: 2026-05-14.
- Steam app id: 1962700.
- Steam platform: Windows only at time of check.
- Xbox ecosystem: Xbox Series X|S, Xbox on PC, ROG Xbox Ally / Ally X, Xbox Game Pass Ultimate, PC Game Pass.
- Genre tags / categories: Action, Adventure, Early Access, single-player, multiplayer, co-op, online co-op, cross-platform multiplayer, full controller support, accessibility categories.
- Storage: 50 GB.
- Steam minimum:
  - OS: Windows 10/11.
  - CPU: Intel Core i5-8400 / AMD Ryzen 5 2600.
  - RAM: 12 GB.
  - GPU: GeForce GTX 1660 6GB / RX 5500 XT 6GB.
  - DirectX: 12.
- Steam recommended:
  - OS: Windows 11.
  - CPU: Intel Core i7-13700 / AMD Ryzen 7 7700X.
  - RAM: 16 GB.
  - GPU: GeForce RTX 3070 8GB / RX 6700 XT 8GB.
  - DirectX: 12.

## Feature Surface

- New alien underwater world.
- Optional online co-op with up to three friends, meaning four players total.
- Four pre-designed characters at Early Access start; more characters/customization planned.
- Tadpole submersible.
- Base design and customization.
- Scanning / studying biodiversity.
- Creatures from small fauna to Leviathans.
- Early Access content expansion: more biomes, creatures, craftables, tools, equipment, vehicles, and narrative.
- Explicit warning from Steam text: bugs, in-development features, and performance issues can exist during Early Access.

## Roadmap / Live Product Reading

The important point is not only what is playable now. Subnautica 2 is structured as a public live-development product. Unknown Worlds explicitly frames feedback as part of development. That means the competitive benchmark will move every few months.

Implication for HECTON-8:

- Build pipelines must support content updates without schema chaos.
- Save format migration must be treated as foundation, not late polish.
- Content validators must become product gates, not menu-only diagnostics.
- Feedback/telemetry loops matter. A technically stronger build can lose if it cannot ingest feedback and patch safely.

## Screenshot Audit

Six official 1920x1080 Steam screenshots were downloaded and inspected.

### Screenshot 0: Underwater Base Exterior

Observed:

- Modular white/yellow underwater base with rounded sci-fi forms.
- Strong window/module silhouettes.
- Heavy blue/yellow haze.
- Dense but localized coral/flora clusters.
- Many small particles/bubbles.
- Bright point lights around the base.
- Cockpit/visor framing.

Tactical reading:

- The look is driven by fog, silhouette, local emissives, and art-directed clusters.
- HECTON-8 can approximate with sector object batches, fog LUTs, emissive masks, cheap particles, and strong base silhouettes.

### Screenshot 1: Base Interior

Observed:

- Glossy modular walls/floor.
- Bright cyan/white light panels.
- Pool or moonpool-like water surface with submersible.
- Clean readable sci-fi shapes.
- Controlled reflections/speculars.

Tactical reading:

- This is asset polish and lighting discipline more than exotic rendering.
- HECTON-8 interior target should be harsher: wet metal, condensation, grime, pressure seals, emergency lighting, corrosion.

### Screenshot 2: Co-op Shallow Biome

Observed:

- Multiple player characters.
- Bright shallow-water blue.
- Orange/yellow flora density.
- Rock arches / coral tunnels.
- Creature silhouettes readable at distance.
- Good co-op scale composition.

Tactical reading:

- Their co-op screenshot sells social exploration.
- HECTON-8 should not chase coral brightness. Our equivalent is squad-scale dread readability: silhouettes in murk, sonar pings, safety line/tether, pressure bubbles, distant metal impacts.

### Screenshot 3: Dark Scanner / Creature Biome

Observed:

- Tool foreground.
- Dark purple/gray water.
- Glowing flora and creature silhouettes.
- Strong flashlight-like focus.
- Sparse but effective particles.

Tactical reading:

- This is closest to HECTON-8's territory.
- We can beat it by adding industrial signals: acoustic returns, silt occlusion, hull echoes, pressure warnings, black-box telemetry dumps, distorted light shafts.

### Screenshot 4: Blue Deeper Biome

Observed:

- Submersible cockpit framing.
- Large purple anemone-like forms.
- Blue haze and readable depth layers.
- Caustic floor lighting.
- Sparse fauna.

Tactical reading:

- Large landmark props plus fog depth sell scale.
- HECTON-8 needs reusable biome landmark grammar: wreck ribs, cables, pressure doors, industrial towers, abyssal flora, sonar occluders.

### Screenshot 5: Orange / Thermal Mood Biome

Observed:

- Strong orange monochrome fog.
- Vehicle silhouette.
- Bubble/particle trail.
- Dark foreground hands/cockpit frame.
- Hot/hostile atmosphere.

Tactical reading:

- The strongest visual trick is a one-color mood band plus silhouette.
- HECTON-8 should build a biome color authority system: each zone gets a strict fog/color/acoustic identity, not random pretty palettes.

## Visual Threat Analysis

Subnautica 2's visible graphics are not unreachable. The screenshots show:

- Strong color grading / fog bands.
- Modular asset polish.
- Readable silhouettes.
- Stylized flora clusters.
- Bubbles and particles.
- Caustic-style lighting.
- Emissive accents.
- Hand/cockpit framing.
- Landmark props.
- Soft underwater depth layering.

HECTON-8 counter-tech:

- 1D depth/fog LUTs.
- Triangle-noise silt.
- Dithered particles.
- Billboard / impostor flora islands.
- Baked/projected caustics.
- Object-batched biome dressing.
- Emissive mask language for industrial props.
- Strong zone-specific color authority.
- Separate low/mid/high/ultra VFX density tables.

## Real Threat Analysis

### Threat 1: Co-op and Shared Worlds

Public guides describe up to four-player co-op and single-player worlds becoming multiplayer worlds. Current weak points include no character import into another world, blunt guest editing permissions, and no revive system at launch.

HECTON-8 implication:

- Even if HECTON-8 ships single-player first, save/world state should not block future co-op.
- World operations need permissions and deterministic state changes.
- Avoid local-only architecture that cannot become replicated intent later.

### Threat 2: Content Cadence

Early Access roadmap language promises additional biomes, creatures, craftables, tools, equipment, vehicles, and narrative.

HECTON-8 implication:

- DataMonolith / MacroDB / content authority cannot remain half-populated.
- Schema migration and content validation are strategic requirements.
- Mod/data overlay architecture should help us move faster, not just serve modders.

### Threat 3: Platform Presets

Xbox Wire says Unknown Worlds used Unreal Insights and tuned ROG Xbox Ally graphics presets.

HECTON-8 implication:

- "Scalability" must be hard content budgets, not just quality sliders.
- MX350 / Steam Deck / Quest / high PC require different object, VFX, audio, shader, and streaming budgets.
- Ultra visual overkill must be isolated in high-tier packs.

### Threat 4: Creature Reactivity

KRAFTON press describes Collector Leviathan using UE5 behavior trees, stimulus systems, reactions to light/sound/player actions, and simulated tentacle animation.

HECTON-8 implication:

- Do not copy UE5 behavior trees.
- Borrow the contract shape: typed stimuli, readable reactions, black-box telemetry, deterministic intent, animation/detail LOD.
- Low-tier AI can be fake: stimulus score, state machine, cheap avoidance, staged animation.
- High-tier can add secondary motion, tentacles, particles, audio response, silt trails.

### Threat 5: First-Hour Loop

Impressions emphasize early survival/exploration readability and dread.

HECTON-8 implication:

- First hour must prove identity immediately.
- Player should understand pressure, air, sound, wreck salvage, scanning, base safety, and a clear goal path.
- Current HECTON-8 scan route validators being menu-only is strategically wrong.

### Threat 6: Player Trust / Comfort

Public negative-review themes include EULA/ToS pushback, missing Early Access features, FOV/comfort complaints, and early-access boundary frustration.

HECTON-8 implication:

- Comfort options are not optional polish: FOV, text scale, motion/camera comfort, input remap, subtitle/audio controls.
- First public build must be honest about scope.
- Black-box crash dumps and telemetry are product trust features.

## What HECTON-8 Should Borrow

Borrow as pattern, not assets/code:

- Clear Early Access roadmap language.
- Screenshot composition discipline.
- Biome color identity.
- Modular base readability.
- Vehicle silhouettes.
- Co-op-safe state thinking.
- Creature stimulus contract.
- Platform preset validation.
- Public feedback loop.
- Strong first-ten-hours loop.

## What HECTON-8 Should Not Borrow

- Bright coral fantasy identity.
- Direct UE5 behavior tree implementation.
- Any proprietary art, assets, code, names, files, UI layouts, or story.
- Feature parity panic.
- Generic "underwater survival but darker" positioning.

## HECTON-8 Counterposition

Subnautica 2: bright alien-ocean adventure.
HECTON-8: NASA-punk / deep-sea noir engineering survival.

Non-negotiable HECTON-8 signatures:

- Pressure as a system, not a number.
- Acoustic threat readability.
- Hull deformation and repair consequences.
- Silt and visibility collapse.
- Salt / condensation / visor contamination.
- Industrial wreck topology.
- Power/atmosphere/logistics failures.
- Black-box telemetry.
- Persistent world scars.
- Controlled visual overkill on high-tier hardware.

## Immediate Tactical Tasks For HECTON-8

P0:

1. Finish ContentAuthority payload generation: DataMonolith/static-data artifact, sector payload
   manifests, Unity object asset groups where deliberately chosen, hash maps, and VFX prewarm
   manifest.
2. Decide authoritative static-data path and make `static_data.h8bin` mandatory for production builds.
3. Promote first-hour route validation from menu/warning to build or preplay gate.
4. Add missing black-box rings for atmosphere, organic destruction, and vegetation/abyssal path.
5. Define biome visual authority: fog color, silt density, acoustic profile, caustic strength, flora/wreck dressing budget.
6. Add comfort settings early: FOV, camera comfort, text scale, subtitle controls, input remap, audio sliders.

P1:

1. Build low/mid/high/ultra content budget tables.
2. Add object-batch payloads for biome dressing.
3. Define typed stimulus lanes for fauna/threat systems.
4. Prepare save/schema migration test harness.
5. Create first-hour playable route proof with scan, pressure, salvage, repair, and base safety loop.

P2:

1. Overkill-only visual pack: visor salt crystals, volumetric silt wakes, procedural hull dents, high-tier POM/raymarch/VFX.
2. Optional future co-op-readiness: replicated intent contracts, permissions, world operation audit trail.
3. Community feedback ingestion process tied to reproducible telemetry/dumps.

## Proof Limits

- Screenshots are official still images, not captured runtime profiler frames.
- Public press/impressions are not engineering proof.
- Launch sales/concurrency are market signals, not quality proof.
- No Subnautica 2 files, assets, binaries, or Unreal internals were inspected.
- No HECTON-8 code was changed in this dossier pass.

## Sources

- Unknown Worlds Early Access release: https://unknownworlds.com/en/news/subnautica-2-early-access-released
- Unknown Worlds Early Access roadmap: https://unknownworlds.com/en/news/subnautica-2-early-access-roadmap
- Steam store / app 1962700: https://store.steampowered.com/app/1962700/Subnautica_2/
- Xbox Wire Game Preview: https://news.xbox.com/en-us/2026/05/04/subnautica-2-game-preview/
- KRAFTON Collector Leviathan press: https://press.krafton.com/en-GB/UNKNOWN-WORLDS-REVEALS-THE-COLLECTOR-LEVIATHAN-IN-SUBNAUTICA-2
- PC Gamer Early Access interview: https://www.pcgamer.com/games/survival-crafting/subnautica-2-devs-say-its-bigger-and-more-polished-than-any-of-the-studios-previous-early-access-launches/
- PC Gamer co-op guide: https://www.pcgamer.com/games/survival-crafting/subnautica-2-multiplayer-co-op-guide/
- PCGamesN Early Access impressions: https://www.pcgamesn.com/subnautica-2/early-access-impressions
- GamesRadar launch signal report: https://www.gamesradar.com/games/survival/subnautica-2-makes-a-splash-with-2-million-copies-sold-in-12-hours-18-000-positive-steam-reviews-and-651-000-concurrent-players-across-pc-and-xbox/
