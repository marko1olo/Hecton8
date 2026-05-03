# Procedural World Vertical Architecture

Status: `ACTIVE`
Verification: `PENDING VERIFICATION`

This file defines how HECTON-8 expands from flora into a full procedural asset pipeline without creating parallel runtime systems.

2026-05-02 current-state boundary:

- This document defines the vertical procedural architecture contract.
- It does not prove that every category below has production-ready prefabs, materials, scatter profiles, or runtime validation.
- Current conceptual project truth starts at `Docs/Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md` and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.
- Current scatter runtime remains owned by `WorldProceduralScatterDirector` and adjacent backend seams; no category may create a parallel scatter stack.
- Encoding-damaged geology production notes were moved to `Docs/DEPRECATED/Encoding_Damaged_2026-05-01/`; use `Docs/ARCHITECTURE/SEISMIC_GEOLOGY_SYSTEM.md` for readable geology/seismic reference.

## Goal

Use one shared world-fill architecture for:

- `ORGANIC` flora
- `GEOLOGICAL` rocks and formations
- `STRUCTURAL` ruins, modules, wreck segments
- `INTERIOR_DECOR` trims, conduits, clutter
- `COLONY_PARTS` large colony limbs, docking bays, habitat shells

The rule is simple:

- shared runtime selection and placement
- category-specific editor authoring
- one family/profile/rule/variant model
- no new scatter stack per category

## Current Owners

These systems already exist and must remain the backbone:

- `WorldProceduralProxyAuthoring`
- `WorldProceduralFinalVariantAuthoring`
- `WorldProceduralScatterDirector`
- `WorldPrefabFamilyProfile`
- `WorldProceduralPlacementRule`
- `BiomeMatrixBootstrapAuthoring`

Category-specific editor/runtime bridges already exist in partial form:

- flora:
  - `WorldProceduralFloraBakedStarterGenerator`
  - `WorldProceduralFloraFinalVariantAuthoring`
  - `WorldProceduralFloraTextureAuthoring`
  - `WorldProceduralFloraMaterialAuthoring`
  - `WorldProceduralFloraFinalVariantValidator`
- geology:
  - `HectonRockRuntimeBootstrapAuthoring`
  - `WorldGenerativeGeologyProfile`
  - `WorldGenerativeGeologyService`
  - `WorldGenerativeGeologyIntegrationDirector`
  - `WorldGenerativeGeologySeamExecutionDirector`

## Core Rule

Do not build:

- a standalone coral runtime
- a standalone rock scatter runtime
- a separate ruin scatter manager
- a second family/variant data model for interiors

Everything must remain compatible with:

- `WorldPrefabFamilyProfile.ProceduralDomain`
- `WorldPrefabFamilyProfile.ScatterLayer`
- `WorldPrefabFamilyProfile.PlacementMode`
- `WorldProceduralScatterDirector`

## Vertical Pattern

Every category must fit the same high-level pattern:

1. `Proxy Family Authoring`
2. `Final Variant Intake`
3. `Category Material/Texture Contract`
4. `LOD + Budget Validation`
5. `Runtime Bootstrap / GPUI Bridge if needed`
6. `Status Report`

The category decides authoring details. The world stack still decides:

- when the thing is eligible
- how often it appears
- which biome/pattern prefers it
- which streaming layer owns it

## Per-Category Ownership

### ORGANIC

Use the existing flora approach:

- family-specific starter generation
- imported texture-set lookup
- strict material contract
- exact LOD thresholds `0.6 / 0.15 / 0.04 / 0`
- GPUI/runtime scatter through `WorldProceduralScatterDirector`

### GEOLOGICAL

Use the current rock/geology split:

- `WorldProceduralProxyAuthoring` owns families/rules/patterns
- `WorldProceduralFinalVariantAuthoring` links authored finals where they exist
- `HectonRockRuntimeBootstrapAuthoring` owns rock GPUI runtime bootstrap
- `WorldGenerativeGeology*` owns large-form fallback generation and seam planning

Geology remains the template for:

- large landmark formations
- shelves
- arches
- cave-adjacent masses

### STRUCTURAL

Structural content must stay on the same family/rule/variant model as rocks and flora.

Structural editor ownership should converge to:

- family definitions in `WorldProceduralProxyAuthoring`
- final prefab linking in `WorldProceduralFinalVariantAuthoring`
- category validator/report for budget, baggage, shader/material contract

Structural runtime behavior must remain under:

- `WorldProceduralScatterDirector`
- existing construction/streaming layers

### INTERIOR_DECOR

Interior decor is not a new runtime system. It is a stricter structural sub-vertical.

Key differences:

- smaller culling range
- socket-driven or room-driven placement
- tighter renderer/material budgets
- stronger validation for duplicate clutter families and interior-only streaming

But the same family/rule/variant pipeline should still be used.

### COLONY_PARTS

Colony parts are the heavy structural landmark layer.

They should use:

- `ProceduralDomain.RuinModule` where possible
- future domain expansion only if existing domains become semantically insufficient

Do not add new domain enums casually. First try to express the content with:

- `RuinModule`
- `Landmark`
- `PowerRoute`
- `ServiceScar`
- `CaveEntrance`

## Recommended Future Splits

Future editor systems should be category helpers, not runtime forks.

Good future additions:

- `WorldProceduralGeologyFinalVariantValidator`
- `WorldProceduralStructureFinalVariantValidator`
- `WorldProceduralInteriorVariantValidator`
- `WorldProceduralStructureMaterialAuthoring`
- `WorldProceduralInteriorMaterialAuthoring`
- `WorldProceduralStructureStatusReport`

Bad future additions:

- `RockScatterDirector`
- `RuinScatterDirector`
- `InteriorScatterDirector`
- `ColonyRuntimeSpawner`

## Domain Expansion Rule

Before adding a new `ProceduralDomain`, answer:

1. Can this content be represented by an existing domain plus a better familyId?
2. Does the new domain change runtime behavior, or only art semantics?
3. Does scatter scoring actually need a new branch for this domain?

Only add a new domain if runtime placement or scoring truly changes.

## Validation Strategy

Every vertical should converge to the same fail-closed gates:

- family resolves cleanly
- variant identity is deterministic
- renderer/material stack is complete
- texture source is managed
- shader/material contract is correct
- LOD contract is correct
- budget stays inside category limits
- forbidden baggage is absent

## Texture Strategy

AI-generated texture sets are valid production input only after:

- manual import into the managed root
- automated importer fix or importer contract check
- validator/report proof

Do not make atlases early.

Atlas work becomes worth doing only when:

- multiple verticals have stable family coverage
- texture contracts are consistently clean
- shared material families are actually reused enough to reduce real SetPass pressure

## Immediate Expansion Order

After flora:

1. `GEOLOGY`
2. `STRUCTURAL`
3. `INTERIOR_DECOR`
4. `COLONY_PARTS`

Reason:

- geology already has live domains and bootstrap hooks
- structural already has live domains in family/rule authoring
- interior decor depends on structural family conventions
- colony parts should reuse structural validation and reporting instead of inventing their own path

## Final Rule

The future system is not "one generator for everything."

The future system is:

- one shared procedural world architecture
- multiple category-specific authoring/validation layers
- one runtime placement owner
- one deterministic family/variant contract

Anything else is architecture drift.
