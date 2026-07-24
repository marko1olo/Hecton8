# ANTIGRAVITY R22 — DECISIVE ONE-VARIABLE TEST: SHELFBREAK ABS() CREASE

## AUTHORITY CHAIN
Read before acting: AGENTS.md → GEMINI.md → COMMON_SENSE.md → this file.

## RULE FOR THIS ROUND (NON-NEGOTIABLE)
DIAGNOSTIC isolation build, NOT a fix. Change EXACTLY ONE variable.
Do NOT touch canyon, strata, mesa, dune, crater, plate flags, or any other code.
Do NOT invent new "root causes". One flag, one build, one eye audit.

## R21 RESULT (already done)
DiagPlateSeamOff = true → lines REMAINED. PLATE CLEARED. Plate is not the source.

## HYPOTHESIS (architect, static analysis — UNPROVEN, this test proves/kills it)
File: Assets\_Project\Scripts\World\WorldMacroGeologyFields.cs

Line 672 (LIVE — confirm exact text in file before editing):
    float shelfBreakMask = math.saturate((1f - math.saturate(math.abs(continentNoise - 0.51f) * 5.7f)) * (0.62f + plateEdgeMask * 0.38f));

The key term is math.abs(continentNoise - 0.51f). math.abs(x) has a C1 kink (V-fold) exactly at x=0, i.e. where continentNoise = 0.51.
shelfBreakMask peaks at 1 along that isoline with an infinitely sharp gradient kink.
It feeds abyssPlainMask (line 673, writes depth at STAGE 1-2).
C1 kink in height = normal discontinuity = thin 1px line on hillshade.
The continentNoise=0.51 isoline crosses a 10km tile as a long near-straight line.
Slope map on P3_west_200m confirms a real height-field discontinuity (not render artifact).

## THE ONLY CHANGES THIS ROUND

### 1. Add diagnostic constant (near other Diag* constants, e.g. after DiagPlateSeamOff line):
    public const bool DiagShelfBreakOff = false;

### 2. Add zeroing line immediately after line 672:
    if (DiagShelfBreakOff) { shelfBreakMask = 0f; }

### 3. Set the new constant to true:
    public const bool DiagShelfBreakOff = true;

### 4. Bump sentinel:
    public static string BuildSentinel => "SENTINEL_R22_2026-07-23_shelfbreak_OFF_test";

Nothing else. DiagPlateSeamOff stays false. No other edits.

## SELF-CONTROL CHECKLIST (MANDATORY)
Before writing:
- [ ] Read line 672 from live file, confirm it is the abs() formula
- [ ] Read current sentinel line, note value
- [ ] Confirm DiagPlateSeamOff is currently false
After writing:
- [ ] Read back: DiagShelfBreakOff = true
- [ ] Read back: new sentinel
- [ ] Read back: DiagPlateSeamOff = false (unchanged)
- [ ] Confirm NO other lines changed
After Unity recompile:
- [ ] atlas_report.txt line 2 == SENTINEL_R22_2026-07-23_shelfbreak_OFF_test
- [ ] If old sentinel: clean Library/ScriptAssemblies only (do NOT delete Bee or BurstCache)

## RENDER & EYE AUDIT
| Tile | Scale | What to look for |
|------|-------|------------------|
| P1_origin  | 10km | LONG 1px lines at Stage 2: PRESENT / GONE? |
| P2_near    | 10km | LONG 1px lines at Stage 2: PRESENT / GONE? |
| P3_west    | 10km | Long line: PRESENT / GONE? |
| P3_west    | 200m | Razor seam + slope map spike: PRESENT / GONE? |
| P5_deepfar | 10km | Long line: PRESENT / GONE? |

For EACH image state literally: PRESENT or GONE. NOT VIEWED if not opened. Do not guess.
Note: terrain will look different (shelf break gone) — that is expected. Only report lines.

## VERDICT LOGIC
- Long 1px lines on 10km tiles GO AWAY:
    => SHELFBREAK ABS CREASE CONFIRMED. Report "SHELFBREAK CONFIRMED".
- Lines REMAIN:
    => shelfBreakMask is not the source. Report "SHELFBREAK CLEARED".
    => Next suspect: continentNoise computation itself (does it use floor/Voronoi?).
       Architect will investigate continentNoise source before R23.

## REPORT FORMAT
    BUILD SENTINEL VERIFIED: SENTINEL_R22_2026-07-23_shelfbreak_OFF_test
    ONLY CHANGES: DiagShelfBreakOff added+true, sentinel bumped
    EYE AUDIT:
    - P1_origin_10km stage2: long 1px line PRESENT/GONE — <desc>
    - P2_near_10km stage2:   long 1px line PRESENT/GONE — <desc>
    - P3_west_10km:          long 1px line PRESENT/GONE — <desc>
    - P3_west_200m:          razor seam PRESENT/GONE — <desc>
    - P3_west_200m slope:    spike PRESENT/GONE — <desc>
    - P5_deepfar_10km:       long 1px line PRESENT/GONE — <desc>
    VERDICT: SHELFBREAK CONFIRMED / SHELFBREAK CLEARED

## AFTER THIS TEST (architect will act, do NOT preempt)
If CONFIRMED: architect replaces abs() with smooth squared falloff:
    float shelfBreakMask = 1f - math.saturate((continentNoise - 0.51f) * (continentNoise - 0.51f) * k);
  (C1-smooth at peak, no kink). DiagShelfBreakOff goes back to false with real fix in place.
If CLEARED: architect reads continentNoise source before designing R23.

## CACHE CRASH NOTE (from R21 experience)
Do NOT delete Library/Bee or Library/BurstCache — causes mono crash.
Delete ONLY Library/ScriptAssemblies to force recompile if sentinel mismatch.
