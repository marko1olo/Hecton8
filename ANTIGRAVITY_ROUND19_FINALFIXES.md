\# ANTIGRAVITY R19 — FINAL 2 SURVIVORS

\*\*Brief path:\*\* `C:\\hades\\Hecton8\\ANTIGRAVITY\_ROUND19\_FINALFIXES.md`

\*\*Target file:\*\* `C:\\hades\\Hecton8\\Assets\\\_Project\\Scripts\\World\\WorldMacroGeologyFields.cs`

\*\*Expected sentinel after edit:\*\* `SENTINEL\_R19\_2026-07-23\_2survivors\_fixed`



\---



\## MANDATORY BUILD PROTOCOL (do this BEFORE every atlas run)



1\. Kill all Unity.exe processes

2\. Delete `Library/ScriptAssemblies`, `Library/Bee`, `Library/BurstCache`

3\. Run atlas, then verify: line 2 of `atlas\_report.txt` must read exactly

&#x20;  `BUILD SENTINEL (must match source): SENTINEL\_R19\_2026-07-23\_2survivors\_fixed`

&#x20;  If it does not match → STALE BUILD. Discard all images. Repeat from step 1.



\---



\## CHANGE 1 — Sentinel bump (line 182)



```diff

\-        public static string BuildSentinel => "SENTINEL\_R18\_2026-07-23\_3survivors\_fixed";

\+        public static string BuildSentinel => "SENTINEL\_R19\_2026-07-23\_2survivors\_fixed";

CHANGE 2 — Canyon floor seam (Survivor 1, visible on P3\_west\_200m \_stage6\_hillshade.png)

Root cause: canyonFloor = math.smoothstep(0.70f, 0.98f, dendritic) — narrow 0.28-wide

ramp creates near-vertical canyon walls → 1px razor seam on hillshade.

R18 fixed canyonRim but left canyonFloor untouched.



Location: \~line 877, inside // --- B2: RIVERS \& DENDRITIC CHANNELS ---





\-                float canyonFloor = math.smoothstep(0.70f, 0.98f, dendritic);

\+                float floorDither = FractalSimplexNoise01(warpedPos \* 0.0035f + new float2(5.5f, -8.8f), seed ^ 0xF3A1B2C4u, 2) \* 0.06f;

\+                float canyonFloor = math.smoothstep(0.40f, 0.99f, dendritic + floorDither);

Why it works: Range 0.40→0.99 gives gradual walls instead of vertical. floorDither

breaks the iso-contour of smoothstep with spatial noise, eliminating the 1px seam line.



CHANGE 3 — Strata stripe on trench wall (Survivor 2, visible on P5\_deepfar\_200m \_stage7\_hillshade.png)

Root cause: P5 has Trench=100%, HardRock=56.5%. strataStrength (line 951) does not

suppress strata on trench walls → DiagStrataNonPeriodic branch fires:

sin(strataPhase \* 4π) produces 4–9 visible periods at 200m scale → 65° stripe aligned

with dominant noise gradient at P5 coordinates.

Strata on oceanic trench walls is geologically nonsensical (no sedimentary layering there).



Location: line 951, inside // --- B4: STRATIFICATION ---





\-                float strataStrength = math.saturate(hardRockMask \* 0.8f + recipe.Strata \* 0.8f - (1f - slopeProxy) \* 0.7f - volcanoMask \* 1.2f);

\+                float strataStrength = math.saturate(hardRockMask \* 0.8f + recipe.Strata \* 0.8f - (1f - slopeProxy) \* 0.7f - volcanoMask \* 1.2f - trenchMask \* 0.9f);

Why it works: trenchMask \* 0.9f drives strataStrength to zero wherever Trench≥1.0

(P5). Affects both DiagStrataNonPeriodic and the legacy else branch simultaneously.

Does not touch continental tiles (P1–P4 have Trench=0.0–0.0).



VISION AUDIT PROTOCOL (mandatory — no scripts, no abstract PASS)

After a confirmed-fresh build, open and describe EVERY image below.

For each image write: what you literally see (shapes, lines, gradients, artifacts).

Then write CLEAN or DEFECT. If you did not open an image, write NOT VIEWED.



Priority images (check these first):

Image	What to look for

P3\_west\_200m\_stage6\_hillshade.png	Survivor 1 target. Should show smooth canyon walls with no 1px razor seam.

P5\_deepfar\_200m\_stage7\_hillshade.png	Survivor 2 target. Should show no diagonal 65° stripe in left 40% of image.

Full audit — all 15 points × stage6 + stage7 hillshades:

P1\_origin\_200m, P1\_origin\_1km, P1\_origin\_10km

P2\_near\_200m, P2\_near\_1km, P2\_near\_10km

P3\_west\_200m ← PRIORITY, P3\_west\_1km, P3\_west\_10km

P4\_far\_200m, P4\_far\_1km, P4\_far\_10km

P5\_deepfar\_200m ← PRIORITY, P5\_deepfar\_1km, P5\_deepfar\_10km



For each: open \_stage6\_hillshade.png AND \_stage7\_hillshade.png. Describe. Verdict.



Also check full hillshades (not stage dumps):

All 15 \*\_2\_hillshade.png files. Describe. Verdict.



WHAT NOT TO TOUCH IN R19

Stage 2 seams on P1/P2/P3 10km (continentality smoothstep(0.40, 0.66)) — next round

Stage 4 overlay artifacts — after Stage 2 is fixed

Any other flag or formula not listed above

KNOWN METRIC RULE

Hatching Index is PERMANENTLY UNRELIABLE at 1km/200m scale (R13 proved a smooth

monotonic ramp scores 5–8 with zero visible stripes). Ignore all numeric hatch values.

Visual inspection of PNG is the sole authority.



FAILURE HISTORY REMINDER

Antigravity has a history of reporting "100% PASS" without opening images.

Worst-tile-first audit is mandatory. If P3\_west\_200m stage6 or P5\_deepfar\_200m stage7

are NOT described with literal visual content, the report is invalid.
