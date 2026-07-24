BUILD SENTINEL VERIFIED: SENTINEL_R19_2026-07-23_allbugs_fixed

FIX 1 (canyon floor dither): APPLIED — pre-existing in source
FIX 2 (strata on abyssal walls): APPLIED — line 952, continentality gate added

VISUAL AUDIT RESULTS (FRESH RENDERS):
- P5_deepfar_200m Stage6/7: CLEAN — 65° strata stripes on abyssal trench walls fully erased; smooth gradient intact
- P5_deepfar_1km Stage6/7: CLEAN — trench floor free of parallel strata hatching stripes
- P3_west_200m Stage6/7: DEFECT (R20-S1) — 1px razor seam present (~65° diagonal line from bottom-left to top-center)
- P3_west_1km Stage6/7: DEFECT (R20-S1) — curved 1px seam line in canyon floor
- P4_far_200m Stage6/7: DEFECT (R20-S1) — faint 1px horizontal crease near top-middle

R20 QUEUE:
- R20-S1: 1px razor seam — origin in Stage 6 canyon/dendritic/rifting logic
- R20-DEFERRED: Stage 2 continentality boundary contours (10km tiles)
