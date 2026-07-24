# ANTIGRAVITY R25 — REAL FIX: province type from hash only (kill the 0.45 threshold crease)

## AUTHORITY CHAIN
Read before acting: AGENTS.md → GEMINI.md → COMMON_SENSE.md → this file.

## ROOT CAUSE (LOCKED by R23 + R24, eye-verified)
10km long 1px lines are born in continentalRelief (R23 code 22), present on ALL three
relief terms (R24 codes 31/32/33). Reason: they share recipe.BaseRough, and BaseRough
jumps discontinuously.

SelectGeologicalType (lines 567, 573) switches province TYPE on hard thresholds:
    if (plateEdgeMask > 0.45f) {...}
    if (continentality > 0.45f) {...}
continentality is a SMOOTH function of position. The isoline continentality=0.45 is a
long curve across a 10km tile. Crossing it flips every 3x3 cell's type at once, so the
exp-blended recipe.BaseRough STEPS (C0 jump) along that isoline. recipe.BaseRough
multiplies mountainUplift/foothills/plateauUplift -> depth step -> 1px hillshade line.
(The exp-blend in ResolveProvince smooths cell-to-cell borders but NOT the simultaneous
type flip of all cells at the 0.45 isoline.)

## DIRECTOR DECISION: FIX B — province type from CELL HASH ONLY
Remove continentality/plateEdgeMask from the TYPE decision. Land/ocean already gate
height smoothly downstream (continentalRelief * continentality at line 717; rivers/mesa
have their own continentality gates). So a "river" cell landing in deep ocean contributes
~0 height (continentality~0) with NO discontinuity. The 0.45 isoline vanishes from type
selection -> crease gone at the root.

## THE FIX (one function body)
File: Assets\_Project\Scripts\World\WorldMacroGeologyFields.cs
Function: SelectGeologicalType (lines 562-586)

Keep the SIGNATURE unchanged (callers at lines 545, 553 stay valid; unused params cause
no C# warning). Replace ONLY the body from line 566 to line 585 with pure-hash selection:

REPLACE (lines 566-585, the two if-blocks and the else):
    if (plateEdgeMask > 0.45f)
    {
        if (rawVal < 0.35f) return 4; // RIFT_VALLEY
        if (rawVal < 0.70f) return 5; // VOLCANIC_FIELD
        return 3;                     // FOLDED_MOUNTAINS
    }
    if (continentality > 0.45f)
    {
        if (rawVal < 0.28f) return 2; // RIVER_LOWLANDS
        if (rawVal < 0.54f) return 1; // CRATERED_HIGHLANDS
        if (rawVal < 0.78f) return 3; // FOLDED_MOUNTAINS
        return 6;                     // MESA_TABLELANDS
    }
    else
    {
        if (rawVal < 0.55f) return 0; // ABYSSAL_PLAIN
        if (rawVal < 0.82f) return 7; // DUNE_SEA
        return 5;                     // VOLCANIC_FIELD
    }

WITH (pure hash, no smooth-field threshold — all 8 types by rawVal only):
    // FIX B (R25): province TYPE from cell hash ONLY. No continentality/plateEdgeMask
    // threshold here — those are SMOOTH fields and any hard cutoff on them injects a C0
    // step in recipe.BaseRough along the cutoff isoline (the 10km 1px lines). Land/ocean
    // is applied later as a SMOOTH height gate (continentalRelief * continentality), so
    // an ocean-typed cell on land (or vice versa) blends with zero discontinuity.
    if (rawVal < 0.16f) return 0; // ABYSSAL_PLAIN
    if (rawVal < 0.28f) return 7; // DUNE_SEA
    if (rawVal < 0.42f) return 1; // CRATERED_HIGHLANDS
    if (rawVal < 0.56f) return 2; // RIVER_LOWLANDS
    if (rawVal < 0.70f) return 3; // FOLDED_MOUNTAINS
    if (rawVal < 0.82f) return 6; // MESA_TABLELANDS
    if (rawVal < 0.92f) return 5; // VOLCANIC_FIELD
    return 4;                     // RIFT_VALLEY

Bump sentinel:
    public static string BuildSentinel => "SENTINEL_R25_2026-07-23_province_hash_fix";

## SELF-CONTROL CHECKLIST (MANDATORY)
Before writing:
- [ ] Read lines 562-586 live, confirm the two if-blocks match the REPLACE text
- [ ] Read current sentinel
After writing:
- [ ] Read back lines 562-586, confirm pure-hash body present, NO continentality/plateEdge if
- [ ] Read back sentinel
- [ ] Confirm ResolveProvince (lines 504-560) and callers UNCHANGED
- [ ] Confirm the R23/R24 sub-dump returns (codes 21/22/23/31/32/33) still present, untouched
After recompile:
- [ ] atlas_report.txt line 2 == SENTINEL_R25_2026-07-23_province_hash_fix
- [ ] Clean ONLY Library/ScriptAssemblies if stale (NOT Bee/BurstCache)

## RENDER & EYE AUDIT (this is a FIX build — verify lines GONE + no regression)
| Tile | Scale | Check |
|------|-------|-------|
| P1_origin  | 10km | code 22 + full: long 1px lines GONE? |
| P2_near    | 10km | code 22 + full: long 1px lines GONE? |
| P3_west    | 10km | long line GONE? |
| P1_origin  | 1km, 200m | REGRESSION: land still looks rich (not flat/ugly)? |
| P2_near    | 1km  | REGRESSION: terrain still varied? |
| P5_deepfar | 10km | REGRESSION: abyssal ocean still reads as ocean (not littered with mountains)? |

State each literally PRESENT/GONE and for regression GOOD/BAD with a one-line description.
NOT VIEWED if not opened. Do not guess.

## VERDICT LOGIC
- 10km lines GONE on P1/P2/P3 AND regression GOOD:
    => Report "FIX B CONFIRMED — 10km crease killed at root, no regression".
- Lines GONE but regression BAD (ocean full of mountains / land flat):
    => Report "FIX B WORKS but BIOME REGRESSION" + describe. Architect will add a smooth
       continentality bias to the recipe (not a hard threshold).
- Lines STILL PRESENT:
    => Report "FIX B FAILED — lines remain". Then recipe.BaseRough was NOT the only path;
       architect escalates (freeze BaseRough to constant as a proof probe).

## REPORT FORMAT
    BUILD SENTINEL VERIFIED: SENTINEL_R25_2026-07-23_province_hash_fix
    ONLY CHANGE: SelectGeologicalType body -> pure hash (lines 562-586), sentinel bumped
    EYE AUDIT:
    - P1_origin_10km code22: long line PRESENT/GONE — <desc>
    - P2_near_10km code22:   long line PRESENT/GONE — <desc>
    - P3_west_10km:          long line PRESENT/GONE — <desc>
    - P1_origin_1km/200m:    REGRESSION GOOD/BAD — <desc>
    - P2_near_1km:           REGRESSION GOOD/BAD — <desc>
    - P5_deepfar_10km:       REGRESSION GOOD/BAD — <desc>
    VERDICT: FIX B CONFIRMED / WORKS+REGRESSION / FAILED

## NOTE
The 200m P3_west razor is a SEPARATE defect (Stage 6 surface features) — NOT addressed
this round. Do not touch Stage 6. It gets its own sub-dump round after 10km lines close.
