# Rationale 2102

Agent ID: 2102
Date: 2026-06-04
Evidence class: STATIC_DOC

## Decisions

1. Primary source family is `Shallow sand/silt mix` with shell/calcite and reef-grit detail.
   - Reason: `2022_GEMINI_TEXTURE_BUDGET_AND_PROMPT_QUEUE.md` ranks `TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604` spend-first rank 3, and the visual locks require bright 0-100 m seabed readability.

2. Included `ShellGritCalcite`, `ReefCalciteFloor`, and `ShallowShelfSiltLines` optional prompt variants.
   - Reason: Q012/G014/G015 evidence requires shallow reef anchors, underwater shelves, and medium-depth route markers. These variants stay inside substrate ownership and do not create coral/flora or shoreline wet-basalt packages.

3. Kept `BasaltSedimentTransition` as CANDIDATE only.
   - Reason: submerged seabed contact is relevant, but shoreline wet/dry basalt and waterline foam/salt are adjacent owner scope.

4. Did not create generated image, derived PBR maps, Unity import instructions-as-proof, or material acceptance.
   - Reason: task forbids browser/Gemini work, Unity, import, material edits, and runtime proof. Static reports cannot be upgraded beyond STATIC VERIFIED.

5. Left MRAO G/A channel order as shader-locked pending owner confirmation.
   - Reason: 2005 texture contracts and texture bible warn that channel order differs by shader and must not be guessed from filenames.

## Scaling Consequence

Continuous `GlobalQualityWeight` may scale texture size, detail intensity, mask precision, decal density, and streaming residency. It cannot change material role semantics, shader channel order, gameplay route truth, collision identity, save identity, or proof state.
