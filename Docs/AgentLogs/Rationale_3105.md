# Rationale 3105 - AEGIR_SKY_CELESTIAL_OWNER

Date: 2026-06-05
Evidence: STATIC_SOURCE / STATIC_YAML / STATIC_DOC

## Decision

Keep `Mat_HectonSky` as the active sky and primary sun-disc route. Do not enable the old flat mesh sun. Do not darken/fog the surface as a fallback.

## Basis

- `02_HECTON_WORLD.unity` and `00_BOOTSTRAP.unity` serialize `Mat_HectonSky` as skybox material.
- `HectonUnderwaterVisuals` and `HectonCelestialEngine` both reference the same `Mat_HectonSky` GUID.
- `Hecton_AlienSky_Master.shader` declares `_MainCloudTex`, `_StarTwinkleLUT`, `_BakedStarCubemap`, sun disc properties, and Aegir halo/lensing properties.
- Static shader search did not find `_HighCloudTex` or `_MainCloudAtlas` declarations. Binding those stale rows by guess would be false proof.
- `Sky/oblaka!.png` is already used by cloud overlay and surface cloud deck materials, making it the only high-confidence candidate for `_MainCloudTex`.
- Source routes already hide mesh sun when the atmosphere/sky route owns the primary sun disc.

## Rejected

- Raw YAML material edits. Unity readback is required before asset binding.
- Binding `_HighCloudTex` or `_MainCloudAtlas` from static guesses.
- Enabling `SURFACE_LOW_SUN_DISC_1428`; its material route is flat/untextured and would duplicate primary sun ownership.
- Noir/dark/fog fallback for normal surface sky/Aegir.

## Regression Model

CPU: no runtime code changed. Future Unity binding has no CPU cost except texture residency/import.

GC: no hot-path allocation introduced. Future proof capture must stay editor/capture-only.

Memory/VRAM: no texture import or residency changed. Future `_MainCloudTex` binding uses existing 2048x2048 source; import/compression/residency still needs Unity proof.

Cadence: no update cadence changed. Continuous `GlobalQualityWeight` route remains required for richer cloud/Aegir presentation; no binary quality switch accepted.

Correctness: one skybox owner and one primary sun owner preserved. Stale serialized material rows remain unresolved until Unity material readback.

## Proof Boundary

Static route review is complete. Visual quality remains `PENDING VERIFICATION` until Unity material slot readback, clean console, screenshot packet, and texture residency proof exist.
