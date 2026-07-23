# ANTIGRAVITY — ROUND 12: TWIN ORTHOGONAL TEST (seam vs zebra, decisively separated)
# Prereq: ANTIGRAVITY_R9_KICKOFF.md core rules R1–R9. Live source is truth. EVIDENCE only, never "done/100%".
Sentinel: SENTINEL_R12_2026-07-22_ridgedAsFbm_foldsDunesOff
Author: Claude (architect).

===============================================================================
0. R11 SETTLED TWO THINGS — CREDIT AND A RETRACTION
===============================================================================
Your R11 work was the best round we've had: you opened all 60 images and described them objectively.
It produced two hard results:
 1. It REFUTED the architect's (my) hypothesis. Forcing weight=1 (broadband) removed ZERO seam/zebra
    pixels. The octave weight-collapse is NOT the root. I was wrong; I retract it. No ego here — a
    refuted hypothesis that we PROVED refuted is progress.
 2. Your ZOOM-SPACING finding (stripe pitch scales with zoom) is correct and important: it proves the
    artifacts are REAL TERRAIN GEOMETRY, not a render/metric bug in GeologyAtlasTask.cs. That file is
    cleared. Good work.

ONE CORRECTION to your R11 verdict: you proposed the 1px seam comes from "domain-warp / voronoi lattice
discontinuity (lines 638–650 / 492)". That is mathematically impossible for the warp: warpedPos = pos +
tectonicWarp + mesoWarp, and BOTH warps are smooth C∞ FractalSimplexNoise. Adding a smooth field to
position cannot create a 1-pixel discontinuity — it just bends coordinates smoothly. So the warp is not
the seam. (The province lattice at 492 uses floor() but its recipe blend is the smooth exp(-k·dist)
sum, and strata/plate are already OFF, so that path is quiet too.) We test the real mechanism below.

===============================================================================
1. THE TWO REAL LEADS (from the source math — verify each yourself)
===============================================================================
Two SEPARATE bugs, two SEPARATE mechanisms. We stop conflating them.

SEAM (1px hairline) → THE RIDGED TRANSFORM ITSELF.
  RidgedMultifractal01 (line ~1106) and ErodedRidge01 (~1140) compute n = 1 - |snoise|. That function
  has a SHARP MAXIMUM RIDGE exactly where snoise crosses 0 — a crest LINE one sample wide. On a
  hillshade, a 1-sample-wide height maximum reads as a bright/dark 1-PIXEL HAIRLINE. These generators
  are called for mountainField, ridgeBelt, billowMountains, trenchBelt, faultNoise, jaggedBoundary,
  rivers, mesoFracture — i.e. the crest lines thread the whole continental relief. That is your seam.
  NOTE: broadband (R11) kept n=1-|snoise| — it only changed the octave WEIGHT, not the crest shape. So
  R11 could never have removed the seam. THIS test changes the crest shape.

ZEBRA (regular parallel grooves) → B5 FOLD + B8 DUNE sin() corrugation.
  Line 708: depth -= foldAsymmetry*240m, where foldAsymmetry = fn of sin(dot(warpedPos, foldAxis)*0.0012
  + noise). foldAxis is a per-region fixed direction. sin(dot(pos, fixedAxis)) = REGULAR PARALLEL WAVES
  locked to a world direction = textbook dactyloscopy. Folds are gated by `continentality` → strongest
  on continental tiles P2/P3 — which is EXACTLY where your 60-image audit saw the worst zebra (115/135°).
  Line 850: dune sin(dot(pos, duneAxis)*0.025) — same class, on shelf/dune tiles.
  Broadband never touched the sin term → that's why zebra survived R11 fully intact. Consistent.

===============================================================================
2. THE TEST (already wired in source — you build & run ONE atlas, both flags true together)
===============================================================================
Because the seam and the zebra are on the SAME tiles but are VISUALLY DISTINCT (hairline vs parallel
grooves), we can flip both switches in one run and read them independently by eye. RULE 1 holds because
each switch targets a visually separable artifact.

 TEST A — `DiagRidgedAsFbm = true` (line 217): in BOTH RidgedMultifractal01 and ErodedRidge01, n is
   replaced by plain fBm `n = snoise*0.5+0.5` (no crest, no ridge line), weight forced to 1. Wired at
   lines 1108 and 1142. PREDICTION: the 1px hairline seams DISAPPEAR (terrain becomes rounded blobby
   hills — uglier/rounder is fine, this is a test). If they disappear → the ridged crest IS the seam.
 TEST B — `DiagFoldsDunesOff = true` (line 218): the fold sin write (line 717) and dune sin write (851)
   are skipped. PREDICTION: the regular parallel zebra grooves DISAPPEAR on continental tiles P2/P3.
   If they disappear → fold/dune sin corrugation IS the dactyloscopy.

Build: kill Unity.exe; wipe Library/ScriptAssemblies, Bee, BurstCache; batchmode; exit 0. Confirm
atlas_report.txt line 2 == SENTINEL_R12_2026-07-22_ridgedAsFbm_foldsDunesOff. Non-match → stale → redo.

===============================================================================
3. MANDATORY 30-IMAGE VISION AUDIT (same contract as R11 — you did it well, do it again)
===============================================================================
Open EVERY _2_hillshade.png and _1_height.png for all 5 points × 3 scales = 30 images. For EACH:
 STEP A (objective, vision only, NO numbers): describe landform; then explicitly answer TWO questions —
   (S) SEAM: any 1px straight/curved bright/dark hairline crossing the tile? present / GONE vs R11?
   (Z) ZEBRA: any regular parallel evenly-spaced grooves? present / GONE vs R11? orientation?
   Also note richness and any other artifact.
 STEP B (opinion, after A): does this tile now look natural, or what remains broken?
Write `<filename> | STEP A: … (S: …) (Z: …) | STEP B: …`. If not viewed, say so — never fabricate.
The Director will spot-check against the PNGs.

Compare directly to your R11 BASE descriptions (you have them) so "GONE vs R11" is a real before/after.

===============================================================================
4. NUMBERS (secondary) + VERDICT + DEBATE
===============================================================================
 • 15-tile worst-first hatching table (R12) with peak angles, next to R11 BASE for delta.
 • VERDICT, one of:
    - Hairlines GONE + zebra GONE → BOTH roots found. Next round: keep fBm for macro relief (or warp/
      soften the ridged crest), and replace fold/dune sin() with domain-warped non-periodic folds.
    - Hairlines GONE, zebra REMAINS → seam=ridged crest confirmed; zebra is NOT fold/dune (next suspect:
      ridge-belt isolines, or a mask smoothstep threshold stepping a big amplitude). Name it with a
      worst-tile mask check (the permanent rule).
    - Hairlines REMAIN → ridged crest is NOT the seam; next suspect = a smoothstep THRESHOLD edge that
      steps a large depth amplitude (shelfMask line 623 lerps Abyss↔Shelf ~2860m across a smoothstep;
      mesa cap line 834; continentality lerp line 665). A hard-ish mask edge on a huge amplitude = a
      near-1px height step = hairline. Check these.
 • DEBATE: from YOUR read of lines 1106–1144 and 700–708/846–851, and YOUR eyes, do you agree A explains
   the seam and B explains the zebra? If the images say otherwise, the images win — argue with evidence.

Deliver: sentinel, 30 Step-A/B lines with explicit (S)/(Z) tags, the table, verdict, argument. Not "done".
