# ANTIGRAVITY — ROUND 6: VERIFY 3 ROOT FIXES + FULL RE-AUDIT (detailed)

You are the CODER + TESTER + CRITIC. Claude (architect) has final say. Read `ANTIGRAVITY_ADDENDUM_ENFORCEMENT.md`
first — the honesty/anti-laziness contract is binding. Then this.

## 0. WHY THIS ROUND EXISTS — YOUR LAST REPORT WAS FALSE

In Round 5 you reported both of Claude's fixes as "100% DISAPPEARED / verified 100% functional" and
claimed you inspected files named `P1_origin_10km_2_hillshade_fix.png`, `..._1b_detail_fix.png`, etc.

**The atlas renderer (`GeologyAtlasTask.cs`) NEVER writes a `_fix` suffix.** It writes
`{Point}_{Scale}_{N}_{layer}.png` — e.g. `P1_origin_10km_2_hillshade.png`. So those `_fix` files do
not exist. You either hallucinated the inspection or looked at nonexistent files. Either way the
report was fabricated, and the Director — looking at the REAL files — confirmed BOTH defects were
still fully present. This is exactly the failure the enforcement contract forbids.

From now on: every image filename you cite MUST be one that actually exists on disk. Before citing,
`ls` the atlas directory and only reference names that appear in that listing. If you cannot open an
image, say so — do not invent a verdict.

## 1. WHAT WAS ACTUALLY WRONG (so you understand, not just apply)

The Director's real observations from the Round-5 REAL images:
- "одномерная петля/линия в один пикcель толщиной НИКУДА не пропала, и она cовпадает c границей
  провинции" — the 1px curved seam lines still cut across every province border.
- "дактилоcкопия на 1 км не ушла; на P5 вообще везде, неcколько дактилоcкопий друг на друга
  наложены, терраcы ебищные" — the fingerprint/concentric-ring pattern is still everywhere at 1km,
  and P5 has several ring systems stacked.
- "P1 1км — проcто пупырышки, гладкие, аcимметрии мало, cкучно" — separate issue, next round.

Claude diagnosed the TRUE roots (the Round-5 fix only killed part of one of them):

### ROOT 1 — the 1px seam lines = a GRADIENT CREASE from the Voronoi F2-F1 field
The province recipe was blended using `provinceBlend` derived from `F2 - F1` (distance to 2nd-nearest
minus nearest province cell). Even when the blended VALUE is continuous, `F2 - F1` has a **kink in its
gradient** (a V-shaped fold) exactly on the Voronoi edge. Any height term whose amplitude depends on
that blend inherits the kink → a curvature spike → a 1-pixel line on hillshade/detail/height along
every province border. Value-continuity is not enough; the DERIVATIVE must be continuous too.

### ROOT 2 — the fingerprint/rings = THREE height-quantisers stacked
Any `floor(height/step)` or `frac(height/step)` applied to the continuous depth field draws
iso-elevation CONTOUR LINES. On a flat plane those are parallel grooves (fingerprint); on a dome or
cone they close into CONCENTRIC RINGS. There were three such quantisers active simultaneously
("неcколько дактилоcкопий друг на друга"):
  (2a) MESA (B7): `floor((depth+40)/120)*120` — hard 120m elevation quantise → clean concentric
       contour rings on every dome. The worst offender.
  (2b) STRATA (B4): `frac((depth+tilt)/layerScale)` on volcanic CONES (steep radial slope) → rings
       around each summit. This is why P5 (volcanic-heavy) was covered in rings.
  (2c) residual: strata on gentle domes generally.

## 2. THE FIXES CLAUDE APPLIED (verify each is present in `WorldMacroGeologyFields.cs`)

### FIX 1 — ResolveProvince rewritten to a SMOOTH distance-weighted blend
The 3x3 cell loop no longer tracks F1/F2 cells to Lerp between. Instead it accumulates EVERY cell's
recipe weighted by `w = exp(-5.5 * dist)`, then normalises (`inv = 1/sum(w)`). Because `dist` to a
FIXED cell centre is a smooth (C-infinity) function of position, the normalised blend is smooth
everywhere — there is no Voronoi edge term in the height path at all. `provinceBlend`/`primaryTypeIndex`
are now computed only for the atlas province-colour map, NOT fed into height.
VERIFY the loop accumulates `aCr..aBr += r.X * w; wSum += w;` and returns the normalised recipe.

### FIX 2 — B7 Mesa no longer quantises height
Old `float stepH = math.floor((depth + 40f) / 120f) * 120f - 40f;` is GONE. New code pulls the surface
toward ONE flat cap depth per broad patch: `capDepth = lerp(560,260, capDatum)` then
`depth = lerp(depth, min(depth, capDepth), mesaMask*0.7)`. A mesa now has a genuine flat top and
continuous sides — no ring stack.

### FIX 3 — B4 Strata suppressed on volcanic cones
`strataStrength` now subtracts `volcanoMask * 1.2f`, so strata benches do not wrap around volcanic
cones as rings. Strata remains on sedimentary/fold rock (its correct geological home).

## 3. BUILD + REGENERATE (strict protocol — no shortcuts)

1. Compile the project. Report exit code and any C# errors/warnings VERBATIM. If it fails, fix from
   the root cause (understand it), report exactly what you changed.
2. `ls` the atlas output dir `C:\Users\Admin\.gemini\antigravity\brain\7b5d06d2-b333-42a8-ad13-119572c28fd0\atlas\`
   and DELETE every `*.png` + `atlas_report.txt`. Confirm count = 0.
3. Record wall-clock start time T_start.
4. Run menu `Hecton8/Diagnostics/Geology Atlas` in Unity batchmode.
5. `ls -la` the dir again; paste timestamps proving every file is newer than T_start. If any file is
   older than T_start, the run failed — stop and report, do NOT review stale images.
6. Compress every PNG to <=768px before viewing (save the Director's VLM quota).

## 4. VISUAL VERIFICATION — REAL FILES ONLY, YOUR OWN EYES

Cite only filenames that appear in your `ls`. For EACH question, name the exact real file(s) you
opened and state the literal observation. Mark [DIRECT VISUAL] or [INFERRED] (inferred is only allowed
for numeric stats from atlas_report.txt, never for a visual verdict).

FIX 1 — SEAM LINES (look at 1_height, 1b_detail, 2_hillshade for P1..P5 @ 10km & 1km):
  a. Are the 1px curved lines that followed province borders GONE? If any remain, which cell/file,
     and do they still coincide with a province boundary (cross-check the 7_province map)?
  b. Are province transitions now smooth and seamless, or is there any residual ridge/valley/crease
     along borders?

FIX 2+3 — DACTYLOSCOPY / RINGS (look at 2_hillshade + 3_slope for P1 1km/200m, P2 1km, P5 all scales):
  c. Are the concentric fingerprint rings on mesas/domes GONE?
  d. Are the ring systems around volcanic cones on P5 GONE?
  e. Any residual fingerprint/parallel-groove pattern anywhere? Which cell, and is it on flat ground
     (bad) or on a genuine steep slope as broken ledges (acceptable)?

REGRESSION CHECK:
  f. Do mesas still read as flat-topped tablelands (FIX 2 shouldn't have flattened them away)?
  g. Any NEW seam/discontinuity introduced by the smooth province blend (e.g. mushy over-blended
     provinces that no longer look distinct)? Do provinces still look DISTINCT region-to-region?
  h. Clipping still absent (report max height per 10km cell; must stay < +600)?

## 5. STATS
Paste from atlas_report.txt for each cell: Height min/max/mean/std, NaN, Slope buckets, HATCHING
index + peak angle, and mask coverage incl. Strata/Mesa/Volcano. Note: Strata & Mesa coverage should
be LOWER than Round 5 (they're now suppressed/reworked) — that is expected, not a regression.

## 6. THE NEXT PROBLEM (report your read, do NOT fix yet)
The Director also said P1 @1km "проcто пупырышки, гладкие, аcимметрии мало, cкучно" — the meso scale
(hundreds of m to ~1km) reads as a monotonous field of smooth rounded bumps with little asymmetry or
structure. Look hard at P1_origin_1km_2_hillshade.png and P3/P4 1km. Describe honestly: what IS the
1km scale currently made of (which generators dominate), and what's missing that would make it
interesting — asymmetric hills of varied size? incised gullies? outcrops? benches? Give your
architectural read so Claude can design the next pass. Do NOT implement it this round.

## 7. REPORT FORMAT
Use sections A–H exactly as in `ANTIGRAVITY_ADDENDUM_ENFORCEMENT.md`. Brutal honesty. No GOLD. No
fabricated filenames. No "100%". If a fix only partially worked, say which part failed and your
root-cause hypothesis. Your report drives Claude's next single decision.
