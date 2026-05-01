# HECTON8 Sargassum Reality Audit And Execution Plan

Status: `ACTIVE`
Verification: `PENDING VERIFICATION`
Date: `2026-04-20`
Source prompt: `Docs/DEPRECATED/External_And_Log_Bundles/САРГАСОВЫ ШТУКИ/САРГАСОВЫ ВОДОРОСЛИ.txt`

## Purpose

Convert the source sargassum concept dump into an architecture-safe execution track for HECTON-8.

This document is not a fantasy brief. It is a reality filter:

- what can be absorbed into the current project owner stack
- what is performance-toxic on MX350
- what improves beauty
- what improves gameplay readability
- what is likely to regress CPU, GC, memory, or content ownership
- what should be implemented first without creating another broken subsystem

## Authority Order

1. `AGENTS.md`
2. `Docs/README.md`
3. `Docs/PROCEDURAL_ASSET_PIPELINE.md`
4. `Docs/Flora_Pipeline/AI_FLORA_EXECUTION_BRIEF.md`
5. `Docs/Flora_Pipeline/FLORA_SYSTEM_PLAN.md`
6. `Docs/Scatter_Runtime/README.md`
7. `Docs/2026-04-20_Sargassum_Reality_Audit/HECTON8_SARGASSUM_REALITY_AUDIT_AND_EXECUTION_PLAN.md`

## Current Codebase Truth

The project already has real owners for most of the requested feature space:

- runtime scatter owner: `WorldProceduralScatterDirector`
- terrain/world bridge owner: `MapMagicBridge`
- fluid/current owner: `HectonFluidEngine`
- localized current authoring: `CurrentVolume`
- audio playback owner: `SpatialAudioManager`
- flora authoring owner stack:
  - `WorldProceduralSeaweedMeshBuilder`
  - `WorldProceduralFloraBakedStarterGenerator`
  - `WorldProceduralFloraFinalVariantAuthoring`
  - `WorldProceduralFloraMaterialAuthoring`
  - `WorldProceduralFloraTextureAuthoring`
  - `WorldProceduralFloraFinalBudgetCatalog`
- existing supported canopy family: `family.kelp.canopy`

Hard fact:

- `family.kelp.canopy` already exists as a structure-layer kelp family with scatter rule, final prefabs, warmup reserve, material support, and biome bootstrap usage.

Conclusion:

- do not create `SAR_GASSUM_ULTRA` as a standalone runtime system
- do not create a second flora placement framework
- do not write custom Crest runtime wrappers
- first absorb sargassum into the existing canopy kelp owner stack

## Source Concept Breakdown

The source file contains six different asks mixed together:

1. silhouette and morphology
2. distribution logic
3. interaction and drag
4. lighting and shading
5. ecology dressing
6. giant runtime architecture claims

Those must be separated. Mixed delivery is what creates dead systems.

## Reality Audit

### Keep

- floating canopy / raft / windrow silhouette direction
- golden-brown sun-cooked color direction
- denser nodes plus thinner connective strands
- corridor gameplay instead of full carpet fill
- stronger under-canopy read from below
- local drag and route obstruction as gameplay signal
- ecology dressing as optional low-cost garnish

### Keep With Existing Owner Only

- biome-driven placement through `WorldProceduralScatterDirector`
- kelp family routing through `family.kelp.canopy`
- material tuning through `WorldProceduralFloraMaterialAuthoring`
- final prefab registration through `WorldProceduralFloraFinalVariantAuthoring`
- local current influence through `CurrentVolume` and `HectonFluidEngine`
- audio entry/exit cues through `SpatialAudioManager`

### Reject For Now

- runtime stochastic L-system mesh generation
- runtime metaballs clustering
- runtime cellular automata for live mass redistribution
- render-texture driven grass split buffer
- giant custom indirect-renderer stack parallel to current scatter pipeline
- bespoke Crest coupling layer
- micro-fauna AI attached to every canopy cluster

Reason:

- wrong owner
- wrong budget target
- wrong verification state
- high regression risk

## Performance Audit

### Likely To Improve Performance

- sparse labyrinth distribution instead of full radial fill
- thinner bridge shapes with open water windows
- fewer center-of-cell instances
- reusing existing GPUI / scatter / baked final asset flow
- stronger silhouette per instance instead of brute-force density
- localized current volumes instead of per-instance physics reactions

### Likely To Hurt Performance

- runtime mesh generation for every patch
- live push-apart simulation for every kelp element
- per-cluster RT interaction mask
- heavy transparency and depth-fade abuse
- spawning fauna particles and decals per cluster without pooling strategy
- one-off materials for special islands
- custom indirect rendering path that bypasses current scatter/bootstrap

### Likely To Cause Problems First

- architecture drift from "one more runtime system"
- canopy visuals staying bright green instead of sargassum brown
- too much density producing flat walls instead of corridors
- too much drag making traversal miserable
- too many high-triangle canopy finals dominating near-field cost
- weak validation if new prefabs are added but not relinked into family variants

## Visual Audit

### What Makes It More Beautiful

- warmer olive, amber, and dried-gold canopy tones
- broader hanging sheets mixed with braided tangles
- more readable underside silhouette
- more asymmetry at canopy edges
- node-to-bridge contrast instead of uniform mass
- strong top silhouette from surface angle and strong underside read from below

### What Makes It Worse

- uniform green kelp look
- identical repeated crowns
- full opaque wall mass with no route windows
- transparent shader hacks
- fake hero complexity built from huge triangle counts

## Gameplay Audit

### Good Direction

- corridors and windows for scooter routing
- dense knots as risk/reward pockets
- local drag as punish-on-mistake, not permanent slowdown
- surface-near canopy as navigation landmark

### Bad Direction

- endless sticky carpet
- always-on heavy drag
- visual density with no route signal
- interaction logic that requires special-case physics on every instance

## Architecture Decision

### Final Direction For This Pass

Sargassum is treated as a `family.kelp.canopy` extension, not a new runtime flora platform.

Implementation shape:

- new canopy baked variants with sargassum-like raft / windrow / tangle silhouettes
- canopy material palette shift toward warmer dried-sun tones
- future scatter tuning through existing rule/family/biome authoring
- future local drag through `CurrentVolume` or existing fluid hooks, not per-instance physics

### Explicit Rejections

- no `SargassumRenderer`
- no `LangmuirWebGenerator` runtime subsystem
- no custom Crest material instancing
- no procedural mesh generation in play mode
- no RT split buffer until there is hard evidence the existing path cannot solve the gameplay need

## Execution Plan

### Phase 0: Documentation Gate

- create this audit bundle
- track all implementation steps in sibling `CHANGELOG.md`
- keep all claims marked `PENDING VERIFICATION`

### Phase 1: Visual Silhouette Upgrade

- extend existing `family.kelp.canopy` authoring with new sargassum-leaning variant roots
- keep within existing family budget
- regenerate baked starters
- relink family variants through existing flora final authoring

Acceptance:

- new canopy prefabs exist in the baked family folder
- family variants link without manual yaml hacking
- no compilation errors

### Phase 2: Material Direction Correction

- shift canopy family default material tuning from bright green toward olive/amber/dried-gold
- preserve shader contract and quality keywords
- do not create a separate sargassum shader

Acceptance:

- canopy family material still validates under current flora contract
- no shader errors

### Phase 3: Distribution Logic Tuning

- inspect current canopy family placement density in existing biome/rule stack
- tune spacing, counts, and weighting only through existing family/rule/biome inputs
- bias for sparse route silhouettes, not flat carpets

Acceptance:

- runtime scatter still uses existing owner path
- no raw runtime instantiate fallback

### Phase 4: Local Drag And Route Pressure

- evaluate `CurrentVolume` and `HectonFluidEngine` integration for zone-based drag
- if needed, author canopy-adjacent localized current/drag volumes
- do not attach per-instance runtime scripts to every flora object

Acceptance:

- drag effect is zone-based and owner-driven
- no hot-path allocations

### Phase 5: Audio Layer

- add canopy-entry ambience only through `SpatialAudioManager` and existing audio routing
- no string-event system
- no new audio subsystem

Acceptance:

- one-shot or loop routing uses existing mixer groups

### Phase 6: Ecology Dressing

- add only if scatter and perf remain healthy
- prefer low-cost pooled particles or sparse decorative anchors
- no free-swimming per-cluster AI swarm on first pass

## Immediate Work Order

1. complete docs bundle
2. add new canopy variant definitions to the editor authoring path
3. tune canopy material defaults toward sargassum color space
4. attempt Unity authoring regeneration using existing menu flows
5. inspect console
6. update changelog

## First Slice Scope

This session is allowed to do:

- docs bundle creation
- editor-only canopy variant additions
- canopy material default tuning
- Unity authoring run if tooling is available

This session is not allowed to do:

- new runtime scatter subsystem
- new render backend
- play-mode live physics carpet simulation

## Risk Matrix

### CPU Risk

- low for editor-only mesh authoring changes
- medium if canopy distribution later increases final-ready counts too aggressively
- high for any attempt at live interaction physics per instance

### GC Risk

- low for current editor-only slice
- high if future drag/audio logic uses ad-hoc per-frame searches or allocations

### Memory Risk

- medium if canopy family receives too many high triangle variants
- high if new textures or materials fork per variant

### Correctness Risk

- medium if baked starters are added but final variant relink step is skipped
- medium if family assets and generated prefabs drift apart

### Visual Risk

- medium because canopy family is shared and palette change may alter existing shallow-biome reads

## Regression Model

CPU:
- editor-only now
- future runtime risk comes from density and drag logic, not from mesh authoring

GC:
- no intended runtime GC change in this pass

Memory:
- more canopy variants increase asset inventory, but runtime memory should stay controlled if family links remain owner-driven and density is not increased blindly

Cadence:
- first slice improves asset variety first
- interaction and ecology stay deferred until evidence exists

Correctness:
- family asset, baked prefabs, and validator output must stay aligned

Why kept:
- existing owner stack already supports canopy flora and is the only sane insertion point

Why rejected:
- the source txt mixes too many expensive systems with no proof and no owner discipline

## Verification Protocol

Required after authoring edits:

1. compile project
2. run:
   - `Hecton/Authoring/Generate Procedural Flora Baked Starters`
   - `Hecton/Authoring/Apply Procedural Flora Final Variants`
3. inspect Unity console for compile or authoring errors
4. if validator path is available, run flora validation
5. do not claim success without Unity evidence

## Current Session Decision

Proceed with `family.kelp.canopy` extension.

Do not build `SAR_GASSUM_ULTRA`.

Status remains `PENDING VERIFICATION`.
