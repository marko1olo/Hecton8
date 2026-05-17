# Subnautica 2 Screenshot Visual Cheats For HECTON-8

Date: 2026-05-17
Status: SOURCE-REFERENCE CHEAT SHEET / RUNTIME PENDING
Source dossier: `Docs/Reports/SUBNAUTICA_2_UE5_REFERENCE_DOSSIER.md`
Related authority: `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md`

## Purpose

This file translates six inspected official Steam screenshots for Subnautica 2 into HECTON-8
visual-fake tactics. It does not copy art direction. It extracts rendering and composition tricks
that can be implemented through deterministic, tiered presentation systems.

## Global Reading

The visible screenshot bar is not impossible tech. It is disciplined staging:

- strong foreground/cockpit/tool framing
- thick color fog by biome
- readable silhouettes
- bright local emissive accents
- stylized flora clusters instead of physically simulated ecology
- particles/bubbles/silt as density cues
- modular base forms with clean materials
- caustic/SSS-like lighting hints
- one dominant color story per image

HECTON-8 can compete if it treats these as authored contracts, not random post-processing.

## Screenshot-Derived Cheat Table

| Reference surface | HECTON-8 fake-first answer | Low | Middle | High | Ultra |
|---|---|---:|---:|---:|---:|
| Blue/yellow base exterior haze | Biome fog LUT + local emissive anchors | 1D LUT, dither fog | fog volume records | local light shafts | raymarch shafts + silt |
| Clean base interior | Modular material discipline + readable function lights | baked AO, no SSR | reflection probes | wetness accents | SSR/SSDO gated |
| Co-op shallow biome density | Clustered flora islands + silhouette arches | billboards, impostors | object batches | sway/biolum layers | dense Overkill flora |
| Dark scanner creature frame | Tool foreground + flashlight cone + particles | cone mask, sparse particles | scan pulse | sonar silhouette | volumetric scan/silt |
| Deep blue vehicle frame | Cockpit framing + caustic floor accents | projected caustics | animated caustic sheet | reactive particles | caustic volume |
| Orange thermal biome | Monochrome danger palette + bubble sheets | color grade + sheets | heat haze mask | vent plume layers | volumetric plume |

## HECTON-8 Style Translation

Subnautica 2 uses wonder. HECTON-8 should use mechanical dread.

Replace:

- bright coral clusters -> industrial debris, pale biolum crust, cable forests, pressure-bent metal
- friendly blue haze -> contaminated blue/green/black water bands
- clean vehicle adventure -> fragile cockpit instrument survival
- colorful thermal fantasy -> oxidized orange hazard, chemical bloom, vent ash
- creature spectacle -> acoustic warning, partial silhouette, silt displacement

## Cheap Effects That Must Carry Low Tier

These are mandatory before any expensive render path is justified:

- 1D depth fog LUT per biome
- fixed particle sheets for bubbles and marine snow
- triangle-noise silt masks
- projected caustic decals
- billboard/impostor flora clusters
- baked vertex-color wetness and AO
- emissive masks for instruments, vents, and creature hints
- screen-space visor grime masks
- audio/haptic pulses for pressure and threat

Low tier must still look intentional. It can be sparse, but it cannot look unstyled.

## High-Tier Visual Overkill Targets

High and Ultra should spend saved logic cycles on visible signatures:

- salt crystals and condensation growing on visor masks
- volumetric silt in vehicle wake and creature movement
- procedural hull dent overlays from pressure/damage state
- abyssal noir light shafts
- high-sample POM only on hero near-field metal/rock
- secondary flora sway driven by flow fields
- reactive biolum pulses on fauna and wreck growth
- richer scan holograms and cockpit distortion

These must be presentation consumers. They may not change gameplay truth.

## Required Data Contracts

Every biome needs a compact visual authority record:

- biome id
- fog LUT id
- fog density low/mid/high/ultra
- silt density
- caustic strength
- dominant color band
- acoustic profile id
- flora object-batch budget
- wreck/debris object-batch budget
- particle sheet budget
- high-tier overkill flags

Every visual overkill feature must read from existing state:

- pressure scalar
- hull stress scalar
- threat stimulus
- vehicle velocity / propwash
- local biome visual record
- damage state
- scan pulse state

No overkill system gets private gameplay truth.

## Build Gates Needed

P0:

- Biome visual authority asset or monolith section exists.
- Low/Middle/High/Ultra budget table exists for each biome.
- Object-batch payloads exist for repeated flora/debris/wreck dressing.
- VFX prewarm manifest covers particles/visor/scan/pressure effects.

P1:

- Screenshot route validation: at least one controlled capture per first-hour route beat.
- Platform matrix validation: MX350, Steam Deck storage profile, High PC, and Quest/Android compatibility where applicable.
- Comfort settings validation: FOV, camera shake, visor effects, text/subtitle scale.

P2:

- Ultra-only overkill pack is isolated from low-tier builds.
- Frame Debugger/RenderDoc evidence records SetPass, batches, overdraw, and raymarch pass cost.

## Rejected Paths

- Full volumetric truth on low hardware.
- Per-bubble or per-flora-blade simulation.
- Dynamic light spam as a substitute for composition.
- Copying Subnautica 2 color palettes directly.
- Using high-tier effects to hide missing first-hour route proof.
- Shipping one "balanced" visual profile instead of tiered contracts.

## Proof Limits

- Based on official still screenshots, not live frame captures.
- No Subnautica 2 assets or Unreal internals were inspected.
- No HECTON-8 visual implementation was changed by this file.
- All runtime performance claims remain pending profiler, Memory Profiler, Frame Debugger, and platform proof.
