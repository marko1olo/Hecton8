# ANTIGRAVITY — ROUND 10 ISOLATION BRIEF (dactyloscopy: the REAL root is slope-gated, not a feature)
# Prereq reading: ANTIGRAVITY_R9_KICKOFF.md (core rules R1–R9) + this file. Live source is truth.
Sentinel target this run: `SENTINEL_R10_2026-07-22_slopeHFisolation`
Author: Claude (architect). Mode: brutal honesty. Report EVIDENCE + ARGUMENT, never "done/100%/GOLD".

===============================================================================
0. YOUR R9 WORK — WHAT WAS RIGHT, AND THE ONE THING YOU GOT WRONG (learn this pattern)
===============================================================================
RIGHT: you parsed all 15 tiles, gave peak angles, and your Q1–Q5 answers were correct — especially
Q1 (crest is a LINE, and steep monotonic slope compresses isolines into parallel zebra) and Q4
(octave-0 rotation is a fixed per-seed constant → imposes a world-locked stripe direction). R9 cleanly
EXONERATED trench, volcano AND fault: hatching moved ≤0.11 and peak angles did not budge one degree.
Excellent isolation. That is exactly how this job is done.

WRONG — and this is the SAME mistake you made with the volcano: you proposed the R10 culprit
(ridgeBelt / billowMountains, lines 661–665) WITHOUT checking the mask coverage on the failing tiles.
I checked it for you. Here is the killer fact from the R8/R9 report:

   TILE        HATCHING   Ridge%mask
   P3 200m      3.89 (WORST)   0.0     ← ZERO ridge, dominant mask is Basin 77%
   P2 200m      2.42           6.7
   P4 200m      2.32         100.0
   P5 200m      1.89          42.2

The WORST tile (P3 200m = 3.89) has NO ridge relief at all. If ridgeBelt/billowMountains drew the
zebra, P3 would be clean. It is the worst. Therefore ridge relief CANNOT be the general root — a term
that is ABSENT on the worst tile cannot cause the worst tile's artifact. Your R10 flag would have moved
nothing on P3 and we'd have burned another round. RULE for you now, permanent:

   **Before naming ANY term as a suspect, pull its mask/coverage on the WORST failing tile from
   atlas_report.txt. If the term's coverage is ~0 there, it is disqualified. No exceptions.**

===============================================================================
1. WHAT ACTUALLY CORRELATES WITH THE BUG (the real lead — verify it yourself)
===============================================================================
I cross-checked EVERY mask against hatching across the failing tiles. NO feature mask tracks the bug —
Ridge swings 0→100, Basin 5→85, Shelf 0→92 while hatching stays high. Exactly ONE quantity tracks it:
STEEP-SLOPE FRACTION.

   TILE       hatch   slope 40-70%   slope 70%+
   P3 200m    3.89       67.2          26.2
   P2 200m    2.42       31.6          67.2
   P4 200m    2.32       34.1          61.4
   P5 1km     1.88       ~60           ~24
   P1 200m    1.27(OK)   45.5          45.4   ← also steep, but CLEAN — see note

So the zebra lives on STEEP slopes regardless of which province/feature is there. This matches your own
Q1/Q4 answers: a high-frequency term written onto a steep monotonic slope collapses into parallel
grooves oriented by the octave-0 world axis. The culprit is therefore a term that writes on ALL steep
ground via slopeProxy/hardRock — NOT a per-feature generator.
(Note P1 200m is steep yet clean at 1.27: its std is only 42m — the steepness there is small-amplitude
micro-relief, not a big monotonic ramp. P3/P4 have std 97–130m: big ramp + HF noise = zebra. This
nuance is exactly what R10 must resolve — is it the HF term, or the ramp it rides, or both?)

PRIME SUSPECTS for R10 (all slope/hardrock-gated, all write everywhere steep — verify each one's
presence on P3 200m yourself before trusting me):
  S1. B9 mesoFractureDelta — lines 879–883:
        mesoFractureMask = saturate(hardRock*0.8 + slopeProxy*0.4) * (0.5 + slopeProxy*0.9)
        depth += (RidgedMultifractal01*0.6 + RidgedMultifractal01*0.4 …)*55m * mesoFractureMask
      RidgedMultifractal01 crest grain (raw n=n*n, the SHARP one) × steep-slope mask = textbook zebra
      on every steep face. THIS IS MY #1 SUSPECT. Check hardRock% on P3 200m (report says HardRock=1.7
      — hmm, low; so on P3 it's carried by slopeProxy*0.4, still nonzero on 93% steep ground).
  S2. TIER 4 talus — lines 886–890: BillowNoise01 * up to 15m * talusMask (slopeProxy-gated). Smaller
      amplitude, but billow is also directional under octave-0 rotation.
  S3. geologicalNoise — lines 649–650: (noise-0.5)*160m*(1-abyssPlain*0.5). Writes almost everywhere,
      big 160m amplitude. On a plain it's blobs; on a steep ramp it could stripe.
  S4. THE RAMP ITSELF — the low-freq base that MAKES the steep monotonic slope (mountainField line 625,
      billowMountains 655, or the shelf/continent slope). If the zebra is really the HF term riding the
      ramp, then removing the HF term (S1–S3) kills it and we keep the ramp. If removing all HF still
      leaves stripes, the RAMP's own isolines are the zebra and we must break the ramp monotonicity.

===============================================================================
2. BUT FIRST — WE CANNOT FULLY TRUST THE HATCHING NUMBER. FIX THE METER. (I read its code; you didn't)
===============================================================================
I read the hatching metric in GeologyAtlasTask.cs (nobody had). It computes:
   residual = height − boxBlur13x13(height)        // lines 172–201, BlurR=6, separable BOX filter
   hatchIndex = peak(orientation-histogram of ∇residual) / mean   // lines 236–278
Two facts:
 (a) GOOD: it measures anisotropy of the HIGH-FREQUENCY residual (after removing a 6px-radius blur) —
     that is the correct thing to measure for "zebra on top of a ramp". The peak ANGLES are real signal
     (105/115/135° are not axis-aligned, so they're genuine terrain direction, not a meter artifact).
 (b) SUSPECT: a separable BOX blur has square 0°/90° axes and injects mild axis-aligned anisotropy into
     the residual on steep tiles. So the ABSOLUTE magnitude (3.89 etc.) may be inflated. We must not
     trust the exact value until the blur is isotropic.
ACTION (do this BEFORE the isolation run so R10 numbers are honest):
   Replace the two-pass box blur (lines 172–201) with a proper GAUSSIAN blur (separable Gaussian is
   fine and stays isotropic): precompute a 1D Gaussian kernel sigma≈BlurR/2 (radius 3*sigma), apply
   horizontally into blurTmp then vertically into residual. Keep everything else identical. This makes
   the residual's high-pass isotropic so the hatch index reflects TERRAIN anisotropy only.
   Re-run the R8-equivalent baseline (all diag flags as they are now) ONCE to get corrected baseline
   numbers, THEN do the isolation run. Report BOTH: "box-blur baseline" vs "gaussian-blur baseline" so
   we see how much of the 3.89 was meter artifact vs real zebra. This is a measurement-integrity fix,
   RULE 7. Do not skip it — an untrusted meter is why we can't tell if a fix worked.

===============================================================================
3. THE R10 ISOLATION FLAGS (I will add these to WorldMacroGeologyFields.cs; you build+run)
===============================================================================
One variable per measurement (RULE 1). Add four independent flags, each defaulting true THIS run:
  DiagMesoFractureOff  → skip the line 883 `depth += mesoFractureDelta …` write        (tests S1)
  DiagTalusOff         → skip the line 890 talus `depth += …` write                     (tests S2)
  DiagGeoNoiseOff      → skip the line 650 `depth += (geologicalNoise-0.5)*160 …` write  (tests S3)
  (S4 the ramp is NOT flagged yet — it's the fallback: if S1–S3 all OFF and stripes REMAIN, the ramp
   itself is the zebra and R11 breaks ramp monotonicity. If stripes GO, one of S1–S3 is the root.)
Because these are all "everywhere-steep" terms, they overlap on the same tiles, so this run tells us
"is the zebra an additive HF term at all (S1|S2|S3) or the ramp (S4)". If it's an HF term, R11 splits
S1/S2/S3 one at a time to name the exact one.

===============================================================================
4. EXECUTION + REPORT CONTRACT
===============================================================================
1. Gaussian-blur meter fix (§2). Clean build (kill Unity.exe; wipe Library/ScriptAssemblies, Bee,
   BurstCache). Batchmode, exit 0.
2. Confirm report line 2 sentinel == the R10 string. Non-R10 → stale → redo. (Note: BuildSentinel is
   now a static property per your R9 fix — good, that kills the const-inlining stale trap. Confirm it
   still prints.)
3. Produce TWO 15-tile worst-first tables:
     Table A: gaussian-meter baseline (all current flags) vs old box-meter R8 — shows meter-artifact delta.
     Table B: gaussian-meter R10 isolation (S1+S2+S3 OFF) vs gaussian baseline — shows the real drop.
   Columns: point|scale|baseline_hatch|R10_hatch|delta|angle|slope70+%|PASS(<1.8)?
4. For the WORST 5 tiles: open the hillshade AND slope PNGs. State rings/zebra/clean + whether the
   visible stripe angle matches the reported peak. Eyes vs number must agree; number wins on conflict.
5. VERDICT per decision table:
     - S1+S2+S3 OFF and P3/P2/P4 200m drop <1.8 → the zebra is an additive HF term; R11 splits which.
     - stripes REMAIN with S1+S2+S3 OFF (angles unchanged) → S4: the RAMP isolines are the zebra;
       R11 must break ramp monotonicity (domain-warp the base slope / add cross-cutting relief), NOT
       add more HF. Say this explicitly.
6. ARGUE: do you agree S1 (mesoFracture RidgedMultifractal×slope) is the prime suspect? If your own
   code read + mask check points elsewhere, make the case with line numbers and the WORST-tile mask
   coverage (per the new permanent rule in §0). I will push back.

Return the two tables, the meter-artifact finding, per-point verdict, and your argument. Not "done".
