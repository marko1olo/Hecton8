\# ANTIGRAVITY — ROUND 17: STAGE-LOCALIZED FIXES (четыре дефекта, четыре хирургичеcких правки)

\# Prereq: ANTIGRAVITY\_R9\_KICKOFF.md core rules R1–R9. Live source is truth. EVIDENCE only, never "done/100%".

\# Sentinel: SENTINEL\_R17\_2026-07-23\_stageFixes\_4defects

\# Author: Claude (architect). Agent has FULL EDIT RIGHTS on WorldMacroGeologyFields.cs.

\# Agent: read this file completely before touching any code. Then act autonomously.



===============================================================================

0\. WHAT WE KNOW — STAGE-DUMP EVIDENCE (Director's eyes, not Antigravity's claims)

===============================================================================

Stage-dump atlas (R16) was reviewed by the Director. His eyes are the authority.

Four defects were localized to four distinct pipeline stages:



&#x20; DEFECT A — 1px hairlines (seam lines)

&#x20;   Born at: STAGE 2 (continental relief block, lines \~695–715)

&#x20;   Confirmed on: P5, P1, P2, P3 at all scales

&#x20;   NOT stage1 (shelfMask) as Antigravity claimed — Director saw them appear at stage2.

&#x20;   Mechanism: mountainUplift uses RidgedMultifractal01 → n=1-|snoise| → sharp crest line

&#x20;   at every snoise zero-crossing = 1px bright/dark hairline on hillshade.



&#x20; DEFECT B — large dactyloscopy / parallel grooves (10km scale)

&#x20;   Born at: STAGE 5 (fold sin() corrugation)

&#x20;   Confirmed on: P5\_10km, P2\_10km

&#x20;   Mechanism: depth -= foldAsymmetry\*240 where foldAsymmetry = sin(dot(warpedPos,foldAxis)\*0.0012)

&#x20;   = regular parallel waves locked to a world direction = textbook fingerprint.



&#x20; DEFECT C — diagonal zebra stripes \~65° (200m scale, survived fold-off in R15)

&#x20;   Born at: STAGE 7 (strata frac() corrugation)

&#x20;   Confirmed on: P5\_200m

&#x20;   Mechanism: strata uses math.frac(depth/strataSpacing) → periodic sawtooth in depth

&#x20;   space → regular parallel isolines at whatever angle the depth gradient runs.

&#x20;   This is NOT the fold — it survived DiagFoldsDunesOff=true.



&#x20; DEFECT D — sharp mask overlay / abrupt transitions (1km and 200m)

&#x20;   Born at: STAGE 4 (trench/fault/basin masks)

&#x20;   Confirmed on: multiple tiles at 1km/200m

&#x20;   Mechanism: basinMask and faultMask use hard smoothstep thresholds stepping large

&#x20;   depth amplitudes → near-1px height discontinuity visible as overlay seam.



CRITICAL META-LESSON: All four defects coexist. Fixing one never killed the others.

That is why 16 rounds of single-feature isolation failed. This round fixes all four.



===============================================================================

1\. THE FOUR FIXES — EXACT CODE CHANGES (agent implements all four)

===============================================================================



\--- FIX A: Replace ridged crest with smooth fBm in mountainUplift ---

File: WorldMacroGeologyFields.cs

Location: RidgedMultifractal01 and ErodedRidge01 calls that feed mountainUplift

&#x20;         (search for "mountainUplift" and trace back to the ridged generators)



Current behavior: n = 1 - abs(snoise) → sharp crest at zero-crossing

Fix: Replace the ridged transform with plain billow/fBm:

&#x20; n = snoise \* 0.5f + 0.5f  (no abs, no inversion, no crest)

&#x20; weight = 1f  (broadband, all octaves)



This rounds the mountain crests from knife-edges to natural rounded ridges.

Terrain will look softer/rounder — that is correct for this diagnostic.

Do NOT change the amplitude or frequency. Change ONLY the n formula and weight.



Implementation: Add a new diagnostic flag at the top of the Diag block:

&#x20; public const bool DiagRidgedAsFbmMountain = true;

Then in RidgedMultifractal01 and ErodedRidge01, guard the n formula:

&#x20; float n = DiagRidgedAsFbmMountain

&#x20;     ? (snoise \* 0.5f + 1f)   // fBm: smooth, no crest

&#x20;     : (1f - math.abs(snoise)); // original ridged



\--- FIX B: Replace fold sin() with domain-warped non-periodic fold ---

File: WorldMacroGeologyFields.cs

Location: the fold sin write (search "foldAsymmetry" and "sin(dot(warpedPos")



Current behavior:

&#x20; foldAsymmetry = fn of sin(dot(warpedPos, foldAxis) \* 0.0012f + noise)

&#x20; depth -= foldAsymmetry \* 240f \* continentality \* ...



Fix: Replace the sin() with a FractalSimplexNoise01 sample along the fold axis:

&#x20; // Non-periodic fold: same spatial scale, no regular repeat

&#x20; float foldPhase = FractalSimplexNoise01(

&#x20;     warpedPos \* 0.0012f + foldAxis \* 3.7f,

&#x20;     seed ^ 0xF01D5EEDu, 3) \* 2f - 1f;

&#x20; float foldAsymmetry = foldPhase \* (0.3f + recipe.FoldIntensity \* 0.7f);

&#x20; depth -= foldAsymmetry \* 240f \* continentality \* (1f - abyssPlainMask);



The spatial frequency (0.0012) stays the same so fold scale is preserved.

The sin() is gone → no regular repeat → no dactyloscopy.

Add flag: public const bool DiagFoldNonPeriodic = true;

Guard the foldAsymmetry line with this flag.



\--- FIX C: Replace strata frac() with smooth bench approximation ---

File: WorldMacroGeologyFields.cs

Location: strata block (search "strataMask" and "math.frac")



Current behavior: uses math.frac(depth / strataSpacing) → sawtooth → regular isolines

Fix: Replace frac() with a smooth multi-scale noise that approximates bench layering

without periodic repeat:



&#x20; // Smooth strata: noise-modulated bench approximation, no frac()

&#x20; float strataPhase = FractalSimplexNoise01(

&#x20;     warpedPos \* (1f / strataSpacing) + new float2(33.1f, -17.4f),

&#x20;     seed ^ 0xBEEF1234u, 4);

&#x20; float strataWave = math.sin(strataPhase \* 6.2831853f \* 2f) \* 0.5f + 0.5f;

&#x20; // strataWave is smooth, non-repeating in world space (phase is noise-driven)

&#x20; float strataDisplace = strataWave \* strataAmplitude \* strataMask;

&#x20; depth += strataDisplace;



The key: strataPhase is noise, not depth/spacing → the "layers" are spatially

irregular, not depth-isolines. They look like real sedimentary benches.

Add flag: public const bool DiagStrataNonPeriodic = true;

Guard the strata displacement with this flag.



\--- FIX D: Soften basin and fault mask edges ---

File: WorldMacroGeologyFields.cs

Location: basinMask and faultMask smoothstep calls (stage 4 block)



Current behavior:

&#x20; faultMask = saturate(smoothstep(0.48f, 0.88f) \* ...)  ← range 0.40 = sharp

&#x20; basinMask = saturate((1-shelfMask) \* ...)              ← hard product



Fix: Widen the smoothstep range and add noise-based edge feathering:

&#x20; // Wider smoothstep = softer transition

&#x20; float faultMaskRaw = math.smoothstep(0.35f, 0.95f, faultNoise); // was 0.48-0.88

&#x20; // Feather with noise to break the hard line

&#x20; float faultEdgeNoise = FractalSimplexNoise01(

&#x20;     warpedPos \* 0.0031f + new float2(-5.5f, 12.3f), seed ^ 0xCAFEBABEu, 2);

&#x20; float faultMask = math.saturate(faultMaskRaw \* (0.7f + faultEdgeNoise \* 0.3f)

&#x20;     \* (1f - shelfMask \* 0.45f) + plateEdgeMask \* 0.34f);



&#x20; // Basin: add noise feather to the shelfMask edge

&#x20; float shelfEdgeNoise = FractalSimplexNoise01(

&#x20;     warpedPos \* 0.0018f + new float2(8.8f, -3.1f), seed ^ 0xDEADBEEFu, 2);

&#x20; float shelfMaskFeathered = math.saturate(shelfMask + (shelfEdgeNoise - 0.5f) \* 0.15f);

&#x20; float basinMask = math.saturate((1f - shelfMaskFeathered)

&#x20;     \* (1f - ridgeMask \* 0.78f) \* (1f - trenchMask \* 0.52f));



Add flag: public const bool DiagSoftMaskEdges = true;

Guard both changes with this flag.



===============================================================================

2\. BUILD PROTOCOL (exact steps — do not deviate)

===============================================================================

Step 1: Read WorldMacroGeologyFields.cs fully. Locate all four fix sites.

&#x20;       Verify current line numbers match the descriptions above.

&#x20;       If a line number is off, find the correct location by searching the

&#x20;       exact string patterns given above.



Step 2: Add the four new Diag flags to the Diag constants block (near line 209):

&#x20; public const bool DiagRidgedAsFbmMountain = true;

&#x20; public const bool DiagFoldNonPeriodic     = true;

&#x20; public const bool DiagStrataNonPeriodic   = true;

&#x20; public const bool DiagSoftMaskEdges       = true;



Step 3: Implement all four fixes as described. Each fix is guarded by its flag

&#x20;       so the original code path is preserved when flag = false.



Step 4: Update the BuildSentinel property to:

&#x20; SENTINEL\_R17\_2026-07-23\_stageFixes\_4defects



Step 5: Kill Unity.exe. Delete:

&#x20; Library/ScriptAssemblies

&#x20; Library/Bee

&#x20; Library/BurstCache

&#x20; Then run batchmode atlas build. Wait for exit 0.



Step 6: Confirm atlas\_report.txt line 2 == SENTINEL\_R17\_2026-07-23\_stageFixes\_4defects

&#x20;       If mismatch → stale build → redo wipe and rebuild. Do NOT proceed with

&#x20;       a stale build. This is non-negotiable.



Step 7: Copy atlas output folder → atlas\_R17\_fixes



===============================================================================

3\. MANDATORY VISUAL AUDIT — 30 IMAGES, ALL OPENED, NO FABRICATION

===============================================================================

Open EVERY \_2\_hillshade.png and \_1\_height.png for all 5 points × 3 scales = 30 images.

For EACH image, in this exact order:



STEP A — OBJECTIVE (vision only, NO numbers, NO hatching index):

&#x20; Describe in 2–4 sentences what you literally see:

&#x20; - Overall landform: smooth ramp, basin, cliffs, hills, cones, plain?

&#x20; - (A) HAIRLINES: any 1px straight/curved bright/dark lines crossing the tile?

&#x20;       Present or GONE vs R16? Orientation?

&#x20; - (B) DACTYLOSCOPY: regular evenly-spaced parallel grooves at 10km scale?

&#x20;       Present or GONE vs R16? Orientation?

&#x20; - (C) DIAGONAL ZEBRA: oblique stripes \~65° at 200m scale?

&#x20;       Present or GONE vs R16?

&#x20; - (D) MASK OVERLAYS: sharp abrupt transitions / overlay seams at 1km/200m?

&#x20;       Present or GONE vs R16?

&#x20; - RICHNESS: geologically rich and varied, or poor/monotonous/procedural?

&#x20; Write: `<filename> | STEP A: ... (A:...) (B:...) (C:...) (D:...)`

&#x20; If you cannot open an image: `<filename> | NOT VIEWED — reason`

&#x20; NEVER fabricate. A fabricated "looks clean" that the Director sees as dirty

&#x20; is the worst possible outcome and has happened before.



STEP B — OPINION (one line, after Step A):

&#x20; Does this tile PASS the Director's taste (rich, real, no lines, no fingerprint)?

&#x20; Or FAIL, and why? Keep A and B visibly separate.



===============================================================================

4\. HATCHING TABLE

===============================================================================

Produce the 15-tile worst-first hatching table:

&#x20; point | scale | hatch\_R17 | hatch\_R16\_delta | peak\_angle | slope70+%

Compare to R16 baseline. We expect:

&#x20; - Tiles that showed hairlines: hatch should drop toward 1.0–1.5

&#x20; - Tiles that showed dactyloscopy: peak angle should become less stable

&#x20; - Tiles that showed zebra: hatch at 200m should drop

If hatching RISES on any tile, flag it loudly and explain which fix may have

introduced a new artifact.



===============================================================================

5\. VERDICT — ONE OF FOUR OUTCOMES

===============================================================================

After all 30 Step-A descriptions and the table, state plainly:



&#x20; OUTCOME 1 (target): All four defects GONE on all tiles.

&#x20;   → Next: re-enable strata+plate (they were OFF since R8), tune amplitudes,

&#x20;     Director beauty pass.



&#x20; OUTCOME 2: Some defects gone, some remain.

&#x20;   → For each remaining defect: name the exact tile, scale, and which fix

&#x20;     failed to kill it. Propose the next surgical change with line numbers.



&#x20; OUTCOME 3: A fix introduced a NEW artifact.

&#x20;   → Describe it with filename and Step-A language. Propose the rollback or

&#x20;     adjustment. Do not hide regressions.



&#x20; OUTCOME 4: Build failed or sentinel mismatch.

&#x20;   → Report the exact error. Do not proceed to visual audit on a stale build.



===============================================================================

6\. DEBATE — MANDATORY

===============================================================================

From YOUR reading of the source code and YOUR eyes on the 30 images:

&#x20; - Do you agree that the four mechanisms described in §0 are the correct roots?

&#x20; - If any image contradicts the theory, the image wins — argue with filenames.

&#x20; - If a fix worked partially, explain why (e.g. foldAxis is per-region but

&#x20;   the noise replacement uses a global seed — does that matter?).

&#x20; - Honest disagreement is required. "Looks good" without evidence is a failure.



===============================================================================

7\. AGENT AUTONOMY RULES

===============================================================================

You have full edit rights on WorldMacroGeologyFields.cs.

You may:

&#x20; - Read any file in the project

&#x20; - Edit WorldMacroGeologyFields.cs to implement the four fixes

&#x20; - Run the Unity batchmode build

&#x20; - Open and describe atlas PNG images

&#x20; - Write the report



You must NOT:

&#x20; - Skip the sentinel verification (§2 Step 6)

&#x20; - Fabricate image descriptions (§3)

&#x20; - Report PASS from numbers alone without opening images

&#x20; - Proceed past a stale build

&#x20; - Change more than the four guarded fixes in one build

&#x20;   (one variable = one build = one verdict, Rule 1 from R9)



If you find that a fix site does not match the description (line numbers shifted,

code was already changed, flag already exists), adapt to the live source and

document what you found vs what was expected.



Deliver: sentinel confirmation, 30 Step-A/B lines with explicit (A)(B)(C)(D) tags,

hatching table, verdict, debate. Not "done". Evidence and argument.

