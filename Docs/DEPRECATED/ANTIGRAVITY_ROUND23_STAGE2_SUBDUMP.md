# ANTIGRAVITY R23 — STAGE 2 SUB-DUMP: LOCALIZE THE CREASE INSIDE STAGE 2

## AUTHORITY CHAIN
Read before acting: AGENTS.md → GEMINI.md → COMMON_SENSE.md → this file.

## WHY THIS ROUND IS DIFFERENT
Turning off ONE feature per build is too slow (R21 plate, R22 shelfbreak — both CLEARED,
2 builds wasted). New method: SUBDIVIDE Stage 2 with sub-dump return points, exactly like
the macro stageDump that already localized the bug to Stage 2. ONE build pinpoints which
of the 3 Stage-2 depth writes creates the razor line. No guessing, no toggling.

## FACTS LOCKED
- Raw probe (R13) clean → renderer/base noise NOT the source.
- Stage 1 clean, Stage 2 = lines appear → source is BETWEEN Stage 1 and Stage 2.
- Slope map = razor spike → a real height discontinuity (kink/step: abs/floor/max/crest),
  NOT a smooth smoothstep.
- Plate (R21) and shelfBreak (R22) both CLEARED.

## THE ONLY CHANGES THIS ROUND (add 3 sub-dump return points inside Stage 2)
File: Assets\_Project\Scripts\World\WorldMacroGeologyFields.cs

Stage 2 has exactly 3 depth writes:
  line 714: depth = math.lerp(depth, landBaseDepth, continentality);
  line 715: depth -= continentalRelief * continentality;
  line 719: depth += (DiagGeoNoiseOff ? 0f : (geologicalNoise - 0.5f) * 160f * ...);

Add these early returns (use the existing stageDump int parameter, new codes 21/22/23):

Immediately AFTER line 714, insert:
    if (stageDump == 21) { masks = default; return parameters.WaterSurfaceY - depth; } // SUB: continentality lerp only

Immediately AFTER line 715, insert:
    if (stageDump == 22) { masks = default; return parameters.WaterSurfaceY - depth; } // SUB: +continentalRelief (mtn+foothill+plateau)

Immediately AFTER line 719 (before the existing `if (stageDump == 2)`), insert:
    if (stageDump == 23) { masks = default; return parameters.WaterSurfaceY - depth; } // SUB: +geologicalNoise

Bump sentinel:
    public static string BuildSentinel => "SENTINEL_R23_2026-07-23_stage2_subdump";

## IF THE ATLAS TASK HARDCODES stageDump VALUES
Open GeologyAtlasTask.cs. Find where it loops stageDump 1..7 (or 1..8). Extend the set
it renders to ALSO include 21, 22, 23 for the target tiles below. If it uses a fixed
array/range, add 21,22,23. Name output files _stage21_hillshade.png etc.
If unsure how, render the 3 extra codes at least for P3_west_200m and P1_origin_10km.

## SELF-CONTROL CHECKLIST (MANDATORY)
Before writing:
- [ ] Read lines 714, 715, 719 from live file — confirm they are the 3 depth writes
- [ ] Read current sentinel
After writing:
- [ ] Read back the 3 inserted lines (21/22/23) — confirm present and correctly placed
- [ ] Read back sentinel
- [ ] Confirm continentality/relief/geoNoise CODE ITSELF unchanged (only returns added)
After Unity recompile:
- [ ] atlas_report.txt line 2 == SENTINEL_R23_2026-07-23_stage2_subdump
- [ ] Delete ONLY Library/ScriptAssemblies if stale (NOT Bee, NOT BurstCache — crash risk)

## RENDER & EYE AUDIT
Render hillshade for these tiles at sub-stages 21, 22, 23 (and 1, 2 for reference):
| Tile | Scale | Codes |
|------|-------|-------|
| P3_west   | 200m | 1, 21, 22, 23, 2 |
| P1_origin | 10km | 1, 21, 22, 23, 2 |
| P2_near   | 10km | 21, 22, 23 |

For EACH image, state literally at which code the RAZOR / LONG LINE first APPEARS.
NOT VIEWED if not opened. Do not guess.

## VERDICT LOGIC (report the FIRST code where the line appears)
- Line appears at code 21 (continentality lerp, no relief yet):
    => source is the continent/ocean depth step (line 714) or landBaseDepth. Report "CULPRIT=CONTINENTALITY_LERP".
- Line appears at code 22 (relief added), was clean at 21:
    => source is continentalRelief = mountainUplift + foothills + plateauUplift. Report "CULPRIT=RELIEF".
- Line appears at code 23 (geoNoise added), was clean at 22:
    => source is geologicalNoise. Report "CULPRIT=GEONOISE".

## REPORT FORMAT
    BUILD SENTINEL VERIFIED: SENTINEL_R23_2026-07-23_stage2_subdump
    ONLY CHANGES: 3 sub-dump returns (codes 21/22/23) inside Stage 2, sentinel bumped
    EYE AUDIT (P3_west_200m):
    - code 1  (base):            line PRESENT/GONE
    - code 21 (continentality):  line PRESENT/GONE
    - code 22 (+relief):         line PRESENT/GONE
    - code 23 (+geoNoise):       line PRESENT/GONE
    (repeat for P1_origin_10km, P2_near_10km)
    FIRST APPEARANCE: code __
    VERDICT: CULPRIT=CONTINENTALITY_LERP / RELIEF / GEONOISE

## AFTER THIS TEST (architect will act — do NOT preempt or "fix" anything)
If CULPRIT=RELIEF: architect will further split mountainUplift vs foothills vs plateauUplift
  (prime suspect: foothills uses BillowNoise01 = abs(snoise) = C1 crease at every zero
   crossing; also verify DiagRidgedAsFbmMountain is actually neutralizing mountainField).
Then architect applies the real fix. Do not edit feature code this round.
