# Subnautica 2 To HECTON-8 Tactical Backlog

Date: 2026-05-17

Status: ARCHITECTURE BACKLOG / RUNTIME PENDING

## Purpose

This file turns the Subnautica 2 research pass into concrete HECTON-8 architecture work.

It is not an order to copy Subnautica 2. It is a pressure test against the HECTON-8 foundation.

## P0 Foundation Backlog

### P0-1 Static Data Monolith Becomes Mandatory

Problem:

`Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` exists in the current X_012 scan; route-specific boot proof remains pending. Boot code must reject a missing or invalid production arena. `Data/Balance/Baked/H8StaticData.bin` exists, but it is a different contract.

Required result:

- One production static-data payload path.

- Freshness validation before build.

- `Items.csv`, `Fauna.csv`, `Economy.csv`, and `Physics.csv` reconciled with the monolith `hash32` schema.

- Missing production monolith becomes a build failure outside explicit development mode.

Why Subnautica 2 matters:

Early Access cadence requires stable, patchable data. Screenshots do not matter if content payloads

cannot be generated and validated repeatedly.

### P0-2 ContentAuthority Payload Generation

Problem:

Addressables package is installed, but `Assets/AddressableAssetsData` is empty and expected

Core/High_Res/Overkill Unity object payloads are not populated. This is separate from

DataMonolith/world-static truth.

Required result:

- Generated or manually authored Addressables-style settings/groups for Core, High_Res, and

  Overkill object/visual/audio assets only where that delivery path is deliberately chosen.

- `ContentAssetHashMap` assets for Unity object asset lookup.

- `ContentVfxPrewarmManifest` coverage.

- Object-batch payloads for flora/debris/wreck dressing.

- `static_data.h8bin` and sector payload manifests remain the authority for static tables and

  baked world cache data.

- Build gate proves dependencies, tier membership, and missing-reference state.

Why Subnautica 2 matters:

Its visible density is content-pipeline discipline, not magic UE5 rendering. HECTON-8 needs

repeatable payload authority before chasing density.

### P0-3 First-Hour Route Gate

Problem:

Scanner/recipe/content validators exist, but first-hour route proof is not a hard production gate.

Required result:

- Build or preplay validation for scan -> salvage -> repair -> craft -> safe return route.

- Required scan entries and recipe gates represented as data.

- Missing critical route records block production build or fail CI.

- One controlled first-hour capture route becomes a recurring validation target.

Why Subnautica 2 matters:

Public impressions point to a strong first-ten-hours loop. HECTON-8 must prove its loop early:

pressure, acoustic threat, wreck salvage, repair, and partial safety.

### P0-4 Biome Visual Authority

Problem:

Biome look cannot remain scattered scene taste.

Required result:

- Compact biome visual records: fog LUT, fog density, silt, caustics, dominant color, acoustic profile,

  object-batch budgets, particle budgets, overkill flags.

- Records live in DataMonolith or a ContentAuthority-governed asset.

- Low/Middle/High/Ultra budgets are explicit.

Why Subnautica 2 matters:

Its screenshots are biome-color discipline. HECTON-8 must answer with noir biome contracts.

### P0-5 Black-Box Coverage For Critical Systems

Problem:

Some critical systems have local native ownership and incomplete 300-frame black-box evidence.

Required result:

- Pressure/atmosphere, organic destruction, world streaming, AI/threat stimulus, and vegetation/path

  systems record last 300 frames of high-level state.

- Dumps are bounded, binary, and linked to telemetry hashes.

- NaN/failure paths write useful state before recovery or shutdown.

Why Subnautica 2 matters:

Early Access feedback cadence only works if crashes and content failures are reproducible.

### P0-6 Comfort And Trust Settings

Problem:

Public reception coverage includes comfort/settings friction such as FOV complaints. HECTON-8

should not leave obvious trust holes.

Required result:

- FOV control.

- Camera shake strength.

- Visor effect strength.

- Subtitle/text scale.

- Audio category sliders.

- Input remap or input abstraction route.

- Accessibility-safe defaults for high-frequency visual distortion.

Why Subnautica 2 matters:

Comfort gaps are avoidable negative-review fuel.

## P1 System Backlog

### P1-1 Typed Creature Stimulus Lanes

Required result:

- Typed lanes for light, sound, impact, blood/biological trace, hull stress, power draw, and scan ping.

- Payloads are unmanaged, layout-stable, finite, and bounded per frame.

- Consumers read snapshots, not managed delegates or concrete cross-domain callbacks.

Low:

- Cheap utility scores and animation states.

Middle:

- Deterministic threat snapshots and black-box heartbeat.

High:

- Reactive fauna presentation layers.

Ultra:

- Secondary body/tentacle motion and silt reactions as presentation only.

### P1-2 Object-Batch World Dressing Payloads

Required result:

- Sector payload type for static biome dressing.

- Concrete object-batch assets or monolith records for flora, debris, cables, wreck fragments, and

  acoustic props.

- BRG/GPU-instancing upload path gated by platform tier.

- No per-object GameObject spawn storm for static dressing.

### P1-3 Save And Schema Migration Harness

Required result:

- Versioned schema validation for static data and save deltas.

- Migration test cases for content update scenarios.

- World operation audit trail prepared for possible future shared-world features.

### P1-4 Platform Preset Matrix

Required result:

- Minimum-budget: MX350/i3 and Steam Deck microSD I/O profile.
- Mobile/Quest/Android: alignment, texture format, shader/thread-group compatibility.

- Mac/Metal: compute limits and shader compatibility.

- High/Ultra PC: isolated Overkill packs.

No single middle profile is acceptable.

## P2 Differentiation Backlog

### P2-1 Overkill Visual Pack

Required result:

- Visor salt/condensation.

- Volumetric silt wake.

- Procedural hull dents.

- Abyssal noir light shafts.

- Hero POM/raymarch materials.

- Dense flora sway and biolum pulses.

Rules:

- Overkill pack is optional and tier-isolated.

- It reads existing gameplay state.

- It cannot alter gameplay truth.

### P2-2 Feedback Ingestion Loop

Required result:

- Bug reports map to telemetry/dumps/content hashes.

- Repro route captures are kept with build number and data hash.

- Community feedback becomes either a data task, comfort task, route task, or verified non-action.

### P2-3 Co-op-Ready State Boundaries

Required result:

- Even if co-op is not shipped, world operations should be permissionable and auditable.

- Character state, world state, base edits, and inventory transfers must not be irreversibly tangled.

- Future shared-world support should not require save-system demolition.

## Rejected Work

- Copying Subnautica 2 assets, binaries, art, names, story, or Unreal internals.

- Replacing HECTON-8 DOD architecture with UE5-style feature imitation.

- Shipping co-op before singleplayer persistence, content authority, and telemetry are stable.

- Treating screenshots as the main threat.

- Treating high-fidelity visuals as a substitute for first-hour route proof.

## Verification Model

Every backlog item must eventually produce:

- source evidence

- build or validation gate

- platform tier behavior

- failure modes

- no fake microsecond claim

- profiler/Unity/player proof when runtime readiness is asserted

Documentation-only status:

- CPU: no runtime change

- GC: no runtime change

- memory: no runtime change

- cadence: no runtime change

- correctness: architectural backlog only
