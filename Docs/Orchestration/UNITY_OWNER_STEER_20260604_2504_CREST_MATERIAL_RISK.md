# STEER_2504_CREST_MATERIAL_RISK

Target: `Продолжить работу по логам` Unity owner.
Date: 2026-06-04.
Source:
- `Docs/Reports/Batch25/2504_CREST_OCEAN_CLIP_FOAM_CAUSTIC_RISK_AUDIT.md`

Use after current compile/import window and route/leak cleanup. Do not interrupt an in-flight MCP run only for this.

Static material audit found real blockers:

1. `Assets/Crest/Crest/Materials/Ocean.mat`
   - `_ClipSurface: 1 -> 0`
   - `_ClipUnderTerrain: 1 -> 0`
   - `_CLIPSURFACE_ON` and `_CLIPUNDERTERRAIN_ON` keywords removed.
   - This is the primary material-side suspect for terrain/water plane exposure and hard slab artifacts.

2. `Assets/Crest/Crest/Materials/Ocean_UnderwaterCurtain.mat`
   - `_CAUSTICS_ON` replaced `_CLIPUNDERTERRAIN_ON`.
   - `_TRANSPARENCY_ON` removed.
   - Current `_CausticsStrength` is `10`.
   - If visible, this can create a curtain/sheet/black-green caustic artifact.

3. `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat`
   - overdriven shallow/subsurface/sky/foam values can explain acid or flat green water:
     - `_CausticsStrength 1.45`
     - `_WaveFoamStrength 3.45`
     - `_LightIntensityMultiplier 1.95`
     - very bright shallow/subsurface/sky colors.

Operational order:
1. First get clean runtime route: no ready-lock, no unassigned `HectonUnderwaterVisuals`, no repeated Persistent leak.
2. Then isolate scene slabs per Batch24.
3. If plane/slab/water artifacts persist, verify/rollback `Ocean.mat` clipping before adding haze or color.
4. Test `Ocean_UnderwaterCurtain.mat` keyword/caustic route separately; do not accept `_CausticsStrength 10` curtain without close proof.
5. Foam remains unproven; numeric boosts do not prove shoreline contact. Require close shoreline proof.

Acceptance remains blocked until the full proof packet shows no material-plane artifacts, believable shoreline foam/wet contact, and real photic underwater depth with caustics/particulates.
