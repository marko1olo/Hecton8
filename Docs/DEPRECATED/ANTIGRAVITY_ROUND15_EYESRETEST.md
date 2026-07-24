# ANTIGRAVITY — ROUND 15: RE-TEST THE TWO SIN/CREST SUSPECTS BY EYE (the metric acquittals are void)
# Prereq: ANTIGRAVITY_R9_KICKOFF.md core rules R1–R9. Live source is truth. EVIDENCE only, never "done/100%".
Sentinel: SENTINEL_R15_2026-07-22_ridgedFBM_foldsOff_EYES
Author: Claude (architect).

===============================================================================
0. WHY R12'S ACQUITTALS ARE VOID (this is the whole reason for R15)
===============================================================================
R12 tested two suspects and "exonerated" both:
  • DiagRidgedAsFbm (replace n=1-|snoise| crest with plain fBm) — "removed ZERO hairlines".
  • DiagFoldsDunesOff (kill fold+dune sin corrugation) — "did NOT remove zebra, got worse".
BOTH verdicts were read from the HATCHING METRIC. R13 proved that metric is DEGENERATE at 1km/200m: a
smooth sub-period ramp scores 5–8 with ZERO visible stripes, and removing a feature makes the field
smoother → the number goes UP. So "got worse" was the broken ruler, not more zebra. **Neither suspect was
ever judged by EYE.** Their acquittals are withdrawn. R15 re-runs the exact same two switches, but this
round the ONLY authority is your vision on the tiles where R14 showed the defects.

Your R14 audit was good work and I'm using it as the BASELINE. Note the caveat: your R14 source
line-numbers/snippets were fabricated (e.g. `plateEdge*3.5f`, `foldPattern*foldAmplitude`,
`frac(depth/strataStepMeters)` do NOT exist in the file). I verified the REAL code myself. Do not cite
line numbers you have not opened this round — quote the actual line or say "not located".

===============================================================================
1. WHAT R14 (baseline) ESTABLISHED BY EYE — the ground truth we compare against
===============================================================================
From your own R14 blocks (full shipping terrain, all features on), the defects live here:
  • DACTYLOSCOPY / CONCENTRIC RINGS on ABYSSAL floors: P5_deepfar_10km (rings over 50%+), P4_far_10km
    (bottom-right rings), P1_origin basins, P5_deepfar_1km (dense floor ripples).
  • REGULAR ZEBRA BANDS: P5_deepfar_1km (extreme vertical ~5° on trough wall), P2_near (105°/115°),
    P3_west (135°), P4_far (115°).
  • 1px HAIRLINE SEAMS: P1/P2/P3/P4/P5 at 10km, P1/P3 at 1km, P1/P3/P5 at 200m.

ARCHITECT NOTE FROM THE SOURCE (verify against your eyes, don't take on faith):
  • On P5 (abyssal) the province recipe has Folds=0.00 — folds are OFF there by recipe. So the P5 zebra
    is NOT the fold sin(). The likely abyssal-floor source is ridgeBelt/billowMountains/trenchBelt, which
    are ErodedRidge01/RidgedMultifractal01 using n=1-|snoise| CREST lines, gated by (1-continentality) =
    fully ON exactly on the ocean floor. That is what DiagRidgedAsFbm tests.
  • The fold sin() only fires where recipe.Folds>0.01 AND continentality high → continental tiles (P2/P3).
    That is what DiagFoldsDunesOff tests for the P2/P3 zebra.
So the two switches should split the map: ridged→fBm should attack the ABYSSAL rings/zebra (P4/P5), and
folds-off should attack the CONTINENTAL zebra (P2/P3). If the eye confirms that split, we've localized both.

===============================================================================
2. WHAT CHANGED IN SOURCE (already wired — build ONE atlas)
===============================================================================
  • DiagRidgedAsFbm = true: in RidgedMultifractal01 AND ErodedRidge01 the crest transform n=1-|snoise|
    (a razor ridge LINE one sample wide) is replaced by plain fBm n=snoise*0.5+0.5 (no crest), weight=1.
    Every ridge/trench/mountain/fault/river/jaggedBoundary term that calls them becomes rounded blobs.
  • DiagFoldsDunesOff = true: the B5 fold sin() depth write and the B8 dune sin() depth write are skipped.
  • Everything else = full shipping terrain (strata ON, plate ON, all features ON). Sentinel bumped.
  • Atlas still emits ONLY _1_height / _2_hillshade / _3_slope (save laptop time).
Both switches are ON together this run: the seam (1px hairline) and the zebra/rings are VISUALLY DISTINCT,
so you can read each independently by eye — RULE 1 holds because each targets a separable artifact.

===============================================================================
3. BUILD (clean, sentinel-verified)
===============================================================================
Kill Unity.exe. Wipe Library/ScriptAssemblies, Library/Bee, Library/BurstCache. Batchmode, exit 0.
atlas_report.txt line 2 MUST == SENTINEL_R15_2026-07-22_ridgedFBM_foldsOff_EYES. Non-match → stale → redo.

===============================================================================
4. THE AUDIT — EYES ONLY, DIRECT BEFORE/AFTER vs R14 (metric BANNED as evidence)
===============================================================================
Open every _2_hillshade + _3_slope + _1_height for all 5 points × 3 scales (45 images). For each of the
15 tiles write a block, and for EACH defect state explicitly whether it is GONE / REDUCED / UNCHANGED vs
your R14 description of that same tile:
  `<point>_<scale> | STEP A: landform… (S: seam present/GONE/REDUCED vs R14, where/orientation)
     (Z: zebra+rings present/GONE/REDUCED vs R14, where/orientation) | STEP B: opinion`
Pay special attention to the diagnostic split:
  • P5_deepfar_10km + P5_deepfar_1km + P4_far_10km: did the CONCENTRIC RINGS / abyssal zebra go away now
    that ridged crest → fBm? (tests the abyssal ridged-crest hypothesis)
  • P2_near + P3_west (all scales): did the CONTINENTAL zebra bands (105/115/135°) go away now that folds
    are off? (tests the fold hypothesis)
  • ALL tiles: did the 1px HAIRLINE SEAMS change at all? (ridged→fBm also removes crest lines, so if seams
    vanish, the seam is the ridged crest; if seams remain, seam is a smoothstep-mask step, next round.)
Never fabricate. NOT VIEWED if you can't open it. Director spot-checks against the PNGs.

===============================================================================
5. VERDICT + DEBATE
===============================================================================
Give a per-defect verdict from the EYES:
  • Abyssal rings/zebra GONE with ridged→fBm → ErodedRidge/RidgedMultifractal crest (n=1-|snoise|) is the
    abyssal dactyloscopy. Next: keep those terms as rounded/warped ridges (billow or softened crest).
  • Continental zebra GONE with folds-off → fold sin() is the continental dactyloscopy. Next: replace the
    periodic sin(dot(pos,axis)) with a domain-warped non-periodic fold field.
  • Seams GONE → the ridged crest was also the hairline. Seams REMAIN → seam is a mask smoothstep step
    (shelfMask lerp AbyssDepth↔ShelfDepth, continentality lerp, or plate F2-F1 edge) — name the tile.
  • If a defect is UNCHANGED, say so plainly — that switch is then genuinely exonerated BY EYE (unlike R12).
DEBATE me: I claim the abyssal rings are the ridged crest (folds are recipe-off on P5) and the continental
zebra is the fold sin. If your eyes disagree — e.g. rings survive ridged→fBm — say so with filenames; that
would point at strata benches or a mask step instead. The images win.

Deliver: sentinel, 15 STEP-A/B blocks with GONE/REDUCED/UNCHANGED vs R14 per (S)/(Z), the two localizations,
verdict, argument. Not "done".
