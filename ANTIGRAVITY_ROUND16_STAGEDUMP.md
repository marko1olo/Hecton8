# ANTIGRAVITY — ROUND 16: STAGE DUMP (the method we should have used in round 1)
# Prereq: ANTIGRAVITY_R9_KICKOFF.md core rules R1–R9. Live source is truth. EVIDENCE only, never "done/100%".
Sentinel: SENTINEL_R16_2026-07-22_STAGEDUMP_perStageDepth
Author: Claude (architect).

===============================================================================
0. WHY EVERY PRIOR ROUND FAILED — AND WHY THIS ONE CANNOT
===============================================================================
R8–R15 all did the same thing: toggle ONE feature off, rebuild, guess. That method has three fatal holes:
  1. The hatching metric was degenerate (R13) → 5 verdicts void.
  2. Reports mis-described images and cited fabricated line numbers.
  3. We NEVER isolated WHICH accumulation stage first introduces a defect.
R15 killed the last excuse: with BOTH ridged→fBm AND folds-off ON, the Director opened P5_deepfar_200m
himself and the zebra (~65°, left 40% of the tile) was STILL THERE in full force. So the zebra is NOT the
ridged crest and NOT the fold sin(). We were wrong for 8 rounds because we guessed instead of measuring.

R16 stops guessing. The evaluator now returns the depth field HALTED after each accumulation stage. ONE
build renders a hillshade of the terrain after stage 1, 2, 3, … 7, plus the full result (stage 8). The
stage at which the zebra / rings / hairline FIRST appears is the culprit — no metric, no feature-guessing,
no trust required. The pixels name the line.

===============================================================================
1. THE STAGE MAP (already wired in source — you only build & run ONE atlas)
===============================================================================
EvaluateHeightMeters now takes a stageDump arg; the atlas loops it 1..7 per tile and saves
`<point>_<scale>_stage<N>_hillshade.png`. Each stage is the depth field with accumulation STOPPED right
after that stage's writes:
  stage1 = base shelf/abyss depth only (shelfMask lerp Abyss↔Shelf + abyssPlain)         [line ~672]
  stage2 = + continental relief: mountains, foothills, plateau, broad geoNoise            [line ~714]
  stage3 = + ridges (ErodedRidge/billowMountains crest, oceanicRidgeGate)                 [line ~722]
  stage4 = + trench, fault, basin                                                         [line ~735]
  stage5 = + FOLD sin() corrugation                                                       [line ~760]
  stage6 = + volcano, crater, river, lake, mesa, DUNE sin()                               [line ~904]
  stage7 = + STRATA frac() benches                                                        [line ~940]
  stage8 = full pipeline (+ mesoFracture, talus, soft ceiling) = the normal _2_hillshade
All Diag flags are at their real shipping values (nothing suppressed). This is the true terrain, sliced.

===============================================================================
2. BUILD (clean, sentinel-verified)
===============================================================================
Kill Unity.exe. Wipe Library/ScriptAssemblies, Library/Bee, Library/BurstCache. Batchmode, exit 0.
atlas_report.txt line 2 MUST == SENTINEL_R16_2026-07-22_STAGEDUMP_perStageDepth. Non-match → stale → redo.
Output now includes, per tile: _1_height, _2_hillshade (full), _3_slope, AND _stage1.._stage7_hillshade.

===============================================================================
3. THE AUDIT — FIND THE STAGE OF BIRTH (eyes only, metric BANNED)
===============================================================================
Focus on the three tiles where the Director has confirmed the worst defects by his own eyes:
  • P5_deepfar_200m — the ~65° regular ZEBRA (left 40%).
  • P5_deepfar_10km — the massive CONCENTRIC dactyloscopy RINGS.
  • P1_origin_10km — the 1px HAIRLINE seam (top-left).
Plus P2_near_1km and P3_west_200m (continental zebra) as secondary.

For EACH of those 5 tiles, open stage1→stage8 IN ORDER and report, per stage, one line:
  `<tile> stage<N> | <what you see> | ZEBRA: absent/PRESENT(orientation)  RINGS: absent/PRESENT  SEAM: absent/PRESENT(where)`
The critical observation is the TRANSITION: name the FIRST stage N where each defect appears that was
absent at stage N-1. Example: "P5_deepfar_200m: zebra ABSENT stage1-6, PRESENT stage7 → strata frac()".
Do this honestly. If a defect is already present at stage1 (base shelf), that is a huge finding (it would
mean the shelfMask/warp base itself stripes). NEVER fabricate; NOT VIEWED if you can't open it.

===============================================================================
4. VERDICT — NAME THE STAGE, THEN THE LINE
===============================================================================
For each of the three defects (zebra, rings, seam) state: "born at stage <N>". Then open the source for
that stage's block and quote the ACTUAL line (not a remembered one) that creates it. Candidate mechanisms,
but let the stage image decide — do not pre-commit:
  • If zebra born at stage7 → strata `math.frac(hPhase)` benches (frac of tilted depth = parallel bands).
  • If rings born at stage3 → ErodedRidge crest on the abyssal floor.
  • If seam born at stage1 → shelfMask smoothstep lerp of a ~2700m Abyss↔Shelf step (a 1px height cliff).
  • If a defect appears at a stage we didn't predict → that is the real answer; report it plainly.
DEBATE: given the stage of birth, argue what the minimal fix is (widen a smoothstep, warp a frac phase,
round a crest) WITHOUT killing the geological intent. The images decide, not my prediction.

Deliver: sentinel, per-stage lines for the 5 tiles with the transition called out, the three "born at
stage N → line X" localizations, verdict, argument. Not "done".
