# Batch25 Synthesis For Unity Owner

Date: 2026-06-04.
Scope: no-Unity static/log/source audit wave for current visual proof blockers.

## Current Verdict

No visual acceptance is possible yet.

Reasons:
- no `1475` six-view packet exists;
- latest accepted evidence is still reject-only `1474`;
- route proof is contaminated by compile/import/domain reload and `WeatherEvents` native leak;
- celestial sun visual is still unresolved;
- ocean materials contain hard plane/clip and overdrive risks;
- underwater/foam/caustic quality remains unproven.

## Required Order

1. Wait for Unity compile/import/ILPP quiet state.
2. Verify current `WeatherEvents` cleanup patch with a fresh play/exit/reload log:
   - no `Leak Detected : Persistent allocates` stack for `WeatherEvents`.
3. Run clean normal route:
   - `01_MAIN_MENU` -> `00_BOOTSTRAP` -> `02_HECTON_WORLD`.
   - No route run during Asset Pipeline Refresh or script compilation.
4. If route stalls:
   - first suspect is `[GameBootstrapper] Step 8: Runtime World Prime`, not async scene activation.
   - inspect scatter bootstrap prime path: `WorldProceduralScatterDirector.TryPrewarmBootstrapSamplingPipeline()` / `TryPrimeBootstrapScatterPass()`.
5. Fix or explicitly resolve celestial sun route:
   - `HectonCelestialEngine.sunVisualTransform` is still `{fileID: 0}`.
   - Candidate `SURFACE_LOW_SUN_DISC_1428` transform `1985271341` exists but is inactive/renderer-disabled.
6. Keep `HectonUnderwaterVisuals` static owner; it now has palette/material/sky assigned, but registration must happen before ready-lock or inside scene runtime publication gate.
7. Then run Batch24 slab/caustic isolation:
   - `H8_DEPTH_LOW_SHELF_1428`
   - `H8_WORLD_LOW_WATER_OCCLUSION_00/01/02/03_1428`
   - `H8_DEPTH_CEILING_OCCLUSION_1428`
   - `NOIR_UPPER_PRESSURE_LID`
   - `H8_FloorCausticSoft_1443`
8. Then verify material-side blockers:
   - `Ocean.mat` `_ClipSurface/_ClipUnderTerrain 1 -> 0` and removed clip keywords;
   - `Ocean_UnderwaterCurtain.mat` `_CAUSTICS_ON`, no `_CLIPUNDERTERRAIN_ON`, no `_TRANSPARENCY_ON`, `_CausticsStrength 10`;
   - `MAT_H8_SurfaceCrestOcean_1428` overdriven shallow/subsurface/sky/foam/caustic values.
9. Only after clean route and isolation produce the full visual packet:
   - surface/coast/Aegir;
   - shoreline close foam/wet contact;
   - underwater 0-5 m;
   - underwater 20-50 m route;
   - Aegir/celestial long;
   - low-oblique regression;
   - manifest with checksums/timestamps/camera/depth/quality/toggles/log path;
   - log tail newer than final screenshot and stable.

## Do Not Do

- Do not claim progress from diagnostics alone.
- Do not write screenshots under `Assets/Screenshots`.
- Do not add dark/green haze over visible slabs.
- Do not raw-enable `H8_UnderwaterHazeCurtain_1454`.
- Do not accept numeric foam/caustic material boosts without shoreline/underwater proof.
- Do not patch `HectonCelestialEngine` for the `WeatherEvents` leak; event queues are owned by `WeatherEvents`.

## Source Reports

- `Docs/Reports/Batch25/2501_WEATHEREVENTS_PERSISTENT_LEAK_AUDIT.md`
- `Docs/Reports/Batch25/2502_BOOTSTRAP_ROUTE_READINESS_HANG_AUDIT.md`
- `Docs/Reports/Batch25/2503_UNDERWATER_VISUALS_OWNER_PHASE_AUDIT.md`
- `Docs/Reports/Batch25/2504_CREST_OCEAN_CLIP_FOAM_CAUSTIC_RISK_AUDIT.md`
- `Docs/Reports/Batch25/2505_VISUAL_PROOF_WATCHDOG_GATE.md`
