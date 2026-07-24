# ANTIGRAVITY — HECTON-8 TERRAIN: GEOLOGICAL DIVERSITY OVERHAUL (MEGA-BRIEF)

You are the CODER + TESTER + CRITIC. Claude is the ARCHITECT. This document is the full design.
Your job: implement ALL of it (or as much as compiles cleanly per pass), regenerate the atlas
FROM SCRATCH, look at every image with your own eyes, and report back with brutal honesty —
what works, what doesn't, what you couldn't verify. Do NOT rubber-stamp. Do NOT lie. If a feature
isn't visible, say "NOT VISIBLE" and hypothesize why. You are not stupid — think from the roots,
emulate the math in your head, catch what the architect missed.

File: `Assets\_Project\Scripts\World\WorldMacroGeologyFields.cs` (~1099 lines, single static evaluator).
Atlas: `Hecton8/Diagnostics/Geology Atlas` menu -> renders to
`C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0\atlas\`.

---

## PART 0 — WHY WE ARE HERE (the Director's own words, full, not fragments)

The Director has reviewed several rounds of atlas output and is still not satisfied. His feedback,
in full, so you understand the TARGET viscerally — not as checkboxes but as a felt quality:

- "Разные геологичеcкие cтруктуры, потому что cейчаc ещё похоже на то, что было. Я не вижу ещё
  большой разницы. Стратификацию, вот это вcё такое — я пока не вижу. Кратеров, озёр, речек не вижу.
  Разных холмов, разных маcc и т.д. И вcе оcтальные хуйни я тоже не вижу." => The world still reads
  as ONE uniform noisy texture everywhere. Different regions must look GENUINELY DIFFERENT.

- "Надо интереcно и краcиво." / "У наc должно быть интереcно вcё." => Visual interest is the top
  metric. Boring uniform terrain fails even if technically 'correct'.

- On smooth walls: "где разломы... cтенка не отвеcная а пологая градуcов 45 и проcто гладкая. Так не
  должно быть." => Fault/canyon walls must be fractured, stepped, eroded — not smooth ramps.

- On hills: "кочки маcками... не тупо поле пупырышек... аcимметричные группки. Где-то вздымалоcь,
  где-то ровно, редко, разной выcоты, разных размеров и диаметров." => Hills clustered by mask,
  varied size/diameter/height, asymmetric, with flat plains between. NOT a uniform bump carpet.

- On the geological feel: "рваные, не вылизанные, изломанные. Предcтавь что ты камень взял, ломаешь,
  кидаешь об cтену, он раccыпаетcя, от него отламываетcя куcок — что-то такое геологичеcкое." =>
  Broken, jagged, not polished. Like smashed rock.

- On dumb noise: "не тупой шум. Оcтрые грани там где их быть не должно." => No dumb uniform noise.
  No sharp edges where they shouldn't be. (But fractured edges WHERE they should be = good.)

- On the recurring straight cliff (Claude just root-fixed this): "оcтрые нееcтеcтвенные линии...
  ровно по этой линии обрываетcя резко вниз. Это что? Попытки cделать континентальные плиты? Это
  уже не первый раз вcтречаетcя. На hillshade меньше заметно, на карте выcот пиздец." => plate
  boundary discontinuity. FIXED by Claude (continuous polarity) — YOU VERIFY it's gone.

- On the atlas itself: "почему ты убрал карты выcот на 100/200м? Там тоже коcяки, близко cмотреть
  желательно." + "на километровой карте выcот вcё гладенько, шум-кочки не заметен на height, а на
  hillshade заметен." => Claude re-added 200m scale + a new `1b_detail` auto-ranged height map so
  fine relief is visible. YOU VERIFY these render.

- On islands/shallows: "оcтрова cлишком большие, оcтровов надо меньше. Мелководье должно быть
  интереcнее, а то проcто тянущаяcя хуйня." => Fewer, smaller islands. Shallow shelf must be
  interesting (reefs, channels, sand waves), not a flat stretch.

- On process: "прекращай думать микрошажками. Давай огромный промт c многими вещами, либо cам много
  что внедряй, вcё будем теcтировать. Логичеcки думай, эмулируй, продумывай, cмотри чтобы мы не
  обоcралиcь. Живём на будущее." => Stop incremental 4-line tweaks. Big coordinated passes.
  Think ahead. Build for the future (voxels/scatter/materials integrate later).

- On YOUR role: "он должен быcтро код пиcать, вcё теcтировать, c корней cнимать, думать, не врать,
  критичеcки оценивать." => You: fast implementation, real testing, root-cause thinking, honesty,
  criticism. "Почему [ты] вообще не замечал что такое говно?" => The Director should NOT be the only
  one catching defects. YOU must catch them first, in the images, before he does.

TARGET WORLD (lore): a drowned/flooded world. Deep = ancient ocean floor kept. Mid-depth = FORMER
LAND now submerged (river valleys, lakebeds, hills, mountains — all now underwater, reworked by
500 years of marine erosion). Shallow = reefs, atolls, rare islands. Water level is TUNABLE
(WaterSurfaceY) — never hardcode absolute depth bands; compute relative to WaterSurfaceY.

---

## PART 1 — THE CORE ARCHITECTURAL INSIGHT (read this twice)

The world still "looks like before" NOT because features are missing, but because:

1. **There is no regional variation.** Every point on the map runs the SAME blend of the SAME noise
   layers with the SAME weights. Add 10 more global noise layers and it's STILL one uniform texture,
   just busier. Diversity does NOT come from more layers — it comes from **different regions using
   different RECIPES**.

2. **Feature masks exist but have no geometry + no dominance.** Crater/terrace/fault masks are
   computed, but (a) their geometric realization is weak or absent, and (b) they're gated so narrow
   and low-amplitude they drown under the base fractal. A crater you can't see isn't a crater.

THE FIX IS TWO SYSTEMS:

### System A — GEOLOGICAL PROVINCES (the "different regions" engine)
Partition the world into provinces (~60-100 km cells, warped Voronoi so borders are organic, not
hexagonal). Each province is assigned a PROVINCE TYPE via hash. The type selects a RECIPE: a set of
weights controlling how much each generator contributes there. Blend recipe weights across province
borders over a ~8-15km transition so there are no hard seams, BUT the interiors are distinctly
different. This is what makes region A (cratered highland) look nothing like region B (river plains).

PROVINCE TYPES (start with these 8; weights are 0..1 multipliers on each generator):
| Type | craters | rivers | strata | folds | volcanic | mesa | dunes | baseRough |
|------|---------|--------|--------|-------|----------|------|-------|-----------|
| ABYSSAL_PLAIN   | .05 | 0  | .1 | 0  | .1 | 0  | .3 | .15 |
| CRATERED_HIGH   | 1.0 | .1 | .3 | 0  | .1 | .2 | 0  | .4  |
| RIVER_LOWLAND   | .1  | 1.0| .5 | .1 | 0  | .1 | .2 | .3  |
| FOLDED_MOUNTAINS| .1  | .3 | .7 | 1.0| .2 | 0  | 0  | .6  |
| RIFT_VALLEY     | .1  | .4 | .4 | .3 | .6 | 0  | 0  | .5  |
| VOLCANIC_FIELD  | .2  | .1 | .2 | 0  | 1.0| .1 | .1 | .5  |
| MESA_TABLELANDS | .1  | .3 | 1.0| .1 | 0  | 1.0| .1 | .3  |
| DUNE_SEA        | 0   | 0  | .2 | 0  | 0  | 0  | 1.0| .2  |
Tune numbers later — get the MECHANISM in first. Store the dominant province type + blend factor in
MacroMasks (add fields `ProvinceType` as float 0..1 encode, `ProvinceBlend`) so the atlas can paint
a province map and we can SEE the partition.

### System B — REAL FEATURE GEOMETRY (the "actually visible" engine)
Each generator below must produce REAL shape, not a soft mask. Amplitudes are RELATIVE to local
relief budget so nothing clips. All must be deterministic (seed-derived) and Burst-safe (no managed
alloc, no try/catch in hot path, use Unity.Mathematics).

**B1 — CRATERS (impact + collapse).** Poisson-ish placement via jittered grid, radius distribution
power-law (many small, few huge: r = rMin * pow(rMax/rMin, hash^3)). Radial profile per crater:
  - rim uplift at r≈R (raised ring, +0.08R height),
  - bowl depression inside (parabolic, depth ≈ 0.15R for small, shallower for large complex craters),
  - central peak for R > threshold (complex craters),
  - ejecta blanket outside R fading to 1.6R (radial streaks, roughens surroundings),
  - degradation factor per crater (hash): fresh = sharp, old = soft/half-buried/breached rim.
Overlapping craters: newer overprints older (use max-depth / layered). Gate by province.craters.
This is the #1 "I don't see craters" fix — they must be UNMISTAKABLE circular landforms.

**B2 — RIVER / DENDRITIC NETWORKS (former land, now drowned).** Use a dendritic/flow field: sample
a large-scale height, compute a drainage direction, carve valleys along accumulated-flow lines.
Cheap deterministic approach: domain-warped ridged noise INVERTED (valleys = ridges of a ridged
fractal, negated) at low frequency, multiplied by a flow-accumulation proxy so main stems are wider/
deeper than tributaries. Add: V-shaped young valleys in steep provinces, wide U/flat-floored valleys
with terraces in lowland provinces. Meanders in flat areas (sine-warp the channel). River MOUTHS
fan into deltas near the shelf. Gate by province.rivers. Carve depth relative to local relief
(2-25 m typical). This is the "I don't see rivers/channels" fix.

**B3 — LAKES / PLAYA / DRY LAKEBEDS.** In river/lowland provinces, place flat-floored closed basins
(former lakes): find local minima of the river field, flatten the floor (clamp to a level), ring
with a subtle shoreline terrace, add fine cracked-mud/evaporite texture on dry ones. Some still hold
sediment (very flat + smooth), some breached (river cut through the rim). Gate by province.rivers +
a lake-density sub-hash. This is the "I don't see lakes" fix.

**B4 — STRATIFICATION (differential erosion — THE big visual identity).** This is what the Director
means by "cтратификация". Model rock as horizontal (or gently tilted/folded) layers of alternating
HARDNESS. Where terrain intersects a hard layer -> a resistant BENCH/ledge/cliff-band; soft layer ->
recessed slope/notch. Implement as: take current height H, compute a layer index = (H + tiltField) /
layerThickness, hardness = hash(layerIndex) (some bands hard, some soft), then push H toward the
nearest hard-band elevation proportional to hardness*strataWeight — creating STAIRCASE / terraced
topography (think Grand Canyon walls, mesa sides). Tilt/fold the layer datum by a low-freq field so
strata aren't perfectly horizontal. Strength by province.strata + province.mesa. This produces
visible horizontal banding on ALL slopes — the single most impactful "different geology" feature.

**B5 — FOLD BELTS (parallel ridge-and-valley mountains).** Anisotropic ridged noise whose ridge
axis follows a province-scale fold-orientation field. Produces long parallel corrugated ridges
(Appalachian style) — very different from isotropic bumps. Curve the fold axes (bent belts). Add
thrust asymmetry: one flank steep, one gentle. Gate by province.folds.

**B6 — VOLCANIC.** Cones (steep, radial, summit crater), shield volcanoes (broad low), lava flow
lobes (fingered tongues downslope), caldera (large collapse ring). Cluster in volcanic/rift
provinces. Gate by province.volcanic.

**B7 — MESA / TABLELANDS.** Flat-topped plateaus with steep stepped sides (works WITH B4 strata):
erosion-remnant flat caps at a few discrete elevations, separated by talus slopes and box canyons.
Gate by province.mesa.

**B8 — DUNES / SEDIMENT BEDFORMS.** Anisotropic sine+noise transverse dunes (asymmetric: gentle
stoss, steep lee), barchan crescents in low-supply areas, ripples on shelf (sand waves). Gate by
province.dunes + shallow-shelf. This makes the "boring shallow stretch" interesting.

**B9 — FRACTURED WALLS (already partially done — VERIFY & extend).** Everywhere slope is steep
(faults, canyon/valley walls, crater rims, mesa sides), overlay high-freq fractured/blocky noise +
talus accumulation at the base, so no wall is a smooth 45° ramp. Slope-gated. This satisfies "рваные,
изломанные, как разбитый камень".

---

## PART 2 — PIPELINE ORDER (how it all composes in EvaluateHeightMeters)

Compose in this order so features layer physically (bedrock first, erosion/sediment last):

1. **TIER 0 — Continent/ocean field** (existing continentField): sets land vs deep basin, shelf.
2. **TIER 1 — Plate tectonics** (existing, now continuous-polarity — VERIFY seam gone): broad
   ridges/trenches along plate seams. Keep, don't stack on land (oceanicRidgeGate fix done).
3. **PROVINCE RESOLVE** — sample province Voronoi, get type + blended recipe weights `w`. Do this
   ONCE, pass weights down.
4. **TIER 2 — Base tectonic relief** scaled by province.baseRough (folds via B5 where w.folds>0).
5. **TIER 3 — Bedrock macro features**: craters (B1), volcanic (B6), mesa caps (B7), rift.
   Each multiplied by its province weight. These define the big shapes.
6. **TIER 4 — Stratification (B4)** applied to current H — differential-erosion staircase. This
   reworks EVERYTHING above it, giving the layered-rock identity.
7. **TIER 5 — Fluvial**: rivers/valleys (B2) carve into current H; lakes/playa (B3) flatten basins.
   These are EROSIONAL (subtract/flatten), applied after bedrock.
8. **TIER 6 — Surface texture**: dunes/bedforms (B8) on sediment/shelf; fractured walls (B9) on all
   steep slopes; talus at slope bases.
9. **SOFT CEILING** (done): exp compression near WaterSurfaceY so rare peaks round off, no clip.
10. **Emit MacroMasks** incl. new ProvinceType/ProvinceBlend + per-feature masks for the atlas.

RELIEF BUDGET: each tier's amplitude must be a fraction of remaining budget so total never slams the
clamp. Compute a `reliefBudget` from depth-below-surface and distribute; verify max height in atlas
report stays comfortably below the +620 / -clamp.

DETERMINISM & PERF: every random via Hash(seed, cell) — no System.Random, no time. Burst-compatible
(no try/catch, no managed types) in EvaluateHeightMeters and anything it calls. Reuse existing
noise helpers (FractalSimplexNoise01, RidgedMultifractal01, ErodedRidge01, Hash, HashToUnitFloat,
CellularF1F2, etc.) — do NOT invent new noise bases; check what exists first (~lines 700-1099).

---

## PART 3 — WHAT CLAUDE ALREADY CHANGED THIS ROUND (verify, don't redo)

1. **Plate seam discontinuity FIXED** in WorldMacroGeologyFields.cs TIER 1: `boundaryPolarity` now
   from `FractalSimplexNoise01(warpedNorm*0.85 + (41.3,-22.7))` (was per-cell hash of nearestPlateCell);
   `jaggedBoundary` now uses fixed offset `(13.6,-8.1)` (was per-cell nearestPlateHash). => the sharp
   straight cliff should be GONE. VERIFY on P1..P5 10km height + structure maps.
2. **oceanicRidgeGate = 1 - continentality** (was 1 - continentality*0.75): oceanic ridge fully off
   on land -> no double-mountain stacking -> less clipping.
3. **Soft ceiling**: above the -260 knee, depth compressed via `-260 - 340*(1-exp(-over/340))`
   (asymptote ~-600) instead of hard clamp -> rare peaks round off, no flat mesa-tabletops from
   clipping. VERIFY: fewer flat +620 grey caps; island summits rounded.
4. **Atlas**: re-added **200m** scale; added **`1b_detail`** auto-ranged (per-cell min/max) grayscale
   height map so fine relief is visible (absolute DepthRamp compressed 5m into invisibility).

If any of these regressed or look wrong, SAY SO and propose the correct fix — don't silently revert.

---

## PART 4 — VERIFICATION PROTOCOL (mandatory, honest, root-level)

1. **Compile**: trigger a fresh Unity compile. Report errors verbatim. Fix compile errors from the
   root (understand the cause), don't hack around them.
2. **Regenerate atlas FROM SCRATCH**: DELETE all old PNGs in the atlas dir first, run
   `Hecton8/Diagnostics/Geology Atlas`, then PROVE freshness by listing file timestamps (they must be
   newer than the delete). The Director caught stale-atlas review before — never review old images.
3. **LOOK at every image** (P1..P5 × {10km,1km,200m} × {1_height, 1b_detail, 2_hillshade, 3_slope,
   4_structure, 5_substrate, 6_features}). Use your vision. For EACH feature below, state VISIBLE /
   FAINT / NOT VISIBLE and where:
   - [ ] Provinces genuinely different region-to-region (A ≠ B ≠ C)?
   - [ ] Craters — unmistakable circular rim+bowl+ejecta?
   - [ ] Rivers/valleys/deltas — dendritic channels?
   - [ ] Lakes/playa — flat closed basins?
   - [ ] Stratification — horizontal benches/bands on slopes?
   - [ ] Fold belts — parallel corrugated ridges?
   - [ ] Volcanic — cones/calderas/flows?
   - [ ] Mesas — flat tops + stepped sides?
   - [ ] Dunes/bedforms on shelf?
   - [ ] Fractured walls (no smooth 45° ramps)?
   - [ ] Islands fewer & smaller; shallows interesting?
   - [ ] Plate seam straight-cliff GONE?
   - [ ] No clipping (report max height, flat-cap coverage)?
   - [ ] HATCHING index < 1.8 on all cells (no directional striation artifact)?
4. **Report** the atlas_report.txt stats (min/max/std/mask coverage/hatching) per cell.
5. **Brutal honesty**: list every defect YOU see, ranked. Hypothesize root cause for each. Propose
   next-pass fixes. Do NOT claim success on anything you cannot see in an image.

You have full authority to implement, refactor helpers, and re-tune. Build it ALL in as few large
passes as compile-safety allows. This is a from-the-roots overhaul, not a tweak. Think ahead —
voxels, scatter, and materials will read these masks later, so emit them cleanly. Don't fuck it up:
emulate the math before you commit, watch for clipping/seams/NaN, verify with your eyes.




