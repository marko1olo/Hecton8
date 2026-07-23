# ANTIGRAVITY R24 — SPLIT continentalRelief: mountain vs foothills vs plateau

## AUTHORITY CHAIN
Read before acting: AGENTS.md → GEMINI.md → COMMON_SENSE.md → this file.

## R23 RESULT (locked)
Sub-dump proved: 10km long 1px lines are BORN at code 22 = `depth -= continentalRelief * continentality`.
continentalRelief = mountainUplift + foothills + plateauUplift (lines 697/708/711).
Now split those 3 terms in ONE build to find which one carries the razor.

## ARCHITECT STATIC ANALYSIS (prediction — this build proves/kills it)
- mountainUplift (line 697): ErodedRidge01, but DiagRidgedAsFbmMountain=true makes it
  plain fBm (line 1252) → SMOOTH, no crest. Predicted CLEAN.
- plateauUplift (line 711): plateauField = smoothstep → SMOOTH. Predicted CLEAN.
- foothills (line 708): largeHills/medHills/smallHills = BillowNoise01, and
  BillowNoise01 = math.abs(snoise) (line ~1268) → C1 V-fold at every noise zero-crossing
  = razor spike on slope map. largeHills freq 0.00045 → ~2200m wavelength → long curved
  1px lines on a 10km tile. PREDICTED CULPRIT = foothills (largeHills).

## THE ONLY CHANGES THIS ROUND (3 isolation return points)
File: Assets\_Project\Scripts\World\WorldMacroGeologyFields.cs

Line 713 currently:
    float continentalRelief = mountainUplift + foothills + plateauUplift;

Replace the single depth write region so each term can be isolated. Insert BEFORE
line 715 (the `depth = math.lerp(depth, landBaseDepth, continentality);`) nothing —
instead add these 3 sub-dumps that re-derive depth with only ONE relief term each.

Add immediately AFTER line 715 (`depth = math.lerp(...continentality);`), BEFORE the
existing `if (stageDump == 21)`:

    if (stageDump == 31) { masks = default; return parameters.WaterSurfaceY - (depth - mountainUplift * continentality); } // SUB: mountain only
    if (stageDump == 32) { masks = default; return parameters.WaterSurfaceY - (depth - foothills * continentality); }      // SUB: foothills only
    if (stageDump == 33) { masks = default; return parameters.WaterSurfaceY - (depth - plateauUplift * continentality); }  // SUB: plateau only

(Each subtracts ONLY its one relief term from the post-lerp base depth, so the tile
shows base+that-single-term. The razor appears only on the guilty term's tile.)

Bump sentinel:
    public static string BuildSentinel => "SENTINEL_R24_2026-07-23_relief_split";

Ensure GeologyAtlasTask.cs renders codes 31, 32, 33 hillshade for the target tiles.

## SELF-CONTROL CHECKLIST (MANDATORY)
Before writing:
- [ ] Read lines 713-718 live — confirm mountainUplift/foothills/plateauUplift names exist
- [ ] Read current sentinel
After writing:
- [ ] Read back the 3 inserted lines (31/32/33), confirm each subtracts the RIGHT term
- [ ] Read back sentinel
- [ ] Confirm relief formulas (lines 695-711) themselves UNCHANGED
After recompile:
- [ ] atlas_report.txt line 2 == SENTINEL_R24_2026-07-23_relief_split
- [ ] Clean ONLY Library/ScriptAssemblies if stale (NOT Bee/BurstCache)

## RENDER & EYE AUDIT
| Tile | Scale | Codes |
|------|-------|-------|
| P1_origin | 10km | 31, 32, 33 |
| P2_near   | 10km | 31, 32, 33 |

For EACH: does the long 1px line APPEAR on this term's tile? PRESENT / GONE.
NOT VIEWED if not opened. Do not guess.

## VERDICT LOGIC
- Line PRESENT only on code 32 (foothills): => CULPRIT=FOOTHILLS (BillowNoise01 abs-fold).
- Line PRESENT only on code 31 (mountain): => CULPRIT=MOUNTAIN (DiagRidgedAsFbmMountain not working).
- Line PRESENT only on code 33 (plateau): => CULPRIT=PLATEAU.
- Line on more than one: report ALL codes where PRESENT.

## REPORT FORMAT
    BUILD SENTINEL VERIFIED: SENTINEL_R24_2026-07-23_relief_split
    ONLY CHANGES: 3 relief-isolation sub-dumps (codes 31/32/33), sentinel bumped
    EYE AUDIT (P1_origin_10km):
    - code 31 (mountain only):  long line PRESENT/GONE
    - code 32 (foothills only): long line PRESENT/GONE
    - code 33 (plateau only):   long line PRESENT/GONE
    (repeat P2_near_10km)
    VERDICT: CULPRIT=FOOTHILLS / MOUNTAIN / PLATEAU / <list>

## AFTER THIS TEST (architect acts — do NOT preempt)
If CULPRIT=FOOTHILLS: architect replaces BillowNoise01 (abs-fold) in the hills with a
smooth fBm (FractalSimplexNoise01) or a squared-billow that is C1 at the fold. That is
the REAL fix — first real height-source fix in this whole hunt.
