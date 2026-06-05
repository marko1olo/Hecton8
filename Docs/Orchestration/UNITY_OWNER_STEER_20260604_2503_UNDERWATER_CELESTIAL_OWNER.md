# STEER_2503_UNDERWATER_CELESTIAL_OWNER

Target: `Продолжить работу по логам` Unity owner.
Date: 2026-06-04.
Source:
- `Docs/Reports/Batch25/2503_UNDERWATER_VISUALS_OWNER_PHASE_AUDIT.md`

Use after current compile/import/ILPP quiets down.

2503 static owner-phase findings:

1. `HectonUnderwaterVisuals` owner is now statically sane:
   - exactly one instance:
     - GameObject `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474`
     - MonoBehaviour `101536743`
   - required references are assigned:
     - `biomePalette` -> `Assets/_Project/Data/biom/Main_Ocean_Palette.asset`
     - `oceanUnderwaterMaterial` -> `Assets/Crest/Crest/Materials/Ocean-Underwater.mat`
     - `skyMaterial` -> `Assets/_Project/Art/Materials/Mat_HectonSky.mat`

2. `HectonCelestialEngine` still has unresolved sun visual:
   - GameObject `H8_ATMOSPHERE_CELESTIAL_OWNERS_1428`
   - MonoBehaviour `1893406170`
   - `sunVisualTransform: {fileID: 0}`

3. Candidate sun visual:
   - `SURFACE_LOW_SUN_DISC_1428`
   - transform fileID `1985271341`
   - current static state: GameObject inactive, MeshRenderer disabled.

Required owner decision:
- If `SURFACE_LOW_SUN_DISC_1428` is intended, assign it to `HectonCelestialEngine.sunVisualTransform` and make its activation/renderer policy explicit enough that first celestial sync can prove it.
- If sky material fully owns the sun disc, document that route and remove the stale expectation; do not leave silent `{fileID: 0}` while proof expects sun visual routing.

Clean log proof must include:
- no `HectonUnderwaterVisuals` unassigned warnings;
- no `GlobalRegistry ready-lock rejected registration: HectonUnderwaterVisuals`;
- no `sunVisualTransform still unresolved after runtime retry`;
- no `WeatherEvents` Persistent leak after the current cleanup patch compiles.

Only after that should the visual proof packet and Batch24 slab/caustic isolation proceed.
