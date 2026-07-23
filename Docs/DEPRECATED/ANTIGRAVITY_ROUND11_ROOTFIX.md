# ANTIGRAVITY — ROUND 11: DECISIVE A/B TEST + FULL INDEPENDENT VISUAL AUDIT (60 IMAGES)
# Prereq: ANTIGRAVITY_R9_KICKOFF.md core rules R1–R9. Live source is truth. EVIDENCE only, never "done/100%".
# Sentinel PASS 1 (BASE): SENTINEL_R11_2026-07-22_BASE_weightFeedbackLIVE
# Sentinel PASS 2 (BROAD): SENTINEL_R11_2026-07-22_BROAD_weightEQ1  (you set this before pass 2)
Author: Claude (architect). The Director is out of patience: 11 rounds, the 1px seam lines AND the
dactyloscopy are BOTH still alive, and R10 made things look WORSE. He does not trust either of us. He
believes you report from parsed numbers without opening the images. THIS ROUND YOU DESCRIBE EVERY IMAGE
WITH YOUR OWN VISION, OBJECTIVELY, BEFORE ANY OPINION. Read §4 — it is the core of the whole task.

===============================================================================
0. HONESTY FROM THE ARCHITECT — MY HYPOTHESIS AND WHY IT MIGHT BE WRONG
===============================================================================
I will NOT hand you a false certainty (we've both done that and been humiliated). Here is exactly where
I stand, including the hole in my own theory:

MY HYPOTHESIS (the thing this run tests): RidgedMultifractal01 and ErodedRidge01 (lines 1085–1140) use
a WEIGHT-FEEDBACK: `weight = saturate(n*2)` (RMF) / `saturate(0.35 + n*0.9)` (Eroded). weight multiplies
the NEXT octave. Where octave-0 is weak, weight→0, higher octaves die, and the field is dominated by a
few low octaves at their fixed per-seed rotation angles. Low-octave-dominated ridged noise tends toward
coherent, directional crest lines rather than isotropic fractal blobs. This could explain the fixed
world-locked peak angle and why R10's removal of HF dither made the lines CLEARER.

THE HOLE I FOUND IN MY OWN HYPOTHESIS (be aware of it while you look): the report shows fine striping
at the 200m scale (hatching up to 4.0 at 0.39 m/px). If the pattern were purely octave-0 dominance, a
200m tile would show ~1/50th of one big ridge = a nearly smooth gradient, NOT fine parallel stripes.
Fine stripes at 200m require HIGH-frequency octaves to survive. That partially CONTRADICTS pure
weight-collapse. So BROADBAND (this fix) may reduce the artifact but may NOT fully kill it. That's fine —
this is a TEST, not a promised cure. If it doesn't work we have eliminated a suspect and learned.

THEREFORE this run also carries a SECOND, cheap diagnostic that YOUR EYES resolve (§5): does the stripe
SPACING (in pixels) stay constant across zoom, or scale with the terrain? That single observation tells
us whether we're even looking at terrain or at a RENDER/METRIC artifact — a layer we may have never
tested in 11 rounds. Do not skip it.

===============================================================================
1. WHAT THE FLAG DOES (already wired in source — you only build & run)
===============================================================================
`public const bool DiagNoiseBroadband` (line 209). When true it forces `weight = 1f` in BOTH
RidgedMultifractal01 (line 1098) and ErodedRidge01 (line 1131) → every octave contributes at full
designed amplitude → broadband multifractal (many scales, irregular blobs) instead of a
few-octave-dominated coherent ridge field. Everything else identical.
All R9/R10 feature flags are RESTORED to false → features are back, real terrain, not the bald diagnostic.
Strata + plate stay OFF → they are CONSTANT across both passes, so the ONLY variable between BASE and
BROAD is the weight feedback. RULE 1 honored.

===============================================================================
2. TWO-PASS BUILD PROTOCOL (exact steps — do not deviate)
===============================================================================
PASS 1 (BASE):
  • Confirm line 209 is `= false` and line 182 sentinel ends `_BASE_weightFeedbackLIVE`.
  • Kill Unity.exe. Delete Library/ScriptAssemblies, Library/Bee, Library/BurstCache. (Kills stale DLLs.)
  • Batchmode atlas run, exit 0. Confirm atlas_report.txt line 2 == the BASE sentinel. Non-match → stale → redo.
  • COPY the entire atlas output folder to a sibling named `atlas_R11_BASE` so pass 2 cannot overwrite it.
PASS 2 (BROAD):
  • Edit line 209 to `= true`. Edit line 182 sentinel to `SENTINEL_R11_2026-07-22_BROAD_weightEQ1`.
  • Clean build again (same wipe). Batchmode run, exit 0. Confirm line 2 == the BROAD sentinel.
  • COPY the atlas output folder to `atlas_R11_BROAD`.
Now you have two full image sets that differ by exactly one variable.

===============================================================================
3. WHICH IMAGES EXIST
===============================================================================
Per pass: 5 points {P1_origin, P2_near, P3_west, P4_far, P5_deepfar} × 3 scales {10km, 1km, 200m}.
For the visual audit you MUST open, per pass:
  • every `_2_hillshade.png`  (15 per pass)
  • every `_1_height.png`     (15 per pass)
= 30 images per pass × 2 passes = 60 images total. Not a sample. All 60.

===============================================================================
4. THE MANDATORY INDEPENDENT VISUAL AUDIT — THIS IS THE POINT OF THE ROUND (Director's direct order)
===============================================================================
For EACH of the 60 images you will do this, IN THIS ORDER, and it goes into the report verbatim:

STEP A — OBJECTIVE DESCRIPTION FIRST (your vision model, NO scripts, NO numbers from the report).
   Look at the actual pixels and describe, in 2–4 honest sentences, LITERALLY what you see. You are
   forbidden to mention the hatching index or any parsed metric in Step A. Describe:
     - Overall landform: is it a smooth ramp, a basin, cliffs, hills, cones, a plain?
     - LINES: are there any 1-pixel-thin straight or curved bright/dark hairlines crossing the tile?
       Where, what orientation, how many?
     - REPEAT PATTERN: are there regular, evenly-spaced parallel grooves/ridges (fingerprint/zebra)?
       At what orientation? Roughly what spacing (fraction of tile width)?
     - RICHNESS: does the relief look geologically RICH and varied (multiple scales of detail, natural
       irregularity), or POOR (one repeating motif, plastic, monotonous, obviously procedural)?
     - ARTIFACTS: any other “shit” — blockiness, grid squares, radial rings, terracing, pinch points,
       symmetry that shouldn't be there.
   Write it as: `<filename> | <objective description>`. If you genuinely cannot open an image, write
   `<filename> | NOT VIEWED — reason`. Do NOT fabricate a description. A fabricated "looks clean" that
   the Director then sees is dirty is the worst possible outcome and has happened before.

STEP B — ONLY AFTER Step A for that image, you may add ONE subjective line: your opinion — does this
   tile PASS the Director's taste (rich, real, no lines, no fingerprint) or FAIL, and why. Keep A and B
   visibly separate. Opinion never contaminates the objective description.

Do this for all 60. Yes it is long. That is the job. The Director will spot-check your Step-A lines
against the real PNGs; if your words don't match his eyes, the round is a failure regardless of numbers.

===============================================================================
5. THE ZOOM-SPACING DIAGNOSTIC (one crucial cross-image observation — resolve it explicitly)
===============================================================================
For a point that shows stripes at multiple scales (P3_west is the strongest: 10km, 1km, 200m), compare
the STRIPE SPACING across the three scales BY EYE:
  • If the stripes are spaced roughly the SAME in PIXELS at 10km, 1km and 200m (i.e. the pattern looks
    identical regardless of how far you zoomed) → the stripes are NOT terrain; they are a RENDER or
    METRIC artifact (hillshade normal / slope calc / residual blur in GeologyAtlasTask.cs). This would
    mean we've been fixing the wrong file for 11 rounds. FLAG THIS LOUDLY if you see it.
  • If the stripe spacing SCALES with zoom (wide bands at 10km, finer detail resolves at 200m) → it is
    real terrain geometry, and the BASE→BROAD comparison tells us if broadband fixes it.
State your finding on this in plain words. It may be the most important sentence in the report.

===============================================================================
6. NUMBERS (secondary this round — eyes are the authority)
===============================================================================
Produce a 15-tile worst-first hatching table for BASE and for BROAD side by side (point|scale|BASE_hatch|
BROAD_hatch|delta|BASE_angle|BROAD_angle|slope70+%). We EXPECT BROAD to lower hatching if the hypothesis
holds. But if the images say otherwise, the images win. Never report a PASS from the number alone.

===============================================================================
7. VERDICT + DEBATE (after all 60 objective descriptions)
===============================================================================
Give a written verdict grounded in the IMAGES:
  • BROAD visibly removes fingerprint AND seam hairlines, relief looks richer → hypothesis SUPPORTED;
    next we keep broadband, re-enable strata/plate, round crests, tune amplitude to taste.
  • BROAD removes fingerprint but hairline seams REMAIN → weight-collapse was the fingerprint; seam is
    separate (next suspects: domain-warp coherence at lines 638–650, or province lattice floor line 492).
  • BROAD changes little, OR the zoom-spacing test says "render artifact" → I (Claude) am wrong about the
    layer; we pivot to auditing GeologyAtlasTask.cs render/metric math. Say so with the image evidence.
And answer directly: from YOUR own reading of lines 1085–1140 and YOUR own eyes on the 60 images, do you
AGREE the weight-feedback is the root, or do you see a better explanation? Make the case with filenames
and line numbers. Truth is born in the argument; this round it is settled by the Director's eyes.

Deliver: both sentinels, all 60 Step-A/Step-B lines, the zoom-spacing finding, the BASE/BROAD table,
the verdict, and your argument. Not "done, looks good." Evidence and an argument.
