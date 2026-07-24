# ANTIGRAVITY R20 — TASK: R20-S1 Canyon Rim Razor Seam

## AUTHORITY CHAIN
Read before acting: AGENTS.md → GEMINI.md → COMMON_SENSE.md → this file.

## DEFECT DESCRIPTION
1px bright white diagonal line (~65°) visible in hillshade at P3_west_200m and P4_far_200m,
present from Stage 6 output. Seam is a raised canyon rim rendered as a razor-thin ridge.

## ROOT CAUSE (CONFIRMED BY ARCHITECT)
File: `Assets\_Project\Scripts\World\WorldMacroGeologyFields.cs`

Line 876:
```csharp
float canyonRim = math.smoothstep(0.28f, 0.96f, dendritic) * (0.75f + rimNoise * 0.25f);
```

Line 879:
```csharp
riverMask = canyonRim * continentality * recipe.Rivers;
```

Line 883:
```csharp
depth += riverMask * canyonRim * 220f;
```

riverMask already contains canyonRim, so line 883 effectively applies canyonRim².
RidgedMultifractal01 has a cusp (fold) at its ridge peak (value ≈ 1.0) — spatially
very narrow. The squared canyonRim sharpens this peak further. 220f amplifies it into
a visible raised rim → 1px bright seam in hillshade.

## FIX STRATEGY
Break the rim into a bump (rises then falls) instead of a ramp (rises to plateau).
The rim should peak at the canyon shoulder and be ZERO at the ridge center.

PATCH — line 876 and 883
Step 1: Replace canyonRim with a bump that peaks at dendritic ≈ 0.65 and
falls back to zero at dendritic ≈ 0.96 (the ridge center):

```csharp
// BEFORE (line 876):
float canyonRim = math.smoothstep(0.28f, 0.96f, dendritic) * (0.75f + rimNoise * 0.25f);

// AFTER (line 876):
float rimRise = math.smoothstep(0.28f, 0.65f, dendritic);
float rimFall = 1f - math.smoothstep(0.65f, 0.96f, dendritic);
float canyonRim = rimRise * rimFall * (0.75f + rimNoise * 0.25f);
```
This creates a bump peaking at dendritic=0.65 (canyon shoulder), zero at the ridge
center (dendritic≥0.96). The rim is now spatially wide and smooth, not a razor edge.

Step 2: Line 883 stays unchanged — riverMask * canyonRim * 220f.
With the new bump shape, canyonRim is already zero at the ridge, so the squared
effect is gone and the raised rim is distributed across the shoulder, not the peak.

Step 3: Bump sentinel on line 182:
```csharp
public static string BuildSentinel => "SENTINEL_R20_2026-07-23_rim_bump";
```

## SELF-CONTROL CHECKLIST (MANDATORY)
Before writing:
 Read line 876 from live file — confirm it matches the BEFORE string exactly
 Read line 182 — confirm current sentinel
After writing:
 Read line 876 back — confirm rimRise * rimFall present
 Read line 182 — confirm new sentinel
 If mismatch → rewrite, do not proceed
After Unity recompile:
 Console shows SENTINEL_R20_2026-07-23_rim_bump
 If console shows old sentinel → Assets → Reimport All → recheck

## RENDER & AUDIT
Render fresh hillshades (Stage 6 AND Stage 7) for:

Tile	Scale	Expected
P3_west	200m, 1km	PASS: no 1px diagonal seam
P4_far	200m	PASS: no horizontal crease
P5_deepfar	200m	PASS: strata still clean (regression check)
P1_origin	200m	PASS: no new artifacts on continental terrain

PASS criteria:
P3_west_200m: zero continuous diagonal bright line at ~65°
P4_far_200m: zero horizontal crease near top-middle
Canyon walls show smooth organic rim, not razor edge

FAIL criteria (rollback if any):
New concentric rings on canyon floors
Canyon rim completely missing (terrain looks flat at edges)
Strata stripes returned on P5_deepfar

## IF FIX IS INSUFFICIENT
If seam persists after rim bump fix, the source may be in riverCut (line 881):
```csharp
float riverCut = RidgedMultifractal01(warpedPos * 0.00072f + new float2(9.5f, -3.1f), seed ^ 0x8A4B2C1Du, 4);
```
RidgedMultifractal01 cusps at its own ridges. Line 882:
```csharp
depth += riverCut * 320f * riverMask * canyonFloor;
```
If the rim bump fix alone is insufficient, add dither to riverCut:
```csharp
float riverCutDither = FractalSimplexNoise01(warpedPos * 0.0041f + new float2(2.2f, -9.1f), seed ^ 0xB3C4D5E6u, 2) * 0.08f;
float riverCut = RidgedMultifractal01(warpedPos * 0.00072f + new float2(9.5f, -3.1f), seed ^ 0x8A4B2C1Du, 4) + riverCutDither;
```
Report both attempts separately in the R20 audit.

## DEFERRED (DO NOT TOUCH IN R20)
Stage 2 continentality boundary contours (10km tiles) — separate round

## REPORT FORMAT
```markdown
BUILD SENTINEL VERIFIED: SENTINEL_R20_2026-07-23_rim_bump
FIX: Canyon rim bump (lines 876, 182)

VISUAL AUDIT:
- P3_west_200m Stage6/7: [CLEAN/DEFECT] — [description]
- P3_west_1km Stage6/7: [CLEAN/DEFECT] — [description]
- P4_far_200m Stage6/7: [CLEAN/DEFECT] — [description]
- P5_deepfar_200m Stage6/7: [CLEAN] — regression check passed/failed
- P1_origin_200m Stage6/7: [CLEAN] — regression check passed/failed

VERDICT: [R20-S1 CLOSED / ESCALATE TO ARCHITECT]
```
