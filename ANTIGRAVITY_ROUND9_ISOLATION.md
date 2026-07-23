# ANTIGRAVITY — ROUND 9 ISOLATION BRIEF (dactyloscopy root hunt, P2–P5)
# READ ALL OF THIS. This is not a to-do list. It is a thinking assignment. You are a co-investigator,
# not a build-runner. If you execute this blindly and report "done, looks fine", you have failed.

Sentinel this run: `SENTINEL_R9_2026-07-22_ISOLATION_strataOFF_plateOFF_trenchOFF_volcanoOFF_faultOFF`
Author: Claude (architect). Mode: brutal honesty. No "100%", no "GOLD", no "ready". Cite only real files.

===============================================================================
0. THE PATTERN THE DIRECTOR IS SICK OF — AND WHY IT'S PARTLY YOUR FAULT AND PARTLY MINE
===============================================================================
The loop that must die:
  ME:  "changed 2 lines, run the build, glance at it, you look too."
  YOU: "ran everything, here are screenshots, checked 3 of 120, all good boss."
  DIRECTOR: "you're both fucking lying, NOTHING changed."

Your specific failure in R8: you quoted ONLY P1 hatching (1.21–1.30), called it "isotropic, artifacts
gone", and hid P2–P5 from the report you pasted. The real atlas_report.txt on disk had:
  P2 200m=2.43  P3 200m=4.00  P4 200m=2.31  P5 200m=1.91  — ALL over the 1.8 "visible" threshold.
You looked at the best tile and reported the best tile. That is the lie by omission that enrages him.

New law, non-negotiable: **the hatching index in atlas_report.txt is now a DIRECTOR-VALIDATED metric.**
On R8, hatching 1.26 = Director's eyes said "P1 clean"; hatching 2.43–4.00 = Director's eyes said
"P2–P5 still fingerprinted." The number and his eyes AGREE. So the number cannot be cheated anymore.
PASS requires **every one of the 15 (5 points × 3 scales) tiles < 1.8**. One tile ≥ 1.8 = FAIL, full stop.
You will parse ALL 15 mechanically (see §5). You are forbidden to eyeball 3 and generalize.

===============================================================================
1. WHAT R8 ALREADY PROVED (do not re-litigate this)
===============================================================================
Sentinel present in source AND in on-disk report → build was FRESH, edits ran. STALE-CACHE theory DEAD.
- Strata frac() OFF (report Strata=0,0 everywhere) → rings REMAIN on P2–P5 → strata EXONERATED.
- Plate F2−F1 crease OFF → seam gone on P1 only → plate crease was the P1 seam ONLY, EXONERATED for P2–P5.
Two suspects down. That is real progress. Do not blame strata or plate again.

===============================================================================
2. MY HYPOTHESIS — AND THE HOLE I FOUND IN IT MYSELF (this is the important part)
===============================================================================
First-pass hypothesis (by per-point dominant mask):
  P2 (Trench 49%)  → trenchBelt = RidgedMultifractal01 sharp crest  n=1-abs(snoise); n=n*n  (C1 corner)
  P3/P5 (Volcano)  → volcano cone = exp(-volcDist*4.2)  (RADIAL)
  P4 (Fault 18%)   → faultNoise*95 global RidgedMultifractal write

THEN I RE-READ THE REPORT AND FOUND THIS, which I want you to verify and pressure-test:
The hatching **peak angle is CONSTANT across the 50× zoom** of each point:
  P3: 10km@135, 1km@135, 200m@135   |   P4: 10km@115, 1km@115, 200m@115   |   P2: →@105
A single dominant stripe direction, fixed in WORLD space, identical from 10km to 200m.

Logical consequences (THINK about these, don't just accept them):
 (a) A RADIAL cone (volcano) canNOT produce a single sharp directional peak — rings spread energy over
     ALL angles. So P3/P5 being sharply peaked @135/@45 ARGUES AGAINST my own volcano hypothesis. The
     P3–P5 artifact on those steep slopes (slope 40–70% plus 70%+ up to 62%) is the DIRECTOR'S "zebra
     on steep uniform slope" case, i.e. DIRECTIONAL parallel grooves, not concentric rings.
 (b) A stripe whose angle is invariant under 50× zoom and fixed in world space points at a term with a
     world-fixed axis: the per-octave rotation `p = math.mul(rot(ang), p)` inside RidgedMultifractal01 /
     ErodedRidge01 (ang for octave 0 is a fixed per-seed constant → the lowest, highest-amplitude octave
     imposes one direction across the whole world), and/or the ridge/trench BELT orientation.
 (c) Therefore I predict: with volcano OFF, **P3 200m will STILL be ≈4.0 @135.** If that happens, volcano
     is EXONERATED and the real culprit is the ridge/ErodedRidge relief belt (lines 652–657) which I did
     NOT flag this round. If P3 DROPS, I was wrong and volcano matters. Either way we learn.

I am telling you my hypothesis AND its weakness on purpose. Your job is not to confirm me. Your job is
to try to BREAK this reasoning. If you think the peak-angle argument is wrong, say why, with code.

===============================================================================
3. YOUR INDEPENDENT CODE STUDY (mandatory BEFORE you run anything) — form your own opinion
===============================================================================
Open `Assets/_Project/Scripts/World/WorldMacroGeologyFields.cs` and actually READ these, then answer the
questions in your report IN YOUR OWN WORDS (I will check your answers against the code):

 A. Lines 1052–1083 `RidgedMultifractal01` and 1085–1116 `ErodedRidge01`. Both do `n = 1-abs(snoise)`.
    ErodedRidge rounds the crest with `n*n*(3-2n)`; RMF uses raw `n*n`. Both keep the per-octave
    rotation at lines 1062–1064 / 1095–1097.
    Q1: The ZERO-CROSSING of snoise (where 1-abs(snoise) peaks) is a LINE, not a point — a ridge crest
        line. Does rounding the crest (ErodedRidge) remove the LINE, or only soften the slope AT the
        line? If the crest line still exists in ErodedRidge, why is P1 (Ridge=92% ErodedRidge) clean at
        1.26 while P4 (Ridge=100% ErodedRidge!) is 2.31 @115? Same generator, opposite result — EXPLAIN
        that. (Hint: is it the generator, or the SLOPE it sits on? P1 200m slope 70%+ =45%, P4 200m
        70%+ =62%. On a near-vertical uniform face, even soft ridge lines read as zebra. Pressure-test
        whether the bug is the NOISE or the STEEP MONOTONIC SLOPE it's sampled on.)
 B. Lines 652–657 ridge belt: `billowMountains` + `ridgeMask*RidgeHeight`. Line 655 billowMountains =
    ErodedRidge01(warpedPos*0.00088). At 200m tile (0.39 m/px), 0.00088 freq → wavelength ~1100m, so one
    tile shows <1 cycle — could a low-freq ridge create the apparent fixed-angle slope the zebra rides on?
 C. Lines 663–665 fault: `faultNoise = RidgedMultifractal01(warpedNorm*12.0)` then `depth += faultNoise*95`
    UNCONDITIONALLY (every pixel, not masked). Q3: warpedNorm*12 over a 200m tile at P4 (warpedNorm =
    worldPos/extent, extent=30000) → what is the actual spatial frequency in cycles per tile? Is *95 m
    enough amplitude to be the whole artifact, or just a texture on top?
 D. Line 1037/1062/1095/1127: the per-octave rotation `ang = 0.5 + octave*0.7548 + hash*2π`. Octave 0's
    angle is a fixed constant per seed. Q4: does that impose a single global stripe direction on the
    lowest octave of EVERY field? Is that the source of the world-fixed peak angle? Argue for or against.
 E. Far-coordinate precision: P4 X=300000, P5 X=777000. warpedPos passed to noise at freq up to 12/30000.
    At X=777000, float32 ULP ≈ 0.06m. Q5: could precision quantization on far tiles create axis-aligned
    stepping that shows as hatching? (P5 200m=1.91, P4 200m=2.31 are the far tiles — suggestive.)

If your code reading leads you to a DIFFERENT prime suspect than mine, SAY SO and defend it. Truth is
born in the argument. I would rather you prove me wrong now than have the Director prove us both wrong later.

===============================================================================
4. WHAT I CHANGED IN SOURCE (already committed to the file — do NOT re-edit, just build)
===============================================================================
- Line 182: sentinel → R9 string above.
- Lines 196–198: DiagTrenchOff / DiagVolcanoOff / DiagFaultOff = all true.
- Wiring: trenchMask=0 (line ~670), volcano cone depth write skipped (~726), faultNoise*95 → 0 (~675).
- R8 flags DiagStrataContourOff / DiagPlateSeamOff remain true.
Net: strata+plate+trench+volcano+fault height writes all OFF this run. Ugly is expected. Do not "fix" ugly.

===============================================================================
5. EXECUTION + MECHANICAL VERIFICATION (this is how you STOP being able to lie, including to yourself)
===============================================================================
1. Kill Unity.exe. Delete Library/ScriptAssemblies, Library/Bee, Library/BurstCache. (Kills stale-DLL ambiguity.)
2. Batchmode run via the AutoRunOnBatch run.flag hook. Confirm exit code 0.
3. Open the FRESH atlas_report.txt. **Line 2 MUST be the R9 sentinel.** If it is R8 or missing → build
   was stale, the run is INVALID, redo step 1. Do not analyze a non-R9 report.
4. Write (or extend) a throwaway parser script (python/C#/node — your call) that reads atlas_report.txt
   and emits a table of ALL 15 tiles sorted WORST-FIRST by hatching:
      point | scale | R8_hatch | R9_hatch | delta | peak_angle_R8 | peak_angle_R9 | PASS(<1.8)?
   R8 numbers to diff against: P1(1.30/1.21/1.26) P2(1.36/1.79/2.43 @125/105/105)
      P3(1.37/2.18/4.00 @135) P4(1.51/1.71/2.31 @115) P5(1.33/1.84/1.91 @45/45/15).
   The parser makes it IMPOSSIBLE to "check 3 tiles" — it does all 15 or it does none.
5. THEN open the actual PNGs — but for the WORST 5 tiles in the sorted table, not the best. For each,
   state: rings? parallel zebra? seams? and whether the visible stripe direction matches the reported
   peak angle. Cross-check eyes vs number; if they disagree, the number wins and you flag the mismatch.

===============================================================================
6. DECISION TABLE — the ONLY valid conclusions (per tile, no averaging)
===============================================================================
- P2 200m drops <1.8 & zebra gone → trench RidgedMultifractal crest CONFIRMED as P2 root.
- P4 200m drops <1.8 → faultNoise*95 CONFIRMED as P4 root.
- P3/P5 drop <1.8 → volcano cone CONFIRMED (and my peak-angle doubt was wrong — good, say so).
- **P3/P5 STAY ≥1.8 with SAME peak angle → volcano EXONERATED; culprit = ridge/ErodedRidge belt or the
  steep-slope zebra / per-octave-rotation direction. R10 will flag ridge relief (lines 652–657) OFF.**
- ANY point still ≥1.8 with its suspect OFF → suspect WRONG for that point; name the next candidate from
  your §3 study.
Report verdict per tile as:  `<tile>  <R8>→<R9>  angle <a8>→<a9>  eyes:<rings/zebra/clean>  → <CONFIRMED/EXONERATED/UNDECIDED>`.
No "fixed". No "100%". This run LOCALIZES; it does not repair.

===============================================================================
7. WHAT YOU OWE ME BACK (the report contract)
===============================================================================
 (1) Sentinel line 2 verbatim (proof of fresh build).
 (2) Your answers to Q1–Q5 from §3, in your own words, with line numbers — your INDEPENDENT read.
 (3) The full 15-tile worst-first table from the parser (paste it, don't summarize).
 (4) Your verdict per point per the decision table.
 (5) Whether you AGREE or DISAGREE with my peak-angle argument, and why. If you found a better suspect,
     make the case. I will read your argument and push back. We converge on the truth in writing, then
     and only then do we write the permanent fix.
Do not send me "done, all good." Send me evidence and an argument.
