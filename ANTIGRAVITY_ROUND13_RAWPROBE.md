# ANTIGRAVITY — ROUND 13: RAW PRIMITIVE PROBE (stop hunting features; test the foundation)
# Prereq: ANTIGRAVITY_R9_KICKOFF.md core rules R1–R9. Live source is truth. EVIDENCE only, never "done/100%".
Sentinel PASS 1: SENTINEL_R13_2026-07-22_RAWPROBE1_bareSimplexUnwarped
Author: Claude (architect).

===============================================================================
0. WHY WE ARE CHANGING STRATEGY COMPLETELY (read this — it's the whole point)
===============================================================================
Rounds 8–12 each isolated ONE guessed feature (strata, plate, trench, volcano, fault, mesoFracture,
talus, geoNoise, weight-feedback, ridged-crest, folds, dunes). EVERY SINGLE ONE was exonerated. Worse:
removing features often made the zebra WORSE (R10 HF-off, R12 folds-off → hatching exploded). And your
own R12 audit found the fact that breaks every feature theory:

   P5_deepfar_1km has slope70+ = 0.1% — it is essentially FLAT — yet hatching = 4.33 at 45°.
   ZEBRA STRIPES ON A FLAT TILE. No steep slope, no fold there, and turning features off made it worse.

Logical conclusion (this is forced, not a guess): the striping is NOT produced by any feature we add on
top. It is INTRINSIC to the FOUNDATION that every single term is built on — either Unity's `noise.snoise`
itself has a directional/grid artifact, or the domain WARP that we apply to almost every sample creates
coherent striping. In 5 rounds we NEVER tested the bare primitive with everything else stripped away.
That ends now. R13 bypasses ALL geology and outputs pure noise so we can see, at the very bottom of the
stack, where the stripes are born.

I am not giving you a suspect to confirm this round. I am giving you a MEASUREMENT that will tell US
both where the rot is. If probe 1 stripes, then 11 rounds of feature-hunting were the wrong layer of the
codebase entirely — and that is the single most important thing we could learn.

===============================================================================
1. WHAT THE PROBE DOES (already wired — EvaluateHeightMeters early-returns pure noise)
===============================================================================
`public const int DiagRawProbe` (line 231). Right after warpedPos is computed (line 603) the function
returns raw noise and nothing else runs:
  DiagRawProbe = 1 : depth = noise.snoise(pos * 0.0009) * 400        ← BARE simplex, UNWARPED world pos
  DiagRawProbe = 2 : depth = noise.snoise(warpedPos * 0.0009) * 400  ← BARE simplex, WARPED pos
  DiagRawProbe = 3 : depth = FractalSimplexNoise01(pos*0.0009,5)*... ← 5-octave fBm, UNWARPED
No plates, no provinces, no features, no ridged transform, no sin folds. Just the primitive.
Interpretation:
  • Probe 1 shows STRIPES/grid/hairlines  → the root is Unity `noise.snoise` (a directional lattice
    artifact in the noise implementation). Everything downstream inherits it. THIS is the jackpot answer.
  • Probe 1 CLEAN, Probe 2 STRIPES        → the DOMAIN WARP (tectonicWarp+mesoWarp) creates coherence.
  • Probe 1 & 2 CLEAN, Probe 3 STRIPES    → the octave accumulation / per-octave rotation in
    FractalSimplexNoise01 is the source.
  • All three CLEAN                        → striping really is in the feature stack; we re-add layers
    from a clean base one at a time (next round). But given the flat-tile zebra, I expect 1 or 2 to stripe.

===============================================================================
2. THREE-PASS BUILD (RULE 1: one variable = the probe value)
===============================================================================
PASS 1: line 231 `= 1`, sentinel already `..._RAWPROBE1_bareSimplexUnwarped`. Clean build (kill Unity.exe;
        wipe Library/ScriptAssemblies, Bee, BurstCache). Batchmode, exit 0. Confirm report line 2 == that
        sentinel. Copy atlas folder → atlas_R13_P1.
PASS 2: line 231 `= 2`, change sentinel to `SENTINEL_R13_2026-07-22_RAWPROBE2_bareSimplexWarped`.
        Clean build, run. Copy → atlas_R13_P2.
PASS 3: line 231 `= 3`, sentinel `SENTINEL_R13_2026-07-22_RAWPROBE3_fbmUnwarped`. Clean build, run.
        Copy → atlas_R13_P3.

===============================================================================
3. VISION AUDIT — focused (this round the KEY images, described honestly)
===============================================================================
For EACH of the 3 passes, open the hillshade for P1_origin, P3_west, P5_deepfar at 10km AND 200m
(6 hillshades per pass, 18 total). For each, STEP A objective (vision, no numbers):
   - Is the surface smooth isotropic noise blobs, or are there STRIPES / parallel grooves / a grid /
     1px hairlines? If striped: orientation and rough spacing.
   - (S) hairline? (Z) parallel stripes? — same tags as before.
STEP B: your read of what this pass proves.
Also report the 15-tile hatching table per pass (raw noise should be ~1.0–1.2 if the primitive is
isotropic; if probe-1 hatching is high, the primitive itself is directional).
Never fabricate an image you didn't open — say NOT VIEWED.

===============================================================================
4. VERDICT + DEBATE
===============================================================================
State plainly which pass first shows striping, and therefore which layer is the root:
  primitive (probe1) / warp (probe2) / octave machinery (probe3) / none (feature stack).
This is the round that finally localizes the bug to a FILE and a FUNCTION instead of a feature. If you
disagree with my reasoning that flat-tile zebra forces a foundation cause — argue it, with the image
evidence and the P5_deepfar_1km numbers. If probe 1 is clean and features are off yet earlier rounds
showed zebra, that itself is a critical clue (means the warp or octave stage).

BONUS — while you have raw noise in front of you: does Unity `noise.snoise` (Unity.Mathematics) look
isotropic to your eye at these frequencies, or does it show the diagonal ridge tendency simplex noise is
known for? Your honest visual read matters here.

Deliver: 3 sentinels, 18 hillshade Step-A/B lines, 3 hatching tables, the localization verdict, argument.
Not "done".
