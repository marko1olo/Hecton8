# ANTIGRAVITY — ROUND 14: FULL SHIPPING TERRAIN, PURE-VISION AUDIT (the metric is dead, your eyes rule)
# Prereq: ANTIGRAVITY_R9_KICKOFF.md core rules R1–R9. Live source is truth. EVIDENCE only, never "done/100%".
Sentinel: SENTINEL_R14_2026-07-22_FULLTERRAIN_eyesOnly
Author: Claude (architect).

===============================================================================
0. WHAT R13 ACTUALLY PROVED (read this — it changes the whole game)
===============================================================================
Your R13 raw-probe run was valuable, but your VERDICT was wrong, and your own data refutes it. You
concluded "noise.snoise primitive is the root (45°/135° lattice)". It is not. Here is the proof, from
YOUR OWN R13 table and YOUR OWN Step-A descriptions:

  • BARE simplex (Probe 1) at 10km — where the tile contains ~9 noise periods and the metric is valid —
    scored hatching 1.25 / 1.28 / 1.34 / 1.40 / 1.35 on all five points. That is ISOTROPIC. PASS.
    If snoise had a strong directional 45°/135° artifact, it would show WORST exactly here. It doesn't.
  • BARE simplex at 200m scored 4.90 / 6.27 / 8.18 — YET you described those same tiles, with your own
    eyes, as "Monotonic smooth luminance ramp", "uniform gradient plane", "(Z: GONE)". You even wrote
    "hatching 4.90 is an artifact of measuring a uniform gradient vector." YOU SAW NO STRIPES AND THE
    NUMBER WAS 8.18.

Conclusion (forced by the data, not a guess): **the HATCHING METRIC is degenerate.** At any tile smaller
than ~1 noise period, the terrain is a smooth monotonic ramp; its high-pass residual is a tiny but
UNI-DIRECTIONAL curvature field; every residual-gradient points the same way; peak/mean of the direction
histogram blows up to 5–8. A flat smooth slope reads as "maximum anisotropy". That is the whole bug in
the ruler. The number tracks SCALE (1km/200m always high, 10km always ~1.0–1.4), not the noise type —
which is why it looked identical across Probe 1/2/3.

Downstream consequences we now accept:
  1. The primitive is EXONERATED as a striping source (10km isotropy is the proof).
  2. Every 1km/200m numeric FAIL across R8–R13 is SUSPECT — likely metric degeneracy, not visible zebra.
  3. The "P5_deepfar_1km flat-tile zebra hatch 4.33" Rosetta Stone that drove R13 was the SAME artifact:
     a smooth flat tile inflating the metric. It was a NUMBER, never a visible stripe.
  4. The 5-round paradox "removing a feature made hatching WORSE" is explained: removing a feature makes
     the field SMOOTHER/more monotonic → MORE metric degeneracy → higher number. The ruler lied.

BUT — two defects are REAL because the Director saw them with his own eyes over 13 rounds: the 1px
hairline "seam" lines and the "dactyloscopy" (fingerprint) pattern. Those are authority. They are NOT in
the bare probe (probe tiles are smooth/planar). So the real visible bug lives in the FULL FEATURE TERRAIN
— and for 13 rounds we hunted it with a broken ruler that fired on smooth ramps and misdirected us both.

R14 throws the ruler away. You will judge the TRUE, COMPLETE, SHIPPING terrain with your VISION only.

===============================================================================
1. WHAT CHANGED IN SOURCE (already wired — you only clean-build & run ONE atlas)
===============================================================================
Architect edits already applied to live source:
  • WorldMacroGeologyFields.cs: DiagRawProbe = 0 (probe OFF). ALL isolation flags = false, INCLUDING
    DiagStrataContourOff=false and DiagPlateSeamOff=false. Every feature, strata, plate, fold, dune,
    ridge, volcano, fault, warp is ON. This is the REAL terrain the player gets — nothing suppressed.
  • Sentinel bumped to SENTINEL_R14_2026-07-22_FULLTERRAIN_eyesOnly.
  • GeologyAtlasTask.cs: to save your laptop's time/power, the atlas now emits ONLY three maps per tile:
    _1_height.png, _2_hillshade.png, _3_slope.png. The structure/substrate/feature/province/detail maps
    are disabled this round. The report still prints; its HATCHING line is now tagged UNRELIABLE — ignore
    the number, it is kept only for the record.

===============================================================================
2. BUILD (RULE: clean, sentinel-verified, exit 0)
===============================================================================
Kill Unity.exe. Delete Library/ScriptAssemblies, Library/Bee, Library/BurstCache. Batchmode atlas run,
exit 0. Open atlas_report.txt and confirm line 2 == SENTINEL_R14_2026-07-22_FULLTERRAIN_eyesOnly. If it
does NOT match → Unity ran a stale assembly → redo the clean build. Do not proceed on a stale sentinel.

===============================================================================
3. THE AUDIT — YOUR EYES ON EVERY HILLSHADE + SLOPE + HEIGHT, ALL SCALES (the whole job)
===============================================================================
Images that exist: 5 points {P1_origin, P2_near, P3_west, P4_far, P5_deepfar} × 3 scales {10km,1km,200m}
× 3 maps {_1_height, _2_hillshade, _3_slope} = 45 images.

You OPEN AND LOOK AT EVERY ONE with your vision model. NO scripts, NO parsing, NO metric — the metric is
proven degenerate this round, it is BANNED from your reasoning. If you cite a hatching number as evidence
of striping in R14 you have failed the round. The only authority is what the pixels show.

For EACH of the 15 tiles (point×scale), write ONE block. Look at all 3 maps of that tile together
(hillshade for relief/lines, slope for where it's steep, height for the actual landform), then:

STEP A — OBJECTIVE, what you literally SEE (2–5 sentences, no opinion, no numbers):
   - Landform: basin / plain / cliffs / cones / ridges / dunes / ramp?
   - (S) SEAM: any 1-pixel-thin straight or curved bright/dark HAIRLINE crossing the tile? How many,
     what orientation, do they run the full tile or terminate? present / absent.
   - (Z) ZEBRA/DACTYLOSCOPY: any REGULAR, evenly-spaced parallel grooves or fingerprint ridges?
     orientation, rough spacing as a fraction of tile width. present / absent.
   - RICHNESS: does the relief look geologically rich and multi-scale, or poor/plastic/repetitive?
   - OTHER: blockiness, grid, radial rings, terracing, pinch points, unnatural symmetry.
Write: `<point>_<scale> | STEP A: … (S: …) (Z: …) | STEP B: …`
STEP B — one opinion line: does this tile PASS the Director's taste (rich, real, no lines, no fingerprint)
   or FAIL, and what specifically is wrong.
If you genuinely cannot open an image, write NOT VIEWED — reason. NEVER fabricate. The Director will
spot-check your Step-A words against the actual PNGs; if your words don't match his eyes, the round fails.

===============================================================================
4. LOCATE THE TWO REAL DEFECTS BY EYE — then argue the mechanism from SOURCE
===============================================================================
After the 15 blocks, answer directly, grounded in the images you just described:
 A) THE SEAM (1px hairlines): on WHICH tiles/scales do you actually SEE them? Are they present at 10km,
    or only 1km/200m? Straight or following contours? This tells us the mechanism. Then, from YOUR reading
    of the SOURCE, name the most likely code cause and cite file+line. Candidate mechanisms to check in
    WorldMacroGeologyFields.cs (verify each against where you SEE the seam):
      - RidgedMultifractal01 / ErodedRidge01: n = 1 - |snoise| makes a C1 crest LINE one sample wide.
      - any smoothstep MASK edge lerping a large depth amplitude across a narrow band (a near-step in
        height = a 1px hillshade hairline): shelfMask abyss↔shelf lerp, continentality lerp, mesa cap.
      - strata frac()/floor() writes (now ON again) — iso-contour lines on slopes.
 B) THE DACTYLOSCOPY (regular fingerprint): on WHICH tiles/scales? orientation world-locked across tiles
    or per-region? Then name the most likely SOURCE with file+line. Candidates: fold sin(dot(pos,axis)),
    dune sin(dot(pos,axis)), or per-octave rotation coherence in the multifractal.
 The rule stands: before you name a suspect, confirm that suspect's mask/term is actually ACTIVE on the
 worst tile where you SEE the defect. A term absent on the worst tile cannot be its cause.

===============================================================================
5. VERDICT + DEBATE
===============================================================================
 • State, in plain words, whether the REAL terrain (all features on) shows the seam and the dactyloscopy,
   and at which scales — by eye, not metric.
 • Give your best single hypothesis for EACH defect with file+line, and say how confident you are.
 • DEBATE me: I claim R13 proved the metric — not the primitive — was the problem, and that the true bug
   is a feature/warp interaction visible only at certain scales. If your eyes on the full terrain say
   otherwise (e.g. you now see the 45° grain even at 10km on the real terrain), SAY SO with filenames and
   argue it. The images win over both of us.

Deliver: the sentinel line, 15 STEP-A/B blocks with explicit (S)/(Z) tags, the two defect localizations
with file+line, the verdict, and your argument. Not "done". Evidence and an argument.
