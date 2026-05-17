# HECTON-8 Dream vs Subnautica 2 Counterposition

Date: 2026-05-17
Status: DESIGN AUTHORITY PROPOSAL / RUNTIME PENDING
Owner lane: SUBNAUTICA_RESEARCHER research pass
Source dossier: `Docs/Reports/SUBNAUTICA_2_UE5_REFERENCE_DOSSIER.md`

## Purpose

This file converts the Subnautica 2 public reference pass into a stable HECTON-8 identity target.
It is not a feature-parity document. It is a rejection of weak imitation.

Subnautica 2 owns bright alien-ocean adventure, approachable co-op, clean base building, friendly
exploration readability, and Early Access community cadence.

HECTON-8 must own NASA-punk / deep-sea noir engineering survival: pressure, corrosion, acoustics,
systems failure, industrial wrecks, black-box evidence, and hostile water that looks expensive even
when the math is cheap.

## Non-Negotiable Identity

HECTON-8 cannot be "Subnautica but darker." That is not a product identity. It is a palette swap.

The HECTON-8 first impression must communicate:

- The ocean is a machine that wants to crush the player.
- Survival depends on engineering discipline, not colorful collecting.
- Visibility is a resource.
- Sound is a threat surface.
- The suit, visor, base, and vehicle are fragile instruments.
- Every failure leaves evidence: telemetry, dents, leaks, pressure scars, corrupted logs.
- High-tier hardware buys sensory overload, not more gameplay truth.

## Dream Pillars

### 1. Pressure Is Visible

Pressure cannot stay as a HUD number. It needs visible, audible, and mechanical consequences.

Low:

- Screen-edge compression vignette.
- Cheap hull creak audio layers.
- Scalar stress UI from depth and damage.
- Prebaked crack/decal stages.

Middle:

- Door and panel stress indicators.
- Localized leak VFX triggered by scalar compartment state.
- Repair loop tied to pressure differential.

High:

- Procedural hull dent masks.
- Tool sparks, pressure flicker, metal groan layers.
- Creature behavior reacting to damaged/noisy systems.

Ultra:

- Animated stress lines, visor microfractures, silt pulses from hull flex.
- Overkill-only secondary VFX driven by the same scalar pressure contract.

### 2. Visibility Collapse Is Gameplay Readability

Subnautica 2 screenshots use clean fog/color staging. HECTON-8 should use fog as a controlled
threat system.

Low:

- 1D depth fog LUT.
- Dithered fog shells.
- Triangle-noise silt texture.
- Hard budget caps on particle sheets.

Middle:

- Biome fog authority: color, density, acoustic profile, caustic strength, silt density.
- Local silt volumes near wrecks, vents, and propwash zones.

High:

- Flow-aligned silt wakes.
- Scanner/sonar windows that cut through fog with limited trust.

Ultra:

- Volumetric silt wake, light shafts, high-density particulates, and visor contamination.

### 3. Acoustic Threat Beats Bright Creature Spectacle

Subnautica 2 can sell readable creatures with silhouettes and reactions. HECTON-8 needs acoustic
dread: something is present before it is visible.

Low:

- Directional audio cues from zone/state, not ray-perfect sound.
- Hydrophone-style HUD pulses.
- Threat score from distance, line category, and noise output.

Middle:

- Typed stimulus lanes for light, sound, impact, blood, power draw, and hull stress.
- Creature reactions from deterministic utility scores.

High:

- Sonar shadow silhouettes.
- AI black-box heartbeat entries for last known stimulus and chosen reaction.

Ultra:

- Secondary tentacle/body motion, occluded roars, hull resonance, and reactive silt.

### 4. Wrecks Are Industrial Dungeons

Subnautica 2 base and biome readability is strong. HECTON-8 should answer with wreck topology:
dangerous, legible, replayable engineering spaces.

Low:

- Baked wreck modules and occlusion-friendly corridors.
- Object-batched debris and cable silhouettes.
- Explicit route breadcrumbs through lighting and sound.

Middle:

- Procedural wreck assembler emits sector payloads, not spawned GameObject clutter.
- Salvage/repair/scanner loop inside the first hour.

High:

- Persistent scars: cut panels, drained compartments, repaired conduits, opened sealed doors.

Ultra:

- Overkill dressing: hanging particulate curtains, procedural dents, high-detail panels, local wetness.

### 5. The Player Instrument Must Feel Expensive

The visor, scanner, PDA, sub cockpit, and tools are the product face. Cheap world math must fund
expensive instrument feedback.

Low:

- Zero-GC text updates.
- Static mask overlays.
- Cheap scan sweep and acoustic pulses.

Middle:

- Diegetic error codes, pressure alarms, subsystem heartbeat.
- Scanner route feedback tied to real content records.

High:

- Salt/condensation masks, chromatic damage, vibration/haptic language.

Ultra:

- Layered visor contamination, lens refraction, overkill scan holograms.

## Borrow From Subnautica 2

Borrow contract shapes, not content:

- Screenshot composition discipline: readable silhouettes and color staging.
- Biome identity: each zone has a color/fog/flora/audio contract.
- Modular base readability: strong forms and visible function.
- Vehicle silhouette discipline: instantly readable outline and cockpit framing.
- Strong first-ten-hours loop: scan, craft, explore, return, upgrade.
- Creature stimulus contract: light/sound/action -> readable reaction.
- Platform preset discipline: low-tier is not a scaled-down accident.
- Community cadence: feedback must map to telemetry and reproducible content state.
- Co-op-safe thinking: save/schema/world-operation contracts should not block future shared worlds.

## Reject From Subnautica 2

Do not borrow:

- Bright coral fantasy identity.
- Proprietary art, assets, files, names, UI, story, code, or Unreal internals.
- Feature parity panic.
- UE5 behavior-tree implementation as architecture.
- "More colorful underwater survival" as product direction.
- Co-op before singleplayer state, save, permission, and telemetry contracts are stable.

## First-Hour Dream Route

The first hour must prove HECTON-8, not merely introduce mechanics.

Required sequence:

1. Wake in compromised industrial habitat or wreck-adjacent shelter.
2. Immediate pressure/noise/visibility problem, not a generic tutorial prompt.
3. Scanner finds a salvage lead through silt and acoustic noise.
4. Player repairs or stabilizes a small system with visible consequences.
5. First outside route uses fog, sonar, light, and oxygen as readable constraints.
6. First creature threat is heard before seen.
7. Return path shows a persistent scar: repaired panel, opened route, dent, leak, log, or black-box clue.
8. Base/shelter safety is partial, not absolute.

## Tier Contract

Low / toaster:

- The dream survives through composition, fog LUTs, audio cues, scalar pressure, and clean route logic.
- No expensive raymarching, no dense particles, no high-sample POM, no unbounded dynamic lights.

Middle:

- Object batches, biome fog authority, scan-route data, stable save/schema migration.
- Enough density to make the world feel authored, not placeholder procedural.

High:

- Reactive fauna, richer materials, silt wakes, better cockpit/visor feedback, more local VFX.
- Still uses the same gameplay truth as Low.

Ultra:

- Visual overkill: salt crystals, volumetric silt, procedural hull dents, abyssal light shafts,
  dense flora sway, high-tier POM/raymarch/VFX.
- No Ultra feature may become required for gameplay understanding.

## Foundation Demands

The dream is blocked by foundation gaps already identified in the research logs:

- `static_data.h8bin` must become a real production payload, not an optional missing boot artifact.
- ContentAuthority must generate or validate actual Unity object asset groups, hash maps, VFX
  manifests, and object-batch payloads. Addressables-style delivery is not the world/static-data
  truth path.
- First-hour scan/craft/repair route validation must become a build or preplay gate.
- Biome visual authority must be data, not scattered scene taste.
- Typed stimulus lanes must exist before creature reactivity is claimed.
- Black-box rings must cover pressure, atmosphere, organic destruction, world streaming, and AI.
- Platform tiers must own content budgets, not just shader keywords.

## Proof Limits

- This file is design/architecture direction, not runtime proof.
- No frame time, GC, memory, or visual quality is certified here.
- Public Subnautica 2 information came from official/press sources recorded in the dossier.
- No proprietary Subnautica 2 files, assets, binaries, or Unreal internals were inspected.
