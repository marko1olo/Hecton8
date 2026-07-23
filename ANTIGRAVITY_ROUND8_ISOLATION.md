# ANTIGRAVITY — ROUND 8: ISOLATION RUN + SELF-RECHECK AGAINST DIRECTOR'S COMPLAINTS

Read `ANTIGRAVITY_ADDENDUM_ENFORCEMENT.md` first (honesty contract, binding). Then this, fully.

## 0. WHY YOU ARE ON A LEASH RIGHT NOW

Three rounds in a row you reported defects as "REMOVED / 100% / PASSED". The Director looked at the
REAL images and confirmed EVERY time that the seam lines and the dactyloscopy (fingerprint/concentric
rings) were STILL FULLY PRESENT. In Round 5 you even cited files named `*_fix.png` that the atlas
never writes — fabricated inspection. This is unacceptable and it stops now.

We do not yet even know if Unity is compiling the new code or running a stale cached assembly. This
round settles that with a sentinel, and isolates the ring/seam source by turning the two suspect
mechanisms OFF. The terrain will look UGLIER this round — features are disabled ON PURPOSE. That is
correct. This is a diagnostic, not a beauty pass. Do not "fix" the ugliness.

## 1. MANDATORY CLEAN REBUILD (this is likely why "nothing changes")

An open Unity Editor keeps old compiled DLLs in memory and may not reload them for a batchmode run.
So:
1. Ensure NO Unity process is running (kill Unity.exe if open). Confirm.
2. Delete these folders to force a from-scratch compile:
   - `C:\hades\Hecton8\Library\ScriptAssemblies`
   - `C:\hades\Hecton8\Library\Bee`
   - `C:\hades\Hecton8\Library\BurstCache`
3. Launch Unity in batchmode fresh and run menu `Hecton8/Diagnostics/Geology Atlas`.

## 2. THE SENTINEL — QUOTE IT VERBATIM, FIRST THING

Claude added `WorldMacroGeologyFields.BuildSentinel` and the atlas prints it as the 2nd line of
`atlas_report.txt`. The expected exact value this round:

    SENTINEL_R8_2026-07-22_ISOLATION_strataOFF_plateOFF

**Open `atlas_report.txt` and quote its first TWO lines verbatim as the very first thing in your report.**
- If line 2 does NOT contain `SENTINEL_R8_..._ISOLATION_strataOFF_plateOFF` → Unity ran a STALE build.
  Say so loudly. That means every prior "verified" report was against old code. Report it and stop.
- If it matches → the new code is confirmed live. Proceed.

## 3. WHAT THIS BUILD CHANGES (isolation flags, in WorldMacroGeologyFields.cs)

- `DiagStrataContourOff = true` → the entire B4 stratification block is skipped. `frac(depth/scale)`
  is the ONLY thing in the evaluator that can draw iso-contour lines (fingerprints on slopes, rings
  on domes). Noise makes blobs, not lines. If the rings vanish → strata was the ring source.
- `DiagPlateSeamOff = true` → `plateRidgeMask`, `plateTrenchMask`, `plateEdgeMask` forced to 0. This
  is the Voronoi F2−F1 crease suspected of drawing the 1px seam line. If the seam vanishes → confirmed.

## 4. GENERATE + PROVE FRESH

Delete all old atlas PNGs + atlas_report.txt (confirm count 0). Record T_start. Run the atlas.
`ls -la` and paste timestamps proving all files are newer than T_start. Compress PNGs ≤768px.

## 5. VISUAL VERDICT — DIRECTOR'S EXACT COMPLAINTS, YOUR OWN EYES, REAL FILENAMES ONLY

The Director's literal complaints you must check against the images (do not paraphrase away):
- "шов / однопикcельная линия НИКУДА не делаетcя" — the 1px seam line (diagonal on height, sharp
  line on slope) across 10km/1km cells.
- "дактилоcкопия тоже никуда не жеваетcя" — fingerprint / concentric-ring / zebra contour pattern,
  worst on P5 and P1 1km/200m.

For each, cite the exact real filename (must appear in your `ls`) and state literally:
  a. SEAM: on P1_origin_10km_1_height.png, P1_origin_10km_2_hillshade.png, P4_far_10km_2_hillshade.png
     — is the 1px seam line GONE now that plate relief is off? If a line remains, describe where and
     whether it follows a province border (cross-check 7_province if present).
  b. RINGS: on P5_deepfar_1km_2_hillshade.png, P5_deepfar_200m_2_hillshade.png, P1_origin_1km_2_hillshade.png,
     P1_origin_200m_2_hillshade.png — is the fingerprint/zebra/concentric pattern GONE now that strata
     is off? Any residual contour lines anywhere? Which file, on flat or steep ground?
  c. If EITHER still remains with both mechanisms off, that is critical new information — it means the
     source is something else (mesa min(), dune sin(), province blend, continentality/shelf smoothstep
     edge). Say so and point at the most likely remaining candidate.

## 6. SELF-RECHECK (mandatory — the Director explicitly ordered this)

After your first-pass report, DO A SECOND CRITICAL LOOK, adopting the Director's stance ("I assume
you're lying until the pixels prove otherwise"). Re-open the SAME images and answer:
- Did I actually SEE the seam gone, or did I assume it because the flag is set? Prove with the file.
- Zoom into corners and borders specifically — the Director keeps finding 1px lines I glossed over.
- Is there ANY thin line, any repeating contour, any ring, anywhere in any of the 15 hillshade files?
  Go cell by cell. List every one you find with its filename, or state "none found in <file>".
- Contradict your own first pass if the pixels disagree with it. Honesty over consistency.

## 7. REPORT FORMAT
1) The verbatim 2 lines of atlas_report.txt (sentinel check) FIRST.
2) Freshness proof (delete + timestamps).
3) Sections A–H per the enforcement addendum.
4) Section 6 self-recheck findings (may contradict your first pass — that's expected and good).
No "100%". No GOLD. No invented filenames. If you cannot open an image, say so, do not guess.
