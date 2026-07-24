# ANTIGRAVITY R21 — DECISIVE ONE-VARIABLE TEST: PLATE F2-F1 CREASE

## AUTHORITY CHAIN
Read before acting: AGENTS.md → GEMINI.md → COMMON_SENSE.md → this file.

## RULE FOR THIS ROUND (NON-NEGOTIABLE)
This is a DIAGNOSTIC isolation build, NOT a fix. Change EXACTLY ONE variable.
Do NOT touch canyon, strata, mesa, dune, crater, or any feature code this round.
Do NOT invent new "root causes". Do NOT stack edits. One flag, one build, one eye audit.

## HYPOTHESIS (from architect, static analysis — UNPROVEN, this test proves/kills it)
File: Assets\_Project\Scripts\World\WorldMacroGeologyFields.cs

Lines 657-658:
    float plateEdgeDelta = math.max(0f, plateF2 - plateF1);
    float plateEdgeMask  = 1f - math.smoothstep(0.035f, 0.28f, plateEdgeDelta);

plateF2-plateF1 (Voronoi 2nd-minus-1st distance) is ZERO on the plate boundary and
has a GRADIENT KINK (C1 discontinuity) there. So plateEdgeMask is a roof-ridge peaking
along every plate boundary. It feeds abyssPlainMask (line 673, writes depth at STAGE 1),
shelfBreakMask (672), ridgeMask, faultMask, slopeProxy, hardRockMask — everywhere.
A C1 kink in height = a normal discontinuity = a thin 1px line on hillshade, running
along plate boundaries. Plate grid = 12000m, warped; a 10km tile is crossed by ~one
boundary => the "long 1px lines" the Director sees from Stage 2 on the 10km tiles.

This was "exonerated" in R8 using the hatching metric. R13 proved that metric is
degenerate/dead. So the R8 exoneration is INVALID and plate was never eye-tested since.

## THE ONLY CHANGE THIS ROUND
Line 667 already contains:
    if (DiagPlateSeamOff) { plateRidgeMask = 0f; plateTrenchMask = 0f; plateEdgeMask = 0f; }

Line 196 currently:
    public const bool DiagPlateSeamOff = false;

CHANGE line 196 to:
    public const bool DiagPlateSeamOff = true;

Bump sentinel, line 182:
    public static string BuildSentinel => "SENTINEL_R21_2026-07-23_plateseam_OFF_test";

Nothing else. Do not revert or add anything else this round.

## SELF-CONTROL CHECKLIST (MANDATORY)
Before writing:
- [ ] Read line 196 from live file, confirm it is `= false;`
- [ ] Read line 182, confirm current sentinel
After writing:
- [ ] Read line 196 back, confirm `= true;`
- [ ] Read line 182 back, confirm new sentinel
- [ ] Confirm NO other lines changed (line 667 stays as-is, feature code untouched)
After Unity recompile:
- [ ] atlas_report.txt line 2 == SENTINEL_R21_2026-07-23_plateseam_OFF_test
- [ ] If old sentinel shows: clean Library/ScriptAssemblies, Library/Bee, Library/BurstCache, rebuild

## RENDER & EYE AUDIT (Stage 2 AND full hillshade; 10km is the key scale)
Render fresh hillshades for:
| Tile | Scale | What to look for |
|------|-------|------------------|
| P1_origin  | 10km | LONG 1px lines present or GONE? (Stage 2 + full) |
| P2_near    | 10km | LONG 1px lines present or GONE? |
| P3_west    | 10km, 200m | Long line (10km) + razor seam (200m) present or GONE? |
| P5_deepfar | 10km, 200m | Long line + any striping present or GONE? |

For EACH image state literally: "long straight 1px line: PRESENT / GONE".
If you did not open an image, write NOT VIEWED. Do not guess.

## VERDICT LOGIC (report exactly this)
- If the long 1px lines on the 10km tiles GO AWAY with the flag ON:
    => PLATE F2-F1 CREASE CONFIRMED as the source. Report "PLATE CONFIRMED".
- If the long lines REMAIN with the flag ON:
    => plate is NOT the (only) source. Report "PLATE CLEARED". Next suspect is
       shelfBreakMask abs-ridge (line 672) — architect will design R22.

## REPORT FORMAT
    BUILD SENTINEL VERIFIED: SENTINEL_R21_2026-07-23_plateseam_OFF_test
    ONLY CHANGE: DiagPlateSeamOff false->true (line 196)
    EYE AUDIT:
    - P1_origin_10km: long 1px line PRESENT/GONE — <desc>
    - P2_near_10km:   long 1px line PRESENT/GONE — <desc>
    - P3_west_10km:   long 1px line PRESENT/GONE — <desc>
    - P3_west_200m:   razor seam PRESENT/GONE — <desc>
    - P5_deepfar_10km:long 1px line PRESENT/GONE — <desc>
    - P5_deepfar_200m:striping PRESENT/GONE — <desc>
    VERDICT: PLATE CONFIRMED / PLATE CLEARED

## AFTER THIS TEST (architect will act, do NOT preempt)
- If CONFIRMED: architect replaces the F2-F1 crease with a smooth exp-weighted plate
  blend (same technique already used in ResolveProvince lines 522-559), then
  DiagPlateSeamOff goes back to false with the real fix in place.
- Separately: the R20 canyon 220f->22f edit flattened canyons and the crater 3x3->5x5
  edit was based on a false diagnosis. Both will be reviewed/reverted AFTER root cause
  is locked. Do NOT touch them this round.
